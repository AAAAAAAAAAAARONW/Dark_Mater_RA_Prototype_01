using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

/// <summary>
/// The physical model behind the baked journey spectrum. EDITOR ONLY, PURE C#.
///
/// No UnityEngine anywhere in this file, so the whole model can be run and checked from
/// the command line (see README_SPECTRUM_DATA.md, "Checking a bake without Unity").
/// SpectrumBaker_NEW is the thin Unity wrapper around it.
///
/// WHAT IT COMPUTES. The spectrum of one quasar's light at every step of its journey to
/// Earth, on a fixed log-wavelength axis, as the light:
///
///   1. is emitted with a typical quasar spectrum
///        power-law continuum + broad emission lines (Tables/emission_lines.csv)
///   2. is stretched by the expansion of space
///        flat ΛCDM, Planck 2018 (Tables/cosmology.csv)
///   3. passes through intergalactic hydrogen, which absorbs at its local 1216 Å
///        (Lyman-alpha) and 1026 Å (Lyman-beta), with the average absorption at each z
///        matched to measurements (Tables/mean_flux.csv)
///   4. loses everything shorter than 912 Å locally to photoionisation, at a rate set by
///        the measured mean free path of ionising photons (Tables/mean_free_path.csv)
///   5. loses everything shorter than 228 Å locally to helium, until helium is fully
///      ionised at z ≈ 3 (bake_settings.csv)
///   6. is less absorbed close to the quasar, which ionises its surroundings
///        (the proximity zone, bake_settings.csv)
///
/// HOW. The light is followed step by step. On a log axis the stretch is a pure shift,
/// so the grid is sized so each step shifts it by exactly `cells_per_row` cells — an
/// integer move, no resampling, so nothing blurs however many steps are taken. Each step:
/// shift, imprint the newly crossed hydrogen at the fixed 1216 Å (and 1026 Å) cells,
/// apply photoionisation to everything below 912 Å. Then read out one row.
///
/// The hydrogen along the path is a correlated lognormal density field (the standard
/// "fluctuating Gunn–Peterson" approximation): optical depth τ = A(z)·ρ^α, with A(z)
/// solved so the mean transmission at every z equals the measured value. The individual
/// absorbers are therefore made up; their statistics are not.
///
/// See SOURCES.md for every number and its confidence, and OPEN_QUESTIONS.md for what
/// this model says about the narrative.
/// </summary>
public static class SpectrumModel_NEW
{
    const double CKms = 299792.458;

    /// <summary>Ionisation edge of singly ionised helium (He II), Å.</summary>
    const double HeIIEdgeA = 227.84;

    // =====================================================================================
    // Inputs
    // =====================================================================================

    public sealed class Settings
    {
        public double LambdaMin = 800.0;
        public double LambdaMax = 13000.0;
        public int Columns = 1024;
        public int Rows = 384;
        public int CellsPerRow = 4;
        public int Subsamples = 8;
        public double LookbackStartGyr = 13.0;
        public int Seed = 20260927;
        public double DensitySigma = 1.0;
        public double DensityCorrKms = 100.0;
        public double TauDensitySlope = 1.6;
        public double ContinuumAlphaLambda = -1.70;
        public double ContinuumPivotA = 1450.0;
        public double EuvAlphaLambda = -0.3;
        public double HeIIReionizationZ = 3.0;
        public double ProximityZonePMpc = 1.5;
        public double LymanBetaTauRatio = 0.1603;
        public double LymanContinuumSlope = 1.5;
        public int NotableCount = 12;
        public bool DisplayLyaOnly = true;
        public double NotableMinZ = 0.9;
        public int CalibrationDraws = 50000;
        public int ZTableSize = 256;
        public double H0 = 67.66;
        public double OmegaM = 0.30966;

        // The arrival frame: what the HUD zooms into at Earth. Emitted wavelengths, like
        // the classic forest figure (Q1422+2309, 1000–1350 Å).
        public double FinalEmittedMinA = 1000.0;
        public double FinalEmittedMaxA = 1350.0;
        public int FinalColumns = 16384;
    }

    public struct EmissionLine
    {
        public string Name;
        public double RestA;
        public double EwA;
        public double FwhmKms;
    }

    public struct Point
    {
        public double Z;
        public double Value;
    }

    public sealed class Inputs
    {
        public Settings Settings;
        public EmissionLine[] Lines;
        public Point[] MeanFlux;
        public Point[] MeanFreePath;
    }

    /// <summary>
    /// Read every table. `readTable` takes a file name (e.g. "bake_settings.csv") and
    /// returns its text — the Unity baker reads from Assets, the command-line check reads
    /// from disk, and both go through exactly this code.
    /// </summary>
    public static Inputs LoadInputs(Func<string, string> readTable)
    {
        CsvTable_NEW settings = new CsvTable_NEW("bake_settings.csv", readTable("bake_settings.csv"));
        CsvTable_NEW cosmology = new CsvTable_NEW("cosmology.csv", readTable("cosmology.csv"));
        CsvTable_NEW lines = new CsvTable_NEW("emission_lines.csv", readTable("emission_lines.csv"));
        CsvTable_NEW meanFlux = new CsvTable_NEW("mean_flux.csv", readTable("mean_flux.csv"));
        CsvTable_NEW mfp = new CsvTable_NEW("mean_free_path.csv", readTable("mean_free_path.csv"));

        Settings s = new Settings
        {
            LambdaMin = settings.Value("lambda_min_A"),
            LambdaMax = settings.Value("lambda_max_A"),
            Columns = (int)settings.Value("columns"),
            Rows = (int)settings.Value("rows"),
            CellsPerRow = (int)settings.Value("cells_per_row"),
            Subsamples = (int)settings.Value("subsamples_per_cell"),
            LookbackStartGyr = settings.Value("lookback_start_gyr"),
            Seed = (int)settings.Value("random_seed"),
            DensitySigma = settings.Value("density_sigma"),
            DensityCorrKms = settings.Value("density_correlation_kms"),
            TauDensitySlope = settings.Value("tau_density_slope"),
            ContinuumAlphaLambda = settings.Value("continuum_alpha_lambda"),
            ContinuumPivotA = settings.Value("continuum_pivot_A"),
            EuvAlphaLambda = settings.Value("euv_alpha_lambda"),
            HeIIReionizationZ = settings.Value("heii_reionization_z"),
            ProximityZonePMpc = settings.Value("proximity_zone_pmpc"),
            LymanBetaTauRatio = settings.Value("lyman_beta_tau_ratio"),
            LymanContinuumSlope = settings.Value("lyman_continuum_slope"),
            NotableCount = (int)settings.Value("notable_absorbers"),
            DisplayLyaOnly = settings.Value("display_lya_only") != 0.0,
            NotableMinZ = settings.Value("notable_min_z"),
            CalibrationDraws = (int)settings.Value("calibration_draws"),
            ZTableSize = (int)settings.Value("z_table_size"),
            H0 = cosmology.Value("H0"),
            OmegaM = cosmology.Value("Om0"),
            FinalEmittedMinA = settings.Value("final_emitted_min_A"),
            FinalEmittedMaxA = settings.Value("final_emitted_max_A"),
            FinalColumns = (int)settings.Value("final_columns"),
        };

        Validate(s);

        int cName = lines.Column("name");
        int cRest = lines.Column("rest_A");
        int cEw = lines.Column("rest_ew_A");
        int cFwhm = lines.Column("fwhm_kms");

        EmissionLine[] el = new EmissionLine[lines.Rows.Count];
        for (int i = 0; i < el.Length; i++)
        {
            el[i] = new EmissionLine
            {
                Name = lines.Text(i, cName),
                RestA = lines.Number(i, cRest),
                EwA = lines.Number(i, cEw),
                FwhmKms = lines.Number(i, cFwhm),
            };
        }

        return new Inputs
        {
            Settings = s,
            Lines = el,
            MeanFlux = ReadPoints(meanFlux, "z", "mean_flux", 0.0, 1.0),
            MeanFreePath = ReadPoints(mfp, "z", "mfp_pmpc", 1e-6, 1e6),
        };
    }

    static Point[] ReadPoints(CsvTable_NEW t, string zCol, string vCol, double min, double max)
    {
        int cz = t.Column(zCol);
        int cv = t.Column(vCol);

        Point[] p = new Point[t.Rows.Count];
        for (int i = 0; i < p.Length; i++)
        {
            p[i] = new Point { Z = t.Number(i, cz), Value = t.Number(i, cv) };

            if (p[i].Value < min || p[i].Value > max)
                throw new FormatException(t.Name + ": " + vCol + " = " + p[i].Value + " at z = " + p[i].Z + " is outside [" + min + ", " + max + "]");
            if (i > 0 && p[i].Z <= p[i - 1].Z)
                throw new FormatException(t.Name + ": z must increase down the table (row " + (i + 1) + ")");
        }

        if (p.Length < 2) throw new FormatException(t.Name + ": needs at least two rows");
        return p;
    }

    static void Validate(Settings s)
    {
        // 0 means "auto" (see ResolveAxis). Otherwise the range must be sane, and the birth
        // tick (1216 Å) must be on the bar, or no line is ever seen being born.
        if (s.LambdaMin < 0.0 || s.LambdaMax < 0.0) throw new FormatException("bake_settings: lambda_min_A / lambda_max_A must be positive, or 0 for auto");
        if (s.LambdaMin > 0.0 && s.LambdaMax > 0.0 && !(s.LambdaMax > s.LambdaMin)) throw new FormatException("bake_settings: lambda_max_A must be above lambda_min_A");
        if (s.LambdaMin > BakedSpectrumData_NEW.LymanAlphaA + 0.01) throw new FormatException("bake_settings: lambda_min_A must be at or below 1216 Å so the birth tick is on the bar (0 = exactly 1216)");
        if (!(s.FinalEmittedMaxA > s.FinalEmittedMinA) || s.FinalEmittedMinA <= 0.0) throw new FormatException("bake_settings: final_emitted_min_A / final_emitted_max_A are invalid");
        if (s.FinalColumns < 64 || s.FinalColumns > 65536) throw new FormatException("bake_settings: final_columns out of range");
        if (s.Columns < 16 || s.Columns > 8192) throw new FormatException("bake_settings: columns out of range");
        if (s.Rows < 16 || s.Rows > 8192) throw new FormatException("bake_settings: rows out of range");
        if (s.CellsPerRow < 1 || s.CellsPerRow > 64) throw new FormatException("bake_settings: cells_per_row out of range");
        if (s.Subsamples < 1 || s.Subsamples > 256) throw new FormatException("bake_settings: subsamples_per_cell out of range");
        if (s.LookbackStartGyr <= 0.5 || s.LookbackStartGyr >= 13.7) throw new FormatException("bake_settings: lookback_start_gyr must be between 0.5 and 13.7");
        if (s.NotableCount < 1) throw new FormatException("bake_settings: notable_absorbers must be at least 1");
        if (s.ZTableSize < 16) throw new FormatException("bake_settings: z_table_size must be at least 16");
    }

    // =====================================================================================
    // Output
    // =====================================================================================

    public sealed class Result
    {
        public Settings Settings;
        public double ZQuasar;
        public double ZFreeze;
        public float[] TableLookback;
        public float[] TableZ;
        public float[] AbsorberZ;
        public float[] AbsorberT;
        public float[] Flux;          // Rows × Columns, row-major, row 0 = emission
        public float[] Absorption;    // same layout, 1 − transmission
        public float[] EmissionPeakT; // per row: where the quasar's own Lyα peak sits, for the preview
        public int FreezeRow;

        // Arrival frame, at Earth, sampled finely on a log axis over [FinalLambdaMin, FinalLambdaMax]
        // (observed Å). It covers the whole journey bar AND the reference window, so the HUD can
        // zoom continuously from one to the other.
        public double FinalLambdaMin;
        public double FinalLambdaMax;
        public double FinalWindowMin;           // observed Å of the reference window's left edge
        public double FinalWindowMax;           // observed Å of its right edge
        public float[] FinalTransmissionLya;    // what the journey HUD shows, at Earth
        public float[] FinalFluxLya;            // quasar spectrum × the displayed absorption — continuous with the journey
        public float[] FinalFlux;               // the real spectrum, at Earth — the reference look
        public readonly List<string> Report = new List<string>();
    }

    // =====================================================================================
    // Bake
    // =====================================================================================

    public static Result Bake(Inputs input)
    {
        Settings s = input.Settings;
        Result res = new Result { Settings = s };
        List<string> rep = res.Report;

        // -- Cosmology ------------------------------------------------------------------

        Cosmology_NEW cosmo = new Cosmology_NEW(s.H0, s.OmegaM);

        double zQ = cosmo.ZFromLookback(s.LookbackStartGyr);
        double sQ = Math.Log(1.0 + zQ);
        double zFreeze = (1.0 + zQ) * BakedSpectrumData_NEW.LymanLimitA / BakedSpectrumData_NEW.LymanAlphaA - 1.0;

        res.ZQuasar = zQ;
        res.ZFreeze = zFreeze;

        // Axis "auto" (0). Left edge: the birth tick. Every Ly-alpha line is born at 1216 Å and
        // only moves right, so nothing the piece draws as absorption ever lives left of it.
        if (s.LambdaMin <= 0.0) s.LambdaMin = BakedSpectrumData_NEW.LymanAlphaA;
        // Right edge: the reference window's right edge at Earth (emitted 1350 Å). The HUD draws
        // the quasar's own spectrum, so the right side always has shape; at arrival the peak
        // lands at ~94% with its red side showing, and the arrival zoom only moves the left edge.
        if (s.LambdaMax <= 0.0) s.LambdaMax = s.FinalEmittedMaxA * (1.0 + zQ);
        if (!(s.LambdaMax > s.LambdaMin)) throw new FormatException("bake_settings: resolved lambda range is empty");

        res.FinalWindowMin = s.FinalEmittedMinA * (1.0 + zQ);
        res.FinalWindowMax = s.FinalEmittedMaxA * (1.0 + zQ);
        res.FinalLambdaMin = Math.Min(s.LambdaMin, res.FinalWindowMin);
        res.FinalLambdaMax = Math.Max(s.LambdaMax, res.FinalWindowMax);

        double age = cosmo.AgeGyr();
        double lbFreeze = cosmo.LookbackGyr(zFreeze);

        rep.Add("COSMOLOGY");
        rep.Add(F("  H0 = {0} km/s/Mpc, Omega_m = {1}, flat", s.H0, s.OmegaM));
        rep.Add(F("  Age of the universe today           {0:0.000} Gyr", age));
        rep.Add(F("  Journey starts                       {0:0.000} Gyr ago  ->  z_quasar = {1:0.000}", s.LookbackStartGyr, zQ));
        rep.Add(F("  Universe age at emission             {0:0.000} Gyr", age - s.LookbackStartGyr));
        rep.Add(F("  Total stretch at Earth               x{0:0.000}", 1.0 + zQ));
        rep.Add("");
        rep.Add("WHEN THE FOREST FREEZES");
        rep.Add(F("  z_freeze = {0:0.000}  (light at 1216 A here left the quasar at 912 A)", zFreeze));
        rep.Add(F("  reached {0:0.000} Gyr ago, {1:0.000} Gyr after emission", lbFreeze, s.LookbackStartGyr - lbFreeze));
        rep.Add(F("  = the first {0:0.0}% of the journey's light-travel time", 100.0 * (s.LookbackStartGyr - lbFreeze) / s.LookbackStartGyr));
        rep.Add(F("  light-travel distance quasar -> freeze: {0:0.0} proper Mpc", cosmo.LightTravelMpc(zQ, zFreeze)));
        rep.Add("");

        // -- Emission template --------------------------------------------------------

        Template template = new Template(s, input.Lines);
        double norm = template.PeakOver(s.LambdaMin, s.LambdaMax);

        // -- Calibration: A(z) such that <exp(-A rho^alpha)> = measured mean flux ---------

        Random rng = new Random(s.Seed);

        double[] p = new double[s.CalibrationDraws];
        for (int i = 0; i < p.Length; i++)
        {
            double g = Gaussian(rng);
            p[i] = Math.Exp(s.TauDensitySlope * (s.DensitySigma * g - 0.5 * s.DensitySigma * s.DensitySigma));
        }

        Point[] mf = input.MeanFlux;
        double[] lnA = new double[mf.Length];

        rep.Add("CALIBRATION  (mean transmission of the model vs the measured table)");
        rep.Add("      z    target    model");

        for (int i = 0; i < mf.Length; i++)
        {
            lnA[i] = SolveLnA(p, mf[i].Value);
            rep.Add(F("  {0,5:0.00}  {1,8:0.00000}  {2,8:0.00000}", mf[i].Z, mf[i].Value, MeanTransmission(p, Math.Exp(lnA[i]))));
        }

        rep.Add("");

        // -- Grid -------------------------------------------------------------------------
        //
        // -- Proximity zone ----------------------------------------------------------------
        //
        // Near the quasar its own light ionises the hydrogen, so τ is scaled down by
        // 1 / (1 + (R_ion / r)²) — the quasar's ionising flux against the background's.
        //
        // The table gives the zone size the way the literature MEASURES it: the distance
        // at which the transmission falls to 10%. R_ion is derived from that, so the
        // setting means what the papers mean. (At z ≈ 7 the two differ by roughly twenty
        // times, because the surrounding gas is so opaque that even a strongly reduced τ
        // is still large. Using one for the other would shrink the zone to a sliver.)

        double aAtQuasar = Math.Exp(Interp(mf, lnA, zQ));
        double aAtTenPercent = Math.Exp(SolveLnA(p, 0.1));
        double rIon = aAtQuasar > aAtTenPercent
            ? s.ProximityZonePMpc * Math.Sqrt(aAtQuasar / aAtTenPercent - 1.0)
            : 0.0;

        rep.Add("PROXIMITY ZONE");
        rep.Add(F("  measured size (transmission -> 10%): {0:0.00} proper Mpc", s.ProximityZonePMpc));
        rep.Add(F("  derived ionisation scale R_ion:      {0:0.00} proper Mpc", rIon));
        rep.Add("");

        // -- Grid -------------------------------------------------------------------------
        //
        // Cells of width delta in ln(lambda). One row step is a stretch of ds in ln, which
        // is exactly CellsPerRow cells, so every step is an integer shift.

        int R = s.Rows;
        int C = s.Columns;
        int k = s.CellsPerRow;

        double ds = sQ / (R - 1);
        double delta = ds / k;

        double lnLyA = Math.Log(BakedSpectrumData_NEW.LymanAlphaA);
        double lnLyB = Math.Log(BakedSpectrumData_NEW.LymanBetaA);
        double lnLL = Math.Log(BakedSpectrumData_NEW.LymanLimitA);
        double lnOutMin = Math.Log(s.LambdaMin);
        double lnOutMax = Math.Log(s.LambdaMax);

        // Extend left by the whole journey's stretch, so every photon that will ever be on
        // the bar is tracked from the moment of emission. Align so 1216 Å is a cell centre.
        // The grid spans both the journey bar and the arrival window, so the arrival frame can
        // be read out of the same simulation.
        double lnLow = Math.Log(res.FinalLambdaMin) - sQ - 2.0 * delta;
        double lnTop = Math.Log(res.FinalLambdaMax);
        int birthIdx = (int)Math.Ceiling((lnLyA - lnLow) / delta);
        double lnGrid0 = lnLyA - birthIdx * delta;
        int n = (int)Math.Ceiling((lnTop + 2.0 * delta - lnGrid0) / delta) + 1;
        int betaIdx = birthIdx + (int)Math.Round((lnLyB - lnLyA) / delta);

        // Max(0, …): for a low-redshift quasar the tracked range may not reach 912 Å at all.
        int llCount = Math.Max(0, (int)Math.Ceiling((lnLL - lnGrid0) / delta));
        int heCount = Math.Max(0, (int)Math.Ceiling((Math.Log(HeIIEdgeA) - lnGrid0) / delta));

        double[] T = new double[n];      // everything: Ly-alpha, Ly-beta, photoionisation, helium
        double[] Ta = new double[n];     // Ly-alpha only — what the display shows when DisplayLyaOnly
        for (int i = 0; i < n; i++) { T[i] = 1.0; Ta[i] = 1.0; }

        double[] lcShape = new double[llCount];
        for (int i = 0; i < llCount; i++)
            lcShape[i] = Math.Exp(s.LymanContinuumSlope * (lnGrid0 + i * delta - lnLL));

        double cellKms = delta * CKms;
        double colKms = (lnOutMax - lnOutMin) / (C - 1) * CKms;

        rep.Add("GRID");
        rep.Add(F("  {0} cells of {1:0} km/s, {2} per journey step; output {3} columns of {4:0} km/s x {5} rows", n, cellKms, k, C, colKms, R));
        rep.Add(F("  axis {0:0} - {1:0} A, log. 1216 A at {2:0.000} of the bar", s.LambdaMin, s.LambdaMax, (lnLyA - lnOutMin) / (lnOutMax - lnOutMin)));
        rep.Add("");

        // -- Density field along the path, correlated (AR(1)) --------------------------------

        double subKms = cellKms / s.Subsamples;
        double phi = Math.Exp(-subKms / Math.Max(1e-6, s.DensityCorrKms));
        double phiC = Math.Sqrt(1.0 - phi * phi);
        double g0 = Gaussian(rng);

        List<double> segZ = new List<double>((R - 1) * k);
        List<double> segT = new List<double>((R - 1) * k);

        res.Flux = new float[R * C];
        res.Absorption = new float[R * C];
        res.EmissionPeakT = new float[R];
        res.FreezeRow = -1;

        double halfCol = 0.5 * (lnOutMax - lnOutMin) / (C - 1);

        for (int r = 0; r < R; r++)
        {
            double sNow = sQ - r * ds;
            double zNow = Math.Exp(sNow) - 1.0;

            if (r > 0)
            {
                // 1 · Stretch: everything already imprinted moves k cells redward.
                Array.Copy(T, 0, T, k, n - k);
                for (int i = 0; i < k; i++) T[i] = T[k];
                Array.Copy(Ta, 0, Ta, k, n - k);
                for (int i = 0; i < k; i++) Ta[i] = Ta[k];

                // 2 · Hydrogen crossed during this step, in path order (highest z first).
                //     Cell birthIdx + j is light that was at 1216 Å locally j cells ago.
                for (int j = k - 1; j >= 0; j--)
                {
                    double zSeg = Math.Exp(sNow + (j + 0.5) * delta) - 1.0;

                    double rFromQ = cosmo.LightTravelMpc(zQ, zSeg);
                    double prox = rIon <= 0.0 ? 1.0
                                : rFromQ <= 0.0 ? 0.0
                                : 1.0 / (1.0 + Sq(rIon / rFromQ));

                    double A = Math.Exp(Interp(mf, lnA, zSeg)) * prox;

                    double sumA = 0.0, sumB = 0.0;
                    for (int m = 0; m < s.Subsamples; m++)
                    {
                        g0 = phi * g0 + phiC * Gaussian(rng);
                        double tau = A * Math.Exp(s.TauDensitySlope * (s.DensitySigma * g0 - 0.5 * s.DensitySigma * s.DensitySigma));
                        sumA += Math.Exp(-tau);
                        sumB += Math.Exp(-s.LymanBetaTauRatio * tau);
                    }

                    double tA = sumA / s.Subsamples;
                    double tB = sumB / s.Subsamples;

                    T[birthIdx + j] *= tA;
                    Ta[birthIdx + j] *= tA;
                    if (betaIdx + j >= 0) T[betaIdx + j] *= tB;

                    segZ.Add(zSeg);
                    segT.Add(tA);
                }

                // 3 · Photoionisation: everything below 912 Å locally, at the measured rate.
                double zPrev = Math.Exp(sNow + ds) - 1.0;
                double dl = cosmo.LightTravelMpc(zPrev, zNow);
                double mfp = Math.Exp(InterpLog(input.MeanFreePath, 0.5 * (zPrev + zNow)));
                double tau0 = dl / mfp;

                for (int i = 0; i < llCount; i++) T[i] *= Math.Exp(-tau0 * lcShape[i]);

                // 4 · Helium: until helium is fully ionised (z ≈ 3), singly ionised helium
                //     absorbs everything below its own ionisation edge, 228 Å locally.
                //     Treated as opaque — the helium equivalent of the Gunn–Peterson trough.
                if (zNow > s.HeIIReionizationZ)
                {
                    for (int i = 0; i < heCount; i++) T[i] = 0.0;
                }
            }

            if (res.FreezeRow < 0 && zNow <= zFreeze) res.FreezeRow = r;

            // 5 · Read out one row onto the display axis.
            double toRest = Math.Exp(sNow - sQ);   // (1+zNow)/(1+zQ): now-frame -> emitted wavelength
            int rowBase = r * C;

            for (int c = 0; c < C; c++)
            {
                double lnL = lnOutMin + c * (lnOutMax - lnOutMin) / (C - 1);
                double t = BoxAverage(T, lnGrid0, delta, lnL - halfCol, lnL + halfCol);
                double intr = template.At(Math.Exp(lnL) * toRest) / norm;

                // What the piece SHOWS as absorption. By design that is Ly-alpha only: the
                // lines born at 1216 Å and carried redward, nothing else. Photoionisation,
                // Ly-beta and helium are simulated (they shape the arrival frame's real
                // spectrum) but are not drawn during the journey.
                double shown = s.DisplayLyaOnly ? BoxAverage(Ta, lnGrid0, delta, lnL - halfCol, lnL + halfCol) : t;
                res.Absorption[rowBase + c] = (float)Clamp01(1.0 - shown);

                // Flux: the quasar's own spectrum — its Ly-alpha peak, emission lines and
                // continuum — with that same absorption cut into it. This is what the HUD
                // draws by default: the shape of the reference figure, the whole way.
                res.Flux[rowBase + c] = (float)Clamp01(intr * shown);
            }

            res.EmissionPeakT[r] = (float)((lnLyA + (sQ - sNow) - lnOutMin) / (lnOutMax - lnOutMin));
        }

        // -- Arrival frame -----------------------------------------------------------------
        //
        // The loop has just finished its last step, so T and Ta are the light as it arrives
        // at Earth. Read them out finely over a range that covers both the journey bar and
        // the reference window, so the HUD can zoom from one to the other on real data.

        int FC = s.FinalColumns;
        double lnFMin = Math.Log(res.FinalLambdaMin), lnFMax = Math.Log(res.FinalLambdaMax);
        double halfF = 0.5 * (lnFMax - lnFMin) / (FC - 1);
        double toRestAtEarth = Math.Exp(-sQ);

        res.FinalTransmissionLya = new float[FC];
        res.FinalFluxLya = new float[FC];
        res.FinalFlux = new float[FC];

        for (int c = 0; c < FC; c++)
        {
            double lnL = lnFMin + c * (lnFMax - lnFMin) / (FC - 1);
            double t = BoxAverage(T, lnGrid0, delta, lnL - halfF, lnL + halfF);
            double ta = BoxAverage(Ta, lnGrid0, delta, lnL - halfF, lnL + halfF);
            double intr = template.At(Math.Exp(lnL) * toRestAtEarth) / norm;
            double shown = s.DisplayLyaOnly ? ta : t;

            res.FinalTransmissionLya[c] = (float)Clamp01(shown);
            res.FinalFluxLya[c] = (float)Clamp01(intr * shown);   // continuous with the journey HUD
            res.FinalFlux[c] = (float)Clamp01(intr * t);          // full physics: the reference, exactly
        }

        rep.Add("ARRIVAL FRAME  (what the HUD zooms into at Earth)");
        rep.Add(F("  reference window: emitted {0:0}-{1:0} A = observed {2:0}-{3:0} A", s.FinalEmittedMinA, s.FinalEmittedMaxA, res.FinalWindowMin, res.FinalWindowMax));
        rep.Add(F("  sampled {0} columns over {1:0}-{2:0} A observed ({3:0} km/s each)", FC, res.FinalLambdaMin, res.FinalLambdaMax, 2.0 * halfF * CKms));
        double forest = MeanFinal(res, 1050.0 * (1.0 + zQ), 1180.0 * (1.0 + zQ));
        double red = MeanFinal(res, 1270.0 * (1.0 + zQ), 1350.0 * (1.0 + zQ));
        rep.Add(F("  forest level (emitted 1050-1180 A) / red continuum (1270-1350 A) = {0:0.000}", red > 0.0 ? forest / red : double.NaN));
        rep.Add("  (for comparison: Q1422+2309 at z = 3.62, the reference, reads ~0.7 on this measure)");
        rep.Add("");

        // -- Notable absorbers --------------------------------------------------------------

        SelectAbsorbers(s, segZ, segT, zQ, zFreeze, res);

        rep.Add("NOTABLE ABSORBERS  (for RedshiftMarks_NEW; [0] is the first line)");
        for (int i = 0; i < res.AbsorberZ.Length; i++)
            rep.Add(F("  [{0,2}]  z = {1:0.0000}   transmission {2:0.000}{3}", i, res.AbsorberZ[i], res.AbsorberT[i], i == 0 ? "   <- first" : ""));
        rep.Add("");

        // -- Path check: what the bake actually imprinted, binned by z ------------------------

        rep.Add("PATH CHECK  (mean transmission actually imprinted, outside the proximity zone)");
        rep.Add("      z    target    baked    segments");
        for (int i = 0; i < mf.Length; i++)
        {
            double lo = mf[i].Z - 0.1, hi = mf[i].Z + 0.1, sum = 0.0;
            int count = 0;
            for (int q = 0; q < segZ.Count; q++)
            {
                if (segZ[q] < lo || segZ[q] >= hi) continue;
                if (cosmo.LightTravelMpc(zQ, segZ[q]) < 4.0 * s.ProximityZonePMpc) continue;
                sum += segT[q];
                count++;
            }

            if (count > 0) rep.Add(F("  {0,5:0.00}  {1,8:0.00000}  {2,8:0.00000}   {3}", mf[i].Z, mf[i].Value, sum / count, count));
        }
        rep.Add("  (few segments = noisy. The journey spends few steps at low z, where it does not matter:");
        rep.Add("   that light was already absorbed below 912 A. See OPEN_QUESTIONS.md.)");
        rep.Add("");

        // -- Arrival --------------------------------------------------------------------------

        int last = (R - 1) * C;
        double obsLL = BakedSpectrumData_NEW.LymanLimitA * (1.0 + zQ);
        double obsLyA = BakedSpectrumData_NEW.LymanAlphaA * (1.0 + zQ);
        rep.Add("ARRIVAL AT EARTH  (last row)");
        string band = obsLyA < 4000.0 ? "ultraviolet" : obsLyA <= 7000.0 ? "VISIBLE" : "infrared";
        rep.Add(F("  quasar Ly-alpha peak observed at {0:0} A ({1}); Lyman limit at {2:0} A", obsLyA, band, obsLL));
        rep.Add(F("  DISPLAYED ({0}):", s.DisplayLyaOnly ? "Ly-alpha absorption only" : "all absorption"));
        rep.Add(F("    mean transmission {0:0} - {1:0} A (older lines, z_abs > {2:0.00}):  {3:0.0000}", obsLL, obsLyA, zFreeze, MeanOver(res.Absorption, last, C, s, obsLL, obsLyA, true)));
        rep.Add(F("    mean transmission {0:0} - {1:0} A (younger lines):             {2:0.0000}", BakedSpectrumData_NEW.LymanAlphaA, obsLL, MeanOver(res.Absorption, last, C, s, BakedSpectrumData_NEW.LymanAlphaA, obsLL, true)));
        rep.Add(F("    mean transmission below {0:0} A (no line reaches here):      {1:0.0000}", BakedSpectrumData_NEW.LymanAlphaA, MeanOver(res.Absorption, last, C, s, s.LambdaMin, BakedSpectrumData_NEW.LymanAlphaA, true)));
        if (s.DisplayLyaOnly)
        {
            rep.Add("  NOT DISPLAYED: photoionisation below 912 A, Ly-beta and helium. They are in the");
            rep.Add("  flux grid (HudCurve.Flux) but the piece does not draw them. See OPEN_QUESTIONS.md.");
        }
        rep.Add("");

        // -- z(lookback) table for the runtime ----------------------------------------------

        int M = s.ZTableSize;
        res.TableLookback = new float[M];
        res.TableZ = new float[M];
        for (int i = 0; i < M; i++)
        {
            double z = Math.Exp(sQ * i / (M - 1)) - 1.0;
            res.TableZ[i] = (float)z;
            res.TableLookback[i] = (float)cosmo.LookbackGyr(z);
        }

        return res;
    }

    // =====================================================================================
    // Writing the baked file
    // =====================================================================================

    /// <summary>Serialise a result in the format BakedSpectrumData_NEW.Load reads.</summary>
    public static byte[] Write(Result r)
    {
        Settings s = r.Settings;

        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter w = new BinaryWriter(ms))
        {
            w.Write(BakedSpectrumData_NEW.Magic);
            w.Write(BakedSpectrumData_NEW.FormatVersion);
            w.Write(s.Columns);
            w.Write(s.Rows);
            w.Write((float)s.LambdaMin);
            w.Write((float)s.LambdaMax);
            w.Write((float)r.ZQuasar);
            w.Write((float)r.ZFreeze);
            w.Write((float)s.LookbackStartGyr);

            w.Write(r.TableZ.Length);
            for (int i = 0; i < r.TableZ.Length; i++)
            {
                w.Write(r.TableLookback[i]);
                w.Write(r.TableZ[i]);
            }

            w.Write(r.AbsorberZ.Length);
            for (int i = 0; i < r.AbsorberZ.Length; i++)
            {
                w.Write(r.AbsorberZ[i]);
                w.Write(r.AbsorberT[i]);
            }

            for (int i = 0; i < r.Flux.Length; i++) w.Write(ToU16(r.Flux[i]));
            for (int i = 0; i < r.Absorption.Length; i++) w.Write(ToU16(r.Absorption[i]));

            // Version 2: the arrival frame.
            w.Write(r.FinalFlux.Length);
            w.Write((float)r.FinalLambdaMin);
            w.Write((float)r.FinalLambdaMax);
            w.Write((float)r.FinalWindowMin);
            w.Write((float)r.FinalWindowMax);
            for (int i = 0; i < r.FinalTransmissionLya.Length; i++) w.Write(ToU16(r.FinalTransmissionLya[i]));
            for (int i = 0; i < r.FinalFlux.Length; i++) w.Write(ToU16(r.FinalFlux[i]));

            // Version 3: the arrival frame's displayed spectrum (quasar × displayed absorption).
            for (int i = 0; i < r.FinalFluxLya.Length; i++) w.Write(ToU16(r.FinalFluxLya[i]));

            w.Flush();
            return ms.ToArray();
        }
    }

    static ushort ToU16(float v)
    {
        if (v <= 0f) return 0;
        if (v >= 1f) return 65535;
        return (ushort)Math.Round(v * 65535.0);
    }

    // =====================================================================================
    // Pieces
    // =====================================================================================

    /// <summary>Power-law continuum plus Gaussian emission lines, in the quasar's rest frame.</summary>
    sealed class Template
    {
        readonly double _alpha;
        readonly double _euvAlpha;
        readonly double _pivot;
        readonly double _at912;
        readonly double[] _centre, _sigma, _amp;

        public Template(Settings s, EmissionLine[] lines)
        {
            _alpha = s.ContinuumAlphaLambda;
            _euvAlpha = s.EuvAlphaLambda;
            _pivot = s.ContinuumPivotA;
            _at912 = Math.Pow(BakedSpectrumData_NEW.LymanLimitA / _pivot, _alpha);

            _centre = new double[lines.Length];
            _sigma = new double[lines.Length];
            _amp = new double[lines.Length];

            for (int i = 0; i < lines.Length; i++)
            {
                _centre[i] = lines[i].RestA;
                _sigma[i] = lines[i].RestA * (lines[i].FwhmKms / CKms) / 2.3548200450309493;

                // Equivalent width → integrated line flux → Gaussian peak.
                double flux = lines[i].EwA * Continuum(lines[i].RestA);
                _amp[i] = flux / (_sigma[i] * Math.Sqrt(2.0 * Math.PI));
            }
        }

        /// <summary>
        /// Continuum. Above 912 Å the near-UV/optical slope; below it the much flatter
        /// extreme-UV slope, joined continuously. Extending the near-UV law down to 100 Å
        /// would make the quasar ~90× brighter there than at 1450 Å — which no quasar is,
        /// and which showed up in the first bake as light arriving where real spectra
        /// have none.
        /// </summary>
        double Continuum(double lambda)
        {
            if (lambda >= BakedSpectrumData_NEW.LymanLimitA) return Math.Pow(lambda / _pivot, _alpha);
            return _at912 * Math.Pow(lambda / BakedSpectrumData_NEW.LymanLimitA, _euvAlpha);
        }

        public double At(double lambdaRest)
        {
            if (lambdaRest <= 0.0) return 0.0;

            double v = Continuum(lambdaRest);
            for (int i = 0; i < _centre.Length; i++)
            {
                double x = (lambdaRest - _centre[i]) / _sigma[i];
                if (x > -8.0 && x < 8.0) v += _amp[i] * Math.Exp(-0.5 * x * x);
            }

            return v;
        }

        /// <summary>Brightest point in a range — the normalisation, so the Ly-alpha peak is 1.</summary>
        public double PeakOver(double lo, double hi)
        {
            double best = 0.0;
            const int N = 16384;
            double a = Math.Log(lo), b = Math.Log(hi);
            for (int i = 0; i < N; i++)
            {
                double v = At(Math.Exp(a + (b - a) * i / (N - 1)));
                if (v > best) best = v;
            }

            return best > 0.0 ? best : 1.0;
        }
    }

    static void SelectAbsorbers(Settings s, List<double> segZ, List<double> segT, double zQ, double zFreeze, Result res)
    {
        List<float> z = new List<float>();
        List<float> t = new List<float>();

        // [0] — the first line the light acquired: the first step, leaving the quasar, where
        // at least half the light at 1216 Å was taken.
        int first = -1;
        for (int i = 0; i < segZ.Count; i++)
        {
            if (segT[i] < 0.5) { first = i; break; }
        }
        if (first < 0 && segZ.Count > 0) first = 0;

        if (first >= 0)
        {
            z.Add((float)segZ[first]);
            t.Add((float)segT[first]);
        }

        // The rest — the strongest absorber in each of (NotableCount − 1) bins between the
        // first line and a lower limit. Bins are equal in ln(1+z), i.e. equal in stretch,
        // so the marks come out evenly spaced along the bar rather than bunched at one end.
        //
        // The lower limit depends on what is displayed. Ly-alpha only: every line is drawn
        // however late it was cut, so go down to notable_min_z (set just above the second
        // cosmic web, so every mark exists by the time the demo points at them). Full
        // physics: stop at the freeze — below it, lines are cut into light that is gone.
        int bins = Math.Max(0, s.NotableCount - 1);
        double top = first >= 0 ? segZ[first] : zQ;
        double bottom = s.DisplayLyaOnly ? s.NotableMinZ : Math.Max(zFreeze, s.NotableMinZ);
        double lnTop = Math.Log(1.0 + top), lnBottom = Math.Log(1.0 + bottom);

        for (int b = 0; b < bins; b++)
        {
            double hi = Math.Exp(lnTop - (lnTop - lnBottom) * b / bins) - 1.0;
            double lo = Math.Exp(lnTop - (lnTop - lnBottom) * (b + 1) / bins) - 1.0;

            int best = -1;
            for (int i = 0; i < segZ.Count; i++)
            {
                if (i == first) continue;
                if (segZ[i] >= hi || segZ[i] < lo) continue;
                if (best < 0 || segT[i] < segT[best]) best = i;
            }

            if (best < 0) continue;
            z.Add((float)segZ[best]);
            t.Add((float)segT[best]);
        }

        res.AbsorberZ = z.ToArray();
        res.AbsorberT = t.ToArray();
    }

    /// <summary>Solve ln A so that the mean of exp(−A p) is the target. Bisection; the mean falls monotonically with A.</summary>
    static double SolveLnA(double[] p, double target)
    {
        if (target >= 0.99999) return -40.0;

        double lo = -40.0, hi = 60.0;
        for (int it = 0; it < 80; it++)
        {
            double mid = 0.5 * (lo + hi);
            if (MeanTransmission(p, Math.Exp(mid)) > target) lo = mid; else hi = mid;
        }

        return 0.5 * (lo + hi);
    }

    static double MeanTransmission(double[] p, double A)
    {
        double sum = 0.0;
        for (int i = 0; i < p.Length; i++) sum += Math.Exp(-A * p[i]);
        return sum / p.Length;
    }

    /// <summary>Linear interpolation of values[] against table z, clamped at the ends.</summary>
    static double Interp(Point[] table, double[] values, double z)
    {
        int n = table.Length;
        if (z <= table[0].Z) return values[0];
        if (z >= table[n - 1].Z) return values[n - 1];

        for (int i = 1; i < n; i++)
        {
            if (z > table[i].Z) continue;
            double k = (z - table[i - 1].Z) / (table[i].Z - table[i - 1].Z);
            return values[i - 1] + (values[i] - values[i - 1]) * k;
        }

        return values[n - 1];
    }

    /// <summary>ln of a table's values, interpolated linearly in z. Used for the mean free path, which spans decades.</summary>
    static double InterpLog(Point[] table, double z)
    {
        double[] ln = new double[table.Length];
        for (int i = 0; i < ln.Length; i++) ln[i] = Math.Log(table[i].Value);
        return Interp(table, ln, z);
    }

    static double BoxAverage(double[] T, double lnGrid0, double delta, double lo, double hi)
    {
        int a = (int)Math.Ceiling((lo - lnGrid0) / delta);
        int b = (int)Math.Floor((hi - lnGrid0) / delta);

        if (a < 0) a = 0;
        if (b >= T.Length) b = T.Length - 1;

        if (b < a)
        {
            int nearest = (int)Math.Round((0.5 * (lo + hi) - lnGrid0) / delta);
            if (nearest < 0) nearest = 0;
            if (nearest >= T.Length) nearest = T.Length - 1;
            return T[nearest];
        }

        double sum = 0.0;
        for (int i = a; i <= b; i++) sum += T[i];
        return sum / (b - a + 1);
    }

    static double MeanOver(float[] absorption, int rowBase, int C, Settings s, double lamLo, double lamHi, bool asTransmission)
    {
        double lnMin = Math.Log(s.LambdaMin), lnMax = Math.Log(s.LambdaMax);
        double sum = 0.0;
        int count = 0;

        for (int c = 0; c < C; c++)
        {
            double lam = Math.Exp(lnMin + c * (lnMax - lnMin) / (C - 1));
            if (lam < lamLo || lam >= lamHi) continue;
            double a = absorption[rowBase + c];
            sum += asTransmission ? 1.0 - a : a;
            count++;
        }

        return count > 0 ? sum / count : double.NaN;
    }

    static double MeanFinal(Result r, double lamLo, double lamHi)
    {
        int FC = r.FinalFlux.Length;
        double lnMin = Math.Log(r.FinalLambdaMin), lnMax = Math.Log(r.FinalLambdaMax);
        double sum = 0.0;
        int count = 0;

        for (int c = 0; c < FC; c++)
        {
            double lam = Math.Exp(lnMin + c * (lnMax - lnMin) / (FC - 1));
            if (lam < lamLo || lam >= lamHi) continue;
            sum += r.FinalFlux[c];
            count++;
        }

        return count > 0 ? sum / count : double.NaN;
    }

    static double Gaussian(Random rng)
    {
        // Box–Muller. 1 − NextDouble() keeps the log argument away from zero.
        double u1 = 1.0 - rng.NextDouble();
        double u2 = rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    static double Sq(double x) { return x * x; }

    static double Clamp01(double v) { return v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v); }

    static string F(string format, params object[] args)
    {
        return string.Format(CultureInfo.InvariantCulture, format, args);
    }
}

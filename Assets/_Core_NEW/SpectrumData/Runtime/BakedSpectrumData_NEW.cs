using System;
using System.IO;

/// <summary>
/// The baked journey spectrum, loaded once and sampled by row.
///
/// WHAT IS IN THE FILE. The spectrum of the quasar's light at every point of the
/// journey, precomputed by SpectrumBaker_NEW (Editor only) and stored as a grid:
///
///     columns  wavelength, on a LOG axis from LambdaMin to LambdaMax
///     rows     how far the light has travelled, uniform in ln(1+z) — row 0 is the
///              moment of emission, the last row is arrival at Earth
///
/// Two values per cell: the flux the HUD draws, and the absorption the trail draws.
/// Plus a small table to turn "light-years still to travel" into z, and a short list
/// of notable absorbers for RedshiftMarks_NEW to point at.
///
/// WHY A LOG AXIS. Redshift multiplies every wavelength by the same factor. On a log
/// axis a multiplication is a shift, so the whole spectrum slides as one piece and every
/// line keeps its shape — which is also why the fake version's "add an offset" drift
/// was the right maths on the wrong axis.
///
/// WHY PURE C#. No UnityEngine in this file, on purpose: the baker's command-line test
/// harness reads files through this exact class, so a round trip through the format is
/// testable without opening Unity.
///
/// COST. Load: one pass over ~1.5 MB, once, at startup. Per frame: SampleRow is two
/// rows of `Columns` ushorts blended into caller-owned arrays — about 2,000 multiply-adds,
/// no allocation. That is deliberate. The previous spectrum-data path ran a reader in the
/// background every frame and was a known frame-rate problem; nothing here may do I/O,
/// parsing or allocation after Load returns.
/// </summary>
public sealed class BakedSpectrumData_NEW
{
    // -- Physical constants shared with the baker ------------------------------

    /// <summary>Hydrogen Lyman-alpha, rest frame, Ångström. Where every forest line is born.</summary>
    public const double LymanAlphaA = 1215.67;

    /// <summary>Hydrogen Lyman-beta, rest frame, Ångström.</summary>
    public const double LymanBetaA = 1025.72;

    /// <summary>Hydrogen ionisation edge (Lyman limit), Ångström. Shorter than this, light ionises hydrogen and is absorbed.</summary>
    public const double LymanLimitA = 911.75;

    // -- Format ------------------------------------------------------------------

    /// <summary>"LAFJ", little-endian. Lyman-Alpha Forest, Journey.</summary>
    public const uint Magic = 0x4A46414C;

    /// <summary>
    /// Bump when the layout changes. A mismatch refuses to load rather than misreading.
    /// 2 — adds the arrival frame (the zoom into the reference forest figure at Earth).
    /// 3 — adds the arrival frame's displayed spectrum (quasar × displayed absorption).
    /// </summary>
    public const int FormatVersion = 3;

    // -- Header ------------------------------------------------------------------

    public int Columns { get; private set; }
    public int Rows { get; private set; }

    /// <summary>Wavelength at the left edge of the bar, Ångström.</summary>
    public float LambdaMin { get; private set; }

    /// <summary>Wavelength at the right edge of the bar, Ångström.</summary>
    public float LambdaMax { get; private set; }

    /// <summary>The quasar's redshift. Row 0 is the light at emission, at this z.</summary>
    public float ZQuasar { get; private set; }

    /// <summary>
    /// Below this z, no new Lyman-alpha line can appear in this light.
    ///
    /// Light that is at 1216 Å here was shorter than 912 Å when it left the quasar, and
    /// light that short was absorbed near the start of the journey. So after this point
    /// the forest is finished; it only travels. See OPEN_QUESTIONS.md.
    /// </summary>
    public float ZFreeze { get; private set; }

    /// <summary>Light-travel time of the whole journey, Gyr. Must match the tracker's starting distance.</summary>
    public float LookbackStartGyr { get; private set; }

    /// <summary>Notable absorber redshifts, highest first. [0] is the first line the light acquired.</summary>
    public float[] AbsorberZ { get; private set; }

    /// <summary>Transmission of each notable absorber's segment at the moment it was cut, 0..1.</summary>
    public float[] AbsorberTransmission { get; private set; }

    // -- Arrival frame -----------------------------------------------------------
    //
    // The light as it reaches Earth, sampled finely on a log axis wide enough to cover both
    // the journey bar and the reference window — so the HUD can zoom from one to the other
    // on real data rather than on an upscaled row.

    /// <summary>Observed wavelength at the first arrival-frame sample, Å.</summary>
    public float FinalLambdaMin { get; private set; }

    /// <summary>Observed wavelength at the last arrival-frame sample, Å.</summary>
    public float FinalLambdaMax { get; private set; }

    /// <summary>Left edge of the reference window (emitted 1000 Å), in observed Å.</summary>
    public float FinalWindowMin { get; private set; }

    /// <summary>Right edge of the reference window (emitted 1350 Å), in observed Å.</summary>
    public float FinalWindowMax { get; private set; }

    ushort[] _finalLya;
    ushort[] _finalFlux;
    ushort[] _finalFluxLya;
    double _finalLnMin;
    double _finalLnWidth;

    float[] _tableLookback;   // ascending
    float[] _tableZ;          // matching z, ascending
    ushort[] _flux;
    ushort[] _absorption;
    double _lnMin;
    double _lnWidth;
    double _sQuasar;

    BakedSpectrumData_NEW() { }

    // -- Load --------------------------------------------------------------------

    /// <summary>
    /// Parse a baked file. Returns null and a human-readable reason on any problem —
    /// wrong magic, wrong version, truncated, inconsistent counts. Never throws for a bad
    /// file; a bad file must switch the feature off, not take the scene down with it.
    /// </summary>
    public static BakedSpectrumData_NEW Load(byte[] bytes, out string error)
    {
        error = null;

        if (bytes == null || bytes.Length < 64)
        {
            error = "file is missing or too short to contain a header";
            return null;
        }

        if (!BitConverter.IsLittleEndian)
        {
            error = "this platform is big-endian; the baked format is little-endian";
            return null;
        }

        try
        {
            using (MemoryStream ms = new MemoryStream(bytes, false))
            using (BinaryReader r = new BinaryReader(ms))
            {
                if (r.ReadUInt32() != Magic)
                {
                    error = "not a baked journey spectrum (wrong magic)";
                    return null;
                }

                int version = r.ReadInt32();
                if (version != FormatVersion)
                {
                    error = "format version " + version + ", this build reads " + FormatVersion + " — re-bake";
                    return null;
                }

                BakedSpectrumData_NEW d = new BakedSpectrumData_NEW();

                d.Columns = r.ReadInt32();
                d.Rows = r.ReadInt32();
                d.LambdaMin = r.ReadSingle();
                d.LambdaMax = r.ReadSingle();
                d.ZQuasar = r.ReadSingle();
                d.ZFreeze = r.ReadSingle();
                d.LookbackStartGyr = r.ReadSingle();

                if (d.Columns < 2 || d.Rows < 2 || d.Columns > 16384 || d.Rows > 16384 ||
                    !(d.LambdaMax > d.LambdaMin) || d.LambdaMin <= 0f || !(d.ZQuasar > 0f))
                {
                    error = "header values are out of range";
                    return null;
                }

                int tableCount = r.ReadInt32();
                if (tableCount < 2 || tableCount > 65536)
                {
                    error = "z table size " + tableCount + " is out of range";
                    return null;
                }

                d._tableLookback = new float[tableCount];
                d._tableZ = new float[tableCount];
                for (int i = 0; i < tableCount; i++)
                {
                    d._tableLookback[i] = r.ReadSingle();
                    d._tableZ[i] = r.ReadSingle();
                }

                int absorberCount = r.ReadInt32();
                if (absorberCount < 0 || absorberCount > 4096)
                {
                    error = "absorber count " + absorberCount + " is out of range";
                    return null;
                }

                d.AbsorberZ = new float[absorberCount];
                d.AbsorberTransmission = new float[absorberCount];
                for (int i = 0; i < absorberCount; i++)
                {
                    d.AbsorberZ[i] = r.ReadSingle();
                    d.AbsorberTransmission[i] = r.ReadSingle();
                }

                int cells = d.Columns * d.Rows;
                long gridBytes = (long)cells * 2 * sizeof(ushort);
                long offset = ms.Position;
                const int FinalHeaderBytes = sizeof(int) + 4 * sizeof(float);

                if (bytes.Length - offset < gridBytes + FinalHeaderBytes)
                {
                    error = "expected at least " + (gridBytes + FinalHeaderBytes) + " bytes after the header, found " +
                            (bytes.Length - offset) + " — the file is truncated";
                    return null;
                }

                d._flux = new ushort[cells];
                d._absorption = new ushort[cells];
                Buffer.BlockCopy(bytes, (int)offset, d._flux, 0, cells * sizeof(ushort));
                Buffer.BlockCopy(bytes, (int)offset + cells * sizeof(ushort), d._absorption, 0, cells * sizeof(ushort));

                ms.Position = offset + gridBytes;

                int finalColumns = r.ReadInt32();
                d.FinalLambdaMin = r.ReadSingle();
                d.FinalLambdaMax = r.ReadSingle();
                d.FinalWindowMin = r.ReadSingle();
                d.FinalWindowMax = r.ReadSingle();

                if (finalColumns < 2 || finalColumns > 65536 || !(d.FinalLambdaMax > d.FinalLambdaMin) ||
                    d.FinalLambdaMin <= 0f || !(d.FinalWindowMax > d.FinalWindowMin))
                {
                    error = "arrival-frame header values are out of range";
                    return null;
                }

                long finalBytes = (long)finalColumns * 3 * sizeof(ushort);
                long finalOffset = ms.Position;
                if (bytes.Length - finalOffset != finalBytes)
                {
                    error = "expected " + finalBytes + " bytes of arrival-frame data, found " +
                            (bytes.Length - finalOffset) + " — the file is truncated or from another version";
                    return null;
                }

                d._finalLya = new ushort[finalColumns];
                d._finalFlux = new ushort[finalColumns];
                Buffer.BlockCopy(bytes, (int)finalOffset, d._finalLya, 0, finalColumns * sizeof(ushort));
                Buffer.BlockCopy(bytes, (int)finalOffset + finalColumns * sizeof(ushort), d._finalFlux, 0, finalColumns * sizeof(ushort));
                d._finalFluxLya = new ushort[finalColumns];
                Buffer.BlockCopy(bytes, (int)finalOffset + 2 * finalColumns * sizeof(ushort), d._finalFluxLya, 0, finalColumns * sizeof(ushort));
                d._finalLnMin = Math.Log(d.FinalLambdaMin);
                d._finalLnWidth = Math.Log(d.FinalLambdaMax / (double)d.FinalLambdaMin);

                d._lnMin = Math.Log(d.LambdaMin);
                d._lnWidth = Math.Log(d.LambdaMax / (double)d.LambdaMin);
                d._sQuasar = Math.Log(1.0 + d.ZQuasar);

                return d;
            }
        }
        catch (EndOfStreamException)
        {
            error = "file ends inside the header";
            return null;
        }
    }

    // -- Axis --------------------------------------------------------------------

    /// <summary>Where a wavelength (Å) sits across the bar, 0 = left edge, 1 = right edge. Not clamped.</summary>
    public float ToT(double lambdaA)
    {
        return (float)((Math.Log(lambdaA) - _lnMin) / _lnWidth);
    }

    /// <summary>The wavelength at a position across the bar.</summary>
    public double ToLambda(float t)
    {
        return Math.Exp(_lnMin + t * _lnWidth);
    }

    /// <summary>Where every Lyman-alpha line is born: 1216 Å. Fixed for the whole journey.</summary>
    public float BirthT { get { return ToT(LymanAlphaA); } }

    /// <summary>
    /// Where a line cut by a cloud at zAbs sits now, when the light has reached zNow.
    /// It was cut at 1216 Å locally and has been stretched by (1+zAbs)/(1+zNow) since.
    /// </summary>
    public float LineT(float zAbs, float zNow)
    {
        return ToT(LymanAlphaA * (1.0 + zAbs) / (1.0 + zNow));
    }

    /// <summary>How much the light has been stretched so far: 1 at emission, 1+z_quasar at Earth.</summary>
    public float Stretch(float zNow)
    {
        return (float)((1.0 + ZQuasar) / (1.0 + zNow));
    }

    // -- Journey position ----------------------------------------------------------

    /// <summary>
    /// The redshift at which the light is, given how much light-travel time is left.
    ///
    /// UniverseJourneyTracker counts remaining distance in light-years, and a light-year
    /// of light-travel distance is a year of lookback time — so remaining ly / 1e9 is the
    /// lookback in Gyr, and that maps to z through the cosmology baked into this table.
    /// </summary>
    public float ZFromLookback(float lookbackGyr)
    {
        float[] lb = _tableLookback;
        int n = lb.Length;

        if (lookbackGyr <= lb[0]) return _tableZ[0];
        if (lookbackGyr >= lb[n - 1]) return _tableZ[n - 1];

        int lo = 0, hi = n - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) >> 1;
            if (lb[mid] <= lookbackGyr) lo = mid; else hi = mid;
        }

        float k = (lookbackGyr - lb[lo]) / (lb[hi] - lb[lo]);
        return _tableZ[lo] + (_tableZ[hi] - _tableZ[lo]) * k;
    }

    /// <summary>Fractional row for a redshift. 0 at the quasar, Rows-1 at z = 0. Clamped.</summary>
    public float RowFromZ(float z)
    {
        double s = Math.Log(1.0 + Math.Max(0.0, z));
        double f = (_sQuasar - s) / _sQuasar;
        if (f < 0.0) f = 0.0;
        if (f > 1.0) f = 1.0;
        return (float)(f * (Rows - 1));
    }

    // -- Arrival frame sampling ------------------------------------------------------

    /// <summary>Which arrival-frame curve to sample.</summary>
    public enum FinalChannel
    {
        /// <summary>The displayed absorption only (Ly-alpha), as transmission 0..1.</summary>
        Transmission = 0,

        /// <summary>The quasar's spectrum × the displayed absorption — what the journey HUD draws. Continuous with it.</summary>
        DisplayedSpectrum = 1,

        /// <summary>The real spectrum, all absorption included — the reference figure exactly.</summary>
        RealSpectrum = 2
    }

    /// <summary>
    /// The arrival frame at one observed wavelength, linearly interpolated. Spectra are
    /// normalised so the quasar's Ly-alpha peak is ~1. Outside the sampled range,
    /// transmission reads 1 and spectra read 0.
    /// </summary>
    public float SampleFinal(double lambdaA, FinalChannel channel)
    {
        ushort[] src = channel == FinalChannel.Transmission ? _finalLya
                     : channel == FinalChannel.DisplayedSpectrum ? _finalFluxLya
                     : _finalFlux;
        float outside = channel == FinalChannel.Transmission ? 1f : 0f;
        if (src == null || lambdaA <= 0.0) return outside;

        double f = (Math.Log(lambdaA) - _finalLnMin) / _finalLnWidth * (src.Length - 1);

        // Outside by more than a sample: genuinely off the frame. Within one sample: that is
        // float rounding of the stored range, and the edge value is the right answer — the
        // bar's end column lands exactly on the range's end and must not read as empty.
        if (f < -1.0 || f > src.Length) return outside;
        if (f < 0.0) f = 0.0;
        if (f > src.Length - 1) f = src.Length - 1;

        int lo = (int)f;
        int hi = lo + 1 < src.Length ? lo + 1 : lo;
        float k = (float)(f - lo);
        const float Inv = 1f / 65535f;
        return (src[lo] + (src[hi] - src[lo]) * k) * Inv;
    }

    // -- Sampling ------------------------------------------------------------------

    /// <summary>
    /// Fill the caller's arrays with one row, blended between the two nearest rows.
    ///
    /// Both arrays must be at least Columns long, and are owned by the caller so this
    /// never allocates. Either may be null to skip it.
    /// </summary>
    public void SampleRow(float row, float[] fluxOut, float[] absorptionOut)
    {
        if (row < 0f) row = 0f;
        float maxRow = Rows - 1;
        if (row > maxRow) row = maxRow;

        int lo = (int)row;
        int hi = lo + 1 < Rows ? lo + 1 : lo;
        float k = row - lo;

        int a = lo * Columns;
        int b = hi * Columns;
        const float Inv = 1f / 65535f;

        for (int c = 0; c < Columns; c++)
        {
            if (fluxOut != null)
                fluxOut[c] = (_flux[a + c] + (_flux[b + c] - _flux[a + c]) * k) * Inv;

            if (absorptionOut != null)
                absorptionOut[c] = (_absorption[a + c] + (_absorption[b + c] - _absorption[a + c]) * k) * Inv;
        }
    }
}

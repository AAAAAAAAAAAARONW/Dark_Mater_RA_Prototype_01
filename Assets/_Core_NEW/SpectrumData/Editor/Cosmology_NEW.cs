using System;

/// <summary>
/// Flat ΛCDM cosmology: how redshift, lookback time and light-travel distance relate.
///
/// EDITOR ONLY, PURE C#. Used by the baker; never shipped, never run per frame. The
/// runtime gets a small z(lookback) table baked into the file instead.
///
/// Parameters come from Tables/cosmology.csv (Planck 2018, the values astropy ships as
/// Planck18). Radiation is ignored: at z = 8 it is about 0.3% of the expansion rate,
/// well below anything the display can show. See SOURCES.md.
/// </summary>
public sealed class Cosmology_NEW
{
    /// <summary>Speed of light × 1 Gyr, in Mpc. Light-travel distance per Gyr of lookback.</summary>
    public const double MpcPerGyrOfLight = 306.6013937855506;

    /// <summary>1 / (1 km/s/Mpc), in Gyr. Hubble time = this / H0.</summary>
    const double GyrPerInverseH0 = 977.7922216807891;

    public readonly double H0;
    public readonly double OmegaM;
    public readonly double OmegaLambda;
    public readonly double HubbleTimeGyr;

    readonly double[] _z;
    readonly double[] _lookback;

    /// <summary>Build and tabulate lookback time from z = 0 to zMax.</summary>
    public Cosmology_NEW(double h0, double omegaM, double zMax = 30.0, int steps = 60000)
    {
        if (h0 <= 0.0) throw new ArgumentException("H0 must be positive");
        if (omegaM <= 0.0 || omegaM >= 1.0) throw new ArgumentException("Omega_m must be in (0, 1) for a flat universe");

        H0 = h0;
        OmegaM = omegaM;
        OmegaLambda = 1.0 - omegaM;
        HubbleTimeGyr = GyrPerInverseH0 / h0;

        // Lookback time t_L(z) = t_H ∫0^z dz' / ((1+z') E(z')), trapezoid on a fine grid.
        _z = new double[steps + 1];
        _lookback = new double[steps + 1];

        double dz = zMax / steps;
        double prev = Integrand(0.0);
        _z[0] = 0.0;
        _lookback[0] = 0.0;

        for (int i = 1; i <= steps; i++)
        {
            double z = i * dz;
            double f = Integrand(z);
            _z[i] = z;
            _lookback[i] = _lookback[i - 1] + 0.5 * (prev + f) * dz * HubbleTimeGyr;
            prev = f;
        }
    }

    /// <summary>Dimensionless expansion rate H(z)/H0.</summary>
    public double E(double z)
    {
        double a3 = (1.0 + z) * (1.0 + z) * (1.0 + z);
        return Math.Sqrt(OmegaM * a3 + OmegaLambda);
    }

    double Integrand(double z)
    {
        return 1.0 / ((1.0 + z) * E(z));
    }

    /// <summary>Lookback time to redshift z, Gyr. Equal to light-travel distance in Gly.</summary>
    public double LookbackGyr(double z)
    {
        if (z <= 0.0) return 0.0;

        double zMax = _z[_z.Length - 1];
        if (z >= zMax) return _lookback[_lookback.Length - 1];

        double f = z / zMax * (_z.Length - 1);
        int lo = (int)f;
        int hi = Math.Min(lo + 1, _z.Length - 1);
        return _lookback[lo] + (_lookback[hi] - _lookback[lo]) * (f - lo);
    }

    /// <summary>The redshift whose lookback time is this many Gyr.</summary>
    public double ZFromLookback(double gyr)
    {
        if (gyr <= 0.0) return 0.0;

        int n = _lookback.Length;
        if (gyr >= _lookback[n - 1]) return _z[n - 1];

        int lo = 0, hi = n - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) >> 1;
            if (_lookback[mid] <= gyr) lo = mid; else hi = mid;
        }

        double k = (gyr - _lookback[lo]) / (_lookback[hi] - _lookback[lo]);
        return _z[lo] + (_z[hi] - _z[lo]) * k;
    }

    /// <summary>
    /// Age of the universe today, Gyr. t0 = t_H ∫0^1 da / sqrt(Ωm/a + ΩΛ a²), which is
    /// finite at a = 0 so a plain midpoint rule is fine. Used only as a sanity check.
    /// </summary>
    public double AgeGyr()
    {
        const int N = 200000;
        double sum = 0.0;
        for (int i = 0; i < N; i++)
        {
            double a = (i + 0.5) / N;
            sum += 1.0 / Math.Sqrt(OmegaM / a + OmegaLambda * a * a);
        }

        return sum / N * HubbleTimeGyr;
    }

    /// <summary>Proper distance light travels between two redshifts, Mpc (zHigh > zLow).</summary>
    public double LightTravelMpc(double zHigh, double zLow)
    {
        return (LookbackGyr(zHigh) - LookbackGyr(zLow)) * MpcPerGyrOfLight;
    }
}

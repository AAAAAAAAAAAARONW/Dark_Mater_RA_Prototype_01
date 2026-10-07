using UnityEngine;

/// <summary>
/// The noise the quasar's shaders read, made once and shared: 256 x 256, tileable in both
/// directions, four independent channels, each remapped to the same middle and spread so
/// the shaders' thresholds mean the same thing on every channel.
///
///   R  soft fbm — the gas        G  soft fbm — the dust
///   B  ridged fbm — filaments    A  fine fbm — shimmer
///
/// WHY A TEXTURE. The disc used to work out its noise in the shader, sixteen 3D noise
/// lookups a pixel for the disc and as many again for each sheet of gas. Up close — the
/// tutorial parks the light where the disc overfills the frame — that is four full-screen
/// layers of it, before the jet. Read from here it is a handful of texture fetches. Made in
/// code rather than imported, so there is nothing to keep in step with an import setting.
/// </summary>
public static class QuasarNoise_NEW
{
    public const int Size = 256;

    static Texture2D _texture;

    /// <summary>The texture; made on first use (about a tenth of a second) and kept.</summary>
    public static Texture2D Texture
    {
        get
        {
            if (_texture == null) _texture = Build();
            return _texture;
        }
    }

    static Texture2D Build()
    {
        float[] r = Remap(Channel(4, 5, 11, false), 0.5f, 0.105f);
        float[] g = Remap(Channel(4, 5, 23, false), 0.5f, 0.105f);
        float[] b = Remap(Channel(6, 3, 37, true), 0.5f, 0.15f);
        float[] a = Remap(Channel(16, 3, 51, false), 0.5f, 0.12f);

        var pixels = new Color32[Size * Size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(Byte(r[i]), Byte(g[i]), Byte(b[i]), Byte(a[i]));

        // Linear: these are numbers, not colours.
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true, true)
        {
            name = "Quasar noise (runtime)",
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 4
        };
        texture.SetPixels32(pixels);
        texture.Apply(true, true);
        return texture;
    }

    static byte Byte(float v) => (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);

    /// <summary>Octaves of tileable gradient noise, each twice the frequency of the last.</summary>
    static float[] Channel(int basePeriod, int octaves, int seed, bool ridged)
    {
        var values = new float[Size * Size];
        float total = 0f, amplitude = 1f;
        for (int k = 0; k < octaves; k++, amplitude *= 0.5f)
        {
            int period = basePeriod << k;
            Lattice lattice = new Lattice(period, seed + k * 101);
            total += amplitude;

            for (int j = 0; j < Size; j++)
            for (int i = 0; i < Size; i++)
            {
                float n = lattice.Perlin(i / (float)Size * period, j / (float)Size * period);
                if (ridged)
                {
                    n = 1f - Mathf.Abs(n) * 1.6f;
                    n *= n;
                }
                values[j * Size + i] += n * amplitude;
            }
        }

        for (int i = 0; i < values.Length; i++) values[i] /= total;
        return values;
    }

    /// <summary>To the given mean and standard deviation.</summary>
    static float[] Remap(float[] values, float mean, float spread)
    {
        double m = 0, s = 0;
        foreach (float v in values) m += v;
        m /= values.Length;
        foreach (float v in values) s += (v - m) * (v - m);
        s = System.Math.Sqrt(s / values.Length);

        for (int i = 0; i < values.Length; i++)
            values[i] = mean + (float)((values[i] - m) / s) * spread;
        return values;
    }

    /// <summary>
    /// One octave's gradients, a unit vector at each lattice point, repeating every `period`
    /// points so the noise tiles. Worked out once per octave rather than at every pixel.
    /// </summary>
    struct Lattice
    {
        readonly int _period;
        readonly float[] _x, _y;

        public Lattice(int period, int seed)
        {
            _period = period;
            _x = new float[period * period];
            _y = new float[period * period];
            for (int iy = 0; iy < period; iy++)
            for (int ix = 0; ix < period; ix++)
            {
                float angle = (Hash(ix, iy, seed) & 1023u) * (Mathf.PI * 2f / 1024f);
                _x[iy * period + ix] = Mathf.Cos(angle);
                _y[iy * period + ix] = Mathf.Sin(angle);
            }
        }

        /// <summary>2D gradient noise at (x, y), in cells. About ±0.7.</summary>
        public float Perlin(float x, float y)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            float u = Fade(xf), v = Fade(yf);

            float n00 = Dot(xi, yi, xf, yf);
            float n10 = Dot(xi + 1, yi, xf - 1f, yf);
            float n01 = Dot(xi, yi + 1, xf, yf - 1f);
            float n11 = Dot(xi + 1, yi + 1, xf - 1f, yf - 1f);

            float a = n00 + (n10 - n00) * u;
            float b = n01 + (n11 - n01) * u;
            return a + (b - a) * v;
        }

        float Dot(int ix, int iy, float dx, float dy)
        {
            int k = Wrap(iy, _period) * _period + Wrap(ix, _period);
            return _x[k] * dx + _y[k] * dy;
        }
    }

    static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

    static int Wrap(int i, int period) => ((i % period) + period) % period;

    static uint Hash(int ix, int iy, int seed)
    {
        unchecked
        {
            uint h = (uint)ix * 374761393u + (uint)iy * 668265263u + (uint)seed * 1442695041u;
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A night sky drawn once into a texture: a faint nebula wash and a few thousand stars of
/// different sizes and temperatures. For the cover and the credits, behind everything.
///
/// Made in code, in a texture shown by a RawImage, so it needs no shader or asset that a
/// build might strip: UI/Default draws it. The same seed gives the same sky every time, and
/// on every copy of a getReal3D cluster.
/// </summary>
public static class Starfield_NEW
{
    /// <summary>A square sky texture, <paramref name="size"/> pixels, wrapping, so it can drift.</summary>
    public static Texture2D Make(int size, int seed, float density = 1f, float nebula = 1f)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Starfield",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };

        var px = new Color[size * size];
        var rng = new System.Random(seed);
        float ox = (float)rng.NextDouble() * 100f, oy = (float)rng.NextDouble() * 100f;

        // The wash: two octaves of noise, deep blue into violet, barely there.
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size, v = (float)y / size;
                float n = Tileable(u, v, 3f, ox, oy) * 0.65f + Tileable(u, v, 7f, ox + 31f, oy + 17f) * 0.35f;
                n = Mathf.Clamp01((n - 0.45f) * 2.2f);
                n *= n * nebula;
                px[y * size + x] = new Color(0.010f + 0.035f * n, 0.012f + 0.018f * n, 0.022f + 0.060f * n, 1f);
            }
        }

        // The stars: most of them faint points, a few bright with a soft halo.
        int count = Mathf.RoundToInt(size * size / 260f * density);
        for (int i = 0; i < count; i++)
        {
            int x = rng.Next(size), y = rng.Next(size);
            float r = (float)rng.NextDouble();
            float b = Mathf.Pow(r, 6f) * 0.95f + 0.05f;           // brightness, heavily skewed to faint
            float t = (float)rng.NextDouble();                     // temperature: blue-white to warm
            Color c = t < 0.6f ? Color.Lerp(new Color(0.75f, 0.85f, 1f), Color.white, t / 0.6f)
                               : Color.Lerp(Color.white, new Color(1f, 0.86f, 0.68f), (t - 0.6f) / 0.4f);
            Add(px, size, x, y, c * b);

            if (b > 0.55f)
            {
                float h = b * 0.35f;
                Add(px, size, x + 1, y, c * h); Add(px, size, x - 1, y, c * h);
                Add(px, size, x, y + 1, c * h); Add(px, size, x, y - 1, c * h);
                float d = b * 0.12f;
                Add(px, size, x + 1, y + 1, c * d); Add(px, size, x - 1, y - 1, c * d);
                Add(px, size, x + 1, y - 1, c * d); Add(px, size, x - 1, y + 1, c * d);
            }
        }

        tex.SetPixels(px);
        tex.Apply(false, true);
        return tex;
    }

    /// <summary>
    /// A full-screen sky as the first child of <paramref name="parent"/>, so everything else on
    /// that canvas draws over it. Returns the image, to drift its uvRect.
    /// </summary>
    public static RawImage AddBehind(RectTransform parent, Texture2D sky, float tiles = 1.6f)
    {
        var go = new GameObject("Starfield", typeof(RectTransform)) { hideFlags = HideFlags.DontSave };
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.SetAsFirstSibling();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var img = go.AddComponent<RawImage>();
        img.texture = sky;
        img.raycastTarget = false;
        Rect canvas = parent.rect;
        float aspect = canvas.height > 0f ? canvas.width / canvas.height : 16f / 9f;
        img.uvRect = new Rect(0f, 0f, tiles * aspect, tiles);
        return img;
    }

    static void Add(Color[] px, int size, int x, int y, Color c)
    {
        x = (x % size + size) % size;
        y = (y % size + size) % size;
        Color p = px[y * size + x];
        px[y * size + x] = new Color(Mathf.Min(1f, p.r + c.r), Mathf.Min(1f, p.g + c.g), Mathf.Min(1f, p.b + c.b), 1f);
    }

    /// <summary>Perlin noise that wraps at the texture's edges, so the drifting sky has no seam.</summary>
    static float Tileable(float u, float v, float freq, float ox, float oy)
    {
        float a = Mathf.PerlinNoise(ox + u * freq, oy + v * freq);
        float b = Mathf.PerlinNoise(ox + (u - 1f) * freq, oy + v * freq);
        float c = Mathf.PerlinNoise(ox + u * freq, oy + (v - 1f) * freq);
        float d = Mathf.PerlinNoise(ox + (u - 1f) * freq, oy + (v - 1f) * freq);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }
}

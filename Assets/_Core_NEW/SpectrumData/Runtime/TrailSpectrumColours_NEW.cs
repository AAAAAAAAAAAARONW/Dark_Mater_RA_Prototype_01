using System;
using UnityEngine;

/// <summary>
/// Real-wavelength colours for the photon trail's spectrum texture, shared by the
/// tutorial (TutorialBakedSpectrum_NEW) and the journey (BakedSpectrumSource_NEW) so both
/// trails mean the same thing: UV below 4000 Å, the visible rainbow 4000–7000 Å, IR above,
/// with a short blend at each edge.
/// </summary>
public static class TrailSpectrumColours_NEW
{
    /// <summary>
    /// Fill one row of colours. lambdaAt(t) gives the wavelength (Å) at texture position t
    /// (0–1, texel centres). uv and ir are the colours (alpha included) of those bands.
    /// </summary>
    public static void Fill(Color32[] pixels, Func<float, double> lambdaAt, Color uv, Color ir)
    {
        int w = pixels.Length;
        for (int x = 0; x < w; x++)
        {
            double lambda = lambdaAt((x + 0.5f) / w);
            Color c;
            if (lambda < 3850.0) c = uv;
            else if (lambda < 4150.0) c = Color.Lerp(uv, Visible(4150.0), (float)((lambda - 3850.0) / 300.0));
            else if (lambda <= 6850.0) c = Visible(lambda);
            else if (lambda < 7150.0) c = Color.Lerp(Visible(6850.0), ir, (float)((lambda - 6850.0) / 300.0));
            else c = ir;
            pixels[x] = c;
        }
    }

    /// <summary>
    /// Approximate colour of visible light, 4000–7000 Å (after Bruton's piecewise fit), at
    /// full alpha like PhotonSpectrumTrail's visible band.
    /// </summary>
    public static Color Visible(double lambdaA)
    {
        float nm = (float)(lambdaA / 10.0);
        float r, g, b;
        if (nm < 440f) { r = -(nm - 440f) / 60f; g = 0f; b = 1f; }
        else if (nm < 490f) { r = 0f; g = (nm - 440f) / 50f; b = 1f; }
        else if (nm < 510f) { r = 0f; g = 1f; b = -(nm - 510f) / 20f; }
        else if (nm < 580f) { r = (nm - 510f) / 70f; g = 1f; b = 0f; }
        else if (nm < 645f) { r = 1f; g = -(nm - 645f) / 65f; b = 0f; }
        else { r = 1f; g = 0f; b = 0f; }
        return new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), 1f);
    }

    /// <summary>A 1-pixel-high texture of the right width, created or reused.</summary>
    public static Texture2D Ensure(ref Texture2D tex, ref Color32[] pixels, int width, string name)
    {
        if (tex == null || tex.width != width)
        {
            if (tex != null) UnityEngine.Object.Destroy(tex);
            tex = new Texture2D(width, 1, TextureFormat.RGBA32, false, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            pixels = new Color32[width];
        }
        return tex;
    }
}

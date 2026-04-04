using UnityEngine;

/// <summary>
/// Generates a full electromagnetic spectrum gradient texture
/// (Infrared → Red → Orange → Yellow → Green → Blue → Violet → Ultraviolet)
/// and applies it to the PhotonTrail material on this GameObject's TrailRenderer.
///
/// Attach to the same GameObject as your TrailRenderer.
/// </summary>
[RequireComponent(typeof(TrailRenderer))]
public class PhotonSpectrumTrail : MonoBehaviour
{
    [Header("Spectrum Texture")]
    [Tooltip("Resolution of the generated gradient (higher = smoother, 512 is plenty)")]
    public int textureWidth = 512;

    [Header("IR / UV Representation")]
    [Tooltip("How far (0–1) the infrared band extends into the gradient")]
    [Range(0f, 0.25f)] public float irBandWidth  = 0.12f;
    [Tooltip("How far (0–1) the ultraviolet band extends at the right end")]
    [Range(0f, 0.25f)] public float uvBandWidth  = 0.10f;
    [Tooltip("Brightness multiplier for the IR band (dim = 'invisible' feel)")]
    [Range(0f, 1f)]    public float irBrightness = 0.35f;
    [Tooltip("Brightness multiplier for the UV band")]
    [Range(0f, 1f)]    public float uvBrightness = 0.25f;

    [Header("Trail Renderer Gradient (vertex colors)")]
    [Tooltip("Also update the TrailRenderer color gradient to match the spectrum")]
    public bool applyGradientToTrail = true;

    // ── Private ──────────────────────────────────────────────────────────────
    private TrailRenderer   _trail;
    private Texture2D       _spectrumTex;
    private static readonly int SpectrumTexProp = Shader.PropertyToID("_SpectrumTex");
    private static readonly int UseSpectrumProp  = Shader.PropertyToID("_UseSpectrum");

    void Awake()
    {
        _trail = GetComponent<TrailRenderer>();
        BuildAndApply();
    }

#if UNITY_EDITOR
    // Rebuild in editor when values change
    void OnValidate() { if (Application.isPlaying && _trail != null) BuildAndApply(); }
#endif

    void OnDestroy()
    {
        if (_spectrumTex != null)
            Destroy(_spectrumTex);
    }

    // ─────────────────────────────────────────────────────────────────────────

    void BuildAndApply()
    {
        _spectrumTex = GenerateSpectrumTexture();

        // Apply to material
        Material mat = Application.isPlaying
            ? _trail.material          // runtime instance
            : _trail.sharedMaterial;   // editor shared

        if (mat != null)
        {
            mat.SetTexture(SpectrumTexProp, _spectrumTex);
            mat.SetFloat(UseSpectrumProp, 1f);
        }

        // Optionally mirror to the TrailRenderer vertex gradient
        if (applyGradientToTrail)
            _trail.colorGradient = BuildTrailGradient();
    }

    // ── Spectrum Texture ─────────────────────────────────────────────────────

    Texture2D GenerateSpectrumTexture()
    {
        Texture2D tex = new Texture2D(textureWidth, 1, TextureFormat.RGBA32, false)
        {
            wrapMode   = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name       = "SpectrumGradient"
        };

        Color[] pixels = new Color[textureWidth];

        for (int x = 0; x < textureWidth; x++)
        {
            float t = x / (float)(textureWidth - 1);   // 0 = IR, 1 = UV
            pixels[x] = SampleSpectrum(t);
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    /// <summary>
    /// Maps t ∈ [0,1] to the full electromagnetic spectrum:
    ///   0.00 – 0.25 : UV   — transparent violet, fades in from nothing
    ///   0.25 – 0.75 : Visible — Violet → Blue → Green → Yellow → Orange → Red
    ///   0.75 – 1.00 : IR   — transparent red, fades out to nothing
    /// UV and IR bands are wide (matching real-world proportions where
    /// visible light is a narrow slice of the full spectrum).
    /// </summary>
    Color SampleSpectrum(float t)
    {
        // ── UV band (0 → uvEnd) ──────────────────────────────────────────
        float uvEnd  = 0.25f;
        float irStart = 0.75f;

        if (t < uvEnd)
        {
            // Transparent violet: fully invisible at t=0, opaque at t=uvEnd
            float fade = t / uvEnd;                        // 0 → 1
            Color c = new Color(0.6f, 0.0f, 1.0f, 1f);   // violet
            c.a = Mathf.Pow(fade, 0.6f);                  // non-linear — quick rise
            c   *= Mathf.Lerp(0f, 1f, fade);
            return c;
        }

        // ── IR band (irStart → 1) ────────────────────────────────────────
        if (t > irStart)
        {
            // Transparent red: opaque at t=irStart, invisible at t=1
            float fade = 1f - (t - irStart) / (1f - irStart);  // 1 → 0
            Color c = new Color(1.0f, 0.05f, 0.0f, 1f);        // deep red
            c.a = Mathf.Pow(fade, 0.6f);
            c   *= Mathf.Lerp(0f, 1f, fade);
            return c;
        }

        // ── Visible spectrum (uvEnd → irStart) ───────────────────────────
        // Remap to 0..1 within the visible window
        float v = (t - uvEnd) / (irStart - uvEnd);
        return VisibleSpectrumColor(v);
    }

    /// <summary>
    /// Visible spectrum: v=0 → violet, v=1 → red  (short → long wavelength)
    /// </summary>
    Color VisibleSpectrumColor(float v)
    {
        float r, g, b;

        if (v < 1f / 5f)        // Violet → Blue
        {
            float s = v * 5f;
            r = 0.55f - s * 0.55f; g = 0f; b = 1f;
        }
        else if (v < 2f / 5f)   // Blue → Cyan
        {
            float s = (v - 1f / 5f) * 5f;
            r = 0f; g = s * 0.7f; b = 1f;
        }
        else if (v < 3f / 5f)   // Cyan → Green → Yellow
        {
            float s = (v - 2f / 5f) * 5f;
            r = s; g = 1f; b = 1f - s;
        }
        else if (v < 4f / 5f)   // Yellow → Orange
        {
            float s = (v - 3f / 5f) * 5f;
            r = 1f; g = 1f - s * 0.45f; b = 0f;
        }
        else                     // Orange → Red
        {
            float s = (v - 4f / 5f) * 5f;
            r = 1f; g = 0.55f - s * 0.55f; b = 0f;
        }

        return new Color(r, g, b, 1f);
    }

    // ── Trail Renderer Gradient ──────────────────────────────────────────────

    Gradient BuildTrailGradient()
    {
        Gradient grad = new Gradient();
        int steps = 16;

        GradientColorKey[]  colorKeys = new GradientColorKey[steps];
        GradientAlphaKey[]  alphaKeys = new GradientAlphaKey[steps];

        for (int i = 0; i < steps; i++)
        {
            float t   = i / (float)(steps - 1);
            Color col = SampleSpectrum(t);
            colorKeys[i] = new GradientColorKey(new Color(col.r, col.g, col.b), t);
            alphaKeys[i] = new GradientAlphaKey(col.a, t);
        }

        grad.SetKeys(colorKeys, alphaKeys);
        return grad;
    }
}

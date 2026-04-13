using UnityEngine;

/// <summary>
/// Generates a full electromagnetic spectrum gradient texture and applies it
/// to the PhotonTrail material on this GameObject's TrailRenderer.
///
/// Changes in this version:
///   - Preset only applies when enum value changes — sliders stay editable after switching
///   - Meteor shape: sets TrailRenderer.widthCurve to taper head to a point
///   - Spectrum distribution: Equal (all colors same width) or PhysicsBased (nm proportional)
///
/// Attach to the same GameObject as your TrailRenderer.
/// </summary>
[RequireComponent(typeof(TrailRenderer))]
public class PhotonSpectrumTrail : MonoBehaviour
{
    // ── Enums ─────────────────────────────────────────────────────────────────

    public enum SpectrumPreset
    {
        EqualFlat,      // 1. Equal distribution, flat band, no head
        ScientificFlat, // 2. Physics-based distribution, flat band, scientific UV/IR sizes
        SoftFlat,       // 3. Equal distribution, flat band, softer edges
        MeteorHead,     // 4. Equal distribution, meteor shape, head on
        Custom          // Manual — sliders freely editable
    }

    public enum SpectrumDistribution
    {
        Equal,        // All visible colors get equal width
        PhysicsBased  // Width proportional to real nanometer range
    }

    public enum TrailShape
    {
        Flat,    // Uniform width — rectangle
        Meteor   // Tapers to a point at the head
    }

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Preset")]
    [Tooltip("Applies preset values once on change. Switch to Custom to freely adjust sliders.")]
    public SpectrumPreset preset = SpectrumPreset.EqualFlat;

    [Header("Spectrum Texture")]
    [Tooltip("Resolution of the generated gradient (512 is plenty)")]
    public int textureWidth = 512;

    [Header("UV Band (short wavelength end)")]
    [Range(0f, 0.4f)] public float uvBandWidth = 0.15f;
    public Color uvColor = new Color(0.55f, 0f, 1f, 1f);
    [Range(0f, 1f)] public float uvBrightness = 0.30f;

    [Header("IR Band (long wavelength end)")]
    [Range(0f, 0.4f)] public float irBandWidth = 0.15f;
    public Color irColor = new Color(0.8f, 0.02f, 0f, 1f);
    [Range(0f, 1f)] public float irBrightness = 0.28f;

    [Header("Visible Spectrum")]
    [Tooltip("How smoothly color bands blend. 0 = sharp bands, 1 = very soft gradient.")]
    [Range(0f, 1f)] public float bandSoftness = 0.3f;

    [Tooltip("Equal = all colors same width. PhysicsBased = proportional to real nm range.")]
    public SpectrumDistribution distribution = SpectrumDistribution.Equal;

    [Header("Head Indicator")]
    public bool showHead = false;
    [Range(0f, 0.15f)] public float headWidth = 0.04f;
    public Color headColor = new Color(1f, 1f, 1f, 1f);
    [Range(0f, 3f)] public float headBrightness = 1.8f;

    [Header("Trail Shape")]
    [Tooltip("Flat = uniform width. Meteor = semicircle cap at head, full width body.")]
    public TrailShape trailShape = TrailShape.Flat;
    [Tooltip("How much of the trail length the rounded cap occupies. 0.1 = short cap, 0.4 = long cap.")]
    [Range(0.05f, 0.5f)] public float meteorHeadFraction = 0.15f;

    [Header("Trail Renderer Gradient")]
    public bool applyGradientToTrail = true;

    // ── Private ───────────────────────────────────────────────────────────────

    TrailRenderer _trail;
    Texture2D _spectrumTex;
    SpectrumPreset _lastPreset = (SpectrumPreset)(-1); // force apply on first run

    static readonly int SpectrumTexProp = Shader.PropertyToID("_SpectrumTex");
    static readonly int UseSpectrumProp = Shader.PropertyToID("_UseSpectrum");
    static readonly int HeadWidthProp = Shader.PropertyToID("_HeadWidth");
    static readonly int HeadColorProp = Shader.PropertyToID("_HeadColor");
    static readonly int HeadBrightProp = Shader.PropertyToID("_HeadBrightness");
    static readonly int ShowHeadProp = Shader.PropertyToID("_ShowHead");

    // Physics-based stop positions (cumulative nm fractions within visible spectrum)
    // Violet 70nm, Blue 45nm, Cyan 25nm, Green 45nm, Yellow 25nm, Orange 35nm, Red 125nm
    // Total = 370nm
    static readonly float[] PhysicsStops = { 0f, 0.189f, 0.311f, 0.378f, 0.500f, 0.568f, 0.662f, 1.0f };
    static readonly float[] EqualStops = { 0f, 0.167f, 0.333f, 0.500f, 0.667f, 0.833f, 1.0f, 1.0f };

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        _trail = GetComponent<TrailRenderer>();
        ApplyPresetIfChanged();
        BuildAndApply();
    }

    void Start()
    {
        // Clear any leftover editor vertices so UVs start clean
        if (_trail != null) _trail.Clear();
    }

    void OnEnable()
    {
        if (_trail == null) _trail = GetComponent<TrailRenderer>();
        if (_trail == null) return;
        Material mat = Application.isPlaying ? _trail.material : _trail.sharedMaterial;
        if (mat != null && mat.GetTexture(SpectrumTexProp) == null)
            BuildAndApply();
    }

    void OnDestroy()
    {
        if (_spectrumTex != null) Destroy(_spectrumTex);
    }

    [ContextMenu("Rebuild Spectrum Texture")]
    public void RebuildFromMenu()
    {
        if (_trail == null) _trail = GetComponent<TrailRenderer>();
        _lastPreset = (SpectrumPreset)(-1); // force re-apply
        ApplyPresetIfChanged();
        BuildAndApply();
        Debug.Log("[PhotonSpectrumTrail] Rebuilt.");
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (_trail == null) _trail = GetComponent<TrailRenderer>();
        if (_trail == null) return;

        // Only overwrite slider values when the preset enum actually changes.
        // This lets users freely adjust sliders after switching to any preset,
        // not just Custom.
        ApplyPresetIfChanged();

        if (Application.isPlaying)
        {
            BuildAndApply();
        }
        else
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && _trail != null)
                    BuildAndApply();
            };
        }
    }
#endif

    // ── Preset ────────────────────────────────────────────────────────────────

    void ApplyPresetIfChanged()
    {
        if (preset == _lastPreset) return;
        _lastPreset = preset;

        switch (preset)
        {
            case SpectrumPreset.EqualFlat:
                distribution = SpectrumDistribution.Equal;
                trailShape = TrailShape.Flat;
                showHead = false;
                uvBandWidth = 0.15f; uvBrightness = 0.30f;
                irBandWidth = 0.15f; irBrightness = 0.28f;
                bandSoftness = 0.30f;
                uvColor = new Color(0.55f, 0f, 1f, 1f);
                irColor = new Color(0.80f, 0.02f, 0f, 1f);
                break;

            case SpectrumPreset.ScientificFlat:
                // Physics-proportioned UV/IR: UV ~100nm, Visible ~370nm, Near-IR ~450nm (total 920nm)
                // UV ≈ 11%, IR ≈ 49% of total band
                distribution = SpectrumDistribution.PhysicsBased;
                trailShape = TrailShape.Flat;
                showHead = false;
                uvBandWidth = 0.11f; uvBrightness = 0.25f;
                irBandWidth = 0.40f; irBrightness = 0.22f;
                bandSoftness = 0.40f;
                uvColor = new Color(0.50f, 0f, 0.95f, 1f);
                irColor = new Color(0.75f, 0.02f, 0f, 1f);
                break;

            case SpectrumPreset.SoftFlat:
                distribution = SpectrumDistribution.Equal;
                trailShape = TrailShape.Flat;
                showHead = false;
                uvBandWidth = 0.18f; uvBrightness = 0.25f;
                irBandWidth = 0.18f; irBrightness = 0.22f;
                bandSoftness = 0.85f;
                uvColor = new Color(0.50f, 0f, 0.95f, 1f);
                irColor = new Color(0.75f, 0.02f, 0f, 1f);
                break;

            case SpectrumPreset.MeteorHead:
                distribution = SpectrumDistribution.Equal;
                trailShape = TrailShape.Meteor;
                showHead = true;
                meteorHeadFraction = 0.15f;
                headBrightness = 1.8f;
                headColor = new Color(1f, 1f, 1f, 1f);
                uvBandWidth = 0.15f; uvBrightness = 0.30f;
                irBandWidth = 0.15f; irBrightness = 0.28f;
                bandSoftness = 0.30f;
                uvColor = new Color(0.55f, 0f, 1f, 1f);
                irColor = new Color(0.80f, 0.02f, 0f, 1f);
                break;

            case SpectrumPreset.Custom:
                break;
        }
    }

    // ── Build & Apply ─────────────────────────────────────────────────────────

    void BuildAndApply()
    {
        _spectrumTex = GenerateSpectrumTexture();

        Material mat = Application.isPlaying ? _trail.material : _trail.sharedMaterial;
        if (mat != null)
        {
            mat.SetTexture(SpectrumTexProp, _spectrumTex);
            mat.SetFloat(UseSpectrumProp, 1f);
            mat.SetFloat(ShowHeadProp, showHead ? 1f : 0f);
            // In Meteor mode, head glow covers exactly the cap region
            float shaderHeadWidth = (trailShape == TrailShape.Meteor) ? meteorHeadFraction : headWidth;
            mat.SetFloat(HeadWidthProp, shaderHeadWidth);
            mat.SetColor(HeadColorProp, headColor);
            mat.SetFloat(HeadBrightProp, headBrightness);
        }

        ApplyTrailShape();

        if (applyGradientToTrail)
            _trail.colorGradient = BuildTrailGradient();
    }

    // ── Trail Shape ───────────────────────────────────────────────────────────

    void ApplyTrailShape()
    {
        if (_trail == null) return;

        AnimationCurve curve = new AnimationCurve();

        switch (trailShape)
        {
            case TrailShape.Flat:
                curve.AddKey(0f, 1f);
                curve.AddKey(1f, 1f);
                break;

            case TrailShape.Meteor:
                // widthCurve: time=0 = head (newest), time=1 = tail (oldest)
                // Cap region: 0 → meteorHeadFraction  — semicircle profile
                // Body region: meteorHeadFraction → 1  — stays at full width 1
                //
                // Circular width formula for cap:
                //   localT = 0 at tip, 1 where cap meets body
                //   w = sqrt(1 - (1 - localT)^2)  — quarter circle shape
                int capSteps = 16;
                int bodySteps = 4;

                for (int s = 0; s <= capSteps; s++)
                {
                    float t = (s / (float)capSteps) * meteorHeadFraction;
                    float localT = s / (float)capSteps;  // 0 at tip → 1 at join
                    float w = Mathf.Sqrt(1f - (1f - localT) * (1f - localT));
                    curve.AddKey(t, w);
                }

                // Body: full width from cap join to tail
                for (int s = 1; s <= bodySteps; s++)
                {
                    float t = meteorHeadFraction + (s / (float)bodySteps) * (1f - meteorHeadFraction);
                    curve.AddKey(t, 1f);
                }
                break;
        }

        for (int k = 0; k < curve.length; k++)
            AnimationUtility_SmoothKey(curve, k);

        _trail.widthCurve = curve;
    }

    // AnimationCurve doesn't expose SmoothTangents outside editor, so we do it manually
    static void AnimationUtility_SmoothKey(AnimationCurve c, int index)
    {
        // Flat tangent — smooth enough for our purposes without editor API
        Keyframe kf = c[index];
        kf.inTangent = 0f;
        kf.outTangent = 0f;
        c.MoveKey(index, kf);
    }

    // ── Spectrum Texture ──────────────────────────────────────────────────────

    Texture2D GenerateSpectrumTexture()
    {
        Texture2D tex = new Texture2D(textureWidth, 1, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "SpectrumGradient"
        };

        Color[] pixels = new Color[textureWidth];
        for (int x = 0; x < textureWidth; x++)
            pixels[x] = SampleSpectrum(x / (float)(textureWidth - 1));

        // Blur pass — makes colors genuinely blend into each other across band boundaries.
        // bandSoftness controls blur radius: 0 = no blur (sharp bands), 1 = very wide blur.
        // Multiple passes approximate a Gaussian for a smooth gradient feel.
        int blurRadius = Mathf.RoundToInt(bandSoftness * textureWidth * 0.08f);
        if (blurRadius > 0)
        {
            int passes = 3; // 3 box blur passes ≈ Gaussian
            for (int pass = 0; pass < passes; pass++)
            {
                Color[] blurred = new Color[textureWidth];
                for (int x = 0; x < textureWidth; x++)
                {
                    Color sum = Color.black;
                    int count = 0;
                    for (int k = -blurRadius; k <= blurRadius; k++)
                    {
                        int idx = Mathf.Clamp(x + k, 0, textureWidth - 1);
                        sum += pixels[idx];
                        count++;
                    }
                    blurred[x] = sum / count;
                }
                pixels = blurred;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    Color SampleSpectrum(float t)
    {
        float visStart = uvBandWidth;
        float visEnd = 1f - irBandWidth;

        if (t < visStart)
        {
            float smooth = Mathf.SmoothStep(0f, 1f, t / Mathf.Max(visStart, 0.001f));
            Color c = uvColor * uvBrightness;
            c.a = smooth * uvBrightness;
            return c;
        }

        if (t > visEnd)
        {
            float smooth = Mathf.SmoothStep(0f, 1f, 1f - (t - visEnd) / Mathf.Max(irBandWidth, 0.001f));
            Color c = irColor * irBrightness;
            c.a = smooth * irBrightness;
            return c;
        }

        float v = (t - visStart) / Mathf.Max(visEnd - visStart, 0.001f);
        return VisibleSpectrumColor(v);
    }

    Color VisibleSpectrumColor(float v)
    {
        // Stop positions differ between Equal and PhysicsBased
        float[] pos = distribution == SpectrumDistribution.PhysicsBased ? PhysicsStops : EqualStops;

        // Color at each stop: Violet, Blue, Cyan, Green, Yellow, Orange, Red
        float[,] rgb = new float[,]
        {
            { 0.58f, 0.00f, 1.00f },  // Violet
            { 0.00f, 0.10f, 1.00f },  // Blue      — less green so blue stays pure
            { 0.00f, 0.60f, 0.80f },  // Cyan      — reduced green (was 0.90) tighter band
            { 0.00f, 1.00f, 0.00f },  // Green     — pure green peak
            { 0.90f, 0.80f, 0.00f },  // Yellow    — reduced green (was 1.00) tighter band
            { 1.00f, 0.35f, 0.00f },  // Orange
            { 1.00f, 0.05f, 0.00f },  // Red
        };

        int count = rgb.GetLength(0);
        int lo = 0, hi = count - 1;

        for (int i = 0; i < count - 1; i++)
        {
            if (v >= pos[i] && v <= pos[i + 1])
            {
                lo = i; hi = i + 1;
                break;
            }
        }

        float span = pos[hi] - pos[lo];
        float local = span > 0.001f ? (v - pos[lo]) / span : 0f;
        // Simple linear lerp — softness is now handled by the blur pass in GenerateSpectrumTexture
        return new Color(
            Mathf.Lerp(rgb[lo, 0], rgb[hi, 0], local),
            Mathf.Lerp(rgb[lo, 1], rgb[hi, 1], local),
            Mathf.Lerp(rgb[lo, 2], rgb[hi, 2], local),
            1f);
    }

    // ── Trail Gradient ────────────────────────────────────────────────────────

    Gradient BuildTrailGradient()
    {
        // Simple white → transparent fade along the trail length.
        // All color work is done by the spectrum texture in the shader.
        // Using a spectrum gradient here would multiply two spectrums together
        // (length × width), causing complementary colors to cancel and create dark gaps.
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        return grad;
    }
}
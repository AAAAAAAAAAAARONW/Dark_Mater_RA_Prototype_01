using UnityEngine;

/// <summary>
/// Generates a full electromagnetic spectrum gradient texture and applies it
/// to the PhotonTrail material on this GameObject's TrailRenderer.
///
/// [ExecuteAlways] keeps the texture live in edit mode.
/// OnDrawGizmos draws a preview strip in the Scene view so you can see
/// the spectrum without entering Play mode.
///
/// Attach to the same GameObject as your TrailRenderer.
/// </summary>
[ExecuteAlways]
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

    [Header("Absorption Lines")]
    [Tooltip("Master switch for absorption lines. Must match LymanAlphaAbsorptionController.")]
    public bool useAbsorptionLines = false;
    [Tooltip("Global darkness multiplier on all absorption lines.")]
    [Range(0f, 1f)] public float absorptionLineStrength = 1f;
    public bool applyGradientToTrail = true;
    [Tooltip("Lower = more vertices = smoother head cap at high speed. 0.05 is a good balance.")]
    [Range(0.01f, 1f)] public float minVertexDistance = 0.05f;

    // ── Private ───────────────────────────────────────────────────────────────

    TrailRenderer _trail;
    Texture2D _spectrumTex;
    // Serialized so Unity's Preset system saves/restores it alongside preset enum.
    // This prevents OnValidate from re-applying preset defaults after a Preset load.
    [SerializeField, HideInInspector] SpectrumPreset _lastPreset = (SpectrumPreset)(-1);

    static readonly int SpectrumTexProp = Shader.PropertyToID("_SpectrumTex");
    static readonly int UseSpectrumProp = Shader.PropertyToID("_UseSpectrum");
    static readonly int HeadWidthProp = Shader.PropertyToID("_HeadWidth");
    static readonly int HeadColorProp = Shader.PropertyToID("_HeadColor");
    static readonly int HeadBrightProp = Shader.PropertyToID("_HeadBrightness");
    static readonly int ShowHeadProp = Shader.PropertyToID("_ShowHead");
    static readonly int UseAbsorptionProp = Shader.PropertyToID("_UseAbsorptionLine");
    static readonly int AbsorptionStrengthProp = Shader.PropertyToID("_AbsorptionLineStrength");

    // Physics-based stop positions (cumulative nm fractions within visible spectrum)
    // Violet 70nm, Blue 45nm, Cyan 25nm, Green 45nm, Yellow 25nm, Orange 35nm, Red 125nm
    // Total = 370nm
    static readonly float[] PhysicsStops = { 0f, 0.189f, 0.311f, 0.378f, 0.500f, 0.568f, 0.662f, 1.0f };
    static readonly float[] EqualStops = { 0f, 0.167f, 0.333f, 0.500f, 0.667f, 0.833f, 1.0f, 1.0f };

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        _trail = GetComponent<TrailRenderer>();
        // Mark preset as already applied so entering Play mode never
        // overwrites inspector values. Only actual dropdown changes trigger re-apply.
        _lastPreset = preset;
        BuildAndApply();
    }

    void Start()
    {
        // Clear any leftover editor vertices so UVs start clean
        if (_trail != null) _trail.Clear();
    }

    void Update()
    {
        // Push head + absorption params every frame so inspector changes are instant.
        // This is cheap (just float/color writes) — texture rebuild only happens when needed.
        PushLiveParams();
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
        if (_spectrumTex == null) return;
        if (Application.isPlaying)
            Destroy(_spectrumTex);
        else
            DestroyImmediate(_spectrumTex);
    }

    [ContextMenu("Rebuild Spectrum Texture")]
    public void RebuildFromMenu()
    {
        if (_trail == null) _trail = GetComponent<TrailRenderer>();
        _lastPreset = preset; // don't re-apply preset values, just rebuild texture
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

    [Header("Material Control")]
    [Tooltip("If true, script writes head settings to the material at runtime. " +
             "Turn OFF if you want to control head settings via the material asset / Preset instead.")]
    public bool overrideHeadSettingsOnMaterial = true;

    void BuildAndApply()
    {
        _spectrumTex = GenerateSpectrumTexture();

        Material mat = Application.isPlaying ? _trail.material : _trail.sharedMaterial;
        if (mat != null)
        {
            mat.SetTexture(SpectrumTexProp, _spectrumTex);
            mat.SetFloat(UseSpectrumProp, 1f);
        }

        PushLiveParams();

        // Lower minVertexDistance = more vertices near the head = smoother cap at high speed
        _trail.minVertexDistance = minVertexDistance;

        ApplyTrailShape();

        if (applyGradientToTrail)
            _trail.colorGradient = BuildTrailGradient();
    }

    /// <summary>
    /// Writes head and absorption params to the material every frame.
    /// Cheap — no texture rebuild. Keeps inspector sliders live.
    /// </summary>
    void PushLiveParams()
    {
        if (_trail == null) return;
        Material mat = Application.isPlaying ? _trail.material : _trail.sharedMaterial;
        if (mat == null) return;

        if (overrideHeadSettingsOnMaterial)
        {
            mat.SetFloat(ShowHeadProp, showHead ? 1f : 0f);
            float shaderHeadWidth = (trailShape == TrailShape.Meteor) ? meteorHeadFraction : headWidth;
            mat.SetFloat(HeadWidthProp, shaderHeadWidth);
            mat.SetColor(HeadColorProp, headColor);
            mat.SetFloat(HeadBrightProp, headBrightness);
        }

        mat.SetFloat(UseAbsorptionProp, useAbsorptionLines ? 1f : 0f);
        mat.SetFloat(AbsorptionStrengthProp, absorptionLineStrength);
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
                // WidthCurve stays flat — the rounded cap is drawn entirely by
                // the shader's circular UV clip. No geometry manipulation needed.
                curve.AddKey(0f, 1f);
                curve.AddKey(1f, 1f);
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

    // ── Editor Preview ────────────────────────────────────────────────────────

    [Header("Editor Preview")]
    [Tooltip("Show spectrum preview strip in the Scene view without entering Play mode.")]
    public bool showEditorPreview = true;
    [Tooltip("Length of the preview strip in world units.")]
    public float previewLength = 4f;
    [Tooltip("Width of the preview strip in world units.")]
    public float previewWidth = 0.4f;
    [Tooltip("How many color slices to draw across the width (more = smoother).")]
    [Range(8, 64)] public int previewSlices = 32;

    void OnDrawGizmos()
    {
        if (!showEditorPreview) return;
        if (!Application.isEditor) return;

        // Rebuild texture if needed so preview reflects current settings
        if (_spectrumTex == null)
        {
            if (_trail == null) _trail = GetComponent<TrailRenderer>();
            ApplyPresetIfChanged();
            _spectrumTex = GenerateSpectrumTexture();
        }

        Vector3 origin = transform.position;
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        float sliceWidth = previewWidth / previewSlices;

        // Draw the spectrum across the width (matching uv.y axis in shader)
        // Each slice is one color band, sampled from the spectrum texture
        for (int s = 0; s < previewSlices; s++)
        {
            // t goes from 0 (UV end) to 1 (IR end) across the width
            float t0 = s / (float)previewSlices;
            float t1 = (s + 1) / (float)previewSlices;
            float tMid = (t0 + t1) * 0.5f;

            // Sample the spectrum texture at this width position
            // Invert because uv.y=0 is UV edge (one side), =1 is IR edge (other side)
            Color col = SampleSpectrum(1f - tMid);
            col.a = 1f;

            Gizmos.color = col;

            // Offset from centre: slice s is at (s - half) * sliceWidth
            float offsetCentre = (tMid - 0.5f) * previewWidth;
            Vector3 sliceCentre = origin + right * offsetCentre;

            // Draw a thin quad slice as two triangles via a line-based rectangle
            Vector3 p0 = sliceCentre - right * (sliceWidth * 0.5f);
            Vector3 p1 = sliceCentre + right * (sliceWidth * 0.5f);
            Vector3 p0f = p0 + forward * previewLength;
            Vector3 p1f = p1 + forward * previewLength;

            // DrawLine outlines — visible as coloured bands in scene view
            Gizmos.DrawLine(p0, p0f);
            Gizmos.DrawLine(p0, p1);
            Gizmos.DrawLine(p0f, p1f);
            Gizmos.DrawLine(p1, p1f);

            // Fill via a wire cube scaled to the slice dimensions
            Vector3 centre = (p0 + p1f) * 0.5f;
            Vector3 size = new Vector3(sliceWidth, 0.001f, previewLength);

#if UNITY_EDITOR
            // Use DrawMesh for a filled preview (editor only)
            UnityEditor.Handles.color = col;
            UnityEditor.Handles.DrawSolidRectangleWithOutline(
                new Vector3[]
                {
                    p0,
                    p0 + forward * previewLength,
                    p1 + forward * previewLength,
                    p1
                },
                col, new Color(0, 0, 0, 0)
            );
#endif
        }

        // Draw head semicircle outline if head is enabled
        if (showHead)
        {
#if UNITY_EDITOR
            UnityEditor.Handles.color = headColor;
            float radius = previewWidth * 0.5f;
            UnityEditor.Handles.DrawWireArc(origin, transform.up, -right, 180f, radius);
            UnityEditor.Handles.DrawLine(origin - right * radius, origin + right * radius);
#endif
        }

        // Label
#if UNITY_EDITOR
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(origin + transform.up * (previewWidth * 0.5f + 0.1f),
            $"Spectrum Preview [{preset}]");
#endif
    }
}
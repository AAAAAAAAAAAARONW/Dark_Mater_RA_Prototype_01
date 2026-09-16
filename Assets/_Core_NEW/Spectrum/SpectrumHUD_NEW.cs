using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Draws the quasar spectrum with the live absorption forest cut into it.
///
/// Replaces LAFSpectrumHUD. The emission template is a line-for-line port — the same
/// continuum break, the same seven emission lines at the same centres, sigmas and
/// heights — so the curve is the one that was tuned, not a new one.
///
/// WHAT CHANGED: the template is baked once into a lookup table instead of being
/// recomputed every frame. It is a pure function of position, and the only thing that
/// changes frame to frame is the redshift offset, which is a horizontal shift. The old
/// version nevertheless evaluated it 1024 times per frame, each evaluation running about
/// seven Mathf.Exp calls — roughly seven thousand exponentials per frame to redraw a
/// curve whose shape never changes. Now it is a table read.
///
/// WHAT WAS DROPPED: CSV mode. The scene has no CSV assigned and runs procedurally;
/// carrying the sliding-window reader would have doubled this file to support a path
/// nothing uses. If a real spectrum file is ever needed, bake it into the same LUT.
///
/// Absorption still comes from the shared AbsorptionField_NEW, so the HUD and the photon
/// trail read the same buffer and stay in step.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("SPEC HUD", "#33B3B3")]
public class SpectrumHUD_NEW : MonoBehaviour
{
    [Header("Graph target")]
    [Tooltip("The mesh-based line renderer. Auto-found on this GameObject.")]
    [SerializeField] LineGraphRenderer_NEW graph;

    [Tooltip("Points drawn across the spectrum. The graph is only a few hundred pixels " +
             "wide, so 512 is already about one sample per pixel; the old value of 1024 " +
             "doubled the mesh for no visible gain.")]
    [Range(64, 2048)]
    [SerializeField] int sampleCount = 512;

    [Header("Absorption source")]
    [SerializeField] AbsorptionField_NEW field;
    [SerializeField] bool useAbsorption = true;

    [Tooltip("HUD-only multiplier on absorption depth.")]
    [Range(0f, 3f)]
    [SerializeField] float absorptionStrength = 1f;

    [Tooltip("On: max(0, template - absorption), a harder cut. " +
             "Off: template * (1 - absorption), softer.")]
    [SerializeField] bool dramaticSubtraction = false;

    [Header("Redshift")]
    [Tooltip("Used only when no AbsorptionField_NEW is present. Normally the HUD follows " +
             "the field's drift so the curve and the lines move as one.")]
    [SerializeField] float fallbackDriftPerSecond = 0.005f;

    [Tooltip("Slide the continuum along with the absorption lines.\n\n" +
             "ON is the journey's behaviour and the default: the whole picture — curve and " +
             "marks together — drifts redward, which is one long slow statement about " +
             "expansion.\n\n" +
             "OFF holds the curve still and lets the lines drift through it, and the " +
             "tutorial needs that. Its lines are cut at one authored wavelength on the " +
             "bright flank of the Ly-alpha peak, because absorption is drawn by taking " +
             "height away and a line can only be as visible as whatever it cuts into. Let " +
             "the peak slide and that wavelength is somewhere else within seconds — a mark " +
             "cut into the eleven pixels of flat continuum out in the forest, which is to " +
             "say no mark at all.\n\n" +
             "Holding the curve also makes the drift readable as a thing happening TO the " +
             "marks. Everything moving at once has no reference to move against.")]
    [SerializeField] bool driftContinuum = true;

    [SerializeField] bool wrapSpectrum = true;
    [SerializeField] bool useUnscaledTime = true;

    [Header("Template")]
    [Tooltip("Normalised position of the Ly-alpha emission peak. Overwritten per layer " +
             "by SpectrumResponder_NEW.")]
    [Range(0f, 1f)]
    [SerializeField] float lyaPosition = 0.42f;

    [Tooltip("Entries in the baked template table. The narrowest feature has a sigma of " +
             "0.0085, so 4096 gives it about 35 samples — far more than the display needs.")]
    [Range(512, 8192)]
    [SerializeField] int lutResolution = 4096;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    float[] _lut;
    float _bakedLyaPosition = float.NaN;
    float _redshiftOffset;
    readonly List<float> _display = new List<float>();

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Put the continuum back where it started, as if no time had passed.
    ///
    /// The redshift offset only ever accumulates, which is right for a journey that runs
    /// once and forward. It is not right for anything that restarts: the tutorial loops
    /// all day on an attract timer, and a spectrum that drifts from the moment the scene
    /// loads would show its Ly-alpha peak at a different place on every run, depending
    /// on how long the last visitor took. Nobody can rehearse against that, and the
    /// authored line positions stop meaning what they say.
    ///
    /// Nothing in the journey calls this; it is here so a restart has somewhere to put
    /// the clock back to.
    /// </summary>
    public void ResetRedshift()
    {
        _redshiftOffset = 0f;
        Redraw();
    }

    /// <summary>Apply a layer's continuum shape. Rebakes the table if the peak moved.</summary>
    public void Configure(SpectrumProfile_NEW profile)
    {
        if (profile == null) return;

        lyaPosition = profile.continuumPeakPosition;
        if (!Mathf.Approximately(lyaPosition, _bakedLyaPosition)) BakeTemplate();

        if (debugLog)
            Debug.Log($"[SpectrumHUD_NEW] Continuum peak -> {lyaPosition:F3}.", this);
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (graph == null) graph = GetComponent<LineGraphRenderer_NEW>();
        if (field == null) field = FindObjectOfType<AbsorptionField_NEW>();

        if (graph == null)
            Debug.LogError("[SpectrumHUD_NEW] No LineGraphRenderer_NEW found. Nothing will draw.", this);

        BakeTemplate();
    }

    void OnEnable() => Redraw();

    void Update()
    {
        if (driftContinuum)
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            float drift = field != null ? field.CurrentDriftPerSecond : fallbackDriftPerSecond;
            _redshiftOffset += dt * drift;
            if (wrapSpectrum) _redshiftOffset -= Mathf.Floor(_redshiftOffset);
        }

        // Redrawn either way: the absorption buffer is still moving under a held curve,
        // and it is sampled fresh on every pass.
        Redraw();
    }

    // ── Drawing ──────────────────────────────────────────────────────────────

    void Redraw()
    {
        if (graph == null || _lut == null) return;

        bool absorb = useAbsorption && field != null;

        _display.Clear();
        for (int i = 0; i < sampleCount; i++)
        {
            float screenT = sampleCount <= 1 ? 0f : i / (float)(sampleCount - 1);

            // The whole spectrum slides toward the red end; sample the template shifted back.
            float spectrumT = screenT - _redshiftOffset;
            if (wrapSpectrum) spectrumT -= Mathf.Floor(spectrumT);

            float template = SampleTemplate(spectrumT);
            float absorption = absorb ? field.SampleAbsorption(screenT) * absorptionStrength : 0f;

            _display.Add(Mathf.Clamp01(ApplyAbsorption(template, absorption)));
        }

        if (_display.Count < 2) return;
        graph.SetValues(_display, 0f, 1f);
    }

    float ApplyAbsorption(float template, float absorption)
    {
        if (absorption <= 0f) return template;

        return dramaticSubtraction
            ? Mathf.Max(0f, template - absorption)
            : template * (1f - Mathf.Clamp01(absorption));
    }

    // ── Template ─────────────────────────────────────────────────────────────

    void BakeTemplate()
    {
        lutResolution = Mathf.Clamp(lutResolution, 512, 8192);
        if (_lut == null || _lut.Length != lutResolution) _lut = new float[lutResolution];

        for (int i = 0; i < lutResolution; i++)
            _lut[i] = QuasarTemplate(i / (float)(lutResolution - 1));

        _bakedLyaPosition = lyaPosition;

        if (debugLog)
            Debug.Log($"[SpectrumHUD_NEW] Template baked: {lutResolution} entries, peak at {lyaPosition:F3}.", this);
    }

    float SampleTemplate(float t)
    {
        if (_lut == null) return 0f;
        if (t < 0f || t > 1f) return 0f;

        float fp = t * (_lut.Length - 1);
        int lo = Mathf.FloorToInt(fp);
        int hi = Mathf.Min(lo + 1, _lut.Length - 1);
        return Mathf.Lerp(_lut[lo], _lut[hi], fp - lo);
    }

    /// <summary>
    /// Quasar emission template over a normalised 800-1800A window.
    /// Ported unchanged: same continuum break, same seven lines, same constants.
    /// Only ever called at bake time now.
    /// </summary>
    float QuasarTemplate(float t)
    {
        if (t < 0f || t > 1f) return 0f;

        float continuum;
        if (t < lyaPosition)
        {
            float forestT = Mathf.Clamp01((t - 0.06f) / Mathf.Max(lyaPosition - 0.06f, 0.001f));
            continuum = 0.18f * forestT;
        }
        else
        {
            float pastT = (t - lyaPosition) / Mathf.Max(1f - lyaPosition, 0.001f);
            continuum = 0.20f - 0.07f * pastT;
        }

        float em = 0f;
        em += GaussBump(t, 0.226f, 0.012f, 0.06f);

        float lyaDt = t - lyaPosition;
        float lyaSigma = lyaDt < 0f ? 0.0085f : 0.013f;
        em += Mathf.Exp(-0.5f * (lyaDt / lyaSigma) * (lyaDt / lyaSigma)) * 0.80f;

        em += GaussBump(t, 0.535f, 0.012f, 0.025f); // C II  1335A
        em += GaussBump(t, 0.600f, 0.014f, 0.045f); // Si IV 1400A
        em += GaussBump(t, 0.727f, 0.012f, 0.025f); // Si II 1527A
        em += GaussBump(t, 0.749f, 0.018f, 0.13f);  // C IV  1549A — second strongest
        em += GaussBump(t, 0.808f, 0.014f, 0.020f); // Fe II 1608A

        float lyLimit = Mathf.SmoothStep(0f, 1f, (t - 0.07f) / 0.06f);

        return Mathf.Max(0f, (continuum + em) * lyLimit);
    }

    static float GaussBump(float t, float center, float sigma, float height)
    {
        float d = (t - center) / Mathf.Max(sigma, 0.0001f);
        return Mathf.Exp(-0.5f * d * d) * height;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying && _lut != null) BakeTemplate();
    }
#endif
}

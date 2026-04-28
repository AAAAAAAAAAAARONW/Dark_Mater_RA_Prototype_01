using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HUD spectrum display.
///
///   • Renders a quasar template (procedural multi-bump or CSV) as the
///     underlying emission spectrum.
///   • Overlays the live absorption field from LymanAlphaAbsorptionController
///     so HUD and photon-trail show the same lines at aligned positions.
///   • The whole spectrum (template AND absorption) drifts rightward over
///     time at a rate proportional to player speed — cosmological redshift.
/// </summary>
public class LAFSpectrumHUD : MonoBehaviour
{
    // ── Graph Targets ─────────────────────────────────────────────────────────

    [Header("Graph Target")]
    [SerializeField] UICanvasLineGraph canvasGraph;
    [SerializeField] UIImageLineGraphEffect imageGraph;

    // ── Source mode ───────────────────────────────────────────────────────────

    [Header("Quasar Template Source")]
    [Tooltip("Optional CSV (index,wavelength,flux). Leave blank to use the built-in procedural template (recommended).")]
    [SerializeField] TextAsset spectrumCsv;
    [Tooltip("CSV mode: how many samples are visible at once.")]
    [SerializeField] int csvWindowSize = 200;

    [Header("Procedural Template (no-CSV mode)")]
    [Tooltip("How many points to render across the visible 800-1800Å range. Higher = smoother line.")]
    [SerializeField] int proceduralSampleCount = 1024;

    // ── Absorption Source ─────────────────────────────────────────────────────

    [Header("Absorption Source")]
    [Tooltip("Reads the live absorption field. Auto-found if blank.")]
    [SerializeField] LymanAlphaAbsorptionController absorptionController;
    [SerializeField] bool useExternalAbsorption = true;
    [Tooltip("HUD-only extra multiplier on the absorption depth (separate from controller's).")]
    [Range(0f, 3f)]
    [SerializeField] float absorptionStrength = 1.5f;
    [Tooltip("If true: max(0, template - absorption × strength) — more dramatic. " +
             "If false: template × (1 - absorption × strength) — softer.")]
    [SerializeField] bool useDramaticSubtraction = true;

    // ── Redshift (whole-spectrum drift) ───────────────────────────────────────

    [Header("Redshift (whole-spectrum drift)")]
    [Tooltip("Fallback drift rate when no LymanAlphaAbsorptionController is found. " +
             "Otherwise the HUD reads the controller's CurrentDriftPerSecond so spectrum " +
             "and absorption stay perfectly locked together.")]
    [SerializeField] float fallbackRedshiftRate = 0.005f;
    [Tooltip("Wrap the template so features re-enter from the left after exiting the right.")]
    [SerializeField] bool wrapSpectrum = true;
    [SerializeField] bool useUnscaledTime = true;

    [Header("Player Speed Coupling")]
    [SerializeField] bool linkToPlayerSpeed = true;
    [SerializeField] DarkMatterPlayerControllerTest playerController;
    [SerializeField] float referencePlayerSpeed = 20f;
    [SerializeField] float maxSpeedMultiplier = 4f;

    // ── Quasar Shape Knobs ────────────────────────────────────────────────────

    [Header("Procedural Continuum (no-CSV mode only)")]
    [Tooltip("Position of Ly-α emission peak in t [0..1]. 1216Å maps to ~0.42 over the 800-1800Å range.")]
    [Range(0f, 1f)]
    [SerializeField] float lyaPosition = 0.42f;

    // ── Diagnostics ───────────────────────────────────────────────────────────

    [Header("Diagnostics")]
    [SerializeField] bool warnIfControllerMissing = true;

    // ── Private ───────────────────────────────────────────────────────────────

    enum HUDMode { CSV, Procedural }
    HUDMode _mode;

    // CSV mode
    List<SpectrumSample> _master;
    SpectrumSlidingWindow _window;
    float _csvFluxMax = 1f;
    float _csvScrollAccum;

    // Shared (procedural mode uses _redshiftOffset directly via QuasarTemplate)
    float _redshiftOffset;

    readonly List<float> _display = new List<float>();

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        if (canvasGraph == null) canvasGraph = GetComponent<UICanvasLineGraph>();
        if (imageGraph  == null) imageGraph  = GetComponent<UIImageLineGraphEffect>();
        if (playerController == null)
            playerController = FindObjectOfType<DarkMatterPlayerControllerTest>();
        if (absorptionController == null)
            absorptionController = FindObjectOfType<LymanAlphaAbsorptionController>();
    }

    void OnEnable()
    {
        Initialise();

        if (warnIfControllerMissing && useExternalAbsorption && absorptionController == null)
            Debug.LogWarning("[LAFSpectrumHUD] No LymanAlphaAbsorptionController found in the scene — " +
                             "the HUD will show a clean spectrum with no absorption forest. " +
                             "Add the component to a persistent GameObject (e.g. GameManager) to see absorption.");

        Redraw();
    }

    void Update()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        // Pull drift rate from the controller so spectrum and absorption are perfectly locked.
        // Falls back to local rate × speed if no controller is present.
        float driftPerSecond;
        if (absorptionController != null)
            driftPerSecond = absorptionController.CurrentDriftPerSecond;
        else
            driftPerSecond = fallbackRedshiftRate * GetSpeedFactor();

        _redshiftOffset += dt * driftPerSecond;
        if (wrapSpectrum) _redshiftOffset -= Mathf.Floor(_redshiftOffset);

        // CSV mode also steps its sliding window at the same rate.
        if (_mode == HUDMode.CSV && _window != null)
        {
            float effective = driftPerSecond * _master.Count;
            _csvScrollAccum += dt * effective;
            int steps = Mathf.FloorToInt(_csvScrollAccum);
            if (steps > 0)
            {
                _csvScrollAccum -= steps;
                for (int i = 0; i < steps; i++)
                    if (!_window.TryStepLeft(true)) break;
            }
        }

        Redraw();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void SetScrollSpeed(float ratePerSecond)
    {
        fallbackRedshiftRate = Mathf.Max(0f, ratePerSecond);
    }

    public void Configure(LayerDefinitionTest def)
    {
        if (def == null) return;
        // Only used as a fallback when no controller is present; controller's rate wins otherwise.
        fallbackRedshiftRate = def.lafScrollSpeedStepsPerSecond * 0.0025f;
        lyaPosition          = def.lafContinuumPeakPosition;
    }

    [ContextMenu("Reinitialise")]
    public void Reinitialise() => Initialise();

    // ── Initialisation ────────────────────────────────────────────────────────

    void Initialise()
    {
        if (spectrumCsv != null) InitCSV();
        else                     InitProcedural();
        _redshiftOffset = 0f;
    }

    void InitCSV()
    {
        _mode   = HUDMode.CSV;
        _master = SpectrumDataLoader.LoadFromTextAsset(spectrumCsv);

        int winSize = Mathf.Clamp(csvWindowSize, 2, _master.Count);
        _window = new SpectrumSlidingWindow(_master, winSize, startAtEnd: true);

        _csvFluxMax = 0.0001f;
        for (int i = 0; i < _master.Count; i++)
            if (_master[i].Flux > _csvFluxMax) _csvFluxMax = _master[i].Flux;
    }

    void InitProcedural()
    {
        _mode = HUDMode.Procedural;
        proceduralSampleCount = Mathf.Max(64, proceduralSampleCount);
    }

    // SDSS-composite-inspired quasar template, 800-1800Å mapped onto t=0..1.
    // Tuned to reproduce the prominent Ly-α spike, depressed forest baseline,
    // and characteristic post-Ly-α features.
    float QuasarTemplate(float t)
    {
        // Hard cutoff outside [0,1] when not wrapping.
        if (t < 0f || t > 1f) return 0f;

        // Continuum baseline:
        //   • forest region (left of Ly-α): rises from 0 at Ly-limit to ~0.18 at peak
        //   • post Ly-α: starts ~0.20, gently fades to ~0.13 by 1800Å
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

        // Emission lines.
        float em = 0f;

        // Ly-β (1026Å, t≈0.226): small bump
        em += GaussBump(t, 0.226f, 0.012f, 0.06f);

        // Ly-α: dominant spike. Real composite FWHM ~12-15Å in flat-continuum frame.
        // Slight red-side asymmetry from the broad damping wing.
        float lyaDt    = t - lyaPosition;
        float lyaSigma = lyaDt < 0f ? 0.0085f : 0.013f;
        em += Mathf.Exp(-0.5f * (lyaDt / lyaSigma) * (lyaDt / lyaSigma)) * 0.80f;

        // Post-Ly-α features.
        em += GaussBump(t, 0.535f, 0.012f, 0.025f); // C II  1335Å
        em += GaussBump(t, 0.600f, 0.014f, 0.045f); // Si IV 1400Å
        em += GaussBump(t, 0.727f, 0.012f, 0.025f); // Si II 1527Å
        em += GaussBump(t, 0.749f, 0.018f, 0.13f);  // C IV  1549Å — second strongest
        em += GaussBump(t, 0.808f, 0.014f, 0.020f); // Fe II 1608Å

        // Lyman-limit: hard drop below 912Å (t≈0.112).
        float lyLimit = Mathf.SmoothStep(0f, 1f, (t - 0.07f) / 0.06f);

        return Mathf.Max(0f, (continuum + em) * lyLimit);
    }

    static float GaussBump(float t, float center, float sigma, float height)
    {
        float d = (t - center) / Mathf.Max(sigma, 0.0001f);
        return Mathf.Exp(-0.5f * d * d) * height;
    }

    // ── Speed ─────────────────────────────────────────────────────────────────

    float GetSpeedFactor()
    {
        if (!linkToPlayerSpeed || playerController == null || referencePlayerSpeed <= 0.0001f)
            return 1f;
        float f = playerController.Speed / referencePlayerSpeed;
        if (f < 0f) f = 0f;
        if (f > maxSpeedMultiplier) f = maxSpeedMultiplier;
        return f;
    }

    // ── Render ────────────────────────────────────────────────────────────────

    void Redraw()
    {
        _display.Clear();
        bool useAbs = useExternalAbsorption && absorptionController != null;

        if (_mode == HUDMode.Procedural)
        {
            int n = proceduralSampleCount;
            for (int i = 0; i < n; i++)
            {
                float screenT = (n <= 1) ? 0f : i / (float)(n - 1);

                // Whole spectrum drifts right by _redshiftOffset; sample template at shifted position.
                float spectrumT = screenT - _redshiftOffset;
                if (wrapSpectrum) spectrumT -= Mathf.Floor(spectrumT);

                float tmpl = QuasarTemplate(spectrumT);
                float abs  = useAbs ? absorptionController.SampleAbsorption(screenT) * absorptionStrength : 0f;
                _display.Add(Mathf.Clamp01(ApplyAbsorption(tmpl, abs)));
            }
        }
        else if (_mode == HUDMode.CSV && _window != null)
        {
            var visible = _window.Visible;
            int n = visible.Count;
            for (int i = 0; i < n; i++)
            {
                float flux    = visible[i].Flux / _csvFluxMax;
                float screenT = (n <= 1) ? 0f : i / (float)(n - 1);
                float abs     = useAbs ? absorptionController.SampleAbsorption(screenT) * absorptionStrength : 0f;
                _display.Add(Mathf.Clamp01(ApplyAbsorption(flux, abs)));
            }
        }

        if (_display.Count < 2) return;

        if (canvasGraph != null) canvasGraph.SetValues(_display, 0f, 1f);
        if (imageGraph  != null) imageGraph.SetValues(_display,  0f, 1f);
    }

    float ApplyAbsorption(float template, float absorption)
    {
        if (absorption <= 0f) return template;
        return useDramaticSubtraction
            ? Mathf.Max(0f, template - absorption)
            : template * (1f - Mathf.Clamp01(absorption));
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        fallbackRedshiftRate  = Mathf.Max(0f, fallbackRedshiftRate);
        proceduralSampleCount = Mathf.Max(64, proceduralSampleCount);
        csvWindowSize         = Mathf.Max(2, csvWindowSize);
    }
#endif
}

using UnityEngine;

/// <summary>
/// Manages the Lyman-alpha absorption line system.
///
/// Two modes:
///   TriggerBox — lines are stamped in real-time by LymanAlphaAbsorptionTrigger volumes.
///   FakeForest — a procedural forest is pre-generated at startup using the same
///                two-Gaussian approach as FakeLymanAlphaForestHUD.
///
/// In both modes, all lines drift toward IR each frame (redshift).
/// Switch modes at runtime via the Mode dropdown or SetMode().
///
/// Attach to any persistent GameObject (e.g. GameManager).
///
/// SETUP:
/// 1. Attach to GameManager.
/// 2. Assign PhotonSpectrumTrail reference (or leave blank for auto-find).
/// 3. Optionally assign UniverseJourneyTracker for automatic drift speed.
/// </summary>
public class LymanAlphaAbsorptionController : MonoBehaviour
{
    // ── Mode ──────────────────────────────────────────────────────────────────

    public enum AbsorptionMode
    {
        TriggerBox,  // Real-time lines stamped by trigger volumes
        FakeForest   // Procedural forest pre-generated at startup
    }

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Mode")]
    [Tooltip("TriggerBox: lines stamped by LymanAlphaAbsorptionTrigger volumes in the scene.\n" +
             "FakeForest: procedural forest generated at startup for testing/dense zones.")]
    [SerializeField] AbsorptionMode mode = AbsorptionMode.TriggerBox;

    [Header("References")]
    [Tooltip("Auto-found from PhotonSpectrumTrail in scene if not assigned.")]
    [SerializeField] PhotonSpectrumTrail spectrumTrail;
    [Tooltip("Optional — if assigned, drift speed scales with journey redshift.")]
    [SerializeField] UniverseJourneyTracker journeyTracker;

    [Header("Absorption Line Settings (TriggerBox mode)")]
    [Tooltip("UV position (0-1) where new trigger lines appear. 0 = UV end of spectrum.")]
    [Range(0f, 0.3f)]
    [SerializeField] float spawnPositionUV = 0.05f;
    [Tooltip("Width of each absorption line in texture pixels.")]
    [Range(1, 12)]
    [SerializeField] int lineWidthPixels = 3;

    [Header("Fake Forest Settings (FakeForest mode)")]
    [Tooltip("How many absorption dips to generate.")]
    [Range(0, 300)]
    [SerializeField] int fakeDipCount = 120;
    [Tooltip("Random seed — same seed always produces the same forest.")]
    [SerializeField] int fakeRandomSeed = 42;
    [Tooltip("Minimum dip depth (darkness).")]
    [Range(0f, 1f)]
    [SerializeField] float fakeDepthMin = 0.08f;
    [Tooltip("Maximum dip depth (darkness).")]
    [Range(0f, 1f)]
    [SerializeField] float fakeDepthMax = 0.90f;
    [Tooltip("Minimum dip width in pixels.")]
    [Range(1, 12)]
    [SerializeField] int fakeWidthMinPixels = 2;
    [Tooltip("Maximum dip width in pixels.")]
    [Range(1, 16)]
    [SerializeField] int fakeWidthMaxPixels = 6;
    [Tooltip("How many new lines appear per second at the UV end.")]
    [Range(0.1f, 20f)]
    [SerializeField] float fakeSpawnRate = 2f;

    [Header("Redshift Drift")]
    [Range(0f, 0.05f)]
    [SerializeField] float manualDriftSpeed = 0.002f;
    [Range(0f, 10f)]
    [SerializeField] float driftSpeedScale = 1f;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    // ── Private ───────────────────────────────────────────────────────────────

    const int TexWidth = 256;

    float[]   _absorptionData;
    Texture2D _absorptionTex;
    Color[]   _texPixels;
    float     _driftAccumulator;
    AbsorptionMode _activeMode;

    // Fake forest — pending dips queued for gradual release
    struct PendingDip { public float depth; public int widthPixels; }
    System.Collections.Generic.Queue<PendingDip> _pendingDips
        = new System.Collections.Generic.Queue<PendingDip>();
    float _spawnTimer;

    /// <summary>Current active mode — readable by other scripts.</summary>
    public AbsorptionMode CurrentMode => _activeMode;

    static readonly int AbsorptionLineTexProp = Shader.PropertyToID("_AbsorptionLineTex");
    static readonly int UseAbsorptionLineProp  = Shader.PropertyToID("_UseAbsorptionLine");

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        if (spectrumTrail == null)
            spectrumTrail = FindObjectOfType<PhotonSpectrumTrail>();
        if (journeyTracker == null)
            journeyTracker = FindObjectOfType<UniverseJourneyTracker>();

        _absorptionData = new float[TexWidth];
        _texPixels      = new Color[TexWidth];

        _absorptionTex = new Texture2D(TexWidth, 1, TextureFormat.RFloat, false)
        {
            wrapMode   = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name       = "LyaAbsorptionTex"
        };

        UploadTexture();
    }

    void Start()
    {
        ApplyToMaterial();
        SetMode(mode); // apply initial mode
    }

    void OnDestroy()
    {
        if (_absorptionTex != null) Destroy(_absorptionTex);
    }

    void Update()
    {
        // Release pending fake dips one by one at spawn rate
        if (_activeMode == AbsorptionMode.FakeForest && _pendingDips.Count > 0)
        {
            _spawnTimer += Time.deltaTime;
            float interval = 1f / Mathf.Max(fakeSpawnRate, 0.01f);
            while (_spawnTimer >= interval && _pendingDips.Count > 0)
            {
                var dip = _pendingDips.Dequeue();
                StampLine(spawnPositionUV, dip.depth, dip.widthPixels);
                _spawnTimer -= interval;
            }
        }

        float drift = GetDriftSpeed() * Time.deltaTime;
        DriftLines(drift);
        UploadTexture();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Switch between TriggerBox and FakeForest modes at runtime.
    /// Clears current lines and rebuilds if switching to FakeForest.
    /// </summary>
    public void SetMode(AbsorptionMode newMode)
    {
        _activeMode = newMode;
        mode        = newMode; // keep inspector in sync

        ClearAllLines();

        if (newMode == AbsorptionMode.FakeForest)
            PopulateFakeForest();

        if (debugLog)
            Debug.Log($"[LyaAbsorption] Mode set to {newMode}.");
    }

    /// <summary>
    /// Convenience toggles for use from UI buttons or other scripts.
    /// </summary>
    public void SetModeTriggerBox()  => SetMode(AbsorptionMode.TriggerBox);
    public void SetModeFakeForest()  => SetMode(AbsorptionMode.FakeForest);
    public void ToggleMode()         => SetMode(_activeMode == AbsorptionMode.TriggerBox
                                                    ? AbsorptionMode.FakeForest
                                                    : AbsorptionMode.TriggerBox);

    /// <summary>
    /// Stamp a new absorption line at the UV end.
    /// Only has an effect in TriggerBox mode — ignored in FakeForest mode.
    /// Called by LymanAlphaAbsorptionTrigger on player enter.
    /// </summary>
    public void AddAbsorptionLine(float intensity)
    {
        if (_activeMode == AbsorptionMode.FakeForest)
        {
            if (debugLog)
                Debug.Log("[LyaAbsorption] Ignoring trigger stamp — currently in FakeForest mode.");
            return;
        }

        StampLine(spawnPositionUV, intensity, lineWidthPixels);

        if (debugLog)
            Debug.Log($"[LyaAbsorption] Line stamped at UV {spawnPositionUV:F2}, intensity {intensity:F2}");
    }

    /// <summary>Clear all absorption lines and reset the texture.</summary>
    public void ClearAllLines()
    {
        for (int i = 0; i < TexWidth; i++)
            _absorptionData[i] = 0f;
        _driftAccumulator = 0f;
        _pendingDips.Clear();
        _spawnTimer = 0f;
        UploadTexture();
    }

    /// <summary>
    /// Regenerate the fake forest with current inspector settings.
    /// Can be called from inspector context menu or at runtime.
    /// </summary>
    [ContextMenu("Regenerate Fake Forest")]
    public void PopulateFakeForest()
    {
        ClearAllLines();
        _pendingDips.Clear();
        _spawnTimer = 0f;

        var rng = new System.Random(fakeRandomSeed);

        float depthMin = Mathf.Min(fakeDepthMin, fakeDepthMax);
        float depthMax = Mathf.Max(fakeDepthMin, fakeDepthMax);

        for (int d = 0; d < fakeDipCount; d++)
        {
            float depth = Mathf.Lerp(depthMin, depthMax,
                              Mathf.Pow((float)rng.NextDouble(), 1.25f));
            int width = Mathf.RoundToInt(Mathf.Lerp(
                              fakeWidthMinPixels, fakeWidthMaxPixels,
                              (float)rng.NextDouble()));

            _pendingDips.Enqueue(new PendingDip { depth = depth, widthPixels = width });
        }

        if (debugLog)
            Debug.Log($"[LyaAbsorption] Fake forest queued — {fakeDipCount} dips at " +
                      $"{fakeSpawnRate}/sec, seed {fakeRandomSeed}.");
    }

    // ── Internal ─────────────────────────────────────────────────────────────

    void StampLine(float uvPosition, float intensity, int widthPixels)
    {
        int centerPixel = Mathf.RoundToInt(uvPosition * (TexWidth - 1));
        intensity = Mathf.Clamp01(intensity);
        int halfWidth = widthPixels / 2;

        for (int offset = -halfWidth; offset <= halfWidth; offset++)
        {
            int pixel = centerPixel + offset;
            if (pixel < 0 || pixel >= TexWidth) continue;
            float falloff = 1f - Mathf.Abs(offset) / (float)(halfWidth + 1);
            falloff = falloff * falloff;
            _absorptionData[pixel] = Mathf.Max(_absorptionData[pixel], intensity * falloff);
        }
    }

    void ApplyToMaterial()
    {
        if (spectrumTrail == null) return;
        var trail = spectrumTrail.GetComponent<TrailRenderer>();
        if (trail == null) return;
        Material mat = Application.isPlaying ? trail.material : trail.sharedMaterial;
        if (mat == null) return;
        mat.SetTexture(AbsorptionLineTexProp, _absorptionTex);
        mat.SetFloat(UseAbsorptionLineProp, 1f);
    }

    float GetDriftSpeed()
    {
        if (journeyTracker == null) return manualDriftSpeed * driftSpeedScale;

        int phaseIndex = (int)journeyTracker.CurrentPhase;
        var phases = journeyTracker.Phases;
        if (phases == null || phaseIndex >= phases.Length)
            return manualDriftSpeed * driftSpeedScale;

        double lyPerUnit = phases[phaseIndex].lyPerUnityUnit;
        double refScale  = 3e7;
        float scaledRate = (float)(lyPerUnit / refScale) * manualDriftSpeed;
        return scaledRate * driftSpeedScale;
    }

    void DriftLines(float deltaUV)
    {
        _driftAccumulator += deltaUV * (TexWidth - 1);
        int pixelShift = Mathf.FloorToInt(_driftAccumulator);
        if (pixelShift < 1) return;
        _driftAccumulator -= pixelShift;

        for (int i = TexWidth - 1; i >= pixelShift; i--)
            _absorptionData[i] = _absorptionData[i - pixelShift];
        for (int i = 0; i < pixelShift && i < TexWidth; i++)
            _absorptionData[i] = 0f;
    }

    void UploadTexture()
    {
        for (int i = 0; i < TexWidth; i++)
            _texPixels[i] = new Color(_absorptionData[i], 0f, 0f, 1f);
        _absorptionTex.SetPixels(_texPixels);
        _absorptionTex.Apply(false);
    }
}

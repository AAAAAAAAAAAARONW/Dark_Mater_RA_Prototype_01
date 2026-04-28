using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central absorption-field controller. Single source of truth shared by:
///   • PhotonSpectrumTrail shader (via the _AbsorptionLineTex texture)
///   • LAFSpectrumHUD          (via SampleAbsorption(t))
///
/// Background Lyman-α forest and trigger-driven dramatic events run
/// simultaneously and stamp into the same buffer. New lines spawn with a
/// short "bold pulse" (extra depth+width that decays in ~0.5s) so the player
/// immediately notices them; the line then settles to its target shape.
///
/// Drift speed is proportional to player speed — same coupling as
/// LAFSpectrumHUD so the two displays stay in sync.
/// </summary>
public class LymanAlphaAbsorptionController : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("Auto-found from PhotonSpectrumTrail in scene if not assigned.")]
    [SerializeField] PhotonSpectrumTrail spectrumTrail;
    [Tooltip("Optional. Speed drives the drift rate and background spawn rate.")]
    [SerializeField] DarkMatterPlayerControllerTest playerController;

    [Header("Background Forest (UV / forest region)")]
    [Tooltip("Master enable for procedural background forest generation.")]
    [SerializeField] bool backgroundEnabled = true;
    [Tooltip("Lines per second at the reference player speed.")]
    [SerializeField] float backgroundBaseRate = 6f;
    [Tooltip("Fraction of base rate that still runs when player is fully stopped (0 = freezes, 1 = always full speed).")]
    [Range(0f, 1f)]
    [SerializeField] float backgroundIdleRate = 0.2f;
    [Tooltip("Spawn position range in normalised buffer position [0..1]. " +
             "Lines distribute uniformly across this range — covers the forest region " +
             "(Ly-limit to just before Ly-α). Default 0.05–0.40 fits an 800-1800Å view with Ly-α at ~0.42.")]
    [SerializeField] Vector2 backgroundSpawnRangeUV = new Vector2(0.05f, 0.40f);
    [Tooltip("Background-line depth range. Higher = darker forest.")]
    [SerializeField] Vector2 backgroundDepthRange = new Vector2(0.20f, 0.65f);
    [Tooltip("Background-line width range in pixels.")]
    [SerializeField] Vector2Int backgroundWidthPixels = new Vector2Int(1, 3);

    [Header("Initial Population")]
    [Tooltip("Spawn a pre-existing forest at startup so the HUD isn't empty for the first few seconds.")]
    [SerializeField] bool prePopulateForest = true;
    [Tooltip("How many lines to pre-spawn across the forest range at game start.")]
    [Range(0, 200)]
    [SerializeField] int prePopulateLineCount = 80;

    [Header("Drift (Redshift)")]
    [Tooltip("Drift in normalised UV-units per second at reference player speed. " +
             "Lower = slower whole-spectrum drift. The HUD reads this same rate to stay locked in step.")]
    [SerializeField] float driftBaseRate = 0.005f;

    [Header("Player Speed Coupling")]
    [SerializeField] bool linkToPlayerSpeed = true;
    [Tooltip("Player speed at which all rates equal their base values above.")]
    [SerializeField] float referencePlayerSpeed = 20f;
    [Tooltip("Cap on the speed-driven multiplier so extreme speeds don't blow rates up.")]
    [SerializeField] float maxSpeedMultiplier = 4f;

    [Header("Spawn Pulse (newly-stamped lines start bold)")]
    [Tooltip("Extra depth at spawn time (added on top of the target depth via overlay).")]
    [Range(0f, 1f)]
    [SerializeField] float pulseExtraDepth = 0.5f;
    [Tooltip("Extra width multiplier at spawn time. 1 = same width, 2 = twice as wide.")]
    [Range(1f, 3f)]
    [SerializeField] float pulseWidthMultiplier = 1.6f;
    [Tooltip("How long the pulse takes to fade. The line keeps existing afterward at its target shape.")]
    [Range(0.1f, 2f)]
    [SerializeField] float pulseDuration = 0.5f;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Texture width — also the length of the absorption sample array.</summary>
    public const int TexWidth = 256;

    /// <summary>Current drift rate in normalised UV-units per second. HUD reads this so the
    /// spectrum redshift stays locked to the absorption drift.</summary>
    public float CurrentDriftPerSecond
    {
        get
        {
            float speedFactor = GetSpeedFactor();
            float idleScale   = backgroundIdleRate + (1f - backgroundIdleRate) * speedFactor;
            return driftBaseRate * idleScale;
        }
    }

    /// <summary>Sample the current absorption at a normalised UV→IR position [0..1]. Linear filter.</summary>
    public float SampleAbsorption(float t)
    {
        if (_finalAbsorption == null) return 0f;
        t = Mathf.Clamp01(t);
        float fp = t * (TexWidth - 1);
        int lo = Mathf.FloorToInt(fp);
        int hi = Mathf.Min(lo + 1, TexWidth - 1);
        float frac = fp - lo;
        return Mathf.Lerp(_finalAbsorption[lo], _finalAbsorption[hi], frac);
    }

    /// <summary>
    /// Stamp a new absorption line. Used by triggers AND the background spawner.
    /// uvPosition is normalised [0..1] (0 = UV end, 1 = IR end).
    /// addPulse=false skips the spawn-pulse highlight (used for pre-population).
    /// </summary>
    public void StampLine(float uvPosition, float depth, int widthPixels, bool addPulse = true)
    {
        depth        = Mathf.Clamp01(depth);
        widthPixels  = Mathf.Clamp(widthPixels, 1, TexWidth / 4);
        int center   = Mathf.Clamp(Mathf.RoundToInt(uvPosition * (TexWidth - 1)), 0, TexWidth - 1);

        // Permanent contribution baked into the main buffer.
        StampIntoMain(center, depth, widthPixels);

        if (addPulse)
        {
            _pulses.Add(new Pulse
            {
                center      = center,
                extraDepth  = pulseExtraDepth * depth,
                extraWidth  = Mathf.RoundToInt(widthPixels * pulseWidthMultiplier),
                spawnTime   = Time.time,
                duration    = pulseDuration
            });
        }
    }

    /// <summary>Legacy single-line API kept for compatibility. Uses default background width.</summary>
    public void AddAbsorptionLine(float intensity)
    {
        int w = Mathf.Max(1, (backgroundWidthPixels.x + backgroundWidthPixels.y) / 2);
        StampLine(backgroundSpawnRangeUV.x, intensity, w);
    }

    /// <summary>Clear all current absorption (does not stop background spawning).</summary>
    public void ClearAllLines()
    {
        if (_mainAbsorption == null) return;
        for (int i = 0; i < TexWidth; i++) _mainAbsorption[i] = 0f;
        _pulses.Clear();
        _driftAccum = 0f;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    struct Pulse
    {
        public int   center;
        public float extraDepth;
        public int   extraWidth;
        public float spawnTime;
        public float duration;
    }

    float[]   _mainAbsorption;     // permanent baked absorption
    float[]   _finalAbsorption;    // _mainAbsorption + active pulses, what gets uploaded/sampled
    Color[]   _texPixels;
    Texture2D _absorptionTex;
    readonly List<Pulse> _pulses = new List<Pulse>();

    float _driftAccum;
    float _backgroundSpawnAccum;
    System.Random _backgroundRng;

    static readonly int AbsorptionLineTexProp = Shader.PropertyToID("_AbsorptionLineTex");
    static readonly int UseAbsorptionLineProp = Shader.PropertyToID("_UseAbsorptionLine");

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        if (spectrumTrail    == null) spectrumTrail    = FindObjectOfType<PhotonSpectrumTrail>();
        if (playerController == null) playerController = FindObjectOfType<DarkMatterPlayerControllerTest>();

        _mainAbsorption  = new float[TexWidth];
        _finalAbsorption = new float[TexWidth];
        _texPixels       = new Color[TexWidth];

        _absorptionTex = new Texture2D(TexWidth, 1, TextureFormat.RFloat, false)
        {
            wrapMode   = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name       = "LyaAbsorptionTex"
        };

        _backgroundRng = new System.Random(System.Environment.TickCount);
        UploadTexture();
    }

    void Start()
    {
        ApplyToMaterial();

        if (prePopulateForest && prePopulateLineCount > 0)
            PrePopulateForest();
    }

    void PrePopulateForest()
    {
        for (int i = 0; i < prePopulateLineCount; i++)
        {
            float pos   = Mathf.Lerp(backgroundSpawnRangeUV.x, backgroundSpawnRangeUV.y,
                              (float)_backgroundRng.NextDouble());
            float depth = Mathf.Lerp(backgroundDepthRange.x, backgroundDepthRange.y,
                              (float)_backgroundRng.NextDouble());
            int range   = Mathf.Max(1, backgroundWidthPixels.y - backgroundWidthPixels.x + 1);
            int width   = Mathf.Clamp(
                              backgroundWidthPixels.x + (int)(_backgroundRng.NextDouble() * range),
                              1, TexWidth / 4);
            StampLine(pos, depth, width, addPulse: false);
        }
        ComposeFinal();
        UploadTexture();
    }

    void OnDestroy()
    {
        if (_absorptionTex != null) Destroy(_absorptionTex);
    }

    void Update()
    {
        float speedFactor = GetSpeedFactor();
        float idleScale   = backgroundIdleRate + (1f - backgroundIdleRate) * speedFactor;
        float dt          = Time.deltaTime;

        // 1) Background spawn (UV end only, slows but never freezes)
        if (backgroundEnabled && backgroundBaseRate > 0f)
        {
            float spawnRate = backgroundBaseRate * idleScale;
            _backgroundSpawnAccum += spawnRate * dt;
            while (_backgroundSpawnAccum >= 1f)
            {
                _backgroundSpawnAccum -= 1f;
                SpawnBackgroundLine();
            }
        }

        // 2) Drift the main buffer (uses the SAME idle-scaled rate as spawn,
        //    so lines never pile up at the spawn band when the player is idle).
        float drift = driftBaseRate * idleScale * dt;
        DriftMain(drift);

        // 3) Compose final = main + active pulses
        ComposeFinal();

        // 4) Push to GPU
        UploadTexture();
    }

    // ── Internal ─────────────────────────────────────────────────────────────

    float GetSpeedFactor()
    {
        if (!linkToPlayerSpeed || playerController == null || referencePlayerSpeed <= 0.0001f)
            return 1f;
        float f = playerController.Speed / referencePlayerSpeed;
        if (f < 0f) f = 0f;
        if (f > maxSpeedMultiplier) f = maxSpeedMultiplier;
        return f;
    }

    void SpawnBackgroundLine()
    {
        // Distribute spawn position uniformly across the forest range so lines
        // don't pile up at one column and saturate to a solid black band.
        float pos = Mathf.Lerp(backgroundSpawnRangeUV.x, backgroundSpawnRangeUV.y,
                        (float)_backgroundRng.NextDouble());

        float depth = Mathf.Lerp(backgroundDepthRange.x, backgroundDepthRange.y,
                          (float)_backgroundRng.NextDouble());
        int range   = Mathf.Max(1, backgroundWidthPixels.y - backgroundWidthPixels.x + 1);
        int width   = Mathf.Clamp(
                          backgroundWidthPixels.x + (int)(_backgroundRng.NextDouble() * range),
                          1, TexWidth / 4);

        StampLine(pos, depth, width);
    }

    void StampIntoMain(int center, float depth, int widthPixels)
    {
        int half = widthPixels / 2;
        for (int o = -half; o <= half; o++)
        {
            int p = center + o;
            if (p < 0 || p >= TexWidth) continue;
            float falloff = 1f - Mathf.Abs(o) / (float)(half + 1);
            falloff *= falloff;
            _mainAbsorption[p] = Mathf.Min(1f, _mainAbsorption[p] + depth * falloff);
        }
    }

    void DriftMain(float deltaUV)
    {
        _driftAccum += deltaUV * (TexWidth - 1);
        int shift = Mathf.FloorToInt(_driftAccum);
        if (shift < 1) return;
        _driftAccum -= shift;

        // Drift main buffer pixels right (toward IR).
        for (int i = TexWidth - 1; i >= shift; i--)
            _mainAbsorption[i] = _mainAbsorption[i - shift];
        for (int i = 0; i < shift && i < TexWidth; i++)
            _mainAbsorption[i] = 0f;

        // Drift pulse centers along with their host lines.
        for (int i = 0; i < _pulses.Count; i++)
        {
            var p = _pulses[i];
            p.center += shift;
            _pulses[i] = p;
        }
    }

    void ComposeFinal()
    {
        for (int i = 0; i < TexWidth; i++)
            _finalAbsorption[i] = _mainAbsorption[i];

        float now = Time.time;
        for (int i = _pulses.Count - 1; i >= 0; i--)
        {
            var p     = _pulses[i];
            float age = now - p.spawnTime;
            if (age >= p.duration || p.center < 0 || p.center >= TexWidth)
            {
                _pulses.RemoveAt(i);
                continue;
            }
            // Ease-out: k goes 1 → 0 over duration, squared for sharper decay.
            float k = 1f - Mathf.Clamp01(age / p.duration);
            k = k * k;

            int half = p.extraWidth / 2;
            for (int o = -half; o <= half; o++)
            {
                int idx = p.center + o;
                if (idx < 0 || idx >= TexWidth) continue;
                float falloff = 1f - Mathf.Abs(o) / (float)(half + 1);
                falloff *= falloff;
                _finalAbsorption[idx] = Mathf.Min(1f,
                    _finalAbsorption[idx] + p.extraDepth * falloff * k);
            }
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

    void UploadTexture()
    {
        for (int i = 0; i < TexWidth; i++)
            _texPixels[i] = new Color(_finalAbsorption[i], 0f, 0f, 1f);
        _absorptionTex.SetPixels(_texPixels);
        _absorptionTex.Apply(false);
    }
}

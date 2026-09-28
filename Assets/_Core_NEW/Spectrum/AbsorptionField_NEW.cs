using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The absorption buffer shared by the photon trail shader and the spectrum HUD.
///
/// Replaces LymanAlphaAbsorptionController. The line-stamping, drift and pulse maths are
/// a faithful port; three things changed.
///
/// RESOLUTION IS NO LONGER A CONSTANT. The original hard-coded `public const int
/// TexWidth = 256`, which capped how fine an absorption line could be — and the layer
/// profiles asked for lines 0.0004 and 0.004 of the spectrum wide, which at 256 texels
/// are 0.10 and 1.02 pixels. Both clamped to one texel, so the entire authored width
/// range collapsed to a single value and every line came out identical. The HUD then
/// sampled that 256-entry buffer at 1024 points, so the display was four times finer
/// than the data behind it. Resolution is now serialized and defaults to 2048.
///
/// WIDTHS ARE NORMALISED, NOT PIXELS. The original expressed background line width in
/// texels, so raising the resolution would have made every line thinner. Widths are now
/// fractions of the spectrum: a line keeps its apparent width at any resolution, and a
/// higher resolution buys a smoother falloff instead of a narrower line.
///
/// PARAMETERS COME FROM A PROFILE. Configure() accepts a SpectrumProfile_NEW so each
/// layer can have its own forest density, depth and seed — the eight dip fields that
/// existed on the old layer asset but reached no consumer.
///
/// The texture format, filter mode, shader property names and per-frame upload are
/// unchanged, so the trail shader sees exactly the same kind of data it always did.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("ABSORB", "#E09E38")]
public class AbsorptionField_NEW : MonoBehaviour
{
    [Header("Resolution")]
    [Tooltip("Texels across the spectrum. The original was fixed at 256, which was too " +
             "coarse for the authored line widths. 2048 costs 8 KB and a 2048-element " +
             "loop per frame — negligible — and gives lines a real width range.")]
    [Range(256, 8192)]
    [SerializeField] int resolution = 2048;

    [Header("Target")]
    [Tooltip("TrailRenderer whose material receives the absorption texture. " +
             "Assign it — there is no auto-find, deliberately: reaching for a type in the " +
             "old stack just to locate a component was the only thing tying this file to it.")]
    [SerializeField] TrailRenderer targetTrail;

    [Header("Player speed coupling")]
    [SerializeField] bool linkToPlayerSpeed = true;
    [SerializeField] PlayerRig_NEW player;

    [Tooltip("Player speed at which the rates equal their authored values. " +
             "Must match the player's actual range — the old value of 20 was left over " +
             "from a build where max speed was 20, and with today's max of 3.5 it pinned " +
             "the multiplier at about 0.125, effectively disabling the coupling.")]
    [SerializeField] float referencePlayerSpeed = 3.5f;

    [Tooltip("Cap so extreme speeds cannot run the rates away.")]
    [SerializeField] float maxSpeedMultiplier = 4f;

    [Tooltip("Fraction of the rate that still runs when the player is stopped.")]
    [Range(0f, 1f)]
    [SerializeField] float idleRate = 0.2f;

    [Header("Spawn pulse (new lines start bold, then settle)")]
    [Range(0f, 1f)] [SerializeField] float pulseExtraDepth = 0.5f;
    [Range(1f, 3f)] [SerializeField] float pulseWidthMultiplier = 1.6f;
    [Range(0.1f, 2f)] [SerializeField] float pulseDuration = 0.5f;

    [Header("Fallback profile")]
    [Tooltip("Used before any layer configures this field.")]
    [SerializeField] SpectrumProfile_NEW defaultProfile;

    [Header("Drift budget")]
    [Tooltip("Multiplier on the active profile's driftPerSecond. See DriftScale.\n\n" +
             "1 is the rate the profiles ship, which scrolls the whole spectrum past in " +
             "about two minutes. Leave this alone and let RedshiftBudget_NEW set it from " +
             "a journey length.")]
    [Range(0f, 2f)]
    [SerializeField] float _driftScale = 1f;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    // ── State ────────────────────────────────────────────────────────────────

    struct Pulse
    {
        public int center;
        public float extraDepth;
        public int extraWidth;
        public float spawnTime;
        public float duration;
    }

    float[] _main;      // permanent absorption
    float[] _final;     // _main plus live pulses — what is sampled and uploaded
    Color[] _pixels;
    Texture2D _tex;
    readonly List<Pulse> _pulses = new List<Pulse>();

    // An externally supplied absorption row, used instead of spawning and drifting.
    // Owned by the caller; see SetExternalAbsorption.
    float[] _external;
    int _externalCount;
    bool _externalDirty;

    float _driftAccum;
    float _spawnAccum;
    System.Random _rng;
    SpectrumProfile_NEW _profile;

    static readonly int AbsorptionLineTexProp = Shader.PropertyToID("_AbsorptionLineTex");
    static readonly int UseAbsorptionLineProp = Shader.PropertyToID("_UseAbsorptionLine");

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Texels across the spectrum.</summary>
    public int Resolution => resolution;

    /// <summary>
    /// Current drift in normalised units per second. The HUD reads this so the emission
    /// template and the absorption lines scroll as one.
    /// </summary>
    public float CurrentDriftPerSecond => Drift * IdleScale * _driftScale;

    /// <summary>
    /// A multiplier on every profile's drift, so the whole journey's redshift can be
    /// budgeted in one place without editing seven SpectrumProfile assets.
    ///
    /// WHY IT WAS NEEDED. The profiles ship 0.00778 normalised units per second, and the
    /// spectrum is one unit wide — so the whole thing scrolls past in 128 seconds, and a
    /// journey several times that length wraps round more than once. Nothing about the
    /// picture is wrong at that rate; it is simply far too fast to read as "space
    /// stretching over thirteen billion years", and it makes the anchor-and-gap reading
    /// in RedshiftMarks_NEW impossible, because the gap resets to zero every wrap.
    ///
    /// Set by RedshiftBudget_NEW from a journey length rather than by hand, so the
    /// number stays tied to a statement somebody can check.
    /// </summary>
    public float DriftScale
    {
        get { return _driftScale; }
        set { _driftScale = Mathf.Max(0f, value); }
    }

    float Drift => _profile != null ? _profile.driftPerSecond : 0.005f;
    float IdleScale => idleRate + (1f - idleRate) * SpeedFactor;

    /// <summary>Absorption at a normalised position, linearly filtered.</summary>
    public float SampleAbsorption(float t)
    {
        if (_final == null) return 0f;

        t = Mathf.Clamp01(t);
        float fp = t * (resolution - 1);
        int lo = Mathf.FloorToInt(fp);
        int hi = Mathf.Min(lo + 1, resolution - 1);
        return Mathf.Lerp(_final[lo], _final[hi], fp - lo);
    }

    /// <summary>
    /// Adopt a layer's forest settings. Existing lines are left alone so the forest
    /// evolves rather than snapping; call ClearLines() first for a hard reset.
    /// </summary>
    public void Configure(SpectrumProfile_NEW profile)
    {
        if (profile == null) return;

        bool seedChanged = _profile == null || _profile.randomSeed != profile.randomSeed;
        _profile = profile;

        if (seedChanged) _rng = new System.Random(profile.randomSeed);

        if (debugLog)
            Debug.Log($"[AbsorptionField_NEW] Configured: {profile.name} — " +
                      $"width {profile.dipWidthMin:F4}-{profile.dipWidthMax:F4} " +
                      $"({profile.dipWidthMin * resolution:F1}-{profile.dipWidthMax * resolution:F1} texels), " +
                      $"depth {profile.dipDepthMin:F2}-{profile.dipDepthMax:F2}.", this);
    }

    /// <summary>Stamp a line. uvPosition and widthUV are fractions of the spectrum.</summary>
    public void StampLine(float uvPosition, float depth, float widthUV, bool addPulse = true)
    {
        depth = Mathf.Clamp01(depth);

        int widthTexels = Mathf.Clamp(Mathf.RoundToInt(widthUV * resolution), 1, resolution / 4);
        int center = Mathf.Clamp(Mathf.RoundToInt(uvPosition * (resolution - 1)), 0, resolution - 1);

        StampIntoMain(center, depth, widthTexels);

        if (!addPulse) return;

        _pulses.Add(new Pulse
        {
            center = center,
            extraDepth = pulseExtraDepth * depth,
            extraWidth = Mathf.RoundToInt(widthTexels * pulseWidthMultiplier),
            spawnTime = Time.time,
            duration = pulseDuration
        });
    }

    /// <summary>
    /// Use this absorption (0 = clear, 1 = fully absorbed) instead of spawning and
    /// drifting lines. Evenly spaced across the spectrum; resampled to the resolution.
    ///
    /// A HOOK, NOT A DEPENDENCY — the supplier calls in, this class never names it, and
    /// with nothing calling it the field behaves exactly as before.
    ///
    /// It is also the cheap path, on purpose. While external absorption is set, this
    /// component stops spawning, stops drifting, and uploads its texture ONLY when the
    /// supplier says the row changed (MarkExternalAbsorptionDirty) — instead of a
    /// 2048-texel SetPixels and Apply every frame, which is what the procedural path
    /// does.
    /// </summary>
    public void SetExternalAbsorption(float[] values, int count)
    {
        _external = values;
        _externalCount = values != null ? Mathf.Clamp(count, 0, values.Length) : 0;
        _externalDirty = true;
    }

    /// <summary>Go back to spawning and drifting procedurally.</summary>
    public void ClearExternalAbsorption()
    {
        _external = null;
        _externalCount = 0;
    }

    /// <summary>The external row was refilled in place; resample and upload it next Update.</summary>
    public void MarkExternalAbsorptionDirty()
    {
        _externalDirty = true;
    }

    /// <summary>True while external absorption is in use.</summary>
    public bool HasExternalAbsorption { get { return _external != null; } }

    /// <summary>Wipe all absorption. Background spawning continues.</summary>
    public void ClearLines()
    {
        if (_main == null) return;
        for (int i = 0; i < resolution; i++) _main[i] = 0f;
        _pulses.Clear();
        _driftAccum = 0f;
    }

    /// <summary>Fill the forest immediately, using the active profile's line count.</summary>
    public void PrePopulate()
    {
        if (_profile == null || _profile.initialLineCount <= 0) return;

        for (int i = 0; i < _profile.initialLineCount; i++) SpawnLine(addPulse: false);

        ComposeFinal();
        Upload();
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (player == null) player = FindObjectOfType<PlayerRig_NEW>();

        if (targetTrail == null)
            Debug.LogWarning("[AbsorptionField_NEW] No TrailRenderer found. The HUD will still " +
                             "work; the photon trail will not show absorption lines.", this);

        Allocate();

        _profile = defaultProfile;
        _rng = new System.Random(_profile != null ? _profile.randomSeed : System.Environment.TickCount);

        Upload();
    }

    void Start()
    {
        ApplyToMaterial();
        PrePopulate();
    }

    void OnDestroy()
    {
        if (_tex != null) Destroy(_tex);
    }

    void Allocate()
    {
        resolution = Mathf.Clamp(resolution, 256, 8192);

        _main = new float[resolution];
        _final = new float[resolution];
        _pixels = new Color[resolution];

        _tex = new Texture2D(resolution, 1, TextureFormat.RFloat, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "AbsorptionField"
        };
    }

    void Update()
    {
        if (_external != null)
        {
            // Nothing spawns and nothing drifts: the supplier owns where every line is.
            // Upload only on change — see SetExternalAbsorption.
            if (_externalDirty)
            {
                ComposeExternal();
                Upload();
                _externalDirty = false;
            }

            return;
        }

        if (_profile == null) return;

        float dt = Time.deltaTime;
        float idleScale = IdleScale;

        // 1 · spawn
        if (_profile.spawnRatePerSecond > 0f)
        {
            _spawnAccum += _profile.spawnRatePerSecond * idleScale * dt;
            while (_spawnAccum >= 1f)
            {
                _spawnAccum -= 1f;
                SpawnLine(addPulse: true);
            }
        }

        // 2 · drift, on the same idle-scaled rate as spawning so lines never pile up.
        //
        // CurrentDriftPerSecond, not the profile's raw rate: that property is what the
        // HUD slides the curve by (and so what RedshiftMarks_NEW measures against), and
        // it includes the budget's DriftScale. Reading the raw rate here moved the lines
        // about ten times faster than the curve they are cut into.
        DriftMain(CurrentDriftPerSecond * dt);

        // 3 · compose and upload
        ComposeFinal();
        Upload();
    }

    // ── Internals ────────────────────────────────────────────────────────────

    float SpeedFactor
    {
        get
        {
            if (!linkToPlayerSpeed || player == null || referencePlayerSpeed <= 0.0001f) return 1f;
            return Mathf.Clamp(player.Speed / referencePlayerSpeed, 0f, maxSpeedMultiplier);
        }
    }

    void SpawnLine(bool addPulse)
    {
        if (_profile == null || _rng == null) return;

        float pos = Mathf.Lerp(_profile.spawnRangeUV.x, _profile.spawnRangeUV.y, (float)_rng.NextDouble());
        float depth = Mathf.Lerp(_profile.dipDepthMin, _profile.dipDepthMax, (float)_rng.NextDouble());
        float width = Mathf.Lerp(_profile.dipWidthMin, _profile.dipWidthMax, (float)_rng.NextDouble());

        StampLine(pos, depth, width, addPulse);
    }

    void StampIntoMain(int center, float depth, int widthTexels)
    {
        int half = widthTexels / 2;
        for (int o = -half; o <= half; o++)
        {
            int p = center + o;
            if (p < 0 || p >= resolution) continue;

            float falloff = 1f - Mathf.Abs(o) / (float)(half + 1);
            falloff *= falloff;
            _main[p] = Mathf.Min(1f, _main[p] + depth * falloff);
        }
    }

    void DriftMain(float deltaUV)
    {
        _driftAccum += deltaUV * (resolution - 1);
        int shift = Mathf.FloorToInt(_driftAccum);
        if (shift < 1) return;
        _driftAccum -= shift;

        // Everything moves toward the red end.
        for (int i = resolution - 1; i >= shift; i--) _main[i] = _main[i - shift];
        for (int i = 0; i < shift && i < resolution; i++) _main[i] = 0f;

        for (int i = 0; i < _pulses.Count; i++)
        {
            Pulse p = _pulses[i];
            p.center += shift;
            _pulses[i] = p;
        }
    }

    void ComposeExternal()
    {
        if (_final == null) return;

        int n = _externalCount;
        if (n < 2)
        {
            System.Array.Clear(_final, 0, resolution);
            return;
        }

        for (int i = 0; i < resolution; i++)
        {
            float fp = i / (float)(resolution - 1) * (n - 1);
            int lo = Mathf.FloorToInt(fp);
            int hi = Mathf.Min(lo + 1, n - 1);
            _final[i] = Mathf.Clamp01(Mathf.Lerp(_external[lo], _external[hi], fp - lo));
        }
    }

    void ComposeFinal()
    {
        System.Array.Copy(_main, _final, resolution);

        float now = Time.time;
        for (int i = _pulses.Count - 1; i >= 0; i--)
        {
            Pulse p = _pulses[i];
            float age = now - p.spawnTime;

            if (age >= p.duration || p.center < 0 || p.center >= resolution)
            {
                _pulses.RemoveAt(i);
                continue;
            }

            // Ease out: 1 → 0, squared for a sharper decay.
            float k = 1f - Mathf.Clamp01(age / p.duration);
            k *= k;

            int half = p.extraWidth / 2;
            for (int o = -half; o <= half; o++)
            {
                int idx = p.center + o;
                if (idx < 0 || idx >= resolution) continue;

                float falloff = 1f - Mathf.Abs(o) / (float)(half + 1);
                falloff *= falloff;
                _final[idx] = Mathf.Min(1f, _final[idx] + p.extraDepth * falloff * k);
            }
        }
    }

    void ApplyToMaterial()
    {
        if (targetTrail == null) return;

        Material mat = Application.isPlaying ? targetTrail.material : targetTrail.sharedMaterial;
        if (mat == null) return;

        if (!mat.HasProperty(AbsorptionLineTexProp))
        {
            Debug.LogWarning($"[AbsorptionField_NEW] '{mat.shader.name}' has no _AbsorptionLineTex " +
                             "property, so absorption lines will not appear on the trail. " +
                             "The trail material needs the PhotonTrailRainbow shader.", this);
            return;
        }

        mat.SetTexture(AbsorptionLineTexProp, _tex);
        mat.SetFloat(UseAbsorptionLineProp, 1f);
    }

    void Upload()
    {
        for (int i = 0; i < resolution; i++)
            _pixels[i] = new Color(_final[i], 0f, 0f, 1f);

        _tex.SetPixels(_pixels);
        _tex.Apply(false);
    }
}

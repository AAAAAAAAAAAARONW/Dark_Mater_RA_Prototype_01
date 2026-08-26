using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Universe Journey Tracker — trigger-driven progress.
///
/// Progress is computed from the player's Z position relative to
/// consecutive layer trigger transforms. No lyPerUnityUnit needed.
///
/// Setup:
///   1. Assign Player Transform.
///   2. Assign Layer Triggers in journey order (Quasar door → CW door →
///      Galaxy door → CW2 door → MilkyWay door → SolarSystem door → Earth).
///      These are the door/plane triggers that already exist in your scene.
///   3. Phase Data array still drives display names and distance labels.
///      lyPerUnityUnit fields are no longer used and can be ignored.
/// </summary>
public class UniverseJourneyTracker : MonoBehaviour
{
    // ─────────────────────────────────────────────
    // PHASE DEFINITIONS
    // ─────────────────────────────────────────────

    public enum JourneyPhase
    {
        Quasar = 0,
        CosmicWeb1 = 1,
        Galaxy = 2,
        CosmicWeb2 = 3,
        MilkyWay = 4,
        SolarSystem = 5,
        Earth = 6,
    }

    [Serializable]
    public class PhaseData
    {
        [Tooltip("Display name shown in HUD.")]
        public string displayName;

        [Tooltip("Remaining distance at which this phase BEGINS (ly). " +
                 "Used only for the distance label — does not drive progress.")]
        public double remainingDistanceAtStart;

        [Tooltip("Remaining distance at which this phase ENDS (ly). " +
                 "Used only for the distance label — does not drive progress.")]
        public double remainingDistanceAtEnd;

        [Tooltip("Used by LymanAlphaAbsorptionController and other visual scripts.\n\n" +
                 "For the Journey HUD distance calculation, the EFFECTIVE ly/unit is\n" +
                 "auto-computed at runtime as:\n" +
                 "  phaseTotalDistanceLy ÷ phaseZSpan\n\n" +
                 "Run [ContextMenu → Debug: Log ly/unit Rates] at Play time to see\n" +
                 "the actual computed values for your scene's trigger layout.")]
        public double lyPerUnityUnit;

        /// <summary>Total ly span of this phase (for HUD label only).</summary>
        public double TotalDistanceLy => remainingDistanceAtStart - remainingDistanceAtEnd;
    }

    // ─────────────────────────────────────────────
    // MILESTONE DEFINITIONS
    // ─────────────────────────────────────────────

    [Serializable]
    public class MilestoneData
    {
        [Tooltip("Short event name shown as the card title.")]
        public string label;

        [Tooltip("One-sentence description shown on the card body.")]
        [TextArea(2, 3)]
        public string description;

        [Tooltip("Optional icon sprite shown in the card icon circle.\n" +
                 "Leave empty to use a solid color circle.")]
        public Sprite icon;

        [Tooltip("Background color of the icon circle on the card.\n" +
                 "Purple = early universe  |  Gold = galactic era  |  Green = life era")]
        public Color iconColor = new Color(0.35f, 0.15f, 0.6f, 1f);

        [Tooltip("Fires when remaining distance (ly) drops BELOW this value.\n" +
                 "This is the journey trigger point — set it to the cosmological\n" +
                 "distance at which you want the card to appear.")]
        public double remainingDistanceThresholdLy;

        [Tooltip("How long ago this event actually occurred (years).\n" +
                 "Displayed on the card as 'X years ago'.\n" +
                 "For events in the last ~5B years this equals the threshold.\n" +
                 "For early-universe events use the actual age (e.g. Big Bang = 13.8e9).")]
        public double yearsAgo;

        [HideInInspector] public bool triggered;
    }

    // ─────────────────────────────────────────────
    // INSPECTOR
    // ─────────────────────────────────────────────

    [Header("Player")]
    [SerializeField] Transform playerTransform;

    [Header("Layer Triggers (6 doors + 1 arrival point = 7 total)")]
    [Tooltip("Do NOT include a Quasar trigger — Quasar is the default start state.\n" +
             "Player Z at Start() is used automatically as Quasar phase start.\n\n" +
             "[0] Cosmic Web door\n" +
             "[1] Galaxy door\n" +
             "[2] Cosmic Web 2 door\n" +
             "[3] Milky Way door\n" +
             "[4] Solar System door\n" +
             "[5] Earth door\n" +
             "[6] Earth arrival point (empty GameObject at end of Earth phase)")]
    [SerializeField] Transform[] layerTriggers;

    [Header("Journey")]
    [Tooltip("Total journey distance in light years (for display only).")]
    [SerializeField] double totalJourneyLy = 13_000_100_024e3;

    [Header("Phase Data (7 entries, one per JourneyPhase)")]
    [SerializeField]
    PhaseData[] phases = new PhaseData[]
    {
        new PhaseData { displayName="Quasar",
            remainingDistanceAtStart=13.0e9,  remainingDistanceAtEnd=11.0e9,
            lyPerUnityUnit=3e7  },
        new PhaseData { displayName="Cosmic Web",
            remainingDistanceAtStart=11.0e9,  remainingDistanceAtEnd=7.0e9,
            lyPerUnityUnit=3e7  },
        new PhaseData { displayName="Galaxy",
            remainingDistanceAtStart=7.0e9,   remainingDistanceAtEnd=6.9999e9,
            lyPerUnityUnit=1e3  },
        new PhaseData { displayName="Cosmic Web",
            remainingDistanceAtStart=6.9999e9,remainingDistanceAtEnd=2.4e4,
            lyPerUnityUnit=6e7  },
        new PhaseData { displayName="Milky Way",
            remainingDistanceAtStart=2.4e4,   remainingDistanceAtEnd=1.0,
            lyPerUnityUnit=240  },
        new PhaseData { displayName="Solar System",
            remainingDistanceAtStart=1.0,     remainingDistanceAtEnd=1e-12,
            lyPerUnityUnit=0.01 },
        new PhaseData { displayName="Earth",
            remainingDistanceAtStart=1e-12,   remainingDistanceAtEnd=0,
            lyPerUnityUnit=1e-14 },
    };

    // ── Color presets (reused across milestones) ─────────────────────────
    static readonly Color _colEarlyUniverse = new Color(0.30f, 0.10f, 0.50f);   // deep purple
    static readonly Color _colGalactic      = new Color(0.48f, 0.33f, 0.08f);   // gold
    static readonly Color _colSolar         = new Color(0.15f, 0.50f, 0.25f);   // green
    static readonly Color _colLife          = new Color(0.10f, 0.45f, 0.35f);   // teal-green

    [Header("Milestones — exposed for timeline tuning")]
    [Tooltip("Milestones fire when RemainingDistanceLy drops BELOW remainingDistanceThresholdLy.\n" +
             "Journey starts at ~13B ly (Quasar). Thresholds above 13B fire at game start.\n\n" +
             "Phase ranges (remaining ly):\n" +
             "  Quasar        13B → 11B\n" +
             "  CosmicWeb1    11B → 7B\n" +
             "  Galaxy        7B  → 6.9999B  (100K ly window)\n" +
             "  CosmicWeb2    6.9999B → 24K\n" +
             "  MilkyWay      24K → 1\n" +
             "  SolarSystem   1   → 1e-12\n" +
             "  Earth         1e-12 → 0")]
    [SerializeField]
    MilestoneData[] milestones = new MilestoneData[]
    {
        // ── PRE-JOURNEY  threshold > 13B → fires at game start ───────────
        new MilestoneData { label = "Big Bang",
            description = "Space, time, and matter explode into existence from a singular point",
            remainingDistanceThresholdLy = 13.8e9, yearsAgo = 13.8e9,
            iconColor = new Color(0.20f, 0.05f, 0.40f) },

        new MilestoneData { label = "Cosmic Microwave Background",
            description = "The universe cools enough for atoms to form — ancient light floods space",
            remainingDistanceThresholdLy = 13.78e9, yearsAgo = 13.78e9,
            iconColor = new Color(0.28f, 0.08f, 0.48f) },

        // ── QUASAR PHASE  13B → 11B ly ───────────────────────────────────
        new MilestoneData { label = "First Stars Ignite",
            description = "The universe's first massive Population III stars blaze to life in the dark",
            remainingDistanceThresholdLy = 12.5e9, yearsAgo = 1.3e9,
            iconColor = _colEarlyUniverse },

        new MilestoneData { label = "Cosmic Reionization",
            description = "Radiation from the first stars tears electrons free — the universe turns transparent",
            remainingDistanceThresholdLy = 12.0e9, yearsAgo = 1.8e9,
            iconColor = _colEarlyUniverse },

        // ── COSMIC WEB 1  11B → 7B ly ────────────────────────────────────
        new MilestoneData { label = "Milky Way Forms",
            description = "Our galaxy assembles from merging clouds of gas and infant star clusters",
            remainingDistanceThresholdLy = 11.0e9, yearsAgo = 2.8e9,
            iconColor = _colGalactic },

        new MilestoneData { label = "Peak Quasar Activity",
            description = "Thousands of quasars blazing simultaneously — the universe is at its brightest",
            remainingDistanceThresholdLy = 10.5e9, yearsAgo = 3.3e9,
            iconColor = _colEarlyUniverse },

        new MilestoneData { label = "Cosmic Star Formation Noon",
            description = "Stars born 10× faster than today — the universe reaches peak stellar output",
            remainingDistanceThresholdLy = 10.49e9, yearsAgo = 3.3e9,
            iconColor = _colGalactic },

        new MilestoneData { label = "Dark Energy Dominates",
            description = "Mysterious dark energy overcomes gravity — cosmic expansion begins to accelerate",
            remainingDistanceThresholdLy = 7.5e9, yearsAgo = 6.3e9,
            iconColor = _colGalactic },

        // ── GALAXY PHASE  7B → 6.9999B ly (100K ly window) ───────────────
        new MilestoneData { label = "Galaxy Mergers Peak",
            description = "Massive galaxies collide and fuse — you pass through one as it tears itself apart and rebuilds",
            remainingDistanceThresholdLy = 7.0e9, yearsAgo = 6.8e9,
            iconColor = _colGalactic },

        // ── COSMIC WEB 2  6.9999B → 24K ly ───────────────────────────────
        new MilestoneData { label = "Sun Ignites",
            description = "Our star coalesces from a collapsing cloud of gas and interstellar dust",
            remainingDistanceThresholdLy = 4.6e9, yearsAgo = 4.6e9,
            iconColor = _colSolar },

        new MilestoneData { label = "Earth Is Born",
            description = "A rocky planet accretes from solar debris — the future cradle of all known life",
            remainingDistanceThresholdLy = 4.54e9, yearsAgo = 4.54e9,
            iconColor = _colSolar },

        new MilestoneData { label = "Giant Impact — Moon Forms",
            description = "A Mars-sized body collides with Earth, ejecting the debris that becomes the Moon",
            remainingDistanceThresholdLy = 4.5e9, yearsAgo = 4.5e9,
            iconColor = _colSolar },

        new MilestoneData { label = "First Life Emerges",
            description = "Single-celled organisms appear in warm shallow seas — life takes its first breath",
            remainingDistanceThresholdLy = 3.8e9, yearsAgo = 3.8e9,
            iconColor = _colLife },

        new MilestoneData { label = "Oxygen Revolution",
            description = "Cyanobacteria flood the atmosphere with oxygen — the Great Oxidation Event",
            remainingDistanceThresholdLy = 2.7e9, yearsAgo = 2.7e9,
            iconColor = _colLife },

        new MilestoneData { label = "Multicellular Life",
            description = "Complex organisms with differentiated cells appear — evolution takes a giant leap",
            remainingDistanceThresholdLy = 600e6, yearsAgo = 600e6,
            iconColor = _colLife },

        new MilestoneData { label = "Dinosaurs Rise",
            description = "Dinosaurs dominate a warm, oxygen-rich Earth for 165 million years",
            remainingDistanceThresholdLy = 230e6, yearsAgo = 230e6,
            iconColor = _colLife },

        new MilestoneData { label = "Mass Extinction",
            description = "An asteroid ends the dinosaurs — mammals inherit the Earth",
            remainingDistanceThresholdLy = 66e6, yearsAgo = 66e6,
            iconColor = _colLife },

        new MilestoneData { label = "Homo Sapiens Appear",
            description = "The first beings capable of looking up and wondering about the light above",
            remainingDistanceThresholdLy = 300e3, yearsAgo = 300e3,
            iconColor = _colLife },

        // ── MILKY WAY PHASE  24K → 1 ly ──────────────────────────────────
        new MilestoneData { label = "First Cities Rise",
            description = "Humans build the first cities along river valleys — civilization ignites",
            remainingDistanceThresholdLy = 5000, yearsAgo = 5000,
            iconColor = _colLife },

        new MilestoneData { label = "First Telescope",
            description = "Galileo points a lens at the sky — humanity sees the cosmos for the first time",
            remainingDistanceThresholdLy = 400, yearsAgo = 400,
            iconColor = _colLife },

        new MilestoneData { label = "First Radio Signals Leave Earth",
            description = "Humanity's earliest transmissions radiate outward — becoming the light you are",
            remainingDistanceThresholdLy = 120, yearsAgo = 120,
            iconColor = _colLife },

        // ── SOLAR SYSTEM PHASE  1 → 1e-12 ly ─────────────────────────────
        new MilestoneData { label = "Edge of the Solar System",
            description = "You cross into the Oort Cloud — a vast sphere of ice at the frontier of our Sun's gravity",
            remainingDistanceThresholdLy = 0.5, yearsAgo = 0.5,
            iconColor = _colSolar },

        new MilestoneData { label = "Crossing the Heliopause",
            description = "The solar wind fades — you enter the bubble where our Sun's reach ends and the stars begin",
            remainingDistanceThresholdLy = 0.002, yearsAgo = 0.002,
            iconColor = _colSolar },

        // ── EARTH PHASE  1e-12 → 0 ly ────────────────────────────────────
        new MilestoneData { label = "Earth",
            description = "After 13.8 billion light-years, you arrive — the pale blue dot in the dark",
            remainingDistanceThresholdLy = 1e-12, yearsAgo = 0,
            iconColor = new Color(0.05f, 0.35f, 0.65f) },
    };

    // ─────────────────────────────────────────────
    // EVENTS
    // ─────────────────────────────────────────────

    /// <summary>Fired when the active phase changes.</summary>
    public event Action<JourneyPhase, PhaseData> OnPhaseChanged;

    /// <summary>Fired when a milestone is crossed. Passes the full MilestoneData.</summary>
    public event Action<MilestoneData> OnMilestoneReached;

    // ─────────────────────────────────────────────
    // PRIVATE STATE
    // ─────────────────────────────────────────────

    JourneyPhase _currentPhase;
    bool _phasePinned;
    bool _initialized;

    // Trigger Z positions computed at Start()
    float[] _triggerZ;          // one per trigger transform
    float _currentPhaseStartZ;
    float _currentPhaseEndZ;

    // Log-scale weights per phase (computed once, used for TotalProgressLog)
    double[] _logWeights;
    double _totalLogWeight;
    const double MIN_LOG = 2.0;

    // Effective ly-per-Unity-unit for each phase.
    // Derived at runtime from phases[i].TotalDistanceLy / phaseZSpan[i].
    // This is the physical scale factor that makes distance-per-frame different
    // in CosmicWeb (billions of ly/unit) vs SolarSystem (0.01 ly/unit).
    double[] _lyPerUnit;

    // ─────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────

    void Awake()
    {
        _currentPhase = JourneyPhase.Quasar;
    }

    void Start()
    {
        if (!ValidateSetup()) return;

        BuildTriggerZCache();
        BuildLyPerUnitCache();
        BuildLogWeights();
        SetPhaseInternal(_currentPhase, fireEvent: false);

        _initialized = true;
    }

    void Update()
    {
        if (!_initialized) return;

        if (!_phasePinned)
            DetectPhaseFromTriggerZ();

        CheckMilestones();
    }

    // ─────────────────────────────────────────────
    // SETUP VALIDATION
    // ─────────────────────────────────────────────

    bool ValidateSetup()
    {
        if (playerTransform == null)
        {
            Debug.LogWarning("[Tracker] Player Transform not assigned.");
            return false;
        }

        // Need exactly 7 transforms:
        //   [0]-[5] = the 6 phase door triggers (CW, Galaxy, CW2, MW, Solar, Earth)
        //   [6]     = Earth arrival empty GameObject
        // Quasar has NO trigger door — the player starts there automatically.
        const int needed = 7;
        if (layerTriggers == null || layerTriggers.Length < needed)
        {
            Debug.LogWarning(
                $"[Tracker] Need {needed} Layer Triggers " +
                "(6 phase doors + 1 Earth arrival point). " +
                $"Got {layerTriggers?.Length ?? 0}. " +
                "Do NOT add a Quasar trigger — player starts there automatically.");
            return false;
        }

        return true;
    }

    // ─────────────────────────────────────────────
    // TRIGGER Z CACHE
    // ─────────────────────────────────────────────

    void BuildTriggerZCache()
    {
        // Build an 8-entry Z array so phase i spans _triggerZ[i] to _triggerZ[i+1]:
        //   _triggerZ[0] = Quasar start   (player spawn Z — no door needed)
        //   _triggerZ[1] = CW door        (layerTriggers[0])
        //   _triggerZ[2] = Galaxy door    (layerTriggers[1])
        //   _triggerZ[3] = CW2 door       (layerTriggers[2])
        //   _triggerZ[4] = MW door        (layerTriggers[3])
        //   _triggerZ[5] = Solar door     (layerTriggers[4])
        //   _triggerZ[6] = Earth door     (layerTriggers[5])
        //   _triggerZ[7] = Earth arrival  (layerTriggers[6])

        _triggerZ = new float[layerTriggers.Length + 1];
        _triggerZ[0] = playerTransform.position.z; // Quasar start = spawn Z

        for (int i = 0; i < layerTriggers.Length; i++)
            _triggerZ[i + 1] = layerTriggers[i] != null
                ? layerTriggers[i].position.z
                : _triggerZ[i];

        Debug.Log("[Tracker] Z cache:\n" +
                  $"  [0] Quasar start (spawn): {_triggerZ[0]:F1}\n" +
                  $"  [1] CW door:              {_triggerZ[1]:F1}\n" +
                  $"  [2] Galaxy door:          {_triggerZ[2]:F1}\n" +
                  $"  [3] CW2 door:             {_triggerZ[3]:F1}\n" +
                  $"  [4] MW door:              {_triggerZ[4]:F1}\n" +
                  $"  [5] Solar door:           {_triggerZ[5]:F1}\n" +
                  $"  [6] Earth door:           {_triggerZ[6]:F1}\n" +
                  $"  [7] Earth arrival:        {_triggerZ[7]:F1}");
    }

    // ─────────────────────────────────────────────
    // LY-PER-UNIT CACHE
    // ─────────────────────────────────────────────

    /// <summary>
    /// Computes the effective ly/unit scale for each phase from the actual scene
    /// trigger Z spans and the designer-set phase ly distances.
    ///
    /// This is what makes the distance display physically meaningful:
    ///   CosmicWeb  — ~30 M ly per Unity unit (billions drop fast)
    ///   MilkyWay   — ~248 ly per Unity unit  (thousands change slowly)
    ///   SolarSystem— ~0.009 ly per Unity unit (sub-ly crawl)
    ///
    /// If a phase has zero Z span (collapsed trigger), its scale is set to 0
    /// and it contributes nothing to RemainingDistanceLy.
    /// </summary>
    void BuildLyPerUnitCache()
    {
        _lyPerUnit = new double[phases.Length];
        var sb = new System.Text.StringBuilder("[Tracker] Effective ly/unit per phase:\n");

        for (int i = 0; i < phases.Length; i++)
        {
            double zSpan = (i + 1 < _triggerZ.Length)
                ? Math.Abs(_triggerZ[i + 1] - _triggerZ[i])
                : 0.0;

            _lyPerUnit[i] = (zSpan > 0.0001)
                ? phases[i].TotalDistanceLy / zSpan
                : 0.0;

            sb.AppendLine($"  [{(JourneyPhase)i,-12}] " +
                          $"Z-span={zSpan,7:F1}  " +
                          $"{FormatDistance(phases[i].TotalDistanceLy),10} total  " +
                          $"{FormatDistance(_lyPerUnit[i])}/unit");
        }
        Debug.Log(sb.ToString());
    }

    void SetPhaseInternal(JourneyPhase phase, bool fireEvent)
    {
        _currentPhase = phase;
        int i = (int)phase; // phase i spans _triggerZ[i] to _triggerZ[i+1]

        _currentPhaseStartZ = (i < _triggerZ.Length) ? _triggerZ[i] : _triggerZ[_triggerZ.Length - 1];
        _currentPhaseEndZ = (i + 1 < _triggerZ.Length) ? _triggerZ[i + 1] : _triggerZ[_triggerZ.Length - 1];

        if (fireEvent)
            OnPhaseChanged?.Invoke(_currentPhase, GetPhaseData(_currentPhase));

        Debug.Log($"[Tracker] Phase: {_currentPhase} | Z: {_currentPhaseStartZ:F1} to {_currentPhaseEndZ:F1}");
    }


    // ─────────────────────────────────────────────
    // LOG WEIGHTS (for TotalProgressLog)
    // ─────────────────────────────────────────────

    void BuildLogWeights()
    {
        _logWeights = new double[phases.Length];
        _totalLogWeight = 0.0;

        for (int i = 0; i < phases.Length; i++)
        {
            double span = phases[i].TotalDistanceLy;
            _logWeights[i] = Math.Max(Math.Log10(Math.Max(span, 1.0)), MIN_LOG);
            _totalLogWeight += _logWeights[i];
        }
    }

    // ─────────────────────────────────────────────
    // PHASE AUTO-DETECTION FROM Z
    // ─────────────────────────────────────────────

    void DetectPhaseFromTriggerZ()
    {
        float playerZ = playerTransform.position.z;

        // Walk triggers to find which phase window the player is in.
        // We go from last to first so the highest-Z trigger wins.
        for (int i = phases.Length - 1; i >= 0; i--)
        {
            if (i < _triggerZ.Length && playerZ >= _triggerZ[i])
            {
                JourneyPhase detected = (JourneyPhase)i;
                if (detected != _currentPhase)
                    SetPhaseInternal(detected, fireEvent: true);
                return;
            }
        }
    }

    // ─────────────────────────────────────────────
    // MILESTONE CHECKING
    // ─────────────────────────────────────────────

    void CheckMilestones()
    {
        double remaining = RemainingDistanceLy;
        foreach (var m in milestones)
        {
            if (!m.triggered && remaining <= m.remainingDistanceThresholdLy)
            {
                m.triggered = true;
                OnMilestoneReached?.Invoke(m);
            }
        }
    }

    // ─────────────────────────────────────────────
    // PUBLIC API
    // ─────────────────────────────────────────────

    /// <summary>
    /// Called by JourneyLayerResponder when a layer trigger fires.
    /// Pins the phase so auto-detection doesn't override it mid-transition.
    /// </summary>
    public void SetPhase(JourneyPhase newPhase)
    {
        _phasePinned = true;
        if (newPhase == _currentPhase) return;
        SetPhaseInternal(newPhase, fireEvent: true);
    }

    /// <summary>
    /// Called by JourneyLayerResponder when returning to a default/macro layer.
    /// Resumes Z-based auto-detection.
    /// </summary>
    public void UnpinPhase()
    {
        _phasePinned = false;
    }

    /// <summary>
    /// Progress through the current phase (0→1), driven by player Z position.
    /// </summary>
    public float PhaseProgress
    {
        get
        {
            if (!_initialized || playerTransform == null) return 0f;

            float span = _currentPhaseEndZ - _currentPhaseStartZ;
            if (Mathf.Abs(span) < 0.001f) return 0f;

            float t = (playerTransform.position.z - _currentPhaseStartZ) / span;
            return Mathf.Clamp01(t);
        }
    }

    /// <summary>
    /// Total journey progress (0→1) on a log scale.
    /// Drives the gradient fill bar height in UniverseJourneyHUD.
    /// Always derived from raw Z so it stays accurate even when a phase is pinned.
    /// </summary>
    public float TotalProgressLog
    {
        get
        {
            if (_logWeights == null || _totalLogWeight <= 0.0) return 0f;
            var (idx, t) = RawZProgress();
            double completed = 0.0;
            for (int i = 0; i < idx && i < _logWeights.Length; i++)
                completed += _logWeights[i];
            double current = idx < _logWeights.Length ? _logWeights[idx] * t : 0.0;
            return Mathf.Clamp01((float)((completed + current) / _totalLogWeight));
        }
    }

    /// <summary>
    /// Remaining journey distance in light-years to Earth.
    ///
    /// Formula:
    ///   remaining  =  remaining_Z_in_current_phase × ly/unit[current]
    ///              +  Σ( full_Z_span[i] × ly/unit[i] )  for every future phase i
    ///
    /// The ly/unit scale is DIFFERENT per phase (see BuildLyPerUnitCache), so:
    ///   • In CosmicWeb  the number drops by ~30 M ly per Unity unit
    ///   • In MilkyWay   it drops by ~248 ly per Unity unit
    ///   • In SolarSystem it drops by ~0.009 ly per Unity unit
    ///
    /// Always reads raw player Z — unaffected by phase pinning.
    /// </summary>
    public double RemainingDistanceLy
    {
        get
        {
            if (_lyPerUnit == null || _triggerZ == null) return 0.0;
            var (idx, t) = RawZProgress();

            double remaining = 0.0;

            // ── Remaining fraction of the CURRENT phase ──────────────────────
            if (idx < _lyPerUnit.Length && idx + 1 < _triggerZ.Length)
            {
                double zSpan = Math.Abs(_triggerZ[idx + 1] - _triggerZ[idx]);
                remaining += zSpan * (1.0 - t) * _lyPerUnit[idx];
            }

            // ── Full span of every FUTURE phase ──────────────────────────────
            for (int i = idx + 1; i < phases.Length; i++)
            {
                if (i < _lyPerUnit.Length && i + 1 < _triggerZ.Length)
                    remaining += Math.Abs(_triggerZ[i + 1] - _triggerZ[i]) * _lyPerUnit[i];
            }

            return Math.Max(0.0, remaining);
        }
    }

    // Returns (phaseIndex, phaseProgress 0-1) purely from player Z,
    // ignoring _phasePinned and _currentPhase.
    (int idx, float t) RawZProgress()
    {
        if (!_initialized || playerTransform == null || _triggerZ == null)
            return (0, 0f);

        float pz = playerTransform.position.z;
        for (int i = phases.Length - 1; i >= 0; i--)
        {
            if (i >= _triggerZ.Length) continue;
            if (pz >= _triggerZ[i])
            {
                float startZ = _triggerZ[i];
                float endZ   = (i + 1 < _triggerZ.Length) ? _triggerZ[i + 1] : startZ;
                float span   = endZ - startZ;
                float t = Mathf.Abs(span) > 0.001f
                    ? Mathf.Clamp01((pz - startZ) / span)
                    : 0f;
                return (i, t);
            }
        }
        return (0, 0f);
    }

    // ── Convenience properties ────────────────────────────────────────

    public JourneyPhase CurrentPhase => _currentPhase;
    public string CurrentPhaseName => GetPhaseData(_currentPhase).displayName;
    public double CurrentPhaseTotalLy => GetPhaseData(_currentPhase).TotalDistanceLy;
    public PhaseData[] Phases => phases;
    public MilestoneData[] Milestones => milestones;

    public string FormattedRemainingDistance =>
        FormatDistance(RemainingDistanceLy) + " to Earth";

    public string ScaleLabel
    {
        get
        {
            // Shows the real-world scale of the current phase for the HUD
            PhaseData d = GetPhaseData(_currentPhase);
            return FormatDistance(d.TotalDistanceLy) + " phase";
        }
    }

    PhaseData GetPhaseData(JourneyPhase phase)
    {
        int i = (int)phase;
        if (phases == null || i < 0 || i >= phases.Length)
            return new PhaseData { displayName = "Unknown" };
        return phases[i];
    }

    public static string FormatDistance(double ly)
    {
        // Large-scale: B / M / K ly
        if (ly >= 1e9)   return (ly / 1e9).ToString("0.##")  + " B ly";
        if (ly >= 1e6)   return (ly / 1e6).ToString("0.##")  + " M ly";
        if (ly >= 1e3)   return (ly / 1e3).ToString("0.##")  + " K ly";

        // 0.001 – 999 ly  (covers Milky Way, Solar System, and sub-ly approach)
        // Using "0.####" suppresses trailing zeros while preserving up to 4 decimal places,
        // so 0.002 shows as "0.002 ly" rather than "1051 light-min".
        if (ly >= 0.001) return ly.ToString("0.####") + " ly";

        // Below 0.001 ly — switch to time-of-flight units
        double lm = ly * 525960.0;          // light-minutes (1 ly = 525 960 light-min)
        if (lm >= 1440.0) return (lm / 1440.0).ToString("0.#") + " light-day";
        if (lm >= 60.0)   return (lm / 60.0).ToString("0.#")   + " light-hr";
        if (lm >= 1.0)    return lm.ToString("0.#")             + " light-min";
        double ls = lm * 60.0;              // light-seconds
        if (ls >= 1.0)    return ls.ToString("0.#")             + " light-sec";
        return (ls * 299792.458).ToString("0.##") + " km";
    }

    public void ResetJourney()
    {
        _currentPhase = JourneyPhase.Quasar;
        _phasePinned = false;
        if (_initialized) SetPhaseInternal(JourneyPhase.Quasar, fireEvent: true);
        foreach (var m in milestones) m.triggered = false;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (phases != null && phases.Length != 7)
            Debug.LogWarning("[Tracker] phases array should have exactly 7 entries.");

        if (layerTriggers != null && layerTriggers.Length != 7)
            Debug.LogWarning("[Tracker] layerTriggers should have exactly 7 entries: " +
                             "6 phase doors (CW, Galaxy, CW2, MW, Solar, Earth) + 1 Earth arrival point. " +
                             "Do NOT add a Quasar door.");
    }

    [ContextMenu("Debug: Log ly/unit Rates (Play mode only)")]
    void DebugLogLyPerUnit()
    {
        if (_lyPerUnit == null)
        { Debug.LogWarning("[Tracker] _lyPerUnit not built yet — enter Play mode first."); return; }

        var sb = new System.Text.StringBuilder("[Tracker] Effective ly/unit per phase (from scene triggers):\n");
        for (int i = 0; i < phases.Length; i++)
        {
            double zSpan = (i + 1 < _triggerZ.Length)
                ? Math.Abs(_triggerZ[i + 1] - _triggerZ[i]) : 0.0;
            sb.AppendLine($"  [{(JourneyPhase)i,-12}] " +
                          $"{FormatDistance(_lyPerUnit[i]),-18}/unit  " +
                          $"z-span={zSpan,7:F1}  total={FormatDistance(phases[i].TotalDistanceLy)}");
        }
        sb.AppendLine($"\n  Current remaining: {FormatDistance(RemainingDistanceLy)}");
        Debug.Log(sb.ToString());
    }

    /// <summary>
    /// Resets the 7 PhaseData entries to the canonical scientific distances.
    /// Run this if the Inspector shows wrong start/end distances —
    /// symptoms: early number drops too fast, or Solar System doesn't show ~1 ly.
    ///
    /// Phase boundaries (remaining ly to Earth at each phase entry):
    ///   Quasar      13.0 B → 11.0 B ly
    ///   CosmicWeb1  11.0 B →  7.0 B ly
    ///   Galaxy       7.0 B →  6.9999 B ly  (100 K ly window)
    ///   CosmicWeb2   6.9999 B → 24 K ly
    ///   MilkyWay     24 K → 1 ly
    ///   SolarSystem  1 ly → ~0 ly          ← entry shows ~1 ly
    ///   Earth        ~0 → 0
    /// </summary>
    [ContextMenu("Apply Default Phase Distances (overwrites Inspector values)")]
    void ApplyDefaultPhaseDistances()
    {
        phases = new PhaseData[]
        {
            new PhaseData { displayName = "Quasar",
                remainingDistanceAtStart = 13.0e9,    remainingDistanceAtEnd = 11.0e9,
                lyPerUnityUnit = 3e7  },
            new PhaseData { displayName = "Cosmic Web",
                remainingDistanceAtStart = 11.0e9,    remainingDistanceAtEnd = 7.0e9,
                lyPerUnityUnit = 3e7  },
            new PhaseData { displayName = "Galaxy",
                remainingDistanceAtStart = 7.0e9,     remainingDistanceAtEnd = 6.9999e9,
                lyPerUnityUnit = 1e3  },
            new PhaseData { displayName = "Cosmic Web",
                remainingDistanceAtStart = 6.9999e9,  remainingDistanceAtEnd = 2.4e4,
                lyPerUnityUnit = 6e7  },
            new PhaseData { displayName = "Milky Way",
                remainingDistanceAtStart = 2.4e4,     remainingDistanceAtEnd = 1.0,
                lyPerUnityUnit = 240  },
            new PhaseData { displayName = "Solar System",
                remainingDistanceAtStart = 1.0,       remainingDistanceAtEnd = 1e-12,
                lyPerUnityUnit = 0.01 },
            new PhaseData { displayName = "Earth",
                remainingDistanceAtStart = 1e-12,     remainingDistanceAtEnd = 0,
                lyPerUnityUnit = 1e-14 },
        };

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[Tracker] Applied default phase distances.\n" +
                  "Solar System entry will now show ~1 ly.\n" +
                  "Re-enter Play mode for _lyPerUnit cache to rebuild.");
    }

    [ContextMenu("Debug: Fire Next Untriggered Milestone")]
    void DebugFireNextMilestone()
    {
        if (milestones == null) return;
        foreach (var m in milestones)
        {
            if (!m.triggered)
            {
                m.triggered = true;
                OnMilestoneReached?.Invoke(m);
                Debug.Log($"[Tracker] Debug fired: {m.label}");
                return;
            }
        }
        Debug.Log("[Tracker] All milestones already triggered. Call ResetJourney() to reset.");
    }

    [ContextMenu("Debug: Reset All Milestones")]
    void DebugResetMilestones()
    {
        if (milestones == null) return;
        foreach (var m in milestones) m.triggered = false;
        Debug.Log("[Tracker] All milestones reset.");
    }

    /// <summary>
    /// Re-populates the milestones array with the canonical 24-event timeline.
    /// Use this if the Inspector is showing stale/old milestone data.
    /// Existing sprite/icon assignments are cleared — re-assign after running.
    /// </summary>
    [ContextMenu("Apply Default 24 Milestones (overwrites current list)")]
    void ApplyDefaultMilestones()
    {
        var pu = new Color(0.20f, 0.05f, 0.40f);
        var eu = new Color(0.30f, 0.10f, 0.50f);
        var gl = new Color(0.48f, 0.33f, 0.08f);
        var so = new Color(0.15f, 0.50f, 0.25f);
        var li = new Color(0.10f, 0.45f, 0.35f);

        milestones = new MilestoneData[]
        {
            // ── PRE-JOURNEY  threshold > 13B → fires at game start ─────────
            new MilestoneData { label="Big Bang",
                description="Space, time, and matter explode into existence from a singular point",
                remainingDistanceThresholdLy=13.8e9, yearsAgo=13.8e9,
                iconColor=pu },
            new MilestoneData { label="Cosmic Microwave Background",
                description="The universe cools enough for atoms to form — ancient light floods space",
                remainingDistanceThresholdLy=13.78e9, yearsAgo=13.78e9,
                iconColor=new Color(0.28f,0.08f,0.48f) },

            // ── QUASAR PHASE  13B → 11B ly ─────────────────────────────────
            new MilestoneData { label="First Stars Ignite",
                description="The universe's first massive Population III stars blaze to life in the dark",
                remainingDistanceThresholdLy=12.5e9, yearsAgo=1.3e9, iconColor=eu },
            new MilestoneData { label="Cosmic Reionization",
                description="Radiation from the first stars tears electrons free — the universe turns transparent",
                remainingDistanceThresholdLy=12.0e9, yearsAgo=1.8e9, iconColor=eu },

            // ── COSMIC WEB 1  11B → 7B ly ──────────────────────────────────
            new MilestoneData { label="Milky Way Forms",
                description="Our galaxy assembles from merging clouds of gas and infant star clusters",
                remainingDistanceThresholdLy=11.0e9, yearsAgo=2.8e9, iconColor=gl },
            new MilestoneData { label="Peak Quasar Activity",
                description="Thousands of quasars blazing simultaneously — the universe is at its brightest",
                remainingDistanceThresholdLy=10.5e9, yearsAgo=3.3e9, iconColor=eu },
            new MilestoneData { label="Cosmic Star Formation Noon",
                description="Stars born 10× faster than today — the universe reaches peak stellar output",
                remainingDistanceThresholdLy=10.49e9, yearsAgo=3.3e9, iconColor=gl },
            new MilestoneData { label="Dark Energy Dominates",
                description="Mysterious dark energy overcomes gravity — cosmic expansion begins to accelerate",
                remainingDistanceThresholdLy=7.5e9, yearsAgo=6.3e9, iconColor=gl },

            // ── GALAXY PHASE  7B → 6.9999B ly (100K ly window) ─────────────
            new MilestoneData { label="Galaxy Mergers Peak",
                description="Massive galaxies collide and fuse — you pass through one as it tears itself apart and rebuilds",
                remainingDistanceThresholdLy=7.0e9, yearsAgo=6.8e9, iconColor=gl },

            // ── COSMIC WEB 2  6.9999B → 24K ly ────────────────────────────
            new MilestoneData { label="Sun Ignites",
                description="Our star coalesces from a collapsing cloud of gas and interstellar dust",
                remainingDistanceThresholdLy=4.6e9, yearsAgo=4.6e9, iconColor=so },
            new MilestoneData { label="Earth Is Born",
                description="A rocky planet accretes from solar debris — the future cradle of all known life",
                remainingDistanceThresholdLy=4.54e9, yearsAgo=4.54e9, iconColor=so },
            new MilestoneData { label="Giant Impact — Moon Forms",
                description="A Mars-sized body collides with Earth, ejecting the debris that becomes the Moon",
                remainingDistanceThresholdLy=4.5e9, yearsAgo=4.5e9, iconColor=so },
            new MilestoneData { label="First Life Emerges",
                description="Single-celled organisms appear in warm shallow seas — life takes its first breath",
                remainingDistanceThresholdLy=3.8e9, yearsAgo=3.8e9, iconColor=li },
            new MilestoneData { label="Oxygen Revolution",
                description="Cyanobacteria flood the atmosphere with oxygen — the Great Oxidation Event",
                remainingDistanceThresholdLy=2.7e9, yearsAgo=2.7e9, iconColor=li },
            new MilestoneData { label="Multicellular Life",
                description="Complex organisms with differentiated cells appear — evolution takes a giant leap",
                remainingDistanceThresholdLy=600e6, yearsAgo=600e6, iconColor=li },
            new MilestoneData { label="Dinosaurs Rise",
                description="Dinosaurs dominate a warm, oxygen-rich Earth for 165 million years",
                remainingDistanceThresholdLy=230e6, yearsAgo=230e6, iconColor=li },
            new MilestoneData { label="Mass Extinction",
                description="An asteroid ends the dinosaurs — mammals inherit the Earth",
                remainingDistanceThresholdLy=66e6, yearsAgo=66e6, iconColor=li },
            new MilestoneData { label="Homo Sapiens Appear",
                description="The first beings capable of looking up and wondering about the light above",
                remainingDistanceThresholdLy=300e3, yearsAgo=300e3, iconColor=li },

            // ── MILKY WAY PHASE  24K → 1 ly ────────────────────────────────
            new MilestoneData { label="First Cities Rise",
                description="Humans build the first cities along river valleys — civilization ignites",
                remainingDistanceThresholdLy=5000, yearsAgo=5000, iconColor=li },
            new MilestoneData { label="First Telescope",
                description="Galileo points a lens at the sky — humanity sees the cosmos for the first time",
                remainingDistanceThresholdLy=400, yearsAgo=400, iconColor=li },
            new MilestoneData { label="First Radio Signals Leave Earth",
                description="Humanity's earliest transmissions radiate outward — becoming the light you are",
                remainingDistanceThresholdLy=120, yearsAgo=120, iconColor=li },

            // ── SOLAR SYSTEM PHASE  1 → 1e-12 ly ───────────────────────────
            new MilestoneData { label="Edge of the Solar System",
                description="You cross into the Oort Cloud — a vast sphere of ice at the frontier of our Sun's gravity",
                remainingDistanceThresholdLy=0.5, yearsAgo=0.5, iconColor=so },
            new MilestoneData { label="Crossing the Heliopause",
                description="The solar wind fades — you enter the bubble where our Sun's reach ends and the stars begin",
                remainingDistanceThresholdLy=0.002, yearsAgo=0.002, iconColor=so },

            // ── EARTH PHASE  1e-12 → 0 ly ──────────────────────────────────
            new MilestoneData { label="Earth",
                description="After 13.8 billion light-years, you arrive — the pale blue dot in the dark",
                remainingDistanceThresholdLy=1e-12, yearsAgo=0,
                iconColor=new Color(0.05f,0.35f,0.65f) },
        };

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[Tracker] Applied default 24 milestones. Re-assign sprites in MilestoneHUD.");
    }
#endif
}
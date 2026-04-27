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

        [Tooltip("Legacy field — kept for compatibility with LymanAlphaAbsorptionController.\n" +
                 "No longer used to drive HUD progress (now trigger-based).\n" +
                 "Set to 0 to disable, or keep your existing value for other scripts.")]
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
        [Tooltip("Display label e.g. '🌟 First Stars Form'")]
        public string label;

        [Tooltip("Optional icon sprite shown in milestone popup.")]
        public Sprite icon;

        [Tooltip("Fires when remaining distance (ly) drops BELOW this value.")]
        public double remainingDistanceThresholdLy;

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

    [Header("Milestones")]
    [SerializeField]
    MilestoneData[] milestones = new MilestoneData[]
    {
        new MilestoneData { label="🌟 First Stars Form",
            remainingDistanceThresholdLy=12.5e9 },
        new MilestoneData { label="🌌 Milky Way Forms",
            remainingDistanceThresholdLy=11.0e9 },
        new MilestoneData { label="💥 Cosmic Noon — Peak Star Formation",
            remainingDistanceThresholdLy=10.5e9 },
        new MilestoneData { label="🌑 Dark Energy Takes Over",
            remainingDistanceThresholdLy=7.5e9  },
        new MilestoneData { label="☀️ Sun Born",
            remainingDistanceThresholdLy=4.6e9  },
        new MilestoneData { label="🌍 Earth Born",
            remainingDistanceThresholdLy=4.5e9  },
        new MilestoneData { label="🦕 Dinosaurs Roam",
            remainingDistanceThresholdLy=2.3e8  },
        new MilestoneData { label="🧑 Humans Appear",
            remainingDistanceThresholdLy=3e5    },
        new MilestoneData { label="📡 First Radio Signal",
            remainingDistanceThresholdLy=120    },
    };

    // ─────────────────────────────────────────────
    // EVENTS
    // ─────────────────────────────────────────────

    /// <summary>Fired when the active phase changes.</summary>
    public event Action<JourneyPhase, PhaseData> OnPhaseChanged;

    /// <summary>Fired when a milestone is crossed. Args: label, icon sprite.</summary>
    public event Action<string, Sprite> OnMilestoneReached;

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
                OnMilestoneReached?.Invoke(m.label, m.icon);
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
    /// </summary>
    public float TotalProgressLog
    {
        get
        {
            if (_logWeights == null || _totalLogWeight <= 0.0) return 0f;

            int idx = (int)_currentPhase;
            double completed = 0.0;

            for (int i = 0; i < idx && i < _logWeights.Length; i++)
                completed += _logWeights[i];

            double current = idx < _logWeights.Length
                ? _logWeights[idx] * PhaseProgress
                : 0.0;

            return Mathf.Clamp01((float)((completed + current) / _totalLogWeight));
        }
    }

    /// <summary>
    /// Remaining journey distance in ly, interpolated from phase data.
    /// Used for the distance label only — not for driving progress.
    /// </summary>
    public double RemainingDistanceLy
    {
        get
        {
            PhaseData d = GetPhaseData(_currentPhase);
            double remaining = d.remainingDistanceAtStart
                - d.TotalDistanceLy * PhaseProgress;
            return Math.Max(0.0, remaining);
        }
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
        if (ly >= 1e9) return (ly / 1e9).ToString("0.##") + "B ly";
        if (ly >= 1e6) return (ly / 1e6).ToString("0.##") + "M ly";
        if (ly >= 1e3) return (ly / 1e3).ToString("0.##") + "K ly";
        if (ly >= 1.0) return ly.ToString("0.##") + " ly";
        double lm = ly * 525960.0;
        if (lm >= 1.0) return lm.ToString("0.#") + " light-min";
        double ls = lm * 60.0;
        if (ls >= 1.0) return ls.ToString("0.#") + " light-sec";
        return (ls * 299792.0).ToString("0.##") + " km";
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
#endif
}
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Universe Journey Tracker
///
/// Updated phase structure:
///   Quasar       13B → 11B ly   (starting layer, same scale as CosmicWeb1)
///   CosmicWeb1   11B → 7B  ly
///   Galaxy        7B → 6.9B ly  (intermediate galaxy, ~100K ly)
///   CosmicWeb2   6.9B → 24K ly
///   MilkyWay     24K → 1   ly
///   SolarSystem    1 → 1e-12 ly
///   Earth       1e-12 → 0
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
        [Tooltip("Display name shown in HUD")]
        public string displayName;
        [Tooltip("Remaining distance at which this phase BEGINS (ly).")]
        public double remainingDistanceAtStart;
        [Tooltip("Remaining distance at which this phase ENDS (ly).")]
        public double remainingDistanceAtEnd;
        [Tooltip("Light years per Unity world unit travelled in this phase.")]
        public double lyPerUnityUnit;

        public double TotalDistanceLy => remainingDistanceAtStart - remainingDistanceAtEnd;
    }

    // ─────────────────────────────────────────────
    // MILESTONE DEFINITIONS
    // ─────────────────────────────────────────────

    [Serializable]
    public class MilestoneData
    {
        [Tooltip("Display emoji + label e.g. '🌟 First Stars Form'")]
        public string label;
        [Tooltip("Milestone fires when remaining distance (ly) drops BELOW this value.")]
        public double remainingDistanceThresholdLy;
        [HideInInspector] public bool triggered;
    }

    // ─────────────────────────────────────────────
    // INSPECTOR
    // ─────────────────────────────────────────────

    [Header("Player")]
    [SerializeField] Transform playerTransform;

    [Header("Journey")]
    [Tooltip("Total journey distance in light years (Quasar to Earth).")]
    [SerializeField] double totalJourneyLy = 13.000100024e9;

    [Header("Phase Data")]
    [Tooltip("One entry per JourneyPhase enum value, in order from furthest to closest.")]
    [SerializeField]
    PhaseData[] phases = new PhaseData[]
    {
        // Quasar: 13B → 11B ly (2B ly)
        new PhaseData {
            displayName              = "Quasar",
            remainingDistanceAtStart = 13.0e9,
            remainingDistanceAtEnd   = 11.0e9,
            lyPerUnityUnit           = 3e7
        },
        // CosmicWeb1: 11B → 7B ly (4B ly)
        new PhaseData {
            displayName              = "Cosmic Web",
            remainingDistanceAtStart = 11.0e9,
            remainingDistanceAtEnd   = 7.0e9,
            lyPerUnityUnit           = 3e7
        },
        // Galaxy: 7B → 6.9B ly (~100K ly intermediate galaxy)
        new PhaseData {
            displayName              = "Galaxy",
            remainingDistanceAtStart = 7.0e9,
            remainingDistanceAtEnd   = 6.9999e9,
            lyPerUnityUnit           = 1e3
        },
        // CosmicWeb2: 6.9B → 24K ly (~6.9B ly)
        new PhaseData {
            displayName              = "Cosmic Web",
            remainingDistanceAtStart = 6.9999e9,
            remainingDistanceAtEnd   = 2.4e4,
            lyPerUnityUnit           = 6e7
        },
        // MilkyWay: 24K → 1 ly
        new PhaseData {
            displayName              = "Milky Way",
            remainingDistanceAtStart = 2.4e4,
            remainingDistanceAtEnd   = 1.0,
            lyPerUnityUnit           = 240
        },
        // SolarSystem: 1 → 1e-12 ly
        new PhaseData {
            displayName              = "Solar System",
            remainingDistanceAtStart = 1.0,
            remainingDistanceAtEnd   = 1e-12,
            lyPerUnityUnit           = 0.01
        },
        // Earth: 1e-12 → 0
        new PhaseData {
            displayName              = "Earth",
            remainingDistanceAtStart = 1e-12,
            remainingDistanceAtEnd   = 0,
            lyPerUnityUnit           = 1e-14
        },
    };

    [Header("Milestones")]
    [SerializeField]
    MilestoneData[] milestones = new MilestoneData[]
    {
        // Quasar / CosmicWeb1
        new MilestoneData { label = "🌟 First Stars Form",   remainingDistanceThresholdLy = 12.5e9 },
        new MilestoneData { label = "🌌 Milky Way Forms",    remainingDistanceThresholdLy = 11.0e9 },
        // CosmicWeb2
        new MilestoneData { label = "☀️ Sun Born",           remainingDistanceThresholdLy = 4.6e9  },
        new MilestoneData { label = "🌍 Earth Born",         remainingDistanceThresholdLy = 4.5e9  },
        new MilestoneData { label = "🦕 Dinosaurs Roam",     remainingDistanceThresholdLy = 2.3e8  },
        new MilestoneData { label = "🧑 Humans Appear",      remainingDistanceThresholdLy = 3e5    },
        // Milky Way
        new MilestoneData { label = "📡 First Radio Signal", remainingDistanceThresholdLy = 120    },
    };

    // ─────────────────────────────────────────────
    // EVENTS
    // ─────────────────────────────────────────────

    public event Action<JourneyPhase, PhaseData> OnPhaseChanged;
    public event Action<string> OnMilestoneReached;

    // ─────────────────────────────────────────────
    // PRIVATE STATE
    // ─────────────────────────────────────────────

    [System.NonSerialized] JourneyPhase _currentPhase;
    [System.NonSerialized] double _remainingDistanceLy;
    [System.NonSerialized] double _phaseDistanceTravelled;
    [System.NonSerialized] bool _phasePinned;
    Vector3 _lastPlayerPosition;
    float _smoothedSpeedUnitsPerSec;
    bool _initialized;

    // ─────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────

    void Awake()
    {
        _currentPhase = JourneyPhase.Quasar;
        _remainingDistanceLy = totalJourneyLy;
        _phaseDistanceTravelled = 0;
    }

    void Start()
    {
        if (playerTransform != null)
        {
            _lastPlayerPosition = playerTransform.position;
            _initialized = true;
        }
        else
        {
            Debug.LogWarning("[UniverseJourneyTracker] Player Transform not assigned.");
        }
    }

    void Update()
    {
        if (!_initialized || playerTransform == null) return;

        Vector3 currentPos = playerTransform.position;
        float unityDelta = Vector3.Distance(currentPos, _lastPlayerPosition);
        _lastPlayerPosition = currentPos;

        _smoothedSpeedUnitsPerSec = Mathf.Lerp(
            _smoothedSpeedUnitsPerSec,
            unityDelta / Mathf.Max(Time.deltaTime, 0.0001f),
            Time.deltaTime * 5f
        );

        if (unityDelta > 0f)
        {
            PhaseData phase = GetPhaseData(_currentPhase);
            double lyDelta = unityDelta * phase.lyPerUnityUnit;

            _remainingDistanceLy = Math.Max(0, _remainingDistanceLy - lyDelta);
            _phaseDistanceTravelled += lyDelta;

            DetectPhaseFromRemaining();
            CheckMilestones();
        }
    }

    void DetectPhaseFromRemaining()
    {
        if (_phasePinned) return;

        for (int i = 0; i < phases.Length; i++)
        {
            double start = phases[i].remainingDistanceAtStart;
            double end = phases[i].remainingDistanceAtEnd;

            if (_remainingDistanceLy <= start && _remainingDistanceLy > end)
            {
                JourneyPhase detected = (JourneyPhase)i;
                if (detected != _currentPhase)
                {
                    _currentPhase = detected;
                    _phaseDistanceTravelled = 0;
                    OnPhaseChanged?.Invoke(_currentPhase, GetPhaseData(_currentPhase));
                    Debug.Log($"[Tracker] Phase: {_currentPhase}, Remaining: {_remainingDistanceLy:e2}");
                }
                return;
            }
        }

        if (_remainingDistanceLy <= 0)
        {
            JourneyPhase last = (JourneyPhase)(phases.Length - 1);
            if (_currentPhase != last)
            {
                _currentPhase = last;
                _phaseDistanceTravelled = 0;
                OnPhaseChanged?.Invoke(_currentPhase, GetPhaseData(_currentPhase));
            }
        }
    }

    // ─────────────────────────────────────────────
    // MILESTONE CHECKING
    // ─────────────────────────────────────────────

    void CheckMilestones()
    {
        foreach (var m in milestones)
        {
            if (!m.triggered && _remainingDistanceLy <= m.remainingDistanceThresholdLy)
            {
                m.triggered = true;
                OnMilestoneReached?.Invoke(m.label);
            }
        }
    }

    // ─────────────────────────────────────────────
    // PUBLIC API
    // ─────────────────────────────────────────────

    public void SetPhase(JourneyPhase newPhase)
    {
        _phasePinned = true;
        if (newPhase == _currentPhase) return;
        _currentPhase = newPhase;
        _phaseDistanceTravelled = 0;
        OnPhaseChanged?.Invoke(_currentPhase, GetPhaseData(_currentPhase));
    }

    public void UnpinPhase()
    {
        _phasePinned = false;
    }

    public float PhaseProgress
    {
        get
        {
            PhaseData d = GetPhaseData(_currentPhase);
            double phaseTotalLy = d.TotalDistanceLy;
            if (phaseTotalLy <= 0) return 0f;
            return Mathf.Clamp01((float)(_phaseDistanceTravelled / phaseTotalLy));
        }
    }

    public float TotalProgressLog
    {
        get
        {
            if (_remainingDistanceLy <= 0) return 1f;
            if (totalJourneyLy <= 0) return 0f;
            double logTotal = Math.Log10(Math.Max(totalJourneyLy, 1));
            double logRemaining = Math.Log10(Math.Max(_remainingDistanceLy, 1));
            return Mathf.Clamp01((float)((logTotal - logRemaining) / logTotal));
        }
    }

    public double RemainingDistanceLy => _remainingDistanceLy;
    public double PhaseDistanceLy => _phaseDistanceTravelled;
    public JourneyPhase CurrentPhase => _currentPhase;
    public string CurrentPhaseName => GetPhaseData(_currentPhase).displayName;
    public double CurrentPhaseTotalLy => GetPhaseData(_currentPhase).TotalDistanceLy;
    public PhaseData[] Phases => phases;
    public MilestoneData[] Milestones => milestones;

    public string FormattedRemainingDistance =>
        FormatDistance(_remainingDistanceLy) + " to Earth";

    public string FormattedPhaseDistance => FormatDistance(_phaseDistanceTravelled);

    public string ScaleLabel
    {
        get
        {
            PhaseData d = GetPhaseData(_currentPhase);
            return "1 m = " + FormatDistance(d.lyPerUnityUnit);
        }
    }

    PhaseData GetPhaseData(JourneyPhase phase)
    {
        int i = (int)phase;
        if (phases == null || i < 0 || i >= phases.Length)
            return new PhaseData { displayName = "Unknown", lyPerUnityUnit = 1 };
        return phases[i];
    }

    public static string FormatDistance(double ly)
    {
        if (ly >= 1e9) return (ly / 1e9).ToString("0.##") + "B ly";
        if (ly >= 1e6) return (ly / 1e6).ToString("0.##") + "M ly";
        if (ly >= 1e3) return (ly / 1e3).ToString("0.##") + "K ly";
        if (ly >= 1.0) return ly.ToString("0.##") + " ly";
        double lightMin = ly * 525960.0;
        if (lightMin >= 1.0) return lightMin.ToString("0.#") + " light-min";
        double lightSec = lightMin * 60.0;
        if (lightSec >= 1.0) return lightSec.ToString("0.#") + " light-sec";
        double km = lightSec * 299792.0;
        return km.ToString("0.##") + " km";
    }

    public void ResetJourney()
    {
        _remainingDistanceLy = totalJourneyLy;
        _phaseDistanceTravelled = 0;
        _currentPhase = JourneyPhase.Quasar;
        foreach (var m in milestones) m.triggered = false;
        if (playerTransform != null)
            _lastPlayerPosition = playerTransform.position;
    }
}
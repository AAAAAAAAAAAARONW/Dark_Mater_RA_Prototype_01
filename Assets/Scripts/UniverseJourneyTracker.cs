using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Universe Journey Tracker
///
/// Tracks the player's remaining distance to Earth across the full ~10 billion light year journey.
/// Distance counts DOWN from ~10B ly to 0 as the player travels.
///
/// Phase is determined by remaining distance brackets — not by accumulated distance.
/// Milestones fire when remaining distance drops below their threshold.
///
/// Distance accumulates from player position delta (standing still = no progress).
/// Each phase has its own lyPerUnityUnit scale.
///
/// SETUP:
/// 1. Attach to any persistent GameObject (e.g. GameManager).
/// 2. Assign Player Transform.
/// 3. Call SetPhase(JourneyPhase) from trigger scripts when player enters a new region.
/// </summary>
public class UniverseJourneyTracker : MonoBehaviour
{
    // ─────────────────────────────────────────────
    // PHASE DEFINITIONS
    // ─────────────────────────────────────────────

    public enum JourneyPhase
    {
        CosmicWeb1 = 0,  // Quasar → Intermediate Galaxy  (~7B ly remaining at start)
        //IntermGalaxy = 1,  // Through Intermediate Galaxy   (~3.0001B ly remaining)
        //CosmicWeb2 = 2,  // Intermediate Galaxy → Milky Way (~3B ly remaining)
        MilkyWay = 1,  // Milky Way edge → Solar System  (~24K ly remaining)
        SolarSystem = 2,  // Solar System → Earth           (~1 ly remaining)
        Earth = 3,  // Earth → Pasadena               (~1e-12 ly remaining)
    }

    [Serializable]
    public class PhaseData
    {
        [Tooltip("Display name shown in HUD")]
        public string displayName;
        [Tooltip("Remaining distance at which this phase BEGINS (ly). " +
                 "Player enters this phase when remaining distance drops below the previous phase's threshold.")]
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
    [Tooltip("The player transform. Position delta drives distance accumulation.")]
    [SerializeField] Transform playerTransform;

    [Header("Journey")]
    [Tooltip("Total journey distance in light years (Quasar to Earth).")]
    [SerializeField] double totalJourneyLy = 9.000100024e9;  // 3B + 100K ly + 6B + 24K + 1 + trace

    [Header("Phase Data")]
    [Tooltip("One entry per JourneyPhase enum value, in order from furthest to closest.")]
    [SerializeField]
    PhaseData[] phases = new PhaseData[]
    {
        // CosmicWeb1: Quasar → Intermediate Galaxy (3B ly)
        new PhaseData {
            displayName              = "Cosmic Web",
            remainingDistanceAtStart = 9.000100024e9,
            remainingDistanceAtEnd   = 6.000100024e9,
            lyPerUnityUnit           = 3e7
        },
        // IntermGalaxy: Through Intermediate Galaxy (~100K ly)
        /*new PhaseData {
            displayName              = "Intermediate Galaxy",
            remainingDistanceAtStart = 6.000100024e9,
            remainingDistanceAtEnd   = 6.000000024e9,
            lyPerUnityUnit           = 1e3
        },
        // CosmicWeb2: Intermediate Galaxy → Milky Way (6B ly)
        /*new PhaseData {
            displayName              = "Cosmic Web",
            remainingDistanceAtStart = 6.000000024e9,
            remainingDistanceAtEnd   = 2.4e4,
            lyPerUnityUnit           = 6e7
        },*/
        // MilkyWay: Milky Way edge → Solar System (24K ly)
        new PhaseData {
            displayName              = "Milky Way",
            remainingDistanceAtStart = 2.4e4,
            remainingDistanceAtEnd   = 1.0,
            lyPerUnityUnit           = 240
        },
        // SolarSystem: Oort Cloud → Earth (~1 ly)
        new PhaseData {
            displayName              = "Solar System",
            remainingDistanceAtStart = 1.0,
            remainingDistanceAtEnd   = 1e-12,
            lyPerUnityUnit           = 0.01
        },
        // Earth: Earth surface → Pasadena
        new PhaseData {
            displayName              = "Earth",
            remainingDistanceAtStart = 1e-12,
            remainingDistanceAtEnd   = 0,
            lyPerUnityUnit           = 1e-14
        },
    };

    [Header("Milestones")]
    [Tooltip("Fires when remaining distance drops below threshold. " +
             "Order from highest remaining distance to lowest.")]
    [SerializeField]
    MilestoneData[] milestones = new MilestoneData[]
    {
        // CosmicWeb1 milestones (remaining > 6B ly)
        new MilestoneData { label = "🌟 First Stars Form",   remainingDistanceThresholdLy = 8.5e9 },
        new MilestoneData { label = "🌌 Milky Way Forms",    remainingDistanceThresholdLy = 8.2e9 },
        // CosmicWeb2 milestones (remaining between 6B and 24K ly)
        new MilestoneData { label = "☀️ Sun Born",           remainingDistanceThresholdLy = 4.6e9 },
        new MilestoneData { label = "🌍 Earth Born",         remainingDistanceThresholdLy = 4.5e9 },
        new MilestoneData { label = "🦕 Dinosaurs Roam",     remainingDistanceThresholdLy = 2.3e8 },
        new MilestoneData { label = "🧑 Humans Appear",      remainingDistanceThresholdLy = 3e5   },
        // Milky Way milestone (remaining < 24K ly)
        new MilestoneData { label = "📡 First Radio Signal", remainingDistanceThresholdLy = 120   },
    };

    // ─────────────────────────────────────────────
    // EVENTS
    // ─────────────────────────────────────────────

    /// <summary>Fired when the phase changes. Args: new phase, phase data.</summary>
    public event Action<JourneyPhase, PhaseData> OnPhaseChanged;

    /// <summary>Fired when a milestone is reached. Arg: milestone label.</summary>
    public event Action<string> OnMilestoneReached;

    // ─────────────────────────────────────────────
    // PRIVATE STATE
    // ─────────────────────────────────────────────

    // NonSerialized prevents Unity from saving/restoring these between editor sessions
    [System.NonSerialized] JourneyPhase _currentPhase;
    [System.NonSerialized] double _remainingDistanceLy;
    [System.NonSerialized] double _phaseDistanceTravelled;
    // When true, SetPhase() has pinned the phase — auto-detect is suspended
    // until UnpinPhase() is called (e.g. when player exits a micro trigger)
    [System.NonSerialized] bool _phasePinned;
    Vector3 _lastPlayerPosition;
    float _smoothedSpeedUnitsPerSec;
    bool _initialized;

    // ─────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────

    void Awake()
    {
        // Reset in Awake so phase is correct before any other script reads it in Start()
        _currentPhase = JourneyPhase.CosmicWeb1;
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

        // Track smoothed speed for ScaleLabel
        _smoothedSpeedUnitsPerSec = Mathf.Lerp(
            _smoothedSpeedUnitsPerSec,
            unityDelta / Mathf.Max(Time.deltaTime, 0.0001f),
            Time.deltaTime * 5f
        );

        if (unityDelta > 0f)
        {
            PhaseData phase = GetPhaseData(_currentPhase);
            double lyDelta = unityDelta * phase.lyPerUnityUnit;

            // Count DOWN remaining distance
            _remainingDistanceLy = Math.Max(0, _remainingDistanceLy - lyDelta);
            _phaseDistanceTravelled += lyDelta;

            // Auto-detect phase from remaining distance brackets
            DetectPhaseFromRemaining();

            CheckMilestones();
        }
    }

    /// <summary>
    /// Automatically determines current phase based on remaining distance.
    /// Each PhaseData defines remainingDistanceAtStart and remainingDistanceAtEnd.
    /// Player is in phase i when remaining distance is between those two values.
    /// </summary>
    void DetectPhaseFromRemaining()
    {
        // If a trigger has pinned the phase (e.g. inside a galaxy micro zone),
        // skip auto-detection so the pinned scale stays active
        if (_phasePinned) return;

        for (int i = 0; i < phases.Length; i++)
        {
            double start = phases[i].remainingDistanceAtStart;
            double end = phases[i].remainingDistanceAtEnd;

            // Player is in this phase when remaining is between start and end
            if (_remainingDistanceLy <= start && _remainingDistanceLy > end)
            {
                JourneyPhase detected = (JourneyPhase)i;
                if (detected != _currentPhase)
                {
                    _currentPhase = detected;
                    _phaseDistanceTravelled = 0;
                    OnPhaseChanged?.Invoke(_currentPhase, GetPhaseData(_currentPhase));
                    Debug.Log($"[Tracker] Phase auto-detected: {_currentPhase}, Remaining: {_remainingDistanceLy}");
                }
                return;
            }
        }

        // Edge case: remaining is exactly 0 → Earth (last phase)
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
            // Fire when remaining distance drops BELOW the threshold
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

    /// <summary>
    /// Called from trigger scripts to force a phase override and PIN it.
    /// Auto-detection is suspended while pinned so the micro scale stays active
    /// even though remaining distance hasn't changed to match the new phase bracket.
    /// Call UnpinPhase() when the player exits the trigger to resume auto-detection.
    /// </summary>
    public void SetPhase(JourneyPhase newPhase)
    {
        _phasePinned = true;
        if (newPhase == _currentPhase) return;
        _currentPhase = newPhase;
        _phaseDistanceTravelled = 0;
        OnPhaseChanged?.Invoke(_currentPhase, GetPhaseData(_currentPhase));
    }

    /// <summary>
    /// Call from trigger scripts when the player EXITS a micro trigger zone.
    /// Resumes auto-detection from remaining distance.
    /// </summary>
    public void UnpinPhase()
    {
        _phasePinned = false;
    }

    /// <summary>
    /// Normalized progress 0..1 within the current phase.
    /// 0 = just entered phase, 1 = phase complete.
    /// </summary>
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

    /// <summary>
    /// Normalized overall journey progress 0..1.
    /// Uses log scale so the enormous range compresses meaningfully.
    /// </summary>
    public float TotalProgressLog
    {
        get
        {
            if (_remainingDistanceLy <= 0) return 1f;
            if (totalJourneyLy <= 0) return 0f;
            // Log scale: log(remaining) going from log(total) down to 0
            double logTotal = Math.Log10(Math.Max(totalJourneyLy, 1));
            double logRemaining = Math.Log10(Math.Max(_remainingDistanceLy, 1));
            return Mathf.Clamp01((float)((logTotal - logRemaining) / logTotal));
        }
    }

    /// <summary>Remaining distance to Earth in light years.</summary>
    public double RemainingDistanceLy => _remainingDistanceLy;

    /// <summary>Distance travelled within current phase.</summary>
    public double PhaseDistanceLy => _phaseDistanceTravelled;

    /// <summary>Current journey phase.</summary>
    public JourneyPhase CurrentPhase => _currentPhase;

    /// <summary>Current phase display name.</summary>
    public string CurrentPhaseName => GetPhaseData(_currentPhase).displayName;

    /// <summary>Total distance of the current phase in light years.</summary>
    public double CurrentPhaseTotalLy => GetPhaseData(_currentPhase).TotalDistanceLy;

    /// <summary>
    /// Remaining distance formatted with auto unit switching.
    /// e.g. "7.2B ly to Earth", "8.3 light-min to Earth"
    /// </summary>
    public string FormattedRemainingDistance =>
        FormatDistance(_remainingDistanceLy) + " to Earth";

    /// <summary>
    /// Distance travelled in the current phase, formatted.
    /// </summary>
    public string FormattedPhaseDistance => FormatDistance(_phaseDistanceTravelled);

    /// <summary>
    /// Scale label for current phase.
    /// e.g. "700M ly / sec" (assumes ~60 unity units/sec baseline speed)
    /// </summary>
    // FIX: track actual player speed so ScaleLabel is accurate

    /// <summary>
    /// Spatial scale label — how much real distance 1 Unity metre represents.
    /// Framed as a map scale ratio, not a speed, because light speed is constant.
    /// What changes between phases is the map scale, not physics.
    /// e.g. "1 m = 700M ly" in cosmic web, "1 m = 1K ly" inside a galaxy.
    /// </summary>
    public string ScaleLabel
    {
        get
        {
            PhaseData d = GetPhaseData(_currentPhase);
            // lyPerUnityUnit = how many light years one Unity world unit represents
            return "1 m = " + FormatDistance(d.lyPerUnityUnit);
        }
    }

    /// <summary>All phase data. Used by HUD to draw node chain.</summary>
    public PhaseData[] Phases => phases;

    /// <summary>All milestones. Used by HUD.</summary>
    public MilestoneData[] Milestones => milestones;

    // ─────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────

    PhaseData GetPhaseData(JourneyPhase phase)
    {
        int i = (int)phase;
        if (phases == null || i < 0 || i >= phases.Length)
            return new PhaseData { displayName = "Unknown", lyPerUnityUnit = 1 };
        return phases[i];
    }

    /// <summary>
    /// Auto-switches units: B ly → M ly → K ly → ly → light-min → light-sec → km
    /// </summary>
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

    /// <summary>Reset all state for a new run.</summary>
    public void ResetJourney()
    {
        _remainingDistanceLy = totalJourneyLy;
        _phaseDistanceTravelled = 0;
        _currentPhase = JourneyPhase.CosmicWeb1;
        foreach (var m in milestones) m.triggered = false;
        if (playerTransform != null)
            _lastPlayerPosition = playerTransform.position;
    }
}
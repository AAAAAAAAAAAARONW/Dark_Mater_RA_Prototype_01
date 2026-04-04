using System;
using UnityEngine;

/// <summary>
/// Snapshot of current layer state. Passed via OnStateChanged event.
/// TestVersion of LayerStateSnapshot — uses LayerDefinitionTest.
/// </summary>
public class LayerStateSnapshotTest
{
    public LayerDefinitionTest CurrentDefinition { get; }
    public LayerDefinitionTest PreviousDefinition { get; }
    public string CurrentLayerId { get; }
    public string PreviousLayerId { get; }
    public UniverseLayer CurrentUniverseLayer { get; }
    public UniverseLayer PreviousUniverseLayer { get; }
    public LayerTransitionPhase Phase { get; }
    public float TimeInPhase { get; }
    public float NormalizedProgress01 { get; }

    public LayerStateSnapshotTest(
        LayerDefinitionTest currentDefinition,
        LayerDefinitionTest previousDefinition,
        string currentLayerId,
        string previousLayerId,
        UniverseLayer currentUniverseLayer,
        UniverseLayer previousUniverseLayer,
        LayerTransitionPhase phase,
        float timeInPhase,
        float normalizedProgress01)
    {
        CurrentDefinition = currentDefinition;
        PreviousDefinition = previousDefinition;
        CurrentLayerId = currentLayerId;
        PreviousLayerId = previousLayerId;
        CurrentUniverseLayer = currentUniverseLayer;
        PreviousUniverseLayer = previousUniverseLayer;
        Phase = phase;
        TimeInPhase = timeInPhase;
        NormalizedProgress01 = normalizedProgress01;
    }
}

/// <summary>
/// Test version of LayerStateManager.
/// Changes from original:
///   - Uses LayerCatalogTest instead of LayerCatalog
///   - Uses LayerDefinitionTest instead of LayerDefinition
///   - Events fire LayerDefinitionTest so listeners can read the full profile
///   - Uses LayerStateSnapshotTest
/// Everything else is identical to the original.
/// </summary>
public class LayerStateManagerTest : MonoBehaviour
{
    [Header("Catalog")]
    [SerializeField] LayerCatalogTest catalog;
    [Tooltip("layerId used when no trigger volume is active.")]
    [SerializeField] string defaultLayerId = "Macro";

    [Header("Debug")]
    [SerializeField] bool debugLog = true;

    LayerDefinitionTest _currentDefinition;
    string _currentLayerId;
    UniverseLayer _currentUniverseLayer;

    LayerDefinitionTest _previousDefinition;
    string _previousLayerId;
    UniverseLayer _previousUniverseLayer;

    LayerTransitionPhase _phase;

    // ── Events ───────────────────────────────────────────────────────────────
    /// <summary>Fired when the active layer changes. Args: previous, current LayerDefinitionTest.</summary>
    public event Action<LayerDefinitionTest, LayerDefinitionTest> OnLayerChanged;

    /// <summary>Fired when the transition phase changes.</summary>
    public event Action<LayerTransitionPhase> OnPhaseChanged;

    /// <summary>Fired on every state change with a full snapshot.</summary>
    public event Action<LayerStateSnapshotTest> OnStateChanged;

    // ── Public API ───────────────────────────────────────────────────────────
    public string CurrentLayerId => _currentLayerId;
    public LayerDefinitionTest CurrentDefinition => _currentDefinition;
    public UniverseLayer CurrentUniverseLayer => _currentUniverseLayer;
    public LayerTransitionPhase Phase => _phase;
    public LayerStateSnapshotTest Snapshot => BuildSnapshot();

    // ── Unity Lifecycle ──────────────────────────────────────────────────────
    void Awake()
    {
        ResolveAndApplyDefault(initial: true);
        EmitState();
    }

    // ── Internal ─────────────────────────────────────────────────────────────
    void ResolveAndApplyDefault(bool initial)
    {
        LayerDefinitionTest def = GetDefinition(defaultLayerId);
        if (def == null)
        {
            _currentDefinition = null;
            _currentLayerId = defaultLayerId;
            _currentUniverseLayer = UniverseLayer.Macro;
        }
        else
        {
            _currentDefinition = def;
            _currentLayerId = def.layerId;
            _currentUniverseLayer = def.universeLayer;
        }

        if (initial)
        {
            _previousDefinition = null;
            _previousLayerId = null;
            _previousUniverseLayer = UniverseLayer.Macro;
        }
    }

    public LayerDefinitionTest GetDefinition(string layerId)
    {
        if (catalog == null) return null;
        return catalog.GetDefinition(layerId);
    }

    LayerStateSnapshotTest BuildSnapshot()
    {
        return new LayerStateSnapshotTest(
            currentDefinition: _currentDefinition,
            previousDefinition: _previousDefinition,
            currentLayerId: _currentLayerId,
            previousLayerId: _previousLayerId,
            currentUniverseLayer: _currentUniverseLayer,
            previousUniverseLayer: _previousUniverseLayer,
            phase: _phase,
            timeInPhase: 0f,
            normalizedProgress01: 0f
        );
    }

    void EmitState()
    {
        OnStateChanged?.Invoke(BuildSnapshot());
    }

    void SetPhase(LayerTransitionPhase newPhase)
    {
        if (_phase == newPhase) return;
        _phase = newPhase;
        OnPhaseChanged?.Invoke(_phase);
    }

    void ApplyLayerChange(LayerDefinitionTest targetDef, string reason)
    {
        _previousDefinition = _currentDefinition;
        _previousLayerId = _currentLayerId;
        _previousUniverseLayer = _currentUniverseLayer;

        if (targetDef == null)
        {
            _currentDefinition = null;
            _currentLayerId = defaultLayerId;
            _currentUniverseLayer = UniverseLayer.Macro;
        }
        else
        {
            _currentDefinition = targetDef;
            _currentLayerId = targetDef.layerId;
            _currentUniverseLayer = targetDef.universeLayer;
        }

        bool layerChanged = !string.Equals(_currentLayerId, _previousLayerId, StringComparison.Ordinal);
        if (layerChanged)
            OnLayerChanged?.Invoke(_previousDefinition, _currentDefinition);

        if (debugLog)
        {
            string prev = _previousLayerId ?? "(none)";
            Debug.Log($"[LayerStateManagerTest] {reason}: '{prev}' -> '{_currentLayerId}', phase={_phase}");
        }

        EmitState();
    }

    // ── Public Request API (called by LayerTriggerVolumeTest) ─────────────────
    public void RequestEnter(string layerId, object source, int priorityIgnored)
    {
        if (source == null) return;
        if (string.IsNullOrEmpty(layerId)) return;

        LayerDefinitionTest targetDef = GetDefinition(layerId);
        if (targetDef == null)
        {
            if (debugLog)
                Debug.LogWarning($"[LayerStateManagerTest] Unknown layerId '{layerId}'.");
            return;
        }

        ApplyLayerChange(targetDef, reason: "RequestEnter");
    }

    public void RequestExit(object source)
    {
        // Layer only changes when entering a new trigger, not when leaving one.
        // Zones are sequential and non-overlapping — stay in current layer on exit.
        if (debugLog)
            Debug.Log($"[LayerStateManagerTest] RequestExit ignored — layer stays as '{_currentLayerId}'.");
    }
}
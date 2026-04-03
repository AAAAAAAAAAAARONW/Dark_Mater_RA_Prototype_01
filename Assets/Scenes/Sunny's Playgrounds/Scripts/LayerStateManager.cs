using System;
using UnityEngine;
public class LayerStateManager : MonoBehaviour
{
    [Header("Catalog")]
    [SerializeField] LayerCatalog catalog; // reference to layercatalog .asset
    [Tooltip("layerId used when no trigger volume is active.")]
    [SerializeField] string defaultLayerId = "Micro";
    [Header("Debug")]
    [SerializeField] bool debugLog = true;

    object _activeSource; // stores trigger that requested layer cdhange

    LayerDefinition _currentDefinition;
    string _currentLayerId;
    UniverseLayer _currentUniverseLayer;

    LayerDefinition _previousDefinition;
    string _previousLayerId;
    UniverseLayer _previousUniverseLayer;

    // some possible api signal examples to listen to, can change or add based on request
    LayerTransitionPhase _phase;
    public event Action<LayerDefinition, LayerDefinition> OnLayerChanged;
    public event Action<LayerTransitionPhase> OnPhaseChanged;
    public event Action<LayerStateSnapshot> OnStateChanged;

    void Awake()
    {
        ResolveAndApplyDefault(initial: true);
        EmitState();
    }

    // default state when no layers (can change based on request) 
    void ResolveAndApplyDefault(bool initial)
    {
        LayerDefinition def = GetDefinition(defaultLayerId);
        if (def == null)
        {
            _currentDefinition = null;
            _currentLayerId = defaultLayerId;
            _currentUniverseLayer = UniverseLayer.Micro;
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
            _previousUniverseLayer = UniverseLayer.Micro;
        }
    }

    // this fetches the parameter profile of the current layer
    public LayerDefinition GetDefinition(string layerId)
    {
        if (catalog == null) return null;
        return catalog.GetDefinition(layerId);
    }

    // the following is a api packet that sends information about the current layer, there are dummy values such as time in phase etc that can be added if necessary
    public string CurrentLayerId => _currentLayerId;
    public LayerDefinition CurrentDefinition => _currentDefinition;
    public UniverseLayer CurrentUniverseLayer => _currentUniverseLayer;
    public LayerTransitionPhase Phase => _phase;
    public LayerStateSnapshot Snapshot => BuildSnapshot();
    LayerStateSnapshot BuildSnapshot()
    {
        return new LayerStateSnapshot(
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

    // state emitter
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

    // enter and exit request when trigger hit, called by trigger to make requests
    public void RequestEnter(string layerId, object source, int priorityIgnored)
    {
        if (source == null) return;
        if (string.IsNullOrEmpty(layerId)) return;
        LayerDefinition targetDef = GetDefinition(layerId);
        if (targetDef == null)
        {
            if (debugLog)
                Debug.LogWarning($"[LayerStateManager] Unknown layerId '{layerId}'.");
            return;
        }
        _activeSource = source;
        ApplyLayerChange(targetDef, reason: "RequestEnter");
    }

    public void RequestExit(object source)
    {
        if (source == null) return;
        if (!ReferenceEquals(_activeSource, source))
            return;
        _activeSource = null;
        LayerDefinition targetDef = GetDefinition(defaultLayerId);
        ApplyLayerChange(targetDef, reason: "RequestExitToDefault");
    }

    void ApplyLayerChange(LayerDefinition targetDef, string reason)
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
            Debug.Log($"[LayerStateManager] {reason}: '{prev}' -> '{_currentLayerId}', phase={_phase}");
        }
        EmitState();
    }
}
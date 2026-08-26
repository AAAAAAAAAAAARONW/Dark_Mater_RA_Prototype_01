using System;
using UnityEngine;

/// <summary>
/// The single source of truth for "which layer are we in".
///
/// Replaces LayerStateManagerTest. Three things changed:
///
///   1. The phase machinery is gone. LayerTransitionPhase, OnPhaseChanged, OnStateChanged,
///      Snapshot and RequestExit had zero consumers, and SetPhase() was never called at all
///      so the phase never left Idle. Removing them also drops this file's compile
///      dependency on the old Sunny's Playgrounds LayerStateSnapshot.cs.
///
///   2. Previous-layer bookkeeping only advances when the layer actually changes.
///      The old version overwrote it before the equality check, so re-entering a gate
///      silently destroyed the history that CameraDirector routes on.
///
///   3. EmitInitialLayer() broadcasts the start layer once in Start(). Previously no
///      OnLayerChanged fired for the start layer, and CameraLayerResponder and
///      VisualLayerResponder each hand-rolled their own Start() compensation. Now there
///      is one code path and new responders get startup for free.
///
/// This component owns no game logic. It never touches cameras, renderers or speed.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("STATE", "#8C5CEB")]
public class LayerState_NEW : MonoBehaviour
{
    [Header("Catalog")]
    [SerializeField] LayerCatalog_NEW catalog;

    [Tooltip("Leave empty to use the catalog entry flagged isStartLayer.")]
    [SerializeField] string startLayerIdOverride = "";

    [Header("Startup")]
    [Tooltip("Broadcast OnLayerChanged(null, startLayer) once in Start so responders can " +
             "initialise without each writing their own Start() compensation.")]
    [SerializeField] bool emitInitialLayer = true;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    LayerProfile_NEW _current;
    LayerProfile_NEW _previous;
    bool _initialEmitted;

    /// <summary>Fired when the active layer changes. previous is null on the initial emit.</summary>
    public event Action<LayerProfile_NEW, LayerProfile_NEW> OnLayerChanged;

    public LayerProfile_NEW Current => _current;
    public LayerProfile_NEW Previous => _previous;
    public string CurrentLayerId => _current != null ? _current.layerId : null;
    public LayerCatalog_NEW Catalog => catalog;

    void Awake()
    {
        if (catalog == null)
        {
            Debug.LogError("[LayerState_NEW] No catalog assigned. Layer switching is disabled.", this);
            return;
        }

        _current = !string.IsNullOrEmpty(startLayerIdOverride)
            ? catalog.Get(startLayerIdOverride)
            : catalog.GetStartLayer();

        if (_current == null)
            Debug.LogError("[LayerState_NEW] Could not resolve a start layer.", this);
    }

    void Start()
    {
        if (!emitInitialLayer || _initialEmitted || _current == null) return;

        _initialEmitted = true;
        if (debugLog) Debug.Log($"[LayerState_NEW] Initial layer '{_current.layerId}'.", this);
        OnLayerChanged?.Invoke(null, _current);
    }

    /// <summary>
    /// Called by LayerGate_NEW when the player crosses a gate.
    /// Re-entering the layer already active is a no-op — no event, no history change.
    /// </summary>
    public void RequestEnter(string layerId, Component source)
    {
        if (source == null || string.IsNullOrEmpty(layerId) || catalog == null) return;

        LayerProfile_NEW target = catalog.Get(layerId);
        if (target == null)
        {
            Debug.LogWarning($"[LayerState_NEW] Gate '{source.name}' asked for unknown layerId " +
                             $"'{layerId}'. Ignored.", source);
            return;
        }

        if (ReferenceEquals(target, _current))
        {
            if (debugLog) Debug.Log($"[LayerState_NEW] Already in '{layerId}'. Ignored.", this);
            return;
        }

        // Only advance history on a real change — see note 2 in the class summary.
        _previous = _current;
        _current = target;

        if (debugLog)
        {
            string prev = _previous != null ? _previous.layerId : "(none)";
            Debug.Log($"[LayerState_NEW] '{prev}' -> '{_current.layerId}' (gate: {source.name}).", this);
        }

        OnLayerChanged?.Invoke(_previous, _current);
    }
}

using System.Collections;
using UnityEngine;

/// <summary>
/// Drives RenderLayerTransitionController crossfades.
///
/// No longer reacts to OnLayerChanged directly for transitions.
/// CameraLayerResponder calls CrossfadeTo() at the right moment in its
/// coroutine — when the camera reaches TopDown — so the render swap
/// happens while the player is looking down, not before.
///
/// Still handles startup snap via Start().
///
/// Attach to any persistent GameObject (e.g. GameManager).
/// </summary>
public class VisualLayerResponder : MonoBehaviour
{
    [Header("References")]
    [SerializeField] LayerStateManagerTest stateManager;
    [SerializeField] RenderLayerTransitionController transitionController;

    [Header("Transition")]
    [Tooltip("If true, previous layer hides and new layer shows at exactly the same frame — no crossfade. " +
             "If false, uses transitionDuration from the LayerDefinitionTest asset.")]
    [SerializeField] bool instantSwap = true;

    [Header("Debug")]
    [SerializeField] bool debugLog = true;

    Coroutine _transitionRoutine;

    void Awake()
    {
        if (stateManager == null)
            stateManager = FindObjectOfType<LayerStateManagerTest>();
        if (transitionController == null)
            transitionController = FindObjectOfType<RenderLayerTransitionController>();
    }

    void Start()
    {
        // Snap to the starting layer immediately on play
        if (transitionController == null || stateManager == null) return;
        transitionController.RebuildCaches();
        transitionController.SetImmediately(stateManager.CurrentLayerId);
        if (debugLog)
            Debug.Log($"[VisualLayerResponder] Initialized — snapped to '{stateManager.CurrentLayerId}'.");
    }

    // ── Public API — called by CameraLayerResponder at the right moment ────────

    /// <summary>
    /// Trigger the crossfade to the given layer's render group.
    /// Called by CameraLayerResponder when the camera reaches TopDown,
    /// so the swap happens while the player is looking down.
    /// </summary>
    public void CrossfadeTo(LayerDefinitionTest layerDef)
    {
        if (layerDef == null || transitionController == null) return;

        if (_transitionRoutine != null)
            StopCoroutine(_transitionRoutine);

        _transitionRoutine = StartCoroutine(RunCrossfade(layerDef));
    }

    IEnumerator RunCrossfade(LayerDefinitionTest layerDef)
    {
        if (instantSwap)
        {
            transitionController.SetImmediately(layerDef.layerId);
            if (debugLog)
                Debug.Log($"[VisualLayerResponder] Instant swap to '{layerDef.layerId}'.");
        }
        else
        {
            transitionController.CrossfadeTo(layerDef.layerId, layerDef.transitionDuration);
            if (debugLog)
                Debug.Log($"[VisualLayerResponder] Crossfading to '{layerDef.layerId}' " +
                          $"over {layerDef.transitionDuration}s.");
        }

        _transitionRoutine = null;
        yield break;
    }
}
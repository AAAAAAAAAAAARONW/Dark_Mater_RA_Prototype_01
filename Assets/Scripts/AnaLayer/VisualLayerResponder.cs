using System.Collections;
using UnityEngine;

/// <summary>
/// Listens to LayerStateManagerTest.OnLayerChanged and tells
/// RenderLayerTransitionController which group to crossfade to.
///
/// Respects flareLeadTime — waits before starting the crossfade
/// so the flare fires first.
///
/// Attach to any persistent GameObject (e.g. GameManager).
/// </summary>
public class VisualLayerResponder : MonoBehaviour
{
    [Header("References")]
    [SerializeField] LayerStateManagerTest stateManager;
    [SerializeField] RenderLayerTransitionController transitionController;

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
        // Snap to the default layer's render group on startup
        if (transitionController == null || stateManager == null) return;
        string defaultId = stateManager.CurrentLayerId;
        transitionController.RebuildCaches();
        transitionController.SetImmediately(defaultId);
        if (debugLog)
            Debug.Log($"[VisualLayerResponder] Initialized — snapped to '{defaultId}'.");
    }

    void OnEnable()
    {
        if (stateManager != null)
            stateManager.OnLayerChanged += HandleLayerChanged;
    }

    void OnDisable()
    {
        if (stateManager != null)
            stateManager.OnLayerChanged -= HandleLayerChanged;
    }

    void HandleLayerChanged(LayerDefinitionTest previous, LayerDefinitionTest current)
    {
        if (current == null) return;
        if (transitionController == null)
        {
            Debug.LogWarning("[VisualLayerResponder] RenderLayerTransitionController not assigned.");
            return;
        }

        if (_transitionRoutine != null)
            StopCoroutine(_transitionRoutine);

        _transitionRoutine = StartCoroutine(RunTransition(current));
    }

    IEnumerator RunTransition(LayerDefinitionTest current)
    {
        // Wait for flare lead time before starting crossfade
        if (current.useFlareBeforeSwitch && current.flareLeadTime > 0f)
        {
            if (debugLog)
                Debug.Log($"[VisualLayerResponder] Waiting {current.flareLeadTime}s for flare.");
            yield return new WaitForSeconds(current.flareLeadTime);
        }

        transitionController.CrossfadeTo(current.layerId, current.transitionDuration);

        if (debugLog)
            Debug.Log($"[VisualLayerResponder] Crossfading to '{current.layerId}' " +
                      $"over {current.transitionDuration}s.");

        _transitionRoutine = null;
    }
}
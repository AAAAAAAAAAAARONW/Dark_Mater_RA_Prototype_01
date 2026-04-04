using UnityEngine;

/// <summary>
/// Listens to LayerStateManagerTest.OnLayerChanged and updates UniverseJourneyTracker
/// to match the new layer's journey phase.
///
/// Enter a non-default layer → pins the phase defined in that layer's profile.
/// Enter the default layer   → unpins, resumes auto-detection.
///
/// Attach to any persistent GameObject (e.g. GameManager).
/// </summary>
public class JourneyLayerResponder : MonoBehaviour
{
    [Header("References")]
    [SerializeField] LayerStateManagerTest stateManager;
    [SerializeField] UniverseJourneyTracker journeyTracker;

    [Header("Debug")]
    [SerializeField] bool debugLog = true;

    void Awake()
    {
        if (stateManager == null)
            stateManager = FindObjectOfType<LayerStateManagerTest>();
        if (journeyTracker == null)
            journeyTracker = FindObjectOfType<UniverseJourneyTracker>();
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
        if (journeyTracker == null)
        {
            Debug.LogWarning("[JourneyLayerResponder] UniverseJourneyTracker not assigned.");
            return;
        }

        if (current == null) return;

        if (current.isDefaultLayer)
        {
            // Returning to baseline — unpin so auto-detection resumes from remaining distance
            journeyTracker.UnpinPhase();
            if (debugLog)
                Debug.Log($"[JourneyLayerResponder] Default layer '{current.layerId}' — unpinned phase, auto-detect resumed.");
        }
        else
        {
            // Entering a specific zone — pin to the phase defined in this layer's profile
            journeyTracker.SetPhase(current.phaseOnEnter);
            if (debugLog)
                Debug.Log($"[JourneyLayerResponder] Layer '{current.layerId}' — pinned phase to {current.phaseOnEnter}.");
        }
    }
}

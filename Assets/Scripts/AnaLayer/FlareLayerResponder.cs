using UnityEngine;

/// <summary>
/// Listens to LayerStateManagerTest.OnLayerChanged and triggers a camera
/// flare flash when entering a new layer, if configured in the layer profile.
///
/// Attach to any persistent GameObject (e.g. GameManager).
/// </summary>
public class FlareLayerResponder : MonoBehaviour
{
    [Header("References")]
    [SerializeField] LayerStateManagerTest stateManager;
    [SerializeField] CameraFlareFlash flareFlash;

    [Header("Debug")]
    [SerializeField] bool debugLog = true;

    void Awake()
    {
        if (stateManager == null)
            stateManager = FindObjectOfType<LayerStateManagerTest>();
        if (flareFlash == null)
            flareFlash = FindObjectOfType<CameraFlareFlash>();
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
        if (!current.useFlareBeforeSwitch) return;

        if (flareFlash == null)
        {
            Debug.LogWarning("[FlareLayerResponder] CameraFlareFlash not assigned.");
            return;
        }

        flareFlash.PlayFlash(current.flareDuration, current.flarePeakAlpha, current.flareColor);

        if (debugLog)
            Debug.Log($"[FlareLayerResponder] Layer '{current.layerId}' — playing flare " +
                      $"(duration={current.flareDuration}, alpha={current.flarePeakAlpha}).");
    }
}

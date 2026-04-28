using UnityEngine;

/// <summary>
/// Bridges LayerStateManagerTest layer-change events to LAFSpectrumHUD.
///
/// When the active layer changes, this reads the new LayerDefinition's LAF
/// parameters and passes them to the HUD so scroll speed and spectrum shape
/// update automatically.
///
/// Attach to any persistent GameObject (e.g. GameManager).
/// Wire up stateManager and lafHUD in the Inspector, or leave blank for auto-find.
/// </summary>
public class LAFLayerResponder : MonoBehaviour
{
    [Header("References")]
    [SerializeField] LayerStateManagerTest stateManager;
    [SerializeField] LAFSpectrumHUD lafHUD;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    void Awake()
    {
        if (stateManager == null)
            stateManager = FindObjectOfType<LayerStateManagerTest>();
        if (lafHUD == null)
            lafHUD = FindObjectOfType<LAFSpectrumHUD>();
    }

    void OnEnable()
    {
        if (stateManager != null)
            stateManager.OnLayerChanged += HandleLayerChanged;

        // Apply current layer immediately on enable.
        if (stateManager != null && lafHUD != null)
            ApplyDefinition(stateManager.CurrentDefinition);
    }

    void OnDisable()
    {
        if (stateManager != null)
            stateManager.OnLayerChanged -= HandleLayerChanged;
    }

    void HandleLayerChanged(LayerDefinitionTest previous, LayerDefinitionTest current)
    {
        ApplyDefinition(current);
    }

    void ApplyDefinition(LayerDefinitionTest def)
    {
        if (lafHUD == null) return;

        if (def == null)
        {
            if (debugLog)
                Debug.Log("[LAFLayerResponder] No LayerDefinitionTest — HUD keeps current settings.");
            return;
        }

        lafHUD.Configure(def);

        if (debugLog)
            Debug.Log($"[LAFLayerResponder] Applied layer '{def.layerId}': " +
                      $"scrollSpeed={def.lafScrollSpeedStepsPerSecond:F1}, " +
                      $"dips={def.lafDipCount}, seed={def.lafRandomSeed}");
    }
}

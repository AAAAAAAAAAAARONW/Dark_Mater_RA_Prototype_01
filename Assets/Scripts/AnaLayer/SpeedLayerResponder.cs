using UnityEngine;

/// <summary>
/// Listens to LayerStateManagerTest.OnLayerChanged and tweens the player's
/// external speed multiplier to match the new layer's profile.
///
/// Enter a non-default layer → tween to enterSpeedMultiplier.
/// Enter the default layer   → tween to exitSpeedMultiplier.
///
/// Attach to any persistent GameObject (e.g. GameManager).
/// </summary>
public class SpeedLayerResponder : MonoBehaviour
{
    [Header("References")]
    [SerializeField] LayerStateManagerTest stateManager;
    [SerializeField] DarkMatterPlayerControllerTest playerController;

    [Header("Debug")]
    [SerializeField] bool debugLog = true;

    Coroutine _speedRoutine;

    void Awake()
    {
        if (stateManager == null)
            stateManager = FindObjectOfType<LayerStateManagerTest>();
        if (playerController == null)
            playerController = FindObjectOfType<DarkMatterPlayerControllerTest>();
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
        if (playerController == null)
        {
            Debug.LogWarning("[SpeedLayerResponder] DarkMatterPlayerControllerTest not assigned.");
            return;
        }

        if (current == null) return;

        // Use enterSpeedMultiplier when entering a zone, exitSpeedMultiplier when returning to default
        float targetMultiplier = current.isDefaultLayer
            ? current.exitSpeedMultiplier
            : current.enterSpeedMultiplier;

        if (_speedRoutine != null)
            StopCoroutine(_speedRoutine);

        _speedRoutine = StartCoroutine(
            playerController.TweenExternalSpeedMultiplier(targetMultiplier, current.speedBlendDuration)
        );

        if (debugLog)
            Debug.Log($"[SpeedLayerResponder] Layer '{current.layerId}' — tweening speed to {targetMultiplier} over {current.speedBlendDuration}s.");
    }
}

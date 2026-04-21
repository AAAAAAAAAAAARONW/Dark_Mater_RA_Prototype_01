using System.Collections;
using UnityEngine;

/// <summary>
/// Listens to LayerStateManagerTest.OnLayerChanged and tweens the player's
/// external speed multiplier to match the new layer's profile.
///
/// Enter a non-default layer -> tween to zoneSpeedMultiplier.
/// Enter the default layer   -> tween to returnSpeedMultiplier.
///
/// Speed tween is delayed by transitionDuration to avoid jiggle caused by
/// the camera blend and speed ramp fighting each other simultaneously.
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

        float targetMultiplier = current.isDefaultLayer
            ? current.returnSpeedMultiplier
            : current.zoneSpeedMultiplier;

        if (_speedRoutine != null)
            StopCoroutine(_speedRoutine);

        _speedRoutine = StartCoroutine(
            DelayedSpeedTween(targetMultiplier, current.speedBlendDuration, current.transitionDuration)
        );

        if (debugLog)
            Debug.Log("[SpeedLayerResponder] Layer '" + current.layerId + "' - speed tween to " + targetMultiplier + " over " + current.speedBlendDuration + "s (delay: " + current.transitionDuration + "s).");
    }

    IEnumerator DelayedSpeedTween(float targetMultiplier, float blendDuration, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        yield return StartCoroutine(
            playerController.TweenExternalSpeedMultiplier(targetMultiplier, blendDuration)
        );

        if (debugLog)
            Debug.Log("[SpeedLayerResponder] Speed tween complete -> " + targetMultiplier);
    }
}
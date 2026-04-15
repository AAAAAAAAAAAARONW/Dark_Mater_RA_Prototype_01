using System.Collections;
using UnityEngine;
using Cinemachine;

/// <summary>
/// Listens to LayerStateManagerTest.OnLayerChanged and handles all camera
/// behaviour for layer transitions.
///
/// Three possible sequences depending on LayerDefinitionTest config:
///
///   Look-Back (useLookBackSequence = true, e.g. Quasar):
///     vcamLookBack → hold → vcamMacro
///
///   Look-Down Enter (normal zone entry, e.g. Micro/Galaxy):
///     vcamTopDown → hold → vcamMicro
///
///   Look-Down Exit (returning to default/Macro):
///     vcamTopDown → hold → vcamMacro
///
/// Attach to any persistent GameObject (e.g. GameManager).
/// </summary>
public class CameraLayerResponder : MonoBehaviour
{
    [Header("References")]
    [SerializeField] LayerStateManagerTest stateManager;
    [SerializeField] DarkMatterPlayerControllerTest playerController;
    [SerializeField] VisualLayerResponder visualResponder;

    [Header("Cinemachine Cameras")]
    [Tooltip("Normal gameplay camera (Macro / forward-facing)")]
    [SerializeField] CinemachineFreeLook vcamMacro;
    [Tooltip("Inside zone camera (Micro / Galaxy)")]
    [SerializeField] CinemachineFreeLook vcamMicro;
    [Tooltip("Backward-facing camera for Quasar / entry sequence")]
    [SerializeField] CinemachineFreeLook vcamLookBack;
    [Tooltip("Top-down camera for look-down sequence")]
    [SerializeField] CinemachineVirtualCamera vcamTopDown;

    [Header("Debug")]
    [SerializeField] bool debugLog = true;

    Coroutine _sequenceRoutine;

    // ── Priority constants ────────────────────────────────────────────────────
    const int PriorityHigh = 20;
    const int PriorityNormal = 10;
    const int PriorityOff = 0;

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        if (stateManager == null)
            stateManager = FindObjectOfType<LayerStateManagerTest>();
        if (playerController == null)
            playerController = FindObjectOfType<DarkMatterPlayerControllerTest>();
        if (visualResponder == null)
            visualResponder = FindObjectOfType<VisualLayerResponder>();
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

    // ── Event Handler ─────────────────────────────────────────────────────────

    void HandleLayerChanged(LayerDefinitionTest previous, LayerDefinitionTest current)
    {
        if (current == null) return;
        if (playerController == null)
        {
            Debug.LogWarning("[CameraLayerResponder] DarkMatterPlayerControllerTest not assigned.");
            return;
        }

        // Stop any in-progress sequence and unlock input before starting new one
        if (_sequenceRoutine != null)
        {
            StopCoroutine(_sequenceRoutine);
            playerController.SetCameraInputLocked(false);
        }

        _sequenceRoutine = StartCoroutine(PlaySequence(current));
    }

    // ── Sequences ─────────────────────────────────────────────────────────────

    IEnumerator PlaySequence(LayerDefinitionTest current)
    {
        if (current.lockLookInputDuringLookDown)
            playerController.SetCameraInputLocked(true);

        if (current.useLookBackSequence)
            yield return StartCoroutine(LookBackSequence(current));
        else if (current.isDefaultLayer)
            yield return StartCoroutine(LookDownExitSequence(current));
        else
            yield return StartCoroutine(LookDownEnterSequence(current));

        if (current.lockLookInputDuringLookDown)
            playerController.SetCameraInputLocked(false);

        if (debugLog)
            Debug.Log($"[CameraLayerResponder] Layer '{current.layerId}' — sequence complete.");

        _sequenceRoutine = null;
    }

    /// <summary>
    /// Quasar entry: face backward, hold, blend forward to vcamMacro.
    /// </summary>
    IEnumerator LookBackSequence(LayerDefinitionTest current)
    {
        if (debugLog)
            Debug.Log($"[CameraLayerResponder] '{current.layerId}' — look-back sequence.");

        // 1. Switch to look-back camera
        SetPriority(vcamLookBack, PriorityHigh);
        SetPriority(vcamMacro, PriorityNormal);
        SetPriority(vcamMicro, PriorityOff);
        SetPriority(vcamTopDown, PriorityOff);

        // 2. Hold looking back
        yield return new WaitForSeconds(current.lookBackHoldDuration);

        // 3. Blend forward to vcamMacro
        SetPriority(vcamMacro, PriorityHigh);
        SetPriority(vcamLookBack, PriorityOff);

        yield return new WaitForSeconds(current.lookBackBlendDuration);
    }

    /// <summary>
    /// Normal zone entry (Micro/Galaxy): look down, hold, come up into vcamMicro.
    /// </summary>
    IEnumerator LookDownEnterSequence(LayerDefinitionTest current)
    {
        if (debugLog)
            Debug.Log($"[CameraLayerResponder] '{current.layerId}' — look-down enter sequence.");

        // 1. Switch to TopDown — previous layer still fully visible
        SetPriority(vcamTopDown, PriorityHigh);
        SetPriority(vcamMicro, PriorityOff);
        SetPriority(vcamMacro, PriorityNormal);
        SetPriority(vcamLookBack, PriorityOff);

        yield return new WaitForSeconds(current.enterLookDownDuration);

        // 2. Camera is now looking down — trigger render crossfade here
        //    Previous layer fades out, new layer fades in while player can't see either
        visualResponder?.CrossfadeTo(current);

        // 3. Hold at top-down while crossfade plays
        yield return new WaitForSeconds(current.lookDownHoldDuration);

        // 4. Pause before looking up
        if (current.lookUpStartDelay > 0f)
            yield return new WaitForSeconds(current.lookUpStartDelay);

        // 5. Come up into new layer camera — new render layer is already active
        SetPriority(vcamMicro, PriorityHigh);
        SetPriority(vcamTopDown, PriorityOff);

        yield return new WaitForSeconds(current.lookUpBlendDuration);
    }

    /// <summary>
    /// Returning to default (Macro): look down, hold, come up into vcamMacro.
    /// </summary>
    IEnumerator LookDownExitSequence(LayerDefinitionTest current)
    {
        if (debugLog)
            Debug.Log($"[CameraLayerResponder] '{current.layerId}' — look-down exit sequence.");

        // 1. Switch to TopDown — previous layer still fully visible
        SetPriority(vcamTopDown, PriorityHigh);
        SetPriority(vcamMicro, PriorityOff);
        SetPriority(vcamMacro, PriorityNormal);
        SetPriority(vcamLookBack, PriorityOff);

        yield return new WaitForSeconds(current.lookDownBlendDuration);

        // 2. Camera is looking down — trigger render crossfade to default layer
        visualResponder?.CrossfadeTo(current);

        // 3. Hold while crossfade plays
        yield return new WaitForSeconds(current.lookDownHoldDuration);

        // 4. Pause before looking up
        if (current.lookUpStartDelay > 0f)
            yield return new WaitForSeconds(current.lookUpStartDelay);

        // 5. Come up into Macro camera — new layer already active
        SetPriority(vcamMacro, PriorityHigh);
        SetPriority(vcamTopDown, PriorityOff);

        yield return new WaitForSeconds(current.lookUpBlendDuration);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void SetPriority(CinemachineFreeLook vcam, int priority)
    {
        if (vcam != null) vcam.Priority = priority;
    }

    void SetPriority(CinemachineVirtualCamera vcam, int priority)
    {
        if (vcam != null) vcam.Priority = priority;
    }
}
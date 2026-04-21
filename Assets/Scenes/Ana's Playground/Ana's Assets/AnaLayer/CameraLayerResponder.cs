using System.Collections;
using UnityEngine;
using Cinemachine;

/// <summary>
/// Listens to LayerStateManagerTest.OnLayerChanged and handles all camera
/// behaviour for layer transitions.
///
/// Blend durations are read directly from CinemachineBlenderSettings at runtime -
/// no manual sync required between coroutine waits and Blender Settings.
///
/// Sequences:
///
///   Look-Back (useLookBackSequence = true, e.g. Quasar):
///     Countdown timer -> orbit vcamLookBack 180 -> 0 degrees.
///     Orbit may still be running when trigger fires - QuasarToMacroSequence waits for it.
///
///   Quasar -> Macro (previous.useLookBackSequence = true):
///     Wait for orbit if still running -> crossfade renders -> blend to vcamMacro.
///
///   Look-Down Enter (normal zone entry):
///     vcamTopDown -> crossfade -> vcamMicro
///
///   Look-Down Exit (returning to default):
///     vcamTopDown -> crossfade -> vcamMacro
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
    [Tooltip("Top-down camera used as a transition cover between layers")]
    [SerializeField] CinemachineVirtualCamera vcamTopDown;

    [Header("Cinemachine Blender Settings")]
    [Tooltip("Assign the Main Camera Blends asset here - used to read blend durations at runtime.")]
    [SerializeField] CinemachineBlenderSettings blenderSettings;

    [Header("Debug")]
    [SerializeField] bool debugLog = true;

    Coroutine _sequenceRoutine;
    Coroutine _orbitRoutine;
    CinemachineBrain _brain;

    // Priority constants
    const int PriorityHigh = 20;
    const int PriorityNormal = 10;
    const int PriorityOff = 0;

    // Unity Lifecycle

    void Awake()
    {
        if (stateManager == null)
            stateManager = FindObjectOfType<LayerStateManagerTest>();
        if (playerController == null)
            playerController = FindObjectOfType<DarkMatterPlayerControllerTest>();
        if (visualResponder == null)
            visualResponder = FindObjectOfType<VisualLayerResponder>();

        _brain = FindObjectOfType<CinemachineBrain>();

        if (_brain == null)
            Debug.LogWarning("[CameraLayerResponder] No CinemachineBrain found in scene.");
    }

    void Start()
    {
        // Quasar is the default layer - OnLayerChanged never fires for it on startup.
        // Manually start the lookback sequence so the orbit countdown begins on play.
        if (stateManager != null)
        {
            LayerDefinitionTest current = stateManager.CurrentDefinition;
            if (current != null && current.useLookBackSequence)
            {
                if (debugLog)
                    Debug.Log("[CameraLayerResponder] Start - default layer has useLookBackSequence, starting countdown.");
                _sequenceRoutine = StartCoroutine(LookBackSequence(current));
            }
        }
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

    // Event Handler

    void HandleLayerChanged(LayerDefinitionTest previous, LayerDefinitionTest current)
    {
        if (current == null) return;
        if (playerController == null)
        {
            Debug.LogWarning("[CameraLayerResponder] DarkMatterPlayerControllerTest not assigned.");
            return;
        }

        if (_sequenceRoutine != null)
        {
            StopCoroutine(_sequenceRoutine);
            playerController.SetCameraInputLocked(false);
        }

        _sequenceRoutine = StartCoroutine(PlaySequence(previous, current));
    }

    // Sequence Router

    IEnumerator PlaySequence(LayerDefinitionTest previous, LayerDefinitionTest current)
    {
        if (current.lockLookInputDuringLookDown)
            playerController.SetCameraInputLocked(true);

        if (current.useLookBackSequence)
            yield return StartCoroutine(LookBackSequence(current));
        else if (previous != null && previous.useLookBackSequence)
            yield return StartCoroutine(QuasarToMacroSequence(current));
        else if (current.isDefaultLayer)
            yield return StartCoroutine(LookDownExitSequence(current));
        else
            yield return StartCoroutine(LookDownEnterSequence(current));

        if (current.lockLookInputDuringLookDown)
            playerController.SetCameraInputLocked(false);

        if (debugLog)
            Debug.Log("[CameraLayerResponder] Layer '" + current.layerId + "' - sequence complete.");

        _sequenceRoutine = null;
    }

    // Sequences

    /// <summary>
    /// Quasar: face backward, wait for countdown, then start orbit.
    /// Orbit may still be running when the Macro trigger fires -
    /// QuasarToMacroSequence will wait for it to finish.
    /// </summary>
    IEnumerator LookBackSequence(LayerDefinitionTest current)
    {
        if (debugLog)
            Debug.Log("[CameraLayerResponder] '" + current.layerId + "' - look-back sequence, orbit in " + current.lookBackOrbitCountdown + "s.");

        SetPriority(vcamLookBack, PriorityHigh);
        SetPriority(vcamMacro, PriorityNormal);
        SetPriority(vcamMicro, PriorityOff);
        SetPriority(vcamTopDown, PriorityOff);

        // Wait countdown before starting orbit
        yield return new WaitForSeconds(current.lookBackOrbitCountdown);

        // Start orbit - trigger box may fire before this completes
        if (_orbitRoutine != null)
            StopCoroutine(_orbitRoutine);

        _orbitRoutine = StartCoroutine(OrbitRoutine(current.lookBackOrbitDuration));
        yield return _orbitRoutine;
    }

    /// <summary>
    /// Shared orbit coroutine - animates vcamLookBack.m_XAxis from current angle to 0 degrees.
    /// Disables Cinemachine input axis during orbit to prevent fighting the lerp.
    /// </summary>
    IEnumerator OrbitRoutine(float duration)
    {
        if (debugLog)
            Debug.Log("[CameraLayerResponder] Orbit started.");

        playerController.SetCameraInputLocked(true);

        // Disable Cinemachine's own input so it doesn't fight the lerp
        string savedXAxisName = vcamLookBack.m_XAxis.m_InputAxisName;
        string savedYAxisName = vcamLookBack.m_YAxis.m_InputAxisName;
        vcamLookBack.m_XAxis.m_InputAxisName = "";
        vcamLookBack.m_YAxis.m_InputAxisName = "";

        float elapsed = 0f;
        float startAngle = vcamLookBack.m_XAxis.Value;
        float endAngle = 180f; // 180 = forward given Heading Bias of 180 on vcamLookBack

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            vcamLookBack.m_XAxis.Value = Mathf.Lerp(startAngle, endAngle, smoothT);
            yield return null;
        }

        // Snap exactly to forward
        vcamLookBack.m_XAxis.Value = 180f;
        vcamLookBack.m_XAxis.m_InputAxisName = savedXAxisName;
        vcamLookBack.m_YAxis.m_InputAxisName = savedYAxisName;

        playerController.SetCameraInputLocked(false);

        if (debugLog)
            Debug.Log("[CameraLayerResponder] Orbit complete.");

        _orbitRoutine = null;
    }

    /// <summary>
    /// Quasar -> Macro: waits for orbit if still running, then crossfades
    /// renders and blends to vcamMacro.
    /// </summary>
    IEnumerator QuasarToMacroSequence(LayerDefinitionTest current)
    {
        if (debugLog)
            Debug.Log("[CameraLayerResponder] Quasar -> '" + current.layerId + "' - quasar-to-macro sequence.");

        SetPriority(vcamLookBack, PriorityHigh);
        SetPriority(vcamMacro, PriorityNormal);
        SetPriority(vcamMicro, PriorityOff);
        SetPriority(vcamTopDown, PriorityOff);

        // Crossfade renders immediately when trigger fires - Macro appears during orbit
        visualResponder?.CrossfadeTo(current);

        // If orbit is still running, wait for it to finish
        if (_orbitRoutine != null)
        {
            if (debugLog)
                Debug.Log("[CameraLayerResponder] Orbit still in progress - waiting.");
            yield return _orbitRoutine;
        }
        // Fallback: orbit never started (trigger fired before countdown), run it now
        else if (vcamLookBack.m_XAxis.Value != 180f)
        {
            if (debugLog)
                Debug.Log("[CameraLayerResponder] Orbit not started yet - running now as fallback.");
            yield return StartCoroutine(OrbitRoutine(current.lookBackOrbitDuration));
        }

        // Blend from lookback to Macro
        SetPriority(vcamMacro, PriorityHigh);
        SetPriority(vcamLookBack, PriorityOff);

        yield return new WaitForSeconds(GetBlendDuration(vcamLookBack, vcamMacro));
    }

    /// <summary>
    /// Normal zone entry (Micro/Galaxy/MilkyWay etc):
    /// look down, crossfade renders, come up into vcamMicro.
    /// </summary>
    IEnumerator LookDownEnterSequence(LayerDefinitionTest current)
    {
        if (debugLog)
            Debug.Log("[CameraLayerResponder] '" + current.layerId + "' - look-down enter sequence.");

        // 1. Blend to topdown
        SetPriority(vcamTopDown, PriorityHigh);
        SetPriority(vcamMicro, PriorityOff);
        SetPriority(vcamMacro, PriorityNormal);
        SetPriority(vcamLookBack, PriorityOff);

        // Wait for blend - player can no longer see either layer
        yield return new WaitForSeconds(GetBlendDuration(vcamMacro, vcamTopDown));

        // 2. Crossfade renders immediately - topdown is fully in
        visualResponder?.CrossfadeTo(current);

        // 3. Hold while crossfade plays
        yield return new WaitForSeconds(current.lookDownHoldDuration);

        // 4. Optional delay before looking up
        if (current.lookUpStartDelay > 0f)
            yield return new WaitForSeconds(current.lookUpStartDelay);

        // 5. Blend up into new layer camera
        SetPriority(vcamMicro, PriorityHigh);
        SetPriority(vcamTopDown, PriorityOff);

        yield return new WaitForSeconds(GetBlendDuration(vcamTopDown, vcamMicro));
    }

    /// <summary>
    /// Returning to default (Macro): look down, crossfade renders, come up into vcamMacro.
    /// </summary>
    IEnumerator LookDownExitSequence(LayerDefinitionTest current)
    {
        if (debugLog)
            Debug.Log("[CameraLayerResponder] '" + current.layerId + "' - look-down exit sequence.");

        // 1. Blend to topdown
        SetPriority(vcamTopDown, PriorityHigh);
        SetPriority(vcamMicro, PriorityOff);
        SetPriority(vcamMacro, PriorityNormal);
        SetPriority(vcamLookBack, PriorityOff);

        yield return new WaitForSeconds(GetBlendDuration(vcamMicro, vcamTopDown));

        // 2. Crossfade renders
        visualResponder?.CrossfadeTo(current);

        // 3. Hold while crossfade plays
        yield return new WaitForSeconds(current.lookDownHoldDuration);

        // 4. Optional delay before looking up
        if (current.lookUpStartDelay > 0f)
            yield return new WaitForSeconds(current.lookUpStartDelay);

        // 5. Blend up into Macro
        SetPriority(vcamMacro, PriorityHigh);
        SetPriority(vcamTopDown, PriorityOff);

        yield return new WaitForSeconds(GetBlendDuration(vcamTopDown, vcamMacro));
    }

    // Helpers

    /// <summary>
    /// Reads blend duration from CinemachineBlenderSettings asset.
    /// Falls back to CinemachineBrain default blend if no custom entry found.
    /// </summary>
    float GetBlendDuration(CinemachineVirtualCameraBase from, CinemachineVirtualCameraBase to)
    {
        if (from == null || to == null)
        {
            Debug.LogWarning("[CameraLayerResponder] Null vcam passed to GetBlendDuration - defaulting to 1s.");
            return 1f;
        }

        if (blenderSettings != null)
        {
            for (int i = 0; i < blenderSettings.m_CustomBlends.Length; i++)
            {
                var item = blenderSettings.m_CustomBlends[i];
                if (item.m_From == from.Name && item.m_To == to.Name)
                {
                    if (debugLog)
                        Debug.Log("[CameraLayerResponder] Blend " + from.Name + " -> " + to.Name + " = " + item.m_Blend.m_Time + "s");
                    return item.m_Blend.m_Time;
                }
            }
        }

        if (_brain != null)
        {
            if (debugLog)
                Debug.Log("[CameraLayerResponder] No custom blend found for " + from.Name + " -> " + to.Name + ", using brain default = " + _brain.m_DefaultBlend.m_Time + "s");
            return _brain.m_DefaultBlend.m_Time;
        }

        Debug.LogWarning("[CameraLayerResponder] Could not resolve blend duration - defaulting to 1s.");
        return 1f;
    }

    void SetPriority(CinemachineFreeLook vcam, int priority)
    {
        if (vcam != null) vcam.Priority = priority;
    }

    void SetPriority(CinemachineVirtualCamera vcam, int priority)
    {
        if (vcam != null) vcam.Priority = priority;
    }
}
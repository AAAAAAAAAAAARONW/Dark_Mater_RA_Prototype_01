using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Cleaned-up version of LayerDefinition.
/// Changes from original:
///   - Added isDefaultLayer flag (used by listeners to detect enter vs exit direction)
///   - Removed scene-ref fields (yellowFilamentRenderers - can't live in a ScriptableObject)
///   - Removed trigger detection fields (requireTag, usePollingFallback, pollingInterval,
///     autoAddKinematicRigidbody - these belong in LayerTriggerVolume, not the profile)
///   - Renamed enterSpeedMultiplier -> zoneSpeedMultiplier
///   - Renamed exitSpeedMultiplier -> returnSpeedMultiplier
///   - Removed camera blend duration fields (enterLookDownDuration, lookDownBlendDuration,
///     lookUpBlendDuration, lookBackBlendDuration) - now read directly from
///     CinemachineBlenderSettings at runtime by CameraLayerResponder
///   - Added lookBackOrbitCountdown and lookBackOrbitDuration for Quasar orbit sequence
/// </summary>
[CreateAssetMenu(menuName = "Layer Profiles/Layer Definition Test")]
public class LayerDefinitionTest : ScriptableObject
{
    [Header("Identity")]
    public string layerId = "Macro";
    public UniverseLayer universeLayer = UniverseLayer.Macro;
    [Tooltip("Mark true on your single default/fallback layer (e.g. Quasar). " +
             "Listeners use this to distinguish returning-to-baseline from entering a new zone. " +
             "Only one LayerDefinitionTest in the registry should have this set to true.")]
    public bool isDefaultLayer = false;

    [Header("Blend Timing")]
    public float transitionDuration = 3f;

    [Header("Speed")]
    [Tooltip("Speed multiplier applied when the player is inside this zone.")]
    [FormerlySerializedAs("enterSpeedMultiplier")]
    public float zoneSpeedMultiplier = 0.3f;
    [Tooltip("Speed multiplier applied when the player returns to this layer as home base.")]
    [FormerlySerializedAs("exitSpeedMultiplier")]
    public float returnSpeedMultiplier = 1f;
    public float speedBlendDuration = 3f;

    [Header("Flare Before Switch")]
    public bool useFlareBeforeSwitch = true;
    public Color flareColor = Color.white;
    [Range(0f, 1f)] public float flarePeakAlpha = 1f;
    public float flareDuration = 2f;
    public float flareLeadTime = 0.08f;

    [Header("Yellow Filament Transparency")]
    public bool driveYellowFilamentTransparency = true;
    public string yellowFilamentShaderName = "Custom/YellowFilamentNebulaVolume";
    public float filamentOpaqueValue = 1f;
    public float filamentHiddenValue = 0f;
    public float filamentBlendDuration = 3f;

    [Header("Camera Sequence")]
    [Tooltip("Lock player camera input during the transition sequence.")]
    public bool lockLookInputDuringLookDown = true;
    [Tooltip("How long to hold at top-down before blending up into the new layer camera.")]
    public float lookDownHoldDuration = 3f;
    [Tooltip("Optional pause before the camera starts blending back up.")]
    public float lookUpStartDelay = 0.12f;

    [Header("Camera Look-Back Sequence (Quasar / entry layers)")]
    [Tooltip("Use this instead of look-down for layers where the camera should face backward first.")]
    public bool useLookBackSequence = false;
    [Tooltip("How long to hold the look-back camera before the orbit countdown begins.")]
    public float lookBackHoldDuration = 4f;
    [Tooltip("Countdown from Quasar layer start before orbit begins. " +
             "Set so orbit completes around the time the player reaches the Macro trigger.")]
    public float lookBackOrbitCountdown = 5f;
    [Tooltip("Duration of the orbital rotation from look-back (180 degrees) to forward-facing (0 degrees).")]
    public float lookBackOrbitDuration = 2.5f;

    [Header("HUD")]
    [Tooltip("Micro travel slider max light years when this layer is active. 0 = use default.")]
    public float microSliderMaxLightYearsWhenInside = 0f;

    [Header("Journey")]
    [Tooltip("Journey phase to pin when entering this layer.")]
    public UniverseJourneyTracker.JourneyPhase phaseOnEnter = UniverseJourneyTracker.JourneyPhase.MilkyWay;
    [Tooltip("Only used when isDefaultLayer = true. Phase to set when returning to baseline.")]
    public UniverseJourneyTracker.JourneyPhase phaseOnExit = UniverseJourneyTracker.JourneyPhase.CosmicWeb1;

    [Header("Debug")]
    public bool debugLog = true;

    // Helpers

    /// <summary>
    /// Approximate total duration of an enter sequence.
    /// Blend durations are not included - those are read from CinemachineBlenderSettings at runtime.
    /// </summary>
    public float GetEnterSequenceDuration()
    {
        float flare = useFlareBeforeSwitch ? flareLeadTime : 0f;
        return flare + lookDownHoldDuration + Mathf.Max(0f, lookUpStartDelay);
    }

    /// <summary>
    /// Approximate total duration of an exit sequence.
    /// Blend durations are not included - those are read from CinemachineBlenderSettings at runtime.
    /// </summary>
    public float GetExitSequenceDuration()
    {
        float flare = useFlareBeforeSwitch ? flareLeadTime : 0f;
        return flare + lookDownHoldDuration + Mathf.Max(0f, lookUpStartDelay);
    }

    void OnValidate()
    {
        zoneSpeedMultiplier = Mathf.Max(0f, zoneSpeedMultiplier);
        returnSpeedMultiplier = Mathf.Max(0f, returnSpeedMultiplier);
        speedBlendDuration = Mathf.Max(0.01f, speedBlendDuration);
        transitionDuration = Mathf.Max(0.05f, transitionDuration);
        flareDuration = Mathf.Max(0.01f, flareDuration);
        flareLeadTime = Mathf.Max(0f, flareLeadTime);
        filamentOpaqueValue = Mathf.Clamp01(filamentOpaqueValue);
        filamentHiddenValue = Mathf.Clamp01(filamentHiddenValue);
        filamentBlendDuration = Mathf.Max(0.01f, filamentBlendDuration);
        lookDownHoldDuration = Mathf.Max(0f, lookDownHoldDuration);
        lookUpStartDelay = Mathf.Max(0f, lookUpStartDelay);
        lookBackHoldDuration = Mathf.Max(0f, lookBackHoldDuration);
        lookBackOrbitCountdown = Mathf.Max(0f, lookBackOrbitCountdown);
        lookBackOrbitDuration = Mathf.Max(0.1f, lookBackOrbitDuration);
    }
}
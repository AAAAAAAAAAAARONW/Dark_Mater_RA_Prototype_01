using UnityEngine;

/// <summary>
/// Cleaned-up version of LayerDefinition.
/// Changes from original:
///   - Added isDefaultLayer flag (used by listeners to detect enter vs exit direction)
///   - Removed scene-ref fields (yellowFilamentRenderers — can't live in a ScriptableObject)
///   - Removed trigger detection fields (requireTag, usePollingFallback, pollingInterval,
///     autoAddKinematicRigidbody — these belong in LayerTriggerVolume, not the profile)
/// </summary>
[CreateAssetMenu(menuName = "Layer Profiles/Layer Definition Test")]
public class LayerDefinitionTest : ScriptableObject
{
    [Header("Identity")]
    public string layerId = "Macro";
    public UniverseLayer universeLayer = UniverseLayer.Macro;
    [Tooltip("Mark true on your single default/fallback layer (e.g. Macro). " +
             "Listeners use this to distinguish returning-to-baseline from entering a new zone. " +
             "Only one LayerDefinitionTest in the registry should have this set to true.")]
    public bool isDefaultLayer = false;

    [Header("Blend Timing")]
    public float transitionDuration = 3f;

    [Header("Speed")]
    public float enterSpeedMultiplier = 0.3f;
    public float exitSpeedMultiplier = 1f;
    public float speedBlendDuration = 3f;

    [Header("Flare Before Switch")]
    public bool useFlareBeforeSwitch = true;
    public Color flareColor = Color.white;
    [Range(0f, 1f)] public float flarePeakAlpha = 1f;
    public float flareDuration = 2f;
    public float flareLeadTime = 0.08f;

    [Header("Yellow Filament Transparency")]
    public bool driveYellowFilamentTransparency = true;
    // Note: renderer references live in FilamentLayerResponder (scene object),
    // not here — ScriptableObjects cannot hold scene object references.
    public string yellowFilamentShaderName = "Custom/YellowFilamentNebulaVolume";
    public float filamentOpaqueValue = 1f;
    public float filamentHiddenValue = 0f;
    public float filamentBlendDuration = 3f;

    [Header("Camera Look-Down Sequence")]
    public bool lockLookInputDuringLookDown = true;
    public float enterLookDownDuration = 5f;
    public float lookDownBlendDuration = 5f;
    public float lookDownHoldDuration = 3f;
    public float lookUpBlendDuration = 5f;
    public float lookUpStartDelay = 0.12f;

    [Header("Camera Look-Back Sequence (Quasar / entry layers)")]
    [Tooltip("Use this instead of look-down for layers where the camera should face backward first.")]
    public bool useLookBackSequence = false;
    [Tooltip("How long to hold the look-back camera before blending forward.")]
    public float lookBackHoldDuration = 4f;
    [Tooltip("How long the blend from look-back to forward camera takes.")]
    public float lookBackBlendDuration = 3f;

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

    // ── Helpers ──────────────────────────────────────────────────────────────

    public float GetEnterSequenceDuration()
    {
        float flare = useFlareBeforeSwitch ? flareLeadTime : 0f;
        return flare + enterLookDownDuration + lookDownHoldDuration +
               Mathf.Max(0f, lookUpStartDelay) + lookUpBlendDuration;
    }

    public float GetExitSequenceDuration()
    {
        float flare = useFlareBeforeSwitch ? flareLeadTime : 0f;
        return lookDownBlendDuration + flare + lookDownHoldDuration +
               Mathf.Max(0f, lookUpStartDelay) + lookUpBlendDuration;
    }

    void OnValidate()
    {
        enterSpeedMultiplier = Mathf.Max(0f, enterSpeedMultiplier);
        exitSpeedMultiplier = Mathf.Max(0f, exitSpeedMultiplier);
        speedBlendDuration = Mathf.Max(0.01f, speedBlendDuration);
        transitionDuration = Mathf.Max(0.05f, transitionDuration);
        flareDuration = Mathf.Max(0.01f, flareDuration);
        flareLeadTime = Mathf.Max(0f, flareLeadTime);
        filamentOpaqueValue = Mathf.Clamp01(filamentOpaqueValue);
        filamentHiddenValue = Mathf.Clamp01(filamentHiddenValue);
        filamentBlendDuration = Mathf.Max(0.01f, filamentBlendDuration);
        enterLookDownDuration = Mathf.Max(0.2f, enterLookDownDuration);
        lookDownBlendDuration = Mathf.Max(0.01f, lookDownBlendDuration);
        lookDownHoldDuration = Mathf.Max(0f, lookDownHoldDuration);
        lookUpBlendDuration = Mathf.Max(0.01f, lookUpBlendDuration);
        lookUpStartDelay = Mathf.Max(0f, lookUpStartDelay);
    }
}
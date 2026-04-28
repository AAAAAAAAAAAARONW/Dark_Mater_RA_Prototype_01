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

    [Header("LAF Spectrum HUD")]
    [Tooltip("How many buffer steps the spectrum scrolls per second (redshift rate). Higher = faster rightward drift.")]
    public float lafScrollSpeedStepsPerSecond = 8f;
    [Tooltip("Number of absorption dips overlaid on the spectrum. " +
             "Set to 0 for the clean quasar template (default).")]
    public int lafDipCount = 0;
    [Tooltip("Random seed for deterministic spectrum shape.")]
    public int lafRandomSeed = 42;
    [Tooltip("Minimum dip width as fraction of the buffer length.")]
    [Range(0.0001f, 0.02f)] public float lafDipWidthMin = 0.0004f;
    [Tooltip("Maximum dip width as fraction of the buffer length.")]
    [Range(0.0001f, 0.05f)] public float lafDipWidthMax = 0.004f;
    [Tooltip("Minimum absorption dip depth (0 = no dip, 1 = full absorption).")]
    [Range(0f, 1f)] public float lafDipDepthMin = 0.08f;
    [Tooltip("Maximum absorption dip depth.")]
    [Range(0f, 1f)] public float lafDipDepthMax = 0.92f;
    [Tooltip("Normalised position of the Ly-α emission peak (0=left/UV, 1=right/IR). " +
             "Also sets the boundary between Ly-α forest and the rest of the spectrum. Default 0.42.")]
    [Range(0f, 1f)] public float lafContinuumPeakPosition = 0.42f;
    [Tooltip("Sigma of the continuum rise (left side of peak) as fraction of buffer length. " +
             "Only used in procedural (no-CSV) mode.")]
    [Range(0.02f, 0.5f)] public float lafContinuumRiseSigma = 0.14f;
    [Tooltip("Sigma of the continuum fall (right side of peak) as fraction of buffer length). " +
             "Only used in procedural (no-CSV) mode.")]
    [Range(0.02f, 0.5f)] public float lafContinuumFallSigma = 0.32f;

    [Header("Journey")]
    [Tooltip("Journey phase to pin when entering this layer.")]
    public UniverseJourneyTracker.JourneyPhase phaseOnEnter = UniverseJourneyTracker.JourneyPhase.MilkyWay;
    [Tooltip("Only used when isDefaultLayer = true. Phase to set when returning to baseline.")]
    public UniverseJourneyTracker.JourneyPhase phaseOnExit = UniverseJourneyTracker.JourneyPhase.CosmicWeb1;

    [Header("Debug")]
    public bool debugLog = true;

    [Header("Nebula Shader")]
    [Tooltip("Enable layer-driven tweening for Custom/Nebula shader parameters.")]
    public bool driveNebulaShader = false;
    [Tooltip("Blend duration for Nebula shader params when this layer becomes active.")]
    public float nebulaBlendDuration = 3f;

    [Header("Nebula Colors")]
    public Color nebulaColorDark = new Color(0.08f, 0.02f, 0.18f, 1f);
    public Color nebulaColorMid = new Color(0.3f, 0.15f, 0.5f, 1f);
    public Color nebulaColorBright = new Color(0.6f, 0.4f, 0.9f, 1f);
    public Color nebulaColorStar = new Color(1f, 0.95f, 1f, 1f);

    [Header("Nebula Noise")]
    public float nebulaScale = 1.2f;
    public float nebulaOctaves = 4f;
    public float nebulaPersistence = 0.5f;
    public float nebulaDensity = 0.8f;
    public float nebulaSharpness = 1.5f;

    [Header("Nebula Stars")]
    public float nebulaStarScale = 80f;
    public float nebulaStarThreshold = 0.992f;
    public float nebulaStarBrightness = 1.2f;

    [Header("Nebula Animation")]
    [Tooltip("1 = enable animation, 0 = disable animation.")]
    public float nebulaAnimate = 1f;
    public float nebulaSpeed = 0.05f;

    [Header("Editor Preview")]
    [Tooltip("When enabled, changing this asset in Inspector previews Nebula params in Editor (not Play mode).")]
    public bool enableEditorNebulaPreview = false;

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

        nebulaBlendDuration = Mathf.Max(0.01f, nebulaBlendDuration);
        nebulaScale = Mathf.Clamp(nebulaScale, 0.5f, 4f);
        nebulaOctaves = Mathf.Clamp(nebulaOctaves, 1f, 6f);
        nebulaPersistence = Mathf.Clamp(nebulaPersistence, 0.2f, 0.9f);
        nebulaDensity = Mathf.Clamp(nebulaDensity, 0.2f, 2f);
        nebulaSharpness = Mathf.Clamp(nebulaSharpness, 0.5f, 4f);
        nebulaStarScale = Mathf.Clamp(nebulaStarScale, 20f, 200f);
        nebulaStarThreshold = Mathf.Clamp(nebulaStarThreshold, 0.95f, 0.999f);
        nebulaStarBrightness = Mathf.Clamp(nebulaStarBrightness, 0.5f, 3f);
        nebulaAnimate = nebulaAnimate >= 0.5f ? 1f : 0f;
        nebulaSpeed = Mathf.Clamp(nebulaSpeed, 0f, 0.5f);

        lafScrollSpeedStepsPerSecond = Mathf.Max(0f, lafScrollSpeedStepsPerSecond);
        lafDipCount = Mathf.Max(0, lafDipCount);
        lafDipWidthMin = Mathf.Max(0.0001f, lafDipWidthMin);
        lafDipWidthMax = Mathf.Max(lafDipWidthMin, lafDipWidthMax);
        lafDipDepthMin = Mathf.Clamp01(lafDipDepthMin);
        lafDipDepthMax = Mathf.Clamp(lafDipDepthMax, lafDipDepthMin, 1f);
        lafContinuumRiseSigma = Mathf.Max(0.02f, lafContinuumRiseSigma);
        lafContinuumFallSigma = Mathf.Max(0.02f, lafContinuumFallSigma);

#if UNITY_EDITOR
        LayerDefinitionTestEditorPreview.ApplyIfEnabled(this);
#endif
    }
}
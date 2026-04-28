using UnityEngine;

/// <summary>
/// Layer profile are used for defining layer behavior like parameters and transition sequence 
/// (i.e. Macro vs Micro) plus transition sequence tuning.
/// Listeners (scripts and functionalities) read desired profiles upon event broadcast to decide what to do.
/// </summary>
 
// Create instances (.asset) within Unity using this scriptable object to define new layer profiles
// Add them to the Layer Catalog asset, which maps layer id to layer definitions

// Now trigger boxes only have to tell layer manager to switch active id -> functionality read + react

// Below defines the tunable parameters when creating a new profile
// The values here are default values based on the trigger sequence test currently used in scene
// TODO configure what you want inside a layerdefinition, below is placeholder

[CreateAssetMenu(menuName = "Layer Profiles/Layer Definition")]
public class LayerDefinition : ScriptableObject
{
    [Header("Identity")]
    public string layerId = "Macro";
    public UniverseLayer universeLayer = UniverseLayer.Macro;
    
    // Below are used serialized fields from before. 

    [Header("Filter")]
    [SerializeField] bool requireTag = false;
    [SerializeField] string targetTag = "Player";
    [SerializeField] bool usePollingFallback = true;
    [SerializeField] float pollingInterval = 0.1f;
    [SerializeField] bool autoAddKinematicRigidbody = true;

    [Header("Blend Timing")]
    public float transitionDuration = 3f;

    [Header("Speed (Enter/Exit")]
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
    public bool autoFindYellowFilamentRenderers = true;
    public Renderer[] yellowFilamentRenderers;
    public string yellowFilamentShaderName = "Custom/YellowFilamentNebulaVolume";
    public float filamentOpaqueValue = 1f;
    public float filamentHiddenValue = 0f;
    public float filamentBlendDuration = 3f;
    
    [Header("Camera Look-Down Sequence Timing (matches MicroToMacroTriggerSequenceTest coroutine timing)")]
    public bool lockLookInputDuringLookDown = true;
    public float enterLookDownDuration = 5f;
    public float lookDownBlendDuration = 5f;
    public float lookDownHoldDuration = 3f;
    public float lookUpBlendDuration = 5f;
    public float lookUpStartDelay = 0.12f;

    [Header("HUD")]
    [Tooltip("Micro travel slider max light years when this layer is active. 0 = use default.")]
    public float microSliderMaxLightYearsWhenInside = 0f;

    [Header("Journey (pins/unpins via UniverseJourneyTracker)")]
    public UniverseJourneyTracker.JourneyPhase phaseOnEnter = UniverseJourneyTracker.JourneyPhase.MilkyWay;
    public UniverseJourneyTracker.JourneyPhase phaseOnExit = UniverseJourneyTracker.JourneyPhase.SolarSystem;

    [Header("LAF Spectrum HUD")]
    [Tooltip("How many buffer steps the spectrum scrolls per second (redshift rate). Higher = faster rightward drift.")]
    public float lafScrollSpeedStepsPerSecond = 8f;
    [Tooltip("Number of absorption dips in the pre-generated spectrum buffer.")]
    public int lafDipCount = 180;
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
    [Tooltip("Position of the continuum emission peak within the buffer (0 = UV/left end, 1 = IR/right end).")]
    [Range(0f, 1f)] public float lafContinuumPeakPosition = 0.32f;
    [Tooltip("Sigma of the continuum rise (left side of peak) as fraction of buffer length.")]
    [Range(0.02f, 0.5f)] public float lafContinuumRiseSigma = 0.14f;
    [Tooltip("Sigma of the continuum fall (right side of peak) as fraction of buffer length.")]
    [Range(0.02f, 0.5f)] public float lafContinuumFallSigma = 0.32f;

    [Header("Debug")]
    public bool debugLog = true;
    public bool debugVerbose = false;

    public float GetEnterSequenceDuration01()

    {
        float flare = useFlareBeforeSwitch ? flareLeadTime : 0f;
        return flare + enterLookDownDuration + lookDownHoldDuration + Mathf.Max(0f, lookUpStartDelay) + lookUpBlendDuration;
    }

    public float GetExitSequenceDuration01()
    {
        float flare = useFlareBeforeSwitch ? flareLeadTime : 0f;
        return lookDownBlendDuration + flare + lookDownHoldDuration + Mathf.Max(0f, lookUpStartDelay) + lookUpBlendDuration;
    }

    void OnValidate() // safety ranges to verify parameters are within valid range, TUNE THIS for your purposes
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

        lafScrollSpeedStepsPerSecond = Mathf.Max(0f, lafScrollSpeedStepsPerSecond);
        lafDipCount = Mathf.Max(0, lafDipCount);
        lafDipWidthMin = Mathf.Max(0.0001f, lafDipWidthMin);
        lafDipWidthMax = Mathf.Max(lafDipWidthMin, lafDipWidthMax);
        lafDipDepthMin = Mathf.Clamp01(lafDipDepthMin);
        lafDipDepthMax = Mathf.Clamp(lafDipDepthMax, lafDipDepthMin, 1f);
        lafContinuumRiseSigma = Mathf.Max(0.02f, lafContinuumRiseSigma);
        lafContinuumFallSigma = Mathf.Max(0.02f, lafContinuumFallSigma);
    }
}
using UnityEngine;

/// <summary>
/// One layer's behaviour parameters.
///
/// Replaces LayerDefinitionTest. The audit that preceded this rewrite found 26 of its
/// 54 fields had no live consumer, so this asset carries only what something actually
/// reads. What was dropped and why:
///
///   flare* / filament*            no component in the playtest scene consumes them
///   returnSpeedMultiplier         gates are one-way; the player never returns to a layer
///   phaseOnExit / isDefaultLayer   same reason — "exit" does not exist in the gate model
///   transitionDuration            replaced by TransitionTiming_NEW anchors
///   laf*                          belongs to the spectrum system, not the layer system
///   universeLayer / debugLog      zero readers
///   nebula* (16 fields)           moved to NebulaProfile_NEW
///
/// Anything still here is read by exactly one responder.
/// </summary>
[CreateAssetMenu(menuName = "Journey NEW/Layer Profile", fileName = "LP_New")]
public class LayerProfile_NEW : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Must match the LayerGate_NEW, the LayerCatalog_NEW entry and the WorldSwitcher_NEW group.")]
    public string layerId = "Macro";

    [Tooltip("True on the single layer the player starts in. It has no gate — the state " +
             "machine adopts it at startup. Exactly one profile in the catalog should set this.")]
    public bool isStartLayer = false;

    [Header("Speed")]
    [Tooltip("External speed multiplier applied while in this layer.")]
    public float zoneSpeedMultiplier = 1f;

    [Tooltip("Seconds to tween into zoneSpeedMultiplier.")]
    public float speedBlendDuration = 3f;

    [Header("Camera — cover sequence")]
    [Tooltip("Lock player look input for the whole transition.")]
    public bool lockLookInput = true;

    [Tooltip("Seconds to hold on the cover camera after the world has swapped.")]
    public float lookDownHoldDuration = 3f;

    [Tooltip("Extra pause after the hold, before blending back up.")]
    public float lookUpStartDelay = 0.12f;

    [Header("Camera — look-back sequence (start layer only)")]
    [Tooltip("Face backward first, then orbit to forward. Only meaningful on the start layer.")]
    public bool useLookBackSequence = false;

    [Tooltip("Seconds after the sequence begins before the orbit starts.")]
    public float lookBackOrbitCountdown = 5f;

    [Tooltip("Seconds the 180-degree orbit takes.")]
    public float lookBackOrbitDuration = 2.5f;

    [Header("Visuals")]
    [Tooltip("Nebula look for this layer. Leave empty to leave the sky untouched.")]
    public NebulaProfile_NEW nebula;

    [Tooltip("Lyman-alpha forest and continuum shape for this layer. " +
             "Leave empty to keep whatever the previous layer set.")]
    public SpectrumProfile_NEW spectrum;

    [Tooltip("Seconds for the render group crossfade. Ignored when WorldSwitcher_NEW " +
             "is set to instant swap, which is the shipping configuration.")]
    public float renderSwapDuration = 3f;

    [Header("Journey")]
    [Tooltip("Journey phase pinned when this layer is entered. Drives the HUD label only — " +
             "the distance readout is computed from player Z and is unaffected.")]
    public UniverseJourneyTracker.JourneyPhase phaseOnEnter = UniverseJourneyTracker.JourneyPhase.MilkyWay;

    [Header("Transition schedule")]
    [Tooltip("When each channel fires, relative to a camera anchor. Defaults put the render " +
             "swap, the nebula swap and the speed ramp all on CoverReached.")]
    public TransitionTiming_NEW timing = new TransitionTiming_NEW();

    void OnValidate()
    {
        zoneSpeedMultiplier = Mathf.Max(0f, zoneSpeedMultiplier);
        speedBlendDuration = Mathf.Max(0.01f, speedBlendDuration);
        lookDownHoldDuration = Mathf.Max(0f, lookDownHoldDuration);
        lookUpStartDelay = Mathf.Max(0f, lookUpStartDelay);
        lookBackOrbitCountdown = Mathf.Max(0f, lookBackOrbitCountdown);
        lookBackOrbitDuration = Mathf.Max(0.1f, lookBackOrbitDuration);
        renderSwapDuration = Mathf.Max(0.01f, renderSwapDuration);

        if (timing == null) timing = new TransitionTiming_NEW();
    }
}

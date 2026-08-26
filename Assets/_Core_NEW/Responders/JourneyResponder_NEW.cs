using UnityEngine;

/// <summary>
/// Pins the journey phase when a layer is entered.
///
/// Replaces JourneyLayerResponder, whose isDefaultLayer branch called UnpinPhase().
/// That branch was unreachable — only the start layer set the flag, and the start
/// layer has no gate — so in practice the responder had exactly one behaviour.
/// It is written as one behaviour here.
///
/// This one deliberately does NOT ride the CoverReached anchor. The phase only drives
/// the HUD's phase name; the distance readout is computed from raw player Z inside
/// UniverseJourneyTracker and is explicitly unaffected by pinning. Waiting for the
/// cover would leave the label showing the previous phase for several seconds after
/// the player has already crossed the gate.
///
/// UniverseJourneyTracker itself is untouched. Its scale model — designer light-years
/// divided by the artist's actual gate spacing, per phase — is the strongest piece of
/// the original codebase and there is nothing to gain by rewriting it.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("JOURNEY", "#4A94F2")]
public class JourneyResponder_NEW : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] LayerState_NEW state;
    [SerializeField] UniverseJourneyTracker tracker;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    void Awake()
    {
        if (state == null) state = FindObjectOfType<LayerState_NEW>();
        if (tracker == null) tracker = FindObjectOfType<UniverseJourneyTracker>();

        if (tracker == null)
            Debug.LogError("[JourneyResponder_NEW] No UniverseJourneyTracker found. " +
                           "The HUD phase label will not follow layer changes.", this);
    }

    void OnEnable()
    {
        if (state != null) state.OnLayerChanged += HandleLayerChanged;
    }

    void OnDisable()
    {
        if (state != null) state.OnLayerChanged -= HandleLayerChanged;
    }

    void HandleLayerChanged(LayerProfile_NEW previous, LayerProfile_NEW current)
    {
        if (current == null || tracker == null) return;

        tracker.SetPhase(current.phaseOnEnter);

        if (debugLog)
            Debug.Log($"[JourneyResponder_NEW] '{current.layerId}' pinned phase to " +
                      $"{current.phaseOnEnter}.", this);
    }
}

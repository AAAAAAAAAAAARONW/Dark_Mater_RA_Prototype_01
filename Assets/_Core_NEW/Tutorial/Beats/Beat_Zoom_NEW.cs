using UnityEngine;

/// <summary>
/// A4 — the zoom lesson. Gated on the player actually narrowing the view.
///
/// A hint line is not teaching. Every other control in this piece is taught the same
/// way — the beat does not advance until the player has done the thing — and zoom was
/// the one exception: A2 said LEFT STICK · LOOK CLOSER and then moved on whether anyone
/// had touched the stick or not. This closes that.
///
/// It sits at the end of Phase 0, after A3 and before B1, which is where the opening had
/// the room. By then the player has had twenty seconds of free looking with the right
/// stick, so the second stick lands as an addition rather than as competition.
///
/// A4 is not a storyboard frame. Phase 0 in the GDD is three frames titled Silence, with
/// nothing to press; this is a fourth with a gate on it. See TutorialZoom_NEW for why the
/// zoom exists at all, and the README for the departure.
///
/// The gate is a threshold on the zoom, not a dwell and not a round trip. GDD §6's note
/// on B1 — "not a dwell timer" — is about the same thing: a walk-up visitor who has just
/// worked out a control should be rewarded for using it, not asked to hold it steady.
/// </summary>
[HierarchyBadge_NEW("BEAT ZOOM", "#6FA8DC")]
public class Beat_Zoom_NEW : TutorialBeat_NEW
{
    [Header("Gate")]
    [Tooltip("How far in the player has to zoom, 0 to 1. Well short of the full range: " +
             "the lesson is that the stick does something, not that it can be pinned.")]
    [Range(0.1f, 1f)]
    [SerializeField] float requiredZoom = 0.55f;

    [Tooltip("Also require the view to come back out afterwards. Off by default — one " +
             "action, one gate. Turn it on if the round trip turns out to be the lesson.")]
    [SerializeField] bool requireReturn = false;

    [Tooltip("Zoom at or below this counts as back out again. Only used with requireReturn.")]
    [Range(0f, 0.9f)]
    [SerializeField] float returnedZoom = 0.15f;

    [Header("Wiring")]
    [Tooltip("Leave empty to find it in the scene.")]
    [SerializeField] TutorialZoom_NEW zoom;

    bool _reachedIn;
    bool _forced;

    protected override void OnBeatEnter()
    {
        _reachedIn = false;
        _forced = false;

        if (zoom == null) zoom = FindObjectOfType<TutorialZoom_NEW>();

        if (zoom == null)
            Debug.LogError("[Beat_Zoom_NEW] " + BeatId + " has no TutorialZoom_NEW. " +
                           "This beat can never be satisfied.", this);
    }

    protected override void OnBeatTick(float dt)
    {
        if (zoom == null) return;

        if (zoom.Amount >= requiredZoom) _reachedIn = true;
    }

    protected override bool GateSatisfied()
    {
        if (_forced) return true;
        if (zoom == null) return false;

        if (!_reachedIn) return false;
        if (!requireReturn) return true;

        return zoom.Amount <= returnedZoom;
    }

    protected override void OnForceSatisfy()
    {
        _forced = true;
    }

    public override string GateStatus()
    {
        if (zoom == null) return "misconfigured";

        if (!_reachedIn)
            return string.Format("zoom {0:F2} / need {1:F2}", zoom.Amount, requiredZoom);

        if (requireReturn && zoom.Amount > returnedZoom)
            return string.Format("zoom {0:F2} / back out below {1:F2}", zoom.Amount, returnedZoom);

        return "done";
    }
}

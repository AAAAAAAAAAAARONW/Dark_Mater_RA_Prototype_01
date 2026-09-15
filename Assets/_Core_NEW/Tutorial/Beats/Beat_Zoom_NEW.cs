using UnityEngine;

/// <summary>
/// A2 and A3 — the zoom lesson, in two halves. Gated on the player actually moving the
/// field of view.
///
/// A hint line is not teaching. Every other control in this piece is taught the same way
/// — the beat does not advance until the player has done the thing — and the zoom was
/// the one exception: a prompt went up and the storyboard moved on whether anyone had
/// touched the stick or not.
///
/// TWO BEATS RATHER THAN ONE, because zooming in and zooming back out are two different
/// discoveries. In is the reward: the quasar stops being a bright point and becomes a
/// disc with a jet coming off it. Out is the half people do not find on their own, and a
/// player left at 12 degrees of field for the rest of the piece is a player who will lose
/// B2's jet and get disoriented in B3's turn. So the lesson costs a push and a pull, and
/// Phase 0 hands over to B1 with the view back where it started.
///
/// These take the storyboard's A2 and A3, which the GDD has as silent cinematic frames.
/// A3's own content survives the change: the mote still arrives on this beat's enter, and
/// it now arrives while the player is widening the view — which is the moment something
/// at the edge of frame becomes visible at all.
///
/// See TutorialZoom_NEW for why the zoom exists, and the README for the departure from
/// GDD section 4.
///
/// The gate is a threshold, not a dwell and not a hold. GDD section 6's note on B1 — "not
/// a dwell timer" — is about the same thing: someone who has just worked out a control
/// should be rewarded for using it, not asked to hold it steady.
/// </summary>
[HierarchyBadge_NEW("BEAT ZOOM", "#6FA8DC")]
public class Beat_Zoom_NEW : TutorialBeat_NEW
{
    public enum Direction
    {
        /// <summary>Narrow the view. Satisfied at or above the threshold.</summary>
        In,

        /// <summary>Widen it again. Satisfied at or below the threshold.</summary>
        Out
    }

    [Header("Gate")]
    [Tooltip("Which way this beat asks the view to move. The hint line has to agree with " +
             "it — the builder writes both, so change them together.")]
    [SerializeField] Direction direction = Direction.In;

    [Tooltip("Where the gate sits. 0 is the base field of view, 1 is fully zoomed.\n\n" +
             "In is satisfied at or above it; Out at or below it.\n\n" +
             "Both are set short of the ends of the range: the lesson is that the stick " +
             "does something, not that it can be pinned.")]
    [Range(0f, 1f)]
    [SerializeField] float threshold = 0.55f;

    [Header("Wiring")]
    [Tooltip("Leave empty to find it in the scene.")]
    [SerializeField] TutorialZoom_NEW zoom;

    // The gate was already true when the beat opened. Happens when the beats are
    // reordered, when F2 skips the beat before, or when the attract loop restarts
    // mid-zoom. A beat that asks for a zoom and then costs none teaches nothing, so the
    // player has to be on the wrong side of the threshold before crossing it counts.
    bool _mustLeaveFirst;

    bool _forced;

    /// <summary>Which way this beat asks the view to move. Read by nothing yet; here so
    /// a wrong hint line can be caught without opening the Inspector.</summary>
    public Direction Way { get { return direction; } }

    /// <summary>
    /// Push the left stick up to zoom in, down to zoom out, by as much as the gate is
    /// still short by.
    ///
    /// THE KNOB COMES HOME AS THE VIEW ARRIVES, which is what makes this teach a rate
    /// rather than a position. The lesson is that the stick moves the view — "not that
    /// it can be pinned", as the threshold's own tooltip puts it — and a diagram that
    /// stayed hard over after the gate was met would be teaching a visitor to hold the
    /// view at its narrowest, which is exactly the state A3 then exists to get them out
    /// of. Reaching zero says stop as clearly as the rim says push.
    ///
    /// AND IT POINTS THE OTHER WAY WHILE THE VIEW HAS TO BE BACKED OFF FIRST. A beat
    /// that opens with its gate already true asks the player to leave the threshold
    /// before crossing it — see _mustLeaveFirst — and for those few seconds the honest
    /// instruction is the opposite of the beat's own name. A diagram still pointing at
    /// ZOOM IN while the beat is waiting for the player to zoom out would be the one
    /// thing worse than no diagram.
    ///
    /// The left stick because that is the only stick this component reads, and the sign
    /// from its invert flag rather than from a constant — see TutorialZoom_NEW.Inverted.
    /// </summary>
    public override TutorialStickGuide_NEW.Gesture StickGesture
    {
        get
        {
            bool up = direction == Direction.In;
            if (zoom != null && zoom.Inverted) up = !up;

            // Backing off first: the other way, and firmly — the ask is not "a little
            // less", it is "off the threshold".
            if (_mustLeaveFirst)
                return TutorialStickGuide_NEW.Gesture.Track(TutorialStickGuide_NEW.StickSide.Left,
                                                            up ? Vector2.down : Vector2.up, 1f);

            float amount = zoom != null ? zoom.Amount : 0f;

            // How much of the range is still between the view and the gate. Each side is
            // scaled by its own half of the range, so half a knob means half the work
            // left whichever way the beat is asking.
            float demand = direction == Direction.In
                ? (threshold - amount) / Mathf.Max(threshold, 0.01f)
                : (amount - threshold) / Mathf.Max(1f - threshold, 0.01f);

            return TutorialStickGuide_NEW.Gesture.Track(TutorialStickGuide_NEW.StickSide.Left,
                                                        up ? Vector2.up : Vector2.down,
                                                        Mathf.Clamp01(demand));
        }
    }

    protected override void OnBeatEnter()
    {
        _forced = false;

        if (zoom == null) zoom = FindObjectOfType<TutorialZoom_NEW>();

        if (zoom == null)
        {
            Debug.LogError("[Beat_Zoom_NEW] " + BeatId + " has no TutorialZoom_NEW. " +
                           "This beat can never be satisfied.", this);
            return;
        }

        _mustLeaveFirst = Past(zoom.Amount);
    }

    protected override void OnBeatTick(float dt)
    {
        if (zoom == null) return;

        if (_mustLeaveFirst && !Past(zoom.Amount)) _mustLeaveFirst = false;
    }

    protected override bool GateSatisfied()
    {
        if (_forced) return true;
        if (zoom == null) return false;
        if (_mustLeaveFirst) return false;

        return Past(zoom.Amount);
    }

    protected override void OnForceSatisfy()
    {
        _forced = true;
    }

    /// <summary>Is the zoom on the far side of this beat's threshold?</summary>
    bool Past(float amount)
    {
        return direction == Direction.In ? amount >= threshold : amount <= threshold;
    }

    public override string GateStatus()
    {
        if (zoom == null) return "misconfigured";

        string want = direction == Direction.In ? "in past " : "out below ";

        if (_mustLeaveFirst)
            return string.Format("zoom {0:F2} — already {1}{2:F2}, needs moving first",
                                 zoom.Amount, want, threshold);

        return string.Format("zoom {0:F2} / {1}{2:F2}", zoom.Amount, want, threshold);
    }
}

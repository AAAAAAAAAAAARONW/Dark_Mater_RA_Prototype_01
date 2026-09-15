using UnityEngine;

/// <summary>
/// B3 — turning around. The black hole silhouette and the full disc, seen for the
/// first time.
///
/// Two conditions, both required: the player has turned past a yaw threshold, and the
/// disc is actually in frame. The GDD gate reads "Player turns past 150° and the disc
/// is in frame", and the second half is not redundant. An angle alone can be satisfied
/// by a player staring at their feet while the stick drifts, and B3 is where spatial
/// orientation lands — the note in GDD §6 is "Protect it." A beat that can be passed
/// without ever seeing the disc has not protected anything.
///
/// Yaw is measured with shortest-angle deltas, so a full 360° spin reads as 0. That is
/// correct rather than pedantic: a player who has come all the way back round is facing
/// forward again and has not turned around. Because the disc must also be in frame,
/// the spin case cannot slip through on the angle alone either.
///
/// Which yaw the threshold is measured from is a real choice, so it is a field rather
/// than a constant. JetAxis means "turned away from the direction of travel", which is
/// what the storyboard describes. BeatStart means "turned 150° from wherever B2 left
/// you", which is what you want if B2's framing moves.
/// </summary>
[HierarchyBadge_NEW("BEAT TURN", "#4080C0")]
public class Beat_TurnAround_NEW : TutorialBeat_NEW
{
    public enum YawReference
    {
        /// <summary>Measured from the direction of travel. Matches the storyboard.</summary>
        JetAxis,

        /// <summary>Measured from wherever the previous beat left the view.</summary>
        BeatStart
    }

    [Header("Turn")]
    [Tooltip("Degrees of yaw away from the reference. GDD §6 says 150.")]
    [SerializeField] float minYawDegrees = 150f;

    [SerializeField] YawReference measureFrom = YawReference.JetAxis;

    [Header("Disc in frame")]
    [Tooltip("The disc, or the black hole at its centre. Required — see the summary.")]
    [SerializeField] Transform disc;

    [Tooltip("Half-angle that counts as 'in frame'. Wider than a reticle: this asks the " +
             "player to be looking at the disc, not to be aiming at it.")]
    [SerializeField] float discInFrameHalfAngle = 35f;

    [Header("Wiring")]
    [SerializeField] FirstPersonLookRig_NEW lookRig;

    bool _forced;

    /// <summary>
    /// Hold the look stick sideways, the short way round to the disc.
    ///
    /// A HOLD AND NOT A CIRCLE. The obvious picture for "turn around" is a knob going
    /// round the ring, and it is the wrong one: rolling the stick round its rim is a
    /// different gesture from pushing it to one side, and this beat is satisfied by the
    /// second. A visitor who copied the circle would spin the view and arrive back where
    /// they started, which — because yaw is measured with shortest-angle deltas — reads
    /// as not having turned at all. The gate would be right and the picture would have
    /// caused the failure.
    ///
    /// WHICH SIDE is read off the disc, so the diagram sends the player the short way
    /// round to the thing the gate also requires to be in frame. Beyond 180 degrees both
    /// answers are equally short and the sign flips about; it settles as soon as the
    /// player has committed either way, and by then they are past needing it.
    /// </summary>
    public override TutorialStickGuide_NEW.Gesture StickGesture
    {
        get
        {
            if (lookRig == null || disc == null) return base.StickGesture;

            Vector3 local = lookRig.transform.InverseTransformPoint(disc.position);

            // Behind and dead centre is the one bearing with no short way round. Right
            // is as good an answer as left, and a fixed answer beats a flickering one.
            float side = Mathf.Abs(local.x) > 0.0001f ? Mathf.Sign(local.x) : 1f;

            return TutorialStickGuide_NEW.Gesture.Hold(Beat_LookAt_NEW.LookSide(lookRig),
                                                       new Vector2(side, 0f));
        }
    }

    protected override void OnBeatEnter()
    {
        _forced = false;

        if (lookRig == null) lookRig = FindObjectOfType<FirstPersonLookRig_NEW>();

        if (lookRig == null)
        {
            Debug.LogError("[Beat_TurnAround_NEW] " + BeatId + " has no FirstPersonLookRig_NEW. " +
                           "This beat can never be satisfied.", this);
            return;
        }

        if (disc == null)
            Debug.LogError("[Beat_TurnAround_NEW] " + BeatId + " has no disc transform. " +
                           "This beat can never be satisfied.", this);

        if (measureFrom == YawReference.BeatStart) lookRig.MarkYaw();
    }

    protected override bool GateSatisfied()
    {
        if (_forced) return true;
        if (lookRig == null || disc == null) return false;

        return TurnedFar() && DiscInFrame();
    }

    protected override void OnForceSatisfy()
    {
        _forced = true;
    }

    bool TurnedFar()
    {
        return YawTurned() >= minYawDegrees;
    }

    bool DiscInFrame()
    {
        return lookRig.IsInReticle(disc, discInFrameHalfAngle);
    }

    float YawTurned()
    {
        if (measureFrom == YawReference.BeatStart) return lookRig.YawFromMark;
        return Mathf.Abs(lookRig.YawFromForward);
    }

    public override string GateStatus()
    {
        if (lookRig == null || disc == null) return "misconfigured";

        return string.Format("yaw {0:F0}/{1:F0}deg {2}   disc in frame {3}",
                             YawTurned(), minYawDegrees,
                             TurnedFar() ? "ok" : "--",
                             DiscInFrame() ? "ok" : "--");
    }
}

using UnityEngine;

/// <summary>
/// Gated on the player bringing something into the reticle.
///
/// B1 (look right, catch the mote) and B2 (look up, the jet channel and a second mote).
/// Phase 2's C4 is the same gate again — "the quasar brought into frame behind the
/// player" — and GDD §6 notes that C4 deliberately reuses the B1 lesson with no new
/// prompt, which only works if it is literally the same component.
///
/// The gate is "in the reticle", not "in the reticle for N seconds". GDD §6 spells that
/// out for B1: "Mote held inside the reticle. Not a dwell timer." The May 2026 playtest
/// failure was non-gamers not working out the camera at all, and a dwell timer punishes
/// exactly the player who has just worked it out and is moving the stick in small
/// nervous corrections. holdSeconds exists and defaults to zero; setting it above zero
/// is a decision to disagree with the GDD, so it says so in the tooltip.
///
/// The direction the player has to look is encoded by where the target sits in the
/// scene, not by a field here. That keeps "look right" a property of the world rather
/// than a second place to keep in sync with it.
/// </summary>
[HierarchyBadge_NEW("BEAT LOOK", "#40A0C0")]
public class Beat_LookAt_NEW : TutorialBeat_NEW
{
    [Header("Target")]
    [Tooltip("What the player has to bring into frame. Usually a GuideMote_NEW.")]
    [SerializeField] Transform target;

    [Tooltip("Half-angle from the view centre that counts as 'in the reticle'. " +
             "Generous on purpose: this teaches the stick, it does not test aim.")]
    [SerializeField] float reticleHalfAngle = 12f;

    [Tooltip("Seconds the target must stay inside the reticle. GDD §6 says this is not " +
             "a dwell timer, so leave it at 0. Above 0 disagrees with the design.")]
    [SerializeField] float holdSeconds = 0f;

    [Header("Wiring")]
    [Tooltip("Leave empty to find the rig in the scene on enter.")]
    [SerializeField] FirstPersonLookRig_NEW lookRig;

    [Tooltip("Optional. Blooms into a ripple the moment it is caught, which is the " +
             "feedback that teaches the stick. Survives into the next beat.")]
    [SerializeField] GuideMote_NEW mote;

    [Tooltip("Show the target only once this beat opens. Nothing is on screen before " +
             "B1, and a mote visible three frames early reads as a stray particle.")]
    [SerializeField] bool activateTargetOnEnter = true;

    [Header("Placement")]
    [Tooltip("Put the target at a bearing from WHERE THE PLAYER IS LOOKING when this beat " +
             "opens, instead of leaving it wherever it sits in the scene.\n\n" +
             "THIS IS WHAT MAKES 'LOOK RIGHT' TRUE. A mote parked at a fixed spot is a " +
             "fixed direction — the player transform never rotates, so a local offset is " +
             "a world one — and the prompt that goes with it is only correct for a player " +
             "who happens to be facing down the travel axis when the beat opens. A1 lets " +
             "them look anywhere they like for eight seconds first. Someone who finished " +
             "it looking 60 degrees right was then told LOOK RIGHT about a mote that was " +
             "now on their left, and the one instruction in the piece they had already " +
             "obeyed became the wrong one.\n\n" +
             "Placed from the view, the ask is always the movement the words describe. The " +
             "gate does not change: it is still the mote in the reticle.\n\n" +
             "Off for a target that IS a place — C4's quasar is a real object behind the " +
             "player and 'bring it into frame' means that object, not a bearing.")]
    [SerializeField] bool placeRelativeToView = false;

    [Tooltip("Where to put it, in degrees from the view: x turns right, y lifts up.\n\n" +
             "Far enough off centre that it is a real movement and not a nudge; inside a " +
             "quarter turn so it is findable without the player losing which way they were " +
             "facing.")]
    [SerializeField] Vector2 bearingFromView = new Vector2(38f, 4f);

    [Tooltip("Metres out along that bearing. The motes are parented to the light, so this " +
             "is a distance it keeps as the light travels.")]
    [SerializeField] float placeDistance = 45f;

    float _insideFor;
    bool _forced;

    /// <summary>
    /// Push the look stick towards the target, by as much as is still missing.
    ///
    /// COMPUTED, NOT AUTHORED, for exactly the reason the beat has no direction field:
    /// "the direction the player has to look is encoded by where the target sits in the
    /// scene, not by a field here". A stored direction would be a second copy of that,
    /// and the failure would be silent and specific — B1's mote dragged left in the
    /// Scene view, a diagram still leaning right, and a visitor doing what the picture
    /// says and never passing the gate.
    ///
    /// THE DEMAND IS THE GATE'S OWN ERROR, off the same angle IsInReticle is judged on
    /// and against the same reticleHalfAngle. So the knob is hard over while the mote is
    /// behind them, eases back as they turn towards it, and is home on the frame the
    /// gate opens — there is no second definition of "close enough" to fall out of step
    /// with the first.
    ///
    /// Direction is a bearing in degrees rather than a projection, so a target BEHIND
    /// the player still reads correctly: C4 reuses this beat for the quasar after C3 has
    /// reversed the course, and a projected direction would collapse to nearly nothing
    /// at the moment the answer matters most. Yaw of ±180 leans the knob hard sideways,
    /// which is the right instruction — turn first, then look up or down.
    /// </summary>
    protected override TutorialStickGuide_NEW.Gesture GateGesture
    {
        get
        {
            if (lookRig == null || target == null) return base.GateGesture;

            return TutorialStickGuide_NEW.Gesture.Track(LookSide(lookRig), BearingToTarget(),
                                                        Demand());
        }
    }

    /// <summary>
    /// How far off the reticle the target still is, as a fraction of a full push.
    ///
    /// Measured from the camera as it actually points, which is what IsInReticle does,
    /// so this reaches zero exactly when the gate opens.
    /// </summary>
    float Demand()
    {
        Vector3 local = lookRig.transform.InverseTransformPoint(target.position);
        if (local.sqrMagnitude < 0.0001f) return 0f;

        float offBy = Vector3.Angle(Vector3.forward, local) - reticleHalfAngle;

        return Mathf.Clamp01(offBy / TutorialStickGuide_NEW.FullDeflectionDegrees);
    }

    /// <summary>
    /// Yaw and pitch to the target, in degrees, as screen axes: x right, y up.
    ///
    /// Measured from the camera as it actually points — offset behind the light and
    /// tilted down by the follow view — because that is what IsInReticle measures from
    /// too. The picture and the gate then cannot disagree about which way is towards it.
    /// </summary>
    Vector2 BearingToTarget()
    {
        Vector3 local = lookRig.transform.InverseTransformPoint(target.position);

        float flat = new Vector2(local.x, local.z).magnitude;

        float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
        float pitch = Mathf.Atan2(local.y, flat) * Mathf.Rad2Deg;

        return new Vector2(yaw, pitch);
    }

    /// <summary>
    /// Which stick the diagram should draw, taken from the rig rather than assumed.
    ///
    /// The sticks have moved once already — see SeparateTheSticks — and a hard-coded
    /// R here would be the one part of the HUD that did not move with them.
    /// </summary>
    internal static TutorialStickGuide_NEW.StickSide LookSide(FirstPersonLookRig_NEW rig)
    {
        return rig != null && rig.LookStickSetting == TutorialInput_NEW.LookStick.Left
            ? TutorialStickGuide_NEW.StickSide.Left
            : TutorialStickGuide_NEW.StickSide.Right;
    }

    protected override void OnBeatEnter()
    {
        _insideFor = 0f;
        _forced = false;

        if (lookRig == null) lookRig = FindObjectOfType<FirstPersonLookRig_NEW>();

        if (lookRig == null)
            Debug.LogError("[Beat_LookAt_NEW] " + BeatId + " has no FirstPersonLookRig_NEW. " +
                           "This beat can never be satisfied.", this);

        if (target == null)
            Debug.LogError("[Beat_LookAt_NEW] " + BeatId + " has no target. " +
                           "This beat can never be satisfied.", this);

        // Before it is switched on, so it is never seen at the old position for a frame.
        PlaceTarget();

        if (activateTargetOnEnter && target != null && !target.gameObject.activeSelf)
            target.gameObject.SetActive(true);
    }

    /// <summary>
    /// Move the target onto its bearing from the current view.
    ///
    /// Built off the camera as it actually points — including the follow view's downward
    /// tilt — because that is the frame IsInReticle judges in and the frame
    /// BearingToTarget reports in. Place it in one frame and measure it in another and
    /// the mote lands a few degrees off the reticle it is supposed to arrive in.
    /// </summary>
    void PlaceTarget()
    {
        if (!placeRelativeToView || lookRig == null || target == null) return;

        // Negative X pitches up in Unity, so a positive y in the bearing lifts the mote.
        Vector3 local = Quaternion.Euler(-bearingFromView.y, bearingFromView.x, 0f) * Vector3.forward;

        Vector3 world = lookRig.transform.TransformDirection(local);

        target.position = lookRig.transform.position + world * Mathf.Max(1f, placeDistance);
    }

    protected override void OnBeatTick(float dt)
    {
        if (IsInside())
        {
            _insideFor += dt;

            // The bloom is the reward for catching it, so it fires on the first frame
            // inside regardless of holdSeconds.
            if (mote != null) mote.Bloom();
        }
        else
        {
            _insideFor = 0f;
        }
    }

    protected override bool GateSatisfied()
    {
        if (_forced) return true;

        return _insideFor >= holdSeconds && IsInside();
    }

    protected override void OnForceSatisfy()
    {
        _forced = true;
    }

    bool IsInside()
    {
        if (lookRig == null || target == null) return false;
        return lookRig.IsInReticle(target, reticleHalfAngle);
    }

    public override string GateStatus()
    {
        if (lookRig == null || target == null) return "misconfigured";

        Vector3 toTarget = target.position - lookRig.transform.position;
        float angle = Vector3.Angle(lookRig.transform.forward, toTarget);

        return string.Format("target {0:F0}deg / need <{1:F0}deg", angle, reticleHalfAngle);
    }
}

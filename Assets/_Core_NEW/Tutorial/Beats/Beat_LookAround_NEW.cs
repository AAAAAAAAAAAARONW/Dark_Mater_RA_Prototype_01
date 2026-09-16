using UnityEngine;

/// <summary>
/// A1 — the first thing the player ever does. Gated on having moved the view at all.
///
/// It was a cinematic run: eight seconds of RIGHT STICK · LOOK AROUND on screen, and
/// then the storyboard moved on whether anybody had touched the pad or not. That is the
/// same failure Beat_Zoom_NEW was written to fix one frame later — "a hint line is not
/// teaching" — and it was worse here, because this is the frame where a walk-up visitor
/// decides whether the thing in front of them responds to them at all. The May 2026
/// playtest's finding was non-gamers not working out the camera; a frame that asks them
/// to try and then proceeds identically if they do not is not an introduction to the
/// camera, it is a caption over an establishing shot.
///
/// It also could not carry a stick diagram. TutorialBeat_NEW refuses to draw one on a
/// beat that time advances, on the grounds that a knob asking for a movement that
/// changes nothing teaches exactly the wrong lesson — so the opening frame, the one that
/// most needs the picture, was the only gated-feeling frame without it.
///
/// NO DIRECTION, WHICH IS THE POINT. Every other gate in the piece has somewhere to
/// look; this one has nowhere. It is satisfied by TOTAL movement in any direction, so a
/// player who sweeps left, or up, or wanders, has done the thing. The diagram matches:
/// the knob rolls around the rim, which is the one prompt in the tutorial where
/// "anywhere you like" is the whole instruction.
///
/// The threshold is deliberately low. This is not an exercise — it is the handshake, and
/// the reward for it is the piece continuing.
/// </summary>
[HierarchyBadge_NEW("BEAT LOOK AROUND", "#40A0C0")]
public class Beat_LookAround_NEW : TutorialBeat_NEW
{
    [Header("Gate")]
    [Tooltip("Degrees of view movement, added up in any direction, that satisfy this beat.\n\n" +
             "Roughly a third of a turn. Low on purpose: enough that it cannot be passed " +
             "by stick drift or by the recentre settling, and low enough that the first " +
             "thing a nervous visitor does with the stick is already most of it.")]
    [SerializeField] float degreesToSweep = 120f;

    [Header("Wiring")]
    [Tooltip("Leave empty to find the rig in the scene on enter.")]
    [SerializeField] FirstPersonLookRig_NEW lookRig;

    float _swept;
    float _lastYaw;
    float _lastPitch;
    bool _forced;

    /// <summary>How much of the sweep is done, 0 to 1. The debug overlay reads it.</summary>
    public float Progress
    {
        get { return degreesToSweep > 0.0001f ? Mathf.Clamp01(_swept / degreesToSweep) : 1f; }
    }

    /// <summary>
    /// Roll the look stick round the ring, closing in as they do it.
    ///
    /// Sweep rather than a push, because there is no direction to push in. The demand
    /// falls as the view moves, so the orbit tightens towards the centre and stops when
    /// the beat is met — the same "you are there" the other beats say by coming home.
    /// </summary>
    protected override TutorialStickGuide_NEW.Gesture GateGesture
    {
        get
        {
            return TutorialStickGuide_NEW.Gesture.Sweep(Beat_LookAt_NEW.LookSide(lookRig), 1f,
                                                        1f - Progress);
        }
    }

    protected override void OnBeatEnter()
    {
        _swept = 0f;
        _forced = false;

        if (lookRig == null) lookRig = FindObjectOfType<FirstPersonLookRig_NEW>();

        if (lookRig == null)
        {
            Debug.LogError("[Beat_LookAround_NEW] " + BeatId + " has no FirstPersonLookRig_NEW. " +
                           "This beat can never be satisfied.", this);
            return;
        }

        _lastYaw = lookRig.Yaw;
        _lastPitch = lookRig.Pitch;
    }

    protected override void OnBeatTick(float dt)
    {
        if (lookRig == null) return;

        float yaw = lookRig.Yaw;
        float pitch = lookRig.Pitch;

        // Shortest-angle on yaw, which wraps; pitch is clamped and does not. Added as
        // absolute amounts rather than as a distance from the start, so a player who
        // looks right and comes back has still looked around — the gate is that they
        // moved the view, not that they ended up somewhere.
        _swept += Mathf.Abs(Mathf.DeltaAngle(_lastYaw, yaw)) + Mathf.Abs(pitch - _lastPitch);

        _lastYaw = yaw;
        _lastPitch = pitch;
    }

    protected override bool GateSatisfied()
    {
        if (_forced) return true;

        return _swept >= degreesToSweep;
    }

    protected override void OnForceSatisfy()
    {
        _forced = true;
    }

    public override string GateStatus()
    {
        if (lookRig == null) return "misconfigured";

        return string.Format("looked {0:F0}/{1:F0}deg", _swept, degreesToSweep);
    }
}

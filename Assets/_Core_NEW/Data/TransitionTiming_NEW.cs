using System;
using UnityEngine;

/// <summary>
/// Anchors fired by CameraDirector_NEW during a layer transition.
///
///   SequenceStart ──[camera blend, closed-loop]──▶ CoverReached ──[hold]──▶ LookUpStart
///
/// Channels attach to an anchor plus an offset instead of an absolute delay,
/// so retiming the Cinemachine blend table never invalidates the profile data.
/// </summary>
public enum TransitionAnchor_NEW
{
    /// <summary>The frame the gate fired. Blend to the cover camera has just started.</summary>
    SequenceStart = 0,

    /// <summary>The cover camera has reached full blend weight — the player cannot see the world.</summary>
    CoverReached = 1,

    /// <summary>The hold is over and the camera is about to blend back into the zone camera.</summary>
    LookUpStart = 2,
}

/// <summary>
/// One scheduled channel: "fire relative to this anchor, offset by this many seconds".
///
/// Negative offsets are only meaningful against <see cref="TransitionAnchor_NEW.LookUpStart"/>,
/// because that anchor's arrival time is computable the moment CoverReached fires
/// (hold + lookUpStartDelay). SequenceStart and CoverReached are reached by closed-loop
/// waiting, so nothing can be scheduled before them.
/// </summary>
[Serializable]
public class TimedChannel_NEW
{
    public TransitionAnchor_NEW anchor = TransitionAnchor_NEW.CoverReached;

    [Tooltip("Seconds relative to the anchor. Negative only works on LookUpStart.")]
    public float offset = 0f;

    public bool MatchesDirectly(TransitionAnchor_NEW fired) => anchor == fired && offset >= 0f;

    /// <summary>
    /// True when this channel wants LookUpStart with a negative offset, which must be
    /// pre-scheduled at CoverReached because it lands before LookUpStart is reached.
    /// </summary>
    public bool NeedsPreSchedule(TransitionAnchor_NEW fired)
        => fired == TransitionAnchor_NEW.CoverReached
        && anchor == TransitionAnchor_NEW.LookUpStart
        && offset < 0f;
}

/// <summary>
/// Per-layer transition schedule. Lives on LayerProfile_NEW.
///
/// Default is every visual + speed channel on CoverReached with zero offset:
/// the world, the sky and the speed ramp all change on the single frame the
/// top-down camera finishes covering the view.
/// </summary>
[Serializable]
public class TransitionTiming_NEW
{
    [Tooltip("Render layer group swap.")]
    public TimedChannel_NEW renderSwap = new TimedChannel_NEW();

    [Tooltip("Nebula shader parameters.")]
    public TimedChannel_NEW nebula = new TimedChannel_NEW();

    [Tooltip("Player speed multiplier tween.")]
    public TimedChannel_NEW speed = new TimedChannel_NEW();

    [Tooltip("Lyman-alpha forest parameters and continuum shape.")]
    public TimedChannel_NEW spectrum = new TimedChannel_NEW();

    [Tooltip("Reserved for a screen flare. No consumer is wired yet.")]
    public TimedChannel_NEW flare = new TimedChannel_NEW { anchor = TransitionAnchor_NEW.SequenceStart };
}

/// <summary>
/// Payload broadcast by CameraDirector_NEW at each anchor.
/// </summary>
public struct AnchorEvent_NEW
{
    public TransitionAnchor_NEW anchor;
    public LayerProfile_NEW profile;

    /// <summary>
    /// Seconds from now until LookUpStart. Only valid when <see cref="anchor"/> is
    /// CoverReached; negative elsewhere. Lets channels with a negative LookUpStart
    /// offset schedule themselves ahead of time.
    /// </summary>
    public float secondsUntilLookUp;
}

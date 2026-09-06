using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A beat that asks the player for nothing and runs for its storyboard duration.
///
/// Covers all of Phase 0 — A1 (0:00–0:08), A2 (0:08–0:15), A3 (0:15–0:20) — and later
/// C1, C3 and D1–D4. These are the only beats where time advances anything, and the
/// reason is in TutorialBeat_NEW's summary: the "nothing advances on a timer" rule is
/// about never letting a timer stand in for a player action, and these frames have no
/// player action to stand in for.
///
/// Look input is live through every one of them. Phase 0 has no interface and nothing
/// to press, but the player can already move the view, which is what makes A3's mote
/// leaving frame at the right edge legible as an invitation rather than an accident.
///
/// The three optional hooks are the ones Phase 0 actually needs:
///
///   * onRumble    A3 asks for one short controller rumble. This project is on the
///                 legacy Input Manager with no Input System package, so Unity 2019.4
///                 exposes no rumble API at all. The event is here so the cue is wired
///                 and visible in the Inspector the day a haptics path exists, rather
///                 than being a line of the GDD that quietly went missing. See the
///                 tutorial README.
///   * voiceOver   One line per beat maximum (GDD §8), placeholder until the writer is
///                 in place. Assigning nothing is the normal case.
///   * onHalfway   Fires once at the midpoint. A2's "the flow brightens enough to read
///                 as orbiting something" is a change inside a frame, not a new frame.
/// </summary>
[HierarchyBadge_NEW("BEAT CINE", "#C08040")]
public class Beat_Cinematic_NEW : TutorialBeat_NEW
{
    [Header("Voice over")]
    [Tooltip("One line maximum, and most beats get none. Placeholder until the writer " +
             "is in place; the frame timing does not depend on it.")]
    [SerializeField] AudioSource voiceOverSource;

    [SerializeField] AudioClip voiceOverClip;

    [Header("Extra cues")]
    [Tooltip("Fires once at the midpoint of the beat, for a change inside a frame.")]
    [SerializeField] UnityEvent onHalfway = new UnityEvent();

    [Tooltip("A3 asks for one short controller rumble. Unity 2019.4 with the legacy " +
             "Input Manager has no rumble API, so this is the seam to hook one to.")]
    [SerializeField] UnityEvent onRumble = new UnityEvent();

    [Tooltip("Fire onRumble on enter. A3 only.")]
    [SerializeField] bool rumbleOnEnter = false;

    bool _halfwayFired;

    protected override void OnBeatEnter()
    {
        _halfwayFired = false;

        if (voiceOverSource != null && voiceOverClip != null)
            voiceOverSource.PlayOneShot(voiceOverClip);

        if (rumbleOnEnter) onRumble.Invoke();
    }

    protected override void OnBeatTick(float dt)
    {
        if (_halfwayFired) return;
        if (NormalisedTime < 0.5f) return;

        _halfwayFired = true;
        onHalfway.Invoke();
    }
}

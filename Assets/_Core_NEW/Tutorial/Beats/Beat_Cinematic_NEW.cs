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
/// One optional hook, and it is something only an untimed frame wants:
///
///   * onHalfway   Fires once at the midpoint, for a change inside a frame rather than
///                 a new frame. Meaningless on a gated beat, which has no midpoint.
///
/// The voice-over slot used to live here and now lives on TutorialBeat_NEW, played by
/// TutorialVoiceOver_NEW: the gated frames have lines too, and a beat playing its own
/// clip had no way to know the previous frame was still talking. The rumble seam moved
/// to the base for the same kind of reason — A3 became a gated beat and took the GDD's
/// one rumble cue with it.
/// </summary>
[HierarchyBadge_NEW("BEAT CINE", "#C08040")]
public class Beat_Cinematic_NEW : TutorialBeat_NEW
{
    [Header("Extra cues")]
    [Tooltip("Fires once at the midpoint of the beat, for a change inside a frame.")]
    [SerializeField] UnityEvent onHalfway = new UnityEvent();

    bool _halfwayFired;

    protected override void OnBeatEnter()
    {
        _halfwayFired = false;
    }

    protected override void OnBeatTick(float dt)
    {
        if (_halfwayFired) return;
        if (NormalisedTime < 0.5f) return;

        _halfwayFired = true;
        onHalfway.Invoke();
    }
}

using UnityEngine;

/// <summary>
/// D5 — the frame that teaches pause. It says B TO PAUSE, and is satisfied once the
/// player has paused and resumed.
///
/// The class keeps its first name, Inspect, because the scene already refers to it by
/// that script; renaming the file under a running project is how a beat loses its
/// references.
///
/// PAUSE ITSELF IS NOT HERE. It lives in TutorialPause_NEW and works everywhere once the
/// tutorial is running, because GDD §4's rule is one button, one meaning — a pause that
/// only worked in D5 would fail the first time someone pressed it anywhere else. D5 is
/// only where it is introduced: it puts the words on screen and waits for the player to
/// use them.
///
/// GDD §6 gives the gate as "opened and closed once", and both halves are load-bearing.
/// Pausing proves the player found the control. RESUMING proves they know how to get
/// back out, which is the same lesson B4 teaches about A: in the May 2026 playtest one
/// participant only started experimenting freely once he had found reset.
///
/// WHY IT COUNTS RESUMES RATHER THAN WATCHING. Beats do not tick while the piece is
/// paused — that is what keeps A from firing a gate behind the pause — so this beat
/// cannot see the pause happen. It notes TutorialPause_NEW.ResumeCount when it opens and
/// is satisfied once that count has moved. That also means a pause taken just before D5
/// opened cannot satisfy it early.
///
/// While paused, the hint line reads B TO RESUME; TutorialHUD_NEW does that for every
/// beat, not just this one.
/// </summary>
[HierarchyBadge_NEW("BEAT PAUSE", "#33B3B3")]
public class Beat_Inspect_NEW : TutorialBeat_NEW
{
    // NO A-PROMPT FIELD HERE, deliberately. §5 requires the continue affordance to be
    // the same shape in the same position every time, and that shape has an A in it.
    // D5 is a different button, so it asks through the hint line instead: B TO PAUSE.

    [Header("Wiring")]
    [Tooltip("Leave empty to find it in the scene.")]
    [SerializeField] TutorialPause_NEW pause;

    int _resumesAtEnter;
    bool _forced;

    protected override void OnBeatEnter()
    {
        _forced = false;

        if (pause == null) pause = FindObjectOfType<TutorialPause_NEW>();

        if (pause == null)
        {
            Debug.LogError("[Beat_Inspect_NEW] " + BeatId + " has no TutorialPause_NEW in the " +
                           "scene. B will do nothing and this beat can never be satisfied.", this);
            return;
        }

        _resumesAtEnter = pause.ResumeCount;
    }

    protected override bool GateSatisfied()
    {
        if (_forced) return true;
        if (pause == null) return false;

        return pause.ResumeCount > _resumesAtEnter;
    }

    protected override void OnForceSatisfy()
    {
        _forced = true;
    }

    public override string GateStatus()
    {
        if (pause == null) return "misconfigured — no pause";

        return pause.ResumeCount > _resumesAtEnter ? "paused and resumed" : "waiting for pause";
    }
}

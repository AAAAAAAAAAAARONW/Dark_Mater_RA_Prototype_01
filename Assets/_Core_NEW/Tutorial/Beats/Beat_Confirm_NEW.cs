using UnityEngine;

/// <summary>
/// B4 — the A prompt appears, the player presses A, the view lerps back to the jet axis.
///
/// This is the beat that introduces the one button the whole piece runs on. GDD §4:
/// A is confirm, recentre and emit, one button with one meaning in the tutorial and the
/// journey alike. Phase 2's C2 (emit) and Phase 4's E3 (hand off) are this same
/// component with recentre switched off and a different prompt.
///
/// Recentre is given away this early on purpose. The GDD's note on B4 is that in
/// playtest one participant only started experimenting freely once he had found reset —
/// a player who knows they can always get back is a player who will look around.
///
/// The beat is satisfied on the press, not on the lerp arriving. The recentre keeps
/// running into the next beat, because waiting for it would mean a beat that ignores
/// input for the best part of a second, and a camera that ignores input is the thing
/// playtesters read as a bug. waitForRecentre exists for the case where a later beat
/// genuinely must not open until the view has settled; it is off by default.
///
/// The prompt text lives here and the prompt's shape and position live in
/// TutorialHUD_NEW, because GDD §5 requires the continue affordance to be the same
/// shape in the same position every time. Position is a property of the HUD; only the
/// words change per beat.
/// </summary>
[HierarchyBadge_NEW("BEAT A", "#E0A030")]
public class Beat_Confirm_NEW : TutorialBeat_NEW
{
    [Header("Prompt")]
    [Tooltip("Words only. The HUD owns the shape and the position so every continue " +
             "affordance in the piece is identical.")]
    [SerializeField] string promptText = "A";

    [Tooltip("Seconds after the beat opens before the prompt appears. The press is " +
             "accepted from the start regardless, so this delays the ask, not the gate.")]
    [SerializeField] float promptDelaySeconds = 0f;

    [Header("Recentre")]
    [Tooltip("Whether A still means recentre while this beat is open. True for B4 — and " +
             "the rig does the recentring, this only says not to suppress it. False for " +
             "the beats where A means emit or hand off instead.")]
    [SerializeField] bool recentreOnPress = true;

    [Tooltip("Hold this beat open until the recentre lerp has arrived. Off by default; " +
             "see the class summary before switching it on.")]
    [SerializeField] bool waitForRecentre = false;

    [Header("Wiring")]
    [SerializeField] FirstPersonLookRig_NEW lookRig;

    bool _pressed;
    bool _forced;

    /// <summary>Read by TutorialHUD_NEW to decide what the prompt says.</summary>
    public string PromptText { get { return promptText; } }

    /// <summary>True while the prompt should be on screen.</summary>
    public bool PromptVisible
    {
        get { return IsActive && !_pressed && Elapsed >= promptDelaySeconds; }
    }

    protected override void OnBeatEnter()
    {
        _pressed = false;
        _forced = false;

        if (lookRig == null) lookRig = FindObjectOfType<FirstPersonLookRig_NEW>();

        if (lookRig == null)
        {
            Debug.LogError("[Beat_Confirm_NEW] " + BeatId + " has no FirstPersonLookRig_NEW.", this);
            return;
        }

        // The rig owns the A-recentres binding, so this beat only has to say whether A
        // still means recentre while it is open. C2 (emit) and E3 (hand over) set this
        // false; B4 leaves it alone.
        if (!recentreOnPress) lookRig.SetConfirmRecentres(false);
    }

    protected override void OnBeatExit()
    {
        // Hand the binding back, whatever this beat did with it.
        if (lookRig != null) lookRig.SetConfirmRecentres(true);
    }

    protected override void OnBeatTick(float dt)
    {
        if (_pressed) return;
        if (!TutorialInput_NEW.ConfirmDown()) return;

        _pressed = true;
    }

    protected override bool GateSatisfied()
    {
        if (_forced) return true;
        if (!_pressed) return false;

        if (waitForRecentre && lookRig != null && lookRig.IsRecentring) return false;

        return true;
    }

    protected override void OnForceSatisfy()
    {
        _forced = true;
    }

    public override string GateStatus()
    {
        if (!_pressed) return "waiting for A";
        if (waitForRecentre && lookRig != null && lookRig.IsRecentring) return "recentring";

        return "pressed";
    }
}

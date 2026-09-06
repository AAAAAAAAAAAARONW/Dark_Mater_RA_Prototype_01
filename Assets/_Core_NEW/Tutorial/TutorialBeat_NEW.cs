using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// One frame of the storyboard. The base class every beat derives from.
///
/// GDD §5 states the rule this class exists to enforce: one action, one gate, and
/// nothing advances on a timer. That rule has an exact scope, and getting it wrong in
/// either direction breaks something:
///
///   * Beats that ask the player for an action (B1–B4) must never advance without it.
///     A timer here is the failure the rule names. AdvanceMode.PlayerAction has no
///     timeout field at all, so one cannot be added by accident from the Inspector.
///
///   * Beats that ask for nothing (A1–A3, later C1 and D1–D4) have durations written
///     into the storyboard — A1 is 0:00–0:08. Those are cinematic runs, not gates.
///     AdvanceMode.Duration covers them and is the only mode where time advances
///     anything.
///
/// So the invariant is narrower than "no timers": no timer ever stands in for a player
/// action. The director enforces it by refusing to compile a PlayerAction beat's
/// duration into an advance, and the two modes are separate enum values so the intent
/// is visible in the Inspector rather than inferred from a zero.
///
/// Every beat carries UnityEvents for enter and satisfy. VO, VFX, audio cues and the
/// A3 rumble hook are wired there rather than in code, because the storyboard is going
/// to move and the person moving it is not going to be in Visual Studio.
/// </summary>
[DisallowMultipleComponent]
public abstract class TutorialBeat_NEW : MonoBehaviour
{
    public enum AdvanceMode
    {
        /// <summary>No player action. Runs for its storyboard duration, then hands on.</summary>
        Duration,

        /// <summary>Gated on the player. Time never advances this beat.</summary>
        PlayerAction
    }

    [Header("Identity")]
    [Tooltip("Storyboard frame id, e.g. A1 or B3. Used by the HUD and the debug overlay.")]
    [SerializeField] string beatId = "A1";

    [Tooltip("What this frame is, in the storyboard's words. Editor readability only.")]
    [TextArea(1, 3)]
    [SerializeField] string description = "";

    [Header("Prompt")]
    [Tooltip("Control hint shown while this beat runs, e.g. LEFT STICK · LOOK.\n\n" +
             "Empty means 'no change' rather than 'no hint', which is what the storyboard " +
             "asks for: B1 introduces LEFT STICK · LOOK and B2 and B3 both say 'No new " +
             "prompt', so the same line stays up across all three. A beat that needs the " +
             "hint gone sets it to a single space.")]
    [SerializeField] string hintText = "";

    [Header("Advance")]
    [SerializeField] AdvanceMode advanceMode = AdvanceMode.Duration;

    [Tooltip("Duration mode only: seconds this frame runs. Ignored by PlayerAction beats.")]
    [SerializeField] float duration = 8f;

    [Tooltip("PlayerAction mode only: seconds the gate is ignored after the beat opens. " +
             "Stops a press meant for the previous beat carrying through. Not a timeout.")]
    [SerializeField] float inputGraceSeconds = 0.15f;

    [Header("Events")]
    [Tooltip("Fires the frame this beat opens. VO, VFX and audio cues go here.")]
    [SerializeField] UnityEvent onEnter = new UnityEvent();

    [Tooltip("Fires the frame the gate is met, before the next beat opens.")]
    [SerializeField] UnityEvent onSatisfied = new UnityEvent();

    float _elapsed;
    bool _active;

    // ── Public API, read by TutorialDirector_NEW ─────────────────────────────

    public string BeatId { get { return beatId; } }
    public string Description { get { return description; } }

    /// <summary>Control hint for this beat. Empty means keep whatever is already up.</summary>
    public string HintText { get { return hintText; } }
    public AdvanceMode Mode { get { return advanceMode; } }
    public float Duration { get { return duration; } }
    public float Elapsed { get { return _elapsed; } }
    public bool IsActive { get { return _active; } }

    /// <summary>Progress 0–1 for Duration beats; always 0 for PlayerAction beats.</summary>
    public float NormalisedTime
    {
        get
        {
            if (advanceMode != AdvanceMode.Duration || duration <= 0f) return 0f;
            return Mathf.Clamp01(_elapsed / duration);
        }
    }

    /// <summary>
    /// One line for the debug overlay: what this beat is waiting for right now.
    /// Overridden by gated beats so whitebox testing does not need a breakpoint.
    /// </summary>
    public virtual string GateStatus()
    {
        if (advanceMode == AdvanceMode.Duration)
            return string.Format("runs {0:F1}/{1:F1}s", _elapsed, duration);

        return "waiting on player";
    }

    // ── Director-facing lifecycle ────────────────────────────────────────────

    public void Enter()
    {
        _active = true;
        _elapsed = 0f;

        OnBeatEnter();
        onEnter.Invoke();
    }

    public void Tick(float dt)
    {
        if (!_active) return;

        _elapsed += dt;
        OnBeatTick(dt);
    }

    public void Exit()
    {
        if (!_active) return;

        onSatisfied.Invoke();
        OnBeatExit();

        _active = false;
    }

    /// <summary>
    /// True when this beat is finished. Duration beats answer with the clock;
    /// PlayerAction beats answer with GateSatisfied and never with the clock.
    /// </summary>
    public bool IsSatisfied()
    {
        if (!_active) return false;

        if (advanceMode == AdvanceMode.Duration)
            return _elapsed >= duration;

        if (_elapsed < inputGraceSeconds) return false;

        return GateSatisfied();
    }

    /// <summary>
    /// Skip this beat. Debug only — the director calls it from its skip key, which is
    /// itself behind DebugView_NEW.Overlay.
    /// </summary>
    public virtual void ForceSatisfy()
    {
        _elapsed = Mathf.Max(_elapsed, duration);
        OnForceSatisfy();
    }

    // ── Subclass hooks ───────────────────────────────────────────────────────

    /// <summary>The gate. Only consulted for PlayerAction beats.</summary>
    protected virtual bool GateSatisfied()
    {
        return true;
    }

    protected virtual void OnBeatEnter() { }
    protected virtual void OnBeatTick(float dt) { }
    protected virtual void OnBeatExit() { }

    /// <summary>Let a gated subclass set whatever flag its gate reads.</summary>
    protected virtual void OnForceSatisfy() { }
}

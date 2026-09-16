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

    /// <summary>What a beat does to the control hint line when it opens.</summary>
    public enum HintMode
    {
        /// <summary>Leave whatever is up. The storyboard's "No new prompt" on B2 and B3.</summary>
        Keep,

        /// <summary>Replace it with this beat's hintText.</summary>
        Show,

        /// <summary>Take it away, leaving no hint line at all.</summary>
        Clear
    }

    [Header("Prompt")]
    [Tooltip("What this beat does to the control hint line when it opens.\n\n" +
             "Keep is the common case and what the storyboard's 'No new prompt' means.\n\n" +
             "Show replaces the line, which is what B4 does: the player is being asked " +
             "to try the recentre, so LEFT STICK · LOOK is swapped for A TO RECENTRE " +
             "rather than the two stacking up on screen together.\n\n" +
             "Clear removes the line entirely. Nothing needs it yet; it exists so a beat " +
             "that wants a bare screen does not have to fake it with a blank string.")]
    [SerializeField] HintMode hintMode = HintMode.Keep;

    [Tooltip("The line itself, e.g. RIGHT STICK · LOOK. Only used when hintMode is Show — " +
             "the field is hidden otherwise, because editing a string nothing reads is a " +
             "quiet way to lose ten minutes.")]
    [SerializeField] string hintText = "";

    [Tooltip("Let the builder keep this beat's wording in step with the storyboard.\n\n" +
             "Prompt copy is content the builder authored, not a value somebody tuned, so " +
             "Build or Update rewrites it — which is how a prompt still reading LEFT STICK " +
             "after look moved to the right stick gets corrected in a scene that already " +
             "exists.\n\n" +
             "Untick it to write your own wording and have the builder leave it alone.")]
    [SerializeField] bool builderOwnsCopy = true;

    [Tooltip("The stick diagram shown beside the hint line while this beat is open.\n\n" +
             "MOST BEATS LEAVE THIS AT NONE AND STILL GET A DIAGRAM. Beat_LookAt_NEW, " +
             "Beat_TurnAround_NEW and Beat_Zoom_NEW each work their own out from what " +
             "they are gated on — where the target actually sits, which way the view has " +
             "to turn, which way the field of view has to move — so the picture cannot " +
             "disagree with the gate. This field is for the cinematic frames, which have " +
             "no gate to read it off.\n\n" +
             "None also means none: a frame that asks for nothing shows nothing, which " +
             "is what the Phase 3 frames want.")]
    [SerializeField] TutorialStickGuide_NEW.GuideKind stickGuide =
        TutorialStickGuide_NEW.GuideKind.None;

    [Header("Advance")]
    [SerializeField] AdvanceMode advanceMode = AdvanceMode.Duration;

    [Tooltip("Duration mode only: seconds this frame runs. Ignored by PlayerAction beats.")]
    [SerializeField] float duration = 8f;

    [Tooltip("PlayerAction mode only: seconds the gate is ignored after the beat opens. " +
             "Stops a press meant for the previous beat carrying through. Not a timeout.")]
    [SerializeField] float inputGraceSeconds = 0.15f;

    [Tooltip("Run this beat's clock in scaled time instead of real time.\n\n" +
             "OFF is correct for every beat built so far, and is the answer for D2: the " +
             "storyboard's durations are seconds the PLAYER EXPERIENCES, so D2's twelve " +
             "seconds are twelve seconds of wall clock and are not stretched to sixty by " +
             "its 0.2x time scale.\n\n" +
             "This exists as an escape hatch for a beat that genuinely wants to be " +
             "stretched by slow motion, and it governs THIS BEAT'S CLOCK ONLY. The " +
             "camera rig and the zoom stay on real time in every case — they read " +
             "Time.unscaledDeltaTime directly and never see this flag. That is " +
             "deliberate: a beat that slowed the camera down with the world would be " +
             "locking it, which GDD section 5 forbids, and D2 explicitly needs the " +
             "player free to look between the impact and the bar.\n\n" +
             "Turning it on also scales inputGraceSeconds, since the grace is measured " +
             "on the same clock.")]
    [SerializeField] bool useScaledTime = false;

    [Header("Events")]
    [Tooltip("Fires the frame this beat opens. VO, VFX and audio cues go here.")]
    [SerializeField] UnityEvent onEnter = new UnityEvent();

    [Tooltip("Fires the frame the gate is met, before the next beat opens.")]
    [SerializeField] UnityEvent onSatisfied = new UnityEvent();

    [Tooltip("A3 asks for one short controller rumble. Unity 2019.4 on the legacy Input " +
             "Manager has no rumble API at all, so this is the seam to hook one to the " +
             "day a haptics path exists — rather than a line of the GDD quietly going " +
             "missing. See the tutorial README.")]
    [SerializeField] UnityEvent onRumble = new UnityEvent();

    [Tooltip("Fire onRumble when this beat opens.")]
    [SerializeField] bool rumbleOnEnter = false;

    float _elapsed;
    bool _active;

    // ── Public API, read by TutorialDirector_NEW ─────────────────────────────

    public string BeatId { get { return beatId; } }
    public string Description { get { return description; } }

    /// <summary>What this beat does to the control hint line.</summary>
    public HintMode Hint { get { return hintMode; } }

    /// <summary>The hint line this beat shows. Only meaningful when Hint is Show.</summary>
    public string HintText { get { return hintText; } }

    /// <summary>
    /// The hint line this beat wants on screen RIGHT NOW. Defaults to HintText.
    ///
    /// For a beat whose instruction changes while it is open — D5 says B TO PAUSE, and
    /// once paused, B TO RESUME. TutorialHUD_NEW polls this for the beat it is showing,
    /// the same way it already polls a confirm beat's PromptVisible, so a beat can change
    /// its own words without ever holding a reference to the HUD.
    /// </summary>
    public virtual string LiveHintText { get { return hintText; } }

    /// <summary>
    /// The stick diagram this beat wants beside its hint line, RIGHT NOW.
    ///
    /// Polled every frame by TutorialHUD_NEW, on the same reasoning as LiveHintText: a
    /// beat says what it wants and never holds a reference to the HUD. Polling rather
    /// than an event is what lets the answer be computed instead of stored — Beat_LookAt
    /// _NEW returns the live bearing to its target, so the knob leans wherever the mote
    /// has actually got to, including while the player is turning towards it.
    ///
    /// The default is the authored field, which is None for everything that has not
    /// deliberately set one. A beat that asks the player for nothing shows nothing.
    /// </summary>
    public TutorialStickGuide_NEW.Gesture StickGesture
    {
        get
        {
            // A BEAT THAT TIME ADVANCES CANNOT ASK FOR ANYTHING, and this is the rule
            // rather than a check. The diagram's whole claim is "this is what gets you to
            // the next frame"; on a Duration beat the answer is "wait", and a knob
            // leaning somewhere is then telling the player their thumb is what moves the
            // piece on when it demonstrably is not. They push, nothing they do changes
            // anything, and the beat ends on its own a few seconds later — which teaches
            // precisely the thing the May 2026 playtest recorded as the failure, that the
            // controls are not connected to the world.
            //
            // Enforced here and not left to each beat, because the field is on this class
            // and the next person to fill it in from the Inspector will not have read
            // this. A1 is the one that had it and is the reason it is written down: it
            // swept the ring for eight seconds while advancing on a clock.
            if (advanceMode != AdvanceMode.PlayerAction) return TutorialStickGuide_NEW.Gesture.None;

            return GateGesture;
        }
    }

    /// <summary>
    /// What THIS beat's gate is waiting for, as a stick movement. Override it in a beat
    /// that has a gate it can measure; the default is the authored field.
    ///
    /// Only reached on a PlayerAction beat — StickGesture is the public door and it
    /// closes on everything else — so an override can assume there is a gate to read and
    /// does not have to check the mode itself.
    /// </summary>
    protected virtual TutorialStickGuide_NEW.Gesture GateGesture
    {
        get { return TutorialStickGuide_NEW.Gesture.From(stickGuide); }
    }

    /// <summary>
    /// Whether Build or Update may rewrite this beat's wording. The builder reads it
    /// through SerializedObject; this is here so the intent is visible from code too.
    /// </summary>
    public bool BuilderOwnsCopy { get { return builderOwnsCopy; } }
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

        if (rumbleOnEnter) onRumble.Invoke();
    }

    /// <summary>
    /// Advance this beat by one frame.
    ///
    /// The director hands in real seconds, because everything in the tutorial runs on
    /// an unscaled clock. useScaledTime converts here rather than at the director, so
    /// one beat can be stretched by slow motion without the rest of the piece — and,
    /// more importantly, without the camera, which never sees this conversion.
    /// </summary>
    public void Tick(float unscaledDt)
    {
        if (!_active) return;

        float dt = useScaledTime ? unscaledDt * Time.timeScale : unscaledDt;

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

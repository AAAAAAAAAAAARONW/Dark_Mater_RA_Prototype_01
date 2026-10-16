using System;
using UnityEngine;

/// <summary>
/// The dark matter moment, in the first cosmic web: the voice explains dark matter and the
/// visitor is taught to show and hide it with Y — while the light keeps flying.
///
/// WHY IT DOES NOT STOP THE LIGHT. A stop was tried first. The trail is drawn from the light's
/// motion, and a resting light changes it; and JourneySequence_NEW gives the deeper reason,
/// that the journey is a piece on a wall which never waits for anyone. So the press is a
/// moment in the flight, not a pause in it, and the first cosmic web is long enough for the
/// lines to fit (Tools > Journey NEW > Layout > Lengthen First Cosmic Web; the Flight Path
/// view shows where each line falls).
///
/// THE RHYTHM (each number is a field below):
///
///   Begin    J3 has finished (the Macro voice-over row's onFinished calls Begin).
///   before   The line before the press. The hint, Y · SHOW / HIDE DARK MATTER, comes up with
///            it (or after it, hintWithBeforeLine off). Y works from now: a visitor who
///            presses during the line is not asked again (the tutorial's latch).
///   nudge    nudgeAfterLine after the before line, if nothing yet: the spoken instruction.
///   auto     autoAfterLine after the before line, if still nothing: the toggle happens for
///            them, so a walk-up visitor still sees it and hears why.
///   after    The scaffold eases in (or out); the line after the press is said.
///
/// Y keeps working for the rest of the first cosmic web, silently. It plays once a run.
///
/// TWO VERSIONS, ONE SWITCH. Dark matter shown when the visitor arrives and Y hides it
/// (ShownByDefault, the script's version A), or hidden and Y shows it (HiddenByDefault,
/// version B). Each has its own lines and hint; nothing else differs.
///
/// LINES NOT YET RECORDED are timed by their estimate and said silently, with the words on
/// the debug overlay (F1), so the moment can be laid out before the recordings exist. They are
/// marked for the flight recorder like recorded ones.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("DM MOMENT", "#5C8CFF")]
public class DarkMatterMoment_NEW : MonoBehaviour
{
    public enum Version
    {
        [Tooltip("Version A: dark matter is on screen when the visitor arrives; Y hides it.")]
        ShownByDefault,

        [Tooltip("Version B: only the gas is on screen; Y shows the dark matter under it.")]
        HiddenByDefault
    }

    [Serializable]
    public class Lines
    {
        [Tooltip("Said when J3 has finished, before the press.")]
        public AudioClip before;
        [TextArea(2, 4)] public string beforeText = "";
        [Min(0f)] public float beforeEstimate = 10f;

        [Tooltip("Said after the press (or the automatic toggle), while the scaffold moves.")]
        public AudioClip after;
        [TextArea(2, 4)] public string afterText = "";
        [Min(0f)] public float afterEstimate = 13f;

        [Tooltip("The spoken instruction, said once if nothing has been pressed. Not said " +
                 "silently when missing: a nudge nobody hears is nothing.")]
        public AudioClip nudge;
        [TextArea(1, 2)] public string nudgeText = "";

        [Tooltip("What the hint says after the letter, e.g. SHOW DARK MATTER. The letter is " +
                 "the one printed on the pad that is plugged in.")]
        public string hint = "";
    }

    [Header("Version")]
    [Tooltip("ShownByDefault = script version A (Y hides dark matter); " +
             "HiddenByDefault = version B (Y shows it).")]
    [SerializeField] Version version = Version.HiddenByDefault;

    [SerializeField] Lines shownByDefault = new Lines
    {
        beforeText = "The soft glow around these threads is dark matter, drawn so you can see it. " +
                     "It gives off no light, yet it outweighs ordinary matter five to one.",
        beforeEstimate = 12f,
        afterText = "Without it on screen, only the gas remains. Dark matter's gravity pulled the gas " +
                    "into this structure, so the gas is a beacon, showing us where the dark matter lies.",
        afterEstimate = 13f,
        nudgeText = "Press Y to hide the dark matter.",
        hint = "HIDE DARK MATTER"
    };

    [SerializeField] Lines hiddenByDefault = new Lines
    {
        beforeText = "There is far too little gas here to pull itself into threads like these. " +
                     "Something you cannot see is holding it in place.",
        beforeEstimate = 10f,
        afterText = "This is dark matter, drawn so you can see it. It gives off no light, yet it outweighs " +
                    "ordinary matter five to one, and its gravity pulls the gas into this structure.",
        afterEstimate = 14f,
        nudgeText = "Press Y to see what holds it together.",
        hint = "SHOW DARK MATTER"
    };

    [Header("Where")]
    [Tooltip("The layer the moment belongs to: the first cosmic web.")]
    [SerializeField] string layerId = "Macro";

    [Header("Wiring (found if empty)")]
    [SerializeField] LayerState_NEW layerState;
    [SerializeField] JourneyVoiceOver_NEW voiceOver;
    [SerializeField] DarkMatterScaffold_NEW scaffold;
    [SerializeField] JourneyHint_NEW hint;

    [Header("Timing")]
    [Tooltip("On: the hint comes up as the before line starts, so a visitor can press while " +
             "listening. Off: after the before line, as in the tutorial.")]
    [SerializeField] bool hintWithBeforeLine = true;

    [Tooltip("Seconds after the before line ends before the spoken nudge, if Y has not been pressed.")]
    [Min(0f)]
    [SerializeField] float nudgeAfterLine = 1.5f;

    [Tooltip("Seconds after the before line ends before the toggle happens on its own. The light " +
             "is flying the whole time, so this is how long the press is offered.")]
    [Min(0f)]
    [SerializeField] float autoAfterLine = 6f;

    [Header("Behaviour")]
    [Tooltip("Y keeps showing and hiding dark matter, silently, for the rest of the first " +
             "cosmic web after the moment.")]
    [SerializeField] bool toggleAfterMoment = true;

    [SerializeField] bool playOnce = true;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    [Tooltip("Start the moment NOW, wherever the light is. Editor and development builds only.")]
    [SerializeField] KeyCode debugBeginKey = KeyCode.F8;

    const string Lane = "Moment";

    enum Phase { Idle, Before, Waiting, After, Done }

    Phase _phase = Phase.Idle;
    bool _played;
    bool _pressed;
    bool _beforeDone;
    bool _nudged;
    float _sinceLine = -1f;     // seconds since the before line ended, -1 = not yet

    // A line said without a recording: time left, its words, what to do after. It waits its
    // turn like a recorded one (_silentQueued) and only starts counting once the voice is free.
    float _silentLeft = -1f;
    bool _silentQueued;
    string _silentText;
    string _silentLabel;
    Action _silentDone;

    Lines Current { get { return version == Version.ShownByDefault ? shownByDefault : hiddenByDefault; } }

    bool DefaultShown { get { return version == Version.ShownByDefault; } }

    /// <summary>True from Begin until the after line has been said.</summary>
    public bool IsRunning { get { return _phase != Phase.Idle && _phase != Phase.Done; } }

    // -- Public API -----------------------------------------------------------

    /// <summary>
    /// Start the moment. Hung on the Macro voice-over row's onFinished, so it starts when J3
    /// has been said. Ignored outside the first cosmic web, while running, and on a second
    /// call when playOnce is set.
    /// </summary>
    public void Begin()
    {
        if (IsRunning) return;
        if (playOnce && _played)
        {
            if (debugLog) Debug.Log("[DarkMatterMoment_NEW] Already played; ignoring Begin.", this);
            return;
        }
        if (!InLayer())
        {
            if (debugLog) Debug.Log("[DarkMatterMoment_NEW] Begin outside '" + layerId + "'; ignoring.", this);
            return;
        }

        _played = true;
        _pressed = _beforeDone = _nudged = false;
        _sinceLine = -1f;

        Enter(Phase.Before);
    }

    /// <summary>Let it play again. For the debug key and a restart.</summary>
    public void ResetPlayed() { _played = false; }

    // -- Lifecycle ------------------------------------------------------------

    void Awake()
    {
        if (layerState == null) layerState = FindObjectOfType<LayerState_NEW>();
        if (voiceOver == null) voiceOver = FindObjectOfType<JourneyVoiceOver_NEW>();
        if (scaffold == null) scaffold = FindObjectOfType<DarkMatterScaffold_NEW>();
        if (hint == null) hint = FindObjectOfType<JourneyHint_NEW>();

        if (scaffold == null)
            Debug.LogWarning("[DarkMatterMoment_NEW] No DarkMatterScaffold_NEW in the scene: Y has nothing to " +
                             "show. Run Tools > Journey NEW > Dark Matter > Build Dark Matter Moment.", this);
    }

    void OnEnable()
    {
        if (layerState != null) layerState.OnLayerChanged += HandleLayerChanged;
    }

    void OnDisable()
    {
        if (layerState != null) layerState.OnLayerChanged -= HandleLayerChanged;
        if (IsRunning) Abort("disabled");
    }

    void Start()
    {
        // The web can be on screen before its layer begins (the quasar's point stays in
        // it), so the default is set from the first frame, not on entering the layer.
        if (scaffold != null) scaffold.SetImmediate(DefaultShown);
    }

    void HandleLayerChanged(LayerProfile_NEW previous, LayerProfile_NEW current)
    {
        bool entering = current != null && string.Equals(current.layerId, layerId, StringComparison.OrdinalIgnoreCase);

        if (entering)
        {
            // Back to the version's default on every arrival, so a debug jump back into the
            // layer shows what a visitor arriving would.
            if (!IsRunning && scaffold != null) scaffold.SetImmediate(DefaultShown);
            return;
        }

        if (IsRunning) Abort("left " + layerId + " before the moment finished — lengthen the layer");
        if (hint != null && hint.IsShown) hint.Hide();
    }

    void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugBeginKey != KeyCode.None && Input.GetKeyDown(debugBeginKey))
        {
            if (IsRunning) Abort("debug restart");
            ResetPlayed();
            if (InLayer()) Begin();
            else Debug.Log("[DarkMatterMoment_NEW] " + debugBeginKey + ": the light is not in '" + layerId +
                           "'. Jump there first (2).", this);
        }
#endif

        float dt = Time.deltaTime;
        TickSilent(dt);

        if (InputScheme_NEW.ToggleDown()) HandleToggle();

        switch (_phase)
        {
            case Phase.Before:
                if (!_beforeDone) break;
                if (_pressed) Enter(Phase.After);
                else Enter(Phase.Waiting);
                break;

            case Phase.Waiting:
                TickWaiting(dt);
                break;
        }
    }

    // -- The moment -----------------------------------------------------------

    void Enter(Phase phase)
    {
        _phase = phase;

        if (debugLog) Debug.Log("[DarkMatterMoment_NEW] " + phase + " (" + version + ").", this);

        switch (phase)
        {
            case Phase.Before:
                if (hintWithBeforeLine) ShowHint();
                Say(Current.before, Current.beforeEstimate, Current.beforeText, "before", () => _beforeDone = true);
                break;

            case Phase.Waiting:
                _sinceLine = 0f;
                if (!hintWithBeforeLine) ShowHint();
                break;

            case Phase.After:
                if (hint != null) hint.Hide();
                CancelNudge();
                Say(Current.after, Current.afterEstimate, Current.afterText, "after", () => Enter(Phase.Done));
                break;

            case Phase.Done:
                FlightRecorder_NEW.Point(Lane, "done");
                break;
        }
    }

    void TickWaiting(float dt)
    {
        _sinceLine += dt;

        if (!_nudged && _sinceLine >= nudgeAfterLine && !VoiceBusy())
        {
            _nudged = true;
            if (Current.nudge != null && voiceOver != null)
            {
                voiceOver.Say(Current.nudge, null);
                FlightRecorder_NEW.Point(Lane, "nudge");
                if (debugLog) Debug.Log("[DarkMatterMoment_NEW] Nudge: " + Current.nudgeText, this);
            }
        }

        if (_sinceLine >= autoAfterLine)
        {
            if (debugLog) Debug.Log("[DarkMatterMoment_NEW] No Y: toggling for the visitor.", this);
            FlightRecorder_NEW.Point(Lane, "auto toggle");
            Toggle();
            Enter(Phase.After);
        }
    }

    void HandleToggle()
    {
        switch (_phase)
        {
            case Phase.Before:
                // Early: latched. The scaffold moves now; the after line follows the before line.
                if (_pressed) return;
                FlightRecorder_NEW.Point(Lane, "Y (early)");
                Toggle();
                return;

            case Phase.Waiting:
                FlightRecorder_NEW.Point(Lane, "Y");
                Toggle();
                Enter(Phase.After);
                return;

            case Phase.After:
                // The after line is about what is on screen: hold the picture until it is said.
                return;

            default:
                if (toggleAfterMoment && _played && InLayer() && scaffold != null) scaffold.Toggle();
                return;
        }
    }

    void Toggle()
    {
        _pressed = true;
        if (scaffold != null) scaffold.Toggle();
    }

    void ShowHint()
    {
        FlightRecorder_NEW.Point(Lane, "hint");
        if (hint == null) return;

        hint.Show(InputScheme_NEW.ToggleGlyph() + "  ·  " + Current.hint);
    }

    void CancelNudge()
    {
        if (voiceOver != null && Current.nudge != null) voiceOver.Cancel(Current.nudge);
    }

    bool VoiceBusy() { return (voiceOver != null && voiceOver.IsSpeaking) || _silentLeft >= 0f; }


    /// <summary>End at once: hint and lines gone. Said in the Console, because a moment cut off by
    /// the next gate means the layer is too short for it.</summary>
    void Abort(string why)
    {
        Debug.LogWarning("[DarkMatterMoment_NEW] Ended early: " + why + ".", this);

        CancelNudge();
        if (voiceOver != null)
        {
            if (Current.before != null) voiceOver.Cancel(Current.before);
            if (Current.after != null) voiceOver.Cancel(Current.after);
        }
        if (_silentLeft >= 0f && !_silentQueued) FlightRecorder_NEW.SpanEnd("VO", _silentLabel);
        _silentLeft = -1f;
        _silentQueued = false;
        _silentDone = null;

        if (hint != null) hint.Hide();

        _phase = Phase.Done;
    }

    // -- Lines ----------------------------------------------------------------

    void Say(AudioClip clip, float estimate, string text, string which, Action done)
    {
        if (clip != null && voiceOver != null)
        {
            // The voice-over marks a recorded line for the flight recorder itself.
            voiceOver.Say(clip, said => { if (IsRunning) done(); });
            return;
        }

        _silentLeft = estimate;
        _silentText = text;
        _silentLabel = "(silent) DM " + which;
        _silentDone = done;
        _silentQueued = true;

        if (debugLog) Debug.Log("[DarkMatterMoment_NEW] No recording; " + estimate + " s silent: " + text, this);
    }

    void TickSilent(float dt)
    {
        if (_silentLeft < 0f) return;

        if (_silentQueued)
        {
            // Behind whatever is being said — J3, or J3b if its filament came first.
            if (voiceOver != null && voiceOver.IsSpeaking) return;
            _silentQueued = false;
            FlightRecorder_NEW.SpanStart("VO", _silentLabel);
        }

        _silentLeft -= dt;
        if (_silentLeft > 0f) return;

        _silentLeft = -1f;
        FlightRecorder_NEW.SpanEnd("VO", _silentLabel);
        Action done = _silentDone;
        _silentDone = null;
        if (done != null) done();
    }

    bool InLayer()
    {
        if (layerState == null) return true;
        return string.Equals(layerState.CurrentLayerId, layerId, StringComparison.OrdinalIgnoreCase);
    }

    // -- Debug overlay --------------------------------------------------------

    void OnGUI()
    {
        if (!DebugView_NEW.Overlay) return;
        if (_phase == Phase.Idle && !InLayer()) return;

        string line = "DM MOMENT  " + version + "  " + _phase;
        if (_phase == Phase.Waiting)
            line += "  " + _sinceLine.ToString("F1") + "s since line  nudge@" + nudgeAfterLine + "  auto@" + autoAfterLine;
        if (scaffold != null) line += "  reveal " + scaffold.Reveal.ToString("F2");
        if (_silentLeft >= 0f)
            line += "\n  (silent" + (_silentQueued ? ", waiting for the voice" : " " + _silentLeft.ToString("F1") + "s") + ") " + _silentText;

        GUI.Label(new Rect(10f, Screen.height - 74f, 1400f, 44f), line);
    }
}

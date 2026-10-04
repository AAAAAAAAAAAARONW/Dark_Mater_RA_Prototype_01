using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A short authored sequence that plays inside the journey, triggered by entering a
/// layer. Steps have a duration and nothing else.
///
/// WHY IT IS NOT THE TUTORIAL'S BEAT SYSTEM. TutorialBeat_NEW has two advance modes, and
/// the interesting one waits for the player: look here, press this, turn around. That is
/// right for a tutorial and wrong for the journey, which is a continuous piece on a wall
/// at Carnegie Observatories. The journey never locks the camera, never asks for a
/// button, and must never stop and wait — a visitor who walks up mid-piece and does not
/// know they are being waited for is looking at a frozen exhibit.
///
/// So this has ONE advance mode. There is no gate type to add later and no field to set
/// to "wait": a sequence that cannot wait cannot strand anybody. The tutorial's rule was
/// "never use a timer in place of a player action"; here there are no player actions in
/// the first place, so a timer is not standing in for anything.
///
/// WHAT A STEP IS FOR. One statement, one duration, one UnityEvent. The voice-over is
/// laid against these durations, so each explanation's timing is one number on one step,
/// visible in the Inspector, editable without touching a coroutine. Nothing here is a
/// coroutine on purpose: the debug layer jump, a restart, and disabling the object all
/// have to leave this in a known state, and a half-finished coroutine does not.
///
/// STEPS DO NOT KNOW WHAT THEY DRIVE. A step fires a UnityEvent; RedshiftMarks_NEW and
/// friends know nothing about steps. So the whole sequence is legible in the Inspector,
/// and reordering it is a drag in a list rather than an edit to this file.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("SEQUENCE", "#B36AE2")]
public class JourneySequence_NEW : MonoBehaviour
{
    [Serializable]
    public class Step
    {
        [Tooltip("Short id, for the debug readout and for talking about it in a meeting. " +
                 "F1, F2, ... matching the storyboard.")]
        public string id = "F1";

        [Tooltip("What happens on screen. Not shown to anybody — it is here so the next " +
                 "person can read the sequence without playing it.")]
        [TextArea(2, 5)]
        public string description = "";

        [Tooltip("Seconds. A starting value for the voice-over to replace.")]
        [Min(0f)]
        public float duration = 5f;

        [Tooltip("Fired once, on the frame this step starts.")]
        public UnityEvent onEnter = new UnityEvent();
    }

    [Header("Trigger")]
    [Tooltip("The LayerState this sequence watches. Found in the scene if left empty.")]
    [SerializeField] LayerState_NEW layerState;

    [Tooltip("Entering this layer starts the sequence.\n\n" +
             "For the redshift and Lyman-alpha forest demonstration this is 'CosmicWeb' " +
             "— the SECOND cosmic web, the one entered after the intermediate galaxy. " +
             "The first one is layerId 'Macro', which is confusing enough to be worth " +
             "saying out loud: the two cosmic webs are two different layers with two " +
             "different names, and this must not be pointed at Macro.")]
    [SerializeField] string startOnLayerId = "CosmicWeb";

    [Tooltip("Seconds to wait after entering the layer before the first step. The layer " +
             "transition has its own camera move; starting on top of it puts two " +
             "authored things on screen at once and neither reads.")]
    [Min(0f)]
    [SerializeField] float startDelay = 2f;

    [Tooltip("Play at most once per run. Off lets a debug jump re-enter the layer and " +
             "watch the sequence again.")]
    [SerializeField] bool playOnce = true;

    [Header("Steps")]
    [SerializeField] Step[] steps = new Step[0];

    [Header("Time")]
    [Tooltip("Use unscaled time, so a pause that sets the time scale to zero does not " +
             "silently keep the sequence running behind the pause card.\n\n" +
             "OFF is correct here: the pause SHOULD hold the sequence. This is exposed " +
             "because the tutorial needed the opposite answer for one beat and somebody " +
             "will want to know which way this one points.")]
    [SerializeField] bool useUnscaledTime = false;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    [Tooltip("Advance to the next step. Editor and development builds only.")]
    [SerializeField] KeyCode skipStepKey = KeyCode.F6;

    [Tooltip("Start the sequence NOW, wherever the player is, ignoring the layer and " +
             "ignoring playOnce.\n\n" +
             "Because the honest trigger is entering the second cosmic web, which is " +
             "minutes into a run. Waiting that long to find out whether a step's duration " +
             "is right is how a sequence ends up never being tuned. Editor and development " +
             "builds only.")]
    [SerializeField] KeyCode debugPlayKey = KeyCode.F7;

    int _index = -1;
    float _elapsed;
    bool _running;
    bool _played;
    float _delayRemaining;

    // -- Public API -----------------------------------------------------------

    /// <summary>The step playing now, or null.</summary>
    public Step CurrentStep
    {
        get { return _running && _index >= 0 && _index < steps.Length ? steps[_index] : null; }
    }

    /// <summary>Id of the step playing now, or an empty string. For a debug readout.</summary>
    public string CurrentStepId
    {
        get { Step s = CurrentStep; return s != null ? s.id : ""; }
    }

    /// <summary>True while the sequence is playing, including its start delay.</summary>
    public bool IsRunning { get { return _running; } }

    /// <summary>Fired as each step begins, after it is current and before its onEnter. Lets other
    /// components follow the sequence without being wired into every step.</summary>
    public event Action<Step> OnStepEntered;

    /// <summary>Fired once when the last step ends (not on Stop()).</summary>
    public event Action OnFinished;

    /// <summary>
    /// Start from the top. Safe to call while running — it restarts rather than stacking,
    /// because two copies of one sequence driving the same objects is not a state anybody
    /// can reason about.
    /// </summary>
    public void Play()
    {
        if (playOnce && _played)
        {
            if (debugLog) Debug.Log("[JourneySequence_NEW] Already played; ignoring.", this);
            return;
        }

        _played = true;
        _running = true;
        _index = -1;
        _elapsed = 0f;
        _delayRemaining = startDelay;

        if (debugLog)
            Debug.Log("[JourneySequence_NEW] Starting, " + steps.Length + " steps, after " +
                      startDelay.ToString("0.0") + "s.", this);
    }

    /// <summary>Stop where it is. Fires nothing further; does not undo what already fired.</summary>
    public void Stop()
    {
        _running = false;
        _index = -1;
        _elapsed = 0f;
    }

    /// <summary>Let it play again. For the debug jump and for an attract restart.</summary>
    public void ResetPlayed()
    {
        _played = false;
    }

    /// <summary>Finish the current step now and move on. The skip key calls this.</summary>
    public void SkipStep()
    {
        if (!_running) return;

        _delayRemaining = 0f;
        _elapsed = float.MaxValue;
    }

    // -- Lifecycle ------------------------------------------------------------

    void Awake()
    {
        if (layerState == null) layerState = FindObjectOfType<LayerState_NEW>();
    }

    void OnEnable()
    {
        if (layerState != null) layerState.OnLayerChanged += HandleLayerChanged;
    }

    void OnDisable()
    {
        if (layerState != null) layerState.OnLayerChanged -= HandleLayerChanged;
    }

    void HandleLayerChanged(LayerProfile_NEW previous, LayerProfile_NEW current)
    {
        if (current == null) return;
        if (!string.Equals(current.layerId, startOnLayerId, StringComparison.OrdinalIgnoreCase)) return;

        Play();
    }

    void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (skipStepKey != KeyCode.None && Input.GetKeyDown(skipStepKey)) SkipStep();

        if (debugPlayKey != KeyCode.None && Input.GetKeyDown(debugPlayKey))
        {
            // playOnce cleared first, so the key works a second time. A debug key that
            // silently stops responding is worse than not having one.
            ResetPlayed();
            Play();
        }
#endif

        if (!_running) return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        if (_delayRemaining > 0f)
        {
            _delayRemaining -= dt;
            if (_delayRemaining > 0f) return;

            // The overshoot is carried into the first step rather than thrown away, so
            // startDelay plus the first duration is what it says it is. At 60fps this is
            // a sixtieth of a second and nobody would notice it missing; it is here
            // because a sequence the voice-over is laid against should not lose a frame
            // per step for reasons that are not written down anywhere.
            dt = -_delayRemaining;
            _delayRemaining = 0f;
        }

        if (_index < 0)
        {
            EnterStep(0);

            // Fall through with the leftover dt rather than returning: the step has
            // started, and its clock starts now.
            if (!_running) return;
        }

        _elapsed += dt;

        Step step = steps[_index];
        if (_elapsed < step.duration) return;

        EnterStep(_index + 1);
    }

    void EnterStep(int index)
    {
        if (index >= steps.Length)
        {
            if (debugLog) Debug.Log("[JourneySequence_NEW] Finished.", this);
            Stop();
            if (OnFinished != null) OnFinished();
            return;
        }

        _index = index;
        _elapsed = 0f;

        Step step = steps[_index];

        if (debugLog)
            Debug.Log("[JourneySequence_NEW] " + step.id + " (" +
                      step.duration.ToString("0.0") + "s): " + step.description, this);

        if (OnStepEntered != null) OnStepEntered(step);

        // Invoked last, so anything it triggers sees the step already current.
        if (step.onEnter != null) step.onEnter.Invoke();
    }
}

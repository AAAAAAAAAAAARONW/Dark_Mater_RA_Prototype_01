using UnityEngine;

/// <summary>
/// Drops the time scale, and puts it back. D2's "time drops to 0.2×".
///
/// One component, one thing: it owns Time.timeScale and nothing else. The beats say
/// when through their UnityEvents, exactly as Phase 2's beats drive
/// TutorialEmission_NEW.
///
/// WHY THIS DOES NOT TOUCH THE CAMERA, and why that is the whole point. GDD §5 says the
/// camera is never locked, and §6 says the camera stays free during the D2 slow motion
/// so the player can look between the impact and the bar. Both hold for free here,
/// because every clock in the tutorial — FirstPersonLookRig_NEW, TutorialZoom_NEW,
/// TutorialDirector_NEW — reads Time.unscaledDeltaTime and never sees this. A slow
/// motion that also slowed the camera would be a camera lock wearing a different hat,
/// and the old build's locked transition is the thing playtesters read as a bug.
///
/// Beat durations are also unscaled, which is the decision recorded in §12: the
/// storyboard's timings are seconds the PLAYER EXPERIENCES, so D2's twelve seconds are
/// twelve seconds of wall clock and are not stretched to sixty. A beat that genuinely
/// wants stretching sets useScaledTime on itself; nothing here needs to know.
///
/// RESTORING IS NOT OPTIONAL, and it is the reason this is a component rather than two
/// lines in a beat. Time.timeScale is global and survives everything — a scene change,
/// the tutorial restarting, the handoff to the journey. A tutorial that exits D2 through
/// any path other than the one that was drawn, and there are several (F2 skips the beat,
/// number keys jump out of it, the 90 second idle returns to attract, somebody stops
/// Play mode), would otherwise hand the journey a world running at a fifth speed with
/// no clue where it came from. So the restore also runs on disable and on destroy.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("SLOW-MO", "#C0607A")]
public class TutorialSlowMotion_NEW : MonoBehaviour
{
    [Header("Scale")]
    [Tooltip("Time scale while slowed. GDD §6 says 0.2 for D2.")]
    [Range(0.01f, 1f)]
    [SerializeField] float slowScale = 0.2f;

    [Tooltip("Seconds the drop takes, in real time. 0 snaps.\n\n" +
             "A short ramp reads as the world reacting to the impact; a snap reads as a " +
             "frame drop, which is the wrong thing to teach a visitor about the moment " +
             "they made something happen.")]
    [SerializeField] float easeInSeconds = 0.15f;

    [Tooltip("Seconds the return takes, in real time. Longer than the drop on purpose — " +
             "coming back slowly keeps the impact as the sharp edge of the sequence.")]
    [SerializeField] float easeOutSeconds = 0.6f;

    [Tooltip("Time scale for D5's inspect, where the world is held still while the " +
             "player reads the spectrum.\n\n" +
             "Nearly zero rather than exactly zero: a timeScale of 0 stops Update on " +
             "anything that reads scaled time, and the absorption field is one of those " +
             "— it would stop composing and uploading its texture, so the enlarged bar " +
             "would be looking at a frozen buffer rather than a still one. At this value " +
             "nothing visibly moves and everything still runs.\n\n" +
             "The camera is unaffected either way, because the rig reads unscaled time. " +
             "GDD §5's 'the camera is never locked' holds through a frame whose entire " +
             "content is standing still.")]
    [Range(0.001f, 0.2f)]
    [SerializeField] float freezeScale = 0.01f;

    [Header("Physics")]
    [Tooltip("Also scale Time.fixedDeltaTime, so physics keeps its per-second step rate " +
             "while slowed.\n\n" +
             "The tutorial has no physics to speak of — the atom is a distance test, not " +
             "a collision — so this changes nothing today. It is on because leaving it " +
             "off is the kind of thing that is only noticed once something physical is " +
             "added and moves in steps.")]
    [SerializeField] bool scaleFixedTimestep = true;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    float _baseTimeScale = 1f;
    float _baseFixedDelta;
    bool _captured;

    float _target = 1f;

    /// <summary>Distance of the trip currently being made, so the ramp rate is right
    /// for the slow motion and for the deeper freeze alike.</summary>
    float _span = 1f;

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>True while the world is slowed or on its way there.</summary>
    public bool IsSlowed { get { return _target < 1f; } }

    /// <summary>Where the time scale is heading. Read by the debug overlay.</summary>
    public float TargetScale { get { return _target; } }

    /// <summary>Drop into slow motion. D2's onEnter, or the atom's onImpact.</summary>
    public void Enter()
    {
        Capture();

        _span = Mathf.Abs(_baseTimeScale - slowScale);
        _target = slowScale;

        if (easeInSeconds <= 0f) Write(slowScale);

        if (debugLog) Debug.Log("[TutorialSlowMotion_NEW] Entering " + slowScale + "x.", this);
    }

    /// <summary>Hold the world still. D5's inspect. Undone by Exit, like the slow motion.</summary>
    public void Freeze()
    {
        Capture();

        _span = Mathf.Abs(_baseTimeScale - freezeScale);
        _target = freezeScale;

        if (easeInSeconds <= 0f) Write(freezeScale);

        if (debugLog) Debug.Log("[TutorialSlowMotion_NEW] Freezing at " + freezeScale + "x.", this);
    }

    /// <summary>Come back to full speed. D2's onSatisfied, and D5's close.</summary>
    public void Exit()
    {
        _target = _baseTimeScale;

        if (easeOutSeconds <= 0f) Write(_baseTimeScale);

        if (debugLog) Debug.Log("[TutorialSlowMotion_NEW] Exiting.", this);
    }

    /// <summary>
    /// Back to full speed immediately, no ease. The attract reset and every teardown
    /// path use this — see the class summary on why restoring is not optional.
    /// </summary>
    public void RestoreNow()
    {
        _target = _baseTimeScale;
        Write(_baseTimeScale);
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        Capture();
    }

    /// <summary>
    /// Remember what normal is, once.
    ///
    /// Taken from the running scene rather than assumed to be 1, so a rehearsal running
    /// at half speed for a walkthrough is restored to half speed rather than snapped to
    /// full.
    /// </summary>
    void Capture()
    {
        if (_captured) return;

        _baseTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        _baseFixedDelta = Time.fixedDeltaTime;
        _target = _baseTimeScale;
        _captured = true;
    }

    void Update()
    {
        if (Mathf.Approximately(Time.timeScale, _target)) return;

        float seconds = _target < Time.timeScale ? easeInSeconds : easeOutSeconds;

        if (seconds <= 0f)
        {
            Write(_target);
            return;
        }

        // Unscaled: a ramp measured in scaled time would slow down as it worked,
        // and the last stretch of the drop would take most of the wall clock.
        //
        // The span is the distance of the trip that is currently being made, recorded
        // when it started. Deriving it from slowScale worked while that was the only
        // destination; D5's freeze goes further, and would have crawled at the slow
        // motion's rate all the way down.
        float step = _span <= 0f ? 1f : (_span / seconds) * Time.unscaledDeltaTime;

        Write(Mathf.MoveTowards(Time.timeScale, _target, step));
    }

    void OnDisable()
    {
        RestoreNow();
    }

    void OnDestroy()
    {
        RestoreNow();
    }

    void Write(float scale)
    {
        Time.timeScale = scale;

        if (!scaleFixedTimestep || _baseTimeScale <= 0f) return;

        Time.fixedDeltaTime = _baseFixedDelta * (scale / _baseTimeScale);
    }

    // ── Debug overlay ────────────────────────────────────────────────────────

    void OnGUI()
    {
        if (!DebugView_NEW.Overlay) return;
        if (!IsSlowed && Mathf.Approximately(Time.timeScale, _baseTimeScale)) return;

        GUI.Label(DebugOverlayRows_NEW.Row(DebugOverlayRows_NEW.Time),
                  string.Format("TIME  {0:F2}x -> {1:F2}x", Time.timeScale, _target));
    }
}

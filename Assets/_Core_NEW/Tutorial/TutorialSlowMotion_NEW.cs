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

    [Tooltip("Time scale while TutorialPause_NEW holds the piece. 0 is a true pause.\n\n" +
             "Update keeps running at a time scale of 0 — only FixedUpdate stops — so the " +
             "absorption field still composes and uploads its texture and the paused " +
             "spectrum is live, just not moving. Particles, trail fade and the spectrum " +
             "drift all stop.\n\n" +
             "This does NOT stop the light, the atoms or the beats: they run on the world " +
             "clock, TutorialClock_NEW, which the pause stops separately.\n\n" +
             "The camera is unaffected either way, because the rig reads unscaled time. " +
             "GDD §5's 'the camera is never locked' holds through a frame whose entire " +
             "content is standing still.")]
    [Range(0f, 0.2f)]
    [SerializeField] float freezeScale = 0f;

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

    /// <summary>True while TutorialPause_NEW holds the clock.</summary>
    bool _held;

    /// <summary>What Release hands back: where the slow motion would be heading now.</summary>
    float _heldTarget = 1f;

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>True while the world is slowed or on its way there.</summary>
    public bool IsSlowed { get { return _target < 1f; } }

    /// <summary>Where the time scale is heading. Read by the debug overlay.</summary>
    public float TargetScale { get { return _target; } }

    /// <summary>Drop into slow motion. D2's onEnter.</summary>
    public void Enter()
    {
        Capture();

        _span = Mathf.Abs(_baseTimeScale - slowScale);
        SetTarget(slowScale, easeInSeconds);

        if (debugLog) Debug.Log("[TutorialSlowMotion_NEW] Entering " + slowScale + "x.", this);
    }

    /// <summary>Come back to full speed. D2's onSatisfied.</summary>
    public void Exit()
    {
        Capture();

        SetTarget(_baseTimeScale, easeOutSeconds);

        if (debugLog) Debug.Log("[TutorialSlowMotion_NEW] Exiting.", this);
    }

    /// <summary>
    /// Stop the clock for TutorialPause_NEW, remembering what it was doing.
    ///
    /// A LOAN, NOT A SETTING. The pause can land at any moment, including inside D2's
    /// slow motion. If it simply set the time scale to zero and resuming set it back to
    /// one, a player who paused during the impact would resume at full speed with D2
    /// still open — the slow motion silently gone. So the pause holds the clock, and
    /// Release hands back whatever the slow motion would be doing by now.
    ///
    /// Instant both ways. A pause that eased would let the world visibly keep moving
    /// after the button was pressed.
    /// </summary>
    public void Hold()
    {
        Capture();
        if (_held) return;

        _held = true;
        _heldTarget = _target;

        Write(freezeScale);

        if (debugLog) Debug.Log("[TutorialSlowMotion_NEW] Held for pause.", this);
    }

    /// <summary>Hand the clock back to whatever the slow motion is doing now.</summary>
    public void Release()
    {
        if (!_held) return;

        _held = false;
        _target = _heldTarget;

        Write(_target);

        if (debugLog) Debug.Log("[TutorialSlowMotion_NEW] Released to " + _target + "x.", this);
    }

    /// <summary>
    /// Back to full speed immediately, no ease, and out of any pause. The attract reset
    /// and every teardown path use this — see the class summary on why restoring is not
    /// optional.
    /// </summary>
    public void RestoreNow()
    {
        _held = false;
        _target = _baseTimeScale;
        Write(_baseTimeScale);
    }

    /// <summary>
    /// Where the slow motion is heading. While a pause holds the clock, this only
    /// updates what Release will hand back — so D2 ending during a pause (F2, a jump)
    /// does not unfreeze the screen underneath it.
    /// </summary>
    void SetTarget(float scale, float easeSeconds)
    {
        if (_held)
        {
            _heldTarget = scale;
            return;
        }

        _target = scale;

        if (easeSeconds <= 0f) Write(scale);
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
        // A held clock stays exactly where the pause put it; no ramp runs under a pause.
        if (_held) return;

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

        // At a true pause the physics step is left alone rather than scaled to zero,
        // which Unity rejects. Nothing steps at a time scale of 0 anyway.
        if (!scaleFixedTimestep || _baseTimeScale <= 0f || scale <= 0f) return;

        // Floored, because the first frames of a ramp up from a true pause produce a step
        // smaller than Unity accepts.
        Time.fixedDeltaTime = Mathf.Max(0.0001f, _baseFixedDelta * (scale / _baseTimeScale));
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

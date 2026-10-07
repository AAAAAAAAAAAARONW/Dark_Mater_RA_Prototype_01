using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runs the storyboard: one beat at a time, in order, advancing only when the current
/// beat says it is finished.
///
/// This is the whole control flow of the tutorial. It is deliberately a list and an
/// index rather than a coroutine chain, because GDD §5 requires the piece to be fully
/// re-runnable from any beat after a 90 second idle, and a half-finished coroutine is
/// the usual reason a re-run in an exhibition build comes back subtly wrong.
///
/// Beats are children of this object and are collected in Hierarchy order, so
/// reordering the storyboard is a drag in the Hierarchy and not an array edit. An
/// explicit list is still available for the case where a beat lives elsewhere.
///
/// The idle watchdog implements the attract return. It counts unscaled time since the
/// last deliberate input and raises OnIdleTimeout, which TutorialAttract_NEW turns into
/// a restart. It runs during every beat including the ones that ask for nothing, which
/// is intended: a visitor who walks away during A1 should not leave the piece parked in
/// the dark for the next person.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("TUTORIAL", "#E0A030")]
public class TutorialDirector_NEW : MonoBehaviour
{
    public enum State
    {
        /// <summary>Title card. Nothing is running. Waiting for A.</summary>
        Attract,

        /// <summary>A beat is open.</summary>
        Running,

        /// <summary>Every beat has been satisfied.</summary>
        Complete
    }

    [Header("Beats")]
    [Tooltip("Leave empty to collect TutorialBeat_NEW children in Hierarchy order, which " +
             "is the intended setup. Fill it only to include beats that are not children.")]
    [SerializeField] List<TutorialBeat_NEW> beats = new List<TutorialBeat_NEW>();

    [Header("Start")]
    [Tooltip("Begin in Attract, as the exhibition does. Off starts the first beat " +
             "immediately, which is what you want when iterating on one frame.")]
    [SerializeField] bool startInAttract = true;

    [Header("Idle return")]
    [Tooltip("Seconds of no input before the piece returns to attract. GDD §5 says 90.")]
    [SerializeField] float idleTimeoutSeconds = 90f;

    [Tooltip("Stick deflection below this does not count as input for the idle timer.")]
    [SerializeField] float idleStickDeadband = 0.1f;

    [Header("Debug")]
    [Tooltip("Skip the current beat. Behind DebugView_NEW.Overlay, so it is inert once " +
             "the debug overlay is switched off for the exhibition build.")]
    [SerializeField] KeyCode skipBeatKey = KeyCode.F2;

    [Tooltip("Step to the next pad profile — Vizlab, PlayStation, Xbox — for the case where " +
             "the auto-detection guesses wrong. Debug only, and behind DebugView_NEW.Overlay " +
             "like the skip key: the exhibition build pins the Logitech profile explicitly " +
             "through InputProfilePinner_NEW and should never need this. See PadProfile_NEW.")]
    [SerializeField] KeyCode padLayoutKey = KeyCode.F3;

    [Tooltip("First beat of each phase, in storyboard order. Number key 1 jumps to the " +
             "first entry, 2 to the second, and so on — the same convention as " +
             "LayerDebugJump_NEW uses in the journey, so it is one habit and not two.\n\n" +
             "Key 4 is Phase 3, which is where Phase 3 work is driven from. An id that " +
             "does not exist yet logs a warning and changes nothing, so the unbuilt " +
             "phases can sit in this list until they are built.\n\n" +
             "Behind DebugView_NEW.Overlay like the other debug keys, so the exhibition " +
             "build cannot be jumped out of its own storyboard.")]
    [SerializeField] string[] debugPhaseJumps = { "A1", "B1", "C1", "D1", "E1" };

    [Tooltip("On a jump, open and close every beat before the target first, so the world " +
             "arrives in the state the skipped frames would have left it in.\n\n" +
             "Leave this on. Without it, jumping to D1 lands you in Phase 3 still short " +
             "of the quasar with the photon trail switched off, because C3's emission — " +
             "which sends the light on through the quasar, lights the trail and settles " +
             "the speed — never ran.\n\n" +
             "It reproduces what beats do at their edges, not over their durations, so " +
             "ramps land at their final value and a jump can blink as it passes any " +
             "full-screen flash. Play through before judging how a frame feels.")]
    [SerializeField] bool replayPriorBeats = true;

    [SerializeField] bool debugLog = false;

    State _state = State.Attract;
    int _index = -1;
    float _idleElapsed;

    // ── Events ───────────────────────────────────────────────────────────────

    /// <summary>A beat has opened. The HUD uses this to decide what is on screen.</summary>
    public event Action<TutorialBeat_NEW> OnBeatEntered;

    /// <summary>A beat's gate has been met, before the next one opens.</summary>
    public event Action<TutorialBeat_NEW> OnBeatSatisfied;

    /// <summary>Every beat is done. Phase 4 hands over to the journey here.</summary>
    public event Action OnTutorialComplete;

    /// <summary>No input for idleTimeoutSeconds. Attract listens for this.</summary>
    public event Action OnIdleTimeout;

    /// <summary>
    /// A debug jump is about to open a beat out of order. Raised before the beats in
    /// between are replayed, so anything that carries over from one beat to the next —
    /// a voice-over line still being said — can drop it rather than finish it over a
    /// frame it does not belong to.
    /// </summary>
    public event Action OnJumped;

    // ── Public API ───────────────────────────────────────────────────────────

    public State CurrentState { get { return _state; } }

    /// <summary>
    /// Whether the piece opens on the title card.
    ///
    /// Exposed because TutorialAttract_NEW has to know before either Start runs, and
    /// Unity gives no order between two components on different objects. Reading the
    /// intent rather than the current state makes the answer the same either way round.
    /// </summary>
    public bool StartsInAttract { get { return startInAttract; } }
    public int BeatCount { get { return beats.Count; } }
    public int BeatIndex { get { return _index; } }

    public TutorialBeat_NEW CurrentBeat
    {
        get
        {
            if (_index < 0 || _index >= beats.Count) return null;
            return beats[_index];
        }
    }

    /// <summary>Open the first beat. Called by attract on A, or on Start when configured.</summary>
    public void StartTutorial()
    {
        CloseCurrentBeat();

        _state = State.Running;
        _index = -1;
        _idleElapsed = 0f;

        OpenNextBeat();
    }

    /// <summary>Close whatever is running and go back to the title card.</summary>
    public void ReturnToAttract()
    {
        CloseCurrentBeat();

        _state = State.Attract;
        _index = -1;
        _idleElapsed = 0f;

        if (debugLog) Debug.Log("[TutorialDirector_NEW] Returned to attract.", this);
    }

    /// <summary>Jump straight to a beat id. Whitebox and rehearsal only.</summary>
    public bool JumpToBeat(string id)
    {
        for (int i = 0; i < beats.Count; i++)
        {
            if (beats[i] == null) continue;
            if (!string.Equals(beats[i].BeatId, id, StringComparison.OrdinalIgnoreCase)) continue;

            CloseCurrentBeat();

            if (OnJumped != null) OnJumped();

            _state = State.Running;
            _index = i;
            _idleElapsed = 0f;

            if (replayPriorBeats) ReplayBeatsBefore(i);

            EnterBeatAt(i);
            return true;
        }

        Debug.LogWarning("[TutorialDirector_NEW] No beat with id '" + id + "'.", this);
        return false;
    }

    /// <summary>
    /// Open and close every beat before this one, without ticking any of them, so the
    /// world arrives in the state the skipped beats would have left it in.
    ///
    /// WHY A JUMP NEEDS THIS AT ALL. Beats own gating; everything that happens to the
    /// world hangs off their UnityEvents. So the world state after five beats is exactly
    /// the sum of five onEnter and onSatisfied invocations, and a jump that skips them
    /// arrives somewhere that never occurs in a real playthrough.
    ///
    /// Jumping to D1 was the case that made this necessary. C3's Emit sends the light on
    /// through the quasar, switches the photon trail on and settles the speed — so before
    /// this, pressing 4 dropped you into Phase 3 still short of the quasar, with no trail,
    /// at the wrong speed. Not a Phase 3 bug; a jump that skipped the frame that sets
    /// all three. (The replay gives C1's arrival no frames to run, so the emission puts
    /// the light where the arrival would have before sending it through.)
    ///
    /// Enter then Exit, with no Tick between, is the whole trick: Enter fires onEnter,
    /// Exit fires onSatisfied, and nothing advances a clock or waits on a gate. It costs
    /// one frame regardless of how many beats are replayed.
    ///
    /// WHAT IT STILL DOES NOT REPRODUCE. Anything a beat does over its duration rather
    /// than at its edges — the emission's twelve second speed ramp, the camera shake
    /// rising through C1 — lands at whatever value its final call sets rather than being
    /// played through. Full-screen flashes fire in quick succession on the way past, so
    /// a jump can blink. Neither matters for reaching a frame to work on it, and both
    /// are reasons to play through before judging how anything feels.
    /// </summary>
    void ReplayBeatsBefore(int index)
    {
        for (int i = 0; i < index && i < beats.Count; i++)
        {
            TutorialBeat_NEW beat = beats[i];
            if (beat == null) continue;

            beat.Enter();
            beat.Exit();
        }

        if (debugLog)
            Debug.Log("[TutorialDirector_NEW] Replayed " + index + " beat(s) to set up the world.", this);
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (beats.Count == 0) CollectChildBeats();

        int nulls = beats.RemoveAll(IsNullBeat);
        if (nulls > 0)
            Debug.LogWarning("[TutorialDirector_NEW] Removed " + nulls + " empty beat slots.", this);

        if (beats.Count == 0)
            Debug.LogError("[TutorialDirector_NEW] No beats. The tutorial will do nothing.", this);

        WarnOnDuplicateIds();
    }

    static bool IsNullBeat(TutorialBeat_NEW beat)
    {
        return beat == null;
    }

    void Start()
    {
        if (startInAttract) _state = State.Attract;
        else StartTutorial();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        TickIdle(dt);

        // Not inside the Running check: a pad that turns the view from its triggers is
        // most obvious in attract, before there is a beat to skip.
        if (DebugView_NEW.Overlay && Input.GetKeyDown(padLayoutKey))
        {
            TutorialInput_NEW.TogglePad();
            if (debugLog) Debug.Log("[TutorialDirector_NEW] Pad layout " + TutorialInput_NEW.Pad + ".", this);
        }

        // Also outside the Running check, so a phase can be reached from the title card
        // without pressing A first — which is the whole point when iterating on Phase 3.
        if (DebugView_NEW.Overlay) TickPhaseJumps();

        if (_state != State.Running) return;

        // NOTHING ADVANCES WHILE PAUSED — not the clock and not the gates. The idle
        // watchdog above keeps counting on real time, deliberately: a visitor who paused
        // and walked away still returns the piece to attract for the next person.
        //
        // Returning here rather than ticking with a zero delta matters. A zero delta
        // would freeze duration beats but still run every gate's OnBeatTick, and those
        // read input: pressing A while paused would fire C2's emission behind the pause.
        if (TutorialClock_NEW.Paused) return;

        if (DebugView_NEW.Overlay && Input.GetKeyDown(skipBeatKey))
        {
            TutorialBeat_NEW skipped = CurrentBeat;
            if (skipped != null)
            {
                skipped.ForceSatisfy();
                if (debugLog) Debug.Log("[TutorialDirector_NEW] Skipped " + skipped.BeatId + ".", this);
            }
        }

        TutorialBeat_NEW beat = CurrentBeat;
        if (beat == null)
        {
            OpenNextBeat();
            return;
        }

        beat.Tick(dt);

        if (beat.IsSatisfied())
        {
            beat.Exit();

            if (OnBeatSatisfied != null) OnBeatSatisfied(beat);
            if (debugLog) Debug.Log("[TutorialDirector_NEW] " + beat.BeatId + " satisfied.", this);

            OpenNextBeat();
        }
    }

    // ── Internals ────────────────────────────────────────────────────────────

    void TickIdle(float dt)
    {
        if (idleTimeoutSeconds <= 0f) return;

        // Attract is already the idle state. Counting there would restart it forever.
        if (_state == State.Attract) return;

        if (TutorialInput_NEW.AnyInput(idleStickDeadband))
        {
            _idleElapsed = 0f;
            return;
        }

        _idleElapsed += dt;
        if (_idleElapsed < idleTimeoutSeconds) return;

        _idleElapsed = 0f;

        if (debugLog) Debug.Log("[TutorialDirector_NEW] Idle timeout.", this);

        if (OnIdleTimeout != null) OnIdleTimeout();
        else ReturnToAttract();
    }

    /// <summary>
    /// Number keys jump to the first beat of each phase.
    ///
    /// Both the number row and the keypad, matching LayerDebugJump_NEW so the two debug
    /// tools in this project answer to the same fingers.
    ///
    /// THE CAVEAT IS THE SAME ONE THE JOURNEY'S JUMP TOOL CARRIES, and it matters more
    /// here. Jumping to D1 does not run the beats before it, so nothing their onEnter
    /// events would have done has happened: the emission has not fired, the light has not
    /// been through the quasar, the trail is off and the HUD is in whatever state it started in.
    /// That is fine for iterating on one frame and wrong for judging how a frame reads
    /// after the one before it. Play through for anything that is a question about pacing.
    /// </summary>
    void TickPhaseJumps()
    {
        if (debugPhaseJumps == null) return;

        for (int i = 0; i < debugPhaseJumps.Length && i < 9; i++)
        {
            if (!Input.GetKeyDown(KeyCode.Alpha1 + i) && !Input.GetKeyDown(KeyCode.Keypad1 + i))
                continue;

            string id = debugPhaseJumps[i];
            if (string.IsNullOrEmpty(id)) continue;

            // JumpToBeat warns for an id that does not exist yet, which is the normal
            // answer while a phase is unbuilt. Nothing changes and the piece carries on.
            JumpToBeat(id);
            return;
        }
    }

    void OpenNextBeat()
    {
        int next = _index + 1;

        if (next >= beats.Count)
        {
            _state = State.Complete;
            _index = beats.Count;

            if (debugLog) Debug.Log("[TutorialDirector_NEW] Complete.", this);
            if (OnTutorialComplete != null) OnTutorialComplete();
            return;
        }

        _index = next;
        EnterBeatAt(next);
    }

    void EnterBeatAt(int i)
    {
        TutorialBeat_NEW beat = beats[i];
        if (beat == null) return;

        beat.Enter();

        if (debugLog)
            Debug.Log("[TutorialDirector_NEW] " + beat.BeatId + " entered (" + beat.Mode + ").", this);

        if (OnBeatEntered != null) OnBeatEntered(beat);
    }

    void CloseCurrentBeat()
    {
        TutorialBeat_NEW beat = CurrentBeat;
        if (beat != null && beat.IsActive) beat.Exit();
    }

    void CollectChildBeats()
    {
        beats.Clear();

        // GetComponentsInChildren returns depth-first Hierarchy order, which is the
        // order shown in the Hierarchy window. That is the storyboard order.
        TutorialBeat_NEW[] found = GetComponentsInChildren<TutorialBeat_NEW>(true);
        beats.AddRange(found);
    }

    void WarnOnDuplicateIds()
    {
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < beats.Count; i++)
        {
            string id = beats[i].BeatId;

            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning("[TutorialDirector_NEW] Beat at index " + i + " has no id.", beats[i]);
                continue;
            }

            if (!seen.Add(id))
                Debug.LogWarning("[TutorialDirector_NEW] Duplicate beat id '" + id +
                                 "'. JumpToBeat will find the first.", beats[i]);
        }
    }

    // ── Debug overlay ────────────────────────────────────────────────────────

    /// <summary>
    /// The jump map, with an id that has no beat marked so it reads as "not built yet"
    /// rather than as a key that is broken.
    /// </summary>
    string PhaseJumpHint()
    {
        if (debugPhaseJumps == null || debugPhaseJumps.Length == 0) return "";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        for (int i = 0; i < debugPhaseJumps.Length && i < 9; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(i + 1).Append('=').Append(debugPhaseJumps[i]);
            if (!HasBeat(debugPhaseJumps[i])) sb.Append('?');
        }

        return sb.ToString();
    }

    /// <summary>Xbox names for the first four joystick buttons, in hardware order.</summary>
    static readonly string[] FaceButtonNames = { "A", "B", "X", "Y" };

    /// <summary>
    /// Which pad buttons are down right now, named.
    ///
    /// Here to settle a question the source cannot: §10 item 1 blocks D5 on what the
    /// three unused face buttons do, and grepping the project only proves that nothing
    /// in C# reads them. Bindings made through UnityEvents in prefabs, and anything a
    /// package reads internally, are invisible to that.
    ///
    /// So: press a button and watch this line. It reports what the pad is sending, which
    /// is the half of the question a search cannot answer. A button that shows here and
    /// changes nothing on screen is free.
    ///
    /// Names are the Xbox layout, which is the only supported one — on a PlayStation pad
    /// the numbers are the same and the names are not, so read the number.
    /// </summary>
    string PressedButtons()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        for (int i = 0; i < 10; i++)
        {
            if (!Input.GetKey(KeyCode.JoystickButton0 + i)) continue;

            if (sb.Length > 0) sb.Append(' ');
            sb.Append(i);

            if (i < FaceButtonNames.Length) sb.Append('=').Append(FaceButtonNames[i]);
        }

        return sb.Length == 0 ? "none" : sb.ToString();
    }

    /// <summary>
    /// What is plugged in, and what the build is doing about it.
    ///
    /// WHY THIS ROW EXISTS. The exhibition pad is a Logitech at Carnegie Observatories
    /// that nobody here can hold, and three separate things about it cannot be settled
    /// from a desk: which model it is, whether XInput can see it (so whether it can
    /// rumble), and whether detection recognises its name at all. All three are answers
    /// the build can read off the hardware the moment it is plugged in — so the build
    /// asks, rather than anyone guessing in advance.
    ///
    /// Three facts, deliberately side by side:
    ///
    ///   PAD      the model string Windows reports. This is the ONLY identification the
    ///            legacy Input Manager offers, and what every profile is matched against.
    ///   profile  which mapping is actually driving, and whether it was pinned or guessed.
    ///   xinput   whether rumble can reach it. For the Logitech this reads its X/D switch:
    ///            X mode is an XInput device and rumbles, D mode is not and cannot.
    ///
    /// A guessed profile that disagrees with the name is called out, because that is the
    /// scrambled-controls failure and it is otherwise invisible. A PINNED profile that
    /// disagrees is left alone: pinning is a deliberate override, and the exhibition build
    /// pins on purpose.
    /// </summary>
    string PadIdentity()
    {
        string line = "PAD  " + InputScheme_NEW.ConnectedPadNames() +
                      "   profile " + InputScheme_NEW.Pad.displayName +
                      (InputScheme_NEW.IsPinned ? " (pinned)" : " (detected)") +
                      "   xinput " + TutorialRumble_NEW.XInputSummary();

        if (!InputScheme_NEW.IsPinned)
        {
            PadProfile_NEW guess = InputScheme_NEW.Guess();

            if (guess == null)
                line += "   — name matches no profile, using the fallback";
            else if (guess != InputScheme_NEW.Pad)
                line += "   — name says " + guess.id + ", press F3";
        }

        return line;
    }

    bool HasBeat(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;

        for (int i = 0; i < beats.Count; i++)
            if (beats[i] != null && string.Equals(beats[i].BeatId, id, StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
    }

    void OnGUI()
    {
        if (!DebugView_NEW.Overlay) return;

        TutorialBeat_NEW beat = CurrentBeat;

        string line;
        if (_state != State.Running || beat == null)
            line = "TUTORIAL  " + _state;
        else
            line = string.Format("TUTORIAL  {0}  [{1}/{2}]  {3}  -  {4}",
                                 beat.BeatId, _index + 1, beats.Count, beat.Mode, beat.GateStatus());

        if (TutorialClock_NEW.Paused) line += "   PAUSED";

        GUI.Label(DebugOverlayRows_NEW.Row(DebugOverlayRows_NEW.DirectorState), line);
        GUI.Label(DebugOverlayRows_NEW.Row(DebugOverlayRows_NEW.DirectorKeys),
                  "F2 skip beat   F3 pad " + TutorialInput_NEW.Pad +
                  "   " + PhaseJumpHint() +
                  "   idle " + _idleElapsed.ToString("F0") +
                  "/" + idleTimeoutSeconds.ToString("F0") + "s");

        GUI.Label(DebugOverlayRows_NEW.Row(DebugOverlayRows_NEW.Buttons),
                  "PAD BUTTONS  " + PressedButtons() +
                  "   (press one to see what the pad sends — see §10 item 1)");

        GUI.Label(DebugOverlayRows_NEW.Row(DebugOverlayRows_NEW.Pad), PadIdentity());
    }
}

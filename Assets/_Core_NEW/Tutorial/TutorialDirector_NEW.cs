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

            _state = State.Running;
            _index = i;
            _idleElapsed = 0f;

            EnterBeatAt(i);
            return true;
        }

        Debug.LogWarning("[TutorialDirector_NEW] No beat with id '" + id + "'.", this);
        return false;
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

        if (_state != State.Running) return;

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

        GUI.Label(new Rect(10f, 10f, 900f, 22f), line);
        GUI.Label(new Rect(10f, 30f, 900f, 22f),
                  "F2 skip beat   idle " + _idleElapsed.ToString("F0") +
                  "/" + idleTimeoutSeconds.ToString("F0") + "s");
    }
}

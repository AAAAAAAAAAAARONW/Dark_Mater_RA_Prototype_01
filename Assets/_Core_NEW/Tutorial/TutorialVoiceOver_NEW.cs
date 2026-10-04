using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Says the tutorial's voice-over: each beat's narration when it opens, its spoken
/// instruction after that, the instruction again if the player has not done it, and the
/// two ending lines once the last beat is over.
///
/// WHY NOT A PlayOneShot ON EACH BEAT'S onEnter. That was the original seam and it fails
/// in four ways, all of which only show up once real lines are in:
///
///   * Lines talk over each other. A1 is gated, and a player who looks around quickly
///     reaches A2 while A1's eight second line is still going; A2's thirteen second
///     line then starts on top of it.
///   * The pause does not stop them. An AudioSource ignores the time scale and the
///     world clock both, so a line carries on behind the pause card.
///   * The attract reset does not stop them. The next visitor's title card comes up
///     with the last visitor's sentence still finishing.
///   * Nothing can say an instruction a second time, because nothing knows whether the
///     player has done it yet.
///
/// So the beats hold clips (TutorialBeat_NEW.VoiceOverClip and PromptClip) and this is
/// the one place that plays them, on one AudioSource.
///
/// THE RULES, in the order they matter:
///
///   1. Narration is never cut off by the story moving on. A beat that opens while a
///      line is being said queues its own lines behind it.
///   2. A queued line whose beat has already closed is dropped, not said late. Late
///      narration describes a frame the player is no longer looking at.
///   3. An instruction IS cut off the moment its gate is met. "Look right" said after
///      the player has looked right reads as the piece not having noticed.
///   4. On a gated beat the instruction comes back after promptRepeatSeconds of silence,
///      at most maxPromptRepeats times. The ninety second idle return is what deals with
///      a visitor who has walked away; this is for one who is standing there unsure.
///   5. Pause pauses the line. Reset, a debug jump and teardown stop it.
///
/// Gaps and the repeat timer run on the world clock (TutorialClock_NEW), so a pause also
/// holds a line that was about to start.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
[HierarchyBadge_NEW("VOICE OVER", "#6FA8DC")]
public class TutorialVoiceOver_NEW : MonoBehaviour
{
    [Header("Wiring")]
    [Tooltip("Leave empty to find it. Beats are heard about from its events.")]
    [SerializeField] TutorialDirector_NEW director;

    [Tooltip("Leave empty to use the AudioSource on this object. 2D (spatial blend 0): " +
             "a narrator is not a thing in the world, and a 3D source would pan with the " +
             "camera as the player looks around.")]
    [SerializeField] AudioSource source;

    [Header("Ending")]
    [Tooltip("Said when the last beat is done and the camera starts pulling back. " +
             "Assets/VoiceOver/Tutorial/Ending_PullBack.mp3 — Build or Update fills it.")]
    [SerializeField] AudioClip endingClip;

    [Tooltip("Said straight after the ending line, over the black and the title. " +
             "Assets/VoiceOver/Tutorial/Closing.mp3.\n\n" +
             "Watch SceneHandoff_NEW's titleHoldSeconds: the journey loads Single, which " +
             "unloads this scene and this line with it. The two ending lines together run " +
             "about thirteen seconds from the start of the pull-back.")]
    [SerializeField] AudioClip closingClip;

    [Header("Timing")]
    [Tooltip("Seconds of silence between two lines, so narration and the instruction " +
             "after it read as two things said rather than one run-on sentence.")]
    [Min(0f)]
    [SerializeField] float gapSeconds = 0.4f;

    [Tooltip("Gated beats only: seconds of silence after the instruction before it is " +
             "said again. Counted from when it finished, on the world clock.")]
    [Min(1f)]
    [SerializeField] float promptRepeatSeconds = 12f;

    [Tooltip("Gated beats only: how many times the instruction is repeated after the " +
             "first. 0 says it once.")]
    [Min(0)]
    [SerializeField] int maxPromptRepeats = 2;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    /// <summary>One thing to say. beat is null for the ending lines, which belong to no beat.</summary>
    struct Line
    {
        public AudioClip clip;
        public TutorialBeat_NEW beat;
        public bool isPrompt;
    }

    readonly List<Line> _queue = new List<Line>();

    bool _hasPlaying;
    Line _playing;
    bool _pausedByUs;

    /// <summary>World seconds of silence left before the next queued line may start.</summary>
    float _gapLeft;

    /// <summary>The beat that is open now, or null between beats and after the last.</summary>
    TutorialBeat_NEW _beat;

    bool _promptSaid;
    float _sincePrompt;
    int _repeats;

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Stop everything and forget the queue. Hung on the attract's onReset.</summary>
    public void ResetForAttract()
    {
        StopAll();
        _beat = null;
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (director == null) director = FindObjectOfType<TutorialDirector_NEW>();
        if (source == null) source = GetComponent<AudioSource>();

        if (source != null) source.playOnAwake = false;
    }

    void OnEnable()
    {
        if (director == null) return;

        director.OnBeatEntered += HandleBeatEntered;
        director.OnBeatSatisfied += HandleBeatSatisfied;
        director.OnTutorialComplete += HandleComplete;
        director.OnJumped += StopAll;
    }

    void OnDisable()
    {
        if (director != null)
        {
            director.OnBeatEntered -= HandleBeatEntered;
            director.OnBeatSatisfied -= HandleBeatSatisfied;
            director.OnTutorialComplete -= HandleComplete;
            director.OnJumped -= StopAll;
        }

        StopAll();
    }

    void Update()
    {
        if (source == null) return;

        // The pause first, and as a pause rather than a stop, so B TO RESUME picks the
        // sentence up where it left off. isPlaying reads false on a paused source, which
        // is why nothing below may run while paused — it would think the line had ended.
        if (TutorialClock_NEW.Paused)
        {
            if (_hasPlaying && !_pausedByUs)
            {
                source.Pause();
                _pausedByUs = true;
            }
            return;
        }

        if (_pausedByUs)
        {
            source.UnPause();
            _pausedByUs = false;
        }

        float dt = TutorialClock_NEW.DeltaTime;

        if (_hasPlaying)
        {
            if (source.isPlaying) return;
            Finished();
        }

        if (_gapLeft > 0f)
        {
            _gapLeft -= dt;
            if (_gapLeft > 0f) return;
        }

        if (PlayNextQueued()) return;

        TickPromptRepeat(dt);
    }

    // ── Director events ──────────────────────────────────────────────────────

    void HandleBeatEntered(TutorialBeat_NEW beat)
    {
        _beat = beat;
        _promptSaid = false;
        _sincePrompt = 0f;
        _repeats = 0;

        // Anything still waiting belongs to a beat that has closed. Rule 2.
        _queue.Clear();

        Enqueue(beat.VoiceOverClip, beat, false);
        Enqueue(beat.PromptClip, beat, true);
    }

    void HandleBeatSatisfied(TutorialBeat_NEW beat)
    {
        if (_beat == beat) _beat = null;

        // Rule 3: the instruction has been followed, so stop giving it.
        if (_hasPlaying && _playing.isPrompt && _playing.beat == beat)
        {
            source.Stop();
            Finished();
        }
    }

    void HandleComplete()
    {
        _beat = null;
        _queue.Clear();

        Enqueue(endingClip, null, false);
        Enqueue(closingClip, null, false);
    }

    // ── Internals ────────────────────────────────────────────────────────────

    void Enqueue(AudioClip clip, TutorialBeat_NEW beat, bool isPrompt)
    {
        if (clip == null) return;

        _queue.Add(new Line { clip = clip, beat = beat, isPrompt = isPrompt });
    }

    bool PlayNextQueued()
    {
        while (_queue.Count > 0)
        {
            Line line = _queue[0];
            _queue.RemoveAt(0);

            // Rule 2 again, for a line queued behind a long one whose beat has since
            // closed without another opening — the last beat before the ending.
            if (line.beat != null && line.beat != _beat) continue;

            Play(line);
            return true;
        }

        return false;
    }

    void Play(Line line)
    {
        // Play with the clip set, not PlayOneShot: one line at a time is the whole point,
        // and a one-shot can neither be paused on its own nor told apart from the next.
        source.clip = line.clip;
        source.Play();

        _playing = line;
        _hasPlaying = true;

        if (debugLog) Debug.Log("[TutorialVoiceOver_NEW] " + line.clip.name + ".", this);
    }

    void Finished()
    {
        if (_playing.isPrompt && _playing.beat == _beat)
        {
            _promptSaid = true;
            _sincePrompt = 0f;
        }

        _hasPlaying = false;
        _playing = default(Line);
        _gapLeft = gapSeconds;
    }

    /// <summary>Rule 4. Only on a gated beat that is still open and has said its instruction once.</summary>
    void TickPromptRepeat(float dt)
    {
        if (_beat == null || !_beat.IsActive) return;
        if (_beat.Mode != TutorialBeat_NEW.AdvanceMode.PlayerAction) return;
        if (_beat.PromptClip == null || !_promptSaid) return;
        if (_repeats >= maxPromptRepeats) return;

        _sincePrompt += dt;
        if (_sincePrompt < promptRepeatSeconds) return;

        _repeats++;
        _promptSaid = false;
        Enqueue(_beat.PromptClip, _beat, true);
    }

    void StopAll()
    {
        _queue.Clear();

        if (source != null) source.Stop();

        _hasPlaying = false;
        _playing = default(Line);
        _pausedByUs = false;
        _gapLeft = 0f;
        _promptSaid = false;
        _sincePrompt = 0f;
        _repeats = 0;
    }

    // ── Debug overlay ────────────────────────────────────────────────────────

    void OnGUI()
    {
        if (!DebugView_NEW.Overlay) return;

        string line = "VO  ";

        if (_hasPlaying && source != null && source.clip != null)
            line += source.clip.name + "  " + source.time.ToString("F1") + "/" +
                    source.clip.length.ToString("F1") + "s";
        else
            line += "silent";

        if (_queue.Count > 0) line += "   queued " + _queue.Count;

        if (_beat != null && _beat.Mode == TutorialBeat_NEW.AdvanceMode.PlayerAction &&
            _beat.PromptClip != null && _promptSaid)
            line += "   repeat in " + Mathf.Max(0f, promptRepeatSeconds - _sincePrompt).ToString("F0") +
                    "s (" + _repeats + "/" + maxPromptRepeats + ")";

        GUI.Label(DebugOverlayRows_NEW.Row(DebugOverlayRows_NEW.VoiceOver), line);
    }
}

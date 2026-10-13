using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Says the tutorial's voice-over from the voice-over table (VoiceOverTable_NEW), and
/// makes each beat wait for what is being said about it.
///
/// WHAT A BEAT'S LINES DO, in order — every timing below is a setting on that beat's row
/// in the table, edited in Tools > Journey NEW > Voice Over > Timeline:
///
///   1. The beat opens. After narrationDelay, its narration lines are queued, in order.
///   2. Cues fire at their moment inside a line, calling the beat's cue events.
///   3. The hint line comes up: on enter, when the narration ends, or at a named cue.
///      Until then the HUD keeps the hint, the stick diagram and the A prompt down.
///   4. A gated beat waits for its player. Its nudge is said nudgeDelay after the hint,
///      and repeated. If the player acted during the narration, the gate is remembered
///      (TutorialBeat_NEW latches it) and nothing is asked again.
///   5. Once the gate is met, the nudge stops and any AfterAction lines are said.
///   6. The beat lets go when its narration (waitForNarration) and AfterAction lines are
///      done — never sooner than its own clock or gate, never instead of them.
///
/// Duration beats can follow their narration instead of a fixed duration
/// (FollowNarration), so a re-recorded line re-times its frame by itself.
///
/// THE RULES THAT DO NOT CHANGE PER BEAT:
///
///   * A line is never cut off by the story moving on. A beat that opens while a line is
///     being said queues behind it.
///   * A queued line whose beat has closed is dropped, not said late.
///   * A nudge stops the moment its gate is met.
///   * Pause pauses the line. Reset, a debug jump and the F2 skip stop it.
///
/// A line with no recording is played as SILENCE for its estimated length, with its
/// text on the debug overlay — so the timing of a scene can be worked on before the
/// recording exists, and replacing the silence with the take changes nothing else.
///
/// Gaps, delays, silences and nudge timers run on the world clock (TutorialClock_NEW),
/// so a pause holds all of them.
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

    [Tooltip("The lines, their recordings and every beat's timing. Assets/VoiceOver/Tables/" +
             "VO_Tutorial.asset — Build or Update creates it from the script and the " +
             "recordings folder if it does not exist.")]
    [SerializeField] VoiceOverTable_NEW table;

    [Header("Timing")]
    [Tooltip("Seconds of silence between two lines, so narration and the instruction " +
             "after it read as two things said rather than one run-on sentence.")]
    [Min(0f)]
    [SerializeField] float gapSeconds = 0.4f;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    /// <summary>One thing waiting to be said.</summary>
    class Line
    {
        public VoiceOverLine_NEW line;
        public VoiceOverTake_NEW take;

        /// <summary>The beat it belongs to, or null for the ending.</summary>
        public TutorialBeat_NEW owner;

        // Filled in when it starts.
        public bool silent;
        public float length;
        public float elapsed;
        public bool[] cueFired;
    }

    readonly List<Line> _queue = new List<Line>();
    Line _playing;
    bool _pausedByUs;
    float _gapLeft;

    // -- The pause line: its own source, so the line that was paused keeps its place ----

    AudioSource _pauseSource;
    float _pausedFor;
    float _sincePauseLine;
    int _pauseLinesSaid;
    bool _wasPaused;

    // -- The open beat ---------------------------------------------------------

    TutorialBeat_NEW _beat;
    VoiceOverBeat_NEW _entry;

    readonly List<VoiceOverLine_NEW> _narration = new List<VoiceOverLine_NEW>();
    readonly List<VoiceOverLine_NEW> _nudges = new List<VoiceOverLine_NEW>();
    readonly List<VoiceOverLine_NEW> _after = new List<VoiceOverLine_NEW>();

    float _delayLeft;
    bool _narrationQueued;
    int _narrationLeft;
    float _tailLeft;

    bool _hintReleased;
    float _sinceHint;
    int _nudgesSaid;
    bool _nudgeActive;

    bool _latched;
    int _afterLeft;

    // -- The ending -------------------------------------------------------------

    bool _endingPending;
    float _endingDelayLeft;

    // -- Public API -----------------------------------------------------------

    /// <summary>The table being played. The timeline window reads it.</summary>
    public VoiceOverTable_NEW Table { get { return table; } }

    /// <summary>Stop everything and forget the queue. Hung on the attract's onReset.</summary>
    public void ResetForAttract()
    {
        StopAll();
        _endingPending = false;
    }

    // -- Lifecycle ------------------------------------------------------------

    void Awake()
    {
        if (director == null) director = FindObjectOfType<TutorialDirector_NEW>();
        if (source == null) source = GetComponent<AudioSource>();

        if (source != null) source.playOnAwake = false;

        // Made here rather than by the builder: it has nothing to tune that the main
        // source does not, so it copies the main one.
        _pauseSource = gameObject.AddComponent<AudioSource>();
        _pauseSource.playOnAwake = false;
        _pauseSource.loop = false;
        _pauseSource.spatialBlend = 0f;
        _pauseSource.priority = 0;
        if (source != null)
        {
            _pauseSource.volume = source.volume;
            _pauseSource.outputAudioMixerGroup = source.outputAudioMixerGroup;
        }

        if (table == null)
            Debug.LogWarning("[TutorialVoiceOver_NEW] No voice-over table. The tutorial runs " +
                             "silent and no beat waits for a line. Run Build or Update.", this);
    }

    void OnEnable()
    {
        if (director == null) return;

        director.OnBeatEntering += HandleBeatEntered;
        director.OnBeatSatisfied += HandleBeatSatisfied;
        director.OnTutorialComplete += HandleComplete;
        director.OnJumped += StopAll;
        director.OnSkipped += HandleSkipped;
    }

    void OnDisable()
    {
        if (director != null)
        {
            director.OnBeatEntering -= HandleBeatEntered;
            director.OnBeatSatisfied -= HandleBeatSatisfied;
            director.OnTutorialComplete -= HandleComplete;
            director.OnJumped -= StopAll;
            director.OnSkipped -= HandleSkipped;
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
            if (_playing != null && !_playing.silent && !_pausedByUs)
            {
                source.Pause();
                _pausedByUs = true;
            }

            TickPauseLine(Time.unscaledDeltaTime);
            return;
        }

        if (_wasPaused) EndPauseLine();

        if (_pausedByUs)
        {
            source.UnPause();
            _pausedByUs = false;
        }

        float dt = TutorialClock_NEW.DeltaTime;

        TickBeat(dt);
        TickEnding(dt);
        TickPlaying(dt);

        if (_playing == null)
        {
            if (_gapLeft > 0f) _gapLeft -= dt;
            if (_gapLeft <= 0f) PlayNextQueued();
        }

        UpdateHolds();
    }

    // -- Director events ------------------------------------------------------

    void HandleBeatEntered(TutorialBeat_NEW beat)
    {
        ReleaseBeat();

        // Anything still waiting belongs to a beat that has closed. The line being said
        // finishes; the queue does not.
        _queue.Clear();

        _beat = beat;
        _entry = table != null ? table.Find(beat.BeatId) : null;

        Collect(_entry, VoiceOverRole_NEW.Narration, _narration);
        Collect(_entry, VoiceOverRole_NEW.Nudge, _nudges);
        Collect(_entry, VoiceOverRole_NEW.AfterAction, _after);

        VoiceOverBeatSettings_NEW s = Settings;

        _narrationLeft = _narration.Count;
        _narrationQueued = _narration.Count == 0;
        _delayLeft = s.narrationDelay;
        _tailLeft = 0f;
        _afterLeft = _after.Count;
        _latched = false;
        _nudgesSaid = 0;
        _nudgeActive = false;
        _sinceHint = 0f;

        // The hint. Only a gated beat's instruction waits: a timed beat asks for nothing.
        _hintReleased = beat.Mode != TutorialBeat_NEW.AdvanceMode.PlayerAction ||
                        s.hintTiming == VoiceOverHintTiming_NEW.OnEnter ||
                        _narration.Count == 0;

        // A beat that follows its narration runs for exactly that long, from the table.
        if (beat.Mode == TutorialBeat_NEW.AdvanceMode.Duration &&
            s.durationMode == VoiceOverDurationMode_NEW.FollowNarration && _narration.Count > 0)
        {
            beat.OverrideDuration(NarrationSeconds() + s.tailSeconds);
        }

        if (!_narrationQueued && _delayLeft <= 0f) QueueNarration();

        // Set now, not next Update: the director asks the beat whether it is done on the
        // same frame it opened it, and a hold that arrives a frame late lets it go.
        UpdateHolds();
    }

    void HandleBeatSatisfied(TutorialBeat_NEW beat)
    {
        if (beat != _beat) return;

        // The nudge has been followed, so stop giving it.
        CutNudge();
        ReleaseBeat();
    }

    void HandleSkipped(TutorialBeat_NEW beat)
    {
        // A skip is a debug key. Cut the line rather than make the person skipping wait
        // for a sentence they are skipping past.
        StopAll();
    }

    void HandleComplete()
    {
        ReleaseBeat();
        _queue.Clear();

        VoiceOverBeat_NEW ending = table != null ? table.Find(VoiceOverBeat_NEW.EndingId) : null;
        if (ending == null) return;

        _endingPending = true;
        _endingDelayLeft = ending.settings != null ? ending.settings.narrationDelay : 0f;
    }

    // -- The open beat ----------------------------------------------------------

    VoiceOverBeatSettings_NEW Settings
    {
        get { return _entry != null && _entry.settings != null ? _entry.settings : Defaults; }
    }

    static readonly VoiceOverBeatSettings_NEW Defaults = new VoiceOverBeatSettings_NEW();

    void Collect(VoiceOverBeat_NEW entry, VoiceOverRole_NEW role, List<VoiceOverLine_NEW> into)
    {
        into.Clear();
        if (entry == null) return;

        for (int i = 0; i < entry.lines.Count; i++)
        {
            VoiceOverLine_NEW line = entry.lines[i];
            if (line == null || line.role != role) continue;

            VoiceOverTake_NEW take = table.Resolve(line);
            if (take == null || take.Length <= 0f) continue;

            into.Add(line);
        }
    }

    float NarrationSeconds()
    {
        float total = 0f;

        for (int i = 0; i < _narration.Count; i++)
        {
            total += table.Resolve(_narration[i]).Length;
            if (i > 0) total += gapSeconds;
        }

        return Settings.narrationDelay + total;
    }

    void QueueNarration()
    {
        _narrationQueued = true;

        for (int i = 0; i < _narration.Count; i++)
            Enqueue(_narration[i], _beat);
    }

    void TickBeat(float dt)
    {
        if (_beat == null || !_beat.IsActive) return;

        VoiceOverBeatSettings_NEW s = Settings;

        if (!_narrationQueued)
        {
            _delayLeft -= dt;
            if (_delayLeft <= 0f) QueueNarration();
        }

        if (_tailLeft > 0f) _tailLeft -= dt;

        // The player acted. Remembered by the beat; here it stops the nudge and starts
        // whatever is said after the action.
        if (!_latched && _beat.GateLatched)
        {
            _latched = true;
            CutNudge();

            for (int i = 0; i < _after.Count; i++)
                Enqueue(_after[i], _beat);
        }

        TickNudge(dt, s);
    }

    void TickNudge(float dt, VoiceOverBeatSettings_NEW s)
    {
        if (_beat.Mode != TutorialBeat_NEW.AdvanceMode.PlayerAction) return;
        if (!_hintReleased || _latched || _nudgeActive || _nudges.Count == 0) return;
        if (_nudgesSaid > s.nudgeRepeats) return;

        _sinceHint += dt;

        float wait = _nudgesSaid == 0 ? s.nudgeDelay : s.nudgeRepeatSeconds;
        if (_sinceHint < wait) return;

        _nudgeActive = true;
        Enqueue(_nudges[_nudgesSaid % _nudges.Count], _beat);
        _nudgesSaid++;
    }

    void ReleaseHint()
    {
        if (_hintReleased) return;

        _hintReleased = true;
        _sinceHint = 0f;
    }

    void UpdateHolds()
    {
        if (_beat == null) return;

        VoiceOverBeatSettings_NEW s = Settings;

        bool narrating = s.waitForNarration && _narrationLeft > 0;
        bool following = _tailLeft > 0f ||
                         (s.durationMode == VoiceOverDurationMode_NEW.FollowNarration &&
                          _beat.Mode == TutorialBeat_NEW.AdvanceMode.Duration && _narrationLeft > 0);

        _beat.SetExitHeld(narrating || following || _afterLeft > 0);
        _beat.SetHintHeld(!_hintReleased);
    }

    /// <summary>Let go of the open beat. Its line, if one is being said, finishes.</summary>
    void ReleaseBeat()
    {
        if (_beat != null)
        {
            _beat.SetExitHeld(false);
            _beat.SetHintHeld(false);
        }

        _beat = null;
        _entry = null;
        _narration.Clear();
        _nudges.Clear();
        _after.Clear();
        _narrationLeft = 0;
        _afterLeft = 0;
        _tailLeft = 0f;
    }

    void CutNudge()
    {
        _queue.RemoveAll(l => l.line.role == VoiceOverRole_NEW.Nudge);

        if (_playing != null && _playing.line.role == VoiceOverRole_NEW.Nudge)
        {
            source.Stop();
            Finished(_playing, false);
        }

        _nudgeActive = false;
    }

    // -- The ending -------------------------------------------------------------

    void TickEnding(float dt)
    {
        if (!_endingPending) return;

        _endingDelayLeft -= dt;
        if (_endingDelayLeft > 0f) return;

        _endingPending = false;

        VoiceOverBeat_NEW ending = table.Find(VoiceOverBeat_NEW.EndingId);
        if (ending == null) return;

        for (int i = 0; i < ending.lines.Count; i++)
        {
            VoiceOverLine_NEW line = ending.lines[i];
            if (line != null && line.role == VoiceOverRole_NEW.Narration) Enqueue(line, null);
        }
    }

    // -- Playing ---------------------------------------------------------------

    void Enqueue(VoiceOverLine_NEW line, TutorialBeat_NEW owner)
    {
        VoiceOverTake_NEW take = table != null ? table.Resolve(line) : null;
        if (take == null || take.Length <= 0f) return;

        _queue.Add(new Line { line = line, take = take, owner = owner });
    }

    void PlayNextQueued()
    {
        while (_queue.Count > 0)
        {
            Line next = _queue[0];
            _queue.RemoveAt(0);

            // Its beat has closed since it was queued.
            if (next.owner != null && next.owner != _beat) continue;

            Play(next);
            return;
        }
    }

    void Play(Line line)
    {
        line.silent = line.take.clip == null;
        line.length = line.take.Length;
        line.elapsed = 0f;
        line.cueFired = new bool[line.line.cues.Count];

        if (!line.silent)
        {
            // Play with the clip set, not PlayOneShot: one line at a time is the whole
            // point, and a one-shot can neither be paused on its own nor told apart.
            source.clip = line.take.clip;
            source.Play();
        }

        _playing = line;

        if (debugLog)
            Debug.Log("[TutorialVoiceOver_NEW] " + line.line.id + (line.silent ? " (estimate, silent)" : "") +
                      ".", this);
    }

    void TickPlaying(float dt)
    {
        if (_playing == null) return;

        bool done;
        float t;

        if (_playing.silent)
        {
            _playing.elapsed += dt;
            t = _playing.elapsed;
            done = t >= _playing.length;
        }
        else
        {
            t = source.time;
            done = !source.isPlaying;
        }

        FireCues(_playing, done ? float.MaxValue : t);

        if (done) Finished(_playing, true);
    }

    void FireCues(Line line, float upTo)
    {
        List<VoiceOverCue_NEW> cues = line.line.cues;

        for (int i = 0; i < cues.Count && i < line.cueFired.Length; i++)
        {
            if (line.cueFired[i] || cues[i] == null) continue;
            if (cues[i].Resolve(line.length) > upTo) continue;

            line.cueFired[i] = true;

            // A line from a beat that has already closed (an AfterAction line on the way
            // out, or narration finishing over the next frame) does not reach back into
            // the beat that owned it.
            if (line.owner == null || line.owner != _beat) continue;

            line.owner.FireCue(cues[i].key);

            VoiceOverBeatSettings_NEW s = Settings;
            if (s.hintTiming == VoiceOverHintTiming_NEW.AtCue &&
                string.Equals(s.hintCue, cues[i].key, System.StringComparison.OrdinalIgnoreCase))
                ReleaseHint();
        }
    }

    /// <summary>A line has ended — by finishing (completed), or by being cut short.</summary>
    void Finished(Line line, bool completed)
    {
        _playing = null;
        _gapLeft = gapSeconds;

        if (line.owner == null || line.owner != _beat) return;

        switch (line.line.role)
        {
            case VoiceOverRole_NEW.Narration:
                _narrationLeft = Mathf.Max(0, _narrationLeft - 1);

                if (_narrationLeft == 0)
                {
                    // AtCue falls back to the end of the narration, so a cue renamed or
                    // deleted in the table cannot leave the instruction hidden forever.
                    ReleaseHint();

                    if (Settings.durationMode == VoiceOverDurationMode_NEW.FollowNarration &&
                        _beat.Mode == TutorialBeat_NEW.AdvanceMode.Duration)
                        _tailLeft = Settings.tailSeconds;
                }
                break;

            case VoiceOverRole_NEW.Nudge:
                _nudgeActive = false;
                _sinceHint = 0f;
                break;

            case VoiceOverRole_NEW.AfterAction:
                _afterLeft = Mathf.Max(0, _afterLeft - 1);
                break;
        }
    }

    // -- The pause line -----------------------------------------------------------------------

    /// <summary>
    /// While paused: the Pause row's nudge, after its delay, on real time - the world
    /// clock is stopped, which is the point of a pause. Repeats per the row's settings.
    /// </summary>
    void TickPauseLine(float dt)
    {
        _wasPaused = true;

        if (table == null || _pauseSource == null) return;

        VoiceOverBeat_NEW entry = table.Find(VoiceOverBeat_NEW.PauseId);
        if (entry == null) return;

        _pausedFor += dt;
        if (_pauseSource.isPlaying) return;

        if (_pauseLinesSaid > 0) _sincePauseLine += dt;

        VoiceOverBeatSettings_NEW s = entry.settings ?? Defaults;
        if (_pauseLinesSaid > s.nudgeRepeats) return;

        bool due = _pauseLinesSaid == 0 ? _pausedFor >= s.nudgeDelay : _sincePauseLine >= s.nudgeRepeatSeconds;
        if (!due) return;

        List<VoiceOverLine_NEW> lines = new List<VoiceOverLine_NEW>();
        Collect(entry, VoiceOverRole_NEW.Nudge, lines);
        if (lines.Count == 0) return;

        VoiceOverTake_NEW take = table.Resolve(lines[_pauseLinesSaid % lines.Count]);
        _pauseLinesSaid++;
        _sincePauseLine = 0f;

        // A line with no recording yet has nothing to say out loud while paused.
        if (take == null || take.clip == null) return;

        _pauseSource.clip = take.clip;
        _pauseSource.Play();

        if (debugLog) Debug.Log("[TutorialVoiceOver_NEW] Pause line.", this);
    }

    /// <summary>Resumed: the pause line stops mid-word if it has to. Its job is done.</summary>
    void EndPauseLine()
    {
        _wasPaused = false;
        _pausedFor = 0f;
        _sincePauseLine = 0f;
        _pauseLinesSaid = 0;

        if (_pauseSource != null) _pauseSource.Stop();
    }

    void StopAll()
    {
        _queue.Clear();
        EndPauseLine();

        if (source != null) source.Stop();

        _playing = null;
        _pausedByUs = false;
        _gapLeft = 0f;

        ReleaseBeat();
    }

    // -- Debug overlay --------------------------------------------------------

    void OnGUI()
    {
        if (!DebugView_NEW.Overlay) return;

        string text = "VO  ";

        if (_playing != null)
        {
            float t = _playing.silent ? _playing.elapsed : (source != null ? source.time : 0f);
            text += _playing.line.id + "  " + t.ToString("F1") + "/" + _playing.length.ToString("F1") + "s";

            // A line with no recording yet is heard as silence; its words are here instead.
            if (_playing.silent) text += "  [estimate] " + Shorten(_playing.take.text, 70);
        }
        else
        {
            text += table == null ? "no table" : "silent";
        }

        if (_queue.Count > 0) text += "   queued " + _queue.Count;

        if (_beat != null && _hintReleased && !_latched && _nudges.Count > 0 &&
            _beat.Mode == TutorialBeat_NEW.AdvanceMode.PlayerAction)
        {
            VoiceOverBeatSettings_NEW s = Settings;
            float wait = _nudgesSaid == 0 ? s.nudgeDelay : s.nudgeRepeatSeconds;

            if (_nudgesSaid <= s.nudgeRepeats && !_nudgeActive)
                text += "   nudge in " + Mathf.Max(0f, wait - _sinceHint).ToString("F0") + "s";
        }

        GUI.Label(DebugOverlayRows_NEW.Row(DebugOverlayRows_NEW.VoiceOver), text);
    }

    static string Shorten(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace('\n', ' ');
        return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
    }
}

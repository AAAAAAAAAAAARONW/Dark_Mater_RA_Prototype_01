using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Says the journey's voice-over: a few lines per layer, timed off the layer
/// transitions, plus whatever a JourneySequence_NEW step asks for.
///
/// WHERE THE LINES HANG. Two seams, because the journey has two kinds of moment:
///
///   Layers   One row per layerId: which anchor of that layer's transition to wait for,
///            a delay after it, and the clips to say in order. CameraDirector_NEW owns
///            time, so this never decides WHEN a layer has arrived — it rides the same
///            anchors every other responder does. LookUpStart is the default: the cover
///            is coming off and the new world is about to be on screen, which is the
///            moment narration about that world can start.
///
///   Steps    JourneySequence_NEW steps call Say(clip) from their onEnter, so a line is
///            laid against the step it explains, visible in that step's Inspector.
///
/// A row can also say when it is done (onFinished). The redshift sequence hangs its
/// Play() there, so the demonstration starts after the cosmic web's introduction rather
/// than on a delay guessed to be long enough — the time from entering the layer to the
/// cover lifting depends on which transition it was, and a dive is not a cover.
///
/// THE RULES, the same as the tutorial's TutorialVoiceOver_NEW where they apply:
///
///   1. A line is never cut off. Anything that arrives while one is being said waits.
///   2. A queued line whose layer has been left is dropped, not said late — a debug jump
///      or a fast player would otherwise get the galaxy described over the Milky Way.
///   3. One AudioSource, 2D. A narrator is not a thing in the world.
///
/// Say(clip, done) is for something that waits on a line (DarkMatterStop_NEW): done(true)
/// when it has been said, done(false) if it never was, exactly once. Cancel(clip) takes a
/// line back — a nudge whose ask has just been done fades out rather than finishing.
///
/// No pause handling: the journey has no pause. If it gets one, this is where it goes.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
[HierarchyBadge_NEW("VOICE OVER", "#6FA8DC")]
public class JourneyVoiceOver_NEW : MonoBehaviour
{
    /// <summary>Something that happens at a moment inside one of a row's clips.</summary>
    [Serializable]
    public class LineCue
    {
        [Tooltip("Which of the row's clips. By clip, not by position, so adding a line to " +
                 "the row cannot move a cue onto the wrong one.")]
        public AudioClip line;

        [Tooltip("Seconds into that clip. 0 = as it starts.")]
        [Min(0f)]
        public float seconds = 0f;

        public UnityEvent onCue = new UnityEvent();
    }

    [Serializable]
    public class LayerLines
    {
        [Tooltip("Matches LayerProfile_NEW.layerId.")]
        public string layerId = "";

        [Tooltip("Which moment of the transition into this layer starts the lines.\n\n" +
                 "LookUpStart (default): the camera is about to come back to the new world.\n" +
                 "CoverReached: the world has just swapped, still hidden.\n" +
                 "SequenceStart: the gate has just fired.\n\n" +
                 "The start layer fires all three on the first frame.")]
        public TransitionAnchor_NEW anchor = TransitionAnchor_NEW.LookUpStart;

        [Tooltip("Seconds after the anchor before the first line.")]
        [Min(0f)]
        public float delay = 0f;

        [Tooltip("Said in order, one after another.")]
        public AudioClip[] clips = new AudioClip[0];

        [Tooltip("Moments inside the clips: J1's look back fires as J1 starts. Played clip by " +
                 "clip, so a cue on a clip that is dropped (the layer was left) never fires.")]
        public LineCue[] cues = new LineCue[0];

        [Tooltip("Fires when the last of these clips has finished — or straight after the " +
                 "anchor and delay if there are none, so something hung here cannot be " +
                 "stranded by a missing file. Does not fire if the layer was left first.")]
        public UnityEvent onFinished = new UnityEvent();
    }

    [Header("Wiring (found if empty)")]
    [SerializeField] CameraDirector_NEW director;
    [SerializeField] LayerState_NEW layerState;

    [Tooltip("Leave empty to use the AudioSource on this object.")]
    [SerializeField] AudioSource source;

    [Header("Lines")]
    [SerializeField] LayerLines[] layers = new LayerLines[0];

    [Header("Timing")]
    [Tooltip("Seconds of silence between two lines.")]
    [Min(0f)]
    [SerializeField] float gapSeconds = 0.4f;

    [Tooltip("Seconds to fade out a line that is cancelled while being said (Cancel). Only a " +
             "nudge is ever cancelled: the player did what it asked, so it stops at once rather " +
             "than finishing an instruction that has already been followed.")]
    [Min(0f)]
    [SerializeField] float cancelFadeSeconds = 0.3f;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    /// <summary>One thing to say. row is set on a row's last clip, so its onFinished can fire.</summary>
    struct Line
    {
        public AudioClip clip;
        public string layerId;
        public LayerLines finishes;

        /// <summary>The row and clip index it came from, for cues. Null for Say() lines.</summary>
        public LayerLines row;
        public int index;

        /// <summary>Say(clip, done) only: true once said to the end, false if it never was.</summary>
        public Action<bool> done;
    }

    /// <summary>A row whose anchor has fired and whose delay is still running.</summary>
    struct Pending
    {
        public LayerLines row;
        public float timeLeft;
    }

    readonly List<Line> _queue = new List<Line>();
    readonly List<Pending> _pending = new List<Pending>();

    bool _hasPlaying;
    Line _playing;
    float _gapLeft;

    // A cancelled line fading out: seconds left, and the volume to put back after.
    float _fadeLeft = -1f;
    float _volume = 1f;
    bool[] _cueFired = new bool[0];

    // -- Public API -----------------------------------------------------------

    /// <summary>
    /// Say one line, after whatever is already being said. For a JourneySequence_NEW
    /// step's onEnter. Tagged with the layer that is current now, so leaving the layer
    /// before its turn drops it.
    /// </summary>
    public void Say(AudioClip clip)
    {
        if (clip == null) return;

        _queue.Add(new Line { clip = clip, layerId = CurrentLayerId() });
    }

    /// <summary>
    /// Say(clip), and be told how it went: done(true) when it has been said to the end,
    /// done(false) if it never was — dropped because the layer was left, cancelled, or
    /// stopped. Called exactly once either way, so whoever is waiting on a line can never
    /// be left waiting. A null clip calls done(false) at once.
    /// </summary>
    public void Say(AudioClip clip, Action<bool> done)
    {
        if (clip == null)
        {
            if (done != null) done(false);
            return;
        }

        _queue.Add(new Line { clip = clip, layerId = CurrentLayerId(), done = done });
    }

    /// <summary>
    /// Take back a line: forgotten if it is still waiting, faded out over cancelFadeSeconds
    /// if it is being said. For a nudge whose ask has just been done.
    /// </summary>
    public void Cancel(AudioClip clip)
    {
        if (clip == null) return;

        for (int i = _queue.Count - 1; i >= 0; i--)
        {
            if (_queue[i].clip != clip) continue;
            Line l = _queue[i];
            _queue.RemoveAt(i);
            Report(l, false);
        }

        if (!_hasPlaying || _playing.clip != clip || _fadeLeft >= 0f || source == null) return;

        _volume = source.volume;
        _fadeLeft = cancelFadeSeconds;
        if (_fadeLeft <= 0f) EndCancelled();
    }

    /// <summary>True while this clip is being said or waiting to be.</summary>
    public bool IsSaying(AudioClip clip)
    {
        if (clip == null) return false;
        if (_hasPlaying && _playing.clip == clip) return true;

        for (int i = 0; i < _queue.Count; i++)
            if (_queue[i].clip == clip) return true;

        return false;
    }

    /// <summary>A line has begun to be said. For the flight recorder.</summary>
    public event Action<AudioClip> OnLineStarted;

    /// <summary>A line that began has ended: said to the end (true) or cut short (false).</summary>
    public event Action<AudioClip, bool> OnLineEnded;

    /// <summary>True while a line is being said or waiting to be.</summary>
    public bool IsSpeaking { get { return _hasPlaying || _queue.Count > 0 || _pending.Count > 0; } }

    /// <summary>Stop everything and forget what was waiting.</summary>
    public void StopAll()
    {
        List<Line> forgotten = new List<Line>(_queue);
        if (_hasPlaying)
        {
            forgotten.Add(_playing);
            if (OnLineEnded != null) OnLineEnded(_playing.clip, false);
        }

        _queue.Clear();
        _pending.Clear();

        if (source != null)
        {
            source.Stop();
            if (_fadeLeft >= 0f) source.volume = _volume;
        }
        _fadeLeft = -1f;

        _hasPlaying = false;
        _playing = default(Line);
        _gapLeft = 0f;

        foreach (Line l in forgotten) Report(l, false);
    }

    // -- Lifecycle ------------------------------------------------------------

    void Awake()
    {
        if (director == null) director = FindObjectOfType<CameraDirector_NEW>();
        if (layerState == null) layerState = FindObjectOfType<LayerState_NEW>();
        if (source == null) source = GetComponent<AudioSource>();

        if (source != null) source.playOnAwake = false;

        if (director == null)
            Debug.LogError("[JourneyVoiceOver_NEW] No CameraDirector_NEW. No layer lines will play.", this);
    }

    void OnEnable()
    {
        if (director != null) director.OnAnchor += HandleAnchor;
    }

    void OnDisable()
    {
        if (director != null) director.OnAnchor -= HandleAnchor;
        StopAll();
    }

    void Update()
    {
        if (source == null) return;

        float dt = Time.deltaTime;

        TickPending(dt);

        if (_fadeLeft >= 0f)
        {
            _fadeLeft -= dt;
            if (_fadeLeft > 0f)
            {
                source.volume = _volume * (_fadeLeft / Mathf.Max(0.0001f, cancelFadeSeconds));
                return;
            }
            EndCancelled();
        }

        if (_hasPlaying)
        {
            FireCues(source.isPlaying ? source.time : float.MaxValue);
            if (source.isPlaying) return;
            Finished();
        }

        if (_gapLeft > 0f)
        {
            _gapLeft -= dt;
            if (_gapLeft > 0f) return;
        }

        PlayNextQueued();
    }

    // -- Internals ------------------------------------------------------------

    void HandleAnchor(AnchorEvent_NEW e)
    {
        if (e.profile == null) return;

        // A new layer has begun: anything still waiting belongs to one being left. Rule 2.
        // The line being said finishes. Rule 1.
        if (e.anchor == TransitionAnchor_NEW.SequenceStart) DropOtherLayers(e.profile.layerId);

        for (int i = 0; i < layers.Length; i++)
        {
            LayerLines row = layers[i];
            if (row == null || row.anchor != e.anchor) continue;
            if (!string.Equals(row.layerId, e.profile.layerId, StringComparison.OrdinalIgnoreCase)) continue;

            _pending.Add(new Pending { row = row, timeLeft = row.delay });
        }

        // A zero delay is queued on this frame rather than the next, so a start layer
        // whose anchors all fire at once keeps its rows in the order they fired.
        TickPending(0f);
    }

    void TickPending(float dt)
    {
        for (int i = 0; i < _pending.Count; i++)
        {
            Pending p = _pending[i];
            p.timeLeft -= dt;

            if (p.timeLeft > 0f)
            {
                _pending[i] = p;
                continue;
            }

            _pending.RemoveAt(i);
            i--;

            if (!IsCurrent(p.row.layerId)) continue;

            QueueRow(p.row);
        }
    }

    void QueueRow(LayerLines row)
    {
        int last = -1;
        for (int i = 0; i < row.clips.Length; i++)
            if (row.clips[i] != null) last = i;

        if (last < 0)
        {
            row.onFinished.Invoke();
            return;
        }

        for (int i = 0; i <= last; i++)
        {
            if (row.clips[i] == null) continue;

            _queue.Add(new Line
            {
                clip = row.clips[i],
                layerId = row.layerId,
                finishes = i == last ? row : null,
                row = row,
                index = i
            });
        }
    }

    void DropOtherLayers(string layerId)
    {
        // Said out loud in the Console, because a line silently skipped looks exactly like a
        // line that is missing.
        foreach (Line l in _queue)
            if (!string.IsNullOrEmpty(l.layerId) && !string.Equals(l.layerId, layerId, StringComparison.OrdinalIgnoreCase))
                Debug.Log("[JourneyVoiceOver_NEW] Dropped " + l.clip.name + ": the light left " + l.layerId +
                          " before it was said. Slow that layer, or shorten what comes before it.", this);

        for (int i = _queue.Count - 1; i >= 0; i--)
        {
            Line l = _queue[i];
            if (string.IsNullOrEmpty(l.layerId) || string.Equals(l.layerId, layerId, StringComparison.OrdinalIgnoreCase)) continue;
            _queue.RemoveAt(i);
            Report(l, false);
        }

        _pending.RemoveAll(p => !string.Equals(p.row.layerId, layerId, StringComparison.OrdinalIgnoreCase));
    }

    void PlayNextQueued()
    {
        while (_queue.Count > 0)
        {
            Line line = _queue[0];
            _queue.RemoveAt(0);

            if (!string.IsNullOrEmpty(line.layerId) && !IsCurrent(line.layerId))
            {
                Report(line, false);
                continue;
            }

            source.clip = line.clip;
            source.Play();

            _playing = line;
            _hasPlaying = true;
            _cueFired = line.row != null ? new bool[line.row.cues.Length] : new bool[0];

            if (OnLineStarted != null) OnLineStarted(line.clip);

            // A cue at 0 s goes with the first word, not a frame later.
            FireCues(0f);

            if (debugLog) Debug.Log("[JourneyVoiceOver_NEW] " + line.clip.name + ".", this);
            return;
        }
    }

    void FireCues(float upTo)
    {
        LayerLines row = _playing.row;
        if (row == null) return;

        for (int i = 0; i < row.cues.Length && i < _cueFired.Length; i++)
        {
            LineCue cue = row.cues[i];
            if (_cueFired[i] || cue == null || cue.line != _playing.clip || cue.seconds > upTo) continue;

            _cueFired[i] = true;
            cue.onCue.Invoke();

            if (debugLog) Debug.Log("[JourneyVoiceOver_NEW] Cue on " + _playing.clip.name + " at " + cue.seconds + " s.", this);
        }
    }

    void Finished()
    {
        LayerLines finishes = _playing.finishes;
        string layerId = _playing.layerId;
        Line said = _playing;

        _hasPlaying = false;
        _playing = default(Line);
        _gapLeft = gapSeconds;

        // Only if the layer is still the one being shown. A row's follow-on — the
        // redshift sequence — has no business starting in the layer after.
        if (OnLineEnded != null) OnLineEnded(said.clip, true);

        if (finishes != null && IsCurrent(layerId)) finishes.onFinished.Invoke();

        Report(said, true);
    }

    /// <summary>The line being said was cancelled and has faded out: end it, unsaid.</summary>
    void EndCancelled()
    {
        _fadeLeft = -1f;
        source.Stop();
        source.volume = _volume;

        Line cancelled = _playing;
        _hasPlaying = false;
        _playing = default(Line);
        _gapLeft = gapSeconds;

        if (OnLineEnded != null) OnLineEnded(cancelled.clip, false);

        Report(cancelled, false);
    }

    static void Report(Line line, bool said)
    {
        if (line.done != null) line.done(said);
    }

    string CurrentLayerId()
    {
        return layerState != null ? layerState.CurrentLayerId : null;
    }

    bool IsCurrent(string layerId)
    {
        string current = CurrentLayerId();
        if (string.IsNullOrEmpty(current) || string.IsNullOrEmpty(layerId)) return true;

        return string.Equals(current, layerId, StringComparison.OrdinalIgnoreCase);
    }

    // -- Debug overlay --------------------------------------------------------

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
        if (_pending.Count > 0) line += "   waiting on a delay " + _pending.Count;

        GUI.Label(new Rect(10f, Screen.height - 30f, 900f, 22f), line);
    }
}

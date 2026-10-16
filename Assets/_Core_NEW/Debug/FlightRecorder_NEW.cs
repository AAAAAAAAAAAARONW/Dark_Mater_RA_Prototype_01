using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// One play-through of the journey, written down where it happened: where the light was each
/// moment, when it entered each layer, and when each line was said. Read by the Flight Path
/// preview (Tools > Journey NEW > Flight Path) to draw a ruler and the voice-over along the
/// light's path in the Scene view.
///
/// WHY RECORD INSTEAD OF WORK IT OUT. The light's path is a straight line, but its pace is
/// not one number: each layer has its own speed and blend, the galaxies slow for their lines
/// and hurry out, the second web slows for the redshift demo, and every line starts on a
/// camera anchor that a dive fires seconds after the gate. Simulating all of that would be a
/// second copy of the journey that drifts from the first. Playing it once and writing down
/// what happened is exact, and stays exact while the cosmic web is moved, because the web
/// does not change where or when the light flies — only the doors and speeds do.
///
/// HOW IT GETS INTO A RUN. Not placed in any scene: the editor adds it when Play starts, if
/// "Record in Play mode" is ticked in the Flight Path window. Editor only.
///
/// LAYER BY LAYER. Each layer is a segment, timed from the moment the light entered it, so a
/// debug jump (2 = the first cosmic web) records that layer properly in a minute. Saving
/// replaces the segments of the layers flown this time and keeps the rest.
///
/// Anything can add a mark — FlightRecorder_NEW.Point / SpanStart / SpanEnd — and it is a
/// no-op when nothing is recording.
/// </summary>
[DisallowMultipleComponent]
public class FlightRecorder_NEW : MonoBehaviour
{
    // -- Data (also read by the editor) ------------------------------------------------------

    [Serializable]
    public class Mark
    {
        public string lane;     // "VO", "Moment", "Filament", ...
        public string label;
        public float t0, t1;    // seconds since the segment began; t1 = t0 for a point
        public Vector3 p0, p1;
        public bool open;       // started, not yet ended
    }

    [Serializable]
    public class Segment
    {
        public string layerId;
        public float sampleEvery;
        public List<Vector3> positions = new List<Vector3>();   // at 0, sampleEvery, 2×sampleEvery…
        public List<Mark> marks = new List<Mark>();
        public bool enteredByJump;

        public float Duration { get { return positions.Count > 1 ? (positions.Count - 1) * sampleEvery : 0f; } }

        /// <summary>Where the light was <paramref name="t"/> seconds into the segment.</summary>
        public Vector3 At(float t)
        {
            if (positions.Count == 0) return Vector3.zero;
            float f = Mathf.Clamp(t / sampleEvery, 0f, positions.Count - 1);
            int i = Mathf.Min((int)f, positions.Count - 2);
            if (i < 0) return positions[0];
            return Vector3.Lerp(positions[i], positions[i + 1], f - i);
        }
    }

    [Serializable]
    public class Recording
    {
        public string savedAt;
        public List<Segment> segments = new List<Segment>();

        public Segment Find(string layerId)
        {
            return segments.Find(s => string.Equals(s.layerId, layerId, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>Where recordings are kept: per machine, beside the project's other caches.</summary>
    public static string FilePath
    {
        get { return Path.Combine(Path.GetDirectoryName(Application.dataPath), "Library", "JourneyFlightRecording.json"); }
    }

    public static Recording Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            return JsonUtility.FromJson<Recording>(File.ReadAllText(FilePath));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[FlightRecorder_NEW] Could not read " + FilePath + ": " + e.Message);
            return null;
        }
    }

    // -- Marks from anywhere ------------------------------------------------------------

    static FlightRecorder_NEW _active;

    /// <summary>Something that happened at one moment, e.g. "Y pressed".</summary>
    public static void Point(string lane, string label)
    {
        if (_active != null) _active.AddMark(lane, label, false);
    }

    /// <summary>Something that starts now and ends at SpanEnd with the same lane and label.</summary>
    public static void SpanStart(string lane, string label)
    {
        if (_active != null) _active.AddMark(lane, label, true);
    }

    public static void SpanEnd(string lane, string label)
    {
        if (_active != null) _active.CloseMark(lane, label);
    }

    // -- Recording ------------------------------------------------------------------------

    [Tooltip("Seconds between position samples.")]
    [SerializeField] float sampleEvery = 0.1f;

    PlayerRig_NEW _player;
    LayerState_NEW _state;
    JourneyVoiceOver_NEW _voice;

    readonly List<Segment> _done = new List<Segment>();
    Segment _current;
    float _segmentTime;
    float _nextSample;
    Vector3 _lastPos;
    bool _saved;

    void Awake()
    {
        _active = this;
        _player = FindObjectOfType<PlayerRig_NEW>();
        _state = FindObjectOfType<LayerState_NEW>();
        _voice = FindObjectOfType<JourneyVoiceOver_NEW>();
    }

    void OnEnable()
    {
        if (_state != null) _state.OnLayerChanged += HandleLayerChanged;
        if (_voice != null)
        {
            _voice.OnLineStarted += HandleLineStarted;
            _voice.OnLineEnded += HandleLineEnded;
        }
    }

    void OnDisable()
    {
        if (_state != null) _state.OnLayerChanged -= HandleLayerChanged;
        if (_voice != null)
        {
            _voice.OnLineStarted -= HandleLineStarted;
            _voice.OnLineEnded -= HandleLineEnded;
        }
    }

    void Start()
    {
        if (_state != null && !string.IsNullOrEmpty(_state.CurrentLayerId)) StartSegment(_state.CurrentLayerId, false);
    }

    void HandleLayerChanged(LayerProfile_NEW previous, LayerProfile_NEW current)
    {
        if (current == null) return;

        // A jump moves the light further than any frame of flight could.
        bool jump = _player != null && _current != null &&
                    (_player.transform.position - _lastPos).sqrMagnitude > 25f * 25f;
        StartSegment(current.layerId, jump);
    }

    void HandleLineStarted(AudioClip clip) { if (clip != null) AddMark("VO", clip.name, true); }

    void HandleLineEnded(AudioClip clip, bool said) { if (clip != null) CloseMark("VO", clip.name); }

    void StartSegment(string layerId, bool jump)
    {
        if (_current != null) _done.Add(_current);

        _current = new Segment { layerId = layerId, sampleEvery = sampleEvery, enteredByJump = jump };
        _segmentTime = 0f;
        _nextSample = 0f;
        Sample();
    }

    void Update()
    {
        if (_current == null || _player == null) return;

        _segmentTime += Time.deltaTime;
        while (_segmentTime >= _nextSample) Sample();
        _lastPos = _player.transform.position;
    }

    void Sample()
    {
        Vector3 p = _player != null ? _player.transform.position : Vector3.zero;
        _current.positions.Add(p);
        _nextSample = _current.positions.Count * sampleEvery;
        _lastPos = p;
    }

    void AddMark(string lane, string label, bool span)
    {
        if (_current == null) return;

        Vector3 p = _player != null ? _player.transform.position : Vector3.zero;
        _current.marks.Add(new Mark { lane = lane, label = label, t0 = _segmentTime, t1 = _segmentTime, p0 = p, p1 = p, open = span });
    }

    void CloseMark(string lane, string label)
    {
        // A line can end in the layer after the one it started in; close it where it was opened.
        List<Segment> all = new List<Segment>(_done);
        if (_current != null) all.Add(_current);

        for (int s = all.Count - 1; s >= 0; s--)
        {
            List<Mark> marks = all[s].marks;
            for (int i = marks.Count - 1; i >= 0; i--)
            {
                Mark m = marks[i];
                if (!m.open || m.lane != lane || m.label != label) continue;

                m.open = false;
                if (all[s] == _current)
                {
                    m.t1 = _segmentTime;
                    m.p1 = _player != null ? _player.transform.position : m.p0;
                }
                else
                {
                    m.t1 = all[s].Duration;
                    m.p1 = all[s].positions.Count > 0 ? all[s].positions[all[s].positions.Count - 1] : m.p0;
                }
                return;
            }
        }
    }

    void OnDestroy()
    {
        if (_active == this) _active = null;
        Save();
    }

    void OnApplicationQuit() { Save(); }

    void Save()
    {
        if (_saved) return;
        _saved = true;

        if (_current != null) _done.Add(_current);
        _current = null;

        // A segment of a few frames is a layer passed through on the way somewhere (a jump).
        _done.RemoveAll(s => s.Duration < 1f);
        if (_done.Count == 0) return;

        Recording rec = Load() ?? new Recording();
        foreach (Segment s in _done)
        {
            foreach (Mark m in s.marks) if (m.open) { m.open = false; m.t1 = s.Duration; m.p1 = s.positions[s.positions.Count - 1]; }

            rec.segments.RemoveAll(o => string.Equals(o.layerId, s.layerId, StringComparison.OrdinalIgnoreCase));
            rec.segments.Add(s);
        }
        rec.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(rec));
            Debug.Log("[FlightRecorder_NEW] Recorded " + _done.Count + " layer(s): " +
                      string.Join(", ", _done.ConvertAll(s => s.layerId + " " + s.Duration.ToString("0") + " s").ToArray()) +
                      ". The Flight Path view in the Scene shows it.");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[FlightRecorder_NEW] Could not write " + FilePath + ": " + e.Message);
        }
    }
}

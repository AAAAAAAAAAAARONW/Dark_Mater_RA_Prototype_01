using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The music from the tutorial to the end of the journey, played by what happens rather than
/// by the clock.
///
/// WHY NOT ONE TRACK. How long each layer takes is the visitor's and the voice-over's: a line
/// re-recorded, the dark matter moment waited on, a pause, and a fixed track's climaxes have
/// slid off the picture. What IS fixed is each transition: a dive is diveSeconds to its peak
/// whenever it starts. So the score is in two kinds of piece, and this plays them:
///
///   beds   a loop for each place (Resources/Score/Bed_*: Tutorial, and one per layer id),
///          long held harmony with no beat, so it can be left at any moment. Crossfaded when
///          the layer changes - at the dive's peak, when the new world is first seen.
///   cues   one for each transition (Resources/Score/Cue_*), made to that transition's own
///          timing and started by the event that starts the picture, so its climax lands on
///          the picture's:
///            Outro           the tutorial ends: the camera pulls out, black, the title
///            Dive_<layer>    a dive begins (LayerDive_NEW.DiveBegan): the rise to its peak at
///                            diveSeconds, the bloom through the emerge
///            EarthApproach   Earth begins to grow (EarthZoom_NEW.ZoomBegan)
///            EarthArrive     the light goes into Earth (the Earth layer): the fall to the ground
///            Dome            the light goes into the telescope's dome (EarthSummit_NEW.Entered)
///            Spectrum        the cut to the observatory, the spectrum (EarthSummit_NEW.CutToOutside)
///
/// The beds sit under the voice (underVoice while a VoiceOver source plays) and under a cue's
/// rise (underCue). It stops for the cover and the credits, which have their own.
///
/// NOTHING TO SET UP. It makes itself when the game starts and lives across scene loads, so
/// the scenes - four of them, two made from the other two - need nothing added. It plays in
/// the piece's own scenes (PC_*, Vizlab_*) and leaves every other scene as it is. Put one in a
/// scene by hand only to tune its levels there. It silences the journey scenes' old "BGM
/// Audio Source". On the Vizlab wall only the master copy plays: four copies of one piece of
/// music a few milliseconds apart is one piece of music with an echo.
///
/// The pieces are made by Assets/Credits/Tools~/journey_score.py, from the same sines and
/// noise as the credits' theme and in its key, which they lead into.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("SCORE", "#E06AA0")]
public class JourneyScore_NEW : MonoBehaviour
{
    [Header("Levels")]
    [Range(0f, 1f)] [SerializeField] float volume = 0.85f;

    [Tooltip("The beds, against the cues.")]
    [Range(0f, 1f)] [SerializeField] float bedVolume = 0.6f;

    [Range(0f, 1f)] [SerializeField] float cueVolume = 0.9f;

    [Tooltip("The beds while a voice-over line is being said, as a fraction of their level.")]
    [Range(0f, 1f)] [SerializeField] float underVoice = 0.5f;

    [Tooltip("The beds while a cue is rising to its climax, as a fraction of their level.")]
    [Range(0f, 1f)] [SerializeField] float underCue = 0.45f;

    [Header("The scenes' own music")]
    [Tooltip("Stop the journey scenes' old background music, which this replaces.")]
    [SerializeField] bool silenceSceneBgm = true;

    [SerializeField] string sceneBgmName = "BGM Audio Source";

    [SerializeField] bool debugLog = false;

    const string Folder = "Score/";
    static JourneyScore_NEW _instance;

    readonly AudioSource[] _beds = new AudioSource[2];
    readonly float[] _bedLevel = new float[2], _bedTarget = new float[2], _bedRate = new float[2];
    int _bed;                     // which of the two is the bed now
    string _bedName;              // and what it plays; null for silence
    readonly List<AudioSource> _cues = new List<AudioSource>();
    readonly List<AudioSource> _voices = new List<AudioSource>();
    readonly HashSet<string> _missing = new HashSet<string>();

    float _clock, _duckUntil, _switchAt = -1f, _switchFade = 3f, _duck = 1f;
    string _pendingBed;
    bool _silent = true, _muted, _domeDone, _cutDone;
    LayerState_NEW _layers;
    TutorialDirector_NEW _tutorial;
    EarthSummit_NEW _summit;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (_instance != null) return;
        if (FindObjectOfType<JourneyScore_NEW>() == null) new GameObject("Journey Score").AddComponent<JourneyScore_NEW>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);   // one across every scene; a second, in a scene loaded later, gives way
            return;
        }
        _instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        for (int i = 0; i < 2; i++) _beds[i] = Source("Bed " + i, true);
        for (int i = 0; i < 4; i++) _cues.Add(Source("Cue " + i, false));

        SceneManager.sceneLoaded += OnSceneLoaded;
        LayerDive_NEW.DiveBegan += OnDive;
        EarthZoom_NEW.ZoomBegan += OnEarthZoom;
        EnterScene(SceneManager.GetActiveScene());
    }

    void OnDestroy()
    {
        if (_instance != this) return;
        _instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        LayerDive_NEW.DiveBegan -= OnDive;
        EarthZoom_NEW.ZoomBegan -= OnEarthZoom;
        Leave();
    }

    AudioSource Source(string label, bool loop)
    {
        var go = new GameObject(label);
        go.transform.SetParent(transform, false);
        AudioSource a = go.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.loop = loop;
        a.spatialBlend = 0f;
        a.volume = 0f;
        return a;
    }

    // ── Scenes ──────────────────────────────────────────────────────────────

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) EnterScene(scene);
    }

    void Leave()
    {
        if (_layers != null) _layers.OnLayerChanged -= OnLayer;
        if (_tutorial != null) _tutorial.OnTutorialComplete -= OnTutorialComplete;
        _layers = null;
        _tutorial = null;
        _summit = null;
        _voices.Clear();
    }

    /// <summary>What this scene is, by what is in it: the tutorial, the journey, or somewhere the score is quiet.</summary>
    void EnterScene(Scene scene)
    {
        Leave();
        _pendingBed = null;
        _switchAt = -1f;
        _domeDone = _cutDone = false;

        // On the wall, the master's copy alone.
        _muted = GetReal3DRuntime_NEW.LaunchedByGetReal3D && !GetReal3DRuntime_NEW.IsMaster;

        string name = scene.name.ToLowerInvariant();
        _tutorial = FindObjectOfType<TutorialDirector_NEW>();
        _layers = FindObjectOfType<LayerState_NEW>();
        // The piece's own scenes only (PC_*, Vizlab_*): a development scene keeps whatever it has.
        bool ours = name.StartsWith("pc_") || name.StartsWith("vizlab_");
        bool quiet = !ours || name.Contains("cover") || name.Contains("credit");

        foreach (AudioSource a in FindObjectsOfType<AudioSource>())
            if (a.name == "VoiceOver") _voices.Add(a);

        if (!quiet && _tutorial != null)
        {
            _silent = false;
            _tutorial.OnTutorialComplete += OnTutorialComplete;
            SetBed("Tutorial", 3f);
        }
        else if (!quiet && _layers != null)
        {
            _silent = false;
            _layers.OnLayerChanged += OnLayer;
            _summit = FindObjectOfType<EarthSummit_NEW>();
            if (silenceSceneBgm) SilenceSceneBgm();
            if (_layers.CurrentLayerId != null) SetBed(_layers.CurrentLayerId, 4f);
        }
        else
        {
            // The cover, the credits, anything else: out, cues and all.
            _silent = true;
            SetBed(null, 1.5f);
        }
        if (debugLog) Debug.Log("[JourneyScore_NEW] " + scene.name + ": " + (_silent ? "quiet" : _tutorial != null ? "tutorial" : "journey") +
                                (_muted ? " (not the master: muted)" : ""), this);
    }

    void SilenceSceneBgm()
    {
        GameObject old = GameObject.Find(sceneBgmName);
        AudioSource a = old != null ? old.GetComponent<AudioSource>() : null;
        if (a == null) return;
        a.Stop();
        a.enabled = false;
    }

    // ── What happens ────────────────────────────────────────────────────────

    void OnTutorialComplete()
    {
        // The camera pulls out, the frame goes to black, the title comes up: the cue carries it,
        // and the bed goes out under it, so the journey opens out of quiet.
        PlayCue("Outro", 5f);
        SetBed(null, 7f);
    }

    void OnLayer(LayerProfile_NEW previous, LayerProfile_NEW current)
    {
        if (current == null) return;
        // The new place's bed: in a moment - or, if a dive begins for it, at the dive's peak.
        _pendingBed = current.layerId;
        _switchAt = _clock + 0.25f;
        _switchFade = previous == null ? 4f : 3f;

        if (previous != null && current.layerId == "Earth") PlayCue("EarthArrive", 4f);
    }

    void OnDive(LayerDive_NEW.Dive dive)
    {
        if (_silent || dive == null) return;
        PlayCue("Dive_" + dive.toLayerId, dive.diveSeconds + 1f);
        _pendingBed = dive.toLayerId;
        _switchAt = _clock + dive.diveSeconds;
        _switchFade = Mathf.Max(2f, dive.emergeSeconds);
    }

    void OnEarthZoom()
    {
        if (!_silent) PlayCue("EarthApproach", 0f);
    }

    void Update()
    {
        float dt = Time.deltaTime;   // the game's clock, as the dives': a pause holds the score's place too
        _clock += dt;

        if (_switchAt >= 0f && _clock >= _switchAt)
        {
            _switchAt = -1f;
            if (!_silent) SetBed(_pendingBed, _switchFade);
        }

        if (_summit != null)
        {
            if (!_domeDone && _summit.Entered)
            {
                _domeDone = true;
                PlayCue("Dome", 0f);
            }
            if (!_cutDone && _summit.CutToOutside)
            {
                _cutDone = true;
                PlayCue("Spectrum", 0f);
                SetBed(null, 5f);
            }
        }

        // The beds: each to its target; all of them down under the voice and under a cue's rise.
        bool voice = false;
        foreach (AudioSource v in _voices)
            if (v != null && v.isPlaying) { voice = true; break; }
        float duck = (voice ? underVoice : 1f) * (_clock < _duckUntil ? underCue : 1f);
        float real = Time.unscaledDeltaTime;
        _duck = Mathf.MoveTowards(_duck, duck, real / (duck < _duck ? 0.6f : 1.8f));

        float master = _muted ? 0f : volume;
        for (int i = 0; i < 2; i++)
        {
            _bedLevel[i] = Mathf.MoveTowards(_bedLevel[i], _bedTarget[i], _bedRate[i] * real);
            // Equal power, so two beds crossing do not dip.
            _beds[i].volume = master * bedVolume * _duck * Mathf.Sin(_bedLevel[i] * Mathf.PI * 0.5f);
            if (_bedLevel[i] <= 0f && _bedTarget[i] <= 0f && _beds[i].isPlaying) _beds[i].Stop();
        }

        // Cues: at their level; when the score goes quiet they are let down with it.
        foreach (AudioSource c in _cues)
        {
            if (!c.isPlaying) continue;
            c.volume = _silent ? Mathf.MoveTowards(c.volume, 0f, real / 1.5f) : master * cueVolume;
            if (_silent && c.volume <= 0f) c.Stop();
        }
    }

    // ── Playing ─────────────────────────────────────────────────────────────

    AudioClip Clip(string name)
    {
        AudioClip clip = Resources.Load<AudioClip>(Folder + name);
        if (clip == null && _missing.Add(name))
            Debug.LogWarning("[JourneyScore_NEW] No Resources/" + Folder + name + "; that part of the score is silent.", this);
        return clip;
    }

    /// <summary>The bed for <paramref name="name"/> (a layer id, or Tutorial) in over <paramref name="seconds"/>, the one playing out; null for none.</summary>
    void SetBed(string name, float seconds)
    {
        if (name == _bedName) return;
        _bedName = name;
        float rate = 1f / Mathf.Max(0.05f, seconds);

        _bedTarget[_bed] = 0f;
        _bedRate[_bed] = rate;
        if (name == null) return;

        AudioClip clip = Clip("Bed_" + name);
        if (clip == null) return;
        _bed = 1 - _bed;
        AudioSource a = _beds[_bed];
        a.Stop();
        a.clip = clip;
        a.volume = 0f;
        a.Play();
        _bedLevel[_bed] = 0f;
        _bedTarget[_bed] = 1f;
        _bedRate[_bed] = rate;
        if (debugLog) Debug.Log("[JourneyScore_NEW] bed: " + name + " over " + seconds + " s", this);
    }

    /// <summary>A cue, now; the beds held down under it for <paramref name="rise"/> seconds.</summary>
    void PlayCue(string name, float rise)
    {
        AudioClip clip = Clip("Cue_" + name);
        if (clip == null) return;

        AudioSource free = _cues[0];
        foreach (AudioSource c in _cues)
            if (!c.isPlaying) { free = c; break; }
        free.Stop();
        free.clip = clip;
        free.volume = _muted ? 0f : volume * cueVolume;
        free.Play();
        if (rise > 0f) _duckUntil = Mathf.Max(_duckUntil, _clock + rise);
        if (debugLog) Debug.Log("[JourneyScore_NEW] cue: " + name, this);
    }
}

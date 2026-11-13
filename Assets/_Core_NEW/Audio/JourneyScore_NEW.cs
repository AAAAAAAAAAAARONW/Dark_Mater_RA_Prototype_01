using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The music from the tutorial to the end of the journey: one piece that swells and settles
/// with what happens, not a track with effects laid over it.
///
/// WHY NOT ONE TRACK. How long each layer takes is the visitor's and the voice-over's, so a
/// fixed track's climaxes slide off the picture. And a sound effect dropped on each transition
/// is not music either. So the music is in layers that play together, and what happens moves
/// between them (Resources/Score):
///
///   Bed_<place>    the place's music at rest: a 48 s loop, held harmony, a few bells
///   Lift_<place>   the same bars, the same chords, risen: the octave above, a low octave, the
///                  chord running in bells. It plays in step with the bed, silent until wanted
///   Crest_<place>  the phrase the music arrives on: the place's chord struck full and its
///                  bells running up - played once, as its climax
///
/// HOW IT RISES. One number, the lift, 0 to 1, is how much of the Lift layer is heard. It
/// rises towards a gate as the light nears it (from the light's own speed: the last
/// approachSeconds before it), goes to full over a dive, and at the dive's peak - when the new
/// world is first seen - the music changes to the new place, risen, on its Crest; then it
/// settles. So every transition is the music's own climax, and it lands on the picture's
/// because the picture's own events move it:
///
///   tutorial     the light nearing the quasar's core lifts it; EMITTING THE LIGHT (C2) is the
///                crest; each beat met is a small swell; the ending rises through the pull-out
///                to a crest as the title comes up
///   dives        LayerDive_NEW.DiveBegan: full by diveSeconds, the new place and its crest at
///                the peak (into the Solar System, as its star flares)
///   Earth        rising as Earth grows (EarthZoom_NEW.ZoomBegan), the crest as the light goes
///                in, a last one on the cut to the observatory and the spectrum
///
/// The music sits under the voice (underVoice while a VoiceOver source plays). It stops for
/// the cover and the credits, which have their own.
///
/// NOTHING TO SET UP. It makes itself when the game starts and lives across scene loads, so
/// the scenes - four of them, two made from the other two - need nothing added. It plays in
/// the piece's own scenes (PC_*, Vizlab_*) and leaves every other scene as it is. Put one in a
/// scene by hand only to tune its levels there. It silences the journey scenes' old "BGM
/// Audio Source". On the Vizlab wall only the master copy plays: four copies of one piece of
/// music a few milliseconds apart is one piece of music with an echo.
///
/// The layers are made by Assets/Credits/Tools~/journey_score.py, from the same sines as the
/// credits' theme and in its key, which they lead into. There is no noise in them: no
/// whoosh, no boom.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("SCORE", "#E06AA0")]
public class JourneyScore_NEW : MonoBehaviour
{
    [Header("Levels")]
    [Range(0f, 1f)] [SerializeField] float volume = 0.85f;

    [Tooltip("The music at rest.")]
    [Range(0f, 1f)] [SerializeField] float bedVolume = 0.6f;

    [Tooltip("The risen layer, at full lift.")]
    [Range(0f, 1f)] [SerializeField] float liftVolume = 0.75f;

    [Tooltip("The phrase a climax arrives on.")]
    [Range(0f, 1f)] [SerializeField] float crestVolume = 0.7f;

    [Tooltip("All of it while a voice-over line is being said, as a fraction of its level.")]
    [Range(0f, 1f)] [SerializeField] float underVoice = 0.55f;

    [Header("Shape")]
    [Tooltip("How much lift the music keeps when nothing is happening.")]
    [Range(0f, 0.5f)] [SerializeField] float restLift = 0.1f;

    [Tooltip("The music starts to rise this many seconds before the light reaches a gate.")]
    [Min(0f)] [SerializeField] float approachSeconds = 12f;

    [Tooltip("How far it has risen by the gate, before the dive takes it the rest of the way.")]
    [Range(0f, 1f)] [SerializeField] float approachLift = 0.55f;

    [Tooltip("Seconds to settle after a climax.")]
    [Min(1f)] [SerializeField] float settleSeconds = 12f;

    [Header("The scenes' own music")]
    [Tooltip("Stop the journey scenes' old background music, which this replaces.")]
    [SerializeField] bool silenceSceneBgm = true;

    [SerializeField] string sceneBgmName = "BGM Audio Source";

    [SerializeField] bool debugLog = false;

    const string Folder = "Score/";
    static JourneyScore_NEW _instance;

    /// <summary>A place's two layers, started on the same sample so they stay in step.</summary>
    class Slot
    {
        public AudioSource bed, lift;
        public float level, target, rate;
    }

    readonly Slot[] _slots = new Slot[2];
    int _slot;                    // which of the two is the place now
    string _place;                // and what it plays; null for silence
    readonly List<AudioSource> _crests = new List<AudioSource>();
    readonly List<AudioSource> _voices = new List<AudioSource>();
    readonly HashSet<string> _missing = new HashSet<string>();

    // The lift: an envelope the events set, and the rise towards a gate, whichever is higher.
    float _lift, _env, _envTarget, _envRate = 1f, _envHoldUntil;
    float _clock, _duck = 1f;

    // Things due at a time on the game's clock.
    float _placeAt = -1f, _placeFade = 2f, _crestAt = -1f;
    string _pendingPlace, _pendingCrest;

    bool _silent = true, _muted, _domeDone, _cutDone;
    LayerState_NEW _layers;
    TutorialDirector_NEW _tutorial;
    EarthSummit_NEW _summit;
    Transform _player;
    Vector3 _playerWas;
    bool _havePlayerWas;
    LayerGate_NEW[] _gates = new LayerGate_NEW[0];

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

        for (int i = 0; i < 2; i++)
            _slots[i] = new Slot { bed = Source("Bed " + i, true), lift = Source("Lift " + i, true) };
        for (int i = 0; i < 3; i++) _crests.Add(Source("Crest " + i, false));

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
        if (_tutorial != null)
        {
            _tutorial.OnTutorialComplete -= OnTutorialComplete;
            _tutorial.OnBeatEntered -= OnBeatEntered;
            _tutorial.OnBeatSatisfied -= OnBeatSatisfied;
        }
        _layers = null;
        _tutorial = null;
        _summit = null;
        _player = null;
        _havePlayerWas = false;
        _gates = new LayerGate_NEW[0];
        _voices.Clear();
    }

    /// <summary>What this scene is, by what is in it: the tutorial, the journey, or somewhere the score is quiet.</summary>
    void EnterScene(Scene scene)
    {
        Leave();
        _pendingPlace = _pendingCrest = null;
        _placeAt = _crestAt = -1f;
        _domeDone = _cutDone = false;
        _env = _envTarget = restLift;

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
            _tutorial.OnBeatEntered += OnBeatEntered;
            _tutorial.OnBeatSatisfied += OnBeatSatisfied;
            SetPlace("Tutorial", 3f);
        }
        else if (!quiet && _layers != null)
        {
            _silent = false;
            _layers.OnLayerChanged += OnLayer;
            _summit = FindObjectOfType<EarthSummit_NEW>();
            _gates = FindObjectsOfType<LayerGate_NEW>();
            GameObject player = GameObject.FindWithTag("Player");
            _player = player != null ? player.transform : null;
            if (silenceSceneBgm) SilenceSceneBgm();
            if (_layers.CurrentLayerId != null) SetPlace(_layers.CurrentLayerId, 4f);
        }
        else
        {
            // The cover, the credits, anything else: out, all of it.
            _silent = true;
            SetPlace(null, 1.5f);
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

    /// <summary>The lift to <paramref name="to"/> over <paramref name="seconds"/>, held there for <paramref name="hold"/>, then left to settle.</summary>
    void Rise(float to, float seconds, float hold)
    {
        if (to < _env && _clock < _envHoldUntil) return;     // a smaller swell does not pull a bigger one down
        _envTarget = to;
        _envRate = Mathf.Abs(to - _env) / Mathf.Max(0.05f, seconds);
        _envHoldUntil = _clock + seconds + hold;
    }

    void OnBeatEntered(TutorialBeat_NEW beat)
    {
        if (beat == null) return;
        // The light closing on the quasar's core, and waiting there to be emitted: the music gathers.
        if (beat.BeatId == "C1") Rise(0.5f, 6f, 60f);
        else if (beat.BeatId == "C2") Rise(0.8f, 4f, 120f);
    }

    void OnBeatSatisfied(TutorialBeat_NEW beat)
    {
        if (beat == null) return;
        if (beat.BeatId == "C2")
        {
            // The light is emitted: the tutorial's climax.
            Rise(1f, 0.4f, 3f);
            Crest("Emit", 0f);
        }
        else if (_env < 0.45f) Rise(Mathf.Min(0.45f, _env + 0.25f), 1.2f, 1.5f);   // a beat met: a small swell
    }

    void OnTutorialComplete()
    {
        // The camera pulls out, the frame goes to black, the title comes up on the crest; then it
        // all goes out, so the journey opens out of quiet.
        Rise(1f, 5f, 6f);
        Crest("Welcome", 9f);
        _pendingPlace = null;
        _placeAt = _clock + 11f;
        _placeFade = 7f;
    }

    void OnLayer(LayerProfile_NEW previous, LayerProfile_NEW current)
    {
        if (current == null) return;
        // The new place: in a moment - or, if a dive begins for it, at the dive's peak.
        _pendingPlace = current.layerId;
        _placeAt = _clock + 0.25f;
        _placeFade = previous == null ? 4f : 2f;

        if (previous != null && current.layerId == "Earth")
        {
            // The light goes into Earth: the crest, and the long settling of the fall.
            Rise(1f, 0.3f, 4f);
            Crest("Earth", 0.25f);
        }
    }

    void OnDive(LayerDive_NEW.Dive dive)
    {
        if (_silent || dive == null) return;
        // Full by the peak; there the new place, risen, on its crest. Into the Solar System the
        // crest waits for the star's flare, a moment after.
        float peak = dive.diveSeconds;
        float crest = peak + (dive.continuous ? dive.peakHoldSeconds + 0.7f : 0f);
        Rise(1f, peak, (crest - peak) + dive.emergeSeconds * 0.5f);
        _pendingPlace = dive.toLayerId;
        _placeAt = _clock + peak;
        _placeFade = 1.5f;
        Crest(dive.toLayerId, crest);
    }

    void OnEarthZoom()
    {
        if (!_silent) Rise(0.95f, 16f, 30f);   // Earth grows to fill the screen; held until the light goes in
    }

    /// <summary>The crest phrase for <paramref name="name"/>, <paramref name="after"/> seconds from now on the game's clock.</summary>
    void Crest(string name, float after)
    {
        if (after <= 0f) { PlayCrest(name); return; }
        _pendingCrest = name;
        _crestAt = _clock + after;
    }

    /// <summary>How risen the music is for the gate ahead: nothing until approachSeconds before it, approachLift at it.</summary>
    float Approach(float dt)
    {
        if (_player == null || approachSeconds <= 0f || dt <= 0f) return 0f;
        Vector3 at = _player.position;
        Vector3 moved = _havePlayerWas ? at - _playerWas : Vector3.zero;
        _playerWas = at;
        _havePlayerWas = true;
        float speed = moved.magnitude / dt;
        if (speed < 1e-3f) return 0f;
        Vector3 way = moved.normalized;

        float nearest = float.MaxValue;
        string here = _layers != null ? _layers.CurrentLayerId : null;
        foreach (LayerGate_NEW gate in _gates)
        {
            if (gate == null || !gate.isActiveAndEnabled || gate.LayerId == here) continue;
            float ahead = Vector3.Dot(gate.transform.position - at, way);
            if (ahead > 0f && ahead < nearest) nearest = ahead;
        }
        if (nearest == float.MaxValue) return 0f;
        float k = Mathf.Clamp01(1f - nearest / speed / approachSeconds);
        return approachLift * k * k * (3f - 2f * k);
    }

    void Update()
    {
        float dt = Time.deltaTime;   // the game's clock, as the dives': a pause holds the score's place too
        float real = Time.unscaledDeltaTime;
        _clock += dt;

        if (_placeAt >= 0f && _clock >= _placeAt)
        {
            _placeAt = -1f;
            if (!_silent) SetPlace(_pendingPlace, _placeFade);
        }
        if (_crestAt >= 0f && _clock >= _crestAt)
        {
            _crestAt = -1f;
            if (!_silent) PlayCrest(_pendingCrest);
        }

        if (_summit != null)
        {
            if (!_domeDone && _summit.Entered)
            {
                _domeDone = true;
                Rise(0.6f, 1.5f, 2f);              // into the dome: the music leans in
            }
            if (!_cutDone && _summit.CutToOutside)
            {
                _cutDone = true;
                Rise(1f, 2.5f, 8f);                // the observatory, the spectrum: home
                Crest("Home", 2.5f);
            }
        }

        // The lift: the events' envelope, settling when its hold is over; or the rise to a gate.
        if (_clock >= _envHoldUntil && _envTarget != restLift)
        {
            _envRate = Mathf.Abs(_env - restLift) / Mathf.Max(1f, settleSeconds);
            _envTarget = restLift;
        }
        _env = Mathf.MoveTowards(_env, _envTarget, _envRate * dt);
        float want = Mathf.Max(_env, _silent ? 0f : Approach(dt));
        _lift = Mathf.MoveTowards(_lift, want, real * 0.6f);

        // Under the voice.
        bool voice = false;
        foreach (AudioSource v in _voices)
            if (v != null && v.isPlaying) { voice = true; break; }
        float duck = voice ? underVoice : 1f;
        _duck = Mathf.MoveTowards(_duck, duck, real / (duck < _duck ? 0.6f : 1.8f));

        float master = _muted ? 0f : volume * _duck;
        float risen = Mathf.Sin(_lift * Mathf.PI * 0.5f);
        foreach (Slot s in _slots)
        {
            s.level = Mathf.MoveTowards(s.level, s.target, s.rate * real);
            float fade = Mathf.Sin(s.level * Mathf.PI * 0.5f);   // equal power, so two places crossing do not dip
            s.bed.volume = master * bedVolume * fade;
            s.lift.volume = master * liftVolume * fade * risen;
            if (s.level <= 0f && s.target <= 0f && s.bed.isPlaying)
            {
                s.bed.Stop();
                s.lift.Stop();
            }
        }

        foreach (AudioSource c in _crests)
        {
            if (!c.isPlaying) continue;
            c.volume = _silent ? Mathf.MoveTowards(c.volume, 0f, real / 1.5f) : master * crestVolume;
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

    /// <summary>The music of <paramref name="name"/> (a layer id, or Tutorial) in over <paramref name="seconds"/>, the one playing out; null for none.</summary>
    void SetPlace(string name, float seconds)
    {
        if (name == _place) return;
        _place = name;
        float rate = 1f / Mathf.Max(0.05f, seconds);

        _slots[_slot].target = 0f;
        _slots[_slot].rate = rate;
        if (name == null) return;

        AudioClip bed = Clip("Bed_" + name), lift = Clip("Lift_" + name);
        if (bed == null) return;
        _slot = 1 - _slot;
        Slot s = _slots[_slot];
        s.bed.Stop();
        s.lift.Stop();
        s.bed.clip = bed;
        s.lift.clip = lift;
        s.bed.volume = s.lift.volume = 0f;
        // Both on the same sample, a moment from now, so the risen layer stays on the bed's bars.
        double at = AudioSettings.dspTime + 0.1;
        s.bed.PlayScheduled(at);
        if (lift != null) s.lift.PlayScheduled(at);
        s.level = 0f;
        s.target = 1f;
        s.rate = rate;
        if (debugLog) Debug.Log("[JourneyScore_NEW] place: " + name + " over " + seconds + " s", this);
    }

    void PlayCrest(string name)
    {
        AudioClip clip = Clip("Crest_" + name);
        if (clip == null) return;
        AudioSource free = _crests[0];
        foreach (AudioSource c in _crests)
            if (!c.isPlaying) { free = c; break; }
        free.Stop();
        free.clip = clip;
        free.volume = _muted ? 0f : volume * _duck * crestVolume;
        free.Play();
        if (debugLog) Debug.Log("[JourneyScore_NEW] crest: " + name, this);
    }
}

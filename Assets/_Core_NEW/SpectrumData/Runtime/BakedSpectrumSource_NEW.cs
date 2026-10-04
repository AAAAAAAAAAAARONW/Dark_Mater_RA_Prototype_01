using System;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// THE SWITCH for the baked journey spectrum. Feeds the spectrum HUD, the photon trail's
/// absorption and the redshift marks from a precomputed file instead of the procedural
/// forest.
///
/// OFF (or not in the scene) means none of this runs: nothing is loaded, nothing is
/// overridden, and the HUD, the absorption field and the marks behave exactly as they
/// did before this folder existed. Turning it off at runtime hands everything back on
/// the same frame. That is the whole isolation story: the existing components expose
/// hooks and never name this class, so this folder can be switched off, or deleted,
/// without touching anything else.
///
/// PERFORMANCE RULES, because the last spectrum-data path cost frame rate:
///
///   * All loading happens once, in Start, from a TextAsset already in memory. No file
///     I/O, no parsing, no Resources.Load after that. The load time is logged.
///   * Per frame: one lookup (light-years left → z → row) and, only if the row moved
///     by more than rowChangeThreshold, a blend of two rows into arrays allocated once.
///     Most frames do nothing at all — the journey moves slowly through 384 rows.
///   * Nothing allocates per frame. The mapping handed to RedshiftMarks_NEW is created
///     once.
///   * If the file is missing or bad, it logs one error and switches itself off.
/// </summary>
[DefaultExecutionOrder(50)]
[DisallowMultipleComponent]
[HierarchyBadge_NEW("SPEC DATA", "#E2506A")]
public class BakedSpectrumSource_NEW : MonoBehaviour
{
    [Header("Switch")]
    [Tooltip("THE KILL SWITCH. Off = the baked spectrum is not loaded and nothing is " +
             "overridden; the procedural forest runs exactly as before.\n\n" +
             "Can be flipped while playing: off releases every override on the next " +
             "frame, on reloads from the file.")]
    [SerializeField] bool useBakedData = true;

    [Header("Data")]
    [Tooltip("The baked file: SpectrumData/Baked/SpectrumJourney.bytes. Produced by " +
             "Tools > Journey NEW > Spectrum Data > Bake Journey Spectrum.")]
    [SerializeField] TextAsset bakedFile;

    /// <summary>What the HUD curve shows.</summary>
    public enum HudCurve
    {
        /// <summary>How much of the light survived at each wavelength, 0..1. Absorption only, no quasar shape.</summary>
        Transmission = 0,

        /// <summary>The quasar's own spectrum with the displayed (Ly-alpha) absorption cut into it. The default.</summary>
        Flux = 1
    }

    [Header("What it drives (each can be turned off on its own)")]
    [SerializeField] SpectrumHUD_NEW hud;
    [SerializeField] bool driveHud = true;

    [Tooltip("FLUX is the design and the default: the quasar's own spectrum — its " +
             "Ly-alpha peak, emission lines and continuum — with ONLY the Ly-alpha forest " +
             "cut into it. The peak starts at the left edge and walks right with redshift, " +
             "its shape on its right and the forest growing on its left. It is the shape of " +
             "the classic forest figure the whole way, so the arrival zoom only reframes it.\n\n" +
             "TRANSMISSION divides the quasar's shape out and shows absorption alone — flat " +
             "wherever nothing was absorbed. How astronomers analyse the forest, but the " +
             "peak and emission lines are gone. Read once, when the data is activated.")]
    // Renamed from hudCurve on purpose, WITHOUT FormerlySerializedAs. The old field defaulted to
    // Transmission and that value was saved into Gameplay_Scene_Ana; a new code default never
    // reaches a component that already exists. A new name drops the stale saved value so the
    // current default (Flux) applies. Anyone who wants Transmission sets it here explicitly.
    [SerializeField] HudCurve hudDisplay = HudCurve.Flux;

    [SerializeField] AbsorptionField_NEW field;
    [Tooltip("Feed the photon trail's absorption texture. With trailRealWavelengths on (below), " +
             "the trail is also coloured by real wavelength, so each line sits under its own colour.")]
    [SerializeField] bool driveTrail = true;

    [Tooltip("Put the trail on real wavelengths, like the tutorial's: a far-UV margin (1000 Å up to " +
             "the bar's left edge) on the ribbon's outer side, then exactly the bar's axis, coloured by " +
             "real wavelength — UV below 4000 Å, the rainbow 4000–7000 Å, IR above.\n\n" +
             "Without the margin a just-born line sits on the ribbon's outer edge, where the trail " +
             "shader fades everything out; without real colours a line sits under a colour that is " +
             "not its wavelength. Off = the trail is fed the bar's array as before, art colours kept.")]
    [SerializeField] bool trailRealWavelengths = true;

    [Range(0f, 0.4f)]
    [SerializeField] float trailFarUvShare = 0.2f;

    [Tooltip("Opacity of the UV part of the ribbon (most of the journey's lines are UV).")]
    [Range(0f, 1f)]
    [SerializeField] float trailUvAlpha = 0.65f;

    [Tooltip("How wide a highlighted line is cut on the trail, as a fraction of the bar either side.")]
    [Range(0.001f, 0.03f)]
    [SerializeField] float emphasisHalfWidth = 0.005f;

    [Tooltip("The journey's photon trail (its colours are read for the UV and IR bands). Found if empty.")]
    [SerializeField] PhotonSpectrumTrail spectrumTrail;

    [SerializeField] RedshiftMarks_NEW marks;
    [SerializeField] bool driveMarks = true;

    [Header("Journey position")]
    [Tooltip("Where 'how far along the journey' comes from. Found in the scene if empty. " +
             "Its remaining light-years are read as lookback time.")]
    [SerializeField] UniverseJourneyTracker tracker;

    [Tooltip("Skip the update unless the row moved at least this much. 0.02 of a row is " +
             "well under a pixel of movement on the bar.")]
    [Range(0f, 1f)]
    [SerializeField] float rowChangeThreshold = 0.02f;

    [Header("Arrival — zoom into the reference figure at Earth")]
    [Tooltip("At Earth, zoom the bar from the whole journey's barcode into the classic " +
             "Lyman-alpha forest figure (emitted 1000–1350 Å), then let the quasar's own " +
             "spectrum rise into it — the Ly-alpha peak with the forest to its left.\n\n" +
             "It is a continuous zoom, not a cut: at Earth the light's current wavelength IS " +
             "the observed wavelength, so the same curve is simply shown over a narrower " +
             "range. Off = the bar ends on the barcode.")]
    [SerializeField] bool arrivalEnabled = true;

    [Tooltip("Entering this layer starts the arrival, after arrivalDelaySeconds. " +
             "Found in the scene if empty.")]
    [SerializeField] LayerState_NEW layerState;
    [SerializeField] string arrivalLayerId = "Earth";

    [Tooltip("Seconds after entering the arrival layer before the zoom begins. Time it to " +
             "the Earth VO: \"…astronomers read the spectral lines you carried…\".")]
    [Min(0f)]
    [SerializeField] float arrivalDelaySeconds = 4f;

    [Tooltip("Seconds for the bar to zoom from the barcode to the reference window. The " +
             "curve stays absorption-only while it zooms.")]
    [Min(0.1f)]
    [SerializeField] float zoomSeconds = 5f;

    [Tooltip("Seconds for the curve to change from absorption-only into the real spectrum, " +
             "once framed. This is where the Ly-alpha peak rises.")]
    [Min(0.1f)]
    [SerializeField] float revealSeconds = 3f;

    [Tooltip("Fired once the reference frame is fully shown.")]
    [SerializeField] UnityEngine.Events.UnityEvent onArrivalComplete = new UnityEngine.Events.UnityEvent();

    [Tooltip("Play the arrival now, wherever the journey is. Editor and development builds only.")]
    [SerializeField] KeyCode debugArrivalKey = KeyCode.F9;

    [Tooltip("Replay the light's history now (what the demo's Spread step does). Editor and " +
             "development builds only.")]
    [SerializeField] KeyCode debugReplayKey = KeyCode.F10;

    [Tooltip("Show / hide a live readout of where the spectrum thinks the light is: " +
             "lookback, redshift, data row, peak position, state. The first thing to look " +
             "at when the HUD seems not to move. Editor and development builds only.")]
    [SerializeField] KeyCode debugReadoutKey = KeyCode.F11;

    [Header("Debug")]
    [Tooltip("Force the journey position, in Gyr of light-travel time still to go. " +
             "Negative = off, read the tracker. The bake's lookback_start_gyr (12.07) is the moment of emission, 0 is Earth. " +
             "Lets any point of the journey be inspected without flying there.")]
    [SerializeField] float overrideLookbackGyr = -1f;

    [SerializeField] bool logLoad = true;

    BakedSpectrumData_NEW _data;
    float[] _flux;
    float[] _absorption;
    float[] _hudCurve;
    HudCurve _activeHudCurve;
    float _lastRow = float.NaN;
    float _zNow;       // where the light really is
    float _zDisplay;   // what is being drawn: _zNow, except during a replay
    bool _active;
    bool _failed;
    bool _warnedNoTracker;
    Func<float, float> _lineT;
    Action<float> _replay;

    bool _replaying;
    float _replayTime;
    float _replaySeconds;

    bool _showReadout;
    float _holdLookback = -1f;   // from the tutorial: hold here until the tracker passes it
    float[] _trailAbsorption;
    float[] _marksZ = new float[0];
    float _emphT = float.NaN;
    float _emphK;
    bool _trailDirty;
    TrailRenderer _trailRenderer;
    MaterialPropertyBlock _trailBlock;
    Texture2D _trailTex;
    Color32[] _trailPixels;
    float _lastPushTime = -1f;
    float _lastPushedRow = float.NaN;
    GUIStyle _readoutStyle;

    enum Arrival { Journey, Waiting, Zooming, Revealing, Done }
    Arrival _arrival = Arrival.Journey;
    float _arrivalTime;

    // The axis the HUD is currently drawn on, ln Å. The journey axis until the arrival zoom
    // animates it; LineTNow maps marks through it so they stay on their lines while it moves.
    double _axisLnMin;
    double _axisLnMax;

    // -- Public read-outs ------------------------------------------------------

    /// <summary>True while the baked data is loaded and driving things.</summary>
    public bool IsActive { get { return _active; } }

    /// <summary>The light's current redshift. The quasar's z at emission, 0 at Earth.</summary>
    public float CurrentZ { get { return _zNow; } }

    /// <summary>How many times the light has been stretched: 1 at emission, 1 + z_quasar (about 4.6) at Earth. For the ×stretch readout.</summary>
    public float CurrentStretch { get { return _data != null ? _data.Stretch(_zNow) : 1f; } }

    /// <summary>The loaded data, or null. For editor tools and readouts.</summary>
    public BakedSpectrumData_NEW Data { get { return _data; } }

    /// <summary>Redshifts of the marked lines (the bake's notable absorbers; [0] the player's own when handed over).</summary>
    public System.Collections.Generic.IList<float> MarkZ { get { return _marksZ; } }

    /// <summary>True while the bar is simply following the flight: not holding, replaying or arriving.</summary>
    public bool IsFollowingJourney { get { return _active && _arrival == Arrival.Journey && !_replaying && _holdLookback < 0f; } }

    /// <summary>Where a line from gas at zAbs is on the bar now, 0–1. NaN before the light reaches that gas.</summary>
    public float BarLineT(float zAbs) { return LineTNow(zAbs); }

    /// <summary>The birth tick (1216 Å, where every line is born) on the bar now, 0–1.</summary>
    public float BirthBarT { get { return _active ? AxisT(BakedSpectrumData_NEW.LymanAlphaA) : 0f; } }

    /// <summary>The same line across the TRAIL, 0 (UV edge) – 1 (IR edge).</summary>
    public float TrailLineT(float zAbs)
    {
        float t = LineTNow(zAbs);
        return float.IsNaN(t) ? t : TrailT(t);
    }

    /// <summary>The trail renderer the journey's spectrum is drawn on.</summary>
    public TrailRenderer Trail { get { return _trailRenderer; } }

    /// <summary>
    /// Cut one line solid on the trail — barT is its place on the bar, k 0–1 how strongly.
    /// AbsorberHighlight_NEW blinks a line this way, so only THAT line blinks, not the forest.
    /// </summary>
    public void SetTrailEmphasis(float barT, float k)
    {
        _emphT = barT;
        _emphK = Mathf.Clamp01(k);
        _trailDirty = true;
    }

    public void ClearTrailEmphasis()
    {
        if (float.IsNaN(_emphT)) return;
        _emphT = float.NaN;
        _trailDirty = true;
    }

    /// <summary>Flip the switch from code or a UnityEvent.</summary>
    public void SetUseBakedData(bool on) { useBakedData = on; }

    /// <summary>
    /// Zoom into the reference figure now. Normally started by entering arrivalLayerId;
    /// public so a JourneySequence step or a UnityEvent can drive it instead.
    /// </summary>
    public void PlayArrival()
    {
        if (!_active || _arrival == Arrival.Zooming || _arrival == Arrival.Revealing || _arrival == Arrival.Done) return;

        _arrival = Arrival.Zooming;
        _arrivalTime = 0f;
        if (logLoad) Debug.Log("[BakedSpectrumSource_NEW] Arrival: zooming into the reference frame.", this);
    }

    // -- Lifecycle -----------------------------------------------------------------

    void Awake()
    {
        if (hud == null) hud = FindObjectOfType<SpectrumHUD_NEW>();
        if (field == null) field = FindObjectOfType<AbsorptionField_NEW>();
        if (marks == null) marks = FindObjectOfType<RedshiftMarks_NEW>();
        if (tracker == null) tracker = FindObjectOfType<UniverseJourneyTracker>();
        if (layerState == null) layerState = FindObjectOfType<LayerState_NEW>();

        _lineT = LineTNow;
        _replay = ReplayHistory;
    }

    /// <summary>
    /// Replay the light's whole history, from leaving the quasar to where it is now, over
    /// `seconds`. The HUD, trail and marks all follow: the Ly-alpha peak runs out from the
    /// left edge, lines are born at the birth tick one after another and are stretched to
    /// where they really are. Then it hands back to the live journey.
    ///
    /// This is what the demo's Spread step does with real data — the fake version multiplied
    /// distances by an invented factor; this is the baked physics, fast-forwarded. Wired
    /// through RedshiftMarks_NEW.Spread, so the existing JourneySequence step triggers it.
    /// </summary>
    public void ReplayHistory(float seconds)
    {
        if (!_active || _arrival != Arrival.Journey) return;

        _replaying = true;
        _replayTime = 0f;
        _replaySeconds = Mathf.Max(0.5f, seconds);
        if (logLoad) Debug.Log("[BakedSpectrumSource_NEW] Replaying the light's history over " + _replaySeconds.ToString("0.0") + "s.", this);
    }

    void OnEnable()
    {
        if (layerState != null) layerState.OnLayerChanged += HandleLayerChanged;
    }

    void HandleLayerChanged(LayerProfile_NEW previous, LayerProfile_NEW current)
    {
        if (!arrivalEnabled || !_active || current == null) return;
        if (_arrival != Arrival.Journey) return;
        if (!string.Equals(current.layerId, arrivalLayerId, StringComparison.OrdinalIgnoreCase)) return;

        _arrival = Arrival.Waiting;
        _arrivalTime = 0f;
    }

    void Start()
    {
        if (!useBakedData)
        {
            if (logLoad) Debug.Log("[BakedSpectrumSource_NEW] Switched off — the procedural forest is in use.", this);
            return;
        }

        Activate();
    }

    void OnDisable()
    {
        if (layerState != null) layerState.OnLayerChanged -= HandleLayerChanged;
        Release();
    }

    void Update()
    {
        // The switch is live: follow it without needing a restart.
        if (useBakedData && !_active && !_failed) Activate();
        if (!useBakedData && _active) Release();

        if (!_active) return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugArrivalKey != KeyCode.None && Input.GetKeyDown(debugArrivalKey)) PlayArrival();
        if (debugReplayKey != KeyCode.None && Input.GetKeyDown(debugReplayKey)) DebugReplay();
        if (debugReadoutKey != KeyCode.None && Input.GetKeyDown(debugReadoutKey)) _showReadout = !_showReadout;
#endif

        float lookback = CurrentLookbackGyr();
        _zNow = _data.ZFromLookback(lookback);

        if (_arrival != Arrival.Journey)
        {
            _replaying = false;
            _zDisplay = _zNow;
            TickArrival(Time.deltaTime);
            return;
        }

        if (_replaying)
        {
            TickReplay(Time.deltaTime);
            return;
        }

        _zDisplay = _zNow;

        float row = _data.RowFromZ(_zNow);
        if (!float.IsNaN(_lastRow) && Mathf.Abs(row - _lastRow) < rowChangeThreshold) return;

        _lastRow = row;
        Push(row);
    }

    // -- Activation ------------------------------------------------------------------

    void Activate()
    {
        if (_active) return;

        if (bakedFile == null)
        {
            Fail("no baked file assigned. Bake one with Tools > Journey NEW > Spectrum Data > " +
                 "Bake Journey Spectrum, then drag SpectrumData/Baked/SpectrumJourney.bytes here.");
            return;
        }

        Stopwatch sw = Stopwatch.StartNew();
        string error;
        _data = BakedSpectrumData_NEW.Load(bakedFile.bytes, out error);
        sw.Stop();

        if (_data == null)
        {
            Fail("could not read '" + bakedFile.name + "': " + error);
            return;
        }

        _flux = new float[_data.Columns];
        _absorption = new float[_data.Columns];
        _hudCurve = new float[_data.Columns];
        _activeHudCurve = hudDisplay;
        _lastRow = float.NaN;
        _active = true;
        _arrival = Arrival.Journey;
        _axisLnMin = Math.Log(_data.LambdaMin);
        _axisLnMax = Math.Log(_data.LambdaMax);

        if (driveHud && hud != null) hud.SetExternalCurve(_hudCurve, _hudCurve.Length);
        _trailAbsorption = new float[_data.Columns];
        if (spectrumTrail == null) spectrumTrail = FindObjectOfType<PhotonSpectrumTrail>();
        if (spectrumTrail != null) _trailRenderer = spectrumTrail.GetComponent<TrailRenderer>();
        if (driveTrail && field != null) field.SetExternalAbsorption(_trailAbsorption, _trailAbsorption.Length);
        if (driveTrail && trailRealWavelengths) ApplyTrailColours();
        if (driveMarks && marks != null)
        {
            // The marks come from the data itself — the bake's notable absorbers — so they
            // cannot go stale when the data changes, and nobody fills them by hand. [0] is
            // the first line the light acquired; the rest are the strongest absorber in each
            // equal-stretch redshift slice, i.e. the densest gas crossed in that slice.
            _marksZ = MarksWithTutorialLine();
            marks.SetPhysicalMarks(_marksZ);
            marks.SetPhysicalReplay(_replay);
            marks.SetPhysicalMapping(AxisT(BakedSpectrumData_NEW.LymanAlphaA), _lineT);
        }

        // Fill immediately so the first frame is right, not one frame late.
        TakeHandoff();

        _zNow = _data.ZFromLookback(CurrentLookbackGyr());
        _zDisplay = _zNow;
        _replaying = false;
        _lastRow = _data.RowFromZ(_zNow);
        Push(_lastRow);

        if (logLoad)
        {
            Debug.Log("[BakedSpectrumSource_NEW] Loaded '" + bakedFile.name + "' in " +
                      sw.Elapsed.TotalMilliseconds.ToString("0.0") + " ms: " +
                      _data.Columns + "×" + _data.Rows + ", z_quasar " + _data.ZQuasar.ToString("0.00") +
                      ", forest frozen below z " + _data.ZFreeze.ToString("0.00") +
                      ". HUD shows: " + (_activeHudCurve == HudCurve.Flux ? "quasar spectrum + Ly-alpha forest" : "absorption only (Transmission)") +
                      ". Arrival zoom: " + (arrivalEnabled ? "on entering '" + arrivalLayerId + "' (F9 to play now)" : "off") + ".", this);
        }

        if (overrideLookbackGyr >= 0f)
        {
            // Easy to leave on after testing, and then the HUD silently stops following the
            // journey — it looks frozen. Say so every time it is active.
            Debug.LogWarning("[BakedSpectrumSource_NEW] Override Lookback Gyr is " + overrideLookbackGyr.ToString("0.00") +
                             ": the HUD is PINNED to that moment and will not follow the journey. Set it to -1 " +
                             "for normal play.", this);
        }

        if (tracker != null && Mathf.Abs((float)(tracker.RemainingDistanceLy / 1e9) - _data.LookbackStartGyr) > 0.05f &&
            overrideLookbackGyr < 0f && logLoad)
        {
            // Only meaningful at the very start of a run; a warning, not an error, because
            // a debug jump legitimately starts somewhere else.
            Debug.Log("[BakedSpectrumSource_NEW] Tracker is at " +
                      (tracker.RemainingDistanceLy / 1e9).ToString("0.00") + " Gyr, bake starts at " +
                      _data.LookbackStartGyr.ToString("0.00") + " Gyr. Fine after a jump; if this is " +
                      "the start of a run, re-bake with lookback_start_gyr matching the tracker.", this);
        }
    }

    /// <summary>Hand everything back. Safe to call repeatedly.</summary>
    void Release()
    {
        if (!_active) return;

        if (hud != null && hud.HasExternalCurve) hud.ClearExternalCurve();
        if (field != null && field.HasExternalAbsorption) field.ClearExternalAbsorption();
        if (_trailRenderer != null && _trailBlock != null)
        {
            _trailBlock.Clear();
            _trailRenderer.SetPropertyBlock(_trailBlock);
        }
        _emphT = float.NaN;
        if (marks != null) marks.ClearPhysicalMapping();

        _active = false;
        _data = null;
        _flux = null;
        _absorption = null;
        _hudCurve = null;
        _lastRow = float.NaN;
        _arrival = Arrival.Journey;

        _replaying = false;

        if (logLoad) Debug.Log("[BakedSpectrumSource_NEW] Released — procedural forest restored.", this);
    }

    void Fail(string reason)
    {
        _failed = true;
        _active = false;
        Debug.LogError("[BakedSpectrumSource_NEW] Switched off: " + reason, this);
    }

    // -- Per-frame work ----------------------------------------------------------------

    void Push(float row)
    {
        _lastPushTime = Time.time;
        _lastPushedRow = row;

        bool flux = _activeHudCurve == HudCurve.Flux;
        _data.SampleRow(row, flux ? _flux : null, _absorption);

        int n = _hudCurve.Length;
        if (flux)
        {
            Array.Copy(_flux, _hudCurve, n);
        }
        else
        {
            // Transmission: what survived. The quasar's own emission shape is not in here.
            for (int c = 0; c < n; c++) _hudCurve[c] = 1f - _absorption[c];
        }

        if (driveHud && hud != null) hud.MarkExternalCurveDirty();
        BuildTrail();
        if (driveMarks && marks != null) marks.MarkPhysicalDirty();
    }

    float CurrentLookbackGyr()
    {
        if (overrideLookbackGyr >= 0f) return overrideLookbackGyr;

        if (tracker == null)
        {
            if (!_warnedNoTracker)
            {
                _warnedNoTracker = true;
                Debug.LogWarning("[BakedSpectrumSource_NEW] No UniverseJourneyTracker — holding at " +
                                 "the moment of emission. Set overrideLookbackGyr to inspect other points.", this);
            }

            return _data != null ? _data.LookbackStartGyr : 0f;
        }

        float fromTracker = (float)(tracker.RemainingDistanceLy / 1e9);

        // Coming from the tutorial: its light is already a little way out, and the bar must
        // open where the tutorial's closed. Hold there until the tracker catches up, then let
        // go for good. Lookback only falls, so min() is "whichever is further along".
        if (_holdLookback >= 0f)
        {
            if (fromTracker > _holdLookback) return _holdLookback;
            _holdLookback = -1f;
        }

        return fromTracker;
    }

    float LineTNow(float zAbs)
    {
        if (_data == null) return float.NaN;

        // A cloud the light has not reached yet has not cut its line. NaN is off the bar
        // for RedshiftMarks_NEW (every range check fails), so the mark simply is not drawn
        // until the light crosses that cloud — and then appears at the birth tick.
        if (zAbs < _zDisplay) return float.NaN;

        return AxisT(BakedSpectrumData_NEW.LymanAlphaA * (1.0 + zAbs) / (1.0 + _zDisplay));
    }

    /// <summary>Position of a wavelength on the axis the HUD is drawn on right now.</summary>
    float AxisT(double lambdaA)
    {
        return (float)((Math.Log(lambdaA) - _axisLnMin) / (_axisLnMax - _axisLnMin));
    }

    // -- Arrival ---------------------------------------------------------------------

    /// <summary>
    /// Waiting → Zooming → Revealing → Done.
    ///
    ///   Zooming    the axis moves, in ln Å, from the journey bar (1216 Å to the redshift
    ///              edge) to the reference window (emitted 1000–1350 Å). The curve is the
    ///              arrival frame's Ly-alpha transmission — the same thing the journey bar
    ///              showed — so nothing changes but the framing.
    ///   Revealing  the axis holds; the curve blends into the arrival frame's real
    ///              spectrum. The quasar's own Ly-alpha peak rises, the forest settles onto
    ///              its continuum. The result is the classic figure.
    ///
    /// Per frame this samples the arrival frame once per HUD column — about 20 µs.
    /// </summary>
    void TickArrival(float dt)
    {
        _arrivalTime += dt;

        if (_arrival == Arrival.Waiting)
        {
            if (_arrivalTime < arrivalDelaySeconds) return;
            _arrival = Arrival.Zooming;
            _arrivalTime = 0f;
        }

        if (_arrival == Arrival.Done) return;

        double lnJ0 = Math.Log(_data.LambdaMin), lnJ1 = Math.Log(_data.LambdaMax);
        double lnW0 = Math.Log(_data.FinalWindowMin), lnW1 = Math.Log(_data.FinalWindowMax);

        float zoomK, revealK;
        if (_arrival == Arrival.Zooming)
        {
            zoomK = Smooth(_arrivalTime / zoomSeconds);
            revealK = 0f;
            if (_arrivalTime >= zoomSeconds) { _arrival = Arrival.Revealing; _arrivalTime = 0f; }
        }
        else
        {
            zoomK = 1f;
            revealK = Smooth(_arrivalTime / revealSeconds);
            if (_arrivalTime >= revealSeconds) _arrival = Arrival.Done;
        }

        _axisLnMin = lnJ0 + (lnW0 - lnJ0) * zoomK;
        _axisLnMax = lnJ1 + (lnW1 - lnJ1) * zoomK;

        // Zoom on exactly what the journey bar was drawing, so the zoom's first frame IS the
        // journey's last. The reveal then blends into the real spectrum: in Flux mode that
        // only adds the Ly-beta absorption below 1026 Å (the reference has it); in
        // Transmission mode it is where the quasar's peak and shape first appear.
        BakedSpectrumData_NEW.FinalChannel shown = _activeHudCurve == HudCurve.Flux
            ? BakedSpectrumData_NEW.FinalChannel.DisplayedSpectrum
            : BakedSpectrumData_NEW.FinalChannel.Transmission;

        int n = _hudCurve.Length;
        for (int c = 0; c < n; c++)
        {
            double lambda = Math.Exp(_axisLnMin + (_axisLnMax - _axisLnMin) * c / (n - 1));
            float from = _data.SampleFinal(lambda, shown);
            _hudCurve[c] = revealK <= 0f
                ? from
                : from + (_data.SampleFinal(lambda, BakedSpectrumData_NEW.FinalChannel.RealSpectrum) - from) * revealK;
        }

        if (driveHud && hud != null) hud.MarkExternalCurveDirty();

        // The birth tick slides off the left as the view narrows; marks follow their lines.
        if (driveMarks && marks != null) marks.SetPhysicalMapping(AxisT(BakedSpectrumData_NEW.LymanAlphaA), _lineT);

        if (_arrival == Arrival.Done)
        {
            if (logLoad) Debug.Log("[BakedSpectrumSource_NEW] Arrival complete: reference frame shown.", this);
            onArrivalComplete.Invoke();
        }
    }

    /// <summary>
    /// The F10 debug key: make the marks visible (normally the demo sequence at the second
    /// cosmic web does that) and replay. If the light has barely left the quasar there is no
    /// history to replay and no line cut yet, so say so rather than appear to do nothing.
    /// </summary>
    void DebugReplay()
    {
        if (!_active) return;

        if (driveMarks && marks != null) marks.ShowAll();

        // Less than ~2% of the journey's stretch travelled: nothing would visibly move.
        double travelled = Math.Log(1.0 + _data.ZQuasar) - Math.Log(1.0 + _zNow);
        if (travelled < 0.02 * Math.Log(1.0 + _data.ZQuasar))
        {
            Debug.LogWarning("[BakedSpectrumSource_NEW] F10: the light has only just left the quasar " +
                             "(z " + _zNow.ToString("0.000") + "), so there is no history to replay and no line " +
                             "cut yet. Set Override Lookback Gyr to 7 (≈ the second cosmic web), or fly further, " +
                             "then press F10 again.", this);
            return;
        }

        ReplayHistory(6f);
    }

    /// <summary>
    /// One frame of the replay: the drawn redshift runs from the quasar's down to the light's
    /// real one, evenly in ln(1+z) — equal stretch per second, so every line moves at the
    /// pace physics gives it. Eased at both ends so it leaves and rejoins the live view gently.
    /// </summary>
    void TickReplay(float dt)
    {
        _replayTime += dt;
        float k = Smooth(_replayTime / _replaySeconds);

        double sStart = Math.Log(1.0 + _data.ZQuasar);
        double sEnd = Math.Log(1.0 + _zNow);
        _zDisplay = (float)(Math.Exp(sStart + (sEnd - sStart) * k) - 1.0);

        Push(_data.RowFromZ(_zDisplay));

        if (_replayTime >= _replaySeconds)
        {
            _replaying = false;
            _zDisplay = _zNow;
            _lastRow = float.NaN;   // force the next live frame to redraw
            if (logLoad) Debug.Log("[BakedSpectrumSource_NEW] Replay done; back to the live journey.", this);
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>
    /// F11 readout. Every value here is what drives the HUD this frame, so "the HUD is not
    /// moving" becomes one of: the lookback is not changing (the tracker / player), the row
    /// is not changing (the mapping), or the row changes but nothing redraws (the HUD).
    /// </summary>
    void OnGUI()
    {
        if (!_showReadout) return;

        if (_readoutStyle == null)
        {
            _readoutStyle = new GUIStyle(GUI.skin.box);
            _readoutStyle.fontSize = 14;
            _readoutStyle.alignment = TextAnchor.UpperLeft;
            _readoutStyle.normal.textColor = Color.white;
        }

        string text;
        if (!_active)
        {
            text = "SPECTRUM DATA   not active" + (_failed ? " (failed to load, see Console)" : useBakedData ? "" : " (useBakedData is off)");
        }
        else
        {
            float lookback = CurrentLookbackGyr();
            float peakT = AxisT(BakedSpectrumData_NEW.LymanAlphaA * (1.0 + _data.ZQuasar) / (1.0 + _zDisplay));
            string state = _arrival != Arrival.Journey ? "arrival: " + _arrival : _replaying ? "replaying" : "journey";
            string source = overrideLookbackGyr >= 0f ? "OVERRIDE " + overrideLookbackGyr.ToString("0.00")
                          : tracker == null ? "no tracker!" : "tracker";

            text = "SPECTRUM DATA   (F11 to hide)\n" +
                   "lookback  " + lookback.ToString("0.000") + " Gyr   from " + source + "\n" +
                   "z now     " + _zNow.ToString("0.000") + "   drawn at z " + _zDisplay.ToString("0.000") + "\n" +
                   "row       " + (float.IsNaN(_lastPushedRow) ? "—" : _lastPushedRow.ToString("0.0")) + " / " + (_data.Rows - 1) +
                   "   last update " + (_lastPushTime < 0f ? "never" : (Time.time - _lastPushTime).ToString("0.00") + " s ago") + "\n" +
                   "Lyα peak  " + (peakT * 100f).ToString("0.0") + "% across the bar   state: " + state;
        }

        GUI.Box(new Rect(16f, Screen.height - 124f, 560f, 108f), text, _readoutStyle);
    }
#endif

    static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    // ── Tutorial handoff ─────────────────────────────────────────────────────

    /// <summary>
    /// Read (and consume) where the tutorial's light got to. Called on activation, before
    /// the marks are set, so mark [0] can be the player's own first line.
    /// </summary>
    void TakeHandoff()
    {
        if (!SpectrumHandoff_NEW.HasHandoff) return;

        _holdLookback = Mathf.Min(SpectrumHandoff_NEW.EndLookbackGyr, _data.LookbackStartGyr);
        if (logLoad)
            Debug.Log("[BakedSpectrumSource_NEW] Continuing from the tutorial: holding at " +
                      _holdLookback.ToString("0.000") + " Gyr (z " + _data.ZFromLookback(_holdLookback).ToString("0.000") +
                      ") until the tracker passes it.", this);

        SpectrumHandoff_NEW.Clear();
    }

    /// <summary>
    /// The bake's notable absorbers, with [0] — "the first line" — replaced by the line the
    /// player's own atom cut in the tutorial, when there was one. It is a real cell of the same
    /// record, so it is where the data says a line is; it is just the one THEY made.
    /// </summary>
    float[] MarksWithTutorialLine()
    {
        float z = SpectrumHandoff_NEW.FirstLineZ;
        float[] src = _data.AbsorberZ;
        if (float.IsNaN(z) || src.Length == 0 || z > _data.ZQuasar) return src;

        float[] marksZ = (float[])src.Clone();
        marksZ[0] = z;
        return marksZ;
    }

    // ── Trail: real wavelengths, and one line at a time ──────────────────────

    /// <summary>Bar position → trail position (the far-UV margin sits on the ribbon's outer side).</summary>
    float TrailT(float barT)
    {
        return trailRealWavelengths ? trailFarUvShare + (1f - trailFarUvShare) * barT : barT;
    }

    /// <summary>The bar's absorption, laid out on the trail's axis, with any emphasis cut in.</summary>
    void BuildTrail()
    {
        _trailDirty = false;
        if (!driveTrail || field == null || _trailAbsorption == null) return;

        int n = _trailAbsorption.Length;
        float m = trailRealWavelengths ? trailFarUvShare : 0f;
        for (int c = 0; c < n; c++)
        {
            float t = c / (float)(n - 1);
            if (t < m) { _trailAbsorption[c] = 0f; continue; }   // bluer than the birth tick: never absorbed
            float b = (t - m) / (1f - m) * (n - 1);
            int lo = (int)b, hi = lo + 1 < n ? lo + 1 : lo;
            _trailAbsorption[c] = _absorption[lo] + (_absorption[hi] - _absorption[lo]) * (b - lo);
        }

        if (!float.IsNaN(_emphT) && _emphK > 0f)
        {
            float centre = TrailT(_emphT);
            float half = emphasisHalfWidth * (1f - m);
            int c0 = Mathf.Max(0, Mathf.FloorToInt((centre - half) * (n - 1)));
            int c1 = Mathf.Min(n - 1, Mathf.CeilToInt((centre + half) * (n - 1)));
            for (int c = c0; c <= c1; c++) _trailAbsorption[c] = Mathf.Max(_trailAbsorption[c], _emphK);
        }

        field.MarkExternalAbsorptionDirty();
    }

    void LateUpdate()
    {
        if (_active && _trailDirty) BuildTrail();
    }

    /// <summary>
    /// Colour the ribbon by real wavelength on the journey bar's axis (fixed: the trail keeps
    /// the journey axis even during the arrival zoom, as its data does).
    /// </summary>
    void ApplyTrailColours()
    {
        if (_trailRenderer == null || _data == null) return;

        TrailSpectrumColours_NEW.Ensure(ref _trailTex, ref _trailPixels, 512, "BakedSpectrumSource_Trail");

        Color uvBase = spectrumTrail != null ? spectrumTrail.uvColor : new Color(0.55f, 0f, 1f, 1f);
        Color irBase = spectrumTrail != null ? spectrumTrail.irColor : new Color(0.8f, 0.02f, 0f, 1f);
        float irB = spectrumTrail != null ? spectrumTrail.irBrightness : 0.28f;
        Color uv = uvBase * trailUvAlpha; uv.a = trailUvAlpha;
        Color ir = irBase * irB; ir.a = irB;

        double lnFar = Math.Log(1000.0), ln0 = Math.Log(_data.LambdaMin), ln1 = Math.Log(_data.LambdaMax);
        float m = trailFarUvShare;
        TrailSpectrumColours_NEW.Fill(_trailPixels,
            t => t < m ? Math.Exp(lnFar + (ln0 - lnFar) * t / m) : Math.Exp(ln0 + (ln1 - ln0) * (t - m) / (1f - m)),
            uv, ir);
        _trailTex.SetPixels32(_trailPixels);
        _trailTex.Apply(false);

        if (_trailBlock == null) _trailBlock = new MaterialPropertyBlock();
        _trailRenderer.GetPropertyBlock(_trailBlock);
        _trailBlock.SetTexture("_SpectrumTex", _trailTex);
        _trailRenderer.SetPropertyBlock(_trailBlock);
    }

    void OnDestroy()
    {
        if (_trailTex != null) Destroy(_trailTex);
    }
}

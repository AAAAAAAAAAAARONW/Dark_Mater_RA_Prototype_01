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

    [Tooltip("Dim the light that left the quasar bluer than 912 Å — the Lyman break. In reality that " +
             "light is almost all destroyed by photoionisation; the piece draws only Ly-alpha lines, so " +
             "without this the left of the bar stays bright and flat (brighter than the forest).\n\n" +
             "This is the lowest it is dimmed to, as a fraction: 0.3 = down to 30%, so it reads as " +
             "fainter light rather than as black. 1 = off. 0 = the real break, black. Only the HUD " +
             "curve; the trail keeps Ly-alpha lines only. Needs a format-5 bake.")]
    [Range(0f, 1f)]
    [SerializeField] float lymanBreakFloor = 0.3f;

    [SerializeField] AbsorptionField_NEW field;
    [Tooltip("Feed the photon trail's absorption texture. With trailRealWavelengths on (below), " +
             "the trail is also coloured by real wavelength, so each line sits under its own colour.")]
    [SerializeField] bool driveTrail = true;

    [Tooltip("Put the trail on real wavelengths, like the tutorial's, on a fixed piecewise axis: a " +
             "far-UV margin on the ribbon's outer side (1000 Å up to the birth tick), then the UV, the " +
             "visible and the IR, each its own share of the ribbon, coloured by real wavelength.\n\n" +
             "The shares are a broken axis on purpose. On the bar's own log axis the UV is ~60% of the " +
             "width, so a true-to-scale ribbon is mostly purple; this keeps every colour at its real " +
             "wavelength but gives the visible light the room it has in the tutorial.\n\n" +
             "Without the margin a just-born line sits on the ribbon's outer edge, where the trail " +
             "shader fades everything out. Off = the trail is fed the bar's array as before, art colours kept.")]
    [SerializeField] bool trailRealWavelengths = true;

    public enum TrailLook
    {
        Rainbow = 0,
        RealWavelengths = 1
    }

    [Tooltip("RAINBOW (default): the trail is the piece's evenly spread seven-colour rainbow, as always — " +
             "no separate UV or IR bands (those belong to the tutorial's demonstration). Lines sit across " +
             "it in step with the bar: same position, from the left edge to the right.\n\n" +
             "REAL WAVELENGTHS: colours are true wavelengths, with UV / visible / IR shares (below).")]
    [SerializeField] TrailLook trailLook = TrailLook.Rainbow;

    [Tooltip("Rainbow look: a margin on the ribbon's short-wavelength edge where no line sits — the trail " +
             "shader fades the edge, and a just-born line would vanish into it.")]
    [Range(0.03f, 0.3f)]
    [SerializeField] float trailEdgeMargin = 0.1f;

    [Tooltip("Far UV, 1000 Å to the birth tick: real light, never absorbed by Ly-alpha; keeps lines off the ribbon's faded edge.")]
    [Range(0.05f, 0.4f)]
    [SerializeField] float trailFarUvFraction = 0.12f;

    [Tooltip("UV, birth tick to 4000 Å.")]
    [Range(0.05f, 0.6f)]
    [SerializeField] float trailUvFraction = 0.28f;

    [Tooltip("Visible, 4000–7000 Å. IR gets what is left.")]
    [Range(0.1f, 0.8f)]
    [SerializeField] float trailVisibleFraction = 0.42f;

    [Tooltip("Opacity of the UV part of the ribbon.")]
    [Range(0f, 1f)]
    [SerializeField] float trailUvAlpha = 0.65f;

    [Tooltip("The highlighted line blinks over its own width on the trail; this is the least it blinks " +
             "over, as a fraction of the ribbon either side, for a line too thin to see.")]
    [Range(0f, 0.02f)]
    [SerializeField] float trailEmphasisMinHalfWidth = 0.002f;

    [Tooltip("Let absorption cut the trail's glow and emission too. The trail shader adds them AFTER darkening a " +
             "line, and the middle of the ribbon is bright enough to saturate — so the strong old forest " +
             "there was invisible and only weak young lines at the dim blue edge showed (most obviously " +
             "from the Milky Way on, once the nearest gas has been crossed). On: an absorbed wavelength " +
             "stays dark wherever it is. Off: the material's own behaviour.")]
    [SerializeField] bool trailAbsorbThroughGlow = true;

    [Tooltip("Run the ribbon's wavelengths the other way across it. Set automatically by " +
             "AbsorberHighlight_NEW so that, on screen, UV is on the LEFT like the bar and lines move " +
             "left to right; this is only the starting value.")]
    [SerializeField] bool trailMirrored = false;

    [Header("HUD sampling")]
    [Tooltip("How much of the gap between two HUD points each point averages (0.1–1). The points ride " +
             "with the lines (they move as the light redshifts), so a line is measured the same way " +
             "every frame: no jumping, and nothing is smoothed away. Smaller = deeper, sharper lines; " +
             "0.5 matches the depth the bar always had.")]
    [Range(0.1f, 1f)]
    [SerializeField] float hudBinWidth = 0.5f;

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
    float[] _transmission;
    bool _continuous;
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
        if (debugReadoutKey != KeyCode.None && Input.GetKeyDown(debugReadoutKey))
        {
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                _trailTestPattern = !_trailTestPattern;
                Debug.Log("[BakedSpectrumSource_NEW] Trail test pattern " + (_trailTestPattern ? "ON: lines at 25%, 50%, 75% across" : "off") + ".", this);
                BuildTrail();
            }
            else _showReadout = !_showReadout;
        }
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
        // With the path record (format 4) the HUD curve is drawn at exactly the HUD's own sample
        // count, each point box-averaged — no point-resampling, so narrow lines do not flicker.
        _continuous = _data.HasRecord;
        _hudCurve = new float[_continuous && hud != null ? Mathf.Max(2, hud.SampleCount) : _data.Columns];
        _transmission = new float[_data.Columns];
        _activeHudCurve = hudDisplay;
        _lastRow = float.NaN;
        _active = true;
        _arrival = Arrival.Journey;
        _axisLnMin = Math.Log(_data.LambdaMin);
        _axisLnMax = Math.Log(_data.LambdaMax);

        if (driveHud && hud != null) hud.SetExternalCurve(_hudCurve, _hudCurve.Length);
        if (driveHud && hud != null) hud.SetWavelengthAxis(_data.LambdaMin, _data.LambdaMax);
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

        if (_continuous)
        {
            // Straight from the path record at the exact redshift: a line SLIDES between rows
            // instead of fading out in one place and in at the next, which is what blending two
            // stored rows did (it read as the lines jumping).
            _data.SampleView(_zDisplay, _data.LambdaMin, _data.LambdaMax, null, _transmission);
            for (int c = 0; c < _absorption.Length; c++) _absorption[c] = 1f - _transmission[c];
            DrawHudRiding(flux);
        }
        else
        {
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
        }

        if (driveHud && hud != null) hud.MarkExternalCurveDirty();
        BuildTrail();
        if (driveMarks && marks != null) marks.MarkPhysicalDirty();
    }

    /// <summary>
    /// The HUD curve, sampled on points that RIDE WITH THE LINES.
    ///
    /// Redshift moves every line by the same amount on the log axis. Points fixed on the screen
    /// measure a narrow line differently each frame as it slides between them (one deep spike on
    /// a point, two half spikes between) — the jumping. Smoothing hid that but also smoothed the
    /// forest away. Instead the points move with the lines by the fraction of a step they have
    /// travelled, and the graph is drawn shifted by the same fraction: each line is measured
    /// identically every frame, keeps its full depth, and still moves smoothly. When the shift
    /// passes a whole step the points relabel (point i becomes i+1) and nothing changes on screen.
    /// </summary>
    void DrawHudRiding(bool flux)
    {
        int n = _hudCurve.Length;
        double lnMin = Math.Log(_data.LambdaMin), lnMax = Math.Log(_data.LambdaMax);
        double step = (lnMax - lnMin) / (n - 1);
        double moved = (Math.Log(1.0 + _data.ZQuasar) - Math.Log(1.0 + _zDisplay)) / step;
        float phase = (float)(moved - Math.Floor(moved));

        _data.SampleView(_zDisplay, Math.Exp(lnMin + phase * step), Math.Exp(lnMax + phase * step),
                         flux ? _hudCurve : null, flux ? null : _hudCurve, null, lymanBreakFloor, hudBinWidth);
        if (driveHud && hud != null) hud.SetExternalCurveShift(phase);
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

        if (driveHud && hud != null) hud.SetExternalCurveShift(0f);   // the arrival draws on fixed points

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
        if (driveHud && hud != null) hud.SetWavelengthAxis(Math.Exp(_axisLnMin), Math.Exp(_axisLnMax));

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
            // The same Lyman-break dimming as the journey, so the zoom starts on the frame the
            // journey ended on. The reveal then blends into the real spectrum (full physics).
            if (shown == BakedSpectrumData_NEW.FinalChannel.DisplayedSpectrum && lymanBreakFloor < 1f)
                from *= _data.LymanBreakFactor(lambda / (1.0 + _data.ZQuasar), lymanBreakFloor);
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

    // Shift+F11's test pattern. Declared for every build — the trail's own update reads it —
    // and only ever switched on where the key is read, in the editor and development builds.
    bool _trailTestPattern;

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
        if (_active) DrawTrailDebug(new Rect(16f, Screen.height - 124f - 92f, 560f, 84f));
    }

    Texture2D _debugStrip;
    Texture2D _debugActual;
    Color32[] _debugActualPixels;
    Color32[] _debugPixels;

    /// <summary>
    /// What the trail SHOULD look like across its width (left = its short-wavelength edge): the
    /// colour texture darkened by the absorption this source feeds it — and what is actually set
    /// on the trail right now. If the ribbon on screen differs from this strip, something else is
    /// changing the trail.
    /// </summary>
    void DrawTrailDebug(Rect r)
    {
        const int W = 256;
        if (_debugStrip == null)
        {
            _debugStrip = new Texture2D(W, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            _debugPixels = new Color32[W];
        }
        for (int x = 0; x < W; x++)
        {
            float t = (x + 0.5f) / W;
            Color c = _trailTex != null ? _trailTex.GetPixelBilinear(t, 0.5f) : Color.gray;
            float a = _trailAbsorption != null ? _trailAbsorption[Mathf.Clamp(Mathf.RoundToInt(t * (_trailAbsorption.Length - 1)), 0, _trailAbsorption.Length - 1)] : 0f;
            c = c * (1f - a); c.a = 1f;
            _debugPixels[x] = c;
        }
        _debugStrip.SetPixels32(_debugPixels);
        _debugStrip.Apply(false);

        string actual = "no trail renderer";
        if (_trailRenderer != null)
        {
            var block = new MaterialPropertyBlock();
            _trailRenderer.GetPropertyBlock(block);
            Material m = _trailRenderer.sharedMaterial;
            Texture spec = block.GetTexture("_SpectrumTex");
            Texture abs = m != null && m.HasProperty("_AbsorptionLineTex") ? m.GetTexture("_AbsorptionLineTex") : null;
            actual = "trail '" + _trailRenderer.name + "'  material '" + (m != null ? m.name : "none") + "' shader '" + (m != null ? m.shader.name : "-") + "'\n" +
                     "block: spectrumTex " + (spec != null ? spec.name : "NONE") + "  scale " + block.GetFloat("_SpectrumScale").ToString("0") +
                     "  offset " + block.GetFloat("_SpectrumOffset").ToString("0") + "   absorptionTex " + (abs != null ? abs.name : "NONE") +
                     "\nlook " + trailLook + "  mirrored " + trailMirrored + "  enabled " + _trailRenderer.enabled;
        }
        GUI.Box(r, "", _readoutStyle);
        GUI.DrawTexture(new Rect(r.x + 6f, r.y + 6f, r.width - 12f, 18f), _debugStrip, ScaleMode.StretchToFill, false);
        // Second strip: the absorption texture ACTUALLY bound on the trail's material, read
        // back (white = clear, black = absorbed). If it differs from the first strip, the
        // trail is not getting this source's data.
        Texture2D bound = null;
        if (_trailRenderer != null && _trailRenderer.sharedMaterial != null && _trailRenderer.sharedMaterial.HasProperty("_AbsorptionLineTex"))
            bound = _trailRenderer.sharedMaterial.GetTexture("_AbsorptionLineTex") as Texture2D;
        if (_trailRenderer != null)
        {
            var b = new MaterialPropertyBlock();
            _trailRenderer.GetPropertyBlock(b);
            Texture2D fromBlock = b.GetTexture("_AbsorptionLineTex") as Texture2D;
            if (fromBlock != null) bound = fromBlock;   // what the trail really draws
        }
        if (bound != null)
        {
            if (_debugActual == null)
            {
                _debugActual = new Texture2D(W, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                _debugActualPixels = new Color32[W];
            }
            for (int x = 0; x < W; x++)
            {
                float a = 0f;
                try { a = bound.GetPixelBilinear((x + 0.5f) / W, 0.5f).r; } catch (System.Exception) { a = 0.5f; }
                byte v = (byte)(255f * Mathf.Clamp01(1f - a));
                _debugActualPixels[x] = new Color32(v, v, v, 255);
            }
            _debugActual.SetPixels32(_debugActualPixels);
            _debugActual.Apply(false);
            GUI.DrawTexture(new Rect(r.x + 6f, r.y - 14f, r.width - 12f, 12f), _debugActual, ScaleMode.StretchToFill, false);
        }
        if (_trailTestPattern) GUI.Label(new Rect(r.x + r.width - 160f, r.y - 30f, 160f, 16f), "TEST PATTERN (Shift+F11)");
        GUI.Label(new Rect(r.x + 6f, r.y + 26f, r.width - 12f, r.height - 28f), actual, _readoutStyle);
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

    /// <summary>Bar position → trail position.</summary>
    float TrailT(float barT)
    {
        if (!trailRealWavelengths) return barT;
        return TrailTFromLn(Math.Log(_data.LambdaMin) + barT * Math.Log(_data.LambdaMax / (double)_data.LambdaMin));
    }

    readonly double[] _segLn = new double[5];
    readonly float[] _segT = new float[5];

    /// <summary>The piecewise ribbon axis: far UV | UV | visible | IR, each its own share.</summary>
    void BuildTrailAxis()
    {
        float a = trailFarUvFraction, b = trailUvFraction, c = trailVisibleFraction;
        float sum = a + b + c;
        if (sum > 0.95f) { float k = 0.95f / sum; a *= k; b *= k; c *= k; }
        _segLn[0] = Math.Log(1000.0);
        _segLn[1] = Math.Log(_data.LambdaMin);
        _segLn[2] = Math.Log(Math.Max(_data.LambdaMin * 1.01, 4000.0));
        _segLn[3] = Math.Log(Math.Max(_data.LambdaMin * 1.02, 7000.0));
        _segLn[4] = Math.Log(Math.Max(_data.LambdaMin * 1.03, _data.LambdaMax));
        _segT[0] = 0f; _segT[1] = a; _segT[2] = a + b; _segT[3] = a + b + c; _segT[4] = 1f;

        if (trailLook == TrailLook.Rainbow)
        {
            // One rainbow across the ribbon: the bar's axis laid on it evenly after a small margin
            // (lines stay off the faded edge). The break points only keep the same machinery.
            float m = trailEdgeMargin;
            double lnW = _segLn[4] - _segLn[1];
            _segT[1] = m;
            _segT[2] = m + (1f - m) * (float)((_segLn[2] - _segLn[1]) / lnW);
            _segT[3] = m + (1f - m) * (float)((_segLn[3] - _segLn[1]) / lnW);
        }
    }

    float TrailTFromLn(double ln)
    {
        BuildTrailAxis();
        if (ln <= _segLn[0]) return 0f;
        for (int i = 0; i < 4; i++)
            if (ln <= _segLn[i + 1])
                return _segT[i] + (_segT[i + 1] - _segT[i]) * (float)((ln - _segLn[i]) / (_segLn[i + 1] - _segLn[i]));
        return 1f;
    }

    double TrailLn(float t)
    {
        BuildTrailAxis();
        for (int i = 0; i < 4; i++)
            if (t <= _segT[i + 1] || i == 3)
                return _segLn[i] + (_segLn[i + 1] - _segLn[i]) * Mathf.Clamp01((t - _segT[i]) / Mathf.Max(1e-6f, _segT[i + 1] - _segT[i]));
        return _segLn[4];
    }

    /// <summary>True if the ribbon's wavelengths run the other way across it.</summary>
    public bool TrailMirrored { get { return trailMirrored; } }

    /// <summary>Flip the ribbon's wavelength direction (AbsorberHighlight_NEW does this so UV is on screen-left).</summary>
    public void SetTrailMirrored(bool mirrored)
    {
        if (trailMirrored == mirrored) return;
        trailMirrored = mirrored;
        if (_active && driveTrail && trailRealWavelengths) ApplyTrailColours();
    }

    /// <summary>The bar's absorption, laid out on the trail's axis, with any emphasis cut in.</summary>
    void BuildTrail()
    {
        _trailDirty = false;
        if (!driveTrail || field == null || _trailAbsorption == null) return;

        int n = _trailAbsorption.Length;
        if (!trailRealWavelengths)
        {
            Array.Copy(_absorption, _trailAbsorption, Math.Min(n, _absorption.Length));
        }
        else
        {
            double ln0 = Math.Log(_data.LambdaMin), lnW = Math.Log(_data.LambdaMax / (double)_data.LambdaMin);
            int m = _absorption.Length;
            for (int c = 0; c < n; c++)
            {
                double ln = TrailLn(c / (float)(n - 1));
                if (ln < ln0) { _trailAbsorption[c] = 0f; continue; }   // bluer than the birth tick: never absorbed
                double b = (ln - ln0) / lnW * (m - 1);
                int lo = (int)b, hi = lo + 1 < m ? lo + 1 : lo;
                if (lo >= m) lo = hi = m - 1;
                _trailAbsorption[c] = _absorption[lo] + (_absorption[hi] - _absorption[lo]) * (float)(b - lo);
            }
        }

        if (_trailTestPattern)
        {
            // Debug (Shift+F11): three hard lines at exactly 25%, 50% and 75% across the ribbon, so
            // where they appear on the trail shows how the trail maps positions.
            for (int c = 0; c < n; c++)
            {
                float t = c / (float)(n - 1);
                _trailAbsorption[c] = Mathf.Abs(t - 0.25f) < 0.006f || Mathf.Abs(t - 0.5f) < 0.006f || Mathf.Abs(t - 0.75f) < 0.006f ? 1f : 0f;
            }
        }

        if (!float.IsNaN(_emphT) && _emphK > 0f)
        {
            // Blink THE LINE ITSELF: find it on the trail (the deepest point near where it should
            // be), take its own footprint (where it is still at least a third as deep), and darken
            // just that, fully at each blink. Not a band painted on top — the same line, flashing.
            int centre = Mathf.RoundToInt(TrailT(_emphT) * (n - 1));
            int search = Mathf.Max(2, Mathf.RoundToInt(0.006f * n));
            int best = Mathf.Clamp(centre, 0, n - 1);
            for (int c = Mathf.Max(0, centre - search); c <= Mathf.Min(n - 1, centre + search); c++)
                if (_trailAbsorption[c] > _trailAbsorption[best]) best = c;

            float edge = _trailAbsorption[best] / 3f;
            int minHalf = Mathf.Max(1, Mathf.RoundToInt(trailEmphasisMinHalfWidth * n));
            int lo = best, hi = best;
            while (best - lo < 12 && lo - 1 >= 0 && (_trailAbsorption[lo - 1] > edge || best - lo < minHalf)) lo--;
            while (hi - best < 12 && hi + 1 < n && (_trailAbsorption[hi + 1] > edge || hi - best < minHalf)) hi++;
            for (int c = lo; c <= hi; c++) _trailAbsorption[c] = Mathf.Lerp(_trailAbsorption[c], 1f, _emphK);
        }

        field.MarkExternalAbsorptionDirty();
        UploadTrailAbsorption();
    }

    Texture2D _trailAbsTex;
    Color32[] _trailAbsPixels;

    /// <summary>
    /// The trail's absorption, in this source's OWN texture, bound to the trail through its
    /// property block (which wins over the material). The trail then shows exactly this data,
    /// whatever else writes the material's _AbsorptionLineTex — on 2026-10-06 the texture the
    /// trail was drawing had stopped matching the data fed to AbsorptionField_NEW, while a test
    /// pattern drew exactly where it should. The field is still fed, so it does not start
    /// spawning lines of its own.
    /// </summary>
    void UploadTrailAbsorption()
    {
        if (_trailRenderer == null || _trailAbsorption == null) return;
        int n = _trailAbsorption.Length;
        bool created = false;
        if (_trailAbsTex == null || _trailAbsTex.width != n)
        {
            if (_trailAbsTex != null) Destroy(_trailAbsTex);
            _trailAbsTex = new Texture2D(n, 1, TextureFormat.RGBA32, false, true)
            {
                name = "BakedSpectrumSource_TrailAbsorption",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            _trailAbsPixels = new Color32[n];
            created = true;
        }
        for (int c = 0; c < n; c++)
        {
            byte v = (byte)(Mathf.Clamp01(_trailAbsorption[c]) * 255f + 0.5f);
            _trailAbsPixels[c] = new Color32(v, 0, 0, 255);
        }
        _trailAbsTex.SetPixels32(_trailAbsPixels);
        _trailAbsTex.Apply(false);

        if (created)
        {
            if (_trailBlock == null) _trailBlock = new MaterialPropertyBlock();
            _trailRenderer.GetPropertyBlock(_trailBlock);
            _trailBlock.SetTexture("_AbsorptionLineTex", _trailAbsTex);
            _trailRenderer.SetPropertyBlock(_trailBlock);
        }
    }

    void LateUpdate()
    {
        if (_active && _trailDirty) BuildTrail();
    }

    /// <summary>
    /// Colour the ribbon by real wavelength on its piecewise axis (fixed: the trail keeps the
    /// journey axis even during the arrival zoom, as its data does), and set its direction.
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

        if (trailLook == TrailLook.Rainbow) TrailSpectrumColours_NEW.FillRainbow(_trailPixels);
        else TrailSpectrumColours_NEW.Fill(_trailPixels, t => Math.Exp(TrailLn(t)), uv, ir);
        _trailTex.SetPixels32(_trailPixels);
        _trailTex.Apply(false);

        if (_trailBlock == null) _trailBlock = new MaterialPropertyBlock();
        _trailRenderer.GetPropertyBlock(_trailBlock);
        _trailBlock.SetTexture("_SpectrumTex", _trailTex);
        if (_trailAbsTex != null) _trailBlock.SetTexture("_AbsorptionLineTex", _trailAbsTex);
        // The shader reads specT = frac(((1 − uv.y) + offset) × scale). Scale −1, offset −1 runs
        // the same 0–1 window the other way across the ribbon (as TutorialTrailBands_NEW does).
        _trailBlock.SetFloat("_SpectrumScale", trailMirrored ? -1f : 1f);
        _trailBlock.SetFloat("_SpectrumOffset", trailMirrored ? -1f : 0f);
        _trailBlock.SetFloat("_AbsorbThroughGlow", trailAbsorbThroughGlow ? 1f : 0f);
        _trailRenderer.SetPropertyBlock(_trailBlock);
    }

    void OnDestroy()
    {
        if (_trailTex != null) Destroy(_trailTex);
        if (_trailAbsTex != null) Destroy(_trailAbsTex);
    }
}

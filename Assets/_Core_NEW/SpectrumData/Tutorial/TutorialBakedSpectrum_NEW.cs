using System;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// The tutorial's spectrum bar, drawn from the journey's own baked data, so the tutorial
/// and the journey are one continuous piece of physics.
///
/// WHAT IT PLAYS. The first stretch of the real journey, time-compressed: from the moment
/// of emission (z = 3.62) the light's lookback runs down at gyrPerSecond (0.003 Gyr/s, so
/// the ~60 s of Phase 3 cover ~0.18 Gyr, about 1.5% of the trip). The journey then carries
/// on from exactly that point (SpectrumHandoff_NEW) — the bar before and after the black is
/// the same frame.
///
/// WHY IT ZOOMS. Real redshift over that stretch is small: lines move ~0.035 of the full
/// 1216–9000 Å bar in a minute, so atoms 2.5 s apart would land ~1 px apart. From D4 the
/// bar magnifies the region next to the birth tick (~17×, 1202–1350 Å) on the same data,
/// where each atom's line is its own mark; at D9 it zooms back out to the full bar, which is
/// what the journey opens on. All of it is UV, which is the truth: hydrogen's colour at
/// departure is one the eye cannot see.
///
/// WHAT AN ATOM DOES. On each absorption the line is cut at the birth tick, as before — but
/// it is a REAL line: the strongest absorber in the cells the light has just crossed, at its
/// real depth and width. Until D9 only those lines are shown; at D9 the rest of the forest
/// (all the gas the atoms stand for) fades in, so what the journey inherits is the complete,
/// unedited data.
///
/// THE TRAIL. The ribbon uses the same axis as the bar, and its colours are real
/// wavelengths on that axis: UV below 4000 Å, the visible rainbow 4000–7000 Å, IR above.
/// The band edges are pushed into PhotonSpectrumTrail's uvBandWidth / irBandWidth, which the
/// tutorial's band overlays already read, and the ribbon's colour texture is replaced via a
/// MaterialPropertyBlock (the shared material is untouched).
///
/// ISOLATION. Everything goes through hooks the tutorial already exposes
/// (TutorialSpectrum_NEW.SetExternalSource, SpectrumHUD_NEW.SetExternalCurve,
/// AbsorptionField_NEW.SetExternalAbsorption). Untick useBakedData — even while playing —
/// and every override is released; the tutorial runs exactly as it did before.
/// </summary>
[DefaultExecutionOrder(60)]
[DisallowMultipleComponent]
[HierarchyBadge_NEW("TUT DATA", "#E2506A")]
public class TutorialBakedSpectrum_NEW : MonoBehaviour, ITutorialSpectrumSource_NEW
{
    [Header("Switch")]
    [Tooltip("THE KILL SWITCH. Off = nothing is loaded or overridden; the tutorial's own " +
             "authored spectrum runs exactly as before. Can be flipped while playing.")]
    [SerializeField] bool useBakedData = true;

    [Header("Data")]
    [Tooltip("SpectrumData/Baked/SpectrumJourney.bytes — the SAME file the journey reads. " +
             "Needs format 4 or later (re-bake if the console says the file has no path record).")]
    [SerializeField] TextAsset bakedFile;

    [Header("Wiring (found in the scene if empty)")]
    [SerializeField] TutorialSpectrum_NEW spectrum;
    [SerializeField] SpectrumHUD_NEW hud;
    [SerializeField] AbsorptionField_NEW field;
    [SerializeField] TutorialDirector_NEW director;

    [Tooltip("The photon trail. Its band widths are set to the real UV / visible / IR edges, " +
             "and its colour texture is replaced with real wavelengths on the bar's axis.")]
    [SerializeField] PhotonSpectrumTrail spectrumTrail;

    public enum Curve
    {
        Flux = 0,
        Transmission = 1
    }

    [Header("Look")]
    [Tooltip("FLUX (default, same as the journey): the quasar's own spectrum with the lines " +
             "cut into it — the Ly-alpha peak slides red with the lines, because redshift moves " +
             "everything. TRANSMISSION: a flat line with only the dips, which holds still while " +
             "the lines move (what D7's description says), but does NOT match the journey's bar " +
             "at the cut.")]
    [SerializeField] Curve curve = Curve.Flux;

    [Tooltip("Samples across the bar.")]
    [Range(128, 2048)]
    [SerializeField] int columns = 1024;

    [Header("Time compression")]
    [Tooltip("Gyr of the journey per second of tutorial (scaled time: D5's slow motion slows " +
             "it, the pause stops it). 0.003 ≈ 0.18 Gyr over Phase 3's ~60 s ≈ 1.5% of the " +
             "journey. Real light takes 0.18 Gyr; the journey's own pace is ~30× faster than " +
             "this, so the tutorial is the slowest part of the trip.")]
    [Range(0.0005f, 0.05f)]
    [SerializeField] float gyrPerSecond = 0.003f;

    [Tooltip("The clock stops here, in Gyr after emission, however long the visitor lingers. " +
             "0.24 = 2% of the journey.")]
    [Range(0.01f, 2f)]
    [SerializeField] float maxSpanGyr = 0.24f;

    [Header("Magnified view")]
    [Tooltip("Width of the magnified window, in ln(wavelength). 0.117 ≈ 1202–1350 Å, ~17× the " +
             "full bar. Lines a beat apart land a finger-width apart.")]
    [Range(0.02f, 0.5f)]
    [SerializeField] float zoomWidthLn = 0.117f;

    [Tooltip("How much of the window sits LEFT of the birth tick (light not yet absorbed).")]
    [Range(0f, 0.5f)]
    [SerializeField] float zoomLeftShare = 0.1f;

    [Tooltip("Entering this beat zooms in.")]
    [SerializeField] string zoomInBeat = "D4";

    [Tooltip("Entering this beat zooms back out to the full bar — what the journey opens on.")]
    [SerializeField] string zoomOutBeat = "D9";

    [Min(0.1f)]
    [SerializeField] float zoomSeconds = 2f;

    [Header("Lines")]
    [Tooltip("Cells (37 km/s each) back from the birth tick searched for the atom's line: the " +
             "strongest absorber the light has just crossed. Small, so the line is born at the tick.")]
    [Range(1, 64)]
    [SerializeField] int searchCells = 12;

    [Tooltip("If nothing that deep is in searchCells, look this far back instead.")]
    [Range(1, 200)]
    [SerializeField] int searchCellsFallback = 40;

    [Tooltip("A line shallower than this is not good enough for an atom; widen the search.")]
    [Range(0f, 1f)]
    [SerializeField] float minLineDepth = 0.6f;

    [Tooltip("The revealed line extends from its deepest cell while absorption stays above " +
             "this fraction of the peak — i.e. the line's real width.")]
    [Range(0.05f, 0.95f)]
    [SerializeField] float lineEdgeFraction = 0.25f;

    [Range(1, 40)]
    [SerializeField] int maxLineHalfCells = 5;

    [Header("Forest reveal")]
    [Tooltip("Entering this beat fades in the rest of the forest — every cell of gas, not just " +
             "the atoms' lines — so the journey inherits the complete data.")]
    [SerializeField] string revealBeat = "D9";

    [Min(0.1f)]
    [SerializeField] float revealSeconds = 3f;

    public enum TrailAxis
    {
        AllBands = 0,
        FollowBar = 1
    }

    [Header("Trail colours")]
    [Tooltip("ALL BANDS (default): the ribbon always shows UV, visible and IR, on a fixed " +
             "piecewise axis — the bar zooms on its own. The region where the tutorial's lines " +
             "live (birth tick to lineRegionEndA) gets lineRegionShare of the ribbon so they are " +
             "readable; the rest of the UV, the visible and the IR each get their own share. " +
             "Every colour is still its real wavelength; only the spacing is stretched, like a " +
             "broken axis.\n\nFOLLOW BAR: the ribbon uses exactly the bar's axis, zoom included " +
             "(all UV while zoomed).")]
    [SerializeField] TrailAxis trailAxis = TrailAxis.AllBands;

    [Tooltip("Light BLUER than the birth tick, 1000 Å to the line region: real light, never absorbed by Ly-alpha. It is there to push the line region away from the ribbon's outer edge, where the trail shader fades everything — black lines included — to nothing.")]
    [Range(0f, 0.4f)]
    [SerializeField] float farUvShare = 0.20f;

    [Tooltip("Right end of the line region on the ribbon, Å. The tutorial's lines reach ~1310 Å.")]
    [Range(1250f, 3000f)]
    [SerializeField] float lineRegionEndA = 1400f;

    [Range(0.05f, 0.6f)]
    [SerializeField] float lineRegionShare = 0.25f;

    [Tooltip("The rest of the UV, lineRegionEndA to 4000 Å.")]
    [Range(0.02f, 0.5f)]
    [SerializeField] float uvRestShare = 0.10f;

    [Tooltip("Visible, 4000–7000 Å. IR gets whatever is left.")]
    [Range(0.1f, 0.8f)]
    [SerializeField] float visibleShare = 0.30f;

    [SerializeField] bool driveBands = true;
    [SerializeField] bool driveTrailColours = true;

    [Tooltip("Opacity of the UV part of the ribbon. PhotonSpectrumTrail's own UV is faint (its uvBrightness, ~0.3) because it was an edge; here the lines live in the UV, and a black line needs something bright behind it to read.")]
    [Range(0f, 1f)]
    [SerializeField] float uvAlpha = 0.65f;

    [Range(32, 1024)]
    [SerializeField] int trailTextureWidth = 256;

    [Header("Debug")]
    [SerializeField] bool logLoad = true;

    // ── State ────────────────────────────────────────────────────────────────

    BakedSpectrumData_NEW _data;
    bool _active;
    bool _failed;

    float[] _flux;
    float[] _transmission;
    float[] _curve;
    float[] _absorption;
    float[] _weight;            // per record cell: how much of it is shown (atoms' lines)
    Func<int, float> _weightFn;

    float _lookback;
    bool _running;
    float _zNow;

    float _zoom;                // 0 full bar, 1 magnified
    float _zoomFrom;
    float _zoomTo;
    float _zoomTime = -1f;

    float _revealAll;
    float _revealTime = -1f;

    bool _hasLine;
    float _lineZ;
    int _lines;

    double _lnMin;
    double _lnMax;

    float _origUv = -1f;
    float _origIr = -1f;
    TrailRenderer _trail;
    MaterialPropertyBlock _block;
    Texture2D _trailTex;
    Color32[] _trailPixels;
    double _texLnMin = double.NaN;
    double _texLnMax = double.NaN;

    static readonly int SpectrumTexId = Shader.PropertyToID("_SpectrumTex");
    static readonly double LnBirth = Math.Log(BakedSpectrumData_NEW.LymanAlphaA);

    // ── Public ───────────────────────────────────────────────────────────────

    public bool IsActive { get { return _active; } }
    public float CurrentZ { get { return _zNow; } }
    public float CurrentLookbackGyr { get { return _lookback; } }

    public void SetUseBakedData(bool on) { useBakedData = on; }

    /// <summary>Zoom into the birth tick. Hung on zoomInBeat; callable from anywhere.</summary>
    public void ZoomIn() { StartZoom(1f); }

    /// <summary>Back out to the full bar — the journey's framing.</summary>
    public void ZoomOut() { StartZoom(0f); }

    /// <summary>Fade the whole forest in.</summary>
    public void RevealForest() { if (_revealAll < 1f) _revealTime = 0f; }

    // ── ITutorialSpectrumSource_NEW ──────────────────────────────────────────

    public float RestFramePosition { get { return AxisT(LnBirth); } }

    public bool HasTrackedLine { get { return _active && _hasLine && TrackedLinePosition <= 1f; } }

    public float TrackedLinePosition
    {
        get { return _hasLine ? AxisT(LnBirth + Math.Log((1.0 + _lineZ) / (1.0 + _zNow))) : 0f; }
    }

    public float TrackedLineTrailPosition
    {
        get { return _hasLine ? TrailT(LnBirth + Math.Log((1.0 + _lineZ) / (1.0 + _zNow))) : 0f; }
    }

    public float BarVisibleStart { get { return Mathf.Clamp01(AxisT(Math.Log(4000.0))); } }
    public float BarVisibleEnd { get { return Mathf.Clamp01(AxisT(Math.Log(7000.0))); } }

    public void OnClear()
    {
        ResetState();
        _running = true;   // the bar drifts from the moment it is on screen (D3), as before
    }

    public void OnStartDrift()
    {
        // One rate throughout: redshift does not switch on, it was always happening. D7 only
        // names it.
    }

    public void OnReset()
    {
        ResetState();
        _running = false;
    }

    public bool Absorb()
    {
        if (!_active) return false;

        int now = (int)Math.Floor(_data.RecordCellFromZ(_zNow));
        if (now < 0) now = 0;

        int best = Deepest(now, searchCells);
        if (1f - _data.RecordAt(best) < minLineDepth) best = Deepest(now, searchCellsFallback);

        float peak = 1f - _data.RecordAt(best);
        float edge = peak * lineEdgeFraction;
        int lo = best, hi = best;
        while (best - lo < maxLineHalfCells && lo - 1 >= 0 && 1f - _data.RecordAt(lo - 1) > edge) lo--;
        while (hi - best < maxLineHalfCells && hi + 1 <= now && 1f - _data.RecordAt(hi + 1) > edge) hi++;

        for (int q = Mathf.Max(0, lo - 1); q <= Mathf.Min(_weight.Length - 1, hi + 1); q++)
            _weight[q] = Mathf.Max(_weight[q], q < lo || q > hi ? 0.5f : 1f);

        _lineZ = (float)_data.ZFromRecordCell(best);
        _hasLine = true;
        _lines++;

        if (_lines == 1) SpectrumHandoff_NEW.FirstLineZ = _lineZ;

        if (logLoad)
            Debug.Log("[TutorialBakedSpectrum_NEW] Atom " + _lines + ": real line at z " + _lineZ.ToString("0.0000") +
                      " (depth " + peak.ToString("0.00") + ", " + (hi - lo + 1) + " cells), light at z " +
                      _zNow.ToString("0.0000") + ".", this);
        return true;
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        // Inactive objects too: TutorialSpectrum_NEW hides the bar in its own Awake, and
        // FindObjectOfType (2019.4) skips anything inactive — the HUD would come back null.
        if (spectrum == null) spectrum = FindInScene<TutorialSpectrum_NEW>();
        if (director == null) director = FindInScene<TutorialDirector_NEW>();
        _weightFn = CellWeight;
    }

    /// <summary>
    /// The rest of the wiring, resolved in Start (after every Awake): the HUD and field are
    /// taken from TutorialSpectrum_NEW itself, so they are the bar it actually shows.
    /// </summary>
    void ResolveWiring()
    {
        if (hud == null && spectrum != null) hud = spectrum.Hud;
        if (field == null && spectrum != null) field = spectrum.Field;
        if (hud == null) hud = FindInScene<SpectrumHUD_NEW>();
        if (field == null) field = FindInScene<AbsorptionField_NEW>();
        if (spectrumTrail == null) spectrumTrail = FindInScene<PhotonSpectrumTrail>();
        if (spectrumTrail != null && _trail == null) _trail = spectrumTrail.GetComponent<TrailRenderer>();
    }

    T FindInScene<T>() where T : Component
    {
        T[] all = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].gameObject.scene == gameObject.scene) return all[i];
        return null;
    }

    void OnEnable()
    {
        if (director != null) director.OnBeatEntered += HandleBeat;
    }

    void Start()
    {
        if (useBakedData) Activate();
    }

    void OnDisable()
    {
        if (director != null) director.OnBeatEntered -= HandleBeat;
        // NOT clearing the handoff: this runs when the tutorial scene unloads, which is
        // exactly when the journey needs it.
        Release(false);
    }

    void OnDestroy()
    {
        if (_trailTex != null) Destroy(_trailTex);
    }

    void Update()
    {
        if (useBakedData && !_active && !_failed) Activate();
        if (!useBakedData && _active) Release(true);
        if (!_active) return;

        // Scaled time, like the field's own drift — D5's slow motion slows the light — and
        // nothing while the tutorial is paused.
        float dt = TutorialClock_NEW.DeltaTime > 0f ? Time.deltaTime : 0f;

        if (_running)
            _lookback = Mathf.Max(_data.LookbackStartGyr - maxSpanGyr, _lookback - gyrPerSecond * dt);
        _zNow = _data.ZFromLookback(_lookback);

        if (_zoomTime >= 0f)
        {
            _zoomTime += dt;
            float k = Smooth(_zoomTime / zoomSeconds);
            _zoom = _zoomFrom + (_zoomTo - _zoomFrom) * k;
            if (_zoomTime >= zoomSeconds) _zoomTime = -1f;
        }

        if (_revealTime >= 0f)
        {
            _revealTime += dt;
            _revealAll = Smooth(_revealTime / revealSeconds);
            if (_revealTime >= revealSeconds) { _revealAll = 1f; _revealTime = -1f; }
        }

        Draw();

        // Every frame, so whenever the scene changes the journey gets the latest moment.
        SpectrumHandoff_NEW.EndLookbackGyr = _lookback;
    }

    // ── Activation ───────────────────────────────────────────────────────────

    void Activate()
    {
        if (_active) return;

        if (bakedFile == null) { Fail("no baked file assigned (SpectrumData/Baked/SpectrumJourney.bytes)."); return; }

        string error;
        _data = BakedSpectrumData_NEW.Load(bakedFile.bytes, out error);
        if (_data == null) { Fail("could not read '" + bakedFile.name + "': " + error); return; }
        if (!_data.HasRecord)
        {
            Fail("'" + bakedFile.name + "' has no path record (format 3). Re-bake with Tools > Journey NEW > " +
                 "Spectrum Data > Bake Journey Spectrum.");
            _data = null;
            return;
        }
        if (spectrum == null) { Fail("no TutorialSpectrum_NEW in the scene."); _data = null; return; }

        ResolveWiring();
        if (hud == null) { Fail("no SpectrumHUD_NEW found for the tutorial bar — assign Hud by hand."); _data = null; return; }

        _flux = new float[columns];
        _transmission = new float[columns];
        _curve = new float[columns];
        _absorption = new float[columns];
        _weight = new float[_data.RecordCells];

        _active = true;
        ResetState();

        spectrum.SetExternalSource(this);
        if (hud != null) hud.SetExternalCurve(_curve, _curve.Length);
        if (field != null) field.SetExternalAbsorption(_absorption, _absorption.Length);

        if (spectrumTrail != null)
        {
            _origUv = spectrumTrail.uvBandWidth;
            _origIr = spectrumTrail.irBandWidth;
        }

        Draw();

        if (logLoad)
            Debug.Log("[TutorialBakedSpectrum_NEW] Tutorial bar drawn from '" + bakedFile.name + "': light leaves z " +
                      _data.ZQuasar.ToString("0.00") + " at " + gyrPerSecond.ToString("0.000") + " Gyr/s, magnified " +
                      ((Math.Log(_data.LambdaMax) - LnBirth) / zoomWidthLn).ToString("0") + "× from " + zoomInBeat +
                      " to " + zoomOutBeat + ", forest revealed at " + revealBeat + ". Drawing on HUD '" + hud.name +
                      "', field '" + (field != null ? field.name : "NONE") + "', trail '" + (spectrumTrail != null ? spectrumTrail.name : "NONE") +
                      "', beats from " + (director != null ? director.name : "NONE (no zoom or reveal!)") + ".", this);
    }

    void Release(bool clearHandoff)
    {
        if (!_active) return;

        if (spectrum != null) spectrum.ClearExternalSource(this);
        if (hud != null && hud.HasExternalCurve) hud.ClearExternalCurve();
        if (field != null && field.HasExternalAbsorption) field.ClearExternalAbsorption();

        if (spectrumTrail != null && _origUv >= 0f)
        {
            spectrumTrail.uvBandWidth = _origUv;
            spectrumTrail.irBandWidth = _origIr;
        }

        if (_trail != null && _block != null)
        {
            // A block cannot drop one property; clear it and let TutorialTrailBands_NEW
            // re-apply its own values next frame.
            _block.Clear();
            _trail.SetPropertyBlock(_block);
        }
        _texLnMin = _texLnMax = double.NaN;

        // Switched off by hand: the journey must not continue from a tutorial that is no
        // longer drawing the data.
        if (clearHandoff) SpectrumHandoff_NEW.Clear();

        _active = false;
        _data = null;
        if (logLoad) Debug.Log("[TutorialBakedSpectrum_NEW] Released — the tutorial's own spectrum is back.", this);
    }

    void Fail(string reason)
    {
        _failed = true;
        _active = false;
        Debug.LogError("[TutorialBakedSpectrum_NEW] Switched off: " + reason, this);
    }

    void ResetState()
    {
        if (_data == null) return;

        _lookback = _data.LookbackStartGyr;
        _zNow = _data.ZQuasar;
        _zoom = _zoomFrom = _zoomTo = 0f;
        _zoomTime = -1f;
        _revealAll = 0f;
        _revealTime = -1f;
        _hasLine = false;
        _lines = 0;
        if (_weight != null) Array.Clear(_weight, 0, _weight.Length);

        SpectrumHandoff_NEW.EndLookbackGyr = _lookback;
        SpectrumHandoff_NEW.FirstLineZ = float.NaN;
    }

    void HandleBeat(TutorialBeat_NEW beat)
    {
        if (!_active || beat == null) return;
        string id = beat.BeatId;
        if (Is(id, zoomInBeat)) ZoomIn();
        if (Is(id, zoomOutBeat)) ZoomOut();
        if (Is(id, revealBeat)) RevealForest();
    }

    static bool Is(string id, string want)
    {
        return !string.IsNullOrEmpty(want) && string.Equals(id, want, StringComparison.OrdinalIgnoreCase);
    }

    void StartZoom(float to)
    {
        if (!_active) return;
        _zoomFrom = _zoom;
        _zoomTo = to;
        _zoomTime = 0f;
    }

    // ── Drawing ──────────────────────────────────────────────────────────────

    void Draw()
    {
        double fullMin = Math.Log(_data.LambdaMin), fullMax = Math.Log(_data.LambdaMax);
        double zMin = LnBirth - zoomLeftShare * zoomWidthLn, zMax = LnBirth + (1.0 - zoomLeftShare) * zoomWidthLn;
        _lnMin = fullMin + (zMin - fullMin) * _zoom;
        _lnMax = fullMax + (zMax - fullMax) * _zoom;

        bool flux = curve == Curve.Flux;
        _data.SampleView(_zNow, Math.Exp(_lnMin), Math.Exp(_lnMax), flux ? _flux : null, _transmission,
                         _revealAll >= 1f ? null : _weightFn);

        for (int c = 0; c < columns; c++) _curve[c] = flux ? _flux[c] : _transmission[c];

        if (hud != null) hud.MarkExternalCurveDirty();

        // The field feeds the trail. Following the bar it is the bar's own transmission;
        // on the all-bands axis it is sampled segment by segment on the ribbon's axis.
        if (trailAxis == TrailAxis.FollowBar)
        {
            for (int c = 0; c < columns; c++) _absorption[c] = 1f - _transmission[c];
        }
        else
        {
            DrawTrailSegments();
        }
        if (field != null) field.MarkExternalAbsorptionDirty();

        if (driveBands && spectrumTrail != null)
        {
            spectrumTrail.uvBandWidth = Mathf.Clamp01(TrailT(Math.Log(4000.0)));
            spectrumTrail.irBandWidth = Mathf.Clamp01(1f - TrailT(Math.Log(7000.0)));
        }

        if (driveTrailColours) UpdateTrailTexture();
    }

    // ── The ribbon's axis ────────────────────────────────────────────────────

    readonly double[] _segLn = new double[6];
    readonly float[] _segT = new float[6];
    float[] _segBuffer;
    const int Segments = 5;

    /// <summary>Fill the piecewise axis: ln λ break points and where they sit on the ribbon.</summary>
    void BuildTrailAxis()
    {
        float s0 = farUvShare, s1 = lineRegionShare, s2 = uvRestShare, s3 = visibleShare;
        float sum = s0 + s1 + s2 + s3;
        if (sum > 0.95f) { float k = 0.95f / sum; s0 *= k; s1 *= k; s2 *= k; s3 *= k; }

        _segLn[0] = Math.Log(1000.0);
        _segLn[1] = LnBirth - zoomLeftShare * zoomWidthLn;
        _segLn[2] = Math.Log(Mathf.Clamp(lineRegionEndA, 1250f, 3990f));
        _segLn[3] = Math.Log(4000.0);
        _segLn[4] = Math.Log(7000.0);
        _segLn[5] = Math.Log(_data.LambdaMax);
        _segT[0] = 0f; _segT[1] = s0; _segT[2] = s0 + s1; _segT[3] = s0 + s1 + s2; _segT[4] = s0 + s1 + s2 + s3; _segT[5] = 1f;
    }

    /// <summary>Ribbon position (0 UV edge, 1 IR edge) of a wavelength.</summary>
    float TrailT(double lnLambda)
    {
        if (trailAxis == TrailAxis.FollowBar) return AxisT(lnLambda);
        BuildTrailAxis();
        if (lnLambda <= _segLn[0]) return 0f;
        for (int i = 0; i < Segments; i++)
            if (lnLambda <= _segLn[i + 1])
                return _segT[i] + (_segT[i + 1] - _segT[i]) * (float)((lnLambda - _segLn[i]) / (_segLn[i + 1] - _segLn[i]));
        return 1f;
    }

    /// <summary>Wavelength (ln) at a ribbon position — the inverse of TrailT.</summary>
    double TrailLn(float t)
    {
        if (trailAxis == TrailAxis.FollowBar) return _lnMin + (_lnMax - _lnMin) * t;
        BuildTrailAxis();
        for (int i = 0; i < Segments; i++)
            if (t <= _segT[i + 1] || i == Segments - 1)
                return _segLn[i] + (_segLn[i + 1] - _segLn[i]) * Mathf.Clamp01((t - _segT[i]) / Mathf.Max(1e-6f, _segT[i + 1] - _segT[i]));
        return _segLn[Segments];
    }

    void DrawTrailSegments()
    {
        BuildTrailAxis();
        Func<int, float> w = _revealAll >= 1f ? null : _weightFn;
        int start = 0;
        for (int i = 0; i < Segments; i++)
        {
            int end = i == Segments - 1 ? columns : Mathf.RoundToInt(_segT[i + 1] * columns);
            int n = end - start;
            if (n < 2) { for (int c = start; c < end; c++) _absorption[c] = 0f; start = end; continue; }

            // Column centres at the segment's own edges, matching SampleView's layout.
            double a = _segLn[i] + (_segLn[i + 1] - _segLn[i]) * 0.5 / n;
            double b = _segLn[i + 1] - (_segLn[i + 1] - _segLn[i]) * 0.5 / n;
            if (_segBuffer == null || _segBuffer.Length != n) _segBuffer = new float[n];
            _data.SampleView(_zNow, Math.Exp(a), Math.Exp(b), null, _segBuffer, w);
            for (int c = 0; c < n; c++) _absorption[start + c] = 1f - _segBuffer[c];
            start = end;
        }
    }

    float CellWeight(int q)
    {
        float w = q >= 0 && q < _weight.Length ? _weight[q] : 0f;
        return w > _revealAll ? w : _revealAll;
    }

    int Deepest(int now, int back)
    {
        int best = now;
        float bestT = 2f;
        for (int q = now; q >= 0 && q >= now - back; q--)
        {
            // A line an earlier atom already cut is not this atom's: every atom gets its own.
            if (_weight[q] > 0f) continue;
            float t = _data.RecordAt(q);
            if (t < bestT) { bestT = t; best = q; }
        }
        return best;
    }

    float AxisT(double lnLambda)
    {
        return (float)((lnLambda - _lnMin) / (_lnMax - _lnMin));
    }

    static float Smooth(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    // ── Trail colours: real wavelengths on the bar's axis ────────────────────

    void UpdateTrailTexture()
    {
        if (_trail == null) return;
        double k0 = TrailLn(0f), k1 = TrailLn(0.5f);
        if (Math.Abs(k0 - _texLnMin) < 1e-5 && Math.Abs(k1 - _texLnMax) < 1e-5) return;
        _texLnMin = k0;
        _texLnMax = k1;

        TrailSpectrumColours_NEW.Ensure(ref _trailTex, ref _trailPixels, trailTextureWidth, "TutorialBakedSpectrum_Trail");

        Color uv = spectrumTrail.uvColor * Mathf.Max(spectrumTrail.uvBrightness, uvAlpha);
        uv.a = uvAlpha;
        Color ir = spectrumTrail.irColor * spectrumTrail.irBrightness;
        ir.a = spectrumTrail.irBrightness;

        TrailSpectrumColours_NEW.Fill(_trailPixels, t => Math.Exp(TrailLn(t)), uv, ir);
        _trailTex.SetPixels32(_trailPixels);
        _trailTex.Apply(false);

        if (_block == null) _block = new MaterialPropertyBlock();
        _trail.GetPropertyBlock(_block);
        _block.SetTexture(SpectrumTexId, _trailTex);
        _trail.SetPropertyBlock(_block);
    }

    // ── Debug overlay ────────────────────────────────────────────────────────

    void OnGUI()
    {
        if (!_active || !DebugView_NEW.Overlay) return;

        GUI.Label(DebugOverlayRows_NEW.Row(DebugOverlayRows_NEW.Pad + 1),
                  string.Format("TUT DATA  lookback {0:0.000} Gyr ({1:0.0}% of journey)   z {2:0.0000}   zoom {3:0.00}   " +
                                "atoms {4}   forest {5:0.00}   bar {6:0}–{7:0} Å",
                                _lookback, 100f * (_data.LookbackStartGyr - _lookback) / _data.LookbackStartGyr,
                                _zNow, _zoom, _lines, _revealAll, Math.Exp(_lnMin), Math.Exp(_lnMax)));
    }
}

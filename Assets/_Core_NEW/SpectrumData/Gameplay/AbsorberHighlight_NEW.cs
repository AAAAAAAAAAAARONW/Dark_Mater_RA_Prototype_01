using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// The tutorial's line highlight, in the journey: when the light crosses a marked absorber,
/// its line — born that moment at the birth tick — blinks on the bar AND on the trail, and an
/// arrow on each follows it redward for a few seconds. "That gas you just passed = this line."
///
/// WHEN. Only when the piece is explaining redshift: the demo's steps call it
/// (JourneySequence_NEW onEnter → HighlightFirstLine / HighlightMark / StopHighlight). A
/// highlight that just went off by itself during the flight read as odd, so that is now an
/// option (autoOnCrossings, off): the light crossing one of the bake's notable absorbers.
///
/// A step's highlight follows the line wherever the bar shows it — including through the
/// demo's replay (Spread), where it is born at the anchor and stretched to where it is now.
///
/// ISOLATION. Reads BakedSpectrumSource_NEW; writes only its own runtime graphics and the
/// source's SetTrailEmphasis hook. Untick highlightEnabled and nothing is drawn or changed.
/// </summary>
[DefaultExecutionOrder(70)]
[DisallowMultipleComponent]
[HierarchyBadge_NEW("LINE HILITE", "#FFE8A0")]
public class AbsorberHighlight_NEW : MonoBehaviour
{
    [Header("Switch")]
    [SerializeField] bool highlightEnabled = true;

    [Header("Wiring (found in the scene if empty)")]
    [SerializeField] BakedSpectrumSource_NEW source;

    [Tooltip("The object the bar highlight is drawn under. Default: RedshiftMarks_NEW's object " +
             "(it is outside the bar's mask, so the arrow below the bar is not clipped).")]
    [SerializeField] RectTransform host;

    [Tooltip("The curve to line up with. Default: the SpectrumHUD_NEW's own rect.")]
    [SerializeField] RectTransform alignTo;

    [SerializeField] Camera viewCamera;

    [Header("Timing")]
    [Min(1)]
    [SerializeField] int blinkCount = 3;
    [SerializeField] float blinkSeconds = 1.8f;

    [Tooltip("How long the arrows follow the line.")]
    [SerializeField] float followSeconds = 5f;

    [SerializeField] float fadeSeconds = 0.35f;

    [Tooltip("How long a highlight started by a demo step stays on (the arrows keep following " +
             "the line). 8 = the length of the demo's F3 / F4 steps. StopHighlight ends it early.")]
    [SerializeField] float stepSeconds = 8f;

    [Header("Redshift demo (automatic, no wiring)")]
    [Tooltip("The redshift demo. Found in the scene if empty. As each of its steps begins, a step listed " +
             "below highlights the FIRST line (the player's own) for that step's duration.")]
    [SerializeField] JourneySequence_NEW sequence;

    [Tooltip("Comma-separated step ids that highlight the first line. F3 = \"where the first line was vs " +
             "now\", F4 = the replay (Spread), followed from its birth to where it is now.")]
    [SerializeField] string firstLineSteps = "F3,F4";

    [Tooltip("Also mark the anchor (the birth tick, where the line was born) and draw a bracket under " +
             "the bar from the anchor to the line: how far it has travelled.")]
    [SerializeField] bool showDistance = true;

    [Header("Automatic (off by default)")]
    [Tooltip("Also highlight by itself whenever the flying light crosses one of the bake's notable " +
             "absorbers. Off: only the redshift demo's steps highlight.")]
    [SerializeField] bool autoOnCrossings = false;

    [Tooltip("A crossing sooner than this after the last highlight began is skipped.")]
    [SerializeField] float minSecondsBetween = 4f;

    [Header("Trail arrow")]
    [Tooltip("Metres behind the light where the arrow points into the ribbon.")]
    [SerializeField] float pointBehindLight = 0.6f;

    [Tooltip("Pixels above that point where the arrow's tip sits.")]
    [SerializeField] float trailArrowGap = 30f;

    [Tooltip("Tick if the trail arrow points at the opposite edge of the ribbon from the line " +
             "(which edge is UV depends on the camera; it has to be looked at — same as the " +
             "tutorial's flipAcross).")]
    [SerializeField] bool flipAcross = false;

    [Tooltip("Mirror the trail if needed so that, on screen, its UV edge is on the LEFT like the bar and " +
             "lines move left to right. Read from the mesh Unity draws; decided early in the flight and " +
             "again at each highlight.")]
    [SerializeField] bool autoOrientTrail = true;

    [Header("Look")]
    [SerializeField] Color color = new Color(1f, 0.91f, 0.63f, 1f);

    [Range(0f, 1f)]
    [SerializeField] float columnOpacity = 0.55f;

    [Header("Debug")]
    [Tooltip("Highlight the line being born right now. Editor and development builds only.")]
    [SerializeField] KeyCode debugKey = KeyCode.F12;
    [SerializeField] bool debugLog = true;

    AbsorberHighlightGraphic_NEW _bar;
    AbsorberHighlightGraphic_NEW _trailArrow;
    RectTransform _canvasRect;
    Canvas _canvas;

    float _prevZ = float.NaN;
    bool _running;
    float _elapsed;
    float _lineZ;
    float _duration;
    bool _manual;
    bool _showSpan;
    bool _fromSequence;
    Vector3 _prevLightPos;
    Vector3 _travelDir;
    readonly Vector3[] _corners = new Vector3[4];
    Mesh _ribbonMesh;
    float _ribbonSign = 1f;
    float _ribbonWidth = -1f;
    float _nextRibbonRead;
    Vector2 _arrowTip;
    bool _arrowValid;
    bool _oriented;

    /// <summary>
    /// Highlight the first line — the one the player's own atom cut in the tutorial (mark [0]).
    /// For the demo's "where the first line was vs now" step.
    /// </summary>
    public void HighlightFirstLine() { HighlightMark(0); }

    /// <summary>The same, for a given number of seconds (a step longer or shorter than stepSeconds).</summary>
    public void HighlightFirstLineFor(float seconds)
    {
        HighlightMark(0);
        if (_running && _manual) _duration = Mathf.Max(0.5f, seconds);
    }

    /// <summary>Highlight marked line i (RedshiftMarks_NEW's order) wherever it is now.</summary>
    public void HighlightMark(int index)
    {
        if (source == null || !source.IsActive) return;
        var marks = source.MarkZ;
        if (index < 0 || index >= marks.Count) return;
        Begin(marks[index], "mark " + index, true);
    }

    /// <summary>Highlight the line being born at this moment (for a cloud trigger, a sequence step…).</summary>
    public void HighlightNow()
    {
        if (source != null && source.IsActive) Begin(source.CurrentZ, "now", true);
    }

    /// <summary>End the highlight now (fades nothing out: for a step that moves on).</summary>
    public void StopHighlight() { Stop(); }

    void Awake()
    {
        if (source == null) source = FindObjectOfType<BakedSpectrumSource_NEW>();
        if (sequence == null) sequence = FindObjectOfType<JourneySequence_NEW>();
        if (viewCamera == null) viewCamera = Camera.main;
    }

    void Start()
    {
        if (host == null)
        {
            RedshiftMarks_NEW marks = FindObjectOfType<RedshiftMarks_NEW>();
            if (marks != null) host = marks.transform as RectTransform;
        }
        if (alignTo == null)
        {
            SpectrumHUD_NEW hud = FindObjectOfType<SpectrumHUD_NEW>();
            if (hud != null) alignTo = hud.transform as RectTransform;
        }
        if (host == null && alignTo != null) host = alignTo.parent as RectTransform;

        if (host == null)
        {
            Debug.LogWarning("[AbsorberHighlight_NEW] No HUD found to draw on — highlight off.", this);
            enabled = false;
            return;
        }

        _bar = Make("Absorber Highlight (bar)", host, AbsorberHighlightGraphic_NEW.Kind.Bar);

        _canvas = host.GetComponentInParent<Canvas>();
        if (_canvas != null) _canvas = _canvas.rootCanvas;
        _canvasRect = _canvas != null ? _canvas.transform as RectTransform : null;
        if (_canvasRect != null) _trailArrow = Make("Absorber Highlight (trail arrow)", _canvasRect, AbsorberHighlightGraphic_NEW.Kind.TrailArrow);
    }

    AbsorberHighlightGraphic_NEW Make(string name, RectTransform parent, AbsorberHighlightGraphic_NEW.Kind kind)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.SetAsLastSibling();

        AbsorberHighlightGraphic_NEW g = go.AddComponent<AbsorberHighlightGraphic_NEW>();
        g.kind = kind;
        g.raycastTarget = false;
        g.arrowColor = color;
        Color col = color;
        col.a = columnOpacity;
        g.columnColor = col;
        return g;
    }

    void OnDestroy()
    {
        if (_ribbonMesh != null) Destroy(_ribbonMesh);
    }

    void OnEnable()
    {
        if (sequence != null) sequence.OnStepEntered += HandleStep;
    }

    void OnDisable()
    {
        if (sequence != null) sequence.OnStepEntered -= HandleStep;
        Stop();
    }

    /// <summary>The demo moved to a new step: highlight the first line if it is one of ours, else end ours.</summary>
    void HandleStep(JourneySequence_NEW.Step step)
    {
        if (!highlightEnabled || step == null) return;

        bool ours = false;
        foreach (string id in (firstLineSteps ?? "").Split(','))
            if (string.Equals(id.Trim(), step.id, System.StringComparison.OrdinalIgnoreCase)) ours = true;

        if (ours)
        {
            HighlightFirstLineFor(step.duration);
            _showSpan = showDistance;
            _fromSequence = true;
        }
        else if (_running && _fromSequence)
        {
            Stop();
        }
    }

    void Update()
    {
        if (_bar == null) return;
        if (!highlightEnabled || source == null || !source.IsActive) { Stop(); _prevZ = float.NaN; return; }

        TrackLight();
        if (!_oriented && source.IsActive && source.Trail != null && source.Trail.positionCount > 8) OrientTrail("start of flight");

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugKey != KeyCode.None && Input.GetKeyDown(debugKey)) HighlightNow();
#endif

        // Crossings (optional): the light passed a marked absorber's redshift since last frame.
        if (autoOnCrossings && source.IsFollowingJourney)
        {
            float z = source.CurrentZ;
            if (!float.IsNaN(_prevZ) && z < _prevZ)
            {
                var marks = source.MarkZ;
                for (int i = 0; i < marks.Count; i++)
                    if (marks[i] < _prevZ && marks[i] >= z) Begin(marks[i], "mark " + i, false);
            }
            _prevZ = z;
        }
        else
        {
            _prevZ = float.NaN;
            // An automatic highlight belongs to the plain flight; a step's carries on through
            // the demo's replay.
            if (_running && !_manual) Stop();
        }

        if (_running) Tick(Time.deltaTime);
    }

    void Begin(float zLine, string why, bool manual)
    {
        if (!manual && _running && _elapsed < minSecondsBetween)
        {
            if (debugLog) Debug.Log("[AbsorberHighlight_NEW] Skipped " + why + " (z " + zLine.ToString("0.0000") + "): the last highlight is still on.", this);
            return;
        }

        OrientTrail("highlight");

        _running = true;
        _manual = manual;
        _showSpan = false;
        _fromSequence = false;
        _duration = manual ? stepSeconds : followSeconds;
        _elapsed = 0f;
        _lineZ = zLine;
        if (debugLog) Debug.Log("[AbsorberHighlight_NEW] Highlight " + why + ": the line of gas at z " + zLine.ToString("0.0000") +
                                " (light at z " + source.CurrentZ.ToString("0.0000") + ").", this);
    }

    void Stop()
    {
        if (_running && source != null) source.ClearTrailEmphasis();
        _running = false;
        Apply(float.NaN, 0f, 0f);
    }

    void Tick(float dt)
    {
        _elapsed += dt;
        float barT = source.BarLineT(_lineZ);
        if (_elapsed >= _duration || barT > 1f) { Stop(); return; }

        // Not on the bar (yet): early in the demo's replay the light has not reached that gas.
        // A step's highlight waits for it; an automatic one has nothing to show.
        if (float.IsNaN(barT))
        {
            if (!_manual) { Stop(); return; }
            source.ClearTrailEmphasis();
            Apply(float.NaN, 0f, 0f);
            return;
        }

        float blink = Pulse(_elapsed, blinkSeconds, blinkCount);
        float fade = fadeSeconds <= 0f ? 1f : Mathf.Clamp01(Mathf.Min(_elapsed, _duration - _elapsed) / fadeSeconds);

        source.SetTrailEmphasis(barT, blink);
        Apply(barT, blink, fade);
    }

    void Apply(float barT, float columnAlpha, float arrowAlpha)
    {
        if (_bar != null)
        {
            Rect band = Band();
            _bar.band = band;
            _bar.x = float.IsNaN(barT) ? band.xMin : Mathf.Clamp(band.xMin + barT * band.width, band.xMin + 1f, band.xMax - 1f);
            _bar.columnAlpha = columnAlpha;
            _bar.arrowAlpha = arrowAlpha;
            _bar.spanAlpha = _showSpan && !float.IsNaN(barT) ? arrowAlpha : 0f;
            _bar.anchorX = band.xMin + Mathf.Clamp01(source != null ? source.BirthBarT : 0f) * band.width;
            _bar.Refresh();
        }

        if (_trailArrow != null)
        {
            Vector2 tip = Vector2.zero;
            bool ok = !float.IsNaN(barT) && arrowAlpha > 0f && TrailPoint(out tip);
            _trailArrow.arrowAlpha = ok ? arrowAlpha : 0f;
            if (ok)
            {
                // Eased on screen (~12/s), so frame-to-frame noise in the light's heading does not shake it.
                _arrowTip = _arrowValid ? Vector2.Lerp(_arrowTip, tip, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime)) : tip;
                _arrowValid = true;
                _trailArrow.tip = _arrowTip;
            }
            else _arrowValid = false;
            _trailArrow.Refresh();
        }
    }

    /// <summary>The curve's drawing band in the host's local space (as RedshiftMarks_NEW does).</summary>
    Rect Band()
    {
        Rect own = _bar.rectTransform.rect;
        if (alignTo == null) return own;

        alignTo.GetWorldCorners(_corners);
        Vector3 a = _bar.rectTransform.InverseTransformPoint(_corners[0]);
        Vector3 b = _bar.rectTransform.InverseTransformPoint(_corners[2]);
        Rect r = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));

        LineGraphRenderer_NEW graph = alignTo.GetComponent<LineGraphRenderer_NEW>();
        if (graph != null)
        {
            float h = r.height * graph.GraphHeightPercent;
            float y0 = graph.AnchorTop ? r.yMax - h : r.yMin;
            r = new Rect(r.xMin, y0, r.width, h);
        }
        return r;
    }

    void TrackLight()
    {
        TrailRenderer trail = source.Trail;
        if (trail == null) return;
        Vector3 p = trail.transform.position;
        Vector3 d = p - _prevLightPos;
        if (d.sqrMagnitude > 1e-8f) _travelDir = Vector3.Lerp(_travelDir, d.normalized, 0.2f);
        _prevLightPos = p;
    }

    /// <summary>Where the line is on the ribbon, as a point on the canvas (same geometry as the tutorial's arrow).</summary>
    bool TrailPoint(out Vector2 local)
    {
        local = Vector2.zero;
        TrailRenderer trail = source.Trail;
        if (trail == null || viewCamera == null || _canvasRect == null || !trail.gameObject.activeInHierarchy) return false;

        float u = source.TrailLineT(_lineZ);
        if (float.IsNaN(u)) return false;

        // Smooth geometry for the position: a point a little behind the light, across the ribbon.
        Vector3 behind = _travelDir.sqrMagnitude > 1e-4f ? -_travelDir.normalized : -trail.transform.forward;
        Vector3 target = trail.transform.position + behind * pointBehindLight;
        Vector3 across = Vector3.Cross(behind, viewCamera.transform.position - target).normalized;
        if (across.sqrMagnitude < 1e-4f) return false;

        // The drawn mesh only says WHICH SIDE is uv.y = 0 and how wide the ribbon is — read a few
        // times a second and smoothed. Snapping to its vertices made the arrow shake, because the
        // nearest vertex jumps every time the trail adds a point.
        if (Time.unscaledTime >= _nextRibbonRead)
        {
            _nextRibbonRead = Time.unscaledTime + 0.25f;
            Vector3 e0, e1;
            if (RibbonEdges(trail, out e0, out e1))
            {
                Vector3 d = e1 - e0;
                float s = Vector3.Dot(d, across);
                if (Mathf.Abs(s) > 1e-4f) _ribbonSign = Mathf.Sign(s);
                float w = d.magnitude;
                _ribbonWidth = _ribbonWidth < 0f ? w : Mathf.Lerp(_ribbonWidth, w, 0.3f);
            }
        }
        float width = _ribbonWidth > 0f ? _ribbonWidth : trail.widthMultiplier * trail.widthCurve.Evaluate(0f);

        // specT = 1 − uv.y, or uv.y when the ribbon is mirrored.
        float v = source.TrailMirrored ? u : 1f - u;
        if (flipAcross) v = 1f - v;
        Vector3 point = target + across * (_ribbonSign * (v - 0.5f) * width);

        Vector3 screen = viewCamera.WorldToScreenPoint(point);
        if (screen.z <= 0f) return false;

        Camera uiCam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, uiCam, out local)) return false;

        // The graphic fills the canvas rect, so its local space is the canvas's.
        local += new Vector2(0f, trailArrowGap);
        return true;
    }

    /// <summary>The ribbon's two edges a little behind the light, from the mesh Unity draws.</summary>
    bool RibbonEdges(TrailRenderer trail, out Vector3 e0, out Vector3 e1)
    {
        if (_ribbonMesh == null) _ribbonMesh = new Mesh { name = "AbsorberHighlight_TrailBake" };
        Vector3 behind = _travelDir.sqrMagnitude > 1e-4f ? -_travelDir.normalized : -trail.transform.forward;
        Vector3 target = trail.transform.position + behind * pointBehindLight;
        return TrailRibbon_NEW.Edges(trail, viewCamera, _ribbonMesh, target, out e0, out e1);
    }

    /// <summary>
    /// Mirror the ribbon if needed so that on screen its UV edge is on the LEFT, like the bar, and
    /// a line moves left to right on both. Decided from the drawn mesh.
    /// </summary>
    void OrientTrail(string why)
    {
        if (!autoOrientTrail || source == null || source.Trail == null || viewCamera == null) return;
        Vector3 e0, e1;
        if (!RibbonEdges(source.Trail, out e0, out e1)) return;

        // Unmirrored, wavelength grows from the uv.y = 1 edge towards the uv.y = 0 edge.
        float dx = viewCamera.WorldToScreenPoint(e0).x - viewCamera.WorldToScreenPoint(e1).x;
        if (Mathf.Abs(dx) < 3f) return;   // ribbon edge-on to the screen: no clear left/right
        bool mirror = dx < 0f;
        _oriented = true;
        if (mirror != source.TrailMirrored)
        {
            source.SetTrailMirrored(mirror);
            if (debugLog) Debug.Log("[AbsorberHighlight_NEW] Trail " + (mirror ? "mirrored" : "unmirrored") +
                                    " so UV is on screen-left like the bar (" + why + ").", this);
        }
    }

    static float Pulse(float elapsed, float seconds, int count)
    {
        if (elapsed < 0f || seconds <= 0f || elapsed > seconds) return 0f;
        float t = Mathf.Clamp01(elapsed / seconds);
        return 0.5f - 0.5f * Mathf.Cos(t * count * Mathf.PI * 2f);
    }
}

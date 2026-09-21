using System.Text;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Blacks out the display row that a UI element sits on, edge to edge, and keeps doing so
/// while the element is moved from row to row.
///
/// Built for Option 2 of the LAF placement study. The LAF HUD takes the floor row, that row
/// carries no imagery, so the graph reads against black and covers nothing. Whether row 7
/// actually works on site is the open question, so the band follows the HUD wherever the
/// playtest moves it. That lets the same scene answer "what if it were on row 6" without an
/// edit.
///
/// HOW IT FOLLOWS: it watches a VizlabRowAnchor_NEW (the LAF HUD's) and re-places itself
/// whenever that anchor's Row changes. It moves nothing itself. Stepping rows is still
/// VizlabRowShifter_NEW's job (RB / LB on the pad, Page Up / Page Down, Home to reset), so
/// that workflow is unchanged and the band simply tracks it. Reading one int per frame is
/// cheaper and less invasive than adding an event to the anchor. Placement runs in
/// LateUpdate, after the shifter's Update, so the band and the HUD move in the same frame.
///
/// WHY IT SITS ON ITS OWN CANVAS: the band belongs on a separate Screen Space - Overlay
/// canvas with a negative sort order. Overlay canvases always draw over the 3D camera
/// output, and draw among themselves by sort order. So the band hides the world but stays
/// beneath every HUD canvas (the LAF, the journey bar, the milestone cards) instead of
/// blacking those out too. It also keeps the band out of the LAF canvas, which rebuilds
/// every frame while the spectrum scrolls.
///
/// EXTENT
///   Row          just the element's row, full width. This is Option 2 when the row is 7.
///   RowAndBelow  from the element's row down to the bottom edge, which is Option 4B's
///                shape: the imagery stops above the HUD. On row 7 the two are the same.
///
/// DEBUG. All of these can be rebound, and all can be turned off for a build:
///   B  / pad View (button 6)   toggle the blackout
///   N  / pad Menu (button 7)   cycle the extent
///   F4                         show or hide the readout (F1 hides every debug readout)
/// Every change is logged with the row's tilt, its angle from a standing eye, and the band
/// in canvas pixels and in millimetres off the floor. Those are the numbers the playtest is
/// meant to settle. The same actions are in the component's context menu, and they are
/// public, so a UI button or another script can call them.
///
/// Buttons 0 to 3 are taken by Fire1-3, Jump, Submit and Cancel, and 4 and 5 by the row
/// shifter. Nothing reads 6 to 9, and on Windows Unity reads them through KeyCode with no
/// Input Manager entry.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
[HierarchyBadge_NEW("BLACKOUT", "#C08A2E")]
public class VizlabRowBlackout_NEW : MonoBehaviour
{
    public enum Extent
    {
        /// <summary>Only the followed element's row, full width.</summary>
        Row,
        /// <summary>The followed element's row and everything below it, to the bottom edge.</summary>
        RowAndBelow,
    }

    [Header("What to follow")]
    [Tooltip("The anchor whose row gets blacked out, which is the LAF HUD's. If left empty, " +
             "the anchor of the scene's VizlabRowShifter_NEW is used.")]
    [SerializeField] VizlabRowAnchor_NEW follow;

    [Tooltip("The shifter that moves it. Only the debug methods that step rows use this, " +
             "so that they take the same path as RB / LB. Found on the followed object if " +
             "left empty.")]
    [SerializeField] VizlabRowShifter_NEW shifter;

    [Tooltip("The row to black out when nothing is being followed.")]
    [Range(1, VizlabDisplay_NEW.RowCount)]
    [SerializeField] int fallbackRow = VizlabDisplay_NEW.RowCount;

    [Header("Blackout")]
    [Tooltip("Whether the band is up when the scene starts.")]
    [SerializeField] bool blackoutOnStart = true;

    [SerializeField] Extent extent = Extent.Row;

    [Tooltip("The band's colour. Pure black by default. Lower the alpha to see the world " +
             "through it while judging where the band should end.")]
    [SerializeField] Color colour = Color.black;

    [Tooltip("Seconds to fade in or out when toggled. Row changes always snap, because the " +
             "HUD itself snaps.")]
    [Range(0f, 2f)]
    [SerializeField] float fadeSeconds = 0.25f;

    [Header("Debug controls")]
    [SerializeField] KeyCode toggleKey = KeyCode.B;

    [Tooltip("View / Back on an Xbox pad.")]
    [SerializeField] KeyCode togglePadButton = KeyCode.JoystickButton6;

    [SerializeField] KeyCode extentKey = KeyCode.N;

    [Tooltip("Menu / Start on an Xbox pad.")]
    [SerializeField] KeyCode extentPadButton = KeyCode.JoystickButton7;

    [SerializeField] KeyCode overlayKey = KeyCode.F4;

    [Tooltip("On by default, like the row shifter, because the playtest runs as a build on " +
             "the wall. Turn it off to strip the keys and the readout from a build. The " +
             "blackout itself always runs.")]
    [SerializeField] bool debugControlsInBuild = true;

    [Header("Debug output")]
    [Tooltip("Log every row change, toggle and extent change to the console.")]
    [SerializeField] bool logChanges = true;

    [Tooltip("The on-screen readout in the top-right corner. It also respects " +
             "DebugView_NEW.Overlay, so F1 hides it along with every other debug readout.")]
    [SerializeField] bool showOverlay = true;

    [SerializeField] Vector2 overlayMargin = new Vector2(12f, 12f);

    [Tooltip("Raise this on the wall itself. At 9600 x 7560 the default is tiny.")]
    [SerializeField] int fontSize = 13;

    const string LogPrefix = "[VizlabRowBlackout_NEW] ";
    const float ActionFadeSeconds = 2.5f;

    Image _band;
    DrivenRectTransformTracker _tracker;

    /// <summary>The row the band was last placed on. -1 forces a re-place.</summary>
    int _placedRow = -1;
    Extent _placedExtent;
    Vector2 _placedCanvasSize;

    bool _on;
    float _alpha;
    bool _announced;

    string _lastAction = "";
    float _lastActionTime = -999f;

    GUIStyle _panel, _label;
    Texture2D _panelTex;
    readonly StringBuilder _sb = new StringBuilder(512);

    // ── Public surface ───────────────────────────────────────────────────────

    /// <summary>The row being blacked out, 1 to 7: the followed anchor's row, or the fallback.</summary>
    public int CurrentRow { get { return follow != null ? follow.Row : fallbackRow; } }

    /// <summary>True while the band is up or fading in.</summary>
    public bool IsBlackedOut { get { return _on; } }

    public Extent CurrentExtent { get { return extent; } }

    public VizlabRowAnchor_NEW Following { get { return follow; } }

    /// <summary>Points the band at a different element. Used by the Vizlab editor menu.</summary>
    public void SetFollow(VizlabRowAnchor_NEW anchor)
    {
        follow = anchor;
        shifter = anchor != null ? anchor.GetComponentInParent<VizlabRowShifter_NEW>() : null;
        _placedRow = -1;
    }

    [ContextMenu("Blackout: toggle")]
    public void ToggleBlackout()
    {
        SetBlackout(!_on);
    }

    [ContextMenu("Blackout: on")]
    public void BlackoutOn()
    {
        SetBlackout(true);
    }

    [ContextMenu("Blackout: off")]
    public void BlackoutOff()
    {
        SetBlackout(false);
    }

    public void SetBlackout(bool on)
    {
        if (_on == on) return;
        _on = on;

        if (!Application.isPlaying || fadeSeconds <= 0f)
        {
            _alpha = on ? 1f : 0f;
            ApplyColour();
        }

        Report("blackout " + (on ? "ON" : "OFF") + " on " + Describe(CurrentRow, extent));
    }

    [ContextMenu("Extent: cycle")]
    public void CycleExtent()
    {
        SetExtent(extent == Extent.Row ? Extent.RowAndBelow : Extent.Row);
    }

    public void SetExtent(Extent value)
    {
        if (extent == value) return;
        extent = value;
        Place(CurrentRow);
        Report("extent -> " + value + ": " + Describe(CurrentRow, extent));
    }

    /// <summary>Up the display, toward row 1. Goes through the shifter when there is one.</summary>
    [ContextMenu("Row: step up (toward row 1)")]
    public void StepUp()
    {
        Step(-1);
    }

    /// <summary>Down the display, toward the floor row.</summary>
    [ContextMenu("Row: step down (toward row 7)")]
    public void StepDown()
    {
        Step(1);
    }

    [ContextMenu("Row: back to the shifter's starting row")]
    public void ResetRow()
    {
        if (ShifterLive) shifter.ResetToStartRow();
        else Report("reset needs a VizlabRowShifter_NEW in play mode, where it resolves its " +
                    "starting row, so the row is unchanged");
    }

    /// <summary>Jumps the followed element, and so the band, to a row.</summary>
    public void GoToRow(int rowNumber)
    {
        rowNumber = Mathf.Clamp(rowNumber, 1, VizlabDisplay_NEW.RowCount);

        if (ShifterLive) shifter.GoToRow(rowNumber);
        else if (follow != null) follow.Row = rowNumber;
        else fallbackRow = rowNumber;
    }

    [ContextMenu("Re-apply placement")]
    public void Refresh()
    {
        ResolveReferences();
        _placedRow = -1;
        Place(CurrentRow);
        ApplyColour();
    }

    /// <summary>Everything the band knows, in one console entry. For pasting into a playtest note.</summary>
    [ContextMenu("Log state")]
    public void LogState()
    {
        ResolveReferences();

        Debug.Log(LogPrefix + "state\n" +
                  "  following   " + (follow != null ? follow.name : "nothing (fallback row " + fallbackRow + ")") + "\n" +
                  "  shifter     " + (shifter != null ? shifter.name : "none") + "\n" +
                  "  blackout    " + (_on ? "ON" : "OFF") + "   alpha " + _alpha.ToString("0.00") + "\n" +
                  "  extent      " + extent + "\n" +
                  "  band        " + Describe(CurrentRow, extent) + "\n" +
                  "  anchors     " + ((RectTransform)transform).anchorMin.ToString("F3") +
                  " - " + ((RectTransform)transform).anchorMax.ToString("F3") + "\n" +
                  "  canvas      " + DescribeCanvas(),
                  this);
    }

    /// <summary>
    /// The band as fractions of canvas height, 0 at the bottom, in the form Unity's anchors
    /// want. Static so an editor tool can ask without an instance.
    /// </summary>
    public static void GetBandFractions(int row, Extent extent, out float yMin, out float yMax)
    {
        VizlabDisplay_NEW.GetRowAnchorFractions(row, out yMin, out yMax);
        if (extent == Extent.RowAndBelow) yMin = 0f;
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Reset()
    {
        // A band that swallowed clicks would be a nasty surprise on a canvas with buttons.
        Image image = GetComponent<Image>();
        if (image != null)
        {
            image.raycastTarget = false;
            image.color = colour;
        }
    }

    void OnEnable()
    {
        ResolveReferences();

        _on = blackoutOnStart;
        _alpha = _on ? 1f : 0f;
        _placedRow = -1;
        _announced = false;

        Place(CurrentRow);
        ApplyColour();
    }

    void OnDisable()
    {
        _tracker.Clear();

        // The band is part of the scene, so switching the component off should not strand
        // a black bar on screen with nothing managing it.
        if (_band != null) _band.enabled = false;
    }

    void OnDestroy()
    {
        if (_panelTex == null) return;
        if (Application.isPlaying) Destroy(_panelTex);
        else DestroyImmediate(_panelTex);
    }

    void Update()
    {
        if (!Application.isPlaying || !DebugControlsActive) return;

        if (Pressed(toggleKey, togglePadButton)) ToggleBlackout();
        if (Pressed(extentKey, extentPadButton)) CycleExtent();

        if (overlayKey != KeyCode.None && Input.GetKeyDown(overlayKey))
        {
            showOverlay = !showOverlay;
            if (logChanges) Debug.Log(LogPrefix + "readout " + (showOverlay ? "shown" : "hidden"), this);
        }
    }

    void LateUpdate()
    {
        if (follow == null || _band == null) ResolveReferences();

        int row = CurrentRow;

        if (row != _placedRow || extent != _placedExtent || CanvasSizeChanged())
        {
            int from = _placedRow;
            Place(row);

            // The first placement in play mode is where the shifter's starting row lands.
            // Report it once as the opening state rather than as a move.
            if (Application.isPlaying && _announced && from > 0 && from != row)
                Report((follow != null ? follow.name : "band") + " row " + from + " -> " + row +
                       ": " + Describe(row, extent));
        }

        if (Application.isPlaying && !_announced)
        {
            _announced = true;
            Announce();
        }

        UpdateFade();
    }

    // ── Placement ────────────────────────────────────────────────────────────

    /// <summary>
    /// Stretches the band across the full width of the root canvas and over the rows the
    /// extent covers. Like VizlabRowAnchor_NEW, the band is resolved in root-canvas space and
    /// then expressed as anchors in this object's parent, so it is right at any depth. The
    /// width is the canvas's full width, which is the screen's edge on an overlay canvas.
    /// </summary>
    void Place(int row)
    {
        var rt = (RectTransform)transform;
        var parent = rt.parent as RectTransform;
        RectTransform canvasRt = RootCanvasRect();
        if (parent == null || canvasRt == null) return;

        Rect c = canvasRt.rect;
        Rect p = parent.rect;
        if (c.width <= 0f || c.height <= 0f || p.width <= 0f || p.height <= 0f) return;

        float yMin, yMax;
        GetBandFractions(row, extent, out yMin, out yMax);

        Vector3 min = parent.InverseTransformPoint(
            canvasRt.TransformPoint(new Vector3(c.xMin, c.yMin + yMin * c.height, 0f)));
        Vector3 max = parent.InverseTransformPoint(
            canvasRt.TransformPoint(new Vector3(c.xMax, c.yMin + yMax * c.height, 0f)));

        // Marks the fields as driven, so the Inspector greys them out instead of letting a
        // hand edit be silently overwritten on the next frame.
        _tracker.Clear();
        _tracker.Add(this, rt, DrivenTransformProperties.Anchors |
                               DrivenTransformProperties.SizeDelta |
                               DrivenTransformProperties.AnchoredPosition);

        rt.anchorMin = new Vector2((min.x - p.xMin) / p.width, (min.y - p.yMin) / p.height);
        rt.anchorMax = new Vector2((max.x - p.xMin) / p.width, (max.y - p.yMin) / p.height);
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        _placedRow = row;
        _placedExtent = extent;
        _placedCanvasSize = c.size;
    }

    /// <summary>
    /// Anchors are fractions, so a resize only matters when the band is nested. It is one
    /// Vector2 compare a frame either way, and it keeps the nested case correct.
    /// </summary>
    bool CanvasSizeChanged()
    {
        RectTransform canvasRt = RootCanvasRect();
        return canvasRt != null && canvasRt.rect.size != _placedCanvasSize;
    }

    RectTransform RootCanvasRect()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        return canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
    }

    // ── Colour and fade ──────────────────────────────────────────────────────

    void UpdateFade()
    {
        float target = _on ? 1f : 0f;
        if (Mathf.Approximately(_alpha, target)) return;

        _alpha = !Application.isPlaying || fadeSeconds <= 0f
            ? target
            : Mathf.MoveTowards(_alpha, target, Time.unscaledDeltaTime / fadeSeconds);

        ApplyColour();
    }

    void ApplyColour()
    {
        if (_band == null) return;

        Color c = colour;
        c.a *= _alpha;
        _band.color = c;

        // Fully faded out means nothing to draw. Disabling saves a full-width fill at the
        // wall's resolution rather than blending a transparent quad every frame.
        _band.enabled = _alpha > 0f;
    }

    // ── References ───────────────────────────────────────────────────────────

    void ResolveReferences()
    {
        if (_band == null) _band = GetComponent<Image>();

        if (follow == null)
        {
            if (shifter == null) shifter = FindObjectOfType<VizlabRowShifter_NEW>();
            if (shifter != null) follow = shifter.GetComponentInChildren<VizlabRowAnchor_NEW>();
        }

        if (shifter == null && follow != null)
            shifter = follow.GetComponentInParent<VizlabRowShifter_NEW>();
    }

    /// <summary>
    /// The shifter finds its anchor in Awake, which does not run in edit mode, so outside
    /// play it would quietly do nothing. The row methods go through it only while it is
    /// live, to pick up its logging and wrap setting, and drive the anchor directly otherwise.
    /// </summary>
    bool ShifterLive { get { return Application.isPlaying && shifter != null && shifter.isActiveAndEnabled; } }

    void Step(int direction)
    {
        if (ShifterLive)
        {
            shifter.Shift(direction);
            return;
        }

        if (follow != null) follow.Row = follow.Row + direction;
        else fallbackRow = Mathf.Clamp(fallbackRow + direction, 1, VizlabDisplay_NEW.RowCount);
    }

    // ── Debug text ───────────────────────────────────────────────────────────

    bool DebugControlsActive { get { return debugControlsInBuild || Application.isEditor; } }

    static bool Pressed(KeyCode key, KeyCode pad)
    {
        return (key != KeyCode.None && Input.GetKeyDown(key))
            || (pad != KeyCode.None && Input.GetKeyDown(pad));
    }

    void Announce()
    {
        if (follow == null)
            Debug.LogWarning(LogPrefix + "nothing to follow, so the band stays on row " + fallbackRow +
                             ". Set Follow to the LAF HUD's VizlabRowAnchor_NEW, or add a " +
                             "VizlabRowShifter_NEW to the HUD.", this);

        if (!logChanges) return;

        Debug.Log(LogPrefix + "ready. Following '" + (follow != null ? follow.name : "nothing") +
                  "', blackout " + (_on ? "ON" : "OFF") + ", " + Describe(CurrentRow, extent) +
                  "\n  " + ControlsHint(), this);
    }

    /// <summary>Logs and puts the message on the readout for a couple of seconds.</summary>
    void Report(string message)
    {
        _lastAction = message;
        _lastActionTime = Time.unscaledTime;

        if (logChanges) Debug.Log(LogPrefix + message, this);
    }

    /// <summary>
    /// One line that answers "where is it and how does it read": row, tilt, angle from a
    /// standing eye, apparent scale against the safe row, and the band in canvas pixels and
    /// in millimetres off the floor.
    /// </summary>
    string Describe(int row, Extent ext)
    {
        VizlabDisplay_NEW.Row data = VizlabDisplay_NEW.GetRow(row);
        Vector3 eye = VizlabDisplay_NEW.StandingEyeMm;

        float yMin, yMax;
        GetBandFractions(row, ext, out yMin, out yMax);

        // The band's physical run: the lowest row it covers to the top of the followed row.
        Vector3[] top = VizlabDisplay_NEW.GetCornersMm(row);
        Vector3[] bottom = VizlabDisplay_NEW.GetCornersMm(ext == Extent.RowAndBelow ? VizlabDisplay_NEW.RowCount : row);

        return string.Format(
            "row {0}{2}, extent {1}, tilt {3:0.#}°, {4:+0.#;-0.#;0}° from standing eyeline, reads {5:0.00}x vs row {6} | " +
            "band y {7:0.000}-{8:0.000} = px {9:0}-{10:0} of {11} | {12:0}-{13:0} mm off the floor",
            row, ext, Advice(row), data.TiltDeg,
            VizlabDisplay_NEW.GetElevationDeg(row, eye),
            VizlabDisplay_NEW.GetApparentScale(row, eye), VizlabDisplay_NEW.SafeAreaRow,
            yMin, yMax, yMin * VizlabDisplay_NEW.CanvasHeightPx, yMax * VizlabDisplay_NEW.CanvasHeightPx,
            VizlabDisplay_NEW.CanvasHeightPx,
            bottom[3].y, top[0].y);
    }

    /// <summary>The same verdict VizlabRowShifter_NEW prints, so the two log lines agree.</summary>
    static string Advice(int row)
    {
        return row == VizlabDisplay_NEW.SafeAreaRow ? " (safe area)"
             : VizlabDisplay_NEW.IsPoorPlacement(row) ? " (avoid)" : "";
    }

    string DescribeCanvas()
    {
        RectTransform canvasRt = RootCanvasRect();
        if (canvasRt == null) return "none: the band must sit under a Canvas";

        Canvas canvas = canvasRt.GetComponent<Canvas>();
        return string.Format("'{0}' {1}, sort order {2}, {3:0} x {4:0}",
                             canvas.name, canvas.renderMode, canvas.sortingOrder,
                             canvasRt.rect.width, canvasRt.rect.height);
    }

    string ControlsHint()
    {
        return string.Format("{0} / pad {1} toggles, {2} / pad {3} cycles extent, {4} readout. " +
                             "Rows move with the shifter: RB up, LB down, Home reset (PgUp / PgDn).",
                             toggleKey, togglePadButton, extentKey, extentPadButton, overlayKey);
    }

    // ── Readout ──────────────────────────────────────────────────────────────

    void OnGUI()
    {
        if (!Application.isPlaying || !DebugControlsActive) return;
        if (!showOverlay || !DebugView_NEW.Overlay) return;

        EnsureStyles();

        string body = BuildBody();
        Vector2 size = _label.CalcSize(new GUIContent(body));

        // Top right, because LayerDebugJump_NEW owns the top left.
        var rect = new Rect(Screen.width - overlayMargin.x - size.x - 20f, overlayMargin.y,
                            size.x + 20f, size.y + 16f);

        GUI.Box(rect, GUIContent.none, _panel);
        GUI.Label(new Rect(rect.x + 10f, rect.y + 8f, size.x, size.y), body, _label);
    }

    string BuildBody()
    {
        int row = CurrentRow;
        VizlabDisplay_NEW.Row data = VizlabDisplay_NEW.GetRow(row);
        Vector3 eye = VizlabDisplay_NEW.StandingEyeMm;

        float yMin, yMax;
        GetBandFractions(row, extent, out yMin, out yMax);

        _sb.Length = 0;
        _sb.Append("ROW BLACKOUT   ").Append(overlayKey).Append(" to hide\n");
        _sb.Append("following ").Append(follow != null ? follow.name : "(nothing)").Append('\n');
        _sb.Append("row       ").Append(row).Append(" of ").Append(VizlabDisplay_NEW.RowCount)
           .Append(Advice(row)).Append('\n');
        _sb.Append("tilt      ").Append(data.TiltDeg.ToString("0.#")).Append("°   ")
           .Append(VizlabDisplay_NEW.GetElevationDeg(row, eye).ToString("+0.#;-0.#;0")).Append("° from eye   reads ")
           .Append(VizlabDisplay_NEW.GetApparentScale(row, eye).ToString("0.00")).Append("x\n");
        _sb.Append("blackout  ").Append((_on ? "ON" : "OFF").PadRight(13))
           .Append(toggleKey).Append(" / pad ").Append(PadName(togglePadButton)).Append('\n');
        _sb.Append("extent    ").Append(extent.ToString().PadRight(13))
           .Append(extentKey).Append(" / pad ").Append(PadName(extentPadButton)).Append('\n');
        _sb.Append("band      y ").Append(yMin.ToString("0.000")).Append('-').Append(yMax.ToString("0.000"))
           .Append("   px ").Append((yMin * VizlabDisplay_NEW.CanvasHeightPx).ToString("0")).Append('-')
           .Append((yMax * VizlabDisplay_NEW.CanvasHeightPx).ToString("0")).Append('\n');
        _sb.Append("move      RB up  LB down  Home reset\n");

        float age = Time.unscaledTime - _lastActionTime;
        if (age < ActionFadeSeconds && !string.IsNullOrEmpty(_lastAction))
        {
            // The full log line is long; the readout only needs its head.
            string head = _lastAction.Split('|')[0].Trim();
            _sb.Append('\n').Append("· ").Append(head);
        }

        return _sb.ToString();
    }

    /// <summary>Joystick buttons by the name printed on an Xbox pad, which is what a playtester looks for.</summary>
    static string PadName(KeyCode button)
    {
        switch (button)
        {
            case KeyCode.JoystickButton6: return "View";
            case KeyCode.JoystickButton7: return "Menu";
            case KeyCode.JoystickButton8: return "LS click";
            case KeyCode.JoystickButton9: return "RS click";
            case KeyCode.None: return "-";
            default: return button.ToString();
        }
    }

    void EnsureStyles()
    {
        if (_panelTex == null)
        {
            _panelTex = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            _panelTex.SetPixel(0, 0, new Color(0.05f, 0.03f, 0.11f, 0.82f));
            _panelTex.Apply();
        }

        if (_panel == null)
            _panel = new GUIStyle(GUIStyle.none) { normal = { background = _panelTex } };

        if (_label == null)
        {
            _label = new GUIStyle(GUI.skin.label)
            {
                font = Font.CreateDynamicFontFromOSFont("Consolas", fontSize),
                fontSize = fontSize,
                richText = false,
                wordWrap = false,
                normal = { textColor = new Color(0.88f, 0.85f, 0.96f, 1f) }
            };
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        fallbackRow = Mathf.Clamp(fallbackRow, 1, VizlabDisplay_NEW.RowCount);

        // Transforms cannot be written during OnValidate, so the write is deferred a tick,
        // the same way VizlabRowAnchor_NEW does it.
        EditorApplication.delayCall += () =>
        {
            if (this == null || !isActiveAndEnabled) return;
            Refresh();
        };
    }
#endif
}

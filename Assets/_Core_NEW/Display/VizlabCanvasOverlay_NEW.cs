using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Puts the display's shape onto the Canvas you are actually laying out on.
///
/// UI work happens on a flat canvas rect, so that is where the row boundaries, the seams and
/// the "which panel is this" answer have to appear. Drop this on the Canvas and they do. This
/// is the only thing in the folder that draws the rows; placement itself is
/// VizlabRowAnchor_NEW on the element.
///
/// THREE THINGS IT DRAWS, all as Scene View gizmos, nothing rendered and nothing at runtime:
///
///   1. The seven row bands across the canvas, numbered, with each row's tilt. Row 5 reads
///      green as the eye-level safe area; rows 1, 2 and 7 read red.
///
///   2. The seams, at their true width. They are only about 10 px, which is exactly why they
///      are easy to lay a line of text across without noticing.
///
///   3. A side profile to the right of the canvas: the seven panels seen edge-on, folded as
///      they physically are, with the viewer marked and a connector from each flat band to
///      the panel it becomes.
///
/// Set Eye to compare viewing positions: from the design point every row is equidistant and
/// reads at 1.00x, and from standing height they do not. The figures are reported per row.
///
/// COST: gizmo text redraws on every Scene View repaint and is the expensive part. Turn off
/// Show Labels if the editor feels sluggish.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[HierarchyBadge_NEW("VIZ CANVAS", "#C08A2E")]
public class VizlabCanvasOverlay_NEW : MonoBehaviour
{
    public enum EyeMode
    {
        /// <summary>The centre of curvature, y 1238.8. Every row on the arc is equidistant from here.</summary>
        DesignPoint,
        /// <summary>A standing adult, y 1600. Rows are no longer equidistant, so scale varies.</summary>
        Standing,
        /// <summary>Whatever you put in Custom Eye.</summary>
        Custom,
    }

    [Header("What to draw")]
    [Tooltip("The seven row bands across the canvas, numbered and tinted.")]
    [SerializeField] bool showBands = true;

    [Tooltip("The inter-row gaps, drawn at their true canvas width. Small, and worth seeing.")]
    [SerializeField] bool showSeams = true;

    [Tooltip("The edge-on side view to the right of the canvas, showing how the rows fold.")]
    [SerializeField] bool showProfile = true;

    [Tooltip("Thin connectors from each canvas band to its panel in the side profile.")]
    [SerializeField] bool showConnectors = true;

    [Tooltip("Row numbers, tilts and seam widths. Text is by far the most expensive thing " +
             "gizmos draw, and it redraws on every Scene View repaint - turn this off if the " +
             "editor feels sluggish while the overlay is visible.")]
    [SerializeField] bool showLabels = true;

    [Tooltip("Highlight one row and dim the rest. 0 draws all seven evenly.")]
    [Range(0, VizlabDisplay_NEW.RowCount)]
    [SerializeField] int focusRow = 0;

    [Header("Viewer")]
    [Tooltip("Where to measure from. The design point is where the surface reads perfectly " +
             "even; Standing is where a visitor's eyes actually are, and is the honest one " +
             "to check a layout against.")]
    [SerializeField] EyeMode eye = EyeMode.Standing;

    [Tooltip("Used when Eye is set to Custom. Millimetres, in the display's own coordinates.")]
    [SerializeField] Vector3 customEyeMm = new Vector3(0f, 1600f, 0f);

    [Tooltip("Per row, how large content reads compared to the safe-area row from the chosen " +
             "eye position, plus the angle above the eyeline.")]
    [SerializeField] bool showApparentScale = true;

    /// <summary>The eye position in display millimetres, for whichever mode is selected.</summary>
    public Vector3 EyeMm
    {
        get
        {
            switch (eye)
            {
                case EyeMode.Standing: return VizlabDisplay_NEW.StandingEyeMm;
                case EyeMode.Custom: return customEyeMm;
                default: return VizlabDisplay_NEW.CentreOfCurvatureMm;
            }
        }
    }

    RectTransform Rt { get { return (RectTransform)transform; } }

#if UNITY_EDITOR

    static readonly Color BandColour = new Color(0.78f, 0.86f, 1f, 0.55f);
    static readonly Color SafeColour = new Color(0.35f, 0.95f, 0.55f, 1f);
    static readonly Color AvoidColour = new Color(0.95f, 0.45f, 0.40f, 1f);
    static readonly Color SeamColour = new Color(1f, 0.35f, 0.25f, 0.85f);
    static readonly Color FocusColour = new Color(1f, 0.80f, 0.25f, 1f);
    static readonly Color EyeColour = new Color(1f, 0.95f, 0.5f, 0.95f);

    // Rebuilt lazily and reused: OnDrawGizmos runs on every Scene View repaint, and
    // EditorStyles can only be touched from inside one.
    static GUIStyle _label;
    static GUIStyle _small;

    static GUIStyle Label
    {
        get
        {
            if (_label == null) _label = new GUIStyle(EditorStyles.boldLabel);
            return _label;
        }
    }

    static GUIStyle Small
    {
        get
        {
            if (_small == null) _small = new GUIStyle(EditorStyles.miniLabel);
            return _small;
        }
    }

    void OnDrawGizmos()
    {
        Rect rect = Rt.rect;
        if (rect.width <= 0f || rect.height <= 0f) return;

        if (showBands) DrawBands(rect);
        if (showSeams) DrawSeams(rect);
        if (showProfile) DrawProfile(rect);
    }

    Vector3 ToWorld(float x, float y)
    {
        return Rt.TransformPoint(new Vector3(x, y, 0f));
    }

    Color ColourFor(int row)
    {
        if (focusRow == row && focusRow != 0) return FocusColour;
        if (row == VizlabDisplay_NEW.SafeAreaRow) return SafeColour;
        if (VizlabDisplay_NEW.IsPoorPlacement(row)) return AvoidColour;
        return BandColour;
    }

    bool Dimmed(int row)
    {
        return focusRow != 0 && focusRow != row;
    }

    // ---- 1. The bands ------------------------------------------------------------

    void DrawBands(Rect rect)
    {
        Vector3 eyeMm = EyeMm;

        for (int row = 1; row <= VizlabDisplay_NEW.RowCount; row++)
        {
            float fMin, fMax;
            VizlabDisplay_NEW.GetRowAnchorFractions(row, out fMin, out fMax);

            float yMin = rect.yMin + fMin * rect.height;
            float yMax = rect.yMin + fMax * rect.height;

            Color c = ColourFor(row);
            if (Dimmed(row)) c.a *= 0.3f;

            Handles.color = c;
            Handles.DrawAAPolyLine(focusRow == row && focusRow != 0 ? 4f : 2f,
                                   ToWorld(rect.xMin, yMin), ToWorld(rect.xMax, yMin),
                                   ToWorld(rect.xMax, yMax), ToWorld(rect.xMin, yMax),
                                   ToWorld(rect.xMin, yMin));

            // A faint wash, so a band reads as an area rather than four lines.
            Color fill = c;
            fill.a *= 0.09f;
            Handles.DrawSolidRectangleWithOutline(
                new[]
                {
                    ToWorld(rect.xMin, yMax), ToWorld(rect.xMax, yMax),
                    ToWorld(rect.xMax, yMin), ToWorld(rect.xMin, yMin),
                }, fill, Color.clear);

            if (Dimmed(row) || !showLabels) continue;

            VizlabDisplay_NEW.Row data = VizlabDisplay_NEW.GetRow(row);

            string note = row == VizlabDisplay_NEW.SafeAreaRow ? "   SAFE AREA"
                        : VizlabDisplay_NEW.IsPoorPlacement(row) ? "   avoid" : "";

            Label.normal.textColor = c;
            Handles.Label(ToWorld(rect.xMin + rect.width * 0.02f, yMax - rect.height * 0.018f),
                          string.Format("Row {0}    tilt {1:0.#}°{2}", row, data.TiltDeg, note),
                          Label);

            if (!showApparentScale) continue;

            float scale = VizlabDisplay_NEW.GetApparentScale(row, eyeMm);
            float elevation = VizlabDisplay_NEW.GetElevationDeg(row, eyeMm);

            Small.normal.textColor = c;
            Handles.Label(ToWorld(rect.xMin + rect.width * 0.02f, yMax - rect.height * 0.048f),
                          string.Format("reads {0:0.00}x vs row {1}     {2:+0.#;-0.#;0}° above eyeline",
                                        scale, VizlabDisplay_NEW.SafeAreaRow, elevation),
                          Small);
        }
    }

    // ---- 2. The seams ------------------------------------------------------------

    void DrawSeams(Rect rect)
    {
        // Seams are drawn at true width so the 10-odd pixels read as a real obstacle rather
        // than a hairline. They straddle the boundary between two bands.
        float pxToCanvas = rect.height / VizlabDisplay_NEW.CanvasHeightPx;

        for (int upper = 1; upper < VizlabDisplay_NEW.RowCount; upper++)
        {
            if (focusRow != 0 && focusRow != upper && focusRow != upper + 1) continue;

            float fMin, fMax;
            VizlabDisplay_NEW.GetRowAnchorFractions(upper, out fMin, out fMax);
            float boundary = rect.yMin + fMin * rect.height;

            float halfWidth = VizlabDisplay_NEW.GetSeamPx(upper) * pxToCanvas * 0.5f;

            Handles.DrawSolidRectangleWithOutline(
                new[]
                {
                    ToWorld(rect.xMin, boundary + halfWidth), ToWorld(rect.xMax, boundary + halfWidth),
                    ToWorld(rect.xMax, boundary - halfWidth), ToWorld(rect.xMin, boundary - halfWidth),
                }, SeamColour, Color.clear);

            if (!showLabels) continue;

            Small.normal.textColor = SeamColour;
            Handles.Label(ToWorld(rect.xMax - rect.width * 0.09f, boundary + halfWidth * 3f),
                          string.Format("seam {0:0.0} mm / {1:0} px",
                                        VizlabDisplay_NEW.GetSeamMm(upper),
                                        VizlabDisplay_NEW.GetSeamPx(upper)),
                          Small);
        }
    }

    // ---- 3. The side profile -----------------------------------------------------

    void DrawProfile(Rect rect)
    {
        Vector3 eyeMm = EyeMm;

        // Fit the physical surface into a box the same height as the canvas, sitting off its
        // right edge. Millimetres map to canvas units through one scale, so the profile is
        // to scale with itself even though it is not to scale with the canvas.
        const float SpanMm = 3300f;    // floor to the top edge, with headroom
        const float DepthMm = 2000f;   // viewer at z 0 out to the farthest panel

        float s = rect.height / SpanMm;
        float gap = rect.width * 0.03f;
        float left = rect.xMax + gap;
        float right = left + DepthMm * s;
        float bottom = rect.yMin;

        // The viewer sits at z = 0, so the profile's right edge is the near side.
        System.Func<Vector3, Vector2> project = mm => new Vector2(right + mm.z * s, bottom + mm.y * s);

        // Floor line, for a sense of where the ground is.
        Handles.color = new Color(1f, 1f, 1f, 0.15f);
        Handles.DrawAAPolyLine(1f, ToWorld(left, bottom), ToWorld(right, bottom));

        var corners = new Vector3[4];

        for (int row = 1; row <= VizlabDisplay_NEW.RowCount; row++)
        {
            VizlabDisplay_NEW.GetCornersMm(row, corners);

            // Edge-on, so the panel is the segment from its top edge to its bottom edge.
            Vector2 top = project(corners[0]);
            Vector2 bot = project(corners[3]);

            Color c = ColourFor(row);
            if (Dimmed(row)) c.a *= 0.3f;

            Handles.color = c;
            Handles.DrawAAPolyLine(focusRow == row && focusRow != 0 ? 6f : 4f,
                                   ToWorld(top.x, top.y), ToWorld(bot.x, bot.y));

            if (Dimmed(row)) continue;

            Vector2 mid = (top + bot) * 0.5f;

            if (showLabels)
            {
                Small.normal.textColor = c;
                Handles.Label(ToWorld(mid.x - rect.width * 0.035f, mid.y), row.ToString(), Small);
            }

            // The connector that says "this flat band becomes that tilted panel".
            if (showConnectors)
            {
                float fMin, fMax;
                VizlabDisplay_NEW.GetRowAnchorFractions(row, out fMin, out fMax);
                float bandMidY = rect.yMin + (fMin + fMax) * 0.5f * rect.height;

                Color link = c;
                link.a *= 0.30f;
                Handles.color = link;
                Handles.DrawAAPolyLine(1f, ToWorld(rect.xMax, bandMidY), ToWorld(mid.x, mid.y));
            }
        }

        // The viewer, and sight lines out to each panel centre.
        Vector2 eye2 = project(eyeMm);

        Color ray = EyeColour;
        ray.a = 0.22f;
        Handles.color = ray;

        for (int row = 1; row <= VizlabDisplay_NEW.RowCount; row++)
        {
            if (Dimmed(row)) continue;
            Vector2 centre = project(VizlabDisplay_NEW.GetRow(row).CentreMm);
            Handles.DrawAAPolyLine(1f, ToWorld(eye2.x, eye2.y), ToWorld(centre.x, centre.y));
        }

        Handles.color = EyeColour;
        Handles.DrawSolidDisc(ToWorld(eye2.x, eye2.y), -Rt.forward, rect.height * 0.006f);

        if (!showLabels) return;

        Small.normal.textColor = EyeColour;
        Handles.Label(ToWorld(eye2.x + rect.width * 0.006f, eye2.y),
                      string.Format("{0} eye, y {1:0} mm",
                                    eye == EyeMode.DesignPoint ? "design" :
                                    eye == EyeMode.Standing ? "standing" : "custom",
                                    eyeMm.y),
                      Small);
    }
#endif
}

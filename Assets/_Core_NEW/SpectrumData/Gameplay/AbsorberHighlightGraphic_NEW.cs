using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What AbsorberHighlight_NEW draws: on the bar, a glowing column over the line plus an
/// arrow under the bar pointing up at it; on the canvas, a single arrow pointing down at the
/// line on the trail. Created and driven at runtime — nothing to set up by hand.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class AbsorberHighlightGraphic_NEW : MaskableGraphic
{
    public enum Kind { Bar, TrailArrow }

    [HideInInspector] public Kind kind = Kind.Bar;

    // Bar: the band the curve is drawn in (local space), the line's x, and two alphas.
    [HideInInspector] public Rect band;
    [HideInInspector] public float x;
    [HideInInspector] public float columnAlpha;
    [HideInInspector] public float arrowAlpha;

    // Distance: where the line was born (the anchor, on the birth tick) and a bracket under the
    // bar from there to the line — "how far it has travelled".
    [HideInInspector] public float anchorX = float.NaN;
    [HideInInspector] public float spanAlpha;

    // Trail arrow: its tip, in local space.
    [HideInInspector] public Vector2 tip;

    [HideInInspector] public Color columnColor = new Color(1f, 0.91f, 0.63f, 0.55f);
    [HideInInspector] public Color arrowColor = new Color(1f, 0.91f, 0.63f, 1f);
    [HideInInspector] public float columnWidth = 6f;
    [HideInInspector] public Vector2 arrowSize = new Vector2(14f, 11f);
    [HideInInspector] public float arrowGap = 4f;

    public void Refresh() { SetVerticesDirty(); }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (kind == Kind.TrailArrow)
        {
            if (arrowAlpha <= 0.001f) return;
            Color32 c = Fade(arrowColor, arrowAlpha);
            float hw = arrowSize.x * 0.5f;
            // Pointing down: apex at the tip, body above it.
            Triangle(vh, tip, tip + new Vector2(-hw, arrowSize.y), tip + new Vector2(hw, arrowSize.y), c);
            return;
        }

        if (columnAlpha > 0.001f)
        {
            Color32 c = Fade(columnColor, columnAlpha);
            float hw = columnWidth * 0.5f;
            Quad(vh, new Vector2(x - hw, band.yMin), new Vector2(x + hw, band.yMax), c);
        }

        if (spanAlpha > 0.001f && !float.IsNaN(anchorX))
        {
            // No anchor of its own: RedshiftMarks_NEW's arrow already marks the birth point
            // at this exact x, and a second tick and triangle on top of it only doubled it.

            // The bracket: from the anchor to the line, under both arrows, with end ticks.
            float y = band.yMin - arrowGap - arrowSize.y - 5f;
            Color32 b = Fade(arrowColor, spanAlpha);
            float x0 = Mathf.Min(anchorX, x), x1 = Mathf.Max(anchorX, x);
            if (x1 - x0 > 2f)
            {
                Quad(vh, new Vector2(x0, y - 1f), new Vector2(x1, y + 1f), b);
                Quad(vh, new Vector2(x0 - 1f, y - 4f), new Vector2(x0 + 1f, y + 4f), b);
                Quad(vh, new Vector2(x1 - 1f, y - 4f), new Vector2(x1 + 1f, y + 4f), b);
            }
        }

        if (arrowAlpha > 0.001f)
        {
            Color32 c = Fade(arrowColor, arrowAlpha);
            float hw = arrowSize.x * 0.5f;
            Vector2 apex = new Vector2(x, band.yMin - arrowGap);
            // Pointing up at the line from below the bar.
            Triangle(vh, apex, apex + new Vector2(hw, -arrowSize.y), apex + new Vector2(-hw, -arrowSize.y), c);
        }
    }

    static Color32 Fade(Color c, float a)
    {
        c.a *= Mathf.Clamp01(a);
        return c;
    }

    static void Quad(VertexHelper vh, Vector2 min, Vector2 max, Color32 c)
    {
        int i = vh.currentVertCount;
        UIVertex v = UIVertex.simpleVert;
        v.color = c;
        v.position = new Vector2(min.x, min.y); vh.AddVert(v);
        v.position = new Vector2(min.x, max.y); vh.AddVert(v);
        v.position = new Vector2(max.x, max.y); vh.AddVert(v);
        v.position = new Vector2(max.x, min.y); vh.AddVert(v);
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i, i + 2, i + 3);
    }

    static void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 cpos, Color32 c)
    {
        int i = vh.currentVertCount;
        UIVertex v = UIVertex.simpleVert;
        v.color = c;
        v.position = a; vh.AddVert(v);
        v.position = b; vh.AddVert(v);
        v.position = cpos; vh.AddVert(v);
        vh.AddTriangle(i, i + 1, i + 2);
    }
}

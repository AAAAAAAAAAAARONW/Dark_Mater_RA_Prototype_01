using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// An outline box around one absorption line on the spectrum bar: the mark for "this line,
/// wherever it goes".
///
/// THE RULE IT ENFORCES, in the tutorial and the journey alike. There are two kinds of mark
/// on the bar and they mean opposite things:
///
///   FIXED   the birth point, 121.6 nm, where hydrogen always absorbs and every line is
///           born. It never moves. Drawn as the green ARROW under the bar
///           (TutorialSpectrumBands_NEW.birthArrow, RedshiftMarks_NEW's anchor).
///   MOVING  one particular line, followed as it is stretched redward. Drawn as THIS: a
///           box around the line, never an arrow.
///
/// They used to be the same shape — an arrow under the bar at the birth point, and an arrow
/// under the bar at the line — so the one moment the redshift is visible, a line leaving its
/// birth point, read as two identical marks with no way to tell which one stays.
///
/// Fills its own RectTransform. Stroke is in pixels so it stays crisp at any bar size.
/// The static Stroke() is shared with AbsorberHighlightGraphic_NEW, so the journey's box is
/// the same box.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class LineOutlineGraphic_NEW : MaskableGraphic
{
    [Tooltip("Line thickness of the box, pixels.")]
    [Min(0.5f)]
    [SerializeField] float stroke = 2f;

    public float StrokeWidth { get { return stroke; } set { stroke = value; SetVerticesDirty(); } }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Stroke(vh, rectTransform.rect, stroke, color);
    }

    /// <summary>An empty rectangle: four bars of the given thickness, inside r.</summary>
    public static void Stroke(VertexHelper vh, Rect r, float thickness, Color32 c)
    {
        float t = Mathf.Min(thickness, r.width * 0.5f, r.height * 0.5f);
        if (t <= 0f) return;

        Quad(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin + t), c);          // bottom
        Quad(vh, new Vector2(r.xMin, r.yMax - t), new Vector2(r.xMax, r.yMax), c);          // top
        Quad(vh, new Vector2(r.xMin, r.yMin + t), new Vector2(r.xMin + t, r.yMax - t), c);  // left
        Quad(vh, new Vector2(r.xMax - t, r.yMin + t), new Vector2(r.xMax, r.yMax - t), c);  // right
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
}

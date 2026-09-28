using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds a line graph as UI mesh on the Graphic it sits on.
///
/// Ported from UIImageLineGraphEffect. The vertex layout is unchanged — two triangles per
/// line segment, two more for the area fill under it — so the graph looks the same.
///
/// One thing worth knowing before turning the highlight back on: it draws a second quad
/// over exactly the same geometry as the main line. In this scene both thicknesses are
/// 0.1, so the highlight was not a thinner accent on top of a thicker line, it was a
/// duplicate in a slightly different colour — a third of every vertex for a colour blend.
/// It is off, and the main line carries the blended colour instead.
///
/// Cost at the settings used here: 512 samples means 511 segments, two quads each, so
/// about 4,100 vertices rebuilt whenever the values change. That is every frame while the
/// spectrum scrolls, and a vertex-dirty Graphic forces its whole Canvas to rebuild — worth
/// remembering before raising the sample count.
/// </summary>
[RequireComponent(typeof(Graphic))]
[HierarchyBadge_NEW("GRAPH", "#33B3B3")]
public class LineGraphRenderer_NEW : BaseMeshEffect
{
    [Header("Line")]
    [SerializeField] float thickness = 0.1f;
    [SerializeField] Color lineColor = new Color(0.8575f, 0.9525f, 1f, 1f);

    [Tooltip("Second pass over the same geometry in a different colour. Costs a third of " +
             "the mesh; only worth it when its thickness differs from the main line.")]
    [SerializeField] bool drawHighlightLine = false;
    [SerializeField] float highlightThickness = 0.1f;
    [SerializeField] Color highlightColor = new Color(0.85f, 0.95f, 1f, 0.95f);

    [Header("Fill")]
    [SerializeField] bool drawAreaFill = true;
    [SerializeField] Color fillTopColor = new Color(0.35f, 0.65f, 0.95f, 0.45f);
    [SerializeField] Color fillBottomColor = new Color(0.05f, 0.12f, 0.22f, 0.15f);

    [Header("Layout")]
    [Range(0.1f, 1f)]
    [SerializeField] float graphHeightPercent = 0.8f;
    [SerializeField] bool anchorTop = true;

    [Header("Axes")]
    [SerializeField] bool drawAxes = true;
    [SerializeField] float axisThickness = 1f;
    [SerializeField] Color axisColor = new Color(1f, 1f, 1f, 0.9f);

    readonly List<float> _values = new List<float>();
    float _min;
    float _max = 1f;

    /// <summary>Fraction of the rect's height the curve uses. Read by overlays that must line up with it.</summary>
    public float GraphHeightPercent { get { return graphHeightPercent; } }

    /// <summary>True if the curve's band hangs from the top of the rect rather than sitting on the bottom.</summary>
    public bool AnchorTop { get { return anchorTop; } }

    /// <summary>Replace the plotted series and queue a mesh rebuild.</summary>
    public void SetValues(IReadOnlyList<float> values, float min, float max)
    {
        _values.Clear();
        if (values != null)
            for (int i = 0; i < values.Count; i++) _values.Add(values[i]);

        _min = min;
        _max = Mathf.Max(min + 0.0001f, max);

        if (graphic != null) graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive()) return;

        vh.Clear();
        if (_values.Count < 2) return;

        Rect rect = graphic.rectTransform.rect;
        float width = rect.width;
        float height = rect.height * graphHeightPercent;
        float yBase = anchorTop ? rect.yMax - height : rect.yMin;

        if (drawAxes)
        {
            AddLine(vh, new Vector2(rect.xMin, yBase + height),
                        new Vector2(rect.xMax, yBase + height), axisThickness, axisColor);
            AddLine(vh, new Vector2(rect.xMin, yBase),
                        new Vector2(rect.xMin, yBase + height), axisThickness, axisColor);
        }

        float stepX = width / (_values.Count - 1);
        Vector2 prev = new Vector2(rect.xMin, yBase + Normalise(_values[0]) * height);

        for (int i = 1; i < _values.Count; i++)
        {
            var curr = new Vector2(rect.xMin + stepX * i, yBase + Normalise(_values[i]) * height);

            if (drawAreaFill) AddFilledStrip(vh, prev, curr, yBase, fillTopColor, fillBottomColor);

            AddLine(vh, prev, curr, thickness, lineColor);
            if (drawHighlightLine) AddLine(vh, prev, curr, highlightThickness, highlightColor);

            prev = curr;
        }
    }

    float Normalise(float v) => Mathf.InverseLerp(_min, _max, v);

    static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float lineThickness, Color c)
    {
        Vector2 dir = b - a;
        if (dir.sqrMagnitude <= 0.000001f) return;
        dir.Normalize();

        Vector2 normal = new Vector2(-dir.y, dir.x) * (lineThickness * 0.5f);

        UIVertex v = UIVertex.simpleVert;
        v.color = c;
        int idx = vh.currentVertCount;

        v.position = a - normal; vh.AddVert(v);
        v.position = a + normal; vh.AddVert(v);
        v.position = b + normal; vh.AddVert(v);
        v.position = b - normal; vh.AddVert(v);

        vh.AddTriangle(idx, idx + 1, idx + 2);
        vh.AddTriangle(idx, idx + 2, idx + 3);
    }

    static void AddFilledStrip(VertexHelper vh, Vector2 topA, Vector2 topB, float yBottom,
                               Color topColor, Color bottomColor)
    {
        UIVertex v = UIVertex.simpleVert;
        int idx = vh.currentVertCount;

        v.color = topColor;
        v.position = topA; vh.AddVert(v);
        v.position = topB; vh.AddVert(v);

        v.color = bottomColor;
        v.position = new Vector2(topB.x, yBottom); vh.AddVert(v);
        v.position = new Vector2(topA.x, yBottom); vh.AddVert(v);

        vh.AddTriangle(idx, idx + 1, idx + 2);
        vh.AddTriangle(idx, idx + 2, idx + 3);
    }
}

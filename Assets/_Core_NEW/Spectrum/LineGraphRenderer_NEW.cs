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

    [Tooltip("Colour the line along its length instead of with lineColor: the gradient's " +
             "left end at the left of the graph, its right end at the right. Blended per " +
             "vertex, so it is smooth and costs nothing extra.")]
    [SerializeField] bool useLineGradient = false;
    [SerializeField] Gradient lineGradient = DefaultLineGradient();

    [Tooltip("Colour the line by the real wavelength under each point, the same colours as " +
             "the photon trail: UV, the visible rainbow, IR. Needs a wavelength axis from " +
             "whatever feeds the graph (BakedSpectrumSource_NEW gives one); until then the " +
             "line falls back to the gradient or lineColor.")]
    [SerializeField] bool colourByWavelength = false;

    [Tooltip("Colour below the visible, at full strength. The trail's own UV colour.")]
    [SerializeField] Color uvColour = new Color(0.55f, 0f, 1f, 1f);

    [Tooltip("Colour above the visible, at full strength. The trail's own IR colour.")]
    [SerializeField] Color irColour = new Color(0.8f, 0.02f, 0f, 1f);

    bool _hasAxis;
    double _lnMin, _lnMax;

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
    float _xShift;

    /// <summary>
    /// Draw every point this many sample-steps to the right (0–1), clamped to the rect. A
    /// supplier that samples on points riding with the data (BakedSpectrumSource_NEW) uses it so
    /// the curve sits where the data really is between steps. 0 = the usual fixed points.
    /// </summary>
    public void SetXShift(float samples)
    {
        if (Mathf.Approximately(samples, _xShift)) return;
        _xShift = samples;
        if (graphic != null) graphic.SetVerticesDirty();
    }

    /// <summary>
    /// The wavelengths at the graph's two ends, in Å, on a log axis — what
    /// colourByWavelength reads. Cheap to call every frame; only a change rebuilds.
    /// </summary>
    public void SetWavelengthAxis(double lambdaMin, double lambdaMax)
    {
        double lnMin = System.Math.Log(lambdaMin), lnMax = System.Math.Log(lambdaMax);
        if (_hasAxis && lnMin == _lnMin && lnMax == _lnMax) return;

        _hasAxis = true;
        _lnMin = lnMin;
        _lnMax = lnMax;

        if (colourByWavelength && graphic != null) graphic.SetVerticesDirty();
    }

    /// <summary>No known wavelengths any more: back to the gradient or lineColor.</summary>
    public void ClearWavelengthAxis()
    {
        if (!_hasAxis) return;
        _hasAxis = false;
        if (graphic != null) graphic.SetVerticesDirty();
    }

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
        Vector2 prev = new Vector2(Mathf.Clamp(rect.xMin + stepX * _xShift, rect.xMin, rect.xMax), yBase + Normalise(_values[0]) * height);

        for (int i = 1; i < _values.Count; i++)
        {
            var curr = new Vector2(Mathf.Clamp(rect.xMin + stepX * (i + _xShift), rect.xMin, rect.xMax), yBase + Normalise(_values[i]) * height);

            if (drawAreaFill) AddFilledStrip(vh, prev, curr, yBase, fillTopColor, fillBottomColor);

            if (colourByWavelength && _hasAxis)
                AddLine(vh, prev, curr, thickness, WavelengthColourAt(prev.x, rect), WavelengthColourAt(curr.x, rect));
            else if (useLineGradient && lineGradient != null)
                AddLine(vh, prev, curr, thickness, ColourAt(prev.x, rect), ColourAt(curr.x, rect));
            else
                AddLine(vh, prev, curr, thickness, lineColor);
            if (drawHighlightLine) AddLine(vh, prev, curr, highlightThickness, highlightColor);

            prev = curr;
        }
    }

    float Normalise(float v) => Mathf.InverseLerp(_min, _max, v);

    Color ColourAt(float x, Rect rect)
    {
        return lineGradient.Evaluate(Mathf.InverseLerp(rect.xMin, rect.xMax, x));
    }

    /// <summary>
    /// The trail's colour rule (TrailSpectrumColours_NEW.Fill) at the wavelength under x:
    /// UV below 4000 Å, the rainbow to 7000 Å, IR above, with the same 300 Å blends — but
    /// at full strength, where the trail dims its UV and IR to sit behind the visible.
    /// </summary>
    Color WavelengthColourAt(float x, Rect rect)
    {
        double t = Mathf.InverseLerp(rect.xMin, rect.xMax, x);
        double lambda = System.Math.Exp(_lnMin + (_lnMax - _lnMin) * t);

        if (lambda < 3850.0) return uvColour;
        if (lambda < 4150.0) return Color.Lerp(uvColour, TrailSpectrumColours_NEW.Visible(4150.0), (float)((lambda - 3850.0) / 300.0));
        if (lambda <= 6850.0) return TrailSpectrumColours_NEW.Visible(lambda);
        if (lambda < 7150.0) return Color.Lerp(TrailSpectrumColours_NEW.Visible(6850.0), irColour, (float)((lambda - 6850.0) / 300.0));
        return irColour;
    }

    /// <summary>
    /// The LAF art's line: violet at the blue end through blue, teal and green to yellow
    /// at the red end — short wavelengths to long, left to right, as the graph is laid out.
    /// </summary>
    static Gradient DefaultLineGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.48f, 0.20f, 0.85f), 0f),
                new GradientColorKey(new Color(0.20f, 0.45f, 1.00f), 0.25f),
                new GradientColorKey(new Color(0.25f, 0.70f, 0.60f), 0.50f),
                new GradientColorKey(new Color(0.45f, 0.85f, 0.25f), 0.75f),
                new GradientColorKey(new Color(0.95f, 0.95f, 0.10f), 1f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }

    static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float lineThickness, Color c)
    {
        AddLine(vh, a, b, lineThickness, c, c);
    }

    static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float lineThickness, Color ca, Color cb)
    {
        Vector2 dir = b - a;
        if (dir.sqrMagnitude <= 0.000001f) return;
        dir.Normalize();

        Vector2 normal = new Vector2(-dir.y, dir.x) * (lineThickness * 0.5f);

        UIVertex v = UIVertex.simpleVert;
        int idx = vh.currentVertCount;

        v.color = ca;
        v.position = a - normal; vh.AddVert(v);
        v.position = a + normal; vh.AddVert(v);
        v.color = cb;
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

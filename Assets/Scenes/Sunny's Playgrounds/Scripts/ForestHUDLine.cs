using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class ForestHUDLine : MaskableGraphic
{
    [Header("Samples (scrolling)")]
    [Min(2)] public int maxSamples = 120;
    [SerializeField] private List<float> samples = new List<float>();

    [Header("Y Range")]
    public float minY = 0f;
    public float maxY = 100f;
    public bool clampToRange = true;
    public bool autoRange = false;
    public float autoRangePadding = 1f;

    [Header("Graph Line")]
    [Min(1f)] public float lineThickness = 3f;

    [Header("Axes")]
    public bool drawAxes = true;
    [Min(1f)] public float axisThickness = 2f;
    public Color axisColor = Color.white;
    // 0 = left, 0.5 = middle, 1 = right
    [Range(0f, 1f)] public float xAxisAnchorY = 0f;  // where X-axis sits vertically (0 = bottom)
    [Range(0f, 1f)] public float yAxisAnchorX = 0f;  // where Y-axis sits horizontally (0 = left)

    public void Clear()
    {
        samples.Clear();
        SetVerticesDirty();
    }

    public void AddSample(float v)
    {
        samples.Add(v);
        if (samples.Count > maxSamples)
            samples.RemoveRange(0, samples.Count - maxSamples);

        SetVerticesDirty();
    }

    public void AddSamples(IList<float> vs)
    {
        if (vs == null || vs.Count == 0) return;

        samples.AddRange(vs);
        if (samples.Count > maxSamples)
            samples.RemoveRange(0, samples.Count - maxSamples);

        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect r = rectTransform.rect;
        float w = r.width;
        float h = r.height;

        // Draw axes first so graph line appears on top
        if (drawAxes)
        {
            Color32 axisCol32 = axisColor;

        // Y axis (vertical)
        float yAxisXPos = Mathf.Lerp(r.xMin, r.xMax, yAxisAnchorX);
        Vector2 yAxisBottom = new Vector2(yAxisXPos, r.yMin);
        Vector2 yAxisTop = new Vector2(yAxisXPos, r.yMax);
        AddLineSegment(vh, yAxisBottom, yAxisTop, axisThickness, axisCol32);

        // X axis (horizontal)
        float xAxisYPos = Mathf.Lerp(r.yMin, r.yMax, xAxisAnchorY);
        Vector2 xAxisLeft  = new Vector2(r.xMin, xAxisYPos);
        Vector2 xAxisRight = new Vector2(r.xMax, xAxisYPos);
        AddLineSegment(vh, xAxisLeft, xAxisRight, axisThickness, axisCol32);
        }

        if (samples == null || samples.Count < 2) return;

        float min = minY, max = maxY;

        if (autoRange)
        {
            min = float.PositiveInfinity;
            max = float.NegativeInfinity;
            for (int i = 0; i < samples.Count; i++)
            {
                float s = samples[i];
                if (s < min) min = s;
                if (s > max) max = s;
            }
            if (Mathf.Approximately(min, max))
            {
                min -= 0.5f;
                max += 0.5f;
            }
            min -= autoRangePadding;
            max += autoRangePadding;
        }

        int n = samples.Count;
        float dx = w / (n - 1);

        Vector2 Point(int i)
        {
            float y = samples[i];
            if (clampToRange && !autoRange) y = Mathf.Clamp(y, min, max);

            float t = Mathf.InverseLerp(min, max, y);
            float xPos = r.xMin + dx * i;
            float yPos = r.yMin + t * h;
            return new Vector2(xPos, yPos);
        }

        Color32 lineCol32 = color;
        Vector2 p0 = Point(0);
        for (int i = 1; i < n; i++)
        {
            Vector2 p1 = Point(i);
            AddLineSegment(vh, p0, p1, lineThickness, lineCol32);
            p0 = p1;
        }
    }

    static void AddLineSegment(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color32 col)
    {
        Vector2 dir = b - a;
        float len = dir.magnitude;
        if (len <= 0.0001f) return;

        dir /= len;
        Vector2 n = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);

        Vector2 v0 = a - n;
        Vector2 v1 = a + n;
        Vector2 v2 = b + n;
        Vector2 v3 = b - n;

        int idx = vh.currentVertCount;
        vh.AddVert(v0, col, Vector2.zero);
        vh.AddVert(v1, col, Vector2.zero);
        vh.AddVert(v2, col, Vector2.zero);
        vh.AddVert(v3, col, Vector2.zero);
        vh.AddTriangle(idx + 0, idx + 1, idx + 2);
        vh.AddTriangle(idx + 0, idx + 2, idx + 3);
    }
}
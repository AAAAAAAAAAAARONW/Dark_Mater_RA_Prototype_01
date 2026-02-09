using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple UI line graph for HUD. Attach to a UI GameObject with RectTransform.
/// Use SetValues() to provide a time series; it draws a thick polyline.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UILineGraph : Graphic
{
    [Header("Line")]
    [SerializeField] float thickness = 4f;
    [Header("Layout")]
    [Tooltip("If < 1, the graph is drawn in the top portion of the rect.")]
    [Range(0.1f, 1f)]
    [SerializeField] float graphHeightPercent = 0.8f;
    [Tooltip("Anchor graph to the top of the rect.")]
    [SerializeField] bool anchorTop = true;
    [Header("Axes")]
    [SerializeField] bool drawAxes = true;
    [SerializeField] float axisThickness = 2f;
    [SerializeField] Color axisColor = new Color(1f, 1f, 1f, 0.9f);

    readonly List<float> _values = new List<float>();
    float _minValue = 0f;
    float _maxValue = 1f;

    public void SetValues(IReadOnlyList<float> values, float minValue, float maxValue)
    {
        _values.Clear();
        if (values != null)
        {
            for (int i = 0; i < values.Count; i++)
                _values.Add(values[i]);
        }
        _minValue = minValue;
        _maxValue = Mathf.Max(minValue + 0.0001f, maxValue);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (_values.Count < 2) return;

        Rect rect = GetPixelAdjustedRect();
        float width = rect.width;
        float height = rect.height * graphHeightPercent;
        float halfThickness = thickness * 0.5f;
        float yBase = anchorTop ? (rect.yMax - height) : rect.yMin;

        if (drawAxes)
        {
            AddLine(vh, new Vector2(rect.xMin, yBase + height), new Vector2(rect.xMax, yBase + height), axisThickness, axisColor);
            AddLine(vh, new Vector2(rect.xMin, yBase), new Vector2(rect.xMin, yBase + height), axisThickness, axisColor);
        }

        int count = _values.Count;
        float stepX = width / (count - 1);

        Vector2 prev = new Vector2(rect.xMin, yBase + Normalize(_values[0]) * height);

        for (int i = 1; i < count; i++)
        {
            float x = rect.xMin + stepX * i;
            float y = yBase + Normalize(_values[i]) * height;
            Vector2 curr = new Vector2(x, y);

            AddLine(vh, prev, curr, thickness, color);

            prev = curr;
        }
    }

    void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float lineThickness, Color lineColor)
    {
        Vector2 dir = (b - a).normalized;
        Vector2 normal = new Vector2(-dir.y, dir.x) * (lineThickness * 0.5f);

        int idx = vh.currentVertCount;
        vh.AddVert(a - normal, lineColor, Vector2.zero);
        vh.AddVert(a + normal, lineColor, Vector2.zero);
        vh.AddVert(b + normal, lineColor, Vector2.zero);
        vh.AddVert(b - normal, lineColor, Vector2.zero);

        vh.AddTriangle(idx, idx + 1, idx + 2);
        vh.AddTriangle(idx, idx + 2, idx + 3);
    }

    float Normalize(float v)
    {
        return Mathf.InverseLerp(_minValue, _maxValue, v);
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UGUI line graph that renders inside Canvas via VertexHelper.
/// Works with Screen Space Overlay/Camera/World Space canvases.
/// </summary>
public class UICanvasLineGraph : Graphic
{
    [Header("Line")]
    [SerializeField] float thickness = 3f;

    [Header("Layout")]
    [Range(0.1f, 1f)]
    [SerializeField] float graphHeightPercent = 0.8f;
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

        Rect rect = rectTransform.rect;
        float width = rect.width;
        float height = rect.height * graphHeightPercent;
        float yBase = anchorTop ? (rect.yMax - height) : rect.yMin;

        if (drawAxes)
        {
            AddLine(vh, new Vector2(rect.xMin, yBase + height), new Vector2(rect.xMax, yBase + height), axisThickness, axisColor);
            AddLine(vh, new Vector2(rect.xMin, yBase), new Vector2(rect.xMin, yBase + height), axisThickness, axisColor);
        }

        float stepX = width / (_values.Count - 1);
        Vector2 prev = new Vector2(rect.xMin, yBase + Normalize(_values[0]) * height);
        for (int i = 1; i < _values.Count; i++)
        {
            float x = rect.xMin + stepX * i;
            float y = yBase + Normalize(_values[i]) * height;
            Vector2 curr = new Vector2(x, y);
            AddLine(vh, prev, curr, thickness, color);
            prev = curr;
        }
    }

    void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float lineThickness, Color c)
    {
        Vector2 dir = b - a;
        if (dir.sqrMagnitude <= 0.000001f) return;
        dir.Normalize();
        Vector2 normal = new Vector2(-dir.y, dir.x) * (lineThickness * 0.5f);

        UIVertex v = UIVertex.simpleVert;
        v.color = c;

        int idx = vh.currentVertCount;

        v.position = a - normal;
        vh.AddVert(v);
        v.position = a + normal;
        vh.AddVert(v);
        v.position = b + normal;
        vh.AddVert(v);
        v.position = b - normal;
        vh.AddVert(v);

        vh.AddTriangle(idx, idx + 1, idx + 2);
        vh.AddTriangle(idx, idx + 2, idx + 3);
    }

    float Normalize(float v)
    {
        return Mathf.InverseLerp(_minValue, _maxValue, v);
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws a line graph directly on a UI Image via BaseMeshEffect.
/// Attach this to the same GameObject as Image.
/// </summary>
[RequireComponent(typeof(Graphic))]
public class UIImageLineGraphEffect : BaseMeshEffect
{
    [Header("Line")]
    [SerializeField] float thickness = 3f;
    [SerializeField] Color lineColor = Color.white;
    [SerializeField] bool drawHighlightLine = true;
    [SerializeField] float highlightThickness = 1.5f;
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

        if (graphic != null)
            graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive()) return;
        vh.Clear();
        if (_values.Count < 2) return;

        Rect rect = graphic.rectTransform.rect;
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

            if (drawAreaFill)
                AddFilledStrip(vh, prev, curr, yBase, fillTopColor, fillBottomColor);

            AddLine(vh, prev, curr, thickness, lineColor);
            if (drawHighlightLine)
                AddLine(vh, prev, curr, highlightThickness, highlightColor);
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

    void AddFilledStrip(VertexHelper vh, Vector2 topA, Vector2 topB, float yBottom, Color topColor, Color bottomColor)
    {
        UIVertex v = UIVertex.simpleVert;
        int idx = vh.currentVertCount;

        v.color = topColor;
        v.position = topA;
        vh.AddVert(v);
        v.position = topB;
        vh.AddVert(v);

        v.color = bottomColor;
        v.position = new Vector2(topB.x, yBottom);
        vh.AddVert(v);
        v.position = new Vector2(topA.x, yBottom);
        vh.AddVert(v);

        vh.AddTriangle(idx, idx + 1, idx + 2);
        vh.AddTriangle(idx, idx + 2, idx + 3);
    }
}

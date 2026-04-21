using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
/// <summary>
/// Minimal UI line graph in RectTransform.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class SpectrumLineGraph : MaskableGraphic
{
    [Header("Line")]
    [SerializeField] float lineThickness = 2f;
    [SerializeField] float padding = 8f;
    [Header("Viewport (flux)")]
    [SerializeField] float viewportFluxMin = 0f;
    [SerializeField] float viewportFluxMax = 2f;
    [Header("Axes (plot border)")]
    [SerializeField] bool showAxes = true;
    [SerializeField] float axisThickness = 2f;
    [SerializeField] Color axisColor = new Color(0.25f, 0.25f, 0.25f, 1f);
    readonly List<Vector2> _scratch = new List<Vector2>(256);
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
    }
    /// <summary>Call when the visible window changes.</summary>
    public void SetFluxSeries(IReadOnlyList<SpectrumSample> window)
    {
        if (window == null || window.Count < 2) return;
        var vh = new VertexHelper();
        BuildMesh(vh, window);
        vh.FillMesh(workerMesh);
        canvasRenderer.SetMesh(workerMesh);
    }
    private void BuildMesh(VertexHelper vh, IReadOnlyList<SpectrumSample> window)
    {
        float minY = viewportFluxMin;
        float maxY = viewportFluxMax;
        for (int i = 0; i < window.Count; i++)
        {
            float y = window[i].Flux;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }
        if (Mathf.Approximately(minY, maxY))
        {
            minY -= 1f;
            maxY += 1f;
        }
        Rect r = rectTransform.rect;
        float innerW = Mathf.Max(1f, r.width - 2f * padding);
        float innerH = Mathf.Max(1f, r.height - 2f * padding);
        float left = r.xMin + padding;
        float bottom = r.yMin + padding;
        Color32 axisC = axisColor;
        if (showAxes)
        {
            AddThickSegment(vh, new Vector2(left, bottom), new Vector2(left + innerW, bottom), axisThickness, axisC);
            AddThickSegment(vh, new Vector2(left, bottom), new Vector2(left, bottom + innerH), axisThickness, axisC);
        }
        int n = window.Count;
        _scratch.Clear();
        for (int i = 0; i < n; i++)
        {
            float t = (n == 1) ? 0f : i / (float)(n - 1);
            float x = left + t * innerW;
            float yn = (window[i].Flux - minY) / (maxY - minY);
            float y = bottom + yn * innerH;
            _scratch.Add(new Vector2(x, y));
        }
        Color32 c32 = color;
        for (int i = 0; i < _scratch.Count - 1; i++)
            AddThickSegment(vh, _scratch[i], _scratch[i + 1], lineThickness, c32);
    }
    private static void AddThickSegment(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color32 color)
    {
        Vector2 d = b - a;
        float len = d.magnitude;
        if (len < 1e-5f) return;
        Vector2 n = new Vector2(-d.y, d.x) / len * (thickness * 0.5f);
        int start = vh.currentVertCount;
        vh.AddVert(a - n, color, Vector2.zero);
        vh.AddVert(a + n, color, Vector2.zero);
        vh.AddVert(b - n, color, Vector2.zero);
        vh.AddVert(b + n, color, Vector2.zero);
        vh.AddTriangle(start + 0, start + 1, start + 2);
        vh.AddTriangle(start + 2, start + 1, start + 3);
    }
}
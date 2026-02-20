using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple UI line graph for HUD. Attach to a GameObject with RectTransform and MeshFilter/MeshRenderer.
/// Use SetValues() to provide a time series; it draws a thick polyline.
/// Works without UnityEngine.UI (Unity 2019 compatible). Assign a Material to the MeshRenderer.
/// </summary>
[RequireComponent(typeof(RectTransform), typeof(MeshFilter), typeof(MeshRenderer))]
public class UILineGraph : MonoBehaviour
{
    [Header("Line")]
    [SerializeField] float thickness = 4f;
    [SerializeField] Color lineColor = Color.white;
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

    Mesh _mesh;
    MeshFilter _meshFilter;

    void Awake()
    {
        _mesh = new Mesh();
        _mesh.name = "UILineGraph";
        _meshFilter = GetComponent<MeshFilter>();
        _meshFilter.sharedMesh = _mesh;
    }

    void OnDestroy()
    {
        if (_mesh != null)
            Destroy(_mesh);
    }

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
        RebuildMesh();
    }

    void RebuildMesh()
    {
        if (_mesh == null || _meshFilter == null) return;

        _mesh.Clear();
        if (_values.Count < 2) return;

        RectTransform rt = GetComponent<RectTransform>();
        Rect rect = rt.rect;
        float width = rect.width;
        float height = rect.height * graphHeightPercent;
        float yBase = anchorTop ? (rect.yMax - height) : rect.yMin;

        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var colors = new List<Color>();

        if (drawAxes)
        {
            AddLineToMesh(vertices, triangles, colors,
                new Vector2(rect.xMin, yBase + height), new Vector2(rect.xMax, yBase + height), axisThickness, axisColor);
            AddLineToMesh(vertices, triangles, colors,
                new Vector2(rect.xMin, yBase), new Vector2(rect.xMin, yBase + height), axisThickness, axisColor);
        }

        int count = _values.Count;
        float stepX = width / (count - 1);
        Vector2 prev = new Vector2(rect.xMin, yBase + Normalize(_values[0]) * height);

        for (int i = 1; i < count; i++)
        {
            float x = rect.xMin + stepX * i;
            float y = yBase + Normalize(_values[i]) * height;
            Vector2 curr = new Vector2(x, y);
            AddLineToMesh(vertices, triangles, colors, prev, curr, thickness, lineColor);
            prev = curr;
        }

        _mesh.SetVertices(vertices);
        _mesh.SetTriangles(triangles, 0);
        _mesh.SetColors(colors);
        _mesh.RecalculateBounds();
    }

    void AddLineToMesh(List<Vector3> vertices, List<int> triangles, List<Color> colors,
        Vector2 a, Vector2 b, float lineThickness, Color lineColor)
    {
        Vector2 dir = (b - a).normalized;
        Vector2 normal = new Vector2(-dir.y, dir.x) * (lineThickness * 0.5f);

        int idx = vertices.Count;
        vertices.Add(a - normal);
        vertices.Add(a + normal);
        vertices.Add(b + normal);
        vertices.Add(b - normal);

        for (int i = 0; i < 4; i++) colors.Add(lineColor);

        triangles.Add(idx);
        triangles.Add(idx + 1);
        triangles.Add(idx + 2);
        triangles.Add(idx);
        triangles.Add(idx + 2);
        triangles.Add(idx + 3);
    }

    float Normalize(float v)
    {
        return Mathf.InverseLerp(_minValue, _maxValue, v);
    }
}

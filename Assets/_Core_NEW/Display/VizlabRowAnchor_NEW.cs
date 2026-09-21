using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Puts a UI element on one of the display's seven rows, by row number.
///
/// WHAT PROBLEM THIS SOLVES: the surface is 7560 px tall and bends 124 degrees over that
/// run, so "which row is this on" is the placement question that actually matters, and it
/// is the one Unity's anchor widget cannot express. Answering it by hand means knowing that
/// row 3 is canvas y 4320 to 5400 and typing 4860. This component turns that into picking
/// a number from 1 to 7.
///
/// It runs in edit mode and writes ordinary anchors, so the result is a normal RectTransform
/// that behaves normally afterwards, and it stays correct if the canvas is resized. Remove
/// the component and the layout it produced stays put.
///
/// HOW IT MEASURES: the row band is resolved against the root Canvas, then expressed as
/// anchors in this element's own parent. So it is correct whether the element sits directly
/// under the canvas or inside a container, and it does not depend on the unconfirmed
/// 5 x 1920 per-row figure - rows are seven equal bands whatever the pixel count turns out
/// to be.
///
/// Geometry and the placement advice come from VizlabDisplay_NEW.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[HierarchyBadge_NEW("ROW", "#C08A2E")]
public class VizlabRowAnchor_NEW : MonoBehaviour
{
    public enum Horizontal { Fill, Left, Centre, Right }
    public enum Vertical { Fill, Bottom, Middle, Top }

    [Header("Row")]
    [Tooltip("1 is the top row, nearly overhead. 7 is the floor row. Row 5 is eye level and " +
             "is the safe area for anything persistent.")]
    [Range(1, VizlabDisplay_NEW.RowCount)]
    [SerializeField] int row = VizlabDisplay_NEW.SafeAreaRow;

    [Header("Placement within the row")]
    [SerializeField] Horizontal horizontal = Horizontal.Centre;
    [SerializeField] Vertical vertical = Vertical.Middle;

    [Tooltip("Canvas pixels of inset from the row's edges. Only the edges an alignment " +
             "actually touches are used: Centre ignores left and right, Fill uses both.")]
    [SerializeField] RectOffset margins = new RectOffset();

    [Tooltip("A free nudge in canvas pixels, applied after everything else. For fine " +
             "adjustment without disturbing the alignment.")]
    [SerializeField] Vector2 nudge = Vector2.zero;

    /// <summary>1 is the top row, 7 is the floor row.</summary>
    public int Row
    {
        get { return row; }
        set { row = Mathf.Clamp(value, 1, VizlabDisplay_NEW.RowCount); Apply(); }
    }

    /// <summary>True when this row is one the geometry says to keep clear of.</summary>
    public bool IsOnAPoorRow { get { return VizlabDisplay_NEW.IsPoorPlacement(row); } }

    RectTransform Rt { get { return (RectTransform)transform; } }

    void OnEnable() { Apply(); }

    /// <summary>Writes the anchors for the current row and alignment. Safe to call repeatedly.</summary>
    [ContextMenu("Apply row placement")]
    public void Apply()
    {
        RectTransform rt = Rt;
        var parent = rt.parent as RectTransform;
        if (parent == null) return;

        RectTransform canvasRt = FindCanvasRect(rt);
        if (canvasRt == null) return;

        Rect parentRect = parent.rect;
        if (parentRect.width <= 0f || parentRect.height <= 0f) return;

        // The row band, first in canvas space, then as a fraction of this element's parent.
        // Going through the canvas is what lets the component sit at any depth.
        float fracMin, fracMax;
        VizlabDisplay_NEW.GetRowAnchorFractions(row, out fracMin, out fracMax);

        Rect canvasRect = canvasRt.rect;
        Vector3 bandMinInCanvas = new Vector3(canvasRect.xMin,
                                              canvasRect.yMin + fracMin * canvasRect.height, 0f);
        Vector3 bandMaxInCanvas = new Vector3(canvasRect.xMax,
                                              canvasRect.yMin + fracMax * canvasRect.height, 0f);

        Vector3 bandMin = parent.InverseTransformPoint(canvasRt.TransformPoint(bandMinInCanvas));
        Vector3 bandMax = parent.InverseTransformPoint(canvasRt.TransformPoint(bandMaxInCanvas));

        var anchorMin = new Vector2((bandMin.x - parentRect.xMin) / parentRect.width,
                                    (bandMin.y - parentRect.yMin) / parentRect.height);
        var anchorMax = new Vector2((bandMax.x - parentRect.xMin) / parentRect.width,
                                    (bandMax.y - parentRect.yMin) / parentRect.height);

        Vector2 pivot = rt.pivot;
        Vector2 size = rt.rect.size;   // preserved on any axis that is not filling
        Vector2 finalAnchorMin = anchorMin;
        Vector2 finalAnchorMax = anchorMax;
        Vector2 sizeDelta = size;
        Vector2 position = Vector2.zero;

        // ---- Horizontal ----
        switch (horizontal)
        {
            case Horizontal.Fill:
                sizeDelta.x = -(margins.left + margins.right);
                position.x = margins.left - pivot.x * (margins.left + margins.right);
                break;

            case Horizontal.Left:
                finalAnchorMax.x = anchorMin.x;
                position.x = margins.left + pivot.x * size.x;
                break;

            case Horizontal.Right:
                finalAnchorMin.x = anchorMax.x;
                position.x = -margins.right - (1f - pivot.x) * size.x;
                break;

            case Horizontal.Centre:
                float midX = (anchorMin.x + anchorMax.x) * 0.5f;
                finalAnchorMin.x = midX;
                finalAnchorMax.x = midX;
                position.x = (pivot.x - 0.5f) * size.x;
                break;
        }

        // ---- Vertical ----
        switch (vertical)
        {
            case Vertical.Fill:
                sizeDelta.y = -(margins.top + margins.bottom);
                position.y = margins.bottom - pivot.y * (margins.bottom + margins.top);
                break;

            case Vertical.Bottom:
                finalAnchorMax.y = anchorMin.y;
                position.y = margins.bottom + pivot.y * size.y;
                break;

            case Vertical.Top:
                finalAnchorMin.y = anchorMax.y;
                position.y = -margins.top - (1f - pivot.y) * size.y;
                break;

            case Vertical.Middle:
                float midY = (anchorMin.y + anchorMax.y) * 0.5f;
                finalAnchorMin.y = midY;
                finalAnchorMax.y = midY;
                position.y = (pivot.y - 0.5f) * size.y;
                break;
        }

        rt.anchorMin = finalAnchorMin;
        rt.anchorMax = finalAnchorMax;
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = position + nudge;
    }

    /// <summary>
    /// The root Canvas's RectTransform. Walks up rather than using GetComponentInParent so a
    /// nested Canvas does not silently become the reference frame.
    /// </summary>
    static RectTransform FindCanvasRect(RectTransform from)
    {
        Canvas canvas = from.GetComponentInParent<Canvas>();
        if (canvas == null) return null;
        return canvas.rootCanvas.transform as RectTransform;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        margins = margins ?? new RectOffset();
        row = Mathf.Clamp(row, 1, VizlabDisplay_NEW.RowCount);

        // Transforms cannot be written during OnValidate, so the write is deferred a tick.
        EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            Apply();
        };
    }

#endif
}

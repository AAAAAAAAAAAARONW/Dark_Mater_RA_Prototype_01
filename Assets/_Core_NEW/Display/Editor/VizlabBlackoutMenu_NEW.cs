using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sets up a row blackout behind a row-shiftable element with one menu item, so the setup
/// in PlaytestBuild_NEW_Row7Blackout can be repeated in any other scene without building a
/// canvas by hand. Select the LAF HUD (after Make Selection Row-Shiftable) and run it.
///
/// It sits next to VizlabDisplayMenu_NEW rather than inside it, so that file stays as its
/// author left it. The item appears in the same GameObject > Vizlab submenu.
///
/// Nothing here renders or ships.
/// </summary>
static class VizlabBlackoutMenu_NEW
{
    /// <summary>
    /// Every HUD canvas in the project sits at 0, so this is safely beneath all of them. It
    /// is still an overlay canvas, so it draws over the 3D world.
    /// </summary>
    const int BandSortOrder = -10;

    [MenuItem("GameObject/Vizlab/Black Out This Element's Row", true, 14)]
    static bool ValidateBlackOutRow()
    {
        GameObject go = Selection.activeGameObject;
        return go != null
            && go.GetComponent<VizlabRowAnchor_NEW>() != null
            && go.GetComponentInParent<Canvas>() != null;
    }

    /// <summary>
    /// Adds an overlay canvas just above the element's canvas in the hierarchy, with a
    /// full-width black band on it that follows the element's row. Running it again selects
    /// the band that already exists instead of adding a second one.
    /// </summary>
    [MenuItem("GameObject/Vizlab/Black Out This Element's Row", false, 14)]
    static void BlackOutRow()
    {
        GameObject go = Selection.activeGameObject;
        var anchor = go.GetComponent<VizlabRowAnchor_NEW>();

        foreach (VizlabRowBlackout_NEW existing in Object.FindObjectsOfType<VizlabRowBlackout_NEW>())
        {
            if (existing.Following != anchor) continue;

            Selection.activeObject = existing.gameObject;
            Debug.Log("Vizlab: '" + go.name + "' already has a blackout band, '" + existing.name +
                      "'. Selected it rather than adding another.", existing);
            return;
        }

        Canvas hudCanvas = go.GetComponentInParent<Canvas>().rootCanvas;

        if (hudCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            Debug.LogWarning("Vizlab: '" + hudCanvas.name + "' is not a Screen Space - Overlay " +
                             "canvas. The band is made as an overlay anyway, which is what the " +
                             "row layout assumes, but check that it lines up with the HUD.", hudCanvas);

        var canvasGo = new GameObject(go.name + " Blackout Canvas", typeof(RectTransform), typeof(Canvas));
        canvasGo.layer = LayerMask.NameToLayer("UI");
        canvasGo.transform.SetParent(hudCanvas.transform.parent, false);
        canvasGo.transform.SetSiblingIndex(hudCanvas.transform.GetSiblingIndex());

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = Mathf.Min(BandSortOrder, hudCanvas.sortingOrder - 1);

        var bandGo = new GameObject(go.name + " Blackout Band",
                                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bandGo.layer = canvasGo.layer;
        bandGo.transform.SetParent(canvasGo.transform, false);

        var image = bandGo.GetComponent<Image>();
        image.raycastTarget = false;
        image.color = Color.black;

        var blackout = bandGo.AddComponent<VizlabRowBlackout_NEW>();
        blackout.SetFollow(anchor);
        blackout.Refresh();

        // Registered once the hierarchy is complete, so a single undo removes all of it.
        Undo.RegisterCreatedObjectUndo(canvasGo, "Black Out Element's Row");
        Selection.activeObject = bandGo;

        Debug.Log(string.Format(
            "Vizlab: '{0}' now blacks out row {1} edge to edge, on '{2}' at sort order {3}, " +
            "beneath the HUD. It follows the row as RB / LB move the element. In play mode: " +
            "B / pad View toggles it, N / pad Menu switches Row / RowAndBelow, F4 shows the readout.",
            go.name, anchor.Row, canvasGo.name, canvas.sortingOrder), bandGo);
    }
}

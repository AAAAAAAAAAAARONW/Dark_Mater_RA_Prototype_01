using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The setup steps for designing against the Vizlab surface, as menu items, so none of them
/// has to be done by hand against a table of numbers.
///
/// Nothing here renders or ships.
/// </summary>
static class VizlabDisplayMenu_NEW
{
    /// <summary>
    /// The Game View is registered at this fraction of the real surface as well as at full
    /// size. Aspect ratio is what a layout needs to be checked against, and the full surface
    /// is 72.6 megapixels - about 35 times a 1080p frame - which is enough to make the editor
    /// crawl. A fifth-scale view is the same shape for a thirty-fifth of the cost.
    /// </summary>
    const int PreviewDivisor = 5;

    // ---------------------------------------------------------------------------------

    [MenuItem("GameObject/Vizlab/Show Rows on This Canvas", true, 10)]
    static bool ValidateShowRowsOnCanvas()
    {
        return Selection.activeGameObject != null
            && Selection.activeGameObject.GetComponent<Canvas>() != null;
    }

    /// <summary>
    /// Puts the row bands, the seams and the side profile onto the selected Canvas, which is
    /// where they are useful. This is the only thing that draws the rows; placement itself is
    /// VizlabRowAnchor_NEW on the element.
    /// </summary>
    [MenuItem("GameObject/Vizlab/Show Rows on This Canvas", false, 10)]
    static void ShowRowsOnCanvas()
    {
        var canvas = Selection.activeGameObject.GetComponent<Canvas>();

        if (canvas.GetComponent<VizlabCanvasOverlay_NEW>() == null)
            Undo.AddComponent<VizlabCanvasOverlay_NEW>(canvas.gameObject);

        Selection.activeObject = canvas.gameObject;
        EditorUtility.SetDirty(canvas);

        // Gizmos are what this draws, so if they are switched off in the Scene View nothing
        // appears and it looks broken. Say so rather than letting it be a mystery.
        Debug.Log("Vizlab: row overlay added to '" + canvas.name + "'. It draws as Scene View " +
                  "gizmos - if you see nothing, check the Gizmos toggle in the Scene View " +
                  "toolbar, and frame the Canvas (select it and press F).", canvas);
    }

    // ---------------------------------------------------------------------------------

    [MenuItem("GameObject/Vizlab/Set Canvas to Full Surface (9600 x 7560)", true, 11)]
    static bool ValidateSetFullSurface()
    {
        return Selection.activeGameObject != null
            && Selection.activeGameObject.GetComponent<Canvas>() != null;
    }

    /// <summary>
    /// Points a Canvas Scaler at the whole seven-row surface, matching on height.
    ///
    /// Height is the right axis to match on: it is the dimension that stays put across the
    /// editor window, a 1080p test monitor and the wall, whereas matching on width makes the
    /// scale factor track an aspect ratio that changes by a factor of five between them.
    /// Matching on height is also what makes the fifth-scale Game View size below show the
    /// identical layout rather than a different one.
    /// </summary>
    [MenuItem("GameObject/Vizlab/Set Canvas to Full Surface (9600 x 7560)", false, 11)]
    static void SetFullSurface()
    {
        var canvas = Selection.activeGameObject.GetComponent<Canvas>();
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = Undo.AddComponent<CanvasScaler>(canvas.gameObject);

        Undo.RecordObject(scaler, "Set Canvas to Vizlab Full Surface");
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(VizlabDisplay_NEW.CanvasWidthPx,
                                                 VizlabDisplay_NEW.CanvasHeightPx);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;   // height
        EditorUtility.SetDirty(scaler);

        Debug.Log(string.Format(
            "Vizlab: '{0}' now references {1} x {2}, matching on height. The per-row " +
            "resolution (5 x 1920) is still unconfirmed - if it changes, only " +
            "VizlabDisplay_NEW.RowWidthPx needs editing.",
            canvas.name, VizlabDisplay_NEW.CanvasWidthPx, VizlabDisplay_NEW.CanvasHeightPx), canvas);
    }

    // ---------------------------------------------------------------------------------

    [MenuItem("GameObject/Vizlab/Make Selection Row-Shiftable", true, 12)]
    static bool ValidateMakeShiftable()
    {
        return Selection.activeGameObject != null
            && Selection.activeGameObject.GetComponent<RectTransform>() != null
            && Selection.activeGameObject.GetComponentInParent<Canvas>() != null;
    }

    /// <summary>
    /// Gives the selected UI element both halves of the row workflow: the anchor that places
    /// it on a row, and the input that steps it between rows during play. For the LAF HUD,
    /// select it and run this.
    ///
    /// The anchor starts on the safe-area row and keeps the element's current alignment as
    /// best it can - centred, which is what most HUD elements already are.
    /// </summary>
    [MenuItem("GameObject/Vizlab/Make Selection Row-Shiftable", false, 12)]
    static void MakeShiftable()
    {
        GameObject go = Selection.activeGameObject;

        var anchor = go.GetComponent<VizlabRowAnchor_NEW>();
        if (anchor == null) anchor = Undo.AddComponent<VizlabRowAnchor_NEW>(go);

        if (go.GetComponent<VizlabRowShifter_NEW>() == null)
            Undo.AddComponent<VizlabRowShifter_NEW>(go);

        EditorUtility.SetDirty(go);

        Debug.Log(string.Format(
            "Vizlab: '{0}' is now on row {1} and shiftable in play mode. Page Up / Page Down " +
            "on the keyboard, RB / LB on an Xbox pad, Home to reset. Change the starting row " +
            "with the anchor's Row slider; note that rows found during play are reverted when " +
            "play stops, so copy the component values before you leave play mode.",
            go.name, anchor.Row), go);
    }

    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Adds the display's resolutions to the Game View's size dropdown, so the Game View can
    /// be pointed at the real surface instead of whatever 16:9 preset it opened with.
    ///
    /// Four entries, and the 1:5 ones are the ones to work in day to day. Because the Canvas
    /// Scaler matches on height, a fifth-scale view shows exactly the same layout at a
    /// thirty-fifth of the fill cost. Reach for the full-size entries only for a final look.
    ///
    /// UNITY INTERNALS: GameViewSizes and GameViewSize are internal, so this goes through
    /// reflection. That is fragile across Unity versions by nature, so a failure reports how
    /// to do it by hand rather than throwing.
    /// </summary>
    [MenuItem("GameObject/Vizlab/Add Vizlab Sizes to Game View", false, 13)]
    static void AddGameViewSizes()
    {
        int fullW = VizlabDisplay_NEW.CanvasWidthPx;
        int fullH = VizlabDisplay_NEW.CanvasHeightPx;
        int rowW = VizlabDisplay_NEW.RowWidthPx;
        int rowH = VizlabDisplay_NEW.RowHeightPx;

        int added = 0;
        if (TryAddGameViewSize("Vizlab Surface 1:5", fullW / PreviewDivisor, fullH / PreviewDivisor)) added++;
        if (TryAddGameViewSize("Vizlab Row 1:5", rowW / PreviewDivisor, rowH / PreviewDivisor)) added++;
        if (TryAddGameViewSize("Vizlab Surface Full", fullW, fullH)) added++;
        if (TryAddGameViewSize("Vizlab Row Full", rowW, rowH)) added++;

        if (added > 0)
        {
            Debug.Log(string.Format(
                "Vizlab: {0} Game View sizes registered. Work in 'Vizlab Surface 1:5' " +
                "({1} x {2}) - it is the same shape as the full surface but 1/{3} the pixels. " +
                "'Vizlab Surface Full' is {4} x {5}, which is 72.6 megapixels and will make " +
                "the editor crawl; open it only for a final check.",
                added, fullW / PreviewDivisor, fullH / PreviewDivisor,
                PreviewDivisor * PreviewDivisor, fullW, fullH));
        }
        else
        {
            Debug.LogWarning(string.Format(
                "Vizlab: could not add Game View sizes automatically on this Unity version. " +
                "Add them by hand: Game View > resolution dropdown > + > Fixed Resolution, " +
                "{0} x {1} for daily work and {2} x {3} for a final check.",
                fullW / PreviewDivisor, fullH / PreviewDivisor, fullW, fullH));
        }
    }

    /// <summary>Returns false rather than throwing, so a Unity version change degrades to a hint.</summary>
    static bool TryAddGameViewSize(string label, int width, int height)
    {
        try
        {
            Assembly editorAsm = typeof(Editor).Assembly;

            Type sizesType = editorAsm.GetType("UnityEditor.GameViewSizes");
            Type sizeType = editorAsm.GetType("UnityEditor.GameViewSize");
            Type sizeKind = editorAsm.GetType("UnityEditor.GameViewSizeType");
            if (sizesType == null || sizeType == null || sizeKind == null) return false;

            Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static)
                                    .GetValue(null, null);

            object group = sizesType.GetMethod("GetGroup")
                                    .Invoke(sizes, new object[] { (int)GameViewSizeGroupType.Standalone });

            if (GroupAlreadyHas(group, sizeType, label)) return true;

            // GameViewSizeType.FixedResolution is 1; AspectRatio is 0.
            object kind = Enum.ToObject(sizeKind, 1);

            ConstructorInfo ctor = sizeType.GetConstructor(new[] { sizeKind, typeof(int), typeof(int), typeof(string) });
            if (ctor == null) return false;

            object size = ctor.Invoke(new object[] { kind, width, height, label });

            group.GetType().GetMethod("AddCustomSize").Invoke(group, new object[] { size });
            sizesType.GetMethod("SaveToHDD").Invoke(sizes, null);

            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("Vizlab: Game View size registration failed - " + e.Message);
            return false;
        }
    }

    /// <summary>Keeps the menu item idempotent, so re-running it does not stack duplicates.</summary>
    static bool GroupAlreadyHas(object group, Type sizeType, string label)
    {
        try
        {
            int total = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
            MethodInfo get = group.GetType().GetMethod("GetGameViewSize");

            PropertyInfo baseText = sizeType.GetProperty("baseText");

            for (int i = 0; i < total; i++)
            {
                object size = get.Invoke(group, new object[] { i });
                if (size == null) continue;

                string text = baseText != null
                    ? baseText.GetValue(size, null) as string
                    : size.ToString();

                if (text == label) return true;
            }
        }
        catch
        {
            // A failed duplicate check is not worth failing the whole operation over; the
            // worst case is a second entry in the dropdown.
        }

        return false;
    }
}

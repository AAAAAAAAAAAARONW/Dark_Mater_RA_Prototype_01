using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Puts the LAF art on a spectrum bar — the journey's LAF panel or the tutorial's bar — so
/// the two read as one instrument on both sides of the handoff.
///
/// THE BACKGROUND IS NINE-SLICED. LAF_BG.png is 11:1 and neither bar is: stretched, its F,
/// its DISTANCE and its axis strokes would distort. The sprite's borders (set on its import)
/// hold the axis corner, both labels and the stroke widths at one scale, and only the
/// straight runs of the axes stretch. The scale is chosen so the art's plot area lands
/// exactly on the curve's rect; see the Art* constants for where the axes are in the PNG.
///
/// JOURNEY: select the graph (AllCanvas/LAF HUD/Row_2/LAF HUD/Image in Gameplay_Scene_Ana)
/// and run Tools > Journey NEW > LAF > Apply Art To Selected Graph.
///
/// TUTORIAL: the scene builder calls ApplyTutorial on the run that first adds the
/// background, so Build or Update is enough. Tools > Journey NEW > LAF > Apply Art To
/// Tutorial Bar re-applies the whole look on demand.
///
/// Both work on the open scene through Undo, so unsaved edits are kept and a run is one
/// Ctrl+Z. Safe to run again: they find what they made last time.
/// </summary>
public static class LAFArtSetup_NEW
{
    const string BackgroundPath = "Assets/_Core_NEW/UI/LAF_BG.png";
    const string ArrowPath = "Assets/_Core_NEW/UI/LAF_Arrow.png";

    const string BackgroundName = "LAF_BG";
    const string BirthArrowName = "BirthArrow";

    /// <summary>The image arrow the first version of this tool added. Removed now.</summary>
    const string OldArrowName = "LAF_DriftArrow";

    // Where the plot area sits in LAF_BG.png (7080 x 624), measured from its pixels: the
    // x-axis starts 200 px in and its top edge is 170 px up; the F axis's inner edge is
    // 268 px from the right and its top 96 px down. The curve's rect is exactly this box.
    const float ArtHeight = 624f;
    const float ArtLeft = 200f;
    const float ArtRight = 268f;
    const float ArtTop = 96f;
    const float ArtBottom = 170f;

    /// <summary>Line width in canvas pixels: a thin chart line.</summary>
    const float LineThickness = 1f;

    /// <summary>The curve's share of the plot height, hanging from the top: the 0.8 both bars
    /// were tuned with.</summary>
    const float CurveHeightOfPlot = 0.8f;

    /// <summary>LAF_Arrow.png's green: the birth point, where every line is cut.</summary>
    static readonly Color ArtGreen = new Color(0.03f, 0.95f, 0.52f, 1f);

    /// <summary>The journey highlight's cream: a line being followed.</summary>
    static readonly Color TrackCream = new Color(1f, 0.91f, 0.63f, 1f);

    /// <summary>How far LAF_Arrow.png's back edge is cut in, as a fraction of its length.</summary>
    const float ArtArrowNotch = 0.3f;

    /// <summary>LAF_Arrow.png points right; this turns it to point up.</summary>
    const float ArrowSpriteAngle = 90f;

    /// <summary>Tutorial arrows: across, and pixels between the bar and the tip.</summary>
    const float TutorialArrowSize = 18f;
    const float TutorialTrailArrowSize = 22f;
    const float TutorialArrowGap = 6f;

    /// <summary>The UV and IR tints, lowered now the line carries the colours too.</summary>
    const float BandTintAlpha = 0.06f;

    /// <summary>
    /// Scripts that used to draw the journey graph and are switched off on it. Matched by
    /// name so this file does not depend on them still existing.
    /// </summary>
    static readonly string[] LegacyScripts =
    {
        "FakeLymanAlphaForestHUD", "LAFSpectrumHUD", "UILineGraph", "UIImageLineGraphEffect",
        "ForestHUDLine"
    };

    // ── Journey ──────────────────────────────────────────────────────────────

    [MenuItem("Tools/Journey NEW/LAF/Apply Art To Selected Graph", false, 60)]
    static void ApplyJourney()
    {
        GameObject selected = Selection.activeGameObject;
        LineGraphRenderer_NEW graph = selected != null
            ? selected.GetComponentInChildren<LineGraphRenderer_NEW>(true)
            : null;

        if (graph == null)
        {
            EditorUtility.DisplayDialog("LAF art",
                "Select the LAF graph first — the object with LineGraphRenderer_NEW on it " +
                "(AllCanvas/LAF HUD/Row_2/LAF HUD/Image in Gameplay_Scene_Ana).", "OK");
            return;
        }

        RectTransform panel = graph.transform.parent as RectTransform;
        if (panel == null)
        {
            Debug.LogError("[LAFArtSetup_NEW] The graph has no RectTransform parent to put the background in.", graph);
            return;
        }

        Sprite background = LoadSprite(BackgroundPath);
        Sprite arrow = LoadSprite(ArrowPath);
        if (background == null) return;

        Undo.SetCurrentGroupName("Apply LAF art");
        int group = Undo.GetCurrentGroup();

        // Background: behind everything in the panel, filling it, at the scale that keeps
        // the art's own proportions vertically. The curve then fits its plot area.
        float m = ArtHeight / Mathf.Max(1f, panel.rect.height);

        Image bg = FindOrCreateImage(panel, BackgroundName);
        StyleBackground(bg, background, m);
        Place(bg.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        bg.transform.SetSiblingIndex(0);

        Place((RectTransform)graph.transform, Vector2.zero, Vector2.one,
              new Vector2(ArtLeft / m, ArtBottom / m), new Vector2(-ArtRight / m, -ArtTop / m));
        StyleCurve(graph);

        // The rendered anchor arrow, in the art's shape and colour.
        RedshiftMarks_NEW marks = panel.GetComponentInChildren<RedshiftMarks_NEW>(true);
        if (marks == null) marks = panel.GetComponentInParent<RedshiftMarks_NEW>();

        if (marks != null) StyleRedshiftAnchor(marks, arrow);
        else Debug.LogWarning("[LAFArtSetup_NEW] No RedshiftMarks_NEW on the panel, so there is no " +
                              "rendered arrow to restyle.", panel);

        // The old text labels: the background has F and DISTANCE drawn on it.
        foreach (TMP_Text label in panel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (label.transform.parent != panel) continue;
            if (label.name != "F" && label.name != "Distance") continue;

            Undo.RecordObject(label.gameObject, "Hide LAF label");
            label.gameObject.SetActive(false);
        }

        int removed = RemoveUnused(panel);

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);

        Debug.Log("[LAFArtSetup_NEW] LAF art applied under " + panel.name + "; removed " + removed +
                  " unused object(s)/script(s). Save the scene to keep it; Ctrl+Z undoes all of it.", panel);
    }

    // ── Tutorial ─────────────────────────────────────────────────────────────

    [MenuItem("Tools/Journey NEW/LAF/Apply Art To Tutorial Bar", false, 61)]
    static void ApplyTutorialMenu()
    {
        TutorialSpectrumBands_NEW bands = FindInScene<TutorialSpectrumBands_NEW>();
        RectTransform bar = bands != null ? bands.transform.parent as RectTransform : null;

        if (bar == null)
        {
            EditorUtility.DisplayDialog("LAF art",
                "Open the Tutorial scene first — no SpectrumBar with bands was found.", "OK");
            return;
        }

        Undo.SetCurrentGroupName("Apply LAF art to the tutorial bar");
        int group = Undo.GetCurrentGroup();

        ApplyTutorial(bar, bands, FindInScene<TutorialLineIndicator_NEW>(), true);

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(bar.gameObject.scene);

        Debug.Log("[LAFArtSetup_NEW] LAF art applied to the tutorial bar. Save the scene to keep it.", bar);
    }

    /// <summary>
    /// The tutorial bar in the LAF look. The bar's rect IS the plot: the background hangs
    /// round it. Called by the scene builder with restyle false, which builds what is
    /// missing and styles it only on the run that first adds the background, so later
    /// tuning in the scene survives Build or Update. The menu passes true.
    /// </summary>
    public static void ApplyTutorial(RectTransform bar, TutorialSpectrumBands_NEW bands,
                                     TutorialLineIndicator_NEW indicator, bool restyle)
    {
        Sprite background = LoadSprite(BackgroundPath);
        Sprite arrow = LoadSprite(ArrowPath);
        if (background == null || bar == null) return;

        bool isNew = bar.Find(BackgroundName) == null;
        bool style = restyle || isNew;

        // Background. A child, so it rides the bar's slide in D1 — but a child draws over
        // its parent, and the curve is drawn by the bar itself. A nested canvas sorted one
        // below the HUD puts it behind instead, while it still moves and fades with the bar.
        Image bg = FindOrCreateImage(bar, BackgroundName);

        if (style)
        {
            float m = (ArtHeight - ArtTop - ArtBottom) / Mathf.Max(1f, bar.rect.height);

            StyleBackground(bg, background, m);
            Place(bg.rectTransform, Vector2.zero, Vector2.one,
                  new Vector2(-ArtLeft / m, -ArtBottom / m), new Vector2(ArtRight / m, ArtTop / m));
            bg.transform.SetSiblingIndex(0);

            Canvas nested = bg.GetComponent<Canvas>();
            if (nested == null) nested = Undo.AddComponent<Canvas>(bg.gameObject);

            Canvas root = bar.GetComponentInParent<Canvas>();
            if (root != null) root = root.rootCanvas;

            Undo.RecordObject(nested, "LAF background order");
            nested.overrideSorting = true;
            nested.sortingOrder = (root != null ? root.sortingOrder : 0) - 1;

            LineGraphRenderer_NEW graph = bar.GetComponent<LineGraphRenderer_NEW>();
            if (graph != null) StyleCurve(graph);
        }

        if (bands == null) return;

        // Birth arrow: green, under the bar, where every line is cut. Positioned along the
        // bar by the bands every frame; its look is set here.
        Transform birth = bands.transform.Find(BirthArrowName);
        bool birthIsNew = birth == null;
        Image birthImage = FindOrCreateImage((RectTransform)bands.transform, BirthArrowName);

        if (style || birthIsNew)
            StyleUpArrow(birthImage, arrow, ArtGreen, TutorialArrowSize, TutorialArrowGap);

        SerializedObject bs = new SerializedObject(bands);
        SerializedProperty birthRef = bs.FindProperty("birthArrow");
        if (birthRef != null && birthRef.objectReferenceValue == null)
        {
            birthRef.objectReferenceValue = birthImage.rectTransform;
            bs.ApplyModifiedProperties();
        }

        if (!style) return;

        // The bands: still the lesson, but quieter, with the art's green labels.
        foreach (string name in new[] { "UV", "IR" })
        {
            Transform band = bands.transform.Find(name);
            Image tint = band != null ? band.GetComponent<Image>() : null;
            if (tint == null) continue;

            Undo.RecordObject(tint, "LAF band tint");
            Color c = tint.color;
            c.a = BandTintAlpha;
            tint.color = c;
        }

        foreach (TMP_Text label in bands.GetComponentsInChildren<TMP_Text>(true))
        {
            if (label.name != "Label") continue;

            Undo.RecordObject(label, "LAF band label");
            label.color = new Color(ArtGreen.r, ArtGreen.g, ArtGreen.b, 0.8f);
        }

        // The blink where a line is cut: cream, like the arrow that then follows it.
        Transform marker = bands.transform.Find("LineMarker");
        Image markerImage = marker != null ? marker.GetComponent<Image>() : null;
        if (markerImage != null)
        {
            Undo.RecordObject(markerImage, "LAF line marker");
            markerImage.color = new Color(TrackCream.r, TrackCream.g, TrackCream.b, markerImage.color.a);
        }

        // Birth arrow under the line arrow, so the followed line's arrow is on top where
        // they start together.
        Transform lineArrow = bands.transform.Find("LineArrow");
        if (lineArrow != null)
            birthImage.transform.SetSiblingIndex(Mathf.Max(0, lineArrow.GetSiblingIndex()));

        // The line arrows, on the bar and on the trail: the art's arrowhead, in cream.
        if (indicator != null)
        {
            SerializedObject so = new SerializedObject(indicator);
            SetFloat(so, "arrowSpriteAngle", ArrowSpriteAngle);
            SetFloat(so, "barArrowGap", TutorialArrowGap);

            RectTransform barArrow = so.FindProperty("barArrow").objectReferenceValue as RectTransform;
            RectTransform trailArrow = so.FindProperty("trailArrow").objectReferenceValue as RectTransform;
            so.ApplyModifiedProperties();

            if (barArrow != null)
                StyleArrowImage(barArrow.GetComponent<Image>(), arrow, TrackCream, TutorialArrowSize);
            if (trailArrow != null)
                StyleArrowImage(trailArrow.GetComponent<Image>(), arrow, TrackCream, TutorialTrailArrowSize);
        }
    }

    // ── Shared styling ───────────────────────────────────────────────────────

    static void StyleBackground(Image bg, Sprite sprite, float multiplier)
    {
        Undo.RecordObject(bg, "LAF background");
        bg.sprite = sprite;
        bg.type = Image.Type.Sliced;
        bg.fillCenter = true;
        bg.pixelsPerUnitMultiplier = multiplier;
        bg.preserveAspect = false;
        bg.raycastTarget = false;
        bg.color = Color.white;
    }

    /// <summary>A thin line in real-wavelength colours, no fill, no axes of its own.</summary>
    static void StyleCurve(LineGraphRenderer_NEW graph)
    {
        SerializedObject so = new SerializedObject(graph);
        SetBool(so, "drawAxes", false);
        SetBool(so, "drawAreaFill", false);
        SetBool(so, "colourByWavelength", true);
        SetBool(so, "useLineGradient", true);     // only until the wavelengths arrive
        SetFloat(so, "thickness", LineThickness);
        SetFloat(so, "graphHeightPercent", CurveHeightOfPlot);
        SetBool(so, "anchorTop", true);
        so.ApplyModifiedProperties();
    }

    /// <summary>RedshiftMarks_NEW's rendered birth arrow, as the art's arrowhead. Same
    /// proportions as LAF_Arrow.png turned to point up.</summary>
    static void StyleRedshiftAnchor(RedshiftMarks_NEW marks, Sprite arrow)
    {
        SerializedObject ms = new SerializedObject(marks);
        SerializedProperty style = ms.FindProperty("anchorStyle");
        if (style != null) style.enumValueIndex = 0;   // Triangle

        SetFloat(ms, "anchorTriangleNotch", ArtArrowNotch);

        SerializedProperty colour = ms.FindProperty("anchorTriangleColor");
        if (colour != null) colour.colorValue = ArtGreen;

        if (arrow != null)
        {
            SerializedProperty h = ms.FindProperty("anchorTriangleHeight");
            SerializedProperty w = ms.FindProperty("anchorTriangleWidth");
            if (h != null && w != null)
                w.floatValue = h.floatValue * arrow.rect.height / arrow.rect.width;
        }

        ms.ApplyModifiedProperties();
    }

    /// <summary>An arrow placed by its own script: sprite, colour and size only.</summary>
    static void StyleArrowImage(Image image, Sprite arrow, Color colour, float across)
    {
        if (image == null || arrow == null) return;

        Undo.RecordObject(image, "LAF arrow");
        image.sprite = arrow;
        image.preserveAspect = true;
        image.raycastTarget = false;
        // Keep the alpha: the indicator fades these through their CanvasGroups and colour.
        image.color = new Color(colour.r, colour.g, colour.b, image.color.a);

        RectTransform rect = image.rectTransform;
        Undo.RecordObject(rect, "LAF arrow");
        rect.sizeDelta = new Vector2(across * arrow.rect.width / arrow.rect.height, across);
    }

    /// <summary>A static arrow hanging under the bar, tip up, tip at its anchor.</summary>
    static void StyleUpArrow(Image image, Sprite arrow, Color colour, float across, float gap)
    {
        if (arrow == null) return;

        StyleArrowImage(image, arrow, colour, across);
        image.color = colour;

        RectTransform rect = image.rectTransform;
        Undo.RecordObject(rect, "LAF arrow");
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = TutorialLineIndicator_NEW.TipPivot(ArrowSpriteAngle);
        rect.localEulerAngles = new Vector3(0f, 0f, ArrowSpriteAngle);
        rect.anchoredPosition = new Vector2(0f, -gap);
    }

    // ── Cleanup ──────────────────────────────────────────────────────────────

    /// <summary>
    /// The image arrow from the first version of this tool, and the switched-off legacy
    /// graph scripts on the journey's LAF HUD. Only DISABLED legacy scripts are removed:
    /// one that somebody has turned back on is presumably in use, so it is reported instead.
    /// </summary>
    static int RemoveUnused(RectTransform panel)
    {
        int removed = 0;

        Transform oldArrow = panel.Find(OldArrowName);
        if (oldArrow != null)
        {
            Undo.DestroyObjectImmediate(oldArrow.gameObject);
            removed++;
        }

        // The whole LAF HUD, not just this row: ForestHUDLine sits on a sibling of the rows.
        // Up to the nearest Canvas, which is the LAF HUD itself or the canvas holding it.
        Transform hud = panel;
        while (hud.parent != null && hud.GetComponent<Canvas>() == null) hud = hud.parent;

        foreach (MonoBehaviour mb in hud.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            string type = mb.GetType().Name;
            if (System.Array.IndexOf(LegacyScripts, type) < 0) continue;

            if (mb.enabled)
            {
                Debug.LogWarning("[LAFArtSetup_NEW] Left " + type + " on " + mb.name +
                                 ": it is enabled, so something may still use it.", mb);
                continue;
            }

            Undo.DestroyObjectImmediate(mb);
            removed++;
        }

        return removed;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            Debug.LogError("[LAFArtSetup_NEW] Missing art. Expected a sprite (2D and UI) at " + path + ".");
        return sprite;
    }

    /// <summary>The first one in an open scene, active or not (2019.4 has no includeInactive find).</summary>
    static T FindInScene<T>() where T : Component
    {
        foreach (T t in Resources.FindObjectsOfTypeAll<T>())
            if (t.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(t)) return t;
        return null;
    }

    static Image FindOrCreateImage(RectTransform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            Image found = existing.GetComponent<Image>();
            return found != null ? found : Undo.AddComponent<Image>(existing.gameObject);
        }

        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return go.GetComponent<Image>();
    }

    static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        Undo.RecordObject(rect, "LAF layout");
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        // The journey graph used to be a 100 x 100 rect stretched to a bar by a scale of
        // (5.08, 0.51). Anchored to the plot instead, that scale would blow it five times
        // past the axes, and squash the line's thickness vertically on top.
        rect.localScale = Vector3.one;
    }

    static void SetBool(SerializedObject so, string field, bool value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p != null) p.boolValue = value;
    }

    static void SetFloat(SerializedObject so, string field, float value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p != null) p.floatValue = value;
    }
}

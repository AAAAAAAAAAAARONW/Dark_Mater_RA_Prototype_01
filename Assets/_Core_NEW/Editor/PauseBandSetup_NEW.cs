using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Turns the pause card of the open scene into the wall version: a narrow black band
/// across the middle of the picture with the drawn PAUSED (UI/Pause_Title.png) in it, and
/// the drawn PRESS B - RESUME plate low in the frame, as in the wall mock. The full-screen
/// dim it had is switched off: the band says the world has stopped without greying out the
/// whole wall.
///
/// Vizlab_tut: restyles the PauseCard the tutorial builder made.
/// Vizlab_Journey: builds the same card on the journey HUD's canvas (TutorialPauseCard_NEW
/// follows JourneyPause_NEW when there is no tutorial pause), adds JourneyPause_NEW if the
/// scene has none, and takes the resume words off the hint line, since the plate says it.
///
/// Every size is an anchor fraction of the canvas, so it holds at any resolution; the
/// drawn pieces keep their proportions through an AspectRatioFitter. Through Undo, so the
/// scene changes are one Ctrl+Z. Safe to run again.
/// </summary>
public static class PauseBandSetup_NEW
{
    const string TitlePath = "Assets/_Core_NEW/UI/Pause_Title.png";
    const string ResumePath = "Assets/_Core_NEW/Tutorial/Assets/Hints/TutorialHint_Resume.png";

    // Fractions of the canvas height from the bottom, measured on the wall mock.
    const float BandBottom = 0.485f, BandTop = 0.605f;
    const float TitleFill = 0.8f;                         // of the band's height
    const float ResumeBottom = 0.025f, ResumeTop = 0.11f;

    static readonly Color BandColour = new Color(0f, 0f, 0f, 0.78f);

    [MenuItem("Tools/Vizlab NEW/Pause Band/Apply", false, 70)]
    static void Apply()
    {
        FixImport(TitlePath);
        Sprite title = AssetDatabase.LoadAssetAtPath<Sprite>(TitlePath);
        Sprite resume = AssetDatabase.LoadAssetAtPath<Sprite>(ResumePath);
        if (title == null || resume == null)
        {
            Debug.LogError("[PauseBandSetup_NEW] Missing art: expected sprites at " + TitlePath +
                           " and " + ResumePath + ".");
            return;
        }

        Undo.SetCurrentGroupName("Apply pause band");
        int group = Undo.GetCurrentGroup();

        TutorialPauseCard_NEW card = FindInOpenScene<TutorialPauseCard_NEW>();
        bool journey = FindInOpenScene<TutorialPause_NEW>() == null;

        if (card == null)
        {
            Canvas canvas = JourneyCanvas();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("Pause band",
                    "No pause card and no journey HUD canvas in the open scene. Open Vizlab_tut " +
                    "or Vizlab_Journey.", "OK");
                return;
            }

            var go = new GameObject("PauseCard", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Create pause card");
            go.layer = canvas.gameObject.layer;
            go.transform.SetParent(canvas.transform, false);
            Stretch((RectTransform)go.transform, Vector2.zero, Vector2.one);

            CanvasGroup g = Undo.AddComponent<CanvasGroup>(go);
            g.alpha = 0f;
            g.blocksRaycasts = false;
            g.interactable = false;

            card = Undo.AddComponent<TutorialPauseCard_NEW>(go);
        }

        RectTransform root = (RectTransform)card.transform;

        // The old full-screen dim, if any: kept, switched off.
        Image dim = card.GetComponent<Image>();
        if (dim != null && dim.enabled)
        {
            Undo.RecordObject(dim, "Pause band: dim off");
            dim.enabled = false;
        }

        Image band = FindOrCreate(root, "Band");
        Undo.RecordObject(band, "Pause band");
        band.sprite = null;
        band.color = BandColour;
        band.raycastTarget = false;
        Stretch(band.rectTransform, new Vector2(0f, BandBottom), new Vector2(1f, BandTop));
        band.transform.SetSiblingIndex(0);

        float inset = (BandTop - BandBottom) * (1f - TitleFill) * 0.5f;
        Image frame = Art(root, "FrameArt", title, BandBottom + inset, BandTop - inset);
        frame.transform.SetSiblingIndex(1);

        Image resumeArt = Art(root, "ResumeArt", resume, ResumeBottom, ResumeTop);
        resumeArt.transform.SetAsLastSibling();

        root.SetAsLastSibling();

        var so = new SerializedObject(card);
        so.FindProperty("group").objectReferenceValue = card.GetComponent<CanvasGroup>();
        so.FindProperty("frameImage").objectReferenceValue = frame;
        so.FindProperty("resumeImage").objectReferenceValue = resumeArt;
        if (so.FindProperty("pausedLabel").objectReferenceValue == null)
            so.FindProperty("pausedLabel").objectReferenceValue = FindText(root, "Title");
        if (so.FindProperty("resumeLabel").objectReferenceValue == null)
            so.FindProperty("resumeLabel").objectReferenceValue = FindText(root, "Resume");
        so.ApplyModifiedProperties();

        if (journey) SetUpJourneyPause(card.gameObject);

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(card.gameObject.scene);
        Selection.activeGameObject = card.gameObject;

        Debug.Log("[PauseBandSetup_NEW] Pause band applied to " + card.name + " in " +
                  card.gameObject.scene.name + ". Save the scene to keep it.", card);
    }

    /// <summary>The canvas the journey HUD is on: the tracker's, or failing that the hint's.</summary>
    static Canvas JourneyCanvas()
    {
        Component anchor = FindInOpenScene<JourneyTrackerHUD_NEW>();
        if (anchor == null) anchor = FindInOpenScene<JourneyHint_NEW>();
        if (anchor == null) return null;

        Canvas c = anchor.GetComponentInParent<Canvas>();
        return c != null ? c.rootCanvas : null;
    }

    static void SetUpJourneyPause(GameObject fallbackHost)
    {
        JourneyPause_NEW pause = FindInOpenScene<JourneyPause_NEW>();
        if (pause == null)
        {
            // Where VizlabControlsFix_NEW puts it, so the two tools agree.
            InputDiagnostics_NEW diagnostics = FindInOpenScene<InputDiagnostics_NEW>();
            GameObject host = diagnostics != null ? diagnostics.gameObject : fallbackHost;
            pause = Undo.AddComponent<JourneyPause_NEW>(host);
            Debug.Log("[PauseBandSetup_NEW] Added JourneyPause_NEW to '" + host.name + "'.", host);
        }

        var so = new SerializedObject(pause);
        so.FindProperty("showHintWhilePaused").boolValue = false;
        so.ApplyModifiedProperties();
    }

    /// <summary>A drawn piece centred across, its height a band of the canvas, its width from the sprite.</summary>
    static Image Art(RectTransform parent, string name, Sprite sprite, float bottom, float top)
    {
        Image img = FindOrCreate(parent, name);
        Undo.RecordObject(img, "Pause art");
        img.sprite = sprite;
        img.color = Color.white;
        img.preserveAspect = true;
        img.raycastTarget = false;

        Stretch(img.rectTransform, new Vector2(0.5f, bottom), new Vector2(0.5f, top));

        AspectRatioFitter fit = img.GetComponent<AspectRatioFitter>();
        if (fit == null) fit = Undo.AddComponent<AspectRatioFitter>(img.gameObject);
        Undo.RecordObject(fit, "Pause art aspect");
        fit.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
        fit.aspectRatio = sprite.rect.width / sprite.rect.height;
        return img;
    }

    static void FixImport(string path)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) return;
        if (ti.textureType == TextureImporterType.Sprite && ti.mipmapEnabled &&
            ti.textureCompression == TextureImporterCompression.Uncompressed) return;

        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = true;
        ti.filterMode = FilterMode.Trilinear;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.SaveAndReimport();
    }

    static T FindInOpenScene<T>() where T : Component
    {
        foreach (T c in Resources.FindObjectsOfTypeAll<T>())
            if (c.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(c))
                return c;
        return null;
    }

    static Image FindOrCreate(RectTransform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            Image found = existing.GetComponent<Image>();
            if (found != null) return found;
            return Undo.AddComponent<Image>(existing.gameObject);
        }

        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return go.GetComponent<Image>();
    }

    static TMP_Text FindText(RectTransform parent, string name)
    {
        Transform t = parent.Find(name);
        return t != null ? t.GetComponent<TMP_Text>() : null;
    }

    static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        Undo.RecordObject(rect, "Pause band layout");
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }
}

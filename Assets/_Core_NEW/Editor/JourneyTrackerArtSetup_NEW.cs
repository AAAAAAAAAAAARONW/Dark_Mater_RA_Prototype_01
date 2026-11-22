using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Turns the journey HUD into the horizontal art tracker along the bottom of the screen:
/// JourneyTracker_BG_V2.png (the TRAVEL TRACKER panel, seven ticks) behind it, JourneyTrackerHUD_NEW driving three-state node
/// images, the gradient line and JourneyTracker_Arrow.png, and the remaining distance
/// in the green tab.
///
/// Works on the JourneyHUD of the open scene (the object with UniverseJourneyHUD or
/// JourneyTrackerHUD_NEW on it, AllCanvas/JourneyCanvas/JourneyHUD in Gameplay_Scene_Ana).
/// It reuses that HUD's Background, TrackContainer, FillBar and DistanceText, hides
/// PhaseNameText (the yellow node names the current phase), deletes TrackBackground (the
/// background has the line drawn on it) and replaces UniverseJourneyHUD with
/// JourneyTrackerHUD_NEW, carrying over its tracker, line colours and auto-hide settings.
///
/// Through Undo, so the scene changes are one Ctrl+Z. Safe to run again. The two import
/// fixes it makes (the arrow imported as a sprite, the background allowed past 2048 px)
/// are asset changes and are not undone with it.
/// </summary>
public static class JourneyTrackerArtSetup_NEW
{
    const string ArtFolder = "Assets/_Core_NEW/UI/";
    const string BackgroundPath = ArtFolder + "JourneyTracker_BG_V2.png";
    const string ArrowPath = ArtFolder + "JourneyTracker_Arrow.png";

    /// <summary>
    /// The art name of each station, one per tick and one per UniverseJourneyTracker phase,
    /// Quasar first, so each node lights as the journey's own transitions cross into it.
    /// Cosmic Web twice.
    /// </summary>
    static readonly string[] StationArt =
    {
        "Quasar", "CosmicWeb", "Galaxy", "CosmicWeb", "MilkyWay", "SolarSystem", "Earth"
    };

    /// <summary>The tracker phase the first station stands for (Quasar).</summary>
    const int FirstPhase = 0;

    // Measured on JourneyTracker_BG_V2.png (2000 x 208): seven ticks centred at x 123.5
    // and every 293.33 after, the line 5 px thick centred at y 97 from the top.
    const float FirstTickX = 123.5f / 2000f;
    const float TickSpacing = 1760f / 6f / 2000f;
    const float LineY = 97f / 208f;
    const float LineThickness = 5f / 208f;

    /// <summary>
    /// The background width the node and arrow art was drawn against (the first
    /// background, 5605 px). The new one is exported smaller, so its own pixel width would
    /// blow the nodes up 2.8 times.
    /// </summary>
    const float NodeArtReferenceWidth = 5605f;

    /// <summary>The tracker's width on the 1920 x 1080 reference canvas.</summary>
    const float HudWidth = 1600f;

    /// <summary>Gap between the tracker and the bottom of the screen.</summary>
    const float BottomMargin = 12f;

    // The green tab at the bottom right of JourneyTracker_BG_V2.png (2000 x 208), inside
    // its slant: x 1615 to 1960, y 168 to 204 from the top.
    static readonly Vector2 TabMin = new Vector2(1615f / 2000f, 1f - 204f / 208f);
    static readonly Vector2 TabMax = new Vector2(1960f / 2000f, 1f - 168f / 208f);

    static readonly Color TabTextColour = new Color(0.06f, 0.06f, 0.06f, 1f);

    [MenuItem("Tools/Journey NEW/Journey Tracker/Apply Art", false, 61)]
    static void Apply()
    {
        RectTransform hud = FindHud(out UniverseJourneyHUD oldHud, out JourneyTrackerHUD_NEW newHud);
        if (hud == null)
        {
            EditorUtility.DisplayDialog("Journey tracker art",
                "No journey HUD in the open scene: nothing has UniverseJourneyHUD or " +
                "JourneyTrackerHUD_NEW on it. Open Gameplay_Scene_Ana.", "OK");
            return;
        }

        FixImports();

        Sprite background = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
        Sprite arrow = AssetDatabase.LoadAssetAtPath<Sprite>(ArrowPath);
        if (background == null || arrow == null)
        {
            Debug.LogError("[JourneyTrackerArtSetup_NEW] Missing art: expected sprites at " +
                           BackgroundPath + " and " + ArrowPath + ".");
            return;
        }

        var stations = new JourneyTrackerHUD_NEW.StationArt[StationArt.Length];
        for (int i = 0; i < StationArt.Length; i++)
        {
            stations[i] = new JourneyTrackerHUD_NEW.StationArt
            {
                ahead = LoadStation(StationArt[i], "Ahead"),
                current = LoadStation(StationArt[i], "Current"),
                passed = LoadStation(StationArt[i], "Passed"),
            };
            if (stations[i].ahead == null || stations[i].current == null || stations[i].passed == null)
                return;
        }

        Undo.SetCurrentGroupName("Apply journey tracker art");
        int group = Undo.GetCurrentGroup();

        // The panel: the background's proportions, centred along the bottom.
        Undo.RecordObject(hud, "Journey tracker layout");
        hud.anchorMin = hud.anchorMax = new Vector2(0.5f, 0f);
        hud.pivot = new Vector2(0.5f, 0f);
        hud.sizeDelta = new Vector2(HudWidth, HudWidth * background.rect.height / background.rect.width);
        hud.anchoredPosition = new Vector2(0f, BottomMargin);

        Image bg = FindOrCreate<Image>(hud, "Background");
        Undo.RecordObject(bg, "Journey tracker background");
        bg.sprite = background;
        bg.type = Image.Type.Simple;
        bg.preserveAspect = false;
        bg.color = Color.white;
        bg.raycastTarget = false;
        Stretch(bg.rectTransform, Vector2.zero, Vector2.one);
        bg.transform.SetSiblingIndex(0);

        RectTransform content = FindOrCreateRect(hud, "HUDContent");
        Stretch(content, Vector2.zero, Vector2.one);

        RectTransform track = FindOrCreateRect(content, "TrackContainer");
        Stretch(track, Vector2.zero, Vector2.one);

        Transform oldTrackBg = track.Find("TrackBackground");
        if (oldTrackBg != null) Undo.DestroyObjectImmediate(oldTrackBg.gameObject);

        RawImage fill = FindOrCreate<RawImage>(track, "FillBar");
        Undo.RecordObject(fill, "Journey tracker line");
        fill.color = Color.white;
        fill.raycastTarget = false;
        fill.transform.SetSiblingIndex(0);

        // Above the line, below the nodes the HUD adds at runtime, so it slides under them.
        Image arrowImage = FindOrCreate<Image>(track, "Arrow");
        Undo.RecordObject(arrowImage, "Journey tracker arrow");
        arrowImage.sprite = arrow;
        arrowImage.preserveAspect = true;
        arrowImage.color = Color.white;
        arrowImage.raycastTarget = false;
        arrowImage.transform.SetSiblingIndex(1);

        TMP_Text distance = FindText(content, "DistanceText");
        if (distance != null)
        {
            Undo.RecordObject(distance, "Journey tracker distance");
            Stretch(distance.rectTransform, TabMin, TabMax);
            distance.color = TabTextColour;
            distance.fontStyle = FontStyles.Bold;
            distance.alignment = TextAlignmentOptions.Center;
            distance.enableWordWrapping = false;
            distance.enableAutoSizing = true;
            distance.fontSizeMin = 8f;
            distance.fontSizeMax = 22f;
            distance.raycastTarget = false;
        }

        TMP_Text phaseName = FindText(content, "PhaseNameText");
        if (phaseName != null && phaseName.gameObject.activeSelf)
        {
            Undo.RecordObject(phaseName.gameObject, "Hide phase name");
            phaseName.gameObject.SetActive(false);
        }

        // Swap the HUD script, keeping what the old one was set to.
        if (newHud == null) newHud = Undo.AddComponent<JourneyTrackerHUD_NEW>(hud.gameObject);

        var so = new SerializedObject(newHud);
        if (oldHud != null)
        {
            var old = new SerializedObject(oldHud);
            Copy(old, so, "tracker");
            Copy(old, so, "gradientColors");
            Copy(old, so, "autoHide");
            Copy(old, so, "hudVisibleDuration");
            Copy(old, so, "hudSlideDuration");
            Copy(old, so, "hudShowButton");
        }
        if (so.FindProperty("tracker").objectReferenceValue == null)
            so.FindProperty("tracker").objectReferenceValue = FindInOpenScene<UniverseJourneyTracker>();

        so.FindProperty("trackArea").objectReferenceValue = track;
        so.FindProperty("fillImage").objectReferenceValue = fill;
        so.FindProperty("arrowImage").objectReferenceValue = arrowImage;
        so.FindProperty("distanceText").objectReferenceValue = distance;
        so.FindProperty("hudPanel").objectReferenceValue = hud;
        so.FindProperty("backgroundPixelWidth").floatValue = NodeArtReferenceWidth;
        so.FindProperty("firstPhase").intValue = FirstPhase;
        so.FindProperty("firstTickX").floatValue = FirstTickX;
        so.FindProperty("tickSpacing").floatValue = TickSpacing;
        so.FindProperty("lineY").floatValue = LineY;
        so.FindProperty("lineThickness").floatValue = LineThickness;

        SerializedProperty list = so.FindProperty("stations");
        list.arraySize = stations.Length;
        for (int i = 0; i < stations.Length; i++)
        {
            SerializedProperty e = list.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("ahead").objectReferenceValue = stations[i].ahead;
            e.FindPropertyRelative("current").objectReferenceValue = stations[i].current;
            e.FindPropertyRelative("passed").objectReferenceValue = stations[i].passed;
        }
        so.ApplyModifiedProperties();

        if (oldHud != null) Undo.DestroyObjectImmediate(oldHud);

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
        Selection.activeGameObject = hud.gameObject;

        Debug.Log("[JourneyTrackerArtSetup_NEW] Journey tracker art applied to " + hud.name +
                  ". Save the scene to keep it; Ctrl+Z undoes the scene changes.", hud);
    }

    static RectTransform FindHud(out UniverseJourneyHUD oldHud, out JourneyTrackerHUD_NEW newHud)
    {
        newHud = FindInOpenScene<JourneyTrackerHUD_NEW>();
        oldHud = null;
        if (newHud != null)
        {
            oldHud = newHud.GetComponent<UniverseJourneyHUD>();
            return newHud.transform as RectTransform;
        }

        oldHud = FindInOpenScene<UniverseJourneyHUD>();
        return oldHud != null ? oldHud.transform as RectTransform : null;
    }

    /// <summary>FindObjectOfType, inactive objects included (2019.4 has no flag for it).</summary>
    static T FindInOpenScene<T>() where T : Component
    {
        foreach (T c in Resources.FindObjectsOfTypeAll<T>())
            if (c.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(c))
                return c;
        return null;
    }

    static Sprite LoadStation(string station, string state)
    {
        string path = ArtFolder + "JourneyTracker_" + station + "_" + state + ".png";
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null)
            Debug.LogError("[JourneyTrackerArtSetup_NEW] Missing art: expected a sprite at " + path + ".");
        return s;
    }

    /// <summary>
    /// The arrow came in as a plain texture, which an Image cannot show, and the background
    /// is 5605 px wide but was capped at 2048, which blurs its thin lines.
    ///
    /// All of the tracker art is drawn about 3.5x smaller than its pixels. Without mipmaps
    /// bilinear filtering samples only every few pixels at that size, so thin strokes break
    /// up and edges go jagged; mipmaps with trilinear filtering average them properly, and
    /// a small negative bias keeps the text from going soft.
    /// </summary>
    static void FixImports()
    {
        foreach (string guid in AssetDatabase.FindAssets("JourneyTracker_ t:Texture2D", new[] { ArtFolder.TrimEnd('/') }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;

            bool dirty = false;
            if (ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                dirty = true;
            }
            if (!ti.mipmapEnabled || ti.filterMode != FilterMode.Trilinear || ti.mipMapBias > -0.5f)
            {
                ti.mipmapEnabled = true;
                ti.filterMode = FilterMode.Trilinear;
                ti.mipMapBias = -0.5f;
                dirty = true;
            }
            if (path == BackgroundPath && ti.maxTextureSize < 8192)
            {
                ti.maxTextureSize = 8192;
                dirty = true;
            }
            if (dirty) ti.SaveAndReimport();
        }
    }

    static T FindOrCreate<T>(RectTransform parent, string name) where T : Graphic
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            T found = existing.GetComponent<T>();
            if (found != null) return found;
            if (existing.GetComponent<Graphic>() == null) return Undo.AddComponent<T>(existing.gameObject);
            Undo.DestroyObjectImmediate(existing.gameObject);   // some other graphic: start over
        }

        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return go.GetComponent<T>();
    }

    static RectTransform FindOrCreateRect(RectTransform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing is RectTransform rt) return rt;

        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static TMP_Text FindText(RectTransform parent, string name)
    {
        Transform t = parent.Find(name);
        return t != null ? t.GetComponent<TMP_Text>() : null;
    }

    static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        Undo.RecordObject(rect, "Journey tracker layout");
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    static void Copy(SerializedObject from, SerializedObject to, string field)
    {
        SerializedProperty src = from.FindProperty(field);
        if (src != null) to.CopyFromSerializedProperty(src);
    }
}

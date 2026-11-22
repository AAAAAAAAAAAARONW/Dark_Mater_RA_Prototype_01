using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gives the journey's hint line (JourneyHint_NEW, DarkMatterHint in the journey scenes) the
/// drawn PRESS Y - TOGGLE DARK MATTER prompt: an "Art" child filling the hint's rect, the
/// sprite fitted inside it, off until the dark matter moment asks for it. Wires the hint's
/// art and plate so the plate and words step aside while the art is up.
///
/// Through Undo, so the scene changes are one Ctrl+Z. Safe to run again.
/// </summary>
public static class DarkMatterHintArtSetup_NEW
{
    const string ArtPath = "Assets/_Core_NEW/UI/Hint_ToggleDarkMatter.png";

    [MenuItem("Tools/Journey NEW/Dark Matter Hint Art", false, 62)]
    static void Apply()
    {
        JourneyHint_NEW hint = null;
        foreach (JourneyHint_NEW h in Resources.FindObjectsOfTypeAll<JourneyHint_NEW>())
            if (h.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(h)) { hint = h; break; }

        if (hint == null)
        {
            EditorUtility.DisplayDialog("Dark matter hint art",
                "No JourneyHint_NEW in the open scene. Open PC_Journey or Vizlab_Journey.", "OK");
            return;
        }

        FixImport();
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath);
        if (sprite == null)
        {
            Debug.LogError("[DarkMatterHintArtSetup_NEW] Missing art: expected a sprite at " + ArtPath + ".");
            return;
        }

        Undo.SetCurrentGroupName("Apply dark matter hint art");
        int group = Undo.GetCurrentGroup();

        var root = (RectTransform)hint.transform;
        Transform existing = root.Find("Art");
        Image art = existing != null ? existing.GetComponent<Image>() : null;
        if (art == null)
        {
            if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);
            var go = new GameObject("Art", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "Create hint art");
            go.layer = root.gameObject.layer;
            go.transform.SetParent(root, false);
            art = go.GetComponent<Image>();
        }

        Undo.RecordObject(art, "Hint art");
        art.sprite = sprite;
        art.color = Color.white;
        art.preserveAspect = true;
        art.raycastTarget = false;
        art.enabled = false;

        RectTransform rt = art.rectTransform;
        Undo.RecordObject(rt, "Hint art layout");
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.SetAsLastSibling();

        var so = new SerializedObject(hint);
        so.FindProperty("art").objectReferenceValue = art;
        Transform plate = root.Find("Plate");
        if (plate != null) so.FindProperty("plate").objectReferenceValue = plate.GetComponent<Graphic>();
        so.ApplyModifiedProperties();

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(hint.gameObject.scene);
        Selection.activeGameObject = art.gameObject;

        Debug.Log("[DarkMatterHintArtSetup_NEW] Dark matter hint art applied in " +
                  hint.gameObject.scene.name + ". Save the scene to keep it.", hint);
    }

    static void FixImport()
    {
        if (!(AssetImporter.GetAtPath(ArtPath) is TextureImporter ti)) return;
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
}

using System.IO;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Bakes every Custom/PurpleCloudVolume renderer in the scene to a 2D RGBA
/// sprite texture, then replaces it with a Quad using BakedCloudBillboard.shader.
///
/// Each volume is rendered in isolation (layer 30) from the current Scene View
/// camera direction, so the baked image matches the perspective you're working in.
///
/// Trade-off: the baked Quad looks correct from roughly the same viewing angle
/// used during baking.  For a mostly fixed or slowly-panning camera (typical in
/// dark-matter visualisations) this gives effectively zero runtime cost.
///
/// Usage: Tools > Laxi > Bake Cloud Volumes to Sprites
/// Output: Assets/BakedCloudSprites/<ObjectName>.png  +  matching Quad prefab
/// </summary>
public static class BakeCloudVolumesToSprites
{
    const string VOLUME_SHADER    = "Custom/PurpleCloudVolume";
    const string BILLBOARD_SHADER = "Custom/BakedCloudBillboard";
    const string OUT_FOLDER       = "Assets/BakedCloudSprites";
    const int    BAKE_LAYER       = 30;   // assumed unused; change if your project uses it
    const int    TEX_SIZE         = 512;  // render resolution per volume

    // -------------------------------------------------------------------------
    [MenuItem("Tools/Laxi/Bake Cloud Volumes to Sprites")]
    static void BakeAll()
    {
        // Ensure output folder exists.
        if (!AssetDatabase.IsValidFolder(OUT_FOLDER))
            AssetDatabase.CreateFolder("Assets", "BakedCloudSprites");

        // Collect all PurpleCloudVolume renderers in the active scene.
        var allRenderers = Object.FindObjectsOfType<MeshRenderer>();
        int baked = 0;

        foreach (var mr in allRenderers)
        {
            if (mr.sharedMaterial == null) continue;
            if (mr.sharedMaterial.shader == null) continue;
            if (mr.sharedMaterial.shader.name != VOLUME_SHADER) continue;

            BakeRenderer(mr);
            baked++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (baked == 0)
            Debug.LogWarning("[BakeCloudVolumesToSprites] No Custom/PurpleCloudVolume renderers found in the active scene.");
        else
            Debug.Log($"[BakeCloudVolumesToSprites] Baked {baked} volume(s) → {OUT_FOLDER}");
    }

    [MenuItem("Tools/Laxi/Bake Cloud Volumes to Sprites", validate = true)]
    static bool ValidateBakeAll() => Application.isPlaying == false;

    // -------------------------------------------------------------------------
    static void BakeRenderer(MeshRenderer mr)
    {
        string safeName = mr.gameObject.name.Replace(" ", "_").Replace("/", "_");
        string pngPath  = $"{OUT_FOLDER}/{safeName}.png";
        string matPath  = $"{OUT_FOLDER}/{safeName}_mat.mat";

        // ------------------------------------------------------------------
        // 1. Create bake camera
        // ------------------------------------------------------------------
        var camGO = new GameObject("__BakeCam__");
        var cam   = camGO.AddComponent<Camera>();

        Bounds b       = mr.bounds;
        float  radius  = b.extents.magnitude;

        // Use Scene View camera direction so the bake matches your working view.
        Camera sceneViewCam = SceneView.lastActiveSceneView?.camera;
        Vector3 viewDir = sceneViewCam != null
            ? sceneViewCam.transform.forward
            : Vector3.forward;

        cam.orthographic     = true;
        cam.orthographicSize = Mathf.Max(b.extents.x, b.extents.y, b.extents.z) * 1.05f;
        cam.transform.position  = b.center - viewDir * (radius + 10f);
        cam.transform.LookAt(b.center);
        cam.nearClipPlane    = 0.1f;
        cam.farClipPlane     = radius * 2f + 20f;
        cam.backgroundColor  = new Color(0f, 0f, 0f, 0f);
        cam.clearFlags       = CameraClearFlags.SolidColor;
        cam.cullingMask      = 1 << BAKE_LAYER;  // only render the isolated object

        // ------------------------------------------------------------------
        // 2. Move the target to the bake layer temporarily
        // ------------------------------------------------------------------
        int originalLayer = mr.gameObject.layer;
        SetLayerRecursive(mr.gameObject, BAKE_LAYER);

        // ------------------------------------------------------------------
        // 3. Render to RenderTexture → Texture2D → PNG
        // ------------------------------------------------------------------
        var rt = new RenderTexture(TEX_SIZE, TEX_SIZE, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 2;
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        var tex2D = new Texture2D(TEX_SIZE, TEX_SIZE, TextureFormat.RGBA32, false);
        tex2D.ReadPixels(new Rect(0, 0, TEX_SIZE, TEX_SIZE), 0, 0);
        tex2D.Apply();
        RenderTexture.active = null;

        byte[] png = tex2D.EncodeToPNG();
        string absPath = Path.Combine(Application.dataPath, "..", pngPath);
        File.WriteAllBytes(Path.GetFullPath(absPath), png);

        // ------------------------------------------------------------------
        // 4. Restore original layer + clean up camera
        // ------------------------------------------------------------------
        SetLayerRecursive(mr.gameObject, originalLayer);
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex2D);
        Object.DestroyImmediate(camGO);

        // ------------------------------------------------------------------
        // 5. Import the PNG as a transparent sprite texture
        // ------------------------------------------------------------------
        AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(pngPath);
        if (importer != null)
        {
            importer.textureType        = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled      = false;
            importer.wrapMode           = TextureWrapMode.Clamp;
            importer.filterMode         = FilterMode.Bilinear;
            importer.maxTextureSize     = TEX_SIZE;
            importer.SaveAndReimport();
        }

        // ------------------------------------------------------------------
        // 6. Create billboard material using the baked texture
        // ------------------------------------------------------------------
        var billboardShader = Shader.Find(BILLBOARD_SHADER);
        if (billboardShader == null)
        {
            Debug.LogError("[BakeCloudVolumesToSprites] BakedCloudBillboard shader not found.");
            return;
        }

        var bakedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
        var mat      = new Material(billboardShader) { name = safeName + "_mat" };
        mat.SetTexture("_MainTex", bakedTex);
        AssetDatabase.DeleteAsset(matPath);
        AssetDatabase.CreateAsset(mat, matPath);

        // ------------------------------------------------------------------
        // 7. Create Quad at the same position / size as the original volume
        // ------------------------------------------------------------------
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(quad.GetComponent<MeshCollider>());
        quad.name = safeName + "_Baked";
        quad.transform.SetParent(mr.transform.parent);

        // Orient the quad to face the bake camera direction (and keep it there).
        quad.transform.position = b.center;
        quad.transform.rotation = Quaternion.LookRotation(-viewDir);

        // Scale to cover the bounding box footprint as seen from the bake angle.
        float w = Vector3.ProjectOnPlane(b.size, viewDir).magnitude * 1.05f;
        float h = cam.orthographicSize * 2f;
        quad.transform.localScale = new Vector3(Mathf.Max(w, h), Mathf.Max(w, h), 1f);

        quad.GetComponent<MeshRenderer>().sharedMaterial = mat;

        // Register undo so the operation can be reversed.
        Undo.RegisterCreatedObjectUndo(quad, "Bake Cloud Volume");

        // Disable the original volumetric renderer (keep the GameObject for reference).
        Undo.RecordObject(mr, "Disable original volume");
        mr.enabled = false;

        Debug.Log($"[BakeCloudVolumesToSprites] '{mr.gameObject.name}' → {pngPath}");
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
}

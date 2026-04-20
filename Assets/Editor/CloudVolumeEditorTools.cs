using System.Linq;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor helpers for the PurpleCloudVolume family of shaders.
/// All menu items are under Tools > Laxi.
/// </summary>
public static class CloudVolumeEditorTools
{
    const string MULTI_SHADER   = "Custom/PurpleCloudVolumeMulti";
    const string SINGLE_SHADER  = "Custom/PurpleCloudVolume";
    const string MAT_OUT_PATH   = "Assets/Shaders/Multi_PurpleCloudVolume.mat";

    // -------------------------------------------------------------------------
    // Create Multi-Volume from Selection
    //
    // Select 2–4 GameObjects that each have a Custom/PurpleCloudVolume material.
    // The tool will:
    //   1. Copy shader parameters from the first selected material.
    //   2. Create a new Multi_PurpleCloudVolume.mat asset.
    //   3. Spawn a cube GameObject sized to cover all selected volumes.
    //   4. Attach PurpleCloudMultiVolume, configure sub-volume references.
    //   5. Disable the original renderers (they are no longer needed).
    // -------------------------------------------------------------------------
    [MenuItem("Tools/Laxi/Create Multi-Volume from Selection")]
    static void CreateMultiVolume()
    {
        // Collect valid sub-volume candidates from the current selection.
        var candidates = Selection.gameObjects
            .Select(sel => new
            {
                obj      = sel,
                renderer = sel.GetComponent<Renderer>()
            })
            .Where(x => x.renderer != null &&
                        x.renderer.sharedMaterial != null &&
                        x.renderer.sharedMaterial.shader != null &&
                        x.renderer.sharedMaterial.shader.name == SINGLE_SHADER)
            .Take(4)
            .ToArray();

        if (candidates.Length < 2)
        {
            EditorUtility.DisplayDialog(
                "Create Multi-Volume",
                "Select 2–4 GameObjects that each have a \"Custom/PurpleCloudVolume\" material, then run this tool.",
                "OK");
            return;
        }

        if (candidates.Length > 4)
        {
            EditorUtility.DisplayDialog(
                "Create Multi-Volume",
                "The shader supports a maximum of 4 sub-volumes.  " +
                "Only the first 4 selected objects will be used.",
                "OK");
        }

        // Build multi-volume material from the first sub-volume's parameters.
        const string SHADER_PATH = "Assets/Shaders/PurpleCloudVolumeMulti.shader";
        var multiShader = AssetDatabase.LoadAssetAtPath<Shader>(SHADER_PATH);
        if (multiShader == null) multiShader = Shader.Find(MULTI_SHADER); // fallback
        if (multiShader == null)
        {
            Debug.LogError($"[CloudVolumeEditorTools] Could not load shader at \"{SHADER_PATH}\". " +
                           "Make sure PurpleCloudVolumeMulti.shader is in Assets/Shaders/.");
            return;
        }

        var srcMat   = candidates[0].renderer.sharedMaterial;
        var multiMat = new Material(multiShader);
        multiMat.CopyPropertiesFromMaterial(srcMat);
        multiMat.name = "Multi_PurpleCloudVolume";

        AssetDatabase.DeleteAsset(MAT_OUT_PATH);
        AssetDatabase.CreateAsset(multiMat, MAT_OUT_PATH);

        // Create the multi-volume cube.
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "PurpleCloudVolumeMulti";
        Object.DestroyImmediate(go.GetComponent<BoxCollider>());
        go.GetComponent<MeshRenderer>().sharedMaterial = multiMat;

        var mv = go.AddComponent<PurpleCloudMultiVolume>();
        mv.subVolumes = candidates.Select(c => c.obj.transform).ToArray();

        // Size the cube to cover all sub-volumes, then push initial matrices.
        mv.FitBounds();
        mv.PushMatrices();

        // Disable original renderers — geometry and transforms are still referenced
        // by the sub-volume matrices so the cloud shape is preserved.
        foreach (var c in candidates)
        {
            Undo.RecordObject(c.obj, "Disable sub-volume renderer");
            c.renderer.enabled = false;
        }

        Undo.RegisterCreatedObjectUndo(go, "Create Multi-Volume");
        Selection.activeGameObject = go;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[CloudVolumeEditorTools] Created \"{go.name}\" from {candidates.Length} " +
                  $"sub-volumes.  Material saved to {MAT_OUT_PATH}\n" +
                  $"Original renderers disabled (re-enable to revert).");
    }

    [MenuItem("Tools/Laxi/Create Multi-Volume from Selection", validate = true)]
    static bool ValidateCreateMultiVolume()
    {
        return Selection.gameObjects.Length >= 2;
    }

    // -------------------------------------------------------------------------
    // Fit Multi-Volume Bounds (convenience re-fit if sub-volumes were moved)
    // -------------------------------------------------------------------------
    [MenuItem("Tools/Laxi/Fit Multi-Volume Bounds")]
    static void FitMultiVolumeBounds()
    {
        var mv = Selection.activeGameObject?.GetComponent<PurpleCloudMultiVolume>();
        if (mv == null)
        {
            EditorUtility.DisplayDialog(
                "Fit Multi-Volume Bounds",
                "Select a GameObject with a PurpleCloudMultiVolume component.",
                "OK");
            return;
        }
        Undo.RecordObject(mv.transform, "Fit Multi-Volume Bounds");
        mv.FitBounds();
        mv.PushMatrices();
        Debug.Log($"[CloudVolumeEditorTools] Bounds refitted for \"{mv.gameObject.name}\".");
    }

    [MenuItem("Tools/Laxi/Fit Multi-Volume Bounds", validate = true)]
    static bool ValidateFitMultiVolumeBounds()
    {
        return Selection.activeGameObject?.GetComponent<PurpleCloudMultiVolume>() != null;
    }
}

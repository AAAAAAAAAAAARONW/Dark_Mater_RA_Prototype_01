using UnityEditor;
using UnityEngine;

/// <summary>
/// Turns the second cosmic web's purple threads into nebula gas, in the open journey scene:
/// Tools > Journey NEW > Nebula Gas In Second Cosmic Web, and back with Restore Second Cosmic
/// Web Gas.
///
/// WHY. The second cosmic web is where the light's spectrum fills with the Lyman-alpha forest —
/// "thousands of hydrogen clouds have cut lines into your light" (J6) — and the scientists asked
/// for hydrogen to be met as a translucent cloud (LARC, 14 October). The first try scattered
/// separate nebulae (NebulaCloudVFX_NEW) at the far end of the web; but the gas the light flies
/// through is the web's threads themselves, so that is where the nebula goes: the threads' own
/// material is replaced, and the player crosses it all the way through the web. The separate
/// clouds ray-marched every pixel they covered, which was too much for the GPU; so did the
/// threads' baked volumes.
///
/// WHAT IT DOES, in the CosmicWeb render group only (the first web, Macro, is left alone):
///   BIG       the purple threads: their material becomes CosmicWebNebula_NEW, the web's purple
///             as a soft, clumped, drifting gas with dark lanes, shaded once a pixel, no ray
///             march (Resources/CosmicWebNebula_NEW.shader).
///   BIG (1)   the same 1.7 M-vertex mesh again with another bake, drawn over it: switched off,
///             as the one nebula layer is enough. Half the purple's vertices.
///   Coreyellow  the smaller sheets and threads in the middle, round the yellow clusters (pink
///             in its bake, whatever its name): CosmicWebNebulaPink_NEW, the same gas in pink and
///             magenta, finer, drifting the other way, so the middle reads apart from the threads.
///   Nebulae   the separate clouds the first try added: deleted.
/// The yellow clusters (Core, Coreyellow2) keep their baked volumes: they are the galaxies'
/// light.
///
/// Restore puts the baked materials back and switches BIG (1) on again; the separate clouds
/// are not brought back.
///
/// It works on the scene as loaded in the editor, as one undoable step. Save the scene to keep it.
/// </summary>
static class JourneyNebulaMenu_NEW
{
    const string ApplyPath = "Tools/Journey NEW/Nebula Gas In Second Cosmic Web";
    const string RestorePath = "Tools/Journey NEW/Restore Second Cosmic Web Gas";

    const string NebulaMaterialPath = "Assets/_Core_NEW/Assets/CosmicWebNebula/CosmicWebNebula_NEW.mat";
    const string PinkMaterialPath = "Assets/_Core_NEW/Assets/CosmicWebNebula/CosmicWebNebulaPink_NEW.mat";
    const string BakedMaterialPath = "Assets/BakedVolumeTex/BIG_mat.mat";
    const string BakedMiddlePath = "Assets/BakedVolumeTex/Coreyellow_mat.mat";

    /// <summary>The purple threads, the same mesh again with another bake, and the pink middle.</summary>
    const string Gas = "BIG";
    const string GasAgain = "BIG (1)";
    const string Middle = "Coreyellow";

    /// <summary>The separate clouds the first try put in the web.</summary>
    const string OldClouds = "Nebulae";

    [MenuItem(ApplyPath)]
    static void Apply()
    {
        Transform group = SecondWeb();
        Material nebula = AssetDatabase.LoadAssetAtPath<Material>(NebulaMaterialPath);
        Material pink = AssetDatabase.LoadAssetAtPath<Material>(PinkMaterialPath);
        if (group == null) return;
        if (nebula == null || pink == null)
        {
            Debug.LogWarning("Journey NEW: " + NebulaMaterialPath + " or " + PinkMaterialPath + " is missing, so the web was not changed.");
            return;
        }

        Undo.SetCurrentGroupName("Nebula gas in second cosmic web");
        int threads = SetGas(group, nebula, pink, false);

        int removed = 0;
        foreach (Transform t in group.GetComponentsInChildren<Transform>(true))
        {
            if (t == null || t.name != OldClouds) continue;
            Undo.DestroyObjectImmediate(t.gameObject);
            removed++;
        }

        Debug.Log(string.Format("Journey NEW: the second cosmic web's purple threads and pink middle are nebula gas now ({0} meshes), the " +
                                "second layer of threads switched off{1}. Save the scene to keep it.",
                                threads, removed > 0 ? ", the separate nebulae deleted" : ""), group);
    }

    [MenuItem(RestorePath)]
    static void Restore()
    {
        Transform group = SecondWeb();
        Material baked = AssetDatabase.LoadAssetAtPath<Material>(BakedMaterialPath);
        Material bakedMiddle = AssetDatabase.LoadAssetAtPath<Material>(BakedMiddlePath);
        if (group == null) return;
        if (baked == null || bakedMiddle == null)
        {
            Debug.LogWarning("Journey NEW: " + BakedMaterialPath + " or " + BakedMiddlePath + " is missing, so the web was not changed.");
            return;
        }

        Undo.SetCurrentGroupName("Restore second cosmic web gas");
        int threads = SetGas(group, baked, bakedMiddle, true);
        Debug.Log(string.Format("Journey NEW: the second cosmic web's purple threads and pink middle have their baked volumes back ({0} meshes). " +
                                "Save the scene to keep it.", threads), group);
    }

    [MenuItem(ApplyPath, true)]
    static bool ApplyValidate() => !Application.isPlaying;

    [MenuItem(RestorePath, true)]
    static bool RestoreValidate() => !Application.isPlaying;

    static Transform SecondWeb()
    {
        WorldSwitcher_NEW worlds = Object.FindObjectOfType<WorldSwitcher_NEW>();
        Transform group = worlds != null ? worlds.GroupRoot("CosmicWeb") : null;
        if (group == null)
            Debug.LogWarning("Journey NEW: no WorldSwitcher_NEW with a 'CosmicWeb' group in the open scene, so the web was not changed.");
        return group;
    }

    /// <summary>The purple threads' and the middle's materials, and the second layer of threads on or off. How many meshes were set.</summary>
    static int SetGas(Transform group, Material threads, Material middle, bool secondLayer)
    {
        int count = 0;
        foreach (Transform t in group.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == Gas || t.name == Middle)
            {
                Renderer r = t.GetComponent<Renderer>();
                if (r == null) continue;
                Undo.RecordObject(r, "Set cosmic web gas");
                r.sharedMaterial = t.name == Gas ? threads : middle;
                count++;
            }
            else if (t.name == GasAgain && t.gameObject.activeSelf != secondLayer)
            {
                Undo.RecordObject(t.gameObject, "Set cosmic web gas");
                t.gameObject.SetActive(secondLayer);
            }
        }
        return count;
    }
}

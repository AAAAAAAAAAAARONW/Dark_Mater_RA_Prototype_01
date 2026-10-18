using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Journey NEW > Cosmic Web Test > ...
///
/// The cosmic web as the eye would see it (team proposal "Hydrogen &amp; Cosmic Web Visuals"):
/// galaxies are the only thing that shines; the hydrogen gas is invisible but absorbs; dark
/// matter is invisible and only pulls. Made for Test_Journey and run on the open scene. It
/// writes only new assets, under _Core_NEW/Assets/CosmicWebTest, and never edits the web's
/// own materials, so Gameplay_Scene_Ana and the other scenes are untouched.
///
/// BAKE GALAXIES. For each cosmic web render group (Macro, CosmicWeb), every
/// Custom/PreBakedCloudVolume under it is sampled into one grid (the densest wins, as in
/// DarkMatterMomentBuilder_NEW), each voxel weighted by how YELLOW its baked colour is: the
/// web's yellow threads are where its galaxies are, the purple is gas. No Houdini pass: the
/// galaxies come from the same textures the web is drawn from, so they line up with it. A web
/// with no yellow falls back to all its density.
///   nodes      3x3x3 local maxima above NodeThreshold of the peak: a cluster each, more
///              galaxies and brighter, warm, for a denser node.
///   threads    every other voxel above ThreadThreshold spawns a faint, small galaxy with a
///              chance in proportion to density squared (galaxies are a biased tracer: they
///              gather where matter is densest far more than in proportion).
/// One mesh per group, saved as an asset; one child "Galaxies" under the group root drawing it
/// (Custom/GalaxyField_NEW), so it fades and dives with the web. Re-running rebakes into the
/// same assets.
///
/// GALAXY-FIRST LOOK. Swaps, in this scene only, the purple gas volumes' materials for copies
/// with the gas turned down (the yellow threads keep their light) (faint, so the light is still seen passing through something), and the
/// dark matter scaffold's for a copy in grey-white, the colour of the reference simulation, so
/// purple means gas and grey means dark matter. REVERT puts the originals back.
/// </summary>
public static class GalaxyFieldBuilder_NEW
{
    const string Menu = "Tools/Journey NEW/Cosmic Web Test/";
    const string AssetFolder = "Assets/_Core_NEW/Assets/CosmicWebTest";
    const string MaterialPath = AssetFolder + "/GalaxyField.mat";
    const string ShaderName = "Custom/GalaxyField_NEW";
    const string VolumeShaderName = "Custom/PreBakedCloudVolume";
    const string ScaffoldShaderName = "Custom/DarkMatterScaffold_NEW";
    const string IntendedScene = "Test_Journey";
    const string ChildName = "Galaxies";

    static readonly string[] WebGroups = { "Macro", "CosmicWeb" };

    // ---- the bake ----------------------------------------------------------------------

    /// <summary>Grid cells along the web box's longest side.</summary>
    const int Resolution = 128;

    /// <summary>A node is a local maximum at least this fraction of the web's peak density.</summary>
    const float NodeThreshold = 0.5f;

    /// <summary>At most this many nodes per web, densest first.</summary>
    const int MaxNodes = 60;

    /// <summary>Galaxies in a node's cluster, from the faintest node to the densest.</summary>
    const int ClusterMin = 4, ClusterMax = 45;

    /// <summary>A cluster's spread, in grid cells, for the densest node.</summary>
    const float ClusterSpread = 1.6f;

    /// <summary>Thread galaxies: voxels below this fraction of the peak spawn none.</summary>
    const float ThreadThreshold = 0.3f;

    /// <summary>Chance a voxel at peak density spawns a thread galaxy (scaled by density²).</summary>
    const float ThreadRate = 0.35f;

    /// <summary>Hard cap, per web, on galaxies drawn.</summary>
    const int MaxGalaxies = 8000;

    /// <summary>A galaxy's radius as a fraction of the grid cell, cluster and thread.</summary>
    const float ClusterSize = 0.15f, ThreadSize = 0.08f;

    const int Seed = 1729;

    // Kept well short of white: past the bloom threshold these all read as white dots.
    static readonly Color ClusterCore = new Color(1.0f, 0.62f, 0.22f);   // old, red stars in the clusters
    static readonly Color ClusterEdge = new Color(1.0f, 0.74f, 0.32f);
    static readonly Color ThreadColour = new Color(1.0f, 0.84f, 0.48f);  // field galaxies, paler

    // ---- the look ----------------------------------------------------------------------

    /// <summary>The gas, turned down: emission and opacity scaled by these.</summary>
    const float GasEmission = 0.35f, GasOpacity = 0.45f;

    static readonly Color DarkMatterGrey = new Color(0.62f, 0.62f, 0.68f, 1f);

    // ------------------------------------------------------------------------------------

    [MenuItem(Menu + "Bake Galaxies From Web (open scene)")]
    public static void Bake()
    {
        if (!ConfirmScene("Bake galaxies")) return;

        WorldSwitcher_NEW worlds = UnityEngine.Object.FindObjectOfType<WorldSwitcher_NEW>();
        if (worlds == null)
        {
            EditorUtility.DisplayDialog("Galaxies", "No WorldSwitcher_NEW in this scene: not a journey scene.", "OK");
            return;
        }

        Material material = EnsureMaterial();
        if (material == null) return;

        Undo.SetCurrentGroupName("Bake galaxies");
        int made = 0;

        foreach (string id in WebGroups)
        {
            Transform root = GroupRoot(worlds, id);
            if (root == null) { Debug.Log("[GalaxyFieldBuilder_NEW] No render group '" + id + "'; skipped."); continue; }

            // WHERE THE YELLOW IS. The web's volumes are meshes (Core.fbx, Coreyellow2.fbx …)
            // drawn with a raymarch material: the texture gives the colour, but the volume
            // only shows on the mesh's own triangles. So the yellow sparkles on screen are the
            // yellow meshes' geometry, and the galaxies go on those meshes' vertices.
            List<string> used = new List<string>();
            List<Vector3> points = YellowPoints(root, used, out int layer);

            int nodes = 0, galaxies = 0;
            Mesh mesh = null;
            if (points.Count > 0)
                mesh = BakeFromPoints(id, points, out nodes, out galaxies);
            else
            {
                Debug.LogWarning("[GalaxyFieldBuilder_NEW] " + id + ": no yellow mesh found (or its vertices unreadable); " +
                                 "galaxies from all the web's density instead.", root);
                List<Source> sources = CollectSources(root, false);
                if (sources.Count > 0)
                {
                    mesh = BakeMesh(id, root, sources, out nodes, out galaxies);
                    layer = sources[0].renderer.gameObject.layer;
                    used.Add(sources.Count + " volume(s)");
                }
            }
            if (mesh == null) continue;

            PlaceChild(root, mesh, material, layer);
            made++;
            Debug.Log("[GalaxyFieldBuilder_NEW] " + id + ": " + galaxies + " galaxies, " + nodes + " nodes, on " +
                      string.Join(", ", used.ToArray()) + ".", root);
        }

        if (made > 0) EditorSceneManager.MarkSceneDirty(worlds.gameObject.scene);
        Debug.Log("[GalaxyFieldBuilder_NEW] Done: " + made + " web(s). Save the scene. Brightness and size on screen: " + MaterialPath);
    }

    [MenuItem(Menu + "Apply Galaxy-First Look (open scene)")]
    public static void ApplyLook()
    {
        if (!ConfirmScene("Galaxy-first look")) return;

        WorldSwitcher_NEW worlds = UnityEngine.Object.FindObjectOfType<WorldSwitcher_NEW>();
        if (worlds == null) return;

        EnsureFolder();
        int swapped = 0;

        foreach (string id in WebGroups)
        {
            Transform root = GroupRoot(worlds, id);
            if (root == null) continue;

            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                Material m = r.sharedMaterial;
                if (m == null || m.shader == null) continue;

                // Already swapped: judge from the original, so a re-run can correct an earlier one.
                Material original = OriginalOf(m);
                if (original != m)
                {
                    m = original;
                    Undo.RecordObject(r, "Galaxy-first look");
                    r.sharedMaterial = m;
                }

                Material copy = null;
                if (m.shader.name == VolumeShaderName && IsYellow(m))
                {
                    // The yellow threads are where the galaxies are: their light stays.
                    continue;
                }
                if (m.shader.name == VolumeShaderName)
                    copy = CopyOf(m, "faint", c =>
                    {
                        if (c.HasProperty("_Emission")) c.SetFloat("_Emission", m.GetFloat("_Emission") * GasEmission);
                        if (c.HasProperty("_Opacity")) c.SetFloat("_Opacity", m.GetFloat("_Opacity") * GasOpacity);
                    });
                else if (m.shader.name == ScaffoldShaderName)
                    copy = CopyOf(m, "grey", c => c.SetColor("_Color", DarkMatterGrey));

                if (copy == null) continue;
                Undo.RecordObject(r, "Galaxy-first look");
                r.sharedMaterial = copy;
                swapped++;
            }
        }

        EditorSceneManager.MarkSceneDirty(worlds.gameObject.scene);
        Debug.Log("[GalaxyFieldBuilder_NEW] Galaxy-first look on " + swapped + " renderer(s); the originals are untouched. " +
                  "Tune the copies in " + AssetFolder + ", or Revert Galaxy-First Look. Save the scene.");
    }

    [MenuItem(Menu + "Revert Galaxy-First Look (open scene)")]
    public static void RevertLook()
    {
        int reverted = 0;
        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetActiveScene();
        // Inactive ones too (2019.4 has no FindObjectsOfType(true)); only this scene's.
        foreach (Renderer r in Resources.FindObjectsOfTypeAll<Renderer>())
        {
            if (r.gameObject.scene != scene || EditorUtility.IsPersistent(r)) continue;
            Material m = r.sharedMaterial;
            if (m == null || !IsCopy(m)) continue;

            Material o = OriginalOf(m);
            if (o == m) { Debug.LogWarning("[GalaxyFieldBuilder_NEW] " + m.name + ": its original is gone; left.", r); continue; }

            Undo.RecordObject(r, "Revert galaxy-first look");
            r.sharedMaterial = o;
            reverted++;
        }

        if (reverted > 0) EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[GalaxyFieldBuilder_NEW] Reverted " + reverted + " renderer(s) to their original materials. Save the scene.");
    }

    // -- dark matter Y in both webs ------------------------------------------------------

    /// <summary>Scaffold bake, as DarkMatterMomentBuilder_NEW does it for Macro.</summary>
    const int ScaffoldResolution = 96;
    const float ScaffoldPadding = 0.12f;
    const int ScaffoldBlurRadius = 3, ScaffoldBlurPasses = 2;
    const float ScaffoldGamma = 0.65f;

    [MenuItem(Menu + "Set Up Dark Matter Y (open scene)")]
    public static void SetUpDarkMatterY()
    {
        if (!ConfirmScene("Dark matter Y")) return;

        WorldSwitcher_NEW worlds = UnityEngine.Object.FindObjectOfType<WorldSwitcher_NEW>();
        DarkMatterMoment_NEW moment = UnityEngine.Object.FindObjectOfType<DarkMatterMoment_NEW>();
        if (worlds == null) return;

        Transform macro = GroupRoot(worlds, "Macro");
        Transform web = GroupRoot(worlds, "CosmicWeb");
        DarkMatterScaffold_NEW macroScaffold = macro != null ? macro.GetComponentInChildren<DarkMatterScaffold_NEW>(true) : null;
        if (macroScaffold == null)
        {
            EditorUtility.DisplayDialog("Dark matter Y", "No dark matter scaffold in the early cosmic web. Run Tools > Journey NEW > " +
                                        "Dark Matter > Build Dark Matter Moment first.", "OK");
            return;
        }

        Undo.SetCurrentGroupName("Set up dark matter Y");

        // 1  A scaffold for the second cosmic web, made like the first one's.
        if (web != null && web.GetComponentInChildren<DarkMatterScaffold_NEW>(true) == null)
            BakeScaffold(web, macroScaffold.GetComponent<MeshRenderer>().sharedMaterial);

        // 2  The toggle.
        DarkMatterToggleTest_NEW toggle = UnityEngine.Object.FindObjectOfType<DarkMatterToggleTest_NEW>();
        if (toggle == null)
        {
            GameObject go = new GameObject("DarkMatterToggleTest");
            Undo.RegisterCreatedObjectUndo(go, "Create dark matter toggle");
            if (moment != null && moment.transform.parent != null) go.transform.SetParent(moment.transform.parent, false);
            toggle = go.AddComponent<DarkMatterToggleTest_NEW>();
        }

        // 3  In this scene, the moment no longer answers Y once it is over: the toggle does.
        if (moment != null)
        {
            SerializedObject so = new SerializedObject(moment);
            SerializedProperty p = so.FindProperty("toggleAfterMoment");
            if (p != null && p.boolValue) { p.boolValue = false; so.ApplyModifiedProperties(); }
        }

        EditorSceneManager.MarkSceneDirty(worlds.gameObject.scene);
        Debug.Log("[GalaxyFieldBuilder_NEW] Y now shows and hides dark matter in Macro and CosmicWeb. Save the scene.", toggle);
    }

    static void BakeScaffold(Transform root, Material template)
    {
        List<Source> sources = CollectSources(root, false);
        if (sources.Count == 0) { Debug.LogWarning("[GalaxyFieldBuilder_NEW] No web volume under " + root.name + "; no scaffold.", root); return; }

        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        foreach (Source s in sources)
        {
            Matrix4x4 toRoot = root.worldToLocalMatrix * s.renderer.transform.localToWorldMatrix;
            for (int c = 0; c < 8; c++)
            {
                Vector3 p = toRoot.MultiplyPoint3x4(new Vector3((c & 1) == 0 ? -0.5f : 0.5f, (c & 2) == 0 ? -0.5f : 0.5f, (c & 4) == 0 ? -0.5f : 0.5f));
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
        }
        Vector3 size = max - min;
        min -= size * ScaffoldPadding;
        size *= 1f + 2f * ScaffoldPadding;

        int n = ScaffoldResolution;
        float[] field = new float[n * n * n];
        try
        {
            for (int z = 0; z < n; z++)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Dark matter", "Sampling " + root.name, z / (float)n)) return;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        Vector3 world = root.TransformPoint(min + Vector3.Scale(size, new Vector3((x + 0.5f) / n, (y + 0.5f) / n, (z + 0.5f) / n)));
                        float best = 0f;
                        foreach (Source s in sources)
                        {
                            Vector3 uv = s.worldToLocal.MultiplyPoint3x4(world) + new Vector3(0.5f, 0.5f, 0.5f);
                            if (uv.x < 0f || uv.y < 0f || uv.z < 0f || uv.x > 1f || uv.y > 1f || uv.z > 1f) continue;
                            best = Mathf.Max(best, SampleAlpha(s, uv) * s.density);
                        }
                        field[(z * n + y) * n + x] = best;
                    }
            }
        }
        finally { EditorUtility.ClearProgressBar(); }

        for (int p = 0; p < ScaffoldBlurPasses; p++) Blur(field, n, ScaffoldBlurRadius);
        float peak = 0f;
        foreach (float f in field) peak = Mathf.Max(peak, f);
        if (peak <= 0f) return;

        var pixels = new Color32[field.Length];
        for (int i = 0; i < field.Length; i++)
            pixels[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(Mathf.Pow(field[i] / peak, ScaffoldGamma)) * 255f));

        EnsureFolder();
        string texPath = AssetFolder + "/DM_Scaffold_" + root.name + "_density.asset";
        AssetDatabase.DeleteAsset(texPath);
        var tex = new Texture3D(n, n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        tex.SetPixels32(pixels);
        tex.Apply(false);
        AssetDatabase.CreateAsset(tex, texPath);

        string matPath = AssetFolder + "/DM_Scaffold_" + root.name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(template) { name = "DM_Scaffold_" + root.name };
            AssetDatabase.CreateAsset(mat, matPath);
        }
        mat.SetTexture("_BakedTex", tex);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();

        GameObject go = new GameObject("Dark Matter Scaffold");
        Undo.RegisterCreatedObjectUndo(go, "Create dark matter scaffold");
        go.transform.SetParent(root, false);
        go.layer = sources[0].renderer.gameObject.layer;
        go.transform.localPosition = min + size * 0.5f;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = size;
        go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        go.AddComponent<DarkMatterScaffold_NEW>();

        Debug.Log("[GalaxyFieldBuilder_NEW] Baked a dark matter scaffold for " + root.name + " from " + sources.Count + " volume(s).", go);
    }

    /// <summary>Separable Gaussian, in place, clamped at the edges.</summary>
    static void Blur(float[] f, int n, int radius)
    {
        float[] k = new float[radius * 2 + 1];
        float sigma = Mathf.Max(0.5f, radius * 0.5f), sum = 0f;
        for (int i = -radius; i <= radius; i++) { k[i + radius] = Mathf.Exp(-(i * i) / (2f * sigma * sigma)); sum += k[i + radius]; }
        for (int i = 0; i < k.Length; i++) k[i] /= sum;

        float[] tmp = new float[f.Length];
        int[] strides = { 1, n, n * n };
        for (int axis = 0; axis < 3; axis++)
        {
            int stride = strides[axis];
            for (int z = 0; z < n; z++)
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        int idx = (z * n + y) * n + x;
                        int coord = axis == 0 ? x : axis == 1 ? y : z;
                        float acc = 0f;
                        for (int i = -radius; i <= radius; i++)
                            acc += f[idx + (Mathf.Clamp(coord + i, 0, n - 1) - coord) * stride] * k[i + radius];
                        tmp[idx] = acc;
                    }
            Array.Copy(tmp, f, f.Length);
        }
    }

    // -- scene guard ---------------------------------------------------------------------

    static bool ConfirmScene(string what)
    {
        string name = EditorSceneManager.GetActiveScene().name;
        if (name == IntendedScene) return true;
        return EditorUtility.DisplayDialog(what,
            "This is meant for " + IntendedScene + ", and the open scene is " + name +
            ". Run it here anyway? It changes this scene.", "Run here", "Cancel");
    }

    // -- sampling ------------------------------------------------------------------------

    struct Source
    {
        public Renderer renderer;
        public Matrix4x4 worldToLocal;
        /// <summary>Per voxel: density × how yellow it is (the galaxies' threads), or plain density as a fallback.</summary>
        public float[] weight;
        public int w, h, d;
        public float density;
    }

    /// <summary>
    /// How yellow a baked colour is, 0 to 1: high red and green over blue. The web's RGB is
    /// colour × emission, so only the ratio counts. Yellow (1, 0.9, 0.3) is 0.6; the purple gas
    /// and white are 0.
    /// </summary>
    static float Yellowness(Color c)
    {
        float top = Mathf.Max(c.r, c.g);
        if (top <= 1e-4f) return 0f;
        return Mathf.Clamp01((Mathf.Min(c.r, c.g) - c.b) / top);
    }

    /// <summary>A volume material whose texture is mostly yellow: one of the web's yellow threads.</summary>
    static bool IsYellow(Material m)
    {
        Texture3D tex = m.GetTexture("_BakedTex") as Texture3D;
        if (tex == null || !tex.isReadable) return false;
        double ya = 0, a = 0;
        foreach (Color c in tex.GetPixels(0)) { ya += c.a * Yellowness(c); a += c.a; }
        return a > 0 && ya / a > 0.25;
    }

    static List<Source> CollectSources(Transform root, bool useYellow)
    {
        var list = new List<Source>();
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(false))
        {
            Material m = r.sharedMaterial;
            if (m == null || m.shader == null || m.shader.name != VolumeShaderName) continue;

            Texture3D tex = m.GetTexture("_BakedTex") as Texture3D;
            if (tex == null) continue;
            if (!tex.isReadable)
            {
                Debug.LogWarning("[GalaxyFieldBuilder_NEW] " + r.name + ": " + tex.name + " is not readable; skipped.", r);
                continue;
            }

            Color[] pixels = tex.GetPixels(0);
            float[] weight = new float[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
                // Yellow: how brightly it shines yellow (the RGB is colour × emission), so the
                // galaxies follow the bright yellow cores, not the faint yellow haze round them.
                weight[i] = useYellow
                    ? Mathf.Max(pixels[i].r, Mathf.Max(pixels[i].g, pixels[i].b)) * Yellowness(pixels[i])
                    : pixels[i].a;

            list.Add(new Source
            {
                renderer = r,
                worldToLocal = r.transform.worldToLocalMatrix,
                weight = weight,
                w = tex.width, h = tex.height, d = tex.depth,
                density = m.HasProperty("_Density") ? m.GetFloat("_Density") : 1f
            });
        }
        return list;
    }

    static Mesh BakeMesh(string id, Transform root, List<Source> sources, out int nodeCount, out int galaxyCount)
    {
        nodeCount = galaxyCount = 0;

        // The web's box, in the group root's space.
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        foreach (Source s in sources)
        {
            Matrix4x4 toRoot = root.worldToLocalMatrix * s.renderer.transform.localToWorldMatrix;
            for (int c = 0; c < 8; c++)
            {
                Vector3 p = toRoot.MultiplyPoint3x4(new Vector3((c & 1) == 0 ? -0.5f : 0.5f, (c & 2) == 0 ? -0.5f : 0.5f, (c & 4) == 0 ? -0.5f : 0.5f));
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
        }

        Vector3 size = max - min;
        float cell = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) / Resolution;
        int nx = Mathf.Max(2, Mathf.CeilToInt(size.x / cell)), ny = Mathf.Max(2, Mathf.CeilToInt(size.y / cell)), nz = Mathf.Max(2, Mathf.CeilToInt(size.z / cell));
        float[] field = new float[nx * ny * nz];
        Func<int, int, int, int> I = (x, y, z) => (z * ny + y) * nx + x;

        float peak = 0f;
        try
        {
            for (int z = 0; z < nz; z++)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Galaxies", "Sampling " + id, z / (float)nz)) return null;
                for (int y = 0; y < ny; y++)
                    for (int x = 0; x < nx; x++)
                    {
                        Vector3 world = root.TransformPoint(min + new Vector3((x + 0.5f) * cell, (y + 0.5f) * cell, (z + 0.5f) * cell));
                        float best = 0f;
                        foreach (Source s in sources)
                        {
                            Vector3 uv = s.worldToLocal.MultiplyPoint3x4(world) + new Vector3(0.5f, 0.5f, 0.5f);
                            if (uv.x < 0f || uv.y < 0f || uv.z < 0f || uv.x > 1f || uv.y > 1f || uv.z > 1f) continue;
                            best = Mathf.Max(best, SampleAlpha(s, uv) * s.density);
                        }
                        field[I(x, y, z)] = best;
                        peak = Mathf.Max(peak, best);
                    }
            }
        }
        finally { EditorUtility.ClearProgressBar(); }

        if (peak <= 0f) { Debug.LogWarning("[GalaxyFieldBuilder_NEW] " + id + ": the web sampled to nothing."); return null; }
        for (int i = 0; i < field.Length; i++) field[i] /= peak;

        var rng = new System.Random(Seed + id.GetHashCode());
        var centres = new List<Vector3>();
        var sizes = new List<float>();
        var brights = new List<float>();
        var colours = new List<Color>();

        Func<int, int, int, Vector3> Centre = (x, y, z) => min + new Vector3((x + 0.5f) * cell, (y + 0.5f) * cell, (z + 0.5f) * cell);

        // Nodes: local maxima, densest first.
        var nodes = new List<(float v, int x, int y, int z)>();
        for (int z = 1; z < nz - 1; z++)
            for (int y = 1; y < ny - 1; y++)
                for (int x = 1; x < nx - 1; x++)
                {
                    float v = field[I(x, y, z)];
                    if (v < NodeThreshold) continue;
                    bool top = true;
                    for (int dz = -1; dz <= 1 && top; dz++)
                        for (int dy = -1; dy <= 1 && top; dy++)
                            for (int dx = -1; dx <= 1 && top; dx++)
                                if ((dx | dy | dz) != 0 && field[I(x + dx, y + dy, z + dz)] > v) top = false;
                    if (top) nodes.Add((v, x, y, z));
                }
        nodes.Sort((a, b) => b.v.CompareTo(a.v));
        if (nodes.Count > MaxNodes) nodes.RemoveRange(MaxNodes, nodes.Count - MaxNodes);
        nodeCount = nodes.Count;

        foreach (var n in nodes)
        {
            float t = Mathf.InverseLerp(NodeThreshold, 1f, n.v);
            int count = Mathf.RoundToInt(Mathf.Lerp(ClusterMin, ClusterMax, t * t));
            float spread = cell * ClusterSpread * (0.5f + 0.5f * t);
            Vector3 c = Centre(n.x, n.y, n.z);
            for (int k = 0; k < count; k++)
            {
                Vector3 off = new Vector3(Gauss(rng), Gauss(rng), Gauss(rng)) * spread;
                float r = Mathf.Clamp01(off.magnitude / (spread * 2f));
                // The brightest at the heart: the cluster's giant ellipticals.
                float b = Mathf.Lerp(0.9f, 0.3f, r) * (0.6f + 0.6f * (float)rng.NextDouble()) * (k == 0 ? 1.6f : 1f);
                centres.Add(c + (k == 0 ? Vector3.zero : off));
                sizes.Add(cell * ClusterSize * (k == 0 ? 2.2f : 0.7f + 0.8f * (float)rng.NextDouble()));
                brights.Add(b);
                colours.Add(Color.Lerp(ClusterCore, ClusterEdge, r));
            }
        }

        // Threads: a chance per voxel, in proportion to density squared. Drawn over the whole
        // web first and thinned evenly after, so a cap never fills up on the first slabs.
        var threads = new List<(int x, int y, int z, float v)>();
        for (int z = 0; z < nz; z++)
            for (int y = 0; y < ny; y++)
                for (int x = 0; x < nx; x++)
                {
                    float v = field[I(x, y, z)];
                    if (v < ThreadThreshold || rng.NextDouble() > ThreadRate * v * v * v) continue;
                    threads.Add((x, y, z, v));
                }

        int room = Mathf.Max(0, MaxGalaxies - centres.Count);
        for (int i = 0; i < threads.Count && i < room; i++)
        {
            int j = i + rng.Next(threads.Count - i);
            var tmp = threads[i]; threads[i] = threads[j]; threads[j] = tmp;

            var g = threads[i];
            Vector3 jitter = new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * cell;
            centres.Add(Centre(g.x, g.y, g.z) + jitter);
            sizes.Add(cell * ThreadSize * (0.6f + 0.8f * (float)rng.NextDouble()));
            brights.Add((0.1f + 0.25f * g.v) * (0.6f + 0.8f * (float)rng.NextDouble()));
            colours.Add(ThreadColour);
        }
        if (threads.Count > room)
            Debug.Log("[GalaxyFieldBuilder_NEW] " + id + ": " + threads.Count + " thread galaxies drawn, " + room +
                      " kept (MaxGalaxies), thinned evenly over the web.");

        galaxyCount = centres.Count;
        if (galaxyCount == 0) { Debug.LogWarning("[GalaxyFieldBuilder_NEW] " + id + ": no galaxies; thresholds too high?"); return null; }

        return WriteMesh(id, centres, sizes, brights, colours, cell);
    }

    /// <summary>Galaxies on the yellow meshes at most, thread galaxies included.</summary>
    const int MaxThreadGalaxies = 3000;

    /// <summary>
    /// Every vertex of the web's yellow volume meshes, in the group root's space. A volume is
    /// yellow when its baked texture is (IsYellow): Core and Coreyellow2 in the early web.
    /// </summary>
    static List<Vector3> YellowPoints(Transform root, List<string> used, out int layer)
    {
        var points = new List<Vector3>();
        layer = root.gameObject.layer;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(false))
        {
            Material m = r.sharedMaterial == null ? null : OriginalOf(r.sharedMaterial);
            if (m == null || m.shader == null || m.shader.name != VolumeShaderName || !IsYellow(m)) continue;

            MeshFilter mf = r.GetComponent<MeshFilter>();
            Mesh mesh = mf != null ? mf.sharedMesh : null;
            if (mesh == null) continue;

            Vector3[] verts = mesh.vertices;
            if (verts == null || verts.Length == 0)
            {
                Debug.LogWarning("[GalaxyFieldBuilder_NEW] " + r.name + ": can't read the vertices of " + mesh.name +
                                 ". Tick Read/Write on its model's import settings and bake again.", r);
                continue;
            }

            Matrix4x4 toRoot = root.worldToLocalMatrix * r.transform.localToWorldMatrix;
            foreach (Vector3 v in verts) points.Add(toRoot.MultiplyPoint3x4(v));
            layer = r.gameObject.layer;
            used.Add(r.name + " (" + verts.Length + " verts)");
        }
        return points;
    }

    /// <summary>
    /// Galaxies on points: clusters where the points crowd (local maxima of their count on a
    /// grid), and single galaxies on a random, even sample of the points themselves.
    /// </summary>
    static Mesh BakeFromPoints(string id, List<Vector3> points, out int nodeCount, out int galaxyCount)
    {
        Vector3 lo = Vector3.positiveInfinity, hi = Vector3.negativeInfinity;
        foreach (Vector3 p in points) { lo = Vector3.Min(lo, p); hi = Vector3.Max(hi, p); }
        Vector3 size = hi - lo;
        float cell = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) / Resolution;
        cell = Mathf.Max(cell, 1e-6f);
        int nx = Mathf.Max(3, Mathf.CeilToInt(size.x / cell) + 1), ny = Mathf.Max(3, Mathf.CeilToInt(size.y / cell) + 1), nz = Mathf.Max(3, Mathf.CeilToInt(size.z / cell) + 1);
        Func<int, int, int, int> I = (x, y, z) => (z * ny + y) * nx + x;
        Func<Vector3, int> Cell = p => I(Mathf.Clamp((int)((p.x - lo.x) / cell), 0, nx - 1),
                                         Mathf.Clamp((int)((p.y - lo.y) / cell), 0, ny - 1),
                                         Mathf.Clamp((int)((p.z - lo.z) / cell), 0, nz - 1));

        // Point counts, then each cell summed with its 26 neighbours so a crowd reads as one.
        float[] count = new float[nx * ny * nz];
        foreach (Vector3 p in points) count[Cell(p)] += 1f;
        float[] field = new float[count.Length];
        float peak = 0f;
        for (int z = 0; z < nz; z++)
            for (int y = 0; y < ny; y++)
                for (int x = 0; x < nx; x++)
                {
                    float s = 0f;
                    for (int dz = -1; dz <= 1; dz++)
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int X = x + dx, Y = y + dy, Z = z + dz;
                                if (X < 0 || Y < 0 || Z < 0 || X >= nx || Y >= ny || Z >= nz) continue;
                                s += count[I(X, Y, Z)];
                            }
                    field[I(x, y, z)] = s;
                    peak = Mathf.Max(peak, s);
                }
        for (int i = 0; i < field.Length; i++) field[i] /= peak;

        var rng = new System.Random(Seed + id.GetHashCode());
        var centres = new List<Vector3>();
        var sizes = new List<float>();
        var brights = new List<float>();
        var colours = new List<Color>();

        // Clusters at the crowds.
        var nodes = new List<(float v, int x, int y, int z)>();
        for (int z = 1; z < nz - 1; z++)
            for (int y = 1; y < ny - 1; y++)
                for (int x = 1; x < nx - 1; x++)
                {
                    float v = field[I(x, y, z)];
                    if (v < NodeThreshold) continue;
                    bool top = true;
                    for (int dz = -1; dz <= 1 && top; dz++)
                        for (int dy = -1; dy <= 1 && top; dy++)
                            for (int dx = -1; dx <= 1 && top; dx++)
                                if ((dx | dy | dz) != 0 && field[I(x + dx, y + dy, z + dz)] > v) top = false;
                    if (top) nodes.Add((v, x, y, z));
                }
        nodes.Sort((a, b) => b.v.CompareTo(a.v));
        if (nodes.Count > MaxNodes) nodes.RemoveRange(MaxNodes, nodes.Count - MaxNodes);
        nodeCount = nodes.Count;

        foreach (var n in nodes)
        {
            float t = Mathf.InverseLerp(NodeThreshold, 1f, n.v);
            int k = Mathf.RoundToInt(Mathf.Lerp(ClusterMin, ClusterMax, t * t));
            float spread = cell * (0.5f + 0.5f * t);
            Vector3 c = lo + new Vector3((n.x + 0.5f) * cell, (n.y + 0.5f) * cell, (n.z + 0.5f) * cell);
            for (int j = 0; j < k; j++)
            {
                Vector3 off = new Vector3(Gauss(rng), Gauss(rng), Gauss(rng)) * spread;
                float r = Mathf.Clamp01(off.magnitude / (spread * 2f));
                centres.Add(c + (j == 0 ? Vector3.zero : off));
                sizes.Add(cell * ClusterSize * (j == 0 ? 2f : 0.7f + 0.6f * (float)rng.NextDouble()));
                brights.Add(Mathf.Lerp(0.9f, 0.3f, r) * (0.6f + 0.6f * (float)rng.NextDouble()) * (j == 0 ? 1.6f : 1f));
                colours.Add(Color.Lerp(ClusterCore, ClusterEdge, r));
            }
        }

        // Single galaxies on the points themselves, an even random sample.
        int room = Mathf.Min(MaxThreadGalaxies, Mathf.Max(0, MaxGalaxies - centres.Count), points.Count);
        var order = new int[points.Count];
        for (int i = 0; i < order.Length; i++) order[i] = i;
        for (int i = 0; i < room; i++)
        {
            int j = i + rng.Next(order.Length - i);
            int tmp = order[i]; order[i] = order[j]; order[j] = tmp;
            Vector3 p = points[order[i]];
            float v = field[Cell(p)];
            centres.Add(p);
            sizes.Add(cell * ThreadSize * (0.6f + 0.8f * (float)rng.NextDouble()));
            brights.Add((0.12f + 0.3f * v) * (0.6f + 0.8f * (float)rng.NextDouble()));
            colours.Add(ThreadColour);
        }

        galaxyCount = centres.Count;
        if (galaxyCount == 0) return null;
        return WriteMesh(id, centres, sizes, brights, colours, cell);
    }

    static float Gauss(System.Random rng)
    {
        double u = 1.0 - rng.NextDouble(), v = rng.NextDouble();
        return (float)(Math.Sqrt(-2.0 * Math.Log(u)) * Math.Cos(2.0 * Math.PI * v));
    }

    static Mesh WriteMesh(string id, List<Vector3> centres, List<float> sizes, List<float> brights, List<Color> colours, float cell)
    {
        int n = centres.Count;
        var v = new Vector3[n * 4];
        var uv = new Vector2[n * 4];
        var uv2 = new Vector2[n * 4];
        var col = new Color[n * 4];
        var tri = new int[n * 6];
        Vector2[] corners = { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) };

        Vector3 lo = Vector3.positiveInfinity, hi = Vector3.negativeInfinity;
        float biggest = 0f;
        for (int g = 0; g < n; g++)
        {
            for (int c = 0; c < 4; c++)
            {
                int i = g * 4 + c;
                v[i] = centres[g];
                uv[i] = corners[c];
                uv2[i] = new Vector2(sizes[g], brights[g]);
                col[i] = colours[g];
            }
            int t = g * 6, b = g * 4;
            tri[t] = b; tri[t + 1] = b + 1; tri[t + 2] = b + 2;
            tri[t + 3] = b; tri[t + 4] = b + 2; tri[t + 5] = b + 3;
            lo = Vector3.Min(lo, centres[g]);
            hi = Vector3.Max(hi, centres[g]);
            biggest = Mathf.Max(biggest, sizes[g]);
        }

        EnsureFolder();
        string path = AssetFolder + "/Galaxies_" + id + ".asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool fresh = mesh == null;
        if (fresh) mesh = new Mesh();
        mesh.Clear();
        mesh.name = "Galaxies_" + id;
        mesh.indexFormat = n * 4 > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.vertices = v;
        mesh.uv = uv;
        mesh.uv2 = uv2;
        mesh.colors = col;
        mesh.triangles = tri;
        // The quads open in the vertex shader; leave room for that, and for the minimum
        // size on screen, so the field is not culled at its edges.
        Bounds bounds = new Bounds((lo + hi) * 0.5f, hi - lo);
        bounds.Expand(Mathf.Max(biggest, cell) * 4f);
        mesh.bounds = bounds;

        if (fresh) AssetDatabase.CreateAsset(mesh, path);
        else EditorUtility.SetDirty(mesh);
        AssetDatabase.SaveAssets();
        return mesh;
    }

    static void PlaceChild(Transform root, Mesh mesh, Material material, int layer)
    {
        Transform child = root.Find(ChildName);
        GameObject go;
        if (child == null)
        {
            go = new GameObject(ChildName);
            Undo.RegisterCreatedObjectUndo(go, "Create galaxies");
            go.transform.SetParent(root, false);
            go.layer = layer;
            go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }
        else go = child.gameObject;

        Undo.RecordObject(go.transform, "Place galaxies");
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        MeshFilter f = go.GetComponent<MeshFilter>();
        MeshRenderer r = go.GetComponent<MeshRenderer>();
        Undo.RecordObject(f, "Galaxies mesh");
        Undo.RecordObject(r, "Galaxies material");
        f.sharedMesh = mesh;
        r.sharedMaterial = material;
    }

    static float SampleAlpha(Source s, Vector3 uv)
    {
        float fx = Mathf.Clamp(uv.x * s.w - 0.5f, 0f, s.w - 1), fy = Mathf.Clamp(uv.y * s.h - 0.5f, 0f, s.h - 1), fz = Mathf.Clamp(uv.z * s.d - 0.5f, 0f, s.d - 1);
        int x0 = (int)fx, y0 = (int)fy, z0 = (int)fz;
        int x1 = Mathf.Min(x0 + 1, s.w - 1), y1 = Mathf.Min(y0 + 1, s.h - 1), z1 = Mathf.Min(z0 + 1, s.d - 1);
        float tx = fx - x0, ty = fy - y0, tz = fz - z0;
        float c00 = Mathf.Lerp(A(s, x0, y0, z0), A(s, x1, y0, z0), tx);
        float c10 = Mathf.Lerp(A(s, x0, y1, z0), A(s, x1, y1, z0), tx);
        float c01 = Mathf.Lerp(A(s, x0, y0, z1), A(s, x1, y0, z1), tx);
        float c11 = Mathf.Lerp(A(s, x0, y1, z1), A(s, x1, y1, z1), tx);
        return Mathf.Lerp(Mathf.Lerp(c00, c10, ty), Mathf.Lerp(c01, c11, ty), tz);
    }

    static float A(Source s, int x, int y, int z) { return s.weight[(z * s.h + y) * s.w + x]; }

    // -- assets --------------------------------------------------------------------------

    static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(AssetFolder))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(AssetFolder).Replace('\\', '/'), Path.GetFileName(AssetFolder));
    }

    static Material EnsureMaterial()
    {
        EnsureFolder();
        Material m = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (m != null) return m;

        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError("[GalaxyFieldBuilder_NEW] Shader " + ShaderName + " not found. Has Unity imported Resources/GalaxyField_NEW.shader?");
            return null;
        }

        // After the web's volumes (2950 to 2954), with the close-up galaxies (2989 to 2993).
        m = new Material(shader) { name = "GalaxyField", renderQueue = 2988 };
        AssetDatabase.CreateAsset(m, MaterialPath);
        AssetDatabase.SaveAssets();
        return m;
    }

    /// <summary>The material a copy was made from; the material itself if it is not a copy.</summary>
    static Material OriginalOf(Material m)
    {
        if (!IsCopy(m)) return m;
        string guid = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(m)).userData;
        Material o = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
        return o != null ? o : m;
    }

    static bool IsCopy(Material m)
    {
        string path = AssetDatabase.GetAssetPath(m);
        return path.StartsWith(AssetFolder + "/") && !string.IsNullOrEmpty(AssetImporter.GetAtPath(path)?.userData);
    }

    /// <summary>A copy of a material in the test folder, made once; its importer remembers the original.</summary>
    static Material CopyOf(Material original, string suffix, Action<Material> change)
    {
        string path = AssetFolder + "/" + original.name + "_" + suffix + ".mat";
        Material copy = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (copy != null) return copy;

        copy = new Material(original) { name = original.name + "_" + suffix };
        change(copy);
        AssetDatabase.CreateAsset(copy, path);
        AssetImporter importer = AssetImporter.GetAtPath(path);
        importer.userData = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(original));
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    static Transform GroupRoot(WorldSwitcher_NEW worlds, string layerId)
    {
        SerializedProperty groups = new SerializedObject(worlds).FindProperty("groups");
        if (groups == null) return null;
        for (int i = 0; i < groups.arraySize; i++)
        {
            SerializedProperty g = groups.GetArrayElementAtIndex(i);
            if (!string.Equals(g.FindPropertyRelative("layerId").stringValue, layerId, StringComparison.OrdinalIgnoreCase)) continue;
            UnityEngine.Object o = g.FindPropertyRelative("root").objectReferenceValue;
            if (o is Transform t) return t;
            if (o is GameObject go) return go.transform;
        }
        return null;
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Tools > Journey NEW > Dark Matter > Build Dark Matter Moment (open scene)
///
/// Puts the dark matter moment into a journey scene (Gameplay_Scene_Ana or _Aaron):
///
///   1  BAKE the scaffold from the first cosmic web's own volumes: every
///      Custom/PreBakedCloudVolume under the Macro render group is sampled into one box,
///      the densest of them wins at each point, and the result is blurred wider and
///      softer — dark matter's halo around the gas. Written to
///      _Core_NEW/Assets/DarkMatter/DM_Scaffold_density.asset.
///   2  SCAFFOLD: one box in the Macro render group drawing that texture
///      (Custom/DarkMatterScaffold_NEW), with DarkMatterScaffold_NEW. In the group, so it
///      fades and dives with the web.
///   3  HINT: a plate on the LAF HUD canvas, row 4, with JourneyHint_NEW.
///   4  MOMENT: DarkMatterMoment_NEW beside the voice-over, wired to everything above, its
///      lines filled from Assets/VoiceOver/Journey where the recordings exist.
///   5  The Macro voice-over row's onFinished calls the moment's Begin: it starts when
///      J3 has been said.
///
/// RE-RUNNING IS SAFE AND IS HOW YOU REBAKE. The texture is rewritten (same asset, same
/// GUID) and the box moved to match it — the two only work together. Everything a person
/// tunes is left alone: the material's colour and numbers, the moment's version and timings,
/// the hint's place and size, any clip already in a slot. Run it again after moving or
/// adding a web volume.
/// </summary>
public static class DarkMatterMomentBuilder_NEW
{
    const string AssetFolder = "Assets/_Core_NEW/Assets/DarkMatter";
    const string TexturePath = AssetFolder + "/DM_Scaffold_density.asset";
    const string MaterialPath = AssetFolder + "/DM_Scaffold.mat";
    const string ShaderName = "Custom/DarkMatterScaffold_NEW";
    const string SourceShaderName = "Custom/PreBakedCloudVolume";
    const string VoiceFolder = "Assets/VoiceOver/Journey";

    const string LayerId = "Macro";

    /// <summary>Voxels along each side of the scaffold. 96³ RGBA32 is 3.5 MB.</summary>
    const int Resolution = 96;

    /// <summary>Extra room round the web's box, as a fraction of its size, so the blur has
    /// somewhere to spread instead of being cut flat at the edge.</summary>
    const float Padding = 0.12f;

    /// <summary>Gaussian blur radius in voxels, and how many passes. Wider = softer halo.</summary>
    const int BlurRadius = 3;
    const int BlurPasses = 2;

    /// <summary>Applied after the blur and normalise: below 1 lifts the thin halo, so the
    /// scaffold reads as wider than the gas rather than as a copy of it.</summary>
    const float HaloGamma = 0.65f;

    // The recordings, by the ids in the VO script (Part 2, J3 dark matter stop).
    static readonly string[] ShownClips = { "J3_DMon_Before", "J3_DMon_After", "N_J3_DMon" };
    static readonly string[] HiddenClips = { "J3_DMoff_Before", "J3_DMoff_After", "N_J3_DMoff" };

    static int _changes;

    [MenuItem("Tools/Journey NEW/Dark Matter/Build Dark Matter Moment (open scene)")]
    public static void Run()
    {
        _changes = 0;

        WorldSwitcher_NEW worlds = UnityEngine.Object.FindObjectOfType<WorldSwitcher_NEW>();
        JourneyVoiceOver_NEW voiceOver = UnityEngine.Object.FindObjectOfType<JourneyVoiceOver_NEW>();

        if (worlds == null || voiceOver == null)
        {
            EditorUtility.DisplayDialog("Dark matter moment",
                "This scene has no WorldSwitcher_NEW or no JourneyVoiceOver_NEW, so it is not a journey " +
                "scene, or its voice-over is not wired yet. Open Gameplay_Scene_Ana, run Tools > Journey NEW > " +
                "Voice Over > Wire Journey Voice Over first, then this.", "OK");
            return;
        }

        Transform root = GroupRoot(worlds, LayerId);
        if (root == null)
        {
            EditorUtility.DisplayDialog("Dark matter moment",
                "WorldSwitcher_NEW has no render group for '" + LayerId + "'. Nothing to bake from.", "OK");
            return;
        }

        Undo.SetCurrentGroupName("Build dark matter moment");

        DarkMatterScaffold_NEW scaffold = BuildScaffold(root);
        if (scaffold == null) return;

        JourneyHint_NEW hint = BuildHint();
        DarkMatterMoment_NEW stop = BuildMoment(voiceOver, scaffold, hint);
        WireBegin(voiceOver, stop);

        EditorSceneManager.MarkSceneDirty(voiceOver.gameObject.scene);

        SerializedProperty version = new SerializedObject(stop).FindProperty("version");
        Debug.Log("[DarkMatterMomentBuilder_NEW] Done, " + _changes + " change(s). Save the scene. Version: " +
                  version.enumDisplayNames[version.enumValueIndex] +
                  ". In Play: press 2 (Macro) and wait for J3, or press F8 there to start the moment at once.", stop);
    }

    // -- 1 + 2: bake and scaffold -------------------------------------------------------

    struct Source
    {
        public Renderer renderer;
        public Matrix4x4 worldToLocal;
        public Color[] pixels;
        public int w, h, d;
        public float density;
    }

    static DarkMatterScaffold_NEW BuildScaffold(Transform root)
    {
        List<Source> sources = CollectSources(root);
        if (sources.Count == 0)
        {
            EditorUtility.DisplayDialog("Dark matter moment",
                "No active " + SourceShaderName + " volume with a readable texture under '" + root.name +
                "'. Nothing to bake from.", "OK");
            return null;
        }

        // The box, in the group root's own space, so it follows whatever the dive does to the root.
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        foreach (Source s in sources)
        {
            Matrix4x4 toRoot = root.worldToLocalMatrix * s.renderer.transform.localToWorldMatrix;
            for (int c = 0; c < 8; c++)
            {
                Vector3 corner = new Vector3((c & 1) == 0 ? -0.5f : 0.5f, (c & 2) == 0 ? -0.5f : 0.5f, (c & 4) == 0 ? -0.5f : 0.5f);
                Vector3 p = toRoot.MultiplyPoint3x4(corner);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
        }

        Vector3 size = max - min;
        min -= size * Padding;
        max += size * Padding;
        size = max - min;
        Vector3 centre = (min + max) * 0.5f;

        float[] field = Bake(root, sources, min, size);
        if (field == null) return null;

        Texture3D texture = WriteTexture(field);
        Material material = EnsureMaterial(texture);

        DarkMatterScaffold_NEW scaffold = root.GetComponentInChildren<DarkMatterScaffold_NEW>(true);
        GameObject go;

        if (scaffold == null)
        {
            go = new GameObject("Dark Matter Scaffold");
            Undo.RegisterCreatedObjectUndo(go, "Create dark matter scaffold");
            go.transform.SetParent(root, false);
            go.layer = sources[0].renderer.gameObject.layer;

            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            scaffold = go.AddComponent<DarkMatterScaffold_NEW>();
            _changes++;
        }
        else
        {
            go = scaffold.gameObject;
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr != null && mr.sharedMaterial != material)
            {
                Undo.RecordObject(mr, "Dark matter material");
                mr.sharedMaterial = material;
                _changes++;
            }
        }

        // The box and the texture are one thing: always put the box where this bake is.
        Undo.RecordObject(go.transform, "Place dark matter scaffold");
        go.transform.localPosition = centre;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = size;
        _changes++;

        Debug.Log("[DarkMatterMomentBuilder_NEW] Baked the scaffold from " + sources.Count + " web volume(s) at " +
                  Resolution + "³: " + string.Join(", ", sources.ConvertAll(s => s.renderer.name).ToArray()) + ".", go);

        return scaffold;
    }

    static List<Source> CollectSources(Transform root)
    {
        List<Source> list = new List<Source>();

        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(false))
        {
            Material m = r.sharedMaterial;
            if (m == null || m.shader == null || m.shader.name != SourceShaderName) continue;

            Texture3D tex = m.GetTexture("_BakedTex") as Texture3D;
            if (tex == null) continue;

            if (!tex.isReadable)
            {
                Debug.LogWarning("[DarkMatterMomentBuilder_NEW] " + r.name + ": " + tex.name + " is not readable; skipped.", r);
                continue;
            }

            list.Add(new Source
            {
                renderer = r,
                worldToLocal = r.transform.worldToLocalMatrix,
                pixels = tex.GetPixels(0),
                w = tex.width,
                h = tex.height,
                d = tex.depth,
                density = m.HasProperty("_Density") ? m.GetFloat("_Density") : 1f
            });
        }

        return list;
    }

    static float[] Bake(Transform root, List<Source> sources, Vector3 min, Vector3 size)
    {
        int n = Resolution;
        float[] field = new float[n * n * n];

        try
        {
            for (int z = 0; z < n; z++)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Dark matter", "Sampling the web", z / (float)n))
                    return null;

                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        Vector3 local = min + Vector3.Scale(size, new Vector3((x + 0.5f) / n, (y + 0.5f) / n, (z + 0.5f) / n));
                        Vector3 world = root.TransformPoint(local);

                        float best = 0f;
                        for (int i = 0; i < sources.Count; i++)
                        {
                            Source s = sources[i];
                            Vector3 uv = s.worldToLocal.MultiplyPoint3x4(world) + new Vector3(0.5f, 0.5f, 0.5f);
                            if (uv.x < 0f || uv.y < 0f || uv.z < 0f || uv.x > 1f || uv.y > 1f || uv.z > 1f) continue;

                            float a = SampleAlpha(s, uv) * s.density;
                            if (a > best) best = a;
                        }

                        field[(z * n + y) * n + x] = best;
                    }
            }

            EditorUtility.DisplayProgressBar("Dark matter", "Blurring", 1f);
            for (int p = 0; p < BlurPasses; p++) Blur(field, n, BlurRadius);

            float peak = 0f;
            for (int i = 0; i < field.Length; i++) if (field[i] > peak) peak = field[i];
            if (peak <= 0f)
            {
                Debug.LogError("[DarkMatterMomentBuilder_NEW] The web's volumes sampled to nothing. Not written.");
                return null;
            }

            for (int i = 0; i < field.Length; i++) field[i] = Mathf.Pow(field[i] / peak, HaloGamma);
            return field;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    /// <summary>Trilinear, clamped, on a texture's alpha (its density).</summary>
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

    static float A(Source s, int x, int y, int z) { return s.pixels[(z * s.h + y) * s.w + x].a; }

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
                        {
                            int c = Mathf.Clamp(coord + i, 0, n - 1);
                            acc += f[idx + (c - coord) * stride] * k[i + radius];
                        }
                        tmp[idx] = acc;
                    }
            Array.Copy(tmp, f, f.Length);
        }
    }

    static Texture3D WriteTexture(float[] field)
    {
        int n = Resolution;
        Color32[] pixels = new Color32[field.Length];
        for (int i = 0; i < field.Length; i++)
        {
            byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(field[i]) * 255f);
            pixels[i] = new Color32(255, 255, 255, a);
        }

        if (!AssetDatabase.IsValidFolder(AssetFolder))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(AssetFolder).Replace('\\', '/'), Path.GetFileName(AssetFolder));

        Texture3D existing = AssetDatabase.LoadAssetAtPath<Texture3D>(TexturePath);
        if (existing != null && existing.width == n && existing.height == n && existing.depth == n &&
            existing.format == TextureFormat.RGBA32)
        {
            // Same asset, same GUID: the material keeps pointing at it.
            existing.SetPixels32(pixels);
            existing.Apply(false);
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssets();
            return existing;
        }

        if (existing != null) AssetDatabase.DeleteAsset(TexturePath);

        Texture3D tex = new Texture3D(n, n, n, TextureFormat.RGBA32, false)
        {
            name = "DM_Scaffold_density",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        tex.SetPixels32(pixels);
        tex.Apply(false);

        AssetDatabase.CreateAsset(tex, TexturePath);
        AssetDatabase.SaveAssets();
        return tex;
    }

    static Material EnsureMaterial(Texture3D texture)
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (m == null)
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError("[DarkMatterMomentBuilder_NEW] Shader " + ShaderName + " not found. Has Unity imported " +
                               "Assets/_Core_NEW/Resources/DarkMatterScaffold_NEW.shader?");
                shader = Shader.Find("Unlit/Color");
            }

            m = new Material(shader) { name = "DM_Scaffold" };
            AssetDatabase.CreateAsset(m, MaterialPath);
            _changes++;
        }

        if (m.GetTexture("_BakedTex") != texture)
        {
            m.SetTexture("_BakedTex", texture);
            EditorUtility.SetDirty(m);
        }

        AssetDatabase.SaveAssets();
        return m;
    }

    // -- 3: hint ------------------------------------------------------------------------

    static JourneyHint_NEW BuildHint()
    {
        JourneyHint_NEW hint = UnityEngine.Object.FindObjectOfType<JourneyHint_NEW>();
        if (hint != null) return hint;

        Transform parent = FindRow(4);
        if (parent == null)
        {
            Debug.LogWarning("[DarkMatterMomentBuilder_NEW] No 'LAF HUD/Row_4' on a canvas; the hint goes on the first " +
                             "canvas found. Move it where it reads.");
            Canvas canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[DarkMatterMomentBuilder_NEW] No canvas in the scene; no hint made.");
                return null;
            }
            parent = canvas.transform;
        }

        GameObject go = new GameObject("DarkMatterHint", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create dark matter hint");
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.28f, 0.22f);
        rt.anchorMax = new Vector2(0.72f, 0.78f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        go.AddComponent<CanvasGroup>();

        GameObject plate = new GameObject("Plate", typeof(RectTransform));
        plate.transform.SetParent(go.transform, false);
        plate.layer = go.layer;
        Stretch((RectTransform)plate.transform, 0f);
        Image image = plate.AddComponent<Image>();
        image.sprite = TutorialWorldAssets_NEW.ChipSprite();
        image.type = Image.Type.Sliced;
        image.color = new Color(0.02f, 0.02f, 0.04f, 0.55f);
        image.raycastTarget = false;

        GameObject label = new GameObject("Label", typeof(RectTransform));
        label.transform.SetParent(go.transform, false);
        label.layer = go.layer;
        Stretch((RectTransform)label.transform, 0.06f);
        TextMeshProUGUI text = label.AddComponent<TextMeshProUGUI>();
        text.text = "Y  ·  SHOW DARK MATTER";
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        text.enableAutoSizing = true;
        text.fontSizeMin = 6f;
        text.fontSizeMax = 40f;
        text.characterSpacing = 4f;
        TMP_FontAsset font = TutorialWorldAssets_NEW.HudFont();
        if (font != null) text.font = font;

        hint = go.AddComponent<JourneyHint_NEW>();
        _changes++;

        Debug.Log("[DarkMatterMomentBuilder_NEW] Made the hint plate under " + parent.name +
                  ". Off until the moment asks; move or resize it freely.", go);
        return hint;
    }

    static Transform FindRow(int row)
    {
        foreach (Canvas c in UnityEngine.Object.FindObjectsOfType<Canvas>())
        {
            if (!c.isRootCanvas) continue;
            Transform laf = c.transform.name == "LAF HUD" ? c.transform : c.transform.Find("LAF HUD");
            if (laf == null) continue;
            Transform r = laf.Find("Row_" + row);
            if (r != null) return r;
        }

        GameObject found = GameObject.Find("LAF HUD/Row_" + row);
        return found != null ? found.transform : null;
    }

    /// <summary>Fill the parent, less <paramref name="inset"/> of its width on each side.</summary>
    static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = new Vector2(inset, 0.1f * (inset > 0f ? 1f : 0f));
        rt.anchorMax = new Vector2(1f - inset, 1f - 0.1f * (inset > 0f ? 1f : 0f));
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // -- 4: stop ------------------------------------------------------------------------

    static DarkMatterMoment_NEW BuildMoment(JourneyVoiceOver_NEW voiceOver, DarkMatterScaffold_NEW scaffold, JourneyHint_NEW hint)
    {
        DarkMatterMoment_NEW stop = UnityEngine.Object.FindObjectOfType<DarkMatterMoment_NEW>();

        if (stop == null)
        {
            GameObject go = new GameObject("DarkMatterMoment");
            Undo.RegisterCreatedObjectUndo(go, "Create dark matter moment");
            Transform parent = voiceOver.transform.parent;
            if (parent != null) go.transform.SetParent(parent, false);
            stop = go.AddComponent<DarkMatterMoment_NEW>();
            _changes++;
        }

        SerializedObject so = new SerializedObject(stop);
        Fill(so, "layerState", UnityEngine.Object.FindObjectOfType<LayerState_NEW>());
        Fill(so, "voiceOver", voiceOver);
        Fill(so, "scaffold", scaffold);
        Fill(so, "hint", hint);

        FillLines(so.FindProperty("shownByDefault"), ShownClips);
        FillLines(so.FindProperty("hiddenByDefault"), HiddenClips);

        so.ApplyModifiedProperties();
        return stop;
    }

    static void FillLines(SerializedProperty lines, string[] names)
    {
        string[] slots = { "before", "after", "nudge" };
        for (int i = 0; i < slots.Length; i++)
        {
            SerializedProperty p = lines.FindPropertyRelative(slots[i]);
            if (p == null || p.objectReferenceValue != null) continue;

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(VoiceFolder + "/" + names[i] + ".mp3");
            if (clip == null)
            {
                Debug.Log("[DarkMatterMomentBuilder_NEW] No recording yet at " + VoiceFolder + "/" + names[i] +
                          ".mp3; the moment says it silently by its estimate. Run this again once it is recorded.");
                continue;
            }

            p.objectReferenceValue = clip;
            _changes++;
        }
    }

    static void Fill(SerializedObject so, string field, UnityEngine.Object value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p == null || p.objectReferenceValue != null || value == null) return;

        p.objectReferenceValue = value;
        _changes++;
    }

    // -- 5: begin when J3 is said ---------------------------------------------------------

    static void WireBegin(JourneyVoiceOver_NEW voiceOver, DarkMatterMoment_NEW stop)
    {
        FieldInfo field = typeof(JourneyVoiceOver_NEW).GetField("layers", BindingFlags.Instance | BindingFlags.NonPublic);
        JourneyVoiceOver_NEW.LayerLines[] rows = field != null ? field.GetValue(voiceOver) as JourneyVoiceOver_NEW.LayerLines[] : null;

        JourneyVoiceOver_NEW.LayerLines macro = null;
        if (rows != null)
            foreach (JourneyVoiceOver_NEW.LayerLines r in rows)
                if (r != null && string.Equals(r.layerId, LayerId, StringComparison.OrdinalIgnoreCase)) { macro = r; break; }

        if (macro == null)
        {
            Debug.LogWarning("[DarkMatterMomentBuilder_NEW] The voice-over has no '" + LayerId + "' row, so nothing starts " +
                             "the moment. Run Wire Journey Voice Over, then this again.", voiceOver);
            return;
        }

        for (int i = 0; i < macro.onFinished.GetPersistentEventCount(); i++)
            if (macro.onFinished.GetPersistentTarget(i) == stop && macro.onFinished.GetPersistentMethodName(i) == "Begin")
                return;

        Undo.RecordObject(voiceOver, "Start the dark matter moment after J3");
        UnityEventTools.AddPersistentListener(macro.onFinished, new UnityAction(stop.Begin));
        EditorUtility.SetDirty(voiceOver);
        _changes++;
    }

    // -- Helpers ------------------------------------------------------------------------

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

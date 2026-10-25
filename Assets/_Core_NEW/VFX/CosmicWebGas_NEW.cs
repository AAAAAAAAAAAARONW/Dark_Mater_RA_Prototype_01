using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// The cosmic web's gas as soft glowing puffs (Resources/CosmicWebGas_NEW.shader): thousands
/// of camera-facing blobs over one of the web's thread meshes, overlapping into glowing
/// hydrogen. Coloured the way a density map of the cosmic web is: a dim violet out in the
/// voids, purple and magenta along ordinary threads, a warm peach where the threads crowd
/// into a node — so the web reads as gas, densest where the galaxies are.
///
/// Put it on an empty object under a COsmicWebGameObject, at its origin, unturned and unscaled:
/// the puffs are in the thread mesh's own space (baked from it by
/// CosmicWebGas/Tools~/bake_web_gas.py into a .bytes file), so they sit on the threads of
/// whichever web object holds them, at its size. In Test_Journey_WebGas (a copy of
/// Test_Journey, to try this in) three of these sit under each object of the second cosmic web:
///   threads    over the purple threads (BIG), whose own soft surface still draws their fine
///              lines inside the glow (CosmicWebNebulaFine_NEW, dimmed)
///   middle     in place of the smaller sheets round the yellow clusters (Coreyellow), switched
///              off: as a surface they read as membranes, not gas
///   clusters   in place of the yellow clusters' baked volumes (Core), switched off: their
///              hard-edged blocks become a soft warm glow, and their stray fragments small
///              bright knots along the threads
///
/// CHEAP. One quad a puff, built once on the CPU; each pixel of a puff is one exp and one
/// noise read, additive, no sorting. A hundred thousand puffs are a fraction of the triangles
/// of the meshes they sit on.
///
/// It builds what it draws as a hidden child that is never saved, in the editor too, before
/// WorldSwitcher_NEW gathers renderers, so the gas fades with its world (_Opacity, _Emission);
/// the dark matter toggle (DarkMatterToggleTest_NEW) finds it under the web object and fades
/// it through _Density with the rest of the web. The two web objects share one mesh for the
/// same puffs.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
[HierarchyBadge_NEW("WEB GAS", "#B07AE0")]
public class CosmicWebGas_NEW : MonoBehaviour
{
    [Tooltip("The puffs, baked from a thread mesh (CosmicWebGas/WebGas_*_NEW.bytes): float32 records of x, y, z, size, hue, brightness.")]
    [SerializeField] TextAsset points;

    [Tooltip("How they are drawn (Custom/CosmicWebGas_NEW): their size, colours, brightness.")]
    [SerializeField] Material material;

    const HideFlags Built = HideFlags.HideAndDontSave;
    const int Stride = 6;

    // One mesh for every object drawing the same puffs: both web objects hold the same
    // threads, at different sizes. Counted, and destroyed with the last one using it.
    class Shared { public Mesh mesh; public int users; }
    static readonly Dictionary<TextAsset, Shared> Meshes = new Dictionary<TextAsset, Shared>();

    GameObject _part;
    TextAsset _built;
    Material _drawn;
    bool _rebuild;

    void OnEnable() => Build();

    // Kept while switched off in play: the dark matter toggle switches the whole web object off
    // when it has faded it out, and holds the copy of the material it fades this through; built
    // again, the puffs would come back at full strength with the rest still fading in.
    void OnDisable()
    {
        if (!Application.isPlaying) Clear();
    }

    void OnDestroy() => Clear();

    // Unity calls this on every load too, entering play mode included, not only when a field
    // is changed. Only a real change (another puff file or material) rebuilds: built again,
    // the renderer would be a new one, holding the material asset instead of the copy the dark
    // matter toggle fades and missing from WorldSwitcher_NEW's list, so the gas would pop on
    // and off with the web instead of fading with it.
    void OnValidate()
    {
        _rebuild = true;
#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
    }

    void Update()
    {
        if (!_rebuild) return;
        _rebuild = false;
        if (_part != null && _built == points && _drawn == material) return;
        Clear();
        Build();
    }

    void Build()
    {
        if (_part != null) return;
        if (points == null || material == null) return;

        Mesh mesh = Acquire(points, material.HasProperty("_Size") ? material.GetFloat("_Size") : 0.001f);
        if (mesh == null)
        {
            Debug.LogWarning("[CosmicWebGas_NEW] '" + points.name + "' holds no puffs; nothing drawn.", this);
            return;
        }
        _built = points;
        _drawn = material;

        _part = new GameObject("Web Gas (runtime)") { hideFlags = Built };
        _part.layer = gameObject.layer;
        _part.transform.SetParent(transform, false);
        _part.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = _part.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = LightProbeUsage.Off;
        mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    void Clear()
    {
        Kill(_part);
        _part = null;
        if (_built != null) Release(_built);
        _built = null;
        _drawn = null;
    }

    static Mesh Acquire(TextAsset points, float baseSize)
    {
        if (Meshes.TryGetValue(points, out Shared shared) && shared.mesh != null)
        {
            shared.users++;
            return shared.mesh;
        }

        float[] data = Read(points.bytes);
        int count = data.Length / Stride;
        if (count == 0) return null;
        Mesh mesh = Puffs(data, count, baseSize);
        mesh.hideFlags = Built;
        Meshes[points] = new Shared { mesh = mesh, users = 1 };
        return mesh;
    }

    static void Release(TextAsset points)
    {
        if (!Meshes.TryGetValue(points, out Shared shared)) return;
        if (--shared.users > 0) return;
        Kill(shared.mesh);
        Meshes.Remove(points);
    }

    static void Kill(UnityEngine.Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }

    static float[] Read(byte[] bytes)
    {
        var data = new float[bytes.Length / 4];
        Buffer.BlockCopy(bytes, 0, data, 0, data.Length * 4);
        return data;
    }

    /// <summary>A quad a puff, all four corners at its middle: the vertex shader spreads them to face the camera.</summary>
    static Mesh Puffs(float[] data, int count, float baseSize)
    {
        var vertices = new Vector3[count * 4];
        var corners = new List<Vector2>(count * 4);
        var info = new List<Vector4>(count * 4);
        var triangles = new int[count * 6];
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), max = -min;
        float biggest = 0f;

        for (int i = 0; i < count; i++)
        {
            int k = i * Stride;
            var p = new Vector3(data[k], data[k + 1], data[k + 2]);
            var d = new Vector4(data[k + 3], data[k + 4], data[k + 5], Mathf.Repeat(i * 0.6180339887f, 1f));
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
            biggest = Mathf.Max(biggest, d.x);

            int v = i * 4;
            vertices[v] = vertices[v + 1] = vertices[v + 2] = vertices[v + 3] = p;
            corners.Add(new Vector2(-1f, -1f)); corners.Add(new Vector2(1f, -1f));
            corners.Add(new Vector2(1f, 1f)); corners.Add(new Vector2(-1f, 1f));
            info.Add(d); info.Add(d); info.Add(d); info.Add(d);

            int t = i * 6;
            triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
            triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
        }

        var mesh = new Mesh { name = "Web Gas (runtime)" };
        if (vertices.Length > 65535) mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.SetUVs(0, corners);
        mesh.SetUVs(1, info);
        mesh.triangles = triangles;
        // The quads only open out in the vertex shader: the bounds hold every puff at full size.
        var bounds = new Bounds((min + max) * 0.5f, max - min);
        bounds.Expand(2f * baseSize * biggest);
        mesh.bounds = bounds;
        return mesh;
    }
}

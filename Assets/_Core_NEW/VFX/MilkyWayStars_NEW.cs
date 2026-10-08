using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Stable, locally anchored stars following the same barred spiral as MilkyWayVolume_NEW.
/// One mesh built on enable, no per-frame particle simulation or CPU billboard updates.
/// Attach to a SERIALIZED MeshRenderer child so WorldSwitcher caches it even before Awake.
/// </summary>
[ExecuteAlways, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
[DefaultExecutionOrder(-300)]
public sealed class MilkyWayStars_NEW : MonoBehaviour
{
    [Range(1000, 14000)] public int count = 12000;
    public int seed = 7319;
    [Range(1.5f, 5f)] public float pitch = 2.3f;
    public float phase = 0.45f;
    [Range(2, 6)] public int armCount = 4;
    Mesh generated;
    int[] allIndices;
    int visibleBudget = int.MaxValue;

    // Only the index range changes at a quality switch; vertices stay resident.
    public void SetVisibleStarCount(int budget)
    {
        visibleBudget = Mathf.Clamp(budget,0,count);
        if (generated != null && allIndices != null)
            generated.SetTriangles(allIndices,0,visibleBudget*6,0,false);
    }

    void OnEnable() { Rebuild(); }
    void OnDisable() { Release(); }
    void OnValidate()
    {
        count = Mathf.Clamp(count, 1000, 14000);
        // OnValidate can run on Unity's loading thread; mesh work belongs to Update.
        dirty = true;
    }
    bool dirty;
    void Update() { if (dirty) { dirty = false; Rebuild(); } }

    [ContextMenu("Rebuild spiral stars")]
    public void Rebuild()
    {
        count = Mathf.Clamp(count,1000,14000);
        dirty = false;
        Release();
        // The volume is the source of truth for the stellar arms too.
        var volume = transform.parent != null ? transform.parent.GetComponent<Renderer>() : null;
        var material = volume != null ? volume.sharedMaterial : null;
        if (material != null)
        {
            if (material.HasProperty("_Pitch")) pitch = material.GetFloat("_Pitch");
            if (material.HasProperty("_Phase")) phase = material.GetFloat("_Phase");
            if (material.HasProperty("_ArmCount")) armCount = Mathf.RoundToInt(material.GetFloat("_ArmCount"));
        }
        armCount = Mathf.Clamp(armCount,2,6);
        var random = new System.Random(seed);
        var positions = new Vector3[count * 4];
        var colors = new Color[count * 4];
        var corners = new Vector2[count * 4];
        var sizes = new Vector2[count * 4];
        var indices = new int[count * 6];
        Vector2[] quad = { new Vector2(-1,-1), new Vector2(-1,1), new Vector2(1,1), new Vector2(1,-1) };
        for (int i = 0; i < count; i++)
        {
            float r, angle, height;
            // Every prefix contains both arm and core stars for the reduced quality tiers.
            bool arm = Next(random) < 0.82f;
            if (arm)
            {
                r = Mathf.Lerp(0.22f, 0.94f, Mathf.Pow(Next(random), 0.8f));
                angle = pitch * Mathf.Log(r / 0.22f) + (i % armCount) * (2*Mathf.PI/armCount);
                angle += Gaussian(random) * (0.021f + 0.035f * r) / r;
                // A minority of stars bridge the arms, rather than a uniformly filled disc.
                if (i % 9 == 0) angle += 0.7f;
                height = Gaussian(random) * 0.0035f;
            }
            else
            {
                r = Mathf.Sqrt(Next(random)) * 0.22f;
                angle = Next(random) * Mathf.PI * 2;
                height = Gaussian(random) * 0.018f;
            }
            Vector3 p = new Vector3(Mathf.Cos(angle) * r / 2.1f, height, Mathf.Sin(angle) * r / 2.1f);
            if (!arm) p.z *= 0.35f;
            p = Quaternion.AngleAxis(-phase * Mathf.Rad2Deg, Vector3.up) * p;
            Color color = arm ? Color.Lerp(new Color(.5f,.7f,1f), new Color(1f,.96f,.84f),Next(random))
                              : Color.Lerp(new Color(1f,.62f,.31f), new Color(1f,.9f,.7f),Next(random));
            if (arm && i % 47 == 0) color = new Color(1f,.28f,.45f);
            float size = Mathf.Lerp(0.00065f, 0.0017f, Mathf.Pow(Next(random), 3));
            if (i % 173 == 0) size *= 2.0f;
            color.a = Mathf.Lerp(0.28f, 0.85f, Next(random));
            for (int j = 0; j < 4; j++)
            {
                int k = i * 4 + j;
                positions[k] = p; colors[k] = color; corners[k] = quad[j]; sizes[k] = new Vector2(size,0);
            }
            int v = i * 4, t = i * 6;
            indices[t] = v; indices[t+1] = v+1; indices[t+2] = v+2;
            indices[t+3] = v; indices[t+4] = v+2; indices[t+5] = v+3;
        }
        generated = new Mesh { name = "Milky Way spiral stars (generated)", hideFlags = HideFlags.HideAndDontSave };
        generated.vertices = positions;
        generated.colors = colors;
        generated.uv = corners;
        generated.uv2 = sizes;
        allIndices = indices;
        generated.SetTriangles(indices,0,Mathf.Min(visibleBudget,count)*6,0,false);
        generated.bounds = new Bounds(Vector3.zero, new Vector3(1.1f,0.4f,1.1f));
        GetComponent<MeshFilter>().sharedMesh = generated;
        var renderer = GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    static float Next(System.Random r) { return (float)r.NextDouble(); }
    static float Gaussian(System.Random r)
    {
        return Mathf.Sqrt(-2 * Mathf.Log(Mathf.Max(0.0001f, Next(r)))) * Mathf.Cos(2 * Mathf.PI * Next(r));
    }
    void Release()
    {
        if (generated == null) return;
        var filter = GetComponent<MeshFilter>();
        if (filter != null && filter.sharedMesh == generated) filter.sharedMesh = null;
        if (Application.isPlaying) Destroy(generated); else DestroyImmediate(generated);
        generated = null;
        allIndices = null;
    }
}

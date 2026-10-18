using UnityEngine;

/// <summary>
/// A cloud of cold neutral hydrogen that absorbs: dim, diffuse magenta gas the light passes
/// through, not a glowing object. The look the team proposal settled on (Hydrogen &amp; Cosmic
/// Web Visuals, option B): the Orion Nebula's colour, none of its glow.
///
/// BESIDE NebulaCloudVFX_NEW, NOT INSTEAD OF IT. That one is the glowing star-forming nebula
/// (and the tutorial's cold-blue cloud in Tutorial.unity); this one is tried in
/// Test_Tutorial.unity, on the same Cloud children, so the two can be compared without
/// either scene or either script changing the other.
///
/// WHAT IT DRAWS. Resources/HydrogenAbsorber_NEW.shader on a sphere of this object's scale
/// in radius, built as a hidden child that is never saved, drawn from its back faces so it
/// works from inside. The gas adds no light: it darkens and tints what is behind it, so it
/// reads as a silhouette on the sky and the quasar. Without a glow it is hard to see at 200
/// metres; if so, raise opacity or the object's scale before adding light back.
///
/// CHEAP. Four samples along the ray, two noise reads each (both sheets of
/// QuasarNoise_NEW in one fetch): sixteen texture reads a pixel. Built before
/// WorldSwitcher_NEW gathers renderers, so it fades with its world through _Color.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
[HierarchyBadge_NEW("H I", "#B0457E")]
public class HydrogenAbsorberVFX_NEW : MonoBehaviour
{
    [Header("Gas")]
    [Tooltip("Which cloud: the shape of its lumps, and where its wisps and lanes fall.")]
    [SerializeField] int seed = 0;

    [Tooltip("How much it hides what is behind it. Low: a faint tint; high: a dark cloud.")]
    [Range(0f, 8f)] [SerializeField] float opacity = 2.5f;

    [Tooltip("How far the wisps reach out past the body's lumps.")]
    [Range(0.1f, 1f)] [SerializeField] float softness = 0.55f;

    [Tooltip("Darker lanes of dust through the gas.")]
    [Range(0f, 2f)] [SerializeField] float dustAmount = 0.6f;

    [Tooltip("How fast the gas drifts, radii a second.")]
    [SerializeField] float drift = 0.004f;

    [Tooltip("Nearer the camera than x (of the radius) the gas is left out, from y it is all there: " +
             "from inside, a tint rather than a wall.")]
    [SerializeField] Vector2 resolve = new Vector2(0.05f, 0.3f);

    [Header("Colours")]
    [Tooltip("The gas: a dim magenta. Keep every channel under 1 so it never blooms.")]
    [SerializeField] Color gasColour = new Color(0.36f, 0.12f, 0.28f, 1f);
    [SerializeField] Color dustColour = new Color(0.05f, 0.015f, 0.03f, 1f);

    [Tooltip("After the cosmic web's volumes (2950 to 2954) and the galaxies (2989 to 2993), before the light's own glow (3005).")]
    [SerializeField] int renderQueue = 2995;

    const HideFlags Built = HideFlags.HideAndDontSave;

    GameObject _part;
    Material _material;
    Mesh _mesh;
    bool _dirty;

    /// <summary>The cloud's bound, in world units: the radius the gas never reaches past.</summary>
    public float Radius => transform.lossyScale.x;

    void OnEnable() => Build();

    void OnDisable() => Clear();

    void OnValidate()
    {
        _dirty = true;
#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
    }

    void Update()
    {
        if (_dirty && _material != null) Apply();
        _dirty = false;
    }

    void Build()
    {
        if (_material != null) return;

        Shader shader = Resources.Load<Shader>("HydrogenAbsorber_NEW");
        if (shader == null)
        {
            Debug.LogWarning("[HydrogenAbsorberVFX_NEW] Resources/HydrogenAbsorber_NEW.shader is missing; nothing drawn.", this);
            return;
        }

        _mesh = Sphere();
        _mesh.hideFlags = Built;

        _part = new GameObject("Hydrogen absorber") { hideFlags = Built };
        _part.layer = gameObject.layer;
        _part.transform.SetParent(transform, false);
        _part.AddComponent<MeshFilter>().sharedMesh = _mesh;
        MeshRenderer mr = _part.AddComponent<MeshRenderer>();
        _material = new Material(shader) { name = "Hydrogen absorber (runtime)", hideFlags = Built, renderQueue = renderQueue };
        mr.sharedMaterial = _material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        Apply();
    }

    void Clear()
    {
        Kill(_part);
        Kill(_material);
        Kill(_mesh);
        _part = null;
        _material = null;
        _mesh = null;
    }

    static void Kill(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }

    /// <summary>Every setting onto the material. _Color is left alone: it is the world's fade.</summary>
    void Apply()
    {
        _material.renderQueue = renderQueue;
        _material.SetTexture("_Noise", QuasarNoise_NEW.Texture);
        _material.SetFloat("_Seed", seed);
        _material.SetFloat("_Opacity", opacity);
        _material.SetFloat("_Softness", softness);
        _material.SetFloat("_DustAmount", dustAmount);
        _material.SetFloat("_Flow", drift);
        _material.SetVector("_Resolve", resolve);
        _material.SetColor("_GasColor", gasColour);
        _material.SetColor("_DustColor", dustColour);
    }

    /// <summary>A sphere of radius 1, outward-facing; the shader draws its back faces.</summary>
    static Mesh Sphere()
    {
        const int around = 32, across = 16;
        int row = around + 1;
        var vertices = new Vector3[row * (across + 1)];
        for (int j = 0; j <= across; j++)
        {
            float lat = -Mathf.PI * 0.5f + Mathf.PI * j / across;
            for (int i = 0; i <= around; i++)
            {
                float lon = Mathf.PI * 2f * i / around;
                vertices[j * row + i] = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
            }
        }
        var triangles = new int[around * across * 6];
        int t = 0;
        for (int j = 0; j < across; j++)
        for (int i = 0; i < around; i++)
        {
            int a = j * row + i, b = a + 1, c = a + row, d = c + 1;
            triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
            triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
        }
        var mesh = new Mesh { name = "Hydrogen absorber sphere", vertices = vertices, triangles = triangles };
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
        return mesh;
    }
}

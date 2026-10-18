using UnityEngine;

/// <summary>
/// A cloud of hydrogen gas, as the scientists asked for one: not a ball or a single atom but
/// a translucent, reddish-purple nebula like the Orion Nebula — gas glowing red and pink
/// where the stars born inside it light it, violet and blue further out, torn by dark lanes
/// of dust, a few bright stars in its hollow heart; and see-through, so what is behind it
/// shows through.
///
/// Where it is used:
///   the journey   Tools > Journey NEW > Add Nebulae To Second Cosmic Web scatters a few in
///                 the second cosmic web, among the threads of gas, two of them on the light's
///                 path: the hydrogen clouds that cut the lines of the Lyman-alpha forest, seen
///                 (J6: "thousands of hydrogen clouds have cut lines into your light").
///   the tutorial  TutorialAtom_NEW's body (D4 to D8): the gas cloud that closes on the light
///                 and absorbs a wavelength of it, where there used to be a glowing atom.
///
/// Put it on an empty object: the cloud is a sphere of this object's scale in radius. It
/// builds what it draws — one sphere, drawn from its back faces so it works from inside — as
/// a hidden child that is never saved, in the editor too, and fades with its world through
/// _Color, built before WorldSwitcher_NEW gathers renderers.
///
/// CHEAP. One ray a pixel, a dozen samples, each a few reads of the shared noise texture
/// (Resources/NebulaCloud_NEW.shader). Near the camera the gas is left out, so from inside it
/// is a glow all round and a tint on what is beyond, not a wall of colour.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
[HierarchyBadge_NEW("NEBULA", "#E07AA8")]
public class NebulaCloudVFX_NEW : MonoBehaviour
{
    [Header("Gas")]
    [Tooltip("Which cloud: where its wisps, its lanes and its stars fall.")]
    [SerializeField] int seed = 0;

    [Range(0f, 2f)] [SerializeField] float density = 0.85f;

    [Tooltip("How soft the gas's edges are: how far inside the sphere it starts to thin.")]
    [Range(0.1f, 1f)] [SerializeField] float softness = 0.5f;

    [Tooltip("How hollow the middle is, round the stars that have blown the gas clear.")]
    [Range(0f, 1f)] [SerializeField] float hollow = 0.6f;

    [Tooltip("Dark lanes of dust through the gas.")]
    [Range(0f, 2f)] [SerializeField] float dustAmount = 0.9f;

    [Range(0f, 4f)] [SerializeField] float brightness = 1f;

    [Tooltip("How much the gas hides what is behind it. Low: a tint; high: a cloud.")]
    [Range(0f, 4f)] [SerializeField] float absorb = 0.8f;

    [Tooltip("How fast the gas drifts, radii a second.")]
    [SerializeField] float drift = 0.004f;

    [Tooltip("Nearer the camera than x (of the radius) the gas is left out, from y it is all there: " +
             "from inside, a glow all round rather than a wall.")]
    [SerializeField] Vector2 resolve = new Vector2(0.05f, 0.3f);

    [Header("Stars inside")]
    [Range(0, 4)] [SerializeField] int stars = 3;
    [Range(0f, 3f)] [SerializeField] float starBrightness = 1f;

    [Header("Colours")]
    [Tooltip("The gas where the stars light it: the red of hydrogen.")]
    [ColorUsage(false, true)] [SerializeField] Color lit = new Color(1.5f, 0.4f, 0.45f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color mid = new Color(0.6f, 0.3f, 0.85f, 1f);
    [Tooltip("The faint outer gas.")]
    [ColorUsage(false, true)] [SerializeField] Color rim = new Color(0.25f, 0.3f, 0.8f, 1f);
    [SerializeField] Color dustColour = new Color(0.06f, 0.025f, 0.03f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color starColour = new Color(3f, 3.2f, 4f, 1f);

    [Tooltip("After the cosmic web's volumes (2950 to 2954) and the galaxies (2989 to 2993), before the light's own glow (3005).")]
    [SerializeField] int renderQueue = 2995;

    const HideFlags Built = HideFlags.HideAndDontSave;

    GameObject _part;
    Material _material;
    Mesh _mesh;
    bool _dirty;

    /// <summary>The cloud's radius, in world units.</summary>
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

        Shader shader = Resources.Load<Shader>("NebulaCloud_NEW");
        if (shader == null)
        {
            Debug.LogWarning("[NebulaCloudVFX_NEW] Resources/NebulaCloud_NEW.shader is missing; nothing drawn.", this);
            return;
        }

        _mesh = Sphere();
        _mesh.hideFlags = Built;

        _part = new GameObject("Nebula cloud") { hideFlags = Built };
        _part.layer = gameObject.layer;
        _part.transform.SetParent(transform, false);
        _part.AddComponent<MeshFilter>().sharedMesh = _mesh;
        MeshRenderer mr = _part.AddComponent<MeshRenderer>();
        _material = new Material(shader) { name = "Nebula cloud (runtime)", hideFlags = Built, renderQueue = renderQueue };
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
        _material.SetFloat("_Density", density);
        _material.SetFloat("_Softness", softness);
        _material.SetFloat("_Hollow", hollow);
        _material.SetFloat("_DustAmount", dustAmount);
        _material.SetFloat("_Brightness", brightness);
        _material.SetFloat("_Absorb", absorb);
        _material.SetFloat("_Flow", drift);
        _material.SetVector("_Resolve", resolve);
        _material.SetVector("_Stars", new Vector4(stars, starBrightness, 0f, 0f));
        _material.SetColor("_Core", lit);
        _material.SetColor("_Mid", mid);
        _material.SetColor("_Rim", rim);
        _material.SetColor("_DustColor", dustColour);
        _material.SetColor("_StarColor", starColour);
    }

    /// <summary>A sphere of radius 1, outward-facing; the shader draws its back faces.</summary>
    static Mesh Sphere()
    {
        const int around = 48, across = 24;
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
        var mesh = new Mesh { name = "Nebula cloud sphere", vertices = vertices, triangles = triangles };
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
        return mesh;
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A planet or a moon drawn as it looks in sunlight: lit from where the Sun actually is, with
/// relief that stands up along the terminator, and — on the worlds that have them — air,
/// clouds, oceans and city lights.
///
/// Put it on the planet's own object (the one with the sphere and its day map). It draws the
/// planet itself, from the same mesh and the same map, so latitudes and longitudes are where
/// they were, and stops the original renderer drawing (forceRenderingOff, which
/// WorldSwitcher_NEW never touches — it only switches renderers on and off). Disable it and
/// the original is back.
///
/// WHAT EACH KIND GETS (Tools > Journey NEW > Polish Solar System sets these by name):
///   Rocky     Mercury, the Moon: strong relief, the Lommel-Seeliger lighting of dust
///   Venus     its cloud-top map under a thick, pale yellow air
///   Earth     oceans with the Sun's glint, relief on land, city lights at night, drifting
///             clouds casting shadows, and a thin blue air reddening at the terminator
///   Mars      relief, and a thin dusty air — pinkish by day, blue at sunset, as it is there
///   Gas giant Jupiter, Saturn: limb darkening, bands flowing at the equator, a faint haze
///   Ice giant Uranus, Neptune: limb darkening, a cyan or blue haze
///
/// Every shader takes _Color, which WorldSwitcher_NEW fades a world by, and the parts are
/// built before WorldSwitcher_NEW gathers its renderers. Nothing is saved but the settings.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
[HierarchyBadge_NEW("PLANET", "#6FB4E8")]
public class CelestialBody_NEW : MonoBehaviour
{
    public enum Kind { Rocky, Venus, Earth, Mars, GasGiant, IceGiant }

    [Header("What it is")]
    [SerializeField] Kind kind = Kind.Rocky;

    [Tooltip("The Sun. Found by name ('Sun') among this object's parents' children if empty.")]
    [SerializeField] Transform sun;

    [Tooltip("The day map. Taken from the original material if empty.")]
    [SerializeField] Texture dayMap;

    [Header("Surface")]
    [Range(0.3f, 3f)] [SerializeField] float exposure = 1.25f;
    [Tooltip("Relief from the map's own brightness, standing up along the terminator.")]
    [Range(0f, 6f)] [SerializeField] float relief = 2f;
    [Tooltip("0 Lambert; 1 Lommel-Seeliger, the lighting of dusty regolith.")]
    [Range(0f, 1f)] [SerializeField] float regolith = 0.8f;
    [SerializeField] Color nightSide = new Color(0.004f, 0.005f, 0.009f, 1f);
    [Range(0f, 1f)] [SerializeField] float terminatorReddening = 0f;
    [SerializeField] Color terminatorColour = new Color(1f, 0.45f, 0.2f, 1f);
    [Tooltip("The air seen over the ground towards the limb.")]
    [Range(0f, 2f)] [SerializeField] float hazeOverGround = 0f;
    [ColorUsage(false, true)] [SerializeField] Color hazeColour = new Color(0.25f, 0.5f, 1f, 1f);
    [Tooltip("Oceans, picked out of the map by colour, with the Sun's glint on them.")]
    [Range(0f, 1f)] [SerializeField] float oceans = 0f;
    [Range(0f, 3f)] [SerializeField] float cityLights = 0f;
    [Range(0f, 1f)] [SerializeField] float limbDarkening = 0f;
    [Tooltip("Gas giants: how fast the bands flow at the equator, map widths a second.")]
    [SerializeField] float bandFlow = 0f;

    [Header("Clouds")]
    [SerializeField] bool clouds = false;
    [Range(0f, 1f)] [SerializeField] float cloudCover = 0.55f;
    [Tooltip("Radians a second.")]
    [SerializeField] float cloudDrift = 0.01f;
    [Range(0f, 1f)] [SerializeField] float cloudShadows = 0.5f;
    [Range(0f, 3f)] [SerializeField] float cloudBrightness = 1.1f;

    [Header("Air")]
    [SerializeField] bool atmosphere = false;
    [Tooltip("How deep the air is, as a fraction of the radius. Earth's visible air is very thin.")]
    [Range(0.005f, 0.2f)] [SerializeField] float airDepth = 0.025f;
    [Tooltip("Scale height, as a fraction of the air's depth.")]
    [Range(0.05f, 1f)] [SerializeField] float scaleHeight = 0.25f;
    [ColorUsage(false, true)] [SerializeField] Color scattering = new Color(0.3f, 0.6f, 1.5f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color sunset = new Color(1.1f, 0.45f, 0.18f, 1f);
    [Range(0f, 4f)] [SerializeField] float airStrength = 0.7f;

    const HideFlags Built = HideFlags.HideAndDontSave;

    readonly List<GameObject> _parts = new List<GameObject>();
    readonly List<Material> _materials = new List<Material>();
    Material _surface, _air;
    Renderer _original;
    float _meshRadius = 0.5f;
    bool _rebuild, _dirty;

    static readonly int SunId = Shader.PropertyToID("_SunPosition");
    static readonly int CentreId = Shader.PropertyToID("_Centre");
    static readonly int RadiiId = Shader.PropertyToID("_Radii");

    public Kind BodyKind => kind;

    /// <summary>Sets every look to the kind's own, for Tools > Journey NEW > Polish Solar System.</summary>
    public void ApplyPreset(Kind k)
    {
        kind = k;
        clouds = false;
        atmosphere = false;
        oceans = cityLights = hazeOverGround = terminatorReddening = limbDarkening = bandFlow = 0f;
        exposure = 1.25f;
        nightSide = new Color(0.004f, 0.005f, 0.009f, 1f);

        switch (k)
        {
            case Kind.Rocky:
                relief = 3f; regolith = 0.85f; exposure = 1.35f;
                break;
            case Kind.Venus:
                relief = 0.3f; regolith = 0f; exposure = 1.1f;
                hazeOverGround = 0.9f; hazeColour = new Color(0.55f, 0.48f, 0.3f, 1f);
                atmosphere = true; airDepth = 0.06f; scaleHeight = 0.35f; airStrength = 0.9f;
                scattering = new Color(1.2f, 1.05f, 0.65f, 1f); sunset = new Color(1.2f, 0.6f, 0.25f, 1f);
                break;
            case Kind.Earth:
                relief = 1.2f; regolith = 0f; exposure = 1.3f;
                oceans = 1f; cityLights = 1.2f;
                hazeOverGround = 0.55f; hazeColour = new Color(0.25f, 0.5f, 1f, 1f);
                terminatorReddening = 0.7f; terminatorColour = new Color(1f, 0.45f, 0.2f, 1f);
                clouds = true; cloudCover = 0.55f; cloudDrift = 0.01f; cloudShadows = 0.5f; cloudBrightness = 1.1f;
                atmosphere = true; airDepth = 0.025f; scaleHeight = 0.25f; airStrength = 0.7f;
                scattering = new Color(0.3f, 0.6f, 1.5f, 1f); sunset = new Color(1.1f, 0.45f, 0.18f, 1f);
                break;
            case Kind.Mars:
                relief = 2.5f; regolith = 0.5f; exposure = 1.3f;
                hazeOverGround = 0.25f; hazeColour = new Color(0.6f, 0.4f, 0.3f, 1f);
                atmosphere = true; airDepth = 0.02f; scaleHeight = 0.3f; airStrength = 0.35f;
                scattering = new Color(1.1f, 0.7f, 0.5f, 1f); sunset = new Color(0.45f, 0.65f, 1.2f, 1f);
                break;
            case Kind.GasGiant:
                relief = 0f; regolith = 0f; exposure = 1.25f;
                limbDarkening = 0.6f; bandFlow = 0.0015f;
                atmosphere = true; airDepth = 0.02f; scaleHeight = 0.4f; airStrength = 0.25f;
                scattering = new Color(1f, 0.85f, 0.65f, 1f); sunset = new Color(1f, 0.6f, 0.35f, 1f);
                break;
            case Kind.IceGiant:
                relief = 0f; regolith = 0f; exposure = 1.25f;
                limbDarkening = 0.5f; bandFlow = 0.0006f;
                atmosphere = true; airDepth = 0.03f; scaleHeight = 0.4f; airStrength = 0.5f;
                scattering = new Color(0.45f, 0.85f, 1.3f, 1f); sunset = new Color(0.6f, 0.8f, 1f, 1f);
                break;
        }

        _rebuild = true;
        _dirty = true;
#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
    }

    void OnEnable() => Build();

    void OnDisable() => Clear();

    void OnValidate()
    {
        _rebuild |= _surface != null && atmosphere != (_air != null);
        _dirty = true;
#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
    }

    void Update()
    {
        if (_rebuild)
        {
            _rebuild = false;
            Clear();
            Build();
        }
        else if (_dirty && _surface != null)
        {
            Apply();
        }
        _dirty = false;
    }

    // Every frame: the Sun moves relative to the planet while a dive turns and scales the world.
    void LateUpdate() => Track();

    void Track()
    {
        if (_surface == null) return;
        if (sun == null) sun = FindSun();

        Vector3 sunPosition = sun != null ? sun.position : transform.position + Vector3.right * 1000f;
        float ground = _meshRadius * transform.lossyScale.x;

        foreach (Material m in _materials) m.SetVector(SunId, sunPosition);
        if (_air != null)
        {
            _air.SetVector(CentreId, transform.position);
            _air.SetVector(RadiiId, new Vector4(ground, ground * (1f + airDepth), 0f, 0f));
        }
    }

    Transform FindSun()
    {
        for (Transform p = transform.parent; p != null; p = p.parent)
        {
            Transform found = p.Find("Sun");
            if (found != null) return found;
        }
        return null;
    }

    // ── Building ────────────────────────────────────────────────────────────

    static Mesh _fallback;

    /// <summary>A smooth sphere of radius 0.5 with equirectangular UVs, for a planet that has no mesh of its own yet.</summary>
    static Mesh FallbackSphere()
    {
        if (_fallback != null) return _fallback;
        const int around = 128, down = 64;
        int stride = around + 1;
        var vertices = new Vector3[stride * (down + 1)];
        var normals = new Vector3[vertices.Length];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[around * down * 6];
        for (int y = 0; y <= down; y++)
        for (int x = 0; x <= around; x++)
        {
            float u = x / (float)around, v = y / (float)down;
            float theta = (1f - v) * Mathf.PI, phi = u * Mathf.PI * 2f;
            var n = new Vector3(-Mathf.Sin(phi) * Mathf.Sin(theta), Mathf.Cos(theta), -Mathf.Cos(phi) * Mathf.Sin(theta));
            int i = y * stride + x;
            normals[i] = n; vertices[i] = n * 0.5f; uv[i] = new Vector2(u, v);
        }
        int t = 0;
        for (int y = 0; y < down; y++)
        for (int x = 0; x < around; x++)
        {
            int i = y * stride + x;
            triangles[t++] = i; triangles[t++] = i + 1; triangles[t++] = i + stride;
            triangles[t++] = i + 1; triangles[t++] = i + stride + 1; triangles[t++] = i + stride;
        }
        _fallback = new Mesh { name = "Celestial sphere (runtime)", hideFlags = Built, vertices = vertices, normals = normals, uv = uv, triangles = triangles };
        _fallback.bounds = new Bounds(Vector3.zero, Vector3.one);
        return _fallback;
    }

    void Build()
    {
        if (_surface != null) return;

        MeshFilter filter = GetComponent<MeshFilter>();
        _original = GetComponent<Renderer>();
        Mesh mesh = filter != null && filter.sharedMesh != null ? filter.sharedMesh : FallbackSphere();
        if (_original == null)
        {
            Debug.LogWarning("[CelestialBody_NEW] Needs to sit on the planet's own sphere (a MeshFilter and renderer).", this);
            return;
        }

        Shader surfaceShader = Resources.Load<Shader>("CelestialSurface_NEW");
        Shader airShader = Resources.Load<Shader>("CelestialAtmosphere_NEW");
        if (surfaceShader == null || airShader == null)
        {
            Debug.LogWarning("[CelestialBody_NEW] Its shaders (Resources/Celestial*_NEW.shader) are missing; leaving the original.", this);
            return;
        }

        if (dayMap == null && _original.sharedMaterial != null && _original.sharedMaterial.HasProperty("_MainTex"))
            dayMap = _original.sharedMaterial.mainTexture;

        _meshRadius = Mathf.Max(mesh.bounds.extents.x, 1e-4f);

        _surface = Part("Surface", mesh, surfaceShader, 1f, 2000);
        if (atmosphere) _air = Part("Air", mesh, airShader, 1f + airDepth * 1.05f, 3000);

        _original.forceRenderingOff = true;

        Apply();
        Track();
    }

    Material Part(string name, Mesh mesh, Shader shader, float scale, int queue)
    {
        var go = new GameObject(gameObject.name + " " + name) { hideFlags = Built };
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * scale;

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        var material = new Material(shader) { name = gameObject.name + " " + name + " (runtime)", hideFlags = Built, renderQueue = queue };
        mr.sharedMaterial = material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        _parts.Add(go);
        _materials.Add(material);
        return material;
    }

    void Clear()
    {
        if (_original != null) _original.forceRenderingOff = false;
        foreach (GameObject go in _parts) Kill(go);
        foreach (Material m in _materials) Kill(m);
        _parts.Clear();
        _materials.Clear();
        _surface = _air = null;
    }

    static void Kill(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }

    void Apply()
    {
        Texture2D noise = QuasarNoise_NEW.Texture;

        _surface.SetTexture("_MainTex", dayMap);
        _surface.SetTexture("_Noise", noise);
        _surface.SetFloat("_Exposure", exposure);
        _surface.SetFloat("_Bump", relief);
        _surface.SetFloat("_Regolith", regolith);
        _surface.SetColor("_Night", nightSide);
        _surface.SetColor("_Terminator", terminatorColour);
        _surface.SetFloat("_TerminatorAmount", terminatorReddening);
        _surface.SetColor("_Haze", hazeColour);
        _surface.SetFloat("_HazeAmount", hazeOverGround);
        _surface.SetFloat("_Ocean", oceans);
        _surface.SetFloat("_CityAmount", cityLights);
        _surface.SetFloat("_CloudShadow", clouds ? cloudShadows : 0f);
        _surface.SetFloat("_CloudCover", cloudCover);
        _surface.SetFloat("_CloudSpin", cloudDrift);
        _surface.SetFloat("_LimbDarkening", limbDarkening);
        _surface.SetFloat("_BandFlow", bandFlow);
        _surface.SetFloat("_CloudAmount", clouds ? 1f : 0f);
        _surface.SetFloat("_CloudBrightness", cloudBrightness);
        _surface.SetColor("_CloudTerminator", terminatorColour);

        if (_air != null)
        {
            _air.SetFloat("_ScaleHeight", scaleHeight);
            _air.SetColor("_Scatter", scattering);
            _air.SetColor("_Sunset", sunset);
            _air.SetFloat("_Strength", airStrength);
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Lights the Solar System and gives it what makes such a scene read in the pictures: a
/// corona round the Sun, an atmosphere round each planet that has one, sunlight from the Sun,
/// and night sides that are deep blue rather than black.
///
/// WHY. As built, every planet is lit by a small point light of its own placed beside it and
/// by the scene's one directional light, and nothing else: the ambient comes from the nebula
/// sky, which is near black, so whatever those lights miss is black; the Sun is a lit ball with
/// a rim and no glow round it; and a planet is a few pixels against black. The whole system
/// read dark and flat.
///
///   sunlight     A point light at the Sun reaching past the farthest planet, so every planet
///                has a day side towards the Sun and a night side away — seen from the photon,
///                which mostly looks towards the Sun, a lit limb round each planet. The
///                planets' own lights stay, as fill.
///   fill         While the Solar System is in view, a cool ambient light, so night sides are
///                a deep blue and their shapes still read. The scene's own ambient is put back
///                as it goes.
///   atmospheres  A thin glowing shell round each planet named in the list: Earth blue, Venus
///                gold, the ice giants cyan and blue, the gas giants a faint warm haze. Bright
///                on the day side and where the Sun is behind the planet, faint round the night.
///   corona       The Sun's corona and halo, in slow streamers (Resources/SunCorona_NEW).
///
/// Everything it makes is a child of the world, made in Awake before WorldSwitcher_NEW gathers
/// its groups (DefaultExecutionOrder), so it fades with the Solar System, is hidden with it,
/// and grows with it in the dive from the Milky Way (LayerDive_NEW scales lights' ranges too).
/// Nothing is saved: it is all made again each run, and the planets, their materials and their
/// lights are left as they are.
///
/// Put it on the Solar System's group root.
/// </summary>
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
[HierarchyBadge_NEW("SOLAR LOOK", "#F2C14E")]
public class SolarSystemLook_NEW : MonoBehaviour
{
    [Serializable]
    public class Atmosphere
    {
        [Tooltip("The planet: a child of the Solar System with this name.")]
        public string planet = "Earth";

        [ColorUsage(false, true)] public Color colour = new Color(0.35f, 0.6f, 1f, 1f);

        [Range(0f, 4f)] public float strength = 1f;
    }

    [Header("Which world")]
    [Tooltip("The layer whose render group this is: the fill is there as much as the group shows.")]
    [SerializeField] string layerId = "SolarSystem";

    [Tooltip("Optional. The Sun; if empty, the child named 'Sun'.")]
    [SerializeField] Transform sun;

    [Header("Sunlight")]
    [Tooltip("A light at the Sun that lights every planet from the Sun's side.")]
    [SerializeField] bool sunlight = true;

    [SerializeField] Color sunlightColour = new Color(1f, 0.93f, 0.82f, 1f);

    [Range(0f, 8f)] [SerializeField] float sunlightIntensity = 2f;

    [Tooltip("How far it reaches, as a multiple of the farthest planet's distance from the Sun. " +
             "A point light fades with distance; further reach lights the outer planets more evenly.")]
    [Range(1f, 6f)] [SerializeField] float sunlightReach = 2.5f;

    [Header("Fill")]
    [Tooltip("Ambient light while the Solar System is in view, so night sides are not black. " +
             "Black = off.")]
    [SerializeField] Color fill = new Color(0.1f, 0.11f, 0.17f, 1f);

    [Header("Atmospheres")]
    [Tooltip("How far out an atmosphere reaches, as a fraction of its planet's radius.")]
    [Range(0.01f, 0.3f)] [SerializeField] float atmosphereHeight = 0.08f;

    [SerializeField] Atmosphere[] atmospheres =
    {
        new Atmosphere { planet = "Earth", colour = new Color(0.35f, 0.62f, 1.25f, 1f), strength = 1.3f },
        new Atmosphere { planet = "Venus", colour = new Color(1.1f, 0.85f, 0.5f, 1f), strength = 1f },
        new Atmosphere { planet = "Mars", colour = new Color(1f, 0.55f, 0.35f, 1f), strength = 0.5f },
        new Atmosphere { planet = "Jupiter", colour = new Color(1f, 0.82f, 0.6f, 1f), strength = 0.45f },
        new Atmosphere { planet = "Saturn", colour = new Color(1f, 0.88f, 0.62f, 1f), strength = 0.45f },
        new Atmosphere { planet = "Uranus", colour = new Color(0.55f, 0.95f, 1.1f, 1f), strength = 0.9f },
        new Atmosphere { planet = "Neptune", colour = new Color(0.35f, 0.55f, 1.2f, 1f), strength = 1f }
    };

    [Header("Corona")]
    [SerializeField] bool corona = true;

    [ColorUsage(false, true)] [SerializeField] Color coronaColour = new Color(1.4f, 0.85f, 0.4f, 1f);

    [Tooltip("How far it reaches: scales the corona's and the halo's falloff.")]
    [Range(0.25f, 2f)] [SerializeField] float coronaSize = 1f;

    [Range(0f, 1f)] [SerializeField] float streamers = 0.5f;

    [Header("Wiring (found if empty)")]
    [SerializeField] WorldSwitcher_NEW worlds;

    static readonly int TintId = Shader.PropertyToID("_Tint");
    static readonly int SunPosId = Shader.PropertyToID("_SunPos");

    readonly List<Material> _atmosphereMaterials = new List<Material>();
    readonly List<UnityEngine.Object> _made = new List<UnityEngine.Object>();
    Material _coronaMaterial;
    Renderer _sunRenderer;

    // The scene's own ambient, while the fill is on.
    bool _filling;
    AmbientMode _savedMode;
    Color _savedLight;
    SphericalHarmonicsL2 _savedProbe;
    Color _savedAsColour;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (worlds == null) worlds = FindObjectOfType<WorldSwitcher_NEW>();
        if (sun == null) sun = transform.Find("Sun");
        if (sun == null)
        {
            Debug.LogWarning("[SolarSystemLook_NEW] No Sun (a child named 'Sun'); nothing added.", this);
            return;
        }
        _sunRenderer = sun.GetComponent<Renderer>();

        if (sunlight) BuildSunlight();
        BuildAtmospheres();
        if (corona) BuildCorona();
    }

    void OnDisable() => StopFill();

    void OnDestroy()
    {
        StopFill();
        foreach (UnityEngine.Object o in _made)
            if (o != null) Destroy(o);
        _made.Clear();
    }

    void LateUpdate()
    {
        if (sun == null) return;

        Vector3 at = sun.position;
        for (int i = 0; i < _atmosphereMaterials.Count; i++) _atmosphereMaterials[i].SetVector(SunPosId, at);

        // The fill, as much as the world shows.
        float shown = worlds != null ? worlds.GroupAlpha(layerId)
                    : (_sunRenderer != null && _sunRenderer.enabled ? 1f : 0f);
        if (shown <= 0.001f || fill.maxColorComponent <= 0f) StopFill();
        else Fill(shown);
    }

    // ── Building ─────────────────────────────────────────────────────────────

    void BuildSunlight()
    {
        float farthest = 0f;
        foreach (Transform planet in transform)
            if (planet != sun && planet.GetComponent<Renderer>() != null)
                farthest = Mathf.Max(farthest, Vector3.Distance(planet.position, sun.position));
        if (farthest <= 0f) return;

        var go = new GameObject("Sunlight (runtime)");
        go.transform.SetParent(sun, false);
        go.layer = sun.gameObject.layer;
        _made.Add(go);

        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = sunlightColour;
        light.intensity = sunlightIntensity;
        light.range = farthest * sunlightReach;
        light.shadows = LightShadows.None;
        light.renderMode = LightRenderMode.ForcePixel;
    }

    void BuildAtmospheres()
    {
        Shader shader = Resources.Load<Shader>("PlanetAtmosphere_NEW");
        if (shader == null)
        {
            Debug.LogWarning("[SolarSystemLook_NEW] Resources/PlanetAtmosphere_NEW.shader is missing; no atmospheres.", this);
            return;
        }

        foreach (Atmosphere a in atmospheres)
        {
            if (a == null || a.strength <= 0f || string.IsNullOrEmpty(a.planet)) continue;

            Transform planet = transform.Find(a.planet);
            MeshFilter mf = planet != null ? planet.GetComponent<MeshFilter>() : null;
            if (mf == null || mf.sharedMesh == null) continue;

            var material = new Material(shader) { name = a.planet + " atmosphere (runtime)" };
            material.SetColor(TintId, a.colour * a.strength);
            material.SetVector(SunPosId, sun.position);
            _atmosphereMaterials.Add(material);
            _made.Add(material);

            var go = new GameObject(a.planet + " atmosphere (runtime)");
            go.layer = planet.gameObject.layer;
            go.transform.SetParent(planet, false);
            go.transform.localScale = Vector3.one * (1f + atmosphereHeight);
            go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _made.Add(go);
        }
    }

    void BuildCorona()
    {
        Shader shader = Resources.Load<Shader>("SunCorona_NEW");
        if (shader == null)
        {
            Debug.LogWarning("[SolarSystemLook_NEW] Resources/SunCorona_NEW.shader is missing; no corona.", this);
            return;
        }

        // The Sun's radius in its mesh's units: a sphere's bounds are as wide as it is.
        MeshFilter mf = sun.GetComponent<MeshFilter>();
        float radius = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.extents.x : 0.5f;

        _coronaMaterial = new Material(shader) { name = "Sun corona (runtime)" };
        _coronaMaterial.SetColor(TintId, coronaColour);
        _coronaMaterial.SetFloat("_Radius", radius);
        _coronaMaterial.SetFloat("_Inner", 0.22f * coronaSize);
        _coronaMaterial.SetFloat("_Outer", 1.1f * coronaSize);
        _coronaMaterial.SetFloat("_Streamers", streamers);
        _made.Add(_coronaMaterial);

        // A unit quad; the shader turns it to the camera and sizes it, four radii out, so its
        // bounds have to hold that.
        var quad = new Mesh { name = "Sun corona quad" };
        quad.vertices = new[] { new Vector3(-1f, -1f, 0f), new Vector3(1f, -1f, 0f), new Vector3(1f, 1f, 0f), new Vector3(-1f, 1f, 0f) };
        quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        quad.bounds = new Bounds(Vector3.zero, Vector3.one * (2f * 4f * radius));
        _made.Add(quad);

        var go = new GameObject("Sun corona (runtime)");
        go.layer = sun.gameObject.layer;
        go.transform.SetParent(sun, false);
        go.AddComponent<MeshFilter>().sharedMesh = quad;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = _coronaMaterial;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = LightProbeUsage.Off;
        mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        _made.Add(go);
    }

    // ── Fill ─────────────────────────────────────────────────────────────────

    /// <summary>The scene's ambient, eased towards the fill by <paramref name="shown"/>.</summary>
    void Fill(float shown)
    {
        if (!_filling)
        {
            _savedMode = RenderSettings.ambientMode;
            _savedLight = RenderSettings.ambientLight;
            _savedProbe = RenderSettings.ambientProbe;
            _savedAsColour = AverageOf(_savedProbe);
            _filling = true;
        }

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.Lerp(_savedAsColour, fill, Mathf.Clamp01(shown));
    }

    void StopFill()
    {
        if (!_filling) return;
        _filling = false;
        RenderSettings.ambientMode = _savedMode;
        RenderSettings.ambientLight = _savedLight;
        RenderSettings.ambientProbe = _savedProbe;
    }

    /// <summary>The colour an ambient probe lights a surface with, averaged over six ways it could face.</summary>
    static Color AverageOf(SphericalHarmonicsL2 probe)
    {
        Vector3[] ways = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
        var colours = new Color[ways.Length];
        probe.Evaluate(ways, colours);
        Color sum = Color.black;
        for (int i = 0; i < colours.Length; i++) sum += colours[i];
        Color average = sum / ways.Length;
        average.a = 1f;
        return average;
    }
}

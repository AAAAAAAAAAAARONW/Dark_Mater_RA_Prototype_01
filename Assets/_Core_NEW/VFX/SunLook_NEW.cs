using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Sun, drawn after NASA's SDO pictures of it in extreme ultraviolet — a red disc
/// mottled by the chromosphere's network, crossed by dark filaments, with bright active
/// regions; a fuzzy edge of spicules and a few loops standing on the limb; a faint red glow
/// — but softer and less busy than the pictures. Resources/SunSurface_NEW.shader draws the
/// disc, SunGlow_NEW what is round it.
///
/// Put it on the Sun's own object (the sphere). It draws the Sun from the same mesh and stops
/// the original renderer drawing (forceRenderingOff, which WorldSwitcher_NEW never touches);
/// disable it and the old Sun is back. The parts are built before WorldSwitcher_NEW gathers
/// its renderers, so it fades them with the Solar System. Nothing is saved but the settings.
/// Tools > Journey NEW > Sun Look adds it.
///
/// IT MOVES. A slow flow drags the surface about (flow). The Sun turns (turnSpeed), carrying
/// six active regions round in the activity belts; as one comes onto the limb its loops rise
/// into view there, and sink as it goes behind. And now and then, every 6 to 20 seconds as
/// activity sets it, a region flares: a second's brightening, a few seconds dying away, its
/// loops and the glow round the Sun flaring with it. Flare() sets one off at once, a big one,
/// for a moment the journey wants to point at.
///
/// The regions are worked out here, in world space, and handed to both shaders, so the
/// patches on the disc and the loops at the limb are the same regions.
///
/// Its colours are one ramp, measured from NASA's picture (Resources/SunRamp_NEW.cginc): the
/// disc mostly red, orange and amber only in its brightest tenth, never white; the glow faint
/// and dark red. Brightness is what the settings here move; colour follows from it.
///
/// 21 Nov: it was SunWhiteLight_NEW, the Sun as a filtered telescope shows it — white, limb
/// darkened, granulated, spotted. Too bright in the journey, and the spots read as grey
/// bubbles; the settings were renamed with the change of look, so the new defaults apply.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
[HierarchyBadge_NEW("SUN", "#F2C14E")]
public class SunLook_NEW : MonoBehaviour
{
    [Header("Disc")]
    [Range(0.3f, 2f)] [SerializeField] float brightness = 1f;
    [Tooltip("A slow flow dragging the surface about, so it moves like gas.")]
    [Range(0f, 1f)] [SerializeField] float flow = 0.5f;
    [Tooltip("The chromosphere's mottling: bright along the edges of convection cells.")]
    [Range(0f, 1f)] [SerializeField] float network = 0.8f;
    [Tooltip("How far the disc's brightness spreads from its median. At 2.1 its darkest and " +
             "brightest tenths fall where NASA's picture's do.")]
    [Range(0.5f, 3f)] [SerializeField] float contrast = 2.1f;
    [Tooltip("Cells across the Sun's radius.")]
    [Range(4f, 60f)] [SerializeField] float cellScale = 14f;
    [Tooltip("Thin dark threads across the disc.")]
    [Range(0f, 1f)] [SerializeField] float filaments = 0.6f;
    [Tooltip("Bright patches in the activity belts.")]
    [Range(0f, 1f)] [SerializeField] float activeRegions = 1f;
    [Tooltip("How far the active regions go over 1, for the bloom. Kept low so a region in the " +
             "middle of the disc does not bloom into a white spot there.")]
    [Range(0f, 4f)] [SerializeField] float regionGlow = 1.2f;
    [Tooltip("Even across the disc, brighter towards the edge, as in the ultraviolet.")]
    [Range(0f, 2f)] [SerializeField] float limbBrightening = 0.9f;
    [Tooltip("How fast the Sun turns, radians a second, carrying the active regions - and their " +
             "loops - round. The real Sun: 0.0000027.")]
    [SerializeField] float turnSpeed = 0.01f;

    [Header("Round it")]
    [Range(0f, 2f)] [SerializeField] float glow = 0.6f;
    [Tooltip("How far the glow reaches, in the Sun's radii.")]
    [Range(2f, 12f)] [SerializeField] float glowExtent = 5f;
    [Tooltip("The fuzzy edge of jets just off the limb.")]
    [Range(0f, 2f)] [SerializeField] float spicules = 0.7f;
    [Tooltip("Arches of gas standing on the limb, three to an active region.")]
    [Range(0f, 2f)] [SerializeField] float limbLoops = 0.8f;
    [Tooltip("Bright knots running along the loops.")]
    [Range(0f, 2f)] [SerializeField] float loopFlow = 1f;

    [Header("Flares")]
    [Tooltip("How often regions flare by themselves: 0 never; 1 every 6 to 20 seconds; less, less often.")]
    [Range(0f, 1f)] [SerializeField] float activity = 0.6f;
    [Tooltip("Seconds a flare takes to rise.")]
    [Min(0.05f)] [SerializeField] float flareRise = 1f;
    [Tooltip("Seconds it takes to die away.")]
    [Min(0.1f)] [SerializeField] float flareFall = 3f;

    const HideFlags Built = HideFlags.HideAndDontSave;

    readonly List<GameObject> _parts = new List<GameObject>();
    readonly List<Material> _materials = new List<Material>();
    Material _disc, _glow;
    Renderer _original;
    float _meshRadius = 0.5f;
    bool _dirty;

    // The active regions: longitude and latitude in degrees, and size (chord length on the
    // unit sphere). Six round the activity belts, so wherever the Sun is seen from, one is
    // near the limb.
    static readonly Vector3[] Regions =
    {
        new Vector3( 20f,  18f, 0.14f), new Vector3( 80f, -22f, 0.12f), new Vector3(140f,  15f, 0.10f),
        new Vector3(200f, -17f, 0.13f), new Vector3(260f,  20f, 0.11f), new Vector3(320f, -15f, 0.12f),
    };

    readonly Vector4[] _regionWorld = new Vector4[6];
    readonly float[] _flare = new float[6];
    readonly float[] _flareStart = { -1f, -1f, -1f, -1f, -1f, -1f };
    readonly float[] _flarePeak = new float[6];
    float _turn;
    float _nextFlare = -1f;

    static readonly int RegionsId = Shader.PropertyToID("_Regions");
    static readonly int RegionFlareId = Shader.PropertyToID("_RegionFlare");
    static readonly int TurnId = Shader.PropertyToID("_Turn");
    static Mesh _quad;

    /// <summary>Sets off a big flare at once, in the region most nearly facing the main camera.</summary>
    public void Flare()
    {
        int pick = Random.Range(0, Regions.Length);
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 toCamera = (cam.transform.position - transform.position).normalized;
            float best = -2f;
            for (int k = 0; k < Regions.Length; k++)
            {
                float facing = Vector3.Dot(_regionWorld[k], toCamera);
                if (facing > best) { best = facing; pick = k; }
            }
        }
        StartFlare(pick, 1f);
    }

    void StartFlare(int k, float peak)
    {
        _flareStart[k] = Time.time;
        _flarePeak[k] = peak;
    }

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
        if (_disc == null) return;
        if (_dirty) { Apply(); _dirty = false; }

        _turn = Mathf.Repeat(_turn + turnSpeed * Time.deltaTime, Mathf.PI * 2f);

        // Now and then, a region flares by itself.
        float now = Time.time;
        if (activity > 0f && Application.isPlaying)
        {
            if (_nextFlare < 0f) _nextFlare = now + NextGap();
            if (now >= _nextFlare)
            {
                StartFlare(Random.Range(0, Regions.Length), Random.Range(0.3f, 1f));
                _nextFlare = now + NextGap();
            }
        }

        for (int k = 0; k < Regions.Length; k++)
        {
            float f = 0f;
            if (_flareStart[k] >= 0f)
            {
                float t = now - _flareStart[k];
                f = t < flareRise ? t / flareRise : Mathf.Exp(-(t - flareRise) / flareFall * 3f);
                if (t > flareRise + flareFall) _flareStart[k] = -1f;
                f *= _flarePeak[k];
            }
            _flare[k] = f;

            // Where the region is now: turned with the Sun, then into the world.
            Vector3 r = Regions[k];
            float lon = r.x * Mathf.Deg2Rad, lat = r.y * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Sin(lon) * Mathf.Cos(lat), Mathf.Sin(lat), Mathf.Cos(lon) * Mathf.Cos(lat));
            Vector3 world = (transform.rotation * Turn(dir, _turn)).normalized;
            _regionWorld[k] = new Vector4(world.x, world.y, world.z, r.z);
        }

        foreach (Material m in _materials)
        {
            m.SetVectorArray(RegionsId, _regionWorld);
            m.SetFloatArray(RegionFlareId, _flare);
        }
        _disc.SetFloat(TurnId, _turn);
    }

    float NextGap() => Random.Range(6f, 20f) / Mathf.Max(activity, 0.05f);

    // SunSurface_NEW's Turn. The shader finds a surface point's place on the unturned Sun as
    // Turn(n, -a); so the point that was at v on the unturned Sun is now at Turn(v, a).
    static Vector3 Turn(Vector3 v, float a)
    {
        float c = Mathf.Cos(a), s = Mathf.Sin(a);
        return new Vector3(c * v.x + s * v.z, v.y, -s * v.x + c * v.z);
    }

    void Build()
    {
        if (_disc != null) return;

        _original = GetComponent<Renderer>();
        MeshFilter filter = GetComponent<MeshFilter>();
        if (_original == null || filter == null || filter.sharedMesh == null)
        {
            Debug.LogWarning("[SunLook_NEW] Needs to sit on the Sun's own sphere (a MeshFilter and renderer).", this);
            return;
        }

        Shader discShader = Resources.Load<Shader>("SunSurface_NEW");
        Shader glowShader = Resources.Load<Shader>("SunGlow_NEW");
        if (discShader == null || glowShader == null)
        {
            Debug.LogWarning("[SunLook_NEW] Its shaders (Resources/SunSurface_NEW, SunGlow_NEW) are missing; leaving the original.", this);
            return;
        }

        Mesh sphere = filter.sharedMesh;
        _meshRadius = Mathf.Max(sphere.bounds.extents.x, 1e-4f);

        _disc = Part("Disc", sphere, discShader, 2000);
        _glow = Part("Glow", Quad(), glowShader, 3000);

        _original.forceRenderingOff = true;
        Apply();
    }

    Material Part(string name, Mesh mesh, Shader shader, int queue)
    {
        var go = new GameObject(gameObject.name + " " + name) { hideFlags = Built };
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);

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

    /// <summary>A quad from -1 to 1, turned to the camera in the shader; its bounds cover the glow at its widest.</summary>
    static Mesh Quad()
    {
        if (_quad != null) return _quad;
        _quad = new Mesh
        {
            name = "Sun glow quad (runtime)",
            hideFlags = Built,
            vertices = new[] { new Vector3(-1, -1, 0), new Vector3(1, -1, 0), new Vector3(1, 1, 0), new Vector3(-1, 1, 0) },
            uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) },
            triangles = new[] { 0, 2, 1, 0, 3, 2 },
        };
        // The shader spreads it to glowExtent radii (12 at most) round the centre, whichever way it faces.
        _quad.bounds = new Bounds(Vector3.zero, Vector3.one * 12f);
        return _quad;
    }

    void Apply()
    {
        if (_disc == null) return;
        _disc.SetFloat("_Brightness", brightness);
        _disc.SetFloat("_Flow", flow);
        _disc.SetFloat("_Network", network);
        _disc.SetFloat("_Contrast", contrast);
        _disc.SetFloat("_CellScale", cellScale);
        _disc.SetFloat("_Filaments", filaments);
        _disc.SetFloat("_Active", activeRegions);
        _disc.SetFloat("_ActiveGlow", regionGlow);
        _disc.SetFloat("_LimbBright", limbBrightening);

        _glow.SetFloat("_Strength", glow);
        _glow.SetFloat("_Spicules", spicules);
        _glow.SetFloat("_Loops", limbLoops);
        _glow.SetFloat("_LoopFlow", loopFlow);
        _glow.SetFloat("_MeshRadius", _meshRadius);
        _glow.SetFloat("_Extent", glowExtent);
    }

    void Clear()
    {
        if (_original != null) _original.forceRenderingOff = false;
        foreach (GameObject go in _parts) Kill(go);
        foreach (Material m in _materials) Kill(m);
        _parts.Clear();
        _materials.Clear();
        _disc = _glow = null;
    }

    static void Kill(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }
}

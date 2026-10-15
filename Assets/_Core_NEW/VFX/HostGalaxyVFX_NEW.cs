using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The galaxy a quasar lives in. The scientists' first note on the quasar: it is the heart of a
/// galaxy, not a light on its own — inside it there should be stars all round, and from far off
/// the whole galaxy should be seen, with the quasar a blinding point at its centre.
///
/// Not the Milky Way, which the journey reaches at its end and which must not be mistaken for
/// it. A galaxy of the early universe, less than two billion years after the Big Bang, round a
/// black hole feeding fast:
///   puffy    a thick disc, not a thin plate (the scientists' note on young galaxies), a
///            round bulge and a dim halo — its light a volume, seen in depth at any angle
///   arms     two, open, warm peach, knotted with giant clumps of star formation (young
///            galaxies are built of them), dark dust lanes along their inner edges; lopsided —
///            one runs on past the edge into a faint tidal plume, the mark of a neighbour
///   bar      short and fat, its straight dust lanes carrying gas in towards the quasar
///   ring     where that gas piles up, round the quasar: a bright ring of star formation, as
///            NGC 1097 has round its active nucleus — the quasar's frame, from near and far
///   stars    born on the arms and orbiting off them; clusters strung round the ring; and a
///            round swarm crowding in on the quasar, so from its disc there are stars on every
///            side, near and far
/// Everything turns the way the quasar's disc does.
///
/// Put it on an empty object at the quasar. Its disc lies in the object's XZ plane and its
/// radius is the object's scale (tilt it off the quasar's disc a little: a quasar's disc need
/// not lie in its galaxy's plane). As QuasarVFX_NEW and GalaxyVFX_NEW do, it builds what it
/// draws as hidden children that are never saved, in the editor too, and fades with its world
/// through _Color, built before WorldSwitcher_NEW gathers renderers.
///
/// FROM INSIDE. The flat sheet goes as the camera comes into the layer of the disc, where there
/// is no sheet to see; the volume leaves out the light near the camera, which is stars, each one
/// drawn; what is left is stars all round and the far reaches of the galaxy as a band across
/// the sky.
///
/// CHEAP. One sheet worked out per pixel with a handful of noise reads (QuasarNoise_NEW); the
/// volume smooth maths, twelve samples a ray for each half; the stars one mesh moved on the
/// GPU. Nothing runs per frame on the CPU.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
[HierarchyBadge_NEW("HOST GALAXY", "#E8B48A")]
public class HostGalaxyVFX_NEW : MonoBehaviour
{
    [Header("Shape (radius 1 is this object's scale)")]
    [Tooltip("How open the two arms are, in degrees. The Milky Way's are about 12; these are looser.")]
    [Range(10f, 40f)] [SerializeField] float pitch = 22f;

    [Tooltip("The bar: its angle in degrees, its half length (where the arms start) and its half width.")]
    [SerializeField] Vector3 bar = new Vector3(20f, 0.3f, 0.1f);

    [Tooltip("The starburst ring round the quasar: its radius and its width, of the galaxy's.")]
    [SerializeField] Vector2 ring = new Vector2(0.13f, 0.016f);

    [Tooltip("0: two even arms. 1: one runs on past the edge into a tidal plume, the other dies early.")]
    [Range(0f, 1f)] [SerializeField] float lopsided = 0.6f;

    [Tooltip("Arm width at the bar's end, and how much it grows outward.")]
    [SerializeField] Vector2 armWidth = new Vector2(0.06f, 0.09f);

    [Tooltip("The disc's scale length, of the radius.")]
    [Range(0.05f, 0.5f)] [SerializeField] float scaleLength = 0.22f;

    [Tooltip("The disc's scale height at the centre, and how much it grows outward: puffy, as a young " +
             "galaxy is.")]
    [SerializeField] Vector2 thickness = new Vector2(0.05f, 0.06f);

    [Header("Look")]
    [Range(0f, 2f)] [SerializeField] float dust = 1.2f;
    [Tooltip("Giant clumps of star formation along the arms.")]
    [Range(0f, 3f)] [SerializeField] float clumps = 1.2f;
    [Tooltip("Light between the arms.")]
    [Range(0f, 1f)] [SerializeField] float haze = 0.5f;
    [Range(0f, 3f)] [SerializeField] float brightness = 0.6f;
    [Tooltip("The volume's light: the puffy disc, the bulge, the ring standing up, the halo.")]
    [Range(0f, 4f)] [SerializeField] float depthGlow = 1.8f;
    [Tooltip("The dust along the disc's middle in the volume: a dark lane seen edge on or from inside.")]
    [Range(0f, 2f)] [SerializeField] float dustVolume = 0.6f;
    [Tooltip("Nearer the camera than x (of the radius) its light is all stars, each drawn; from y on, " +
             "all glow. From inside it is stars all round, not a fog.")]
    [SerializeField] Vector2 resolve = new Vector2(0.03f, 0.4f);

    [ColorUsage(false, true)] [SerializeField] Color bulgeColour = new Color(1.5f, 1.05f, 0.55f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color armColour = new Color(1.3f, 0.92f, 0.62f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color between = new Color(0.7f, 0.48f, 0.32f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color ringColour = new Color(1.7f, 1.25f, 0.85f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color clumpColour = new Color(1.7f, 1.2f, 0.95f, 1f);
    [Tooltip("The rose glow of ionised gas round the clumps and the ring.")]
    [ColorUsage(false, true)] [SerializeField] Color glow = new Color(1.2f, 0.5f, 0.42f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color plume = new Color(0.55f, 0.42f, 0.34f, 1f);
    [SerializeField] Color dustColour = new Color(0.1f, 0.05f, 0.025f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color discGlow = new Color(0.9f, 0.62f, 0.4f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color ringGlow = new Color(1.4f, 0.8f, 0.55f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color haloColour = new Color(0.45f, 0.36f, 0.3f, 1f);

    [Header("Motion")]
    [Tooltip("How fast the gas streams round on its orbits, radii a second.")]
    [SerializeField] float gasFlow = 0.012f;
    [Tooltip("How fast the pattern — bar, arms, ring — turns, radians a second. Slow: it is a galaxy.")]
    [SerializeField] float patternSpeed = 0.004f;
    [Tooltip("The clumps brightening and dimming, each on its own slow clock.")]
    [Range(0f, 1f)] [SerializeField] float twinkle = 0.4f;

    [Header("Stars")]
    [Range(0, 20000)] [SerializeField] int armStars = 5000;
    [Tooltip("Young clusters strung round the starburst ring.")]
    [Range(0, 3000)] [SerializeField] int ringStars = 500;
    [Tooltip("Giant clumps of star formation, drawn as big soft knots of light.")]
    [Range(0, 300)] [SerializeField] int clumpCount = 70;
    [Tooltip("The round swarm crowding in on the quasar: the stars round its disc.")]
    [Range(0, 20000)] [SerializeField] int nucleusStars = 6000;
    [Tooltip("How far out that swarm reaches, of the radius.")]
    [Range(0.02f, 0.6f)] [SerializeField] float nucleusReach = 0.22f;
    [Range(0, 30000)] [SerializeField] int discStars = 7000;
    [Tooltip("Globular clusters in the halo.")]
    [Range(0, 500)] [SerializeField] int globulars = 120;
    [Tooltip("Their size, as a fraction of the radius. Never less than two pixels on screen.")]
    [Min(0f)] [SerializeField] float starSize = 0.0018f;
    [Tooltip("The largest a star is drawn, of half the screen's height — so one passing close is still a star.")]
    [Min(0.001f)] [SerializeField] float maxStarSize = 0.01f;
    [Tooltip("Orbital speed on the flat rotation curve, radii a second.")]
    [SerializeField] float orbitalSpeed = 0.012f;
    [ColorUsage(false, true)] [SerializeField] Color youngStarColour = new Color(1.4f, 1.15f, 0.9f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color ringStarColour = new Color(1.2f, 0.95f, 0.7f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color clumpStarColour = new Color(1.3f, 0.95f, 0.75f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color nucleusStarColour = new Color(1.6f, 1.15f, 0.75f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color discStarColour = new Color(0.32f, 0.24f, 0.17f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color globularColour = new Color(0.42f, 0.36f, 0.3f, 1f);

    [SerializeField] int seed = 0;

    const HideFlags Built = HideFlags.HideAndDontSave;

    // The box the volume is worked out in, and the sheet's reach: past the plume.
    static readonly Vector3 VolumeBox = new Vector3(1.45f, 0.5f, 1.45f);
    const float SheetReach = 1.46f;

    readonly List<GameObject> _parts = new List<GameObject>();
    readonly List<Material> _materials = new List<Material>();
    readonly List<Mesh> _meshes = new List<Mesh>();
    Material _disc, _stars, _depthFar, _depthNear;
    int _builtStars = -1, _builtSeed;
    bool _rebuild, _dirty;

    /// <summary>The galaxy's radius, in world units.</summary>
    public float Radius => transform.lossyScale.x;

    int StarTotal => armStars + ringStars + clumpCount + nucleusStars + discStars + globulars;

    void OnEnable() => Build();

    void OnDisable() => Clear();

    void OnValidate()
    {
        _rebuild |= _builtStars >= 0 && (_builtStars != StarTotal || _builtSeed != seed);
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
        else if (_dirty && _disc != null)
        {
            Apply();
        }
        _dirty = false;
    }

    // ── Building ────────────────────────────────────────────────────────────

    void Build()
    {
        if (_disc != null) return;

        Shader discShader = Resources.Load<Shader>("HostGalaxyDisc_NEW");
        Shader starShader = Resources.Load<Shader>("HostGalaxyStars_NEW");
        Shader depthShader = Resources.Load<Shader>("HostGalaxyVolume_NEW");
        if (discShader == null || starShader == null || depthShader == null)
        {
            Debug.LogWarning("[HostGalaxyVFX_NEW] Its shaders (Resources/HostGalaxy*_NEW.shader) are missing; nothing drawn.", this);
            return;
        }

        // The volume's far half under the gas sheet, its near half over it (render queues).
        Mesh box = Keep(Box());
        _depthFar = Part("Depth (far half)", box, depthShader);
        _depthFar.renderQueue = 2989;
        _depthFar.SetFloat("_Side", -1f);
        _disc = Part("Disc", Keep(Plane()), discShader);
        _disc.renderQueue = 2990;
        _depthNear = Part("Depth (near half)", box, depthShader);
        _depthNear.renderQueue = 2993;
        _depthNear.SetFloat("_Side", 1f);
        _builtStars = StarTotal;
        _builtSeed = seed;
        _stars = Part("Stars", Keep(Stars(new[] { armStars, ringStars, clumpCount, nucleusStars, discStars, globulars }, seed)), starShader);
        _stars.renderQueue = 2992;

        Apply();
    }

    Mesh Keep(Mesh mesh)
    {
        mesh.hideFlags = Built;
        _meshes.Add(mesh);
        return mesh;
    }

    Material Part(string name, Mesh mesh, Shader shader)
    {
        var go = new GameObject("Host Galaxy " + name) { hideFlags = Built };
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        var material = new Material(shader) { name = "Host Galaxy " + name + " (runtime)", hideFlags = Built };
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
        foreach (GameObject go in _parts) Kill(go);
        foreach (Material m in _materials) Kill(m);
        foreach (Mesh m in _meshes) Kill(m);
        _parts.Clear();
        _materials.Clear();
        _meshes.Clear();
        _disc = _stars = _depthFar = _depthNear = null;
        _builtStars = -1;
    }

    static void Kill(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }

    /// <summary>Every setting onto the materials. _Color is left alone: it is the world's fade.</summary>
    void Apply()
    {
        Texture2D noise = QuasarNoise_NEW.Texture;

        // The shape, which every part shares (HostGalaxy_NEW.cginc).
        foreach (Material m in _materials)
        {
            m.SetFloat("_Pitch", pitch);
            m.SetVector("_Bar", bar);
            m.SetFloat("_RingRadius", ring.x);
            m.SetFloat("_Lopsided", lopsided);
            m.SetFloat("_PatternSpeed", patternSpeed);
            m.SetVector("_Thickness", thickness);
        }

        _disc.SetTexture("_Noise", noise);
        _disc.SetFloat("_RingWidth", ring.y);
        _disc.SetVector("_ArmWidth", armWidth);
        _disc.SetFloat("_ScaleLength", scaleLength);
        _disc.SetFloat("_Dust", dust);
        _disc.SetFloat("_Clumps", clumps);
        _disc.SetFloat("_Haze", haze);
        _disc.SetFloat("_Brightness", brightness);
        _disc.SetFloat("_Flow", gasFlow);
        _disc.SetFloat("_Twinkle", twinkle);
        _disc.SetColor("_Core", bulgeColour);
        _disc.SetColor("_Arm", armColour);
        _disc.SetColor("_Between", between);
        _disc.SetColor("_Ring", ringColour);
        _disc.SetColor("_Clump", clumpColour);
        _disc.SetColor("_Glow", glow);
        _disc.SetColor("_Plume", plume);
        _disc.SetColor("_DustColor", dustColour);

        foreach (Material depth in new[] { _depthFar, _depthNear })
        {
            depth.SetVector("_Box", new Vector4(VolumeBox.x, VolumeBox.y, VolumeBox.z, 0f));
            depth.SetFloat("_Volume", depthGlow * brightness / 0.6f);
            depth.SetFloat("_DustVolume", dustVolume);
            depth.SetVector("_Resolve", resolve);
            depth.SetColor("_Core", bulgeColour);
            depth.SetColor("_Old", discGlow);
            depth.SetColor("_Ring", ringGlow);
            depth.SetColor("_Halo", haloColour);
        }

        _stars.SetTexture("_Noise", noise);
        _stars.SetFloat("_Speed", orbitalSpeed);
        _stars.SetFloat("_Size", starSize);
        _stars.SetFloat("_MaxSize", maxStarSize);
        _stars.SetFloat("_NucleusReach", nucleusReach);
        _stars.SetColor("_Young", youngStarColour);
        _stars.SetColor("_RingStars", ringStarColour);
        _stars.SetColor("_Clump", clumpStarColour);
        _stars.SetColor("_Nucleus", nucleusStarColour);
        _stars.SetColor("_DiscStars", discStarColour);
        _stars.SetColor("_Globular", globularColour);
    }

    // ── Meshes ──────────────────────────────────────────────────────────────

    /// <summary>A square grid in XZ out past the plume; the shader draws nothing beyond it.</summary>
    static Mesh Plane()
    {
        const int cells = 128;
        int stride = cells + 1;
        var vertices = new Vector3[stride * stride];
        for (int z = 0; z <= cells; z++)
        for (int x = 0; x <= cells; x++)
            vertices[z * stride + x] = new Vector3(Mathf.Lerp(-SheetReach, SheetReach, x / (float)cells), 0f,
                                                   Mathf.Lerp(-SheetReach, SheetReach, z / (float)cells));
        var triangles = new int[cells * cells * 6];
        int t = 0;
        for (int z = 0; z < cells; z++)
        for (int x = 0; x < cells; x++)
        {
            int i = z * stride + x;
            triangles[t++] = i; triangles[t++] = i + stride; triangles[t++] = i + 1;
            triangles[t++] = i + 1; triangles[t++] = i + stride; triangles[t++] = i + stride + 1;
        }
        var mesh = new Mesh { name = "Host galaxy disc", vertices = vertices, triangles = triangles };
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(SheetReach * 2f, 0.2f, SheetReach * 2f));
        return mesh;
    }

    /// <summary>The box the volume is worked out in; its shader matches _Box to it.</summary>
    static Mesh Box()
    {
        Vector3 b = VolumeBox;
        var vertices = new Vector3[8];
        for (int i = 0; i < 8; i++)
            vertices[i] = new Vector3((i & 1) != 0 ? b.x : -b.x, (i & 2) != 0 ? b.y : -b.y, (i & 4) != 0 ? b.z : -b.z);
        // Outward-facing; the shader draws the back faces.
        int[] triangles =
        {
            0, 2, 3, 0, 3, 1,   4, 5, 7, 4, 7, 6,   // -z, +z
            0, 1, 5, 0, 5, 4,   2, 6, 7, 2, 7, 3,   // -y, +y
            0, 4, 6, 0, 6, 2,   1, 3, 7, 1, 7, 5,   // -x, +x
        };
        var mesh = new Mesh { name = "Host galaxy depth", vertices = vertices, triangles = triangles };
        mesh.bounds = new Bounds(Vector3.zero, b * 2f);
        return mesh;
    }

    /// <summary>One quad per star, all at the origin: the shader moves them. Seeds and kind in UV1.
    /// counts: arm, ring, clump, nucleus, disc, halo (kinds 0..5).</summary>
    static Mesh Stars(int[] counts, int starSeed)
    {
        int count = 0;
        foreach (int c in counts) count += c;
        var vertices = new Vector3[count * 4];
        var corners = new List<Vector2>(count * 4);
        var seeds = new List<Vector4>(count * 4);
        var triangles = new int[count * 6];
        var random = new System.Random(7919 + starSeed);

        for (int i = 0; i < count; i++)
        {
            int kind = 0;
            for (int k = 0, end = counts[0]; i >= end && k < counts.Length - 1; end += counts[++k]) kind = k + 1;
            var s = new Vector4((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble(), kind);
            corners.Add(new Vector2(-1f, -1f)); corners.Add(new Vector2(1f, -1f));
            corners.Add(new Vector2(1f, 1f)); corners.Add(new Vector2(-1f, 1f));
            for (int c = 0; c < 4; c++) seeds.Add(s);

            int v = i * 4, t = i * 6;
            triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
            triangles[t + 3] = v; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
        }

        var mesh = new Mesh { name = "Host galaxy stars" };
        if (vertices.Length > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.SetUVs(0, corners);
        mesh.SetUVs(1, seeds);
        mesh.triangles = triangles;
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(3f, 2f, 3f));
        return mesh;
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The first galaxy the light meets on its journey (the Micro layer, J4): a galaxy at cosmic
/// noon, ten billion years ago, when the universe made stars faster than at any time before
/// or since — "galaxies grow where the threads of the web cross, and this one is in its
/// busiest age".
///
/// Not the Milky Way (GalaxyVFX_NEW: a grand barred spiral, blue and white) and not the
/// quasar's host (HostGalaxyVFX_NEW: two lopsided arms, peach and gold, a starburst ring).
/// A galaxy of that age looked like neither:
///   clumps    no arms — a dozen giant clumps of star formation, each a knot of blue-white
///             stars in a magenta glow of ionised hydrogen, some a tenth of the galaxy across,
///             and smaller knots through the gas between them
///   disc      thick and turbulent, violet-blue, torn by patches of dust, with a small, warm,
///             dusty core
///   streams   cold gas falling in along narrow filaments from the web outside, blue-green,
///             feeding the star formation (the cold streams of the simulations)
///   wind      hot gas blown out above and below the disc by all that star formation
///   stars     crowded into the clumps, through the thick disc, along the streams, and a dim
///             round halo
/// The clumps go round on their orbits, the gas streams through, the streams flow in.
///
/// Put it on an empty object in the Micro render group. Its disc lies in the object's XZ
/// plane and its radius is the object's scale. Tools > Journey NEW > Add Young Galaxy VFX to
/// Open Scene places it across the light's path so the light comes in over the disc among
/// the clumps, crosses it, and leaves with the galaxy behind. As the other galaxies do, it
/// builds what it draws as hidden children that are never saved, in the editor too, and fades
/// with its world through _Color, built before WorldSwitcher_NEW gathers renderers.
///
/// FROM INSIDE. The flat sheet goes as the camera comes into the layer of the disc; the
/// volume leaves out most of the gas's glow from inside (it would be a fog) and keeps the
/// clumps, the core and the far reaches; the stars are all round, each one drawn.
///
/// CHEAP. One sheet worked out per pixel with a handful of noise reads (QuasarNoise_NEW); the
/// volume smooth maths, twelve samples a ray for each half; the stars one mesh moved on the
/// GPU. Nothing runs per frame on the CPU.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
[HierarchyBadge_NEW("YOUNG GALAXY", "#D08CFF")]
public class YoungGalaxyVFX_NEW : MonoBehaviour
{
    [Header("Shape (radius 1 is this object's scale)")]
    [Tooltip("Which galaxy: where its clumps and its streams fall.")]
    [SerializeField] int seed = 0;

    [Tooltip("How fast the clumps go round, radians a second at half the radius. Slow: it is a galaxy.")]
    [SerializeField] float clumpTurn = 0.01f;

    [Tooltip("The disc's scale height at the centre, and how much it grows outward: thick, as a galaxy " +
             "of that age was.")]
    [SerializeField] Vector2 thickness = new Vector2(0.06f, 0.07f);

    [Tooltip("How many cold streams of gas fall in from outside, 0 to 3; a fraction fades the last one in.")]
    [Range(0f, 3f)] [SerializeField] float streams = 2f;

    [Tooltip("The disc's scale length, of the radius.")]
    [Range(0.1f, 0.5f)] [SerializeField] float scaleLength = 0.3f;

    [Header("Look")]
    [Range(0f, 2f)] [SerializeField] float dust = 1f;
    [Tooltip("The giant clumps of star formation.")]
    [Range(0f, 3f)] [SerializeField] float clumps = 1.2f;
    [Tooltip("Smaller knots of star formation through the gas.")]
    [Range(0f, 3f)] [SerializeField] float knots = 1f;
    [Tooltip("The gas between the clumps.")]
    [Range(0f, 1f)] [SerializeField] float haze = 0.7f;
    [Range(0f, 3f)] [SerializeField] float brightness = 0.6f;
    [Tooltip("The volume's light: the thick disc, the clumps' glow, the streams, the wind, the halo.")]
    [Range(0f, 4f)] [SerializeField] float depthGlow = 1f;
    [Tooltip("The dust along the disc's middle in the volume: a dark lane seen edge on or from inside.")]
    [Range(0f, 2f)] [SerializeField] float dustVolume = 0.5f;
    [Tooltip("The wind of hot gas above and below the disc.")]
    [Range(0f, 3f)] [SerializeField] float wind = 1f;
    [Tooltip("Nearer the camera than x (of the radius) its light is all stars, each drawn; from y on, " +
             "all glow. From inside it is stars all round, not a fog.")]
    [SerializeField] Vector2 resolve = new Vector2(0.02f, 0.12f);

    [ColorUsage(false, true)] [SerializeField] Color coreColour = new Color(1.5f, 0.85f, 0.5f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color gasColour = new Color(0.45f, 0.5f, 1f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color clumpColour = new Color(1.5f, 1.75f, 2.4f, 1f);
    [Tooltip("The glow of ionised hydrogen round the clumps.")]
    [ColorUsage(false, true)] [SerializeField] Color glowColour = new Color(1.7f, 0.55f, 0.85f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color streamColour = new Color(0.5f, 0.85f, 0.95f, 1f);
    [SerializeField] Color dustColour = new Color(0.05f, 0.03f, 0.06f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color windColour = new Color(0.8f, 0.4f, 1f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color haloColour = new Color(0.3f, 0.32f, 0.55f, 1f);

    [Header("Motion")]
    [Tooltip("How fast the gas streams round on its orbits, radii a second.")]
    [SerializeField] float gasFlow = 0.01f;
    [Tooltip("The clumps brightening and dimming, each on its own slow clock.")]
    [Range(0f, 1f)] [SerializeField] float twinkle = 0.4f;

    [Header("Stars")]
    [Tooltip("Young stars crowded into the giant clumps.")]
    [Range(0, 20000)] [SerializeField] int clumpStars = 8000;
    [Tooltip("Smaller knots of star formation, drawn as soft glowing blobs.")]
    [Range(0, 500)] [SerializeField] int knotCount = 150;
    [Range(0, 30000)] [SerializeField] int discStars = 16000;
    [Tooltip("Stars forming along the cold streams.")]
    [Range(0, 5000)] [SerializeField] int streamStars = 1200;
    [Range(0, 5000)] [SerializeField] int haloStars = 1500;
    [Tooltip("Their size, as a fraction of the radius. Never less than two pixels on screen.")]
    [Min(0f)] [SerializeField] float starSize = 0.0018f;
    [Tooltip("The largest a star is drawn, of half the screen's height — so one passing close is still a star.")]
    [Min(0.001f)] [SerializeField] float maxStarSize = 0.01f;
    [Tooltip("Orbital speed on the flat rotation curve, radii a second.")]
    [SerializeField] float orbitalSpeed = 0.01f;
    [ColorUsage(false, true)] [SerializeField] Color clumpStarColour = new Color(1.5f, 1.7f, 2.3f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color knotColour = new Color(1.6f, 0.8f, 1.2f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color discStarColour = new Color(0.7f, 0.7f, 1.1f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color streamStarColour = new Color(0.5f, 0.85f, 0.9f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color haloStarColour = new Color(0.4f, 0.33f, 0.3f, 1f);

    const HideFlags Built = HideFlags.HideAndDontSave;

    // The box the volume is worked out in, and the sheet's reach: out past the streams.
    static readonly Vector3 VolumeBox = new Vector3(2f, 0.8f, 2f);
    const float SheetReach = 1.96f;

    readonly List<GameObject> _parts = new List<GameObject>();
    readonly List<Material> _materials = new List<Material>();
    readonly List<Mesh> _meshes = new List<Mesh>();
    Material _disc, _stars, _depthFar, _depthNear;
    int _builtStars = -1, _builtSeed;
    bool _rebuild, _dirty;

    /// <summary>The galaxy's radius, in world units.</summary>
    public float Radius => transform.lossyScale.x;

    int StarTotal => clumpStars + knotCount + discStars + streamStars + haloStars;

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

        Shader discShader = Resources.Load<Shader>("YoungGalaxyDisc_NEW");
        Shader starShader = Resources.Load<Shader>("YoungGalaxyStars_NEW");
        Shader depthShader = Resources.Load<Shader>("YoungGalaxyVolume_NEW");
        if (discShader == null || starShader == null || depthShader == null)
        {
            Debug.LogWarning("[YoungGalaxyVFX_NEW] Its shaders (Resources/YoungGalaxy*_NEW.shader) are missing; nothing drawn.", this);
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
        _stars = Part("Stars", Keep(Stars(new[] { clumpStars, knotCount, discStars, streamStars, haloStars }, seed)), starShader);
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
        var go = new GameObject("Young Galaxy " + name) { hideFlags = Built };
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        var material = new Material(shader) { name = "Young Galaxy " + name + " (runtime)", hideFlags = Built };
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

        // The shape, which every part shares (YoungGalaxy_NEW.cginc).
        foreach (Material m in _materials)
        {
            m.SetFloat("_Seed", seed);
            m.SetFloat("_ClumpTurn", clumpTurn);
            m.SetVector("_Thickness", thickness);
            m.SetFloat("_Streams", streams);
        }

        _disc.SetTexture("_Noise", noise);
        _disc.SetFloat("_ScaleLength", scaleLength);
        _disc.SetFloat("_Dust", dust);
        _disc.SetFloat("_Clumps", clumps);
        _disc.SetFloat("_Knots", knots);
        _disc.SetFloat("_Haze", haze);
        _disc.SetFloat("_Brightness", brightness);
        _disc.SetFloat("_Flow", gasFlow);
        _disc.SetFloat("_Twinkle", twinkle);
        _disc.SetColor("_Core", coreColour);
        _disc.SetColor("_Gas", gasColour);
        _disc.SetColor("_Clump", clumpColour);
        _disc.SetColor("_Glow", glowColour);
        _disc.SetColor("_Stream", streamColour);
        _disc.SetColor("_DustColor", dustColour);

        foreach (Material depth in new[] { _depthFar, _depthNear })
        {
            depth.SetVector("_Box", new Vector4(VolumeBox.x, VolumeBox.y, VolumeBox.z, 0f));
            depth.SetFloat("_Volume", depthGlow * brightness / 0.6f);
            depth.SetFloat("_DustVolume", dustVolume);
            depth.SetFloat("_Wind", wind);
            depth.SetVector("_Resolve", resolve);
            depth.SetColor("_Core", coreColour);
            depth.SetColor("_Gas", gasColour);
            depth.SetColor("_Glow", glowColour);
            depth.SetColor("_Clump", clumpColour);
            depth.SetColor("_Stream", streamColour);
            depth.SetColor("_WindColor", windColour);
            depth.SetColor("_Halo", haloColour);
        }

        _stars.SetFloat("_Speed", orbitalSpeed);
        _stars.SetFloat("_Size", starSize);
        _stars.SetFloat("_MaxSize", maxStarSize);
        _stars.SetColor("_ClumpStars", clumpStarColour);
        _stars.SetColor("_Knot", knotColour);
        _stars.SetColor("_DiscStars", discStarColour);
        _stars.SetColor("_StreamStars", streamStarColour);
        _stars.SetColor("_HaloStars", haloStarColour);
    }

    // ── Meshes ──────────────────────────────────────────────────────────────

    /// <summary>A square grid in XZ out past the streams; the shader draws nothing beyond it.</summary>
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
        var mesh = new Mesh { name = "Young galaxy disc", vertices = vertices, triangles = triangles };
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
        var mesh = new Mesh { name = "Young galaxy depth", vertices = vertices, triangles = triangles };
        mesh.bounds = new Bounds(Vector3.zero, b * 2f);
        return mesh;
    }

    /// <summary>One quad per star, all at the origin: the shader moves them. Seeds and kind in UV1.
    /// counts: clump, knot, disc, stream, halo (kinds 0..4).</summary>
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

        var mesh = new Mesh { name = "Young galaxy stars" };
        if (vertices.Length > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.SetUVs(0, corners);
        mesh.SetUVs(1, seeds);
        mesh.triangles = triangles;
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(4f, 2f, 4f));
        return mesh;
    }
}

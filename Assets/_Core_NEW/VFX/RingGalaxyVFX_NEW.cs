using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The first galaxy the light meets on its journey (the Micro layer, J4): a collisional ring
/// galaxy — "peculiar" in Hubble's scheme, not a spiral.
///
/// J4: "Ten billion years ago, the universe was making stars faster than at any time before or
/// since. Galaxies grow where the threads of the web cross, and this one is in its busiest age."
/// Galaxies grew by running into each other, and this one has just been run through: a smaller
/// galaxy fell straight through its disc, and the ripple that sent out through the gas is
/// lighting a burst of new stars all round it. Arp 148 (Mayall's Object) is the picture:
///   ring      a great loop of blue star clusters with a few pink knots, bunched with gaps, thick
///             and bright on the far side and opening out thin and faint where the intruder is
///   middle    a dim haze inside the ring; the old nucleus barely seen
///   intruder  the smaller galaxy that went through, touching the ring's edge: a long bright
///             streak at right angles to the ring, a dark lane of dust along its middle, its tail
///             drawn out away from the ring — ring and streak together the "question mark"
///             Nicholas Mayall described in 1940
///   outside   what is left of the old disc, and a plume of gas towards the intruder
/// Not the Milky Way (GalaxyVFX_NEW: a grand barred spiral) and not the quasar's host
/// (HostGalaxyVFX_NEW: two lopsided arms).
///
/// It moves, so it is alive: the ring and what is in it go round; every few seconds a ripple
/// spreads out from the nucleus, faint across the empty middle, and lights the ring and its knots
/// as it crosses them — the wave that made the ring, shown again; the knots light up, go round a
/// while and fade as others light up elsewhere; stars stream along the bridge into the ring and
/// out along the tail; every star twinkles.
///
/// Put it on an empty object in the Micro render group. Its disc lies in the object's XZ plane,
/// the intruder off to -X, and its radius is the object's scale. Tools > Journey NEW > Replace
/// First Galaxy with Ring Galaxy puts it where it was placed by hand in the Ana scene, ahead of
/// the light all the way across Micro and a little below its line, tipped 18° to face it (an
/// oval with the streak running into its left side), and switches the old galaxy off. The light
/// comes over its middle at the gate out into the cosmic web. As the other galaxies do, it builds
/// what it draws as hidden children that are never saved, in the editor too, and fades with its
/// world through _Color, built before WorldSwitcher_NEW gathers renderers.
///
/// CHEAP. One sheet worked out per pixel with a handful of noise reads (QuasarNoise_NEW); the
/// glow in depth added up exactly from some forty soft blobs (no ray marching); the stars one
/// mesh moved on the GPU. Nothing runs per frame on the CPU.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
[HierarchyBadge_NEW("RING GALAXY", "#7FA8FF")]
public class RingGalaxyVFX_NEW : MonoBehaviour
{
    [Header("Shape (radius 1 is this object's scale)")]
    [Tooltip("Which galaxy: where its knots fall, how its ring wobbles.")]
    [SerializeField] int seed = 0;

    [Tooltip("How fast the ring and what is in it go round, radians a second: slow, but to be seen going round in the time the light takes to reach it.")]
    [SerializeField] float turn = 0.02f;

    [Tooltip("The ring's radius, of the galaxy's.")]
    [Range(0.4f, 0.9f)] [SerializeField] float ringRadius = 0.72f;

    [Tooltip("The ring's half width, of the galaxy's radius.")]
    [Range(0.02f, 0.2f)] [SerializeField] float ringWidth = 0.085f;

    [Tooltip("How much brighter and wider the ring is on the far side from the intruder, 0 even all round.")]
    [Range(0f, 1f)] [SerializeField] float lopsided = 0.85f;

    [Tooltip("How egg-shaped the ring is, its long side away from the intruder.")]
    [Range(0f, 0.25f)] [SerializeField] float eggShape = 0.12f;

    [Tooltip("Where the intruder is, of the galaxy's radius, in this object's space.")]
    [SerializeField] Vector3 intruder = new Vector3(-0.98f, 0f, -0.05f);

    [Tooltip("The intruder's size: the half length of its bright body, of the galaxy's radius. Its tail runs on beyond.")]
    [Range(0.1f, 0.5f)] [SerializeField] float intruderSize = 0.3f;

    [Tooltip("How far the intruder's disc is turned about its long axis, degrees. The opposite of how steeply the light " +
             "looks at the ring's plane (the menu tips it 18° towards the light, and the light looks down at it about " +
             "17° more) keeps it edge on to the light: a streak with a dust lane.")]
    [Range(-90f, 90f)] [SerializeField] float intruderTilt = -35f;

    [Tooltip("The ring's and the disc's half thickness, of the radius.")]
    [Range(0.01f, 0.08f)] [SerializeField] float thickness = 0.03f;

    [Header("Motion")]
    [Tooltip("Seconds for a ripple to spread from the nucleus out past the ring. Two are always going, half this apart.")]
    [Min(1f)] [SerializeField] float rippleSeconds = 14f;
    [Tooltip("How bright the ripples are, and how much the ring and its knots flare as one crosses them. 0 none.")]
    [Range(0f, 2f)] [SerializeField] float ripples = 1f;
    [Tooltip("How much the stars twinkle. 0 steady.")]
    [Range(0f, 1f)] [SerializeField] float twinkle = 0.6f;
    [Tooltip("About how long each knot of star formation lasts, seconds, going round with the ring, before it fades and another lights up elsewhere.")]
    [Min(5f)] [SerializeField] float knotLife = 50f;

    [Header("Look")]
    [Tooltip("The giant knots of star formation along the ring.")]
    [Range(0f, 3f)] [SerializeField] float knots = 1f;
    [Tooltip("The faint spokes from the nucleus out to the ring.")]
    [Range(0f, 2f)] [SerializeField] float spokes = 0f;
    [Tooltip("The dim haze inside the ring.")]
    [Range(0f, 2f)] [SerializeField] float interior = 1f;
    [Tooltip("The plume of gas pulled out towards the intruder.")]
    [Range(0f, 2f)] [SerializeField] float plume = 1f;
    [Range(0f, 2f)] [SerializeField] float dust = 1f;
    [Range(0f, 3f)] [SerializeField] float brightness = 0.6f;
    [Tooltip("The glow in depth: the ring standing up as a band of light, the knots, the nucleus, the intruder, the bridge.")]
    [Range(0f, 4f)] [SerializeField] float depthGlow = 1f;

    [ColorUsage(false, true)] [SerializeField] Color nucleusColour = new Color(1.6f, 1.05f, 0.55f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color innerColour = new Color(0.3f, 0.38f, 0.62f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color spokeColour = new Color(0.45f, 0.55f, 0.85f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color ringColour = new Color(0.45f, 0.62f, 1.25f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color knotCoreColour = new Color(1.5f, 1.75f, 2.4f, 1f);
    [Tooltip("The glow of ionised hydrogen round the knots.")]
    [ColorUsage(false, true)] [SerializeField] Color knotGlowColour = new Color(1.4f, 0.6f, 1.1f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color plumeColour = new Color(0.35f, 0.5f, 0.95f, 1f);
    [SerializeField] Color dustColour = new Color(0.05f, 0.03f, 0.03f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color intruderColour = new Color(1.5f, 1.2f, 0.85f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color haloColour = new Color(0.3f, 0.32f, 0.5f, 1f);

    [Header("Stars")]
    [Tooltip("Young stars along the ring and crowded into its knots.")]
    [Range(0, 20000)] [SerializeField] int ringStars = 12000;
    [Tooltip("Soft pink blobs of glowing gas, a few to each knot.")]
    [Range(0, 500)] [SerializeField] int knotGlows = 140;
    [Tooltip("The old disc's stars, thickest round the nucleus.")]
    [Range(0, 20000)] [SerializeField] int oldStars = 3000;
    [Tooltip("Faint stars along the spokes, the intruder's tail, the bridge and the plume.")]
    [Range(0, 8000)] [SerializeField] int debrisStars = 3500;
    [Range(0, 12000)] [SerializeField] int intruderStars = 5000;
    [Range(0, 5000)] [SerializeField] int haloStars = 1200;
    [Tooltip("Their size, as a fraction of the radius. Never less than two pixels on screen.")]
    [Min(0f)] [SerializeField] float starSize = 0.0018f;
    [Tooltip("The largest a star is drawn, of half the screen's height — so one passing close is still a star.")]
    [Min(0.001f)] [SerializeField] float maxStarSize = 0.01f;
    [ColorUsage(false, true)] [SerializeField] Color ringStarColour = new Color(1.5f, 1.75f, 2.4f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color knotStarGlow = new Color(1.4f, 0.6f, 1.1f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color oldStarColour = new Color(0.75f, 0.6f, 0.42f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color debrisColour = new Color(0.45f, 0.6f, 1f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color intruderStarColour = new Color(1.2f, 1f, 0.75f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color haloStarColour = new Color(0.4f, 0.35f, 0.32f, 1f);

    const HideFlags Built = HideFlags.HideAndDontSave;

    // The box the glow is worked out in (it holds the intruder), and the sheet's reach.
    static readonly Vector3 VolumeBox = new Vector3(2.4f, 0.6f, 2.4f);
    const float SheetReach = 1.96f;

    readonly List<GameObject> _parts = new List<GameObject>();
    readonly List<Material> _materials = new List<Material>();
    readonly List<Mesh> _meshes = new List<Mesh>();
    Material _disc, _stars, _depthFar, _depthNear;
    int _builtStars = -1, _builtSeed;
    bool _rebuild, _dirty;

    /// <summary>The galaxy's radius, in world units.</summary>
    public float Radius => transform.lossyScale.x;

    int StarTotal => ringStars + knotGlows + oldStars + debrisStars + intruderStars + haloStars;

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

        Shader discShader = Resources.Load<Shader>("RingGalaxyDisc_NEW");
        Shader starShader = Resources.Load<Shader>("RingGalaxyStars_NEW");
        Shader depthShader = Resources.Load<Shader>("RingGalaxyVolume_NEW");
        if (discShader == null || starShader == null || depthShader == null)
        {
            Debug.LogWarning("[RingGalaxyVFX_NEW] Its shaders (Resources/RingGalaxy*_NEW.shader) are missing; nothing drawn.", this);
            return;
        }

        // The glow's far half under the face sheet, its near half over it (render queues).
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
        _stars = Part("Stars", Keep(Stars(new[] { ringStars, knotGlows, oldStars, debrisStars, intruderStars, haloStars }, seed)), starShader);
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
        var go = new GameObject("Ring Galaxy " + name) { hideFlags = Built };
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        var material = new Material(shader) { name = "Ring Galaxy " + name + " (runtime)", hideFlags = Built };
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
        // The shape, which every part shares (RingGalaxy_NEW.cginc).
        var ring = new Vector4(ringRadius, ringWidth, lopsided, eggShape);
        var comp = new Vector4(intruder.x, intruder.y, intruder.z, intruderSize);
        foreach (Material m in _materials)
        {
            m.SetFloat("_Seed", seed);
            m.SetFloat("_Turn", turn);
            m.SetVector("_Ring", ring);
            m.SetVector("_Comp", comp);
            m.SetFloat("_CompTilt", intruderTilt * Mathf.Deg2Rad);
            m.SetVector("_Ripple", new Vector4(Mathf.Max(rippleSeconds, 1f), ripples, twinkle, Mathf.Max(knotLife, 5f)));
        }

        _disc.SetTexture("_Noise", QuasarNoise_NEW.Texture);
        _disc.SetFloat("_Knots", knots);
        _disc.SetFloat("_Spokes", spokes);
        _disc.SetFloat("_Interior", interior);
        _disc.SetFloat("_Plume", plume);
        _disc.SetFloat("_Dust", dust);
        _disc.SetFloat("_Brightness", brightness);
        _disc.SetColor("_Core", nucleusColour);
        _disc.SetColor("_Inner", innerColour);
        _disc.SetColor("_Spoke", spokeColour);
        _disc.SetColor("_Gas", ringColour);
        _disc.SetColor("_Clump", knotCoreColour);
        _disc.SetColor("_Glow", knotGlowColour);
        _disc.SetColor("_PlumeColor", plumeColour);
        _disc.SetColor("_DustColor", dustColour);

        foreach (Material depth in new[] { _depthFar, _depthNear })
        {
            depth.SetVector("_Box", new Vector4(VolumeBox.x, VolumeBox.y, VolumeBox.z, 0f));
            depth.SetFloat("_Volume", depthGlow * brightness / 0.6f);
            depth.SetFloat("_Thickness", thickness);
            depth.SetColor("_Core", nucleusColour);
            depth.SetColor("_Inner", innerColour);
            depth.SetColor("_Gas", ringColour);
            depth.SetColor("_Glow", knotGlowColour);
            depth.SetColor("_CompColor", intruderColour);
            depth.SetColor("_PlumeColor", plumeColour);
            depth.SetColor("_Halo", haloColour);
        }

        _stars.SetFloat("_Size", starSize);
        _stars.SetFloat("_MaxSize", maxStarSize);
        _stars.SetColor("_RingStars", ringStarColour);
        _stars.SetColor("_Glow", knotStarGlow);
        _stars.SetColor("_OldStars", oldStarColour);
        _stars.SetColor("_Debris", debrisColour);
        _stars.SetColor("_CompStars", intruderStarColour);
        _stars.SetColor("_HaloStars", haloStarColour);
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
        var mesh = new Mesh { name = "Ring galaxy disc", vertices = vertices, triangles = triangles };
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(SheetReach * 2f, 0.2f, SheetReach * 2f));
        return mesh;
    }

    /// <summary>The box the glow is worked out in; its shader matches _Box to it.</summary>
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
        var mesh = new Mesh { name = "Ring galaxy depth", vertices = vertices, triangles = triangles };
        mesh.bounds = new Bounds(Vector3.zero, b * 2f);
        return mesh;
    }

    /// <summary>One quad per star, all at the origin: the shader moves them. Seeds and kind in UV1.
    /// counts: ring, knot glow, old, debris, intruder, halo (kinds 0..5).</summary>
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

        var mesh = new Mesh { name = "Ring galaxy stars" };
        if (vertices.Length > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.SetUVs(0, corners);
        mesh.SetUVs(1, seeds);
        mesh.triangles = triangles;
        // Everything the shader can put a star at: out to the intruder and the halo.
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(5f, 1.6f, 5f));
        return mesh;
    }
}

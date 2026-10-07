using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A quasar, as the artists' impressions draw one: a white-hot core; an accretion disc round
/// it, yellow inside, orange, deep red at the rim, in rings of gas with dark lanes of dust
/// between them, turning fast inside and slowly outside; sheets of red and purple gas
/// swirling round the disc in filaments; and two jets out along its axis, white at the
/// heart, blue round it, flowing outward.
///
/// Put it on an empty object. The disc lies in the object's XZ plane, the jets go along its
/// ±Y, and its radius is the object's scale — so tilt and size it with the transform. It
/// builds what it draws itself, as hidden children that are never saved, in the editor too,
/// so it can be placed by eye. Tools > Journey NEW > Add Quasar VFX to Open Scene puts one
/// in the journey where the old sphere was.
///
/// HOW IT IS DRAWN. All procedural, nothing to import:
///   disc   An annulus (QuasarDisc_NEW). Each pixel works out how far out and which way round
///          it is, and colours itself from that; the spiral shears with the Keplerian turn.
///   gas    More annuli, larger and tilted a little off the disc, the same shader keeping only
///          its brightest strands. Additive, so their order against each other never shows.
///   core   A quad turned to the camera (QuasarCore_NEW), with the black hole's shadow.
///   jets   Strips along ±Y turned round their own axis to face the camera (QuasarJet_NEW).
///          Whichever is on the far side of the disc from a camera is drawn before the disc,
///          so the disc's dust hides it, and the near one after.
///
/// IT FADES WITH ITS WORLD. Every shader takes _Color, which WorldSwitcher_NEW and the dives
/// fade a world by, and it is built before WorldSwitcher_NEW gathers its groups' renderers
/// (it runs first, by execution order), so a quasar inside a render group comes and goes —
/// and is scaled by a dive — with the rest of it.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
[HierarchyBadge_NEW("QUASAR", "#F08A3C")]
public class QuasarVFX_NEW : MonoBehaviour
{
    [Header("Accretion disc (its radius is this object's scale)")]
    [Tooltip("The inner edge, as a fraction of the radius. Inside it is the core.")]
    [Range(0.02f, 0.4f)] [SerializeField] float innerEdge = 0.07f;

    [Tooltip("Turns a second at the inner edge. Further out it turns slower, as r^-1.5.")]
    [SerializeField] float spin = 0.12f;

    [Tooltip("How far the gas spirals as it falls in.")]
    [SerializeField] float twist = 2.2f;

    [Tooltip("Rings of gas across the disc.")]
    [Min(1f)] [SerializeField] float rings = 12f;

    [Tooltip("Dark lanes of dust between the rings.")]
    [Range(0f, 1f)] [SerializeField] float dust = 0.75f;

    [Tooltip("How far the rim breaks up into tendrils.")]
    [Range(0f, 1f)] [SerializeField] float tendrils = 0.6f;

    [ColorUsage(false, true)] [SerializeField] Color hot = new Color(2.6f, 2f, 1.2f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color middle = new Color(1.7f, 0.42f, 0.05f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color rim = new Color(0.5f, 0.035f, 0.015f, 1f);
    [SerializeField] Color dustColour = new Color(0.03f, 0.01f, 0.005f, 1f);

    [Range(0f, 3f)] [SerializeField] float discBrightness = 0.8f;

    [Header("Gas round it")]
    [Tooltip("Sheets of gas swirling round the disc, each a little more tilted. 0 = none.")]
    [Range(0, 6)] [SerializeField] int gasSheets = 3;

    [Tooltip("How far out the gas reaches, in disc radii.")]
    [Min(1f)] [SerializeField] float gasReach = 2.6f;

    [Tooltip("How much more each sheet is tilted off the disc than the one before, in degrees.")]
    [SerializeField] float gasTilt = 14f;

    [ColorUsage(false, true)] [SerializeField] Color gasInner = new Color(1f, 0.16f, 0.04f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color gasOuter = new Color(0.28f, 0.03f, 0.2f, 1f);

    [Range(0f, 2f)] [SerializeField] float gasBrightness = 0.55f;

    [Tooltip("1 keeps only the gas's brightest strands — filaments; lower fills in a haze.")]
    [Range(0f, 1f)] [SerializeField] float gasWisps = 0.85f;

    [Header("Core")]
    [Tooltip("The core's glow, in disc radii.")]
    [Min(0.01f)] [SerializeField] float coreSize = 0.35f;

    [ColorUsage(false, true)] [SerializeField] Color coreHot = new Color(4f, 3.7f, 3.2f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color coreGlow = new Color(1.1f, 0.6f, 0.25f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color coreHalo = new Color(0.3f, 0.1f, 0.03f, 1f);

    [Tooltip("The black hole's shadow at the very middle, as a fraction of the core's size. 0 = none.")]
    [Range(0f, 0.2f)] [SerializeField] float shadow = 0.03f;

    [Header("Jets")]
    [Tooltip("Their length, in disc radii. 0 = none.")]
    [Min(0f)] [SerializeField] float jetLength = 2.6f;

    [Tooltip("Their width at the far end, as a fraction of their length.")]
    [Range(0.005f, 0.2f)] [SerializeField] float jetWidth = 0.035f;

    [ColorUsage(false, true)] [SerializeField] Color jetHeart = new Color(2.6f, 2.9f, 3.2f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color jetSheath = new Color(0.35f, 0.55f, 1.1f, 1f);

    [Range(0f, 3f)] [SerializeField] float jetBrightness = 1f;

    [Tooltip("The jet on the far side of the disc, against the near one.")]
    [Range(0f, 1f)] [SerializeField] float counterJet = 0.5f;

    [Tooltip("How fast the knots flow out, in lengths a second.")]
    [SerializeField] float jetFlow = 0.25f;

    [Header("Variation")]
    [Tooltip("Another quasar looks different with another seed.")]
    [SerializeField] int seed = 0;

    // Built on enable, gone on disable; never saved, never shown in the hierarchy.
    const HideFlags Built = HideFlags.HideAndDontSave;

    // Transparent is 3000. The disc in the middle; the gas and the far jet before it, so its
    // dust darkens them; the core and the near jet after.
    const int FarJetQueue = 2998, GasQueue = 2999, DiscQueue = 3000, CoreQueue = 3001, NearJetQueue = 3002;

    readonly List<GameObject> _parts = new List<GameObject>();
    readonly List<Material> _materials = new List<Material>();
    readonly List<Material> _gas = new List<Material>();
    Mesh _discMesh, _quadMesh, _jetMesh;
    Material _disc, _core, _jet, _counterJet;
    Transform _jetPart, _counterJetPart;
    int _builtSheets = -1;
    bool _rebuild, _dirty;

    void OnEnable()
    {
        Build();
        Camera.onPreCull += OrderJets;
    }

    void OnDisable()
    {
        Camera.onPreCull -= OrderJets;
        Clear();
    }

    void OnValidate()
    {
        // Objects may not be made or destroyed from here, and transforms are better left alone:
        // note what changed, and Update does it.
        _rebuild |= _builtSheets >= 0 && _builtSheets != gasSheets;
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

    /// <summary>
    /// Before each camera draws: the jet on its side of the disc after the disc, the other
    /// before it.
    /// </summary>
    void OrderJets(Camera cam)
    {
        if (_jet == null || _counterJet == null || cam == null) return;
        bool above = Vector3.Dot(cam.transform.position - transform.position, transform.up) >= 0f;
        _jet.renderQueue = above ? NearJetQueue : FarJetQueue;
        _counterJet.renderQueue = above ? FarJetQueue : NearJetQueue;
    }

    // ── Building ────────────────────────────────────────────────────────────

    void Build()
    {
        if (_disc != null) return;

        Shader discShader = Resources.Load<Shader>("QuasarDisc_NEW");
        Shader coreShader = Resources.Load<Shader>("QuasarCore_NEW");
        Shader jetShader = Resources.Load<Shader>("QuasarJet_NEW");
        if (discShader == null || coreShader == null || jetShader == null)
        {
            Debug.LogWarning("[QuasarVFX_NEW] Its shaders (Resources/Quasar*_NEW.shader) are missing; nothing drawn.", this);
            return;
        }

        _discMesh = Annulus(160, 24);
        _quadMesh = Quad();
        _jetMesh = Strip(32);

        _disc = Part("Disc", _discMesh, discShader, DiscQueue, Quaternion.identity, Vector3.one);

        _builtSheets = gasSheets;
        for (int s = 0; s < gasSheets; s++)
        {
            float tilt = (s - (gasSheets - 1) * 0.5f) * gasTilt;
            Quaternion turn = Quaternion.Euler(tilt, s * 47f, tilt * 0.6f);
            _gas.Add(Part("Gas " + s, _discMesh, discShader, GasQueue, turn, Vector3.one * (gasReach * (1f - 0.12f * s))));
        }

        _core = Part("Core", _quadMesh, coreShader, CoreQueue, Quaternion.identity, Vector3.one);
        _jet = Part("Jet", _jetMesh, jetShader, NearJetQueue, Quaternion.identity, Vector3.one);
        _jetPart = _parts[_parts.Count - 1].transform;
        _counterJet = Part("Counter jet", _jetMesh, jetShader, FarJetQueue, Quaternion.Euler(180f, 0f, 0f), Vector3.one);
        _counterJetPart = _parts[_parts.Count - 1].transform;

        Apply();
    }

    Material Part(string name, Mesh mesh, Shader shader, int queue, Quaternion rotation, Vector3 scale)
    {
        var go = new GameObject("Quasar " + name) { hideFlags = Built };
        go.layer = gameObject.layer;
        Transform t = go.transform;
        t.SetParent(transform, false);
        t.localPosition = Vector3.zero;
        t.localRotation = rotation;
        t.localScale = scale;

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        var material = new Material(shader) { name = "Quasar " + name + " (runtime)", hideFlags = Built, renderQueue = queue };
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
        Kill(_discMesh);
        Kill(_quadMesh);
        Kill(_jetMesh);
        _parts.Clear();
        _materials.Clear();
        _gas.Clear();
        _disc = _core = _jet = _counterJet = null;
        _jetPart = _counterJetPart = null;
        _discMesh = _quadMesh = _jetMesh = null;
        _builtSheets = -1;
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
        SetDisc(_disc, innerEdge, spin, twist, rings, 2f, dust, tendrils, 2f, 0f, 0.7f,
                hot * discBrightness, middle * discBrightness, rim * discBrightness, dustColour, seed);

        // The gas: only its brightest strands, with no dust and nothing hidden behind it.
        Color gasMiddle = Color.Lerp(gasInner, gasOuter, 0.4f);
        for (int s = 0; s < _gas.Count; s++)
            SetDisc(_gas[s], 0.32f, 0.03f, 3f, 7f, 2.5f, 0f, 1f, 0f, gasWisps, 0f,
                    gasInner * gasBrightness, gasMiddle * gasBrightness, gasOuter * gasBrightness, Color.black,
                    seed + 13.7f * (s + 1));

        _core.SetFloat("_Size", coreSize);
        _core.SetColor("_Hot", coreHot);
        _core.SetColor("_Glow", coreGlow);
        _core.SetColor("_Halo", coreHalo);
        _core.SetFloat("_Shadow", shadow);

        // No jets is black jets: additive, so they draw nothing.
        float jets = jetLength > 0f ? jetBrightness : 0f;
        SetJet(_jet, jets, seed);
        SetJet(_counterJet, jets * counterJet, seed + 7.3f);
        Vector3 length = new Vector3(1f, Mathf.Max(1e-3f, jetLength), 1f);
        _jetPart.localScale = length;
        _counterJetPart.localScale = length;

        // The core and the jets are turned to the camera in their shaders, so their bounds
        // have to hold them whichever way they face: the core a disc of coreSize, the jets
        // up to jetWidth times their length either side of the axis.
        _quadMesh.bounds = new Bounds(Vector3.zero, Vector3.one * (3f * coreSize));
        float across = 2.4f * jetWidth * Mathf.Max(1e-3f, jetLength);
        _jetMesh.bounds = new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(across, 1f, across));
    }

    void SetJet(Material m, float brightness, float jetSeed)
    {
        m.SetFloat("_Width", jetWidth);
        m.SetColor("_Core", jetHeart * brightness);
        m.SetColor("_Sheath", jetSheath * brightness);
        m.SetFloat("_Flow", jetFlow);
        m.SetFloat("_Seed", jetSeed);
    }

    static void SetDisc(Material m, float inner, float turnRate, float spiral, float ringCount, float streaks,
                        float dustLanes, float rimBreakUp, float rimLight, float wisps, float opacity,
                        Color inside, Color between, Color outside, Color dustTint, float discSeed)
    {
        m.SetFloat("_Inner", inner);
        m.SetFloat("_Spin", turnRate);
        m.SetFloat("_Twist", spiral);
        m.SetFloat("_Rings", ringCount);
        m.SetFloat("_Streaks", streaks);
        m.SetFloat("_Dust", dustLanes);
        m.SetFloat("_Tendrils", rimBreakUp);
        m.SetFloat("_Rim", rimLight);
        m.SetFloat("_Wisps", wisps);
        m.SetFloat("_Opacity", opacity);
        m.SetColor("_Hot", inside);
        m.SetColor("_Mid", between);
        m.SetColor("_Outer", outside);
        m.SetColor("_DustColor", dustTint);
        m.SetFloat("_Seed", discSeed);
    }

    // ── Meshes ──────────────────────────────────────────────────────────────

    /// <summary>A flat disc of radius 1 in XZ, in rings, so the fade at its edges has vertices to follow.</summary>
    static Mesh Annulus(int around, int across)
    {
        var vertices = new List<Vector3> { Vector3.zero };
        var triangles = new List<int>();
        for (int j = 1; j <= across; j++)
        {
            float r = (float)j / across;
            for (int i = 0; i < around; i++)
            {
                float a = i * Mathf.PI * 2f / around;
                vertices.Add(new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
            }
        }

        for (int i = 0; i < around; i++)
        {
            int next = (i + 1) % around;
            triangles.Add(0); triangles.Add(1 + next); triangles.Add(1 + i);
        }
        for (int j = 1; j < across; j++)
        {
            int ring = 1 + (j - 1) * around, outer = ring + around;
            for (int i = 0; i < around; i++)
            {
                int next = (i + 1) % around;
                triangles.Add(ring + i); triangles.Add(ring + next); triangles.Add(outer + next);
                triangles.Add(ring + i); triangles.Add(outer + next); triangles.Add(outer + i);
            }
        }

        var mesh = new Mesh { name = "Quasar disc", hideFlags = Built };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh Quad()
    {
        var mesh = new Mesh { name = "Quasar core", hideFlags = Built };
        mesh.vertices = new[] { new Vector3(-1f, -1f, 0f), new Vector3(1f, -1f, 0f), new Vector3(1f, 1f, 0f), new Vector3(-1f, 1f, 0f) };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        return mesh;
    }

    /// <summary>A strip along +Y from 0 to 1, x -1 or 1 across it; the shader turns it to the camera.</summary>
    static Mesh Strip(int steps)
    {
        var vertices = new Vector3[(steps + 1) * 2];
        var triangles = new int[steps * 6];
        for (int i = 0; i <= steps; i++)
        {
            float y = (float)i / steps;
            vertices[i * 2] = new Vector3(-1f, y, 0f);
            vertices[i * 2 + 1] = new Vector3(1f, y, 0f);
        }
        for (int i = 0; i < steps; i++)
        {
            int v = i * 2, t = i * 6;
            triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
            triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
        }
        var mesh = new Mesh { name = "Quasar jet", hideFlags = Built };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        return mesh;
    }
}

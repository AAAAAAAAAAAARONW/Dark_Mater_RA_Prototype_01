using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Milky Way from outside, as the face-on maps draw it: a warm bar and bulge of old stars,
/// four logarithmic spiral arms of young blue-white ones — Scutum-Centaurus and Perseus from
/// the ends of the bar, Sagittarius and Norma fainter between — strung with pink star-forming
/// regions and lined with dust, and the short Orion spur the Sun sits on.
///
/// Put it on an empty object. The disc lies in the object's XZ plane, its radius is the
/// object's scale, and the Sun is at local (sunRadius, 0, 0) — so the object's +X is the
/// direction from the galaxy's centre to the Sun. Tools > Journey NEW > Add Milky Way VFX to
/// Open Scene places it with the Sun on SolarDivePoint, so the dive into the Solar System
/// starts from where the Sun really is.
///
/// THE NUMBERS ARE THE GALAXY'S. Arms pitched 12.5°; a bar about a third of the Sun's
/// distance long, 27° off the Sun's line; an exponential old disc; the Sun 0.39 of the way
/// out, between Sagittarius just inside it and Perseus outside — 26,000 of the 65,000 light
/// years the reference maps run to.
///
/// WHAT MOVES. The gas and dust stream through the arms along their orbits; star-forming
/// regions and young clusters flicker; the core breathes; stars are born on the arms with a
/// flash. The arms themselves are a density wave, and the frame here turns with the Sun, which is
/// close to their corotation, so in it they hardly move and the Sun stays on SolarDivePoint.
/// The stars move: born on the arms, they orbit on a flat rotation curve, inside the Sun's
/// orbit pulling ahead of the arms and outside falling behind, fading and being born again.
///
/// CHEAP. The disc is one quad worked out per pixel in one pass, with a handful of reads of
/// the shared noise texture (QuasarNoise_NEW) — no volume, no ray march. The bulge is one
/// quad; the stars are one mesh moved on the GPU. Nothing runs per frame on the CPU.
///
/// It builds what it draws as hidden children that are never saved, in the editor too, and
/// fades with its world through _Color, built before WorldSwitcher_NEW gathers renderers.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
[HierarchyBadge_NEW("MILKY WAY", "#8FB4FF")]
public class GalaxyVFX_NEW : MonoBehaviour
{
    [Header("Structure (radius 1 is this object's scale)")]
    [Tooltip("Where the Sun is, as a fraction of the radius. 0.39: 26,000 light years of 65,000.")]
    [Range(0.2f, 0.7f)] [SerializeField] float sunRadius = 0.39f;

    [Tooltip("How tightly the arms wind, in degrees. The Milky Way's are about 12°.")]
    [Range(5f, 25f)] [SerializeField] float pitch = 12.5f;

    [Tooltip("Arm width at the centre, and how much it grows to the rim.")]
    [SerializeField] Vector2 armWidth = new Vector2(0.022f, 0.04f);

    [Tooltip("The two major arms, the two minor ones, and the Orion spur.")]
    [SerializeField] Vector3 armStrength = new Vector3(1f, 0.4f, 0.45f);

    [Tooltip("The bar's angle off the Sun's line in degrees, its half length and half width.")]
    [SerializeField] Vector3 bar = new Vector3(27f, 0.24f, 0.075f);

    [Tooltip("The old disc's exponential scale length, of the radius.")]
    [Range(0.05f, 0.4f)] [SerializeField] float scaleLength = 0.16f;

    [Header("Look")]
    [Range(0f, 2f)] [SerializeField] float dust = 0.85f;
    [Tooltip("The pink star-forming regions along the arms.")]
    [Range(0f, 3f)] [SerializeField] float starFormation = 1.3f;
    [Tooltip("Young star clusters, as specks.")]
    [Range(0f, 2f)] [SerializeField] float clusters = 0.7f;
    [Tooltip("Light between the arms.")]
    [Range(0f, 1f)] [SerializeField] float haze = 0.32f;
    [Range(0f, 3f)] [SerializeField] float brightness = 0.6f;

    [Header("Motion")]
    [Tooltip("How fast the gas and dust stream along their orbits through the arms. The arms " +
             "themselves hold still: they are a density wave.")]
    [SerializeField] float gasFlow = 0.012f;
    [Tooltip("Star-forming regions and young clusters flickering.")]
    [Range(0f, 1f)] [SerializeField] float twinkle = 0.6f;

    [ColorUsage(false, true)] [SerializeField] Color bulgeColour = new Color(1.6f, 1.25f, 0.85f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color oldDisc = new Color(1f, 0.78f, 0.52f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color youngStars = new Color(0.55f, 0.70f, 1.05f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color outerDisc = new Color(0.30f, 0.38f, 0.62f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color hiiRegions = new Color(1.7f, 0.42f, 0.75f, 1f);
    [SerializeField] Color dustColour = new Color(0.05f, 0.03f, 0.025f, 1f);

    [Header("Bulge, seen at a slant")]
    [Tooltip("The round glow that keeps the bulge standing out of the disc at a slant.")]
    [Min(0f)] [SerializeField] float bulgeSize = 0.16f;
    [ColorUsage(false, true)] [SerializeField] Color bulgeGlow = new Color(0.55f, 0.42f, 0.28f, 1f);

    [Header("Stars")]
    [Tooltip("Young stars, born on the arms.")]
    [Range(0, 20000)] [SerializeField] int youngStarCount = 5000;
    [Tooltip("Old stars in the bulge.")]
    [Range(0, 10000)] [SerializeField] int bulgeStarCount = 600;
    [Tooltip("Their size, as a fraction of the radius. Never less than a pixel and a half on screen.")]
    [Min(0f)] [SerializeField] float starSize = 0.0035f;
    [Tooltip("Orbital speed on the flat rotation curve, radii a second. Slow: it is a galaxy.")]
    [SerializeField] float orbitalSpeed = 0.01f;
    [ColorUsage(false, true)] [SerializeField] Color youngStarColour = new Color(1.2f, 1.4f, 2f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color bulgeStarColour = new Color(1.6f, 1.2f, 0.75f, 1f);

    [SerializeField] int seed = 0;

    const HideFlags Built = HideFlags.HideAndDontSave;

    readonly List<GameObject> _parts = new List<GameObject>();
    readonly List<Material> _materials = new List<Material>();
    readonly List<Mesh> _meshes = new List<Mesh>();
    Material _disc, _bulge, _stars;
    Mesh _starMesh;
    int _builtStars = -1;
    bool _rebuild, _dirty;

    /// <summary>The disc's radius, in world units.</summary>
    public float Radius => transform.lossyScale.x;

    /// <summary>The Sun's position, in world space.</summary>
    public Vector3 SunPosition => transform.TransformPoint(new Vector3(sunRadius, 0f, 0f));

    /// <summary>The Sun's distance from the centre, as a fraction of the radius.</summary>
    public float SunRadius => sunRadius;

    void OnEnable() => Build();

    void OnDisable() => Clear();

    void OnValidate()
    {
        _rebuild |= _builtStars >= 0 && _builtStars != youngStarCount + bulgeStarCount;
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

        Shader discShader = Resources.Load<Shader>("GalaxyDisc_NEW");
        Shader bulgeShader = Resources.Load<Shader>("GalaxyBulge_NEW");
        Shader starShader = Resources.Load<Shader>("GalaxyStars_NEW");
        if (discShader == null || bulgeShader == null || starShader == null)
        {
            Debug.LogWarning("[GalaxyVFX_NEW] Its shaders (Resources/Galaxy*_NEW.shader) are missing; nothing drawn.", this);
            return;
        }

        _disc = Part("Disc", Keep(Plane()), discShader);
        _bulge = Part("Bulge", Keep(Quad()), bulgeShader);
        _builtStars = youngStarCount + bulgeStarCount;
        _starMesh = Keep(Stars(youngStarCount, bulgeStarCount, seed));
        _stars = Part("Stars", _starMesh, starShader);

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
        var go = new GameObject("Milky Way " + name) { hideFlags = Built };
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        var material = new Material(shader) { name = "Milky Way " + name + " (runtime)", hideFlags = Built };
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
        _disc = _bulge = _stars = null;
        _starMesh = null;
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

        _disc.SetTexture("_Noise", noise);
        _disc.SetFloat("_SunRadius", sunRadius);
        _disc.SetFloat("_Pitch", pitch);
        _disc.SetVector("_ArmWidth", armWidth);
        _disc.SetVector("_ArmStrength", armStrength);
        _disc.SetVector("_Bar", bar);
        _disc.SetFloat("_ScaleLength", scaleLength);
        _disc.SetFloat("_Dust", dust);
        _disc.SetFloat("_HII", starFormation);
        _disc.SetFloat("_Sparkle", clusters);
        _disc.SetFloat("_Haze", haze);
        _disc.SetFloat("_Brightness", brightness);
        _disc.SetFloat("_Flow", gasFlow);
        _disc.SetFloat("_Twinkle", twinkle);
        _disc.SetColor("_Core", bulgeColour);
        _disc.SetColor("_Old", oldDisc);
        _disc.SetColor("_Young", youngStars);
        _disc.SetColor("_OuterColor", outerDisc);
        _disc.SetColor("_Pink", hiiRegions);
        _disc.SetColor("_DustColor", dustColour);

        _bulge.SetFloat("_Size", bulgeSize);
        _bulge.SetColor("_Glow", bulgeGlow * brightness / 0.6f);
        _bulge.SetColor("_Hot", bulgeColour * 0.55f * brightness / 0.6f);

        _stars.SetTexture("_Noise", noise);
        _stars.SetFloat("_SunRadius", sunRadius);
        _stars.SetFloat("_Pitch", pitch);
        _stars.SetFloat("_Speed", orbitalSpeed);
        _stars.SetFloat("_Size", starSize);
        _stars.SetColor("_Young", youngStarColour);
        _stars.SetColor("_OldStars", bulgeStarColour);
    }

    // ── Meshes ──────────────────────────────────────────────────────────────

    /// <summary>A square in XZ just larger than the disc; the shader draws nothing outside radius 1.05.</summary>
    static Mesh Plane()
    {
        const float e = 1.06f;
        var mesh = new Mesh { name = "Milky Way disc" };
        mesh.vertices = new[] { new Vector3(-e, 0f, -e), new Vector3(e, 0f, -e), new Vector3(e, 0f, e), new Vector3(-e, 0f, e) };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>A unit quad; the bulge's shader sizes it and turns it to the camera.</summary>
    static Mesh Quad()
    {
        var mesh = new Mesh { name = "Milky Way bulge" };
        mesh.vertices = new[] { new Vector3(-1f, -1f, 0f), new Vector3(1f, -1f, 0f), new Vector3(1f, 1f, 0f), new Vector3(-1f, 1f, 0f) };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 0.6f);
        return mesh;
    }

    /// <summary>One quad per star, all at the origin: the shader moves them. Seeds and kind in UV1.</summary>
    static Mesh Stars(int young, int bulge, int starSeed)
    {
        int count = young + bulge;
        var vertices = new Vector3[count * 4];
        var corners = new List<Vector2>(count * 4);
        var seeds = new List<Vector4>(count * 4);
        var triangles = new int[count * 6];
        var random = new System.Random(4271 + starSeed);

        for (int i = 0; i < count; i++)
        {
            var s = new Vector4((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble(), i < young ? 0f : 1f);
            corners.Add(new Vector2(-1f, -1f)); corners.Add(new Vector2(1f, -1f));
            corners.Add(new Vector2(1f, 1f)); corners.Add(new Vector2(-1f, 1f));
            for (int c = 0; c < 4; c++) seeds.Add(s);

            int v = i * 4, t = i * 6;
            triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
            triangles[t + 3] = v; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
        }

        var mesh = new Mesh { name = "Milky Way stars" };
        if (vertices.Length > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.SetUVs(0, corners);
        mesh.SetUVs(1, seeds);
        mesh.triangles = triangles;
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(2.2f, 0.4f, 2.2f));
        return mesh;
    }
}

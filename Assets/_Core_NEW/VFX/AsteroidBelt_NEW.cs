using UnityEngine;

/// <summary>
/// The asteroid belt between Mars and Jupiter: a few thousand lumpy rocks, lit from the Sun,
/// each tumbling slowly, and a denser scatter where the light's line crosses the belt so that
/// some go past close as the light flies through.
///
/// HOW. One particle system of mesh particles, so the rest of the journey handles it as it
/// handles any other part of the Solar System: WorldSwitcher_NEW fades it with the group and
/// pauses it while hidden, and because it simulates in local space and scales with the
/// hierarchy, LayerDive_NEW's dive into the system and EarthZoom_NEW's growth carry it with
/// the planets. Nothing is emitted: the rocks are placed once, on Start, from seed, and live
/// for good. The meshes are made here — three bent icospheres — and the material is
/// Resources/AsteroidRock_NEW.shader, lit from the Sun's position like the planets.
///
/// WHERE. In AU, squeezed as the planets' orbits are. Every planet here is at Earth's
/// distance times its true distance in AU to the power 0.4 (Mercury to Neptune all fit), so
/// the belt is placed the same way: that power is measured from Earth and Jupiter (outer, at
/// 5.2 AU), and the belt runs from fromAU to toAU, 2.1 to 3.3. Heights above the plane are
/// squeezed by the same amount as distances are there, so the belt keeps its true shape in
/// the squeezed picture. Rock sizes are fractions of Earth's diameter, so they follow it when the
/// bodies are resized (Tools > Journey NEW > Solar System > Sizes from NASA).
///
/// AS IT IS, mostly: a doughnut, orbits tilted by about orbitTilt; the Kirkwood gaps Jupiter
/// clears at 2.50, 2.82 and 2.95 AU (wider than life, to be seen); most rocks as dark as
/// charcoal, a quarter stony and lighter; most small; and the two largest, Ceres and Vesta,
/// true to the planets here but for largestBoost. What is NOT as it is: how many and how close.
///
/// ALONG THE LIGHT. The light comes into the system from outside and crosses the belt on its
/// way in to Earth. pathCount more rocks are scattered round its line there, between
/// pathClear and pathSpread of it. None go above the line within keepAbove degrees of
/// straight up: the camera rides above and behind the light, and EarthZoom_NEW's growth
/// sweeps everything near the line out past it.
///
/// DUST. At the Solar System's scale a rock is a pixel or two from across the system — Mars
/// is hardly more — so the belt is also a few thousand faint, soft, added specks on a child
/// made at start (Resources/AsteroidDust_NEW.shader). From afar they are what reads as a band.
///
/// It is not real: the real belt is so sparse a probe flying through sees nothing. This is
/// what it is drawn as.
///
/// Put it on a child of the Solar System's group root; Tools > Journey NEW > Solar System >
/// Add Asteroid Belt makes one. It centres itself on the Sun.
/// </summary>
[DefaultExecutionOrder(-300)]   // before WorldSwitcher_NEW reads the group's materials
[DisallowMultipleComponent]
[RequireComponent(typeof(ParticleSystem))]
[HierarchyBadge_NEW("ASTEROIDS", "#B8A48C")]
public class AsteroidBelt_NEW : MonoBehaviour
{
    [Header("Wiring (siblings by name if empty)")]
    [SerializeField] Transform sun;

    [Tooltip("Jupiter, at 5.2 AU: with Earth, it gives how the orbits here are squeezed. 'Jupiter' if empty.")]
    [SerializeField] Transform outer;

    [Tooltip("The light's line goes through Earth's centre. 'Earth' if empty.")]
    [SerializeField] Transform earth;

    [Tooltip("The photon, for the way the light flies. Found if empty.")]
    [SerializeField] PlayerRig_NEW photon;

    [Header("Belt")]
    [Tooltip("Where the belt starts, AU from the Sun.")]
    [SerializeField] float fromAU = 2.1f;

    [Tooltip("Where it ends, AU.")]
    [SerializeField] float toAU = 3.3f;

    [Min(0)] [SerializeField] int count = 3000;

    [Tooltip("How tilted the rocks' orbits are, degrees (one standard deviation). Most of the real " +
             "belt's are under 20.")]
    [Range(0f, 30f)] [SerializeField] float orbitTilt = 8f;

    [Tooltip("Smallest and biggest rock across, as a fraction of Earth's diameter. Most are small; " +
             "Ceres and Vesta, below, are bigger than all of them.")]
    [SerializeField] Vector2 rockSizeOfEarth = new Vector2(0.02f, 0.13f);

    [Tooltip("Fastest tumble, degrees a second.")]
    [Min(0f)] [SerializeField] float tumble = 20f;

    [Header("Kirkwood gaps")]
    [Tooltip("Leave the gaps Jupiter clears, at 2.50, 2.82 and 2.95 AU.")]
    [SerializeField] bool kirkwoodGaps = true;

    [Tooltip("How wide each gap is either side of its middle, in AU. Wider than life, to be seen.")]
    [Range(0f, 0.1f)] [SerializeField] float gapHalfWidth = 0.035f;

    [Header("Ceres and Vesta")]
    [Tooltip("Put in the two largest: Ceres (2.77 AU, dark) and Vesta (2.36 AU, bright).")]
    [SerializeField] bool largest = true;

    [Tooltip("How many times larger than true to the planets' scale here they are drawn. At 1, Ceres " +
             "is 7% of Earth across.")]
    [Min(0.1f)] [SerializeField] float largestBoost = 3f;

    [Header("Along the light")]
    [Min(0)] [SerializeField] int pathCount = 250;

    [Tooltip("No rock is nearer the light's line than this.")]
    [Min(0f)] [SerializeField] float pathClear = 4f;

    [Tooltip("How far out from the line they are scattered.")]
    [Min(0f)] [SerializeField] float pathSpread = 20f;

    [Tooltip("Likewise, the rocks along the light: a little bigger, still smaller than Ceres.")]
    [SerializeField] Vector2 pathRockSizeOfEarth = new Vector2(0.05f, 0.2f);

    [Tooltip("Half-angle of the wedge above the line kept empty, for the camera.")]
    [Range(0f, 180f)] [SerializeField] float keepAbove = 60f;

    [Header("Look")]
    [Tooltip("Most asteroids (C-type) are as dark as charcoal.")]
    [SerializeField] Color darkColour = new Color(0.2f, 0.19f, 0.18f, 1f);

    [Tooltip("The stony ones (S-type): lighter, a little red.")]
    [SerializeField] Color stonyColour = new Color(0.5f, 0.43f, 0.35f, 1f);

    [Tooltip("How many are stony. About a sixth to a third, more of them nearer the Sun.")]
    [Range(0f, 1f)] [SerializeField] float stonyFraction = 0.25f;

    [Tooltip("How much the rocks differ in brightness and tint.")]
    [Range(0f, 1f)] [SerializeField] float variation = 0.3f;

    [SerializeField] float sunlight = 1.8f;

    [Range(0f, 0.3f)] [SerializeField] float fill = 0.04f;

    [SerializeField] int seed = 1;

    [Header("Dust")]
    [Tooltip("Faint soft specks over the same belt. At the Solar System's scale the rocks are a pixel or " +
             "two from across the system; the dust is what reads as a band from there.")]
    [Min(0)] [SerializeField] int speckCount = 12000;

    [SerializeField] Vector2 speckSize = new Vector2(0.5f, 1.5f);

    [SerializeField] Color dustColour = new Color(0.55f, 0.5f, 0.45f, 1f);

    [Tooltip("How bright each speck is at its middle.")]
    [Range(0f, 1f)] [SerializeField] float speckStrength = 0.25f;

    [SerializeField] bool debugLog = false;

    static readonly int SunId = Shader.PropertyToID("_SunPosition");

    ParticleSystem _system, _dust;
    Material _material, _dustMaterial;
    System.Random _rng;

    // The light's line in this object's space, and a frame round it.
    bool _hasLine;
    Vector3 _lineAt, _lineDir, _lineSide, _lineUp;

    // Earth's distance from the Sun here, and the power the orbits are squeezed by.
    float _earthOrbit = 100f, _squeeze = 1f;
    static readonly float[] GapsAU = { 2.50f, 2.82f, 2.95f };

    void Awake()
    {
        Transform system = transform.parent;
        if (system != null)
        {
            if (sun == null) sun = system.Find("Sun");
            if (outer == null) outer = system.Find("Jupiter");
            if (earth == null) earth = system.Find("Earth");
        }
        if (photon == null) photon = FindObjectOfType<PlayerRig_NEW>();

        _system = GetComponent<ParticleSystem>();
        Configure();
        ConfigureDust();
    }

    void Start()
    {
        if (sun == null)
        {
            Debug.LogWarning("[AsteroidBelt_NEW] No Sun found beside '" + name + "'; no belt.", this);
            return;
        }

        transform.localPosition = sun.localPosition;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;

        Place();
    }

    void LateUpdate()
    {
        if (_material != null && sun != null) _material.SetVector(SunId, sun.position);
    }

    void OnDestroy()
    {
        if (_material != null) Destroy(_material);
        if (_dustMaterial != null) Destroy(_dustMaterial);
    }

    // ── Set-up ───────────────────────────────────────────────────────────────

    void Configure()
    {
        ParticleSystem.MainModule main = _system.main;
        main.loop = true;
        main.duration = 1000f;
        main.playOnAwake = false;
        main.startLifetime = 1e8f;
        main.startSpeed = 0f;
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.startRotation3D = true;
        main.startSize3D = true;
        main.maxParticles = count + pathCount + 2;

        ParticleSystem.EmissionModule emission = _system.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = _system.shape;
        shape.enabled = false;

        var renderer = GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.alignment = ParticleSystemRenderSpace.Local;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.enableGPUInstancing = true;

        var rng = new System.Random(seed);
        renderer.SetMeshes(new[] { Rock(rng, 0.35f), Rock(rng, 0.25f), Rock(rng, 0.45f) });

        Shader shader = Resources.Load<Shader>("AsteroidRock_NEW");
        if (shader == null)
        {
            Debug.LogWarning("[AsteroidBelt_NEW] Resources/AsteroidRock_NEW.shader is missing; the rocks use the default material.", this);
            return;
        }
        _material = new Material(shader) { name = "Asteroid Rock (runtime)", hideFlags = HideFlags.DontSave };
        _material.SetColor("_Color", Color.white);
        _material.SetFloat("_Exposure", sunlight);
        _material.SetFloat("_Fill", fill);
        renderer.sharedMaterial = _material;
    }

    /// <summary>
    /// The dust is a second system on a child made here — before WorldSwitcher_NEW gathers the
    /// group's renderers, so it fades and pauses with the rest.
    /// </summary>
    void ConfigureDust()
    {
        if (speckCount <= 0) return;

        Shader shader = Resources.Load<Shader>("AsteroidDust_NEW");
        if (shader == null)
        {
            Debug.LogWarning("[AsteroidBelt_NEW] Resources/AsteroidDust_NEW.shader is missing; no dust.", this);
            return;
        }

        var go = new GameObject("Asteroid Dust");
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);

        _dust = go.AddComponent<ParticleSystem>();
        _dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = _dust.main;
        main.loop = true;
        main.duration = 1000f;
        main.playOnAwake = false;
        main.startLifetime = 1e8f;
        main.startSpeed = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = speckCount;

        ParticleSystem.EmissionModule emission = _dust.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = _dust.shape;
        shape.enabled = false;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        _dustMaterial = new Material(shader) { name = "Asteroid Dust (runtime)", hideFlags = HideFlags.DontSave };
        _dustMaterial.SetColor("_Color", Color.white);
        renderer.sharedMaterial = _dustMaterial;
    }

    void Place()
    {
        _rng = new System.Random(seed);
        FindLine();

        MeasureOrbits();
        float earthAcross = earth != null ? earth.localScale.x : 0.92f;
        float a = AU(Mathf.Min(fromAU, toAU));
        float b = AU(Mathf.Max(fromAU, toAU));

        var rocks = new ParticleSystem.Particle[count + pathCount + 2];
        int n = 0;

        for (int i = 0; i < count; i++)
        {
            Vector3 p = Vector3.zero;
            for (int tries = 0; tries < 8; tries++)
            {
                p = InBelt(Radius(), 1f);
                if (!InTheWay(p)) break;
            }
            rocks[n++] = Rock(p, rockSizeOfEarth * earthAcross);
        }

        if (largest)
        {
            // True to the planets here, Ceres is 7.4% of Earth across and Vesta 4.1%. Ceres is
            // dark (it reflects 9%), Vesta the brightest of the big ones (42%).
            rocks[n++] = Body(2.77f, earthAcross * 0.074f * largestBoost, new Color(0.3f, 0.29f, 0.28f, 1f));
            rocks[n++] = Body(2.36f, earthAcross * 0.041f * largestBoost, new Color(0.62f, 0.58f, 0.52f, 1f));
        }

        if (_hasLine && pathCount > 0 && PathSpan(a, b, out float t0, out float t1))
        {
            for (int i = 0; i < pathCount; i++)
            {
                float angle;
                do angle = Next() * 360f; while (Mathf.Abs(Mathf.DeltaAngle(angle, 90f)) < keepAbove);

                float off = Mathf.Sqrt(Mathf.Lerp(pathClear * pathClear, pathSpread * pathSpread, Next()));
                float rad = angle * Mathf.Deg2Rad;
                Vector3 p = _lineAt + _lineDir * Mathf.Lerp(t0, t1, Next())
                          + (_lineSide * Mathf.Cos(rad) + _lineUp * Mathf.Sin(rad)) * off;
                rocks[n++] = Rock(p, pathRockSizeOfEarth * earthAcross);
            }
        }

        _system.Play();
        _system.SetParticles(rocks, n);

        int dust = _dust != null ? PlaceDust(a, b) : 0;

        if (debugLog)
            Debug.Log("[AsteroidBelt_NEW] " + n + " rocks (" + (n - count - (largest ? 2 : 0)) + " along the light) and " + dust + " specks, from " +
                      a.ToString("0.0") + " to " + b.ToString("0.0") + " from the Sun (Earth's orbit " + _earthOrbit.ToString("0") +
                      ", squeezed to the power " + _squeeze.ToString("0.000") + ").", this);
    }

    /// <summary>The dust: the same belt, kept well clear of the light's line, where a speck grown by the zoom would be a fog.</summary>
    int PlaceDust(float a, float b)
    {
        var specks = new ParticleSystem.Particle[speckCount];
        int n = 0;

        for (int i = 0; i < speckCount; i++)
        {
            Vector3 p = InBelt(Radius(), 0.5f);
            if (NearLine(p) < pathSpread * 2f) continue;

            float s = Mathf.Lerp(speckSize.x, speckSize.y, Next());
            specks[n++] = new ParticleSystem.Particle
            {
                position = p,
                velocity = Vector3.zero,
                startSize = s,
                startColor = new Color(dustColour.r, dustColour.g, dustColour.b, speckStrength * Mathf.Lerp(0.5f, 1f, Next())),
                startLifetime = 1e8f,
                remainingLifetime = 1e8f,
                randomSeed = (uint)_rng.Next()
            };
        }

        _dust.Play();
        _dust.SetParticles(specks, n);
        return n;
    }

    // ── The light's line ─────────────────────────────────────────────────────

    void FindLine()
    {
        _hasLine = earth != null && photon != null;
        if (!_hasLine) return;

        _lineAt = transform.InverseTransformPoint(earth.position);
        _lineDir = transform.InverseTransformDirection(photon.MovementDirection).normalized;
        _lineSide = Vector3.Cross(Vector3.up, _lineDir).normalized;
        _lineUp = Vector3.Cross(_lineDir, _lineSide);
    }

    /// <summary>
    /// Where, along the line from Earth, the light is inside the belt on its way in: before
    /// Earth (negative), on the stretch before it passes the Sun.
    /// </summary>
    bool PathSpan(float a, float b, out float t0, out float t1)
    {
        // Flat, the Sun at the origin: |at + t·dir| = r.
        Vector2 o = new Vector2(_lineAt.x, _lineAt.z), d = new Vector2(_lineDir.x, _lineDir.z);
        float dd = d.sqrMagnitude;
        t0 = t1 = 0f;
        if (dd < 1e-6f) return false;

        float tc = -Vector2.Dot(o, d) / dd;
        float near = (o + d * tc).magnitude;
        if (b <= near) return false;

        t0 = tc - Mathf.Sqrt((b * b - near * near) / dd);
        t1 = tc - Mathf.Sqrt(Mathf.Max(0f, a * a - near * near) / dd);
        return true;
    }

    /// <summary>How far from the light's line; very far if there is none.</summary>
    float NearLine(Vector3 p)
    {
        if (!_hasLine) return float.MaxValue;
        Vector3 off = p - _lineAt;
        return (off - _lineDir * Vector3.Dot(off, _lineDir)).magnitude;
    }

    /// <summary>Above the line and near it: where the camera goes, so kept clear.</summary>
    bool InTheWay(Vector3 p)
    {
        if (!_hasLine) return false;

        Vector3 off = p - _lineAt;
        off -= _lineDir * Vector3.Dot(off, _lineDir);
        float distance = off.magnitude;
        if (distance > pathSpread) return false;
        if (distance < pathClear) return true;

        float angle = Mathf.Atan2(Vector3.Dot(off, _lineUp), Vector3.Dot(off, _lineSide)) * Mathf.Rad2Deg;
        return Mathf.Abs(Mathf.DeltaAngle(angle, 90f)) < keepAbove;
    }

    // ── Rocks ────────────────────────────────────────────────────────────────

    ParticleSystem.Particle Rock(Vector3 position, Vector2 across)
    {
        // Most small: a size drawn evenly in log space, squared towards the small end.
        float s = Mathf.Exp(Mathf.Lerp(Mathf.Log(Mathf.Max(across.x, 1e-4f)), Mathf.Log(Mathf.Max(across.y, 1e-4f)), Next() * Next()));
        Color colour = Next() < stonyFraction ? stonyColour : darkColour;
        float shade = 1f + (Next() - 0.5f) * variation;
        float warm = (Next() - 0.5f) * variation * 0.3f;

        return new ParticleSystem.Particle
        {
            position = position,
            velocity = Vector3.zero,
            startSize3D = new Vector3(s * Mathf.Lerp(0.8f, 1.2f, Next()), s * Mathf.Lerp(0.6f, 1f, Next()), s * Mathf.Lerp(0.8f, 1.2f, Next())),
            rotation3D = new Vector3(Next() * 360f, Next() * 360f, Next() * 360f),
            angularVelocity3D = new Vector3(Next() - 0.5f, Next() - 0.5f, Next() - 0.5f) * (2f * tumble),
            startColor = new Color(colour.r * shade * (1f + warm), colour.g * shade, colour.b * shade * (1f - warm), 1f),
            startLifetime = 1e8f,
            remainingLifetime = 1e8f,
            randomSeed = (uint)_rng.Next()
        };
    }

    /// <summary>A unit rock: an icosphere, once subdivided, pushed in and out by <paramref name="lumps"/>.</summary>
    static Mesh Rock(System.Random rng, float lumps)
    {
        float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
        var v = new System.Collections.Generic.List<Vector3>
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
        };
        int[] f =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };

        var mid = new System.Collections.Generic.Dictionary<long, int>();
        int Mid(int i, int j)
        {
            long key = i < j ? ((long)i << 32) | (uint)j : ((long)j << 32) | (uint)i;
            if (mid.TryGetValue(key, out int m)) return m;
            v.Add((v[i] + v[j]) * 0.5f);
            return mid[key] = v.Count - 1;
        }

        var tris = new System.Collections.Generic.List<int>();
        for (int k = 0; k < f.Length; k += 3)
        {
            int a = f[k], b = f[k + 1], c = f[k + 2];
            int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
            tris.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
        }

        // A few broad dents and bulges, not noise: rocks, not sponges.
        var bumps = new Vector3[4];
        for (int i = 0; i < bumps.Length; i++)
            bumps[i] = new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f).normalized;

        var verts = new Vector3[v.Count];
        for (int i = 0; i < v.Count; i++)
        {
            Vector3 dir = v[i].normalized;
            float r = 1f;
            for (int j = 0; j < bumps.Length; j++)
                r += lumps * (j % 2 == 0 ? 1f : -1f) * Mathf.Pow(Mathf.Max(0f, Vector3.Dot(dir, bumps[j])), 3f);
            verts[i] = dir * (0.5f * r);
        }

        var mesh = new Mesh { name = "Asteroid", hideFlags = HideFlags.DontSave, vertices = verts, triangles = tris.ToArray() };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>One of the largest, on its own orbit at <paramref name="au"/>, somewhere out of the camera's way.</summary>
    ParticleSystem.Particle Body(float au, float across, Color colour)
    {
        Vector3 p = Vector3.zero;
        for (int tries = 0; tries < 16; tries++)
        {
            p = InBelt(AU(au), 0.5f);
            if (NearLine(p) > pathSpread * 2f) break;
        }

        ParticleSystem.Particle body = Rock(p, new Vector2(across, across));
        body.startSize3D = new Vector3(across, across * 0.93f, across);
        body.startColor = colour;
        return body;
    }

    // ── The belt's shape ─────────────────────────────────────────────────────

    /// <summary>
    /// A distance from the Sun: thicker in the middle of the belt, thinning to its edges (two
    /// draws averaged), and, with kirkwoodGaps, never in one of Jupiter's gaps.
    /// </summary>
    float Radius()
    {
        float au = 0f;
        for (int tries = 0; tries < 16; tries++)
        {
            au = Mathf.Lerp(fromAU, toAU, (Next() + Next()) * 0.5f);
            if (!InGap(au)) break;
        }
        return AU(au);
    }

    bool InGap(float au)
    {
        if (!kirkwoodGaps) return false;

        for (int i = 0; i < GapsAU.Length; i++)
            if (Mathf.Abs(au - GapsAU[i]) < gapHalfWidth) return true;
        return false;
    }

    /// <summary>
    /// How far from the Sun <paramref name="au"/> is drawn here: Earth's distance times
    /// <paramref name="au"/> to the power the planets are squeezed by.
    /// </summary>
    float AU(float au) => _earthOrbit * Mathf.Pow(Mathf.Max(au, 1e-4f), _squeeze);

    /// <summary>
    /// A point <paramref name="r"/> from the Sun on an orbit tilted by about orbitTilt (times
    /// <paramref name="tilt"/>), at a random point round it: how far above or below the plane
    /// it is goes with where on its tilted orbit it is. Squeezed as distances are there — a
    /// power p squeezes a short way across, at any distance, by p — so the belt keeps its shape.
    /// </summary>
    Vector3 InBelt(float r, float tilt)
    {
        float angle = Next() * Mathf.PI * 2f;
        float tilted = Gaussian() * orbitTilt * tilt * Mathf.Deg2Rad;
        float height = _squeeze * r * Mathf.Sin(tilted) * Mathf.Sin(Next() * Mathf.PI * 2f);
        return new Vector3(Mathf.Cos(angle) * r, height, Mathf.Sin(angle) * r);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Earth's distance from the Sun here, and the power the orbits are squeezed by, from
    /// Earth's and Jupiter's (5.2 AU). Without Jupiter, unsqueezed.
    /// </summary>
    void MeasureOrbits()
    {
        Vector3 at = sun != null ? sun.localPosition : Vector3.zero;
        float Flat(Transform t) => new Vector2(t.localPosition.x - at.x, t.localPosition.z - at.z).magnitude;

        _earthOrbit = earth != null ? Flat(earth) : 100f;
        _squeeze = outer != null && _earthOrbit > 1e-4f ? Mathf.Log(Flat(outer) / _earthOrbit) / Mathf.Log(5.2f) : 1f;
    }

    float Next() => (float)_rng.NextDouble();

    float Gaussian()
    {
        float u = Mathf.Max(Next(), 1e-6f);
        return Mathf.Sqrt(-2f * Mathf.Log(u)) * Mathf.Cos(2f * Mathf.PI * Next());
    }
}

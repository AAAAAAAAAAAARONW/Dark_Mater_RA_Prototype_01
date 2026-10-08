using System;
using System.Threading.Tasks;
using Cinemachine;
using UnityEngine;

/// <summary>
/// The cloud the photon falls through as it comes into Earth's atmosphere.
///
/// WHAT IT LOOKS LIKE. On entering the Earth layer the camera blends to the top-down cover
/// and looks down past the photon. Far below is a deck of cumulus, its tops lit by the
/// sun, shadowed blue underneath, and round everything the black of space is turning into
/// sky. The deck comes up — the photon is falling — and swallows the photon first, which
/// lights the cloud round it from inside, then the camera: a few seconds of white fog with
/// its wisps streaming past. Then the camera comes out under it into clear air, the deck a
/// ceiling overhead, and what is below can begin (EarthCutscene_NEW's video, waiting for
/// ClearedAt, until the city is built).
///
/// HOW. One raymarched slab (Resources/EarthClouds_NEW.shader) drawn on a cube that goes
/// round the camera everywhere, so it covers the screen from above the deck, in it or
/// under it; the haze of sky over space is the same pass. The photon does not move down —
/// the deck moves up past it, its noise with it, so the shapes rise past the camera.
/// Everything is in the world, so it reads as a fall from the top-down cover and from the
/// camera behind the photon alike.
///
/// What is left above — the Solar System — is hidden once the camera is inside the cloud:
/// nothing up there is meant to be seen from under Earth's sky.
///
/// The noise is a tiling 64-cube made at start on a worker thread, so building it never
/// holds up a frame. Until it is there the deck is not drawn; the sky still is.
///
/// LATER: SPACE STAYS SPACE UNTIL THE AIR. The sky used to come up over everything within
/// three and a half seconds of arriving, while the camera was still far above the deck — the
/// black of space turned into a pale blue fog with the Solar System hanging in it. Now how much
/// sky there is goes with how far into the air the camera has come: above the deck by
/// spaceAbove or more it is space, with only a thin glow along the horizon (limb), and the sky
/// closes in as the deck comes up to the camera. The cloud also scatters light deeper into
/// itself (its insides were a flat grey), and its wisps change as it rises past (evolve)
/// instead of rising as one rigid shape.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("CLOUDS", "#9FC4E8")]
public class EarthClouds_NEW : MonoBehaviour
{
    [Header("When")]
    [Tooltip("The layer whose arrival this plays on.")]
    [SerializeField] string layerId = "Earth";

    [Tooltip("Seconds from arriving until the deck has come up past the camera and stops, a " +
             "ceiling overhead. It comes up slowly, fastest as it passes the photon, and slows " +
             "to a stop.")]
    [Min(1f)] [SerializeField] float passSeconds = 7f;

    [Header("The deck")]
    [Tooltip("How far below the photon its top is on arrival, in world units.")]
    [Min(0f)] [SerializeField] float startDepth = 12f;

    [Tooltip("Its thickness, in world units.")]
    [Min(0.5f)] [SerializeField] float thickness = 7f;

    [Tooltip("How far above the photon its base is when it stops.")]
    [SerializeField] float endHeight = 6f;

    [Tooltip("How much of the sky the cloud covers.")]
    [Range(0f, 1f)] [SerializeField] float coverage = 0.6f;

    [Tooltip("How unevenly: higher gathers the cloud into bigger clumps with wider gaps.")]
    [Range(0f, 2f)] [SerializeField] float clumping = 0.9f;

    [Tooltip("How thick the cloud is: higher hides more, sooner.")]
    [Min(0f)] [SerializeField] float density = 1.4f;

    [Tooltip("The size of its big shapes, in world units — one tile of the noise.")]
    [Min(0.5f)] [SerializeField] float shapeSize = 8f;

    [Tooltip("The size of the detail that wears their edges, in world units.")]
    [Min(0.1f)] [SerializeField] float detailSize = 2.2f;

    [Tooltip("How far the detail wears the shapes away: higher, wispier.")]
    [Range(0f, 1f)] [SerializeField] float erosion = 0.4f;

    [Tooltip("Wind, world units a second: the cloud drifts across as it comes up.")]
    [SerializeField] Vector3 wind = new Vector3(0.25f, 0f, 0.1f);

    [Header("Light")]
    [Tooltip("Optional. The sun the light comes from, taken flat; if empty, the SolarSystem " +
             "group's 'Sun'.")]
    [SerializeField] Transform sun;

    [Tooltip("How high the sun stands over the clouds, in degrees.")]
    [Range(5f, 85f)] [SerializeField] float sunElevation = 28f;

    [SerializeField] Color sunColor = new Color(1f, 0.96f, 0.9f, 1f);

    [Tooltip("How bright the sun is on the cloud. Against the sky light below, this is what " +
             "makes the tops white and the far sides blue.")]
    [Range(0f, 4f)] [SerializeField] float sunIntensity = 1.6f;

    [SerializeField] Color skyLightTop = new Color(0.62f, 0.72f, 0.9f, 1f);
    [SerializeField] Color skyLightBottom = new Color(0.3f, 0.35f, 0.46f, 1f);

    [Tooltip("How much the sky lights the cloud, from all round.")]
    [Range(0f, 2f)] [SerializeField] float skyLight = 0.5f;

    [Tooltip("The photon's light in the cloud round it.")]
    [SerializeField] Color photonGlow = new Color(1f, 0.95f, 0.85f, 1f);

    [Range(0f, 4f)] [SerializeField] float photonGlowStrength = 1.5f;

    [Tooltip("How far its light reaches into the cloud, in world units.")]
    [Min(0.1f)] [SerializeField] float photonGlowRadius = 2.5f;

    [Header("Sky")]
    [SerializeField] Color skyOverhead = new Color(0.16f, 0.3f, 0.6f, 1f);
    [SerializeField] Color skyAtHorizon = new Color(0.62f, 0.74f, 0.9f, 1f);

    [Tooltip("Looking down: the ground, far below, through the air.")]
    [SerializeField] Color groundThroughAir = new Color(0.1f, 0.16f, 0.26f, 1f);

    [Tooltip("How much sky, over the black of space, once in the atmosphere.")]
    [Range(0f, 1f)] [SerializeField] float sky = 0.9f;

    [Tooltip("Seconds over which space turns into sky.")]
    [Min(0.1f)] [SerializeField] float skySeconds = 3.5f;

    [Tooltip("How far above the deck's top the camera is still in space, in world units: there the " +
             "sky above the horizon is black, with only its glow; below the horizon the ground shows " +
             "through the gaps from the start. The sky closes in as the deck comes up, all of it by " +
             "the time the camera reaches the deck. 0 = sky everywhere from the start, as before.")]
    [Min(0f)] [SerializeField] float spaceAbove = 14f;

    [Tooltip("The thin glow of air along the horizon, seen from space.")]
    [Range(0f, 1f)] [SerializeField] float limb = 0.35f;

    [Tooltip("How fast the cloud's wisps change as it comes up, in detail tiles a second.")]
    [Range(0f, 0.3f)] [SerializeField] float evolve = 0.05f;

    [Header("What is left above")]
    [Tooltip("The render group hidden once the camera is in the cloud. Empty: none.")]
    [SerializeField] string hideGroup = "SolarSystem";

    [Header("Quality")]
    [Tooltip("Steps along each ray through the deck. Fewer is faster and grainier.")]
    [Range(8, 96)] [SerializeField] int steps = 40;

    [Tooltip("The longest a step may be, in world units. A long grazing ray takes more steps " +
             "(up to twice as many) rather than longer ones.")]
    [Range(0.1f, 2f)] [SerializeField] float longestStep = 0.6f;

    [Tooltip("Furthest cloud drawn, in world units; it fades into the sky well before.")]
    [Min(10f)] [SerializeField] float farthest = 60f;

    [Header("Wiring (found if empty)")]
    [SerializeField] LayerState_NEW state;
    [SerializeField] WorldSwitcher_NEW worlds;
    [SerializeField] PlayerRig_NEW player;
    [SerializeField] CinemachineBrain brain;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    /// <summary>
    /// Seconds from arriving until the camera of the top-down cover — CoverHeight above the
    /// photon — comes out under the deck. For whatever is to begin as the cloud parts.
    /// </summary>
    public float ClearedAt
    {
        get
        {
            float travel = startDepth + thickness + endHeight;
            float p = Mathf.Clamp01((startDepth + thickness + CoverHeight) / Mathf.Max(1e-3f, travel));
            return passSeconds * Mathf.Acos(1f - 2f * p) / Mathf.PI;
        }
    }

    /// <summary>How high above the photon the top-down cover camera sits (its transposer's offset).</summary>
    public const float CoverHeight = 3f;

    const int NoiseSize = 64;

    static readonly int NoiseId = Shader.PropertyToID("_Noise");
    static readonly int DeckBottomId = Shader.PropertyToID("_DeckBottom");
    static readonly int DeckTopId = Shader.PropertyToID("_DeckTop");
    static readonly int DeckOffsetId = Shader.PropertyToID("_DeckOffset");
    static readonly int ShapeScaleId = Shader.PropertyToID("_ShapeScale");
    static readonly int DetailScaleId = Shader.PropertyToID("_DetailScale");
    static readonly int CoverageId = Shader.PropertyToID("_Coverage");
    static readonly int ErosionId = Shader.PropertyToID("_Erosion");
    static readonly int WeatherId = Shader.PropertyToID("_Weather");
    static readonly int MaxStepId = Shader.PropertyToID("_MaxStep");
    static readonly int GroundId = Shader.PropertyToID("_Ground");
    static readonly int DensityId = Shader.PropertyToID("_Density");
    static readonly int StepsId = Shader.PropertyToID("_Steps");
    static readonly int MaxDistanceId = Shader.PropertyToID("_MaxDistance");
    static readonly int SunDirId = Shader.PropertyToID("_SunDir");
    static readonly int SunColorId = Shader.PropertyToID("_SunColor");
    static readonly int AmbientTopId = Shader.PropertyToID("_AmbientTop");
    static readonly int AmbientBottomId = Shader.PropertyToID("_AmbientBottom");
    static readonly int PhotonPosId = Shader.PropertyToID("_PhotonPos");
    static readonly int PhotonColorId = Shader.PropertyToID("_PhotonColor");
    static readonly int PhotonRadiusId = Shader.PropertyToID("_PhotonRadius");
    static readonly int ZenithId = Shader.PropertyToID("_Zenith");
    static readonly int HorizonId = Shader.PropertyToID("_Horizon");
    static readonly int HazeId = Shader.PropertyToID("_Haze");
    static readonly int OpacityId = Shader.PropertyToID("_Opacity");
    static readonly int LimbId = Shader.PropertyToID("_Limb");
    static readonly int BelowId = Shader.PropertyToID("_Below");
    static readonly int EvolveId = Shader.PropertyToID("_Evolve");

    Task<Color32[]> _building;
    Texture3D _noise;

    GameObject _shell;
    Material _material;
    Mesh _cube;
    bool _warnedNoShader;

    bool _playing;
    float _clock;
    float _photonY;
    Vector3 _sunDir;
    bool _hidAbove;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (state == null) state = FindObjectOfType<LayerState_NEW>();
        if (worlds == null) worlds = FindObjectOfType<WorldSwitcher_NEW>();
        if (player == null) player = FindObjectOfType<PlayerRig_NEW>();
        if (brain == null) brain = FindObjectOfType<CinemachineBrain>();

        if (state == null)
            Debug.LogWarning("[EarthClouds_NEW] No LayerState_NEW in the scene; the clouds will never play.", this);

        _building = Task.Run(() => BuildNoise(4217));
    }

    void OnEnable()
    {
        if (state != null) state.OnLayerChanged += HandleLayerChanged;
    }

    void OnDisable()
    {
        if (state != null) state.OnLayerChanged -= HandleLayerChanged;
        Stop();
    }

    void OnDestroy()
    {
        if (_shell != null) Destroy(_shell);
        if (_material != null) Destroy(_material);
        if (_cube != null) Destroy(_cube);
        if (_noise != null) Destroy(_noise);
    }

    void HandleLayerChanged(LayerProfile_NEW previous, LayerProfile_NEW current)
    {
        if (current != null && string.Equals(current.layerId, layerId, StringComparison.Ordinal)) Play();
        else Stop();
    }

    void Play()
    {
        if (!EnsureShell()) return;

        _playing = true;
        _clock = 0f;
        _hidAbove = false;
        _photonY = player != null ? player.transform.position.y : transform.position.y;
        _sunDir = SunDirection();
        _shell.SetActive(true);

        if (debugLog)
            Debug.Log($"[EarthClouds_NEW] Into the atmosphere: the deck comes up from {startDepth} below the " +
                      $"photon; the camera of the cover is out under it at {ClearedAt:0.0}s.", this);

        Tick(0f);
    }

    void Stop()
    {
        _playing = false;
        if (_shell != null && _shell.activeSelf) _shell.SetActive(false);
    }

    void LateUpdate()
    {
        if (_noise == null && _building != null && _building.IsCompleted) UploadNoise();
        if (!_playing) return;

        _clock += Time.deltaTime;
        Tick(_clock);
    }

    // ── The fall ─────────────────────────────────────────────────────────────

    void Tick(float t)
    {
        Camera cam = brain != null ? brain.OutputCamera : Camera.main;
        if (cam == null || _material == null) return;

        // The deck comes up past the photon: slowly at first, fastest through it, slowing to
        // a stop as a ceiling overhead. Its noise comes up with it, so its shapes rise past.
        float travel = startDepth + thickness + endHeight;
        float p = 0.5f - 0.5f * Mathf.Cos(Mathf.PI * Mathf.Clamp01(t / passSeconds));
        float risen = p * travel;
        float top = _photonY - startDepth + risen;
        float bottom = top - thickness;

        _material.SetFloat(DeckBottomId, bottom);
        _material.SetFloat(DeckTopId, top);
        _material.SetVector(DeckOffsetId, new Vector4(wind.x * t, risen + wind.y * t, wind.z * t, 0f));
        _material.SetFloat(ShapeScaleId, 1f / shapeSize);
        _material.SetFloat(DetailScaleId, 1f / detailSize);
        _material.SetFloat(CoverageId, coverage);
        _material.SetFloat(ErosionId, erosion);
        _material.SetFloat(WeatherId, clumping);
        _material.SetFloat(DensityId, density);
        _material.SetFloat(StepsId, steps);
        _material.SetFloat(MaxStepId, longestStep);
        _material.SetFloat(MaxDistanceId, farthest);

        _material.SetVector(SunDirId, _sunDir);
        _material.SetColor(SunColorId, sunColor * sunIntensity);
        _material.SetColor(AmbientTopId, skyLightTop * skyLight);
        _material.SetColor(AmbientBottomId, skyLightBottom * skyLight);

        Vector3 photon = player != null ? player.transform.position : cam.transform.position;
        _material.SetVector(PhotonPosId, photon);
        _material.SetColor(PhotonColorId, new Color(photonGlow.r, photonGlow.g, photonGlow.b, photonGlowStrength));
        _material.SetFloat(PhotonRadiusId, photonGlowRadius);

        _material.SetColor(ZenithId, skyOverhead);
        _material.SetColor(HorizonId, skyAtHorizon);
        _material.SetColor(GroundId, groundThroughAir);
        // The sky: as much as the camera is into the air — none while it is spaceAbove over the
        // deck, all of it at the deck — and only along the horizon above that.
        float ramp = sky * Smooth(0f, skySeconds, t);
        float camY = cam.transform.position.y;
        float inAir = spaceAbove > 0f ? 1f - Smooth(0f, spaceAbove, camY - top) : 1f;
        _material.SetFloat(HazeId, ramp * inAir);
        _material.SetFloat(LimbId, ramp * limb * (1f - inAir));
        _material.SetFloat(BelowId, ramp);
        _material.SetFloat(EvolveId, evolve);

        // The deck fades up from far below rather than appearing there — and only once the
        // noise is made.
        _material.SetFloat(OpacityId, _noise != null ? Smooth(0f, 1.2f, t) : 0f);

        // Round the camera, wherever it is: a cube it is always inside.
        Transform shell = _shell.transform;
        shell.position = cam.transform.position;
        shell.rotation = Quaternion.identity;
        shell.localScale = Vector3.one * Mathf.Clamp(cam.farClipPlane * 0.5f, 2f, 40f);

        // Once the camera is well into the cloud, what is left above goes.
        if (!_hidAbove && cam.transform.position.y < top - 1.5f)
        {
            _hidAbove = true;
            if (worlds != null && !string.IsNullOrEmpty(hideGroup)) worlds.SetGroupLook(hideGroup, 0f);
            if (debugLog) Debug.Log($"[EarthClouds_NEW] In the cloud at {t:0.0}s; '{hideGroup}' hidden.", this);
        }
    }

    /// <summary>Towards the sun: the way to the Solar System's Sun, flat, raised by sunElevation.</summary>
    Vector3 SunDirection()
    {
        Transform s = sun;
        if (s == null && worlds != null)
        {
            Transform system = worlds.GroupRoot("SolarSystem");
            if (system != null) s = system.Find("Sun");
        }

        Vector3 from = player != null ? player.transform.position : transform.position;
        Vector3 flat = s != null ? s.position - from : Vector3.forward;
        flat.y = 0f;
        if (flat.sqrMagnitude < 1e-6f) flat = Vector3.forward;
        flat.Normalize();

        float e = sunElevation * Mathf.Deg2Rad;
        return (flat * Mathf.Cos(e) + Vector3.up * Mathf.Sin(e)).normalized;
    }

    bool EnsureShell()
    {
        if (_shell != null) return true;

        Shader shader = Resources.Load<Shader>("EarthClouds_NEW");
        if (shader == null)
        {
            if (!_warnedNoShader)
                Debug.LogWarning("[EarthClouds_NEW] Resources/EarthClouds_NEW.shader is missing; no clouds.", this);
            _warnedNoShader = true;
            return false;
        }

        _material = new Material(shader) { name = "EarthClouds (runtime)" };
        if (_noise != null) _material.SetTexture(NoiseId, _noise);

        // A unit cube; the shader draws its inside faces.
        _cube = new Mesh { name = "EarthClouds cube" };
        _cube.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f)
        };
        _cube.triangles = new[]
        {
            0, 2, 1, 0, 3, 2,   4, 5, 6, 4, 6, 7,   0, 1, 5, 0, 5, 4,
            3, 6, 2, 3, 7, 6,   0, 4, 7, 0, 7, 3,   1, 2, 6, 1, 6, 5
        };
        _cube.RecalculateBounds();

        _shell = new GameObject("EarthClouds (temporary)");
        if (player != null) _shell.layer = player.gameObject.layer;
        _shell.AddComponent<MeshFilter>().sharedMesh = _cube;
        MeshRenderer mr = _shell.AddComponent<MeshRenderer>();
        mr.sharedMaterial = _material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        _shell.SetActive(false);
        return true;
    }

    // ── The noise ────────────────────────────────────────────────────────────

    void UploadNoise()
    {
        Color32[] data = _building.Status == TaskStatus.RanToCompletion ? _building.Result : null;
        _building = null;
        if (data == null)
        {
            Debug.LogWarning("[EarthClouds_NEW] Making the cloud noise failed; the deck is not drawn.", this);
            return;
        }

        _noise = new Texture3D(NoiseSize, NoiseSize, NoiseSize, TextureFormat.RGBA32, false)
        {
            name = "EarthClouds noise (runtime)",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };
        _noise.SetPixels32(data);
        _noise.Apply(false, true);
        if (_material != null) _material.SetTexture(NoiseId, _noise);
    }

    /// <summary>
    /// Tiling 3D noise, NoiseSize a side. R: the big shapes — billowy cellular noise
    /// (inverted Worley, three octaves) over smooth gradient noise (Perlin, three). G: finer
    /// cellular noise, the detail that wears their edges. B: slow gradient noise, the weather
    /// that clumps the shapes, sampled four times larger. R and G are stretched to fill 0 to 1
    /// (as made they sit between about 0.3 and 0.7), so coverage and erosion mean what they
    /// say. Pure arithmetic, safe off the main thread.
    /// </summary>
    static Color32[] BuildNoise(int seed)
    {
        int n = NoiseSize;
        var rng = new System.Random(seed);

        int[] perm = new int[512];
        for (int i = 0; i < 256; i++) perm[i] = i;
        for (int i = 255; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            int swap = perm[i];
            perm[i] = perm[j];
            perm[j] = swap;
        }
        for (int i = 0; i < 256; i++) perm[256 + i] = perm[i];

        Vector3[] shape4 = FeaturePoints(rng, 4), shape8 = FeaturePoints(rng, 8), shape16 = FeaturePoints(rng, 16);
        Vector3[] detail8 = FeaturePoints(rng, 8), detail16 = FeaturePoints(rng, 16), detail32 = FeaturePoints(rng, 32);

        var data = new Color32[n * n * n];
        for (int z = 0; z < n; z++)
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float u = (x + 0.5f) / n, v = (y + 0.5f) / n, w = (z + 0.5f) / n;

            float cells = 0.625f * Worley(u, v, w, 4, shape4) + 0.25f * Worley(u, v, w, 8, shape8)
                        + 0.125f * Worley(u, v, w, 16, shape16);
            float smooth = 0.5f + 0.5f * (0.57f * Perlin(u, v, w, 4, perm) + 0.29f * Perlin(u, v, w, 8, perm)
                                       + 0.14f * Perlin(u, v, w, 16, perm));
            float shape = Clamp01((0.6f * cells + 0.4f * smooth - 0.28f) / 0.44f);

            float detail = Clamp01((0.625f * Worley(u, v, w, 8, detail8) + 0.25f * Worley(u, v, w, 16, detail16)
                                  + 0.125f * Worley(u, v, w, 32, detail32) - 0.18f) / 0.55f);

            float weather = Clamp01(0.5f + 0.8f * (0.75f * Perlin(u, v, w, 2, perm) + 0.25f * Perlin(u, v, w, 4, perm)));

            data[(z * n + y) * n + x] = new Color32((byte)(shape * 255f), (byte)(detail * 255f), (byte)(weather * 255f), 255);
        }
        return data;
    }

    static Vector3[] FeaturePoints(System.Random rng, int cells)
    {
        var points = new Vector3[cells * cells * cells];
        for (int i = 0; i < points.Length; i++)
            points[i] = new Vector3((float)rng.NextDouble(), (float)rng.NextDouble(), (float)rng.NextDouble());
        return points;
    }

    /// <summary>1 on a feature point, down to 0 a cell away — inverted cellular noise, tiling.</summary>
    static float Worley(float u, float v, float w, int cells, Vector3[] points)
    {
        float x = u * cells, y = v * cells, z = w * cells;
        int cx = (int)x, cy = (int)y, cz = (int)z;
        float best = 9f;

        for (int dz = -1; dz <= 1; dz++)
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
        {
            int nx = cx + dx, ny = cy + dy, nz = cz + dz;
            Vector3 f = points[(((nz + cells) % cells) * cells + (ny + cells) % cells) * cells + (nx + cells) % cells];
            float fx = nx + f.x - x, fy = ny + f.y - y, fz = nz + f.z - z;
            float d = fx * fx + fy * fy + fz * fz;
            if (d < best) best = d;
        }

        return 1f - Clamp01((float)Math.Sqrt(best));
    }

    /// <summary>Gradient noise, about -1 to 1, tiling every <paramref name="cells"/> cells.</summary>
    static float Perlin(float u, float v, float w, int cells, int[] perm)
    {
        float x = u * cells, y = v * cells, z = w * cells;
        int xi = (int)x, yi = (int)y, zi = (int)z;
        float xf = x - xi, yf = y - yi, zf = z - zi;
        int x0 = xi % cells, x1 = (xi + 1) % cells;
        int y0 = yi % cells, y1 = (yi + 1) % cells;
        int z0 = zi % cells, z1 = (zi + 1) % cells;

        float a = Lerp(Grad(perm[perm[perm[x0] + y0] + z0], xf, yf, zf),
                       Grad(perm[perm[perm[x1] + y0] + z0], xf - 1f, yf, zf), Fade(xf));
        float b = Lerp(Grad(perm[perm[perm[x0] + y1] + z0], xf, yf - 1f, zf),
                       Grad(perm[perm[perm[x1] + y1] + z0], xf - 1f, yf - 1f, zf), Fade(xf));
        float c = Lerp(Grad(perm[perm[perm[x0] + y0] + z1], xf, yf, zf - 1f),
                       Grad(perm[perm[perm[x1] + y0] + z1], xf - 1f, yf, zf - 1f), Fade(xf));
        float d = Lerp(Grad(perm[perm[perm[x0] + y1] + z1], xf, yf - 1f, zf - 1f),
                       Grad(perm[perm[perm[x1] + y1] + z1], xf - 1f, yf - 1f, zf - 1f), Fade(xf));
        return Lerp(Lerp(a, b, Fade(yf)), Lerp(c, d, Fade(yf)), Fade(zf));
    }

    static float Grad(int hash, float x, float y, float z)
    {
        int h = hash & 15;
        float a = h < 8 ? x : y;
        float b = h < 4 ? y : (h == 12 || h == 14 ? x : z);
        return ((h & 1) == 0 ? a : -a) + ((h & 2) == 0 ? b : -b);
    }

    static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
    static float Lerp(float a, float b, float t) => a + (b - a) * t;
    static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

    static float Smooth(float from, float to, float x)
    {
        float t = Mathf.Clamp01((x - from) / Mathf.Max(1e-5f, to - from));
        return t * t * (3f - 2f * t);
    }
}

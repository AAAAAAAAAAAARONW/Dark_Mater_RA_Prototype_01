using UnityEngine;

/// <summary>
/// A cloud of cold neutral hydrogen the light passes through: a real volume, lit from one side.
/// It hides what is behind it and is seen by the light it catches — the lobes facing the light
/// pink, the ones behind them and the cloud's heart deep violet — so it has a near side, a far
/// side and a shape.
///
/// BESIDE NebulaCloudVFX_NEW, NOT INSTEAD OF IT. That one is the glowing star-forming nebula;
/// this one is the cold gas, on the tutorial's Cloud children.
///
/// WHAT IT DRAWS. Resources/HydrogenAbsorber_NEW.shader on a sphere of this object's scale in
/// radius, built as a hidden child that is never saved, drawn from its back faces so it works
/// from inside. Ten steps along the ray; at each, where the gas is (three lumps pushed in and
/// out by a tiling 3D noise of billows) and how much of it lies a step towards the light: its
/// own shadow, which is what makes a cloud read as a volume. Thirty reads a pixel of a
/// 32-cubed texture made here once and shared by every cloud.
///
/// THE LIGHT is the quasar's (lightSource), brought round to one side and above (lightFromSide):
/// lit from dead behind, a cloud is a dark shape with a bright rim; from the side, its lobes
/// shade one another.
///
/// Built before WorldSwitcher_NEW gathers renderers, so it fades with its world through _Color.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
[HierarchyBadge_NEW("H I", "#B0457E")]
public class HydrogenAbsorberVFX_NEW : MonoBehaviour
{
    [Header("Gas")]
    [Tooltip("Which cloud: the shape of its lumps, and where its wisps and lanes fall.")]
    [SerializeField] int seed = 0;

    [Tooltip("How much it hides what is behind it. Low: a faint tint; high: a dark cloud.")]
    [Range(0f, 8f)] [SerializeField] float opacity = 2.5f;

    [Tooltip("How soft the cloud's edge is: low, crisp lobes; high, a haze round them.")]
    [Range(0.1f, 1f)] [SerializeField] float softness = 0.55f;

    [Tooltip("How dark the creases between its billows are.")]
    [Range(0f, 2f)] [SerializeField] float dustAmount = 0.6f;

    [Tooltip("How fast the gas drifts, radii a second.")]
    [SerializeField] float drift = 0.004f;

    [Tooltip("Nearer the camera than x (of the radius) the gas is left out, from y it is all there: " +
             "from inside, a tint rather than a wall.")]
    [SerializeField] Vector2 resolve = new Vector2(0.05f, 0.3f);

    [Header("Colours")]
    [Tooltip("Gas in the light. It is never drawn brighter than 0.95, so it does not bloom.")]
    [SerializeField] Color litColour = new Color(1f, 0.46f, 0.66f, 1f);

    [Tooltip("Gas in the cloud's own shadow.")]
    [SerializeField] Color shadowColour = new Color(0.10f, 0.035f, 0.16f, 1f);

    [Tooltip("Light the thin gas scatters towards the eye: a pale rim on the edges facing the light.")]
    [SerializeField] Color rimColour = new Color(1f, 0.72f, 0.82f, 1f);

    [Tooltip("How much of that rim.")]
    [Range(0f, 2f)] [SerializeField] float scatter = 0.9f;

    [Header("Volume: lit from one side")]
    [Tooltip("What lights it: the quasar. Empty: the tutorial's destination, else the scene's quasar, " +
             "else from ahead.")]
    [SerializeField] Transform lightSource;

    [Tooltip("How far the light is brought round from behind the cloud: x to the side, y above. " +
             "0, 0 is the quasar's true direction, which from the light's path is dead behind the " +
             "cloud: a dark shape with a rim. Round to one side its lobes shade one another.")]
    [SerializeField] Vector2 lightFromSide = new Vector2(0.75f, 0.85f);

    [Tooltip("How dark the cloud's own shadow is.")]
    [Range(0f, 6f)] [SerializeField] float shadow = 2.6f;

    [Header("Seen from far away")]
    [Tooltip("Smallest size it is drawn at, as an angle (degrees, radius) from the camera. A cloud " +
             "of its true size is a speck until the last second of the approach, then fills the " +
             "screen at once; held at this size it is seen arriving and grows only once it is " +
             "close enough to be bigger than this. 0 = always its true size.")]
    [Range(0f, 15f)] [SerializeField] float minAngularRadius = 4f;

    [Tooltip("Seconds to fade in when it appears (the atom is switched on at spawn).")]
    [Min(0f)] [SerializeField] float appearSeconds = 0.5f;

    [Tooltip("Leave empty to use the main camera.")]
    [SerializeField] Transform viewCamera;

    [Tooltip("After the cosmic web's volumes (2950 to 2954) and the galaxies (2989 to 2993), before the light's own glow (3005).")]
    [SerializeField] int renderQueue = 2995;

    const HideFlags Built = HideFlags.HideAndDontSave;
    static readonly int AppearId = Shader.PropertyToID("_Appear");
    static readonly int LightDirId = Shader.PropertyToID("_TowardLight");

    GameObject _part;
    Material _material;
    Mesh _mesh;
    bool _dirty;
    float _appear;

    /// <summary>The cloud's bound, in world units: the radius the gas never reaches past.</summary>
    public float Radius => transform.lossyScale.x;

    void OnEnable()
    {
        _appear = Application.isPlaying && appearSeconds > 0f ? 0f : 1f;
        Build();
    }

    void OnDisable() => Clear();

    void LateUpdate()
    {
        if (_part == null || _material == null) return;

        if (_appear < 1f)
        {
            _appear = Mathf.Min(1f, _appear + Time.deltaTime / appearSeconds);
            _material.SetFloat(AppearId, _appear * _appear * (3f - 2f * _appear));
        }

        // Held at a minimum size on screen: the drawn sphere is enlarged until it covers
        // minAngularRadius, the gas inside it with it. Opacity is in the cloud's own units,
        // so an enlarged cloud is no denser, only bigger.
        Transform cam = viewCamera != null ? viewCamera : (Camera.main != null ? Camera.main.transform : null);
        float k = 1f;
        if (cam != null && minAngularRadius > 0f && Radius > 1e-4f)
        {
            float distance = Vector3.Distance(cam.position, transform.position);
            float need = distance * Mathf.Tan(minAngularRadius * Mathf.Deg2Rad);
            k = Mathf.Max(1f, need / Radius);
        }
        _part.transform.localScale = Vector3.one * k;

        // Towards the light, in the cloud's own space: it moves as the cloud is passed.
        Vector3 toLight = lightSource != null ? lightSource.position - transform.position : Vector3.forward;
        if (toLight.sqrMagnitude < 1e-6f) toLight = Vector3.forward;
        toLight.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, toLight);
        if (side.sqrMagnitude < 1e-6f) side = Vector3.right;
        side.Normalize();
        Vector3 above = Vector3.Cross(toLight, side);
        Vector3 from = toLight + side * lightFromSide.x + above * lightFromSide.y;
        _material.SetVector(LightDirId, transform.InverseTransformDirection(from.normalized));
    }

    /// <summary>The quasar, if nothing was wired: the tutorial's destination, else the scene's quasar.</summary>
    void FindLight()
    {
        if (lightSource != null) return;

        TutorialTravel_NEW travel = FindObjectOfType<TutorialTravel_NEW>();
        if (travel != null && travel.Destination != null) { lightSource = travel.Destination; return; }

        QuasarVFX_NEW quasar = FindObjectOfType<QuasarVFX_NEW>();
        if (quasar != null) lightSource = quasar.transform;
    }

    void OnValidate()
    {
        _dirty = true;
#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
    }

    void Update()
    {
        if (_dirty && _material != null) Apply();
        _dirty = false;
    }

    void Build()
    {
        if (_material != null) return;

        Shader shader = Resources.Load<Shader>("HydrogenAbsorber_NEW");
        if (shader == null)
        {
            Debug.LogWarning("[HydrogenAbsorberVFX_NEW] Resources/HydrogenAbsorber_NEW.shader is missing; nothing drawn.", this);
            return;
        }

        _mesh = Sphere();
        _mesh.hideFlags = Built;

        _part = new GameObject("Hydrogen absorber") { hideFlags = Built };
        _part.layer = gameObject.layer;
        _part.transform.SetParent(transform, false);
        _part.AddComponent<MeshFilter>().sharedMesh = _mesh;
        MeshRenderer mr = _part.AddComponent<MeshRenderer>();
        _material = new Material(shader) { name = "Hydrogen absorber (runtime)", hideFlags = Built, renderQueue = renderQueue };
        mr.sharedMaterial = _material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        FindLight();
        Apply();
    }

    void Clear()
    {
        Kill(_part);
        Kill(_material);
        Kill(_mesh);
        _part = null;
        _material = null;
        _mesh = null;
    }

    static void Kill(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }

    /// <summary>Every setting onto the material. _Color is left alone: it is the world's fade.</summary>
    void Apply()
    {
        _material.renderQueue = renderQueue;
        _material.SetTexture("_Noise3D", Billows());
        _material.SetFloat("_Seed", seed);
        _material.SetFloat("_Opacity", opacity);
        _material.SetFloat("_Softness", softness);
        _material.SetFloat("_DustAmount", dustAmount);
        _material.SetFloat("_Flow", drift);
        _material.SetVector("_Resolve", resolve);
        _material.SetColor("_LitColor", litColour);
        _material.SetColor("_ShadowColor", shadowColour);
        _material.SetColor("_RimColor", rimColour);
        _material.SetFloat("_Scatter", scatter);
        _material.SetFloat("_Shadow", shadow);
        _material.SetFloat(AppearId, _appear * _appear * (3f - 2f * _appear));
    }

    // ── The noise ───────────────────────────────────────────────────────────

    const int NoiseSize = 32;
    static Texture3D _billows;

    /// <summary>
    /// The cloud's 3D noise, made once and shared by every cloud: 32 cubed, tiling. Red is the
    /// billows — cells (Worley noise, turned over, so each cell is a rounded lobe) at two sizes
    /// with a little smooth noise; green is finer cells, for the creases and the eaten edge.
    /// </summary>
    static Texture3D Billows()
    {
        if (_billows != null) return _billows;

        int n = NoiseSize;
        float[] w3 = Cells(3, 11), w6 = Cells(6, 12), w5 = Cells(5, 16), w10 = Cells(10, 17);
        float[] v4 = Smooth3(4, 14), v8 = Smooth3(8, 15);

        var red = new float[n * n * n];
        var green = new float[n * n * n];
        float rMin = 1f, rMax = 0f, gMin = 1f, gMax = 0f;
        for (int i = 0; i < red.Length; i++)
        {
            float cells = (1f - w3[i]) * 0.6f + (1f - w6[i]) * 0.4f;
            float smooth = v4[i] * 0.6f + v8[i] * 0.4f;
            red[i] = cells * 0.75f + smooth * 0.25f;
            green[i] = (1f - w5[i]) * 0.6f + (1f - w10[i]) * 0.4f;
            rMin = Mathf.Min(rMin, red[i]); rMax = Mathf.Max(rMax, red[i]);
            gMin = Mathf.Min(gMin, green[i]); gMax = Mathf.Max(gMax, green[i]);
        }

        var pixels = new Color32[red.Length];
        for (int i = 0; i < red.Length; i++)
        {
            byte r = (byte)Mathf.RoundToInt(255f * Mathf.InverseLerp(rMin, rMax, red[i]));
            byte g = (byte)Mathf.RoundToInt(255f * Mathf.InverseLerp(gMin, gMax, green[i]));
            pixels[i] = new Color32(r, g, 0, 255);
        }

        _billows = new Texture3D(n, n, n, TextureFormat.RGBA32, false)
        {
            name = "Hydrogen billows (runtime)",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };
        _billows.SetPixels32(pixels);
        _billows.Apply(false, true);
        return _billows;
    }

    /// <summary>Tiling Worley noise: the distance to the nearest of one point a cell, 0 to 1.</summary>
    static float[] Cells(int cells, int seed)
    {
        int n = NoiseSize;
        var random = new System.Random(seed);
        var points = new Vector3[cells * cells * cells];
        for (int i = 0; i < points.Length; i++)
            points[i] = new Vector3((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble());

        var result = new float[n * n * n];
        float k = (float)cells / n;
        for (int z = 0; z < n; z++)
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float px = (x + 0.5f) * k, py = (y + 0.5f) * k, pz = (z + 0.5f) * k;
            int cx = Mathf.FloorToInt(px), cy = Mathf.FloorToInt(py), cz = Mathf.FloorToInt(pz);
            float best = 9f;
            for (int dz = -1; dz <= 1; dz++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int ix = cx + dx, iy = cy + dy, iz = cz + dz;
                Vector3 q = points[Wrap(ix, cells) + cells * (Wrap(iy, cells) + cells * Wrap(iz, cells))];
                float ex = ix + q.x - px, ey = iy + q.y - py, ez = iz + q.z - pz;
                float distance = ex * ex + ey * ey + ez * ez;
                if (distance < best) best = distance;
            }
            result[x + n * (y + n * z)] = Mathf.Min(1f, Mathf.Sqrt(best));
        }
        return result;
    }

    /// <summary>Tiling smooth noise: one number a cell corner, blended between.</summary>
    static float[] Smooth3(int cells, int seed)
    {
        int n = NoiseSize;
        var random = new System.Random(seed);
        var values = new float[cells * cells * cells];
        for (int i = 0; i < values.Length; i++) values[i] = (float)random.NextDouble();

        var result = new float[n * n * n];
        float k = (float)cells / n;
        for (int z = 0; z < n; z++)
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float px = (x + 0.5f) * k, py = (y + 0.5f) * k, pz = (z + 0.5f) * k;
            int cx = Mathf.FloorToInt(px), cy = Mathf.FloorToInt(py), cz = Mathf.FloorToInt(pz);
            float fx = px - cx, fy = py - cy, fz = pz - cz;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy); fz = fz * fz * (3f - 2f * fz);

            float sum = 0f;
            for (int dz = 0; dz <= 1; dz++)
            for (int dy = 0; dy <= 1; dy++)
            for (int dx = 0; dx <= 1; dx++)
            {
                float weight = (dx == 1 ? fx : 1f - fx) * (dy == 1 ? fy : 1f - fy) * (dz == 1 ? fz : 1f - fz);
                sum += weight * values[Wrap(cx + dx, cells) + cells * (Wrap(cy + dy, cells) + cells * Wrap(cz + dz, cells))];
            }
            result[x + n * (y + n * z)] = sum;
        }
        return result;
    }

    static int Wrap(int i, int n) => ((i % n) + n) % n;

    /// <summary>A sphere of radius 1, outward-facing; the shader draws its back faces.</summary>
    static Mesh Sphere()
    {
        const int around = 32, across = 16;
        int row = around + 1;
        var vertices = new Vector3[row * (across + 1)];
        for (int j = 0; j <= across; j++)
        {
            float lat = -Mathf.PI * 0.5f + Mathf.PI * j / across;
            for (int i = 0; i <= around; i++)
            {
                float lon = Mathf.PI * 2f * i / around;
                vertices[j * row + i] = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
            }
        }
        var triangles = new int[around * across * 6];
        int t = 0;
        for (int j = 0; j < across; j++)
        for (int i = 0; i < around; i++)
        {
            int a = j * row + i, b = a + 1, c = a + row, d = c + 1;
            triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
            triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
        }
        var mesh = new Mesh { name = "Hydrogen absorber sphere", vertices = vertices, triangles = triangles };
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
        return mesh;
    }
}

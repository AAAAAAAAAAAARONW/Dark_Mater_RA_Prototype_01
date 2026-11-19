using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Dust for the light to fly through once it has been let go: what makes its speed seen.
///
/// The tutorial's own Dust and Streaks are particle systems, made for the slow flight in. After
/// the emission the light is doing hundreds of units a second, and at that speed they are hard
/// lines flickering across the frame. This is drawn by one shader instead
/// (Resources/LightDust_NEW.shader): motes tiled in a cube round the light, each a soft point at
/// rest and a soft streak at speed, lit by the light as it passes them, out of focus near the
/// lens. See the shader for how.
///
/// SPEED IS SHOWN, NOT COPIED. At the light's real speed a mote would cross the cube in a
/// frame or two and the dust would be noise. So the motes go by at a speed that grows with the
/// light's and levels off (maxDrift), and the streaks carry the rest: they go on lengthening
/// as the light speeds up. Slow reads as drifting points, fast as long lines pouring past.
///
/// It comes up over fadeSeconds after TutorialEmission_NEW.Emit and goes at the attract reset.
/// While it is up the old Streaks are held back (holdOldStreaks), so the two do not fight.
///
/// NOTHING TO WIRE. It adds itself, at run time, to any scene that has a TutorialEmission_NEW:
/// one object under the light, never saved. To tune it, select "Light Dust" under the Photon
/// in Play mode; to keep values, put this component on an object in the scene yourself and it
/// uses that one.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("LIGHT DUST", "#BFD8FF")]
public class TutorialLightDust_NEW : MonoBehaviour
{
    [Header("Wiring (found if left empty)")]
    [SerializeField] TutorialEmission_NEW emission;
    [SerializeField] TutorialTravel_NEW travel;

    [Header("The dust")]
    [Tooltip("How many motes. One mesh, placed on the GPU; a couple of thousand cost next to nothing.")]
    [Range(100, 6000)] [SerializeField] int count = 2000;

    [Tooltip("Edge of the cube they are tiled in round the light, units. Bigger is sparser and deeper.")]
    [Min(5f)] [SerializeField] float box = 50f;

    [Tooltip("Mote radius, units.")]
    [Min(0.001f)] [SerializeField] float size = 0.03f;

    [Tooltip("Brightness overall.")]
    [Range(0f, 3f)] [SerializeField] float brightness = 1f;

    [Tooltip("Brightness away from the light.")]
    [Range(0f, 2f)] [SerializeField] float ambient = 0.3f;

    [SerializeField] Color warm = new Color(1f, 0.84f, 0.62f, 1f);
    [SerializeField] Color cool = new Color(0.68f, 0.8f, 1f, 1f);

    [Header("Lit by the light")]
    [Tooltip("The light's colour times how brightly it lights the motes beside it.")]
    [ColorUsage(false, true)] [SerializeField] Color lightColour = new Color(1.5f, 1.6f, 2f, 1f);

    [Tooltip("How far it lights them, units.")]
    [Min(0.1f)] [SerializeField] float lightReach = 7f;

    [Tooltip("Nearer the lens than this a mote goes out of focus: a wide dim disc.")]
    [Min(0f)] [SerializeField] float focusDistance = 5f;

    [Header("Speed")]
    [Tooltip("The fastest the motes go by, units a second, however fast the light is. They reach " +
             "about two thirds of it when the light is doing this much itself.")]
    [Min(1f)] [SerializeField] float maxDrift = 55f;

    [Tooltip("Seconds the shutter is open: streak length is the speed the motes go by times this.")]
    [Range(0f, 0.2f)] [SerializeField] float exposure = 0.022f;

    [Tooltip("The streaks go on lengthening with the light's real speed: this much longer for every " +
             "500 units a second.")]
    [Range(0f, 3f)] [SerializeField] float streakGain = 0.8f;

    [Tooltip("The longest streak, units.")]
    [Min(0f)] [SerializeField] float maxStreak = 3.5f;

    [Header("Coming and going")]
    [Tooltip("Seconds after the emission before it starts to show, and seconds to come up.")]
    [SerializeField] Vector2 fadeIn = new Vector2(0.6f, 2.5f);

    [Tooltip("Hold the tutorial's own Streaks particle systems back while this is up.")]
    [SerializeField] bool holdOldStreaks = true;

    Material _material;
    Mesh _mesh;
    MeshRenderer _renderer;
    Transform _light;
    TutorialSpeedStreaks_NEW[] _oldStreaks;
    bool _oldHeld;
    Vector3 _offset;
    float _amount, _since;
    int _builtCount;

    static readonly int AmountId = Shader.PropertyToID("_Amount");
    static readonly int BoxId = Shader.PropertyToID("_Box");
    static readonly int CentreId = Shader.PropertyToID("_Centre");
    static readonly int OffsetId = Shader.PropertyToID("_Offset");
    static readonly int StreakId = Shader.PropertyToID("_Streak");
    static readonly int SizeId = Shader.PropertyToID("_Size");
    static readonly int AmbientId = Shader.PropertyToID("_Ambient");
    static readonly int LightColorId = Shader.PropertyToID("_LampColor");
    static readonly int LightReachId = Shader.PropertyToID("_LightReach");
    static readonly int WarmId = Shader.PropertyToID("_Warm");
    static readonly int CoolId = Shader.PropertyToID("_Cool");
    static readonly int FocusId = Shader.PropertyToID("_Focus");

    // ── Adds itself ─────────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Install();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { Install(); }

    static void Install()
    {
        if (FindObjectOfType<TutorialLightDust_NEW>() != null) return;

        TutorialEmission_NEW found = FindObjectOfType<TutorialEmission_NEW>();
        if (found == null) return;

        var made = new GameObject("Light Dust");
        made.transform.SetParent(found.transform, false);
        made.AddComponent<TutorialLightDust_NEW>().emission = found;
    }

    // ── Building ────────────────────────────────────────────────────────────

    void Awake()
    {
        Shader shader = Resources.Load<Shader>("LightDust_NEW");
        if (shader == null)
        {
            Debug.LogWarning("[TutorialLightDust_NEW] Resources/LightDust_NEW.shader is missing; no dust is drawn.", this);
            enabled = false;
            return;
        }

        _material = new Material(shader) { name = "Light dust (runtime)" };

        _renderer = gameObject.GetComponent<MeshRenderer>();
        if (_renderer == null) _renderer = gameObject.AddComponent<MeshRenderer>();
        _renderer.sharedMaterial = _material;
        _renderer.shadowCastingMode = ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
        _renderer.lightProbeUsage = LightProbeUsage.Off;
        _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        _renderer.enabled = false;

        BuildMesh();
    }

    void Start()
    {
        if (emission == null) emission = GetComponentInParent<TutorialEmission_NEW>();
        if (emission == null) emission = FindObjectOfType<TutorialEmission_NEW>();
        if (travel == null && emission != null) travel = emission.GetComponent<TutorialTravel_NEW>();
        if (travel == null) travel = FindObjectOfType<TutorialTravel_NEW>();

        // The light as it is seen sits a little under the travelling object: where its glow is.
        TutorialPhotonGlow_NEW glow = travel != null ? travel.GetComponentInChildren<TutorialPhotonGlow_NEW>(true) : null;
        _light = glow != null ? glow.transform : (travel != null ? travel.transform : transform);

        _oldStreaks = FindObjectsOfType<TutorialSpeedStreaks_NEW>();
    }

    /// <summary>
    /// One quad a mote. Its seed (where in the cube) goes in the position, its corner in uv, and
    /// four numbers of its own in uv2. The shader does the rest.
    /// </summary>
    void BuildMesh()
    {
        if (_mesh != null) Destroy(_mesh);

        int n = Mathf.Clamp(count, 100, 6000);
        var vertices = new Vector3[n * 4];
        var corners = new Vector2[n * 4];
        var own = new Vector4[n * 4];
        var triangles = new int[n * 6];

        var random = new System.Random(20231);
        for (int i = 0; i < n; i++)
        {
            var seed = new Vector3((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble());
            var numbers = new Vector4((float)random.NextDouble(), (float)random.NextDouble(),
                                      (float)random.NextDouble(), (float)random.NextDouble());
            int v = i * 4;
            for (int c = 0; c < 4; c++)
            {
                vertices[v + c] = seed;
                own[v + c] = numbers;
            }
            corners[v] = new Vector2(-1f, -1f);
            corners[v + 1] = new Vector2(1f, -1f);
            corners[v + 2] = new Vector2(1f, 1f);
            corners[v + 3] = new Vector2(-1f, 1f);

            int t = i * 6;
            triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
            triangles[t + 3] = v; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
        }

        _mesh = new Mesh { name = "Light dust" };
        _mesh.vertices = vertices;
        _mesh.uv = corners;
        _mesh.SetUVs(1, new System.Collections.Generic.List<Vector4>(own));
        _mesh.triangles = triangles;
        // The shader places every mote: the bounds only have to keep it from being culled.
        _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (box * 2f + 40f));

        MeshFilter filter = gameObject.GetComponent<MeshFilter>();
        if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
        filter.sharedMesh = _mesh;
        _builtCount = count;
    }

    // ── Every frame ─────────────────────────────────────────────────────────

    void LateUpdate()
    {
        if (_material == null) return;

        float dt = TutorialClock_NEW.DeltaTime;
        bool emitted = emission != null && emission.Emitted;

        // Up after the emission, a moment late so the light is seen to go first; gone at once
        // with the attract reset, which puts everything else back at once too.
        if (emitted)
        {
            _since += dt;
            if (_since >= fadeIn.x) _amount = Mathf.MoveTowards(_amount, 1f, dt / Mathf.Max(0.05f, fadeIn.y));
        }
        else
        {
            _since = 0f;
            _amount = 0f;
        }

        bool on = _amount > 0.001f;
        if (_renderer.enabled != on) _renderer.enabled = on;
        HoldOldStreaks(on);
        if (!on) return;

        if (_builtCount != count) BuildMesh();

        // The object carries no turn or scale of its own: the shader works in the world.
        Vector3 centre = _light != null ? _light.position : transform.position;
        transform.SetPositionAndRotation(centre, Quaternion.identity);

        float speed = travel != null ? Mathf.Max(0f, travel.CurrentSpeed) : 0f;
        Vector3 course = travel != null && travel.Direction.sqrMagnitude > 1e-6f ? travel.Direction.normalized : Vector3.forward;

        // How fast the motes go by: with the light's speed at first, levelling off.
        float drift = maxDrift * (1f - Mathf.Exp(-speed / Mathf.Max(1f, maxDrift)));
        _offset += course * (drift * dt / Mathf.Max(1f, box));
        _offset.x -= Mathf.Floor(_offset.x);        // only its fraction is read: kept small
        _offset.y -= Mathf.Floor(_offset.y);
        _offset.z -= Mathf.Floor(_offset.z);

        float streak = Mathf.Min(maxStreak, drift * exposure * (1f + streakGain * speed / 500f));

        float smooth = _amount * _amount * (3f - 2f * _amount);
        _material.SetFloat(AmountId, smooth * brightness);
        _material.SetFloat(BoxId, box);
        _material.SetVector(CentreId, centre);
        _material.SetVector(OffsetId, _offset);
        _material.SetVector(StreakId, course * streak);
        _material.SetFloat(SizeId, size);
        _material.SetFloat(AmbientId, ambient);
        _material.SetColor(LightColorId, lightColour);
        _material.SetFloat(LightReachId, lightReach);
        _material.SetColor(WarmId, warm);
        _material.SetColor(CoolId, cool);
        _material.SetFloat(FocusId, focusDistance);
    }

    /// <summary>The old streaks stop being made while this is up, and start again when it goes.</summary>
    void HoldOldStreaks(bool hold)
    {
        hold &= holdOldStreaks;
        if (hold == _oldHeld || _oldStreaks == null) return;
        _oldHeld = hold;

        for (int i = 0; i < _oldStreaks.Length; i++)
        {
            ParticleSystem particles = _oldStreaks[i] != null ? _oldStreaks[i].GetComponent<ParticleSystem>() : null;
            if (particles == null) continue;
            ParticleSystem.EmissionModule module = particles.emission;
            module.enabled = !hold;
        }
    }

    void OnDisable()
    {
        HoldOldStreaks(false);
        if (_renderer != null) _renderer.enabled = false;
        _amount = 0f;
    }

    void OnDestroy()
    {
        if (_material != null) Destroy(_material);
        if (_mesh != null) Destroy(_mesh);
    }
}

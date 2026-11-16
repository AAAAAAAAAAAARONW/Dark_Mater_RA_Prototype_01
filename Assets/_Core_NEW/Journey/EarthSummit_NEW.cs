using System.Collections.Generic;
using Cinemachine;
using UnityEngine;

/// <summary>
/// The end of the journey, before the telescope: down on Mount Wilson at night, the camera
/// lifting from the ground below to the horizon — the lights of Los Angeles spread out
/// beyond the dark ridges of the San Gabriels, the 100-inch telescope's dome on the summit
/// with its slit open, and the stars and the Milky Way over it all.
///
/// WHEN. EarthZoom_NEW's last stretch (THE SUMMIT): from about 190 km up the world grows a
/// hundredfold more about the point of ground below the light, until the camera is
/// summitCameraKm over it. This builds everything as that stretch begins, while the camera
/// still looks straight down, and takes the camera over part way through it.
///
/// THE GROUND. Earth's sphere is 128 x 64 flat faces, a couple of km below the true sphere
/// in the middle of each: fine from space, not to stand on. So a finer patch of the true
/// sphere is laid over the ground round the landing point, drawn with Earth's own surface
/// material — the same night map, the same Los Angeles close-up, the same clear sky round
/// the landing point — so the change cannot be seen, and the coarse sphere and its air are
/// put away. It reaches past the horizon.
///
/// THE MOUNTAINS AND THE DOME are built in a frame of km standing on the landing point
/// (x east, y up, z north), so they keep their shape however big the world grows round
/// them. The mountains are made up, not surveyed: a summit under the camera, the front of
/// the range dropping to the basin towards the city in spurs and valleys, ranges behind.
/// The dome is made too — a drum and a hemisphere with the slit open across the top — at
/// domeScale times its real size, so it reads at the distance it stands.
///
/// THE WAY IN. We are the light to the end: it is not left hanging in the sky but flown,
/// on a curve in the frame of km, from where it was down into the dome's open slit, and a
/// virtual camera of its own rides behind it as the journey's camera did - first at the
/// usual few units back, which the world's growth makes a long way down, closing in as it
/// comes. It starts where the live camera is, so the brain's blend changes nothing. The
/// light narrows on the way (lightShrink) so that it fits the slit, and goes in; the red
/// light inside flashes (EarthEnding_NEW), and after holdInside the picture cuts to the
/// observatory from outside: the camera's stand just over the summit, the dome to the left,
/// the city beyond, the stars and the Milky Way come up over it. (6 Nov: until then the
/// camera lifted to the horizon and a separate streak fell into the dome.) The light is let go of meanwhile: its trail thins away, then
/// it is hidden. The telescope is the next step's.
///
/// THE SKY. A skybox of its own (Resources/SummitSky_NEW), swapped in while the camera still
/// looks down, the stars and the Milky Way coming up as it lifts.
///
/// Put it on the Solar System's group root, beside EarthZoom_NEW. Everything it makes is
/// runtime-only and goes when it is disabled.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EarthZoom_NEW))]
[HierarchyBadge_NEW("SUMMIT", "#4A94F2")]
public class EarthSummit_NEW : MonoBehaviour
{
    [Header("Where")]
    [Tooltip("The city the camera turns to: latitude, longitude. Downtown Los Angeles.")]
    [SerializeField] Vector2 city = new Vector2(34.05f, -118.24f);

    [Tooltip("The summit's height, km: Mount Wilson, 1.74.")]
    [SerializeField] float summitKm = 1.74f;

    [Header("The way in")]
    [Tooltip("The light's size at the end, as a fraction of its own, so it fits the slit and the tube. " +
             "A unit wide, at 30 units a km: 0.05 is 1.7 m.")]
    [Range(0.01f, 1f)] [SerializeField] float lightShrink = 0.05f;

    [Tooltip("Seconds from the light going in to the cut to the observatory from outside.")]
    [Min(0f)] [SerializeField] float holdInside = 1.2f;

    [Header("The observatory from outside")]
    [Tooltip("Degrees the camera is turned from the city: negative to the left, towards the dome.")]
    [Range(-60f, 60f)] [SerializeField] float viewYaw = -8f;

    [Tooltip("Degrees the camera looks above the horizon.")]
    [Range(-30f, 30f)] [SerializeField] float viewPitch = 2f;

    [Header("Dome")]
    [Tooltip("Use the observatory model (Resources/Observatory telescope.fbx) when it is there; " +
             "off, or without it, the dome and telescope made here.")]
    [SerializeField] bool useObservatoryModel = true;

    [Tooltip("The model's size, times its own (a dome 27 m across). Bigger reads better from afar.")]
    [Range(0.5f, 4f)] [SerializeField] float observatoryScale = 1.5f;

    [Tooltip("Degrees from the way to the city the dome stands, as seen from the camera: negative is left.")]
    [Range(-60f, 60f)] [SerializeField] float domeYaw = -16f;

    [Tooltip("How far from the camera's spot the dome stands, km.")]
    [Range(0.1f, 3f)] [SerializeField] float domeKm = 0.3f;

    [Tooltip("Times its real size (a drum and dome 30 m across). Bigger reads better from afar.")]
    [Range(0.5f, 5f)] [SerializeField] float domeScale = 2.2f;

    [SerializeField] Color domeColour = new Color(0.75f, 0.77f, 0.8f, 1f);
    [Tooltip("The city's glow round the dome's edges, so it stands out against the dark.")]
    [SerializeField] Color domeRim = new Color(0.35f, 0.22f, 0.14f, 1f);
    [Tooltip("The faint light of the dome's inside walls, seen through the slit.")]
    [SerializeField] Color domeInside = new Color(0.03f, 0.03f, 0.035f, 1f);
    [Tooltip("The light inside the dome on the telescope: the sky's, from above, through the slit.")]
    [SerializeField] Color insideLight = new Color(0.38f, 0.4f, 0.46f, 1f);
    [SerializeField] Color mountainColour = new Color(0.06f, 0.06f, 0.065f, 1f);
    [SerializeField] Color mountainRim = new Color(0.05f, 0.035f, 0.03f, 1f);

    [Tooltip("The camera's height, km, below which Earth's coarse sphere and its air are put away " +
             "and only the fine patch round the landing point is drawn.")]
    [Min(1f)] [SerializeField] float hideSphereBelowKm = 100f;

    [Header("Light")]
    [SerializeField] Color moonlight = new Color(0.45f, 0.47f, 0.55f, 1f);
    [Tooltip("Light from the whole sky, on everything.")]
    [SerializeField] Color skyLight = new Color(0.03f, 0.032f, 0.045f, 1f);
    [SerializeField] Color cityGlow = new Color(0.3f, 0.17f, 0.08f, 1f);
    [SerializeField] Color haze = new Color(0.08f, 0.05f, 0.045f, 1f);
    [Tooltip("km over which the haze thickens on the mountains: short, so ridge behind ridge stands paler.")]
    [Min(1f)] [SerializeField] float hazeKm = 8f;

    [Header("Sky")]
    [ColorUsage(false, true)] [SerializeField] Color skyGlow = new Color(0.32f, 0.14f, 0.05f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color milkyWay = new Color(0.22f, 0.21f, 0.19f, 1f);
    [Range(0f, 4f)] [SerializeField] float stars = 1.2f;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    // Not saved, but listed in the Hierarchy while playing, so they can be found and looked at.
    const HideFlags Built = HideFlags.DontSave;
    const float CapLift = 1e-5f;   // the patch above the sphere, a fraction of the radius: 64 m

    EarthZoom_NEW _zoom;
    CinemachineBrain _brain;
    bool _built, _ending, _entered, _cut, _sphereHidden;
    float _insideClock;
    CinemachineBlendDefinition _blendBefore;
    bool _blendSwapped;

    readonly List<Object> _made = new List<Object>();
    Transform _frame;
    Material _setMountain, _setDome, _sky;
    readonly List<Material> _setMaterials = new List<Material>();
    Material _skyBefore;
    bool _skySwapped;

    CinemachineVirtualCamera _vcam;
    Quaternion _cutTo;
    Vector3 _photonScale;
    float _flatInner, _flatOuter;    // km round the dome that the mountains are levelled
    Vector2 _here;                   // the frame's origin on the maps: latitude, longitude
    Vector4 _rect;                   // where the close-up night map lies, degrees
    bool _drape;

    // The observatory model, in its own metres (y up, z north): the floor of the pit, the
    // mirror at the foot of the tube once turned to the zenith, how far the dome's shell is
    // slid along its slit so the light's line, through the tube, clears the cap at the top.
    const string ModelName = "Observatory telescope", ModelTube = "pCylinder289", ModelShell = "group5", ModelScope = "group3";
    const float ModelGroundY = -4.58f, ModelMirrorY = -2.9f, ModelShellShift = 2.5f;

    readonly List<Renderer> _scopeRenderers = new List<Renderer>();

    static GameObject _model;
    static bool _lookedForModel;

    static GameObject ObservatoryModel
    {
        get
        {
            if (!_lookedForModel) { _lookedForModel = true; _model = Resources.Load<GameObject>(ModelName); }
            return _model;
        }
    }

    bool UseModel => useObservatoryModel && ObservatoryModel != null;

    /// <summary>
    /// How high above sea level the light ends, km: the summit, and the telescope's mirror
    /// over it. EarthZoom_NEW fixes the arrival with it as the approach begins.
    /// </summary>
    public float ArrivalHeightKm => summitKm + 0.001f * (UseModel ? (ModelMirrorY - ModelGroundY) * observatoryScale : (MirrorY + 0.3f) * domeScale);
    Transform _dome;
    readonly List<Material> _insideLights = new List<Material>();
    readonly List<Color> _insideEmission = new List<Color>();

    // ── For EarthEnding_NEW ─────────────────────────────────────────────────

    /// <summary>The light has gone into the dome.</summary>
    public bool Entered => _entered;

    /// <summary>The picture has cut to the observatory from outside.</summary>
    public bool CutToOutside => _cut;

    /// <summary>Up at the summit, world space.</summary>
    public Vector3 Up => _up;

    /// <summary>Which way the camera ends up looking, level, world space.</summary>
    public Vector3 ViewLevel => Rotate(_toCity, viewYaw);

    /// <summary>World units a km, now.</summary>
    public float KmToWorld => _frame != null ? _frame.lossyScale.x : 1f;

    /// <summary>A point in the open slit, half way up it, world space.</summary>
    public Vector3 SlitPoint
    {
        get
        {
            if (_dome == null) return Vector3.zero;
            const float r = 15f, drum = 11f, el = 50f * Mathf.Deg2Rad;
            return _dome.TransformPoint(new Vector3(0f, drum + r * Mathf.Sin(el), r * Mathf.Cos(el)));
        }
    }

    /// <summary>Out of the slit, the way the light comes in from, world space.</summary>
    public Vector3 SlitOutward => _dome != null ? _dome.TransformDirection(new Vector3(0f, Mathf.Sin(50f * Mathf.Deg2Rad), Mathf.Cos(50f * Mathf.Deg2Rad))) : _up;

    /// <summary>The light inside the dome, for a flash as the light goes in.</summary>
    public void SetInsideLight(float times)
    {
        for (int i = 0; i < _insideLights.Count; i++)
            if (_insideLights[i] != null) _insideLights[i].SetColor("_Emission", _insideEmission[i] * times);
    }

    /// <summary>The open top of the telescope's tube, world space.</summary>
    public Vector3 TubeTop => _dome != null ? _dome.TransformPoint(Vector3.up * TubeTopY) : Vector3.zero;

    /// <summary>The telescope's mirror, world space: where the light ends.</summary>
    public Vector3 MirrorPoint => _dome != null ? _dome.TransformPoint(Vector3.up * (MirrorY + 0.3f)) : Vector3.zero;
    Vector3 _stand;   // the frame: where the camera stands for the view from outside

    readonly List<Renderer> _photonParts = new List<Renderer>();
    readonly List<Renderer> _systemParts = new List<Renderer>();
    readonly List<TrailRenderer> _trails = new List<TrailRenderer>();
    readonly List<float> _trailWidths = new List<float>();
    bool _photonHidden;

    // The frame at the landing point, world space, and the way to the city, level.
    Vector3 _up, _east, _north, _toCity;

    void Awake()
    {
        _zoom = GetComponent<EarthZoom_NEW>();
        _brain = FindObjectOfType<CinemachineBrain>();
    }

    // Update, after EarthZoom_NEW's (it runs early) has grown Earth for the frame, and before
    // the brain reads the camera.
    void Update()
    {
        if (!_built)
        {
            if (!_zoom.SummitStarted) return;
            _built = true;
            Build();
        }
        if (_frame == null) return;

        float kmToWorld = _frame.lossyScale.x;
        foreach (Material m in _setMaterials) m.SetFloat("_KmToWorld", kmToWorld);

        // Low enough that the patch of true sphere fills the view: the coarse sphere and its air
        // go (see Build).
        Camera low = GetReal3DView_NEW.ScreenCamera;
        if (!_sphereHidden && low != null && _frame.InverseTransformPoint(low.transform.position).y < hideSphereBelowKm)
        {
            _sphereHidden = true;
            _zoom.Body.SetAirVisible(false);
            SetSurfaceVisible(false);
        }

        // The mountains' haze comes in as the ground's does: with the camera about 30 km down
        // to 8 km over it (CelestialSurface_NEW's _GroundHazeFrom, as radius over height).
        Camera eye = GetReal3DView_NEW.ScreenCamera;
        if (_drape && eye != null)
        {
            float alt = Mathf.Max(_frame.InverseTransformPoint(eye.transform.position).y, 1e-3f);
            _setMountain.SetFloat("_HazeOn", Edge(200f, 800f, EarthZoom_NEW.EarthRadiusKm / alt));
        }

        if (_ending) Ending();
    }

    // While the debug overlay is on (F1), what the summit is doing, under the layer panel:
    // the camera that draws, how high it is over the summit's frame, how far the dome is,
    // and whether this summit's camera is the live one.
    void OnGUI()
    {
        if (!DebugView_NEW.Overlay || _frame == null) return;
        Camera cam = GetReal3DView_NEW.ScreenCamera;
        if (cam == null) return;

        Vector3 c = _frame.InverseTransformPoint(cam.transform.position);
        string live = _brain != null && _vcam != null && ReferenceEquals(_brain.ActiveVirtualCamera, _vcam) ? "yes" : "no";
        string text = "SUMMIT  camera " + c.y.ToString("0.000") + " km up, " + new Vector2(c.x, c.z).magnitude.ToString("0.00") +
                      " km aside   stand " + _stand.y.ToString("0.00") + " km   dome " +
                      (_dome != null ? (Vector3.Distance(cam.transform.position, _dome.position) / _frame.lossyScale.x).ToString("0.00") + " km" : "-") +
                      "   km = " + _frame.lossyScale.x.ToString("0.0000") + " units   summit camera live " + live +
                      "   last " + _zoom.FinalProgress.ToString("0.00") + "   clip " + cam.nearClipPlane.ToString("0.###") + "-" + cam.farClipPlane.ToString("0");
        // Whether each part is on, and whether any camera drew it last frame; where the dome
        // falls on the screen (0-1 across and up, and in front if z > 0).
        string parts = "";
        foreach (Object o in _made)
        {
            var go = o as GameObject;
            Renderer r = go != null ? go.GetComponent<Renderer>() : null;
            if (r == null) continue;
            parts += go.name + " " + (r.enabled ? "on" : "OFF") + "/" + (r.isVisible ? "drawn" : "NOT DRAWN") + " L" + go.layer + "   ";
        }
        Vector3 vp = _dome != null ? cam.WorldToViewportPoint(_dome.position) : Vector3.zero;
        int drawn = 0;
        foreach (Renderer r in _scopeRenderers) if (r != null && r.isVisible) drawn++;
        if (_scopeRenderers.Count > 0) parts += "telescope parts drawn " + drawn + "/" + _scopeRenderers.Count + "   ";
        parts += "dome on screen " + vp.x.ToString("0.00") + ", " + vp.y.ToString("0.00") + ", z " + vp.z.ToString("0.0") +
                 "   cull mask " + cam.cullingMask + "   drawing camera " + (Camera.current != null ? Camera.current.name : "-");

        GUI.color = Color.yellow;
        GUI.Label(new Rect(10, Screen.height - 150, Screen.width - 20, 22), parts);
        GUI.Label(new Rect(10, Screen.height - 128, Screen.width - 20, 22), text);
    }

    void OnDisable()
    {
        if (_skySwapped) { RenderSettings.skybox = _skyBefore; _skySwapped = false; }
        ShowPhoton();
        foreach (Renderer r in _systemParts) if (r != null) r.enabled = true;
        _systemParts.Clear();
        if (_built && _zoom != null && _zoom.Body != null && _zoom.Earth != null) { _zoom.Body.SetAirVisible(true); SetSurfaceVisible(true); }
        if (_zoom != null && _zoom.Photon != null && _ending) _zoom.Photon.transform.localScale = _photonScale;
        if (_blendSwapped && _brain != null) { _brain.m_DefaultBlend = _blendBefore; _blendSwapped = false; }
        foreach (Object o in _made) Kill(o);
        _made.Clear();
        _setMaterials.Clear();
        _insideLights.Clear();
        _insideEmission.Clear();
        _frame = null;
        _vcam = null;
        _built = _ending = _entered = _cut = _sphereHidden = false;
    }

    // ── Building ────────────────────────────────────────────────────────────

    void Build()
    {
        CelestialBody_NEW body = _zoom.Body;
        Transform earth = _zoom.Earth;
        Camera cam = GetReal3DView_NEW.ScreenCamera;
        if (body == null || body.SurfaceMaterial == null || earth == null || cam == null)
        {
            Debug.LogWarning("[EarthSummit_NEW] No Earth surface or no camera to build the summit for.", this);
            return;
        }

        // The frame at the landing point. North is towards Earth's pole, as the maps have it.
        Vector3 ground = _zoom.GroundPoint;
        _up = _zoom.GroundUp.normalized;
        Vector3 pole = earth.TransformDirection(Vector3.up);
        _north = (pole - _up * Vector3.Dot(pole, _up)).normalized;
        _east = Vector3.Cross(_up, _north);

        // Where the landing point is on the maps, and which way the city lies from it.
        Vector3 groundObj = earth.InverseTransformPoint(ground).normalized;
        Vector2 here = LatLon(groundObj);
        Vector3 cityWorld = earth.TransformPoint(Direction(city.x, city.y) * body.MeshRadius);
        _toCity = Level(cityWorld - ground);

        // The camera's stand for the view from outside: just over the summit.
        float kmNow = body.GroundRadius / EarthZoom_NEW.EarthRadiusKm;
        float standKm = Mathf.Clamp(_zoom.SummitCameraKm, summitKm + 0.02f, summitKm + 0.3f);
        Debug.Log("[EarthSummit_NEW] Building the summit at " + here.x.ToString("0.00") + ", " + here.y.ToString("0.00") +
                  "; the city " + (Vector3.Distance(cityWorld, ground) / kmNow).ToString("0") + " km away.", this);

        // The finer patch of the true sphere. The coarse one and its air are put away once the
        // camera is low enough for the patch to fill the view (see Update): built now, the
        // patch is a few hundred km across under a camera a thousand km up, and put away now
        // the rest of Earth went with it (16 Nov: "a black square").
        BuildCap(earth, body, here);

        // The frame of km, standing on the landing point.
        var frameGo = Make("Summit (km)");
        _frame = frameGo.transform;
        _frame.SetParent(earth, false);
        _frame.position = ground;
        _frame.rotation = Quaternion.LookRotation(_north, _up);
        _frame.localScale = Vector3.one * (body.MeshRadius / EarthZoom_NEW.EarthRadiusKm);

        Shader set = Resources.Load<Shader>("SummitSet_NEW");
        if (set == null) Debug.LogError("[EarthSummit_NEW] Resources/SummitSet_NEW.shader is missing: no mountains or dome.", this);
        else if (!set.isSupported) Debug.LogError("[EarthSummit_NEW] SummitSet_NEW does not run here (a compile error?): the mountains and dome will not show.", this);
        _setMountain = Lit(set, mountainColour);
        _setMountain.SetColor("_Rim", mountainRim);
        // The mountains wear the ground's own night, so from above they are the land round
        // them and not a dark patch on it; hazed as the ground is.
        _here = here;
        _drape = body.RegionMap != null;
        if (_drape)
        {
            _setMountain.SetTexture("_GroundMap", body.RegionMap);
            _setMountain.SetFloat("_GroundOn", 1f);
            _setMountain.SetFloat("_GroundExposure", body.RegionExposure);
            _setMountain.SetFloat("_CityGlow", body.CityGlow);
            _setMountain.SetColor("_HazeColor", body.GroundHaze);
            _setMountain.SetFloat("_HazeKm", body.GroundHazeKm);
            _rect = body.RegionRect;
        }
        _setDome = Lit(set, domeColour);
        _setDome.SetColor("_Rim", domeRim);

        // In the frame: the way to the city; the observatory under the arrival, the
        // telescope's mirror on it - the light comes down its own line into the tube; and
        // the camera's stand for the view from outside, placed so the dome stands domeKm off
        // at domeYaw from the way to the city.
        Vector3 cityKm = _frame.InverseTransformDirection(_toCity);
        Vector2 toward = new Vector2(cityKm.x, cityKm.z).normalized;
        Vector3 arriveKm = _frame.InverseTransformPoint(_zoom.ArrivalPoint);
        Vector2 domeAt = new Vector2(arriveKm.x, arriveKm.z);
        float domeGround = arriveKm.y - (ArrivalHeightKm - summitKm);
        Vector2 spot = domeAt - Rotate(toward, domeYaw) * domeKm;
        _stand = new Vector3(spot.x, standKm, spot.y);

        bool model = UseModel;
        _flatInner = model ? 0.022f * observatoryScale : 0.03f * domeScale;
        _flatOuter = model ? 0.05f * observatoryScale : 0.07f * domeScale;
        Part("Mountains", BuildMountains(spot, toward, standKm, domeAt, domeGround), _setMountain, _frame);

        // The slit opens three-quarters towards the camera, so it shows.
        Vector2 faces = Rotate((spot - domeAt).normalized, 50f);
        if (model)
        {
            BuildObservatory(domeAt, domeGround, faces, set);
        }
        else
        {
            var dome = Make("Dome").transform;
            _dome = dome;
            dome.SetParent(_frame, false);
            dome.localPosition = new Vector3(domeAt.x, domeGround, domeAt.y);
            dome.localRotation = Quaternion.LookRotation(new Vector3(faces.x, 0f, faces.y), Vector3.up);
            dome.localScale = Vector3.one * 0.001f * domeScale;   // metres
            BuildDome(dome);
        }

        // The view from outside, cut to after the light goes in.
        Vector3 level = Rotate(_toCity, viewYaw);
        _cutTo = Quaternion.LookRotation(level * Mathf.Cos(viewPitch * Mathf.Deg2Rad) + _up * Mathf.Sin(viewPitch * Mathf.Deg2Rad), _up);

        // The sky, while the camera still looks at the ground.
        BuildSky();

        // The rest of the Solar System, left behind by EarthZoom_NEW when it took Earth out:
        // no planet belongs in this sky.
        foreach (Renderer r in GetComponentsInChildren<Renderer>(false))
            if (r.enabled) { r.enabled = false; _systemParts.Add(r); }

        // The light, to be narrowed into the tube and then hidden in it.
        PlayerRig_NEW photon = _zoom.Photon;
        if (photon != null)
        {
            _ending = true;
            _photonScale = photon.transform.localScale;
            _photonParts.AddRange(photon.GetComponentsInChildren<Renderer>(false));
            foreach (TrailRenderer t in photon.GetComponentsInChildren<TrailRenderer>(false))
            {
                _trails.Add(t);
                _trailWidths.Add(t.widthMultiplier);
            }
        }
    }

    void SetSurfaceVisible(bool visible)
    {
        Material surface = _zoom.Body != null ? _zoom.Body.SurfaceMaterial : null;
        if (surface == null) return;
        foreach (Transform t in _zoom.Earth)
        {
            // The body's own parts are hidden objects; the surface is the one drawing with its material.
            var r = t.GetComponent<MeshRenderer>();
            if (r != null && r.sharedMaterial == surface && !_made.Contains(t.gameObject)) r.enabled = visible;
        }
    }

    Material Lit(Shader shader, Color albedo)
    {
        var m = new Material(shader) { hideFlags = Built };
        m.SetColor("_Albedo", albedo);
        // The moon high behind the camera, to its left: the dome's near side catches it.
        m.SetVector("_MoonDir", (_up * 0.6f + Rotate(_toCity, -150f) * 0.8f).normalized);
        m.SetColor("_MoonColor", moonlight);
        m.SetVector("_GlowDir", (_toCity - _up * 0.25f).normalized);
        m.SetColor("_GlowColor", cityGlow);
        m.SetColor("_HazeColor", haze);
        m.SetColor("_Ambient", skyLight);
        m.SetFloat("_HazeKm", hazeKm);
        _made.Add(m);
        _setMaterials.Add(m);
        return m;
    }

    void BuildSky()
    {
        Shader shader = Resources.Load<Shader>("SummitSky_NEW");
        if (shader == null) { Debug.LogWarning("[EarthSummit_NEW] Resources/SummitSky_NEW is missing; the sky is left as it is.", this); return; }

        _sky = new Material(shader) { hideFlags = Built };
        _made.Add(_sky);
        _sky.SetTexture("_Noise", QuasarNoise_NEW.Texture);
        _sky.SetVector("_Up", _up);
        _sky.SetVector("_ToCity", _toCity);
        _sky.SetColor("_Glow", skyGlow);
        _sky.SetColor("_MilkyWay", milkyWay);
        _sky.SetFloat("_StarBrightness", stars);
        _sky.SetFloat("_Reveal", 0f);

        // The galaxy's centre low over the horizon a little right of the city; the band
        // rising from it up over the sky to the left, as the summer Milky Way stands, and
        // across the picture the camera ends on.
        Vector3 centre = (Rotate(_toCity, 12f) * Mathf.Cos(10f * Mathf.Deg2Rad) + _up * Mathf.Sin(10f * Mathf.Deg2Rad)).normalized;
        Vector3 along = (Rotate(_toCity, -45f) * Mathf.Cos(55f * Mathf.Deg2Rad) + _up * Mathf.Sin(55f * Mathf.Deg2Rad)).normalized;
        Vector3 galPole = Vector3.Cross(centre, along).normalized;
        _sky.SetVector("_GalPole", galPole);
        _sky.SetVector("_GalA", centre);
        _sky.SetVector("_GalB", Vector3.Cross(galPole, centre));

        _skyBefore = RenderSettings.skybox;
        RenderSettings.skybox = _sky;
        _skySwapped = true;
    }

    // ── The camera ──────────────────────────────────────────────────────────

    /// <summary>
    /// The last few seconds, as EarthZoom_NEW flies the light in: it narrows to fit the slit
    /// and the tube, the stars come up; at the mirror it is hidden, and after holdInside the
    /// picture cuts to the observatory from outside (see THE WAY IN).
    /// </summary>
    void Ending()
    {
        PlayerRig_NEW photon = _zoom.Photon;
        float last = _zoom.FinalProgress;

        if (!_entered)
        {
            float size = Mathf.Lerp(1f, lightShrink, Edge(0f, 0.8f, last));
            photon.transform.localScale = _photonScale * size;
            for (int i = 0; i < _trails.Count; i++)
                if (_trails[i] != null) _trails[i].widthMultiplier = _trailWidths[i] * size;

            if (_sky != null) _sky.SetFloat("_Reveal", last);

            if (_zoom.Arrived)
            {
                _entered = true;
                _insideClock = 0f;
                HidePhoton();
            }
            return;
        }

        _insideClock += Time.deltaTime;
        if (!_cut && _insideClock >= holdInside) Cut();
        else if (_cut && _vcam != null)
        {
            _vcam.transform.position = _frame.TransformPoint(_stand);
            // The brain's own blend back, once the cut has been taken.
            if (_blendSwapped && _insideClock >= holdInside + 0.5f) { _brain.m_DefaultBlend = _blendBefore; _blendSwapped = false; }
        }
    }

    /// <summary>To the observatory from outside: a camera of its own on the stand, taken at once, not blended to.</summary>
    void Cut()
    {
        _cut = true;
        Camera cam = GetReal3DView_NEW.ScreenCamera;

        var go = Make("VCam_Summit");
        go.transform.SetPositionAndRotation(_frame.TransformPoint(_stand), _cutTo);
        _vcam = go.AddComponent<CinemachineVirtualCamera>();
        if (cam != null)
        {
            LensSettings lens = _vcam.m_Lens;
            lens.FieldOfView = cam.fieldOfView;
            lens.NearClipPlane = cam.nearClipPlane;
            lens.FarClipPlane = cam.farClipPlane;
            _vcam.m_Lens = lens;
        }

        if (_brain != null)
        {
            _blendBefore = _brain.m_DefaultBlend;
            _brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.Cut, 0f);
            _blendSwapped = true;
        }
        _vcam.Priority = 90;   // over CameraDirector_NEW's 50: nothing else is to move it now
        if (_sky != null) _sky.SetFloat("_Reveal", 1f);
    }

    /// <summary>
    /// The observatory model, standing on the ground under the arrival at observatoryScale:
    /// the telescope (group3) as it is, true to the north - its tube turned to the zenith on
    /// its declination axis, so the light comes straight down it to the mirror; the dome's
    /// shell (group5) turned so its slit shows from the stand, and slid along the slit so the
    /// light's line clears the cap at the top. The model has no materials of its own worth
    /// keeping: the shell takes the dome's night light, the telescope the red light inside.
    /// </summary>
    void BuildObservatory(Vector2 domeAt, float domeGround, Vector2 faces, Shader set)
    {
        var holder = Make("Observatory").transform;
        _dome = holder;
        holder.SetParent(_frame, false);
        holder.localPosition = new Vector3(domeAt.x, domeGround, domeAt.y);
        holder.localRotation = Quaternion.identity;
        holder.localScale = Vector3.one * 0.001f * observatoryScale;   // metres

        GameObject model = Instantiate(ObservatoryModel, holder, false);
        model.hideFlags = Built;
        _made.Add(model);
        model.transform.localPosition = new Vector3(0f, -ModelGroundY, 0f);
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        Transform tube = FindDeep(model.transform, ModelTube);
        if (tube != null) tube.localRotation = Quaternion.identity;
        else Debug.LogWarning("[EarthSummit_NEW] No '" + ModelTube + "' (the tube) in the observatory model: it is left as it points.", this);

        Transform shell = FindDeep(model.transform, ModelShell);
        if (shell != null)
        {
            // The slit is on the shell's -z side: turn that to face.
            shell.localRotation = Quaternion.Euler(0f, Mathf.Atan2(-faces.x, -faces.y) * Mathf.Rad2Deg, 0f);
            shell.localPosition = shell.localRotation * new Vector3(0f, 0f, ModelShellShift);
        }

        Transform scope = FindDeep(model.transform, ModelScope);
        Material metal = Inside(set, new Color(0.6f, 0.62f, 0.66f, 1f), new Color(0.015f, 0.015f, 0.018f, 1f));
        Material outside = Lit(set, domeColour);
        outside.SetColor("_Rim", domeRim);
        outside.SetColor("_Emission", new Color(0.02f, 0.02f, 0.025f, 1f));
        _scopeRenderers.Clear();

        int count = 0;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
        {
            bool inScope = scope != null && r.transform.IsChildOf(scope);
            if (inScope) _scopeRenderers.Add(r);
            Material m = inScope ? metal : outside;
            var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
            for (int i = 0; i < mats.Length; i++) mats[i] = m;
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            r.gameObject.layer = gameObject.layer;
            count++;
        }
        Debug.Log("[EarthSummit_NEW] The observatory model: " + count + " parts, tube " + (tube != null ? "to the zenith" : "as it was") +
                  ", shell " + (shell != null ? "turned" : "not found") + ".", this);
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    void HidePhoton()
    {
        if (_photonHidden) return;
        _photonHidden = true;
        foreach (Renderer r in _photonParts) if (r != null) r.enabled = false;
    }

    void ShowPhoton()
    {
        for (int i = 0; i < _trails.Count; i++)
            if (_trails[i] != null) _trails[i].widthMultiplier = _trailWidths[i];
        if (_photonHidden)
            foreach (Renderer r in _photonParts) if (r != null) r.enabled = true;
        _photonHidden = false;
        _photonParts.Clear();
        _trails.Clear();
        _trailWidths.Clear();
    }

    // ── The ground ──────────────────────────────────────────────────────────

    /// <summary>The true sphere round the landing point, in Earth's object space, its uv as the maps lie.</summary>
    void BuildCap(Transform earth, CelestialBody_NEW body, Vector2 here)
    {
        const int across = 200, down = 160;
        const float halfLon = 2.5f, halfLat = 2f;
        float radius = body.MeshRadius * (1f + CapLift);

        var vertices = new Vector3[(across + 1) * (down + 1)];
        var normals = new Vector3[vertices.Length];
        var uv = new Vector2[vertices.Length];
        for (int y = 0; y <= down; y++)
        for (int x = 0; x <= across; x++)
        {
            float lat = here.x + Mathf.Lerp(-halfLat, halfLat, y / (float)down);
            float lon = here.y + Mathf.Lerp(-halfLon, halfLon, x / (float)across);
            Vector3 n = Direction(lat, lon);
            int i = y * (across + 1) + x;
            vertices[i] = n * radius;
            normals[i] = n;
            uv[i] = new Vector2(Mathf.Repeat((lon + 180f) / 360f, 1f), 0.5f + lat / 180f);
        }

        // Wound to face out, as PlanetSphere_NEW's: east along a row, north up the rows.
        var triangles = new int[across * down * 6];
        int k = 0;
        for (int y = 0; y < down; y++)
        for (int x = 0; x < across; x++)
        {
            int i = y * (across + 1) + x, j = i + across + 1;
            triangles[k++] = i; triangles[k++] = j; triangles[k++] = i + 1;
            triangles[k++] = i + 1; triangles[k++] = j; triangles[k++] = j + 1;
        }

        Mesh mesh = NewMesh("Summit ground", vertices, triangles);
        mesh.normals = normals;
        mesh.uv = uv;
        FixWinding(mesh, earth.InverseTransformPoint(_zoom.GroundPoint + _up), earth.InverseTransformPoint(_zoom.GroundPoint));

        GameObject cap = Part("Ground (true sphere)", mesh, body.SurfaceMaterial, earth);
        cap.transform.localPosition = Vector3.zero;
        cap.transform.localRotation = Quaternion.identity;
        cap.transform.localScale = Vector3.one;
    }

    /// <summary>
    /// Height of the made-up mountains, km, at p in the frame: the summit at spot, the front
    /// of the range falling away towards the city, spurs and valleys down it, ranges behind.
    /// Flattened round the dome (dome, flat) and kept below the camera.
    /// </summary>
    float Mountains(Vector2 p, Vector2 spot, Vector2 toward, float cameraKm, Vector2 dome, float flat)
    {
        Vector2 d = p - spot;
        float s = Vector2.Dot(d, toward);                         // towards the city
        float t = Vector2.Dot(d, new Vector2(-toward.y, toward.x)); // across

        // Steep from the summit's edge, as Mount Wilson falls to the basin.
        float front = 1f - Edge(0.15f, 3f, s);
        float h = Mathf.Lerp(-0.4f, summitKm, front);

        // Spurs running down towards the city, valleys between them, on the slope only.
        float slope = Edge(-0.5f, 2f, s) * (1f - Edge(5.5f, 8f, s));
        float spur = 0.5f + 0.5f * Mathf.Sin(t * 1.35f + (Mathf.PerlinNoise(s * 0.25f + 3.1f, t * 0.15f + 7.7f) - 0.5f) * 6f);
        h -= (1f - spur) * slope * 0.55f;

        // Foothills between the summit and the city, ridge behind ridge, each lower: their
        // crests stand dark against the lights.
        for (int i = 0; i < 3; i++)
        {
            float at = 2.4f + 2.3f * i, crest = 1.35f - 0.3f * i;
            float wander = (Mathf.PerlinNoise(t * 0.22f + 13f * i, 2.3f * i) - 0.5f) * 1.4f;
            float ridge = Mathf.Exp(-Mathf.Pow((s - at - wander) / 0.55f, 2f));
            float height = crest * (0.75f + 0.5f * Mathf.PerlinNoise(t * 0.35f + 7f * i, 5.1f));
            h = Mathf.Max(h, Mathf.Lerp(-0.4f, height, ridge));
        }

        // Roughness, and ranges behind.
        float rough = (Mathf.PerlinNoise(s * 0.6f + 11f, t * 0.6f + 5f) - 0.5f) * 0.25f
                    + (Mathf.PerlinNoise(s * 1.7f + 2f, t * 1.7f + 9f) - 0.5f) * 0.08f;
        h += rough * front;
        h += Mathf.PerlinNoise(s * 0.18f + 4f, t * 0.18f + 1f) * 0.35f * (1f - Edge(-4f, -1f, s));

        // A rounded summit under the camera, and kept well below it.
        float r = d.magnitude;
        h = Mathf.Lerp(summitKm + 0.01f * Mathf.Cos(r * 4f), h, Edge(0.25f, 0.7f, r));
        h = Mathf.Min(h, cameraKm - 0.04f);

        if (flat > 0f || dome != Vector2.zero)
            h = Mathf.Lerp(flat, h, Edge(_flatInner, _flatOuter, (p - dome).magnitude));
        return h;
    }

    Mesh BuildMountains(Vector2 spot, Vector2 toward, float cameraKm, Vector2 dome, float domeGround)
    {
        const int along = 140, across = 180;
        const float back = 14f, ahead = 9f, side = 28f;
        Vector2 perp = new Vector2(-toward.y, toward.x);

        var vertices = new Vector3[(along + 1) * (across + 1)];
        var uv = new Vector2[vertices.Length];
        for (int a = 0; a <= along; a++)
        for (int b = 0; b <= across; b++)
        {
            float s = Mathf.Lerp(-back, ahead, a / (float)along);
            float t = Mathf.Lerp(-side, side, b / (float)across);
            Vector2 p = spot + toward * s + perp * t;
            float h = Mountains(p, spot, toward, cameraKm, dome, domeGround);
            // The edges sink away under the ground, so it has no edge to see.
            h = Mathf.Lerp(h, -0.6f, Mathf.Max(Edge(side * 0.8f, side, Mathf.Abs(t)), Edge(-back * 0.8f, -back, s)));
            vertices[a * (across + 1) + b] = new Vector3(p.x, h, p.y);
            // Where it is on the close-up night map (the frame's km, east and north, to degrees).
            if (_drape)
            {
                float lat = _here.x + p.y / 110.95f, lon = _here.y + p.x / (111.32f * Mathf.Cos(_here.x * Mathf.Deg2Rad));
                uv[a * (across + 1) + b] = new Vector2((lon - _rect.x) / (_rect.z - _rect.x), (lat - _rect.y) / (_rect.w - _rect.y));
            }
        }

        var triangles = new int[along * across * 6];
        int k = 0;
        for (int a = 0; a < along; a++)
        for (int b = 0; b < across; b++)
        {
            int i = a * (across + 1) + b, j = i + across + 1;
            triangles[k++] = i; triangles[k++] = j; triangles[k++] = i + 1;
            triangles[k++] = i + 1; triangles[k++] = j; triangles[k++] = j + 1;
        }

        Mesh mesh = NewMesh("Summit mountains", vertices, triangles);
        mesh.uv = uv;
        FixWinding(mesh, Vector3.up * 10f, Vector3.zero);
        mesh.RecalculateNormals();
        return mesh;
    }

    // ── The dome ────────────────────────────────────────────────────────────

    /// <summary>
    /// The 100-inch's dome, in metres: a drum 15 m round and 11 high, a hemisphere on it, the
    /// slit 5 m wide open from just past the top down the far side (+z), and inside it the
    /// telescope pointing straight up at the top of the slit, under a dim red light.
    /// </summary>
    void BuildDome(Transform dome)
    {
        const float r = 15f, drum = 11f, slit = 2.5f;
        const int around = 64, up = 20;

        var outV = new List<Vector3>();
        var outT = new List<int>();
        var inV = new List<Vector3>();
        var inT = new List<int>();

        // The drum, open at the top where the dome sits; and its wall inside, facing in.
        for (int i = 0; i <= around; i++)
        {
            float a = i * Mathf.PI * 2f / around;
            Vector3 rim = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            outV.Add(rim * r - Vector3.up * 2f);   // into the ground a little
            outV.Add(rim * r + Vector3.up * drum);
            inV.Add(rim * (r - 0.4f));
            inV.Add(rim * (r - 0.4f) + Vector3.up * drum);
        }
        for (int i = 0; i < around; i++)
        {
            int v = i * 2;
            outT.AddRange(new[] { v, v + 2, v + 1, v + 2, v + 3, v + 1 });
            inT.AddRange(new[] { v, v + 1, v + 2, v + 2, v + 1, v + 3 });
        }

        // The hemisphere, less the slit, outside and in.
        int baseOut = outV.Count, baseIn = inV.Count;
        for (int j = 0; j <= up; j++)
        for (int i = 0; i <= around; i++)
        {
            float el = j * Mathf.PI * 0.5f / up, a = i * Mathf.PI * 2f / around;
            Vector3 n = new Vector3(Mathf.Sin(a) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(a) * Mathf.Cos(el));
            outV.Add(Vector3.up * drum + n * r);
            inV.Add(Vector3.up * drum + n * (r - 0.4f));
        }
        for (int j = 0; j < up; j++)
        for (int i = 0; i < around; i++)
        {
            int v = j * (around + 1) + i, w = v + around + 1;
            float el = (j + 0.5f) * Mathf.PI * 0.5f / up, a = (i + 0.5f) * Mathf.PI * 2f / around;
            Vector3 c = new Vector3(Mathf.Sin(a) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(a) * Mathf.Cos(el)) * r;
            // In the slit: narrow across x, from a little past the top (z > -0.3 r) down the far side.
            bool open = Mathf.Abs(c.x) < slit && c.z > -0.3f * r && el > 0.2f;
            if (open) continue;
            outT.AddRange(new[] { baseOut + v, baseOut + v + 1, baseOut + w, baseOut + v + 1, baseOut + w + 1, baseOut + w });
            inT.AddRange(new[] { baseIn + v, baseIn + w, baseIn + v + 1, baseIn + v + 1, baseIn + w, baseIn + w + 1 });
        }

        // The floor, at the ground.
        int floor = inV.Count;
        inV.Add(Vector3.up * 0.3f);
        for (int i = 0; i <= around; i++)
        {
            float a = i * Mathf.PI * 2f / around;
            inV.Add(Vector3.up * 0.3f + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * (r - 0.4f));
        }
        for (int i = 0; i < around; i++) inT.AddRange(new[] { floor, floor + 1 + i, floor + 2 + i });

        // Wound by hand (FixWinding cannot tell out from in on the drum's upright walls):
        // drum and dome face out, the inside faces in, the floor up.
        Mesh outside = NewMesh("Dome", outV.ToArray(), outT.ToArray());
        outside.RecalculateNormals();
        Part("Dome outside", outside, _setDome, dome);

        Shader set = Resources.Load<Shader>("SummitSet_NEW");
        Material dark = Inside(set, new Color(0.06f, 0.05f, 0.05f, 1f), domeInside);
        Mesh inside = NewMesh("Dome inside", inV.ToArray(), inT.ToArray());
        inside.RecalculateNormals();
        Part("Dome inside", inside, dark, dome);

        // The telescope stands to the true north, which the dome, turned for its slit, does
        // not: on the same spot, at the same scale, square to the frame of km.
        var scope = Make("Telescope (to the north)").transform;
        scope.SetParent(dome.parent, false);
        scope.localPosition = dome.localPosition;
        scope.localRotation = Quaternion.identity;
        scope.localScale = dome.localScale;
        BuildTelescope(scope, set);
    }

    // Where the telescope's parts are, in the dome's metres (x east, y up, z north): the
    // tube's pivot on the dome's axis; the mirror at the foot of the tube, its open top under
    // the top of the slit, the tube pointing at the zenith.
    const float PivotY = 10f, MirrorY = 4.05f, TubeTopY = 16.5f, TubeRadius = 1.7f;

    /// <summary>
    /// The 100-inch as it is (near enough, from memory, not from drawings): an English yoke
    /// mount. A long rectangular frame on two piers, a low one to the south and a tall one to
    /// the north, its long axis the polar axis - due north, tilted up by the latitude, 34.2
    /// degrees - so it turns with the sky. The tube hangs in the middle of the frame on two
    /// trunnions, the declination axis, east to west; here turned to point at the zenith,
    /// which the real one can. A skeleton tube: rings and eight longerons, the mirror's cell
    /// closed at the foot, the Cassegrain spectrograph hanging under it. About 2.5 m of
    /// mirror, 12.7 m of focal length, a yoke some 15 m long. The mirror has a light of its
    /// own, to flash as the light reaches it.
    /// </summary>
    void BuildTelescope(Transform scope, Shader set)
    {
        var v = new List<Vector3>();
        var tri = new List<int>();

        // The polar axis, through the pivot, north and up by the latitude.
        float lat = 34.2f * Mathf.Deg2Rad;
        Vector3 pivot = Vector3.up * PivotY;
        Vector3 polar = new Vector3(0f, Mathf.Sin(lat), Mathf.Cos(lat));
        const float halfYoke = 7.5f, yokeSide = 2.3f, beam = 0.7f;
        Vector3 south = pivot - polar * halfYoke, north = pivot + polar * halfYoke;

        // The piers, up to the yoke's ends.
        Box(v, tri, new Vector3(0f, south.y * 0.5f, south.z), new Vector3(3.2f, south.y, 3f));
        Box(v, tri, new Vector3(0f, north.y * 0.5f, north.z), new Vector3(3.2f, north.y, 3f));

        // The yoke: two side beams along the polar axis, a cross member at each end.
        Vector3 side = Vector3.right * yokeSide;
        Beam(v, tri, south - side, north - side, beam);
        Beam(v, tri, south + side, north + side, beam);
        Beam(v, tri, south - side, south + side, beam * 1.3f);
        Beam(v, tri, north - side, north + side, beam * 1.3f);

        // The trunnions: the declination axis, east to west through the pivot.
        Beam(v, tri, pivot - Vector3.right * (yokeSide - beam * 0.5f), pivot - Vector3.right * TubeRadius, 0.6f);
        Beam(v, tri, pivot + Vector3.right * TubeRadius, pivot + Vector3.right * (yokeSide - beam * 0.5f), 0.6f);

        // The tube: the mirror's cell, rings, longerons.
        Band(v, tri, TubeRadius + 0.05f, MirrorY - 1.3f, MirrorY + 0.1f, 32);         // the cell
        Band(v, tri, TubeRadius, MirrorY + 0.1f, MirrorY + 0.6f, 32);                // the lower ring
        Band(v, tri, TubeRadius + 0.1f, PivotY - 0.5f, PivotY + 0.5f, 32);           // the centre ring
        Band(v, tri, TubeRadius, TubeTopY - 0.5f, TubeTopY, 32);                     // the top ring
        for (int i = 0; i < 8; i++)
        {
            float a = (i + 0.5f) * Mathf.PI * 2f / 8f;
            Vector3 at = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * (TubeRadius - 0.12f);
            Beam(v, tri, at + Vector3.up * (MirrorY + 0.3f), at + Vector3.up * (TubeTopY - 0.2f), 0.25f);
        }

        // The cell's floor, and the Cassegrain spectrograph under it.
        Disc(v, tri, MirrorY - 1.3f, TubeRadius + 0.05f, false);
        Box(v, tri, Vector3.up * (MirrorY - 2.1f), new Vector3(1.2f, 1.6f, 1.6f));

        Mesh frame = NewMesh("Telescope", v.ToArray(), tri.ToArray());
        frame.RecalculateNormals();
        Part("Telescope", frame, Inside(set, new Color(0.5f, 0.54f, 0.58f, 1f), new Color(0.01f, 0.004f, 0.004f, 1f)), scope);

        // The mirror: a disc facing up.
        var mv = new List<Vector3>();
        var mt = new List<int>();
        Disc(mv, mt, MirrorY + 0.05f, TubeRadius - 0.25f, true);
        Mesh mirror = NewMesh("Mirror", mv.ToArray(), mt.ToArray());
        mirror.RecalculateNormals();
        Part("Mirror", mirror, Inside(set, Color.black, new Color(0.18f, 0.17f, 0.2f, 1f)), scope);
    }

    /// <summary>A beam from a to b, square in section, wound to face out.</summary>
    static void Beam(List<Vector3> v, List<int> tri, Vector3 a, Vector3 b, float thickness)
    {
        Vector3 d = b - a;
        float length = d.magnitude;
        if (length < 1e-5f) return;
        d /= length;
        // Across it, and across both: a basis the same way round as x, y, z, so Box's winding holds.
        Vector3 u = Vector3.Cross(d, Mathf.Abs(d.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        Vector3 w = Vector3.Cross(u, d);
        Vector3 c = (a + b) * 0.5f;
        float h = thickness * 0.5f, l = length * 0.5f;
        int start = v.Count;
        for (int i = 0; i < 8; i++)
            v.Add(c + u * ((i & 1) == 0 ? -h : h) + d * ((i & 2) == 0 ? -l : l) + w * ((i & 4) == 0 ? -h : h));
        int[] q = { 0, 2, 3, 1,  4, 5, 7, 6,  0, 1, 5, 4,  2, 6, 7, 3,  0, 4, 6, 2,  1, 3, 7, 5 };
        for (int f = 0; f < 6; f++)
        {
            int p = start + q[f * 4], r = start + q[f * 4 + 1], s = start + q[f * 4 + 2], e = start + q[f * 4 + 3];
            tri.AddRange(new[] { p, r, s, p, s, e });
        }
    }

    /// <summary>A disc round the vertical axis at height y, facing up or down.</summary>
    static void Disc(List<Vector3> v, List<int> tri, float y, float radius, bool up)
    {
        int centre = v.Count;
        v.Add(Vector3.up * y);
        for (int i = 0; i <= 32; i++)
        {
            float a = i * Mathf.PI * 2f / 32f;
            v.Add(Vector3.up * y + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius);
        }
        for (int i = 0; i < 32; i++)
            tri.AddRange(up ? new[] { centre, centre + 1 + i, centre + 2 + i } : new[] { centre, centre + 2 + i, centre + 1 + i });
    }

    /// <summary>
    /// A material for inside the dome: the night sky's light from above, through the slit, a
    /// little all round, no city, and a light of its own. (16 Nov: a dim red light all round
    /// turned the telescope a dark red into the dark; now it is grey metal, lit from above.)
    /// </summary>
    Material Inside(Shader set, Color albedo, Color emission)
    {
        Material m = Lit(set, albedo);
        m.SetVector("_MoonDir", _up);
        m.SetColor("_MoonColor", insideLight);
        m.SetColor("_GlowColor", Color.black);
        m.SetColor("_Rim", new Color(0.04f, 0.045f, 0.05f, 1f));
        m.SetColor("_Ambient", insideLight * 0.25f);
        m.SetColor("_Emission", emission);
        _insideLights.Add(m);
        _insideEmission.Add(emission);
        return m;
    }

    /// <summary>A box, centre and size in metres, wound to face out.</summary>
    static void Box(List<Vector3> v, List<int> tri, Vector3 centre, Vector3 size)
    {
        Vector3 h = size * 0.5f;
        int b = v.Count;
        for (int i = 0; i < 8; i++)
            v.Add(centre + new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z));
        int[] q = { 0, 2, 3, 1,  4, 5, 7, 6,  0, 1, 5, 4,  2, 6, 7, 3,  0, 4, 6, 2,  1, 3, 7, 5 };
        for (int f = 0; f < 6; f++)
        {
            int a = b + q[f * 4], c = b + q[f * 4 + 1], d = b + q[f * 4 + 2], e = b + q[f * 4 + 3];
            tri.AddRange(new[] { a, c, d, a, d, e });
        }
    }

    /// <summary>An open band round the vertical axis, seen from both sides.</summary>
    static void Band(List<Vector3> v, List<int> tri, float radius, float from, float to, int around)
    {
        int b = v.Count;
        for (int i = 0; i <= around; i++)
        {
            float a = i * Mathf.PI * 2f / around;
            Vector3 rim = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
            v.Add(rim + Vector3.up * from);
            v.Add(rim + Vector3.up * to);
        }
        for (int i = 0; i < around; i++)
        {
            int k = b + i * 2;
            tri.AddRange(new[] { k, k + 2, k + 1, k + 2, k + 3, k + 1 });   // out
            tri.AddRange(new[] { k, k + 1, k + 2, k + 2, k + 1, k + 3 });   // in
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 0 below a, 1 above b, smooth between: a shader's smoothstep. NOT Mathf.SmoothStep, which
    /// eases from a to b by a t of 0 to 1 — used here by mistake at first, it made the mountains
    /// nothing like their design, and put the dome down the slope out of the picture (5 Nov).
    /// </summary>
    static float Edge(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    /// <summary>Object-space direction of a latitude and longitude, as the maps lie on PlanetSphere_NEW.</summary>
    static Vector3 Direction(float latDeg, float lonDeg)
    {
        float phi = (lonDeg + 180f) * Mathf.Deg2Rad, lat = latDeg * Mathf.Deg2Rad;
        return new Vector3(-Mathf.Sin(phi) * Mathf.Cos(lat), Mathf.Sin(lat), -Mathf.Cos(phi) * Mathf.Cos(lat));
    }

    static Vector2 LatLon(Vector3 n)
    {
        float lon = Mathf.Repeat(Mathf.Atan2(-n.x, -n.z) / (2f * Mathf.PI), 1f) * 360f - 180f;
        return new Vector2(Mathf.Asin(Mathf.Clamp(n.y, -1f, 1f)) * Mathf.Rad2Deg, lon);
    }

    Vector3 Level(Vector3 v) => (v - _up * Vector3.Dot(v, _up)).normalized;

    Vector3 Rotate(Vector3 level, float degrees) => Quaternion.AngleAxis(degrees, _up) * level;

    /// <summary>Turns a 2D direction in the frame (x east, y north) by degrees, positive to the right seen from above.</summary>
    static Vector2 Rotate(Vector2 v, float degrees)
    {
        float a = -degrees * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    /// <summary>
    /// Makes the first triangle face from inside towards outside, flipping them all if not:
    /// whichever way round a mesh was laid out, one-sided surfaces end up facing the camera.
    /// </summary>
    static void FixWinding(Mesh mesh, Vector3 outside, Vector3 inside)
    {
        int[] t = mesh.triangles;
        Vector3[] v = mesh.vertices;
        Vector3 n = Vector3.Cross(v[t[1]] - v[t[0]], v[t[2]] - v[t[0]]);
        if (Vector3.Dot(n, outside - inside) >= 0f) return;
        for (int i = 0; i < t.Length; i += 3) { int x = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = x; }
        mesh.triangles = t;
    }

    Mesh NewMesh(string name, Vector3[] vertices, int[] triangles)
    {
        var mesh = new Mesh { name = name, hideFlags = Built };
        if (vertices.Length > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        _made.Add(mesh);
        return mesh;
    }

    GameObject Make(string name)
    {
        var go = new GameObject(name) { hideFlags = Built };
        go.layer = gameObject.layer;
        _made.Add(go);
        return go;
    }

    GameObject Part(string name, Mesh mesh, Material material, Transform parent)
    {
        GameObject go = Make(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return go;
    }

    static void Kill(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }
}

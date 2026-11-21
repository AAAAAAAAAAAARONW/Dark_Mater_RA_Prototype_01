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
/// INTO THE AIR (21 Nov). Falling from a couple of hundred km to a few, the camera goes through
/// a veil of night air (Resources/EntryVeil_NEW): thickening from veilStartKm, thickest
/// from veilFullKm to veilHoldKm - when the coarse Earth is put away for the fine patch and
/// the mountains, so that change is not seen - and clear again by veilClearKm, thin cloud
/// streaming up past it. Rather than the ground only growing.
///
/// THE SKY. A skybox of its own (Resources/SummitSky_NEW), swapped in while the camera still
/// looks down, the stars and the Milky Way coming up as it lifts.
///
/// LAS CAMPANAS (21 Nov). With site set to Las Campanas the journey ends instead at the
/// Magellan 6.5 m telescope in Chile's Atacama (LCO's model, Models/Magellan_LCO): the ground
/// a dry, rolling ridge of reddish rock, the enclosure made here as the real one stands - a
/// white drum on a ring of braced steel legs, the box that turns on it open down the front and
/// across the top - and its twin beside it, as Baade and Clay stand. The light comes straight
/// down through the open top; as it goes in the picture cuts to inside the enclosure and the
/// light runs through the optics (MagellanLightPath_NEW): onto the primary, up to the
/// secondary, down to the tertiary and out along the elevation axis to the instrument. Once it
/// is there, afterInstrument later, the cut to the observatory from outside, as before. The
/// sky is the southern one: the galaxy's centre high and the Magellanic Clouds.
/// Tools > Journey NEW > Las Campanas > Move the Ending to Las Campanas sets it all up.
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

    [Tooltip("The summit's height, km: Mount Wilson, 1.74; Las Campanas, 2.38.")]
    [SerializeField] float summitKm = 1.74f;

    public enum Site { MountWilson, LasCampanas }

    [Tooltip("Where the journey ends. Mount Wilson: the 100-inch's dome above Los Angeles. Las Campanas: the " +
             "Magellan 6.5 m telescope in Chile, the light run through its optics (needs Magellan, below). " +
             "Tools > Journey NEW > Las Campanas > Move the Ending to Las Campanas sets this and the rest.")]
    [SerializeField] Site site = Site.MountWilson;

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

    [Header("Las Campanas")]
    [Tooltip("The telescope: Models/Magellan_LCO/Magellan_NEW.prefab, made from LCO's model by " +
             "Tools > Journey NEW > Magellan > Build Telescope from STL.")]
    [SerializeField] GameObject magellan;

    [Tooltip("The enclosure and the telescope, times their real size (the enclosure 33 m high). Bigger reads better from afar.")]
    [Range(0.5f, 3f)] [SerializeField] float magellanScale = 1.4f;

    [Tooltip("The second enclosure - Baade and Clay stand side by side - this many metres off, at real size. 0 = none.")]
    [Min(0f)] [SerializeField] float twinMetres = 60f;

    [Tooltip("Degrees round from the way to the camera's stand to where the second enclosure stands, about the first.")]
    [Range(-180f, 180f)] [SerializeField] float twinBearing = 115f;

    [SerializeField] Color enclosureColour = new Color(0.8f, 0.8f, 0.79f, 1f);

    [Tooltip("The ground: the Atacama's dry rock, reddish brown; patched, and pale gravel round the enclosures and on the road.")]
    [SerializeField] Color atacamaRock = new Color(0.22f, 0.15f, 0.11f, 1f);

    [Tooltip("The moon on the Atacama: the air is dry and clear, the ground reads by it.")]
    [SerializeField] Color atacamaMoonlight = new Color(0.62f, 0.64f, 0.74f, 1f);

    [Tooltip("Starlight and airglow on the Atacama, from the whole sky.")]
    [SerializeField] Color atacamaSkyLight = new Color(0.07f, 0.075f, 0.095f, 1f);

    [Tooltip("The lamp over the enclosure's door: warm, a light of its own.")]
    [ColorUsage(false, true)] [SerializeField] Color lampColour = new Color(2.4f, 1.5f, 0.7f, 1f);

    [Tooltip("The beacon on the enclosure's roof, blinking.")]
    [ColorUsage(false, true)] [SerializeField] Color beaconColour = new Color(3f, 0.15f, 0.08f, 1f);

    [Tooltip("The dim red light inside the enclosure, on its walls.")]
    [SerializeField] Color insideRed = new Color(0.07f, 0.01f, 0.008f, 1f);

    [Tooltip("Seconds the view inside holds once the light has reached the instrument, before the cut outside.")]
    [Min(0f)] [SerializeField] float afterInstrument = 1.5f;

    [Tooltip("Where the camera stands inside the enclosure while the light runs through the optics, and what " +
             "it looks at: metres, in the enclosure's frame (y up from the ground, its open front +z, the " +
             "telescope's floor 13.5 up, its elevation axis along x).")]
    [SerializeField] Vector3 insideCamera = new Vector3(-8f, 28f, -7.5f);
    [SerializeField] Vector3 insideLookAt = new Vector3(1.5f, 19.5f, 0f);

    [Header("Las Campanas: the last shot")]
    [Tooltip("Degrees the camera is turned from the way to the city (La Serena's glow): positive turns it right, " +
             "so the glow sits on the left of the picture.")]
    [Range(-60f, 60f)] [SerializeField] float finalViewYaw = 18f;

    [Tooltip("Degrees the camera looks above the horizon: up, so the enclosure stands against the Milky Way.")]
    [Range(-10f, 30f)] [SerializeField] float finalViewPitch = 8f;

    [Tooltip("Degrees from the way to the city the enclosure stands, from the camera: positive is right.")]
    [Range(-60f, 60f)] [SerializeField] float finalDomeYaw = 30f;

    [Tooltip("How far the camera stands from the enclosure, km.")]
    [Range(0.08f, 1f)] [SerializeField] float finalDomeKm = 0.2f;

    [Tooltip("The camera's height over the ground, metres: low, looking up at the enclosure.")]
    [Range(2f, 80f)] [SerializeField] float finalLiftMetres = 25f;

    [Tooltip("How far the camera creeps in towards the enclosure over the shot, as a fraction of the way.")]
    [Range(0f, 0.2f)] [SerializeField] float finalPush = 0.05f;

    [Tooltip("Seconds the creep takes, eased out.")]
    [Min(1f)] [SerializeField] float finalPushSeconds = 18f;

    [Tooltip("The southern Milky Way: brighter than the one over Los Angeles' glow.")]
    [ColorUsage(false, true)] [SerializeField] Color southernMilkyWay = new Color(0.3f, 0.285f, 0.26f, 1f);

    [Tooltip("Airglow: the faint green band low over the horizon in dark-sky photographs of the Atacama.")]
    [ColorUsage(false, true)] [SerializeField] Color airglow = new Color(0.012f, 0.03f, 0.016f, 1f);

    [Tooltip("The Magellanic Clouds in the southern sky, times the Milky Way's light. 0 = none.")]
    [Range(0f, 2f)] [SerializeField] float magellanicClouds = 0.8f;

    [Header("Into the air")]
    [Tooltip("The veil of night air the camera falls through on the way down. Off = the ground only grows.")]
    [SerializeField] bool entryVeil = true;
    [Tooltip("Camera heights, km: the air starts to thicken; is thickest from here...")]
    [Min(1f)] [SerializeField] float veilStartKm = 170f;
    [Min(1f)] [SerializeField] float veilFullKm = 110f;
    [Tooltip("...to here; and is clear again here.")]
    [Min(0.5f)] [SerializeField] float veilHoldKm = 30f;
    [Min(0.1f)] [SerializeField] float veilClearKm = 6f;
    [Tooltip("How thick at its thickest: 1 hides everything.")]
    [Range(0f, 1f)] [SerializeField] float veilStrength = 0.88f;
    [SerializeField] Color veilHigh = new Color(0.05f, 0.08f, 0.17f, 1f);
    [Tooltip("The air below, lit from the ground's lights.")]
    [SerializeField] Color veilLow = new Color(0.22f, 0.13f, 0.08f, 1f);
    [Range(0f, 1f)] [SerializeField] float veilWisps = 0.6f;

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
    Material _setMountain, _setDome, _sky, _beacon;
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

    bool UseModel => !LasCampanas && useObservatoryModel && ObservatoryModel != null;

    /// <summary>Ending at the Magellan telescope (site Las Campanas, with the telescope set).</summary>
    public bool LasCampanas => site == Site.LasCampanas && magellan != null;

    // The Magellan enclosure, in its own metres: the box's floor, where the telescope stands,
    // and its top; the light arrives a little above the top, over the middle of the slot.
    const float EnclosureFloor = 13.5f, EnclosureTop = 33f, ArriveAboveTop = 2f;

    /// <summary>
    /// How high above sea level the light ends, km: the summit, and the telescope's mirror
    /// over it. EarthZoom_NEW fixes the arrival with it as the approach begins.
    /// </summary>
    public float ArrivalHeightKm => summitKm + 0.001f * (LasCampanas ? (EnclosureTop + ArriveAboveTop) * magellanScale
                                                       : UseModel ? (ModelMirrorY - ModelGroundY) * observatoryScale
                                                       : (MirrorY + 0.3f) * domeScale);
    Transform _dome;
    MagellanLightPath_NEW _path;
    CinemachineVirtualCamera _insideVcam;
    float _reachedClock, _cutClock;
    Vector2 _twinAt;
    bool _hasTwin;
    Vector3 _standFrom, _standTo;   // the frame: the last shot's creep in
    Transform _veil;
    Material _veilMaterial;
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
    public Vector3 ViewLevel => Rotate(_toCity, LasCampanas ? finalViewYaw : viewYaw);

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
        // The roof's beacon: a short red blink every second and a half.
        if (_beacon != null) _beacon.SetColor("_Emission", beaconColour * (Mathf.Repeat(Time.time, 1.5f) < 0.22f ? 1f : 0.04f));

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

        Veil();
        if (_ending) Ending();
    }

    /// <summary>The veil of air, round the drawing camera, as thick as its height says (see INTO THE AIR).</summary>
    void Veil()
    {
        if (_veil == null) return;
        Camera eye = GetReal3DView_NEW.ScreenCamera;
        float alt = eye != null ? _frame.InverseTransformPoint(eye.transform.position).y : 0f;
        float thick = veilStrength * (1f - Edge(veilFullKm, veilStartKm, alt)) * Edge(veilClearKm, veilHoldKm, alt);
        bool on = eye != null && thick > 0.002f;
        _veil.gameObject.SetActive(on);
        if (!on) return;

        // Big enough that every camera (the wall's too) is inside it however far one frame
        // moves; drawn over everything whatever its size.
        _veil.position = eye.transform.position;
        _veil.localScale = Vector3.one * eye.farClipPlane * 0.6f;
        _veilMaterial.SetFloat("_Alpha", thick);
        _veilMaterial.SetFloat("_Scroll", Mathf.Log(Mathf.Max(alt, 0.1f)) * 1.5f);
    }

    void BuildVeil()
    {
        if (!entryVeil) return;
        Shader shader = Resources.Load<Shader>("EntryVeil_NEW");
        Mesh sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
        if (shader == null || sphere == null) { Debug.LogWarning("[EarthSummit_NEW] No Resources/EntryVeil_NEW or sphere: no veil of air.", this); return; }

        _veilMaterial = new Material(shader) { hideFlags = Built };
        _made.Add(_veilMaterial);
        _veilMaterial.SetTexture("_Noise", QuasarNoise_NEW.Texture);
        _veilMaterial.SetVector("_Up", _up);
        _veilMaterial.SetColor("_High", veilHigh);
        _veilMaterial.SetColor("_Low", veilLow);
        _veilMaterial.SetFloat("_Wisps", veilWisps);
        _veilMaterial.SetFloat("_Alpha", 0f);

        GameObject go = Part("Entry veil", sphere, _veilMaterial, null);
        _veil = go.transform;
        go.SetActive(false);
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
        _insideVcam = null;
        _path = null;
        _beacon = null;
        _road.Clear();
        _veil = null;
        _veilMaterial = null;
        _reachedClock = _cutClock = 0f;
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
        _setMountain = Lit(set, LasCampanas ? atacamaRock : mountainColour);
        _setMountain.SetColor("_Rim", mountainRim);
        if (LasCampanas)
        {
            // Moonlit rock, patched and grained, the gravel and road by vertex colour; the moon
            // on it under the night map too, where the map is all but black.
            _setMountain.SetColor("_MoonColor", atacamaMoonlight);
            _setMountain.SetColor("_Ambient", atacamaSkyLight);
            _setMountain.SetFloat("_VertexColors", 1f);
            _setMountain.SetFloat("_MoonOnNight", 1f);
            _setMountain.SetFloat("_Grain", 0.45f);
            _setMountain.SetFloat("_GrainScale", 140f);   // the mesh is in km: rock about 7 m across
        }
        // The mountains wear the ground's own night, so from above they are the land round
        // them and not a dark patch on it; hazed as the ground is.
        _here = here;
        // Only where the close-up map is: it covers southern California, not Chile.
        Vector4 rect = body.RegionRect;
        _drape = body.RegionMap != null && here.y >= rect.x && here.y <= rect.z && here.x >= rect.y && here.x <= rect.w;
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
        _setDome = Lit(set, LasCampanas ? enclosureColour : domeColour);
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
        Vector2 spot = domeAt - Rotate(toward, LasCampanas ? finalDomeYaw : domeYaw) * (LasCampanas ? finalDomeKm : domeKm);
        _stand = new Vector3(spot.x, standKm, spot.y);

        bool model = UseModel;
        _flatInner = LasCampanas ? 0.03f * magellanScale : model ? 0.022f * observatoryScale : 0.03f * domeScale;
        _flatOuter = LasCampanas ? 0.08f * magellanScale : model ? 0.05f * observatoryScale : 0.07f * domeScale;
        _hasTwin = LasCampanas && twinMetres > 0f;
        _twinAt = domeAt + Rotate((spot - domeAt).normalized, twinBearing) * (twinMetres * 0.001f * magellanScale);
        if (LasCampanas) PlanRoad(domeAt, spot);
        if (LasCampanas)
        {
            // The last shot from low down: finalLiftMetres over the ground where the camera stands.
            float groundKm = Atacama(spot, spot, toward, 99f, domeAt, domeGround);
            standKm = groundKm + finalLiftMetres * 0.001f;
            _stand = new Vector3(spot.x, standKm, spot.y);
        }
        _standFrom = _stand;
        _standTo = Vector3.Lerp(_stand, new Vector3(domeAt.x, _stand.y, domeAt.y), LasCampanas ? finalPush : 0f);
        Part("Mountains", BuildMountains(spot, toward, standKm, domeAt, domeGround), _setMountain, _frame);

        // The slit opens three-quarters towards the camera, so it shows.
        Vector2 faces = Rotate((spot - domeAt).normalized, 50f);
        if (LasCampanas)
        {
            BuildMagellan(domeAt, domeGround, faces, set);
        }
        else if (model)
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
        float yaw = LasCampanas ? finalViewYaw : viewYaw, pitch = LasCampanas ? finalViewPitch : viewPitch;
        Vector3 level = Rotate(_toCity, yaw);
        _cutTo = Quaternion.LookRotation(level * Mathf.Cos(pitch * Mathf.Deg2Rad) + _up * Mathf.Sin(pitch * Mathf.Deg2Rad), _up);

        // The sky, while the camera still looks at the ground; the air to fall through.
        BuildSky();
        BuildVeil();

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
        _sky.SetColor("_MilkyWay", LasCampanas ? southernMilkyWay : milkyWay);
        _sky.SetColor("_Airglow", LasCampanas ? airglow : Color.black);
        _sky.SetFloat("_StarBrightness", stars);
        _sky.SetFloat("_Reveal", 0f);

        // Mount Wilson: the galaxy's centre low over the horizon a little right of the city; the
        // band rising from it up over the sky to the left, as the summer Milky Way stands, and
        // across the picture the camera ends on. Las Campanas: the southern winter sky, the
        // centre high to the right and the band arching up over the enclosure.
        Vector3 centre = LasCampanas
            ? (Rotate(_toCity, 35f) * Mathf.Cos(40f * Mathf.Deg2Rad) + _up * Mathf.Sin(40f * Mathf.Deg2Rad)).normalized
            : (Rotate(_toCity, 12f) * Mathf.Cos(10f * Mathf.Deg2Rad) + _up * Mathf.Sin(10f * Mathf.Deg2Rad)).normalized;
        Vector3 along = LasCampanas
            ? (Rotate(_toCity, -60f) * Mathf.Cos(70f * Mathf.Deg2Rad) + _up * Mathf.Sin(70f * Mathf.Deg2Rad)).normalized
            : (Rotate(_toCity, -45f) * Mathf.Cos(55f * Mathf.Deg2Rad) + _up * Mathf.Sin(55f * Mathf.Deg2Rad)).normalized;
        Vector3 galPole = Vector3.Cross(centre, along).normalized;
        _sky.SetFloat("_Clouds", 0f);
        if (LasCampanas && magellanicClouds > 0f)
        {
            // The Large and Small Magellanic Clouds, at their galactic longitudes and latitudes
            // (280.5, -32.9 and 302.8, -44.3). Which side of the band is south is free here: the
            // side that puts the Large Cloud higher in the sky.
            Vector3 lmc = Galactic(280.5f, -32.9f, centre, galPole), flipped = Galactic(280.5f, -32.9f, centre, -galPole);
            if (Vector3.Dot(flipped, _up) > Vector3.Dot(lmc, _up)) galPole = -galPole;
            Vector3 l = Galactic(280.5f, -32.9f, centre, galPole), m = Galactic(302.8f, -44.3f, centre, galPole);
            _sky.SetVector("_LMC", new Vector4(l.x, l.y, l.z, 0.075f));
            _sky.SetVector("_SMC", new Vector4(m.x, m.y, m.z, 0.05f));
            _sky.SetFloat("_Clouds", magellanicClouds);
        }
        _sky.SetVector("_GalPole", galPole);
        _sky.SetVector("_GalA", centre);
        _sky.SetVector("_GalB", Vector3.Cross(galPole, centre));

        _skyBefore = RenderSettings.skybox;
        RenderSettings.skybox = _sky;
        _skySwapped = true;
    }

    /// <summary>A direction from galactic longitude and latitude, degrees, the centre and pole given.</summary>
    static Vector3 Galactic(float l, float b, Vector3 centre, Vector3 pole)
    {
        Vector3 l90 = Vector3.Cross(pole, centre);
        float lr = l * Mathf.Deg2Rad, br = b * Mathf.Deg2Rad;
        return ((centre * Mathf.Cos(lr) + l90 * Mathf.Sin(lr)) * Mathf.Cos(br) + pole * Mathf.Sin(br)).normalized;
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
                if (_path != null) InsideShot();
            }
            return;
        }

        _insideClock += Time.deltaTime;
        bool ready;
        if (_path != null)
        {
            // Inside, until the light has reached the instrument and afterInstrument more; never
            // longer than half a minute, whatever happens to the light.
            if (_path.Reached) _reachedClock += Time.deltaTime;
            ready = _reachedClock >= afterInstrument || _insideClock > 30f;
        }
        else ready = _insideClock >= holdInside;

        if (!_cut && ready) Cut();
        else if (_cut && _vcam != null)
        {
            // Las Campanas: creeping in towards the enclosure, eased out, over the shot.
            float creep = 1f - Mathf.Pow(1f - Mathf.Clamp01((_insideClock - _cutClock) / finalPushSeconds), 2f);
            _vcam.transform.position = _frame.TransformPoint(Vector3.Lerp(_standFrom, _standTo, creep));
            // The brain's own blend back, once the cut has been taken.
            if (_blendSwapped && _insideClock >= _cutClock + 0.5f) { _brain.m_DefaultBlend = _blendBefore; _blendSwapped = false; }
        }
    }

    /// <summary>To the observatory from outside: a camera of its own on the stand, taken at once, not blended to.</summary>
    void Cut()
    {
        _cut = true;
        _cutClock = _insideClock;
        _vcam = NewVcam("VCam_Summit", _frame.TransformPoint(_stand), _cutTo, -1f);
        CutTo(_vcam, 90);
        if (_insideVcam != null) _insideVcam.gameObject.SetActive(false);
        if (_sky != null) _sky.SetFloat("_Reveal", 1f);
    }

    /// <summary>
    /// Las Campanas: the cut, as the light goes in, to inside the enclosure, and the light run
    /// through the telescope's optics from just over the open top (see LAS CAMPANAS).
    /// </summary>
    void InsideShot()
    {
        Vector3 at = _dome.TransformPoint(insideCamera);
        Vector3 look = _dome.TransformPoint(insideLookAt) - at;
        // Near enough for the inside: a third of a metre, at the enclosure's size.
        float near = 0.3f * _dome.lossyScale.x;
        _insideVcam = NewVcam("VCam_Inside", at, Quaternion.LookRotation(look, _up), near);
        CutTo(_insideVcam, 91);
        if (_sky != null) _sky.SetFloat("_Reveal", 1f);
        _reachedClock = 0f;
        _path.Play();
    }

    /// <summary>A virtual camera here with the drawing camera's lens; near, if positive, its own near clip.</summary>
    CinemachineVirtualCamera NewVcam(string name, Vector3 position, Quaternion rotation, float near)
    {
        Camera cam = GetReal3DView_NEW.ScreenCamera;
        var go = Make(name);
        go.transform.SetPositionAndRotation(position, rotation);
        var vcam = go.AddComponent<CinemachineVirtualCamera>();
        if (cam != null)
        {
            LensSettings lens = vcam.m_Lens;
            lens.FieldOfView = cam.fieldOfView;
            lens.NearClipPlane = near > 0f ? Mathf.Min(near, cam.nearClipPlane) : cam.nearClipPlane;
            lens.FarClipPlane = cam.farClipPlane;
            vcam.m_Lens = lens;
        }
        return vcam;
    }

    /// <summary>Takes this camera at once, not blended to; over CameraDirector_NEW's 50, so nothing else moves it now.</summary>
    void CutTo(CinemachineVirtualCamera vcam, int priority)
    {
        if (_brain != null && !_blendSwapped)
        {
            _blendBefore = _brain.m_DefaultBlend;
            _brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.Cut, 0f);
            _blendSwapped = true;
        }
        vcam.Priority = priority;
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

    /// <summary>
    /// Las Campanas: the Magellan enclosure on the ground under the arrival at magellanScale,
    /// its open front three-quarters to the stand, LCO's telescope in it pointing at the zenith
    /// (its elevation axis across the slot, the instrument on the +x side); and the twin
    /// enclosure beside it. The telescope wears the inside's light; its light path is readied for
    /// the way in (InsideShot), starting just over the open top where the light arrives.
    /// </summary>
    void BuildMagellan(Vector2 domeAt, float domeGround, Vector2 faces, Shader set)
    {
        Quaternion turn = Quaternion.LookRotation(new Vector3(faces.x, 0f, faces.y), Vector3.up);
        var holder = Make("Magellan (Clay)").transform;
        _dome = holder;
        holder.SetParent(_frame, false);
        holder.localPosition = new Vector3(domeAt.x, domeGround, domeAt.y);
        holder.localRotation = turn;
        holder.localScale = Vector3.one * 0.001f * magellanScale;   // metres

        Enclosure e = BuildEnclosure();

        // The white of the enclosure, its parts told apart by vertex colour and weathered by grain.
        _setDome.SetFloat("_VertexColors", 1f);
        _setDome.SetFloat("_Grain", 0.12f);
        _setDome.SetFloat("_GrainScale", 0.7f);
        Material trim = Lit(set, new Color(0.05f, 0.05f, 0.055f, 1f));
        Material floorMat = Inside(set, new Color(0.2f, 0.2f, 0.21f, 1f), new Color(0.004f, 0.004f, 0.005f, 1f));
        // The inside walls in the dim red of an observatory at night: they flash as the light goes in.
        Material lining = Inside(set, new Color(0.22f, 0.22f, 0.24f, 1f), insideRed);
        Material lamp = Lit(set, Color.black);
        lamp.SetColor("_Rim", Color.black);
        lamp.SetColor("_Emission", lampColour);
        _beacon = Lit(set, Color.black);
        _beacon.SetColor("_Rim", Color.black);

        foreach (Transform where in new[] { holder })
            PlaceEnclosure(where, e, trim, floorMat, lining, lamp);

        // The telescope, as LCO's model stands: its own materials swapped for the inside's light.
        GameObject scope = Instantiate(magellan, holder, false);
        scope.name = "Magellan 6.5 m (LCO model)";
        scope.hideFlags = Built;
        _made.Add(scope);
        scope.transform.localPosition = new Vector3(0f, EnclosureFloor, 0f);
        scope.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        scope.transform.localScale = Vector3.one;

        Material metal = Inside(set, new Color(0.62f, 0.64f, 0.67f, 1f), new Color(0.008f, 0.008f, 0.01f, 1f));
        Material plinth = Inside(set, new Color(0.14f, 0.14f, 0.15f, 1f), Color.black);
        Material mirror = Inside(set, new Color(0.05f, 0.06f, 0.07f, 1f), Color.black);
        Material box = Inside(set, new Color(0.12f, 0.13f, 0.15f, 1f), Color.black);
        _scopeRenderers.Clear();
        foreach (Renderer r in scope.GetComponentsInChildren<Renderer>(true))
        {
            Material body = r.name == "Base" ? plinth : r.name.StartsWith("Instrument") ? box : metal;
            var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
            for (int i = 0; i < mats.Length; i++) mats[i] = i == 0 ? body : mirror;
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            _scopeRenderers.Add(r);
        }
        foreach (Transform t in scope.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = gameObject.layer;

        _path = scope.GetComponent<MagellanLightPath_NEW>();
        if (_path != null && _path.Tube != null)
        {
            _path.UseEmissionProperty("_Emission");
            // From just over the open top, where the light arrives, down onto the primary.
            float rim = EnclosureFloor + _path.Tube.localPosition.y + _path.RimY;
            _path.StartAbove = EnclosureTop + ArriveAboveTop - rim;
        }
        else Debug.LogWarning("[EarthSummit_NEW] The Magellan telescope has no MagellanLightPath_NEW (or no tube): " +
                              "the light will not run through it. Build it again from Tools > Journey NEW > Magellan.", this);

        if (_hasTwin)
        {
            // Baade, its twin: the same enclosure, a grey tube showing through its slot.
            var twin = Make("Magellan (Baade)").transform;
            twin.SetParent(_frame, false);
            twin.localPosition = new Vector3(_twinAt.x, domeGround, _twinAt.y);
            twin.localRotation = turn;
            twin.localScale = holder.localScale;
            PlaceEnclosure(twin, e, trim, floorMat, lining, lamp);
            var v = new List<Vector3>();
            var tri = new List<int>();
            Band(v, tri, 4f, EnclosureFloor + 4.5f, EnclosureFloor + 13f, 24);
            Box(v, tri, new Vector3(0f, EnclosureFloor + 2.5f, 0f), new Vector3(7f, 5f, 9f));
            Mesh tube = NewMesh("Magellan twin's telescope", v.ToArray(), tri.ToArray());
            tube.RecalculateNormals();
            Part("Telescope (shadow)", tube, Inside(set, new Color(0.3f, 0.31f, 0.33f, 1f), Color.black), twin);
        }

        Debug.Log("[EarthSummit_NEW] Las Campanas: the Magellan enclosure" + (_hasTwin ? "s" : "") + " and LCO's telescope, " +
                  _scopeRenderers.Count + " parts; the light starts " + (_path != null ? _path.StartAbove.ToString("0.0") : "-") +
                  " m over the primary.", this);
    }

    void PlaceEnclosure(Transform where, Enclosure e, Material trim, Material floorMat, Material lining, Material lamp)
    {
        Part("Enclosure", e.shell, _setDome, where);
        Part("Drum", e.drum, _setDome, where);
        Part("Louvres and seams", e.trims, trim, where);
        Part("Floor", e.floor, floorMat, where);
        Part("Inside walls", e.lining, lining, where);
        Part("Lamps", e.lamps, lamp, where);
        Part("Beacon", e.beacon, _beacon, where);
    }

    struct Enclosure { public Mesh shell, drum, trims, floor, lining, lamps, beacon; }

    /// <summary>
    /// The Magellan enclosure, metres, +z its open front: sixteen steel legs braced in X under a
    /// white drum 22 m across, with louvres and a door; a skirt; and on it the box that turns,
    /// 26 m across and 24 deep, open down the front and across the top in a slot 9 m wide, its
    /// panels seamed, the telescope's floor at EnclosureFloor and its top at EnclosureTop. Flat
    /// faces, the parts told apart by vertex colour (0.5 = the white as it is); the drum smooth.
    /// A lamp over the door, a red beacon on the roof, the inside walls lined in red.
    /// </summary>
    Enclosure BuildEnclosure()
    {
        var e = new Enclosure();
        var v = new List<Vector3>();
        var tri = new List<int>();
        var col = new List<Color>();
        Color legs = Grey(0.46f), braces = Grey(0.42f), walls = Grey(0.5f), roofTop = Grey(0.44f), skirt = Grey(0.45f), sillC = Grey(0.47f);

        // The legs, braced in X, and a ring beam over them.
        const int legCount = 16;
        const float legR = 10.6f, legTop = 4f;
        for (int i = 0; i < legCount; i++)
        {
            float a = i * Mathf.PI * 2f / legCount, b = (i + 1) * Mathf.PI * 2f / legCount;
            Vector3 p = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * legR, q = new Vector3(Mathf.Sin(b), 0f, Mathf.Cos(b)) * legR;
            Beam(v, tri, p - Vector3.up * 1f, p + Vector3.up * legTop, 0.55f);
            Paint(col, v, legs);
            Beam(v, tri, p + Vector3.up * 0.3f, q + Vector3.up * (legTop - 0.2f), 0.22f);
            Beam(v, tri, q + Vector3.up * 0.3f, p + Vector3.up * (legTop - 0.2f), 0.22f);
            Paint(col, v, braces);
        }

        // The box: walls round a room 19 m by 18, the slot down the front and across the top.
        float y0 = EnclosureFloor, y1 = EnclosureTop, h = y1 - y0, mid = (y0 + y1) * 0.5f;
        const float halfW = 13f, halfD = 12f, roomW = 9.5f, roomD = 9f, slot = 4.5f, roof = 3f, sill = 3.5f, roofBack = -4.5f;
        Box(v, tri, new Vector3(-(halfW + roomW) * 0.5f, mid, 0f), new Vector3(halfW - roomW, h, 2f * halfD));       // side walls
        Box(v, tri, new Vector3((halfW + roomW) * 0.5f, mid, 0f), new Vector3(halfW - roomW, h, 2f * halfD));
        Box(v, tri, new Vector3(0f, mid, -(halfD + roomD) * 0.5f), new Vector3(2f * roomW, h, halfD - roomD));       // back wall
        Box(v, tri, new Vector3(-(roomW + slot) * 0.5f, mid, (halfD + roomD) * 0.5f), new Vector3(roomW - slot, h, halfD - roomD)); // front, beside the slot
        Box(v, tri, new Vector3((roomW + slot) * 0.5f, mid, (halfD + roomD) * 0.5f), new Vector3(roomW - slot, h, halfD - roomD));
        Paint(col, v, walls);
        Box(v, tri, new Vector3(0f, y0 + sill * 0.5f, (halfD + roomD) * 0.5f), new Vector3(2f * slot, sill, halfD - roomD));   // the sill under the slot
        Paint(col, v, sillC);
        Box(v, tri, new Vector3(-(roomW + slot) * 0.5f, y1 - roof * 0.5f, 0f), new Vector3(roomW - slot, roof, 2f * roomD));   // roof, beside the slot
        Box(v, tri, new Vector3((roomW + slot) * 0.5f, y1 - roof * 0.5f, 0f), new Vector3(roomW - slot, roof, 2f * roomD));
        Box(v, tri, new Vector3(0f, y1 - roof * 0.5f, (-roomD + roofBack) * 0.5f), new Vector3(2f * slot, roof, roofBack + roomD)); // behind the slot
        Paint(col, v, roofTop);
        // The skirt between drum and box.
        OBox(v, tri, new Vector3(0f, y0 - 0.3f, 0f), Vector3.right, Vector3.up, Vector3.forward, new Vector3(2f * halfW - 1f, 0.6f, 2f * halfD - 1f));
        Paint(col, v, skirt);
        e.shell = FlatMesh("Magellan enclosure", v, tri, col);

        // The drum, smooth, its underside and the skirt's edge.
        var dv = new List<Vector3>();
        var dt = new List<int>();
        Band(dv, dt, 11f, legTop, EnclosureFloor - 0.6f, 64);
        Disc(dv, dt, legTop, 11f, false);
        e.drum = NewMesh("Magellan drum", dv.ToArray(), dt.ToArray());
        e.drum.RecalculateNormals();
        var dc = new Color[dv.Count];
        for (int i = 0; i < dc.Length; i++) dc[i] = Grey(0.5f);
        e.drum.colors = dc;

        // Louvres on the drum, in five banks; the door, facing the camera's side; the box's
        // panel seams, its corner trims and the dark band along its foot.
        var tv = new List<Vector3>();
        var tt = new List<int>();
        for (int bank = 0; bank < 5; bank++)
        {
            float a = (bank + 0.5f) * Mathf.PI * 2f / 5f + 0.4f;
            Vector3 n = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)), side = new Vector3(n.z, 0f, -n.x);
            for (int slat = 0; slat < 7; slat++)
                OBox(tv, tt, n * 11.12f + Vector3.up * (7f + slat * 0.45f), side, Vector3.up, n, new Vector3(3.6f, 0.26f, 0.22f));
        }
        Vector3 doorN = DoorNormal, doorSide = new Vector3(doorN.z, 0f, -doorN.x);
        OBox(tv, tt, doorN * 11.05f + Vector3.up * (legTop + 1.6f), doorSide, Vector3.up, doorN, new Vector3(2.6f, 3.2f, 0.25f));
        for (float y = y0 + 3.5f; y < y1 - 1f; y += 3.8f)
        {
            OBox(tv, tt, new Vector3(-halfW - 0.03f, y, 0f), Vector3.right, Vector3.up, Vector3.forward, new Vector3(0.08f, 0.1f, 2f * halfD));
            OBox(tv, tt, new Vector3(halfW + 0.03f, y, 0f), Vector3.right, Vector3.up, Vector3.forward, new Vector3(0.08f, 0.1f, 2f * halfD));
            OBox(tv, tt, new Vector3(0f, y, -halfD - 0.03f), Vector3.right, Vector3.up, Vector3.forward, new Vector3(2f * halfW, 0.1f, 0.08f));
            float wing = halfW - slot;
            OBox(tv, tt, new Vector3(-(halfW + slot) * 0.5f, y, halfD + 0.03f), Vector3.right, Vector3.up, Vector3.forward, new Vector3(wing, 0.1f, 0.08f));
            OBox(tv, tt, new Vector3((halfW + slot) * 0.5f, y, halfD + 0.03f), Vector3.right, Vector3.up, Vector3.forward, new Vector3(wing, 0.1f, 0.08f));
        }
        Box(tv, tt, new Vector3(0f, y0 + 0.35f, halfD + 0.05f), new Vector3(2f * halfW, 0.5f, 0.1f));
        e.trims = FlatMesh("Magellan trims", tv, tt, null);

        // The room's floor.
        var fv = new List<Vector3>();
        var ft = new List<int>();
        Box(fv, ft, new Vector3(0f, y0 + 0.1f, 0f), new Vector3(2f * roomW, 0.2f, 2f * roomD));
        e.floor = FlatMesh("Magellan floor", fv, ft, null);

        // The inside walls, just in from the room's walls and roof.
        var lv = new List<Vector3>();
        var lt = new List<int>();
        Box(lv, lt, new Vector3(-roomW + 0.05f, mid, 0f), new Vector3(0.1f, h - 0.2f, 2f * roomD));
        Box(lv, lt, new Vector3(roomW - 0.05f, mid, 0f), new Vector3(0.1f, h - 0.2f, 2f * roomD));
        Box(lv, lt, new Vector3(0f, mid, -roomD + 0.05f), new Vector3(2f * roomW, h - 0.2f, 0.1f));
        Box(lv, lt, new Vector3(-(roomW + slot) * 0.5f, y1 - roof - 0.05f, 0f), new Vector3(roomW - slot, 0.1f, 2f * roomD));
        Box(lv, lt, new Vector3((roomW + slot) * 0.5f, y1 - roof - 0.05f, 0f), new Vector3(roomW - slot, 0.1f, 2f * roomD));
        e.lining = FlatMesh("Magellan inside walls", lv, lt, null);

        // A lamp over the door; the beacon on the roof's front corner.
        var mv = new List<Vector3>();
        var mt = new List<int>();
        OBox(mv, mt, doorN * 11.3f + Vector3.up * (legTop + 3.6f), doorSide, Vector3.up, doorN, new Vector3(0.9f, 0.35f, 0.5f));
        e.lamps = FlatMesh("Magellan lamps", mv, mt, null);
        var bv = new List<Vector3>();
        var bt = new List<int>();
        Box(bv, bt, new Vector3(-halfW + 0.6f, y1 + 0.5f, halfD - 0.6f), Vector3.one * 0.9f);
        e.beacon = FlatMesh("Magellan beacon", bv, bt, null);
        return e;
    }

    // The drum's door: towards the front, round to the camera's side.
    static Vector3 DoorNormal => new Vector3(Mathf.Sin(-0.7f), 0f, Mathf.Cos(-0.7f));

    static Color Grey(float g) => new Color(g, g, g, 1f);

    static void Paint(List<Color> col, List<Vector3> v, Color c)
    {
        while (col.Count < v.Count) col.Add(c);
    }

    /// <summary>A box along right, up and forward (unit, square to each other), size along each.</summary>
    static void OBox(List<Vector3> v, List<int> tri, Vector3 centre, Vector3 right, Vector3 up, Vector3 forward, Vector3 size)
    {
        Vector3 h = size * 0.5f;
        int b = v.Count;
        for (int i = 0; i < 8; i++)
            v.Add(centre + right * ((i & 1) == 0 ? -h.x : h.x) + up * ((i & 2) == 0 ? -h.y : h.y) + forward * ((i & 4) == 0 ? -h.z : h.z));
        int[] q = { 0, 2, 3, 1,  4, 5, 7, 6,  0, 1, 5, 4,  2, 6, 7, 3,  0, 4, 6, 2,  1, 3, 7, 5 };
        for (int f = 0; f < 6; f++)
        {
            int a = b + q[f * 4], c = b + q[f * 4 + 1], d = b + q[f * 4 + 2], e = b + q[f * 4 + 3];
            tri.AddRange(new[] { a, c, d, a, d, e });
        }
    }

    /// <summary>A mesh with no corner shared between faces, so every face is flat; colours by vertex if given.</summary>
    Mesh FlatMesh(string name, List<Vector3> v, List<int> tri, List<Color> col)
    {
        var fv = new Vector3[tri.Count];
        var ft = new int[tri.Count];
        var fc = col != null ? new Color[tri.Count] : null;
        for (int i = 0; i < tri.Count; i++)
        {
            fv[i] = v[tri[i]];
            ft[i] = i;
            if (fc != null) fc[i] = col[tri[i]];
        }
        Mesh m = NewMesh(name, fv, ft);
        if (fc != null) m.colors = fc;
        m.RecalculateNormals();
        return m;
    }

    // ── The Las Campanas ground ─────────────────────────────────────────────

    readonly List<Vector2> _road = new List<Vector2>();

    /// <summary>
    /// The road up to the enclosures: from the pad's edge on the far side from the camera's
    /// stand, winding round and down the ridge's flank. In the frame's km.
    /// </summary>
    void PlanRoad(Vector2 dome, Vector2 spot)
    {
        _road.Clear();
        Vector2 away = (dome - spot).normalized;
        float a0 = Mathf.Atan2(away.y, away.x) - 0.5f;
        for (int i = 0; i <= 40; i++)
        {
            float u = i / 40f;
            float r = Mathf.Lerp(_flatOuter * 0.8f, 0.5f, u);
            float a = a0 + 2.2f * u + 0.25f * Mathf.Sin(u * 8f);
            _road.Add(dome + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
        }
    }

    float RoadDistance(Vector2 p)
    {
        float best = float.MaxValue;
        for (int i = 1; i < _road.Count; i++)
        {
            Vector2 a = _road[i - 1], ab = _road[i] - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-9f));
            best = Mathf.Min(best, (p - a - ab * t).magnitude);
        }
        return best;
    }

    /// <summary>
    /// The colour of the Las Campanas ground at p (0.5 = atacamaRock as it is): patches of
    /// redder and greyer rock, the pale gravel of the pad the enclosures stand on, the road.
    /// </summary>
    Color GroundColour(Vector2 p, Vector2 dome)
    {
        float patch = Mathf.PerlinNoise(p.x * 1.7f + 21f, p.y * 1.7f + 4f);
        float fine = Mathf.PerlinNoise(p.x * 9f + 2f, p.y * 9f + 8f);
        Color c = Color.Lerp(new Color(0.4f, 0.38f, 0.38f), new Color(0.6f, 0.5f, 0.43f), patch) * (0.88f + 0.24f * fine);

        float near = (p - dome).magnitude;
        if (_hasTwin) near = Mathf.Min(near, (p - _twinAt).magnitude);
        float pad = 1f - Edge(_flatInner * 1.25f, _flatInner * 1.25f + 0.012f, near);
        float roadHalf = 0.005f * magellanScale;
        float road = (1f - Edge(roadHalf, roadHalf + 0.004f, RoadDistance(p))) * (1f - Edge(0.4f, 0.5f, (p - dome).magnitude));
        c = Color.Lerp(c, new Color(0.78f, 0.74f, 0.68f), Mathf.Max(pad, road * 0.85f));
        c.a = 1f;
        return c;
    }

    /// <summary>
    /// Where the grid's rows or columns lie from min to max: every step within half of centre,
    /// then each a little further apart (grow) out to the ends, so the ground is fine where the
    /// enclosures and the camera are and coarse far off.
    /// </summary>
    static float[] Samples(float min, float max, float centre, float half, float step, float grow)
    {
        float a = Mathf.Clamp(centre - half, min, max), b = Mathf.Clamp(centre + half, min, max);
        var mid = new List<float>();
        for (float x = a; x < b; x += step) mid.Add(x);
        mid.Add(b);
        var lower = new List<float>();
        float d = step, at = a;
        while (at > min) { d *= grow; at = Mathf.Max(min, at - d); lower.Add(at); }
        lower.Reverse();
        var all = new List<float>(lower);
        all.AddRange(mid);
        d = step; at = b;
        while (at < max) { d *= grow; at = Mathf.Min(max, at + d); all.Add(at); }
        return all.ToArray();
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
        if (LasCampanas) return Atacama(p, spot, toward, cameraKm, dome, flat);
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

    /// <summary>
    /// Las Campanas: the summit a long, broad ridge of bare rock, levelled for the two
    /// enclosures, falling a few hundred metres into dry valleys; rounded hills all round, lower
    /// towards the coast and the city, the Andes' foothills rising behind. Made up, not surveyed.
    /// </summary>
    float Atacama(Vector2 p, Vector2 spot, Vector2 toward, float cameraKm, Vector2 dome, float flat)
    {
        Vector2 d = p - spot;
        float s = Vector2.Dot(d, toward);                         // towards the city, the coast
        float t = Vector2.Dot(d, new Vector2(-toward.y, toward.x)); // across

        // The ridge runs across the way to the city, the ground falling off either side of it.
        Vector2 across = new Vector2(-toward.y, toward.x);
        Vector2 ridgeAt = (dome + spot) * 0.5f;
        float off = Vector2.Dot(p - ridgeAt, toward);
        float along = Vector2.Dot(p - ridgeAt, across);
        float ridge = Mathf.Exp(-off * off / (0.9f * 0.9f)) * Mathf.Exp(-along * along / (4f * 4f));
        float h = Mathf.Lerp(summitKm - 0.5f, summitKm, ridge);

        // Rounded hills, the valleys between them dry and broad, rock on rock.
        float hills = Mathf.PerlinNoise(p.x * 0.32f + 3.1f, p.y * 0.32f + 8.4f) - 0.5f;
        float bumps = Mathf.PerlinNoise(p.x * 0.95f + 11.7f, p.y * 0.95f + 2.2f) - 0.5f;
        float rocks = Mathf.PerlinNoise(p.x * 3.1f + 5.5f, p.y * 3.1f + 9.9f) - 0.5f;
        float r = (p - dome).magnitude;
        h += hills * 0.55f * Edge(0.4f, 2.5f, r) + bumps * 0.16f * Edge(0.2f, 1.2f, r) + rocks * 0.03f;

        // Lower towards the coast; the foothills of the Andes rising behind, ridge behind ridge.
        h -= 0.35f * Edge(1.5f, 8f, s);
        for (int i = 0; i < 3; i++)
        {
            float at = -3.5f - 3.2f * i;
            float wander = (Mathf.PerlinNoise(t * 0.18f + 9f * i, 1.7f * i) - 0.5f) * 2f;
            float crest = Mathf.Exp(-Mathf.Pow((s - at - wander) / 1.1f, 2f));
            float height = summitKm - 0.25f + 0.28f * i + 0.35f * Mathf.PerlinNoise(t * 0.3f + 4f * i, 6.2f);
            h = Mathf.Max(h, Mathf.Lerp(summitKm - 0.6f, height, crest));
        }

        // Kept below the camera only round where it stands (it stands low here, over the ridge).
        h = Mathf.Min(h, Mathf.Lerp(cameraKm - 0.012f, cameraKm + 99f, Edge(0.3f, 0.7f, d.magnitude)));
        if (flat > 0f || dome != Vector2.zero)
        {
            float near = (p - dome).magnitude;
            if (_hasTwin) near = Mathf.Min(near, (p - _twinAt).magnitude);
            h = Mathf.Lerp(flat, h, Edge(_flatInner, _flatOuter, near));
        }
        return h;
    }

    Mesh BuildMountains(Vector2 spot, Vector2 toward, float cameraKm, Vector2 dome, float domeGround)
    {
        const int along = 140, across = 180;
        const float back = 14f, ahead = 9f, side = 28f;
        Vector2 perp = new Vector2(-toward.y, toward.x);

        // Las Campanas: rows and columns close together round the enclosures and the stand, for
        // the pad, the road and the rock underfoot; further apart away from them.
        float[] rows = null, cols = null;
        if (LasCampanas)
        {
            Vector2 c = (dome - spot) * 0.5f;
            rows = Samples(-back, ahead, Vector2.Dot(c, toward), 0.35f, 0.006f, 1.05f);
            cols = Samples(-side, side, Vector2.Dot(c, perp), 0.35f, 0.006f, 1.05f);
        }
        int nAlong = rows != null ? rows.Length - 1 : along, nAcross = cols != null ? cols.Length - 1 : across;
        var vertices = new Vector3[(nAlong + 1) * (nAcross + 1)];
        var uv = new Vector2[vertices.Length];
        var colours = LasCampanas ? new Color[vertices.Length] : null;
        for (int a = 0; a <= nAlong; a++)
        for (int b = 0; b <= nAcross; b++)
        {
            float s = rows != null ? rows[a] : Mathf.Lerp(-back, ahead, a / (float)along);
            float t = cols != null ? cols[b] : Mathf.Lerp(-side, side, b / (float)across);
            Vector2 p = spot + toward * s + perp * t;
            float h = Mountains(p, spot, toward, cameraKm, dome, domeGround);
            // The edges sink away under the ground, so it has no edge to see.
            h = Mathf.Lerp(h, -0.6f, Mathf.Max(Edge(side * 0.8f, side, Mathf.Abs(t)), Edge(-back * 0.8f, -back, s)));
            vertices[a * (nAcross + 1) + b] = new Vector3(p.x, h, p.y);
            if (colours != null) colours[a * (nAcross + 1) + b] = GroundColour(p, dome);
            // Where it is on the close-up night map (the frame's km, east and north, to degrees).
            if (_drape)
            {
                float lat = _here.x + p.y / 110.95f, lon = _here.y + p.x / (111.32f * Mathf.Cos(_here.x * Mathf.Deg2Rad));
                uv[a * (nAcross + 1) + b] = new Vector2((lon - _rect.x) / (_rect.z - _rect.x), (lat - _rect.y) / (_rect.w - _rect.y));
            }
        }

        var triangles = new int[nAlong * nAcross * 6];
        int k = 0;
        for (int a = 0; a < nAlong; a++)
        for (int b = 0; b < nAcross; b++)
        {
            int i = a * (nAcross + 1) + b, j = i + nAcross + 1;
            triangles[k++] = i; triangles[k++] = j; triangles[k++] = i + 1;
            triangles[k++] = i + 1; triangles[k++] = j; triangles[k++] = j + 1;
        }

        Mesh mesh = NewMesh("Summit mountains", vertices, triangles);
        mesh.uv = uv;
        if (colours != null) mesh.colors = colours;
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

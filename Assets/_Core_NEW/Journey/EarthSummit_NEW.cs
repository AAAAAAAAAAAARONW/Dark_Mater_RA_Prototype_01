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
/// THE CAMERA. A virtual camera of its own, at the live camera's place and turned as it is,
/// so the brain's blend to it changes nothing; then it turns from looking down to the
/// horizon, towards the city, and comes down onto its stand just over the summit, held
/// there in the frame of km. Not left where the growth would bring the live camera: in
/// Unity that ended somewhere the ground and the dome were not to be seen (3-4 Nov), so the
/// stand is where the camera goes, whatever the growth did. A line in the Console says
/// where it was and where it ended. The light is let go of meanwhile: its trail thins away, then
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

    [Header("Camera")]
    [Tooltip("How far into the last stretch the camera starts to lift, 0 to 1.")]
    [Range(0f, 1f)] [SerializeField] float liftFrom = 0.55f;

    [Tooltip("Seconds the camera takes to lift from the ground to the horizon.")]
    [Min(0.5f)] [SerializeField] float liftSeconds = 7f;

    [Tooltip("Degrees the camera ends up turned from the city: negative to the left, towards the dome.")]
    [Range(-60f, 60f)] [SerializeField] float viewYaw = -8f;

    [Tooltip("Degrees the camera ends up looking below the horizon.")]
    [Range(-30f, 30f)] [SerializeField] float viewPitch = 2f;

    [Header("Dome")]
    [Tooltip("Degrees from the way to the city the dome stands, as seen from the camera: negative is left.")]
    [Range(-60f, 60f)] [SerializeField] float domeYaw = -16f;

    [Tooltip("How far from the camera's spot the dome stands, km.")]
    [Range(0.1f, 3f)] [SerializeField] float domeKm = 0.3f;

    [Tooltip("Times its real size (a drum and dome 30 m across). Bigger reads better from afar.")]
    [Range(0.5f, 5f)] [SerializeField] float domeScale = 2.2f;

    [SerializeField] Color domeColour = new Color(0.75f, 0.77f, 0.8f, 1f);
    [Tooltip("The city's glow round the dome's edges, so it stands out against the dark.")]
    [SerializeField] Color domeRim = new Color(0.35f, 0.22f, 0.14f, 1f);
    [Tooltip("The dim red light inside the dome, seen through the slit.")]
    [SerializeField] Color domeInside = new Color(0.22f, 0.035f, 0.025f, 1f);
    [SerializeField] Color mountainColour = new Color(0.06f, 0.06f, 0.065f, 1f);
    [SerializeField] Color mountainRim = new Color(0.05f, 0.035f, 0.03f, 1f);

    [Header("Light")]
    [SerializeField] Color moonlight = new Color(0.45f, 0.47f, 0.55f, 1f);
    [Tooltip("Light from the whole sky, on everything.")]
    [SerializeField] Color skyLight = new Color(0.03f, 0.032f, 0.045f, 1f);
    [SerializeField] Color cityGlow = new Color(0.3f, 0.17f, 0.08f, 1f);
    [SerializeField] Color haze = new Color(0.08f, 0.05f, 0.045f, 1f);
    [Tooltip("km over which the haze thickens on the mountains: short, so ridge behind ridge stands paler.")]
    [Min(1f)] [SerializeField] float hazeKm = 8f;

    [Header("Sky")]
    [Tooltip("Seconds after the camera has lifted until the stars and the Milky Way are fully up.")]
    [Min(0f)] [SerializeField] float revealAfter = 3f;

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
    bool _built, _lifting, _reported;
    float _liftClock;

    readonly List<Object> _made = new List<Object>();
    Transform _frame;
    Material _setMountain, _setDome, _sky;
    readonly List<Material> _setMaterials = new List<Material>();
    Material _skyBefore;
    bool _skySwapped;

    CinemachineVirtualCamera _vcam;
    Quaternion _liftFrom, _liftTo;
    Transform _dome;
    Vector3 _liftStart, _stand;   // world, where the lift began; the frame, where it ends

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

    void LateUpdate()
    {
        if (!_built)
        {
            if (_zoom.SummitProgress < 0f) return;
            _built = true;
            Build();
        }
        if (_frame == null) return;

        float kmToWorld = _frame.lossyScale.x;
        foreach (Material m in _setMaterials) m.SetFloat("_KmToWorld", kmToWorld);

        if (!_lifting && _zoom.SummitProgress >= liftFrom) BeginLift();
        if (_lifting) Lift();
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
                      "   progress " + _zoom.SummitProgress.ToString("0.00") + "   clip " + cam.nearClipPlane.ToString("0.###") + "-" + cam.farClipPlane.ToString("0");
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
        parts += "dome on screen " + vp.x.ToString("0.00") + ", " + vp.y.ToString("0.00") + ", z " + vp.z.ToString("0.0") +
                 "   cull mask " + cam.cullingMask + "   drawing camera " + (Camera.current != null ? Camera.current.name : "-");

        GUI.color = Color.yellow;
        GUI.Label(new Rect(10, Screen.height - 66, Screen.width - 20, 22), parts);
        GUI.Label(new Rect(10, Screen.height - 46, Screen.width - 20, 22), text);
    }

    void OnDisable()
    {
        if (_skySwapped) { RenderSettings.skybox = _skyBefore; _skySwapped = false; }
        ShowPhoton();
        foreach (Renderer r in _systemParts) if (r != null) r.enabled = true;
        _systemParts.Clear();
        if (_built && _zoom != null && _zoom.Body != null && _zoom.Earth != null) { _zoom.Body.SetAirVisible(true); SetSurfaceVisible(true); }
        if (_zoom != null && _zoom.Photon != null && _lifting) _zoom.Photon.SetCameraInputLocked(false);
        foreach (Object o in _made) Kill(o);
        _made.Clear();
        _setMaterials.Clear();
        _frame = null;
        _vcam = null;
        _built = _lifting = _reported = false;
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

        // How big a km will be once down, and where the camera will be then, in km.
        float kmNow = body.GroundRadius / EarthZoom_NEW.EarthRadiusKm;
        float kmEnd = kmNow * _zoom.SummitFactor;
        Vector3 c = cam.transform.position - ground;
        Vector3 camKm = new Vector3(Vector3.Dot(c, _east), Vector3.Dot(c, _up), Vector3.Dot(c, _north)) / kmEnd;

        // The camera's stand: just over the summit, near the landing point, whatever the
        // sums above came to (see THE CAMERA).
        Vector2 aside = Vector2.ClampMagnitude(new Vector2(camKm.x, camKm.z), 0.5f);
        float standKm = Mathf.Clamp(_zoom.SummitCameraKm, summitKm + 0.02f, summitKm + 0.3f);
        Debug.Log("[EarthSummit_NEW] Building the summit at " + here.x.ToString("0.00") + ", " + here.y.ToString("0.00") +
                  ". The growth should bring the camera to " + camKm.y.ToString("0.00") + " km up, " +
                  new Vector2(camKm.x, camKm.z).magnitude.ToString("0.00") + " km aside; its stand is " +
                  standKm.ToString("0.00") + " km up, " + aside.magnitude.ToString("0.00") + " km aside.", this);
        camKm = new Vector3(aside.x, standKm, aside.y);
        _stand = camKm;

        if (debugLog)
            Debug.Log("[EarthSummit_NEW] Landing at " + here.x.ToString("0.00") + ", " + here.y.ToString("0.00") +
                      "; the city " + (Vector3.Distance(cityWorld, ground) / kmNow).ToString("0") + " km away; the camera will be " +
                      camKm.y.ToString("0.00") + " km up.", this);

        // The finer patch of the true sphere, and the coarse one put away.
        BuildCap(earth, body, here);
        body.SetAirVisible(false);
        SetSurfaceVisible(false);

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
        _setDome = Lit(set, domeColour);
        _setDome.SetColor("_Rim", domeRim);

        // In the frame: the way to the city, and the camera's spot on the ground.
        Vector3 cityKm = _frame.InverseTransformDirection(_toCity);
        Vector2 toward = new Vector2(cityKm.x, cityKm.z).normalized;
        Vector2 spot = new Vector2(camKm.x, camKm.z);

        Vector2 domeAt = spot + Rotate(toward, domeYaw) * domeKm;
        float domeGround = Mountains(domeAt, spot, toward, camKm.y, Vector2.zero, 0f);
        Part("Mountains", BuildMountains(spot, toward, camKm.y, domeAt, domeGround), _setMountain, _frame);

        // The slit opens three-quarters towards the camera, so it shows.
        Vector2 faces = Rotate((spot - domeAt).normalized, 50f);
        var dome = Make("Dome").transform;
        _dome = dome;
        dome.SetParent(_frame, false);
        dome.localPosition = new Vector3(domeAt.x, domeGround, domeAt.y);
        dome.localRotation = Quaternion.LookRotation(new Vector3(faces.x, 0f, faces.y), Vector3.up);
        dome.localScale = Vector3.one * 0.001f * domeScale;   // metres
        BuildDome(dome);

        // The sky, while the camera still looks at the ground.
        BuildSky();

        // The rest of the Solar System, left behind by EarthZoom_NEW when it took Earth out:
        // no planet belongs in this sky.
        foreach (Renderer r in GetComponentsInChildren<Renderer>(false))
            if (r.enabled) { r.enabled = false; _systemParts.Add(r); }

        // The light, to be let go of as the camera lifts.
        PlayerRig_NEW photon = _zoom.Photon;
        if (photon != null)
        {
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

    void BeginLift()
    {
        _lifting = true;
        _liftClock = 0f;

        Camera cam = GetReal3DView_NEW.ScreenCamera;
        if (cam == null) return;

        var go = Make("VCam_Summit");
        go.transform.SetPositionAndRotation(cam.transform.position, cam.transform.rotation);
        _vcam = go.AddComponent<CinemachineVirtualCamera>();
        LensSettings lens = _vcam.m_Lens;
        lens.FieldOfView = cam.fieldOfView;
        lens.NearClipPlane = cam.nearClipPlane;
        lens.FarClipPlane = cam.farClipPlane;
        _vcam.m_Lens = lens;
        _vcam.Priority = 90;   // over CameraDirector_NEW's 50: nothing else is to move it now

        _liftFrom = cam.transform.rotation;
        _liftStart = cam.transform.position;
        Vector3 level = Rotate(_toCity, viewYaw);
        Vector3 forward = level * Mathf.Cos(viewPitch * Mathf.Deg2Rad) + _up * Mathf.Sin(viewPitch * Mathf.Deg2Rad);
        _liftTo = Quaternion.LookRotation(forward, _up);

        if (_zoom.Photon != null) _zoom.Photon.SetCameraInputLocked(true, "Mount Wilson");
    }

    void Lift()
    {
        _liftClock += Time.deltaTime;
        float q = Mathf.Clamp01(_liftClock / liftSeconds);

        if (_vcam != null)
        {
            // Smootherstep: from rest and to rest, no jolt either end.
            float s = q * q * q * (q * (q * 6f - 15f) + 10f);
            _vcam.transform.rotation = Quaternion.Slerp(_liftFrom, _liftTo, s);

            // Down onto the stand: across in step, the height by the same factor each moment.
            Vector3 from = _frame.InverseTransformPoint(_liftStart);
            float height = Mathf.Exp(Mathf.Lerp(Mathf.Log(Mathf.Max(from.y, 1e-3f)), Mathf.Log(_stand.y), s));
            Vector3 at = new Vector3(Mathf.Lerp(from.x, _stand.x, s), height, Mathf.Lerp(from.z, _stand.z, s));
            _vcam.transform.position = _frame.TransformPoint(at);

            if (q >= 1f && !_reported)
            {
                _reported = true;
                Debug.Log("[EarthSummit_NEW] On the summit. The camera started the lift " + from.y.ToString("0.00") +
                          " km up and is " + height.ToString("0.00") + " km up; a km is " + _frame.lossyScale.x.ToString("0.000") +
                          " world units; Earth's radius is " + (_zoom.Body != null ? _zoom.Body.GroundRadius.ToString("0") : "?") + ".", this);
            }
        }

        // The trail thins away over the first third; then the light is hidden.
        float thin = 1f - Mathf.Clamp01(q / 0.3f);
        for (int i = 0; i < _trails.Count; i++)
            if (_trails[i] != null) _trails[i].widthMultiplier = _trailWidths[i] * thin;
        if (thin <= 0f) HidePhoton();

        if (_sky != null)
        {
            float t = _liftClock - liftSeconds * 0.3f;
            _sky.SetFloat("_Reveal", Mathf.SmoothStep(0f, 1f, t / (liftSeconds * 0.7f + revealAfter)));
        }
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
        float front = 1f - Mathf.SmoothStep(0.15f, 3f, s);
        float h = Mathf.Lerp(-0.4f, summitKm, front);

        // Spurs running down towards the city, valleys between them, on the slope only.
        float slope = Mathf.SmoothStep(-0.5f, 2f, s) * (1f - Mathf.SmoothStep(5.5f, 8f, s));
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
        h += Mathf.PerlinNoise(s * 0.18f + 4f, t * 0.18f + 1f) * 0.35f * (1f - Mathf.SmoothStep(-4f, -1f, s));

        // A rounded summit under the camera, and kept well below it.
        float r = d.magnitude;
        h = Mathf.Lerp(summitKm + 0.01f * Mathf.Cos(r * 4f), h, Mathf.SmoothStep(0.25f, 0.7f, r));
        h = Mathf.Min(h, cameraKm - 0.04f);

        if (flat > 0f || dome != Vector2.zero)
            h = Mathf.Lerp(flat, h, Mathf.SmoothStep(0.03f * domeScale, 0.07f * domeScale, (p - dome).magnitude));
        return h;
    }

    Mesh BuildMountains(Vector2 spot, Vector2 toward, float cameraKm, Vector2 dome, float domeGround)
    {
        const int along = 140, across = 180;
        const float back = 14f, ahead = 9f, side = 28f;
        Vector2 perp = new Vector2(-toward.y, toward.x);

        var vertices = new Vector3[(along + 1) * (across + 1)];
        for (int a = 0; a <= along; a++)
        for (int b = 0; b <= across; b++)
        {
            float s = Mathf.Lerp(-back, ahead, a / (float)along);
            float t = Mathf.Lerp(-side, side, b / (float)across);
            Vector2 p = spot + toward * s + perp * t;
            float h = Mountains(p, spot, toward, cameraKm, dome, domeGround);
            // The sides sink away, out of sight anyway.
            h = Mathf.Lerp(h, -0.6f, Mathf.SmoothStep(side * 0.8f, side, Mathf.Abs(t)));
            vertices[a * (across + 1) + b] = new Vector3(p.x, h, p.y);
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
        FixWinding(mesh, Vector3.up * 10f, Vector3.zero);
        mesh.RecalculateNormals();
        return mesh;
    }

    // ── The dome ────────────────────────────────────────────────────────────

    /// <summary>
    /// The 100-inch's dome, in metres: a drum 15 m round and 11 high, a hemisphere on it, the
    /// slit 5 m wide open from just past the top down the far side (+z), and inside it dark.
    /// </summary>
    void BuildDome(Transform dome)
    {
        const float r = 15f, drum = 11f, slit = 2.5f;
        const int around = 64, up = 20;

        var outV = new List<Vector3>();
        var outT = new List<int>();
        var inV = new List<Vector3>();
        var inT = new List<int>();

        // The drum, open at the top where the dome sits.
        for (int i = 0; i <= around; i++)
        {
            float a = i * Mathf.PI * 2f / around;
            Vector3 rim = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * r;
            outV.Add(rim - Vector3.up * 2f);   // into the ground a little
            outV.Add(rim + Vector3.up * drum);
        }
        for (int i = 0; i < around; i++)
        {
            int v = i * 2;
            outT.AddRange(new[] { v, v + 2, v + 1, v + 2, v + 3, v + 1 });
        }

        // The hemisphere, less the slit; and the same inside, darker, facing in.
        int baseOut = outV.Count;
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
            if (!open) outT.AddRange(new[] { baseOut + v, baseOut + v + 1, baseOut + w, baseOut + v + 1, baseOut + w + 1, baseOut + w });
            inT.AddRange(new[] { v, w, v + 1, v + 1, w, w + 1 });
        }

        // A floor inside, dark, so the slit is not a window onto the ground.
        int floor = inV.Count;
        inV.Add(Vector3.up * (drum - 1f));
        for (int i = 0; i <= around; i++)
        {
            float a = i * Mathf.PI * 2f / around;
            inV.Add(Vector3.up * (drum - 1f) + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * (r - 0.4f));
        }
        for (int i = 0; i < around; i++) inT.AddRange(new[] { floor, floor + 1 + i, floor + 2 + i });

        // Wound by hand (FixWinding cannot tell out from in on the drum's upright walls):
        // drum and dome face out, the inside and the floor face in and up.
        Mesh outside = NewMesh("Dome", outV.ToArray(), outT.ToArray());
        outside.RecalculateNormals();
        Part("Dome outside", outside, _setDome, dome);

        Mesh inside = NewMesh("Dome inside", inV.ToArray(), inT.ToArray());
        inside.RecalculateNormals();
        Material dark = Lit(Resources.Load<Shader>("SummitSet_NEW"), new Color(0.01f, 0.01f, 0.012f, 1f));
        dark.SetColor("_Emission", domeInside);
        Part("Dome inside", inside, dark, dome);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

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

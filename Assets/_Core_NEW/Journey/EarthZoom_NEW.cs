using Cinemachine;
using UnityEngine;

/// <summary>
/// The last stretch to Earth: Earth grows until its air fills the screen, and the light goes
/// into it.
///
/// WHY. The trail is drawn live at one size, and the camera rides 5 units behind it, so the
/// trail is always about the same size on screen. Earth, true to the Sun at the Solar
/// System's scale, is 0.12 across: flown at straight, it stayed a dot beside the trail, and
/// the light went into the dot. How big Earth looks against the trail is only its radius
/// against the trail's width — no lens changes that.
///
/// HOW. From startDistance short of Earth's centre, the whole Solar System is scaled up
/// about Earth's centre, so that holdDistance short of Earth's air, Earth's radius is
/// finalRadius. Scaling it all keeps the Sun, the planets and Earth true to each
/// other; scaling about Earth's centre keeps Earth on the light's line (it is the one point
/// that does not move), and everything else falls away from it — what is between the
/// light and Earth streams out past the camera, as it would flying in far faster. The
/// light never turns and keeps its speed; the growth does the rest.
///
/// THE CURVE. Earth's size ON SCREEN grows by the same factor every unit flown — at the
/// light's steady speed, every second — from the dot it is at startDistance to filling the
/// screen at holdDistance short of its air. The first version grew the system itself on a
/// curve: Earth stayed a dot for most of the way, burst in the last second and a half, and
/// the gate came before anyone saw it big. Then the growth stops and the light flies the
/// last holdDistance into a screen full of Earth before the gate.
///
/// THE CAMERA. Over the first part of it the Solar System camera comes round behind the
/// light (look input is locked) and down to lowHeight above its line. From above, the
/// line ahead rises up the screen, so the light looked to go in over the top of Earth;
/// from behind it goes in at the middle. Near the end the lens widens by fovKick — for
/// the rush, not for size. What it leaves is kept into the Earth layer, which shares the
/// camera; the Earth arrival's own transition unlocks look.
///
/// THE SUN. It is beyond Earth (SolarSystemPath_NEW), and in this system half as wide as
/// Earth's orbit, so grown with everything else it would stand as a third of the sky beside
/// Earth. So it is held to sunSeenDegrees on screen as the rest grows, a bright disc that goes
/// down behind Earth's limb as Earth comes to fill the view: the light arrives in the dark.
///
/// THE DESCENT. Past the gate the light would fly on into the ground and out through the
/// inside of the sphere. EarthClouds_NEW used to hide that under a deck of cloud; without it,
/// the light is held where it crossed the gate, at the top of the air, and the whole system
/// goes on growing — now about the point of ground straight below it, the one point that
/// stays put. The ground comes up, the curve flattens, the night-side clouds and city lights
/// spread out under the camera: a fall, with the light hanging in front of it. It grows by
/// descentGrowth over descentSeconds, fast first and settling, and stops. On the way the
/// night side's global map gives way to the close-up of the Los Angeles basin
/// (CelestialBody_NEW's region map), so twelve times still has detail.
///
/// THE SUMMIT. Then, with summitCameraKm set, it goes on down: from about 190 km over the
/// ground to summitCameraKm, growing again about the same point, slow, fast and slow over
/// summitSeconds. That is a hundredfold more, and there Earth's own sphere is too coarse
/// to stand on; EarthSummit_NEW lays the true sphere round the landing point, the mountains,
/// the dome and the night sky over it, and lifts the camera to the horizon.
///
/// Earth alone grows over that stretch, taken out of the group first. Grown with it, the
/// group's own position went out to tens of millions of units, where a float is a few
/// units apart — a kilometre at that scale — and the ground came out above the camera,
/// which then saw nothing below the horizon (first try in Unity, 3 Nov). Earth on its own
/// stays within some tens of thousands. The rest of the system is left where it was, and
/// EarthSummit_NEW hides it: no planet belongs in that sky.
///
/// THE GATE. On Awake the Earth gate is put at the top of Earth's air as it will be, so
/// the ending starts as the light goes in. This runs after EarthApproach_NEW and overrides
/// where that put the gate; still Awake, so UniverseJourneyTracker_NEW reads it in Start.
///
/// Put it on the Solar System's group root, beside SolarSystemPath_NEW.
/// </summary>
[DefaultExecutionOrder(-199)]
[DisallowMultipleComponent]
[HierarchyBadge_NEW("EARTH ZOOM", "#4A94F2")]
public class EarthZoom_NEW : MonoBehaviour
{
    [Header("Wiring (found if empty)")]
    [SerializeField] PlayerRig_NEW photon;

    [Tooltip("The Earth the light flies into: the child of this group named 'Earth'.")]
    [SerializeField] Transform earth;

    [Tooltip("The gate into the Earth layer.")]
    [SerializeField] LayerGate_NEW earthGate;

    [SerializeField] string earthLayerId = "Earth";

    [Tooltip("The Solar System's camera. VCam_SolarSystem if empty.")]
    [SerializeField] CinemachineFreeLook solarCamera;

    [Tooltip("The Sun. The child named 'Sun' if empty.")]
    [SerializeField] Transform sun;

    [Header("Zoom")]
    [Tooltip("How far short of Earth's centre, along the light's line, the growth starts. " +
             "Further: a longer, gentler rush; nearer: more of the plain approach first.")]
    [Min(1f)] [SerializeField] float startDistance = 45f;

    [Tooltip("Earth's radius, in world units, as the light reaches its air. The camera is " +
             "about 5 behind the light: at 15 the air fills the screen before the light is in.")]
    [Min(0.1f)] [SerializeField] float finalRadius = 15f;

    [Tooltip("How far the light flies at full size, Earth filling the screen, before it " +
             "reaches the air and the gate. At the usual speed of 2.5, 3 is just over a second.")]
    [Min(0f)] [SerializeField] float holdDistance = 3f;

    [Tooltip("Where in the air the gate sits: 1 at its top, 0 on the ground.")]
    [Range(0f, 1f)] [SerializeField] float gateInAir = 1f;

    [Tooltip("The Sun's radius on screen, in degrees, by the end of the zoom. 0 lets it grow with " +
             "the rest.")]
    [Range(0f, 30f)] [SerializeField] float sunSeenDegrees = 3f;

    [Header("Descent")]
    [Tooltip("Past the gate, hold the light at the top of the air and grow Earth about the ground " +
             "below it, so the light seems to fall towards the ground instead of flying into it.")]
    [SerializeField] bool descend = true;

    [Tooltip("Seconds the descent takes, from the gate until Earth stops growing.")]
    [Min(0.5f)] [SerializeField] float descentSeconds = 14f;

    [Tooltip("How many times Earth grows over the descent. With the Los Angeles close-up (Earth's region " +
             "map) about 12 still has detail; much past 20 it goes soft.")]
    [Range(1f, 50f)] [SerializeField] float descentGrowth = 12f;

    [Tooltip("Then on down until the camera is this many km over the ground (sea level), for " +
             "EarthSummit_NEW; Mount Wilson's summit is 1.74. 0: stop after the descent.")]
    [Min(0f)] [SerializeField] float summitCameraKm = 1.8f;

    [Tooltip("Seconds that last stretch takes.")]
    [Min(0.5f)] [SerializeField] float summitSeconds = 12f;

    [Header("Camera")]
    [SerializeField] bool driveCamera = true;

    [Tooltip("By how far through the zoom the camera is behind and down, 0 to 1.")]
    [Range(0.05f, 1f)] [SerializeField] float settleBy = 0.5f;

    [Tooltip("The camera's height above the light's line once down. Not 0: from dead behind " +
             "the trail is end on, a dot.")]
    [SerializeField] float lowHeight = 0.5f;

    [Tooltip("Degrees the lens widens by at the end, for the rush.")]
    [Range(0f, 30f)] [SerializeField] float fovKick = 10f;

    [Tooltip("How far through the zoom the lens starts to widen, 0 to 1.")]
    [Range(0f, 1f)] [SerializeField] float kickFrom = 0.6f;

    [SerializeField] bool debugLog = false;

    CelestialBody_NEW _body;
    LayerDive_NEW _dive;
    LayerState_NEW _state;
    bool _zooming, _done, _warned, _descending, _landed, _toSummit;
    float _u, _from, _r0, _radius, _logSeenFrom, _logSeenTo;
    float _descentClock, _sAtGate;
    Vector3 _ground, _rootAtGate, _scaleAtGate;
    float _summitClock, _summitFactor = 1f;
    Vector3 _earthAtSummit, _earthScaleAtSummit;

    /// <summary>Earth's mean radius, km: what one radius of the sphere stands for.</summary>
    public const float EarthRadiusKm = 6371f;

    /// <summary>-1 until the last stretch to the summit begins, then 0 to 1 through it.</summary>
    public float SummitProgress { get; private set; } = -1f;

    /// <summary>How many times Earth grows over the last stretch; set as it begins.</summary>
    public float SummitFactor => _summitFactor;

    /// <summary>The camera's height over the ground once down, km.</summary>
    public float SummitCameraKm => summitCameraKm;

    /// <summary>The point of ground the descent grows about, world space; it does not move.</summary>
    public Vector3 GroundPoint => _ground;

    /// <summary>Up at the ground point, world space.</summary>
    public Vector3 GroundUp { get; private set; }

    public Transform Earth => earth;
    public CelestialBody_NEW Body => _body;
    public PlayerRig_NEW Photon => photon;
    Vector3 _rootFrom, _scaleFrom, _scaleSet, _centre, _sunScaleFrom;
    float _sunRadiusFrom, _sunSeenFrom;
    float _xFrom, _yFrom, _heightFrom, _fovFrom;

    /// <summary>Distance from Earth's centre, along the line, at which the light reaches the gate.</summary>
    float GateDistance => finalRadius * (1f + (_body != null ? _body.AirDepth : 0f) * gateInAir);

    /// <summary>Distance from Earth's centre at which Earth is full size.</summary>
    float ZoomEnd => GateDistance + holdDistance;

    bool IsEarthLayer => _state != null && _state.CurrentLayerId == earthLayerId;

    void Awake()
    {
        if (photon == null) photon = FindObjectOfType<PlayerRig_NEW>();
        if (earth == null) earth = transform.Find("Earth");
        if (sun == null) sun = transform.Find("Sun");
        if (earthGate == null)
            foreach (LayerGate_NEW g in FindObjectsOfType<LayerGate_NEW>())
                if (g.LayerId == earthLayerId) { earthGate = g; break; }
        if (solarCamera == null)
        {
            GameObject go = GameObject.Find("VCam_SolarSystem");
            if (go != null) solarCamera = go.GetComponent<CinemachineFreeLook>();
        }

        if (photon == null || earth == null)
        {
            Debug.LogWarning("[EarthZoom_NEW] No photon or no child named 'Earth'; Earth will not grow.", this);
            enabled = false;
            return;
        }

        _body = earth.GetComponent<CelestialBody_NEW>();
        _dive = FindObjectOfType<LayerDive_NEW>();
        _state = FindObjectOfType<LayerState_NEW>();

        if (startDistance <= ZoomEnd)
            Debug.LogWarning("[EarthZoom_NEW] startDistance " + startDistance + " is inside where Earth is full size (" +
                             ZoomEnd.ToString("0.0") + "); it will not grow. Raise it.", this);

        if (earthGate != null)
        {
            // Only Z, as EarthApproach_NEW: the gate is a wide plane across the line.
            float along = photon.MovementDirection.z < 0f ? 1f : -1f;
            Vector3 p = earthGate.transform.position;
            float z = earth.position.z + along * GateDistance;

            if (debugLog) Debug.Log("[EarthZoom_NEW] Earth gate z " + p.z.ToString("0.00") + " -> " + z.ToString("0.00") + ".", this);

            earthGate.transform.position = new Vector3(p.x, p.y, z);

            if (debugLog) Debug.Log("[EarthZoom_NEW] Ready on '" + name + "': Earth '" + earth.name + "' at " + earth.position +
                      ", radius " + (_body != null ? _body.GroundRadius.ToString("0.000") : "? (no CelestialBody_NEW)") +
                      ", gate z " + z.ToString("0.0") + ", photon " + photon.name + " flying " + photon.MovementDirection + ".", this);
        }
    }

    void Update()
    {
        if (_landed) return;

        // The dive into the Solar System places and scales this root itself every frame, and
        // puts it back as built at the end. Nothing is measured or moved until it is over.
        if (_dive != null && _dive.IsActive) return;

        if ((_zooming || _descending || _toSummit) && !_warned && (transform.localScale - _scaleSet).sqrMagnitude > 1e-8f && (_warned = true))
            Debug.LogWarning("[EarthZoom_NEW] Something else set '" + name + "''s scale to " + transform.localScale.x +
                             " (this set " + _scaleSet.x + ").", this);

        if (_descending) { Descend(); return; }
        if (_toSummit) { ToSummit(); return; }

        Vector3 dir = photon.MovementDirection;
        float distance = Vector3.Dot(earth.position - photon.transform.position, dir);

        if (_done)
        {
            // Held once the Earth layer has begun, so the light is through the gate first; and
            // half way into the air at the latest, should that never come.
            float deepInAir = GateDistance - 0.5f * (GateDistance - finalRadius);
            if (!descend) _landed = true;
            else if (IsEarthLayer || distance <= deepInAir) BeginDescent(dir);
            return;
        }

        if (!_zooming)
        {
            if (distance > startDistance) return;
            if (distance <= ZoomEnd) { _done = true; return; }
            Begin(distance);
        }

        _u = Mathf.Max(_u, Mathf.InverseLerp(_from, ZoomEnd, distance));

        // Earth's size on screen, about its radius over its distance from the camera, grows by
        // the same factor every unit; the radius is whatever gives that from here. Never smaller.
        float seen = Mathf.Exp(Mathf.Lerp(_logSeenFrom, _logSeenTo, _u));
        _radius = _u >= 1f ? finalRadius : Mathf.Clamp(seen * (distance + CameraBack()), _radius, finalRadius);

        float s = _radius / _r0;
        transform.localScale = _scaleFrom * s;
        transform.position = _centre + (_rootFrom - _centre) * s;
        _scaleSet = transform.localScale;

        HoldSun(s);

        if (driveCamera && solarCamera != null) Frame(_u);

        if (debugLog && Time.frameCount % 30 == 0)
            Debug.Log("[EarthZoom_NEW] u " + _u.ToString("0.00") + ", distance " + distance.ToString("0.0") +
                      ", Earth radius " + _radius.ToString("0.00") + ".", this);

        if (_u >= 1f)
        {
            _done = true;
            if (debugLog) Debug.Log("[EarthZoom_NEW] Done: Earth radius " + (_body != null ? _body.GroundRadius.ToString("0.0") : "?") + ".", this);
        }
    }

    /// <summary>Holds the Sun, grown s times with the rest, to the size on screen it should have now.</summary>
    void HoldSun(float s)
    {
        if (sun == null || sunSeenDegrees <= 0f) return;

        Camera cam = GetReal3DView_NEW.ScreenCamera;
        if (cam == null) return;

        float seen = Mathf.Lerp(_sunSeenFrom, sunSeenDegrees, Mathf.SmoothStep(0f, 1f, _u));
        float wanted = Mathf.Tan(seen * Mathf.Deg2Rad) * Vector3.Distance(sun.position, cam.transform.position);
        sun.localScale = _sunScaleFrom * Mathf.Min(1f, wanted / (_sunRadiusFrom * s));
    }

    /// <summary>Holds the light at the gate and fixes the point of ground below it (see THE DESCENT).</summary>
    void BeginDescent(Vector3 dir)
    {
        _descending = true;
        _descentClock = 0f;
        photon.SetHeld(true);

        float r = _body != null ? _body.GroundRadius : 0.5f * earth.lossyScale.x;
        _ground = earth.position - dir * r;
        GroundUp = -dir;
        _rootAtGate = transform.position;
        _scaleAtGate = transform.localScale;
        _scaleSet = _scaleAtGate;
        _sAtGate = _zooming ? _radius / _r0 : 1f;

        if (debugLog) Debug.Log("[EarthZoom_NEW] Descent: light held " + Vector3.Dot(photon.transform.position - _ground, -dir).ToString("0.00") +
                                " above the ground; Earth grows x" + descentGrowth + " over " + descentSeconds + "s.", this);
    }

    void Descend()
    {
        // On the same clock the light flies by, so every copy on the wall descends alike.
        _descentClock += Time.deltaTime;
        float p = Mathf.Clamp01(_descentClock / descentSeconds);

        // Fastest at the gate, as the zoom was, settling to a stop.
        float eased = 1f - (1f - p) * (1f - p);
        float k = Mathf.Exp(Mathf.Log(descentGrowth) * eased);

        transform.localScale = _scaleAtGate * k;
        transform.position = _ground + (_rootAtGate - _ground) * k;
        _scaleSet = transform.localScale;

        if (_zooming) HoldSun(_sAtGate * k);

        if (p >= 1f)
        {
            _descending = false;
            if (debugLog) Debug.Log("[EarthZoom_NEW] Descent over: Earth radius " + (_body != null ? _body.GroundRadius.ToString("0.0") : "?") + ".", this);
            if (!BeginSummit()) _landed = true;
        }
    }

    /// <summary>Works out how much further to grow for the camera to be summitCameraKm up (see THE SUMMIT).</summary>
    bool BeginSummit()
    {
        Camera cam = GetReal3DView_NEW.ScreenCamera;
        if (summitCameraKm <= 0f || cam == null || _body == null) return false;

        float kmWorld = _body.GroundRadius / EarthRadiusKm;
        float heightKm = Vector3.Dot(cam.transform.position - _ground, GroundUp) / kmWorld;
        _summitFactor = Mathf.Max(1f, heightKm / summitCameraKm);
        _summitClock = 0f;
        _toSummit = true;
        SummitProgress = 0f;

        // Earth on its own from here (see THE SUMMIT): the group stays as it is.
        earth.SetParent(null, true);
        _earthAtSummit = earth.position;
        _earthScaleAtSummit = earth.localScale;

        if (debugLog) Debug.Log("[EarthZoom_NEW] To the summit: camera " + heightKm.ToString("0") + " km up, down to " +
                                summitCameraKm + " km, Earth grows x" + _summitFactor.ToString("0") + " more.", this);
        return true;
    }

    void ToSummit()
    {
        _summitClock += Time.deltaTime;
        float p = Mathf.Clamp01(_summitClock / summitSeconds);
        SummitProgress = p;

        // From rest, as the descent ended, and to rest. Earth alone, about the same point.
        float k = Mathf.Exp(Mathf.Log(_summitFactor) * Mathf.SmoothStep(0f, 1f, p));
        earth.localScale = _earthScaleAtSummit * k;
        earth.position = _ground + (_earthAtSummit - _ground) * k;

        if (p >= 1f)
        {
            _toSummit = false;
            _landed = true;
        }
    }

    /// <summary>
    /// How far behind the light, along its line, the camera is. Through ScreenCamera, not
    /// Camera.main: every copy on the wall must get the same answer, or the copies grow Earth
    /// differently and the wall comes apart at its seams.
    /// </summary>
    float CameraBack()
    {
        Camera cam = GetReal3DView_NEW.ScreenCamera;
        return cam != null ? Mathf.Max(0f, Vector3.Dot(photon.transform.position - cam.transform.position, photon.MovementDirection)) : 5f;
    }

    void Begin(float distance)
    {
        _zooming = true;

        _r0 = Mathf.Max(_body != null ? _body.GroundRadius : 0.5f * earth.lossyScale.x, 1e-4f);
        _radius = _r0;
        _from = distance;
        _logSeenFrom = Mathf.Log(_r0 / (distance + CameraBack()));
        _logSeenTo = Mathf.Log(finalRadius / (ZoomEnd + CameraBack()));
        _rootFrom = transform.position;
        _scaleFrom = transform.localScale;
        _scaleSet = _scaleFrom;
        _centre = earth.position;

        if (sun != null)
        {
            _sunScaleFrom = sun.localScale;
            _sunRadiusFrom = 0.5f * sun.lossyScale.x;
            Camera cam = GetReal3DView_NEW.ScreenCamera;
            float away = cam != null ? Vector3.Distance(sun.position, cam.transform.position) : 50f;
            _sunSeenFrom = Mathf.Atan2(_sunRadiusFrom, away) * Mathf.Rad2Deg;
        }

        if (driveCamera && solarCamera != null)
        {
            photon.SetCameraInputLocked(true, "Earth approach");
            _xFrom = solarCamera.m_XAxis.Value;
            _yFrom = solarCamera.m_YAxis.Value;
            _heightFrom = solarCamera.m_Orbits[1].m_Height;
            _fovFrom = solarCamera.m_Lens.FieldOfView;
        }

        if (debugLog) Debug.Log("[EarthZoom_NEW] Begin at " + distance.ToString("0.0") + ": Earth radius " + _r0.ToString("0.000") +
                                " -> " + finalRadius + " (x" + (finalRadius / _r0).ToString("0") + ").", this);
    }

    void Frame(float u)
    {
        float settle = Mathf.SmoothStep(0f, 1f, u / settleBy);
        solarCamera.m_XAxis.Value = Mathf.LerpAngle(_xFrom, 0f, settle);
        solarCamera.m_YAxis.Value = Mathf.Lerp(_yFrom, 0.5f, settle);
        solarCamera.m_Orbits[1].m_Height = Mathf.Lerp(_heightFrom, lowHeight, settle);

        float kick = kickFrom < 1f ? Mathf.SmoothStep(0f, 1f, (u - kickFrom) / (1f - kickFrom)) : 0f;
        solarCamera.m_Lens.FieldOfView = _fovFrom + fovKick * kick;
    }

    void OnDrawGizmos()
    {
        if (!DebugView_NEW.Gizmos || earth == null) return;

        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.5f);
        Gizmos.DrawWireSphere(earth.position, finalRadius);
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.2f);
        Gizmos.DrawWireSphere(earth.position, startDistance);
    }
}

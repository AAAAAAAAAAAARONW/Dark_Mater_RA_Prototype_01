using Cinemachine;
using UnityEngine;

/// <summary>
/// The last stretch: the light flies on down its line, as it has all the way from the
/// quasar, and Earth comes up to meet it - grows from a dot until the light goes in at the
/// slit of the 100-inch's dome on Mount Wilson and down the telescope to its mirror.
///
/// WHY. The trail is drawn live at one size, and the camera rides 5 units behind it, so the
/// trail is always about the same size on screen. Earth, true to the Sun at the Solar
/// System's scale, is 0.12 across: flown at straight, it stays a dot beside the trail. How
/// big Earth looks against the trail is only its radius against the trail's width, so it
/// is Earth that has to grow.
///
/// HOW. When the light is startDistance short of Earth's centre, the point it will end at
/// is fixed: where its line meets Earth's surface, raised by the height of the telescope's
/// mirror on the summit (EarthSummit_NEW's ArrivalHeightKm) - the arrival. Then the world
/// grows about that point, the one point that does not move, while the light flies on
/// into it at its own speed, never turning, never stopping. How much it has grown goes
/// with how far the light has come: from nothing at the start to a km being
/// summitUnitsPerKm world units with finalUnits still to fly - an even climb in the
/// logarithm, eased in and out. So the light's distance to the arrival shrinks only as fast
/// as it flies, while what it stands for shrinks from millions of km to a few hundred
/// metres: Earth from a dot to filling the screen, its night side, Los Angeles, the
/// mountains, the dome. (6-15 Nov the light was held at the top of the air and carried
/// down; asked for: the same straight line as all the way, unchanged.)
///
/// THE LAST FEW SECONDS. Over the last finalUnits, at full size, the light slows to
/// finalSpeed of its speed, eased, so it is seen to go in at the slit and down the tube;
/// and the camera's orbit closes in on it by endCameraScale so it follows through the slit
/// rather than standing off above the dome. Then it is held at the arrival.
///
/// THE SPEED. The light keeps the speed it had; the Earth layer's own slowing
/// (SpeedResponder_NEW) is cancelled through the approach multiplier, which then does the
/// last slowing. Narration and pacing multipliers still apply.
///
/// THE GROUP AND EARTH. Up to the size where Earth is detachRadius across the whole Solar
/// System grows, so the Sun and planets stay true to Earth; then Earth is taken out of the
/// group and grows alone, and EarthSummit_NEW builds the summit and hides the rest. Grown
/// together a millionfold the group's position goes out to where a float is a kilometre
/// apart (3 Nov: the ground came out above the camera).
///
/// THE GATE. Into the Earth layer where Earth has grown to gateRadius - about filling the
/// screen - so J9 is said as before. Put there as the approach begins.
///
/// THE CAMERA. Over the first part the Solar System camera comes round behind the light
/// (look input is locked) and down to lowHeight above its line; near the end the lens
/// widens by fovKick, for the rush.
///
/// THE SUN. Beyond Earth, and half as wide as Earth's orbit in this system: held to
/// sunSeenDegrees on screen as the rest grows, going down behind Earth's limb.
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

    [Header("Approach")]
    [Tooltip("How far short of Earth's centre, along the light's line, the growth starts. At the " +
             "usual speed of 2.5, every 10 more is 4 seconds more of the approach.")]
    [Min(1f)] [SerializeField] float startDistance = 45f;

    [Tooltip("How many world units a km is at the end. At 30 the light, narrowed by EarthSummit_NEW, " +
             "fits the dome's slit and the telescope's tube, and the horizon, 155 km off, is inside " +
             "the camera's far clip (5000).")]
    [Min(0.01f)] [SerializeField] float summitUnitsPerKm = 30f;

    [Tooltip("World units still to fly when Earth has reached full size: the last few seconds, " +
             "slowing, into the dome. At 30 units a km, 12 is 400 m.")]
    [Min(1f)] [SerializeField] float finalUnits = 12f;

    [Tooltip("The light's speed at the arrival, as a fraction of its own.")]
    [Range(0.05f, 1f)] [SerializeField] float finalSpeed = 0.35f;

    [Tooltip("Earth's radius, world units, at which it is taken out of the group to grow alone " +
             "and the summit is built.")]
    [Min(1f)] [SerializeField] float detachRadius = 180f;

    [Tooltip("Earth's radius, world units, at which the light passes the gate into the Earth layer.")]
    [Min(0.1f)] [SerializeField] float gateRadius = 15f;

    [Tooltip("The camera's height over the ground at the view from outside, km, for EarthSummit_NEW; " +
             "Mount Wilson's summit is 1.74.")]
    [Min(0f)] [SerializeField] float summitCameraKm = 1.8f;

    [Tooltip("The Sun's radius on screen, in degrees, as Earth grows. 0 lets it grow with the rest.")]
    [Range(0f, 30f)] [SerializeField] float sunSeenDegrees = 3f;

    [Header("Camera")]
    [SerializeField] bool driveCamera = true;

    [Tooltip("By how far through the approach the camera is behind and down, 0 to 1.")]
    [Range(0.05f, 1f)] [SerializeField] float settleBy = 0.3f;

    [Tooltip("The camera's height above the light's line once down. Not 0: from dead behind " +
             "the trail is end on, a dot.")]
    [SerializeField] float lowHeight = 0.5f;

    [Tooltip("Degrees the lens widens by, for the rush.")]
    [Range(0f, 30f)] [SerializeField] float fovKick = 10f;

    [Tooltip("How far through the approach the lens starts to widen, 0 to 1.")]
    [Range(0f, 1f)] [SerializeField] float kickFrom = 0.4f;

    [Tooltip("How close the camera's orbit comes in over the last few seconds, as a fraction, " +
             "so it follows the light in through the slit.")]
    [Range(0.02f, 1f)] [SerializeField] float endCameraScale = 0.1f;

    [SerializeField] bool debugLog = false;

    /// <summary>Earth's mean radius, km: what one radius of the sphere stands for.</summary>
    public const float EarthRadiusKm = 6371f;

    CelestialBody_NEW _body;
    EarthSummit_NEW _summit;
    LayerDive_NEW _dive;
    bool _zooming, _done, _warned, _detached;
    float _r0, _d0, _lnK, _kDetach, _k = 1f, _u;
    Vector3 _dir, _rootFrom, _scaleFrom, _scaleSet, _earthAtDetach, _earthScaleAtDetach;
    Vector3 _sunScaleFrom;
    float _sunRadiusFrom, _sunSeenFrom;
    float _xFrom, _yFrom, _fovFrom;
    readonly float[] _orbitRadius = new float[3], _orbitHeight = new float[3];

    // ── For EarthSummit_NEW, EarthEnding_NEW and the debug jump ─────────────

    /// <summary>Earth has been taken out of the group: the summit is to be built.</summary>
    public bool SummitStarted => _detached;

    /// <summary>0 until the last few seconds, then to 1 as the light reaches the arrival.</summary>
    public float FinalProgress { get; private set; }

    /// <summary>The light is at the arrival, held.</summary>
    public bool Arrived => _done && _zooming;

    /// <summary>Where the light ends - the telescope's mirror - world space. It does not move.</summary>
    public Vector3 ArrivalPoint { get; private set; }

    /// <summary>The ground, at sea level, straight under the arrival, world space.</summary>
    public Vector3 GroundPoint => ArrivalPoint - GroundUp * (_summit != null ? _summit.ArrivalHeightKm : 0f) * KmToWorld;

    /// <summary>Up at the arrival, world space.</summary>
    public Vector3 GroundUp { get; private set; }

    /// <summary>World units a km, now.</summary>
    public float KmToWorld => _body != null ? _body.GroundRadius / EarthRadiusKm : 1f;

    /// <summary>The camera's height over the ground at the view from outside, km.</summary>
    public float SummitCameraKm => summitCameraKm;

    public Transform Earth => earth;
    public CelestialBody_NEW Body => _body;
    public PlayerRig_NEW Photon => photon;

    /// <summary>The layer the light goes into at the gate: Earth.</summary>
    public string EarthLayerId => earthLayerId;

    /// <summary>Earth has begun to grow: the last stretch before the light goes in (JourneyScore_NEW plays its rise on it).</summary>
    public static event System.Action ZoomBegan;

    /// <summary>
    /// For LayerDebugJump_NEW: where to put the light so the whole approach to Earth plays
    /// as in a run - on its line through Earth's centre, <paramref name="margin"/> before the
    /// growth begins. False once the growth has begun: it cannot be played twice.
    /// </summary>
    public bool TryGetApproachStart(float margin, out Vector3 at)
    {
        at = default;
        if (photon == null || earth == null || _zooming) return false;
        at = earth.position - photon.MovementDirection.normalized * (startDistance + margin);
        return true;
    }

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
        _summit = GetComponent<EarthSummit_NEW>();
        _dive = FindObjectOfType<LayerDive_NEW>();
    }

    void Update()
    {
        if (_done) return;

        // The dive into the Solar System places and scales this root itself every frame, and
        // puts it back as built at the end. Nothing is measured or moved until it is over.
        if (_dive != null && _dive.IsActive) return;

        if (_zooming && !_detached && !_warned && (transform.localScale - _scaleSet).sqrMagnitude > 1e-8f && (_warned = true))
            Debug.LogWarning("[EarthZoom_NEW] Something else set '" + name + "''s scale to " + transform.localScale.x +
                             " (this set " + _scaleSet.x + ").", this);

        if (!_zooming)
        {
            float toCentre = Vector3.Dot(earth.position - photon.transform.position, photon.MovementDirection.normalized);
            if (toCentre > startDistance || toCentre <= 0f) return;
            Begin();
        }

        // How far the light has come towards the arrival; the growth goes with it.
        float d = Vector3.Dot(ArrivalPoint - photon.transform.position, _dir);
        _u = Mathf.Clamp01((_d0 - d) / Mathf.Max(_d0 - finalUnits, 1e-3f));
        _k = Mathf.Exp(_lnK * Mathf.SmoothStep(0f, 1f, _u));
        Grow(_k);

        // The last few seconds: slower, and the camera closing in.
        FinalProgress = Mathf.Clamp01(1f - d / finalUnits);
        float target = Mathf.Lerp(1f, finalSpeed, Mathf.SmoothStep(0f, 1f, FinalProgress));
        photon.SetApproachSpeedMultiplier(target / Mathf.Max(photon.ExternalSpeedMultiplier, 0.05f));

        if (driveCamera && solarCamera != null) Frame(_u, FinalProgress);

        if (debugLog && Time.frameCount % 30 == 0)
            Debug.Log("[EarthZoom_NEW] " + d.ToString("0.00") + " to go, growth x" + _k.ToString("0") +
                      ", a km " + KmToWorld.ToString("0.000") + " units.", this);

        if (d <= 0.01f)
        {
            photon.SetHeld(true);
            photon.transform.position = ArrivalPoint;
            photon.SetApproachSpeedMultiplier(1f);
            _done = true;
            if (debugLog) Debug.Log("[EarthZoom_NEW] Arrived.", this);
        }
    }

    void Begin()
    {
        _zooming = true;
        if (ZoomBegan != null) ZoomBegan();

        _dir = photon.MovementDirection.normalized;
        _r0 = Mathf.Max(_body != null ? _body.GroundRadius : 0.5f * earth.lossyScale.x, 1e-6f);

        // The arrival: where the light's line meets Earth, raised by the mirror's height.
        float rise = _summit != null ? _summit.ArrivalHeightKm : 0f;
        float ra = _r0 * (1f + rise / EarthRadiusKm);
        Vector3 oc = photon.transform.position - earth.position;
        float b = Vector3.Dot(oc, _dir), c = Vector3.Dot(oc, oc) - ra * ra, disc = b * b - c;
        Vector3 arrival = disc > 0f ? photon.transform.position + _dir * (-b - Mathf.Sqrt(disc)) : earth.position - _dir * ra;
        ArrivalPoint = arrival;
        GroundUp = (arrival - earth.position).normalized;
        _d0 = Vector3.Dot(arrival - photon.transform.position, _dir);
        if (_d0 <= finalUnits * 1.5f)
            Debug.LogWarning("[EarthZoom_NEW] The approach begins only " + _d0.ToString("0.0") + " units out; raise startDistance.", this);

        _lnK = Mathf.Log(Mathf.Max(summitUnitsPerKm / (_r0 / EarthRadiusKm), 1f));
        _kDetach = Mathf.Max(1f, detachRadius / _r0);

        _rootFrom = transform.position;
        _scaleFrom = transform.localScale;
        _scaleSet = _scaleFrom;

        PlaceGate();

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
            _fovFrom = solarCamera.m_Lens.FieldOfView;
            for (int i = 0; i < 3; i++)
            {
                _orbitRadius[i] = solarCamera.m_Orbits[i].m_Radius;
                _orbitHeight[i] = solarCamera.m_Orbits[i].m_Height;
            }
        }

        Debug.Log("[EarthZoom_NEW] The approach begins: " + _d0.ToString("0.0") + " units to fly, Earth to grow x" +
                  Mathf.Exp(_lnK).ToString("0") + "; the arrival is " + rise.ToString("0.000") + " km up.", this);
    }

    /// <summary>The gate where Earth has grown to gateRadius: the light's distance there, from the growth's curve.</summary>
    void PlaceGate()
    {
        if (earthGate == null) return;
        float g = Mathf.Clamp01(Mathf.Log(Mathf.Max(gateRadius / _r0, 1f)) / Mathf.Max(_lnK, 1e-3f));
        // Invert the eased curve by halving.
        float lo = 0f, hi = 1f;
        for (int i = 0; i < 30; i++) { float m = (lo + hi) * 0.5f; if (Mathf.SmoothStep(0f, 1f, m) < g) lo = m; else hi = m; }
        float d = _d0 - lo * (_d0 - finalUnits);
        Vector3 at = ArrivalPoint - _dir * d;
        Vector3 p = earthGate.transform.position;
        // Only along the line, as EarthApproach_NEW: the gate is a wide plane across it.
        earthGate.transform.position = p + _dir * Vector3.Dot(at - p, _dir);
    }

    /// <summary>Grows the world k times about the arrival: the whole group, then Earth alone.</summary>
    void Grow(float k)
    {
        if (!_detached && k < _kDetach)
        {
            transform.localScale = _scaleFrom * k;
            transform.position = ArrivalPoint + (_rootFrom - ArrivalPoint) * k;
            _scaleSet = transform.localScale;
            HoldSun(k);
            return;
        }

        if (!_detached)
        {
            // To the size of the parting, then Earth on its own.
            transform.localScale = _scaleFrom * _kDetach;
            transform.position = ArrivalPoint + (_rootFrom - ArrivalPoint) * _kDetach;
            _scaleSet = transform.localScale;
            HoldSun(_kDetach);
            earth.SetParent(null, true);
            _earthAtDetach = earth.position;
            _earthScaleAtDetach = earth.localScale;
            _detached = true;
            if (debugLog) Debug.Log("[EarthZoom_NEW] Earth grows alone from here: radius " + KmToWorld * EarthRadiusKm + ".", this);
        }

        float alone = k / _kDetach;
        earth.localScale = _earthScaleAtDetach * alone;
        earth.position = ArrivalPoint + (_earthAtDetach - ArrivalPoint) * alone;
    }

    /// <summary>Holds the Sun, grown s times with the rest, to the size on screen it should have now.</summary>
    void HoldSun(float s)
    {
        if (sun == null || sunSeenDegrees <= 0f) return;

        Camera cam = GetReal3DView_NEW.ScreenCamera;
        if (cam == null) return;

        float seen = Mathf.Lerp(_sunSeenFrom, sunSeenDegrees, Mathf.SmoothStep(0f, 1f, _u * 3f));
        float wanted = Mathf.Tan(seen * Mathf.Deg2Rad) * Vector3.Distance(sun.position, cam.transform.position);
        sun.localScale = _sunScaleFrom * Mathf.Min(1f, wanted / (_sunRadiusFrom * s));
    }

    void Frame(float u, float last)
    {
        float settle = Mathf.SmoothStep(0f, 1f, u / settleBy);
        solarCamera.m_XAxis.Value = Mathf.LerpAngle(_xFrom, 0f, settle);
        solarCamera.m_YAxis.Value = Mathf.Lerp(_yFrom, 0.5f, settle);

        // In close over the last few seconds, through the slit after the light.
        float close = Mathf.Lerp(1f, endCameraScale, Mathf.SmoothStep(0f, 1f, last));
        for (int i = 0; i < 3; i++)
        {
            float height = i == 1 ? Mathf.Lerp(_orbitHeight[1], lowHeight, settle) : _orbitHeight[i];
            solarCamera.m_Orbits[i].m_Height = height * close;
            solarCamera.m_Orbits[i].m_Radius = _orbitRadius[i] * close;
        }

        float kick = kickFrom < 1f ? Mathf.SmoothStep(0f, 1f, (u - kickFrom) / (1f - kickFrom)) : 0f;
        solarCamera.m_Lens.FieldOfView = _fovFrom + fovKick * kick;
    }

    void OnDrawGizmos()
    {
        if (!DebugView_NEW.Gizmos || earth == null) return;

        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.2f);
        Gizmos.DrawWireSphere(earth.position, startDistance);
        if (_zooming)
        {
            Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.8f);
            Gizmos.DrawWireSphere(ArrivalPoint, 0.2f);
        }
    }
}

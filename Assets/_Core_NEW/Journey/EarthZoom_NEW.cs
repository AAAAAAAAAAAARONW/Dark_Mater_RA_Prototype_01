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
    bool _zooming, _done, _warned;
    float _u, _from, _r0, _radius, _logSeenFrom, _logSeenTo;
    Vector3 _rootFrom, _scaleFrom, _scaleSet, _centre;
    float _xFrom, _yFrom, _heightFrom, _fovFrom;

    /// <summary>Distance from Earth's centre, along the line, at which the light reaches the gate.</summary>
    float GateDistance => finalRadius * (1f + (_body != null ? _body.AirDepth : 0f) * gateInAir);

    /// <summary>Distance from Earth's centre at which Earth is full size.</summary>
    float ZoomEnd => GateDistance + holdDistance;

    void Awake()
    {
        if (photon == null) photon = FindObjectOfType<PlayerRig_NEW>();
        if (earth == null) earth = transform.Find("Earth");
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
        if (_done) return;

        // The dive into the Solar System places and scales this root itself every frame, and
        // puts it back as built at the end. Nothing is measured or moved until it is over.
        if (_dive != null && _dive.IsActive) return;

        if (_zooming && !_warned && (transform.localScale - _scaleSet).sqrMagnitude > 1e-8f && (_warned = true))
            Debug.LogWarning("[EarthZoom_NEW] Something else set '" + name + "''s scale to " + transform.localScale.x +
                             " (this set " + _scaleSet.x + ").", this);

        Vector3 dir = photon.MovementDirection;
        float distance = Vector3.Dot(earth.position - photon.transform.position, dir);

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

    /// <summary>How far behind the light, along its line, the camera is.</summary>
    float CameraBack()
    {
        Camera cam = Camera.main;
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

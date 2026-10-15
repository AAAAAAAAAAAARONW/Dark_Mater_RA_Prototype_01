using UnityEngine;

/// <summary>
/// The quasar's pull on the light, as the scientists described where it comes from: not out
/// of the black hole — nothing comes out of that — but off the disc of gas round it. So the
/// light is caught, drawn down onto the disc, carried once round the black hole with the gas,
/// and flung off it from the side.
///
///   Capture   (C1) From wherever the light has got to, a spiral in: falling towards the
///             quasar, slowly at first and then faster, as distance goes — evenly in how fast
///             the quasar grows — while it swings round in the way the disc turns and comes
///             down to just above it. It ends going round with the gas, at the disc's own pace.
///   Orbit     (C2) Round and round the black hole above the disc, until A.
///   Launch    (C3) Off the disc from wherever the light has got to: along its orbit, out of the
///             disc's plane — the way light leaves a disc. TutorialEmission_NEW sets the course
///             and the speed from what this returns; this turns the view to face it.
///
/// Travel still owns the transform: this is a path it follows (TutorialTravel_NEW.Carry). The
/// view is carried round with the light (FirstPersonLookRig_NEW.CarryView) — a player looking
/// at the core keeps looking at it while being swung round it — and A recentres on the core,
/// a little ahead of it, which is what "forward" is while circling it.
///
/// The disc is the quasar's (QuasarVFX_NEW): its centre, its plane (its transform's XZ, as it
/// draws it) and its radius, and the way its gas goes round (atan2 of local z and x, rising),
/// read off it every frame — so it can be moved, tilted or rescaled and the orbit follows.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("DISC ORBIT", "#E0A050")]
public class TutorialDiscOrbit_NEW : MonoBehaviour, TutorialTravel_NEW.IPath
{
    [Header("Wiring")]
    [Tooltip("The quasar. Its QuasarVFX_NEW (on it or a child) gives the disc. Empty: the travel's destination.")]
    [SerializeField] Transform quasar;
    [SerializeField] TutorialTravel_NEW travel;
    [SerializeField] FirstPersonLookRig_NEW lookRig;

    [Header("The orbit (in disc radii)")]
    [Tooltip("How far from the black hole the light goes round, in disc radii: out on the disc, where it " +
             "is gas, yellow and orange, with the white-hot middle and the speck of the hole beside it.")]
    [Range(0.15f, 1.2f)] [SerializeField] float orbitRadius = 0.55f;

    [Tooltip("How high above the disc, in disc radii: low enough that it spreads out below like a floor " +
             "of light, high enough to see across it.")]
    [Range(0.01f, 0.5f)] [SerializeField] float orbitHeight = 0.1f;

    [Tooltip("Seconds for one turn round the black hole.")]
    [Min(1f)] [SerializeField] float secondsPerTurn = 8f;

    [Tooltip("How far ahead of the core, in degrees round the orbit, the view is carried to look — and " +
             "where A recentres. 0 straight at the core.")]
    [Range(0f, 60f)] [SerializeField] float leadAngle = 15f;

    [Header("Launch")]
    [Tooltip("How steeply the light leaves the disc, in degrees out of its plane: along the orbit and up " +
             "away from the disc. Steeper, the galaxy round it is seen more face on when the light looks back.")]
    [Range(0f, 80f)] [SerializeField] float launchElevation = 38f;

    [Tooltip("Seconds over which the view turns, at the launch, from the core to the way the light is " +
             "flung — as a body turns with what throws it.")]
    [Min(0.05f)] [SerializeField] float launchTurnSeconds = 1.4f;

    [Header("Debug")]
    [SerializeField] bool drawGizmo = true;

    enum Phase { Idle, Capture, Orbit, Launched }

    Phase _phase = Phase.Idle;
    float _elapsed, _captureSeconds;
    float _rho0, _theta0, _phi0, _sweep;
    float _phi;
    Quaternion _frame;
    bool _hasFrame;

    // The turn to the launch course, carried in over launchTurnSeconds.
    Quaternion _turn = Quaternion.identity;
    float _turnElapsed = -1f;
    float _turnDone;

    QuasarVFX_NEW _vfx;

    /// <summary>True from the capture until the launch.</summary>
    public bool IsCarrying { get { return _phase == Phase.Capture || _phase == Phase.Orbit; } }

    /// <summary>True once the capture is over and the light is going round.</summary>
    public bool IsOrbiting { get { return _phase == Phase.Orbit; } }

    /// <summary>The speed going round, in world units a second.</summary>
    public float OrbitSpeed { get { return Mathf.PI * 2f / secondsPerTurn * OrbitRadiusWorld; } }

    float Turn => Mathf.PI * 2f / Mathf.Max(0.1f, secondsPerTurn);

    void Awake()
    {
        if (travel == null) travel = GetComponentInParent<TutorialTravel_NEW>();
        if (quasar == null && travel != null) quasar = travel.Destination;
        if (lookRig == null) lookRig = FindObjectOfType<FirstPersonLookRig_NEW>();
    }

    // ── The disc ────────────────────────────────────────────────────────────

    Transform Disc
    {
        get
        {
            if (_vfx == null && quasar != null) _vfx = quasar.GetComponentInChildren<QuasarVFX_NEW>();
            return _vfx != null ? _vfx.transform : quasar;
        }
    }

    float DiscRadius
    {
        get
        {
            if (_vfx == null && quasar != null) _vfx = quasar.GetComponentInChildren<QuasarVFX_NEW>();
            if (_vfx != null) return _vfx.Radius;
            return quasar != null ? quasar.lossyScale.x * 0.5f : 1f;
        }
    }

    float OrbitRadiusWorld => orbitRadius * DiscRadius;

    /// <summary>Where the orbit is, as a distance from the middle and a height angle.</summary>
    float OrbitRho => DiscRadius * Mathf.Sqrt(orbitRadius * orbitRadius + orbitHeight * orbitHeight);
    float OrbitTheta => Mathf.Atan2(orbitHeight, orbitRadius);

    /// <summary>A point given its distance from the middle, its angle above the disc and its angle round it.</summary>
    Vector3 Point(float rho, float theta, float phi)
    {
        Transform d = Disc;
        float c = Mathf.Cos(theta);
        Vector3 round = (Mathf.Cos(phi) * d.right + Mathf.Sin(phi) * d.forward) * c;
        return d.position + (round + Mathf.Sin(theta) * d.up) * rho;
    }

    /// <summary>The way round the disc its gas goes, at angle phi: phi rising.</summary>
    Vector3 Along(float phi)
    {
        Transform d = Disc;
        return -Mathf.Sin(phi) * d.right + Mathf.Cos(phi) * d.forward;
    }

    // ── The three calls ─────────────────────────────────────────────────────

    /// <summary>
    /// C1: catch the light where it is and draw it down onto the disc over this many seconds.
    /// False if there is no quasar to be caught by (the emission then arrives the old way).
    /// </summary>
    public bool Capture(float seconds)
    {
        if (quasar == null || travel == null) return false;

        Transform d = Disc;
        Vector3 from = travel.transform.position - d.position;
        float rho = from.magnitude;
        if (rho < 1e-3f) return false;

        _rho0 = Mathf.Max(rho, OrbitRho * 1.01f);
        float up = Vector3.Dot(from, d.up);
        _theta0 = Mathf.Asin(Mathf.Clamp(up / rho, -1f, 1f));
        _phi0 = Mathf.Atan2(Vector3.Dot(from, d.forward), Vector3.Dot(from, d.right));

        // Swinging round faster as it falls in (as s³), to end at the disc's own pace.
        _captureSeconds = Mathf.Max(0.1f, seconds);
        _sweep = Turn * _captureSeconds / 3f;

        _phase = Phase.Capture;
        _elapsed = 0f;
        _phi = _phi0;
        _hasFrame = false;
        _turnElapsed = -1f;

        travel.Carry(this);
        return true;
    }

    /// <summary>
    /// C3: off the disc. Where the light leaves from — the orbit's entry, if the capture had not
    /// finished (a debug jump replays C1 to C3 in one frame) — and the way it goes: along the
    /// orbit and up out of the disc's plane. The view turns to face it over launchTurnSeconds.
    /// </summary>
    public Vector3 Launch(out Vector3 from)
    {
        if (_phase == Phase.Capture)
        {
            _phi = _phi0 + _sweep;
            _phase = Phase.Orbit;
        }

        from = Point(OrbitRho, OrbitTheta, _phi);
        float e = launchElevation * Mathf.Deg2Rad;
        Vector3 course = (Mathf.Cos(e) * Along(_phi) + Mathf.Sin(e) * Disc.up).normalized;

        // The view: from where it is carried to now (the core, a little ahead), to the course.
        Vector3 up = Disc.up;
        Quaternion before = _hasFrame ? _frame : Quaternion.LookRotation(Forward(from, 1f), up);
        _turn = Quaternion.LookRotation(course, up) * Quaternion.Inverse(before);
        _turnElapsed = 0f;
        _turnDone = 0f;

        _phase = Phase.Launched;
        return course;
    }

    /// <summary>Back to rest. The attract reset, through the emission.</summary>
    public void Stop()
    {
        _phase = Phase.Idle;
        _turnElapsed = -1f;
        _hasFrame = false;
    }

    // ── The path ────────────────────────────────────────────────────────────

    public Vector3 Advance(float dt, out Vector3 heading)
    {
        Vector3 before = travel != null ? travel.transform.position : Vector3.zero;
        Vector3 position;

        if (_phase == Phase.Capture)
        {
            _elapsed += dt;
            float s = Mathf.Clamp01(_elapsed / _captureSeconds);

            // Distance eased on its logarithm, so the quasar grows evenly rather than leaping
            // at the end; the height angle eased down; the angle round swinging in as s³.
            float e = s * s * s * (s * (s * 6f - 15f) + 10f);
            float rho = Mathf.Exp(Mathf.Lerp(Mathf.Log(_rho0), Mathf.Log(OrbitRho), e));
            float theta = Mathf.Lerp(_theta0, OrbitTheta, s * s * (3f - 2f * s));
            _phi = _phi0 + _sweep * s * s * s;
            position = Point(rho, theta, _phi);

            if (s >= 1f) _phase = Phase.Orbit;
            CarryView(position, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1f, s)));
        }
        else
        {
            _phi += Turn * dt;
            position = Point(OrbitRho, OrbitTheta, _phi);
            CarryView(position, 1f);
        }

        heading = position - before;
        if (heading.sqrMagnitude < 1e-10f) heading = Along(_phi);
        return position;
    }

    /// <summary>Where "forward" is while circling: the core, `lead` of the way to a little ahead of it.</summary>
    Vector3 Forward(Vector3 position, float lead)
    {
        Vector3 toCore = Disc.position - position;
        float ahead = Mathf.Tan(leadAngle * Mathf.Deg2Rad * lead) * OrbitRadiusWorld;
        Vector3 f = toCore + Along(_phi) * ahead;
        return f.sqrMagnitude > 1e-8f ? f.normalized : Along(_phi);
    }

    /// <summary>Turn the view with the light, by however much its forward turned since the last frame.</summary>
    void CarryView(Vector3 position, float lead)
    {
        Vector3 forward = Forward(position, lead);
        Quaternion frame = Quaternion.LookRotation(forward, Disc.up);

        if (lookRig != null)
        {
            lookRig.SetForwardAxis(forward);
            if (_hasFrame) lookRig.CarryView(frame * Quaternion.Inverse(_frame));
        }

        _frame = frame;
        _hasFrame = true;
    }

    // ── The launch turn ─────────────────────────────────────────────────────

    void Update()
    {
        if (_turnElapsed < 0f || lookRig == null) return;

        _turnElapsed += TutorialClock_NEW.DeltaTime;
        float t = Mathf.Clamp01(_turnElapsed / launchTurnSeconds);
        float eased = t * t * (3f - 2f * t);

        // This frame's share of the turn: from as far as it has gone to as far as it should.
        Quaternion done = Quaternion.Slerp(Quaternion.identity, _turn, _turnDone);
        Quaternion now = Quaternion.Slerp(Quaternion.identity, _turn, eased);
        lookRig.CarryView(now * Quaternion.Inverse(done));
        _turnDone = eased;

        if (t >= 1f) _turnElapsed = -1f;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmo || !DebugView_NEW.Gizmos || quasar == null) return;

        Gizmos.color = new Color(1f, 0.7f, 0.3f, 0.9f);
        Vector3 last = Point(OrbitRho, OrbitTheta, 0f);
        for (int i = 1; i <= 64; i++)
        {
            Vector3 next = Point(OrbitRho, OrbitTheta, i * Mathf.PI * 2f / 64f);
            Gizmos.DrawLine(last, next);
            last = next;
        }
    }
}

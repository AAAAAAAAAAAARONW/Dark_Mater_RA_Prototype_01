using UnityEngine;

/// <summary>
/// The quasar's pull on the light, as the scientists described where it comes from: not out
/// of the black hole — nothing comes out of that — but off the disc of gas round it. So the
/// light is caught, drawn down onto the disc, carried once round the black hole with the gas,
/// and flung off it from the side.
///
///   Capture   (C1) From wherever the light has got to — close, by C1, at the cruise's hold — a
///             spiral in: falling towards the quasar as distance goes (evenly in how fast it
///             grows), swinging round the way the disc turns, slowly at first, and coming down
///             to just above it. It ends going round with the gas, at the disc's own pace.
///   Orbit     (C2) Round the black hole above the disc, until A. The view is carried round
///             with the light, the black hole held in the middle of it, so it is plainly the
///             thing being circled: the stars and the disc wheel past behind it.
///   Launch    (C3) Off the side of the disc from wherever the light has got to: along its orbit,
///             skimming out over the disc's edge, barely rising — and the view turns with it, a
///             level turn, the black hole sliding away beside it. Level, so that looking back
///             at it is one turn of the stick.
///
/// THE REVEAL. Looking back, after the launch, is the first time the galaxy is seen whole: the
/// run starts inside it. A galaxy seen from the plane of its quasar's disc can be edge on, and
/// which way the light leaves depends on when A is pressed — so at the launch, under the white
/// of the emission's flash, the galaxy (revealGalaxy) is turned on the quasar to be seen at
/// revealInclination from the way out, face on enough to see it all. Nothing near enough to
/// see it move is on screen while it does; the attract reset turns it back.
///
/// Travel still owns the transform: this is a path it follows (TutorialTravel_NEW.Carry). The
/// view is carried round with the light (FirstPersonLookRig_NEW.CarryView) — a player looking
/// at the black hole keeps looking at it while being swung round it — and A recentres on it
/// (leadAngle ahead of it, if set), which is what "forward" is while circling it.
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
    [Min(1f)] [SerializeField] float secondsPerTurn = 10f;

    [Tooltip("How far ahead of the core, in degrees round the orbit, the view is carried to look — and " +
             "where A recentres. 0 straight at the black hole.")]
    [Range(0f, 60f)] [SerializeField] float leadAngle = 0f;

    [Header("Launch")]
    [Tooltip("How steeply the light leaves the disc, in degrees out of its plane: along the orbit, skimming " +
             "over the disc's edge. Low, so that looking back at the quasar is a level turn of the stick.")]
    [Range(0f, 80f)] [SerializeField] float launchElevation = 4f;

    [Tooltip("Seconds over which the view turns, at the launch, from the black hole to the way the light " +
             "is flung — level, as a body turns with what throws it, the black hole sliding away beside it.")]
    [Min(0.05f)] [SerializeField] float launchTurnSeconds = 1.8f;

    [Header("The reveal")]
    [Tooltip("The quasar's galaxy. Turned at the launch, under the emission's flash, so that looking back " +
             "from the way out it is seen at revealInclination — whole, the quasar at its heart. Empty: left as it is.")]
    [SerializeField] Transform revealGalaxy;

    [Tooltip("How far from face on the galaxy is seen looking back, in degrees: 0 face on, 90 edge on.")]
    [Range(0f, 85f)] [SerializeField] float revealInclination = 45f;

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

    // The galaxy as it was built, to put back on a reset.
    Quaternion _galaxyRotation;
    Vector3 _galaxyPosition;
    bool _galaxyTurned;

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
        if (revealGalaxy != null)
        {
            _galaxyRotation = revealGalaxy.rotation;
            _galaxyPosition = revealGalaxy.position;
        }
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

        // Swinging round faster as it falls in (as s²), to end at the disc's own pace.
        _captureSeconds = Mathf.Max(0.1f, seconds);
        _sweep = Turn * _captureSeconds / 2f;

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
    /// orbit, rising launchElevation out of the disc's plane. The view turns, level, to face the
    /// way out over launchTurnSeconds, and the galaxy is turned for the reveal (see the class).
    /// </summary>
    public Vector3 Launch(out Vector3 from)
    {
        if (_phase == Phase.Capture)
        {
            _phi = _phi0 + _sweep;
            _phase = Phase.Orbit;
        }

        from = Point(OrbitRho, OrbitTheta, _phi);
        Vector3 up = Disc.up;
        float e = launchElevation * Mathf.Deg2Rad;
        Vector3 course = (Mathf.Cos(e) * Along(_phi) + Mathf.Sin(e) * up).normalized;

        // The view: from the black hole, where it is carried to now, round to the way out —
        // level, so the turn is the one the stick makes, and so is the turn back.
        _level = Along(_phi);
        Quaternion before = _hasFrame ? _frame : Quaternion.LookRotation(Forward(from, 1f), up);
        _turn = Quaternion.LookRotation(_level, up) * Quaternion.Inverse(before);
        _turnElapsed = 0f;
        _turnDone = 0f;

        TurnGalaxyToFace(course);

        _phase = Phase.Launched;
        return course;
    }

    Vector3 _level;

    /// <summary>
    /// THE REVEAL (see the class): turn the galaxy on the quasar so that from the way out it is
    /// seen at revealInclination. Its new pole leans from the level towards the way out.
    /// </summary>
    void TurnGalaxyToFace(Vector3 course)
    {
        if (revealGalaxy == null) return;

        Vector3 up = Disc.up;
        Vector3 level = up - Vector3.Dot(up, course) * course;
        if (level.sqrMagnitude < 1e-6f) return;
        level.Normalize();

        float i = revealInclination * Mathf.Deg2Rad;
        Vector3 pole = Mathf.Cos(i) * course + Mathf.Sin(i) * level;

        Vector3 pivot = Disc.position;
        Quaternion turn = Quaternion.FromToRotation(revealGalaxy.up, pole);
        revealGalaxy.position = pivot + turn * (revealGalaxy.position - pivot);
        revealGalaxy.rotation = turn * revealGalaxy.rotation;
        _galaxyTurned = true;
    }

    /// <summary>Back to rest. The attract reset, through the emission.</summary>
    public void Stop()
    {
        _phase = Phase.Idle;
        _turnElapsed = -1f;
        _hasFrame = false;

        if (_galaxyTurned && revealGalaxy != null)
        {
            revealGalaxy.rotation = _galaxyRotation;
            revealGalaxy.position = _galaxyPosition;
            _galaxyTurned = false;
        }
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
            // at the end; the height angle eased down; the angle round swinging in as s².
            float e = s * s * s * (s * (s * 6f - 15f) + 10f);
            float rho = Mathf.Exp(Mathf.Lerp(Mathf.Log(_rho0), Mathf.Log(OrbitRho), e));
            float theta = Mathf.Lerp(_theta0, OrbitTheta, s * s * (3f - 2f * s));
            _phi = _phi0 + _sweep * s * s;
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

        // Forward is the way out, level: A recentres there, and the turn back is the same turn.
        lookRig.SetForwardAxis(_level);

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

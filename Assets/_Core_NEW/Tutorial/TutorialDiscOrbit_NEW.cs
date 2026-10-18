using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The quasar's pull on the light, as the scientists described where it comes from: not out
/// of the black hole — nothing comes out of that — but off the white-hot gas right beside it.
/// So the light is caught, carried once round the black hole, and set down at the hole's
/// edge, the brightest point there is; it waits; and when A lets it go it leaves, and the
/// camera follows it out.
///
///   Capture   (C1) From wherever the light has got to — close, by C1, at the cruise's hold —
///             a spiral in: once round the black hole, the way the disc turns, falling as
///             distance goes (evenly in how fast it grows) and coming down onto the disc, to
///             stop at the bright inner edge beside the hole. The view is carried round with
///             it, the black hole held in the middle; over the last two thirds of it the camera
///             lets the light go on ahead and stays back to watch it, a little way off and above,
///             so the black hole stays a small dot.
///   Wait      (C2) The light at the black hole's edge, a point brighter than the gas round it
///             (TutorialPhotonGlow_NEW), pulsing; the black hole a small dark dot beside it.
///   Depart    (C3) A lets it go: along the gas's own way round there — light from the disc is
///             beamed the way its gas is going — across the disc and out, rising a little. The light goes
///             first, slowly at first; the camera holds a moment, then follows, coming down
///             behind it, and is with it by chaseSeconds. Nothing turns: the view only rises to
///             level as it closes in. No flash: the light is the bright thing on the screen.
///             TutorialEmission_NEW drives the speed — departSpeed until the camera is with the
///             light, then up to the tunnel's.
///
/// Travel owns the transform: this is a path it follows (TutorialTravel_NEW.Carry). The camera
/// is held back by FirstPersonLookRig_NEW.SetLag, the view carried by CarryView, never locked.
///
/// The way out is fixed by where the light comes in — once round from the hold, it waits on
/// the side it came in on — so the galaxy can be set in the scene to be seen whole, at a slant,
/// looking back along it (see TutorialSceneBuilder_NEW.BuildHostGalaxy).
///
/// The disc is the quasar's (QuasarVFX_NEW): its centre, its plane (its transform's XZ) and its
/// radius, and the way its gas goes round (atan2 of local z and x, rising), read every frame.
/// </summary>
[DefaultExecutionOrder(100)]   // after the travel has moved the light this frame
[DisallowMultipleComponent]
[HierarchyBadge_NEW("DISC ORBIT", "#E0A050")]
public class TutorialDiscOrbit_NEW : MonoBehaviour, TutorialTravel_NEW.IPath
{
    [Header("Wiring")]
    [Tooltip("The quasar. Its QuasarVFX_NEW (on it or a child) gives the disc. Empty: the travel's destination.")]
    [SerializeField] Transform quasar;
    [SerializeField] TutorialTravel_NEW travel;
    [SerializeField] FirstPersonLookRig_NEW lookRig;

    [Tooltip("The light, seen: what the camera watches wait and follows out.")]
    [SerializeField] TutorialPhotonGlow_NEW glow;

    [Header("Where the light waits (in disc radii)")]
    [Tooltip("How far from the middle: at the disc's bright inner edge, just outside the black hole.")]
    [Range(0.005f, 0.5f)] [SerializeField] float waitRadius = 0.03f;

    [Tooltip("How high above the disc.")]
    [Range(0f, 0.1f)] [SerializeField] float waitHeight = 0.004f;

    [Tooltip("How many times round the black hole on the way in.")]
    [Range(0f, 3f)] [SerializeField] float turns = 1f;

    [Header("Watching it wait")]
    [Tooltip("How far back along its way out the camera stays, in disc radii: far enough that the black " +
             "hole is a small dot beside the light.")]
    [Range(0.02f, 1f)] [SerializeField] float watchDistance = 0.3f;

    [Tooltip("How far above it, in degrees: the camera passes over the black hole, not past its side, " +
             "as it follows the light out.")]
    [Range(0f, 60f)] [SerializeField] float watchElevation = 25f;

    [Header("The departure")]
    [Tooltip("How steeply the light leaves, in degrees out of the disc's plane. As steep as the camera is " +
             "tilted down from the look (FirstPersonLookRig_NEW's followTilt, 12), so that looking back later " +
             "is one turn of the stick, and puts the quasar in the middle of the view.")]
    [Range(0f, 45f)] [SerializeField] float launchElevation = 12f;

    [Tooltip("The light's speed while the camera catches it up, units a second.")]
    [Min(1f)] [SerializeField] float departSpeed = 300f;

    [Tooltip("Seconds the light takes to come up to departSpeed from rest.")]
    [Min(0.05f)] [SerializeField] float departRiseSeconds = 1.4f;

    [Tooltip("Seconds the camera holds before following: it sees the light go.")]
    [Min(0f)] [SerializeField] float chaseDelay = 0.5f;

    [Tooltip("Seconds from A until the camera is with the light; then the speed goes up to the tunnel's.")]
    [Min(0.2f)] [SerializeField] float chaseSeconds = 2.6f;

    [Tooltip("Seconds after the camera is with the light before its glow fades, the trail having taken over, and over how long.")]
    [SerializeField] Vector2 glowFade = new Vector2(1f, 2.5f);

    [Header("Debug")]
    [SerializeField] bool drawGizmo = true;

    enum Phase { Idle, Capture, Wait, Depart, Done }

    Phase _phase = Phase.Idle;
    float _elapsed, _captureSeconds;
    float _rho0, _theta0, _phi0, _sweep, _phiWait;
    Vector3 _waitPoint, _level, _course, _lagTarget, _watchForward;
    Quaternion _frame;
    bool _hasFrame;

    // The light's way out, recorded, so the camera can be where it was a moment ago.
    readonly List<Vector4> _history = new List<Vector4>();
    float _departT;
    bool _glowShown;

    QuasarVFX_NEW _vfx;

    /// <summary>True from the capture until A lets the light go.</summary>
    public bool IsCarrying { get { return _phase == Phase.Capture || _phase == Phase.Wait; } }

    /// <summary>True while the light waits at the black hole's edge.</summary>
    public bool IsWaiting { get { return _phase == Phase.Wait; } }

    public float DepartSpeed { get { return departSpeed; } }
    public float DepartRiseSeconds { get { return departRiseSeconds; } }
    public float ChaseSeconds { get { return chaseSeconds; } }

    void Awake()
    {
        if (travel == null) travel = GetComponentInParent<TutorialTravel_NEW>();
        if (quasar == null && travel != null) quasar = travel.Destination;
        if (lookRig == null) lookRig = FindObjectOfType<FirstPersonLookRig_NEW>();
        if (glow == null) glow = GetComponentInChildren<TutorialPhotonGlow_NEW>(true);
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

    float WaitRho => DiscRadius * Mathf.Sqrt(waitRadius * waitRadius + waitHeight * waitHeight);
    float WaitTheta => Mathf.Atan2(waitHeight, waitRadius);

    /// <summary>A point given its distance from the middle, its angle above the disc and its angle round it.</summary>
    Vector3 Point(float rho, float theta, float phi)
    {
        Transform d = Disc;
        Vector3 round = (Mathf.Cos(phi) * d.right + Mathf.Sin(phi) * d.forward) * Mathf.Cos(theta);
        return d.position + (round + Mathf.Sin(theta) * d.up) * rho;
    }

    /// <summary>The way round the disc its gas goes, at angle phi: phi rising.</summary>
    Vector3 Along(float phi)
    {
        Transform d = Disc;
        return -Mathf.Sin(phi) * d.right + Mathf.Cos(phi) * d.forward;
    }

    static float Smooth(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    // ── The three calls ─────────────────────────────────────────────────────

    /// <summary>
    /// C1: catch the light where it is and carry it, once round, down to the black hole's
    /// edge over this many seconds. False if there is no quasar to be caught by (the emission
    /// then arrives the old way).
    /// </summary>
    public bool Capture(float seconds)
    {
        if (quasar == null || travel == null) return false;

        Transform d = Disc;
        Vector3 from = travel.transform.position - d.position;
        float rho = from.magnitude;
        if (rho < 1e-3f) return false;

        _rho0 = Mathf.Max(rho, WaitRho * 1.01f);
        _theta0 = Mathf.Asin(Mathf.Clamp(Vector3.Dot(from, d.up) / rho, -1f, 1f));
        _phi0 = Mathf.Atan2(Vector3.Dot(from, d.forward), Vector3.Dot(from, d.right));

        // Round `turns` times, to wait on the side it came in on.
        _sweep = Mathf.PI * 2f * turns;
        _phiWait = _phi0 + _sweep;
        _captureSeconds = Mathf.Max(0.1f, seconds);
        _elapsed = 0f;

        // Where it waits, the way out from there, and where the camera watches from.
        Vector3 up = d.up;
        _waitPoint = Point(WaitRho, WaitTheta, _phiWait);
        _level = Along(_phiWait);
        float e = launchElevation * Mathf.Deg2Rad;
        _course = (Mathf.Cos(e) * _level + Mathf.Sin(e) * up).normalized;

        float back = watchDistance * DiscRadius;
        float rise = back * Mathf.Tan(watchElevation * Mathf.Deg2Rad);
        _lagTarget = _level * back - up * rise;

        // Look at the light from there: the look is the camera's tilt short of it.
        float tilt = lookRig != null ? lookRig.FollowTilt : 0f;
        float down = (watchElevation - tilt) * Mathf.Deg2Rad;
        _watchForward = (Mathf.Cos(down) * _level - Mathf.Sin(down) * up).normalized;

        _phase = Phase.Capture;
        _hasFrame = false;
        _history.Clear();
        _glowShown = false;

        travel.Carry(this);
        return true;
    }

    /// <summary>
    /// C3: let it go. Where it leaves from — the black hole's edge (put there, if a debug jump
    /// replays C1 to C3 in one frame) — and the way it goes. The light goes first; Update
    /// brings the camera after it.
    /// </summary>
    public Vector3 Launch(out Vector3 from)
    {
        if (_phase == Phase.Capture) ArriveNow();

        from = _waitPoint;
        _phase = Phase.Depart;
        _departT = 0f;
        _history.Clear();
        _history.Add(new Vector4(_waitPoint.x, _waitPoint.y, _waitPoint.z, 0f));

        if (glow != null) glow.Pulsing = false;
        return _course;
    }

    /// <summary>
    /// C2, when the capture has not finished: the light at the edge at once, waiting. A
    /// debug jump opens C1 and C2 in one frame, so the capture it started would otherwise
    /// play out under C2's prompt; in a playthrough the capture is over by then and this
    /// does nothing.
    /// </summary>
    public void SkipToWait()
    {
        if (_phase == Phase.Capture) ArriveNow();
    }

    /// <summary>Back to rest. The attract reset, through the emission.</summary>
    public void Stop()
    {
        _phase = Phase.Idle;
        _hasFrame = false;
        _history.Clear();
        if (lookRig != null) lookRig.SetLag(Vector3.zero);
        if (glow != null) glow.HideNow();
    }

    /// <summary>The end of the capture, at once: the light at the edge, the camera watching.</summary>
    void ArriveNow()
    {
        _phase = Phase.Wait;
        if (lookRig != null)
        {
            lookRig.SetForwardAxis(_watchForward);
            lookRig.SetLag(_lagTarget);
        }
        if (glow != null)
        {
            glow.Show(0f);
            glow.Pulsing = true;
        }
    }

    // ── The path ────────────────────────────────────────────────────────────

    public Vector3 Advance(float dt, out Vector3 heading)
    {
        if (_phase == Phase.Capture)
        {
            Vector3 before = travel != null ? travel.transform.position : _waitPoint;
            _elapsed += dt;
            float s = Mathf.Clamp01(_elapsed / _captureSeconds);

            // Distance eased on its logarithm, so the quasar grows evenly rather than leaping
            // at the end; the angle round and the height eased in and out: it comes to rest.
            float e = s * s * s * (s * (s * 6f - 15f) + 10f);
            float rho = Mathf.Exp(Mathf.Lerp(Mathf.Log(_rho0), Mathf.Log(WaitRho), e));
            float theta = Mathf.Lerp(_theta0, WaitTheta, Smooth(s));
            float phi = _phi0 + _sweep * e;
            Vector3 position = Point(rho, theta, phi);

            // The view carried round with the black hole in the middle; over the last of it,
            // round to watching the light from behind and above as it goes on ahead.
            float watch = Smooth((s - 0.35f) / 0.65f);
            Vector3 toCore = Disc.position - position;
            Vector3 forward = Vector3.Slerp(toCore.sqrMagnitude > 1e-8f ? toCore.normalized : _watchForward, _watchForward, watch);
            CarryView(forward);
            if (lookRig != null) lookRig.SetLag(_lagTarget * watch);

            if (glow != null && s > 0.5f && !_glowShown)
            {
                glow.Show(2.5f);
                _glowShown = true;
            }

            if (s >= 1f)
            {
                _phase = Phase.Wait;
                if (glow != null) glow.Pulsing = true;
            }

            heading = position - before;
            if (heading.sqrMagnitude < 1e-10f) heading = _level;
            return position;
        }

        // Waiting at the edge: still, watched from where the camera is.
        heading = _course;
        return _waitPoint;
    }

    /// <summary>Turn the view by however much "forward" turned since the last frame, and make it forward.</summary>
    void CarryView(Vector3 forward)
    {
        if (forward.sqrMagnitude < 1e-8f) return;
        Quaternion frame = Quaternion.LookRotation(forward, Disc.up);

        if (lookRig != null)
        {
            lookRig.SetForwardAxis(forward);
            if (_hasFrame) lookRig.CarryView(frame * Quaternion.Inverse(_frame));
        }

        _frame = frame;
        _hasFrame = true;
    }

    // ── The chase ───────────────────────────────────────────────────────────

    void Update()
    {
        if (_phase != Phase.Depart && _phase != Phase.Done) return;
        if (travel == null) return;

        float dt = TutorialClock_NEW.DeltaTime;
        _departT += dt;

        if (_phase == Phase.Depart)
        {
            Vector3 now = travel.transform.position;
            _history.Add(new Vector4(now.x, now.y, now.z, _departT));

            // The camera is where the light was `delay` ago, and comes down from watching to
            // following: the delay shrinks to nothing and the hold-back to the follow view's.
            float delayEnd = Mathf.Max(chaseDelay * 0.8f + 0.05f, chaseSeconds * 0.85f);
            float delay = chaseDelay * (1f - Smooth((_departT - chaseDelay * 0.8f) / (delayEnd - chaseDelay * 0.8f)));
            float close = Smooth((_departT - chaseDelay) / Mathf.Max(0.05f, chaseSeconds - chaseDelay));
            Vector3 was = PositionAt(_departT - delay);
            if (lookRig != null) lookRig.SetLag(now - was + _lagTarget * (1f - close));

            // And the view rises from looking down at it to level along its way.
            CarryView(Vector3.Slerp(_watchForward, _level, close));

            // Only the last second or so is ever needed.
            while (_history.Count > 2 && _history[1].w < _departT - chaseDelay - 0.5f) _history.RemoveAt(0);

            if (_departT >= chaseSeconds)
            {
                if (lookRig != null)
                {
                    lookRig.SetLag(Vector3.zero);
                    lookRig.SetForwardAxis(_level);
                }
                _history.Clear();
                _phase = Phase.Done;
            }
        }

        if (_phase == Phase.Done && _departT >= chaseSeconds + glowFade.x)
        {
            if (glow != null) glow.Hide(glowFade.y);
            _phase = Phase.Idle;
        }
    }

    /// <summary>Where the light was at time t of the departure (at the edge, before it).</summary>
    Vector3 PositionAt(float t)
    {
        if (_history.Count == 0) return _waitPoint;
        if (t <= _history[0].w) return _history[0];
        for (int i = 1; i < _history.Count; i++)
        {
            Vector4 b = _history[i];
            if (b.w < t) continue;
            Vector4 a = _history[i - 1];
            float u = b.w > a.w ? (t - a.w) / (b.w - a.w) : 1f;
            return Vector3.Lerp(a, b, u);
        }
        return _history[_history.Count - 1];
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmo || !DebugView_NEW.Gizmos || quasar == null) return;

        Gizmos.color = new Color(1f, 0.7f, 0.3f, 0.9f);
        Vector3 last = Point(WaitRho, WaitTheta, 0f);
        for (int i = 1; i <= 64; i++)
        {
            Vector3 next = Point(WaitRho, WaitTheta, i * Mathf.PI * 2f / 64f);
            Gizmos.DrawLine(last, next);
            last = next;
        }
    }
}

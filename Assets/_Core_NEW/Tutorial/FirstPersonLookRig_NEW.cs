using UnityEngine;

/// <summary>
/// First person look for the tutorial. Yaw and pitch on one transform, nothing else.
///
/// Why this is not OrbitCameraRig_NEW: that rig orbits a FreeLook around the player and
/// is third person. GDD §1 fixes the tutorial as first person throughout, with no view
/// swap. Bending the orbit rig into a first person camera would mean a zero-radius
/// orbit, which is a FreeLook fighting a Composer to stay still.
///
/// FEEL. The numbers are not chosen, they are taken from PlaytestBuild_NEW's Player, so
/// the tutorial hands over to a journey that behaves the same way. That build drives a
/// CinemachineFreeLook by writing its axes directly, so the translation into first
/// person needs care in one place — the vertical:
///
///   Horizontal   xSensitivity 100, degrees per second. Copies straight across.
///   Vertical     ySensitivity 0.2, but of a FreeLook Y axis that runs 0 to 1 across
///                the whole orbit. That rig's orbits are height 3 / 1 / -2 at radius
///                1 / 3 / 1, an arc of very roughly 130°, so 0.2 of it per second is
///                about 26 degrees per second. Reproducing 0.2 as 0.2 deg/s would be
///                unusable; reproducing it as a fresh guess is how a tutorial ends up
///                feeling nothing like the game.
///   Deadband     0.1. Copies straight across.
///   Recentre     MoveTowardsAngle at a constant speed, not an eased fixed-duration
///                lerp. The speed itself is the one number deliberately not copied —
///                see resetXSpeed.
///   FOV          40 on the FreeLook, against Unity's default 60. Set by the builder on
///                the camera; a first person view at 60 reads as a different game.
///
/// PlaytestBuild's softness comes from Cinemachine damping on the rig, which first
/// person has no equivalent for, so `smoothTime` adds it back explicitly. Set it to 0
/// for raw input.
///
/// A IS BOUND HERE, not in a beat. GDD §4 makes A confirm, recentre and emit — "one
/// button, one meaning, tutorial and journey alike". A that only recentres during B4 is
/// not one meaning, and it fails the obvious way: press A at any other moment and
/// nothing happens. Beats where A means something else switch the binding off for their
/// own duration through SetConfirmRecentres.
///
/// One GDD rule is structural here, and one is a knowing trade:
///
///   * The camera is never locked (§5). There is no SetLocked, deliberately. C3 in the
///     old build locked the camera and playtesters read it as a bug. A rig with no lock
///     cannot regress into one.
///   * The recentre is NOT interruptible by default, which is the trade. See
///     recentreCancellable for why, and for how to put it back.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("FP LOOK", "#40B884")]
public class FirstPersonLookRig_NEW : MonoBehaviour
{
    [Header("Sensitivity — from PlaytestBuild_NEW's Player")]
    [Tooltip("Degrees per second at full deflection. PlaytestBuild scene value: 100.")]
    [SerializeField] float xSensitivity = 100f;

    [Tooltip("Degrees per second. PlaytestBuild stores 0.2 of a 0-1 FreeLook axis across " +
             "an arc of roughly 130 degrees, which is about 26 deg/s. See the class summary.")]
    [SerializeField] float ySensitivity = 26f;

    [Tooltip("Which stick turns the view. One of them, not both — see TutorialInput_NEW.\n\n" +
             "Right, matching PlaytestBuild, so the tutorial and the journey agree. The " +
             "storyboard's prompt art says LEFT STICK; the build is the tie-breaker and " +
             "the prompt strings in TutorialSceneBuilder_NEW were changed to match.")]
    [SerializeField] TutorialInput_NEW.LookStick lookStick = TutorialInput_NEW.LookStick.Right;

    [Tooltip("Stick magnitude below this is ignored. PlaytestBuild scene value: 0.1.")]
    [SerializeField] float stickDeadband = 0.1f;

    [Tooltip("Seconds of smoothing on the applied rotation, standing in for the " +
             "Cinemachine damping the journey's rig has. 0 for raw input.")]
    [SerializeField] float smoothTime = 0.06f;

    [Header("Limits")]
    [Tooltip("Pitch clamp in degrees. Yaw is unlimited — B3 needs a full turn.")]
    [SerializeField] float minPitch = -80f;
    [SerializeField] float maxPitch = 80f;

    [Header("Recentre — from PlaytestBuild_NEW's Player")]
    [Tooltip("A recentres the view. Off only for a rig where A means something else " +
             "for the whole scene; individual beats use SetConfirmRecentres instead.")]
    [SerializeField] bool recentreOnConfirm = true;

    [Tooltip("Degrees per second, constant.\n\n" +
             "PlaytestBuild stores 50, and that is right for a FreeLook that is rarely " +
             "more than 90 degrees off axis. B4 always follows a turn of 150 degrees or " +
             "more, where 50 deg/s is three and a half seconds of very slow drift — long " +
             "enough that the press reads as having done nothing at all.")]
    [SerializeField] float resetXSpeed = 140f;

    [Tooltip("Degrees per second, constant. PlaytestBuild's 1.5 axis-units across the " +
             "same arc is about 195 deg/s.")]
    [SerializeField] float resetYSpeed = 195f;

    [Tooltip("Let look input interrupt a recentre in progress.\n\n" +
             "Off by default, and this is the one place the tutorial knowingly trades " +
             "against GDD §5's 'the camera is never locked'. The recentre lasts about a " +
             "second, the player asked for it with a button press, and every attempt to " +
             "keep it interruptible has instead made A look broken — a worn analog stick " +
             "resting off centre cancels it before the view has visibly moved. Switch it " +
             "back on once the exhibition pads are known good.")]
    [SerializeField] bool recentreCancellable = false;

    [Tooltip("Stick deflection, 0-1, above which a recentre in progress is cancelled. " +
             "Only consulted when recentreCancellable is on.")]
    [SerializeField] float recentreCancelStick = 0.6f;

    [Tooltip("Raw mouse movement per frame above which a recentre is cancelled. Well " +
             "above sensor noise on purpose: an earlier version compared a scaled " +
             "per-frame delta and cancelled the recentre on the frame it began.")]
    [SerializeField] float recentreCancelMouse = 2f;

    [Tooltip("Seconds after the press during which the recentre cannot be cancelled. " +
             "The player has just spent eight seconds turning around; their thumb is " +
             "still on the stick when they press A, and without this the recentre is " +
             "cancelled by the input that was already happening.")]
    [SerializeField] float recentreGrace = 0.25f;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    // Target angles, driven by input.
    float _yaw;
    float _pitch;

    // Applied angles, smoothed towards the target.
    float _appliedYaw;
    float _appliedPitch;
    float _yawVelocity;
    float _pitchVelocity;

    float _forwardYaw;
    float _forwardPitch;

    bool _recentring;
    float _recentreElapsed;
    float _lastConfirmTime = -999f;

    // Mark used by the turn-around gate (B3).
    float _markYaw;

    // ── Public API ───────────────────────────────────────────────────────────

    public float Yaw { get { return _yaw; } }
    public float Pitch { get { return _pitch; } }
    public bool IsRecentring { get { return _recentring; } }

    /// <summary>The axis A sends the view back to. The direction of travel.</summary>
    public Vector3 ForwardAxis
    {
        get { return Quaternion.Euler(_forwardPitch, _forwardYaw, 0f) * Vector3.forward; }
    }

    /// <summary>Signed shortest angle from the current yaw to the forward axis yaw.</summary>
    public float YawFromForward
    {
        get { return Mathf.DeltaAngle(_forwardYaw, _yaw); }
    }

    /// <summary>
    /// Absolute yaw travelled since the last MarkYaw. B3 gates on this passing 150°.
    /// Shortest-angle, so spinning a full 360° reads as 0 — which is correct: a player
    /// who has come all the way back round is facing forward and has not turned around.
    /// </summary>
    public float YawFromMark
    {
        get { return Mathf.Abs(Mathf.DeltaAngle(_markYaw, _yaw)); }
    }

    /// <summary>Record the current yaw as the reference for YawFromMark.</summary>
    public void MarkYaw()
    {
        _markYaw = _yaw;
    }

    /// <summary>Start the constant-speed return to the forward axis.</summary>
    public void BeginRecentre()
    {
        _recentring = true;
        _recentreElapsed = 0f;

        if (debugLog) Debug.Log("[FirstPersonLookRig_NEW] Recentre begun.", this);
    }

    public void CancelRecentre()
    {
        _recentring = false;
    }

    /// <summary>
    /// Turn the A-recentres binding on or off for a beat where A means something else.
    /// C2 emits and E3 hands over; neither should also snap the view.
    /// </summary>
    public void SetConfirmRecentres(bool enabled)
    {
        recentreOnConfirm = enabled;
    }

    /// <summary>Snap the view to the forward axis with no lerp. Used by the attract reset.</summary>
    public void SnapToForward()
    {
        _recentring = false;

        _yaw = _appliedYaw = _forwardYaw;
        _pitch = _appliedPitch = _forwardPitch;
        _yawVelocity = _pitchVelocity = 0f;

        Apply();
    }

    /// <summary>
    /// Re-read the forward axis from the transform's current rotation. Phase 2 needs
    /// this at C5, when the emission changes what "forward" means.
    /// </summary>
    public void SetForwardAxisToCurrent()
    {
        _forwardYaw = _yaw;
        _forwardPitch = _pitch;
    }

    /// <summary>Point the forward axis along a world direction, e.g. the travel vector.</summary>
    public void SetForwardAxis(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return;

        Vector3 euler = Quaternion.LookRotation(direction.normalized).eulerAngles;

        _forwardYaw = euler.y;
        _forwardPitch = NormaliseAngle(euler.x);
    }

    /// <summary>True when the target sits within halfAngleDeg of the view centre.</summary>
    public bool IsInReticle(Transform target, float halfAngleDeg)
    {
        if (target == null) return false;

        Vector3 toTarget = target.position - transform.position;
        if (toTarget.sqrMagnitude < 0.0001f) return true;

        return Vector3.Angle(transform.forward, toTarget) <= halfAngleDeg;
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        Vector3 euler = transform.rotation.eulerAngles;

        _yaw = _appliedYaw = euler.y;
        _pitch = _appliedPitch = NormaliseAngle(euler.x);

        _forwardYaw = _yaw;
        _forwardPitch = _pitch;
        _markYaw = _yaw;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        float dx = TutorialInput_NEW.LookX(lookStick, xSensitivity, stickDeadband, dt);
        float dy = TutorialInput_NEW.LookY(lookStick, ySensitivity, stickDeadband, dt);

        // A recentres. Here, not in a beat.
        //
        // GDD §4: "A — confirm / recentre / emit. One button, one meaning, tutorial and
        // journey alike." A that only recentres during B4 is not one meaning, it is a
        // beat-shaped exception, and it fails exactly the way it was reported failing:
        // press A at any other moment and nothing happens. The rig owning the binding
        // means A recentres from the first frame the player has a camera.
        //
        // Beats where A means something else — C2 emits, E3 hands over — switch this off
        // for their duration through SetConfirmRecentres.
        if (recentreOnConfirm && TutorialInput_NEW.ConfirmDown())
        {
            _lastConfirmTime = Time.unscaledTime;
            BeginRecentre();
        }

        if (_recentring)
        {
            _recentreElapsed += dt;

            // Cancellation is off by default. See the recentreCancellable tooltip: the
            // player asked for this with a button press, it takes about a second, and a
            // cancel that fires from stick drift is indistinguishable from A being broken.
            bool cancelled = recentreCancellable
                             && _recentreElapsed >= recentreGrace
                             && (TutorialInput_NEW.StickDeflection(lookStick, stickDeadband) > recentreCancelStick
                                 || TutorialInput_NEW.MouseDeflection() > recentreCancelMouse);

            if (cancelled)
            {
                _recentring = false;
                if (debugLog) Debug.Log("[FirstPersonLookRig_NEW] Recentre cancelled by look input.", this);
            }
            else
            {
                TickRecentre(dt);
                Smooth(dt);
                Apply();
                return;
            }
        }

        _yaw += dx;
        _pitch = Mathf.Clamp(_pitch - dy, minPitch, maxPitch);

        Smooth(dt);
        Apply();
    }

    /// <summary>
    /// Constant-speed return, exactly as DarkMatterPlayerControllerTest does it:
    /// MoveTowardsAngle on the horizontal, MoveTowards on the vertical.
    /// </summary>
    void TickRecentre(float dt)
    {
        _yaw = Mathf.MoveTowardsAngle(_yaw, _forwardYaw, resetXSpeed * dt);
        _pitch = Mathf.MoveTowards(_pitch, _forwardPitch, resetYSpeed * dt);

        bool xDone = Mathf.Abs(Mathf.DeltaAngle(_yaw, _forwardYaw)) < 0.5f;
        bool yDone = Mathf.Abs(_pitch - _forwardPitch) < 0.5f;

        if (!xDone || !yDone) return;

        _recentring = false;
        if (debugLog) Debug.Log("[FirstPersonLookRig_NEW] Recentre complete.", this);
    }

    void Smooth(float dt)
    {
        if (smoothTime <= 0f)
        {
            _appliedYaw = _yaw;
            _appliedPitch = _pitch;
            return;
        }

        _appliedYaw = Mathf.SmoothDampAngle(_appliedYaw, _yaw, ref _yawVelocity, smoothTime, Mathf.Infinity, dt);
        _appliedPitch = Mathf.SmoothDamp(_appliedPitch, _pitch, ref _pitchVelocity, smoothTime, Mathf.Infinity, dt);
    }

    void Apply()
    {
        transform.rotation = Quaternion.Euler(_appliedPitch, _appliedYaw, 0f);
    }

    static float NormaliseAngle(float degrees)
    {
        degrees %= 360f;
        if (degrees > 180f) degrees -= 360f;
        return degrees;
    }

    /// <summary>
    /// One line saying what the camera is doing and whether it saw the confirm press.
    ///
    /// "A does nothing" is a symptom with at least four causes — the button not being
    /// read, the beat not being open, the recentre starting and being cancelled, and the
    /// view already being on axis so there is nothing to see. This distinguishes them
    /// without a breakpoint, which is what a whitebox session at the Observatories needs.
    /// </summary>
    void OnGUI()
    {
        if (!DebugView_NEW.Overlay) return;

        string state = _recentring ? "RECENTRING" : "free";
        float offAxis = Mathf.Abs(Mathf.DeltaAngle(_yaw, _forwardYaw));

        float sinceConfirm = Time.unscaledTime - _lastConfirmTime;
        string lastA = sinceConfirm > 900f ? "never" : sinceConfirm.ToString("F1") + "s ago";

        GUI.Label(new Rect(10f, 50f, 900f, 22f),
                  string.Format("CAMERA  {0}   off-axis {1:F0}deg   last A {2}   stick {3:F2}   A binding {4}",
                                state, offAxis, lastA,
                                TutorialInput_NEW.StickDeflection(lookStick, stickDeadband),
                                recentreOnConfirm ? "on" : "OFF"));
    }
}

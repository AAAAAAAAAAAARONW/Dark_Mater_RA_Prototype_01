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
///   Recentre     resetXSpeed 50 deg/s, applied with MoveTowardsAngle — constant speed,
///                not an eased fixed-duration lerp. resetYSpeed 1.5 axis-units becomes
///                about 195 deg/s by the same arc conversion.
///   FOV          40 on the FreeLook, against Unity's default 60. Set by the builder on
///                the camera; a first person view at 60 reads as a different game.
///
/// PlaytestBuild's softness comes from Cinemachine damping on the rig, which first
/// person has no equivalent for, so `smoothTime` adds it back explicitly. Set it to 0
/// for raw input.
///
/// Two GDD rules are structural here, not options:
///
///   * The camera is never locked (§5). There is no SetLocked, deliberately. C3 in the
///     old build locked the camera and playtesters read it as a bug. A rig with no lock
///     cannot regress into one.
///   * A recentres, and the player can interrupt it (B4). Touching the stick mid-recentre
///     cancels it, because a recentre that fights the stick is a lock wearing a hat.
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
    [Tooltip("Degrees per second, constant. PlaytestBuild scene value: 50.")]
    [SerializeField] float resetXSpeed = 50f;

    [Tooltip("Degrees per second, constant. PlaytestBuild's 1.5 axis-units across the " +
             "same arc is about 195 deg/s.")]
    [SerializeField] float resetYSpeed = 195f;

    [Tooltip("Stick deflection, 0-1, above which a recentre in progress is cancelled. " +
             "The camera is never locked, so the player always outranks the lerp — but " +
             "a resting thumb must not count, or A appears to do nothing.")]
    [SerializeField] float recentreCancelStick = 0.35f;

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

        float dx = TutorialInput_NEW.LookX(xSensitivity, stickDeadband, dt);
        float dy = TutorialInput_NEW.LookY(ySensitivity, stickDeadband, dt);

        if (_recentring)
        {
            // The player outranks the recentre — but only a deliberate input counts.
            // Read the devices, not the resulting per-frame delta. See the tooltips.
            _recentreElapsed += dt;

            bool cancellable = _recentreElapsed >= recentreGrace;
            bool cancelled = cancellable
                             && (TutorialInput_NEW.StickDeflection(stickDeadband) > recentreCancelStick
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

        GUI.Label(new Rect(10f, 50f, 900f, 22f),
                  string.Format("CAMERA  {0}   off-axis {1:F0}deg   confirm-down {2}   stick {3:F2}",
                                state, offAxis,
                                TutorialInput_NEW.ConfirmDown() ? "YES" : "-",
                                TutorialInput_NEW.StickDeflection(stickDeadband)));
    }
}

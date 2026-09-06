using UnityEngine;

/// <summary>
/// First person look for the tutorial. Yaw and pitch on one transform, nothing else.
///
/// Why this is not OrbitCameraRig_NEW: that rig orbits a FreeLook around the player and
/// is third person. GDD §1 fixes the tutorial as first person throughout, with no view
/// swap, and GDD §11 records first person as the whole difference between this design and
/// Tutorial Storyboard v1. Bending the orbit rig into a first person camera would mean a
/// zero-radius orbit, which is a FreeLook fighting a Composer to stay still. This is
/// thirty lines instead.
///
/// Two rules from the GDD are structural here, not options:
///
///   * The camera is never locked (§5). There is no SetLocked, deliberately. C3 in the
///     old build locked the camera and playtesters read it as a bug. A rig with no lock
///     cannot regress into one.
///   * A recentres, and does so as a lerp the player can interrupt (B4). Touching the
///     stick mid-recentre cancels it, because a recentre that fights the stick is a lock
///     wearing a different hat.
///
/// The forward axis is the jet axis — the direction of travel, and the direction A
/// returns you to. It is stored as the rig's yaw and pitch at Awake, so whatever you
/// point the transform at in the scene becomes "forward" with no extra wiring.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("FP LOOK", "#40B884")]
public class FirstPersonLookRig_NEW : MonoBehaviour
{
    [Header("Sensitivity")]
    [Tooltip("Stick, degrees per second at full deflection.")]
    [SerializeField] float stickXSensitivity = 120f;
    [SerializeField] float stickYSensitivity = 90f;

    [Tooltip("Mouse, degrees per unit of raw axis. Desk testing only; not the shipping input.")]
    [SerializeField] float mouseXSensitivity = 2.5f;
    [SerializeField] float mouseYSensitivity = 2.0f;

    [Tooltip("Stick magnitude below this is ignored, so an idle pad does not drift the view.")]
    [SerializeField] float stickDeadband = 0.1f;

    [Header("Limits")]
    [Tooltip("Pitch clamp in degrees. Yaw is unlimited — B3 needs a full turn.")]
    [SerializeField] float minPitch = -80f;
    [SerializeField] float maxPitch = 80f;

    [Header("Recentre")]
    [Tooltip("Seconds for A to lerp the view back to the jet axis (B4).")]
    [SerializeField] float recentreDuration = 0.9f;

    [Tooltip("Stick deflection above this cancels a recentre in progress. The camera is " +
             "never locked, so the player always outranks the lerp.")]
    [SerializeField] float recentreCancelThreshold = 0.25f;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    float _yaw;
    float _pitch;

    float _forwardYaw;
    float _forwardPitch;

    // Recentre state
    bool _recentring;
    float _recentreElapsed;
    float _recentreFromYaw;
    float _recentreFromPitch;

    // Mark used by the turn-around gate (B3).
    float _markYaw;

    // ── Public API ───────────────────────────────────────────────────────────

    public float Yaw { get { return _yaw; } }
    public float Pitch { get { return _pitch; } }
    public bool IsRecentring { get { return _recentring; } }

    /// <summary>The jet axis: where A sends the view back to.</summary>
    public Vector3 ForwardAxis
    {
        get { return Quaternion.Euler(_forwardPitch, _forwardYaw, 0f) * Vector3.forward; }
    }

    /// <summary>Signed shortest angle from the current yaw to the jet axis yaw.</summary>
    public float YawFromForward
    {
        get { return Mathf.DeltaAngle(_forwardYaw, _yaw); }
    }

    /// <summary>
    /// Absolute yaw travelled since the last MarkYaw. B3 gates on this passing 150°.
    /// Shortest-angle, so spinning 360° reads as 0 — which is correct: a player who has
    /// come all the way back round is facing forward again and has not turned around.
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

    /// <summary>Start the lerp back to the jet axis. Safe to call while one is running.</summary>
    public void BeginRecentre()
    {
        if (recentreDuration <= 0f)
        {
            _yaw = _forwardYaw;
            _pitch = _forwardPitch;
            Apply();
            return;
        }

        _recentring = true;
        _recentreElapsed = 0f;
        _recentreFromYaw = _yaw;
        _recentreFromPitch = _pitch;

        if (debugLog) Debug.Log("[FirstPersonLookRig_NEW] Recentre begun.", this);
    }

    public void CancelRecentre()
    {
        _recentring = false;
    }

    /// <summary>
    /// Re-read the jet axis from the transform's current rotation. Phase 2 will need
    /// this when the emission changes what "forward" means.
    /// </summary>
    public void SetForwardAxisToCurrent()
    {
        _forwardYaw = _yaw;
        _forwardPitch = _pitch;
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

        _yaw = euler.y;
        _pitch = NormaliseAngle(euler.x);

        _forwardYaw = _yaw;
        _forwardPitch = _pitch;
        _markYaw = _yaw;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        float dx = TutorialInput_NEW.LookX(stickXSensitivity, mouseXSensitivity, stickDeadband, dt);
        float dy = TutorialInput_NEW.LookY(stickYSensitivity, mouseYSensitivity, stickDeadband, dt);

        if (_recentring)
        {
            // The player outranks the lerp. See the class summary.
            if (Mathf.Abs(dx) > recentreCancelThreshold || Mathf.Abs(dy) > recentreCancelThreshold)
            {
                _recentring = false;
                if (debugLog) Debug.Log("[FirstPersonLookRig_NEW] Recentre cancelled by stick.", this);
            }
            else
            {
                TickRecentre(dt);
                Apply();
                return;
            }
        }

        _yaw += dx;
        _pitch = Mathf.Clamp(_pitch - dy, minPitch, maxPitch);

        Apply();
    }

    void TickRecentre(float dt)
    {
        _recentreElapsed += dt;

        float t = Mathf.Clamp01(_recentreElapsed / recentreDuration);
        float eased = t * t * (3f - 2f * t);

        _yaw = Mathf.LerpAngle(_recentreFromYaw, _forwardYaw, eased);
        _pitch = Mathf.Lerp(_recentreFromPitch, _forwardPitch, eased);

        if (t >= 1f)
        {
            _recentring = false;
            if (debugLog) Debug.Log("[FirstPersonLookRig_NEW] Recentre complete.", this);
        }
    }

    void Apply()
    {
        transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }

    static float NormaliseAngle(float degrees)
    {
        degrees %= 360f;
        if (degrees > 180f) degrees -= 360f;
        return degrees;
    }
}

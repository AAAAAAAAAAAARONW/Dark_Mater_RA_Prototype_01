using System.Collections;
using UnityEngine;
using Cinemachine;

/// <summary>
/// Dark Matter Game - Player Controller (Cinemachine Rewrite)
/// 
/// Movement:
///   - Player moves continuously forward at adjustable speed
///   - Left stick vertical (Vertical axis) = speed up/down
///   - Movement direction is NEVER changed by player input
///   - Dark matter objects bend the path; direction recovers after leaving influence
///
/// Camera (via Cinemachine FreeLook):
///   - Right stick = orbit camera around player (horizontal + vertical)
///   - A button (Submit) = smoothly reset camera to behind-player view
///   - Camera input can be locked externally (e.g. during cinematic transitions)
///   - VCam_TopDown has no orbit input; handled by Cinemachine priority switching
///
/// External API (called by MicroToMacroTriggerSequenceTest):
///   - SetExternalSpeedMultiplier() / TweenExternalSpeedMultiplier()
///   - SetMovementDirection()
///   - MovementDirection, Speed (read-only properties)
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class DarkMatterPlayerControllerTest : MonoBehaviour
{
    // ─────────────────────────────────────────────
    // MOVEMENT
    // ─────────────────────────────────────────────

    [Header("Movement")]
    [Tooltip("Current forward speed")]
    [SerializeField] float speed = 5f;
    [Tooltip("Min / Max speed")]
    [SerializeField] float minSpeed = 0.5f;
    [SerializeField] float maxSpeed = 20f;
    [Tooltip("Speed change per second (left stick vertical / W/S)")]
    [SerializeField] float speedChangePerSecond = 8f;
    [Tooltip("World-space forward direction (normalized). Dark matter may bend this.")]
    [SerializeField] Vector3 movementDirection = Vector3.forward;

    // ─────────────────────────────────────────────
    // CINEMACHINE CAMERA
    // ─────────────────────────────────────────────

    [Header("Cinemachine - FreeLook Cameras")]
    [Tooltip("FreeLook camera used in Macro state")]
    [SerializeField] CinemachineFreeLook vcamMacro;
    [Tooltip("FreeLook camera used in Micro state")]
    [SerializeField] CinemachineFreeLook vcamMicro;

    [Header("Cinemachine - Input Sensitivity")]
    [Tooltip("Right stick horizontal sensitivity (degrees/sec equivalent)")]
    [SerializeField] float xSensitivity = 300f;
    [Tooltip("Right stick vertical sensitivity (FreeLook Y axis is 0-1, keep this small ~1-3)")]
    [SerializeField] float ySensitivity = 2f;

    [Header("Cinemachine - Reset View (A Button)")]
    [Tooltip("Speed at which X axis (horizontal) returns to 0 when A is pressed (units/sec)")]
    [SerializeField] float resetXSpeed = 180f;
    [Tooltip("Speed at which Y axis (vertical) returns to 0.5 when A is pressed (units/sec)")]
    [SerializeField] float resetYSpeed = 1.5f;
    [Tooltip("Dead zone to restore on both axes after reset completes")]
    [SerializeField] float normalDeadZoneWidth = 0.3f;
    [SerializeField] float normalDeadZoneHeight = 0.3f;

    // ─────────────────────────────────────────────
    // DARK MATTER PATH BENDING
    // ─────────────────────────────────────────────

    [Header("Dark Matter Path Bending")]
    [Tooltip("Minimum angular return speed (rad/sec) after leaving dark matter influence")]
    [SerializeField] float darkMatterReturnSpeedMinRadPerSec = 1.5f;
    [Tooltip("Max seconds allowed to return to original direction")]
    [SerializeField] float darkMatterReturnMaxTime = 3f;
    [Tooltip("Min duration for height return after peak (avoids divide-by-zero)")]
    [SerializeField] float darkMatterHeightReturnMinDuration = 0.3f;

    // ─────────────────────────────────────────────
    // PRIVATE STATE
    // ─────────────────────────────────────────────

    CharacterController _controller;

    // Speed
    float _externalSpeedMultiplier = 1f;

    // Camera reset
    bool _isResettingView;
    bool _cameraInputLocked;

    // Dark matter bending
    Vector3 _defaultMovementDirection;
    bool _isReturningFromDarkMatter;
    bool _hadDarkMatterInfluence;
    float _influenceTime;
    float _maxBendAngleRad;
    float _returnAngularSpeedRadPerSec;
    float _darkMatterEntryHeight;
    float _darkMatterEntryTime;
    float _darkMatterMaxHeight;
    float _darkMatterMaxHeightTime;
    float _heightReturnStartTime;
    float _heightReturnDuration;
    bool _isReturningToEntryHeight;

    // ─────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────

    void Awake()
    {
        _controller = GetComponent<CharacterController>();
        movementDirection = movementDirection.normalized;
        _defaultMovementDirection = movementDirection;

        // Register custom input axis handler for Cinemachine
        // This redirects right stick input to FreeLook cameras
        CinemachineCore.GetInputAxis = GetCinemachineAxis;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // 1. Speed input: left stick vertical / W / S
        float speedInput = Input.GetAxis("Vertical");
        speed += speedInput * speedChangePerSecond * dt;
        speed = Mathf.Clamp(speed, minSpeed, maxSpeed);

        // 2. Dark matter path bending
        ApplyDarkMatterBend(dt);

        // 3. Move forward continuously
        float finalSpeed = speed * _externalSpeedMultiplier;
        _controller.Move(movementDirection * (finalSpeed * dt));

        // 4. Camera reset: A button (Submit)
        if (Input.GetButtonDown("Submit"))
            _isResettingView = true;

        // 5. Smooth reset view if active
        if (_isResettingView)
            TickResetView(dt);
    }

    // ─────────────────────────────────────────────
    // CINEMACHINE INPUT
    // ─────────────────────────────────────────────

    /// <summary>
    /// Custom input provider for Cinemachine. Called by Cinemachine every frame.
    /// Redirects right stick axes to "Mouse X" / "Mouse Y" which FreeLook reads by default.
    /// Input is blocked during cinematic lock or active view reset.
    /// </summary>
    float GetCinemachineAxis(string axisName)
    {
        // Block input during lock or while reset animation is playing
        if (_cameraInputLocked || _isResettingView)
            return 0f;

        switch (axisName)
        {
            case "Mouse X":
                return Input.GetAxis("RightStickX") * xSensitivity * Time.deltaTime;
            case "Mouse Y":
                return Input.GetAxis("RightStickY") * ySensitivity * Time.deltaTime;
            default:
                return Input.GetAxis(axisName);
        }
    }

    /// <summary>
    /// Smoothly drives both FreeLook cameras back to behind-player position.
    /// X axis → 0 (behind player), Y axis → 0.5 (middle ring).
    /// Temporarily zeroes dead zones so Composer recenters the player exactly.
    /// Restores dead zones when reset completes.
    /// </summary>
    void TickResetView(float dt)
    {
        bool xDone = true;
        bool yDone = true;

        if (vcamMacro != null)
        {
            // Zero dead zones so Composer recenters player precisely during reset
            SetDeadZones(vcamMacro, 0f, 0f);

            vcamMacro.m_XAxis.Value = Mathf.MoveTowardsAngle(vcamMacro.m_XAxis.Value, 0f, resetXSpeed * dt);
            vcamMacro.m_YAxis.Value = Mathf.MoveTowards(vcamMacro.m_YAxis.Value, 0.5f, resetYSpeed * dt);
            xDone &= Mathf.Abs(Mathf.DeltaAngle(vcamMacro.m_XAxis.Value, 0f)) < 0.5f;
            yDone &= Mathf.Abs(vcamMacro.m_YAxis.Value - 0.5f) < 0.01f;
        }

        if (vcamMicro != null)
        {
            // Zero dead zones so Composer recenters player precisely during reset
            SetDeadZones(vcamMicro, 0f, 0f);

            vcamMicro.m_XAxis.Value = Mathf.MoveTowardsAngle(vcamMicro.m_XAxis.Value, 0f, resetXSpeed * dt);
            vcamMicro.m_YAxis.Value = Mathf.MoveTowards(vcamMicro.m_YAxis.Value, 0.5f, resetYSpeed * dt);
            xDone &= Mathf.Abs(Mathf.DeltaAngle(vcamMicro.m_XAxis.Value, 0f)) < 0.5f;
            yDone &= Mathf.Abs(vcamMicro.m_YAxis.Value - 0.5f) < 0.01f;
        }

        if (xDone && yDone)
        {
            // Reset complete — restore normal dead zones to prevent jiggle
            if (vcamMacro != null) SetDeadZones(vcamMacro, normalDeadZoneWidth, normalDeadZoneHeight);
            if (vcamMicro != null) SetDeadZones(vcamMicro, normalDeadZoneWidth, normalDeadZoneHeight);
            _isResettingView = false;
        }
    }

    /// <summary>
    /// Sets dead zone width and height on all three rigs of a FreeLook camera's Composer.
    /// </summary>
    void SetDeadZones(CinemachineFreeLook vcam, float width, float height)
    {
        for (int i = 0; i < 3; i++)
        {
            var rig = vcam.GetRig(i);
            if (rig == null) continue;
            var composer = rig.GetCinemachineComponent<CinemachineComposer>();
            if (composer == null) continue;
            composer.m_DeadZoneWidth = width;
            composer.m_DeadZoneHeight = height;
        }
    }

    // ─────────────────────────────────────────────
    // DARK MATTER PATH BENDING (unchanged logic)
    // ─────────────────────────────────────────────

    void ApplyDarkMatterBend(float dt)
    {
        DarkMatter[] allDarkMatter = FindObjectsOfType<DarkMatter>();
        Vector3 totalDeflection = Vector3.zero;
        Vector3 pos = transform.position;
        float t = Time.time;

        foreach (DarkMatter dm in allDarkMatter)
        {
            if (!dm.IsInInfluence(pos)) continue;
            totalDeflection += dm.GetDeflectionAt(pos, _defaultMovementDirection);
        }

        bool hasInfluence = totalDeflection.sqrMagnitude > 0.0001f;

        if (hasInfluence)
        {
            if (!_hadDarkMatterInfluence)
            {
                Debug.Log("[Dark Matter] Encountered dark matter influence.");
                _darkMatterEntryHeight = pos.y;
                _darkMatterEntryTime = t;
                _darkMatterMaxHeight = pos.y;
                _darkMatterMaxHeightTime = t;
            }

            _isReturningFromDarkMatter = false;
            _isReturningToEntryHeight = false;

            if (pos.y > _darkMatterMaxHeight)
            {
                _darkMatterMaxHeight = pos.y;
                _darkMatterMaxHeightTime = t;
            }

            Vector3 bent = (_defaultMovementDirection + totalDeflection).normalized;
            movementDirection = bent;
            _influenceTime += dt;

            float angleRad = Mathf.Acos(Mathf.Clamp01(Vector3.Dot(_defaultMovementDirection, bent)));
            if (angleRad > _maxBendAngleRad) _maxBendAngleRad = angleRad;
        }
        else
        {
            if (_hadDarkMatterInfluence)
            {
                _isReturningFromDarkMatter = true;
                float effectiveTime = Mathf.Max(Mathf.Min(_influenceTime, darkMatterReturnMaxTime), 0.05f);
                float matchedSpeed = _maxBendAngleRad / effectiveTime;
                float speedToFinishInMaxTime = _maxBendAngleRad / darkMatterReturnMaxTime;
                _returnAngularSpeedRadPerSec = Mathf.Max(matchedSpeed, speedToFinishInMaxTime, darkMatterReturnSpeedMinRadPerSec);

                _heightReturnDuration = Mathf.Max(_darkMatterMaxHeightTime - _darkMatterEntryTime, darkMatterHeightReturnMinDuration);
                _heightReturnStartTime = t;
                _isReturningToEntryHeight = true;
            }

            _influenceTime = 0f;
            _maxBendAngleRad = 0f;

            if (_isReturningToEntryHeight)
            {
                float elapsed = t - _heightReturnStartTime;
                float timeRemaining = _heightReturnDuration - elapsed;

                if (timeRemaining <= 0f || Mathf.Abs(pos.y - _darkMatterEntryHeight) < 0.02f)
                {
                    movementDirection = _defaultMovementDirection;
                    _isReturningToEntryHeight = false;
                    _isReturningFromDarkMatter = false;
                }
                else
                {
                    float desiredVy = (_darkMatterEntryHeight - pos.y) / timeRemaining;
                    float dy = Mathf.Clamp(desiredVy / speed, -1f, 1f);
                    Vector3 hor = new Vector3(_defaultMovementDirection.x, 0f, _defaultMovementDirection.z);
                    float horLen = hor.magnitude;
                    if (horLen < 0.0001f) hor = Vector3.forward;
                    else hor /= horLen;
                    float k = Mathf.Sqrt(Mathf.Max(0f, 1f - dy * dy));
                    movementDirection = new Vector3(hor.x * k, dy, hor.z * k).normalized;

                    float angleRad = Mathf.Acos(Mathf.Clamp01(Vector3.Dot(movementDirection, _defaultMovementDirection)));
                    if (angleRad < 0.001f) _isReturningFromDarkMatter = false;
                }
            }
            else if (_isReturningFromDarkMatter)
            {
                float angleRad = Mathf.Acos(Mathf.Clamp01(Vector3.Dot(movementDirection, _defaultMovementDirection)));
                float step = angleRad < 0.001f ? 1f : Mathf.Clamp01((_returnAngularSpeedRadPerSec * dt) / angleRad);
                movementDirection = Vector3.Slerp(movementDirection, _defaultMovementDirection, step);

                if (Vector3.Dot(movementDirection, _defaultMovementDirection) >= 0.9995f)
                {
                    movementDirection = _defaultMovementDirection;
                    _isReturningFromDarkMatter = false;
                }
            }
            else
            {
                _defaultMovementDirection = movementDirection;
            }
        }

        _hadDarkMatterInfluence = hasInfluence;
    }

    // ─────────────────────────────────────────────
    // PUBLIC API (called by trigger/transition scripts)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Set movement direction externally (e.g. from collision or gravity).
    /// Pass a normalized direction vector.
    /// </summary>
    public void SetMovementDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude > 0.001f)
            movementDirection = direction.normalized;
    }

    /// <summary>
    /// Immediately set external speed multiplier.
    /// 1 = normal, 0.5 = half speed, 0 = stopped.
    /// </summary>
    public void SetExternalSpeedMultiplier(float multiplier)
    {
        _externalSpeedMultiplier = Mathf.Max(0f, multiplier);
    }

    /// <summary>
    /// Smoothly tween external speed multiplier over duration seconds.
    /// </summary>
    public IEnumerator TweenExternalSpeedMultiplier(float targetMultiplier, float duration)
    {
        float from = _externalSpeedMultiplier;
        float to = Mathf.Max(0f, targetMultiplier);
        float d = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < d)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / d);
            float eased = t * t * (3f - 2f * t); // SmoothStep
            _externalSpeedMultiplier = Mathf.Lerp(from, to, eased);
            yield return null;
        }

        _externalSpeedMultiplier = to;
    }

    /// <summary>
    /// Lock/unlock camera orbit input. Use during cinematic sequences.
    /// When locked, right stick input is ignored by Cinemachine.
    /// </summary>
    public void SetCameraInputLocked(bool locked)
    {
        _cameraInputLocked = locked;
        if (locked) _isResettingView = false; // cancel any in-progress reset
    }

    /// <summary>
    /// Current forward movement direction (read-only).
    /// </summary>
    public Vector3 MovementDirection => movementDirection;

    /// <summary>
    /// Current speed (read-only).
    /// </summary>
    public float Speed => speed;
}
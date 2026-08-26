using Cinemachine;
using UnityEngine;

/// <summary>
/// The player. Moves forward continuously; input changes speed, never heading.
///
/// Replaces DarkMatterPlayerControllerTest. The Update order, the speed maths, the
/// look sensitivities, the deadband, the recentre thresholds and the bend model are all
/// unchanged — this is a reorganisation, not a retune. What moved:
///
///   * The three copy-pasted per-camera blocks became OrbitCameraRig_NEW.
///   * The 100-line bend routine became DarkMatterBend_NEW, called from the same slot.
///   * The per-frame FindObjectsOfType went behind DarkMatterRegistry_NEW.
///   * The dead save/restore of m_InputAxisName is gone. Cinemachine input is already
///     short-circuited globally below, so blanking the axis names did nothing.
///
/// One known issue is preserved rather than silently fixed: the recentre action is not
/// gated by the camera lock, so pressing it during a transition can pull the rig away.
/// Fixing that changes how the game responds to a button, which is a feel change and
/// belongs in its own change. blockResetWhileLocked exposes the fix, defaulted off.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
[HierarchyBadge_NEW("PLAYER", "#40B884")]
public class PlayerRig_NEW : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float speed = 2.5f;
    [SerializeField] float minSpeed = 0.1f;
    [SerializeField] float maxSpeed = 3.5f;

    [Tooltip("Speed change per second from the vertical axis.")]
    [SerializeField] float speedChangePerSecond = 8f;

    [Tooltip("World-space heading. Normalised on Awake. Dark matter bends this.")]
    [SerializeField] Vector3 movementDirection = Vector3.forward;

    [Header("Camera")]
    [SerializeField] OrbitCameraRig_NEW orbit = new OrbitCameraRig_NEW();

    [Tooltip("Ignore the recentre button while the camera is locked by a transition. " +
             "Off reproduces the current behaviour exactly; on fixes the known issue " +
             "where the recentre can interrupt a cutscene camera.")]
    [SerializeField] bool blockResetWhileLocked = false;

    [Header("Input axes")]
    [SerializeField] string speedAxis = "Vertical";
    [SerializeField] string resetButton = "Submit";
    [SerializeField] KeyCode resetKey = KeyCode.Space;

    [Header("Dark matter")]
    [SerializeField] DarkMatterBend_NEW bend = new DarkMatterBend_NEW();

    CharacterController _controller;
    Vector3 _defaultDirection;
    float _externalSpeedMultiplier = 1f;
    bool _cameraInputLocked;

    // ── Public API ───────────────────────────────────────────────────────────

    public Vector3 MovementDirection => movementDirection;
    public float Speed => speed;
    public float ExternalSpeedMultiplier => _externalSpeedMultiplier;
    public bool CameraInputLocked => _cameraInputLocked;

    public void SetMovementDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude > 0.001f) movementDirection = direction.normalized;
    }

    /// <summary>1 = normal, 0.5 = half, 0 = stopped.</summary>
    public void SetExternalSpeedMultiplier(float multiplier)
    {
        _externalSpeedMultiplier = Mathf.Max(0f, multiplier);
    }

    /// <summary>Called by CameraDirector_NEW around a transition.</summary>
    public void SetCameraInputLocked(bool locked)
    {
        _cameraInputLocked = locked;
        if (locked) orbit.CancelReset();
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        _controller = GetComponent<CharacterController>();

        movementDirection = movementDirection.normalized;
        _defaultDirection = movementDirection;

        DarkMatterRegistry_NEW.Rebuild();
    }

    void OnEnable()
    {
        // Cut Cinemachine's built-in axis reads. Without this the rigs fall back to
        // Mouse X / Mouse Y and can spin on their own after a domain reload. Every
        // axis value in OrbitCameraRig_NEW is written by hand as a result.
        CinemachineCore.GetInputAxis = _ => 0f;
    }

    void OnDisable()
    {
        CinemachineCore.GetInputAxis = null;
        DarkMatterRegistry_NEW.Invalidate();
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // 1 · Speed input
        speed += Input.GetAxis(speedAxis) * speedChangePerSecond * dt;
        speed = Mathf.Clamp(speed, minSpeed, maxSpeed);

        // 2 · Dark matter bending
        bend.Tick(dt, transform.position, speed, ref movementDirection, ref _defaultDirection);

        // 3 · Move
        float finalSpeed = speed * _externalSpeedMultiplier;
        _controller.Move(movementDirection * (finalSpeed * dt));

        // 4 · Look input
        if (!_cameraInputLocked && !orbit.IsResetting)
            orbit.DriveInput(dt);

        // 5 · Recentre request
        bool resetPressed = Input.GetButtonDown(resetButton) || Input.GetKeyDown(resetKey);
        if (resetPressed && !(blockResetWhileLocked && _cameraInputLocked))
            orbit.BeginReset();

        // 6 · Recentre step
        if (orbit.IsResetting)
            orbit.TickReset(dt);
    }
}

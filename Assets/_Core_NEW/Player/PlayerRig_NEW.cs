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
/// LATER: INPUT MOVED TO InputScheme_NEW. This file used to name its own axes —
/// "Vertical" for speed, "Submit" plus space for recentre — and OrbitCameraRig_NEW named
/// its own for look. The tutorial named a third set. Three files, one control scheme,
/// agreeing by coincidence; the Carnegie Observatories pad is a Logitech that broke the
/// coincidence, and it broke it by scrambling the mapping rather than killing it, which
/// reads to a visitor as "I am holding this wrong". Every read here now goes through the
/// scheme, which asks a pad profile. See PadProfile_NEW.
///
/// The speed input also became a decision instead of a default — see SpeedInput.
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
    /// <summary>Who may change the journey speed. See the speedInput field.</summary>
    public enum SpeedInput
    {
        /// <summary>Nobody. The design default — light does not take instructions.</summary>
        Off = 0,

        /// <summary>The D-pad. Reachable by whoever is running the room, named in no prompt.</summary>
        Attendant = 1,

        /// <summary>The left stick, as the build did before. Playtest convenience.</summary>
        Visitor = 2
    }

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

    [Tooltip("Looking during a recentre cancels it and hands the view straight back.\n\n" +
             "Off = the recentre runs to the end and look input is ignored until it does: " +
             "up to 3.6 s at this scene's resetXSpeed of 50, with nothing on screen to say why. " +
             "Same rule as the tutorial's look rig, with the same thresholds.")]
    [SerializeField] bool lookCancelsRecentre = true;

    [Header("Speed input")]
    [Tooltip("Who, if anyone, may change the journey speed.\n\n" +
             "OFF is the design and the default. The tutorial spends its whole first act " +
             "teaching that you cannot steer light — it deletes the move stick rather " +
             "than writing a line of text about it — and a journey where a visitor can " +
             "slow the light to a stop makes that lesson a lie. It also leaves the next " +
             "visitor standing in front of a picture that is not moving.\n\n" +
             "ATTENDANT puts it on the D-pad, which no prompt mentions and no visitor " +
             "reaches for, and which the pad profile maps correctly per controller.\n\n" +
             "VISITOR is the old behaviour, on the left stick. Kept for playtests where " +
             "somebody needs to scrub through the journey by hand.")]
    [SerializeField] SpeedInput speedInput = SpeedInput.Off;

    [Header("Dark matter")]
    [SerializeField] DarkMatterBend_NEW bend = new DarkMatterBend_NEW();

    // FirstPersonLookRig_NEW's recentreCancelStick / recentreCancelMouse: a deliberate
    // push, not a stick resting off centre or mouse sensor noise.
    const float RecentreCancelStick = 0.6f;
    const float RecentreCancelMouse = 2f;

    CharacterController _controller;
    Vector3 _defaultDirection;
    float _externalSpeedMultiplier = 1f;
    float _narrationSpeedMultiplier = 1f;
    bool _cameraInputLocked;
    string _lockReason;
    float _lookSensitivityScale = 1f;

    // ── Public API ───────────────────────────────────────────────────────────

    public Vector3 MovementDirection => movementDirection;
    public float Speed => speed;
    public float ExternalSpeedMultiplier => _externalSpeedMultiplier;
    public bool CameraInputLocked => _cameraInputLocked;
    public SpeedInput SpeedInputMode => speedInput;

    /// <summary>
    /// Why the view controls (look and zoom) are doing nothing right now, or null when
    /// they work. Read by InputDiagnostics_NEW's banner and by JourneyZoom_NEW, so the
    /// stick being ignored is never silent.
    /// </summary>
    public string ViewBlockedReason
    {
        get
        {
            if (_cameraInputLocked)
                return string.IsNullOrEmpty(_lockReason) ? "locked by a camera sequence" : _lockReason;

            if (!orbit.AnyLive())
                return "the camera on screen is '" + LiveCameraName() + "', not the player's orbit";

            if (orbit.IsResetting && !lookCancelsRecentre)
                return "recentring (A) — look returns when it finishes";

            return null;
        }
    }

    /// <summary>
    /// Scales look sensitivity. JourneyZoom_NEW sets it to the zoom ratio so that a
    /// narrowed view does not feel faster than a wide one.
    /// </summary>
    public void SetLookSensitivityScale(float scale)
    {
        _lookSensitivityScale = Mathf.Max(0.01f, scale);
    }

    public void SetMovementDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude > 0.001f) movementDirection = direction.normalized;
    }

    /// <summary>1 = normal, 0.5 = half, 0 = stopped.</summary>
    public void SetExternalSpeedMultiplier(float multiplier)
    {
        _externalSpeedMultiplier = Mathf.Max(0f, multiplier);
    }

    /// <summary>
    /// A second, separate multiplier for narration: slows the flight while something is being
    /// explained (NarrationSlowdown_NEW). Separate so it never fights SpeedResponder_NEW, which
    /// owns the external one per layer. The two multiply. 1 = no effect.
    /// </summary>
    public void SetNarrationSpeedMultiplier(float multiplier)
    {
        _narrationSpeedMultiplier = Mathf.Max(0f, multiplier);
    }

    public float NarrationSpeedMultiplier => _narrationSpeedMultiplier;

    /// <summary>
    /// Called by CameraDirector_NEW around a transition. <paramref name="reason"/> is what
    /// the debug banner shows while the lock holds.
    /// </summary>
    public void SetCameraInputLocked(bool locked, string reason = null)
    {
        _cameraInputLocked = locked;
        _lockReason = locked ? reason : null;
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

    void Start()
    {
        // In Start rather than Awake, so the FreeLooks have built their rigs first.
        orbit.ApplyAim();
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
        speed += SpeedAxis() * speedChangePerSecond * dt;
        speed = Mathf.Clamp(speed, minSpeed, maxSpeed);

        // 2 · Dark matter bending
        bend.Tick(dt, transform.position, speed, ref movementDirection, ref _defaultDirection);

        // 3 · Move
        float finalSpeed = speed * _externalSpeedMultiplier * _narrationSpeedMultiplier;
        _controller.Move(movementDirection * (finalSpeed * dt));

        // 4 · Look input
        //
        // Only while one of the player's rigs is on screen. Turning them under the cover
        // or the intro shot moved a camera nobody could see, and the view swung to that
        // angle the moment it came back — Macro does not lock look, so its gate did this.
        if (!_cameraInputLocked)
        {
            if (orbit.IsResetting && lookCancelsRecentre && LookAttempted())
                orbit.CancelReset();

            if (!orbit.IsResetting && orbit.AnyLive())
                orbit.DriveInput(dt, _lookSensitivityScale);
        }

        // 5 · Recentre request
        //
        // A, everywhere, on whatever pad is plugged in. This used to read the Submit
        // axis and the space bar directly, which is the same answer as the tutorial's
        // only by coincidence; both now come from InputScheme_NEW, so "one button, one
        // meaning, tutorial and journey alike" is enforced by there being one reader
        // rather than by two files agreeing.
        bool resetPressed = InputScheme_NEW.ConfirmDown();
        if (resetPressed && !(blockResetWhileLocked && _cameraInputLocked))
            orbit.BeginReset();

        // 6 · Recentre step
        if (orbit.IsResetting)
            orbit.TickReset(dt);
    }

    /// <summary>A deliberate look — the stick pushed well past centre, or the mouse moved.</summary>
    static bool LookAttempted()
    {
        return InputScheme_NEW.StickDeflection(InputScheme_NEW.Stick.Right, RecentreCancelStick) > 0f
            || InputScheme_NEW.MouseDeflection() > RecentreCancelMouse;
    }

    static string LiveCameraName()
    {
        CinemachineBrain brain = CinemachineCore.Instance.BrainCount > 0
            ? CinemachineCore.Instance.GetActiveBrain(0)
            : null;
        ICinemachineCamera live = brain != null ? brain.ActiveVirtualCamera : null;
        return live != null ? live.Name : "none";
    }

    /// <summary>
    /// The speed input for this frame, per <see cref="speedInput"/>.
    ///
    /// Attendant reads the D-pad THROUGH THE PROFILE rather than through the name
    /// "Vertical". On the Vizlab Logitech those happen to be the same axis, but on Xbox
    /// "Vertical" is the left stick — so a build that hard-coded the name would move the
    /// control from the D-pad to a stick a visitor will grab, depending on which pad was
    /// plugged in that morning.
    /// </summary>
    float SpeedAxis()
    {
        switch (speedInput)
        {
            case SpeedInput.Attendant:
                return InputScheme_NEW.DPadY();

            case SpeedInput.Visitor:
                return InputScheme_NEW.StickY(InputScheme_NEW.Stick.Left,
                                              InputScheme_NEW.Deadband());

            default:
                return 0f;
        }
    }
}

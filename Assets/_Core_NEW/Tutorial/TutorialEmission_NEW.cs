using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Phase 2. The one thing that happens to the light rather than around it.
///
/// C1 winds up, C2 waits on A, C3 fires, C4 and C5 are the aftermath. The beats own the
/// gating and the prompts, exactly as in Phase 1 — this owns only what the beats cannot
/// express as a gate: a speed that ramps, a jitter that rises, a course that reverses,
/// and a trail that switches on. Beats call into it from their UnityEvents, so the order
/// of events is visible in the Inspector next to the beat it belongs to.
///
/// THE COURSE REVERSAL is the important part and the reason this component exists.
/// Phase 0–1 flies the light towards the quasar; C4 asks the player to turn around and
/// see "the quasar is already a single bright point", which is only true if the light is
/// now travelling away from it. So the emission flips the heading, and TutorialTravel_NEW
/// takes the look rig's forward axis with it — otherwise A would keep recentring on the
/// direction the player came from, which is the one direction the piece has just spent
/// eight seconds saying they are leaving.
///
/// FOR WHOEVER PICKS THIS UP: the three phases are three public methods, in order.
/// Wire them to C1.onEnter, C2.onSatisfied and C3.onEnter respectively — the builder
/// does that — and tune the serialized values. Nothing here reads a beat id, so the
/// storyboard can be reordered without touching this file.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("EMISSION", "#E06030")]
public class TutorialEmission_NEW : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] TutorialTravel_NEW travel;
    [SerializeField] FirstPersonLookRig_NEW lookRig;
    [SerializeField] TutorialCameraShake_NEW shake;
    [SerializeField] TutorialFlash_NEW flash;

    [Tooltip("The player's photon trail, switched on at the emission. Off before it: the " +
             "light has not left the quasar yet, so it has nothing to leave behind.")]
    [SerializeField] GameObject photonTrail;

    [Header("C1 · spin-up")]
    [Tooltip("Seconds the wind-up takes. Should match C1's beat duration.")]
    [SerializeField] float spinUpSeconds = 12f;

    [Tooltip("Speed at the end of the wind-up, as a multiple of the cruise speed.")]
    [SerializeField] float spinUpSpeedMultiplier = 2.4f;

    [Tooltip("Camera jitter at the end of the wind-up. C2 opens at this and holds.")]
    [Range(0f, 1f)]
    [SerializeField] float spinUpShake = 0.55f;

    [Header("C2 · threshold")]
    [Tooltip("Jitter while the A prompt pulses. The storyboard's near blow-out white.")]
    [Range(0f, 1f)]
    [SerializeField] float thresholdShake = 1f;

    [SerializeField] Color thresholdFlashColor = new Color(1f, 0.97f, 0.92f, 1f);
    [SerializeField] float thresholdFlashHold = 0.02f;
    [SerializeField] float thresholdFlashDecay = 0.9f;

    [Header("C3 · emission")]
    [Tooltip("Speed of the tunnel, as a multiple of the cruise speed.")]
    [SerializeField] float tunnelSpeedMultiplier = 9f;

    [Tooltip("Seconds the tunnel holds before settling towards the cruise speed again.")]
    [SerializeField] float tunnelSeconds = 6f;

    [Tooltip("Speed after it settles, as a multiple of the cruise speed. C4 is " +
             "'speed settles', not 'speed stops'.")]
    [SerializeField] float settledSpeedMultiplier = 1.6f;

    [SerializeField] Color emissionFlashColor = Color.white;
    [SerializeField] float emissionFlashHold = 0.06f;
    [SerializeField] float emissionFlashDecay = 1.4f;

    [Header("Events")]
    [Tooltip("Fires the frame the course reverses. Audio goes here.")]
    [SerializeField] UnityEvent onEmitted = new UnityEvent();

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    float _cruiseSpeed;
    Vector3 _cruiseDirection;

    // Ramp state. One float and one phase enum rather than coroutines, so a reset from
    // attract cannot leave half a sequence running — the same reasoning as the director.
    enum Phase { Idle, SpinUp, Threshold, Tunnel, Settled }

    Phase _phase = Phase.Idle;
    float _elapsed;

    void Awake()
    {
        if (travel == null) travel = GetComponentInParent<TutorialTravel_NEW>();
        if (lookRig == null) lookRig = FindObjectOfType<FirstPersonLookRig_NEW>();
        if (shake == null) shake = FindObjectOfType<TutorialCameraShake_NEW>();
        if (flash == null) flash = FindObjectOfType<TutorialFlash_NEW>();

        if (travel == null)
            Debug.LogError("[TutorialEmission_NEW] No TutorialTravel_NEW. The emission " +
                           "cannot change course.", this);
    }

    void Start()
    {
        // In Start, so TutorialTravel_NEW has already resolved its heading in Awake.
        CaptureCruise();
    }

    // The emission subscribes to its own reset rather than being reset by the attract.
    // TutorialTravel_NEW.ResetToStart puts the heading and speed back, but the trail,
    // the flash and the jitter belong to this component, and a component that leaves
    // its own state behind for somebody else to clean up is how an exhibition build
    // ends up with the second visitor starting mid-emission.

    void OnEnable()
    {
        TutorialDirector_NEW director = FindObjectOfType<TutorialDirector_NEW>();
        if (director != null) director.OnIdleTimeout += ResetEmission;
    }

    void OnDisable()
    {
        TutorialDirector_NEW director = FindObjectOfType<TutorialDirector_NEW>();
        if (director != null) director.OnIdleTimeout -= ResetEmission;
    }

    void CaptureCruise()
    {
        if (travel == null) return;

        _cruiseSpeed = travel.Speed;
        _cruiseDirection = travel.Direction;
    }

    // ── The three calls, in storyboard order ─────────────────────────────────

    /// <summary>C1. The disc accelerates and the frame starts to shake. Wire to C1.onEnter.</summary>
    public void BeginSpinUp()
    {
        CaptureCruise();

        _phase = Phase.SpinUp;
        _elapsed = 0f;

        if (debugLog) Debug.Log("[TutorialEmission_NEW] Spin-up.", this);
    }

    /// <summary>C2. Held at the threshold, waiting on A. Wire to C2.onEnter.</summary>
    public void BeginThreshold()
    {
        _phase = Phase.Threshold;
        _elapsed = 0f;

        if (shake != null) shake.Amplitude = thresholdShake;
        if (flash != null) flash.Flash(thresholdFlashColor, thresholdFlashHold, thresholdFlashDecay);

        if (debugLog) Debug.Log("[TutorialEmission_NEW] Threshold.", this);
    }

    /// <summary>
    /// C3. One white frame, the course reverses, the tunnel opens, the trail lights up.
    /// Wire to C3.onEnter.
    /// </summary>
    public void Emit()
    {
        _phase = Phase.Tunnel;
        _elapsed = 0f;

        if (flash != null) flash.Flash(emissionFlashColor, emissionFlashHold, emissionFlashDecay);

        // Away from the quasar, at tunnel speed. The look rig's forward axis follows.
        if (travel != null) travel.SetCourse(-_cruiseDirection, _cruiseSpeed * tunnelSpeedMultiplier);

        // The player is the light and now it has somewhere to have been.
        if (photonTrail != null) photonTrail.SetActive(true);

        // The jitter belonged to the pressure before the emission, not to the travel
        // after it. C3 is fast, not unstable.
        if (shake != null) shake.Amplitude = 0f;

        onEmitted.Invoke();

        if (debugLog) Debug.Log("[TutorialEmission_NEW] Emitted. Course reversed.", this);
    }

    /// <summary>Back to the pre-emission state. The attract return calls this.</summary>
    public void ResetEmission()
    {
        _phase = Phase.Idle;
        _elapsed = 0f;

        if (shake != null) shake.Amplitude = 0f;
        if (flash != null) flash.Clear();
        if (photonTrail != null) photonTrail.SetActive(false);

        // travel.ResetToStart puts the heading and speed back; this only has to stop
        // driving them.
        CaptureCruise();
    }

    // ── Ramps ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (_phase == Phase.Idle || _phase == Phase.Settled) return;

        float dt = Time.unscaledDeltaTime;
        _elapsed += dt;

        if (_phase == Phase.SpinUp) TickSpinUp();
        else if (_phase == Phase.Tunnel) TickTunnel();
    }

    void TickSpinUp()
    {
        float t = spinUpSeconds > 0f ? Mathf.Clamp01(_elapsed / spinUpSeconds) : 1f;

        // Squared, so most of the acceleration lands late. A linear ramp over twelve
        // seconds is not read as accelerating, it is read as being slightly faster.
        float eased = t * t;

        if (travel != null)
            travel.SetSpeed(Mathf.Lerp(_cruiseSpeed, _cruiseSpeed * spinUpSpeedMultiplier, eased));

        if (shake != null) shake.Amplitude = Mathf.Lerp(0f, spinUpShake, eased);
    }

    void TickTunnel()
    {
        if (_elapsed < tunnelSeconds) return;

        if (travel != null) travel.SetSpeed(_cruiseSpeed * settledSpeedMultiplier);

        _phase = Phase.Settled;

        if (debugLog) Debug.Log("[TutorialEmission_NEW] Tunnel settled.", this);
    }
}

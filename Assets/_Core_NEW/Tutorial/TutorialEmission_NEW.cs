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

    [Header("C1 · arrival and spin-up")]
    [Tooltip("Seconds the arrival takes. Should match C1's beat duration — the player " +
             "reaches the quasar exactly as C2 opens.")]
    [SerializeField] float spinUpSeconds = 12f;

    [Tooltip("How much of the camera's vertical field the quasar spans at the arrival.\n\n" +
             "1 exactly fills it; above 1 overfills, so the quasar runs off every edge and " +
             "there is nothing else on screen when A emits. The distance is worked out " +
             "from this, the quasar's actual size and the camera's actual field of view — " +
             "not typed in — so rescaling the quasar or retuning the FOV cannot silently " +
             "leave the player parked too far away.")]
    [SerializeField] float arrivalScreenFill = 2.2f;

    [Tooltip("Used only when the quasar's size or the camera cannot be read. Distance " +
             "from the quasar's centre.")]
    [SerializeField] float arrivalStandoffFallback = 650f;

    [Tooltip("What the quasar is. Leave empty to take it from the travel destination.")]
    [SerializeField] Transform quasar;

    [Tooltip("Fraction of the approach covered against normalised time. Eases out, so " +
             "the light decelerates into the arrival rather than stopping dead.")]
    [SerializeField] AnimationCurve approachShape = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

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
        if (quasar == null && travel != null) quasar = travel.Destination;
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

    /// <summary>
    /// C1. The light closes the last of the distance and comes to rest at the quasar,
    /// while the disc winds up around it. Wire to C1.onEnter.
    ///
    /// The arrival is posed as "cover whatever is left, in exactly this long" rather
    /// than as a speed, because everything before C1 is player-gated: a visitor who
    /// explores B1 to B4 for two minutes and one who rushes through arrive here from
    /// completely different distances, and both have to reach the quasar as C2 opens.
    /// </summary>
    public void BeginSpinUp()
    {
        CaptureCruise();

        _phase = Phase.SpinUp;
        _elapsed = 0f;

        if (travel != null && quasar != null)
        {
            Vector3 fromQuasar = travel.transform.position - quasar.position;

            // Straight in along the line the light is already on, so the arrival does
            // not slide the view sideways at the moment the player is watching it.
            Vector3 standoffDirection = fromQuasar.sqrMagnitude > 0.0001f
                ? fromQuasar.normalized
                : -travel.Direction;

            travel.ApproachTo(quasar.position + standoffDirection * ArrivalStandoff(),
                              spinUpSeconds, approachShape);
        }
        else
        {
            Debug.LogWarning("[TutorialEmission_NEW] No quasar to arrive at. C1 will wind " +
                             "up but the light will keep cruising past it.", this);
        }

        if (debugLog) Debug.Log("[TutorialEmission_NEW] Arrival and spin-up.", this);
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
        // Taken from where the light actually is rather than from the cruise heading,
        // because the arrival may have come in on a slightly different line.
        if (travel != null)
        {
            Vector3 outward = quasar != null
                ? (travel.transform.position - quasar.position).normalized
                : -_cruiseDirection;

            travel.SetCourse(outward, _cruiseSpeed * tunnelSpeedMultiplier);
        }

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

    /// <summary>
    /// How far from the quasar's centre to stop, so that it spans arrivalScreenFill of
    /// the camera's vertical field.
    ///
    /// A sphere of radius R subtends a half-angle of asin(R/d) from distance d, so for a
    /// wanted half-angle a the distance is R / sin(a). Everything else is reading R and
    /// the field of view off the objects that actually have them rather than trusting a
    /// number somebody typed while the quasar was a different size.
    ///
    /// The wanted half-angle is capped below 90°: past that the camera would be inside
    /// the sphere, which is a different shot and not this one.
    /// </summary>
    float ArrivalStandoff()
    {
        float radius = QuasarRadius();
        float verticalFov = CameraFieldOfView();

        if (radius <= 0f || verticalFov <= 0f) return arrivalStandoffFallback;

        float halfAngle = Mathf.Min(verticalFov * 0.5f * Mathf.Max(0.1f, arrivalScreenFill), 80f);
        float standoff = radius / Mathf.Sin(halfAngle * Mathf.Deg2Rad);

        // Never inside the geometry.
        standoff = Mathf.Max(standoff, radius * 1.05f);

        if (debugLog)
            Debug.Log("[TutorialEmission_NEW] Arrival standoff " + standoff.ToString("F0") +
                      " for a radius of " + radius.ToString("F0") + " at " +
                      verticalFov.ToString("F0") + " degrees of field.", this);

        return standoff;
    }

    /// <summary>The quasar's world radius, from what draws it rather than its scale.</summary>
    float QuasarRadius()
    {
        if (quasar == null) return 0f;

        // Drawn by a QuasarVFX_NEW, the quasar is its disc, and the sphere's renderer — if
        // it is still there — is switched off and no longer the quasar's size.
        QuasarVFX_NEW vfx = quasar.GetComponentInChildren<QuasarVFX_NEW>();
        if (vfx != null && vfx.isActiveAndEnabled) return vfx.Radius;

        Renderer renderer = quasar.GetComponent<Renderer>();
        if (renderer != null) return renderer.bounds.extents.magnitude / Mathf.Sqrt(3f);

        // No renderer: fall back to the transform, assuming a unit-diameter primitive.
        Vector3 scale = quasar.lossyScale;
        return Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z)) * 0.5f;
    }

    float CameraFieldOfView()
    {
        Camera camera = lookRig != null ? lookRig.GetComponent<Camera>() : null;
        if (camera == null) camera = Camera.main;

        return camera != null ? camera.fieldOfView : 0f;
    }

    // ── Ramps ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (_phase == Phase.Idle || _phase == Phase.Settled) return;

        float dt = TutorialClock_NEW.DeltaTime;
        _elapsed += dt;

        if (_phase == Phase.SpinUp) TickSpinUp();
        else if (_phase == Phase.Tunnel) TickTunnel();
    }

    void TickSpinUp()
    {
        float t = spinUpSeconds > 0f ? Mathf.Clamp01(_elapsed / spinUpSeconds) : 1f;

        // Squared, so most of the build lands late. A linear ramp over twelve seconds is
        // not read as building, it is read as being slightly more.
        float eased = t * t;

        // Speed is not touched here — travel is running the arrival and owns it. The
        // pressure the player feels through C1 is the jitter and the quasar filling the
        // frame, not a number going up.
        if (shake != null) shake.Amplitude = Mathf.Lerp(0f, spinUpShake, eased);

        // Arrived. Held here, motionless, until A emits them.
        if (t >= 1f) _phase = Phase.Threshold;
    }

    void TickTunnel()
    {
        if (_elapsed < tunnelSeconds) return;

        if (travel != null) travel.SetSpeed(_cruiseSpeed * settledSpeedMultiplier);

        _phase = Phase.Settled;

        if (debugLog) Debug.Log("[TutorialEmission_NEW] Tunnel settled.", this);
    }
}

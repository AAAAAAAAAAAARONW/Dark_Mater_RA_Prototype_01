using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A guide mote. Drifts, brightens as the player brings it towards the centre of view,
/// and blooms into a ripple once caught.
///
/// A3 (a mote drifts out of frame at the right edge) and B1–B3. GDD §9 lists these as
/// "New, trivial", and they are — but they carry the entire teaching load of Phase 1.
/// They are the only thing on screen, there is no prompt, no legend before B1 and no
/// voiceover, so the mote getting brighter as it approaches the centre of view is the
/// only signal a non-gamer has that the stick is doing something. That proximity ramp
/// is the point of this component; the drift and the bloom are set dressing around it.
///
/// The ramp is continuous rather than a state change at the gate angle. A mote that
/// snaps on at 12° teaches "there is a threshold"; one that rises smoothly from 40°
/// teaches "you are getting warmer", which is what a walk-up visitor needs.
///
/// Nothing here requires art. Scale and an optional Light are driven directly, so a
/// grey sphere works for whitebox, and a ParticleSystem and a UnityEvent are the seams
/// for the real thing.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("MOTE", "#D8C070")]
public class GuideMote_NEW : MonoBehaviour
{
    [Header("Drift")]
    [Tooltip("Direction the mote drifts, in the parent's space.\n\n" +
             "Local rather than world because the motes are parented to the player, who " +
             "is travelling at a constant 25 units a second. A mote pinned in world space " +
             "would be behind the player within four seconds, so B1 would be ungateable — " +
             "the storyboard's 'drifts slowly out of frame at the right edge' is motion " +
             "relative to the light, not to the universe.")]
    [SerializeField] Vector3 driftDirection = Vector3.right;

    [Tooltip("Units per second. Slow: the mote is an invitation, not a target.")]
    [SerializeField] float driftSpeed = 0.4f;

    [Tooltip("Gentle bob so a stationary mote is not a dead pixel.")]
    [SerializeField] float bobAmplitude = 0.08f;

    [SerializeField] float bobFrequency = 0.35f;

    [Header("Proximity ramp")]
    [Tooltip("Leave empty to find the rig on Awake. The ramp needs it; without one the " +
             "mote still drifts but never brightens.")]
    [SerializeField] FirstPersonLookRig_NEW lookRig;

    [Tooltip("Angle from view centre at which the mote is at its dimmest. Wider than " +
             "any gate on purpose — the ramp has to start before the player is close.")]
    [SerializeField] float rampStartAngle = 45f;

    [Tooltip("Angle at which the mote is fully lit.")]
    [SerializeField] float rampEndAngle = 8f;

    [SerializeField] float restScale = 1f;
    [SerializeField] float caughtScale = 1.6f;

    [Tooltip("Optional. Intensity is driven between these two values by the ramp.")]
    [SerializeField] Light moteLight;

    [SerializeField] float restIntensity = 0.6f;
    [SerializeField] float caughtIntensity = 2.4f;

    [Tooltip("Seconds for the ramp to follow the player's aim. Small, or it lags behind " +
             "the stick and stops reading as feedback.")]
    [SerializeField] float rampSmoothing = 0.12f;

    [Header("Bloom")]
    [Tooltip("Optional ripple played once, the first time the mote is caught.")]
    [SerializeField] ParticleSystem ripple;

    [Tooltip("Extra scale kick on the bloom, on top of the ramp.")]
    [SerializeField] float bloomScaleKick = 0.6f;

    [SerializeField] float bloomKickDuration = 0.45f;

    [Tooltip("Fires once, on the first bloom. Audio and VFX go here.")]
    [SerializeField] UnityEvent onBloom = new UnityEvent();

    Vector3 _startPosition;
    Vector3 _localDrift;
    float _ramp;
    float _rampVelocity;
    float _bloomElapsed = -1f;
    bool _bloomed;

    /// <summary>True once the mote has been caught. Never resets during a run.</summary>
    public bool Bloomed { get { return _bloomed; } }

    /// <summary>0 at rampStartAngle or wider, 1 at rampEndAngle or closer.</summary>
    public float Ramp { get { return _ramp; } }

    bool _captured;

    /// <summary>
    /// Record where the mote starts and which way it drifts, once.
    ///
    /// Not simply done in Awake: a mote that a beat switches on later has not run Awake
    /// when the attract reset sweeps the scene, and resetting to an uninitialised
    /// _startPosition would teleport it to the origin. Capturing on first touch from
    /// either path makes the order irrelevant.
    /// </summary>
    void CaptureStart()
    {
        if (_captured) return;

        _startPosition = transform.localPosition;
        _localDrift = driftDirection.sqrMagnitude > 0.0001f ? driftDirection.normalized : Vector3.zero;
        _captured = true;
    }

    void Awake()
    {
        CaptureStart();

        if (lookRig == null) lookRig = FindObjectOfType<FirstPersonLookRig_NEW>();
    }

    void OnEnable()
    {
        // Re-runnable: attract returns the piece to the top, and a mote left mid-drift
        // from the previous visitor would start B1 already off screen.
        ResetMote();
    }

    /// <summary>Put the mote back where it started, unbloomed.</summary>
    public void ResetMote()
    {
        CaptureStart();

        transform.localPosition = _startPosition;

        _ramp = 0f;
        _rampVelocity = 0f;
        _bloomElapsed = -1f;
        _bloomed = false;

        ApplyRamp();
    }

    /// <summary>Catch it. Idempotent — the ripple and the event fire once.</summary>
    public void Bloom()
    {
        if (_bloomed) return;

        _bloomed = true;
        _bloomElapsed = 0f;

        if (ripple != null) ripple.Play();
        onBloom.Invoke();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        Drift(dt);
        TickRamp(dt);
        ApplyRamp();
    }

    void Drift(float dt)
    {
        Vector3 next = transform.localPosition + _localDrift * (driftSpeed * dt);

        if (bobAmplitude > 0f)
        {
            float bob = Mathf.Sin(Time.unscaledTime * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
            next += Vector3.up * (bob * dt);
        }

        transform.localPosition = next;
    }

    void TickRamp(float dt)
    {
        float target = 0f;

        if (lookRig != null)
        {
            Vector3 toMote = transform.position - lookRig.transform.position;

            if (toMote.sqrMagnitude > 0.0001f)
            {
                float angle = Vector3.Angle(lookRig.transform.forward, toMote);
                target = Mathf.InverseLerp(rampStartAngle, rampEndAngle, angle);
            }
            else
            {
                target = 1f;
            }
        }

        _ramp = Mathf.SmoothDamp(_ramp, target, ref _rampVelocity, Mathf.Max(0.0001f, rampSmoothing), Mathf.Infinity, dt);

        if (_bloomElapsed >= 0f && _bloomElapsed < bloomKickDuration) _bloomElapsed += dt;
    }

    void ApplyRamp()
    {
        float scale = Mathf.Lerp(restScale, caughtScale, _ramp) + BloomKick();
        transform.localScale = Vector3.one * scale;

        if (moteLight != null)
            moteLight.intensity = Mathf.Lerp(restIntensity, caughtIntensity, _ramp);
    }

    float BloomKick()
    {
        if (_bloomElapsed < 0f || bloomKickDuration <= 0f) return 0f;

        float t = Mathf.Clamp01(_bloomElapsed / bloomKickDuration);

        // Rise fast, fall slow. One kick, not a pulse — the ripple carries the rest.
        return bloomScaleKick * Mathf.Sin(t * Mathf.PI) * (1f - t * 0.5f);
    }

    void OnDrawGizmosSelected()
    {
        if (!DebugView_NEW.Gizmos) return;

        Gizmos.color = new Color(0.85f, 0.75f, 0.44f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.35f);

        Vector3 drift = transform.parent != null
            ? transform.parent.TransformDirection(driftDirection.normalized)
            : driftDirection.normalized;

        Gizmos.DrawLine(transform.position, transform.position + drift * 4f);
    }
}

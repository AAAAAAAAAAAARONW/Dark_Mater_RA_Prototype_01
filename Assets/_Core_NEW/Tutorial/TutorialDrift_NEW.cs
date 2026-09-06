using UnityEngine;

/// <summary>
/// Carries the player slowly along the flow of the accretion disc.
///
/// The tutorial has no move stick and never will — GDD §4 says the player never
/// translates under their own control, and removing that control is what removes the
/// "why is nothing happening" read from the May 2026 playtest. This is not that. The
/// player is a mote caught in a disc that is itself rotating; A1 describes exactly that,
/// "dark red matter drifts in slow rotation". Being carried is not steering.
///
/// It buys three things that a perfectly still camera cannot have:
///
///   * The photon trail draws. A TrailRenderer with no motion emits no ribbon, so a
///     stationary player is a player with no visible light. The whole premise is that
///     the player IS the light.
///   * Parallax. Near dust moving against the far disc is the only depth cue in a scene
///     with no horizon and no familiar object for scale.
///   * A3 works as written. "A mote drifts out of frame at the right edge" needs
///     relative motion between the player and the mote.
///
/// The motion is an orbit about the quasar's spin axis, so it never carries the player
/// out of the disc no matter how long the attract loop runs at the Observatories. Speed
/// is deliberately at the bottom of what reads as motion at all: this is a drift, and a
/// visitor who notices it as movement is being moved too fast.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("DRIFT", "#40B884")]
public class TutorialDrift_NEW : MonoBehaviour
{
    [Header("Orbit")]
    [Tooltip("Point the drift orbits: the quasar core. Leave empty to orbit the world origin.")]
    [SerializeField] Transform centre;

    [Tooltip("The disc's spin axis. The orbit is in the plane perpendicular to this.")]
    [SerializeField] Vector3 spinAxis = Vector3.forward;

    [Tooltip("Degrees per second around the axis. Small: this is a drift, not a ride.")]
    [SerializeField] float degreesPerSecond = 1.6f;

    [Header("Along the axis")]
    [Tooltip("Units per second along the spin axis, towards the jet. 0 for a flat orbit.")]
    [SerializeField] float axialSpeed = 0.35f;

    [Header("Wobble")]
    [Tooltip("Amplitude of a slow sideways wobble, so the trail is a curve rather than " +
             "a perfectly regular arc. 0 disables it.")]
    [SerializeField] float wobbleAmplitude = 0.5f;

    [SerializeField] float wobblePeriod = 11f;

    [Header("Debug")]
    [SerializeField] bool drawGizmo = true;

    Vector3 _centre;
    Vector3 _axis;
    float _phase;

    /// <summary>Metres per second the player is actually moving, for the trail to size against.</summary>
    public float CurrentSpeed { get; private set; }

    void Awake()
    {
        _centre = centre != null ? centre.position : Vector3.zero;

        _axis = spinAxis.sqrMagnitude > 0.0001f ? spinAxis.normalized : Vector3.forward;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        Vector3 before = transform.position;

        // Orbit: rotate the offset from the centre about the axis.
        Vector3 offset = transform.position - _centre;
        offset = Quaternion.AngleAxis(degreesPerSecond * dt, _axis) * offset;

        Vector3 next = _centre + offset;

        // Rise along the axis, towards the jet.
        next += _axis * (axialSpeed * dt);

        // Wobble across the orbit, so the ribbon is not a machine-perfect arc.
        if (wobbleAmplitude > 0f && wobblePeriod > 0f)
        {
            _phase += dt / wobblePeriod * Mathf.PI * 2f;

            Vector3 radial = offset.sqrMagnitude > 0.0001f
                ? Vector3.ProjectOnPlane(offset, _axis).normalized
                : Vector3.right;

            next += radial * (Mathf.Sin(_phase) * wobbleAmplitude * dt);
        }

        transform.position = next;

        CurrentSpeed = dt > 0f ? Vector3.Distance(before, next) / dt : 0f;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmo || !DebugView_NEW.Gizmos) return;

        Vector3 c = Application.isPlaying
            ? _centre
            : (centre != null ? centre.position : Vector3.zero);

        Vector3 a = spinAxis.sqrMagnitude > 0.0001f ? spinAxis.normalized : Vector3.forward;

        Gizmos.color = new Color(0.25f, 0.72f, 0.52f, 0.8f);
        Gizmos.DrawLine(c - a * 30f, c + a * 30f);
        Gizmos.DrawLine(c, transform.position);
    }
}

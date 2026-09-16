using UnityEngine;

/// <summary>
/// Makes the travel readable. Near-field matter streaking past the light.
///
/// The problem this solves: at 25 units a second in a scene whose nearest object is 40
/// units away and whose next one is 6000, nothing on screen changes fast enough to read
/// as movement. The player is travelling and it looks like they are parked. Speed is not
/// a property of the mover, it is a property of what goes past — a jet at cruise looks
/// stationary until the ground is close enough to blur.
///
/// So this is deliberately the near field, and it works differently from the dust:
///
///   Dust     emitter parented to the light, particles simulated in WORLD space. New
///            ones keep spawning around the player while the old ones stay put and get
///            left behind. That is real parallax, and it is what sells distance.
///   Streaks  emitter parented to the light, particles simulated in LOCAL space and
///            given a velocity straight backwards at the travel speed. Because they
///            carry a real velocity, the renderer's Stretch mode can draw them as
///            streaks along it — which world-space particles cannot do, since standing
///            still while the camera moves is a velocity of zero.
///
/// Both layers together read as depth. Either alone reads as a screensaver.
///
/// The velocity is taken from TutorialTravel_NEW rather than typed in twice. Streaks
/// that disagree with the actual speed are worse than no streaks: they look like the
/// world is sliding under a stationary player.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ParticleSystem))]
[HierarchyBadge_NEW("STREAKS", "#6FA8DC")]
public class TutorialSpeedStreaks_NEW : MonoBehaviour
{
    [Header("Wiring")]
    [Tooltip("Where the speed comes from. Leave empty to find it on a parent.")]
    [SerializeField] TutorialTravel_NEW travel;

    [Header("Feel")]
    [Tooltip("Streak velocity as a multiple of the travel speed. 1 means the streaks " +
             "move backwards exactly as fast as the light moves forwards, which is what " +
             "stationary matter would do. Above 1 exaggerates.")]
    [SerializeField] float speedMultiplier = 1f;

    [Tooltip("Fallback when there is no TutorialTravel_NEW to read.")]
    [SerializeField] float fallbackSpeed = 25f;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    ParticleSystem _particles;

    float _appliedSpeed = -1f;
    Vector3 _appliedDirection = Vector3.zero;

    void Awake()
    {
        _particles = GetComponent<ParticleSystem>();

        if (travel == null) travel = GetComponentInParent<TutorialTravel_NEW>();

        Apply();
    }

    /// <summary>
    /// Follow the speed as it changes.
    ///
    /// The speed is no longer constant: C1 decelerates into the arrival, C2 holds at a
    /// standstill, C3 opens the tunnel. Streaks that keep running at the cruise rate
    /// through all of that are worse than none — the player would be stopped at the
    /// quasar with the medium still tearing past.
    /// </summary>
    void Update()
    {
        Apply();
    }

    /// <summary>Re-read the travel speed and set the streak velocity from it.</summary>
    public void Apply()
    {
        if (_particles == null) return;

        // CurrentSpeed, not Speed: during the arrival the speed field is not what the
        // transform is actually doing.
        float speed = travel != null ? travel.CurrentSpeed : fallbackSpeed;
        float streakSpeed = speed * speedMultiplier;

        // THE COURSE, NOT +Z. This used to write a fixed velocity of -Z on the reasoning
        // that "the light's forward is +Z", and the light has no forward: the player
        // transform never rotates, travel only ever writes position. So local -Z is
        // world -Z for the whole piece, no matter which way the light is actually going.
        //
        // That held until C3. The emission reverses the course, and from that moment the
        // streaks were running the same way as the light instead of past it — matter
        // streaming forwards alongside a photon, which reads as the whole medium having
        // been thrown into reverse at the one moment the player is meant to feel launched.
        //
        // Taken from the direction of travel it is right on both sides of the reversal,
        // and stays right if the course is ever changed again.
        Vector3 course = travel != null && travel.Direction.sqrMagnitude > 0.0001f
            ? travel.Direction.normalized
            : Vector3.forward;

        // Particle modules are not free to touch every frame, and the eye cannot see a
        // fraction of a unit per second — or a fraction of a degree of heading — either way.
        bool speedSame = Mathf.Abs(streakSpeed - _appliedSpeed) < 0.05f;
        bool courseSame = Vector3.Dot(course, _appliedDirection) > 0.9999f;

        if (speedSame && courseSame) return;

        _appliedSpeed = streakSpeed;
        _appliedDirection = course;

        // Backwards along the course, in world space. World rather than Local because the
        // vector is a world heading — and because writing it in the emitter's local axes
        // would be relying on the parent never rotating all over again.
        Vector3 v = -course * streakSpeed;

        ParticleSystem.VelocityOverLifetimeModule velocity = _particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(v.x);
        velocity.y = new ParticleSystem.MinMaxCurve(v.y);
        velocity.z = new ParticleSystem.MinMaxCurve(v.z);

        if (debugLog)
            Debug.Log("[TutorialSpeedStreaks_NEW] Streaks at " + streakSpeed + " u/s along " +
                      (-course) + ".", this);
    }

    void OnValidate()
    {
        if (Application.isPlaying) Apply();
    }
}

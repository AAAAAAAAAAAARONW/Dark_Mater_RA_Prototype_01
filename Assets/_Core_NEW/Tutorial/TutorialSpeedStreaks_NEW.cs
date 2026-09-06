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

    void Awake()
    {
        _particles = GetComponent<ParticleSystem>();

        if (travel == null) travel = GetComponentInParent<TutorialTravel_NEW>();

        Apply();
    }

    /// <summary>Re-read the travel speed and set the streak velocity from it.</summary>
    public void Apply()
    {
        if (_particles == null) return;

        float speed = travel != null ? travel.Speed : fallbackSpeed;
        float streakSpeed = speed * speedMultiplier;

        // Local space, straight back down the travel axis. The emitter is parented to
        // the light and the light's forward is +Z, so backwards is -Z.
        ParticleSystem.VelocityOverLifetimeModule velocity = _particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.z = new ParticleSystem.MinMaxCurve(-streakSpeed);

        if (debugLog)
            Debug.Log("[TutorialSpeedStreaks_NEW] Streaks at " + streakSpeed + " u/s.", this);
    }

    void OnValidate()
    {
        if (Application.isPlaying) Apply();
    }
}

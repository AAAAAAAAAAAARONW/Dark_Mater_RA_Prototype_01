using UnityEngine;

/// <summary>
/// Carries the player in a straight line at a constant speed. No input reaches it.
///
/// This is the premise of the whole piece stated as a component: you are light, you are
/// already moving, and you cannot steer. GDD §3 lists "You cannot steer. You can only
/// look." as the second of the six things the tutorial installs, and §4 removes the move
/// stick entirely so that lesson is enforced by the absence of a control rather than by
/// a line of text.
///
/// So there is deliberately no path from input to this component, and no serialized
/// "allow player control" flag to find later and switch on. The heading is set by
/// pointing `destination` at the thing the light is travelling towards.
///
/// SetCourse and SetSpeed exist for scripted events — the emission at C3 turns the
/// light around, and C1 winds the speed up first. Those are things happening TO the
/// player, which is a different claim from the player steering, and the distinction
/// survives as long as nothing wires a control to them.
///
/// It replaces an earlier TutorialDrift_NEW that orbited the quasar. That was written
/// for a staging where the player starts inside the accretion disc; the experience is an
/// approach, so the motion is a straight line towards the quasar and the orbit is gone.
///
/// The trail cares about this. A TrailRenderer emits nothing while its transform is
/// still, so constant motion is also what makes the player's own light visible at all
/// once the trail is switched on.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("TRAVEL", "#40B884")]
public class TutorialTravel_NEW : MonoBehaviour
{
    [Header("Heading")]
    [Tooltip("What the light is travelling towards. The heading is taken from this once, " +
             "on Awake, so a moving destination does not drag the player around. Scripted " +
             "events change it through SetCourse; nothing the player does can.")]
    [SerializeField] Transform destination;

    [Tooltip("Used when there is no destination. World space, normalised on Awake.")]
    [SerializeField] Vector3 fallbackDirection = Vector3.forward;

    [Header("Speed")]
    [Tooltip("Units per second. There is no speed control in the tutorial; only scripted " +
             "events change this, through SetSpeed.")]
    [SerializeField] float speed = 25f;

    [Header("Camera")]
    [Tooltip("Point the look rig's forward axis along the heading on Awake, so A " +
             "recentres to the direction of travel rather than to whatever rotation the " +
             "camera happened to be built with.")]
    [SerializeField] FirstPersonLookRig_NEW lookRig;

    [Header("Debug")]
    [SerializeField] bool drawGizmo = true;

    Vector3 _direction;
    Vector3 _startPosition;
    float _startSpeed;

    /// <summary>The current world heading. Also the axis A recentres to.</summary>
    public Vector3 Direction { get { return _direction; } }

    /// <summary>Units per second right now.</summary>
    public float Speed { get { return speed; } }

    /// <summary>Metres travelled since the start of the run.</summary>
    public float DistanceTravelled
    {
        get { return Vector3.Distance(_startPosition, transform.position); }
    }

    /// <summary>
    /// Put the light back at the start of its run.
    ///
    /// The attract return calls this. Without it the second visitor of the day starts
    /// wherever the first one got to — which, at 25 units a second through a 90 second
    /// idle timeout, is a long way past the quasar.
    /// </summary>
    public void ResetToStart()
    {
        transform.position = _startPosition;

        _direction = ResolveDirection();
        speed = _startSpeed;

        if (lookRig != null) lookRig.SetForwardAxis(_direction);
    }

    /// <summary>
    /// Change the heading and the speed.
    ///
    /// This is the one crack in "the heading never changes", and it is deliberate: the
    /// emission at C3 turns the light around and flings it away from the quasar, and
    /// that is a scripted event, not the player steering. The distinction the GDD cares
    /// about is preserved — nothing here reads input, and there is no path from a stick
    /// or a button to this method. TutorialEmission_NEW is the only caller.
    ///
    /// The look rig's forward axis follows, so A keeps recentring on the direction of
    /// travel rather than on whatever forward used to mean.
    /// </summary>
    public void SetCourse(Vector3 direction, float newSpeed)
    {
        if (direction.sqrMagnitude > 0.0001f)
        {
            _direction = direction.normalized;
            if (lookRig != null) lookRig.SetForwardAxis(_direction);
        }

        speed = Mathf.Max(0f, newSpeed);
    }

    /// <summary>Change speed without touching the heading. C1's spin-up uses this.</summary>
    public void SetSpeed(float newSpeed)
    {
        speed = Mathf.Max(0f, newSpeed);
    }

    void Awake()
    {
        _startPosition = transform.position;
        _startSpeed = speed;
        _direction = ResolveDirection();

        if (lookRig == null) lookRig = GetComponentInChildren<FirstPersonLookRig_NEW>();
    }

    /// <summary>
    /// Hand the heading to the look rig in Start, not Awake.
    ///
    /// The rig reads its own forward axis out of its transform rotation during its Awake,
    /// and Unity gives no ordering between Awakes on different objects — the rig is on
    /// the camera, this is on the player. Setting the axis from Awake would work or not
    /// depending on which ran second. Start always runs after every Awake.
    /// </summary>
    void Start()
    {
        if (lookRig != null) lookRig.SetForwardAxis(_direction);
    }

    void Update()
    {
        // Unscaled, so a future slow-motion beat (D2 runs at 0.2x) slows the world
        // without also stopping the light that the whole piece says cannot stop.
        transform.position += _direction * (speed * Time.unscaledDeltaTime);
    }

    Vector3 ResolveDirection()
    {
        if (destination != null)
        {
            Vector3 toDestination = destination.position - transform.position;
            if (toDestination.sqrMagnitude > 0.0001f) return toDestination.normalized;

            Debug.LogWarning("[TutorialTravel_NEW] The destination is at the player's own " +
                             "position. Falling back to the explicit direction.", this);
        }

        return fallbackDirection.sqrMagnitude > 0.0001f
            ? fallbackDirection.normalized
            : Vector3.forward;
    }

    /// <summary>
    /// One line confirming the light is actually going where it should.
    ///
    /// "Is the player still moving forward?" is a question you cannot answer by looking
    /// at a starfield — everything is far away and nothing has a known size. The angle
    /// between the heading and the direction to the quasar is the answer: 0 means dead
    /// on, and through Phase 0-1 it should stay at 0. Phase 2's emission reverses the
    /// course on purpose, so from C3 onwards this reads about 180.
    /// </summary>
    void OnGUI()
    {
        if (!DebugView_NEW.Overlay) return;

        float offAxis = 0f;
        float remaining = 0f;

        if (destination != null)
        {
            Vector3 toDestination = destination.position - transform.position;

            remaining = toDestination.magnitude;
            if (remaining > 0.001f) offAxis = Vector3.Angle(_direction, toDestination);
        }

        GUI.Label(new Rect(10f, 70f, 900f, 22f),
                  string.Format("TRAVEL  {0:F1} u/s   travelled {1:F0}   to quasar {2:F0}   off-axis {3:F2}deg",
                                speed, DistanceTravelled, remaining, offAxis));
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmo || !DebugView_NEW.Gizmos) return;

        Vector3 direction = Application.isPlaying ? _direction : ResolveDirection();

        Gizmos.color = new Color(0.25f, 0.72f, 0.52f, 0.9f);
        Gizmos.DrawLine(transform.position, transform.position + direction * (speed * 4f));
    }
}

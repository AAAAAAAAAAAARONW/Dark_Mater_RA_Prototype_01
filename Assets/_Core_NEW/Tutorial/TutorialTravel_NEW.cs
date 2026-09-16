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

    [Tooltip("Never cruise closer to the destination than this. Phase 1 is player-gated, " +
             "so without it a visitor who takes their time flies through the quasar before " +
             "the beat that is meant to bring them to it. The C1 approach ignores it.")]
    [SerializeField] float holdDistance = 900f;

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
    float _currentSpeed;

    // Arrival state. See ApproachTo.
    bool _approaching;
    Vector3 _approachFrom;
    Vector3 _approachTo;
    float _approachSeconds;
    float _approachElapsed;
    AnimationCurve _approachShape;

    /// <summary>The current world heading. Also the axis A recentres to.</summary>
    public Vector3 Direction { get { return _direction; } }

    /// <summary>What the light is travelling towards. The quasar, in the tutorial.</summary>
    public Transform Destination { get { return destination; } }

    /// <summary>The cruise speed setting. Zero while halted at the quasar.</summary>
    public float Speed { get { return speed; } }

    /// <summary>
    /// How fast the light is actually moving this frame, arrival included.
    /// TutorialSpeedStreaks_NEW wants this one — during C1 the speed field is not
    /// what the transform is doing.
    /// </summary>
    public float CurrentSpeed { get { return _currentSpeed; } }

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
        _approaching = false;

        // The next visitor's cruise gets the hold back. Without this, a restart taken
        // after C1 would leave the light free to fly straight through the quasar.
        _insideHold = false;

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
        // AN APPROACH IN PROGRESS HAS TO STOP HERE. A scripted approach owns the
        // position outright — Update runs TickApproach instead of the cruise, so it
        // ignores both direction and speed — and this method is a statement that the
        // light is now going a particular way at a particular rate. The two cannot both
        // be true, and until this line the approach quietly won.
        //
        // C3's emission is the case that matters: it reverses the course while C1's
        // arrival approach may still be running. In an unbroken playthrough C1's twelve
        // seconds always outlast the approach, so the approach has already halted and
        // nothing goes wrong. Replay C1 and C3 in the same frame — which is what a debug
        // jump into Phase 3 does — and the approach is still live, so the reversal was
        // discarded and the light was dragged back towards the quasar and then halted
        // on arrival. It read as being bounced off the atom.
        //
        // Not a jump bug. The jump only removed the twelve seconds that were hiding it.
        CancelApproach();

        if (direction.sqrMagnitude > 0.0001f)
        {
            _direction = direction.normalized;
            if (lookRig != null) lookRig.SetForwardAxis(_direction);
        }

        speed = Mathf.Max(0f, newSpeed);
    }

    /// <summary>
    /// Drop a scripted approach without stopping the light, unlike Halt.
    ///
    /// Halt means "you have arrived and you are standing still". This means "something
    /// else is driving now", which is what both SetCourse and SetSpeed are saying.
    /// </summary>
    public void CancelApproach()
    {
        _approaching = false;
    }

    /// <summary>Change speed without touching the heading. C1's spin-up uses this.</summary>
    public void SetSpeed(float newSpeed)
    {
        // Same reasoning as SetCourse: setting a speed while an approach is driving the
        // position would set a number nothing reads.
        CancelApproach();

        speed = Mathf.Max(0f, newSpeed);
    }

    /// <summary>True while the arrival is running, and false again once it has landed.</summary>
    public bool IsApproaching { get { return _approaching; } }

    /// <summary>
    /// Cover the remaining distance to a point in exactly this many seconds, ending at
    /// rest.
    ///
    /// C1 uses this to land the player at the quasar just as C2 opens. It has to be a
    /// distance problem solved over a duration, not a speed set in advance, because the
    /// beats before it are player-gated: a visitor who explores for two minutes and one
    /// who rushes arrive at C1 from completely different distances, and both have to
    /// reach the quasar at the same moment in the piece.
    ///
    /// Travel owns it rather than the emission, because travel owns the transform, and
    /// two components moving one object is how positions start fighting.
    ///
    /// The shape curve maps normalised time to fraction of the distance covered. The
    /// default eases out, so the light decelerates into the arrival rather than
    /// stopping dead.
    /// </summary>
    public void ApproachTo(Vector3 target, float seconds, AnimationCurve shape)
    {
        _approachFrom = transform.position;
        _approachTo = target;
        _approachSeconds = Mathf.Max(0.01f, seconds);
        _approachShape = shape;
        _approachElapsed = 0f;
        _approaching = true;
    }

    /// <summary>Stop where you are. The arrival ends in this state and C2 holds it.</summary>
    public void Halt()
    {
        _approaching = false;
        speed = 0f;
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
        float dt = TutorialClock_NEW.DeltaTime;
        Vector3 before = transform.position;

        // The world clock: unscaled, so D2's slow motion slows the world without also
        // stopping the light the whole piece says cannot stop — but zero while paused,
        // the one moment the light is allowed to stand still. See TutorialClock_NEW.
        if (_approaching) TickApproach(dt);
        else transform.position += _direction * (speed * dt);

        HoldOffTheDestination();

        _currentSpeed = dt > 0f ? Vector3.Distance(before, transform.position) / dt : 0f;
    }

    void TickApproach(float dt)
    {
        _approachElapsed += dt;

        float t = Mathf.Clamp01(_approachElapsed / _approachSeconds);
        float covered = _approachShape != null ? _approachShape.Evaluate(t) : t;

        transform.position = Vector3.LerpUnclamped(_approachFrom, _approachTo, covered);

        // Noted every frame the approach is inside the hold, not just at the end, so a
        // CancelApproach part way in leaves the cruise free of it too. See
        // HoldOffTheDestination for what this is protecting against.
        if (destination != null)
            _insideHold = Vector3.Distance(transform.position, destination.position) < holdDistance;

        if (t < 1f) return;

        transform.position = _approachTo;
        Halt();
    }

    /// <summary>
    /// Do not sail past, or into, the destination during the cruise.
    ///
    /// Phase 1 is player-gated, so a visitor who takes their time would otherwise fly
    /// straight through the quasar before the beat that is supposed to bring them to it.
    /// Holding at a distance leaves C1 something to close and leaves the quasar the
    /// right size in the meantime.
    ///
    /// The approach ignores this, because the approach is what takes the player inside
    /// the hold distance on purpose.
    /// </summary>
    /// <summary>
    /// True once an approach has deliberately taken the light inside the hold distance.
    /// The hold stops applying until it is back outside again.
    /// </summary>
    bool _insideHold;

    void HoldOffTheDestination()
    {
        if (_approaching || destination == null || holdDistance <= 0f) return;

        Vector3 toDestination = destination.position - transform.position;
        float distance = toDestination.magnitude;

        if (distance >= holdDistance)
        {
            _insideHold = false;
            return;
        }

        // THE HOLD IS A RULE ABOUT THE CRUISE, and C1's approach is the thing that is
        // supposed to go inside it. Without this it applied to the arrival too, and the
        // effect was a jump cut at the worst possible moment:
        //
        // TickApproach lands the light on the standoff and calls Halt, which clears
        // _approaching — and then this runs in the SAME Update, sees the light 650 units
        // from a quasar it is not allowed within 900 of, and teleports it back out. So
        // the player watched the quasar grow until it filled the frame, and on the frame
        // it arrived the view snapped back to a smaller quasar with A TO EMIT over it.
        // The prompt looked like it belonged to an earlier moment because the picture
        // behind it had been thrown back to one.
        //
        // The same jump waited at the other end: C3 reverses the course from inside the
        // hold, and the first frames of the launch would have been spent being shoved
        // back out to 900.
        if (_insideHold) return;

        transform.position = destination.position - toDestination.normalized * holdDistance;
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

        GUI.Label(DebugOverlayRows_NEW.Row(DebugOverlayRows_NEW.Travel),
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

using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A single hydrogen atom, closing on the light. D1 and D2.
///
/// GDD §6 gives D1 as "A single small glowing particle ahead. No label. The only moving
/// thing in frame", gated on "the atom closes on its own", and D2 as the contact. This
/// is both halves: the approach and the moment it lands.
///
/// ONE ATOM, NOT A CLOUD, and the reason is the whole of Phase 3. §3 items 3 and 4 —
/// hitting hydrogen costs you colour, and the colour you lost is that black line — have
/// to be readable from a single collision. A cloud gives the player several causes at
/// once and no way to pair any one of them with an effect, which is the same failure
/// the May 2026 playtest recorded against the Lyman-alpha forest graph: it arrived
/// already populated, and 4 of 5 players could not read it.
///
/// IT CLOSES ON THE PLAYER RATHER THAN THE PLAYER FLYING INTO IT. Those look identical
/// on screen and are very different to build. The light's course is set by
/// TutorialTravel_NEW and changes twice before Phase 3 — C3 reverses it — so an atom
/// parked at a world position would need to know where the course ended up. Closing on
/// the player works from wherever the light actually is, which also means D1 survives
/// somebody retuning Phase 2.
///
/// It does NOT let the player steer into it or away from it. §3 item 2 is "You cannot
/// steer. You can only look", and the approach is on a fixed timer from Arm(): looking
/// away does not slow it down and looking at it does not speed it up. What looking does
/// is decide whether the player sees the impact, which is the only thing D2 asks of them.
///
/// Nothing here requires art. Scale and an optional Light are driven directly, so a
/// grey sphere works for whitebox; onImpact is the seam for the real thing.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("ATOM", "#8FD4E8")]
public class TutorialAtom_NEW : MonoBehaviour
{
    [Header("Approach")]
    [Tooltip("Where the atom starts, in metres ahead of the light along its course. Far " +
             "enough that it arrives as a point and grows, which is what makes it read " +
             "as approaching rather than as appearing.")]
    [SerializeField] float spawnDistance = 220f;

    [Tooltip("Seconds from Arm() to impact. D1 is 0:00-0:10 in the storyboard, so ten " +
             "seconds of the atom being the only moving thing in frame.\n\n" +
             "A duration rather than a speed, because the beat's length and the arrival " +
             "have to agree and only one of them should be authored twice.")]
    [SerializeField] float approachSeconds = 10f;

    [Tooltip("Metres from the camera at which contact fires. Not a physics collision — " +
             "a trigger would need a collider on the light and a rigidbody somewhere, " +
             "for an event that is a distance test.")]
    [SerializeField] float impactRadius = 1.5f;

    [Tooltip("Sideways and vertical offset at spawn, in metres, measured across the " +
             "course rather than in world axes.\n\n" +
             "Zero for D1, whose single atom comes straight down the middle. D4's group " +
             "spreads its atoms with this so they arrive as separate things in different " +
             "parts of the frame rather than as one atom flickering.\n\n" +
             "It offsets where the atom STARTS. The approach still ends at the light, so " +
             "every atom is still a hit — §6 asks for a group the player passes through, " +
             "not for a group they could miss.")]
    [SerializeField] Vector2 spawnSpread = Vector2.zero;

    [Header("Look")]
    [Tooltip("Scale at spawn and at impact. The growth is what sells the approach in a " +
             "scene with no other near-field reference.")]
    [SerializeField] float scaleAtSpawn = 0.25f;

    [SerializeField] float scaleAtImpact = 2.4f;

    [Tooltip("Optional. Brightness follows the same curve as the scale.")]
    [SerializeField] Light glow;

    [SerializeField] float glowAtSpawn = 0.4f;
    [SerializeField] float glowAtImpact = 6f;

    [Tooltip("Additive quad that turns the core into a point of light. Kept facing the " +
             "camera every frame — a flat quad seen edge on disappears, and the player " +
             "can look at this from any angle.\n\n" +
             "It is what makes the atom readable at 200 metres, where the sphere itself " +
             "is two pixels of flat colour.")]
    [SerializeField] Transform halo;

    [Tooltip("Halo size relative to the core, at spawn and at impact. Larger than the " +
             "core throughout: the glow is the thing that is seen and the core is the " +
             "thing inside it.")]
    [SerializeField] float haloAtSpawn = 5f;

    [SerializeField] float haloAtImpact = 3.2f;

    [Tooltip("Depth of the brightness flutter, 0 to 1. Small. A point of light that is " +
             "perfectly steady reads as a UI element; one that breathes slightly reads " +
             "as something physical a long way off.")]
    [Range(0f, 1f)]
    [SerializeField] float shimmer = 0.12f;

    [SerializeField] float shimmerSpeed = 2.7f;

    [Tooltip("Shape of the approach. Linear reads as mechanical; easing in makes the " +
             "last second feel like arrival.")]
    [SerializeField] AnimationCurve approachShape = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Wiring")]
    [Tooltip("Leave empty to use the main camera. The atom closes on this, and impact " +
             "is measured from it.")]
    [SerializeField] Transform target;

    [Tooltip("Leave empty to find it. Only its heading is read, so the atom starts ahead " +
             "on the light's actual course rather than wherever the player is looking.")]
    [SerializeField] TutorialTravel_NEW travel;

    [Header("Events")]
    [Tooltip("Fires once, on contact. D2's spectrum line, flash and impact sound hang " +
             "here — the atom does not know what any of them are.")]
    [SerializeField] UnityEvent onImpact = new UnityEvent();

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    bool _armed;
    bool _hit;
    float _elapsed;
    Vector3 _heading;
    Vector3 _right;
    Vector3 _up;

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>True once contact has fired. D1's gate reads this.</summary>
    public bool HasHit { get { return _hit; } }

    /// <summary>Approach progress, 0 at spawn and 1 at contact.</summary>
    public float Progress
    {
        get { return approachSeconds <= 0f ? 1f : Mathf.Clamp01(_elapsed / approachSeconds); }
    }

    /// <summary>Metres still to run. Read by the debug overlay.</summary>
    public float Distance
    {
        get
        {
            if (target == null) return 0f;
            return Vector3.Distance(transform.position, target.position);
        }
    }

    /// <summary>
    /// Place the atom ahead on the light's course and start it closing. D1's onEnter
    /// calls this.
    /// </summary>
    public void Arm()
    {
        Resolve();

        if (target == null)
        {
            Debug.LogError("[TutorialAtom_NEW] No target and no main camera. The atom " +
                           "cannot close and D1 can never be satisfied.", this);
            return;
        }

        _armed = true;
        _hit = false;
        _elapsed = 0f;

        // Across the course, not in world axes, so a spread stays a spread after C3
        // reverses the heading. Cached, so it cannot rotate under the atom mid-flight.
        _heading = Heading();
        _right = Vector3.Cross(Vector3.up, _heading).normalized;
        if (_right.sqrMagnitude < 0.0001f) _right = Vector3.right;
        _up = Vector3.Cross(_heading, _right).normalized;

        Place(0f);

        Draw(0f);
        gameObject.SetActive(true);

        if (debugLog)
            Debug.Log("[TutorialAtom_NEW] Armed " + spawnDistance.ToString("F0") +
                      "m out, arriving in " + approachSeconds.ToString("F1") + "s.", this);
    }

    /// <summary>
    /// Back to before D1: not armed, not hit, off screen.
    ///
    /// GDD §5's restart has to be total, and an atom left mid-flight is exactly the kind
    /// of thing the second visitor of the day would see.
    /// </summary>
    public void ResetForAttract()
    {
        _armed = false;
        _hit = false;
        _elapsed = 0f;

        gameObject.SetActive(false);
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        Resolve();

        // Nothing is on screen before D1. A single glowing particle visible during
        // Phase 2 reads as a stray, and Phase 3 opens by saying "here is the first
        // thing you have met".
        //
        // THE GUARD IS LOAD-BEARING. The builder saves this object inactive, so Awake
        // has never run by the time D1 opens — and the thing that finally runs it is
        // Arm()'s own SetActive(true). Hiding unconditionally here therefore switched
        // the atom off from inside the call that had just switched it on: _armed stayed
        // true, the object stayed inactive, Update never ran, and D1 played out with
        // nothing in frame while the debug overlay showed no atom row at all.
        if (!_armed) gameObject.SetActive(false);
    }

    void Resolve()
    {
        if (target == null && Camera.main != null) target = Camera.main.transform;
        if (travel == null) travel = FindObjectOfType<TutorialTravel_NEW>();
    }

    /// <summary>
    /// Which way "ahead" is. The light's course, not the player's view.
    ///
    /// Reading the look direction here would let the player put the atom wherever they
    /// were pointing when D1 opened, which is steering by another name.
    /// </summary>
    Vector3 Heading()
    {
        if (travel != null && travel.Direction.sqrMagnitude > 0.0001f)
            return travel.Direction.normalized;

        return target != null ? target.forward : Vector3.forward;
    }

    void Update()
    {
        if (!_armed || _hit || target == null) return;

        // Unscaled, like everything else in the tutorial. D2 drops the time scale on
        // impact, and an atom that slowed down with it would still be arriving.
        _elapsed += Time.unscaledDeltaTime;

        float t = Progress;
        float shaped = approachShape != null ? approachShape.Evaluate(t) : t;

        Place(shaped);
        Draw(shaped);

        if (t < 1f && Distance > impactRadius) return;

        Hit();
    }

    /// <summary>
    /// Put the atom where it should be for a progress of 0 to 1.
    ///
    /// MEASURED FROM THE LIGHT, NOT FROM A FIXED POINT IN SPACE, and that is the whole
    /// of it. The first version lerped from a world position captured at Arm() towards
    /// the camera, which closes only if the camera is not going anywhere. The light is
    /// travelling — fast, after the emission — so it outran the lerp: the atom fell
    /// further behind for most of the approach and then snapped onto the player at the
    /// end. Depending on the speed it either receded the whole way or was dragged into
    /// reach by something else moving the player.
    ///
    /// So the atom holds a distance AHEAD OF THE LIGHT that runs down to zero. It closes
    /// at the authored rate whatever the player's speed does, which is what "the atom
    /// closes on its own" has to mean in a scene where the player never stops.
    ///
    /// The lateral spread shrinks in proportion to the remaining distance, which is a
    /// constant angular offset: the atom sits at a fixed spot in the frame and grows,
    /// the way an object on a collision course actually looks. For D4's group that gives
    /// four points in four parts of the frame, each swelling in place, all arriving at
    /// the light.
    /// </summary>
    void Place(float shaped)
    {
        if (target == null) return;

        float remaining = Mathf.Lerp(spawnDistance, 0f, shaped);
        float lateral = spawnDistance > 0.001f ? remaining / spawnDistance : 0f;

        transform.position = target.position
                             + _heading * remaining
                             + (_right * spawnSpread.x + _up * spawnSpread.y) * lateral;
    }

    void Hit()
    {
        _hit = true;
        _armed = false;

        Draw(1f);

        if (debugLog) Debug.Log("[TutorialAtom_NEW] Impact.", this);

        onImpact.Invoke();
    }

    void Draw(float t)
    {
        // The flutter is on brightness only, never on position: a point of light that
        // wanders is a point of light the player will try to track with the stick, and
        // §3 item 2 is that they cannot steer.
        float flutter = 1f + shimmer * Mathf.Sin(Time.unscaledTime * shimmerSpeed);

        transform.localScale = Vector3.one * Mathf.Lerp(scaleAtSpawn, scaleAtImpact, t);

        if (glow != null) glow.intensity = Mathf.Lerp(glowAtSpawn, glowAtImpact, t) * flutter;

        if (halo == null) return;

        // Local scale, so it rides the core's growth and this is the ratio on top.
        halo.localScale = Vector3.one * Mathf.Lerp(haloAtSpawn, haloAtImpact, t) * flutter;

        FaceCamera();
    }

    /// <summary>
    /// Turn the halo quad to face the camera.
    ///
    /// Done here rather than by a separate billboard component because the atom already
    /// holds the camera it is closing on, and a billboard that used a different camera
    /// from the one the approach is measured against could face the wrong way in a scene
    /// with two.
    /// </summary>
    void FaceCamera()
    {
        if (halo == null || target == null) return;

        Vector3 toCamera = halo.position - target.position;
        if (toCamera.sqrMagnitude < 0.0001f) return;

        halo.rotation = Quaternion.LookRotation(toCamera);
    }

    // ── Debug overlay ────────────────────────────────────────────────────────

    void OnGUI()
    {
        if (!DebugView_NEW.Overlay) return;
        if (!_armed && !_hit) return;

        GUI.Label(DebugOverlayRows_NEW.Row(DebugOverlayRows_NEW.Atom),
                  string.Format("ATOM  {0}   {1:F0}m   {2:P0}",
                                _hit ? "HIT" : "closing", Distance, Progress));
    }
}

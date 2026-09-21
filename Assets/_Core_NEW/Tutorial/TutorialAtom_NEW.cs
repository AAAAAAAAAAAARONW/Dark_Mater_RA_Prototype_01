using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

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

    [Tooltip("Metres from the light at which contact fires. Not a physics collision — " +
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

    [Header("Burst on impact")]
    [Tooltip("Seconds the atom takes to flare and die after it strikes. 0 leaves it where " +
             "it hit, which is what it used to do.\n\n" +
             "WHY IT MUST NOT JUST STOP. The atom is the one thing the player has been " +
             "watching for ten seconds, and it is where their eyes are at the moment of " +
             "contact. Frozen full-size at the impact point while the light flies on past " +
             "it, it read as the collision having been cancelled — the cause of the whole " +
             "frame sat there unaffected while its effect happened somewhere else, on a " +
             "bar at the top of the screen.\n\n" +
             "A flare that swells and collapses to nothing says the atom took something " +
             "and was spent doing it, right where the player is looking, on the frame the " +
             "line appears.\n\n" +
             "On SCALED time, unlike the approach — so inside D5's slow motion the burst " +
             "plays five times slower and gets the room the slow motion was made for.")]
    [SerializeField] float burstSeconds = 0.7f;

    [Tooltip("How far the halo swells at the peak of the burst, as a multiple of its size " +
             "at impact.")]
    [SerializeField] float burstHaloGrow = 3.5f;

    [Tooltip("How bright the point light spikes at the peak, as a multiple of its value at " +
             "impact. This is what throws light across the photon trail at the moment of " +
             "contact.")]
    [SerializeField] float burstGlowPeak = 4f;

    [Header("Wiring")]
    [Tooltip("Leave empty to use the main camera. Only the glow reads this — it turns the " +
             "halo to face it.\n\n" +
             "This used to be what the atom closed on, back when the camera sat on the " +
             "light and the two were the same point. The follow view moved the camera " +
             "behind and above, so closing on the camera would fly the atom past the " +
             "light and strike empty space three metres behind it.")]
    [FormerlySerializedAs("target")]
    [SerializeField] Transform viewCamera;

    [Tooltip("Leave empty to find it. The light itself: the atom closes on this " +
             "transform, measures impact from it, and starts ahead on its heading rather " +
             "than wherever the player is looking.")]
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

    /// <summary>Seconds into the burst; negative when there is none running.</summary>
    float _burstElapsed = -1f;
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

    /// <summary>
    /// What the atom closes on: the light, not the camera.
    ///
    /// The travel component sits on the light, so its transform is the light. The camera
    /// is only a fallback for a scene without one, where the two are also the only
    /// reasonable guess at "the player".
    /// </summary>
    Transform ClosesOn
    {
        get { return travel != null ? travel.transform : viewCamera; }
    }

    /// <summary>Metres still to run. Read by the debug overlay.</summary>
    public float Distance
    {
        get
        {
            Transform light = ClosesOn;
            if (light == null) return 0f;
            return Vector3.Distance(transform.position, light.position);
        }
    }

    /// <summary>
    /// Place the atom ahead on the light's course and start it closing. D1's onEnter
    /// calls this.
    /// </summary>
    public void Arm()
    {
        Resolve();

        if (ClosesOn == null)
        {
            Debug.LogError("[TutorialAtom_NEW] No TutorialTravel_NEW and no camera to close on. " +
                           "The atom cannot close and D1 can never be satisfied.", this);
            return;
        }

        _armed = true;
        _hit = false;
        _elapsed = 0f;
        _burstElapsed = -1f;

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
        _burstElapsed = -1f;

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
        if (viewCamera == null && Camera.main != null) viewCamera = Camera.main.transform;
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

        return viewCamera != null ? viewCamera.forward : Vector3.forward;
    }

    void Update()
    {
        if (_burstElapsed >= 0f)
        {
            TickBurst();
            return;
        }

        if (!_armed || _hit || ClosesOn == null) return;

        // The world clock (TutorialClock_NEW): ignores D2's slow motion, which drops the
        // time scale on impact, but stops while the piece is paused.
        _elapsed += TutorialClock_NEW.DeltaTime;

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
        Transform light = ClosesOn;
        if (light == null) return;

        float remaining = Mathf.Lerp(spawnDistance, 0f, shaped);
        float lateral = spawnDistance > 0.001f ? remaining / spawnDistance : 0f;

        transform.position = light.position
                             + _heading * remaining
                             + (_right * spawnSpread.x + _up * spawnSpread.y) * lateral;
    }

    void Hit()
    {
        _hit = true;
        _armed = false;

        Draw(1f);

        if (debugLog) Debug.Log("[TutorialAtom_NEW] Impact.", this);

        // Before the event, so anything hung on onImpact that reads this atom sees it
        // already bursting rather than the frame before.
        if (burstSeconds > 0f) _burstElapsed = 0f;

        onImpact.Invoke();
    }

    /// <summary>
    /// Swell and collapse. The halo grows to burstHaloGrow and back down to nothing, the
    /// core shrinks away under it, and the light spikes and dies — one envelope for all
    /// three, so they read as one event. Ends switched off.
    /// </summary>
    void TickBurst()
    {
        // Scaled time — see burstSeconds. Also stops dead while paused, since the pause
        // drops the time scale to zero.
        _burstElapsed += Time.deltaTime;

        float t = burstSeconds > 0f ? Mathf.Clamp01(_burstElapsed / burstSeconds) : 1f;

        // Rises and falls once, and whatever it is multiplied by has reached zero by the
        // end, so nothing pops out of existence at full size.
        float swell = Mathf.Sin(t * Mathf.PI);
        float fade = 1f - t;

        transform.localScale = Vector3.one * scaleAtImpact * fade * fade;

        if (glow != null) glow.intensity = glowAtImpact * (1f + burstGlowPeak * swell) * fade;

        if (halo != null)
        {
            // Local, so this is on top of the shrinking core — which is why it has to be
            // divided back out, or the halo would shrink with it.
            float coreScale = Mathf.Max(0.0001f, fade * fade);
            halo.localScale = Vector3.one * haloAtImpact * (1f + burstHaloGrow * swell) * fade / coreScale;
            FaceCamera();
        }

        if (t < 1f) return;

        _burstElapsed = -1f;
        gameObject.SetActive(false);
    }

    void Draw(float t)
    {
        // The flutter is on brightness only, never on position: a point of light that
        // wanders is a point of light the player will try to track with the stick, and
        // §3 item 2 is that they cannot steer.
        float flutter = 1f + shimmer * Mathf.Sin(TutorialClock_NEW.Time * shimmerSpeed);

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
    /// Faces the camera, not the light. The two used to be the same point; with the
    /// follow view the camera is behind and above, and a halo turned towards the light
    /// would be seen nearly edge on for the whole approach.
    /// </summary>
    void FaceCamera()
    {
        if (halo == null || viewCamera == null) return;

        Vector3 toCamera = halo.position - viewCamera.position;
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

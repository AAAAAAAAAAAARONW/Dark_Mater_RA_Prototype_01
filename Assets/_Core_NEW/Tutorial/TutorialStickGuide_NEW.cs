using UnityEngine;
using TMPro;

/// <summary>
/// The stick diagram beside the hint line: a big ring with a small knob in it, moving
/// the way the player is being asked to move the stick.
///
/// WHY A PICTURE AND NOT JUST THE WORDS. The hint line already says RIGHT STICK · LOOK
/// RIGHT, and the May 2026 playtest recorded what that is worth on its own: the failure
/// was non-gamers not working out the camera at all. Somebody who has never held a pad
/// does not read "right stick" as a thing with a location, and reads "look right" as a
/// request rather than as an instruction about their thumb. A ring with a dot leaning
/// out of it is the one part of this that needs no reading at all, and it is the same
/// diagram every console has used for twenty years.
///
/// It does not replace the line. The words carry the WHAT — which stick, and what the
/// result is called — and the diagram carries the HOW. Together they are the same pair
/// as the A prompt's disc and verb, which is deliberate: one grammar for every control
/// this piece teaches.
///
/// IT IS NOT A LOOP. A canned animation says what to do and then says it again, and
/// keeps saying it in exactly the same words whether the player has done nothing or is
/// one degree short. The knob instead sits where the RUNNING BEAT'S OWN GATE says the
/// stick still has to go, and comes home as they get there — hard over when the mote is
/// behind them, half out when it is at the edge of frame, home the moment it is in the
/// reticle. Move the stick and the dot moves with it.
///
/// That makes it a readout of how far they are from the next frame of the piece, which
/// is the thing a walk-up visitor has no other way to know. It also closes the loop the
/// May 2026 playtest found open: the failure was non-gamers not working out the camera,
/// and what they were missing was not an instruction but the feedback that their thumb
/// was connected to anything at all.
///
/// FOUR MOTIONS. The first is the one that matters and the other three are canned:
///
///   Track  The knob follows the gate. Every gated beat uses this, and each one works
///          its own demand out of the same numbers its gate is judged on, so the
///          picture cannot disagree with the gate — see TutorialBeat_NEW.StickGesture.
///   Push   Out and straight back. A nudge.
///   Hold   Out, and stays out.
///   Sweep  Round the ring. "Anywhere you like" — A1's LOOK AROUND, which is the one
///          prompt in the piece with no target, no direction and no gate to read.
///
/// WHERE THE DIRECTION COMES FROM. Nothing here decides it. The running beat does, and
/// most of them work it out rather than storing it: Beat_LookAt_NEW points the knob at
/// wherever its target actually is, live, so moving a mote in the Scene view moves the
/// diagram with it and the two cannot disagree. That is the same rule the beats already
/// follow for the gate itself — "the direction the player has to look is encoded by
/// where the target sits in the scene, not by a field".
///
/// ON REAL TIME, like every other instruction to the player (see TutorialClock_NEW).
/// A diagram that froze with the world would be telling a paused visitor that their
/// controller had stopped working too — and the whole point of it is to be legible to
/// somebody who is not sure the thing is listening to them.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("STICK", "#E0A030")]
public class TutorialStickGuide_NEW : MonoBehaviour
{
    /// <summary>Which stick the diagram is about. None hides it.</summary>
    public enum StickSide
    {
        None,
        Left,
        Right
    }

    /// <summary>How the knob moves. See the class summary for what each one says.</summary>
    public enum StickMotion
    {
        /// <summary>Out and straight back. A nudge.</summary>
        Push,

        /// <summary>Out, and held there. "Keep pushing until something happens."</summary>
        Hold,

        /// <summary>Round the ring. "Anywhere you like."</summary>
        Sweep,

        /// <summary>
        /// The knob sits where the beat's own gate says the player still has to push,
        /// and comes home as they get there. Not a loop at all — see the class summary.
        /// </summary>
        Track
    }

    /// <summary>
    /// How far off a gate has to be before the knob is asking for everything the stick
    /// has, in degrees.
    ///
    /// Shared by every beat that measures an angle, so "hard over" means the same amount
    /// of work wherever the player meets it. 45 is a little under half a turn of the
    /// head: past that the diagram stops saying how far and starts only saying which way,
    /// which is the right thing to say about a target that is not on screen yet.
    /// </summary>
    public const float FullDeflectionDegrees = 45f;

    /// <summary>
    /// The authored answer, for a beat that cannot work its own out.
    ///
    /// Beats with a target or a direction of their own override this — see
    /// Beat_LookAt_NEW, Beat_TurnAround_NEW and Beat_Zoom_NEW. This list exists for the
    /// cinematic frames, where there is nothing in the scene to read the direction off,
    /// and A1 is the only one that uses it.
    /// </summary>
    public enum GuideKind
    {
        None,
        LookAround,
        LookRight,
        LookLeft,
        LookUp,
        LookDown,
        TurnAround,
        ZoomIn,
        ZoomOut
    }

    /// <summary>
    /// One instruction for the diagram: which stick, which motion, which way.
    ///
    /// A value type with no allocation, because the HUD hands one over every frame
    /// rather than on a change — the beats compute theirs live, so there is no event to
    /// listen to and nothing that could be missed.
    /// </summary>
    public struct Gesture
    {
        public StickSide stick;
        public StickMotion motion;

        /// <summary>
        /// Push and Hold: which way the knob leans, in screen axes — x right, y up.
        /// Sweep: only the sign of x is read, for which way round.
        /// </summary>
        public Vector2 direction;

        /// <summary>
        /// Track only: how far the stick still has to go, 0 to 1.
        ///
        /// 1 is hard over, 0 is "you are there". It is the gate's own error, so it falls
        /// as the player closes on whatever the beat is waiting for and reaches zero the
        /// moment the beat is satisfied.
        /// </summary>
        public float demand;

        public bool IsNone { get { return stick == StickSide.None; } }

        public static Gesture None
        {
            get { return new Gesture { stick = StickSide.None }; }
        }

        public static Gesture Push(StickSide stick, Vector2 direction)
        {
            return new Gesture { stick = stick, motion = StickMotion.Push, direction = direction };
        }

        public static Gesture Hold(StickSide stick, Vector2 direction)
        {
            return new Gesture { stick = stick, motion = StickMotion.Hold, direction = direction };
        }

        /// <summary>
        /// The knob follows the gate. `demand` is how far the stick still has to go,
        /// 1 hard over and 0 arrived.
        /// </summary>
        public static Gesture Track(StickSide stick, Vector2 direction, float demand)
        {
            return new Gesture
            {
                stick = stick,
                motion = StickMotion.Track,
                direction = direction,
                demand = Mathf.Clamp01(demand)
            };
        }

        /// <summary>
        /// Round the ring. `demand` is how much of the asking is left — 1 orbits at full
        /// deflection, and a falling value spirals the knob in towards the centre, which
        /// is how a sweep says "you are nearly there" without a direction to come home
        /// from.
        /// </summary>
        public static Gesture Sweep(StickSide stick, float sign, float demand = 1f)
        {
            return new Gesture
            {
                stick = stick,
                motion = StickMotion.Sweep,
                direction = new Vector2(sign >= 0f ? 1f : -1f, 0f),
                demand = Mathf.Clamp01(demand)
            };
        }

        /// <summary>Everything except the direction, which changes continuously.</summary>
        public bool SameShapeAs(Gesture other)
        {
            return stick == other.stick && motion == other.motion;
        }

        /// <summary>Turn one of the authored answers into a gesture.</summary>
        public static Gesture From(GuideKind kind)
        {
            switch (kind)
            {
                case GuideKind.LookAround: return Sweep(StickSide.Right, 1f);
                case GuideKind.LookRight:  return Hold(StickSide.Right, Vector2.right);
                case GuideKind.LookLeft:   return Hold(StickSide.Right, Vector2.left);
                case GuideKind.LookUp:     return Hold(StickSide.Right, Vector2.up);
                case GuideKind.LookDown:   return Hold(StickSide.Right, Vector2.down);
                case GuideKind.TurnAround: return Hold(StickSide.Right, Vector2.right);
                case GuideKind.ZoomIn:     return Push(StickSide.Left, Vector2.up);
                case GuideKind.ZoomOut:    return Push(StickSide.Left, Vector2.down);
                default:                   return None;
            }
        }
    }

    [Header("Parts")]
    // No reference to the ring. It never moves and never changes, so it is a child
    // object and nothing more — a field for it would be one the builder had to fill and
    // nothing would ever read.

    [Tooltip("The knob. Moves inside the ring; this is the whole animation.")]
    [SerializeField] RectTransform knob;

    [Tooltip("L or R, written under the ring — the knob crosses the middle. Which stick " +
             "is the first thing somebody has to know and the one thing a picture of a " +
             "stick cannot say on its own.")]
    [SerializeField] TMP_Text stickLabel;

    [Tooltip("Fades the whole diagram. Left empty, it appears and disappears instantly.")]
    [SerializeField] CanvasGroup group;

    [Header("Travel")]
    [Tooltip("Pixels from the centre of the ring to the centre of the knob at full " +
             "deflection. Short of the ring's own radius, so the knob stays inside it.")]
    [SerializeField] float travelRadius = 26f;

    [Header("Timing")]
    [Tooltip("Seconds for one out-and-back nudge.")]
    [SerializeField] float pushSeconds = 1.3f;

    [Tooltip("Seconds for one hold: out, stay there, and back to start again.\n\n" +
             "Longer than a push on purpose. The stay is what says 'keep going' — a " +
             "hold that snapped back as fast as a nudge would be teaching the nudge.")]
    [SerializeField] float holdSeconds = 2.4f;

    [Tooltip("Seconds for one circuit of the ring.")]
    [SerializeField] float sweepSeconds = 3f;

    [Tooltip("Seconds to fade in or out when the instruction changes.")]
    [SerializeField] float fadeSeconds = 0.25f;

    [Header("Tracking")]
    [Tooltip("Seconds for the knob to catch up with the gate. Short, but not zero.\n\n" +
             "The demand is computed from live input, so it carries the stick's own " +
             "jitter and the recentre's ramp. Following it exactly would make the knob " +
             "shiver; a little lag turns the same signal into something that reads as a " +
             "thumb moving.")]
    [SerializeField] float trackSmoothing = 0.12f;

    [Tooltip("How far the knob breathes in and out while it is waiting, as a fraction of " +
             "where it is sitting.\n\n" +
             "A player who has not touched the stick yet is looking at a dot that does " +
             "not move, and a HUD element that does not move reads as decoration. This " +
             "is small — enough to say the diagram is live, not enough to be read as the " +
             "movement being asked for.")]
    [Range(0f, 0.3f)]
    [SerializeField] float breathe = 0.08f;

    [SerializeField] float breatheSpeed = 2.2f;

    [Tooltip("Below this demand the beat is as good as satisfied and the knob sits home.")]
    [Range(0f, 0.3f)]
    [SerializeField] float arrivedBelow = 0.04f;

    Gesture _gesture;
    float _phase;
    float _alpha;

    /// <summary>Where the knob actually is, 0 to 1, lagging the demand by trackSmoothing.</summary>
    float _shownDemand;
    float _demandVelocity;

    /// <summary>Where the knob was last drawn, so a direction change eases rather than jumps.</summary>
    Vector2 _shownDirection = Vector2.right;

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// What the diagram should be showing right now. Called every frame by
    /// TutorialHUD_NEW, so it has to be cheap and idempotent — it is.
    /// </summary>
    public void SetGesture(Gesture gesture)
    {
        // A new kind of instruction restarts the animation, so the player sees the whole
        // motion from the beginning rather than joining it half way out. A tracked knob
        // starts from wherever the last beat left it and eases to the new demand, which
        // is one continuous movement across a beat change rather than two.
        if (!_gesture.SameShapeAs(gesture) && gesture.motion != StickMotion.Track) _phase = 0f;

        _gesture = gesture;

        if (gesture.IsNone) return;

        if (stickLabel != null)
        {
            string wanted = gesture.stick == StickSide.Left ? "L" : "R";
            if (stickLabel.text != wanted) stickLabel.text = wanted;
        }
    }

    /// <summary>
    /// Whether the L / R under the ring is shown. Off while the diagram sits on a drawn
    /// plate, which already says which stick in its own words.
    /// </summary>
    public void ShowStickLabel(bool show)
    {
        if (stickLabel != null && stickLabel.gameObject.activeSelf != show)
            stickLabel.gameObject.SetActive(show);
    }

    /// <summary>Nothing on screen, nothing part-way through a motion. The attract reset.</summary>
    public void ResetForAttract()
    {
        _gesture = Gesture.None;
        _phase = 0f;
        _alpha = 0f;
        _shownDemand = 0f;
        _demandVelocity = 0f;

        Apply();
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        ResetForAttract();
    }

    void Update()
    {
        // Real time. This is an instruction to the player, not a thing happening in the
        // universe — see TutorialClock_NEW for the rule and TutorialPauseCard_NEW for
        // the other half of it.
        float dt = Time.unscaledDeltaTime;

        _alpha = fadeSeconds > 0f
            ? Mathf.MoveTowards(_alpha, _gesture.IsNone ? 0f : 1f, dt / fadeSeconds)
            : (_gesture.IsNone ? 0f : 1f);

        if (!_gesture.IsNone) _phase += dt;

        _shownDemand = trackSmoothing > 0f
            ? Mathf.SmoothDamp(_shownDemand, _gesture.demand, ref _demandVelocity, trackSmoothing,
                               Mathf.Infinity, dt)
            : _gesture.demand;

        Apply();
    }

    // ── Drawing ──────────────────────────────────────────────────────────────

    void Apply()
    {
        if (group != null) group.alpha = _alpha;

        // Invisible, so where the knob would be does not matter. Left where it was
        // rather than sent home, because the fade back in is a fade and not a cut: a
        // knob that snapped to the centre under an alpha of 0.004 would be a jump the
        // frame before it became visible again.
        if (_alpha <= 0.001f) return;

        if (knob == null) return;

        knob.anchoredPosition = KnobOffset();
    }

    Vector2 KnobOffset()
    {
        if (_gesture.motion == StickMotion.Sweep)
        {
            // Held out at full deflection the whole way round, never passing through the
            // centre. That is what the gesture actually is — the stick stays pushed and
            // the direction rolls — and a knob that dipped back to the middle between
            // compass points would be drawing four separate nudges instead.
            float turns = sweepSeconds > 0f ? _phase / sweepSeconds : 0f;
            float angle = Mathf.PI * 0.5f + turns * Mathf.PI * 2f * Mathf.Sign(_gesture.direction.x);

            // The orbit tightens as the demand falls, so a sweep with a gate behind it
            // reads the same way a push does: less to do, less deflection asked for.
            if (_shownDemand <= arrivedBelow) return Vector2.zero;

            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * travelRadius * _shownDemand;
        }

        Vector2 dir = _gesture.direction;

        // A target almost dead ahead gives a direction of nearly nothing. Keeping the
        // last one rather than collapsing to the centre stops the knob flickering as the
        // player arrives — which is the exact moment they are looking at it.
        if (dir.sqrMagnitude > 0.0001f) _shownDirection = dir.normalized;

        if (_gesture.motion == StickMotion.Track)
        {
            // Arrived: home, and no breathing. A knob still twitching at a gate that is
            // already met would be asking for something the player has finished doing.
            if (_shownDemand <= arrivedBelow) return Vector2.zero;

            float alive = 1f + breathe * Mathf.Sin(_phase * breatheSpeed);

            return _shownDirection * travelRadius * Mathf.Clamp01(_shownDemand * alive);
        }

        float period = _gesture.motion == StickMotion.Hold ? holdSeconds : pushSeconds;
        float t = period > 0f ? Mathf.Repeat(_phase, period) / period : 0f;

        return _shownDirection * travelRadius * (_gesture.motion == StickMotion.Hold
            ? HoldCurve(t)
            : PushCurve(t));
    }

    /// <summary>
    /// Out and back, with a moment at the top. 0 at both ends so the loop has no seam.
    /// </summary>
    static float PushCurve(float t)
    {
        if (t < 0.35f) return Ease(t / 0.35f);
        if (t < 0.55f) return 1f;
        if (t < 0.80f) return 1f - Ease((t - 0.55f) / 0.25f);

        return 0f;
    }

    /// <summary>
    /// Out quickly, held for most of the cycle, then back. The stay is the message.
    /// </summary>
    static float HoldCurve(float t)
    {
        if (t < 0.18f) return Ease(t / 0.18f);
        if (t < 0.80f) return 1f;
        if (t < 0.92f) return 1f - Ease((t - 0.80f) / 0.12f);

        return 0f;
    }

    /// <summary>Smoothstep. A knob that accelerates reads as a thumb; a linear one does not.</summary>
    static float Ease(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}

using System;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
// Aliased: the post-processing namespace has its own MinAttribute, which would make
// [Min] below ambiguous with UnityEngine.MinAttribute.
using PP = UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;

/// <summary>
/// A gate that dives INTO the world you are in, instead of looking away while it is swapped.
///
/// WHY. Every gate played the cover transition: blend to the top-down camera, swap the
/// render groups on the frame the world is out of view, hold, look back up. It hides the
/// swap and says nothing about scale. Going from the cosmic web to a galaxy is not a
/// change of scenery, it is a change of MAGNIFICATION — one small part of one cluster in
/// the web IS a galaxy — and a camera that looks at the floor while the web vanishes and
/// a galaxy appears reads as a cut.
///
/// WHERE IT DIVES. Light only travels in a straight line, so whatever the photon is flying
/// into when it reaches a gate is on that line: the point is the gate's centre carried
/// focusPastGate further along the direction of travel. At the Micro gate that is the
/// bright yellow cluster — the densest knot of the Core and Coreyellow meshes sits eleven
/// units past the trigger, dead ahead.
///
/// THE RHYTHM:
///
///   approach   Before the gate, by distance: a light appears in the cluster
///              approachDistance before the trigger and grows as the photon closes in, so
///              the dive has a destination before it starts. Around it, faint, the crowd
///              the point is made of — the cluster's galaxies — still one glow.
///   dive       From the gate: the old world grows around the point at a CONSTANT rate —
///              every second magnifies by the same factor, so it holds its speed instead
///              of rushing — brightens and dissolves, while the light swells. The crowd
///              opens at the same steady rate but fifteen times further, so its members
///              stream past the camera as streaks while the web behind them hardly grows:
///              the small scale racing by against a large one that barely moves is what
///              makes the difference in size felt. And the photon becomes small against its
///              world: a dolly zoom swells everything behind it while keeping it the same
///              size, its trail narrows, and a vignette closes in on the point like a
///              funnel. The photon keeps flying into the point and arrives about as the
///              dive ends.
///   peak       Under full light, CoverReached fires: sky, speed and spectrum change, and
///              the next zone camera goes live — the frame the cover camera used to give.
///              The lens and the photon snap back here, unseen: the new world opens at
///              normal framing with the light its normal size — in a world now its scale.
///   emerge     The light settles into the middle of the next world and fades as that
///              world fades in around it.
///
/// COMING OUT (Direction.Out), for a gate that goes up a scale — a galaxy back out to the
/// cosmic web. Light only goes forward, so nothing may converge on a point ahead: a world
/// shrinking into the distance in front of the camera, or a lens widening so everything
/// falls away, reads as the light backing off (the first version did both). Everything
/// that moves here moves from ahead to behind. The point is the middle of the world being
/// left — at the CosmicWeb gate, the Micro galaxy, just behind the camera and twelve below
/// — taken as the centre of its cluster:
///
///   cluster    The cluster's galaxies are there all along: dim, soft smudges spread evenly
///              round the world being left, the whole time the photon crosses it. They come
///              in with that world as the dive into it ends, never on their own.
///   approach   Only over the last few seconds before the gate do they brighten. No glow
///              gathers round the photon: washing the screen yellow in four seconds read as
///              the screen suddenly turning yellow.
///   breakout   The whole cluster shrinks into its centre behind and below at a constant
///              rate, the world left with it. Its galaxies come at the camera from ahead and
///              fall away behind.
///   look back  Then the camera swings round the photon to look back at what it has left,
///              while the photon flies on: the cluster, seen from outside for the first
///              time, collapses into one bright knot — its glow shows only now, as the
///              camera comes round to face it — and the web fades in round it, slowly: the
///              whole cluster is now a point in the web. A dolly zoom the other way (the
///              lens widening while the camera closes in) makes it all fall away behind a
///              photon that keeps its size. That is only allowed facing back: facing
///              forward, the world falling away reads as the light backing off; facing
///              back, as the light pulling away. The cluster keeps shrinking through the
///              peak — no whiteout, no swap to hide — and slows to a stop as the camera
///              swings forward again, into the web. In the Aaron scene the web's own yellow
///              cluster sits where the photon comes out, so it is still inside that yellow
///              then, and flies out of it in the next few seconds.
///
/// CONTINUOUS (Dive.continuous), for a gate into a world that is one point of the world being
/// left — the Milky Way down to one star's system. Nothing swells and nothing is hidden. The
/// first Solar dives swelled a light into a whiteout and grew the Solar System under it round
/// the Sun, which the Solar camera, looking down at the photon, has above the top of the frame:
/// all that showed was a yellow light. Instead one zoom runs from the gate to the end on one
/// clock, and everything it does is in frame:
///
///   the star   The point is the new world's star (emergeOverride), a point the size of the
///              old world's own stars, there from the gate, beside the photon rather than
///              behind it, so the photon never covers it.
///   dive       The old world grows round it, its stars streaming out of the frame, and
///              dissolves; the lens pushes in and the camera turns part way towards the star.
///              The new world has been growing out of the point since the gate, from
///              enterScale, still too small to see.
///   resolve    As the old world goes, the star is the one thing that stays, and it opens up:
///              the real star grows out from under the point standing in for it, and its
///              planets come away from it and spread — seen from above at an angle while the
///              system is still small and close.
///   landing    It grows on into its own place and size, faster now, and lands there as the
///              camera turns back to the photon and the lens goes back to the zone camera's —
///              and, ending facing the star (endFacingStar), the zone camera comes down
///              behind the photon until the new world is in the middle of the frame, and is
///              left there for the player.
///
/// THE LIGHT is a camera-facing glow in the world, drawn after the web and before the
/// photon trail, so it sits inside the cluster and behind the photon. Only the full-screen
/// whiteout at the peak is an overlay.
///
/// WHO OWNS WHAT. CameraDirector_NEW still owns time: it runs the clock, fires the anchors
/// and moves the cameras, and asks this component only how the frame looks at a given
/// progress. The approach is the one exception, and it is driven by distance, not time.
/// The render groups are borrowed from WorldSwitcher_NEW for the length of the dive
/// (Hold / Release), so its anchor-driven swap cannot snap a world halfway through fading.
///
/// SCALING A WORLD. The baked volumes scale cleanly. Particle systems in "Local" scaling
/// mode ignore their parents' scale, so for the length of a scale they are switched to
/// "Hierarchy" with the parents divided back out. Systems that simulate in world space
/// leave their particles where they were emitted whatever the root does — the Micro
/// galaxy's spiral arms are eight of them — so for the length of a scale they stop
/// emitting and their particles are carried along by hand, and put back after. A light's
/// range is in world units whatever its parents' scale, so it is scaled with the world: the
/// Solar System lights each planet with its own point light, and at a hundredth of its size
/// every light would reach every planet.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("DIVE", "#E07A5F")]
public class LayerDive_NEW : MonoBehaviour
{
    /// <summary>Which way the scale goes through the gate.</summary>
    public enum Direction
    {
        /// <summary>Down a scale: into the point (cosmic web to one galaxy).</summary>
        In,

        /// <summary>
        /// Up a scale: out of a cluster (a galaxy back out to the cosmic web). The point is
        /// the middle of the world being left, behind the photon; the old world and the
        /// crowd shrink into it by diveZoom and resolveZoom, so they fall away behind, and
        /// the camera can look back at them. The light, the dolly and the photon's width
        /// work as they do going in — ComingOut turns them off; the look back has its own.
        /// </summary>
        Out
    }

    [Serializable]
    public class Dive
    {
        [Tooltip("The layer being entered. The dive plays on the gate into this layer.")]
        public string toLayerId = "Micro";

        [Tooltip("Untick to put this gate back on the cover transition without losing the tuning.")]
        public bool enabled = true;

        [Tooltip("In = down a scale, into the point ahead. Out = up a scale: out of a " +
                 "cluster, which falls away behind.")]
        public Direction direction = Direction.In;

        /// <summary>
        /// Starting values for a gate that goes up a scale, tuned for the galaxy back out to
        /// the cosmic web (see COMING OUT on the class). Measured against the scene: 2000
        /// galaxies spread evenly 20 to 160 out round the Micro galaxy put 100 to 350 in view
        /// while the photon crosses it, dim; over the last 5 units (four seconds at Micro's
        /// speed) they brighten; about 100 are in view at the gate. The camera starts round
        /// 0.8 seconds in and has the cluster in frame by 1.75, the photon to the left in
        /// front of it; its glow, gold rather than yellow and under the bloom while it is
        /// big, is 17 degrees across then, 5 at the peak and a 1-degree knot by 5 seconds,
        /// while the lens widens from 40 to 72. The web takes from 0.75 to 6.5 seconds to
        /// come in. Facing forward again by 7.2.
        /// </summary>
        public static Dive ComingOut(string toLayerId)
        {
            return new Dive
            {
                toLayerId = toLayerId,
                direction = Direction.Out,
                focusPastGate = 30f,
                approachDistance = 5f,
                emergeSeconds = 4.5f,
                diveZoom = 30f,
                leaveGlow = 1f,
                dissolveFrom = 0.4f,
                lightIntensity = 0f,
                whiteout = 0f,
                memberCount = 2000,
                crowdRadius = 160f,
                memberSize = 1.2f,
                resolveZoom = 30f,
                memberGrowth = 1f,
                streak = 3f,
                spread = 180f,
                haloRadius = 60f,
                haloIntensity = 0.6f,
                haloColor = new Color(1f, 0.88f, 0.62f, 1f),
                lookBackYaw = 150f,
                lookBackLift = 2f,
                lookBackFrame = 0.45f,
                lookBackFrom = 0.8f,
                lookBackUntil = 7.2f,
                lookBackSwing = 1.4f,
                lookBackDolly = 2f,
                ambientCrowd = 0.3f,
                approachCrowd = 0.8f,
                diveCrowd = 0.8f,
                coolShare = 0.35f,
                dollyZoom = 1f,
                photonShrink = 1f,
                peakVignette = 0f,
                peakBloom = 1.5f,
                peakChromaticAberration = 0f
            };
        }

        [Header("Where")]
        [Tooltip("How far past the gate, along the direction of travel, the point sits. " +
                 "Light only travels in a straight line, so the gate's position is enough: " +
                 "at the Micro gate the yellow cluster's densest knot is 11 past the trigger. " +
                 "Coming out, where the camera looks: the cluster is laid out towards here.")]
        public float focusPastGate = 11f;

        [Tooltip("Optional. Dive into this object's position instead, for a target that is " +
                 "not on the line. Coming out, the cluster's centre, if not the middle of the " +
                 "world being left.")]
        public Transform focusOverride;

        [Header("Approach (before the gate)")]
        [Tooltip("The light appears at the point this far before the gate, in world units, " +
                 "and grows as the photon closes in. 0 = it appears at the gate. Coming out, " +
                 "where the cluster's galaxies start to brighten and its glow to gather — " +
                 "keep it to a few seconds of flight: 5 is four at Micro's speed.")]
        [Min(0f)] public float approachDistance = 40f;

        [Header("Timing (seconds)")]
        [Tooltip("From the gate to the peak. About the time the photon takes to reach the " +
                 "point, so the peak lands as it arrives: 16 units at Macro's 5 per second.")]
        [Min(0.1f)] public float diveSeconds = 3f;

        [Tooltip("Held at full light. The swap, the sky and the camera change happen at its start.")]
        [Min(0f)] public float peakHoldSeconds = 0.2f;

        [Tooltip("From the peak to the end: the next world appearing around the light.")]
        [Min(0.1f)] public float emergeSeconds = 2.8f;

        [Header("The world being left")]
        [Tooltip("How many times the old world grows around the point by the peak, at a " +
                 "constant rate — or shrinks into it, coming out.")]
        [Min(0.01f)] public float diveZoom = 4f;

        [Tooltip("How much brighter the old world glows at the peak, just before it is gone.")]
        [Min(0f)] public float leaveGlow = 2f;

        [Tooltip("Fraction of the dive at which the old world starts to dissolve. Late enough " +
                 "that it is seen swelling around the photon first.")]
        [Range(0f, 1f)] public float dissolveFrom = 0.6f;

        [Header("The world being entered")]
        [Tooltip("Scale the new world starts at around where it appears, settling to 1. " +
                 "1 = fade in only. Below 1 the new world grows out of the light — the planets " +
                 "of a solar system moving out to their orbits round its star. Continuous, it " +
                 "grows out of the point from this, so far below 1 that it starts as a speck.")]
        [Min(0.0001f)] public float enterScale = 1f;

        [Tooltip("Optional. Where the light settles and the new world appears from, instead of " +
                 "the middle of the new world — its star, for a solar system.")]
        public Transform emergeOverride;

        [Tooltip("Going in, above 0: nothing is hidden. The new world fades in through the old " +
                 "one, to this much by the peak, grown from enterScale from the start; the light " +
                 "stays where it is and fades into what it lands on; the dive eases in and out " +
                 "rather than stopping at speed. Use it with whiteout 0 and the focus and " +
                 "emergeOverride on the same object, so the light never has to move. 0 = the " +
                 "whiteout swap.")]
        [Range(0f, 1f)] public float crossfade = 0f;

        [Header("Continuous (going in: one zoom, nothing hidden)")]
        [Tooltip("Going in: one zoom from the gate to the end, on one clock (see CONTINUOUS on " +
                 "the class). The old world grows round the point while the new one grows out " +
                 "of it from enterScale — slowly through the dive, so it stays at the point while " +
                 "the old world opens round it, then faster, landing at the end. The point is the " +
                 "new world's star all along: until that star is big enough to see, the light is a " +
                 "point the size of the old world's own stars. The lens pushes in to " +
                 "targetFieldOfView and holds it, the camera turns towards the star (watch), and " +
                 "both let go at the end. No whiteout, nothing snapped back. Use it with the focus " +
                 "inside the old world, emergeOverride on the new world's star, and dollyZoom 1.")]
        public bool continuous = false;

        [Tooltip("The star's size on screen, as a fraction of the screen's height: about that of " +
                 "the old world's own stars, so it is one of them until the zoom leaves it alone.")]
        [Range(0.005f, 0.1f)] public float starSize = 0.024f;

        [Tooltip("How far the camera turns from the photon towards the star, 0 to 1, keeping it " +
                 "in frame as the new world grows into place. 0 = the zone camera as it is.")]
        [Range(0f, 1f)] public float watch = 0f;

        [Tooltip("Seconds at the end over which the camera turns back to the photon and the lens " +
                 "goes back to the zone camera's.")]
        [Min(0.1f)] public float watchRelease = 2f;

        [Tooltip("Brightness of the star's diffraction spikes. They come up as the old world thins " +
                 "round it, flare as the real star comes out from under it, and draw back into " +
                 "it. 0 = a plain point.")]
        [Range(0f, 2f)] public float starSpikes = 0f;

        [Tooltip("Brightness of the planets' orbits, drawn round the new world's star as it opens " +
                 "up, each unrolling from its planet the way the planet goes, innermost first; " +
                 "they fade as the camera turns back. Its planets are the new world's children " +
                 "that draw something, the star apart. 0 = none.")]
        [Range(0f, 1f)] public float orbits = 0f;

        [Tooltip("Continuous: leave the zone camera facing the new world. Over the last " +
                 "endSettleSeconds it comes down its orbit, behind the photon, until the star is " +
                 "endStarHeight above the middle of the frame, and stays there after the " +
                 "transition, until the player looks elsewhere or recentres. The Solar camera as " +
                 "authored looks down at the photon, with the Sun above the top of the frame. " +
                 "Off: the zone camera is left as it was.")]
        public bool endFacingStar = false;

        [Tooltip("Where the star ends up, as a fraction of the frame's half-height above its " +
                 "middle. The photon stays where the zone camera's composer puts it, just below.")]
        [Range(-0.8f, 0.8f)] public float endStarHeight = 0.2f;

        [Tooltip("Seconds at the end over which the zone camera settles onto the new world.")]
        [Min(0.1f)] public float endSettleSeconds = 3f;

        [Header("Light")]
        public Color lightColor = new Color(1f, 0.88f, 0.62f, 1f);

        [Tooltip("Brightness of the light, which is added to what is behind it.")]
        [Range(0f, 4f)] public float lightIntensity = 1.5f;

        [Tooltip("World-space diameter when the light first appears, approachDistance before the gate.")]
        [Min(0f)] public float pointSize = 3f;

        [Tooltip("Diameter as the photon reaches the gate.")]
        [Min(0f)] public float gateSize = 8f;

        [Tooltip("Diameter at the peak.")]
        [Min(0f)] public float peakSize = 40f;

        [Tooltip("Opacity of the full-screen light at the peak. 1 hides the swap completely.")]
        [Range(0f, 1f)] public float whiteout = 1f;

        [Tooltip("Fraction of the dive at which the full-screen light starts to come up.")]
        [Range(0f, 0.98f)] public float whiteoutFrom = 0.7f;

        [Tooltip("Going in: how long the light stays a point. 0 grows it steadily from the gate. " +
                 "Higher keeps it small for most of the dive — a destination, not a wash over " +
                 "everything — and swells it only at the end: 0.5 grows it by the square of the " +
                 "progress, 1 by the cube. Close to the camera, a light growing from the start " +
                 "fills the frame within a third of the dive and hides all the rest.")]
        [Range(0f, 1f)] public float lightHold = 0f;

        [Header("Resolve (what the point turns out to be made of)")]
        [Tooltip("The point turns out to be a crowd — here, the galaxies of the cluster. Before " +
                 "the gate they are one glow; in the dive they open up and stream past the camera " +
                 "while the web around them hardly grows. The small scale racing by and the large " +
                 "one barely moving is what makes the difference in size felt. On a later gate: " +
                 "the stars of a galaxy.")]
        public bool resolve = true;

        [Tooltip("How many members the point resolves into.")]
        [Range(0, 3000)] public int memberCount = 700;

        [Tooltip("Outer radius of the crowd around the point before the dive, in world units. " +
                 "Members fill it from the edge down to the point itself, denser inward.")]
        [Min(0.1f)] public float crowdRadius = 6f;

        [Tooltip("Radius of a member at the crowd's edge, in world units. Inner members are " +
                 "smaller, so each one leaves the point looking the same. Coming out, every " +
                 "galaxy of the cluster is about this size.")]
        [Min(0.001f)] public float memberSize = 0.22f;

        [Tooltip("How far the crowd opens up by the peak, at the same constant rate as the dive. " +
                 "Far more than diveZoom on purpose: the small scale races past while the " +
                 "large one hardly moves. Coming out, how far the cluster shrinks into its " +
                 "centre by the peak.")]
        [Min(1f)] public float resolveZoom = 60f;

        [Tooltip("How much a member grows as the crowd opens: 0 keeps it a point, 1 grows it " +
                 "with the crowd. Coming out, 1 = it shrinks with the cluster.")]
        [Range(0f, 1f)] public float memberGrowth = 0.45f;

        [Tooltip("How far the members stretch into streaks as they stream past.")]
        [Range(0f, 20f)] public float streak = 5f;

        [Tooltip("Coming out only: the cluster fills a cone of this half-angle, in degrees, " +
                 "from its centre towards where the camera looks at the gate. 180 = all round, " +
                 "as a cluster is; smaller packs the same galaxies into the part in view.")]
        [Range(10f, 180f)] public float spread = 40f;

        [Tooltip("Going in: the crowd's thickness against its width. Below 1 it lies flat in the " +
                 "plane square to the world's up — a galaxy's disc — so diving into a galaxy, its " +
                 "stars resolve in its own plane and stream past under the photon. 1 = round.")]
        [Range(0.02f, 1f)] public float crowdFlatten = 1f;

        [Tooltip("Going in: how much of each member is a bright point. Lower makes soft glows — " +
                 "a galaxy's own stars, rather than points stuck on it.")]
        [Range(0f, 1f)] public float memberCore = 1f;

        [Header("Halo (coming out: the glow of the cluster being left)")]
        [Tooltip("The cluster's own light, a soft ball of it round its centre that shrinks with " +
                 "the cluster into the knot the camera looks back at. Seen only from outside, " +
                 "as the camera comes round. How far out it fades to a third, in world units, " +
                 "before it shrinks. 0 = none.")]
        [Min(0f)] public float haloRadius = 0f;

        public Color haloColor = new Color(1f, 0.88f, 0.62f, 1f);

        [Tooltip("Brightness looking through its middle. It brightens as it gathers into a " +
                 "knot, up to twice.")]
        [Range(0f, 4f)] public float haloIntensity = 1f;

        [Header("Look back (coming out: the cluster seen from outside, left behind)")]
        [Tooltip("Coming out, the camera swings this far round the photon, in degrees, to look " +
                 "back at the cluster it is leaving while the photon flies on: the cluster " +
                 "collapses into a knot and the web appears round it. Facing back, everything " +
                 "falling away reads as the light pulling away from it. 0 = no look back.")]
        [Range(0f, 180f)] public float lookBackYaw = 0f;

        [Tooltip("How high the camera rises as it swings, in world units, to look down on the " +
                 "cluster behind and below.")]
        public float lookBackLift = 2f;

        [Tooltip("Where the camera aims while looking back: 0 at the photon, 1 at the cluster.")]
        [Range(0f, 1f)] public float lookBackFrame = 0.45f;

        [Tooltip("Seconds from the gate when the swing round begins.")]
        [Min(0f)] public float lookBackFrom = 0.8f;

        [Tooltip("Seconds from the gate by when the camera faces forward again. Keep it within " +
                 "the transition: diveSeconds + peakHoldSeconds + emergeSeconds.")]
        [Min(0f)] public float lookBackUntil = 7.2f;

        [Tooltip("Seconds each swing takes, round and back.")]
        [Min(0.1f)] public float lookBackSwing = 1.4f;

        [Tooltip("While looking back, a dolly zoom the other way: the lens widens while the " +
                 "camera closes in on the photon, so the cluster and the web fall away behind a " +
                 "photon that keeps its size. How many times they shrink. 1 = off.")]
        [Range(1f, 4f)] public float lookBackDolly = 2f;

        [Tooltip("Coming out only: brightness of the cluster's galaxies the whole time the " +
                 "photon crosses the world being left, before the approach brightens them. " +
                 "0 = they show only over the approach.")]
        [Range(0f, 2f)] public float ambientCrowd = 0f;

        [Tooltip("Brightness of the crowd on the approach, while it is still one glow. Coming " +
                 "out, what the cluster's galaxies brighten to by the gate.")]
        [Range(0f, 2f)] public float approachCrowd = 0.35f;

        [Tooltip("Brightness of the crowd during the dive.")]
        [Range(0f, 4f)] public float diveCrowd = 1f;

        public Color memberWarm = new Color(1f, 0.82f, 0.55f, 1f);
        public Color memberCool = new Color(0.62f, 0.75f, 1f, 1f);

        [Tooltip("Share of the members in the cool colour.")]
        [Range(0f, 1f)] public float coolShare = 0.3f;

        [Tooltip("A third colour for some of the members — the Milky Way's own pink, say.")]
        public Color memberAccent = new Color(0.96f, 0.69f, 0.92f, 1f);

        [Tooltip("Share of the members in the accent colour. 0 = none.")]
        [Range(0f, 1f)] public float accentShare = 0f;

        [Header("Spiral (going in)")]
        [Tooltip("How far, in degrees, the old world turns round the point by the peak, about its " +
                 "own up, at the rate it opens: its stars stream out of the frame along curves, and " +
                 "the crowd with them, as if the camera were spiralling down into the point. " +
                 "Continuous, the new world turns on at that rate, slowing to rest in its own " +
                 "place by the end. Positive turns clockwise seen from above. 0 = straight in.")]
        [Range(-360f, 360f)] public float spin = 0f;

        [Tooltip("How far, in degrees, the camera rolls with the turn, at most — it leans in over " +
                 "the first half of the dive and is level again by the peak. 0 = level.")]
        [Range(0f, 20f)] public float bank = 0f;

        [Header("Shrink (the photon becomes small against its world)")]
        [Tooltip("Dolly zoom. The lens narrows while the camera backs away just enough to keep " +
                 "the photon the same size on screen, so everything behind it swells up around " +
                 "it — the world growing around the light, which reads as the light shrinking " +
                 "into it. How many times the world behind the photon is magnified by the peak. " +
                 "Reset under the whiteout. 1 = off.")]
        [Range(1f, 6f)] public float dollyZoom = 2.5f;

        [Tooltip("Width the photon's trail narrows to by the peak, as a fraction of its own. It is " +
                 "back to full width in the new world — the light its normal size again, in a " +
                 "smaller world. 1 = off.")]
        [Range(0.05f, 1f)] public float photonShrink = 0.35f;

        [Tooltip("Vignette at the peak, closing the view in on the point like a funnel. 0 = off.")]
        [Range(0f, 1f)] public float peakVignette = 0.45f;

        [Tooltip("Optional. Played as the dive starts. A descending boom or a long reverse swell " +
                 "makes a change of scale felt more than any picture can.")]
        public AudioClip diveSound;

        [Range(0f, 1f)] public float diveSoundVolume = 0.8f;

        [Header("Lens (blended over the scene's own post-processing)")]
        [Tooltip("Field of view multiplier at the peak. 1 = unchanged. Above 1 widens, which " +
                 "reads as speeding up.")]
        [Range(0.5f, 2f)] public float fieldOfViewScale = 1f;

        [Tooltip("The lens narrows to this field of view over the dive, at the dive's own rate, " +
                 "and holds it: a push-in on the point. Set it to the next zone camera's (the " +
                 "Solar camera's is 20) and the camera hands over without a step. 0 = off.")]
        [Range(0f, 90f)] public float targetFieldOfView = 0f;

        [Tooltip("Bloom intensity at the peak. The scene's own is 0.8.")]
        [Min(0f)] public float peakBloom = 3f;

        [Range(0f, 1f)] public float peakChromaticAberration = 0.15f;

        [Tooltip("Lens distortion at the peak, centred on the point. 0 = off.")]
        [Range(-100f, 100f)] public float peakLensDistortion = 0f;
    }

    // Renamed from 'dives', deliberately without FormerlySerializedAs: a row saved with the
    // first version's rushing defaults (zoom 40, lens widening 1.3) is dropped, and the
    // gate starts from these.
    [Tooltip("One row per gate that dives. Matched on the layer being entered.")]
    [SerializeField] Dive[] gates = { new Dive() };

    [Header("Wiring (found if empty)")]
    [SerializeField] WorldSwitcher_NEW worlds;
    [SerializeField] CinemachineBrain brain;
    [SerializeField] LayerState_NEW state;
    [SerializeField] PlayerRig_NEW player;
    [SerializeField] NebulaResponder_NEW nebula;

    [Header("Overlay")]
    [Tooltip("Sort order of the whiteout. Below the HUD canvases (0) keeps the HUD readable " +
             "through it; the entry fade sits far above at 32000.")]
    [SerializeField] int overlaySortOrder = -50;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    /// <summary>A group root being scaled, and how to put it back.</summary>
    class Scaled
    {
        public Transform root;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 localScale;
        public bool particlesPrepared;
        public readonly List<ParticleState> particles = new List<ParticleState>();

        // World-space systems, whose particles are carried along by hand, and where the world
        // was last carried to (see Placement).
        public readonly List<WorldParticles> world = new List<WorldParticles>();
        public Placement carried = Placement.AsBuilt;

        // Point and spot lights, with the range each was built with.
        public readonly List<LightRange> lights = new List<LightRange>();
    }

    /// <summary>
    /// Where a world is put, relative to where it was built: the point `anchor` of it, as
    /// built, is moved to `at`, and the world is scaled by `scale` and turned by `turn` about
    /// that point. Scaling round a pivot is the anchor and `at` on the pivot.
    /// </summary>
    struct Placement
    {
        public Vector3 anchor;
        public Vector3 at;
        public Quaternion turn;
        public float scale;

        public static Placement AsBuilt => new Placement { turn = Quaternion.identity, scale = 1f };

        /// <summary>Where a point of the world as built is now.</summary>
        public Vector3 Place(Vector3 built) => at + turn * (built - anchor) * scale;

        /// <summary>Where a point of the world now was as built.</summary>
        public Vector3 Unplace(Vector3 now) => anchor + Quaternion.Inverse(turn) * (now - at) / scale;
    }

    struct LightRange
    {
        public Light light;
        public float range;
    }

    struct ParticleState
    {
        public ParticleSystem system;
        public Vector3 localScale;
        public ParticleSystemScalingMode mode;
    }

    struct WorldParticles
    {
        public ParticleSystem system;
        public bool emitting;   // emission was on
        public bool resize;     // its size is carried too (see CarryWorldParticles)
        public bool size3D;
    }

    static ParticleSystem.Particle[] _particleBuffer = Array.Empty<ParticleSystem.Particle>();

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    static readonly int StreakId = Shader.PropertyToID("_Streak");
    static readonly int SizeScaleId = Shader.PropertyToID("_SizeScale");
    static readonly int ResolveId = Shader.PropertyToID("_Resolve");
    static readonly int NearFadeId = Shader.PropertyToID("_NearFade");
    static readonly int CoreId = Shader.PropertyToID("_Core");
    static readonly int SigmaId = Shader.PropertyToID("_Sigma");

    readonly Dictionary<string, LayerGate_NEW> _gatesByLayer = new Dictionary<string, LayerGate_NEW>();
    readonly Dictionary<string, Vector3> _worldCentres = new Dictionary<string, Vector3>();

    Dive _dive;
    string _fromId;
    string _toId;
    Vector3 _focus;
    Vector3 _emergeFocus;
    Quaternion _crowdAim = Quaternion.identity;
    Scaled _leave;
    Scaled _enter;
    Camera _camera;

    GameObject _light;
    Material _lightMaterial;
    Mesh _lightQuad;
    Vector3 _lightPosition;
    float _lightSize;
    bool _warnedNoLightShader;

    GameObject _crowd;
    Material _crowdMaterial;
    Mesh _crowdMesh;
    int _crowdSignature;
    bool _warnedNoCrowdShader;

    GameObject _halo;
    Material _haloMaterial;
    Mesh _haloMesh;
    bool _warnedNoHaloShader;

    GameObject _overlay;
    Image _veil;

    PP.PostProcessVolume _volume;
    PP.LensDistortion _lens;
    PP.Vignette _vignette;

    float _fovMultiplier = 1f;
    float _dolly = 1f;

    // How far the lens has gone towards targetFieldOfView, and, crossfading, the light's size
    // at the peak, which it keeps while it fades into what it landed on.
    float _fovPush;
    float _peakLightSize;

    // Coming out: seconds since the gate, across the dive, the hold and the emerge, and how
    // far round the camera has swung to look back (0 facing forward, 1 facing back).
    float _clock;
    float _lookBack;

    // Coming out, the cluster round the world being left: how far it has faded in (it is
    // there the whole time the photon crosses that world), and how far the approach has
    // brightened it. Neither changes faster than over the seconds below, however the photon
    // got where it is — a debug jump lands it anywhere. And its brightness at the gate.
    float _presence;
    float _brighten;
    float _crowdAtBegin;
    const float ClusterFadeSeconds = 3f;
    const float BrightenSeconds = 2f;

    // Continuous: the new world's growth over the clock (cumulative, 0 to 1), the scale it is
    // at, how far the camera has turned towards its star, and that star's radius as built — to
    // tell when the real star is bigger on screen than the light standing in for it. And the
    // light's size as a fraction of the screen's height while it is a star (0: in world units).
    float[] _growth = Array.Empty<float>();
    float _enterNow = 1f;
    float _watch;
    float _subjectRadius;
    float _lightScreenSize;
    const int GrowthSteps = 256;

    // The camera's roll into the spin, in degrees.
    float _roll;

    // Continuous, ending facing the star: the zone camera being settled, and its Y axis when
    // the settling began.
    CinemachineFreeLook _settleCamera;
    float _settleFromY;

    // As a star, the light's quad is this many times the size of its glow, so the spikes fit.
    const float StarQuad = 8f;

    // Continuous: the planets' orbits, and when they started to be drawn (-1: not yet).
    class Orbit
    {
        public LineRenderer line;
        public Vector3 offset;   // the planet from the star, flat, as built
    }

    readonly List<Orbit> _orbits = new List<Orbit>();
    Material _orbitMaterial;
    MaterialPropertyBlock _orbitBlock;
    float _orbitsFrom = -1f;
    bool _warnedNoOrbitShader;
    static readonly int DrawId = Shader.PropertyToID("_Draw");
    static readonly int HeadId = Shader.PropertyToID("_Head");
    static readonly int GlowScaleId = Shader.PropertyToID("_GlowScale");
    static readonly int SpikesId = Shader.PropertyToID("_Spikes");
    static readonly int SpikeLengthId = Shader.PropertyToID("_SpikeLength");
    static readonly int SpikeAngleId = Shader.PropertyToID("_SpikeAngle");
    static readonly int SpikeTintId = Shader.PropertyToID("_SpikeTint");
    static readonly int SpinId = Shader.PropertyToID("_Spin");
    const int OrbitPoints = 128;
    static readonly Vector3[] _orbitBuffer = new Vector3[OrbitPoints + 1];

    // How much faster the new world grows once the old one is gone than during the dive.
    const float EmergeRate = 2.5f;

    // Going in, the world being left fades its particles between these distances from the
    // camera (world units) by the peak; less before it, in step with the magnification. See
    // WorldSwitcher_NEW.SetGroupNearFade.
    const float NearFadeFrom = 1.5f;
    const float NearFadeTo = 6f;

    // Continuous, each renderer of the new world comes in between these sizes on screen
    // (pixels across). See WorldSwitcher_NEW.SetGroupSizeFade.
    const float SizeFadeFromPixels = 1.5f;
    const float SizeFadeToPixels = 5f;

    TrailRenderer[] _trails = Array.Empty<TrailRenderer>();
    float[] _trailWidths = Array.Empty<float>();

    /// <summary>True from Begin until End or Abort.</summary>
    public bool IsActive { get; private set; }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (worlds == null) worlds = FindObjectOfType<WorldSwitcher_NEW>();
        if (brain == null) brain = FindObjectOfType<CinemachineBrain>();
        if (state == null) state = FindObjectOfType<LayerState_NEW>();
        if (player == null) player = FindObjectOfType<PlayerRig_NEW>();
        if (nebula == null) nebula = FindObjectOfType<NebulaResponder_NEW>();

        foreach (LayerGate_NEW gate in FindObjectsOfType<LayerGate_NEW>())
            if (gate != null && !string.IsNullOrEmpty(gate.LayerId) && !_gatesByLayer.ContainsKey(gate.LayerId))
                _gatesByLayer.Add(gate.LayerId, gate);

        if (worlds == null)
            Debug.LogWarning("[LayerDive_NEW] No WorldSwitcher_NEW in the scene; a dive will " +
                             "play its light but cannot move or fade the worlds.", this);
    }

    void OnDisable()
    {
        Abort();
        HideLight();
        HideCrowd();
        HideHalo();
    }

    void OnDestroy()
    {
        DestroyLight();
        DestroyCrowd();
        DestroyHalo();
        DestroyOrbits();
        if (_orbitMaterial != null) Destroy(_orbitMaterial);
        _orbitMaterial = null;
    }

    /// <summary>
    /// Between gates. Heading for a dive in: its light in the cluster ahead, over the
    /// approach. Heading for a way out: the cluster round the world the photon is crossing,
    /// there the whole way, dim, brightening over the approach with its glow gathering.
    /// </summary>
    void Update()
    {
        if (IsActive) return;   // the dive draws the light itself

        if (!TryNextDive(out Dive dive, out LayerGate_NEW gate, out float ahead))
        {
            _presence = 0f;
            _brighten = 0f;
            HideLight();
            HideCrowd();
            HideHalo();
            return;
        }

        if (dive.direction == Direction.Out)
        {
            TickCluster(dive, gate, ahead, _presence);
            return;
        }

        _presence = 0f;
        _brighten = 0f;
        HideHalo();

        if (dive.approachDistance <= 0f || ahead > dive.approachDistance)
        {
            HideLight();
            HideCrowd();
            return;
        }

        float closeness = 1f - ahead / dive.approachDistance;
        float appear = Smooth(0f, 0.4f, closeness);
        Vector3 focus = FocusFor(dive, gate, state != null ? state.CurrentLayerId : null);
        SetLight(dive, focus, Mathf.Lerp(dive.pointSize, dive.gateSize, closeness), appear);
        SetCrowd(dive, focus, Quaternion.identity, 1f, dive.approachCrowd * appear, 0f);
    }

    /// <summary>
    /// Heading for a way out: the cluster round the world being crossed. It fades in once
    /// (from <paramref name="presenceFrom"/>, at most over ClusterFadeSeconds) and stays,
    /// dim; over the last approachDistance before the gate its galaxies brighten (at most
    /// over BrightenSeconds). Nothing else: its glow is only ever seen from outside, looking
    /// back (TickComingOut) — gathered round the photon here, it turned the screen yellow.
    /// </summary>
    void TickCluster(Dive dive, LayerGate_NEW gate, float ahead, float presenceFrom)
    {
        Vector3 centre = FocusFor(dive, gate, state != null ? state.CurrentLayerId : null);

        float brightenTo = dive.approachDistance > 0f && ahead <= dive.approachDistance
            ? Smooth(0f, 1f, 1f - ahead / dive.approachDistance)
            : 0f;
        _presence = Mathf.MoveTowards(presenceFrom, 1f, Time.deltaTime / ClusterFadeSeconds);
        _brighten = Mathf.MoveTowards(_brighten, brightenTo, Time.deltaTime / BrightenSeconds);

        HideLight();
        HideHalo();
        SetCrowd(dive, centre, CrowdAim(dive, centre, gate), 1f,
                 _presence * Mathf.Lerp(dive.ambientCrowd, dive.approachCrowd, _brighten), 0f);
    }

    void LateUpdate()
    {
        if (_light != null && _light.activeSelf) PlaceLight();
    }

    // ── Called by CameraDirector_NEW ─────────────────────────────────────────

    /// <summary>True if any row, enabled or not, is for the gate into <paramref name="toLayerId"/>.</summary>
    public bool HasRow(string toLayerId)
    {
        for (int i = 0; i < gates.Length; i++)
            if (gates[i] != null && string.Equals(gates[i].toLayerId, toLayerId, StringComparison.Ordinal))
                return true;
        return false;
    }

    /// <summary>Append a row. For the editor's setup menu; record an undo before calling.</summary>
    public void AddRow(Dive row)
    {
        if (row == null) return;
        Array.Resize(ref gates, gates.Length + 1);
        gates[gates.Length - 1] = row;
    }

    /// <summary>
    /// Put <paramref name="row"/> in place of every row for the same gate, or append it if
    /// there is none. For the editor's setup menu; record an undo before calling.
    /// </summary>
    public void ReplaceRow(Dive row)
    {
        if (row == null) return;

        bool replaced = false;
        for (int i = 0; i < gates.Length; i++)
        {
            if (gates[i] == null || !string.Equals(gates[i].toLayerId, row.toLayerId, StringComparison.Ordinal)) continue;
            gates[i] = row;
            replaced = true;
        }

        if (!replaced) AddRow(row);
    }

    /// <summary>How far before the gate into <paramref name="toLayerId"/> its dive's approach begins; 0 if none.</summary>
    public float ApproachDistance(string toLayerId) => TryGetDive(toLayerId, out Dive d) ? d.approachDistance : 0f;

    /// <summary>The enabled row for the gate into <paramref name="toLayerId"/>, if any.</summary>
    public bool TryGetDive(string toLayerId, out Dive dive)
    {
        for (int i = 0; i < gates.Length; i++)
        {
            Dive d = gates[i];
            if (d != null && d.enabled && string.Equals(d.toLayerId, toLayerId, StringComparison.Ordinal))
            {
                dive = d;
                return true;
            }
        }

        dive = null;
        return false;
    }

    public void Begin(LayerProfile_NEW from, LayerProfile_NEW to, Dive dive)
    {
        if (IsActive) Abort();

        _dive = dive;
        _fromId = from != null ? from.layerId : null;
        _toId = to.layerId;
        _camera = brain != null ? brain.OutputCamera : Camera.main;
        _clock = 0f;
        _lookBack = 0f;
        // Coming out, the cluster goes on from how it stood at the gate — even if a debug
        // jump got the photon there before the approach had finished brightening it.
        _crowdAtBegin = _presence * Mathf.Lerp(dive.ambientCrowd, dive.approachCrowd, _brighten);
        _presence = 0f;
        _brighten = 0f;
        IsActive = true;

        if (worlds != null)
        {
            worlds.Hold(this);

            // Going in, the world being left is magnified round the camera: its particles fade
            // as they come close instead of each being cut away whole at the near plane. The
            // distances grow with the magnification (TickDive), from next to nothing here, so
            // nothing near the camera dims on the frame the dive starts.
            if (dive.direction == Direction.In)
            {
                float k = 1f / Mathf.Max(1f, dive.diveZoom);
                worlds.SetGroupNearFade(_fromId, NearFadeFrom * k, NearFadeTo * k);
            }

            // Continuous, the new world grows out of a speck: nothing in it is drawn until it is
            // big enough on screen to draw cleanly, or its planets blink between pixels.
            if (dive.direction == Direction.In && dive.continuous)
                worlds.SetGroupSizeFade(_toId, _camera, SizeFadeFromPixels, SizeFadeToPixels);
        }

        _focus = DiveFocus();
        _crowdAim = CrowdAim(dive, _focus, _gatesByLayer.TryGetValue(_toId, out LayerGate_NEW gate) ? gate : null);
        // Coming out there is no light to settle anywhere; the next world is all round.
        _emergeFocus = dive.direction == Direction.Out ? _focus
                     : dive.emergeOverride != null ? dive.emergeOverride.position
                     : NextWorldCentre();
        _leave = Capture(worlds != null ? worlds.GroupRoot(_fromId) : null);
        _enter = Capture(worlds != null ? worlds.GroupRoot(_toId) : null);
        _fovPush = 0f;

        // The next world stays out of sight until the peak — or, crossfading, until it starts
        // to show through the old one, already at the size it grows from, inside the light.
        if (worlds != null) worlds.SetGroupLook(_toId, 0f);
        if (Crossfading) ScaleAround(_enter, _emergeFocus, _dive.enterScale);

        // Continuous, it starts as small as it gets, at the point, and grows on one clock
        // (TickDive puts it there, below). Its star and planets are measured first, as built.
        _watch = 0f;
        _roll = 0f;
        _enterNow = 1f;
        _settleCamera = null;
        if (Continuous)
        {
            BuildGrowth();
            _subjectRadius = MeasureRadius(dive.emergeOverride);
            BuildOrbits(dive);
            _enterNow = dive.enterScale;
        }

        BuildOverlay();
        BuildEffects();
        CaptureTrails();
        PlayDiveSound(dive);
        CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);

        if (debugLog)
            Debug.Log($"[LayerDive_NEW] '{_fromId}' -> '{_toId}', diving into {_focus}, " +
                      $"the next world appears at {_emergeFocus}.", this);

        TickDive(0f);
    }

    /// <summary>The old world opening up and dissolving into light. <paramref name="u"/> runs 0 to 1.</summary>
    public void TickDive(float u)
    {
        if (!IsActive) return;
        u = Mathf.Clamp01(u);

        bool outward = _dive.direction == Direction.Out;
        // Crossfading, nothing waits under a whiteout to be snapped back, so the dive lands
        // instead of stopping at speed.
        float p = Crossfading ? LandingRamp(u) : SteadyRamp(u);

        // The old world grows around the point ahead going in, turning about its up as it does
        // for a spiral. Coming out it shrinks into its own middle, behind the photon, with the
        // rest of its cluster.
        Place(_leave, new Placement
        {
            anchor = _focus,
            at = _focus,
            turn = outward ? Quaternion.identity : Quaternion.AngleAxis(_dive.spin * p, Vector3.up),
            scale = Mathf.Pow(outward ? 1f / _dive.diveZoom : _dive.diveZoom, p)
        });

        // The camera leans in with the turn over the first half of the dive, level by the peak.
        _roll = outward ? 0f : _dive.bank * Mathf.Sign(_dive.spin) * Smooth(0.1f, 0.5f, u) * (1f - Smooth(0.6f, 1f, u));

        // The lens pushes in on the point at the dive's own rate.
        _fovPush = _dive.targetFieldOfView > 0f ? p : 0f;

        float dissolve = Smooth(_dive.dissolveFrom, 1f, u);
        float glow = Mathf.Lerp(1f, _dive.leaveGlow, Smooth(0f, 0.85f, u));
        if (worlds != null)
        {
            // Going in, the near fade grows as the world does, to NearFadeFrom..To at the peak.
            if (!outward)
            {
                float k = Mathf.Pow(Mathf.Max(1f, _dive.diveZoom), p - 1f);
                worlds.SetGroupNearFade(_fromId, NearFadeFrom * k, NearFadeTo * k, false);
            }
            worlds.SetGroupLook(_fromId, 1f - dissolve, glow);
        }

        SetVeil(Continuous ? 0f : _dive.whiteout * Smooth(_dive.whiteoutFrom, 1f, u));
        _fovMultiplier = Mathf.Lerp(1f, _dive.fieldOfViewScale, Smooth(0f, 1f, u));

        // The photon's trail narrows at the same constant rate: small against its world.
        // Off coming out (ComingOut sets 1): there the photon is the ruler.
        SetTrailWidth(Mathf.Pow(_dive.photonShrink, p));

        if (outward)
        {
            _clock = u * _dive.diveSeconds;
            TickComingOut();
            return;
        }

        if (Continuous)
        {
            // The old world and the crowd as below; the new world, its star, the lens and the
            // camera run on the clock, straight through the peak.
            TickCrowd(u, p);
            _dolly = 1f;
            _clock = u * _dive.diveSeconds;
            TickContinuous();
            return;
        }

        // Going in, the light swells steadily from its size at the gate, and hands over to
        // the whiteout as the camera arrives at it: a quad at the camera would cut through
        // the near plane. Crossfading, or holding (lightHold), it grows as the dive magnifies
        // — by the same factor each second — and holding, by a power of the progress too, so
        // it stays a point for most of the dive and swells at the end.
        float size;
        if (Crossfading || _dive.lightHold > 0f)
        {
            float k = Mathf.Pow(p, 1f + 2f * _dive.lightHold);
            size = _dive.gateSize * Mathf.Pow(_dive.peakSize / Mathf.Max(1e-3f, _dive.gateSize), k);
        }
        else
        {
            size = Mathf.Lerp(_dive.gateSize, _dive.peakSize, p);
        }
        float arriving = _camera != null ? Smooth(1.5f, 6f, Vector3.Distance(_camera.transform.position, _focus)) : 1f;
        // With no approach nothing has shown the light yet: it rises out of the point as the
        // dive begins, rather than popping in at the gate.
        float rising = _dive.approachDistance > 0f ? 1f : Smooth(0f, 0.4f, u);
        SetLight(_dive, _focus, size, arriving * rising);

        TickCrowd(u, p);

        SetEffects(Smooth(0.2f, 1f, u));

        // Crossfading, the next world starts to show through the old one before the peak.
        if (Crossfading && worlds != null) worlds.SetGroupLook(_toId, _dive.crossfade * Smooth(0.6f, 1f, u));

        // And the world swells behind the photon, at the same rate: the dolly zoom.
        _dolly = Mathf.Pow(_dive.dollyZoom, p);
    }

    /// <summary>
    /// Going in, the crowd opens at the dive's own constant rate (<paramref name="p"/>), only
    /// much further, out of the point and past the camera, turning with the old world; it is
    /// gone by the peak. With no approach crowd nothing has shown it yet: it resolves over the
    /// first half of the dive, the way detail comes up as you close in, instead of being there
    /// at the gate.
    /// </summary>
    void TickCrowd(float u, float p)
    {
        float rise = _dive.approachCrowd > 0f ? Smooth(0f, 0.2f, u) : Smooth(0.05f, 0.5f, u);
        float crowd = Mathf.Lerp(_dive.approachCrowd, _dive.diveCrowd, rise) * (1f - Smooth(0.8f, 1f, u));
        float streak = _dive.streak * Smooth(0f, 0.25f, u) * (1f - Smooth(0.85f, 1f, u));

        // It turns as far as the old world, while opening further: its streaks slant by that
        // much less.
        Quaternion turn = Quaternion.AngleAxis(_dive.spin * p, Vector3.up);
        float spin = _dive.resolveZoom > 1f ? _dive.spin * Mathf.Deg2Rad / Mathf.Log(_dive.resolveZoom) : 0f;
        SetCrowd(_dive, _focus, turn * _crowdAim, Mathf.Pow(_dive.resolveZoom, p), crowd, streak, spin);
    }

    /// <summary>
    /// The hold at the peak. <paramref name="h"/> runs 0 to 1. Going in the whiteout just
    /// holds; coming out, the cluster keeps collapsing and the camera keeps looking back;
    /// continuous, the new world keeps growing.
    /// </summary>
    public void TickHold(float h)
    {
        if (!IsActive) return;

        if (_dive.direction == Direction.Out)
        {
            _clock = _dive.diveSeconds + Mathf.Clamp01(h) * _dive.peakHoldSeconds;
            TickComingOut();
        }
        else if (Continuous)
        {
            _clock = _dive.diveSeconds + Mathf.Clamp01(h) * _dive.peakHoldSeconds;
            TickContinuous();
        }
    }

    /// <summary>The peak: the old world goes back where it was, out of sight, and the new one is readied.</summary>
    public void Crossover()
    {
        if (!IsActive) return;

        // The world left has dissolved by now: back where it was built, out of sight.
        Restore(_leave);
        if (worlds != null) worlds.SetGroupLook(_fromId, 0f);

        // The sky changes as soon as this returns (CoverReached), and every sky snaps — fine
        // under a whiteout, but coming out or crossfading nothing hides it: blend it instead.
        if ((Crossfading || Continuous || _dive.direction == Direction.Out) && nebula != null)
            nebula.BlendNextOver(Mathf.Max(0.5f, 0.8f * (_dive.peakHoldSeconds + _dive.emergeSeconds)));

        if (Continuous)
        {
            // Nothing to hide and nothing to snap back: the new world goes on growing out of
            // the point, and its star and the camera with it.
            HideCrowd();
            _dolly = 1f;
            _clock = _dive.diveSeconds;
            TickContinuous();
            return;
        }

        ScaleAround(_enter, _emergeFocus, _dive.enterScale);
        SetVeil(_dive.whiteout);
        SetTrailWidth(1f);

        if (_dive.direction == Direction.Out)
        {
            // Nothing to hide and nothing to snap back: the web keeps fading in round the
            // collapsing cluster, and the camera keeps looking back at it.
            _clock = _dive.diveSeconds;
            TickComingOut();
            return;
        }

        HideCrowd();
        _dolly = 1f;

        if (Crossfading)
        {
            // Nothing to hide: the next world goes on fading in, and the light stays as it is —
            // it is already where the next world grows from, at the size it lands at.
            if (worlds != null) worlds.SetGroupLook(_toId, _dive.crossfade);
            _peakLightSize = _lightSize;
            SetLight(_dive, _emergeFocus, _peakLightSize, 1f);
            return;
        }

        // Under the whiteout, the light moves to where the next world appears, and the lens
        // and the photon snap back: the new world opens at normal framing, the light at its
        // normal size — in a world that is now its scale.
        if (worlds != null) worlds.SetGroupLook(_toId, 0f);
        SetLight(_dive, _emergeFocus, _dive.peakSize * 0.5f, 1f);
    }

    /// <summary>The next world appearing around the light. <paramref name="v"/> runs 0 to 1.</summary>
    public void TickEmerge(float v)
    {
        if (!IsActive) return;
        v = Mathf.Clamp01(v);

        if (Continuous)
        {
            _clock = _dive.diveSeconds + _dive.peakHoldSeconds + v * _dive.emergeSeconds;
            TickContinuous();
            HandClusterIn(v);
            return;
        }

        // Crossfading, the new world's growing eases in as well as out: the dive has just
        // landed, and nothing should set off at speed.
        float settle = Crossfading ? Smooth(0f, 1f, v) : EaseOut(v);

        // In log space, so growing from a hundredth reads as evenly as growing from a half.
        ScaleAround(_enter, _emergeFocus, Mathf.Pow(_dive.enterScale, 1f - settle));

        SetVeil(_dive.whiteout * (1f - Smooth(0f, 0.45f, v)));
        _fovMultiplier = Mathf.Lerp(_dive.fieldOfViewScale, 1f, Smooth(0f, 1f, v));

        if (_dive.direction == Direction.Out)
        {
            _clock = _dive.diveSeconds + _dive.peakHoldSeconds + v * _dive.emergeSeconds;
            TickComingOut();
            return;
        }

        // The lens holds where the push-in left it — the next zone camera blends in under it —
        // and lets go over the end, by when that camera is all there is.
        _fovPush = _dive.targetFieldOfView > 0f ? 1f - Smooth(0.7f, 1f, v) : 0f;

        if (Crossfading)
        {
            // The light fades into what it landed on, keeping its size, while the new world
            // fades the rest of the way in and grows out of it.
            if (worlds != null) worlds.SetGroupLook(_toId, Mathf.Lerp(_dive.crossfade, 1f, Smooth(0f, 0.6f, v)));
            SetLight(_dive, _emergeFocus, _peakLightSize, 1f - Smooth(0.1f, 0.7f, v));
        }
        else
        {
            if (worlds != null) worlds.SetGroupLook(_toId, Smooth(0f, 0.7f, v));
            SetLight(_dive, _emergeFocus, Mathf.Lerp(_dive.peakSize * 0.5f, _dive.pointSize, settle), 1f - Smooth(0.3f, 1f, v));
        }
        SetEffects(1f - Smooth(0f, 0.9f, v));

        HandClusterIn(v);
    }

    /// <summary>
    /// If the way on out of the world just entered leaves a cluster, that cluster comes in
    /// with the world, rather than its galaxies appearing on their own afterwards.
    /// </summary>
    void HandClusterIn(float v)
    {
        if (TryNextDive(out Dive next, out LayerGate_NEW gate, out float ahead) && next.direction == Direction.Out)
            TickCluster(next, gate, ahead, Mathf.Max(_presence, Smooth(0.2f, 1f, v)));
    }

    // ── Coming out, on one clock ─────────────────────────────────────────────

    /// <summary>
    /// Coming out, the cluster, the web and the camera run straight through the peak, so
    /// they are drawn from one clock (_clock, seconds since the gate) rather than from the
    /// dive's and the emerge's own progress. See COMING OUT on the class.
    /// </summary>
    void TickComingOut()
    {
        float dive = _dive.diveSeconds;
        float rest = Mathf.Max(1e-3f, _dive.peakHoldSeconds + _dive.emergeSeconds);
        float t = _clock;

        HideLight();

        // The camera swings round to look back, holds, and swings forward again; while it
        // faces back, the dolly zoom runs the other way and the bloom comes up with it.
        if (_dive.lookBackYaw > 0f)
        {
            float swing = Mathf.Max(0.1f, _dive.lookBackSwing);
            _lookBack = Smooth(_dive.lookBackFrom, _dive.lookBackFrom + swing, t)
                      * (1f - Smooth(_dive.lookBackUntil - swing, _dive.lookBackUntil, t));
            float pull = Smooth(_dive.lookBackFrom + 0.5f * swing, _dive.lookBackUntil - swing, t);
            _dolly = Mathf.Pow(1f / _dive.lookBackDolly, pull * _lookBack);
        }
        else
        {
            _lookBack = 0f;
            _dolly = 1f;
        }
        SetEffects(_lookBack);

        // The web comes in slowly, from when the camera starts round to near the end. The
        // photon comes out where the web's own yellow cluster is — the one the dive in went
        // into — so inside it the web is mostly that yellow, and it should gather, not flood.
        float web = Smooth(0.25f * dive, dive + 0.75f * rest, t);
        if (worlds != null) worlds.SetGroupLook(_toId, web);

        // The cluster shrinks into its centre, going on from how it stood at the gate; its
        // galaxies fade as the knot gets small.
        float shrink = ClusterShrink(t);
        float arrive = Smooth(0f, 0.3f * dive, t);
        float crowd = Mathf.Lerp(_crowdAtBegin, _dive.diveCrowd, arrive)
                    * (1f - Smooth(dive + 0.2f * rest, dive + 0.8f * rest, t));
        float streak = _dive.streak * Smooth(0f, 0.25f * dive, t) * (1f - Smooth(dive, dive + 0.5f * rest, t));
        SetCrowd(_dive, _focus, _crowdAim, shrink, crowd, streak);

        // Its glow is only seen from outside, as the camera comes round to face it: the knot
        // the cluster becomes, not a haze round the photon. Its light gathers into less space
        // as it shrinks, brightening up to twice, and it is the last thing to go.
        float sigma = _dive.haloRadius * shrink;
        float gather = Mathf.Clamp(Mathf.Sqrt(_dive.haloRadius / Mathf.Max(4f * sigma, 1e-3f)), 1f, 2f);
        SetHalo(_dive, _focus, sigma, _dive.haloIntensity * _lookBack * gather
                                      * (1f - Smooth(dive + 0.5f * rest, dive + rest, t)));
    }

    /// <summary>
    /// Coming out, how much of its size the cluster has left, <paramref name="t"/> seconds
    /// from the gate: at the dive's constant rate to 1 / resolveZoom by the peak, then on
    /// at the rate it reached there, slowing to a stop by the end.
    /// </summary>
    float ClusterShrink(float t)
    {
        float dive = _dive.diveSeconds;
        float logZoom = Mathf.Log(Mathf.Max(1f, _dive.resolveZoom));
        if (t <= dive) return Mathf.Exp(-logZoom * SteadyRamp(t / dive));

        // EaseOut starts at three times its average rate: go on as far as matches the rate
        // at the peak.
        float rest = Mathf.Max(1e-3f, _dive.peakHoldSeconds + _dive.emergeSeconds);
        float further = logZoom * SteadyRate * rest / (3f * dive);
        return Mathf.Exp(-logZoom - further * EaseOut((t - dive) / rest));
    }

    // ── Continuous: one zoom, on one clock ──────────────────────────────────

    /// <summary>
    /// Continuous, the new world, its star, the lens and the camera run straight through the
    /// peak, so they are drawn from one clock (_clock, seconds since the gate) rather than from
    /// the dive's and the emerge's own progress. See CONTINUOUS on the class.
    /// </summary>
    void TickContinuous()
    {
        float dive = _dive.diveSeconds;
        float total = dive + _dive.peakHoldSeconds + _dive.emergeSeconds;
        float t = _clock;
        float release = Mathf.Min(_dive.watchRelease, total - dive);
        float letGo = 1f - Smooth(total - release, total, t);

        // The new world grows out of the point at the pace the growth table sets, in log
        // space, and is shown from early on, while it is still far too small to see. It turns
        // round its star with the old world, and on after it, slowing to rest in its own place:
        // its planets sweep round the star as it opens.
        _enterNow = Mathf.Pow(_dive.enterScale, 1f - Growth(t / total));
        float turned = SpinAt(t, out float turns);
        Quaternion turn = Quaternion.AngleAxis(turned - turns, Vector3.up);
        Place(_enter, new Placement { anchor = _emergeFocus, at = Subject(), turn = turn, scale = _enterNow });
        if (worlds != null) worlds.SetGroupLook(_toId, Smooth(0.5f, 2f, t));

        // The lens pushes in over the dive and lands, holds while the new world grows into
        // place, and gives way to the zone camera's at the end. The camera turns towards the
        // star from the gate, and back with the lens.
        _fovPush = _dive.targetFieldOfView > 0f ? (t < dive ? LandingRamp(t / dive) : 1f) * letGo : 0f;
        _watch = _dive.watch * Smooth(0f, 1.5f, t) * letGo;

        // The photon's trail narrows through the dive and is back to its width by the end.
        float narrow = t < dive ? SteadyRamp(t / dive) : 1f - Smooth(dive, total, t);
        SetTrailWidth(Mathf.Pow(_dive.photonShrink, narrow));

        SetEffects(t < dive ? Smooth(0.2f, 1f, t / dive) : 1f - Smooth(dive, total - release, t));

        // The star: one of the old world's, there from the gate and a little brighter as the
        // dive closes in, until the real one is bigger on screen than it and takes over. With
        // nothing to measure the real one by, it goes as the new world grows into place. Its
        // spikes come up as the old world thins round it. As the real star comes out from
        // under it, it flares — brighter, the spikes longer — then fades into that star with
        // its spikes drawn back in.
        Vector3 star = Subject();
        float handover = _subjectRadius > 0f
            ? Smooth(0.25f, 0.8f, SubjectScreenSize(star) / Mathf.Max(1e-4f, _dive.starSize))
            : Smooth(dive, total - release, t);
        float flare = Smooth(0f, 0.3f, handover) * (1f - Smooth(0.3f, 0.8f, handover));
        float bright = Smooth(0f, 1.5f, t) * Mathf.Lerp(0.6f, 1f, Smooth(0f, dive, t))
                     * (1f + 0.8f * flare) * (1f - Smooth(0.35f, 1f, handover));
        SetLight(_dive, star, 0f, bright, _dive.starSize);

        float spikesIn = Smooth(_dive.dissolveFrom * dive, dive, t);
        SetSpikes(_dive.starSpikes * spikesIn * (1f + flare) * (1f - Smooth(0.35f, 0.95f, handover)),
                  Mathf.Min(1f, Mathf.Lerp(0.3f, 0.85f, spikesIn) * (1f + 0.15f * flare)),
                  (15f + 6f * t) * Mathf.Deg2Rad);

        // Once the real star has taken over, its planets' orbits are drawn round it.
        if (_orbitsFrom < 0f && handover >= 0.6f) _orbitsFrom = t;
        TickOrbits(t, total - release, turn);

        // And at the end the zone camera settles onto the new world.
        if (_dive.endFacingStar) TickSettle(t, total);
    }

    /// <summary>
    /// Continuous, ending facing the star: over the last endSettleSeconds, the zone camera
    /// comes down its orbit until the star, where it lands, is endStarHeight above the middle
    /// of the frame. Its Y axis is all that is moved, so the photon stays where its composer
    /// puts it and the player picks up from there. Look input is locked for the transition
    /// (LayerProfile_NEW.lockLookInput), so nothing else is moving it meanwhile.
    /// </summary>
    void TickSettle(float t, float total)
    {
        float from = total - Mathf.Min(_dive.endSettleSeconds, total - _dive.diveSeconds);
        if (t < from) return;

        if (_settleCamera == null)
        {
            _settleCamera = brain != null ? brain.ActiveVirtualCamera as CinemachineFreeLook : null;
            if (_settleCamera == null) return;
            _settleFromY = _settleCamera.m_YAxis.Value;
        }

        float y = LookYFor(_settleCamera, _emergeFocus, _dive.endStarHeight);
        _settleCamera.m_YAxis.Value = Mathf.Lerp(_settleFromY, y, Smooth(from, total, t));
    }

    /// <summary>
    /// The Y-axis value at which <paramref name="vcam"/>, looking at its target the way its
    /// rigs' composers do, has <paramref name="target"/> <paramref name="height"/> of the
    /// frame's half-height above the middle. Only how high the camera sits on its orbits,
    /// and so how far down it looks, is solved for; its heading is left as it is. Higher on
    /// the orbits looks further down and puts the target higher in the frame, so halving
    /// the range finds it.
    /// </summary>
    static float LookYFor(CinemachineFreeLook vcam, Vector3 target, float height)
    {
        Transform follow = vcam.Follow, lookAt = vcam.LookAt;
        if (follow == null || lookAt == null) return vcam.m_YAxis.Value;

        // Which way, flat, the camera sits from what it follows.
        Vector3 back = vcam.State.RawPosition - follow.position;
        back.y = 0f;
        if (back.sqrMagnitude < 1e-6f) back = -follow.forward;
        back.y = 0f;
        if (back.sqrMagnitude < 1e-6f) return vcam.m_YAxis.Value;
        back.Normalize();

        float tanHalf = Mathf.Tan(vcam.m_Lens.FieldOfView * 0.5f * Mathf.Deg2Rad);
        float min = vcam.m_YAxis.m_MinValue, max = vcam.m_YAxis.m_MaxValue;
        float lo = min, hi = max;
        for (int i = 0; i < 24; i++)
        {
            float y = 0.5f * (lo + hi);
            float t = max > min ? (y - min) / (max - min) : 0.5f;

            Vector3 offset = vcam.GetLocalPositionForCameraFromInput(t);
            Vector3 camera = follow.position + Vector3.up * offset.y - back * offset.z;
            float middle = Pitch(lookAt.position - camera) + Mathf.Atan((2f * ComposerScreenY(vcam, t) - 1f) * tanHalf);
            float seen = Mathf.Tan(Mathf.Clamp(Pitch(target - camera) - middle, -1.5f, 1.5f)) / tanHalf;

            if (seen > height) hi = y;
            else lo = y;
        }
        return 0.5f * (lo + hi);
    }

    static float Pitch(Vector3 v) => Mathf.Atan2(v.y, new Vector2(v.x, v.z).magnitude);

    /// <summary>
    /// How far down the screen the FreeLook's composers put its target at a normalised Y,
    /// blending the rigs the way the FreeLook blends their states: bottom to middle below
    /// one half, middle to top above it.
    /// </summary>
    static float ComposerScreenY(CinemachineFreeLook vcam, float t)
    {
        float top = RigScreenY(vcam, 0), middle = RigScreenY(vcam, 1), bottom = RigScreenY(vcam, 2);
        return t <= 0.5f ? Mathf.Lerp(bottom, middle, t * 2f) : Mathf.Lerp(middle, top, (t - 0.5f) * 2f);
    }

    static float RigScreenY(CinemachineFreeLook vcam, int rig)
    {
        CinemachineVirtualCamera r = vcam.GetRig(rig);
        CinemachineComposer composer = r != null ? r.GetCinemachineComponent<CinemachineComposer>() : null;
        return composer != null ? composer.m_ScreenY : 0.5f;
    }

    /// <summary>
    /// Continuous: how far, in degrees, the turn has gone <paramref name="t"/> seconds from the
    /// gate — with the old world through the dive, at its rate (SteadyRamp), then on at the
    /// rate it had at the peak, slowing steadily to rest at the end — and how far it goes in
    /// all (<paramref name="total"/>).
    /// </summary>
    float SpinAt(float t, out float total)
    {
        float dive = _dive.diveSeconds;
        float rest = Mathf.Max(1e-3f, _dive.peakHoldSeconds + _dive.emergeSeconds);
        float rate = _dive.spin * SteadyRate / dive;
        total = _dive.spin + 0.5f * rate * rest;
        if (t <= dive) return _dive.spin * SteadyRamp(t / dive);

        float after = Mathf.Min(t - dive, rest);
        return _dive.spin + rate * (after - after * after / (2f * rest));
    }

    /// <summary>Continuous: where the new world's star is now, as that world grows out of the point.</summary>
    Vector3 Subject() => _focus + (_emergeFocus - _focus) * _enterNow;

    // ── Continuous: the orbits ───────────────────────────────────────────────

    const float OrbitWidth = 0.0016f;     // of the screen's height: about a pixel and a half
    const float OrbitDrawSeconds = 1.6f;
    const float OrbitStagger = 0.14f;

    /// <summary>
    /// The new world's planets — its star's siblings that draw something — innermost first,
    /// with a line each to draw its orbit with. Measured as built, before the dive moves it.
    /// </summary>
    void BuildOrbits(Dive d)
    {
        DestroyOrbits();
        _orbitsFrom = -1f;

        Transform star = d.emergeOverride;
        if (d.orbits <= 0f || star == null || star.parent == null) return;

        if (_orbitMaterial == null)
        {
            Shader shader = Resources.Load<Shader>("DiveOrbit_NEW");
            if (shader == null)
            {
                if (!_warnedNoOrbitShader)
                    Debug.LogWarning("[LayerDive_NEW] Resources/DiveOrbit_NEW.shader is missing; the planets' " +
                                     "orbits are not drawn.", this);
                _warnedNoOrbitShader = true;
                return;
            }

            _orbitMaterial = new Material(shader) { name = "DiveOrbit (runtime)" };
            _orbitBlock = new MaterialPropertyBlock();
        }

        foreach (Transform planet in star.parent)
        {
            if (planet == star || planet.GetComponent<Renderer>() == null) continue;

            // In the plane through the star square to the world's up, as the planets are.
            Vector3 offset = planet.position - star.position;
            offset.y = 0f;
            if (offset.sqrMagnitude > 1e-6f) _orbits.Add(new Orbit { line = MakeOrbitLine(), offset = offset });
        }

        _orbits.Sort((a, b) => a.offset.sqrMagnitude.CompareTo(b.offset.sqrMagnitude));
    }

    LineRenderer MakeOrbitLine()
    {
        var go = new GameObject("DiveOrbit (temporary)");
        if (player != null) go.layer = player.gameObject.layer;

        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = false;
        line.positionCount = OrbitPoints + 1;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.numCapVertices = 0;
        line.numCornerVertices = 0;
        line.sharedMaterial = _orbitMaterial;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        line.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        go.SetActive(false);
        return line;
    }

    /// <summary>
    /// Continuous: the planets' orbits round the star as the new world opens up. Each is drawn
    /// from its planet round the way the planets turn, over OrbitDrawSeconds — innermost
    /// first, the next OrbitStagger later — with a brighter head where it is being drawn, and
    /// all fade as the camera turns back to the photon (<paramref name="releaseFrom"/>). Made
    /// afresh every frame round where the star is, as big as the new world is and turned as it
    /// is (<paramref name="turn"/>), and about a pixel and a half wide however far off.
    /// </summary>
    void TickOrbits(float t, float releaseFrom, Quaternion turn)
    {
        if (_orbits.Count == 0) return;

        float shown = _orbitsFrom < 0f ? 0f : _dive.orbits * (1f - Smooth(releaseFrom - 0.6f, releaseFrom + 1f, t));
        if (shown <= 0.001f || _camera == null)
        {
            HideOrbits();
            return;
        }

        Vector3 centre = Subject();
        float width = OrbitWidth * 2f * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad)
                    * Vector3.Distance(_camera.transform.position, centre);
        float way = _dive.spin > 0f ? 1f : -1f;
        Color c = _dive.memberCool;

        for (int i = 0; i < _orbits.Count; i++)
        {
            Orbit o = _orbits[i];
            if (o.line == null) continue;

            float draw = Smooth(0f, 1f, (t - _orbitsFrom - i * OrbitStagger) / OrbitDrawSeconds);
            bool on = draw > 0f;
            if (o.line.gameObject.activeSelf != on) o.line.gameObject.SetActive(on);
            if (!on) continue;

            Vector3 planet = turn * o.offset * _enterNow;
            for (int j = 0; j <= OrbitPoints; j++)
                _orbitBuffer[j] = centre + Quaternion.AngleAxis(way * 360f * j / OrbitPoints, Vector3.up) * planet;
            o.line.SetPositions(_orbitBuffer);
            o.line.widthMultiplier = width;

            _orbitBlock.SetColor(ColorId, new Color(c.r, c.g, c.b, shown));
            _orbitBlock.SetFloat(DrawId, draw);
            _orbitBlock.SetFloat(HeadId, 1f - Smooth(0.85f, 1f, draw));
            o.line.SetPropertyBlock(_orbitBlock);
        }
    }

    void HideOrbits()
    {
        for (int i = 0; i < _orbits.Count; i++)
            if (_orbits[i].line != null && _orbits[i].line.gameObject.activeSelf)
                _orbits[i].line.gameObject.SetActive(false);
    }

    void DestroyOrbits()
    {
        for (int i = 0; i < _orbits.Count; i++)
            if (_orbits[i].line != null) Destroy(_orbits[i].line.gameObject);
        _orbits.Clear();
    }

    /// <summary>
    /// Continuous: the real star's diameter on screen as a fraction of the screen's height, at
    /// the size the new world has grown to.
    /// </summary>
    float SubjectScreenSize(Vector3 at)
    {
        if (_camera == null) return 0f;
        float distance = Vector3.Distance(_camera.transform.position, at);
        float halfTan = Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        return distance > 1e-3f ? _subjectRadius * _enterNow / (distance * halfTan) : 1f;
    }

    /// <summary>
    /// The radius of what <paramref name="t"/> draws, as built: from its mesh, which a hidden
    /// renderer still has, else from its renderer. 0 if it draws nothing.
    /// </summary>
    static float MeasureRadius(Transform t)
    {
        if (t == null) return 0f;

        Vector3 e;
        MeshFilter mf = t.GetComponent<MeshFilter>();
        Renderer r = t.GetComponent<Renderer>();
        if (mf != null && mf.sharedMesh != null) e = Vector3.Scale(mf.sharedMesh.bounds.extents, t.lossyScale);
        else if (r != null) e = r.bounds.extents;
        else return 0f;

        return Mathf.Max(Mathf.Abs(e.x), Mathf.Max(Mathf.Abs(e.y), Mathf.Abs(e.z)));
    }

    /// <summary>
    /// Continuous: how the new world's growth is spread over the clock, tabulated once per
    /// dive. A rate (in log scale) of 1 through the dive, eased in over its first second;
    /// rising to EmergeRate over the second and a half after the peak; easing to nothing over
    /// the last third of the emerge. So it stays at the point while the old world opens round
    /// it, opens up itself once that world is gone, and lands. Normalised to run 0 to 1.
    /// </summary>
    void BuildGrowth()
    {
        float dive = _dive.diveSeconds;
        float total = dive + _dive.peakHoldSeconds + _dive.emergeSeconds;
        float landFrom = total - _dive.emergeSeconds / 3f - 0.5f;
        if (_growth.Length != GrowthSteps + 1) _growth = new float[GrowthSteps + 1];

        float previous = 0f;
        for (int i = 0; i <= GrowthSteps; i++)
        {
            float t = total * i / GrowthSteps;
            float rate = t <= dive ? Smooth(0f, 1f, t) : 1f + (EmergeRate - 1f) * Smooth(dive, dive + 1.5f, t);
            rate *= 1f - Smooth(landFrom, total, t);
            _growth[i] = i == 0 ? 0f : _growth[i - 1] + 0.5f * (previous + rate);
            previous = rate;
        }

        float sum = _growth[GrowthSteps];
        for (int i = 0; i <= GrowthSteps; i++)
            _growth[i] = sum > 0f ? _growth[i] / sum : (float)i / GrowthSteps;
    }

    /// <summary>Continuous: how far the new world has grown, 0 to 1, <paramref name="x"/> of the way through.</summary>
    float Growth(float x)
    {
        if (_growth.Length < 2) return Mathf.Clamp01(x);
        float f = Mathf.Clamp01(x) * (_growth.Length - 1);
        int i = Mathf.Min((int)f, _growth.Length - 2);
        return Mathf.Lerp(_growth[i], _growth[i + 1], f - i);
    }

    /// <summary>Finished: hand the worlds back and remove everything the dive made.</summary>
    public void End()
    {
        if (!IsActive) return;
        if (debugLog) Debug.Log($"[LayerDive_NEW] Into '{_toId}'.", this);
        Cleanup();

        // Straight on to what shows between gates, this same frame: a cluster that came in
        // with the world would otherwise drop out for one.
        Update();
    }

    /// <summary>
    /// Stopped part-way — another gate, the object disabled, play mode ending. Puts every
    /// world back to its built transform and lets WorldSwitcher_NEW settle what shows.
    /// </summary>
    public void Abort()
    {
        if (!IsActive) return;
        if (debugLog) Debug.Log($"[LayerDive_NEW] Dive into '{_toId}' stopped part-way.", this);
        Cleanup();
    }

    void Cleanup()
    {
        Restore(_leave);
        Restore(_enter);
        _leave = null;
        _enter = null;

        CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
        _fovMultiplier = 1f;
        _dolly = 1f;
        _fovPush = 0f;
        _lookBack = 0f;
        _watch = 0f;
        _roll = 0f;
        _settleCamera = null;
        _enterNow = 1f;
        _lightScreenSize = 0f;
        _clock = 0f;
        SetTrailWidth(1f);
        DestroyOrbits();
        _orbitsFrom = -1f;
        _trails = Array.Empty<TrailRenderer>();
        _trailWidths = Array.Empty<float>();

        DestroyOverlay();
        DestroyEffects();
        HideLight();
        HideCrowd();
        HideHalo();

        IsActive = false;

        if (worlds != null)
        {
            worlds.SetGroupNearFade(_fromId, 0f, 0f);
            worlds.SetGroupSizeFade(_toId, null, 0f, 0f);
            worlds.Release(this, _toId);
        }
    }

    // ── Where ────────────────────────────────────────────────────────────────

    /// <summary>
    /// The nearest gate ahead of the photon along its line, if it has a row: what the photon
    /// is heading for, and how far ahead the gate is. Not a gate into the layer it is
    /// already in (a debug jump can land it before one).
    /// </summary>
    bool TryNextDive(out Dive dive, out LayerGate_NEW gate, out float ahead)
    {
        dive = null;
        gate = null;
        ahead = float.MaxValue;
        if (player == null) return false;

        Vector3 at = player.transform.position;
        Vector3 along = TravelDirection();
        foreach (LayerGate_NEW g in _gatesByLayer.Values)
        {
            if (g == null) continue;
            float d = Vector3.Dot(GateCentre(g) - at, along);
            if (d > 0f && d < ahead)
            {
                ahead = d;
                gate = g;
            }
        }

        if (gate == null) return false;
        if (state != null && string.Equals(state.CurrentLayerId, gate.LayerId, StringComparison.Ordinal)) return false;
        return TryGetDive(gate.LayerId, out dive);
    }

    Vector3 DiveFocus()
    {
        _gatesByLayer.TryGetValue(_toId, out LayerGate_NEW gate);
        return FocusFor(_dive, gate, _fromId);
    }

    /// <summary>
    /// Going in, the point on the line past the gate. Coming out, the centre of the cluster
    /// being left — the middle of the world the photon is leaving — or, with no world to
    /// measure, a point on the line behind: never ahead, which would pull everything away
    /// from the light.
    /// </summary>
    Vector3 FocusFor(Dive d, LayerGate_NEW gate, string leavingLayerId)
    {
        if (d.focusOverride != null) return d.focusOverride.position;

        bool outward = d.direction == Direction.Out;
        if (outward && TryWorldCentre(leavingLayerId, out Vector3 centre)) return centre;

        float along = outward ? -d.focusPastGate : d.focusPastGate;
        if (gate != null) return GateCentre(gate) + TravelDirection() * along;

        // No trigger to measure from — a jump straight into the layer. From the photon.
        if (player != null) return player.transform.position + TravelDirection() * along;
        return _camera != null ? _camera.transform.position + _camera.transform.forward * along : transform.position;
    }

    /// <summary>
    /// Coming out, which way the cluster's cone (the crowd's local +Z) points: from its
    /// centre towards where the camera looks, focusPastGate beyond the gate. Going in the
    /// crowd is round, and stays unturned.
    /// </summary>
    Quaternion CrowdAim(Dive d, Vector3 centre, LayerGate_NEW gate)
    {
        if (d.direction != Direction.Out) return Quaternion.identity;

        Vector3 from = gate != null ? GateCentre(gate) : (player != null ? player.transform.position : centre);
        Vector3 axis = from + TravelDirection() * d.focusPastGate - centre;
        if (axis.sqrMagnitude < 1e-6f) return Quaternion.identity;

        Vector3 up = Mathf.Abs(Vector3.Dot(axis.normalized, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
        return Quaternion.LookRotation(axis, up);
    }

    /// <summary>The light's straight line: the photon's heading.</summary>
    Vector3 TravelDirection()
    {
        Vector3 dir = player != null ? player.MovementDirection : Vector3.forward;
        return dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward;
    }

    static Vector3 GateCentre(LayerGate_NEW gate)
    {
        Collider c = gate.GetComponent<Collider>();
        return c != null ? c.bounds.center : gate.transform.position;
    }

    /// <summary>The middle of what the next world will show.</summary>
    Vector3 NextWorldCentre() => TryWorldCentre(_toId, out Vector3 centre) ? centre : _focus;

    /// <summary>
    /// The middle of what a world shows, measured once — first asked for before any dive
    /// has moved it. Active objects only: groups keep disabled prototypes around, and those
    /// would pull the point off the real thing.
    /// </summary>
    bool TryWorldCentre(string layerId, out Vector3 centre)
    {
        centre = default;
        if (string.IsNullOrEmpty(layerId)) return false;
        if (_worldCentres.TryGetValue(layerId, out centre)) return true;

        Transform root = worlds != null ? worlds.GroupRoot(layerId) : null;
        if (root == null) return false;

        Vector3 sum = Vector3.zero;
        int n = 0;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(false))
        {
            sum += r.transform.position;
            n++;
        }

        centre = n > 0 ? sum / n : root.position;
        _worldCentres[layerId] = centre;
        return true;
    }

    // ── Scaling a world around a point ───────────────────────────────────────

    static Scaled Capture(Transform root)
    {
        if (root == null) return null;
        return new Scaled { root = root, position = root.position, rotation = root.rotation, localScale = root.localScale };
    }

    static void ScaleAround(Scaled s, Vector3 pivot, float k) =>
        Place(s, new Placement { anchor = pivot, at = pivot, turn = Quaternion.identity, scale = k });

    /// <summary>Puts a world where <paramref name="to"/> says, relative to where it was built.</summary>
    static void Place(Scaled s, Placement to)
    {
        if (s == null || s.root == null) return;

        // Prepared on the first real move, while the root is still where it was built —
        // the parents' scale has to be measured at 1.
        if (!s.particlesPrepared && !SamePlacement(to, Placement.AsBuilt)) PrepareScaling(s);

        s.root.position = to.Place(s.position);
        s.root.rotation = to.turn * s.rotation;
        s.root.localScale = s.localScale * to.scale;

        for (int i = 0; i < s.lights.Count; i++)
            if (s.lights[i].light != null) s.lights[i].light.range = s.lights[i].range * to.scale;

        if (s.world.Count > 0 && !SamePlacement(s.carried, to))
        {
            CarryWorldParticles(s.world, s.carried, to);
            s.carried = to;
        }
    }

    /// <summary>True if the two put every point of a world in the same place.</summary>
    static bool SamePlacement(Placement a, Placement b)
    {
        if (!Mathf.Approximately(a.scale, b.scale) || Quaternion.Angle(a.turn, b.turn) > 1e-3f) return false;
        // Both send the point `a.anchor` to the same place, and turn and scale alike round it.
        return (b.Place(a.anchor) - a.at).sqrMagnitude < 1e-8f;
    }

    /// <summary>
    /// See the class summary: Local-scaling systems follow the root for the length of a
    /// scale, world-space systems stop emitting and are listed to be carried by hand, and
    /// lights are listed to have their range scaled with the world.
    /// </summary>
    static void PrepareScaling(Scaled s)
    {
        s.particlesPrepared = true;

        foreach (Light l in s.root.GetComponentsInChildren<Light>(true))
            if (l.type == LightType.Point || l.type == LightType.Spot)
                s.lights.Add(new LightRange { light = l, range = l.range });

        foreach (ParticleSystem ps in s.root.GetComponentsInChildren<ParticleSystem>(true))
        {
            Transform t = ps.transform;
            ParticleSystem.MainModule main = ps.main;

            if (main.simulationSpace == ParticleSystemSimulationSpace.World)
            {
                // Left in its own scaling mode, so whatever size the system's scale gives its
                // particles stays put and the carry alone resizes them — unless Hierarchy
                // scaling already hands them the root's scale. A particle born mid-scale
                // would come out at the old size, so none are, for the few seconds it lasts.
                ParticleSystem.EmissionModule emission = ps.emission;
                s.world.Add(new WorldParticles
                {
                    system = ps,
                    emitting = emission.enabled,
                    resize = main.scalingMode != ParticleSystemScalingMode.Hierarchy,
                    size3D = main.startSize3D
                });
                emission.enabled = false;
                continue;
            }

            // Leaves only: resizing a transform that has children would move them.
            if (main.scalingMode != ParticleSystemScalingMode.Local || t.childCount > 0) continue;

            Vector3 parent = t.parent != null ? t.parent.lossyScale : Vector3.one;
            if (Mathf.Abs(parent.x) < 1e-6f || Mathf.Abs(parent.y) < 1e-6f || Mathf.Abs(parent.z) < 1e-6f)
                continue;

            s.particles.Add(new ParticleState { system = ps, localScale = t.localScale, mode = main.scalingMode });

            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            t.localScale = new Vector3(t.localScale.x / parent.x, t.localScale.y / parent.y, t.localScale.z / parent.z);
        }
    }

    /// <summary>
    /// A world-space system keeps its particles where they were emitted, whatever its root
    /// does, so a move carries them by hand: every particle from where the world was last put
    /// (<paramref name="from"/>) to where it is put now — position, velocity and size. The
    /// Micro galaxy's spiral arms are such systems, a few hundred particles a frame for a few
    /// seconds.
    /// </summary>
    static void CarryWorldParticles(List<WorldParticles> systems, Placement from, Placement to)
    {
        Quaternion turn = to.turn * Quaternion.Inverse(from.turn);
        float ratio = to.scale / from.scale;

        for (int i = 0; i < systems.Count; i++)
        {
            WorldParticles w = systems[i];
            if (w.system == null) continue;

            int count = w.system.particleCount;
            if (count == 0) continue;
            if (_particleBuffer.Length < count) _particleBuffer = new ParticleSystem.Particle[Mathf.NextPowerOfTwo(count)];

            count = w.system.GetParticles(_particleBuffer);
            for (int j = 0; j < count; j++)
            {
                _particleBuffer[j].position = to.Place(from.Unplace(_particleBuffer[j].position));
                _particleBuffer[j].velocity = turn * _particleBuffer[j].velocity * ratio;

                if (!w.resize) continue;
                if (w.size3D) _particleBuffer[j].startSize3D *= ratio;
                else _particleBuffer[j].startSize *= ratio;
            }
            w.system.SetParticles(_particleBuffer, count);
        }
    }

    static void Restore(Scaled s)
    {
        if (s == null || s.root == null) return;

        s.root.position = s.position;
        s.root.rotation = s.rotation;
        s.root.localScale = s.localScale;

        for (int i = 0; i < s.particles.Count; i++)
        {
            ParticleState p = s.particles[i];
            if (p.system == null) continue;

            ParticleSystem.MainModule main = p.system.main;
            main.scalingMode = p.mode;
            p.system.transform.localScale = p.localScale;
        }

        // Back to where and how big they would be had nothing been moved — the world may be
        // shown again — and emitting as before.
        if (!SamePlacement(s.carried, Placement.AsBuilt)) CarryWorldParticles(s.world, s.carried, Placement.AsBuilt);

        for (int i = 0; i < s.world.Count; i++)
        {
            if (s.world[i].system == null) continue;
            ParticleSystem.EmissionModule emission = s.world[i].system.emission;
            emission.enabled = s.world[i].emitting;
        }

        for (int i = 0; i < s.lights.Count; i++)
            if (s.lights[i].light != null) s.lights[i].light.range = s.lights[i].range;

        s.particles.Clear();
        s.world.Clear();
        s.lights.Clear();
        s.carried = Placement.AsBuilt;
        s.particlesPrepared = false;
    }

    // ── The light ────────────────────────────────────────────────────────────

    /// <param name="size">World-space diameter.</param>
    /// <param name="screenSize">Above 0, a star instead: this diameter on screen, as a fraction
    /// of the screen's height, however near or far, and <paramref name="size"/> is ignored.</param>
    void SetLight(Dive d, Vector3 at, float size, float fade, float screenSize = 0f)
    {
        EnsureLight();
        if (_light == null) return;

        float intensity = d.lightIntensity * Mathf.Clamp01(fade);
        bool on = intensity > 0.001f && (size > 0f || screenSize > 0f);
        if (_light.activeSelf != on) _light.SetActive(on);
        if (!on) return;

        _lightPosition = at;
        _lightSize = size;
        _lightScreenSize = screenSize;
        _lightMaterial.SetColor(ColorId, new Color(d.lightColor.r, d.lightColor.g, d.lightColor.b, intensity));

        // A star's glow takes the middle of a bigger quad, leaving room for its spikes
        // (SetSpikes); any other light is a plain glow filling its quad.
        _lightMaterial.SetFloat(GlowScaleId, screenSize > 0f ? StarQuad : 1f);
        if (screenSize <= 0f) _lightMaterial.SetFloat(SpikesId, 0f);
        PlaceLight();
    }

    /// <summary>
    /// The light as a star: its diffraction spikes' brightness, their length as a fraction of
    /// the room round the glow, and their turn, in radians. Their tips take the cool colour.
    /// </summary>
    void SetSpikes(float brightness, float length, float angle)
    {
        if (_lightMaterial == null) return;
        _lightMaterial.SetFloat(SpikesId, brightness);
        _lightMaterial.SetFloat(SpikeLengthId, length);
        _lightMaterial.SetFloat(SpikeAngleId, angle);
        if (_dive != null) _lightMaterial.SetColor(SpikeTintId, _dive.memberCool);
    }

    void HideLight()
    {
        if (_light != null && _light.activeSelf) _light.SetActive(false);
    }

    /// <summary>Faces the camera. Round, so a frame of lag in the facing never shows.</summary>
    void PlaceLight()
    {
        Camera cam = _camera != null ? _camera : (brain != null ? brain.OutputCamera : Camera.main);

        // A star keeps its size on screen: a point stays a point however close it gets and
        // however far the lens pushes in. Its quad is StarQuad times its glow, for the spikes.
        float size = _lightSize;
        if (_lightScreenSize > 0f && cam != null)
            size = _lightScreenSize * StarQuad * 2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad)
                 * Vector3.Distance(cam.transform.position, _lightPosition);

        Transform t = _light.transform;
        t.position = _lightPosition;
        t.localScale = new Vector3(size, size, size);
        if (cam != null) t.rotation = cam.transform.rotation;
    }

    void EnsureLight()
    {
        if (_light != null) return;

        Shader shader = Resources.Load<Shader>("DiveGlow_NEW");
        if (shader == null)
        {
            if (!_warnedNoLightShader)
                Debug.LogWarning("[LayerDive_NEW] Resources/DiveGlow_NEW.shader is missing; the dive " +
                                 "plays without its light.", this);
            _warnedNoLightShader = true;
            return;
        }

        _lightMaterial = new Material(shader) { name = "DiveGlow (runtime)" };

        _lightQuad = new Mesh { name = "DiveLight quad" };
        _lightQuad.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f)
        };
        _lightQuad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
        _lightQuad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        _lightQuad.RecalculateBounds();

        _light = new GameObject("DiveLight (temporary)");
        // The player's layer: whatever draws the photon draws this.
        if (player != null) _light.layer = player.gameObject.layer;

        _light.AddComponent<MeshFilter>().sharedMesh = _lightQuad;
        MeshRenderer mr = _light.AddComponent<MeshRenderer>();
        mr.sharedMaterial = _lightMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        _light.SetActive(false);
    }

    void DestroyLight()
    {
        if (_light != null) Destroy(_light);
        if (_lightMaterial != null) Destroy(_lightMaterial);
        if (_lightQuad != null) Destroy(_lightQuad);
        _light = null;
        _lightMaterial = null;
        _lightQuad = null;
    }

    // ── The crowd the point resolves into ────────────────────────────────────

    /// <param name="aim">Coming out, which way the cluster's cone points (CrowdAim).</param>
    /// <param name="zoom">How far the crowd has opened around the point — or shrunk into it,
    /// coming out. 1 = as built.</param>
    /// <param name="spin">Turning as it opens: radians round its up for every factor of e it
    /// opens by, which slants the streaks (DiveSwarm_NEW's _Spin).</param>
    void SetCrowd(Dive d, Vector3 at, Quaternion aim, float zoom, float alpha, float streak, float spin = 0f)
    {
        if (!d.resolve || d.memberCount <= 0 || alpha <= 0.001f)
        {
            HideCrowd();
            return;
        }

        EnsureCrowd(d);
        if (_crowd == null) return;
        if (!_crowd.activeSelf) _crowd.SetActive(true);

        Transform t = _crowd.transform;
        t.position = at;
        t.rotation = aim;
        // Going in, flattened into the plane square to the world's up (a galaxy's disc). The
        // shader sizes members by the x scale, so flattening leaves their size alone.
        float thickness = d.direction == Direction.In ? d.crowdFlatten : 1f;
        t.localScale = new Vector3(zoom, zoom * thickness, zoom);

        _crowdMaterial.SetFloat(AlphaId, alpha);
        _crowdMaterial.SetFloat(StreakId, streak);
        _crowdMaterial.SetFloat(SpinId, spin);
        // The shader scales members with the crowd; this takes back all but memberGrowth of it.
        _crowdMaterial.SetFloat(SizeScaleId, Mathf.Pow(zoom, d.memberGrowth - 1f));
        // Going in, members come out of the point's glow; coming out there is no point to see.
        // And coming out they are whole galaxies, seen from inside their cluster: soft
        // smudges rather than points of light — dim enough to stay under the bloom — that
        // fade from well off as they come close, before one fills the screen.
        bool inward = d.direction == Direction.In;
        _crowdMaterial.SetFloat(ResolveId, inward ? 1f : 0f);
        _crowdMaterial.SetFloat(CoreId, inward ? d.memberCore : 0.35f);
        _crowdMaterial.SetVector(NearFadeId, inward ? new Vector4(0.5f, 3f, 0f, 0f) : new Vector4(2.5f, 10f, 0f, 0f));
    }

    void HideCrowd()
    {
        if (_crowd != null && _crowd.activeSelf) _crowd.SetActive(false);
    }

    /// <summary>Builds the crowd for this row, and again if its shape settings change in play.</summary>
    void EnsureCrowd(Dive d)
    {
        int signature = CrowdSignature(d);
        if (_crowd != null && _crowdSignature == signature) return;

        if (_crowd == null)
        {
            Shader shader = Resources.Load<Shader>("DiveSwarm_NEW");
            if (shader == null)
            {
                if (!_warnedNoCrowdShader)
                    Debug.LogWarning("[LayerDive_NEW] Resources/DiveSwarm_NEW.shader is missing; the " +
                                     "point dives without resolving into a crowd.", this);
                _warnedNoCrowdShader = true;
                return;
            }

            _crowdMaterial = new Material(shader) { name = "DiveSwarm (runtime)" };

            _crowd = new GameObject("DiveCrowd (temporary)");
            if (player != null) _crowd.layer = player.gameObject.layer;
            _crowd.AddComponent<MeshFilter>();

            MeshRenderer mr = _crowd.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _crowdMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            _crowd.SetActive(false);
        }

        if (_crowdMesh != null) Destroy(_crowdMesh);
        _crowdMesh = BuildCrowdMesh(d);
        _crowd.GetComponent<MeshFilter>().sharedMesh = _crowdMesh;
        _crowdSignature = signature;
    }

    static int CrowdSignature(Dive d)
    {
        unchecked
        {
            int h = d.memberCount;
            h = h * 31 + (int)d.direction;
            h = h * 31 + d.spread.GetHashCode();
            h = h * 31 + d.crowdRadius.GetHashCode();
            h = h * 31 + d.memberSize.GetHashCode();
            h = h * 31 + d.memberGrowth.GetHashCode();
            h = h * 31 + d.resolveZoom.GetHashCode();
            h = h * 31 + d.memberWarm.GetHashCode();
            h = h * 31 + d.memberCool.GetHashCode();
            h = h * 31 + d.coolShare.GetHashCode();
            h = h * 31 + d.memberAccent.GetHashCode();
            h = h * 31 + d.accentShare.GetHashCode();
            return h;
        }
    }

    /// <summary>
    /// One quad per member, all four corners at its centre; DiveSwarm_NEW spreads them on
    /// screen.
    ///
    /// SCALE-FREE, NOT ONE CLUSTER. Members sit at radii spread evenly in log space, from
    /// crowdRadius down past what the dive's zoom will ever open up. A single cluster
    /// empties in the first second of a steady zoom — everything is outside the view by
    /// the time the dive is half done — while this keeps new members emerging from the
    /// point at the same rate all the way to the peak: a steady stream, no rush. It also
    /// reads as a cluster on the approach: a dense core thinning into a halo. Sizes follow
    /// the radius, so every member leaves the point looking the same. Fixed seed: the same
    /// crowd every run.
    ///
    /// COMING OUT it is the cluster round the world being left, there the whole time the
    /// photon crosses that world: galaxies of one size spread evenly through the volume from
    /// an eighth of crowdRadius (clear of the galaxy itself) out to crowdRadius, in every
    /// direction (spread 180) or in a cone round local +Z, turned by CrowdAim.
    /// </summary>
    static Mesh BuildCrowdMesh(Dive d)
    {
        int n = Mathf.Max(0, d.memberCount);
        var vertices = new Vector3[n * 4];
        var colors = new Color[n * 4];
        var corners = new Vector2[n * 4];
        var sizes = new Vector2[n * 4];
        var triangles = new int[n * 6];

        var rng = new System.Random(7919);
        bool outward = d.direction == Direction.Out;
        // Enough decades that members are still leaving the point when the dive peaks.
        float decades = Mathf.Log10(Mathf.Max(1f, d.resolveZoom)) + 1.4f;
        float inner = d.crowdRadius * 0.125f;
        float cosSpread = Mathf.Cos(d.spread * Mathf.Deg2Rad);
        float reach = 0f;

        for (int i = 0; i < n; i++)
        {
            Vector3 p;
            float radius;
            if (outward)
            {
                // Evenly through the volume between inner and crowdRadius.
                float r3 = inner * inner * inner;
                float r = Mathf.Pow(r3 + (float)rng.NextDouble() * (d.crowdRadius * d.crowdRadius * d.crowdRadius - r3), 1f / 3f);
                p = RandomInCone(rng, cosSpread) * r;
                radius = d.memberSize * Mathf.Lerp(0.5f, 1.5f, (float)rng.NextDouble());
            }
            else
            {
                float fraction = Mathf.Pow(10f, -decades * (float)rng.NextDouble());
                p = RandomUnit(rng) * (d.crowdRadius * fraction);
                radius = d.memberSize * Mathf.Pow(fraction, d.memberGrowth)
                       * Mathf.Lerp(0.6f, 1.4f, (float)rng.NextDouble());
            }

            // One draw for the colour, so a crowd with no accent is the one it always was.
            double pick = rng.NextDouble();
            Color c = pick < d.coolShare ? d.memberCool
                    : pick < d.coolShare + d.accentShare ? d.memberAccent
                    : d.memberWarm;
            c.a = Mathf.Lerp(0.25f, 1f, (float)rng.NextDouble());   // brightness

            int v = i * 4;
            for (int k = 0; k < 4; k++)
            {
                vertices[v + k] = p;
                colors[v + k] = c;
                sizes[v + k] = new Vector2(radius, 0f);
            }
            corners[v + 0] = new Vector2(-1f, -1f);
            corners[v + 1] = new Vector2(1f, -1f);
            corners[v + 2] = new Vector2(1f, 1f);
            corners[v + 3] = new Vector2(-1f, 1f);

            int t = i * 6;
            triangles[t + 0] = v;     triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
            triangles[t + 3] = v;     triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;

            reach = Mathf.Max(reach, p.magnitude);
        }

        var mesh = new Mesh { name = "DiveCrowd" };
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.uv = corners;
        mesh.uv2 = sizes;
        mesh.triangles = triangles;

        // Every vertex sits at a member's centre, so the computed bounds would miss the
        // glow and the streak the shader adds around it. Pad for both.
        float pad = d.memberSize * 1.5f * 21f;
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (2f * (reach + pad)));
        return mesh;
    }

    static Vector3 RandomUnit(System.Random rng)
    {
        double z = rng.NextDouble() * 2.0 - 1.0;
        double a = rng.NextDouble() * Math.PI * 2.0;
        double s = Math.Sqrt(1.0 - z * z);
        return new Vector3((float)(s * Math.Cos(a)), (float)(s * Math.Sin(a)), (float)z);
    }

    /// <summary>Evenly over the cap of the unit sphere around +Z down to <paramref name="cosHalfAngle"/>.</summary>
    static Vector3 RandomInCone(System.Random rng, float cosHalfAngle)
    {
        double z = 1.0 - rng.NextDouble() * (1.0 - cosHalfAngle);
        double a = rng.NextDouble() * Math.PI * 2.0;
        double s = Math.Sqrt(Math.Max(0.0, 1.0 - z * z));
        return new Vector3((float)(s * Math.Cos(a)), (float)(s * Math.Sin(a)), (float)z);
    }

    void DestroyCrowd()
    {
        if (_crowd != null) Destroy(_crowd);
        if (_crowdMaterial != null) Destroy(_crowdMaterial);
        if (_crowdMesh != null) Destroy(_crowdMesh);
        _crowd = null;
        _crowdMaterial = null;
        _crowdMesh = null;
    }

    // ── The halo of the cluster being left ───────────────────────────────────

    /// <param name="sigma">How far out it fades to a third, in world units.</param>
    /// <param name="brightness">Looking through its middle from outside; about half that from inside.</param>
    void SetHalo(Dive d, Vector3 at, float sigma, float brightness)
    {
        if (d.haloRadius <= 0f || sigma <= 1e-3f || brightness <= 0.001f)
        {
            HideHalo();
            return;
        }

        EnsureHalo();
        if (_halo == null) return;
        if (!_halo.activeSelf) _halo.SetActive(true);

        // The cube only has to cover what the glow reaches: three falloff radii out it is
        // down to a ten-thousandth.
        Transform t = _halo.transform;
        t.position = at;
        t.rotation = Quaternion.identity;
        t.localScale = Vector3.one * (6f * sigma);

        _haloMaterial.SetFloat(SigmaId, sigma);
        _haloMaterial.SetColor(ColorId, new Color(d.haloColor.r, d.haloColor.g, d.haloColor.b, brightness));
    }

    void HideHalo()
    {
        if (_halo != null && _halo.activeSelf) _halo.SetActive(false);
    }

    void EnsureHalo()
    {
        if (_halo != null) return;

        Shader shader = Resources.Load<Shader>("DiveHalo_NEW");
        if (shader == null)
        {
            if (!_warnedNoHaloShader)
                Debug.LogWarning("[LayerDive_NEW] Resources/DiveHalo_NEW.shader is missing; the " +
                                 "cluster is left without its glow.", this);
            _warnedNoHaloShader = true;
            return;
        }

        _haloMaterial = new Material(shader) { name = "DiveHalo (runtime)" };

        // A unit cube; the shader draws its inside faces, so it covers the view from inside
        // and the ball's outline from out.
        _haloMesh = new Mesh { name = "DiveHalo cube" };
        _haloMesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f)
        };
        _haloMesh.triangles = new[]
        {
            0, 2, 1, 0, 3, 2,   4, 5, 6, 4, 6, 7,   0, 1, 5, 0, 5, 4,
            3, 6, 2, 3, 7, 6,   0, 4, 7, 0, 7, 3,   1, 2, 6, 1, 6, 5
        };
        _haloMesh.RecalculateBounds();

        _halo = new GameObject("DiveHalo (temporary)");
        if (player != null) _halo.layer = player.gameObject.layer;

        _halo.AddComponent<MeshFilter>().sharedMesh = _haloMesh;
        MeshRenderer mr = _halo.AddComponent<MeshRenderer>();
        mr.sharedMaterial = _haloMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        _halo.SetActive(false);
    }

    void DestroyHalo()
    {
        if (_halo != null) Destroy(_halo);
        if (_haloMaterial != null) Destroy(_haloMaterial);
        if (_haloMesh != null) Destroy(_haloMesh);
        _halo = null;
        _haloMaterial = null;
        _haloMesh = null;
    }

    // ── Whiteout ─────────────────────────────────────────────────────────────

    void BuildOverlay()
    {
        _overlay = new GameObject("DiveWhiteout (temporary)", typeof(RectTransform));

        Canvas canvas = _overlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = overlaySortOrder;

        GameObject go = new GameObject("Veil", typeof(RectTransform));
        go.transform.SetParent(_overlay.transform, false);

        _veil = go.AddComponent<Image>();
        _veil.raycastTarget = false;
        _veil.color = Color.clear;

        RectTransform rt = _veil.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    void SetVeil(float alpha)
    {
        if (_veil == null) return;
        Color c = _dive.lightColor;
        _veil.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(alpha));
    }

    void DestroyOverlay()
    {
        if (_overlay != null) Destroy(_overlay);
        _overlay = null;
        _veil = null;
    }

    // ── Lens ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Swings the camera round to look back, or turns it towards the star, scales the brain's
    /// field of view, and plays the dolly zoom. Runs right after the brain has written the
    /// camera, which it does from scratch every time, so none of this accumulates, and it
    /// composes with JourneyZoom_NEW doing the same.
    /// </summary>
    void OnCameraUpdated(CinemachineBrain updated)
    {
        if (brain != null && updated != brain) return;

        Camera cam = updated.OutputCamera;
        if (cam == null || cam.orthographic) return;

        if (_lookBack > 1e-4f && player != null) LookBack(cam.transform);
        if (_watch > 1e-4f) Watch(cam.transform);
        if (Mathf.Abs(_roll) > 1e-3f) cam.transform.rotation *= Quaternion.AngleAxis(_roll, Vector3.forward);

        if (_fovPush > 1e-4f && _dive != null && _dive.targetFieldOfView > 0f)
        {
            // In log space (of the half-angle's tangent, which is what magnifies), so the
            // push-in runs at the dive's rate rather than speeding up as the lens narrows.
            float from = Mathf.Log(Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
            float to = Mathf.Log(Mathf.Tan(_dive.targetFieldOfView * 0.5f * Mathf.Deg2Rad));
            cam.fieldOfView = 2f * Mathf.Atan(Mathf.Exp(Mathf.Lerp(from, to, _fovPush))) * Mathf.Rad2Deg;
        }

        if (!Mathf.Approximately(_fovMultiplier, 1f))
            cam.fieldOfView = Mathf.Clamp(cam.fieldOfView * _fovMultiplier, 1f, 179f);

        if (Mathf.Abs(_dolly - 1f) > 1e-4f && player != null)
        {
            // Narrow the lens by _dolly and back away along the view by exactly as much as
            // keeps the photon's size: at _dolly times the distance, _dolly times the zoom.
            // Everything beyond the photon is magnified by up to _dolly; the photon is not.
            // Below 1 — only ever while looking back — it runs the other way: the lens
            // widens, the camera closes in, and what lies beyond the photon falls away.
            // Facing forward that would read as the light backing off; facing back, as the
            // light pulling away from what it has left.
            float halfTan = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / _dolly;
            float distance = Vector3.Distance(cam.transform.position, player.transform.position);

            cam.fieldOfView = Mathf.Max(1f, 2f * Mathf.Atan(halfTan) * Mathf.Rad2Deg);
            cam.transform.position -= cam.transform.forward * (distance * (_dolly - 1f));
        }
    }

    /// <summary>
    /// Coming out, the camera partway (_lookBack) round the photon, looking back past it at
    /// the cluster it is leaving. The brain's own camera is swung round the photon, kept at
    /// its distance and raised, and turned to aim between the photon and the cluster, so
    /// the light is in front and what it left behind it. At 0 this is the brain's camera
    /// exactly, so the swing starts and ends without a jump.
    /// </summary>
    void LookBack(Transform cam)
    {
        float w = _lookBack;
        Vector3 photon = player.transform.position;

        Quaternion swing = Quaternion.AngleAxis(_dive.lookBackYaw * w, Vector3.up);
        Vector3 position = photon + swing * (cam.position - photon) + Vector3.up * (_dive.lookBackLift * w);

        Quaternion rotation = swing * cam.rotation;
        Vector3 toAim = Vector3.Lerp(photon, _focus, _dive.lookBackFrame) - position;
        if (toAim.sqrMagnitude > 1e-6f)
            rotation = Quaternion.Slerp(rotation, Quaternion.LookRotation(toAim, Vector3.up), w);

        cam.SetPositionAndRotation(position, rotation);
    }

    /// <summary>
    /// Continuous: the brain's camera turned part way (_watch) from the photon towards the
    /// new world's star, so the star stays in frame from the old world's disc all the way up
    /// into its own place — which the zone camera, looking down at the photon, has above the
    /// top of the frame. Turned, not moved: the photon stays in frame, lower down. At 0 this
    /// is the brain's camera exactly.
    /// </summary>
    void Watch(Transform cam)
    {
        Vector3 toStar = Subject() - cam.position;
        if (toStar.sqrMagnitude < 1e-6f) return;
        cam.rotation = Quaternion.Slerp(cam.rotation, Quaternion.LookRotation(toStar, Vector3.up), _watch);
    }

    // ── The photon, the sound ────────────────────────────────────────────────

    void CaptureTrails()
    {
        _trails = player != null ? player.GetComponentsInChildren<TrailRenderer>(false) : Array.Empty<TrailRenderer>();
        _trailWidths = new float[_trails.Length];
        for (int i = 0; i < _trails.Length; i++)
            _trailWidths[i] = _trails[i] != null ? _trails[i].widthMultiplier : 1f;
    }

    /// <param name="fraction">Of each trail's own width when the dive began.</param>
    void SetTrailWidth(float fraction)
    {
        for (int i = 0; i < _trails.Length; i++)
            if (_trails[i] != null)
                _trails[i].widthMultiplier = _trailWidths[i] * fraction;
    }

    /// <summary>Two-dimensional, so it does not fade as the camera flies away from where it started.</summary>
    void PlayDiveSound(Dive d)
    {
        if (d.diveSound == null) return;

        var go = new GameObject("DiveSound (temporary)");
        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = d.diveSound;
        source.volume = d.diveSoundVolume;
        source.spatialBlend = 0f;
        source.playOnAwake = false;
        source.Play();
        Destroy(go, d.diveSound.length + 0.1f);
    }

    void BuildEffects()
    {
        if (_camera == null) return;

        PP.PostProcessLayer layer = _camera.GetComponent<PP.PostProcessLayer>();
        if (layer == null || !layer.enabled) return;

        // The volume has to sit on a layer the camera listens to.
        int mask = layer.volumeLayer.value;
        if (mask == 0) return;

        int bit = 0;
        while (bit < 31 && (mask & (1 << bit)) == 0) bit++;

        var settings = new List<PP.PostProcessEffectSettings>();

        if (_dive.peakBloom > 0f)
        {
            PP.Bloom bloom = ScriptableObject.CreateInstance<PP.Bloom>();
            bloom.enabled.Override(true);
            bloom.intensity.Override(_dive.peakBloom);
            settings.Add(bloom);
        }

        if (_dive.peakChromaticAberration > 0f)
        {
            PP.ChromaticAberration chroma = ScriptableObject.CreateInstance<PP.ChromaticAberration>();
            chroma.enabled.Override(true);
            chroma.intensity.Override(_dive.peakChromaticAberration);
            settings.Add(chroma);
        }

        if (_dive.peakVignette > 0f)
        {
            _vignette = ScriptableObject.CreateInstance<PP.Vignette>();
            _vignette.enabled.Override(true);
            _vignette.intensity.Override(_dive.peakVignette);
            _vignette.smoothness.Override(0.6f);
            _vignette.center.Override(new Vector2(0.5f, 0.5f));
            settings.Add(_vignette);
        }

        if (!Mathf.Approximately(_dive.peakLensDistortion, 0f))
        {
            _lens = ScriptableObject.CreateInstance<PP.LensDistortion>();
            _lens.enabled.Override(true);
            _lens.intensity.Override(_dive.peakLensDistortion);
            _lens.centerX.Override(0f);
            _lens.centerY.Override(0f);
            settings.Add(_lens);
        }

        if (settings.Count == 0) return;

        _volume = PP.PostProcessManager.instance.QuickVolume(bit, 100f, settings.ToArray());
        _volume.weight = 0f;
    }

    void SetEffects(float weight)
    {
        if (_volume == null) return;

        _volume.weight = Mathf.Clamp01(weight);

        // Distort, and close the funnel, around the point being dived into — not the middle
        // of the screen. Continuous, around the star, wherever it has grown to.
        if ((_lens != null || _vignette != null) && _camera != null)
        {
            Vector3 vp = _camera.WorldToViewportPoint(Continuous ? Subject() : _focus);
            if (vp.z > 0f)
            {
                if (_lens != null)
                {
                    _lens.centerX.value = Mathf.Clamp(vp.x * 2f - 1f, -1f, 1f);
                    _lens.centerY.value = Mathf.Clamp(vp.y * 2f - 1f, -1f, 1f);
                }

                if (_vignette != null)
                    _vignette.center.value = new Vector2(Mathf.Clamp01(vp.x), Mathf.Clamp01(vp.y));
            }
        }
    }

    void DestroyEffects()
    {
        if (_volume != null) PP.RuntimeUtilities.DestroyVolume(_volume, true, true);
        _volume = null;
        _lens = null;
        _vignette = null;
    }

    // ── Curves ───────────────────────────────────────────────────────────────

    /// <summary>
    /// 0 to 1 at a constant rate after a short ease-in, so the magnification — exponential
    /// in this — grows by the same factor every second. The first version squared time
    /// here, which is what read as a sudden rush.
    /// </summary>
    static float SteadyRamp(float u)
    {
        u = Mathf.Clamp01(u);
        float p = u < SteadyEaseIn ? u * u / (2f * SteadyEaseIn) : u - SteadyEaseIn * 0.5f;
        return p * SteadyRate;
    }

    const float SteadyEaseIn = 0.12f;

    /// <summary>SteadyRamp's slope once past its ease-in.</summary>
    const float SteadyRate = 1f / (1f - SteadyEaseIn * 0.5f);

    /// <summary>
    /// SteadyRamp with a landing: the same ease-in and constant rate, then the last 30%
    /// slowing to a stop. For a crossfading dive, which arrives instead of being cut off.
    /// </summary>
    static float LandingRamp(float u)
    {
        const float easeOut = 0.3f;
        const float rate = 1f / (1f - (SteadyEaseIn + easeOut) * 0.5f);
        u = Mathf.Clamp01(u);
        if (u < SteadyEaseIn) return rate * u * u / (2f * SteadyEaseIn);
        if (u <= 1f - easeOut) return rate * (u - SteadyEaseIn * 0.5f);
        float s = 1f - u;
        return 1f - rate * s * s / (2f * easeOut);
    }

    /// <summary>A dive in that crossfades into the next world instead of a whiteout swap.</summary>
    bool Crossfading => _dive != null && _dive.direction == Direction.In && !_dive.continuous && _dive.crossfade > 0f;

    /// <summary>A dive in that is one zoom on one clock, with nothing hidden (CONTINUOUS on the class).</summary>
    bool Continuous => _dive != null && _dive.direction == Direction.In && _dive.continuous;

    /// <summary>Smoothstep of x between from and to.</summary>
    static float Smooth(float from, float to, float x)
    {
        float t = Mathf.Clamp01((x - from) / Mathf.Max(1e-5f, to - from));
        return t * t * (3f - 2f * t);
    }

    static float EaseOut(float x)
    {
        x = 1f - Mathf.Clamp01(x);
        return 1f - x * x * x;
    }
}

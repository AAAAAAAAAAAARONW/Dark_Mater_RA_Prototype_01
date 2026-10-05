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
/// emitting and their particles are carried along by hand, and put back after.
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
                 "of a solar system moving out to their orbits round its star.")]
        [Min(0.01f)] public float enterScale = 1f;

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
        public Vector3 localScale;
        public bool particlesPrepared;
        public readonly List<ParticleState> particles = new List<ParticleState>();

        // World-space systems, whose particles are carried along by hand, and the scale and
        // pivot they were last carried to. One pivot per scale, as every caller uses.
        public readonly List<WorldParticles> world = new List<WorldParticles>();
        public float carried = 1f;
        public Vector3 pivot;
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

        if (worlds != null) worlds.Hold(this);

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

        // The old world grows around the point ahead going in. Coming out it shrinks into
        // its own middle, behind the photon, with the rest of its cluster.
        ScaleAround(_leave, _focus, Mathf.Pow(outward ? 1f / _dive.diveZoom : _dive.diveZoom, p));

        // The lens pushes in on the point at the dive's own rate.
        _fovPush = _dive.targetFieldOfView > 0f ? p : 0f;

        float dissolve = Smooth(_dive.dissolveFrom, 1f, u);
        float glow = Mathf.Lerp(1f, _dive.leaveGlow, Smooth(0f, 0.85f, u));
        if (worlds != null) worlds.SetGroupLook(_fromId, 1f - dissolve, glow);

        SetVeil(_dive.whiteout * Smooth(0.7f, 1f, u));
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

        // Going in, the light swells steadily from its size at the gate, and hands over to
        // the whiteout as the camera arrives at it: a quad at the camera would cut through
        // the near plane. Crossfading, it grows as the dive magnifies — by the same factor
        // each second — into what it is about to become.
        float size = Crossfading
            ? _dive.gateSize * Mathf.Pow(_dive.peakSize / Mathf.Max(1e-3f, _dive.gateSize), p)
            : Mathf.Lerp(_dive.gateSize, _dive.peakSize, p);
        float arriving = _camera != null ? Smooth(1.5f, 6f, Vector3.Distance(_camera.transform.position, _focus)) : 1f;
        SetLight(_dive, _focus, size, arriving);

        // The crowd opens at the dive's own constant rate, only much further, out of the
        // point and past the camera; it has handed over to the whiteout by the peak.
        float crowd = Mathf.Lerp(_dive.approachCrowd, _dive.diveCrowd, Smooth(0f, 0.2f, u)) * (1f - Smooth(0.8f, 1f, u));
        float streak = _dive.streak * Smooth(0f, 0.25f, u) * (1f - Smooth(0.85f, 1f, u));
        SetCrowd(_dive, _focus, _crowdAim, Mathf.Pow(_dive.resolveZoom, p), crowd, streak);

        SetEffects(Smooth(0.2f, 1f, u));

        // Crossfading, the next world starts to show through the old one before the peak.
        if (Crossfading && worlds != null) worlds.SetGroupLook(_toId, _dive.crossfade * Smooth(0.6f, 1f, u));

        // And the world swells behind the photon, at the same rate: the dolly zoom.
        _dolly = Mathf.Pow(_dive.dollyZoom, p);
    }

    /// <summary>
    /// The hold at the peak. <paramref name="h"/> runs 0 to 1. Going in the whiteout just
    /// holds; coming out, the cluster keeps collapsing and the camera keeps looking back.
    /// </summary>
    public void TickHold(float h)
    {
        if (!IsActive || _dive.direction != Direction.Out) return;

        _clock = _dive.diveSeconds + Mathf.Clamp01(h) * _dive.peakHoldSeconds;
        TickComingOut();
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
        if ((Crossfading || _dive.direction == Direction.Out) && nebula != null)
            nebula.BlendNextOver(Mathf.Max(0.5f, 0.8f * (_dive.peakHoldSeconds + _dive.emergeSeconds)));

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

        // If the way on out of the world just entered leaves a cluster, that cluster comes in
        // with the world, rather than its galaxies appearing on their own afterwards.
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
        _clock = 0f;
        SetTrailWidth(1f);
        _trails = Array.Empty<TrailRenderer>();
        _trailWidths = Array.Empty<float>();

        DestroyOverlay();
        DestroyEffects();
        HideLight();
        HideCrowd();
        HideHalo();

        IsActive = false;

        if (worlds != null) worlds.Release(this, _toId);
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
        return new Scaled { root = root, position = root.position, localScale = root.localScale };
    }

    static void ScaleAround(Scaled s, Vector3 pivot, float k)
    {
        if (s == null || s.root == null) return;

        // Prepared on the first real scale, while the root is still where it was built —
        // the parents' scale has to be measured at 1.
        if (!s.particlesPrepared && !Mathf.Approximately(k, 1f)) PrepareParticles(s);

        s.root.position = pivot + (s.position - pivot) * k;
        s.root.localScale = s.localScale * k;

        if (s.world.Count > 0 && !Mathf.Approximately(k, s.carried))
        {
            CarryWorldParticles(s.world, pivot, k / s.carried);
            s.carried = k;
            s.pivot = pivot;
        }
    }

    /// <summary>
    /// See the class summary: Local-scaling systems follow the root for the length of a
    /// scale, and world-space systems stop emitting and are listed to be carried by hand.
    /// </summary>
    static void PrepareParticles(Scaled s)
    {
        s.particlesPrepared = true;

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
    /// does, so a scale moves them by hand: every particle, around the same pivot, by the
    /// change since the last call — position, velocity and size. The Micro galaxy's spiral
    /// arms are such systems, a few hundred particles a frame for a few seconds.
    /// </summary>
    static void CarryWorldParticles(List<WorldParticles> systems, Vector3 pivot, float ratio)
    {
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
                _particleBuffer[j].position = pivot + (_particleBuffer[j].position - pivot) * ratio;
                _particleBuffer[j].velocity *= ratio;

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
        s.root.localScale = s.localScale;

        for (int i = 0; i < s.particles.Count; i++)
        {
            ParticleState p = s.particles[i];
            if (p.system == null) continue;

            ParticleSystem.MainModule main = p.system.main;
            main.scalingMode = p.mode;
            p.system.transform.localScale = p.localScale;
        }

        // Back to where and how big they would be had nothing been scaled — the world may be
        // shown again — and emitting as before.
        if (!Mathf.Approximately(s.carried, 1f)) CarryWorldParticles(s.world, s.pivot, 1f / s.carried);

        for (int i = 0; i < s.world.Count; i++)
        {
            if (s.world[i].system == null) continue;
            ParticleSystem.EmissionModule emission = s.world[i].system.emission;
            emission.enabled = s.world[i].emitting;
        }

        s.particles.Clear();
        s.world.Clear();
        s.carried = 1f;
        s.particlesPrepared = false;
    }

    // ── The light ────────────────────────────────────────────────────────────

    void SetLight(Dive d, Vector3 at, float size, float fade)
    {
        EnsureLight();
        if (_light == null) return;

        float intensity = d.lightIntensity * Mathf.Clamp01(fade);
        bool on = intensity > 0.001f && size > 0f;
        if (_light.activeSelf != on) _light.SetActive(on);
        if (!on) return;

        _lightPosition = at;
        _lightSize = size;
        _lightMaterial.SetColor(ColorId, new Color(d.lightColor.r, d.lightColor.g, d.lightColor.b, intensity));
        PlaceLight();
    }

    void HideLight()
    {
        if (_light != null && _light.activeSelf) _light.SetActive(false);
    }

    /// <summary>Faces the camera. Round, so a frame of lag in the facing never shows.</summary>
    void PlaceLight()
    {
        Camera cam = _camera != null ? _camera : (brain != null ? brain.OutputCamera : Camera.main);

        Transform t = _light.transform;
        t.position = _lightPosition;
        t.localScale = new Vector3(_lightSize, _lightSize, _lightSize);
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
    void SetCrowd(Dive d, Vector3 at, Quaternion aim, float zoom, float alpha, float streak)
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
        t.localScale = new Vector3(zoom, zoom, zoom);

        _crowdMaterial.SetFloat(AlphaId, alpha);
        _crowdMaterial.SetFloat(StreakId, streak);
        // The shader scales members with the crowd; this takes back all but memberGrowth of it.
        _crowdMaterial.SetFloat(SizeScaleId, Mathf.Pow(zoom, d.memberGrowth - 1f));
        // Going in, members come out of the point's glow; coming out there is no point to see.
        // And coming out they are whole galaxies, seen from inside their cluster: soft
        // smudges rather than points of light — dim enough to stay under the bloom — that
        // fade from well off as they come close, before one fills the screen.
        bool inward = d.direction == Direction.In;
        _crowdMaterial.SetFloat(ResolveId, inward ? 1f : 0f);
        _crowdMaterial.SetFloat(CoreId, inward ? 1f : 0.35f);
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

            Color c = rng.NextDouble() < d.coolShare ? d.memberCool : d.memberWarm;
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
    /// Swings the camera round to look back, scales the brain's field of view, and plays
    /// the dolly zoom. Runs right after the brain has written the camera, which it does
    /// from scratch every time, so none of this accumulates, and it composes with
    /// JourneyZoom_NEW doing the same.
    /// </summary>
    void OnCameraUpdated(CinemachineBrain updated)
    {
        if (brain != null && updated != brain) return;

        Camera cam = updated.OutputCamera;
        if (cam == null || cam.orthographic) return;

        if (_lookBack > 1e-4f && player != null) LookBack(cam.transform);

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
        // of the screen.
        if ((_lens != null || _vignette != null) && _camera != null)
        {
            Vector3 vp = _camera.WorldToViewportPoint(_focus);
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
    bool Crossfading => _dive != null && _dive.direction == Direction.In && _dive.crossfade > 0f;

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

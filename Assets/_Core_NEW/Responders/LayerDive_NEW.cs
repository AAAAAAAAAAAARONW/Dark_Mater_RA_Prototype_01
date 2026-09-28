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
/// COMING OUT (Direction.Out) runs all of it the other way, for a gate that goes up a
/// scale — a galaxy back out to the cosmic web. The world just left collapses into a point
/// ahead and fades; the crowd gathers in from all round, from beyond the edges and from
/// behind, into a knot of light; the lens widens while the camera closes in, so the world
/// falls away behind a photon that stays the same size, and the photon's trail widens —
/// the light growing against its world. Past the whiteout the next world closes in around
/// the knot from three times its size, and the knot shrinks to a speck in it: the whole
/// galaxy just left, now one small light in the web.
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
        /// Up a scale: out of the world just left, which collapses into the point ahead and
        /// becomes a speck in the next world (a galaxy back out to the cosmic web). Every
        /// magnitude below is used the other way round: the old world shrinks by diveZoom,
        /// the crowd gathers in from resolveZoom, the lens widens by dollyZoom, the photon
        /// grows by 1 / photonShrink, and the next world closes in from enterScale.
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

        [Tooltip("In = down a scale, into the point. Out = up a scale: the world just left " +
                 "collapses into the point and becomes a speck in the next one.")]
        public Direction direction = Direction.In;

        /// <summary>
        /// Starting values for a gate that goes up a scale, tuned for the galaxy back out to
        /// the cosmic web. At that gate the Micro galaxy is just behind the camera and twelve
        /// below — the photon is still inside its stars — so collapsing it into the point
        /// ahead sends it swooping past the camera and away into the distance. To a tenth,
        /// so it ends in the knot as one of its members, not beside it; dissolving only in
        /// the last stretch, once it has been seen getting small. No approach light: the
        /// destination is everything, not a point. The web closes in around the knot from
        /// three times its size.
        /// </summary>
        public static Dive PullOut(string toLayerId)
        {
            return new Dive
            {
                toLayerId = toLayerId,
                direction = Direction.Out,
                focusPastGate = 30f,
                approachDistance = 0f,
                emergeSeconds = 3.5f,
                diveZoom = 10f,
                leaveGlow = 1.5f,
                dissolveFrom = 0.6f,
                enterScale = 3f,
                lightColor = new Color(1f, 0.9f, 0.75f, 1f),
                lightIntensity = 1.2f,
                pointSize = 1.5f,
                gateSize = 4f,
                peakSize = 14f,
                memberCount = 900,
                crowdRadius = 8f,
                memberSize = 0.25f,
                resolveZoom = 40f,
                streak = 4f,
                approachCrowd = 0f,
                coolShare = 0.4f,
                dollyZoom = 2.2f,
                photonShrink = 0.45f,
                peakVignette = 0f,
                peakBloom = 2.5f,
                peakChromaticAberration = 0.1f
            };
        }

        [Header("Where")]
        [Tooltip("How far past the gate, along the direction of travel, the point sits. " +
                 "Light only travels in a straight line, so the gate's position is enough: " +
                 "at the Micro gate the yellow cluster's densest knot is 11 past the trigger.")]
        public float focusPastGate = 11f;

        [Tooltip("Optional. Dive into this object's position instead, for a target that is " +
                 "not on the line.")]
        public Transform focusOverride;

        [Header("Approach (before the gate)")]
        [Tooltip("The light appears at the point this far before the gate, in world units, " +
                 "and grows as the photon closes in. 0 = it appears at the gate.")]
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
                 "1 = fade in only.")]
        [Min(0.01f)] public float enterScale = 1f;

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
                 "smaller, so each one leaves the point looking the same.")]
        [Min(0.001f)] public float memberSize = 0.22f;

        [Tooltip("How far the crowd opens up by the peak, at the same constant rate as the dive. " +
                 "Far more than diveZoom on purpose: the small scale races past while the " +
                 "large one hardly moves.")]
        [Min(1f)] public float resolveZoom = 60f;

        [Tooltip("How much a member grows as the crowd opens: 0 keeps it a point, 1 grows it " +
                 "with the crowd.")]
        [Range(0f, 1f)] public float memberGrowth = 0.45f;

        [Tooltip("How far the members stretch into streaks as they stream past.")]
        [Range(0f, 20f)] public float streak = 5f;

        [Tooltip("Brightness of the crowd on the approach, while it is still one glow.")]
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

    readonly Dictionary<string, LayerGate_NEW> _gatesByLayer = new Dictionary<string, LayerGate_NEW>();

    Dive _dive;
    string _fromId;
    string _toId;
    Vector3 _focus;
    Vector3 _emergeFocus;
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

    GameObject _overlay;
    Image _veil;

    PP.PostProcessVolume _volume;
    PP.LensDistortion _lens;
    PP.Vignette _vignette;

    float _fovMultiplier = 1f;
    float _dolly = 1f;

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
    }

    void OnDestroy()
    {
        DestroyLight();
        DestroyCrowd();
    }

    /// <summary>The approach: the light in the cluster ahead, before the gate is reached.</summary>
    void Update()
    {
        if (IsActive) return;   // the dive draws the light itself

        if (TryApproach(out Dive dive, out Vector3 focus, out float closeness))
        {
            float appear = Smooth(0f, 0.4f, closeness);
            SetLight(dive, focus, Mathf.Lerp(dive.pointSize, dive.gateSize, closeness), appear);
            SetCrowd(dive, focus, 1f, dive.approachCrowd * appear, 0f);
        }
        else
        {
            HideLight();
            HideCrowd();
        }
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
        IsActive = true;

        if (worlds != null) worlds.Hold(this);

        _focus = DiveFocus();
        // Coming out, the next world closes in around the knot the old one collapsed into.
        _emergeFocus = dive.direction == Direction.Out ? _focus : NextWorldCentre();
        _leave = Capture(worlds != null ? worlds.GroupRoot(_fromId) : null);
        _enter = Capture(worlds != null ? worlds.GroupRoot(_toId) : null);

        // The next world stays out of sight until the peak.
        if (worlds != null) worlds.SetGroupLook(_toId, 0f);

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
        float p = SteadyRamp(u);

        // The old world grows around the point going in, and collapses into it coming out.
        ScaleAround(_leave, _focus, Mathf.Pow(outward ? 1f / _dive.diveZoom : _dive.diveZoom, p));

        float dissolve = Smooth(_dive.dissolveFrom, 1f, u);
        float glow = Mathf.Lerp(1f, _dive.leaveGlow, Smooth(0f, 0.85f, u));
        if (worlds != null) worlds.SetGroupLook(_fromId, 1f - dissolve, glow);

        if (outward)
        {
            // Coming out, the light is the knot the crowd gathers into: it contracts and
            // brightens as the members arrive.
            SetLight(_dive, _focus, Mathf.Lerp(_dive.peakSize, _dive.gateSize, p), Smooth(0.25f, 0.9f, u));
        }
        else
        {
            // Going in, the light swells steadily from its size at the gate, and hands over
            // to the whiteout as the camera arrives at it: a quad at the camera would cut
            // through the near plane.
            float size = Mathf.Lerp(_dive.gateSize, _dive.peakSize, p);
            float arriving = _camera != null ? Smooth(1.5f, 6f, Vector3.Distance(_camera.transform.position, _focus)) : 1f;
            SetLight(_dive, _focus, size, arriving);
        }

        SetVeil(_dive.whiteout * Smooth(0.7f, 1f, u));

        // The crowd moves at the dive's own constant rate, only much further: going in it
        // opens up past the camera, coming out it gathers in from all round — from beyond
        // the edges and from behind — into the knot. Either way it hands over to the
        // whiteout at the end.
        float crowd = outward
            ? _dive.diveCrowd * Smooth(0f, 0.25f, u) * (1f - Smooth(0.85f, 1f, u))
            : Mathf.Lerp(_dive.approachCrowd, _dive.diveCrowd, Smooth(0f, 0.2f, u)) * (1f - Smooth(0.8f, 1f, u));
        float streak = _dive.streak * Smooth(0f, 0.25f, u) * (1f - Smooth(0.85f, 1f, u));
        SetCrowd(_dive, _focus, Mathf.Pow(_dive.resolveZoom, outward ? 1f - p : p), crowd, streak);

        _fovMultiplier = Mathf.Lerp(1f, _dive.fieldOfViewScale, Smooth(0f, 1f, u));
        SetEffects(Smooth(0.2f, 1f, u));

        // The photon against its world, at the same constant rate: small going in (the
        // world swells behind it, the light narrows), large coming out (the world falls
        // away behind it, the light widens).
        _dolly = Mathf.Pow(outward ? 1f / _dive.dollyZoom : _dive.dollyZoom, p);
        SetTrailWidth(Mathf.Pow(outward ? 1f / _dive.photonShrink : _dive.photonShrink, p));
    }

    /// <summary>The peak: the old world goes back where it was, out of sight, and the new one is readied.</summary>
    public void Crossover()
    {
        if (!IsActive) return;

        Restore(_leave);
        if (worlds != null) worlds.SetGroupLook(_fromId, 0f);

        ScaleAround(_enter, _emergeFocus, _dive.enterScale);
        if (worlds != null) worlds.SetGroupLook(_toId, 0f);

        // Under the whiteout, the light moves to where the next world appears (coming out,
        // it is already there: the knot stays), and the lens and the photon snap back: the
        // new world opens at normal framing, the light at its normal size — in a world that
        // is now its scale.
        SetVeil(_dive.whiteout);
        SetLight(_dive, _emergeFocus, _dive.direction == Direction.Out ? _dive.gateSize : _dive.peakSize * 0.5f, 1f);
        HideCrowd();
        _dolly = 1f;
        SetTrailWidth(1f);
    }

    /// <summary>The next world appearing around the light. <paramref name="v"/> runs 0 to 1.</summary>
    public void TickEmerge(float v)
    {
        if (!IsActive) return;
        v = Mathf.Clamp01(v);

        float settle = EaseOut(v);

        // In log space, so growing from a hundredth reads as evenly as growing from a half —
        // and closing in from three times the size (coming out) reads as evenly as either.
        ScaleAround(_enter, _emergeFocus, Mathf.Pow(_dive.enterScale, 1f - settle));

        if (worlds != null) worlds.SetGroupLook(_toId, Smooth(0f, 0.7f, v));

        if (_dive.direction == Direction.Out)
        {
            // The knot shrinks to a speck in the web closing in around it — the whole galaxy
            // just left, now one small light — and is the last thing to go.
            SetLight(_dive, _emergeFocus, Mathf.Lerp(_dive.gateSize, _dive.pointSize, settle), 1f - Smooth(0.55f, 1f, v));
        }
        else
        {
            SetLight(_dive, _emergeFocus, Mathf.Lerp(_dive.peakSize * 0.5f, _dive.pointSize, settle), 1f - Smooth(0.3f, 1f, v));
        }
        SetVeil(_dive.whiteout * (1f - Smooth(0f, 0.45f, v)));

        _fovMultiplier = Mathf.Lerp(_dive.fieldOfViewScale, 1f, Smooth(0f, 1f, v));
        SetEffects(1f - Smooth(0f, 0.9f, v));
    }

    /// <summary>Finished: hand the worlds back and remove everything the dive made.</summary>
    public void End()
    {
        if (!IsActive) return;
        if (debugLog) Debug.Log($"[LayerDive_NEW] Into '{_toId}'.", this);
        Cleanup();
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
        SetTrailWidth(1f);
        _trails = Array.Empty<TrailRenderer>();
        _trailWidths = Array.Empty<float>();

        DestroyOverlay();
        DestroyEffects();
        HideLight();
        HideCrowd();

        IsActive = false;

        if (worlds != null) worlds.Release(this, _toId);
    }

    // ── Where ────────────────────────────────────────────────────────────────

    /// <summary>The row whose gate the photon is closing on, within its approach distance.</summary>
    bool TryApproach(out Dive dive, out Vector3 focus, out float closeness)
    {
        dive = null;
        focus = default;
        closeness = 0f;
        if (player == null) return false;

        string current = state != null ? state.CurrentLayerId : null;

        for (int i = 0; i < gates.Length; i++)
        {
            Dive d = gates[i];
            if (d == null || !d.enabled || d.approachDistance <= 0f) continue;
            if (string.Equals(current, d.toLayerId, StringComparison.Ordinal)) continue;   // already through
            if (!_gatesByLayer.TryGetValue(d.toLayerId, out LayerGate_NEW gate) || gate == null) continue;

            float before = Vector3.Dot(GateCentre(gate) - player.transform.position, TravelDirection());
            if (before <= 0f || before > d.approachDistance) continue;

            dive = d;
            focus = FocusFor(d, gate);
            closeness = 1f - before / d.approachDistance;
            return true;
        }

        return false;
    }

    Vector3 DiveFocus()
    {
        if (_dive.focusOverride != null) return _dive.focusOverride.position;

        if (_gatesByLayer.TryGetValue(_toId, out LayerGate_NEW gate) && gate != null)
            return FocusFor(_dive, gate);

        // No trigger to measure from — a jump straight into the layer. Straight ahead.
        if (player != null) return player.transform.position + TravelDirection() * _dive.focusPastGate;
        return _camera != null ? _camera.transform.position + _camera.transform.forward * _dive.focusPastGate : transform.position;
    }

    Vector3 FocusFor(Dive d, LayerGate_NEW gate)
    {
        if (d.focusOverride != null) return d.focusOverride.position;
        return GateCentre(gate) + TravelDirection() * d.focusPastGate;
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

    /// <summary>
    /// The middle of what the next world will show. Active objects only: groups keep
    /// disabled prototypes around, and those would pull the point off the real thing.
    /// </summary>
    Vector3 NextWorldCentre()
    {
        Transform root = worlds != null ? worlds.GroupRoot(_toId) : null;
        if (root == null) return _focus;

        Vector3 sum = Vector3.zero;
        int n = 0;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(false))
        {
            sum += r.transform.position;
            n++;
        }

        return n > 0 ? sum / n : root.position;
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

    /// <param name="zoom">How far the crowd has opened around the point. 1 = as built.</param>
    void SetCrowd(Dive d, Vector3 at, float zoom, float alpha, float streak)
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
        t.rotation = Quaternion.identity;
        t.localScale = new Vector3(zoom, zoom, zoom);

        _crowdMaterial.SetFloat(AlphaId, alpha);
        _crowdMaterial.SetFloat(StreakId, streak);
        // The shader scales members with the crowd; this takes back all but memberGrowth of it.
        _crowdMaterial.SetFloat(SizeScaleId, Mathf.Pow(zoom, d.memberGrowth - 1f));
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
        // Enough decades that members are still leaving the point when the dive peaks.
        float decades = Mathf.Log10(Mathf.Max(1f, d.resolveZoom)) + 1.4f;
        float reach = 0f;

        for (int i = 0; i < n; i++)
        {
            float fraction = Mathf.Pow(10f, -decades * (float)rng.NextDouble());
            Vector3 p = RandomUnit(rng) * (d.crowdRadius * fraction);
            float radius = d.memberSize * Mathf.Pow(fraction, d.memberGrowth)
                         * Mathf.Lerp(0.6f, 1.4f, (float)rng.NextDouble());

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

    void DestroyCrowd()
    {
        if (_crowd != null) Destroy(_crowd);
        if (_crowdMaterial != null) Destroy(_crowdMaterial);
        if (_crowdMesh != null) Destroy(_crowdMesh);
        _crowd = null;
        _crowdMaterial = null;
        _crowdMesh = null;
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
    /// Scales the brain's field of view, and plays the dolly zoom. Runs right after the
    /// brain has written the camera, which it does from scratch every time, so none of
    /// this accumulates, and it composes with JourneyZoom_NEW doing the same.
    /// </summary>
    void OnCameraUpdated(CinemachineBrain updated)
    {
        if (brain != null && updated != brain) return;

        Camera cam = updated.OutputCamera;
        if (cam == null || cam.orthographic) return;

        if (!Mathf.Approximately(_fovMultiplier, 1f))
            cam.fieldOfView = Mathf.Clamp(cam.fieldOfView * _fovMultiplier, 1f, 179f);

        if (Mathf.Abs(_dolly - 1f) > 1e-4f && player != null)
        {
            // Narrow the lens by _dolly and back away along the view by exactly as much as
            // keeps the photon's size: at _dolly times the distance, _dolly times the zoom.
            // Everything beyond the photon is magnified by up to _dolly; the photon is not.
            // Below 1 it runs the other way: the lens widens, the camera closes in, and the
            // world behind the photon falls away while the photon stays put.
            float halfTan = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / _dolly;
            float distance = Vector3.Distance(cam.transform.position, player.transform.position);

            cam.fieldOfView = Mathf.Max(1f, 2f * Mathf.Atan(halfTan) * Mathf.Rad2Deg);
            cam.transform.position -= cam.transform.forward * (distance * (_dolly - 1f));
        }
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
        const float easeIn = 0.12f;
        u = Mathf.Clamp01(u);
        float p = u < easeIn ? u * u / (2f * easeIn) : u - easeIn * 0.5f;
        return p / (1f - easeIn * 0.5f);
    }

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

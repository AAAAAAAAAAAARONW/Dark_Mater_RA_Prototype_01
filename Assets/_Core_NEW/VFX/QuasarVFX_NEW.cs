using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A quasar, as the artists' impressions draw one, and alive: a black hole at its heart, its
/// shadow edged by a thin ring of light with the far side of the disc bent round it; an accretion
/// disc round it, blue-white inside, gold, orange-red at the rim, in rings of gas with dark lanes
/// of dust between them, turning fast inside and slowly outside and drifting in, embers of
/// hot gas orbiting in it and hot spots flaring near its inner edge; sheets of red and purple
/// gas swirling round it in filaments; and two jets out along its axis, white at the heart,
/// blue round it, wound with filaments, with knots and clumps of plasma running out.
///
/// Every so often it erupts: the core flashes and throws out diffraction spikes, a ring of
/// light runs out across the disc, and a blob of light is thrown out along the jets.
///
/// Put it on an empty object. The disc lies in the object's XZ plane, the jets go along its
/// ±Y, and its radius is the object's scale — so tilt and size it with the transform, or let
/// aimJetAt point the jets. It builds what it draws itself, as hidden children that are
/// never saved, in the editor too, so it can be placed by eye.
///
/// HOW IT IS DRAWN. All procedural; the only texture is noise it makes itself (QuasarNoise_NEW):
///   disc   An annulus (QuasarDisc_NEW), coloured per pixel from where on the disc it is, the
///          spiral sheared by the Keplerian turn; the side turning towards the camera beamed
///          brighter.
///   gas    More annuli, larger and tilted a little off the disc, the same shader keeping
///          only its filaments.
///   core   A quad turned to the camera (QuasarCore_NEW): glow, spikes, and — with no hole —
///          a small shadow and photon ring of its own.
///   hole   A quad turned to the camera (QuasarHole_NEW): the black hole's shadow, its photon
///          ring and the lensed far side of the disc. The disc is drawn in two halves split at
///          the middle's depth, the far one before the hole and the near one after, so the
///          shadow blocks what is behind the hole and the near side of the disc crosses in
///          front of it, as in the pictures. The embers behind it are put out too.
///   jets   A box round them (QuasarJet_NEW), each pixel adding up the light along its ray —
///          so they are right side on, end on, and from inside, where they are a tunnel. Two
///          copies: the side of the disc away from the camera before the disc, so its dust
///          darkens it, and the near side after.
///   matter Quads moved on the GPU (QuasarSparks_NEW): embers, hot spots, jet plasma.
///
/// SCRIPTED. WindUp() winds it up over windUpSeconds: everything turns and flows faster,
/// brightens and flickers harder, the embers stretch into streaks. Erupt() erupts it, and
/// lets a wind-up go. Calm() puts it back. The tutorial's C1, emission and reset call them.
///
/// LATER: AS A HOT DISC REALLY GLOWS. The disc was yellow at its inner edge and red outside it,
/// one warm hue throughout, and the hole a third of the disc across: an opaque black ball with
/// the core's diffraction spikes drawn over it. Now the colours follow the disc's temperature,
/// hottest inside: blue-white at the inner edge, gold, orange-red at the rim (and beamed bluer
/// on the side coming at the camera); the hole is a fifth of the disc, its inner edge just
/// outside it as the last stable orbit is; the spikes go once the hole is big enough on screen
/// to be seen as one; the lensed far side of the disc arches over the shadow as a band; the
/// jets are brightest at their base and fade along their length; and the gas round it is
/// dimmer, from orange inside to violet outside, with more haze between its filaments.
///
/// IT FADES WITH ITS WORLD. Every shader takes _Color, which WorldSwitcher_NEW and the dives
/// fade a world by, and it is built before WorldSwitcher_NEW gathers its groups' renderers
/// (it runs first, by execution order), so a quasar inside a render group comes and goes —
/// and is scaled by a dive — with the rest of it.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
[HierarchyBadge_NEW("QUASAR", "#F08A3C")]
public class QuasarVFX_NEW : MonoBehaviour
{
    [Header("Accretion disc (its radius is this object's scale)")]
    [Tooltip("The inner edge, as a fraction of the radius. Inside it is the core.")]
    [Range(0.02f, 0.4f)] [SerializeField] float innerEdge = 0.07f;

    [Tooltip("Turns a second at the inner edge. Further out it turns slower, as r^-1.5.")]
    [SerializeField] float spin = 0.25f;

    [Tooltip("How far the gas spirals as it falls in.")]
    [SerializeField] float twist = 2.2f;

    [Tooltip("Rings of gas across the disc.")]
    [Min(1f)] [SerializeField] float rings = 12f;

    [Tooltip("How fast the gas drifts in, in its own pattern's widths a second.")]
    [SerializeField] float inflow = 0.05f;

    [Tooltip("Dark lanes of dust between the rings.")]
    [Range(0f, 1f)] [SerializeField] float dust = 0.75f;

    [Tooltip("How far the rim breaks up into tendrils.")]
    [Range(0f, 1f)] [SerializeField] float tendrils = 0.6f;

    [Tooltip("Fine flicker running through the gas.")]
    [Range(0f, 1f)] [SerializeField] float shimmer = 0.35f;

    [Tooltip("Relativistic beaming: the side of the disc turning towards the camera brighter and " +
             "whiter, the other dimmer and redder. Nothing shows face on.")]
    [Range(0f, 1f)] [SerializeField] float beaming = 0.5f;

    [Tooltip("The gas's speed at the inner edge, as a fraction of light's. Drives the beaming.")]
    [Range(0f, 0.9f)] [SerializeField] float orbitalSpeed = 0.45f;

    [ColorUsage(false, true)] [SerializeField] Color hot = new Color(2f, 2.1f, 2.5f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color middle = new Color(2.2f, 1.25f, 0.4f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color rim = new Color(0.75f, 0.14f, 0.05f, 1f);
    [SerializeField] Color dustColour = new Color(0.03f, 0.01f, 0.005f, 1f);

    [Tooltip("Up close the inner disc fills the frame; lower this there, so it is not all white.")]
    [Range(0f, 3f)] [SerializeField] float discBrightness = 0.8f;

    [Header("Gas round it")]
    [Tooltip("Sheets of gas swirling round the disc, each a little more tilted. 0 = none.")]
    [Range(0, 6)] [SerializeField] int gasSheets = 3;

    [Tooltip("How far out the gas reaches, in disc radii.")]
    [Min(1f)] [SerializeField] float gasReach = 2.6f;

    [Tooltip("How much more each sheet is tilted off the disc than the one before, in degrees.")]
    [SerializeField] float gasTilt = 14f;

    [ColorUsage(false, true)] [SerializeField] Color gasInner = new Color(1f, 0.3f, 0.08f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color gasOuter = new Color(0.22f, 0.07f, 0.32f, 1f);

    [Range(0f, 2f)] [SerializeField] float gasBrightness = 0.35f;

    [Tooltip("1 keeps only the gas's filaments; lower fills in a haze.")]
    [Range(0f, 1f)] [SerializeField] float gasWisps = 0.7f;

    [Header("Core")]
    [Tooltip("The core's glow, in disc radii.")]
    [Min(0.01f)] [SerializeField] float coreSize = 0.35f;

    [ColorUsage(false, true)] [SerializeField] Color coreHot = new Color(4f, 3.7f, 3.2f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color coreGlow = new Color(1.1f, 0.6f, 0.25f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color coreHalo = new Color(0.3f, 0.1f, 0.03f, 1f);

    [Tooltip("With no hole (hole 0): a small black hole's shadow at the very middle, as a fraction of " +
             "the core's glow. 0 = none.")]
    [Range(0f, 0.2f)] [SerializeField] float shadow = 0.03f;

    [Tooltip("With no hole: the thin ring of light round that shadow.")]
    [Min(0f)] [SerializeField] float photonRing = 1.5f;

    [Tooltip("Diffraction spikes: a point that bright draws them in any telescope. 0 = none.")]
    [Min(0f)] [SerializeField] float spikes = 1f;

    [Tooltip("Their length, as a fraction of half the screen's height — sized on the screen, as a " +
             "lens's are, not in the world.")]
    [Min(0.01f)] [SerializeField] float spikeLength = 0.3f;

    [SerializeField] float spikeAngle = 45f;

    [Tooltip("A horizontal streak through the core, as a wide lens draws one.")]
    [Min(0f)] [SerializeField] float streak = 0.6f;

    [Header("Black hole")]
    [Tooltip("The black hole at the middle: its shadow's radius, as a fraction of the disc's. The " +
             "disc's inner edge is kept outside it (at 1.25 times this at least), as the gas's last " +
             "orbit is. It goes as the camera comes within three of its radii, so the light can fly " +
             "through without the frame going black. 0 = none: the core is a white-hot point, as before.")]
    [Range(0f, 0.45f)] [SerializeField] float hole = 0.2f;

    [Tooltip("The thin ring of light round the hole's shadow.")]
    [Min(0f)] [SerializeField] float holeRing = 1f;

    [Tooltip("The far side of the disc, bent by the hole's gravity into an arc round its shadow — " +
             "over its top and under its bottom, seen edge on.")]
    [Range(0f, 2f)] [SerializeField] float lensing = 1f;

    [Tooltip("How much of the core's glow lies over the hole, which is in front of it: a little, so " +
             "it reads as a hole in the light rather than a cut-out.")]
    [Range(0f, 1f)] [SerializeField] float holeHaze = 0.06f;

    [Tooltip("How much of the jet nearer the camera shows over the hole. Looking down a jet, as the " +
             "tutorial does, its whole length adds up over the core and would fill the hole with light.")]
    [Range(0f, 1f)] [SerializeField] float jetOverHole = 0.12f;

    [Header("Jets")]
    [Tooltip("Lay the jets along the line through here and this — the light, in the tutorial, so it " +
             "flies in down one jet and is emitted out along the other. Empty: the object's own ±Y.")]
    [SerializeField] Transform aimJetAt;

    [Tooltip("Their length, in disc radii. 0 = none.")]
    [Min(0f)] [SerializeField] float jetLength = 2.6f;

    [Tooltip("Their width at the tip, as a fraction of their length.")]
    [Range(0.005f, 0.2f)] [SerializeField] float jetWidth = 0.035f;

    [ColorUsage(false, true)] [SerializeField] Color jetHeart = new Color(0.75f, 0.9f, 1.25f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color jetSheath = new Color(0.35f, 0.55f, 1.25f, 1f);

    [Range(0f, 3f)] [SerializeField] float jetBrightness = 1f;

    [Tooltip("The jet on the far side of the disc, against the near one: its light is beamed away.")]
    [Range(0f, 1f)] [SerializeField] float counterJet = 0.5f;

    [Tooltip("How hollow the blue sheath is. Hollow, it is brightest at its walls, so from inside " +
             "it is a tunnel rather than a fog.")]
    [Range(0f, 0.95f)] [SerializeField] float hollow = 0.6f;

    [Range(0f, 2f)] [SerializeField] float sheathBrightness = 0.7f;

    [Tooltip("How many turns the filaments wind round a jet along its length.")]
    [SerializeField] float filamentTurns = 1.5f;

    [Tooltip("How fast the filaments flow out, in lengths a second. Inside a jet, slower than the " +
             "light, or they will seem to pull away ahead of it instead of streaming past.")]
    [SerializeField] float filamentSpeed = 0.15f;

    [Tooltip("Bright knots along a jet.")]
    [Min(0f)] [SerializeField] float knots = 6f;

    [Tooltip("How fast the knots run out, in lengths a second.")]
    [SerializeField] float knotSpeed = 0.35f;

    [Tooltip("The brightest a jet gets, where it adds up end on.")]
    [Min(0.5f)] [SerializeField] float jetCeiling = 3f;

    [Tooltip("Slabs a jet is cut into along its length, for adding up its light. More is smoother " +
             "end on, and costs more.")]
    [Range(8, 48)] [SerializeField] int jetQuality = 32;

    [Header("Moving matter")]
    [Tooltip("Embers of hot gas orbiting in the disc.")]
    [Range(0, 4000)] [SerializeField] int embers = 900;

    [Tooltip("Bright hot spots flaring near the disc's inner edge.")]
    [Range(0, 200)] [SerializeField] int hotSpots = 24;

    [Tooltip("Clumps of plasma running out along the jets.")]
    [Range(0, 2000)] [SerializeField] int jetClumps = 300;

    [Tooltip("Their speed along a jet, in lengths a second: slowest and fastest.")]
    [SerializeField] Vector2 clumpSpeed = new Vector2(0.25f, 0.6f);

    [Tooltip("Sizes in disc radii: ember, hot spot, jet clump.")]
    [SerializeField] Vector3 matterSizes = new Vector3(0.008f, 0.015f, 0.01f);

    [Range(0f, 4f)] [SerializeField] float matterBrightness = 0.5f;

    [Tooltip("How long a moving particle's streak is, in seconds of its motion.")]
    [Min(0f)] [SerializeField] float matterStreak = 0.6f;

    [Tooltip("How far in an ember drifts over its life, as a fraction of where it started.")]
    [Range(0f, 0.9f)] [SerializeField] float emberDrift = 0.3f;

    [Header("Life")]
    [Tooltip("How much the core and the inner disc flicker.")]
    [Range(0f, 1f)] [SerializeField] float flicker = 0.15f;

    [Tooltip("Flickers a second, about.")]
    [Min(0f)] [SerializeField] float flickerRate = 0.7f;

    [Tooltip("Seconds between eruptions of its own, about. 0 = only when told.")]
    [Min(0f)] [SerializeField] float eruptEvery = 14f;

    [Tooltip("How much those vary, either way, as a fraction.")]
    [Range(0f, 0.9f)] [SerializeField] float eruptJitter = 0.4f;

    [Tooltip("How strong those are; Erupt() is 1.")]
    [Range(0f, 1f)] [SerializeField] float eruptStrength = 0.6f;

    [Tooltip("Erupt, fully, soon after the main camera first finds it — so when the camera turns to look " +
             "at it, it does something.")]
    [SerializeField] bool eruptWhenSeen = true;

    [Tooltip("How soon after.")]
    [Min(0f)] [SerializeField] float eruptSeenDelay = 0.7f;

    [Tooltip("How fast the eruption's ring runs out across the disc, in radii a second.")]
    [Min(0.01f)] [SerializeField] float waveSpeed = 0.35f;

    [Tooltip("How long the ring takes to fade, seconds.")]
    [Min(0.1f)] [SerializeField] float waveFade = 1.6f;

    [Tooltip("How fast the eruption's blob runs out along the jets, in lengths a second.")]
    [Min(0f)] [SerializeField] float blobSpeed = 0.25f;

    [Tooltip("How long the blob takes to fade, seconds.")]
    [Min(0.1f)] [SerializeField] float blobFade = 2.5f;

    [Header("Wind-up (WindUp, then Erupt)")]
    [Tooltip("Seconds to wind all the way up. Most of it lands late.")]
    [Min(0.1f)] [SerializeField] float windUpSeconds = 12f;

    [Tooltip("How much faster everything turns and flows, wound up.")]
    [Min(1f)] [SerializeField] float windUpPace = 3f;

    [Tooltip("How much brighter, wound up.")]
    [Min(1f)] [SerializeField] float windUpBrightness = 1.6f;

    [Tooltip("How much harder it flickers, wound up.")]
    [Range(0f, 1f)] [SerializeField] float windUpFlicker = 0.35f;

    [Tooltip("How much longer the matter's streaks, wound up.")]
    [Min(1f)] [SerializeField] float windUpStreak = 3f;

    [Tooltip("Seconds to wind back down after it erupts.")]
    [Min(0.1f)] [SerializeField] float releaseSeconds = 3f;

    [Tooltip("If nothing erupts it, it winds back down on its own this long after it was fully " +
             "wound — so a visitor walking away mid wind-up does not leave it like that.")]
    [Min(0f)] [SerializeField] float windUpHold = 45f;

    [Header("Variation")]
    [Tooltip("Another quasar looks different with another seed.")]
    [SerializeField] int seed = 0;

    // Built on enable, gone on disable; never saved, never shown in the hierarchy.
    const HideFlags Built = HideFlags.HideAndDontSave;

    // Transparent is 3000. The far jet and the gas first, so the disc's dust darkens them; the
    // half of the disc beyond the middle, then the hole, which blocks all of those behind it;
    // the near half of the disc, which crosses in front of the hole; the core, the near jet
    // and the moving matter after.
    const int FarJetQueue = 2996, GasQueue = 2997, FarDiscQueue = 2998, HoleQueue = 2999, DiscQueue = 3000,
              CoreQueue = 3001, NearJetQueue = 3002, MatterQueue = 3003;

    readonly List<GameObject> _parts = new List<GameObject>();
    readonly List<Material> _materials = new List<Material>();
    readonly List<Material> _gas = new List<Material>();
    readonly List<Mesh> _meshes = new List<Mesh>();
    Material _disc, _farDisc, _hole, _core, _farJet, _nearJet, _matter;
    Transform _farJetPart, _nearJetPart;
    GameObject _holePart;
    Renderer _coreRenderer;
    Mesh _matterMesh;
    int _builtSheets = -1, _builtMatter = -1;
    bool _rebuild, _dirty;

    // Life, in play.
    float _activity;
    float _windStart = -1f, _releaseFrom = -1f, _activityAtRelease;
    float _eruptAt = -1000f, _eruptGain;
    float _nextEruption = -1f, _seenEruptAt = -1f;
    bool _wasVisible;
    float _pace = 1f, _paceOffset;
    float _flickerPhase;

    /// <summary>The disc's radius, in world units.</summary>
    public float Radius => transform.lossyScale.x;

    /// <summary>The disc's inner edge, kept outside the hole's shadow.</summary>
    float InnerEdge => hole > 0f ? Mathf.Max(innerEdge, hole * 1.25f) : innerEdge;

    /// <summary>
    /// The hole is all there from twice this far from the middle (disc radii) and gone at this
    /// far: one and a half of its radii, so it is gone before it fills the frame, and never
    /// sooner than the core's glow, which the light flies through.
    /// </summary>
    float HoleNear => Mathf.Max(coreSize, hole * 1.5f);

    // ── Scripted ────────────────────────────────────────────────────────────

    /// <summary>Erupt now, fully, and let go of any wind-up.</summary>
    public void Erupt() => Erupt(1f);

    /// <summary>Wind up over windUpSeconds, until Erupt or Calm.</summary>
    public void WindUp()
    {
        _windStart = Time.time;
        _releaseFrom = -1f;
    }

    /// <summary>Back to how it was: no wind-up, no eruption under way.</summary>
    public void Calm()
    {
        _windStart = -1f;
        _releaseFrom = -1f;
        _activity = 0f;
        _eruptAt = -1000f;
        _eruptGain = 0f;
        _seenEruptAt = -1f;
        ScheduleEruption();
    }

    void Erupt(float strength)
    {
        _eruptAt = Time.time;
        _eruptGain = strength;
        if (_windStart >= 0f) Release();
        ScheduleEruption();
    }

    void Release()
    {
        _windStart = -1f;
        _releaseFrom = Time.time;
        _activityAtRelease = _activity;
    }

    void ScheduleEruption()
    {
        _nextEruption = eruptEvery > 0f
            ? Time.time + eruptEvery * Random.Range(1f - eruptJitter, 1f + eruptJitter)
            : -1f;
    }

    // ── Lifecycle ───────────────────────────────────────────────────────────

    void OnEnable()
    {
        Build();
        Aim();
        if (Application.isPlaying) ScheduleEruption();
    }

    void OnDisable() => Clear();

    void OnValidate()
    {
        // Objects may not be made or destroyed from here, and transforms are better left alone:
        // note what changed, and Update does it.
        _rebuild |= (_builtSheets >= 0 && _builtSheets != gasSheets)
                 || (_builtMatter >= 0 && _builtMatter != embers + hotSpots + jetClumps);
        _dirty = true;
#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
    }

    void Update()
    {
        if (_rebuild)
        {
            _rebuild = false;
            Clear();
            Build();
        }
        else if (_dirty && _disc != null)
        {
            Apply();
        }
        _dirty = false;

        if (Application.isPlaying && _disc != null) Live();
    }

    void LateUpdate() => Aim();

    /// <summary>
    /// Lays the jets' axis along the line through here and aimJetAt, turning as little as it
    /// can. The line, not the direction: the jets go both ways, so when the light passes
    /// through the core and out the other side the quasar must not flip over to keep
    /// pointing its +Y at it. And not right at the core, where the line is undefined.
    /// </summary>
    void Aim()
    {
        if (aimJetAt == null) return;
        Vector3 to = aimJetAt.position - transform.position;
        float near = 0.02f * Mathf.Max(Radius, 1e-3f);
        if (to.sqrMagnitude < near * near) return;

        Vector3 axis = to.normalized;
        if (Vector3.Dot(axis, transform.up) < 0f) axis = -axis;
        if (Vector3.Angle(transform.up, axis) > 0.01f)
            transform.rotation = Quaternion.FromToRotation(transform.up, axis) * transform.rotation;
    }

    /// <summary>
    /// Is the core on the main camera's screen, and drawn? The main camera's, not
    /// Renderer.isVisible: that counts the Scene view's camera too, so in the editor a quasar
    /// on screen there would use its eruption up before the game's camera turned to it.
    /// </summary>
    bool InView()
    {
        if (_coreRenderer == null || !_coreRenderer.enabled) return false;
        Camera camera = Camera.main;
        if (camera == null) return _coreRenderer.isVisible;

        Vector3 v = camera.WorldToViewportPoint(transform.position);
        return v.z > 0f && v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f;
    }

    /// <summary>Wind-up, flicker and eruptions, onto the materials, every frame in play.</summary>
    void Live()
    {
        float now = Time.time;

        // Winding up: squared, so most of it lands late. Unwinding: eased.
        if (_windStart >= 0f)
        {
            float u = Mathf.Clamp01((now - _windStart) / windUpSeconds);
            _activity = u * u;
            if (now - _windStart > windUpSeconds + windUpHold) Release();
        }
        else if (_releaseFrom >= 0f)
        {
            float u = Mathf.Clamp01((now - _releaseFrom) / releaseSeconds);
            _activity = _activityAtRelease * (1f - u * u * (3f - 2f * u));
            if (u >= 1f) _releaseFrom = -1f;
        }

        // Its own eruptions, not while it is being wound up for one.
        if (_nextEruption >= 0f && now >= _nextEruption && _windStart < 0f) Erupt(eruptStrength);

        // And one when the camera first finds it.
        bool visible = InView();
        if (eruptWhenSeen && visible && !_wasVisible && now - _eruptAt > 6f) _seenEruptAt = now + eruptSeenDelay;
        _wasVisible = visible;
        if (_seenEruptAt >= 0f && now >= _seenEruptAt)
        {
            _seenEruptAt = -1f;
            if (visible) Erupt(1f);
        }

        // The clock speeds up without jumping: the shaders' time is _Time.y * pace + offset,
        // and _Time.y is the time since the scene loaded.
        float pace = Mathf.Lerp(1f, windUpPace, _activity);
        _paceOffset += Time.timeSinceLevelLoad * (_pace - pace);
        _pace = pace;

        _flickerPhase += Time.deltaTime * flickerRate * (1f + 3f * _activity);
        float amount = flicker + windUpFlicker * _activity;
        float pulse = 1f + amount * (Mathf.PerlinNoise(_flickerPhase, seed * 1.37f + 0.5f) - 0.5f) * 2f;

        float bright = Mathf.Lerp(1f, windUpBrightness, _activity);
        float age = now - _eruptAt;

        foreach (Material m in _materials)
        {
            m.SetFloat(PaceId, _pace);
            m.SetFloat(PaceOffsetId, _paceOffset);
            m.SetFloat(BrightId, bright);
            m.SetFloat(PulseId, pulse);
            m.SetFloat(FlareAgeId, age);
            m.SetFloat(FlareGainId, _eruptGain);
        }
        _matter.SetFloat(StretchId, matterStreak * Mathf.Lerp(1f, windUpStreak, _activity));
    }

    static readonly int PaceId = Shader.PropertyToID("_Pace");
    static readonly int PaceOffsetId = Shader.PropertyToID("_PaceOffset");
    static readonly int BrightId = Shader.PropertyToID("_Bright");
    static readonly int PulseId = Shader.PropertyToID("_Pulse");
    static readonly int FlareAgeId = Shader.PropertyToID("_FlareAge");
    static readonly int FlareGainId = Shader.PropertyToID("_FlareGain");
    static readonly int StretchId = Shader.PropertyToID("_Stretch");

    // ── Building ────────────────────────────────────────────────────────────

    void Build()
    {
        if (_disc != null) return;

        Shader discShader = Resources.Load<Shader>("QuasarDisc_NEW");
        Shader coreShader = Resources.Load<Shader>("QuasarCore_NEW");
        Shader jetShader = Resources.Load<Shader>("QuasarJet_NEW");
        Shader matterShader = Resources.Load<Shader>("QuasarSparks_NEW");
        Shader holeShader = Resources.Load<Shader>("QuasarHole_NEW");
        if (discShader == null || coreShader == null || jetShader == null || matterShader == null || holeShader == null)
        {
            Debug.LogWarning("[QuasarVFX_NEW] Its shaders (Resources/Quasar*_NEW.shader) are missing; nothing drawn.", this);
            return;
        }

        Mesh annulus = Keep(Annulus(160, 24));
        Mesh quad = Keep(Quad());
        Mesh box = Keep(Box());

        _farDisc = Part("Disc (far half)", annulus, discShader, FarDiscQueue, Quaternion.identity, Vector3.one, out _);
        _disc = Part("Disc", annulus, discShader, DiscQueue, Quaternion.identity, Vector3.one, out _);
        _hole = Part("Hole", quad, holeShader, HoleQueue, Quaternion.identity, Vector3.one, out _holePart);

        _builtSheets = gasSheets;
        for (int s = 0; s < gasSheets; s++)
        {
            float tilt = (s - (gasSheets - 1) * 0.5f) * gasTilt;
            Quaternion turn = Quaternion.Euler(tilt, s * 47f, tilt * 0.6f);
            _gas.Add(Part("Gas " + s, annulus, discShader, GasQueue, turn, Vector3.one * SheetReach(s), out _));
        }

        _core = Part("Core", quad, coreShader, CoreQueue, Quaternion.identity, Vector3.one, out GameObject core);
        _coreRenderer = core.GetComponent<Renderer>();

        _farJet = Part("Far jet", box, jetShader, FarJetQueue, Quaternion.identity, Vector3.one, out GameObject far);
        _farJetPart = far.transform;
        _nearJet = Part("Near jet", box, jetShader, NearJetQueue, Quaternion.identity, Vector3.one, out GameObject near);
        _nearJetPart = near.transform;

        _builtMatter = embers + hotSpots + jetClumps;
        _matterMesh = Keep(Matter(embers, hotSpots, jetClumps, seed));
        _matter = Part("Matter", _matterMesh, matterShader, MatterQueue, Quaternion.identity, Vector3.one, out _);

        Apply();
    }

    float SheetReach(int s) => gasReach * (1f - 0.12f * s);

    Mesh Keep(Mesh mesh)
    {
        mesh.hideFlags = Built;
        _meshes.Add(mesh);
        return mesh;
    }

    Material Part(string name, Mesh mesh, Shader shader, int queue, Quaternion rotation, Vector3 scale, out GameObject go)
    {
        go = new GameObject("Quasar " + name) { hideFlags = Built };
        go.layer = gameObject.layer;
        Transform t = go.transform;
        t.SetParent(transform, false);
        t.localPosition = Vector3.zero;
        t.localRotation = rotation;
        t.localScale = scale;

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        var material = new Material(shader) { name = "Quasar " + name + " (runtime)", hideFlags = Built, renderQueue = queue };
        mr.sharedMaterial = material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        _parts.Add(go);
        _materials.Add(material);
        return material;
    }

    void Clear()
    {
        foreach (GameObject go in _parts) Kill(go);
        foreach (Material m in _materials) Kill(m);
        foreach (Mesh m in _meshes) Kill(m);
        _parts.Clear();
        _materials.Clear();
        _meshes.Clear();
        _gas.Clear();
        _disc = _farDisc = _hole = _core = _farJet = _nearJet = _matter = null;
        _farJetPart = _nearJetPart = null;
        _holePart = null;
        _coreRenderer = null;
        _matterMesh = null;
        _builtSheets = _builtMatter = -1;
    }

    static void Kill(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }

    /// <summary>
    /// Every setting onto the materials, and the moving ones back to rest. _Color is left
    /// alone: it is the world's fade.
    /// </summary>
    void Apply()
    {
        Texture2D noise = QuasarNoise_NEW.Texture;

        // The disc in two halves, either side of the middle's depth (see the hole).
        float inner = InnerEdge;
        foreach (Material half in new[] { _farDisc, _disc })
            SetDisc(half, inner, spin, twist, rings, inflow, dust, tendrils, 2f, 0f, shimmer, beaming, orbitalSpeed, 0.7f,
                    hot * discBrightness, middle * discBrightness, rim * discBrightness, dustColour, 1f, waveSpeed, seed, noise);
        _farDisc.SetFloat("_Split", 1f);
        _disc.SetFloat("_Split", -1f);

        // The gas: only its filaments, with no dust and nothing hidden behind it; its ring
        // runs at the disc's, in its own larger radius.
        Color gasMiddle = Color.Lerp(gasInner, gasOuter, 0.4f);
        for (int s = 0; s < _gas.Count; s++)
            SetDisc(_gas[s], 0.32f, 0.03f, 3f, 5f, inflow * 0.4f, 0f, 1f, 0f, gasWisps, 0.2f, 0f, 0f, 0f,
                    gasInner * gasBrightness, gasMiddle * gasBrightness, gasOuter * gasBrightness, Color.black,
                    0.25f, waveSpeed / SheetReach(s), seed + 13.7f * (s + 1), noise);

        _core.SetFloat("_Size", coreSize);
        _core.SetColor("_Hot", coreHot);
        _core.SetColor("_Glow", coreGlow);
        _core.SetColor("_Halo", coreHalo);
        // With the hole, its own small shadow is left off: the hole is the shadow.
        bool holeOn = hole > 0f;
        _core.SetFloat("_Shadow", holeOn ? 0f : shadow);
        _core.SetFloat("_Ring", photonRing);
        _core.SetFloat("_Hole", holeOn ? hole / Mathf.Max(coreSize, 1e-3f) : 0f);
        _core.SetFloat("_HoleNear", HoleNear / Mathf.Max(coreSize, 1e-3f));
        _core.SetFloat("_HoleHaze", holeHaze);
        _core.SetFloat("_Spikes", spikes);
        _core.SetFloat("_SpikeLength", spikeLength);
        _core.SetFloat("_SpikeAngle", spikeAngle);
        _core.SetFloat("_Streak", streak);

        // The black hole: its shadow, photon ring and the lensed far side of the disc.
        _holePart.SetActive(holeOn);
        _hole.SetTexture("_Noise", noise);
        _hole.SetFloat("_Radius", hole);
        _hole.SetFloat("_Near", HoleNear);
        _hole.SetFloat("_Ring", holeRing);
        _hole.SetColor("_RingColor", coreHot * 0.5f);
        _hole.SetFloat("_Lens", lensing);
        _hole.SetColor("_ArcHot", hot * discBrightness);
        _hole.SetColor("_ArcMid", middle * discBrightness);
        _hole.SetFloat("_Spin", spin);
        _hole.SetFloat("_Beaming", beaming);
        _hole.SetFloat("_Seed", seed);

        // The jets' box: as long as they are, and wide enough for the sheath at the tip.
        float length = Mathf.Max(1e-3f, jetLength);
        float width = jetWidth * length * 2.5f;
        Vector3 extent = new Vector3(width, length, width);
        bool jets = jetLength > 0f && jetBrightness > 0f;
        SetJet(_farJet, -1f, extent, jets, noise);
        SetJet(_nearJet, 1f, extent, jets, noise);
        foreach (Material jet in new[] { _farJet, _nearJet })
        {
            jet.SetFloat("_HoleRadius", holeOn ? hole : 0f);
            jet.SetFloat("_HoleNear", HoleNear);
            jet.SetFloat("_HoleJet", jetOverHole);
        }
        _farJetPart.localScale = extent;
        _nearJetPart.localScale = extent;
        _farJetPart.gameObject.SetActive(jets);
        _nearJetPart.gameObject.SetActive(jets);

        _matter.SetFloat("_Inner", inner);
        _matter.SetFloat("_HoleRadius", holeOn ? hole : 0f);
        _matter.SetFloat("_HoleNear", HoleNear);
        _matter.SetFloat("_Spin", spin);
        _matter.SetFloat("_Drift", emberDrift);
        _matter.SetColor("_Hot", hot * discBrightness);
        _matter.SetColor("_Mid", middle * discBrightness);
        _matter.SetColor("_Outer", rim * discBrightness);
        // No jets, no plasma in them: black, which adds nothing.
        _matter.SetColor("_JetColor", jets ? Color.Lerp(jetHeart, jetSheath, 0.5f) * jetBrightness : Color.black);
        _matter.SetFloat("_JetLength", jets ? jetLength : 0f);
        _matter.SetFloat("_JetWidth", jetWidth);
        _matter.SetFloat("_TwistTurns", filamentTurns);
        _matter.SetVector("_JetSpeed", new Vector4(clumpSpeed.x, clumpSpeed.y, 0f, 0f));
        _matter.SetFloat("_CounterJet", counterJet);
        _matter.SetVector("_Sizes", matterSizes);
        _matter.SetFloat("_Brightness", matterBrightness);
        _matter.SetFloat("_Stretch", matterStreak);
        _matterMesh.bounds = new Bounds(Vector3.zero,
            2.2f * new Vector3(Mathf.Max(1f, width), Mathf.Max(1f, jets ? length : 0f), Mathf.Max(1f, width)));

        foreach (Material m in _materials)
        {
            m.SetFloat(PaceId, _pace);
            m.SetFloat(PaceOffsetId, _paceOffset);
            m.SetFloat(BrightId, 1f);
            m.SetFloat(PulseId, 1f);
            m.SetFloat(FlareAgeId, 1000f);
            m.SetFloat(FlareGainId, 0f);
        }
    }

    void SetJet(Material m, float side, Vector3 extent, bool on, Texture2D noise)
    {
        m.SetTexture("_Noise", noise);
        m.SetVector("_Extent", extent);
        m.SetFloat("_Length", extent.y);
        m.SetFloat("_Width", jetWidth);
        m.SetColor("_Core", jetHeart * (on ? jetBrightness : 0f));
        m.SetColor("_Sheath", jetSheath * (on ? jetBrightness : 0f));
        m.SetFloat("_Side", side);
        m.SetFloat("_CounterJet", counterJet);
        m.SetFloat("_Hollow", hollow);
        m.SetFloat("_SheathBright", sheathBrightness);
        m.SetFloat("_TwistTurns", filamentTurns);
        m.SetFloat("_StrandFlow", filamentSpeed);
        m.SetFloat("_KnotCount", knots);
        m.SetFloat("_KnotFlow", knotSpeed);
        m.SetFloat("_EruptSpeed", blobSpeed);
        m.SetFloat("_EruptDecay", blobFade);
        m.SetFloat("_MaxBright", jetCeiling);
        m.SetFloat("_Steps", jetQuality);
    }

    void SetDisc(Material m, float inner, float turnRate, float spiral, float ringCount, float drift,
                 float dustLanes, float rimBreakUp, float rimLight, float wisps, float shimmerAmount,
                 float beam, float speed, float opacity, Color inside, Color between, Color outside, Color dustTint,
                 float waveLight, float waveRate, float discSeed, Texture2D noise)
    {
        m.SetTexture("_Noise", noise);
        m.SetFloat("_Inner", inner);
        m.SetFloat("_Spin", turnRate);
        m.SetFloat("_Twist", spiral);
        m.SetFloat("_Rings", ringCount);
        m.SetFloat("_Around", 3f);
        m.SetFloat("_Inflow", drift);
        m.SetFloat("_Dust", dustLanes);
        m.SetFloat("_Tendrils", rimBreakUp);
        m.SetFloat("_Rim", rimLight);
        m.SetFloat("_Wisps", wisps);
        m.SetFloat("_Shimmer", shimmerAmount);
        m.SetFloat("_Beaming", beam);
        m.SetFloat("_Speed", speed);
        m.SetFloat("_Opacity", opacity);
        m.SetColor("_Hot", inside);
        m.SetColor("_Mid", between);
        m.SetColor("_Outer", outside);
        m.SetColor("_DustColor", dustTint);
        m.SetFloat("_WaveLight", waveLight);
        m.SetFloat("_WaveSpeed", waveRate);
        m.SetFloat("_WaveDecay", waveFade);
        m.SetFloat("_Seed", discSeed);
    }

    // ── Meshes ──────────────────────────────────────────────────────────────

    /// <summary>A flat disc of radius 1 in XZ, in rings, so the fade at its edges has vertices to follow.</summary>
    static Mesh Annulus(int around, int across)
    {
        var vertices = new List<Vector3> { Vector3.zero };
        var triangles = new List<int>();
        for (int j = 1; j <= across; j++)
        {
            float r = (float)j / across;
            for (int i = 0; i < around; i++)
            {
                float a = i * Mathf.PI * 2f / around;
                vertices.Add(new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
            }
        }

        for (int i = 0; i < around; i++)
        {
            int next = (i + 1) % around;
            triangles.Add(0); triangles.Add(1 + next); triangles.Add(1 + i);
        }
        for (int j = 1; j < across; j++)
        {
            int ring = 1 + (j - 1) * around, outer = ring + around;
            for (int i = 0; i < around; i++)
            {
                int next = (i + 1) % around;
                triangles.Add(ring + i); triangles.Add(ring + next); triangles.Add(outer + next);
                triangles.Add(ring + i); triangles.Add(outer + next); triangles.Add(outer + i);
            }
        }

        var mesh = new Mesh { name = "Quasar disc" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>A unit quad; the core's shader sizes it and turns it to the camera, so its bounds hold any of that.</summary>
    static Mesh Quad()
    {
        var mesh = new Mesh { name = "Quasar core" };
        mesh.vertices = new[] { new Vector3(-1f, -1f, 0f), new Vector3(1f, -1f, 0f), new Vector3(1f, 1f, 0f), new Vector3(-1f, 1f, 0f) };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
        return mesh;
    }

    /// <summary>A box from -1 to 1, faces outward; the jets' shader draws its inside.</summary>
    static Mesh Box()
    {
        var mesh = new Mesh { name = "Quasar jets" };
        mesh.vertices = new[]
        {
            new Vector3(-1, -1, -1), new Vector3(1, -1, -1), new Vector3(1, 1, -1), new Vector3(-1, 1, -1),
            new Vector3(-1, -1, 1), new Vector3(1, -1, 1), new Vector3(1, 1, 1), new Vector3(-1, 1, 1)
        };
        mesh.triangles = new[]
        {
            0, 2, 1, 0, 3, 2,   4, 5, 6, 4, 6, 7,   0, 1, 5, 0, 5, 4,
            3, 6, 2, 3, 7, 6,   0, 4, 7, 0, 7, 3,   1, 2, 6, 1, 6, 5
        };
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// One quad per particle, all at the origin: the shader moves them. The corners are in the
    /// first UV set, and three seeds and the kind (0 ember, 1 hot spot, 2 jet clump) in the second.
    /// </summary>
    static Mesh Matter(int emberCount, int hotSpotCount, int clumpCount, int matterSeed)
    {
        int count = emberCount + hotSpotCount + clumpCount;
        var vertices = new Vector3[count * 4];
        var corners = new List<Vector2>(count * 4);
        var seeds = new List<Vector4>(count * 4);
        var triangles = new int[count * 6];
        var random = new System.Random(1234 + matterSeed);

        for (int i = 0; i < count; i++)
        {
            float kind = i < emberCount ? 0f : i < emberCount + hotSpotCount ? 1f : 2f;
            var s = new Vector4((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble(), kind);

            corners.Add(new Vector2(-1f, -1f)); corners.Add(new Vector2(1f, -1f));
            corners.Add(new Vector2(1f, 1f)); corners.Add(new Vector2(-1f, 1f));
            for (int c = 0; c < 4; c++) seeds.Add(s);

            int v = i * 4, t = i * 6;
            triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
            triangles[t + 3] = v; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
        }

        var mesh = new Mesh { name = "Quasar matter" };
        if (vertices.Length > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.SetUVs(0, corners);
        mesh.SetUVs(1, seeds);
        mesh.triangles = triangles;
        return mesh;
    }
}

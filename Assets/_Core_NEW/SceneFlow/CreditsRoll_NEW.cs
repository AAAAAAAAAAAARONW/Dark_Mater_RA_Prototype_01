using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The credits, after the journey: one world for each part of the team, filmed like the end
/// of a film. The journey's own planets, Mercury out to Neptune, each on its orbit round the
/// Sun; the camera flies out from one to the next and frames each differently - beside it,
/// skimming its horizon as the Sun comes up over the rim, close on its crescent, or far off -
/// with who made that part of the piece set beside it. Then it leaves the Solar System for the
/// Milky Way, the title and the supporters come up, and the piece fades back to the cover.
///
/// EDITING. Everything said is in the Inspector: a section is a role, its names (one per line)
/// and its planet, and a shot (Auto picks one from the order). Add, remove or reorder sections
/// freely; a section with no planet goes by over the stars alone. A music clip, if given, fades
/// in with the picture and out with it.
///
/// THE FILM. Letterbox bars close to 2.39:1 as it starts and open for the title. The camera is
/// never still: within a shot it pushes in and orbits, so the light slides across the planet;
/// between planets it flies a curve, banking into the turn. Where the Sun is in frame and not
/// behind a planet, a glow and a long horizontal streak mark it, flaring as it clears a rim and
/// kept small near one, so the light never lies over the planet. The stars behind turn with
/// the camera. A role tracks in, its rule wipes across, its names come up one by one.
///
/// NAMES IN COLUMNS. A section's names are a table: titles (Prof, Dr) under titles, given
/// names under given names, family names under family names, notes under notes, each column
/// as wide as its widest entry (SplitNames says how a line is split, and how to say otherwise).
/// Beside a planet every column is set from its left; over or under one, the names are set
/// about the frame's centre line - given names up to it, family names away from it.
///
/// TYPE. The art's own: role titles and headings in Squada One (the drawn titles' face) in the
/// art's yellow over a green rule; names in Montserrat SemiBold (the callouts' face). Both are
/// TTFs made into TMP font assets at start. A note in brackets after a name - "(Annenberg
/// Fellow)" - is set small, in capitals, in green.
///
/// THE FINALE, "POWERS OF TEN". Leaving the Solar System: the journey's dive into it played
/// backwards, with the same lights. The camera pulls back from the last planet and rises over
/// the system - the planets on their orbits round the Sun, each orbit unrolling from its planet
/// - then on out, faster: the orbits fade, the planets are lost in the Sun, the Sun tightens
/// into one star that flares with diffraction spikes, the stars round it stream in along the
/// galaxy's disc, and the Milky Way comes in round it star by star, turning as it shrinks,
/// until it fills the left of the frame, on a slant and running out of it, the bars opening
/// and the title, then the supporters, coming up in a column on the right - the galaxy beside
/// the end card as each planet was beside its names. No cut, no flash. With no galaxy assigned
/// the credits end on a wide shot of the planets, as before.
///
/// The planets are copies of the journey's (CelestialBody_NEW, PlanetSphere_NEW). Their size
/// and place are set here at start, so moving them in the scene changes nothing. A skips.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("CREDITS", "#B36AE2")]
public class CreditsRoll_NEW : MonoBehaviour
{
    /// <summary>How a section is filmed. Auto takes the next from a sequence chosen to vary.</summary>
    public enum Shot { Auto, Left, Right, CloseUp, Horizon, Distant }

    [Serializable]
    public class Section
    {
        [Tooltip("Shown above the names, e.g. PRODUCED BY.")]
        public string role = "ROLE";

        [Tooltip("One name per line, set in columns: title (Prof, Dr), given names, family name (the last word). " +
                 "A note in round brackets after a name is set small and green: Aaron Wang (Annenberg Fellow). " +
                 "A second name someone also goes by, in square brackets, is set small after the family name: " +
                 "Xingru Yi [Winnie]. To split a name yourself, use bars: Dr | Ana | de la Cruz.")]
        [TextArea(2, 8)] public string names = "Name Surname";

        [Tooltip("The planet this section goes by with. Empty = over the stars alone.")]
        public Transform planet;

        [Tooltip("How it is filmed. Auto: from the order (Left, Right, Horizon, CloseUp, Distant...).\n" +
                 "Left / Right: the planet on that side, the names on the other.\n" +
                 "Horizon: skimming the planet's rim as the Sun comes up over it; names above.\n" +
                 "CloseUp: the planet huge and backlit to a crescent, half out of frame; names low on the right.\n" +
                 "Distant: the planet small and far; names below.")]
        public Shot shot = Shot.Auto;
    }

    [Header("People")]
    [SerializeField] List<Section> sections = new List<Section>();

    [Header("End card")]
    [Tooltip("The title art. Empty = the words below.")]
    [SerializeField] Sprite titleSprite;
    [SerializeField] string titleWords = "THE JOURNEY OF LIGHT";
    [SerializeField] string supportHeading = "MADE POSSIBLE WITH THE SUPPORT OF";
    [Tooltip("One per line.")]
    [TextArea(3, 10)] [SerializeField] string supporters = "";
    [TextArea(1, 3)] [SerializeField] string closingLine = "";

    [Header("Look")]
    [SerializeField] Camera view;

    [Tooltip("The planets' Sun (CelestialBody_NEW lights from it). Moved with each shot.")]
    [SerializeField] Transform sun;

    [Tooltip("Role titles and headings: Squada One, the drawn titles' face.")]
    [SerializeField] Font headingFont;

    [Tooltip("Names and supporters: Montserrat SemiBold, the callouts' face.")]
    [SerializeField] Font bodyFont;

    [Tooltip("Used where a font above is empty or cannot be made into a TMP font.")]
    [SerializeField] TMP_FontAsset font;

    [Tooltip("The art's yellow: role titles and headings.")]
    [SerializeField] Color headingColour = new Color(0.933f, 0.941f, 0f, 1f);

    [Tooltip("The art's green: the rules, and notes in brackets after a name.")]
    [SerializeField] Color accentColour = new Color(0f, 0.949f, 0.549f, 1f);

    [SerializeField] Color nameColour = new Color(0.94f, 0.95f, 0.965f, 1f);

    [Tooltip("Set a note in brackets after a name - (Annenberg Fellow) - small, in capitals, in the green.")]
    [SerializeField] bool styleNotes = true;

    [Tooltip("Every planet's radius, world units.")]
    [Min(0.1f)] [SerializeField] float planetRadius = 3f;

    [Tooltip("Distance between neighbouring planets along the flight, world units.")]
    [Min(1f)] [SerializeField] float spacing = 40f;

    [Tooltip("Camera distance from a planet in a Left / Right shot, world units. Other shots scale it.")]
    [Min(1f)] [SerializeField] float viewDistance = 13f;

    [Tooltip("The planets on their orbits round the Sun, a little above and below its plane, so the flight curves and has depth. Off = a straight row.")]
    [SerializeField] bool scatter = true;

    [Tooltip("Degrees per second each planet turns.")]
    [SerializeField] float spin = 5f;

    [Header("Film")]
    [Tooltip("Black bars top and bottom, to 2.39:1, closing as it starts and opening for the title.")]
    [SerializeField] bool letterbox = true;
    [Range(0f, 0.2f)] [SerializeField] float letterboxHeight = 0.12f;

    [Tooltip("A glow and a horizontal streak where the Sun sits at a planet's edge.")]
    [SerializeField] bool sunGlare = true;
    [Range(0f, 1.5f)] [SerializeField] float glareStrength = 1f;

    [Tooltip("Degrees the camera banks into the turn between planets.")]
    [Range(0f, 20f)] [SerializeField] float bankDegrees = 7f;

    [Header("Finale")]
    [Tooltip("The Milky Way (GalaxyVFX_NEW) the credits end on; leave its object switched off in the scene, it is switched on at Start. Empty = end on the planets.")]
    [SerializeField] Transform galaxy;

    [Tooltip("The galaxy's size while it waits, world units (GalaxyVFX_NEW's radius is its scale). The finale sizes it itself.")]
    [Min(10f)] [SerializeField] float galaxyRadius = 200f;

    [Tooltip("Where the galaxy waits, built and unseen, until the finale moves it round the Sun.")]
    [SerializeField] Vector3 galaxyPlace = new Vector3(140f, 0f, 6000f);

    [Tooltip("The Sun's place in the galaxy, in its own units: GalaxyVFX_NEW puts it at (sunRadius, 0, 0).")]
    [SerializeField] Vector3 sunInGalaxy = new Vector3(0.5f, 0f, 0f);

    [Tooltip("The galaxy's radius, in sizes of the Solar System (its largest orbit): how far the zoom out goes.")]
    [Min(20f)] [SerializeField] float galaxyToSystem = 2000f;

    [Tooltip("Brightness of the planets' orbits as the camera leaves the system. 0 = none.")]
    [Range(0f, 1f)] [SerializeField] float orbitBrightness = 0.35f;

    [Tooltip("Stars round the Sun that stream in as the galaxy closes round it.")]
    [Range(0, 3000)] [SerializeField] int swarmCount = 900;

    [Tooltip("The zoom out, from the last planet to the whole galaxy, seconds.")]
    [Min(4f)] [SerializeField] float zoomOutSeconds = 22f;

    [Header("Music")]
    [Tooltip("Optional. Fades in with the picture and out with it.")]
    [SerializeField] AudioClip music;
    [Range(0f, 1f)] [SerializeField] float musicVolume = 0.8f;

    [Header("Timing (seconds)")]
    [Min(0f)] [SerializeField] float introSeconds = 3f;
    [Min(0.1f)] [SerializeField] float textFadeSeconds = 1.2f;
    [Tooltip("A section's hold; each name past three adds a little.")]
    [Min(0f)] [SerializeField] float holdSeconds = 6f;
    [Min(0.1f)] [SerializeField] float moveSeconds = 4f;
    [Min(0f)] [SerializeField] float endHoldSeconds = 10f;
    [Min(0.1f)] [SerializeField] float fadeOutSeconds = 3f;
    [Tooltip("A skips to the end after this long.")]
    [Min(0f)] [SerializeField] float skipAfterSeconds = 3f;

    [Header("Next")]
    [Tooltip("Where the piece goes after the credits, as an asset path: the cover.")]
    [SerializeField] string returnScenePath = "Assets/_Core_NEW/PC_Cover.unity";

    // ── Shots ───────────────────────────────────────────────────────────────

    enum Layout { Right, Left, Top, Bottom, LowerRight }

    /// <summary>
    /// A shot, in the planet's terms. sx, sy: where the planet's centre sits on screen (-1..1 is
    /// the frame's edge; past it is out of frame). distance: times viewDistance. The camera orbits
    /// from yawFrom to yawTo degrees, raised by pitch, rolled by roll. light: where the Sun is,
    /// relative to the camera looking at the planet - x right, y up, z beyond the planet.
    /// </summary>
    struct ShotDef
    {
        public float sx, sy, distance, yawFrom, yawTo, pitch, roll, pushFrom, pushTo;
        public Vector3 light;
        public Layout layout;
    }

    static readonly Shot[] AutoOrder = { Shot.Left, Shot.Right, Shot.Horizon, Shot.CloseUp, Shot.Distant, Shot.Right, Shot.Left, Shot.CloseUp };

    static ShotDef Def(Shot s)
    {
        switch (s)
        {
            case Shot.Right:
                return new ShotDef { sx = 0.42f, sy = 0.03f, distance = 1f, yawFrom = 9f, yawTo = -5f, pitch = 5f, roll = 0f,
                                     pushFrom = 1.04f, pushTo = 0.95f, light = new Vector3(-0.85f, 0.35f, -0.4f), layout = Layout.Left };
            case Shot.CloseUp:
                return new ShotDef { sx = -0.62f, sy = -0.1f, distance = 0.5f, yawFrom = -5f, yawTo = 4f, pitch = 6f, roll = -4f,
                                     pushFrom = 1.03f, pushTo = 0.96f, light = new Vector3(0.45f, 0.2f, 0.87f), layout = Layout.LowerRight };
            case Shot.Horizon:
                return new ShotDef { sx = 0f, sy = -3.3f, distance = 0.36f, yawFrom = -7f, yawTo = 7f, pitch = 10f, roll = 2.5f,
                                     pushFrom = 1.02f, pushTo = 0.98f, light = new Vector3(0.15f, 0.62f, 0.77f), layout = Layout.Top };
            case Shot.Distant:
                return new ShotDef { sx = 0f, sy = 0.16f, distance = 2.3f, yawFrom = -10f, yawTo = 10f, pitch = 8f, roll = 0f,
                                     pushFrom = 1.12f, pushTo = 0.9f, light = new Vector3(0.7f, 0.45f, -0.55f), layout = Layout.Bottom };
            default:
                return new ShotDef { sx = -0.42f, sy = 0.03f, distance = 1f, yawFrom = -9f, yawTo = 5f, pitch = 5f, roll = 0f,
                                     pushFrom = 1.04f, pushTo = 0.95f, light = new Vector3(0.85f, 0.35f, -0.4f), layout = Layout.Right };
        }
    }

    Shot ShotOf(int i)
    {
        Shot s = i >= 0 && i < sections.Count ? sections[i].shot : Shot.Auto;
        return s != Shot.Auto ? s : AutoOrder[i % AutoOrder.Length];
    }

    struct Pose { public Vector3 position, light, focus; public Quaternion rotation; }

    // ── State ───────────────────────────────────────────────────────────────

    TMP_FontAsset _headingFont, _bodyFont;
    CanvasGroup _sectionGroup, _endGroup, _endBody;
    RectTransform _sectionBlock, _barTop, _barBottom, _glareCore, _glareStreak, _skyRect;
    Image _black;
    RawImage _sky, _glareCoreImage, _glareStreakImage;
    AudioSource _music;

    // The section on screen.
    TextMeshProUGUI _role;
    RectTransform _rule;
    readonly List<TextMeshProUGUI> _lines = new List<TextMeshProUGUI>();
    readonly List<float> _lineX = new List<float>();
    readonly List<float> _lineY = new List<float>();
    float _ruleWidth = 150f;

    float _clock, _bars, _glareWeight, _skyZoom = 1f;
    Vector2 _skySize;
    RawImage _textShade;
    Renderer[] _galaxyRenderers;
    MaterialPropertyBlock _galaxyBlock;
    bool _sunHeld;          // the finale has the Sun: at _sunAt, a star, fading to _sunFade
    Vector3 _sunAt;
    float _sunFade = 1f;
    float _sunScale = 1f;     // the glare's size: smaller once the finale looks down on the whole system
    float _sunRange = 5000f;  // how far off the Sun is, along _light, until the finale holds it
    readonly List<MeshFilter> _occluders = new List<MeshFilter>();
    bool _skip;
    Vector3 _focus, _light = new Vector3(0.6f, 0.4f, -0.6f).normalized;
    Vector3 _lastForward;
    bool _haveLast;

    // ── Start ───────────────────────────────────────────────────────────────

    void Start()
    {
        if (view == null) view = Camera.main;
        if (view == null)
        {
            Debug.LogError("[CreditsRoll_NEW] No camera.", this);
            enabled = false;
            return;
        }

        // Each part on its own: a planet or the sky that fails to build must not stop the roll.
        Try(LayOutPlanets, "laying out the planets");
        Try(PlaceGalaxy, "placing the galaxy");
        Try(BuildSky, "building the sky");
        _headingFont = MakeFont(headingFont);
        _bodyFont = MakeFont(bodyFont);
        if (!Try(BuildOverlay, "building the text"))
        {
            enabled = false;
            return;
        }
        Try(StartMusic, "starting the music");
        StartCoroutine(Run());
    }

    bool Try(Action step, string what)
    {
        try { step(); return true; }
        catch (Exception e)
        {
            Debug.LogError("[CreditsRoll_NEW] Failed " + what + ": " + e, this);
            return false;
        }
    }

    /// <summary>A TMP font made from a TTF at runtime, or the fallback TMP font.</summary>
    TMP_FontAsset MakeFont(Font source)
    {
        if (source == null) return font;
        try
        {
            TMP_FontAsset made = TMP_FontAsset.CreateFontAsset(source);
            if (made != null) return made;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[CreditsRoll_NEW] Could not make a TMP font from " + source.name + "; using the fallback. " + e.Message, this);
        }
        return font;
    }

    void StartMusic()
    {
        if (music == null) return;
        _music = gameObject.AddComponent<AudioSource>();
        _music.clip = music;
        _music.loop = false;
        _music.playOnAwake = false;
        _music.spatialBlend = 0f;
        _music.volume = 0f;
        _music.Play();
    }

    /// <summary>
    /// This frame's seconds, never more than a tenth. The first frames carry the scene load and
    /// the sky's build; taken whole, they would skip the fade in and the first section.
    /// </summary>
    static float Step() { return Mathf.Min(Time.unscaledDeltaTime, 0.1f); }

    static float Smooth(float k) { k = Mathf.Clamp01(k); return k * k * k * (k * (k * 6f - 15f) + 10f); }

    // ── Every frame ─────────────────────────────────────────────────────────

    void Update()
    {
        float dt = Step();
        _clock += dt;

        foreach (Section s in sections)
            if (s.planet != null) s.planet.Rotate(Vector3.up, spin * dt, Space.Self);
        if (sun != null && !_sunHeld) sun.position = _focus + _light * _sunRange;

        TurnSky();
        UpdateBars();
        UpdateGlare();

        if (!_skip && _clock > skipAfterSeconds && InputScheme_NEW.ConfirmDown()) _skip = true;
    }

    /// <summary>The stars move as the camera turns and rolls, as stars at infinity would.</summary>
    void TurnSky()
    {
        if (_sky == null) return;
        Vector3 forward = view.transform.forward;
        if (_haveLast)
        {
            float vFov = view.fieldOfView;
            float hFov = 2f * Mathf.Atan(Mathf.Tan(vFov * 0.5f * Mathf.Deg2Rad) * view.aspect) * Mathf.Rad2Deg;
            float yaw = Vector3.SignedAngle(Vector3.ProjectOnPlane(_lastForward, Vector3.up), Vector3.ProjectOnPlane(forward, Vector3.up), Vector3.up);
            float pitch = (Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) - Mathf.Asin(Mathf.Clamp(_lastForward.y, -1f, 1f))) * Mathf.Rad2Deg;
            Rect uv = _sky.uvRect;
            uv.x += yaw / Mathf.Max(1f, hFov) * uv.width + 0.0015f * Step();
            uv.y += pitch / Mathf.Max(1f, vFov) * uv.height;
            // Zoomed out about the middle: the stars stream inward as the camera backs away.
            Vector2 centre = uv.center;
            uv.size = _skySize * _skyZoom;
            uv.center = centre;
            _sky.uvRect = uv;
        }
        _lastForward = forward;
        _haveLast = true;

        float roll = view.transform.eulerAngles.z;
        if (roll > 180f) roll -= 360f;
        if (_skyRect != null) _skyRect.localEulerAngles = new Vector3(0f, 0f, -roll);
    }

    void UpdateBars()
    {
        if (_barTop == null) return;
        float h = letterbox ? letterboxHeight * Smooth(_bars) : 0f;
        _barTop.anchorMin = new Vector2(0f, 1f - h);
        _barBottom.anchorMax = new Vector2(1f, h);
    }

    /// <summary>
    /// The Sun's glare: a glow and a long streak where the Sun is, and only where it can be seen.
    /// Any planet in front of it hides it, softly as it slips behind the rim; it flares brightest
    /// just clear of a rim, and the closer it is to one the smaller the glow and the fainter the
    /// streak, so the light never lies over the planet. In the finale it is the star the system
    /// falls into, going out as its galaxy comes in.
    /// </summary>
    void UpdateGlare()
    {
        if (_glareCore == null) return;
        float a = 0f;
        Vector3 cam = view.transform.position;
        Vector3 sunAt = _sunHeld ? _sunAt : _focus + _light * _sunRange;
        Vector3 toSun = sunAt - cam;
        float sunDist = toSun.magnitude;

        if (sunGlare && sunDist > 1e-3f)
        {
            Vector3 ds = toSun / sunDist;

            // The angle between the Sun and the nearest planet's edge in front of it (radians).
            float gap = Mathf.PI;
            foreach (MeshFilter mf in _occluders)
            {
                if (mf == null || !mf.gameObject.activeInHierarchy || mf.sharedMesh == null) continue;
                Vector3 toPlanet = mf.transform.position - cam;
                float d = toPlanet.magnitude;
                if (d < 1e-4f || d > sunDist) continue;
                float r = mf.sharedMesh.bounds.extents.x * mf.transform.lossyScale.x;
                float disc = Mathf.Asin(Mathf.Clamp01(r / d));
                float theta = Mathf.Acos(Mathf.Clamp(Vector3.Dot(toPlanet / d, ds), -1f, 1f));
                gap = Mathf.Min(gap, theta - disc);
            }

            float vis = Mathf.SmoothStep(0f, 1f, gap / 0.006f);                 // out behind the rim
            float rim = Mathf.Exp(-Mathf.Max(0f, gap) / 0.035f);                 // the flare just clear of it
            float strength = vis * (0.55f + 0.45f * rim);

            Vector3 vp = view.WorldToViewportPoint(sunAt);
            bool onScreen = vp.z > 0f && vp.x > -0.25f && vp.x < 1.25f && vp.y > -0.25f && vp.y < 1.25f;
            if (onScreen && strength > 0.001f)
            {
                a = strength * glareStrength * _glareWeight * _sunFade;
                Vector2 at = new Vector2(vp.x, vp.y);
                _glareCore.anchorMin = _glareCore.anchorMax = at;
                _glareStreak.anchorMin = _glareStreak.anchorMax = at;

                // The gap on screen, in the canvas's 1080 lines: the glow stays inside it.
                float gapLines = Mathf.Max(0f, gap) / (view.fieldOfView * Mathf.Deg2Rad) * 1080f;
                // The glow's radius is 1.4 times the gap: under 7% of it reaches the planet's edge.
                float size = Mathf.Clamp(gapLines / 200f, 0.15f, 1f) * Mathf.Lerp(0.25f, 1f, _sunFade) * _sunScale;
                _glareCore.localScale = new Vector3(size, size, 1f);
                _glareStreak.localScale = new Vector3((0.6f + 0.6f * strength) * Mathf.Lerp(0.5f, 1f, _sunScale), 1f, 1f);
                float streak = Smooth(gapLines / 220f) * _sunFade;
                _glareStreakImage.color = new Color(0.75f, 0.88f, 1f, Mathf.Clamp01(a * 0.65f * streak));
            }
        }

        _glareCoreImage.color = new Color(1f, 0.94f, 0.82f, Mathf.Clamp01(a));
        if (a <= 0f) _glareStreakImage.color = new Color(0.75f, 0.88f, 1f, 0f);
    }

    // ── The run ─────────────────────────────────────────────────────────────

    IEnumerator Run()
    {
        // Out of black onto the first world, the bars closing in.
        Apply(PoseFor(0, 0f));
        _black.color = Color.black;
        float t = 0f;
        while (t < introSeconds && !_skip)
        {
            t += Step();
            float k = t / Mathf.Max(0.01f, introSeconds);
            _black.color = new Color(0f, 0f, 0f, 1f - Smooth(k));
            _bars = k;
            _glareWeight = Smooth(k);
            SetMusic(Smooth(k));
            Apply(PoseFor(0, 0f));
            yield return null;
        }
        _black.color = Color.clear;
        _bars = 1f;
        _glareWeight = 1f;
        SetMusic(1f);

        for (int i = 0; i < sections.Count && !_skip; i++)
        {
            ShowSection(i);
            float total = textFadeSeconds * 2f + HoldFor(i);
            t = 0f;
            while (t < total && !_skip)
            {
                t += Step();
                Apply(PoseFor(i, t / total));
                AnimateSection(t, total);
                yield return null;
            }
            _sectionGroup.alpha = 0f;

            if (i + 1 < sections.Count && !_skip)
                yield return Fly(PoseFor(i, 1f), PoseFor(i + 1, 0f), moveSeconds, true);
        }

        if (!_skip && galaxy != null)
        {
            yield return Finale();
        }
        else if (!_skip)
        {
            // Up and back to see them all, the bars opening, the title coming up.
            Pose wide = WidePose();
            Pose from = CurrentPose();
            float seconds = moveSeconds * 1.8f;
            t = 0f;
            while (t < seconds && !_skip)
            {
                t += Step();
                float k = t / seconds;
                Apply(Between(from, wide, Smooth(k), false, 0f));
                _bars = 1f - Smooth((k - 0.4f) / 0.6f);
                _glareWeight = 1f - Smooth(k * 2f);
                _endGroup.alpha = Smooth((k - 0.5f) / 0.5f);
                yield return null;
            }
            _endGroup.alpha = 1f;

            t = 0f;
            while (t < endHoldSeconds && !_skip)
            {
                t += Step();
                Pose drift = wide;
                drift.position = Vector3.Lerp(wide.position, wide.focus, 0.05f * t / Mathf.Max(0.01f, endHoldSeconds));
                Apply(drift);
                yield return null;
            }
        }

        // To black, the music with it.
        float from01 = _black.color.a;
        t = 0f;
        while (t < fadeOutSeconds)
        {
            t += Step();
            float k = Smooth(t / fadeOutSeconds);
            _black.color = new Color(0f, 0f, 0f, Mathf.Lerp(from01, 1f, k));
            SetMusic(1f - k);
            yield return null;
        }
        enabled = false;
        SceneFlow_NEW.Load(returnScenePath, this);
    }

    // ── The finale ──────────────────────────────────────────────────────────

    void PlaceGalaxy()
    {
        if (galaxy == null) return;
        galaxy.SetParent(null, true);
        galaxy.SetPositionAndRotation(galaxyPlace, Quaternion.identity);
        galaxy.localScale = Vector3.one * galaxyRadius;
        // Switched off in the scene, so opening PC_Credits in the editor shows the planets and
        // not the ending (the galaxy draws in edit mode too). On here, at Start, well before the
        // finale, so it is built and waiting; it stays invisible until the zoom fades it in.
        galaxy.gameObject.SetActive(true);
        _galaxyRenderers = galaxy.GetComponentsInChildren<Renderer>(true);
        _galaxyBlock = new MaterialPropertyBlock();
        SetGalaxyFade(0f);
    }

    /// <summary>The galaxy's fade, as WorldSwitcher_NEW gives it: all four channels of _Color.</summary>
    void SetGalaxyFade(float a)
    {
        if (_galaxyRenderers == null) return;
        _galaxyBlock.SetColor("_Color", new Color(a, a, a, a));
        foreach (Renderer r in _galaxyRenderers)
            if (r != null) r.SetPropertyBlock(_galaxyBlock);
    }

    /// <summary>
    /// Leaving the Solar System: the journey's dive into it, played backwards, in one move on
    /// one clock. What the dive does going in - the Milky Way growing round one of its stars,
    /// its stars streaming out of the frame, the star flaring with spikes and opening into the
    /// Sun, the planets' orbits unrolling round it - happens here in reverse, with the same
    /// shaders (DiveGlow_NEW, DiveOrbit_NEW, DiveSwarm_NEW) and the same colours:
    ///
    ///   leaving   The camera pulls back from the last planet and rises, turning to look down
    ///             on the Sun: the planets are where they have been all along, on their orbits
    ///             round it, and each orbit unrolls from its planet, innermost first. The Sun's
    ///             light comes round onto the planets from where it really is.
    ///   the star  Out over the whole system the pull-back goes on, faster: the orbits fade,
    ///             the planets close in on the Sun and are lost in it, and the Sun's glare
    ///             tightens into a star, which flares with diffraction spikes as the system
    ///             becomes one point.
    ///   the swarm The stars round the Sun stream in from beyond the frame along the galaxy's
    ///             disc, streaking towards it - the dive's crowd, closing instead of opening.
    ///   galaxy    The Milky Way comes in out of the dark round the star, star by star,
    ///             shrinking at the same rate and turning back the quarter turn the dive turns
    ///             it going in; the spikes draw back, the star becomes one of its stars and
    ///             goes, the swarm scatters into the galaxy's own, and the camera comes round
    ///             to its closing frame: the galaxy large on the left, on a slant, running
    ///             out of the frame, and the title and supporters in a column on the right.
    ///
    /// Nothing is cut or covered. Distances run from tens of units to millions, so the camera's
    /// path is worked out at true scale and the world drawn scaled down round the Sun, the
    /// camera always the same distance from it: the numbers on screen stay small.
    /// </summary>
    IEnumerator Finale()
    {
        Pose last = CurrentPose();
        Vector3 planet = last.focus;
        Vector3 sunAt = SunCentre();
        float outer = OuterOrbit(sunAt);

        // Where it starts: looking at the planet from where the last shot left the camera.
        Vector3 f0 = planet - last.position;
        float d0 = f0.magnitude;
        if (d0 < 1e-3f) { f0 = last.rotation * Vector3.forward; d0 = viewDistance; }
        f0 /= d0;

        // Where the system is seen whole: above it, on the side the camera is already on.
        Vector3 side = last.position - sunAt;
        side.y = 0f;
        if (side.sqrMagnitude < 1e-4f) side = Vector3.back;
        side.Normalize();
        Vector3 overhead = (side * Mathf.Cos(SystemElevation * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(SystemElevation * Mathf.Deg2Rad)).normalized;
        float dWhole = outer * 3.2f;

        // The galaxy, at true scale: galaxyToSystem times the system, the camera at its end
        // where GalaxyCameraLocal puts it.
        Vector3 local = GalaxyCameraLocal() - sunInGalaxy;
        float trueRadius = outer * galaxyToSystem;
        float dEnd = trueRadius * local.magnitude;
        Quaternion turn = GalaxyTurn(local, overhead);

        // Drawn scaled down round the Sun so that the camera is always this far from it.
        float held = Mathf.Max(1f, Vector3.Distance(last.position, sunAt));
        float radiusEnd = held / local.magnitude;
        Vector3 centreEnd = sunAt - turn * (sunInGalaxy * radiusEnd);
        Vector3 eyeEnd = sunAt + overhead * held;
        float tanV = Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
        // The closing frame: the galaxy large on the left, its disc on a slant and running out
        // of the frame's left and bottom edges, the right of the frame clear for the end card.
        Quaternion finalLook = Quaternion.LookRotation(centreEnd - eyeEnd, turn * Vector3.up) *
                               Quaternion.Euler(0f, 0f, ClosingRoll) *
                               Quaternion.Euler(Mathf.Atan(ClosingAt.y * tanV) * Mathf.Rad2Deg,
                                                -Mathf.Atan(ClosingAt.x * tanV * view.aspect) * Mathf.Rad2Deg, 0f);

        Transform world = WorldRoot();
        Vector3 worldPosition = world != null ? world.position : Vector3.zero;
        Vector3 worldScale = world != null ? world.localScale : Vector3.one;
        Vector3 shotLight = last.light;

        BuildOrbits(sunAt);
        BuildStar();
        BuildSwarm(outer / trueRadius);
        _sunHeld = true;

        float lnStart = Mathf.Log(d0), lnWhole = Mathf.Log(dWhole), lnEnd = Mathf.Log(dEnd);
        float pixel = 2f * tanV * held / Mathf.Max(1, Screen.height);
        float t = 0f;
        while (t < zoomOutSeconds && !_skip)
        {
            t += Step();
            float k = Mathf.Clamp01(t / zoomOutSeconds);

            // ── The camera's path, at true scale ──
            // How far back from what it looks at, in log space: each second takes it the same
            // number of times further, easing into and out of the moment the system is whole.
            float lnD = k < SystemSplit
                ? Mathf.Lerp(lnStart, lnWhole, Ease(k / SystemSplit))
                : Mathf.Lerp(lnWhole, lnEnd, Ease((k - SystemSplit) / (1f - SystemSplit)));
            float distance = Mathf.Exp(lnD);
            // What it looks at moves from the planet to the Sun only as fast as the camera
            // backs off, so there is always something in frame: the planet, then the planets
            // and orbits between, then the Sun with them all round it.
            float separation = Mathf.Max(1f, Vector3.Distance(planet, sunAt));
            float lnNear = Mathf.Log(0.15f * separation);
            Vector3 target = Vector3.Lerp(planet, sunAt, lnWhole > lnNear ? Smooth((lnD - lnNear) / (lnWhole - lnNear)) : Smooth(k / SystemSplit));
            Vector3 forward = Vector3.Slerp(f0, -overhead, Smooth((k - 0.01f) / SystemSplit)).normalized;
            Vector3 fromSun = (target - sunAt) - forward * distance;

            // ── Drawn scaled round the Sun ──
            float scale = held / Mathf.Max(1e-3f, fromSun.magnitude);
            Vector3 eye = sunAt + fromSun * scale;
            float since = lnD - lnWhole;    // e-folds out past the whole system (negative before)
            float left = lnEnd - lnD;       // e-folds still to go

            if (world != null)
            {
                world.localScale = worldScale * scale;
                world.position = sunAt + (worldPosition - sunAt) * scale;
                // A planet under a pixel only flickers: by then it is in the Sun's light.
                float px = planetRadius * scale / Mathf.Max(1e-3f, held) / tanV * Screen.height * 0.5f;
                if (px < 0.6f && world.gameObject.activeSelf) world.gameObject.SetActive(false);
            }

            // The Sun's light comes round from where the last shot had it to where it is.
            Vector3 planetNow = sunAt + (planet - sunAt) * scale;
            Vector3 toSun = sunAt - planetNow;
            float come = Smooth(k / (SystemSplit * 0.8f));
            Vector3 lightDir = toSun.sqrMagnitude > 1e-8f ? Vector3.Slerp(shotLight, toSun.normalized, come).normalized : shotLight;
            _sunAt = come >= 0.999f ? sunAt : planetNow + lightDir * toSun.magnitude;
            if (sun != null) sun.position = _sunAt;
            _focus = planetNow;
            _light = lightDir;

            // The Sun's glare tightens into a star as the system closes in on it: smaller as the
            // camera rises, so the inner orbits show round it, then gone into the star.
            _sunScale = Mathf.Lerp(1f, 0.35f, Smooth((k - 0.08f) / (SystemSplit - 0.08f)));
            _sunFade = 1f - Smooth((since - 1.2f) / 1.6f);
            float star = 0.7f * Smooth((since - 0.8f) / 1.6f) * (1f - 0.55f * Smooth((4f - left) / 2f)) * (1f - Smooth((1.4f - left) / 1.1f));
            float flare = Smooth((since - 1.6f) / 1.2f) * (1f - Smooth((since - 3.6f) / 1.6f));
            PlaceStar(eye, sunAt, star, 0.024f * (1f + 0.6f * flare), 0.6f * flare + 0.12f, 0.45f + 0.5f * flare, t * 0.05f);

            // The orbits unroll as the camera rises, and go as the system shrinks into its Sun.
            TickOrbits(t - zoomOutSeconds * 0.1f, sunAt, scale, orbitBrightness * (1f - Smooth((since - 0.6f) / 1.4f)), held * tanV);

            // ── The galaxy and the Sun's neighbours, round the same point ──
            float galaxyNow = trueRadius * scale;
            float spinLeft = GalaxySpin * Smooth((left - 0.2f) / 4.5f);
            Quaternion turned = Quaternion.AngleAxis(spinLeft, turn * Vector3.up) * turn;
            float fade = 1f - Smooth((left - 0.2f) / 3.8f);
            if (galaxy != null)
            {
                if (fade > 0.001f)
                {
                    galaxy.rotation = turned;
                    galaxy.localScale = Vector3.one * galaxyNow;
                    galaxy.position = sunAt - turned * (sunInGalaxy * galaxyNow);
                }
                SetGalaxyFade(fade);
            }
            TickSwarm(sunAt, turned, galaxyNow, 0.7f * Smooth((since - 1f) / 1.5f), Smooth((2.2f - left) / 2f), pixel);

            // ── The camera: the planet, then down on the Sun, then round to the galaxy ──
            Quaternion look = Quaternion.LookRotation(forward, Vector3.up);
            Quaternion rotation = Quaternion.Slerp(last.rotation, look, Smooth(k / 0.12f));
            rotation = Quaternion.Slerp(rotation, finalLook, Smooth((k - 0.68f) / 0.28f));
            view.transform.SetPositionAndRotation(eye, rotation);

            _skyZoom = Mathf.Lerp(1f, 2.4f, Smooth((k - SystemSplit) / (1f - SystemSplit)));
            _bars = 1f - Smooth((k - 0.8f) / 0.18f);
            _textShade.color = new Color(0f, 0f, 0f, 0.5f * Smooth((k - 0.82f) / 0.18f));
            // The title as the galaxy settles into its place; the rest once it has.
            _endBody.alpha = 0f;
            _endGroup.alpha = Smooth((k - 0.88f) / 0.12f);
            yield return null;
        }
        _endGroup.alpha = 1f;
        HideFinaleLights();
        if (_skip || galaxy == null) { _endBody.alpha = 1f; yield break; }

        // Held beside the turning galaxy, drifting in a little; the supporters come up.
        Vector3 from = view.transform.position;
        t = 0f;
        while (t < endHoldSeconds && !_skip)
        {
            t += Step();
            _endBody.alpha = Smooth((t - 0.5f) / 1.6f);
            view.transform.position = Vector3.Lerp(from, centreEnd, 0.06f * t / Mathf.Max(0.01f, endHoldSeconds));
            galaxy.Rotate(Vector3.up, -1.2f * Step(), Space.Self);
            yield return null;
        }
    }

    // The finale's shape: the camera looks down on the whole system from this many degrees
    // up, at this point of the clock; the galaxy turns back this far as it comes in.
    const float SystemElevation = 48f;
    const float SystemSplit = 0.32f;
    const float GalaxySpin = 90f;

    /// <summary>Smooth, but never quite still: the move eases through a moment, it does not stop.</summary>
    static float Ease(float k) { return 0.85f * Smooth(k) + 0.15f * Mathf.Clamp01(k); }

    // The closing frame: how far the camera is from the galaxy's centre, in its radii, and how
    // many degrees above its disc; where the centre sits on screen (-1..1 is the frame's edge);
    // and the camera's roll, which sets the disc on a slant.
    const float ClosingDistance = 2.5f, ClosingElevation = 34f, ClosingRoll = 22f;
    static readonly Vector2 ClosingAt = new Vector2(-0.56f, -0.08f);

    /// <summary>The camera's place at the end, from the galaxy's centre, in its radii.</summary>
    static Vector3 GalaxyCameraLocal()
    {
        return Quaternion.Euler(0f, -24f, 0f) *
               new Vector3(0f, Mathf.Sin(ClosingElevation * Mathf.Deg2Rad), -Mathf.Cos(ClosingElevation * Mathf.Deg2Rad)) * ClosingDistance;
    }

    /// <summary>
    /// The galaxy's turn that puts the camera, <paramref name="towardCamera"/> from the Sun, at
    /// <paramref name="local"/> (Sun to camera, in the galaxy's own frame) - its disc as level
    /// as that allows.
    /// </summary>
    static Quaternion GalaxyTurn(Vector3 local, Vector3 towardCamera)
    {
        Vector3 n = towardCamera.normalized;
        Quaternion turn = Quaternion.FromToRotation(local.normalized, n);
        Vector3 pole = Vector3.ProjectOnPlane(turn * Vector3.up, n), level = Vector3.ProjectOnPlane(Vector3.up, n);
        if (pole.sqrMagnitude > 1e-6f && level.sqrMagnitude > 1e-6f)
            turn = Quaternion.AngleAxis(Vector3.SignedAngle(pole, level, n), n) * turn;
        return turn;
    }

    /// <summary>What the finale shrinks: the object all the planets are under.</summary>
    Transform WorldRoot()
    {
        foreach (Section s in sections)
            if (s.planet != null) return s.planet.parent != null ? s.planet.root : s.planet;
        return null;
    }

    /// <summary>The Sun, at the middle of the planets' orbits.</summary>
    Vector3 SunCentre() { return new Vector3(-2f * spacing, 0f, 0f); }

    /// <summary>The largest orbit's radius, across the system's plane.</summary>
    float OuterOrbit(Vector3 sunAt)
    {
        float r = 2f * spacing;
        for (int i = 0; i < sections.Count; i++)
        {
            Vector3 d = Focus(i) - sunAt;
            d.y = 0f;
            r = Mathf.Max(r, d.magnitude);
        }
        return r;
    }

    // ── The finale's lights: the star, the orbits, the swarm ────────────────

    const float StarQuad = 8f;      // the star's quad is this many times its glow, for the spikes
    const int OrbitPoints = 128;
    const float OrbitDrawSeconds = 1.6f, OrbitStagger = 0.14f;
    static readonly Vector3[] OrbitBuffer = new Vector3[OrbitPoints + 1];

    GameObject _star, _swarm;
    Material _starMaterial, _swarmMaterial, _orbitMaterial;
    MaterialPropertyBlock _orbitBlock;
    readonly List<LineRenderer> _orbitLines = new List<LineRenderer>();
    readonly List<Vector3> _orbitOffsets = new List<Vector3>();

    int FinaleLayer() { return galaxy != null ? galaxy.gameObject.layer : gameObject.layer; }

    static MeshRenderer Plain(GameObject go, Mesh mesh, Material material)
    {
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return mr;
    }

    /// <summary>The star the system becomes: the dive's own light (DiveGlow_NEW), as a star.</summary>
    void BuildStar()
    {
        if (_star != null) return;
        Shader shader = Resources.Load<Shader>("DiveGlow_NEW");
        if (shader == null) { Debug.LogWarning("[CreditsRoll_NEW] Resources/DiveGlow_NEW.shader is missing; no star.", this); return; }
        _starMaterial = new Material(shader) { name = "Credits star (runtime)" };
        var quad = new Mesh { name = "Credits star quad" };
        quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
        quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
        quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        quad.RecalculateBounds();
        _star = new GameObject("Credits star") { layer = FinaleLayer() };
        Plain(_star, quad, _starMaterial);
        _starMaterial.SetFloat("_GlowScale", StarQuad);
        _starMaterial.SetColor("_SpikeTint", new Color(0.72f, 0.82f, 1f, 1f));
        _star.SetActive(false);
    }

    /// <param name="screenSize">The glow's diameter as a fraction of the screen's height, however far.</param>
    void PlaceStar(Vector3 eye, Vector3 at, float intensity, float screenSize, float spikes, float spikeLength, float spikeAngle)
    {
        if (_star == null) return;
        bool on = intensity > 0.001f;
        if (_star.activeSelf != on) _star.SetActive(on);
        if (!on) return;
        _starMaterial.SetColor("_Color", new Color(1f, 0.95f, 0.84f, intensity));
        _starMaterial.SetFloat("_Spikes", spikes);
        _starMaterial.SetFloat("_SpikeLength", spikeLength);
        _starMaterial.SetFloat("_SpikeAngle", spikeAngle);
        float size = screenSize * StarQuad * 2f * Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad) * Vector3.Distance(eye, at);
        _star.transform.SetPositionAndRotation(at, view.transform.rotation);
        _star.transform.localScale = new Vector3(size, size, size);
    }

    /// <summary>A line for each planet's orbit (DiveOrbit_NEW), innermost first, through the planet round the Sun.</summary>
    void BuildOrbits(Vector3 sunAt)
    {
        if (_orbitLines.Count > 0 || orbitBrightness <= 0f) return;
        Shader shader = Resources.Load<Shader>("DiveOrbit_NEW");
        if (shader == null) { Debug.LogWarning("[CreditsRoll_NEW] Resources/DiveOrbit_NEW.shader is missing; no orbits.", this); return; }
        _orbitMaterial = new Material(shader) { name = "Credits orbit (runtime)" };
        _orbitBlock = new MaterialPropertyBlock();

        var offsets = new List<Vector3>();
        foreach (Section s in sections)
            if (s.planet != null && (s.planet.position - sunAt).sqrMagnitude > 1e-4f) offsets.Add(s.planet.position - sunAt);
        offsets.Sort((a, b) => a.sqrMagnitude.CompareTo(b.sqrMagnitude));

        foreach (Vector3 offset in offsets)
        {
            var go = new GameObject("Credits orbit") { layer = FinaleLayer() };
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
            _orbitLines.Add(line);
            _orbitOffsets.Add(offset);
        }
    }

    /// <summary>
    /// Each orbit drawn from its planet round the way the planets go, over OrbitDrawSeconds,
    /// innermost first, a bright head where it is being drawn - as the dive draws them - the
    /// size the system is now drawn at, about a pixel and a half wide.
    /// </summary>
    void TickOrbits(float t, Vector3 sunAt, float scale, float shown, float halfHeightAtSun)
    {
        float width = 0.0016f * 2f * halfHeightAtSun;
        for (int i = 0; i < _orbitLines.Count; i++)
        {
            LineRenderer line = _orbitLines[i];
            if (line == null) continue;
            float draw = Smooth((t - i * OrbitStagger) / OrbitDrawSeconds);
            bool on = draw > 0f && shown > 0.001f;
            if (line.gameObject.activeSelf != on) line.gameObject.SetActive(on);
            if (!on) continue;

            // Through the planet: in the plane of its offset and the level tangent there, so a
            // planet a little above or below the Sun's plane still sits on its line.
            Vector3 r = _orbitOffsets[i] * scale;
            Vector3 flat = new Vector3(r.x, 0f, r.z);
            Vector3 tangent = flat.sqrMagnitude > 1e-8f ? Vector3.Cross(Vector3.up, flat).normalized : Vector3.forward;
            float radius = r.magnitude;
            for (int j = 0; j <= OrbitPoints; j++)
            {
                float a = -2f * Mathf.PI * j / OrbitPoints;
                OrbitBuffer[j] = sunAt + r * Mathf.Cos(a) + tangent * (radius * Mathf.Sin(a));
            }
            line.SetPositions(OrbitBuffer);
            line.widthMultiplier = width;
            _orbitBlock.SetColor("_Color", new Color(0.72f, 0.82f, 1f, shown));
            _orbitBlock.SetFloat("_Draw", draw);
            _orbitBlock.SetFloat("_Head", 1f - Smooth((draw - 0.85f) / 0.15f));
            line.SetPropertyBlock(_orbitBlock);
        }
    }

    /// <summary>
    /// The Sun's neighbours in the galaxy (DiveSwarm_NEW): stars in the disc round it at radii
    /// spread evenly in log space, from just outside the system to a quarter of the galaxy,
    /// so that as everything shrinks they keep streaming in from beyond the frame - in the
    /// dive's own white, blue and pink. Fixed seed: the same every run.
    /// </summary>
    /// <param name="inner">The system's size as a fraction of the galaxy's radius.</param>
    void BuildSwarm(float inner)
    {
        if (_swarm != null || swarmCount <= 0) return;
        Shader shader = Resources.Load<Shader>("DiveSwarm_NEW");
        if (shader == null) { Debug.LogWarning("[CreditsRoll_NEW] Resources/DiveSwarm_NEW.shader is missing; no swarm.", this); return; }
        _swarmMaterial = new Material(shader) { name = "Credits swarm (runtime)" };

        int n = swarmCount;
        var vertices = new Vector3[n * 4];
        var colours = new Color[n * 4];
        var corners = new Vector2[n * 4];
        var sizes = new Vector2[n * 4];
        var triangles = new int[n * 6];
        var rng = new System.Random(2026);
        float lo = Mathf.Log(Mathf.Max(1e-5f, inner * 2.5f)), hi = Mathf.Log(0.25f);
        Color warm = new Color(1f, 0.93f, 0.86f, 1f), cool = new Color(0.72f, 0.82f, 1f, 1f), pink = new Color(0.96f, 0.69f, 0.92f, 1f);
        for (int i = 0; i < n; i++)
        {
            float rho = Mathf.Exp(Mathf.Lerp(lo, hi, (float)rng.NextDouble()));
            float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
            float height = ((float)rng.NextDouble() + (float)rng.NextDouble() - 1f) * 0.12f * rho;
            Vector3 p = new Vector3(Mathf.Cos(angle) * rho, height, Mathf.Sin(angle) * rho);

            double pick = rng.NextDouble();
            Color c = pick < 0.4 ? cool : pick < 0.6 ? pink : warm;
            c.a = Mathf.Lerp(0.25f, 1f, (float)rng.NextDouble());
            float size = Mathf.Lerp(0.6f, 1.4f, (float)rng.NextDouble());
            float turnToGo = Mathf.Lerp(0.12f, 1f, (float)rng.NextDouble());

            int v = i * 4;
            for (int k = 0; k < 4; k++)
            {
                vertices[v + k] = p;
                colours[v + k] = c;
                sizes[v + k] = new Vector2(size, turnToGo);
            }
            corners[v + 0] = new Vector2(-1f, -1f);
            corners[v + 1] = new Vector2(1f, -1f);
            corners[v + 2] = new Vector2(1f, 1f);
            corners[v + 3] = new Vector2(-1f, 1f);
            int tri = i * 6;
            triangles[tri + 0] = v; triangles[tri + 1] = v + 2; triangles[tri + 2] = v + 1;
            triangles[tri + 3] = v; triangles[tri + 4] = v + 3; triangles[tri + 5] = v + 2;
        }

        var mesh = new Mesh { name = "Credits swarm" };
        mesh.vertices = vertices;
        mesh.colors = colours;
        mesh.uv = corners;
        mesh.uv2 = sizes;
        mesh.triangles = triangles;
        // Every member is spread by the shader, so the bounds must cover them all however the
        // swarm is scaled: never culled.
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);

        _swarm = new GameObject("Credits swarm") { layer = FinaleLayer() };
        Plain(_swarm, mesh, _swarmMaterial);
        _swarmMaterial.SetFloat("_Streak", 2f);
        _swarmMaterial.SetFloat("_Resolve", 1f);
        _swarmMaterial.SetFloat("_Core", 0.5f);
        _swarmMaterial.SetFloat("_Spin", 0f);
        _swarmMaterial.SetVector("_NearFade", new Vector4(0.5f, 3f, 0f, 0f));
        _swarm.SetActive(false);
    }

    /// <param name="radius">The galaxy's radius as drawn now: the swarm's scale.</param>
    /// <param name="pixel">World units per pixel at the Sun's distance.</param>
    void TickSwarm(Vector3 sunAt, Quaternion turn, float radius, float alpha, float scatter, float pixel)
    {
        if (_swarm == null) return;
        bool on = alpha > 0.001f && scatter < 0.999f;
        if (_swarm.activeSelf != on) _swarm.SetActive(on);
        if (!on) return;
        _swarm.transform.SetPositionAndRotation(sunAt, turn);
        _swarm.transform.localScale = Vector3.one * radius;
        _swarmMaterial.SetFloat("_Alpha", alpha);
        _swarmMaterial.SetFloat("_Scatter", scatter);
        // Members about two pixels across wherever the swarm is in its shrinking.
        _swarmMaterial.SetFloat("_SizeScale", 1.1f * pixel / Mathf.Max(1e-6f, radius));
    }

    void HideFinaleLights()
    {
        if (_star != null) _star.SetActive(false);
        if (_swarm != null) _swarm.SetActive(false);
        foreach (LineRenderer line in _orbitLines)
            if (line != null) line.gameObject.SetActive(false);
    }

    float HoldFor(int i)
    {
        int n = SplitNames(sections[i].names).Count;
        return holdSeconds + 0.5f * Mathf.Max(0, n - 3);
    }

    void SetMusic(float k)
    {
        if (_music != null) _music.volume = musicVolume * Mathf.Clamp01(k);
    }

    // ── Camera ──────────────────────────────────────────────────────────────

    /// <summary>Where the camera is, u of the way through section i's shot.</summary>
    Pose PoseFor(int i, float u)
    {
        Vector3 p = Focus(i);
        ShotDef d = Def(ShotOf(i));
        float baseYaw = BaseYaw(i);

        float yaw = baseYaw + Mathf.Lerp(d.yawFrom, d.yawTo, Smooth(u));
        float distance = viewDistance * d.distance * Mathf.Lerp(d.pushFrom, d.pushTo, Smooth(u));
        Vector3 position = p + Quaternion.Euler(d.pitch, yaw, 0f) * new Vector3(0f, 0f, -distance);

        float tanV = Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float tanH = tanV * view.aspect;
        float across = Mathf.Atan(d.sx * tanH) * Mathf.Rad2Deg;
        float up = Mathf.Atan(d.sy * tanV) * Mathf.Rad2Deg;
        Quaternion look = Quaternion.LookRotation(p - position, Vector3.up);

        // The light is fixed for the shot, from the middle of its orbit, so the camera moving
        // round the planet slides the terminator across it.
        Vector3 middle = p + Quaternion.Euler(d.pitch, baseYaw + (d.yawFrom + d.yawTo) * 0.5f, 0f) * Vector3.back;
        Vector3 light = (Quaternion.LookRotation(p - middle, Vector3.up) * d.light).normalized;

        return new Pose { position = position, rotation = look * Quaternion.Euler(up, -across, d.roll), light = light, focus = p };
    }

    Pose CurrentPose()
    {
        return new Pose { position = view.transform.position, rotation = view.transform.rotation, light = _light, focus = _focus };
    }

    Pose WidePose()
    {
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        for (int i = 0; i < Mathf.Max(1, sections.Count); i++)
        {
            Vector3 f = Focus(i);
            min = Vector3.Min(min, f);
            max = Vector3.Max(max, f);
        }
        Vector3 centre = (min + max) * 0.5f;
        float extent = Mathf.Max(spacing, (max - min).magnitude);
        Vector3 position = centre + Quaternion.Euler(16f, -10f, 0f) * Vector3.back * (extent * 0.85f + viewDistance);
        return new Pose { position = position, rotation = Quaternion.LookRotation(centre - position, Vector3.up),
                          light = new Vector3(0.6f, 0.45f, -0.65f).normalized, focus = centre };
    }

    IEnumerator Fly(Pose a, Pose b, float seconds, bool bank)
    {
        float t = 0f;
        while (t < seconds && !_skip)
        {
            t += Step();
            float raw = Mathf.Clamp01(t / seconds);
            Apply(Between(a, b, Smooth(raw), bank, raw));
            // Between two planets there is no rim to flare at: the glare dims in flight.
            _glareWeight = 1f - Mathf.Sin(raw * Mathf.PI);
            yield return null;
        }
        _glareWeight = 1f;
    }

    /// <summary>
    /// A curve from a to b: out and up a little on the way, turning with it, banking into the
    /// turn. The light swings across with the camera.
    /// </summary>
    Pose Between(Pose a, Pose b, float k, bool bank, float raw)
    {
        float gap = Vector3.Distance(a.position, b.position);
        Vector3 back = (a.rotation * Vector3.back + b.rotation * Vector3.back).normalized;
        Vector3 control = (a.position + b.position) * 0.5f + back * gap * 0.35f + Vector3.up * gap * 0.1f;
        Vector3 position = (1f - k) * (1f - k) * a.position + 2f * (1f - k) * k * control + k * k * b.position;

        Quaternion rotation = Quaternion.Slerp(a.rotation, b.rotation, k);
        if (bank)
        {
            float side = Mathf.Sign(Vector3.Dot(b.position - a.position, a.rotation * Vector3.right));
            rotation *= Quaternion.Euler(0f, 0f, -side * bankDegrees * Mathf.Sin(Mathf.Clamp01(raw) * Mathf.PI));
        }

        return new Pose { position = position, rotation = rotation, light = Vector3.Slerp(a.light, b.light, k).normalized,
                          focus = Vector3.Lerp(a.focus, b.focus, k) };
    }

    void Apply(Pose p)
    {
        view.transform.SetPositionAndRotation(p.position, p.rotation);
        _light = p.light;
        _focus = p.focus;
    }

    /// <summary>Section i's planet, or where it would be.</summary>
    Vector3 Focus(int i)
    {
        Transform p = i >= 0 && i < sections.Count ? sections[i].planet : null;
        return p != null ? p.position : PlaceOf(i);
    }

    // A little above and below the Sun's plane; the same every run.
    static readonly float[] ScatterY = { 0f, 0.15f, -0.1f, 0.2f, -0.15f, 0.08f, -0.2f, 0.12f };
    static readonly float[] Approach = { 0f, -18f, 14f, -10f, 20f, -15f, 10f, -20f };

    Vector3 PlaceOf(int i)
    {
        if (!scatter) return new Vector3(i * spacing, 0f, 0f);
        // On orbits round the Sun (SunCentre), each a spacing further out than the last and
        // a little further round the way the planets go, so the flight from one to the next
        // is about the same length - and leaving at the end, they are a solar system.
        float r = 2f * spacing, angle = 0f;
        for (int m = 1; m <= i; m++)
        {
            float next = r + spacing;
            angle += 1.1f * spacing / (0.5f * (r + next));
            r = next;
        }
        int j = i % ScatterY.Length;
        return SunCentre() + new Vector3(Mathf.Cos(angle) * r, ScatterY[j] * spacing * 0.5f, Mathf.Sin(angle) * r);
    }

    float BaseYaw(int i) { return scatter ? Approach[i % Approach.Length] : 0f; }

    // ── Building ────────────────────────────────────────────────────────────

    /// <summary>Every planet the same size, in its place, in section order.</summary>
    void LayOutPlanets()
    {
        for (int i = 0; i < sections.Count; i++)
        {
            Transform p = sections[i].planet;
            if (p == null) continue;

            p.position = PlaceOf(i);

            MeshFilter mf = p.GetComponent<MeshFilter>();
            if (mf != null) _occluders.Add(mf);
            float r = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.extents.x * p.lossyScale.x : 0f;
            if (r > 1e-4f) p.localScale *= planetRadius / r;
        }
    }

    /// <summary>The stars, on a canvas just inside the far plane, so every planet is in front of them.</summary>
    void BuildSky()
    {
        view.clearFlags = CameraClearFlags.SolidColor;
        view.backgroundColor = Color.black;

        var go = new GameObject("Credits sky", typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = view;
        canvas.planeDistance = view.farClipPlane * 0.95f;
        canvas.sortingOrder = -100;
        _sky = Starfield_NEW.AddBehind((RectTransform)go.transform, Starfield_NEW.Make(1024, 2026, 1.1f, 1.2f), 2.2f);

        // Oversized, so its corners stay off screen when it turns with the camera's roll.
        _skyRect = (RectTransform)_sky.transform;
        _skySize = _sky.uvRect.size;
        _skyRect.anchorMin = new Vector2(-0.4f, -0.4f);
        _skyRect.anchorMax = new Vector2(1.4f, 1.4f);
        _skyRect.offsetMin = _skyRect.offsetMax = Vector2.zero;
    }

    void BuildOverlay()
    {
        var go = new GameObject("Credits overlay", typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var root = (RectTransform)go.transform;

        // The glare, under the words.
        Texture2D glow = Radial(256, 2.2f);
        _glareStreakImage = Raw(root, "Glare streak", glow, new Vector2(1500f, 46f), out _glareStreak);
        _glareCoreImage = Raw(root, "Glare", glow, new Vector2(560f, 560f), out _glareCore);

        // A section; rebuilt for each one in its shot's place.
        _sectionGroup = Group(root, "Section", new Vector2(0.56f, 0.2f), new Vector2(0.95f, 0.8f));
        _sectionBlock = (RectTransform)_sectionGroup.transform;

        // A soft shade in from the right, behind the end card, should the galaxy's rim reach under it.
        _textShade = new GameObject("End shade", typeof(RectTransform)).AddComponent<RawImage>();
        _textShade.transform.SetParent(root, false);
        Stretch(_textShade.rectTransform, new Vector2(0.4f, 0f), Vector2.one);
        _textShade.texture = Gradient(64, true);
        _textShade.color = Color.clear;
        _textShade.raycastTarget = false;

        // The end card: a column down the right of the frame, everything from one left edge, as
        // a section's words sit beside its planet - here beside the galaxy. The title first;
        // the rest (_endBody) comes up after it.
        _endGroup = Group(root, "End", new Vector2(0.57f, 0.16f), new Vector2(0.955f, 0.84f));
        var end = (RectTransform)_endGroup.transform;
        if (titleSprite != null)
        {
            // Taller than the title is at this width, so the title fills the column's width.
            var img = new GameObject("Title", typeof(RectTransform)).AddComponent<Image>();
            img.transform.SetParent(end, false);
            img.sprite = titleSprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            Stretch((RectTransform)img.transform, new Vector2(0f, 0.76f), new Vector2(1f, 1f));
        }
        else
        {
            TextMeshProUGUI title = Text(end, "Title", titleWords, 96f, accentColour, _headingFont, TextAlignmentOptions.Left);
            Stretch(title.rectTransform, new Vector2(0f, 0.76f), new Vector2(1f, 1f));
            title.characterSpacing = 8f;
            title.enableWordWrapping = false;
            title.enableAutoSizing = true;
            title.fontSizeMin = 40f;
            title.fontSizeMax = 96f;
        }
        RectTransform rule = Rule(end, accentColour);
        rule.anchorMin = rule.anchorMax = new Vector2(0f, 0.735f);
        rule.pivot = new Vector2(0f, 0.5f);
        rule.sizeDelta = new Vector2(150f, 3f);

        _endBody = Group(end, "Body", Vector2.zero, new Vector2(1f, 0.7f));
        _endBody.alpha = 1f;
        var body = (RectTransform)_endBody.transform;
        TextMeshProUGUI heading = Text(body, "Heading", supportHeading, 30f, headingColour, _headingFont, TextAlignmentOptions.Left);
        Stretch(heading.rectTransform, new Vector2(0f, 0.86f), new Vector2(1f, 0.97f));
        heading.characterSpacing = 14f;
        heading.enableWordWrapping = false;
        heading.enableAutoSizing = true;
        heading.fontSizeMin = 18f;
        heading.fontSizeMax = 30f;
        TextMeshProUGUI who = Text(body, "Supporters", (supporters ?? "").Replace("\r", ""), 30f, nameColour, _bodyFont, TextAlignmentOptions.TopLeft);
        Stretch(who.rectTransform, new Vector2(0f, 0.2f), new Vector2(1f, 0.8f));
        who.lineSpacing = 16f;
        who.enableWordWrapping = false;
        who.enableAutoSizing = true;
        who.fontSizeMin = 16f;
        who.fontSizeMax = 30f;
        TextMeshProUGUI closing = Text(body, "Closing", closingLine, 24f, accentColour, _bodyFont, TextAlignmentOptions.BottomLeft);
        Stretch(closing.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.12f));
        closing.enableWordWrapping = false;

        // The bars, then black over everything for the way in and out.
        _barTop = Black(root, "Letterbox top");
        _barTop.anchorMin = new Vector2(0f, 1f);
        _barTop.anchorMax = Vector2.one;
        _barBottom = Black(root, "Letterbox bottom");
        _barBottom.anchorMin = Vector2.zero;
        _barBottom.anchorMax = new Vector2(1f, 0f);
        RectTransform fade = Black(root, "Fade");
        fade.anchorMin = Vector2.zero;
        fade.anchorMax = Vector2.one;
        _black = fade.GetComponent<Image>();
    }

    // ── A section's words ───────────────────────────────────────────────────

    void ShowSection(int i)
    {
        foreach (Transform child in _sectionBlock) Destroy(child.gameObject);
        _lines.Clear();
        _lineX.Clear();
        _lineY.Clear();

        Section s = sections[i];
        Layout layout = Def(ShotOf(i)).layout;
        Vector2 min, max;
        float side;   // where the words sit across the block: 0 from its left, 0.5 centred, 1 against its right
        switch (layout)
        {
            case Layout.Left: min = new Vector2(0.05f, 0.18f); max = new Vector2(0.44f, 0.82f); side = 1f; break;
            case Layout.Top: min = new Vector2(0.12f, 0.52f); max = new Vector2(0.88f, 0.86f); side = 0.5f; break;
            case Layout.Bottom: min = new Vector2(0.12f, 0.14f); max = new Vector2(0.88f, 0.46f); side = 0.5f; break;
            // Under the Sun's glare, which a close-up puts high on the right.
            case Layout.LowerRight: min = new Vector2(0.56f, 0.14f); max = new Vector2(0.95f, 0.56f); side = 0f; break;
            default: min = new Vector2(0.56f, 0.18f); max = new Vector2(0.95f, 0.82f); side = 0f; break;
        }
        Stretch(_sectionBlock, min, max);
        _sectionGroup.alpha = 1f;

        List<NameRow> rows = SplitNames(s.names);
        float canvasWidth = ((RectTransform)_sectionBlock.parent).rect.width;
        float blockWidth = (canvasWidth > 1f ? canvasWidth : 1920f) * (max.x - min.x);
        float height = (max.y - min.y) * 1080f;
        const float roleSize = 46f, gap = 20f;
        float roleHeight = roleSize * 1.2f;
        float lineSize = Mathf.Clamp((height - roleHeight - gap * 2f) / (Mathf.Max(1, rows.Count) * 1.32f), 22f, 38f);
        float lineHeight = lineSize * 1.32f;
        float content = roleHeight + gap * 2f + rows.Count * lineHeight;
        float y = -Mathf.Max(0f, height - content) * 0.5f;

        // The role, sized down if it is wider than its block.
        string role = (s.role ?? "").Replace("\r", "").ToUpperInvariant();
        _role = Text(_sectionBlock, "Role", role, roleSize, headingColour, _headingFont,
                     side == 0.5f ? TextAlignmentOptions.Center : TextAlignmentOptions.Left);
        _role.enableWordWrapping = false;
        _role.characterSpacing = 12f;
        float roleWidth = Width(_role, role);
        if (roleWidth > blockWidth)
        {
            _role.fontSize = roleSize * blockWidth / roleWidth;
            roleWidth = blockWidth;
        }
        _role.alpha = 0f;

        // The names, as a table: titles under titles, given names under given names, family
        // names under family names, notes under notes - each column as wide as its widest.
        TextMeshProUGUI probe = Text(_sectionBlock, "Measure", "", lineSize, nameColour, _bodyFont, TextAlignmentOptions.Left);
        probe.enableWordWrapping = false;
        probe.alpha = 0f;
        bool centred = side == 0.5f;
        var givenWidth = new float[rows.Count];
        float wTitle = 0f, wGiven = 0f, wFamily = 0f, wNote = 0f;
        for (int j = 0; j < rows.Count; j++)
        {
            givenWidth[j] = Width(probe, rows[j].given);
            wTitle = Mathf.Max(wTitle, Width(probe, rows[j].title));
            wGiven = Mathf.Max(wGiven, givenWidth[j]);
            wFamily = Mathf.Max(wFamily, Width(probe, rows[j].family));
            wNote = Mathf.Max(wNote, Width(probe, NoteMarkup(rows[j].note)));
        }
        float between = wFamily > 0f ? lineSize * 0.5f : 0f;   // the gap between given and family names
        float xGiven = wTitle > 0f ? wTitle + lineSize * 0.38f : 0f;
        float xFamily = xGiven + wGiven + between;
        float xNote = xFamily + wFamily + lineSize * 0.7f;
        float tableWidth = wNote > 0f ? xNote + wNote : xFamily + wFamily;
        float fit = 1f;
        if (tableWidth > blockWidth)
        {
            // Too wide for its block: everything a little smaller, in proportion.
            fit = blockWidth / tableWidth;
            lineSize *= fit;
            between *= fit; xGiven *= fit; xFamily *= fit; xNote *= fit; tableWidth = blockWidth;
        }

        // Beside a planet, role, rule and table share a left edge and every column is set from
        // its left. Over or under one, the names are set about the middle of the frame: the gap
        // between given and family names is on the centre line in every row, given names
        // running up to it from the left, family names away from it to the right (titles still
        // in a column at the far left, notes at the far right).
        float left = centred ? 0f : Mathf.Max(0f, (blockWidth - Mathf.Max(tableWidth, roleWidth)) * side);
        float tableLeft = centred ? blockWidth * 0.5f - (xFamily - between * 0.5f) : left;

        if (centred) Row(_role.rectTransform, y, roleHeight);
        else RowAt(_role.rectTransform, left, y, Mathf.Max(1f, blockWidth - left), roleHeight);

        _rule = Rule(_sectionBlock, accentColour);
        _rule.anchorMin = _rule.anchorMax = new Vector2(centred ? 0.5f : 0f, 1f);
        _rule.pivot = new Vector2(centred ? 0.5f : 0f, 0.5f);
        _rule.anchoredPosition = new Vector2(centred ? 0f : left, y - roleHeight - gap * 0.55f);
        _rule.sizeDelta = new Vector2(0f, 3f);

        float lineTop = y - roleHeight - gap * 2f;
        for (int j = 0; j < rows.Count; j++)
        {
            NameRow row = rows[j];
            float given = xGiven;
            if (centred)
            {
                // Up to the centre gap from the left; a single name, with no family name, across it.
                float w = givenWidth[j] * fit, end = xFamily - between;
                given = row.family.Length > 0 ? end - w : end + between * 0.5f - w * 0.5f;
            }
            string words = row.title;
            if (row.given.Length > 0) words += "<pos=" + Units(given) + ">" + row.given;
            if (row.family.Length > 0) words += "<pos=" + Units(xFamily) + ">" + row.family;
            if (row.note.Length > 0) words += "<pos=" + Units(xNote) + ">" + NoteMarkup(row.note);

            TextMeshProUGUI line = Text(_sectionBlock, "Name", words, lineSize, nameColour, _bodyFont, TextAlignmentOptions.Left);
            line.enableWordWrapping = false;
            float ly = lineTop - j * lineHeight;
            RowAt(line.rectTransform, tableLeft, ly, Mathf.Max(1f, tableWidth), lineHeight);
            line.alpha = 0f;
            _lines.Add(line);
            _lineX.Add(tableLeft);
            _lineY.Add(ly);
        }
    }

    /// <summary>The role tracks in, the rule wipes across, the names come up one by one; all of it fades together at the end.</summary>
    void AnimateSection(float t, float total)
    {
        float fade = Mathf.Max(0.01f, textFadeSeconds);
        _sectionGroup.alpha = Smooth((total - t) / fade);

        float r = Smooth(t / fade);
        if (_role != null)
        {
            _role.alpha = r;
            _role.characterSpacing = Mathf.Lerp(34f, 12f, r);
        }
        if (_rule != null) _rule.sizeDelta = new Vector2(_ruleWidth * Smooth((t - fade * 0.25f) / fade), 3f);

        for (int j = 0; j < _lines.Count; j++)
        {
            float a = Smooth((t - fade * 0.45f - j * 0.14f) / (fade * 0.8f));
            _lines[j].alpha = a;
            RectTransform rt = _lines[j].rectTransform;
            rt.anchoredPosition = new Vector2(_lineX[j], _lineY[j] - (1f - a) * 12f);
        }
    }

    /// <summary>
    /// A name in its columns: its title (Prof, Dr), given names, family name, the note from its
    /// round brackets, and the other name it also goes by, from its square ones.
    /// </summary>
    struct NameRow { public string title, given, family, note, also; }

    static readonly HashSet<string> Titles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Prof", "Professor", "Dr", "Doctor", "Mr", "Mrs", "Ms", "Mx", "Sir", "Dame" };

    /// <summary>
    /// One row a line. "Prof Vera Gluscevic (Science Lead)": a first word that is a title goes
    /// in the title column, the last word is the family name, what is between is the given
    /// names, and what is in brackets the note. Where that guesses wrong - a family name of
    /// two words - say it with bars: "Dr | Ana | de la Cruz", or "Ana | de la Cruz".
    ///
    /// A second name someone also goes by - a Chinese given name and an English one - would
    /// make the given-names column as wide as both and push every family name away from its
    /// given name. Put one in square brackets instead: "Xingru Yi [Winnie]" sets Xingru and Yi
    /// in their columns and (Winnie) small and quiet after the family name.
    /// </summary>
    List<NameRow> SplitNames(string typed)
    {
        var list = new List<NameRow>();
        foreach (string raw in (typed ?? "").Replace("\r", "").Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;
            var row = new NameRow { title = "", given = "", family = "", note = "", also = "" };

            Match also = Regex.Match(line, @"\[([^\]]*)\]");
            if (also.Success)
            {
                row.also = also.Groups[1].Value.Trim();
                line = line.Remove(also.Index, also.Length).Trim();
            }

            Match note = Regex.Match(line, @"\(([^)]*)\)");
            if (note.Success)
            {
                row.note = note.Groups[1].Value.Trim();
                line = line.Remove(note.Index, note.Length).Trim();
            }

            if (line.IndexOf('|') >= 0)
            {
                string[] cells = line.Split('|');
                for (int c = 0; c < cells.Length; c++) cells[c] = cells[c].Trim();
                if (cells.Length == 2) { row.given = cells[0]; row.family = cells[1]; }
                else { row.title = cells[0]; row.given = cells[1]; row.family = string.Join(" ", cells, 2, cells.Length - 2).Trim(); }
            }
            else
            {
                string[] words = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                int first = 0, last = words.Length - 1;
                if (words.Length > 1 && Titles.Contains(words[0].TrimEnd('.'))) { row.title = words[0]; first = 1; }
                if (last > first)
                {
                    row.family = words[last];
                    row.given = string.Join(" ", words, first, last - first);
                }
                else if (last == first) row.given = words[first];
            }

            // The other name rides after the family name, in its column (after the given
            // name, for someone with one name only).
            if (row.also.Length > 0)
            {
                string small = "<space=0.3em><size=70%><color=#" + ColorUtility.ToHtmlStringRGB(nameColour * 0.72f) + ">(" +
                               row.also + ")</color></size>";
                if (row.family.Length > 0) row.family += small;
                else row.given += small;
            }
            list.Add(row);
        }
        return list;
    }

    /// <summary>A note as it is set: small, in capitals, in the green; or as typed, in its brackets, with styling off.</summary>
    string NoteMarkup(string note)
    {
        if (string.IsNullOrEmpty(note)) return "";
        if (!styleNotes) return "(" + note + ")";
        return "<size=58%><cspace=0.12em><color=#" + ColorUtility.ToHtmlStringRGB(accentColour) + ">" +
               note.ToUpperInvariant() + "</color></cspace></size>";
    }

    /// <summary>How wide <paramref name="words"/> would be set in <paramref name="probe"/>'s face and size.</summary>
    static float Width(TMP_Text probe, string words)
    {
        if (string.IsNullOrEmpty(words)) return 0f;
        float w = probe.GetPreferredValues(words).x;
        // Should the measure fail, a fair guess keeps the columns apart.
        return w > 0f && !float.IsNaN(w) && !float.IsInfinity(w) ? w : words.Length * probe.fontSize * 0.6f;
    }

    static string Units(float x) { return x.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture); }

    // ── UI pieces ───────────────────────────────────────────────────────────

    static CanvasGroup Group(RectTransform parent, string name, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch((RectTransform)go.transform, min, max);
        var g = go.AddComponent<CanvasGroup>();
        g.alpha = 0f;
        g.blocksRaycasts = false;
        g.interactable = false;
        return g;
    }

    static TextMeshProUGUI Text(RectTransform parent, string name, string words, float size, Color colour, TMP_FontAsset face,
                                TextAlignmentOptions align)
    {
        var t = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        t.transform.SetParent(parent, false);
        Stretch(t.rectTransform, Vector2.zero, Vector2.one);
        if (face != null) t.font = face;
        t.text = words;
        t.fontSize = size;
        t.color = colour;
        t.alignment = align;
        t.enableWordWrapping = true;
        t.raycastTarget = false;
        return t;
    }

    /// <summary>A row across the top of its block, <paramref name="top"/> units down (negative), <paramref name="height"/> tall.</summary>
    static void Row(RectTransform rt, float top, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, height);
        rt.anchoredPosition = new Vector2(0f, top);
    }

    /// <summary>A row <paramref name="left"/> in from its block's left edge, <paramref name="top"/> units down (negative), of its own width.</summary>
    static void RowAt(RectTransform rt, float left, float top, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(left, top);
    }

    static RectTransform Rule(RectTransform parent, Color colour)
    {
        var img = new GameObject("Rule", typeof(RectTransform)).AddComponent<Image>();
        img.transform.SetParent(parent, false);
        img.color = colour;
        img.raycastTarget = false;
        return (RectTransform)img.transform;
    }

    static RectTransform Black(RectTransform parent, string name)
    {
        var img = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
        img.transform.SetParent(parent, false);
        var rt = (RectTransform)img.transform;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        img.color = Color.black;
        img.raycastTarget = false;
        return rt;
    }

    static RawImage Raw(RectTransform parent, string name, Texture2D tex, Vector2 size, out RectTransform rt)
    {
        var img = new GameObject(name, typeof(RectTransform)).AddComponent<RawImage>();
        img.transform.SetParent(parent, false);
        img.texture = tex;
        img.color = Color.clear;
        img.raycastTarget = false;
        rt = (RectTransform)img.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        return img;
    }

    /// <summary>A soft round glow, white with its alpha falling off from the middle.</summary>
    static Texture2D Radial(int size, float falloff)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Glow", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
        };
        var px = new Color[size * size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Clamp01(Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c);
                float a = Mathf.Pow(1f - d, falloff);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply(false, true);
        return tex;
    }

    /// <summary>
    /// A shade that has no edge, eased from clear to opaque: up to the top, or with
    /// <paramref name="across"/> to the right.
    /// </summary>
    static Texture2D Gradient(int size, bool across = false)
    {
        var tex = new Texture2D(across ? size : 1, across ? 1 : size, TextureFormat.RGBA32, false)
        {
            name = "Shade", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
        };
        for (int i = 0; i < size; i++)
        {
            float k = i / (float)(size - 1);
            tex.SetPixel(across ? i : 0, across ? 0 : i, new Color(1f, 1f, 1f, k * k * (3f - 2f * k)));
        }
        tex.Apply(false, true);
        return tex;
    }

    static void Stretch(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}

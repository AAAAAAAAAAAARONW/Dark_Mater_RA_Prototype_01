using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The credits, after the journey: one world for each part of the team, filmed like the end
/// of a film. The journey's own planets, Mercury out to Neptune, scattered through space; the
/// camera flies from one to the next and frames each differently - beside it, skimming its
/// horizon as the Sun comes up over the rim, close on its crescent, or far off - with who made
/// that part of the piece set beside it. Then it rises to see them all, the title and the
/// supporters come up, and the piece fades back to the cover.
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
/// TYPE. The art's own: role titles and headings in Squada One (the drawn titles' face) in the
/// art's yellow over a green rule; names in Montserrat SemiBold (the callouts' face). Both are
/// TTFs made into TMP font assets at start. A note in brackets after a name - "(Annenberg
/// Fellow)" - is set small, in capitals, in green.
///
/// THE FINALE, "POWERS OF TEN". The journey's dive into the Solar System, played backwards, in
/// one unbroken zoom: the camera comes round to the last planet's night side, the Sun just past
/// its rim; pulls back from it; the whole system falls into its Sun until it is one star; and
/// round that star the Milky Way - the journey's own GalaxyVFX_NEW - comes in out of the dark,
/// star by star, shrinking at the same rate until it is the whole galaxy, tilted and turning,
/// the bars opening and the title and supporters coming up above it. No cut, no flash. With no
/// galaxy assigned the credits end on a wide shot of the planets, as before.
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

        [Tooltip("One name per line. A note in brackets after a name is set small and green.")]
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

    [Tooltip("Scatter the planets above, below and behind one another, so the flight has depth. Off = a straight row.")]
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

    [Tooltip("How far the last planet's Sun is from it, world units: the star the whole system falls into.")]
    [Min(50f)] [SerializeField] float sunDistance = 500f;

    [Tooltip("How many times smaller everything gets over the zoom out.")]
    [Min(10f)] [SerializeField] float zoomOut = 2500f;

    [Tooltip("The zoom out, from the planet to the whole galaxy, seconds.")]
    [Min(2f)] [SerializeField] float zoomOutSeconds = 16f;

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
    CanvasGroup _sectionGroup, _endGroup;
    RectTransform _sectionBlock, _barTop, _barBottom, _glareCore, _glareStreak, _skyRect;
    Image _black;
    RawImage _sky, _glareCoreImage, _glareStreakImage;
    AudioSource _music;

    // The section on screen.
    TextMeshProUGUI _role;
    RectTransform _rule;
    readonly List<TextMeshProUGUI> _lines = new List<TextMeshProUGUI>();
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
                float size = Mathf.Clamp(gapLines / 200f, 0.15f, 1f) * Mathf.Lerp(0.25f, 1f, _sunFade);
                _glareCore.localScale = new Vector3(size, size, 1f);
                _glareStreak.localScale = new Vector3(0.6f + 0.6f * strength, 1f, 1f);
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
    /// One zoom out, the journey's dive into the Solar System played backwards, on one clock:
    ///
    ///   turn      From the last shot to the planet's night side: the Sun just past its rim,
    ///             the planet a lit crescent, both in frame.
    ///   leaving   The camera pulls back from the planet at a steadily growing rate; the
    ///             planet shrinks towards its Sun.
    ///   the star  The pull-back moves over to the Sun: the whole system - every planet -
    ///             falls into it, until it is one star.
    ///   galaxy    The Milky Way has been there round the star all along, too large to see.
    ///             It comes in out of the dark star by star, shrinking round the Sun at the
    ///             same rate, the camera turning from the Sun to its centre, until it is the
    ///             whole galaxy, tilted, under the title.
    ///
    /// Nothing is cut or covered. The world is scaled down round a point instead of the camera
    /// flying out, so the numbers stay small whatever the zoom.
    /// </summary>
    IEnumerator Finale()
    {
        // 1 · The turn: onto the planet's night side, the Sun just past its rim.
        Pose last = CurrentPose();
        Vector3 planet = last.focus;
        Vector3 back = last.position - planet;
        if (back.sqrMagnitude < 1e-4f) back = last.rotation * Vector3.back;
        back.Normalize();
        Vector3 right = last.rotation * Vector3.right, up = last.rotation * Vector3.up;
        float rimAngle = Mathf.Asin(Mathf.Clamp01(planetRadius / (viewDistance * 1.15f))) * Mathf.Rad2Deg + 3.5f;
        Vector3 toSun = (-back * Mathf.Cos(rimAngle * Mathf.Deg2Rad) +
                         (right * 0.8f + up * 0.6f).normalized * Mathf.Sin(rimAngle * Mathf.Deg2Rad)).normalized;
        Vector3 sunPoint = planet + toSun * sunDistance;
        Vector3 eye = planet + back * viewDistance * 1.15f;
        Pose turned = new Pose
        {
            position = eye,
            rotation = Quaternion.LookRotation(Vector3.Slerp(planet - eye, sunPoint - eye, 0.5f), Vector3.up),
            light = toSun,
            focus = planet
        };
        // As Fly, and the Sun comes in from far off to the star the system will fall into, so
        // the glare does not jump when the finale takes the Sun over.
        float turnSeconds = moveSeconds * 1.2f;
        for (float u = 0f; u < turnSeconds && !_skip; )
        {
            u += Step();
            float raw = Mathf.Clamp01(u / turnSeconds);
            Apply(Between(last, turned, Smooth(raw), false, raw));
            _sunRange = Mathf.Exp(Mathf.Lerp(Mathf.Log(5000f), Mathf.Log(sunDistance), Smooth(raw)));
            _glareWeight = 1f - 0.6f * Mathf.Sin(raw * Mathf.PI);
            yield return null;
        }
        _glareWeight = 1f;
        if (_skip) yield break;
        _sunAt = planet + toSun * sunDistance;

        // The world: everything that is scaled, and where it all started.
        Transform world = WorldRoot();
        Vector3 worldPosition = world != null ? world.position : Vector3.zero;
        Vector3 worldScale = world != null ? world.localScale : Vector3.one;
        Vector3 offset = Vector3.zero;      // world point = offset + scale * starting point
        float scale = 1f, logEnd = Mathf.Log(Mathf.Max(10f, zoomOut));
        _sunHeld = true;

        // The galaxy's place round the Sun, settled once the pull-back is centred on the Sun.
        bool placed = false;
        Quaternion galaxyTurn = Quaternion.identity;
        float galaxyEnd = galaxyRadius;
        Vector3 galaxyCentreEnd = Vector3.zero;
        Quaternion finalLook = turned.rotation;
        Vector3 galaxyCamera = GalaxyCameraLocal();

        float t = 0f, previousLog = 0f;
        while (t < zoomOutSeconds && !_skip)
        {
            t += Step();
            float k = Mathf.Clamp01(t / zoomOutSeconds);

            // A steady magnification, eased in and out: each second shows the last as a
            // smaller part of the next, as the dive does going in.
            float logNow = -logEnd * Smooth(k);
            float ds = Mathf.Exp(logNow - previousLog);
            previousLog = logNow;

            // About the planet first, then about the Sun.
            Vector3 planetNow = offset + scale * planet;
            Vector3 sunNow = offset + scale * sunPoint;
            Vector3 about = Vector3.Lerp(planetNow, sunNow, Smooth((k - 0.04f) / 0.4f));
            offset = about + (offset - about) * ds;
            scale *= ds;
            planetNow = offset + scale * planet;
            sunNow = offset + scale * sunPoint;

            if (world != null)
            {
                world.localScale = worldScale * scale;
                world.position = offset + scale * worldPosition;
                // A planet under a pixel only flickers: by then they are all in the Sun's light,
                // and they go.
                float px = planetRadius * scale / Mathf.Max(1e-3f, Vector3.Distance(eye, sunNow)) /
                           Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad) * Screen.height * 0.5f;
                if (px < 0.6f && world.gameObject.activeSelf) world.gameObject.SetActive(false);
            }
            _sunAt = sunNow;
            if (sun != null) sun.position = sunNow;
            _focus = planetNow;
            _light = (sunNow - planetNow).sqrMagnitude > 1e-8f ? (sunNow - planetNow).normalized : _light;

            // The galaxy: from the moment the pull-back is on the Sun, its Sun on ours, its
            // radius shrinking at the same rate as everything else.
            if (galaxy != null && k >= 0.42f)
            {
                if (!placed)
                {
                    placed = true;
                    PlaceGalaxyRound(sunNow, eye, galaxyCamera, out galaxyTurn, out galaxyEnd);
                    galaxyCentreEnd = sunNow - galaxyTurn * (sunInGalaxy * galaxyEnd);
                    float tanV = Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
                    finalLook = Quaternion.LookRotation(galaxyCentreEnd - eye, galaxyTurn * Vector3.up) *
                                Quaternion.Euler(Mathf.Atan(-0.5f * tanV) * Mathf.Rad2Deg, 0f, 0f);
                }
                float radius = galaxyEnd * Mathf.Exp(logNow + logEnd);   // galaxyEnd at k = 1
                galaxy.rotation = galaxyTurn;
                galaxy.localScale = Vector3.one * radius;
                galaxy.position = sunNow - galaxyTurn * (sunInGalaxy * radius);
                // In from the dark star by star from seventy times too big, whole by the end.
                float fade = 1f - Smooth((Mathf.Log(radius / galaxyEnd) - 0.25f) / 4.05f);
                SetGalaxyFade(fade);
                _sunFade = 1f - Smooth(fade * 1.25f);
            }

            // The camera holds its place; it looks between the planet and the Sun, then at the
            // Sun alone as the planet falls into it, then round to the galaxy's centre.
            Vector3 toPlanet = planetNow - eye, toTheSun = sunNow - eye;
            Vector3 look = Vector3.Slerp(toPlanet, toTheSun, 0.5f + 0.5f * Smooth((k - 0.08f) / 0.35f));
            Quaternion rotation = Quaternion.LookRotation(look, Vector3.up);
            if (placed) rotation = Quaternion.Slerp(rotation, finalLook, Smooth((k - 0.5f) / 0.45f));
            view.transform.SetPositionAndRotation(eye, rotation);

            _skyZoom = Mathf.Lerp(1f, 2.4f, Smooth(k));
            _bars = 1f - Smooth((k - 0.72f) / 0.25f);
            _textShade.color = new Color(0f, 0f, 0f, 0.55f * Smooth((k - 0.75f) / 0.25f));
            _endGroup.alpha = Smooth((k - 0.8f) / 0.2f);
            yield return null;
        }
        _endGroup.alpha = 1f;
        if (!placed || _skip) yield break;

        // Held over the turning galaxy, drifting in a little.
        Vector3 from = view.transform.position;
        t = 0f;
        while (t < endHoldSeconds && !_skip)
        {
            t += Step();
            view.transform.position = Vector3.Lerp(from, galaxyCentreEnd, 0.06f * t / Mathf.Max(0.01f, endHoldSeconds));
            galaxy.Rotate(Vector3.up, -1.2f * Step(), Space.Self);
            yield return null;
        }
    }

    /// <summary>The camera's place at the end, from the galaxy's centre, in its radii: 22 degrees above the disc, 3.8 out.</summary>
    static Vector3 GalaxyCameraLocal()
    {
        return Quaternion.Euler(0f, -24f, 0f) * new Vector3(0f, Mathf.Sin(22f * Mathf.Deg2Rad), -Mathf.Cos(22f * Mathf.Deg2Rad)) * 3.8f;
    }

    /// <summary>
    /// The galaxy's turn and final radius that put its Sun on <paramref name="sunAt"/> and the
    /// camera, where it stands, at <paramref name="cameraLocal"/> from its centre - its disc as
    /// level as that allows.
    /// </summary>
    void PlaceGalaxyRound(Vector3 sunAt, Vector3 eye, Vector3 cameraLocal, out Quaternion turn, out float radius)
    {
        Vector3 local = cameraLocal - sunInGalaxy;            // Sun to camera, in radii
        Vector3 world = eye - sunAt;
        radius = world.magnitude / Mathf.Max(1e-3f, local.magnitude);
        Vector3 n = world.normalized;
        turn = Quaternion.FromToRotation(local.normalized, n);
        Vector3 pole = Vector3.ProjectOnPlane(turn * Vector3.up, n), level = Vector3.ProjectOnPlane(Vector3.up, n);
        if (pole.sqrMagnitude > 1e-6f && level.sqrMagnitude > 1e-6f)
            turn = Quaternion.AngleAxis(Vector3.SignedAngle(pole, level, n), n) * turn;
    }

    /// <summary>What the finale shrinks: the object all the planets are under.</summary>
    Transform WorldRoot()
    {
        foreach (Section s in sections)
            if (s.planet != null) return s.planet.parent != null ? s.planet.root : s.planet;
        return null;
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

    // Scattered above, below and behind one another; the same every run.
    static readonly float[] ScatterY = { 0f, 0.15f, -0.1f, 0.2f, -0.15f, 0.08f, -0.2f, 0.12f };
    static readonly float[] ScatterZ = { 0f, 0.45f, -0.25f, 0.6f, -0.35f, 0.2f, 0.5f, -0.15f };
    static readonly float[] Approach = { 0f, -18f, 14f, -10f, 20f, -15f, 10f, -20f };

    Vector3 PlaceOf(int i)
    {
        if (!scatter) return new Vector3(i * spacing, 0f, 0f);
        int j = i % ScatterY.Length;
        return new Vector3(i * spacing, ScatterY[j] * spacing, ScatterZ[j] * spacing);
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

        // A soft dark band down from the top, behind the end card, over a bright galaxy.
        _textShade = new GameObject("End shade", typeof(RectTransform)).AddComponent<RawImage>();
        _textShade.transform.SetParent(root, false);
        Stretch(_textShade.rectTransform, new Vector2(0f, 0.28f), Vector2.one);
        _textShade.texture = Gradient(64);
        _textShade.color = Color.clear;
        _textShade.raycastTarget = false;

        // The end card, in the upper part of the frame: the galaxy turns below it.
        _endGroup = Group(root, "End", new Vector2(0.1f, 0.3f), new Vector2(0.9f, 0.97f));
        var end = (RectTransform)_endGroup.transform;
        if (titleSprite != null)
        {
            var img = new GameObject("Title", typeof(RectTransform)).AddComponent<Image>();
            img.transform.SetParent(end, false);
            img.sprite = titleSprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            Stretch((RectTransform)img.transform, new Vector2(0.2f, 0.64f), new Vector2(0.8f, 1f));
        }
        else
        {
            TextMeshProUGUI title = Text(end, "Title", titleWords, 96f, accentColour, _headingFont, TextAlignmentOptions.Center);
            Stretch(title.rectTransform, new Vector2(0f, 0.64f), new Vector2(1f, 1f));
            title.characterSpacing = 8f;
        }
        TextMeshProUGUI heading = Text(end, "Heading", supportHeading, 32f, headingColour, _headingFont, TextAlignmentOptions.Center);
        Stretch(heading.rectTransform, new Vector2(0f, 0.48f), new Vector2(1f, 0.57f));
        heading.characterSpacing = 14f;
        RectTransform rule = Rule(end, accentColour);
        rule.anchorMin = rule.anchorMax = new Vector2(0.5f, 0.465f);
        rule.pivot = new Vector2(0.5f, 0.5f);
        rule.sizeDelta = new Vector2(150f, 3f);
        TextMeshProUGUI who = Text(end, "Supporters", (supporters ?? "").Replace("\r", ""), 34f, nameColour, _bodyFont, TextAlignmentOptions.Top);
        Stretch(who.rectTransform, new Vector2(0f, 0.12f), new Vector2(1f, 0.44f));
        who.lineSpacing = 12f;
        who.enableAutoSizing = true;
        who.fontSizeMin = 22f;
        who.fontSizeMax = 34f;
        TextMeshProUGUI closing = Text(end, "Closing", closingLine, 26f, accentColour, _bodyFont, TextAlignmentOptions.Center);
        Stretch(closing.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.1f));

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
        _lineY.Clear();

        Section s = sections[i];
        Layout layout = Def(ShotOf(i)).layout;
        Vector2 min, max;
        TextAlignmentOptions align;
        float side;   // where the rule sits across the block: 0 left, 0.5 middle, 1 right
        switch (layout)
        {
            case Layout.Left: min = new Vector2(0.05f, 0.18f); max = new Vector2(0.44f, 0.82f); align = TextAlignmentOptions.Right; side = 1f; break;
            case Layout.Top: min = new Vector2(0.12f, 0.52f); max = new Vector2(0.88f, 0.86f); align = TextAlignmentOptions.Center; side = 0.5f; break;
            case Layout.Bottom: min = new Vector2(0.12f, 0.14f); max = new Vector2(0.88f, 0.46f); align = TextAlignmentOptions.Center; side = 0.5f; break;
            // Under the Sun's glare, which a close-up puts high on the right.
            case Layout.LowerRight: min = new Vector2(0.56f, 0.14f); max = new Vector2(0.95f, 0.56f); align = TextAlignmentOptions.Left; side = 0f; break;
            default: min = new Vector2(0.56f, 0.18f); max = new Vector2(0.95f, 0.82f); align = TextAlignmentOptions.Left; side = 0f; break;
        }
        Stretch(_sectionBlock, min, max);
        _sectionGroup.alpha = 1f;

        List<string> names = SplitNames(s.names);
        float height = (max.y - min.y) * 1080f;
        const float roleSize = 46f, gap = 20f;
        float roleHeight = roleSize * 1.2f;
        float lineSize = Mathf.Clamp((height - roleHeight - gap * 2f) / (Mathf.Max(1, names.Count) * 1.32f), 22f, 38f);
        float lineHeight = lineSize * 1.32f;
        float content = roleHeight + gap * 2f + names.Count * lineHeight;
        float y = -Mathf.Max(0f, height - content) * 0.5f;

        _role = Text(_sectionBlock, "Role", (s.role ?? "").Replace("\r", ""), roleSize, headingColour, _headingFont, align);
        _role.fontStyle = FontStyles.UpperCase;
        _role.enableWordWrapping = false;
        Row(_role.rectTransform, y, roleHeight);
        _role.alpha = 0f;

        _rule = Rule(_sectionBlock, accentColour);
        _rule.anchorMin = _rule.anchorMax = new Vector2(side, 1f);
        _rule.pivot = new Vector2(side, 0.5f);
        _rule.anchoredPosition = new Vector2(0f, y - roleHeight - gap * 0.55f);
        _rule.sizeDelta = new Vector2(0f, 3f);

        float lineTop = y - roleHeight - gap * 2f;
        for (int j = 0; j < names.Count; j++)
        {
            TextMeshProUGUI line = Text(_sectionBlock, "Name", names[j], lineSize, nameColour, _bodyFont, align);
            line.enableWordWrapping = false;
            float ly = lineTop - j * lineHeight;
            Row(line.rectTransform, ly, lineHeight);
            line.alpha = 0f;
            _lines.Add(line);
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
            rt.anchoredPosition = new Vector2(0f, _lineY[j] - (1f - a) * 12f);
        }
    }

    List<string> SplitNames(string typed)
    {
        var list = new List<string>();
        foreach (string raw in (typed ?? "").Replace("\r", "").Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length > 0) list.Add(StyleNote(line));
        }
        return list;
    }

    /// <summary>A note in brackets after a name, small, in capitals, in the green.</summary>
    string StyleNote(string line)
    {
        if (!styleNotes) return line;
        string hex = ColorUtility.ToHtmlStringRGB(accentColour);
        return Regex.Replace(line, @"\s*\(([^)]*)\)", m =>
            "  <size=58%><cspace=0.12em><color=#" + hex + ">" + m.Groups[1].Value.Trim().ToUpperInvariant() + "</color></cspace></size>");
    }

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

    /// <summary>Opaque at the top, clear at the bottom, eased: a shade that has no edge.</summary>
    static Texture2D Gradient(int height)
    {
        var tex = new Texture2D(1, height, TextureFormat.RGBA32, false)
        {
            name = "Shade", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
        };
        for (int y = 0; y < height; y++)
        {
            float k = y / (float)(height - 1);
            tex.SetPixel(0, y, new Color(1f, 1f, 1f, k * k * (3f - 2f * k)));
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

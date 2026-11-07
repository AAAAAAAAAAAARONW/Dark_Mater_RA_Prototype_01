using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The credits, after the journey: one world for each part of the team, the way a film's
/// end titles go by. The camera glides past the journey's own planets, Mercury out to
/// Neptune, each turning slowly and lit from the side; beside each one, who made that part
/// of the piece. At the end it rises to see all of them in a row, with the title and the
/// people and institutions who made it possible, and fades back to the cover.
///
/// EDITING THE NAMES. Everything said is in the Inspector, nothing in code: a section is a
/// role, its names (one per line) and the planet that carries it. Add, remove or reorder
/// sections freely; a section with no planet goes by over the stars alone. The supporters
/// and the closing line are the end card's.
///
/// The planets are copies of the journey's (CelestialBody_NEW, PlanetSphere_NEW), so they
/// look exactly as they do there. Their size and place are set here at start - every one
/// the same size, in a row along +X - so moving them in the scene changes nothing.
///
/// A skips to the end, after the first few seconds.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("CREDITS", "#B36AE2")]
public class CreditsRoll_NEW : MonoBehaviour
{
    [Serializable]
    public class Section
    {
        [Tooltip("Shown small and spaced above the names, e.g. DIRECTED BY.")]
        public string role = "ROLE";

        [Tooltip("One name per line.")]
        [TextArea(2, 8)] public string names = "Name Surname";

        [Tooltip("The planet this section goes by with. Empty = over the stars alone.")]
        public Transform planet;
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

    [Tooltip("The planets' Sun (CelestialBody_NEW lights from it). It is moved with the camera so " +
             "every planet is lit from the same side.")]
    [SerializeField] Transform sun;

    [SerializeField] TMP_FontAsset font;

    [Tooltip("Every planet's radius, world units.")]
    [Min(0.1f)] [SerializeField] float planetRadius = 3f;

    [Tooltip("Distance between planets along the row, world units.")]
    [Min(1f)] [SerializeField] float spacing = 40f;

    [Tooltip("Camera distance from the planet in focus, world units.")]
    [Min(1f)] [SerializeField] float viewDistance = 13f;

    [Tooltip("Where the planet sits, as a fraction of half the screen's width to the left of centre.")]
    [Range(0f, 0.8f)] [SerializeField] float planetLeft = 0.36f;

    [Tooltip("Degrees per second each planet turns.")]
    [SerializeField] float spin = 5f;

    [Tooltip("Where the light comes from, relative to the planet in focus: right, up, towards the camera.")]
    [SerializeField] Vector3 lightFrom = new Vector3(0.9f, 0.35f, -0.55f);

    [Header("Timing (seconds)")]
    [Min(0f)] [SerializeField] float introSeconds = 3f;
    [Min(0.1f)] [SerializeField] float textFadeSeconds = 1.2f;
    [Min(0f)] [SerializeField] float holdSeconds = 6f;
    [Min(0.1f)] [SerializeField] float moveSeconds = 4f;
    [Min(0f)] [SerializeField] float endHoldSeconds = 10f;
    [Min(0.1f)] [SerializeField] float fadeOutSeconds = 3f;
    [Tooltip("A skips to the end after this long.")]
    [Min(0f)] [SerializeField] float skipAfterSeconds = 3f;

    [Header("Next")]
    [Tooltip("Where the piece goes after the credits, as an asset path: the cover.")]
    [SerializeField] string returnScenePath = "Assets/_Core_NEW/PC_Cover.unity";

    // Built at start.
    CanvasGroup _sectionGroup, _endGroup;
    RectTransform _sectionBlock;
    TextMeshProUGUI _role, _names;
    Image _black;
    RawImage _sky;

    float _clock;
    bool _skip;
    Vector3 _focus;   // where the light is aimed from: the planet in focus, or the row's middle

    void Start()
    {
        if (view == null) view = Camera.main;
        if (view == null)
        {
            Debug.LogError("[CreditsRoll_NEW] No camera.", this);
            enabled = false;
            return;
        }

        LayOutPlanets();
        BuildSky();
        BuildText();
        StartCoroutine(Run());
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        _clock += dt;

        // Every planet turns; the light follows the camera's subject.
        foreach (Section s in sections)
            if (s.planet != null) s.planet.Rotate(Vector3.up, spin * dt, Space.Self);
        if (sun != null) sun.position = _focus + lightFrom.normalized * 5000f;

        if (_sky != null)
        {
            Rect uv = _sky.uvRect;
            uv.x += 0.0025f * dt;
            _sky.uvRect = uv;
        }

        if (!_skip && _clock > skipAfterSeconds && InputScheme_NEW.ConfirmDown()) _skip = true;
    }

    // ── The run ─────────────────────────────────────────────────────────────

    IEnumerator Run()
    {
        // Out of black onto the first world.
        PlaceCamera(Focus(0), 1.05f);
        _black.color = Color.black;
        yield return Fade(_black, 1f, 0f, introSeconds);

        for (int i = 0; i < sections.Count && !_skip; i++)
        {
            Section s = sections[i];
            _role.text = s.role;
            _names.text = s.names;

            // In, held with a slow push, out.
            float total = textFadeSeconds * 2f + holdSeconds, t = 0f;
            while (t < total && !_skip)
            {
                t += Time.unscaledDeltaTime;
                PlaceCamera(Focus(i), Mathf.Lerp(1.05f, 0.97f, t / total));

                float a = Mathf.Min(Mathf.Clamp01(t / textFadeSeconds), Mathf.Clamp01((total - t) / textFadeSeconds));
                _sectionGroup.alpha = Mathf.SmoothStep(0f, 1f, a);
                _sectionBlock.anchoredPosition = new Vector2(0f, Mathf.Lerp(-14f, 0f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / textFadeSeconds))));
                yield return null;
            }
            _sectionGroup.alpha = 0f;

            // On to the next world, pulling back a little on the way.
            if (i + 1 < sections.Count && !_skip)
                yield return Move(CameraAt(Focus(i), 0.97f), CameraAt(Focus(i + 1), 1.05f), Focus(i), Focus(i + 1), moveSeconds, 0.3f);
        }

        if (!_skip)
        {
            // Up and back to see them all in a row.
            Vector3 middle = RowMiddle();
            Vector3 from = view.transform.position, to = WideShot();
            Quaternion r0 = view.transform.rotation, r1 = Quaternion.LookRotation(middle - to);
            float t = 0f, seconds = moveSeconds * 1.6f;
            while (t < seconds && !_skip)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                view.transform.position = Vector3.Lerp(from, to, k);
                view.transform.rotation = Quaternion.Slerp(r0, r1, k);
                _focus = Vector3.Lerp(_focus, middle, k);
                _endGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - seconds * 0.5f) / (seconds * 0.5f)));
                yield return null;
            }
            _endGroup.alpha = 1f;

            t = 0f;
            while (t < endHoldSeconds && !_skip)
            {
                t += Time.unscaledDeltaTime;
                view.transform.position = Vector3.Lerp(to, to + (middle - to) * 0.06f, t / Mathf.Max(0.01f, endHoldSeconds));
                yield return null;
            }
        }

        yield return Fade(_black, _black.color.a, 1f, fadeOutSeconds);
        enabled = false;
        SceneFlow_NEW.Load(returnScenePath, this);
    }

    IEnumerator Move(Vector3 from, Vector3 to, Vector3 focusFrom, Vector3 focusTo, float seconds, float pullBack)
    {
        float t = 0f;
        while (t < seconds && !_skip)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / seconds);
            Vector3 p = Vector3.Lerp(from, to, k);
            p.z -= Mathf.Sin(k * Mathf.PI) * pullBack * viewDistance;
            view.transform.position = p;
            view.transform.rotation = BaseRotation();
            _focus = Vector3.Lerp(focusFrom, focusTo, k);
            yield return null;
        }
    }

    static IEnumerator Fade(Graphic g, float from, float to, float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            g.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds)));
            yield return null;
        }
        g.color = new Color(0f, 0f, 0f, to);
    }

    // ── Camera ──────────────────────────────────────────────────────────────

    /// <summary>The point the camera frames for section <paramref name="i"/>: its planet, or where it would be.</summary>
    Vector3 Focus(int i)
    {
        Transform p = i >= 0 && i < sections.Count ? sections[i].planet : null;
        return p != null ? p.position : new Vector3(i * spacing, 0f, 0f);
    }

    void PlaceCamera(Vector3 focus, float distanceScale)
    {
        _focus = focus;
        view.transform.position = CameraAt(focus, distanceScale);
        view.transform.rotation = BaseRotation();
    }

    /// <summary>Back from the planet, and shifted right so the planet sits in the left of the frame.</summary>
    Vector3 CameraAt(Vector3 focus, float distanceScale)
    {
        float d = viewDistance * distanceScale;
        float halfWidth = d * Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad) * view.aspect;
        return focus + new Vector3(planetLeft * halfWidth, 0.08f * d, -d);
    }

    static Quaternion BaseRotation() { return Quaternion.LookRotation(new Vector3(0f, -0.08f, 1f)); }

    Vector3 RowMiddle()
    {
        return new Vector3(Mathf.Max(0, sections.Count - 1) * spacing * 0.5f, 0f, 0f);
    }

    Vector3 WideShot()
    {
        float row = Mathf.Max(1, sections.Count - 1) * spacing;
        return RowMiddle() + new Vector3(0f, row * 0.18f, -row * 0.62f);
    }

    // ── Building ────────────────────────────────────────────────────────────

    /// <summary>Every planet the same size, in a row along +X, in section order.</summary>
    void LayOutPlanets()
    {
        for (int i = 0; i < sections.Count; i++)
        {
            Transform p = sections[i].planet;
            if (p == null) continue;

            p.position = new Vector3(i * spacing, 0f, 0f);

            MeshFilter mf = p.GetComponent<MeshFilter>();
            float r = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.extents.x * p.lossyScale.x : 0f;
            if (r > 1e-4f) p.localScale *= planetRadius / r;
        }
    }

    /// <summary>The stars, on a canvas just inside the camera's far plane, so every planet is in front of them.</summary>
    void BuildSky()
    {
        view.clearFlags = CameraClearFlags.SolidColor;
        view.backgroundColor = Color.black;

        var go = new GameObject("Credits sky", typeof(RectTransform)) { hideFlags = HideFlags.DontSave };
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = view;
        canvas.planeDistance = view.farClipPlane * 0.95f;
        canvas.sortingOrder = -100;
        _sky = Starfield_NEW.AddBehind((RectTransform)go.transform, Starfield_NEW.Make(1024, 2026, 1.1f, 1.2f), 1.4f);
    }

    void BuildText()
    {
        var go = new GameObject("Credits text", typeof(RectTransform)) { hideFlags = HideFlags.DontSave };
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var root = (RectTransform)go.transform;

        // A section: the role above a short rule, the names below, on the right of the frame.
        _sectionGroup = Group(root, "Section", new Vector2(0.55f, 0.2f), new Vector2(0.94f, 0.8f));
        _sectionBlock = (RectTransform)_sectionGroup.transform;
        _role = Text(_sectionBlock, "Role", "", 24f, new Color(0.86f, 0.8f, 0.68f, 1f), TextAlignmentOptions.BottomLeft,
                     new Vector2(0f, 0.56f), new Vector2(1f, 0.7f));
        _role.characterSpacing = 22f;
        _role.fontStyle = FontStyles.UpperCase;
        Rule(_sectionBlock, new Vector2(0f, 0.53f), 140f, new Color(0.86f, 0.8f, 0.68f, 0.6f));
        _names = Text(_sectionBlock, "Names", "", 48f, Color.white, TextAlignmentOptions.TopLeft,
                      new Vector2(0f, 0.05f), new Vector2(1f, 0.5f));
        _names.lineSpacing = 18f;
        _names.characterSpacing = 2f;

        // The end card, in the middle.
        _endGroup = Group(root, "End", new Vector2(0.15f, 0.12f), new Vector2(0.85f, 0.9f));
        var end = (RectTransform)_endGroup.transform;
        if (titleSprite != null)
        {
            var img = new GameObject("Title", typeof(RectTransform)).AddComponent<Image>();
            img.transform.SetParent(end, false);
            img.sprite = titleSprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            Stretch((RectTransform)img.transform, new Vector2(0.15f, 0.62f), new Vector2(0.85f, 0.98f));
        }
        else
        {
            TextMeshProUGUI title = Text(end, "Title", titleWords, 64f, Color.white, TextAlignmentOptions.Center,
                                         new Vector2(0f, 0.7f), new Vector2(1f, 0.95f));
            title.characterSpacing = 18f;
        }
        TextMeshProUGUI heading = Text(end, "Heading", supportHeading, 20f, new Color(0.86f, 0.8f, 0.68f, 1f),
                                       TextAlignmentOptions.Center, new Vector2(0f, 0.5f), new Vector2(1f, 0.58f));
        heading.characterSpacing = 22f;
        Text(end, "Supporters", supporters, 30f, Color.white, TextAlignmentOptions.Top,
             new Vector2(0f, 0.12f), new Vector2(1f, 0.48f)).lineSpacing = 14f;
        Text(end, "Closing", closingLine, 24f, new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.Center,
             new Vector2(0f, 0f), new Vector2(1f, 0.1f)).fontStyle = FontStyles.Italic;

        // Black over everything, for the way in and out.
        _black = new GameObject("Fade", typeof(RectTransform)).AddComponent<Image>();
        _black.transform.SetParent(root, false);
        Stretch((RectTransform)_black.transform, Vector2.zero, Vector2.one);
        _black.color = Color.black;
        _black.raycastTarget = false;
    }

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

    TextMeshProUGUI Text(RectTransform parent, string name, string words, float size, Color colour,
                         TextAlignmentOptions align, Vector2 min, Vector2 max)
    {
        var t = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        t.transform.SetParent(parent, false);
        Stretch((RectTransform)t.transform, min, max);
        if (font != null) t.font = font;
        t.text = words;
        t.fontSize = size;
        t.color = colour;
        t.alignment = align;
        t.enableWordWrapping = true;
        t.raycastTarget = false;
        return t;
    }

    static void Rule(RectTransform parent, Vector2 anchor, float width, Color colour)
    {
        var img = new GameObject("Rule", typeof(RectTransform)).AddComponent<Image>();
        img.transform.SetParent(parent, false);
        var rt = (RectTransform)img.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(width, 2f);
        rt.anchoredPosition = Vector2.zero;
        img.color = colour;
        img.raycastTarget = false;
    }

    static void Stretch(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}

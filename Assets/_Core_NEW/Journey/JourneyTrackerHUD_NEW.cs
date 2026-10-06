using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The journey tracker as a horizontal bar along the bottom of the screen, drawn with the
/// art in _Core_NEW/UI (JourneyTracker_*.png). Replaces UniverseJourneyHUD's vertical bar
/// in Gameplay_Scene_Ana; the other scenes still use the old one.
///
/// One node per UniverseJourneyTracker phase, sitting on the background's ticks from the
/// left. Each node shows one of three images: Passed (dark green) for phases already
/// behind, Current (yellow) for the phase the tracker is in, Ahead (light green) for the
/// rest. The gradient line grows along the background's line from the first node, its
/// head at the current node plus PhaseProgress of the way to the next one, and the arrow
/// rides on that head. Remaining distance goes in the green tab at the bottom right.
///
/// Every position is a fraction of the background image, measured from
/// JourneyTracker_BG.png, so the HUD can be any size as long as trackArea keeps the
/// image's proportions. Tools > Journey NEW > Journey Tracker > Apply Art builds the
/// hierarchy and fills in the sprites.
/// </summary>
[DisallowMultipleComponent]
public class JourneyTrackerHUD_NEW : MonoBehaviour
{
    [Serializable]
    public class StationArt
    {
        public Sprite ahead;
        public Sprite current;
        public Sprite passed;
    }

    [Header("References")]
    [SerializeField] UniverseJourneyTracker tracker;

    [Tooltip("The rect the background image fills. Nodes, line and arrow are placed in it.")]
    [SerializeField] RectTransform trackArea;

    [Tooltip("The gradient line. Anchored and sized by this script.")]
    [SerializeField] RawImage fillImage;

    [Tooltip("JourneyTracker_Arrow, kept at the head of the gradient line.")]
    [SerializeField] Image arrowImage;

    [Tooltip("Remaining distance, in the green tab at the bottom right.")]
    [SerializeField] TMP_Text distanceText;

    [Header("Stations (one per tracker phase, Quasar first)")]
    [SerializeField] StationArt[] stations = new StationArt[7];

    [Header("Background layout (fractions of JourneyTracker_BG.png, from its top-left)")]
    [Tooltip("x of the first tick's centre. 284.5 / 5605.")]
    [SerializeField] float firstTickX = 0.05076f;

    [Tooltip("Distance between ticks. 728 / 5605.")]
    [SerializeField] float tickSpacing = 0.12989f;

    [Tooltip("y of the line's centre, from the top. 282.5 / 605.")]
    [SerializeField] float lineY = 0.46694f;

    [Tooltip("Gradient line thickness. The drawn line is 12 / 605.")]
    [SerializeField] float lineThickness = 0.02314f;

    [Tooltip("Width of the background image in pixels, to scale the node and arrow images " +
             "the same as the background.")]
    [SerializeField] float backgroundPixelWidth = 5605f;

    [Tooltip("Where in a node image its circle's centre is, as a pivot (0,0 = bottom-left). " +
             "The circle in JourneyTracker_*_*.png is at (207.5, 143.5) from the top-left of 412 x 296.")]
    [SerializeField] Vector2 nodePivot = new Vector2(0.5036f, 0.5152f);

    [Tooltip("The arrow's tip as a pivot, so the tip sits on the head of the line.")]
    [SerializeField] Vector2 arrowPivot = new Vector2(0.95f, 0.5f);

    [Header("Line")]
    [Tooltip("Colours along the whole line, Quasar to Earth.")]
    [SerializeField]
    Color[] gradientColors = new Color[]
    {
        new Color(0.267f, 0.133f, 0.667f, 1f),
        new Color(0.127f, 0.111f, 0.670f, 1f),
        new Color(0.136f, 0.814f, 0.821f, 1f),
        new Color(0.114f, 0.620f, 0.323f, 1f),
        new Color(1.000f, 0.943f, 0.325f, 1f),
        new Color(0.868f, 0.443f, 0.119f, 1f),
        new Color(0.800f, 0.200f, 0.133f, 1f),
    };

    [Tooltip("How fast the line's head catches up with the player, per second. 0 = no smoothing.")]
    [SerializeField] float headFollowSpeed = 6f;

    [Header("Current node")]
    [SerializeField] float pulseSpeed = 2.5f;
    [SerializeField] float pulseAmount = 0.04f;

    [Header("Auto-Hide")]
    [Tooltip("Slide the HUD off the bottom of the screen, showing it for a while on every " +
             "phase change or on the show button.")]
    [SerializeField] bool autoHide = true;

    [Tooltip("The rect that slides. Defaults to this object's.")]
    [SerializeField] RectTransform hudPanel;

    [SerializeField] float hudVisibleDuration = 8f;
    [SerializeField] float hudSlideDuration = 0.35f;

    [Tooltip("Input Manager button that shows the HUD. Fire3 = X on an Xbox pad. Empty = none.")]
    [SerializeField] string hudShowButton = "Fire3";

    Image[] _nodes;
    Texture2D _gradientTex;
    float _head = -1f;      // line head in stations: 2.5 = halfway from node 2 to node 3
    Vector2 _layoutSize;
    bool _initialized;

    RectTransform _rootRT;
    Vector2 _shownPos;
    bool _hudVisible = true;
    float _hideTimer;
    Coroutine _slide;

    float _textTimer;
    const float TextInterval = 0.15f;

    int StationCount => stations != null ? stations.Length : 0;

    void Awake()
    {
        if (tracker == null) tracker = FindObjectOfType<UniverseJourneyTracker>();
        BuildGradientTexture();
    }

    void Start()
    {
        if (tracker == null) { Debug.LogWarning("[JourneyTrackerHUD_NEW] No UniverseJourneyTracker.", this); return; }
        if (trackArea == null) { Debug.LogWarning("[JourneyTrackerHUD_NEW] Track Area not assigned.", this); return; }

        BuildNodes();
        Layout();
        UpdateNodes();
        UpdateLine(snap: true);
        UpdateDistanceText();

        InitAutoHide();
        tracker.OnPhaseChanged += HandlePhaseChanged;
        _initialized = true;
    }

    void OnDestroy()
    {
        if (tracker != null) tracker.OnPhaseChanged -= HandlePhaseChanged;
        if (_gradientTex != null) Destroy(_gradientTex);
    }

    void Update()
    {
        if (!_initialized) return;

        if (trackArea.rect.size != _layoutSize) Layout();

        UpdateNodes();
        UpdateLine(snap: false);

        _textTimer -= Time.deltaTime;
        if (_textTimer <= 0f)
        {
            _textTimer = TextInterval;
            UpdateDistanceText();
        }

        TickAutoHide();
    }

    // ── Nodes ────────────────────────────────────────────────────────────

    void BuildNodes()
    {
        _nodes = new Image[StationCount];
        for (int i = 0; i < StationCount; i++)
        {
            var go = new GameObject("Node_" + (UniverseJourneyTracker.JourneyPhase)i,
                                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = trackArea.gameObject.layer;
            go.transform.SetParent(trackArea, false);

            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            _nodes[i] = img;
        }
    }

    /// <summary>Places nodes, line and arrow in trackArea. Again whenever its size changes.</summary>
    void Layout()
    {
        _layoutSize = trackArea.rect.size;
        float scale = _layoutSize.x / Mathf.Max(1f, backgroundPixelWidth);
        float y = 1f - lineY;

        for (int i = 0; i < StationCount; i++)
        {
            RectTransform rt = _nodes[i].rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(TickX(i), y);
            rt.pivot = nodePivot;
            rt.anchoredPosition = Vector2.zero;

            Sprite s = SpriteFor(i, State.Ahead);
            rt.sizeDelta = s != null ? s.rect.size * scale : new Vector2(412f, 296f) * scale;
        }

        if (fillImage != null)
        {
            RectTransform rt = fillImage.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(TickX(0), y);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            fillImage.raycastTarget = false;
        }

        if (arrowImage != null)
        {
            RectTransform rt = arrowImage.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(TickX(0), y);
            rt.pivot = arrowPivot;
            rt.sizeDelta = arrowImage.sprite != null ? arrowImage.sprite.rect.size * scale : Vector2.one * 20f;
            arrowImage.raycastTarget = false;
        }
    }

    float TickX(float station) => firstTickX + tickSpacing * station;

    enum State { Passed, Current, Ahead }

    Sprite SpriteFor(int i, State state)
    {
        StationArt art = stations[i];
        if (art == null) return null;
        switch (state)
        {
            case State.Passed:  return art.passed;
            case State.Current: return art.current;
            default:            return art.ahead;
        }
    }

    void UpdateNodes()
    {
        int current = (int)tracker.CurrentPhase;
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;

        for (int i = 0; i < StationCount; i++)
        {
            State state = i < current ? State.Passed : i == current ? State.Current : State.Ahead;
            Sprite s = SpriteFor(i, state);
            if (_nodes[i].sprite != s) _nodes[i].sprite = s;
            _nodes[i].enabled = s != null;
            _nodes[i].rectTransform.localScale = Vector3.one * (state == State.Current ? pulse : 1f);
        }
    }

    // ── Line and arrow ───────────────────────────────────────────────────

    void BuildGradientTexture()
    {
        const int width = 256;
        _gradientTex = new Texture2D(width, 2, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        for (int x = 0; x < width; x++)
        {
            Color c = SampleGradient((float)x / (width - 1));
            _gradientTex.SetPixel(x, 0, c);
            _gradientTex.SetPixel(x, 1, c);
        }
        _gradientTex.Apply();

        if (fillImage != null) fillImage.texture = _gradientTex;
    }

    Color SampleGradient(float t)
    {
        if (gradientColors == null || gradientColors.Length == 0) return Color.white;
        if (gradientColors.Length == 1) return gradientColors[0];
        float scaled = Mathf.Clamp01(t) * (gradientColors.Length - 1);
        int lo = Mathf.FloorToInt(scaled);
        int hi = Mathf.Min(lo + 1, gradientColors.Length - 1);
        return Color.Lerp(gradientColors[lo], gradientColors[hi], scaled - lo);
    }

    void UpdateLine(bool snap)
    {
        int last = Mathf.Max(0, StationCount - 1);
        int current = (int)tracker.CurrentPhase;
        float target = current >= last ? last : current + Mathf.Clamp01(tracker.PhaseProgress);

        _head = snap || _head < 0f || headFollowSpeed <= 0f
            ? target
            : Mathf.Lerp(_head, target, 1f - Mathf.Exp(-headFollowSpeed * Time.deltaTime));

        float length = tickSpacing * _head * _layoutSize.x;

        if (fillImage != null)
        {
            fillImage.rectTransform.sizeDelta = new Vector2(length, lineThickness * _layoutSize.y);
            // The line shows the part of the whole gradient it has covered.
            fillImage.uvRect = new Rect(0f, 0f, last > 0 ? Mathf.Max(_head / last, 0.001f) : 1f, 1f);
        }

        if (arrowImage != null)
            arrowImage.rectTransform.anchoredPosition = new Vector2(length, 0f);
    }

    // ── Distance ─────────────────────────────────────────────────────────

    void UpdateDistanceText()
    {
        if (distanceText == null) return;

        // "5.51 B ly" -> "5.51 B LIGHTYEARS TO EARTH", as the art has it.
        string d = UniverseJourneyTracker.FormatDistance(tracker.RemainingDistanceLy).ToUpperInvariant();
        if (d.EndsWith(" LY")) d = d.Substring(0, d.Length - 3) + " LIGHTYEARS";
        distanceText.text = d + " TO EARTH";
    }

    // ── Auto-hide ────────────────────────────────────────────────────────

    void HandlePhaseChanged(UniverseJourneyTracker.JourneyPhase phase, UniverseJourneyTracker.PhaseData data)
    {
        ShowHUD();
    }

    void InitAutoHide()
    {
        _rootRT = hudPanel != null ? hudPanel : transform as RectTransform;
        if (_rootRT == null) return;

        _shownPos = _rootRT.anchoredPosition;
        if (autoHide)
        {
            _rootRT.anchoredPosition = HiddenPos();
            _hudVisible = false;
        }
    }

    /// <summary>Just below the bottom of the screen, whatever the panel's pivot and offset.</summary>
    Vector2 HiddenPos() => _shownPos - new Vector2(0f, _shownPos.y + _rootRT.rect.height * (1f - _rootRT.pivot.y) + 4f);

    void TickAutoHide()
    {
        if (!autoHide || _rootRT == null) return;

        if (!string.IsNullOrEmpty(hudShowButton))
        {
            try { if (Input.GetButtonDown(hudShowButton)) ShowHUD(); }
            catch { /* button not in the Input Manager */ }
        }

        if (_hudVisible)
        {
            _hideTimer -= Time.deltaTime;
            if (_hideTimer <= 0f) Slide(false);
        }
    }

    public void ShowHUD()
    {
        if (!autoHide || _rootRT == null) return;
        _hideTimer = hudVisibleDuration;
        if (!_hudVisible) Slide(true);
    }

    void Slide(bool visible)
    {
        if (_slide != null) StopCoroutine(_slide);
        _hudVisible = visible;
        _slide = StartCoroutine(SlideRoutine(visible ? _shownPos : HiddenPos()));
    }

    IEnumerator SlideRoutine(Vector2 to)
    {
        Vector2 from = _rootRT.anchoredPosition;
        for (float t = 0f; t < hudSlideDuration; t += Time.deltaTime)
        {
            _rootRT.anchoredPosition = Vector2.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / hudSlideDuration));
            yield return null;
        }
        _rootRT.anchoredPosition = to;
        _slide = null;
    }

#if UNITY_EDITOR
    [ContextMenu("Auto-Hide: Show")]
    void DebugShow() => ShowHUD();

    [ContextMenu("Auto-Hide: Hide")]
    void DebugHide() { if (_rootRT != null) Slide(false); }
#endif
}

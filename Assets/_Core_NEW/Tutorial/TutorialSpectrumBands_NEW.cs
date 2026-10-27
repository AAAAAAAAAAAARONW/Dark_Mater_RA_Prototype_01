using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UV, visible and IR marked on the spectrum bar, at the same edges the photon trail
/// uses — plus the marker that blinks on a newly cut line.
///
/// The HUD graph on its own is an unlabelled curve. Phase 3 asks the player to connect
/// three things on it: which part of the light their eyes can see, where a colour went
/// missing, and — once redshift starts — that the missing colour moved. None of that is
/// readable without the bands.
///
/// THE BANDS STAY STILL WHILE THE SPECTRUM MOVES, and that is the physics. The visible
/// range is set by human eyes; it does not shift. During redshift the curve and its
/// absorption lines slide redward underneath these fixed bands, which is the picture
/// that shows a line absorbed in the UV being carried into the visible.
///
/// Edges come from TutorialTrailBands_NEW, which reads them from the trail's own
/// spectrum, so the bar and the ribbon cannot disagree about where visible light starts.
/// They are applied every frame, so tuning the trail's band widths in Play mode moves
/// the bands with it.
///
/// NAMED IN FULL, AND POINTED OUT. LARC (26 Oct) asked for the three bands to be labelled
/// so a visitor can tell them apart: each label reads ULTRAVIOLET / VISIBLE / INFRARED where
/// the band is wide enough, falls back to UV / VIS / IR where it is not, and only then
/// hides. HighlightBands (D3) lights each band and its label in turn, so the first time the
/// bar is on screen the player is shown which part is which. While the bar is zoomed into
/// the UV, visible and IR are off its right end; offBarHint says so there. The trail
/// (TutorialTrailBands_NEW) reads HighlightOf, so the ribbon lights each band with the bar.
///
/// Flashes and fades run on the world clock, so a pause holds them.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("SPEC BANDS", "#33B3B3")]
public class TutorialSpectrumBands_NEW : MonoBehaviour
{
    [Header("Wiring")]
    [Tooltip("Where the band edges come from.")]
    [SerializeField] TutorialTrailBands_NEW trailBands;

    [Tooltip("Where the absorption line is, for the blinking marker.")]
    [SerializeField] TutorialSpectrum_NEW spectrum;

    [SerializeField] RectTransform uvBand;
    [SerializeField] RectTransform visibleBand;
    [SerializeField] RectTransform irBand;

    [Tooltip("UV band and its label, faded out when UV and IR are folded away.")]
    [SerializeField] CanvasGroup uvGroup;

    [SerializeField] CanvasGroup irGroup;

    [Tooltip("The visible band's tint, brightened for the flash.")]
    [SerializeField] Image visibleTint;

    [SerializeField] RectTransform lineMarker;
    [SerializeField] Image lineMarkerImage;

    [Tooltip("Optional. An arrow under the bar at the birth wavelength, where every line " +
             "is cut — the same green arrow the journey's LAF bar uses, so the reference " +
             "point is drawn the same way on both sides of the handoff. Only its x is set " +
             "here; its size, rotation and gap are its own.")]
    [SerializeField] RectTransform birthArrow;

    [Tooltip("Keep UV and IR marked the whole way through: D9's fold does nothing. A spectrum " +
             "plot always covers its full range.")]
    [SerializeField] bool alwaysOpen = true;

    [Header("Wavelength ticks")]
    [Tooltip("Where visible light starts and ends, as the axis of a real spectrum plot marks " +
             "it: a line and a wavelength, in place of UV / VISIBLE / IR names. The line and its " +
             "label are hidden while that wavelength is off the bar (the zoom into the UV).")]
    [SerializeField] Graphic visibleStartEdge;
    [SerializeField] TMP_Text visibleStartTick;
    [SerializeField] Graphic visibleEndEdge;
    [SerializeField] TMP_Text visibleEndTick;

    [Tooltip("A band's UV / VISIBLE / IR label is hidden while the band is narrower than " +
             "the label plus this many pixels. When the bar zooms into the UV the visible and " +
             "IR bands are squeezed against the right edge, and their labels landed on top of " +
             "each other.")]
    [SerializeField] float labelPadding = 6f;

    [Header("Band names")]
    [SerializeField] string uvName = "ULTRAVIOLET";
    [SerializeField] string uvShort = "UV";
    [SerializeField] string visibleName = "VISIBLE";
    [SerializeField] string visibleShort = "VIS";
    [SerializeField] string irName = "INFRARED";
    [SerializeField] string irShort = "IR";

    [Tooltip("Optional. Shown at the bar's right end while visible light is off the bar (the " +
             "zoom into the UV), so the player knows where the rest of the spectrum went.")]
    [SerializeField] TMP_Text offBarHint;

    [Header("Band highlight (HighlightBands, D3)")]
    [Tooltip("Seconds after HighlightBands before the first band lights: the bar is still arriving.")]
    [Min(0f)]
    [SerializeField] float highlightDelay = 0.6f;

    [Tooltip("Seconds each band is lit, UV then visible then IR.")]
    [Min(0.1f)]
    [SerializeField] float highlightSecondsEach = 1.4f;

    [Tooltip("A band's tint alpha at the top of its highlight.")]
    [Range(0f, 1f)]
    [SerializeField] float highlightTintAlpha = 0.32f;

    [Tooltip("A band's label at the top of its highlight, as a multiple of its size.")]
    [Range(1f, 2f)]
    [SerializeField] float highlightLabelScale = 1.3f;

    [Header("Look")]
    [Range(0f, 1f)]
    [SerializeField] float visibleTintAlpha = 0.06f;

    [Range(0f, 1f)]
    [SerializeField] float visibleFlashAlpha = 0.35f;

    [Header("Timing")]
    [SerializeField] float foldSeconds = 1.2f;

    [Min(1)]
    [SerializeField] int flashCount = 3;

    [SerializeField] float flashSeconds = 2.4f;

    [Min(1)]
    [SerializeField] int lineBlinkCount = 3;

    [SerializeField] float lineBlinkSeconds = 1.8f;

    float _open = 1f;
    float _openTarget = 1f;
    float _flashElapsed = -1f;
    float _blinkElapsed = -1f;
    float _highlightElapsed = -1f;
    float _hintAlpha;

    // Per band, UV / visible / IR: its tint, the tint's resting alpha, its label.
    readonly Image[] _tints = new Image[3];
    readonly float[] _tintRest = new float[3];
    readonly TMP_Text[] _labels = new TMP_Text[3];
    readonly Color[] _labelRest = new Color[3];
    bool _cached;

    // ── Public API ───────────────────────────────────────────────────────────

    public void UnfoldUVIR() { _openTarget = 1f; }

    public void FoldUVIR() { if (!alwaysOpen) _openTarget = 0f; }

    public void FlashVisible() { _flashElapsed = 0f; }

    /// <summary>Blink a marker over the line the spectrum last absorbed at.</summary>
    public void BlinkLine() { _blinkElapsed = 0f; }

    /// <summary>Light UV, visible and IR one after another, each with its name.</summary>
    public void HighlightBands() { _highlightElapsed = 0f; }

    public void ResetForAttract()
    {
        _open = _openTarget = 1f;
        _flashElapsed = -1f;
        _blinkElapsed = -1f;
        _highlightElapsed = -1f;
        _hintAlpha = 0f;

        Apply();
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        CacheBands();
        ResetForAttract();
    }

    void CacheBands()
    {
        RectTransform[] bands = { uvBand, visibleBand, irBand };
        for (int i = 0; i < 3; i++)
        {
            if (bands[i] == null) continue;

            // The visible band's tint is visibleTint, whose alpha the flash already drives.
            _tints[i] = i == 1 ? null : bands[i].GetComponent<Image>();
            if (_tints[i] != null) _tintRest[i] = _tints[i].color.a;

            Transform t = bands[i].Find("Label");
            _labels[i] = t != null ? t.GetComponent<TMP_Text>() : null;
            if (_labels[i] != null) _labelRest[i] = _labels[i].color;
        }
        _cached = true;
    }

    void Update()
    {
        float dt = TutorialClock_NEW.DeltaTime;

        _open = Mathf.MoveTowards(_open, _openTarget, foldSeconds > 0f ? dt / foldSeconds : 1f);

        if (_flashElapsed >= 0f)
        {
            _flashElapsed += dt;
            if (_flashElapsed > flashSeconds) _flashElapsed = -1f;
        }

        if (_blinkElapsed >= 0f)
        {
            _blinkElapsed += dt;
            if (_blinkElapsed > lineBlinkSeconds) _blinkElapsed = -1f;
        }

        if (_highlightElapsed >= 0f)
        {
            _highlightElapsed += dt;
            if (_highlightElapsed > highlightDelay + 3f * highlightSecondsEach) _highlightElapsed = -1f;
        }

        Apply();
        ApplyHint(dt);
    }

    void Apply()
    {
        float visStart = trailBands != null ? trailBands.VisibleStart : 0.15f;
        float visEnd = trailBands != null ? trailBands.VisibleEnd : 0.85f;

        // An external data source can put the bar on a different axis from the trail (the
        // tutorial's zoom); the bar's bands then follow the bar.
        float barStart, barEnd;
        if (spectrum != null && spectrum.TryGetBarBands(out barStart, out barEnd)) { visStart = barStart; visEnd = barEnd; }

        Span(uvBand, 0f, visStart);
        Span(visibleBand, visStart, visEnd);
        Span(irBand, visEnd, 1f);

        float barWidth = ((RectTransform)transform).rect.width;
        if (!_cached) CacheBands();
        FitLabel(0, uvName, uvShort, visStart * barWidth);
        FitLabel(1, visibleName, visibleShort, (visEnd - visStart) * barWidth);
        FitLabel(2, irName, irShort, (1f - visEnd) * barWidth);
        ApplyHighlight();

        PlaceTicks(visStart, visEnd, barWidth);

        if (uvGroup != null) uvGroup.alpha = _open;
        if (irGroup != null) irGroup.alpha = _open;

        if (visibleTint != null)
        {
            Color c = visibleTint.color;
            c.a = Mathf.Lerp(visibleTintAlpha, visibleFlashAlpha,
                             Pulse(_flashElapsed, flashSeconds, flashCount));
            c.a = Mathf.Max(c.a, Mathf.Lerp(visibleTintAlpha, highlightTintAlpha, HighlightOf(1)));
            visibleTint.color = c;
        }

        if (lineMarker != null && spectrum != null)
        {
            float x = Mathf.Clamp01(spectrum.RestFramePosition);
            lineMarker.anchorMin = new Vector2(x, 0f);
            lineMarker.anchorMax = new Vector2(x, 1f);
        }

        if (birthArrow != null && spectrum != null)
        {
            float x = Mathf.Clamp01(spectrum.RestFramePosition);
            birthArrow.anchorMin = new Vector2(x, 0f);
            birthArrow.anchorMax = new Vector2(x, 0f);
        }

        if (lineMarkerImage != null)
        {
            Color c = lineMarkerImage.color;
            c.a = Pulse(_blinkElapsed, lineBlinkSeconds, lineBlinkCount) * 0.9f;
            lineMarkerImage.color = c;
        }
    }

    /// <summary>
    /// The 4000 and 7000 Å edges and their labels. A position pinned at the bar's end means
    /// the wavelength is off the bar (the zoom), so that tick goes; and when the two crowd
    /// together the 7000 label gives way rather than printing over the 4000.
    /// </summary>
    void PlaceTicks(float visStart, float visEnd, float barWidth)
    {
        bool startOn = visStart > 0.001f && visStart < 0.999f;
        bool endOn = visEnd > 0.001f && visEnd < 0.999f;

        Show(visibleStartEdge, startOn);
        Show(visibleStartTick, startOn);
        Show(visibleEndEdge, endOn);

        bool endLabelFits = !startOn || visibleStartTick == null ||
                            (visEnd - visStart) * barWidth >= visibleStartTick.preferredWidth + labelPadding;
        Show(visibleEndTick, endOn && endLabelFits);
    }

    static void Show(Behaviour b, bool on)
    {
        if (b != null && b.enabled != on) b.enabled = on;
    }

    /// <summary>
    /// A band's label: its full name where the band is wide enough, its short name where
    /// only that fits, nothing where neither does.
    /// </summary>
    void FitLabel(int band, string full, string brief, float width)
    {
        TMP_Text label = _labels[band];
        if (label == null) return;

        string want = null;
        if (!string.IsNullOrEmpty(full) && width >= label.GetPreferredValues(full).x + labelPadding) want = full;
        else if (!string.IsNullOrEmpty(brief) && width >= label.GetPreferredValues(brief).x + labelPadding) want = brief;

        bool on = want != null;
        if (on && label.text != want) label.text = want;
        if (label.enabled != on) label.enabled = on;
    }

    /// <summary>
    /// 0 to 1 to 0: how lit band i (0 UV, 1 visible, 2 IR) is in HighlightBands' run. Public
    /// so the trail lights the same band at the same moment off this one clock.
    /// </summary>
    public float HighlightOf(int band)
    {
        if (_highlightElapsed < 0f) return 0f;
        float t = (_highlightElapsed - highlightDelay - band * highlightSecondsEach) / highlightSecondsEach;
        if (t <= 0f || t >= 1f) return 0f;
        return Mathf.Sin(t * Mathf.PI);
    }

    void ApplyHighlight()
    {
        for (int i = 0; i < 3; i++)
        {
            float h = HighlightOf(i);

            if (_tints[i] != null)
            {
                Color c = _tints[i].color;
                c.a = Mathf.Lerp(_tintRest[i], highlightTintAlpha, h);
                _tints[i].color = c;
            }

            if (_labels[i] != null)
            {
                _labels[i].color = Color.Lerp(_labelRest[i], Color.white, h);
                _labels[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, highlightLabelScale, h);
            }
        }
    }

    /// <summary>The note at the bar's right end while visible light is off it.</summary>
    void ApplyHint(float dt)
    {
        if (offBarHint == null || spectrum == null) return;

        float barStart, barEnd;
        bool offBar = spectrum.TryGetBarBands(out barStart, out barEnd) && barStart >= 0.999f;
        _hintAlpha = Mathf.MoveTowards(_hintAlpha, offBar ? 1f : 0f, dt / 0.6f);

        Color c = offBarHint.color;
        c.a = _hintAlpha * 0.7f;
        offBarHint.color = c;
        if (offBarHint.enabled != _hintAlpha > 0f) offBarHint.enabled = _hintAlpha > 0f;
    }

    static void Span(RectTransform rect, float from, float to)
    {
        if (rect == null) return;

        rect.anchorMin = new Vector2(from, 0f);
        rect.anchorMax = new Vector2(to, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static float Pulse(float elapsed, float seconds, int count)
    {
        if (elapsed < 0f || seconds <= 0f) return 0f;

        float t = Mathf.Clamp01(elapsed / seconds);
        return 0.5f - 0.5f * Mathf.Cos(t * count * Mathf.PI * 2f);
    }
}

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

    // ── Public API ───────────────────────────────────────────────────────────

    public void UnfoldUVIR() { _openTarget = 1f; }

    public void FoldUVIR() { _openTarget = 0f; }

    public void FlashVisible() { _flashElapsed = 0f; }

    /// <summary>Blink a marker over the line the spectrum last absorbed at.</summary>
    public void BlinkLine() { _blinkElapsed = 0f; }

    public void ResetForAttract()
    {
        _open = _openTarget = 1f;
        _flashElapsed = -1f;
        _blinkElapsed = -1f;

        Apply();
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        ResetForAttract();
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

        Apply();
    }

    void Apply()
    {
        float visStart = trailBands != null ? trailBands.VisibleStart : 0.15f;
        float visEnd = trailBands != null ? trailBands.VisibleEnd : 0.85f;

        Span(uvBand, 0f, visStart);
        Span(visibleBand, visStart, visEnd);
        Span(irBand, visEnd, 1f);

        if (uvGroup != null) uvGroup.alpha = _open;
        if (irGroup != null) irGroup.alpha = _open;

        if (visibleTint != null)
        {
            Color c = visibleTint.color;
            c.a = Mathf.Lerp(visibleTintAlpha, visibleFlashAlpha,
                             Pulse(_flashElapsed, flashSeconds, flashCount));
            visibleTint.color = c;
        }

        if (lineMarker != null && spectrum != null)
        {
            float x = Mathf.Clamp01(spectrum.RestFramePosition);
            lineMarker.anchorMin = new Vector2(x, 0f);
            lineMarker.anchorMax = new Vector2(x, 1f);
        }

        if (lineMarkerImage != null)
        {
            Color c = lineMarkerImage.color;
            c.a = Pulse(_blinkElapsed, lineBlinkSeconds, lineBlinkCount) * 0.9f;
            lineMarkerImage.color = c;
        }
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

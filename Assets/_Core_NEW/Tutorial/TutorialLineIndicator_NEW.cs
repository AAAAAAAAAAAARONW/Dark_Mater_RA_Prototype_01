using UnityEngine;

/// <summary>
/// Two arrows pointing at the first absorption line — one under the spectrum bar, one
/// on the light's trail — that follow it as it redshifts.
///
/// Appear at D5, the moment of contact. Leave when the redshift frame ends.
///
/// WHY TWO. The line is drawn in two places at once: on the bar, as a notch in the
/// curve, and on the trail, as a dark stripe along the ribbon at the same wavelength.
/// D5 has to say these are the same absorption. An arrow at each, moving together
/// through D7, says it without text — and keeps saying it while the line slides red,
/// which is the moment a player is most likely to lose track of which mark is theirs.
///
/// AND A LEADER BETWEEN THEM. Two arrows in different parts of the frame, both moving,
/// are two things happening; a line joining them is one thing seen twice, which is the
/// whole claim. It also survives a player who looked away at the moment of contact and
/// arrives afterwards with two marks already on screen and no reason to connect them.
/// Optional — leave leaderLine unassigned and this is exactly the two arrows it was.
///
/// WHERE THE LINE IS comes from TutorialSpectrum_NEW.TrackedLinePosition, which follows
/// the drift on the same clock the absorption field uses. On the bar that is a position
/// along the axis. On the trail it is a position across the ribbon's width, mapped
/// through TutorialTrailBands_NEW.WidthFraction so it stays right while UV and IR are
/// unfolded or folded, then projected from the world onto the screen.
///
/// THE TRAIL ARROW'S SIDE MAY NEED FLIPPING. A TrailRenderer facing the camera builds
/// its width from the direction of the trail and the direction to the camera, and which
/// edge Unity calls uv.y = 0 is not something that can be confirmed without running it.
/// If the arrow sits over the wrong edge of the ribbon — pointing at IR when the line is
/// in the visible — tick flipAcross. One checkbox, no code.
///
/// Fades run on real time, not the world clock: these are interface, and a pause should
/// not leave an arrow half faded in.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("LINE ARROW", "#FFE8A0")]
public class TutorialLineIndicator_NEW : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] TutorialSpectrum_NEW spectrum;
    [SerializeField] TutorialTrailBands_NEW trailBands;
    [SerializeField] TrailRenderer trail;
    [SerializeField] TutorialTravel_NEW travel;
    [SerializeField] Camera viewCamera;

    [Tooltip("The canvas the trail arrow is placed on.")]
    [SerializeField] RectTransform canvasRect;

    [Header("Bar arrow")]
    [Tooltip("Child of the spectrum bar's bands, hanging below its bottom edge and " +
             "pointing up at the line.")]
    [SerializeField] RectTransform barArrow;

    [SerializeField] CanvasGroup barArrowGroup;

    [Tooltip("Pixels between the edge of the bar and the tip of the arrow.\n\n" +
             "Above the bar this has to clear the UV / VISIBLE / IR labels, which hang " +
             "ten pixels over the top edge and are eighteen tall. Below the bar six was " +
             "enough, because there was nothing under it.")]
    [SerializeField] float barArrowGap = 22f;

    [Tooltip("Hang the arrow ABOVE the bar, pointing down at the line, instead of below " +
             "it pointing up.\n\n" +
             "Above, since the bar docks at the bottom of the frame: the light is up and " +
             "to the middle from there, so an arrow on the underside would put the tip on " +
             "the far side of the bar from everything it relates to, and the leader line " +
             "would cross the graph it is pointing into. Below the bar is also where the " +
             "hint line lives.\n\n" +
             "Untick it if the bar is ever moved back to the top edge.")]
    [SerializeField] bool barArrowAbove = true;

    [Header("Trail arrow")]
    [SerializeField] RectTransform trailArrow;
    [SerializeField] CanvasGroup trailArrowGroup;

    [Tooltip("Metres behind the light where the arrow points into the ribbon. A little " +
             "way back, where the ribbon is already full width.")]
    [SerializeField] float pointBehindLight = 1.5f;

    [Tooltip("Pixels above that point where the arrow sits, pointing down at it.")]
    [SerializeField] float trailArrowGap = 40f;

    [Tooltip("Tick if the trail arrow points at the opposite edge of the ribbon from the " +
             "line. See the class summary.")]
    [SerializeField] bool flipAcross = false;

    [Header("Leader line")]
    [Tooltip("A thin line drawn from one arrow to the other. Optional — with none " +
             "assigned this component is exactly what it was, two arrows.\n\n" +
             "IT IS WHAT MAKES THE PAIRING A STATEMENT RATHER THAN A COINCIDENCE. Two " +
             "arrows in different corners of the screen, both moving, are two things " +
             "happening; joined, they are one thing seen twice. GDD §3 item 4 — 'the " +
             "colour you lost is that black line' — is that sentence and nothing else, " +
             "and it has to survive being read by somebody who looked away at the moment " +
             "of contact.\n\n" +
             "Anchored at its left end and stretched, so one Image with a pivot at " +
             "(0, 0.5) draws any line without a second object.")]
    [SerializeField] RectTransform leaderLine;

    [SerializeField] CanvasGroup leaderLineGroup;

    [Tooltip("Thickness in pixels. Thin: this is a reference mark, not a third thing to " +
             "look at.")]
    [SerializeField] float leaderThickness = 2f;

    [Header("Timing")]
    [SerializeField] float fadeSeconds = 0.35f;

    float _alpha;
    float _alphaTarget;

    /// <summary>Canvas-local position of each arrow's tip, from this frame's placement.</summary>
    Vector2 _barTip;
    Vector2 _trailTip;

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>D5's onEnter.</summary>
    public void Show() { _alphaTarget = 1f; }

    /// <summary>The redshift frame's onSatisfied.</summary>
    public void Hide() { _alphaTarget = 0f; }

    public void ResetForAttract()
    {
        _alpha = _alphaTarget = 0f;
        ApplyAlpha(false, false, false);
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (viewCamera == null) viewCamera = Camera.main;
        ResetForAttract();
    }

    /// <summary>
    /// LateUpdate, so the camera rig has already written this frame's pose and the trail
    /// arrow lands on the ribbon as the frame will actually show it.
    /// </summary>
    void LateUpdate()
    {
        _alpha = Mathf.MoveTowards(_alpha, _alphaTarget,
                                   fadeSeconds > 0f ? Time.unscaledDeltaTime / fadeSeconds : 1f);

        bool tracking = spectrum != null && spectrum.HasTrackedLine && _alpha > 0.001f;

        bool barOk = tracking && PlaceBarArrow();
        bool trailOk = tracking && PlaceTrailArrow();

        // Only when both ends are real. A leader with one end at a stale position would
        // be drawing a connection between the line and nothing.
        bool leaderOk = barOk && trailOk && PlaceLeader();

        ApplyAlpha(barOk, trailOk, leaderOk);
    }

    bool PlaceBarArrow()
    {
        if (barArrow == null) return false;

        float x = Mathf.Clamp01(spectrum.TrackedLinePosition);

        // The edge it hangs off, and which way it then has to point. The pivot is the
        // sprite's apex either way, so the gap is always measured to the tip.
        float edge = barArrowAbove ? 1f : 0f;

        barArrow.anchorMin = new Vector2(x, edge);
        barArrow.anchorMax = new Vector2(x, edge);
        // Top centre in both cases: that is where the sprite's apex is, and turning it
        // half round about its own apex leaves the apex exactly where it was.
        barArrow.pivot = new Vector2(0.5f, 1f);
        barArrow.anchoredPosition = new Vector2(0f, barArrowAbove ? barArrowGap : -barArrowGap);
        barArrow.localEulerAngles = new Vector3(0f, 0f, barArrowAbove ? 180f : 0f);

        // The pivot IS the tip — top centre, with the arrow pointing up at the bar from
        // below it — so the transform's own position is the point the leader joins.
        if (canvasRect != null)
            _barTip = canvasRect.InverseTransformPoint(barArrow.position);

        return true;
    }

    bool PlaceTrailArrow()
    {
        if (trailArrow == null || trail == null || trailBands == null || viewCamera == null ||
            canvasRect == null || !trail.gameObject.activeInHierarchy)
            return false;

        Vector3 behind = travel != null && travel.Direction.sqrMagnitude > 0.0001f
            ? -travel.Direction.normalized
            : -trail.transform.forward;

        Vector3 point = trail.transform.position + behind * pointBehindLight;

        // The ribbon faces the camera, so its width runs across both the trail and the
        // line of sight.
        Vector3 toCamera = viewCamera.transform.position - point;
        Vector3 across = Vector3.Cross(behind, toCamera).normalized;
        if (across.sqrMagnitude < 0.0001f) return false;
        if (flipAcross) across = -across;

        float width = trail.widthMultiplier * trail.widthCurve.Evaluate(0f);
        float u = trailBands.WidthFraction(spectrum.TrackedLinePosition);

        point += across * (u - 0.5f) * width;

        Vector3 screen = viewCamera.WorldToScreenPoint(point);
        if (screen.z <= 0f) return false;

        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out local))
            return false;

        trailArrow.anchorMin = new Vector2(0.5f, 0.5f);
        trailArrow.anchorMax = new Vector2(0.5f, 0.5f);
        // The sprite's apex is its top edge. Pivoting there and turning it half round
        // leaves the apex in place pointing down, with the body above it — so the gap is
        // measured to the tip.
        trailArrow.pivot = new Vector2(0.5f, 1f);
        trailArrow.anchoredPosition = local + new Vector2(0f, trailArrowGap);
        trailArrow.localEulerAngles = new Vector3(0f, 0f, 180f);

        // Same as the bar arrow: the pivot is the apex, so the anchored position is the
        // tip. This one is measured in the canvas's own rect, which is where the leader
        // is drawn too.
        _trailTip = trailArrow.anchoredPosition;

        return true;
    }

    /// <summary>
    /// Stretch the leader between the two tips.
    ///
    /// One RectTransform pivoted at its left edge: anchored at the first tip, as long as
    /// the gap, turned to face the second. No second object, no line renderer, and it
    /// costs nothing to have none — leaderLine unassigned simply returns false.
    ///
    /// Recomputed every frame rather than on a change, because both ends move
    /// independently and continuously: the bar end slides with the redshift and the
    /// trail end moves with the camera, which the player is free to swing about the
    /// whole time. There is no moment at which this could be cached.
    /// </summary>
    bool PlaceLeader()
    {
        if (leaderLine == null) return false;

        Vector2 span = _barTip - _trailTip;
        float length = span.magnitude;

        // The two tips on top of each other. Nothing to draw, and atan2 of nothing is
        // a direction that would flicker.
        if (length < 1f) return false;

        leaderLine.anchorMin = new Vector2(0.5f, 0.5f);
        leaderLine.anchorMax = new Vector2(0.5f, 0.5f);
        leaderLine.pivot = new Vector2(0f, 0.5f);
        leaderLine.anchoredPosition = _trailTip;
        leaderLine.sizeDelta = new Vector2(length, leaderThickness);
        leaderLine.localEulerAngles = new Vector3(0f, 0f,
                                                  Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg);

        return true;
    }

    void ApplyAlpha(bool barVisible, bool trailVisible, bool leaderVisible)
    {
        if (barArrowGroup != null) barArrowGroup.alpha = barVisible ? _alpha : 0f;
        if (trailArrowGroup != null) trailArrowGroup.alpha = trailVisible ? _alpha : 0f;

        // Fainter than the arrows it joins. The arrows say where; this only says that
        // the two are the same, and a bright line across the middle of the frame would
        // be competing with the thing it is pointing out.
        if (leaderLineGroup != null) leaderLineGroup.alpha = leaderVisible ? _alpha * 0.55f : 0f;
    }
}

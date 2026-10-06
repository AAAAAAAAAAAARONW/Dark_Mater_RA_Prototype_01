using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Where each absorption line WAS cut, and where it is NOW. The picture that makes
/// redshift visible.
///
/// THE PROBLEM THIS SOLVES. SpectrumHUD_NEW slides the continuum and the absorption
/// lines together, which is the physically honest thing to draw and is completely
/// invisible: everything on the bar moves at the same rate, so nothing appears to move
/// at all. The tutorial dodged this by holding the curve still and letting the lines
/// drift through it, which reads beautifully at bench scale and is wrong over a journey
/// — a static curve says the wavelength scale travels with the player.
///
/// So the reference is neither the curve nor the lines. It is a row of fixed ticks at
/// the wavelengths where hydrogen cut each line in the first place. Those never move.
/// Everything else slides off them, and the GAP is the redshift — a distance you can
/// measure by eye from the back of a room, where a slow drift is not.
///
/// WHY THE COMB SPREADS. Each mark carries a travelFraction: how much of the journey it
/// has been riding. A line cut at the quasar has been stretched the whole way; a line cut
/// ten minutes ago has barely moved. So the marks do not translate as a block, they
/// SPREAD — the oldest furthest from home, the newest still sitting on its anchor. That
/// spreading is the same gesture the cosmic web makes as its filaments pull apart, and
/// the two are meant to be on screen together. It is the whole reason this demonstration
/// moved out of the tutorial and into the journey: there is no web in the tutorial to
/// pull apart.
///
/// THIS IS THE FAKE VERSION, ON PURPOSE. The marks below are authored, not derived from
/// the clouds the player actually crossed, and the offset is a display coordinate rather
/// than a redshift computed from distance. Both are recorded, with what it would take to
/// fix them, in Spectrum/KNOWN_ISSUES_FOREST.md. The shapes on screen are the shapes the
/// real version will make, which is what this stage is for; the numbers are not the real
/// numbers, and no caption should claim they are.
///
/// Drawn as its own Graphic, stretched over the same rect as the spectrum. It does not
/// touch the spectrum's mesh — that mesh is rebuilt from scratch every frame and anything
/// written into it would be gone before it was seen.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("REDSHIFT", "#E2506A")]
public class RedshiftMarks_NEW : MaskableGraphic
{
    [Serializable]
    public class Mark
    {
        [Tooltip("Where hydrogen cut this line, as a fraction across the bar. This is the " +
                 "anchor, and it never moves.")]
        [Range(0f, 1f)]
        public float restT = 0.2f;

        [Tooltip("How much of the journey this line has been riding, 0 to 1.\n\n" +
                 "1 means cut at the quasar and stretched the whole way. 0 means cut just " +
                 "now, still sitting on its anchor. This is what makes the row SPREAD " +
                 "instead of sliding as a block, and the spread is the point.")]
        [Range(0f, 1f)]
        public float travelFraction = 1f;

        [Tooltip("The one line the sequence points at — the first one, cut in the " +
                 "tutorial. Drawn brighter, and the only one that gets a connector back " +
                 "to its anchor until the rest are revealed.")]
        public bool isPrimary = false;

        [Tooltip("The redshift of the hydrogen cloud that cut this line. Only read when a " +
                 "baked journey spectrum is driving the marks (SpectrumData); 0 means " +
                 "\"not set\" and the mark falls back to restT and travelFraction.\n\n" +
                 "With real data every line is born at the same place — 1216 Å, where " +
                 "all hydrogen absorbs — so restT stops mattering and this is the only " +
                 "number that says where the line is now. Use the source's context menu " +
                 "'Fill marks from baked absorbers' rather than typing these.")]
        [Min(0f)]
        public float zAbs = 0f;
    }

    [Header("Source")]
    [Tooltip("The spectrum this rides. Found in the scene if left empty.\n\n" +
             "Read, never written: the marks follow the HUD's redshift offset so the two " +
             "cannot disagree. A second integration of the same drift would look right " +
             "for about a minute.")]
    [SerializeField] SpectrumHUD_NEW spectrum;

    [Header("Marks")]
    [Tooltip("Authored, one per line worth pointing at. DO NOT add one per forest line: " +
             "the forest is hundreds of lines and a tick under each one is a grey band, " +
             "not a reading. Three to five is what a row of gaps stays legible at.")]
    [SerializeField] Mark[] marks = new Mark[0];

    [Header("Alignment")]
    [Tooltip("Draw on the SPECTRUM CURVE'S rectangle, not this object's own.\n\n" +
             "The marks and the curve live on different UI objects, and there is no reason " +
             "their rectangles agree — in Gameplay_Scene_Ana the marks sat on an 800×90 box " +
             "wider than the curve's, so the birth tick landed left of where the forest " +
             "starts. Aligning to the curve's rect (and to its drawing band, see " +
             "LineGraphRenderer_NEW.GraphHeightPercent) makes a mark sit exactly on the " +
             "wavelength it names, whatever the layout.")]
    [SerializeField] bool alignToCurve = true;

    [Tooltip("The curve's RectTransform. Empty = the object the SpectrumHUD_NEW is on.")]
    [SerializeField] RectTransform alignTo;

    public enum AnchorStyle
    {
        /// <summary>A small triangle under the bar, pointing up at the birth wavelength.</summary>
        Triangle = 0,

        /// <summary>A short vertical tick inside the bar. The original look.</summary>
        Line = 1
    }

    [Header("Anchor")]
    [Tooltip("How the birth point (1216 Å) is marked. TRIANGLE sits under the bar, pointing up " +
             "at it — it marks a place on the axis without adding one more line to a bar " +
             "whose whole content is lines.")]
    [SerializeField] AnchorStyle anchorStyle = AnchorStyle.Triangle;

    [Tooltip("Triangle width, pixels.")]
    [Range(4f, 40f)]
    [SerializeField] float anchorTriangleWidth = 12f;

    [Tooltip("Triangle height, pixels.")]
    [Range(3f, 40f)]
    [SerializeField] float anchorTriangleHeight = 9f;

    [Tooltip("How far the back edge is cut in towards the tip, as a fraction of the height. " +
             "0 is a plain triangle; about 0.3 is the arrowhead in the LAF art.")]
    [Range(0f, 0.8f)]
    [SerializeField] float anchorTriangleNotch = 0f;

    [Tooltip("Gap between the bottom of the curve band and the triangle's tip, pixels.")]
    [Range(0f, 30f)]
    [SerializeField] float anchorTriangleGap = 3f;

    [Tooltip("Triangle colour. Separate from Anchor Color (the line style's), which is kept " +
             "faint on purpose; a small solid shape needs more opacity to read.")]
    [SerializeField] Color anchorTriangleColor = new Color(0.78f, 0.84f, 1f, 0.9f);

    [Header("Geometry")]
    [Tooltip("Tick width in pixels.")]
    [Range(1f, 8f)]
    [SerializeField] float tickWidth = 2f;

    [Tooltip("Anchor tick height, as a fraction of the rect. Anchors are shorter than " +
             "live marks so the two read as different kinds of thing rather than as a " +
             "row of identical ticks that happens to be doubled.")]
    [Range(0.05f, 1f)]
    [SerializeField] float anchorHeight = 0.35f;

    [Tooltip("Live tick height, as a fraction of the rect.")]
    [Range(0.05f, 1f)]
    [SerializeField] float liveHeight = 0.6f;

    [Tooltip("Height of the connector bar joining an anchor to its live mark, in pixels.")]
    [Range(1f, 12f)]
    [SerializeField] float connectorThickness = 3f;

    [Tooltip("Where the connector sits vertically, as a fraction of the rect.")]
    [Range(0f, 1f)]
    [SerializeField] float connectorY = 0.18f;

    [Header("Colour")]
    [Tooltip("Anchors: where the line was cut. Dim — they are the graph paper, not the " +
             "reading.")]
    [SerializeField] Color anchorColor = new Color(0.62f, 0.70f, 0.85f, 0.45f);

    [Tooltip("Live marks: where the line is now.")]
    [SerializeField] Color liveColor = new Color(0.95f, 0.97f, 1f, 0.95f);

    [Tooltip("The primary line — the first one, the one the voice-over is talking about.")]
    [SerializeField] Color primaryColor = new Color(1f, 0.45f, 0.42f, 1f);

    [Tooltip("The gap. Given its own colour because it is the one shape the audience has " +
             "to come away with, and it has to survive being seen from the back of a " +
             "room on a curved wall.")]
    [SerializeField] Color connectorColor = new Color(1f, 0.55f, 0.35f, 0.85f);

    [Header("Reveal")]
    [Tooltip("Seconds for a fade in or out.")]
    [Range(0.05f, 4f)]
    [SerializeField] float fadeSeconds = 0.8f;

    [Tooltip("Seconds for the spread animation to ease to a new value.")]
    [Range(0.05f, 20f)]
    [SerializeField] float spreadSeconds = 6f;

    [Tooltip("Shape of the spread animation. The default eases out, so the row opens " +
             "quickly and settles, rather than arriving at its target and stopping dead.")]
    [SerializeField] AnimationCurve spreadCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Editor preview")]
    [Tooltip("Draw the marks in the Scene and Game views while NOT playing, as if the " +
             "sequence had run.\n\n" +
             "WHY THIS IS NOT A LUXURY. Every element here starts invisible and is brought " +
             "on by a named step, which is the right rule and made this component " +
             "impossible to lay out: the steps only fire on entering the second cosmic web, " +
             "minutes into a run, so positioning a tick meant playing the journey to get " +
             "one look at it. Nothing was on screen in the editor either, so it looked " +
             "broken rather than unstarted.\n\n" +
             "Preview has no effect once playing — the sequence owns the alphas then.")]
    [SerializeField] bool previewInEditor = true;

    [Tooltip("The redshift offset to pretend has accumulated, while previewing. This is " +
             "what moves the live marks off their anchors so the gap can be judged.\n\n" +
             "0.45 is the whole journey's budget in RedshiftBudget_NEW, so this previews " +
             "the picture at ARRIVAL — the widest the gaps ever get, which is the case " +
             "worth laying out for. A mark that fits at 0.45 fits everywhere.")]
    [Range(0f, 1f)]
    [SerializeField] float previewOffset = 0.45f;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    // Reveal is three independent fades rather than one state machine: the sequence turns
    // them on in order (anchors, then the primary's connector, then the rest) and a state
    // machine would make "anchors visible AND nothing else" a state somebody has to name.
    float _anchorsAlpha, _anchorsTarget;
    float _primaryAlpha, _primaryTarget;
    float _othersAlpha, _othersTarget;

    float _spread = 1f;
    float _spreadFrom = 1f;
    float _spreadTo = 1f;
    float _spreadElapsed = -1f;

    float _lastOffset = float.NaN;

    // Physical mode: set from outside by the baked-spectrum source. See SetPhysicalMapping.
    Func<float, float> _physicalLiveT;
    float _physicalBirthT = -1f;
    Mark[] _physicalMarks;
    Action<float> _physicalReplay;

    /// <summary>
    /// The marks being drawn: the supplier's (from real data) while physical mode is on and it
    /// has provided some, otherwise the serialized list (the procedural fallback and the
    /// editor preview).
    /// </summary>
    Mark[] ActiveMarks
    {
        get { return _physicalLiveT != null && _physicalMarks != null ? _physicalMarks : marks; }
    }

    readonly List<Mark> _sorted = new List<Mark>();

    // -- Physical mode --------------------------------------------------------

    /// <summary>
    /// Position marks from real absorber redshifts instead of the fake offset.
    ///
    /// A HOOK, NOT A DEPENDENCY: the supplier (SpectrumData's baked source) calls this;
    /// this class never names it.
    ///
    /// birthT is where every line is born — 1216 Å on the bar — and becomes the single
    /// anchor for every mark, because physically all hydrogen absorbs at the same local
    /// wavelength. The "row of anchors, one per line" in the fake version was wrong on
    /// exactly this point and is kept only as the fallback.
    ///
    /// zAbsToLiveT maps a cloud's redshift to where its line sits on the bar now. It is
    /// called only for marks with zAbs > 0; the others keep the fake behaviour.
    /// </summary>
    public void SetPhysicalMapping(float birthT, Func<float, float> zAbsToLiveT)
    {
        _physicalBirthT = birthT;
        _physicalLiveT = zAbsToLiveT;
        SetVerticesDirty();
    }

    /// <summary>
    /// Which lines to mark, from real data: one cloud redshift per mark, [0] the primary (the
    /// first line the light acquired). Replaces the serialized list while physical mode is on,
    /// so the marks can never disagree with the data behind them — the serialized list stays
    /// untouched for the fallback and the editor preview.
    /// </summary>
    public void SetPhysicalMarks(float[] zAbs)
    {
        if (zAbs == null || zAbs.Length == 0)
        {
            _physicalMarks = null;
        }
        else
        {
            _physicalMarks = new Mark[zAbs.Length];
            for (int i = 0; i < zAbs.Length; i++)
                _physicalMarks[i] = new Mark { zAbs = zAbs[i], isPrimary = i == 0, restT = 0f, travelFraction = 1f };
        }

        SetVerticesDirty();
    }

    /// <summary>
    /// What Spread() does in physical mode. The fake version multiplied every line's distance
    /// from its anchor — an invented gesture. With real data the honest gesture is a replay:
    /// fast-forward from the quasar to now, so every line is seen being born at the anchor
    /// and stretched to where it really is. The supplier implements it; this class only
    /// forwards to it, with spreadSeconds as the duration.
    /// </summary>
    public void SetPhysicalReplay(Action<float> replay)
    {
        _physicalReplay = replay;
    }

    /// <summary>Back to the fake offset. The switch in SpectrumData calls this when turned off.</summary>
    public void ClearPhysicalMapping()
    {
        _physicalLiveT = null;
        _physicalBirthT = -1f;
        _physicalMarks = null;
        _physicalReplay = null;
        SetVerticesDirty();
    }

    /// <summary>The supplier's "now" moved; rebuild from the mapping.</summary>
    public void MarkPhysicalDirty()
    {
        if (_physicalLiveT != null) SetVerticesDirty();
    }

    /// <summary>The marks, for the supplier to fill zAbs from its catalogue. Editor use.</summary>
    public Mark[] Marks { get { return marks; } set { marks = value; } }

    // -- Public API, called by JourneySequence_NEW steps -----------------------

    /// <summary>
    /// F2. Bring up the row of anchors — where every line was cut. Nothing else yet: the
    /// audience needs a moment to read a row of fixed ticks as a scale before anything
    /// starts moving against it.
    /// </summary>
    public void ShowAnchors()
    {
        _anchorsTarget = 1f;
        if (debugLog) Debug.Log("[RedshiftMarks_NEW] Anchors in.", this);
    }

    /// <summary>
    /// F3. Light the first line and draw the gap back to its anchor. One line, one gap —
    /// the whole idea, before it is repeated across a row.
    /// </summary>
    public void HighlightPrimary()
    {
        _anchorsTarget = 1f;
        _primaryTarget = 1f;
        if (debugLog) Debug.Log("[RedshiftMarks_NEW] Primary line highlighted.", this);
    }

    /// <summary>
    /// F5. The rest of the row. Now the gaps differ from one mark to the next, which is
    /// the reading: each cloud sat at a different distance, so space stretched each line
    /// by a different amount.
    /// </summary>
    public void ShowAll()
    {
        _anchorsTarget = 1f;
        _primaryTarget = 1f;
        _othersTarget = 1f;
        if (debugLog) Debug.Log("[RedshiftMarks_NEW] Full row.", this);
    }

    /// <summary>Fade everything out. The sequence ends on this.</summary>
    public void Hide()
    {
        _anchorsTarget = 0f;
        _primaryTarget = 0f;
        _othersTarget = 0f;
    }

    /// <summary>
    /// F4. Open the row.
    ///
    /// Multiplies every mark's displacement, so the comb spreads away from its anchors
    /// over spreadSeconds. This is a DEMONSTRATION GESTURE, not physics: it shows the
    /// shape of what thirteen billion years does, at a speed a person standing in a room
    /// can watch. The honest slow version is the drift that has been running the whole
    /// journey underneath it, and it stays running.
    ///
    /// Drive the cosmic web's own expansion from the same step, with the same duration.
    /// Two things opening together is the claim being made; either one alone is decoration.
    ///
    /// WITH REAL DATA (physical mode) this does not multiply anything: it asks the supplier
    /// to replay the light's history over spreadSeconds — see SetPhysicalReplay. The
    /// multiplier is ignored. The existing JourneySequence wiring needs no change.
    /// </summary>
    public void Spread(float multiplier)
    {
        if (_physicalLiveT != null && _physicalReplay != null)
        {
            _physicalReplay(spreadSeconds);
            return;
        }

        _spreadFrom = _spread;
        _spreadTo = Mathf.Max(0f, multiplier);
        _spreadElapsed = 0f;

        if (debugLog)
            Debug.Log("[RedshiftMarks_NEW] Spread " + _spreadFrom.ToString("0.00") + " -> " +
                      _spreadTo.ToString("0.00") + " over " + spreadSeconds + "s.", this);
    }

    /// <summary>Put the spread back to 1 with no animation. For a restart.</summary>
    public void ResetSpread()
    {
        _spread = 1f;
        _spreadFrom = 1f;
        _spreadTo = 1f;
        _spreadElapsed = -1f;
    }

    /// <summary>
    /// The current gap for the primary mark, in bar widths. For a readout, and for
    /// anything that wants to state the redshift as a number.
    ///
    /// Zero until a primary mark exists. NOT a physical redshift — see the class comment
    /// and KNOWN_ISSUES_FOREST.md.
    /// </summary>
    public float PrimaryGap()
    {
        Mark[] list = ActiveMarks;
        if (list == null) return 0f;

        for (int i = 0; i < list.Length; i++)
        {
            if (list[i] != null && list[i].isPrimary)
                return LiveT(list[i]) - RestT(list[i]);
        }

        return 0f;
    }

    // -- Lifecycle ------------------------------------------------------------

    protected override void Awake()
    {
        base.Awake();

        if (spectrum == null) spectrum = FindObjectOfType<SpectrumHUD_NEW>();

        if (spectrum == null && Application.isPlaying)
        {
            Debug.LogWarning("[RedshiftMarks_NEW] No SpectrumHUD_NEW found. The marks will " +
                             "sit on their anchors and never move.", this);
        }

        // Starts invisible. Every element here is brought on by a named step, the same
        // rule the tutorial HUD follows — nothing appears because it happened to default
        // to on.
        _anchorsAlpha = _anchorsTarget = 0f;
        _primaryAlpha = _primaryTarget = 0f;
        _othersAlpha = _othersTarget = 0f;
    }

    void Update()
    {
        bool dirty = false;
        float dt = Time.unscaledDeltaTime;

        dirty |= Approach(ref _anchorsAlpha, _anchorsTarget, dt);
        dirty |= Approach(ref _primaryAlpha, _primaryTarget, dt);
        dirty |= Approach(ref _othersAlpha, _othersTarget, dt);

        if (_spreadElapsed >= 0f)
        {
            _spreadElapsed += dt;

            float t = spreadSeconds <= 0f ? 1f : Mathf.Clamp01(_spreadElapsed / spreadSeconds);
            _spread = Mathf.LerpUnclamped(_spreadFrom, _spreadTo, spreadCurve.Evaluate(t));

            if (t >= 1f) _spreadElapsed = -1f;
            dirty = true;
        }

        // The offset moves every frame while the journey runs, but a vertex-dirty Graphic
        // rebuilds its whole Canvas — so only rebuild when it has actually moved enough to
        // land on a different pixel. The spectrum next door rebuilds unconditionally and
        // its own comment says what that costs.
        float offset = CurrentOffset();
        if (float.IsNaN(_lastOffset) || Mathf.Abs(offset - _lastOffset) > 0.00002f)
        {
            _lastOffset = offset;
            dirty = true;
        }

        // The curve's rect can move or resize (the Vizlab row shifter, a layout change);
        // follow it. Comparing one Rect per frame is cheap.
        Rect aligned = DrawRect();
        if (aligned != _lastDrawRect)
        {
            _lastDrawRect = aligned;
            dirty = true;
        }

        if (dirty) SetVerticesDirty();
    }

    Rect _lastDrawRect;
    readonly Vector3[] _corners = new Vector3[4];

    /// <summary>
    /// The rectangle to draw in, in this object's local space: the curve's drawing band when
    /// aligned (see alignToCurve), otherwise this object's own rect.
    /// </summary>
    Rect DrawRect()
    {
        Rect own = rectTransform.rect;
        if (!alignToCurve) return own;

        RectTransform target = alignTo;
        if (target == null)
        {
            if (spectrum == null) spectrum = FindObjectOfType<SpectrumHUD_NEW>();
            if (spectrum != null) target = spectrum.transform as RectTransform;
        }
        if (target == null || target == rectTransform) return own;

        // The curve's rect, carried into this object's local space through world space, so
        // any nesting, anchoring, pivot or scale between the two objects cancels out.
        target.GetWorldCorners(_corners);
        Vector3 a = rectTransform.InverseTransformPoint(_corners[0]);
        Vector3 b = rectTransform.InverseTransformPoint(_corners[2]);
        Rect r = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));

        // The curve only uses part of its rect's height (graphHeightPercent, hanging from the
        // top or sitting on the bottom). Use the same band, so a mark's foot is on the curve's
        // zero line.
        LineGraphRenderer_NEW graph = target.GetComponent<LineGraphRenderer_NEW>();
        if (graph != null)
        {
            float h = r.height * graph.GraphHeightPercent;
            float y0 = graph.AnchorTop ? r.yMax - h : r.yMin;
            r = new Rect(r.xMin, y0, r.width, h);
        }

        return r;
    }

    bool Approach(ref float value, float target, float dt)
    {
        if (Mathf.Approximately(value, target)) return false;

        float step = fadeSeconds <= 0f ? 1f : dt / fadeSeconds;
        value = Mathf.MoveTowards(value, target, step);
        return true;
    }

    float CurrentOffset()
    {
        if (!Application.isPlaying) return previewInEditor ? previewOffset : 0f;

        return spectrum != null ? spectrum.RedshiftOffset : 0f;
    }

    /// <summary>
    /// An element's alpha. Forced to 1 while previewing, so laying the marks out does not
    /// require playing to the second cosmic web to see them.
    /// </summary>
    float Alpha(float runtimeAlpha)
    {
        if (!Application.isPlaying) return previewInEditor ? 1f : 0f;

        return runtimeAlpha;
    }

    bool IsPhysical(Mark mark)
    {
        return _physicalLiveT != null && mark.zAbs > 0f;
    }

    float RestT(Mark mark)
    {
        return IsPhysical(mark) ? _physicalBirthT : mark.restT;
    }

    float LiveT(Mark mark)
    {
        if (IsPhysical(mark))
        {
            // _spread still applies, as the demonstration gesture it always was: it
            // exaggerates the line's distance from its birth point, and it is 1 except
            // during the F4 step.
            float birth = _physicalBirthT;
            return birth + (_physicalLiveT(mark.zAbs) - birth) * _spread;
        }

        return mark.restT + CurrentOffset() * mark.travelFraction * _spread;
    }

    // -- Drawing --------------------------------------------------------------

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Mark[] active = ActiveMarks;
        if (active == null || active.Length == 0) return;

        float anchorsAlpha = Alpha(_anchorsAlpha);
        float primaryAlpha = Alpha(_primaryAlpha);
        float othersAlpha = Alpha(_othersAlpha);

        if (anchorsAlpha <= 0.001f && primaryAlpha <= 0.001f && othersAlpha <= 0.001f) return;

        Rect r = DrawRect();
        float anchorHalf = anchorStyle == AnchorStyle.Triangle ? anchorTriangleWidth * 0.5f : tickWidth * 0.5f;

        // Drawn back to front: anchors, then connectors, then live ticks. A live tick
        // sitting on top of its own connector reads as one object with a tail; the other
        // order reads as two objects that overlap.
        SortMarks();

        bool drewPhysicalAnchor = false;

        for (int pass = 0; pass < 3; pass++)
        {
            for (int i = 0; i < _sorted.Count; i++)
            {
                Mark mark = _sorted[i];

                float alpha = mark.isPrimary ? primaryAlpha : othersAlpha;

                // Anchors come up as a row, before either the primary or the rest is
                // named, so they fade on their own schedule.
                if (pass == 0) alpha = anchorsAlpha;

                if (alpha <= 0.001f) continue;

                float restX = Inset(Mathf.Lerp(r.xMin, r.xMax, Mathf.Clamp01(RestT(mark))), r, anchorHalf);
                float liveTValue = LiveT(mark);

                // A mark pushed off the red end of the bar is not drawn rather than being
                // clamped to the edge, because a clamped mark parks against the frame and
                // reads as a line that has stopped moving — the opposite of the point.
                bool liveOnBar = liveTValue >= 0f && liveTValue <= 1f;
                float liveX = Inset(Mathf.Lerp(r.xMin, r.xMax, Mathf.Clamp01(liveTValue)), r, tickWidth * 0.5f);

                Color tint = mark.isPrimary ? primaryColor : liveColor;

                if (pass == 0)
                {
                    // An anchor pushed off the bar (the arrival zoom narrows past it) is not
                    // drawn — clamped, it would sit on the frame claiming to be a birth point.
                    float restT = RestT(mark);
                    if (restT < -0.001f || restT > 1.001f) continue;

                    // In physical mode every line shares one birth point, so draw that
                    // anchor once. Stacking N translucent copies would make it N times
                    // brighter than the design says the reference should be.
                    if (IsPhysical(mark))
                    {
                        if (drewPhysicalAnchor) continue;
                        drewPhysicalAnchor = true;
                    }

                    if (anchorStyle == AnchorStyle.Triangle)
                    {
                        // Under the bar, tip up, pointing at the birth wavelength.
                        // Two halves meeting at a point on the back edge; pulled in by
                        // anchorTriangleNotch, that point makes it the LAF art's arrowhead.
                        float tipY = r.yMin - anchorTriangleGap;
                        float backY = tipY - anchorTriangleHeight;
                        var tip = new Vector2(restX, tipY);
                        var notch = new Vector2(restX, backY + anchorTriangleHeight * anchorTriangleNotch);
                        Color32 c = Fade(anchorTriangleColor, alpha);

                        AddTriangle(vh, tip, new Vector2(restX - anchorTriangleWidth * 0.5f, backY), notch, c);
                        AddTriangle(vh, tip, notch, new Vector2(restX + anchorTriangleWidth * 0.5f, backY), c);
                    }
                    else
                    {
                        AddQuad(vh, restX - tickWidth * 0.5f, r.yMin,
                                    restX + tickWidth * 0.5f, r.yMin + r.height * anchorHeight,
                                    Fade(anchorColor, alpha));
                    }
                }
                else if (pass == 1)
                {
                    if (!liveOnBar) continue;
                    if (Mathf.Abs(liveX - restX) < 1f) continue;

                    float y = r.yMin + r.height * connectorY;

                    AddQuad(vh, Mathf.Min(restX, liveX), y - connectorThickness * 0.5f,
                                Mathf.Max(restX, liveX), y + connectorThickness * 0.5f,
                                Fade(connectorColor, alpha * (mark.isPrimary ? 1f : 0.55f)));
                }
                else
                {
                    if (!liveOnBar) continue;

                    AddQuad(vh, liveX - tickWidth * 0.5f, r.yMin,
                                liveX + tickWidth * 0.5f, r.yMin + r.height * liveHeight,
                                Fade(tint, alpha));
                }
            }
        }
    }

    /// <summary>
    /// Primary last, so it draws over the others within each pass. Rebuilt into a cached
    /// list rather than sorting the serialized array, which would reorder what somebody
    /// authored in the Inspector every time the mesh rebuilt.
    /// </summary>
    void SortMarks()
    {
        _sorted.Clear();

        Mark[] list = ActiveMarks;
        if (list == null) return;

        for (int i = 0; i < list.Length; i++)
            if (list[i] != null && !list[i].isPrimary) _sorted.Add(list[i]);

        for (int i = 0; i < list.Length; i++)
            if (list[i] != null && list[i].isPrimary) _sorted.Add(list[i]);
    }

    /// <summary>
    /// Keep a tick fully inside the bar. With baked data the birth tick IS the bar's left
    /// edge; drawn centred on it, half the tick fell outside and the rest sat under the
    /// graph's own white axis line — so the anchor looked like it had vanished. Nudged in by
    /// half its width plus a pixel, it stays visible next to the axis.
    /// </summary>
    float Inset(float x, Rect r, float halfWidth)
    {
        float pad = halfWidth + 1f;
        if (r.width <= 2f * pad) return x;
        return Mathf.Clamp(x, r.xMin + pad, r.xMax - pad);
    }

    static void AddTriangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color32 color)
    {
        int i = vh.currentVertCount;

        UIVertex v = UIVertex.simpleVert;
        v.color = color;

        v.position = a; vh.AddVert(v);
        v.position = b; vh.AddVert(v);
        v.position = c; vh.AddVert(v);

        // Both windings, so the shape shows regardless of which way the canvas culls.
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i, i + 2, i + 1);
    }

    Color Fade(Color c, float alpha)
    {
        c.a *= Mathf.Clamp01(alpha) * color.a;
        return c;
    }

    static void AddQuad(VertexHelper vh, float x0, float y0, float x1, float y1, Color32 c)
    {
        int i = vh.currentVertCount;

        UIVertex v = UIVertex.simpleVert;
        v.color = c;

        v.position = new Vector3(x0, y0); vh.AddVert(v);
        v.position = new Vector3(x0, y1); vh.AddVert(v);
        v.position = new Vector3(x1, y1); vh.AddVert(v);
        v.position = new Vector3(x1, y0); vh.AddVert(v);

        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i + 2, i + 3, i);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();

        // liveHeight below anchorHeight makes the anchor the taller of the two, which
        // inverts the reading: the fixed reference starts looking like the measurement.
        if (liveHeight < anchorHeight) liveHeight = anchorHeight;
    }
#endif
}

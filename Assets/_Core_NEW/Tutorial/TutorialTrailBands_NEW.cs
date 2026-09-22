using UnityEngine;

/// <summary>
/// What the photon trail shows of its own spectrum, over Phase 3.
///
/// The trail's WIDTH is a spectrum: PhotonTrailRainbow draws UV along one edge, the
/// visible colours across the middle, IR along the other edge, and samples the
/// absorption buffer at the same wavelength coordinate. This component drives the
/// trail's material through a MaterialPropertyBlock to tell that story a step at a time:
///
///   D1  UnfoldUVIR   The ribbon starts cropped to the visible band and widens to show
///                    the UV and IR edges — the light carries more than the eye sees.
///   D2  FlashVisible The visible band alone brightens a few times: this is the part
///                    human eyes can see.
///   D5  FlashLines   The absorption line blinks as it appears.
///   D7  Redden       UV glow falls and IR glow rises as the spectrum redshifts.
///   D9  FoldUVIR     Back to the visible band only.
///
/// CROPPING RATHER THAN HIDING. The shader maps width to wavelength through
/// _SpectrumScale and _SpectrumOffset, so the ribbon can be made to span only the
/// visible range and then opened out to the full range. The UV and IR really do unfold
/// out of the edges rather than fading in on top. Absorption lines use the same
/// coordinate, so they stay on the right wavelengths throughout.
///
/// ONE WAVELENGTH, DRAWN TWICE, AT TWO SCALES. A hydrogen atom takes one wavelength out
/// of the light. On the spectrum bar that is a dip in the curve; on the ribbon it is a
/// dark stripe across the width, at the same place on the same axis, sliding red with
/// the same drift. Nothing joins them and nothing needs to — they are one fact shown in
/// two forms, and the job here is only to make sure they agree: mirrorAcross puts the
/// ribbon's UV and IR on the same sides as the bar's, and lineSpread makes the stripe
/// thick enough to see on a ribbon that is a fifth of the bar's width on screen.
///
/// THE COLOUR BANDS NEVER MOVE DURING REDSHIFT, deliberately. They are the wavelength
/// scale — 500 nm is green whatever the light has been through. What redshift moves is
/// the light's content, and the absorption lines already slide redward through
/// AbsorptionField_NEW. The "redder" read comes from the balance of UV and IR glow.
///
/// A property block, not the material. The trail's material is a copied asset and the
/// absorption field already instances it; writing through a block changes nothing on
/// disk and nothing another component set. Band edges are read from PhotonSpectrumTrail,
/// which paints them, so the highlight and the crop cannot drift away from the colours.
///
/// Ramps run on the world clock, so a pause holds them.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TrailRenderer))]
[HierarchyBadge_NEW("TRAIL BANDS", "#7E3B86")]
public class TutorialTrailBands_NEW : MonoBehaviour
{
    [Header("Wiring")]
    [Tooltip("Leave empty to use the one on this object. Its uvBandWidth and irBandWidth " +
             "are the band edges everything here uses.")]
    [SerializeField] PhotonSpectrumTrail spectrumTrail;

    [Header("Which way round the spectrum runs")]
    [Tooltip("Mirror the ribbon's wavelength axis, so UV and IR swap sides on screen.\n\n" +
             "WHY THIS IS A CHECKBOX AND NOT A CONSTANT. The absorption line is drawn " +
             "twice — as a dip in the bar's curve and as a dark stripe across the " +
             "ribbon — and the two only read as one wavelength if they run the same way " +
             "left to right. The bar is fixed: UV on the left, IR on the right. The " +
             "ribbon is not: it is a TrailRenderer facing the camera, so which edge Unity " +
             "calls uv.y = 0 comes out of a cross product between the direction of travel " +
             "and the direction to the camera, and which of those lands on screen-left " +
             "cannot be worked out from the source — it has to be looked at.\n\n" +
             "So: run it, watch the stripe appear, and if it is on the opposite side of " +
             "the ribbon's centre from the dip in the bar, tick this. Same reasoning as " +
             "flipAcross on TutorialLineIndicator_NEW, and the two want the same answer.")]
    [SerializeField] bool mirrorAcross = false;

    [Header("Unfold / fold UV and IR")]
    [Tooltip("Seconds for the ribbon to widen from the visible band to the full spectrum, " +
             "or back.")]
    [SerializeField] float unfoldSeconds = 2.5f;

    [SerializeField] AnimationCurve unfoldShape = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Visible band flash")]
    [Tooltip("How many times the visible band brightens.")]
    [Min(1)]
    [SerializeField] int flashCount = 3;

    [Tooltip("Seconds for all of the flashes together.")]
    [SerializeField] float flashSeconds = 2.4f;

    [Tooltip("Extra brightness at the top of each flash. 1 doubles the band's colour.")]
    [SerializeField] float flashPeak = 1.4f;

    [Header("Absorption line")]
    [Tooltip("How wide the dark line is painted across the RIBBON, as a fraction of the " +
             "spectrum. 0 leaves it exactly as wide as it is in the shared buffer.\n\n" +
             "The line is cut once, at one wavelength, and drawn in two places at very " +
             "different scales. The spectrum bar is 720 pixels wide, so the authored " +
             "0.009 of the spectrum comes out about six pixels: a clear notch. The " +
             "ribbon is around 150 pixels wide on screen at the light, so the same 0.009 " +
             "is a single pixel — present, correct, and invisible, which is the one thing " +
             "D5 cannot afford.\n\n" +
             "This widens it on the ribbon only. It does not move it, does not touch the " +
             "buffer and does not touch the bar, so the two marks stay at the same " +
             "wavelength and drift together — one of them is simply drawn thick enough " +
             "to be seen at the size it is drawn.\n\n" +
             "The line is drawn about 0.009 plus twice this wide — the buffer's own width " +
             "is in there, so halving this does not halve the line. At 0.013 it comes out " +
             "about 0.035 of the spectrum, or roughly five pixels on the ribbon at the " +
             "size it is drawn at the light.\n\n" +
             "Raise it until the stripe reads from standing distance; drop it if the " +
             "ribbon starts looking banded rather than marked, or if D8's second line " +
             "runs into the first.")]
    [Range(0f, 0.06f)]
    [SerializeField] float lineSpread = 0.013f;

    [Tooltip("0 draws the line with the buffer's soft falloff; 1 cuts it as one solid black " +
             "band with a clean edge.\n\n" +
             "1, because an absorption on the light has to read as ONE thick black line. The " +
             "soft falloff that makes the bar's dip a curve made the ribbon's line a smudge " +
             "that faded into the colours either side of it.")]
    [Range(0f, 1f)]
    [SerializeField] float lineHardness = 1f;

    [Tooltip("How BLACK the absorbed line is, 0 to 1.\n\n" +
             "The ribbon is additive, so on its own an absorbed wavelength just stops " +
             "adding light and the line shows whatever is behind the trail — the quasar, " +
             "the stars — reading as a hole rather than as black. At 1 the shader paints " +
             "the line black over the background first, so the colour that was taken out " +
             "of the light reads as a black line.")]
    [Range(0f, 1f)]
    [SerializeField] float lineBlack = 1f;

    [Header("Absorption line blink")]
    [Min(1)]
    [SerializeField] int lineBlinkCount = 3;

    [SerializeField] float lineBlinkSeconds = 1.8f;

    [Tooltip("Line strength at the bottom of each blink, as a fraction of normal.")]
    [Range(0f, 1f)]
    [SerializeField] float lineBlinkLow = 0f;

    [Header("Redden during redshift")]
    [Tooltip("Seconds to reach the reddened balance once Redden is called.")]
    [SerializeField] float reddenSeconds = 8f;

    [Tooltip("UV glow at full redshift, as a fraction of the material's value.")]
    [SerializeField] float uvGlowWhenRed = 0.25f;

    [Tooltip("IR glow at full redshift, as a multiple of the material's value.")]
    [SerializeField] float irGlowWhenRed = 1.8f;

    TrailRenderer _trail;
    MaterialPropertyBlock _block;

    float _baseUVGlow = 1.8f;
    float _baseIRGlow = 1.2f;
    float _baseLineStrength = 1f;

    float _unfold;          // 0 visible only, 1 full spectrum
    float _unfoldTarget;
    float _redden;          // 0 none, 1 fully reddened
    float _reddenTarget;
    float _flashElapsed = -1f;
    float _blinkElapsed = -1f;

    static readonly int SpectrumOffsetId = Shader.PropertyToID("_SpectrumOffset");
    static readonly int SpectrumScaleId = Shader.PropertyToID("_SpectrumScale");
    static readonly int UVGlowId = Shader.PropertyToID("_UVGlowStrength");
    static readonly int IRGlowId = Shader.PropertyToID("_IRGlowStrength");
    static readonly int VisibleStartId = Shader.PropertyToID("_VisibleBandStart");
    static readonly int VisibleEndId = Shader.PropertyToID("_VisibleBandEnd");
    static readonly int VisibleHighlightId = Shader.PropertyToID("_VisibleHighlight");
    static readonly int LineStrengthId = Shader.PropertyToID("_AbsorptionLineStrength");
    static readonly int LineSpreadId = Shader.PropertyToID("_AbsorptionLineSpread");
    static readonly int LineHardnessId = Shader.PropertyToID("_AbsorptionLineHardness");
    static readonly int LineBlackId = Shader.PropertyToID("_AbsorptionBlack");

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Where the visible band starts on the 0 (UV) to 1 (IR) axis.</summary>
    public float VisibleStart
    {
        get { return spectrumTrail != null ? Mathf.Clamp01(spectrumTrail.uvBandWidth) : 0.15f; }
    }

    /// <summary>Where the visible band ends on the same axis.</summary>
    public float VisibleEnd
    {
        get { return spectrumTrail != null ? Mathf.Clamp01(1f - spectrumTrail.irBandWidth) : 0.85f; }
    }

    /// <summary>
    /// Where a wavelength sits across the ribbon's width right now: the trail's uv.y,
    /// 0 on one edge and 1 on the other.
    ///
    /// Inverts the shader's specT = ((1 − uv.y) + offset) · scale using the crop this
    /// component applied last, so it stays right while the UV and IR edges unfold.
    /// </summary>
    public float WidthFraction(float wavelength)
    {
        float u = (wavelength - _shownLow) / Mathf.Max(0.001f, _shownSpan);

        // Unmirrored the shader reads specT off (1 - uv.y), so the inverse flips; mirrored
        // it reads it off uv.y directly and the inverse does not. Kept here rather than at
        // the caller so the arrow and the stripe cannot end up on different edges.
        return mirrorAcross ? u : 1f - u;
    }

    float _shownLow;
    float _shownSpan = 1f;

    public void UnfoldUVIR() { _unfoldTarget = 1f; }

    public void FoldUVIR() { _unfoldTarget = 0f; }

    public void FlashVisible() { _flashElapsed = 0f; }

    public void BlinkLines() { _blinkElapsed = 0f; }

    public void Redden() { _reddenTarget = 1f; }

    /// <summary>Visible band only, no redshift, nothing flashing. Before D1, and after attract.</summary>
    public void ResetForAttract()
    {
        _unfold = _unfoldTarget = 0f;
        _redden = _reddenTarget = 0f;
        _flashElapsed = -1f;
        _blinkElapsed = -1f;

        Apply();
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        _trail = GetComponent<TrailRenderer>();
        _block = new MaterialPropertyBlock();

        if (spectrumTrail == null) spectrumTrail = GetComponent<PhotonSpectrumTrail>();

        // The material's own values are what "full" means. Read from the shared asset,
        // before anything has instanced it.
        Material m = _trail.sharedMaterial;
        if (m != null)
        {
            if (m.HasProperty(UVGlowId)) _baseUVGlow = m.GetFloat(UVGlowId);
            if (m.HasProperty(IRGlowId)) _baseIRGlow = m.GetFloat(IRGlowId);
            if (m.HasProperty(LineStrengthId)) _baseLineStrength = m.GetFloat(LineStrengthId);
        }

        ResetForAttract();
    }

    void Update()
    {
        float dt = TutorialClock_NEW.DeltaTime;

        _unfold = Mathf.MoveTowards(_unfold, _unfoldTarget, unfoldSeconds > 0f ? dt / unfoldSeconds : 1f);
        _redden = Mathf.MoveTowards(_redden, _reddenTarget, reddenSeconds > 0f ? dt / reddenSeconds : 1f);

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
        if (_trail == null) return;

        float visStart = VisibleStart;
        float visEnd = VisibleEnd;

        // Crop: at 0 the width spans only [visStart, visEnd]; at 1 the whole [0, 1].
        // specT = frac(((1 - uv.y) + offset) * scale), so the lowest wavelength shown is
        // offset * scale and the span is scale.
        float open = unfoldShape != null ? unfoldShape.Evaluate(_unfold) : _unfold;
        float lo = Mathf.Lerp(visStart, 0f, open);
        float hi = Mathf.Lerp(visEnd, 1f, open);
        float scale = Mathf.Max(0.001f, hi - lo);

        _shownLow = lo;
        _shownSpan = scale;

        float uvGlow = _baseUVGlow * open * Mathf.Lerp(1f, uvGlowWhenRed, _redden);
        float irGlow = _baseIRGlow * open * Mathf.Lerp(1f, irGlowWhenRed, _redden);

        _trail.GetPropertyBlock(_block);

        // The shader computes specT = frac(((1 - uv.y) + offset) * scale), so the lowest
        // wavelength shown is offset * scale and the span is scale.
        //
        // Mirroring is a negative scale: at uv.y = 1 that gives (0 + offset) * -span = hi
        // and at uv.y = 0 it gives (1 + offset) * -span = hi - span = lo, which is the
        // same window traversed the other way. frac of a negative is still its positive
        // fractional part, and both ends already sit inside 0 to 1, so nothing wraps.
        float signedScale = mirrorAcross ? -scale : scale;
        float offset = mirrorAcross ? -hi / scale : lo / scale;

        _block.SetFloat(SpectrumScaleId, signedScale);
        _block.SetFloat(SpectrumOffsetId, offset);
        _block.SetFloat(UVGlowId, uvGlow);
        _block.SetFloat(IRGlowId, irGlow);
        _block.SetFloat(VisibleStartId, visStart);
        _block.SetFloat(VisibleEndId, visEnd);
        _block.SetFloat(VisibleHighlightId, Pulse(_flashElapsed, flashSeconds, flashCount) * flashPeak);
        _block.SetFloat(LineStrengthId,
                        _baseLineStrength * Mathf.Lerp(1f, lineBlinkLow,
                                                       Pulse(_blinkElapsed, lineBlinkSeconds, lineBlinkCount)));

        // Scaled by the crop, so the stripe keeps its width on screen. Cropping to the
        // visible band puts a fifth of the spectrum across the same ribbon, which magnifies
        // everything on it — a spread left at its full value would fold away five times
        // too wide at D9.
        _block.SetFloat(LineSpreadId, lineSpread * scale);
        _block.SetFloat(LineHardnessId, lineHardness);
        _block.SetFloat(LineBlackId, lineBlack);

        _trail.SetPropertyBlock(_block);
    }

    /// <summary>
    /// 0 to 1 and back, count times over seconds. Starts and ends at 0, so a flash never
    /// snaps on or off.
    /// </summary>
    static float Pulse(float elapsed, float seconds, int count)
    {
        if (elapsed < 0f || seconds <= 0f) return 0f;

        float t = Mathf.Clamp01(elapsed / seconds);
        return 0.5f - 0.5f * Mathf.Cos(t * count * Mathf.PI * 2f);
    }
}

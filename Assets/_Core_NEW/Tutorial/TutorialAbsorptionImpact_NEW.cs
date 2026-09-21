using UnityEngine;

/// <summary>
/// What happens, everywhere at once, on the frame a hydrogen atom takes a colour out of
/// the light. One component, one call, every absorption.
///
/// WHY THIS EXISTS. The absorption is the frame the piece is built around — GDD §3 items
/// 3 and 4, "hitting hydrogen costs you colour" and "the colour you lost is that black
/// line" — and until now it was the quietest thing in the tutorial. The first one had a
/// slow motion and a blink; every one after it had nothing at all. D6's four atoms struck
/// the light and the only evidence was a few pixels changing on a bar at the top of the
/// screen, which a visitor looking at the atom — where anybody would be looking — did not
/// see happen.
///
/// So the loss is made physical, on every channel the piece has, at the same instant:
///
///   The screen    a cold flash, short and not blinding — it repeats, so it must not
///                 whiteout. The colour of the atom's own light.
///   The camera    a jolt on top of whatever the emission's shake is doing.
///   The hands     the pad rumbles — but NOT from here. TutorialSpectrum_NEW pulses it
///                 directly, so it cannot depend on this component having been built.
///   The light     the new line blinks on the photon trail. Blinks only: the light's own
///                 look is not changed by any of this.
///   The readout   the bar punches out and back so the eye is pulled to it, and the new
///                 line's marker blinks there.
///
/// The atom's own burst and the line arriving wide and black are not here: the atom owns
/// the first (it is where the atom is), and AbsorptionField_NEW's spawn pulse owns the
/// second. Both fire on the same frame as this.
///
/// THE SAME EVERY TIME, which is the rule TutorialSpectrum_NEW.AbsorbAtRestFrame already
/// keeps for the line itself: the tenth atom does what the first did. D5's slow motion is
/// the one thing the first absorption gets that the others do not, and it belongs to D5,
/// not to the absorption.
///
/// Wired to TutorialSpectrum_NEW.onAbsorbed, which fires once per absorption whatever
/// caused it — D5's beat, D6's cluster, D8's atom. That is the one place every absorption
/// already passes through, so nothing can absorb without this happening.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("IMPACT", "#C0607A")]
public class TutorialAbsorptionImpact_NEW : MonoBehaviour
{
    [Header("Wiring")]
    [Tooltip("Full-screen flash. Leave empty to skip it.")]
    [SerializeField] TutorialFlash_NEW flash;

    [Tooltip("The camera's shake. Leave empty to find it.")]
    [SerializeField] TutorialCameraShake_NEW shake;

    [Tooltip("Controller rumble. Only stopped from here, on the attract reset — the pulse " +
             "itself comes from TutorialSpectrum_NEW. Leave empty to find it.")]
    [SerializeField] TutorialRumble_NEW rumble;

    [Tooltip("The photon trail's spectrum. The absorbed line blinks on it.")]
    [SerializeField] TutorialTrailBands_NEW trailBands;

    [Tooltip("The bar's bands. The new line's marker blinks there.")]
    [SerializeField] TutorialSpectrumBands_NEW spectrumBands;

    [Tooltip("The spectrum bar itself, for the punch. Leave empty to skip it.")]
    [SerializeField] RectTransform bar;

    [Header("Flash")]
    [Tooltip("Colour and PEAK opacity. Cold, like the atom's own light, and well short of " +
             "opaque: this fires four times in ten seconds during D6, and a whiteout that " +
             "repeats reads as the display failing rather than as an impact.")]
    [SerializeField] Color flashColor = new Color(0.72f, 0.86f, 1f, 0.42f);

    [SerializeField] float flashHoldSeconds = 0.04f;
    [SerializeField] float flashDecaySeconds = 0.38f;

    [Header("Camera")]
    [Tooltip("Jolt strength, 0 to 1, on top of the emission's own shake.")]
    [Range(0f, 1f)]
    [SerializeField] float shakeKick = 0.9f;

    [Tooltip("Seconds for the jolt to die away.")]
    [SerializeField] float shakeSeconds = 0.45f;

    [Header("Bar punch")]
    [Tooltip("How far the bar swells at the peak, as a fraction of its size. Small: it has " +
             "to catch the eye from the other end of the screen, not jump.")]
    [Range(0f, 0.5f)]
    [SerializeField] float punch = 0.12f;

    [SerializeField] float punchSeconds = 0.4f;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    Vector3 _barRestScale = Vector3.one;
    bool _barScaleCaptured;
    float _punchElapsed = -1f;

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// The light has just lost a colour. Everything, now. No arguments, so it hangs on a
    /// UnityEvent.
    /// </summary>
    public void Play()
    {
        if (flash != null) flash.Flash(flashColor, flashHoldSeconds, flashDecaySeconds);
        if (shake != null) shake.Kick(shakeKick, shakeSeconds);

        // No rumble here. The pad is pulsed directly by TutorialSpectrum_NEW, so it does not
        // depend on this component existing — it did, and the pad was silent. Pulsing here
        // as well would fire it twice per absorption.

        if (trailBands != null) trailBands.BlinkLines();
        if (spectrumBands != null) spectrumBands.BlinkLine();

        if (bar != null)
        {
            CaptureBarScale();
            _punchElapsed = 0f;
        }

        if (debugLog) Debug.Log("[TutorialAbsorptionImpact_NEW] Absorbed.", this);
    }

    /// <summary>Nothing mid-flight, for the next visitor. The attract reset calls this.</summary>
    public void ResetForAttract()
    {
        _punchElapsed = -1f;
        if (bar != null && _barScaleCaptured) bar.localScale = _barRestScale;
        if (rumble != null) rumble.Stop();
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (shake == null) shake = FindObjectOfType<TutorialCameraShake_NEW>();
        if (rumble == null) rumble = FindObjectOfType<TutorialRumble_NEW>();

        CaptureBarScale();
    }

    void Update()
    {
        if (_punchElapsed < 0f || bar == null) return;

        // Real time — it is the readout reacting, not the world. Held while paused.
        if (!TutorialClock_NEW.Paused) _punchElapsed += Time.unscaledDeltaTime;

        float t = punchSeconds > 0f ? Mathf.Clamp01(_punchElapsed / punchSeconds) : 1f;

        // Out fast and settle: a sine lobe with its peak pulled forward.
        float swell = Mathf.Sin(Mathf.Sqrt(t) * Mathf.PI);
        bar.localScale = _barRestScale * (1f + punch * swell);

        if (t < 1f) return;

        bar.localScale = _barRestScale;
        _punchElapsed = -1f;
    }

    void OnDisable()
    {
        if (bar != null && _barScaleCaptured) bar.localScale = _barRestScale;
        _punchElapsed = -1f;
    }

    /// <summary>
    /// Read the bar's resting scale once, and never while a punch is running — a second
    /// absorption during the first punch would otherwise record the swollen size as the
    /// rest and the bar would creep larger with every hit.
    /// </summary>
    void CaptureBarScale()
    {
        if (bar == null || _barScaleCaptured || _punchElapsed >= 0f) return;

        _barRestScale = bar.localScale;
        _barScaleCaptured = true;
    }
}

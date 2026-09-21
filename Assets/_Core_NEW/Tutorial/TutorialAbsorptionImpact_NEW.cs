using UnityEngine;

/// <summary>
/// What the screen does on the frame a hydrogen atom takes a colour out of the light.
/// One call, every absorption.
///
/// Deliberately little, and none of it moves. What makes the absorption read is the line
/// itself — one thick black line cut across the player's own light (see
/// TutorialTrailBands_NEW.lineHardness) — and the pad rumbling in their hands (see
/// TutorialAtom_NEW.rumbleLeadSeconds). This only frames that:
///
///   The screen    a short cold flash the colour of the atom's light, well short of
///                 opaque because it repeats four times in ten seconds during D6.
///   The light     the new line blinks on the photon trail.
///   The readout   the new line's marker blinks on the bar.
///
/// NOTHING SHAKES. An earlier version jolted the camera and punched the bar on every hit
/// to go with the rumble. The rumble is the physical part; the frame shaking along with it
/// competed with the one thing the player is meant to be looking at, the line appearing.
///
/// Wired to TutorialSpectrum_NEW.onAbsorbed, which fires once per absorption whatever
/// caused it.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("IMPACT", "#C0607A")]
public class TutorialAbsorptionImpact_NEW : MonoBehaviour
{
    [Header("Wiring")]
    [Tooltip("Full-screen flash. Leave empty to skip it.")]
    [SerializeField] TutorialFlash_NEW flash;

    [Tooltip("Controller rumble. Only stopped from here, on the attract reset — the pulse " +
             "itself comes from the atom. Leave empty to find it.")]
    [SerializeField] TutorialRumble_NEW rumble;

    [Tooltip("The photon trail's spectrum. The absorbed line blinks on it.")]
    [SerializeField] TutorialTrailBands_NEW trailBands;

    [Tooltip("The bar's bands. The new line's marker blinks there.")]
    [SerializeField] TutorialSpectrumBands_NEW spectrumBands;

    [Header("Flash")]
    [Tooltip("Colour and PEAK opacity. Cold, like the atom's own light, and well short of " +
             "opaque: this fires four times in ten seconds during D6, and a whiteout that " +
             "repeats reads as the display failing rather than as an impact.")]
    [SerializeField] Color flashColor = new Color(0.72f, 0.86f, 1f, 0.42f);

    [SerializeField] float flashHoldSeconds = 0.04f;
    [SerializeField] float flashDecaySeconds = 0.38f;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>The light has just lost a colour. No arguments, so it hangs on a UnityEvent.</summary>
    public void Play()
    {
        if (flash != null) flash.Flash(flashColor, flashHoldSeconds, flashDecaySeconds);
        if (trailBands != null) trailBands.BlinkLines();
        if (spectrumBands != null) spectrumBands.BlinkLine();

        if (debugLog) Debug.Log("[TutorialAbsorptionImpact_NEW] Absorbed.", this);
    }

    /// <summary>Nothing mid-flight, for the next visitor. The attract reset calls this.</summary>
    public void ResetForAttract()
    {
        if (rumble != null) rumble.Stop();
    }

    void Awake()
    {
        if (rumble == null) rumble = FindObjectOfType<TutorialRumble_NEW>();
    }
}

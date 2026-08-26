using UnityEngine;

/// <summary>
/// Routes a layer's spectrum profile to the two components that own the behaviour.
///
/// Replaces LAFLayerResponder, which forwarded all ten laf* fields to the HUD and only
/// two of them landed anywhere — the rest described absorption lines, which the HUD does
/// not generate. Two of the forwarded values were not even read; they only appeared in a
/// debug log, so the console reported settings that were reaching nothing.
///
/// The split here follows ownership:
///
///   SpectrumHUD_NEW       continuum shape — the emission curve it draws
///   AbsorptionField_NEW   line count, seed, widths, depths, drift — the forest it stamps
///
/// It rides the spectrum channel of the transition schedule, which defaults to
/// CoverReached: the forest changes character while the player is under the top-down
/// cover, rather than visibly reshaping itself on screen.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("SPECTRUM", "#4A94F2")]
public class SpectrumResponder_NEW : LayerResponder_NEW
{
    [Header("Targets")]
    [SerializeField] SpectrumHUD_NEW hud;
    [SerializeField] AbsorptionField_NEW field;

    [Header("Options")]
    [Tooltip("Wipe the existing forest when the layer changes. Off lets the old lines " +
             "drift out naturally, which reads as continuous travel rather than a cut.")]
    [SerializeField] bool clearForestOnChange = false;

    [Tooltip("Re-seed the forest to the new layer's line count on entry.")]
    [SerializeField] bool prePopulateOnChange = true;

    protected override void Awake()
    {
        base.Awake();

        if (hud == null) hud = FindObjectOfType<SpectrumHUD_NEW>();
        if (field == null) field = FindObjectOfType<AbsorptionField_NEW>();

        if (hud == null && field == null)
            Debug.LogWarning("[SpectrumResponder_NEW] Neither a SpectrumHUD_NEW nor an " +
                             "AbsorptionField_NEW was found. This responder will do nothing.", this);
    }

    protected override TimedChannel_NEW SelectChannel(LayerProfile_NEW profile) => profile.timing.spectrum;

    protected override void Apply(LayerProfile_NEW profile)
    {
        SpectrumProfile_NEW spectrum = profile.spectrum;

        if (spectrum == null)
        {
            if (debugLog)
                Debug.Log($"[SpectrumResponder_NEW] '{profile.layerId}' has no spectrum profile. " +
                          "Leaving the current forest alone.", this);
            return;
        }

        if (hud != null) hud.Configure(spectrum);

        if (field == null) return;

        if (clearForestOnChange) field.ClearLines();
        field.Configure(spectrum);
        if (prePopulateOnChange) field.PrePopulate();
    }
}

using UnityEngine;

/// <summary>
/// Sets the spectrum's drift rate from a statement about the whole journey, instead of
/// from a per-second number somebody tuned by feel.
///
/// THE STATEMENT. "Over one run of the piece, the first absorption line travels this far
/// across the bar." Two numbers a person can argue about in a meeting — how long the
/// piece runs, and how much of the bar the oldest line should cross — and the per-second
/// rate falls out of them.
///
/// WHY NOT JUST TUNE driftPerSecond. Because the number that comes out of tuning is not
/// checkable. The profiles currently ship 0.00778 per second against a spectrum one unit
/// wide, so the whole bar scrolls past in 128 seconds; against a ten minute journey that
/// is nearly five complete wraps. Nobody chose five wraps. It is what "looks like it is
/// moving" produces when the thing you are looking at is thirty seconds of screen time
/// and the thing you are describing is thirteen billion years.
///
/// WHY IT SURVIVES THE REAL VERSION. The honest rate is not a rate at all: redshift is a
/// function of how far the light has travelled, and it should come from
/// UniverseJourneyTracker rather than from integrating a per-second constant (this is
/// issue 2 in KNOWN_ISSUES_FOREST.md). When that lands, the budget here does not change
/// — the journey still ends with the line that far across the bar, because that is a
/// composition decision, not a physics one. Only the shape of the curve between the two
/// ends changes. Tuning a per-second number instead would have to be redone from scratch.
///
/// IT ALSO TURNS THE WRAP OFF, loudly. An accumulating gap and a wrapping offset are
/// mutually exclusive; see SpectrumHUD_NEW.SetWrapSpectrum.
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[HierarchyBadge_NEW("BUDGET", "#33B3B3")]
public class RedshiftBudget_NEW : MonoBehaviour
{
    [Header("Wiring")]
    [Tooltip("The absorption field whose drift is being budgeted. Found in the scene if empty.")]
    [SerializeField] AbsorptionField_NEW field;

    [Tooltip("The spectrum HUD. Found in the scene if empty. Only used to switch the " +
             "wrap off.")]
    [SerializeField] SpectrumHUD_NEW spectrum;

    [Header("Budget")]
    [Tooltip("How long one run of the piece lasts, in seconds, quasar to Earth.\n\n" +
             "600 is ten minutes and is a PLACEHOLDER. Put the real number here — it is " +
             "the one input that makes everything else honest, and it is knowable by " +
             "timing a run.")]
    [Min(1f)]
    [SerializeField] float journeySeconds = 600f;

    [Tooltip("How far the first line — the oldest one, stretched the whole way — should " +
             "travel across the bar in that time, as a fraction of the bar's width.\n\n" +
             "0.45 puts it just under halfway. Far enough that the gap is unmistakable " +
             "from the back of the room; short enough that the line never reaches the red " +
             "end and parks against the frame, which reads as the drift having stopped.")]
    [Range(0.05f, 0.9f)]
    [SerializeField] float barWidthsTravelled = 0.45f;

    [Tooltip("The reference drift the budget is expressed against — the driftPerSecond " +
             "the layer profiles carry. The scale is the ratio between what the budget " +
             "asks for and this.\n\n" +
             "Read off SP_Macro / SP_CosmicWeb. If those assets are retuned, this has to " +
             "follow or the budget quietly stops meaning what it says.")]
    [Min(0.00001f)]
    [SerializeField] float profileDriftPerSecond = 0.00778f;

    [Header("Wrap")]
    [Tooltip("Switch the spectrum's wrap off on start.\n\n" +
             "On, because the anchors in RedshiftMarks_NEW measure a gap that only grows " +
             "if the offset does, and a wrap resets it to zero mid-show.")]
    [SerializeField] bool disableWrap = true;

    [Header("Debug")]
    [Tooltip("Log the computed rate once. It is three numbers and it answers 'why is the " +
             "spectrum moving at that speed' without opening this file.")]
    [SerializeField] bool logOnStart = true;

    [Tooltip("Wind the redshift forward by one step per press, so the gap between a line " +
             "and its anchor can be seen without playing a whole run.\n\n" +
             "Needed because a budgeted drift is genuinely invisible over the first minute " +
             "— which is indistinguishable from broken. Editor and development builds only.")]
    [SerializeField] KeyCode debugAdvanceKey = KeyCode.F8;

    [Tooltip("Bar widths added per press of the advance key. 0.1 is about a fifth of the " +
             "whole journey's budget, so five presses show the arrival picture.")]
    [Range(0.01f, 0.5f)]
    [SerializeField] float debugAdvanceStep = 0.1f;

    /// <summary>The per-second drift this budget works out to, in bar widths.</summary>
    public float BudgetedDriftPerSecond
    {
        get { return barWidthsTravelled / Mathf.Max(1f, journeySeconds); }
    }

    /// <summary>The multiplier handed to the absorption field.</summary>
    public float DriftScale
    {
        get { return BudgetedDriftPerSecond / Mathf.Max(0.00001f, profileDriftPerSecond); }
    }

    void Awake()
    {
        if (field == null) field = FindObjectOfType<AbsorptionField_NEW>();
        if (spectrum == null) spectrum = FindObjectOfType<SpectrumHUD_NEW>();
    }

    void Start()
    {
        Apply();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    void Update()
    {
        if (debugAdvanceKey == KeyCode.None) return;
        if (!Input.GetKeyDown(debugAdvanceKey)) return;
        if (spectrum == null) return;

        spectrum.AddRedshiftOffset(debugAdvanceStep);

        Debug.Log("[RedshiftBudget_NEW] Redshift wound forward to " +
                  spectrum.RedshiftOffset.ToString("0.000") + " bar widths (of " +
                  barWidthsTravelled.ToString("0.00") + " budgeted for the whole run).", this);
    }
#endif

    /// <summary>
    /// Push the budget into the field and the HUD. Public so a debug tool can re-apply
    /// after changing the numbers in play mode.
    /// </summary>
    public void Apply()
    {
        if (field != null)
        {
            field.DriftScale = DriftScale;
        }
        else
        {
            Debug.LogWarning("[RedshiftBudget_NEW] No AbsorptionField_NEW found. The drift " +
                             "is whatever the profiles say, which is about " +
                             (1f / profileDriftPerSecond).ToString("0") +
                             " seconds per full spectrum.", this);
        }

        if (disableWrap && spectrum != null && spectrum.WrapsSpectrum)
        {
            spectrum.SetWrapSpectrum(false);

            // Loud on purpose. It changes what the spectrum does for the whole run, and
            // the next person to wonder why the bar no longer wraps should find the
            // answer in the Console rather than by reading three files.
            Debug.Log("[RedshiftBudget_NEW] Spectrum wrap switched OFF. The redshift marks " +
                      "measure a gap that only accumulates if the offset does; a wrap " +
                      "resets it to zero mid-show.", this);
        }

        if (!logOnStart) return;

        Debug.Log("[RedshiftBudget_NEW] " + barWidthsTravelled.ToString("0.00") +
                  " bar widths over " + journeySeconds.ToString("0") + "s" +
                  "  ->  " + BudgetedDriftPerSecond.ToString("0.000000") + "/s" +
                  "  (profile " + profileDriftPerSecond.ToString("0.00000") +
                  "/s, scale " + DriftScale.ToString("0.000") + ")", this);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // Applied live so the numbers can be dialled in while playing, which is the only
        // way to judge whether the drift reads as slow-and-deliberate or as stopped.
        if (Application.isPlaying && field != null) field.DriftScale = DriftScale;
    }
#endif
}

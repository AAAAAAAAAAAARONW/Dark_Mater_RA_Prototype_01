using UnityEngine;

/// <summary>
/// The tutorial's spectrum bar: when it exists, what is on it, and what cuts a line
/// into it.
///
/// Phase 3. D2 is the frame the whole piece is built around — GDD §3 items 3 and 4,
/// "hitting hydrogen costs you colour" and "the colour you lost is that black line",
/// both land in one collision — so the bar has to arrive empty, take exactly one line,
/// and let the player pair the two. D4 then cuts three or four more.
///
/// WHY THIS EXISTS RATHER THAN THE JOURNEY'S CHAIN. In the core architecture the
/// spectrum is driven through LayerGate_NEW → LayerState_NEW → SpectrumResponder_NEW →
/// AbsorptionField_NEW / SpectrumHUD_NEW. The tutorial has no layers and no gates, so
/// dragging that chain in would mean wiring four components to make one Configure call,
/// and it would make the tutorial depend on layer state that has no meaning inside it.
///
/// This is the same write, minus the layer machinery — exactly what TutorialSky_NEW
/// does for the nebula, and for the same reason. Read that file first; this one follows
/// its shape deliberately so there is one pattern in the tutorial rather than two.
///
/// THE PROFILE HAS TO HAVE SPAWNING TURNED OFF, and this is the part that is easy to
/// miss. AbsorptionField_NEW grows a background forest on its own at
/// spawnRatePerSecond — 4 lines a second on the journey's profiles — and ClearLines()
/// says so in its own summary: "Background spawning continues." A tutorial that borrows
/// a journey profile therefore gets D2's single hydrogen line buried under forty
/// others within ten seconds, and the one frame that has to read as one atom, one line
/// reads as noise instead.
///
/// So the tutorial owns its own SpectrumProfile_NEW asset with spawnRatePerSecond,
/// initialLineCount and driftPerSecond all at zero. WarnIfProfileSpawns checks this at
/// runtime rather than trusting it, on the same reasoning as
/// TutorialZoom_NEW.WarnIfSharingAStick: a scene can be wrong without anybody having
/// done anything wrong to it, and the failure is quiet.
///
/// LINE POSITIONS ARE AUTHORED, NOT RANDOM. GDD §5 requires the piece to be fully
/// re-runnable after a 90 second idle, and a walk-up visitor at the Observatories sees
/// whatever the last visitor left. A forest seeded from a clock would put D2's line
/// somewhere different every run, which breaks two things at once: nobody can rehearse
/// against it, and D4's lines stop reading as "more of the same thing that happened to
/// me" because the first one is not where it was. The positions are a serialized list,
/// consumed in order.
///
/// This component owns configuration, visibility and line-stamping. It does not own
/// the bar's position on screen — D3 moves it from centre to its docked HUD spot, and
/// that is a UI animation wired to D3's onEnter, not a property of the spectrum.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("TUT SPEC", "#33B3B3")]
public class TutorialSpectrum_NEW : MonoBehaviour
{
    [Header("Look")]
    [Tooltip("The tutorial's own SpectrumProfile_NEW. Create one with " +
             "Assets > Create > Journey NEW > Spectrum Profile.\n\n" +
             "It MUST have spawnRatePerSecond and initialLineCount at 0, or the field " +
             "grows a forest under D2's single line. driftPerSecond at 0 too, unless the " +
             "storyboard asks for the lines to move. See the class summary.")]
    [SerializeField] SpectrumProfile_NEW profile;

    [Tooltip("The same settings with the redshift drift switched on. D3 swaps to this.\n\n" +
             "A second asset rather than a float on this component, because drift is not " +
             "something this component owns — AbsorptionField_NEW reads driftPerSecond " +
             "off the profile to slide its buffer, and SpectrumHUD_NEW reads the same " +
             "number to slide the continuum template, so the lines and the curve move as " +
             "one. Reaching past the profile to set it would be two places holding one " +
             "number.\n\n" +
             "This is also the journey's own idiom: it has seven SP_* assets, one per " +
             "layer, for exactly this kind of per-context difference.\n\n" +
             "Configure() leaves existing lines alone, so D2's line survives the swap and " +
             "starts moving rather than being wiped.")]
    [SerializeField] SpectrumProfile_NEW driftingProfile;

    [Header("Wiring")]
    [Tooltip("Leave empty to find it in the scene. The shared absorption buffer — the " +
             "HUD and the photon trail both read it, so a line cut here shows in both.")]
    [SerializeField] AbsorptionField_NEW field;

    [Tooltip("Leave empty to find it in the scene. Draws the continuum with the lines " +
             "cut into it.")]
    [SerializeField] SpectrumHUD_NEW hud;

    [Tooltip("Root of the bar, switched on when D2 opens. Leave empty to use the HUD's " +
             "own GameObject.\n\n" +
             "GDD §7 puts the spectrum bar's first appearance at D2 — it is the first " +
             "and only time the piece adds a HUD element the player has to read, so it " +
             "must not be sitting on screen through Phases 0 to 2.")]
    [SerializeField] GameObject barRoot;

    [Header("Lines")]
    [Tooltip("Where each line lands, as a fraction of the spectrum. Consumed in order: " +
             "the first is D2's single hydrogen line, the rest are D4's group.\n\n" +
             "Authored rather than random so every run is identical — see the class " +
             "summary.\n\n" +
             "THE FIRST ONE HAS TO SIT ON THE BRIGHT PART OF THE CURVE. Absorption is " +
             "drawn by taking height away, so a line can only be as visible as whatever " +
             "it is cutting into. The continuum blueward of the peak is 0.18 of full " +
             "height at most; the peak itself is 1.0. A line at 0.30 removes 80% of a " +
             "curve that is eleven pixels tall and reads as nothing at all — which is " +
             "exactly what D2 must not do, since it is the frame where the player has to " +
             "see that the colour they lost IS that black line.\n\n" +
             "So 0.412 sits on the blue flank of the Ly-alpha peak (0.42 in SP_Tutorial), " +
             "where the curve is still about 85% of full height. Still blueward, so it is " +
             "a real forest absorber and not an invention. The rest are further out in " +
             "the forest, where D4 wants them.\n\n" +
             "Move the peak in the profile and this first value has to move with it.")]
    [SerializeField] float[] linePositions = { 0.412f, 0.30f, 0.225f, 0.355f, 0.15f };

    [Tooltip("How dark each line is cut, 0 to 1. Deeper than the journey's forest on " +
             "purpose: D2 has exactly one line and it has to be unmissable on a curved " +
             "display seen from standing distance.")]
    [Range(0f, 1f)]
    [SerializeField] float lineDepth = 0.8f;

    [Tooltip("Line width as a fraction of the spectrum. The journey's forest runs " +
             "0.0039 to 0.0117; D2's line is wider so it reads as an event rather than " +
             "as one more member of a forest that does not exist yet.")]
    [SerializeField] float lineWidth = 0.009f;

    [Tooltip("Let the field's spawn pulse run, so a new line arrives bold and settles. " +
             "This is the feedback that says a line was just cut rather than always " +
             "having been there, so D2 wants it.")]
    [SerializeField] bool pulseOnCut = true;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    /// <summary>How many of linePositions have been used since the last Clear.</summary>
    int _cut;

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Lines cut so far. Read by the debug overlay and by D4's beat.</summary>
    public int LinesCut { get { return _cut; } }

    /// <summary>How many lines are authored. D4 cannot ask for more than this.</summary>
    public int LineCapacity { get { return linePositions != null ? linePositions.Length : 0; } }

    /// <summary>True while the bar is on screen.</summary>
    public bool IsVisible
    {
        get { return barRoot != null && barRoot.activeSelf; }
    }

    void Awake()
    {
        if (field == null) field = FindObjectOfType<AbsorptionField_NEW>();
        if (hud == null) hud = FindObjectOfType<SpectrumHUD_NEW>();

        if (barRoot == null && hud != null) barRoot = hud.gameObject;

        if (field == null)
            Debug.LogError("[TutorialSpectrum_NEW] No AbsorptionField_NEW in the scene. " +
                           "D2 cannot cut a line and the whole of Phase 3 is inert.", this);

        if (hud == null)
            Debug.LogWarning("[TutorialSpectrum_NEW] No SpectrumHUD_NEW in the scene. " +
                             "Lines will be cut into the field and reach the photon trail, " +
                             "but nothing draws the bar.", this);

        Configure();
        WarnIfProfileSpawns();

        // Nothing is on screen before D2. Hide first, then empty, so a scene saved with
        // the bar visible does not flash it for a frame on load.
        Hide();
        Clear();
    }

    /// <summary>
    /// Push the tutorial's profile into the field and the HUD.
    ///
    /// Both take the same asset, which is the point: the continuum the HUD draws and
    /// the lines the field stamps come from one set of numbers, so they cannot drift
    /// apart the way they did when the old stack handed absorption settings to the HUD,
    /// which does not generate absorption lines.
    /// </summary>
    public void Configure()
    {
        if (profile == null)
        {
            Debug.LogWarning("[TutorialSpectrum_NEW] No SpectrumProfile_NEW assigned. The " +
                             "field and the HUD keep whatever they were built with, which " +
                             "on a journey profile means a forest spawning under D2's line.",
                             this);
            return;
        }

        if (field != null) field.Configure(profile);
        if (hud != null) hud.Configure(profile);

        if (debugLog)
            Debug.Log("[TutorialSpectrum_NEW] Configured from " + profile.name + ".", this);
    }

    /// <summary>
    /// Start the spectrum drifting. D3.
    ///
    /// The whole picture slides towards the red end: the absorption buffer shifts its
    /// lines and the HUD shifts the continuum template, both off the same
    /// driftPerSecond, so the curve and the marks it carries move together rather than
    /// sliding past each other.
    ///
    /// WHAT IT IS FOR. A spectrum that never moves reads as a picture of a spectrum. One
    /// that drifts reads as a live measurement of something, which is what it is — and
    /// D2 has just told the player that the marks on it are theirs. This is the frame
    /// where the bar stops being an illustration and becomes an instrument.
    ///
    /// The rate is the journey's, so the tutorial hands over to a spectrum moving at the
    /// speed the player has already learned.
    ///
    /// Note that the field drifts on scaled time, so it slows with D2's slow motion and
    /// resumes with it. That is right: the drift is a property of the world, not of the
    /// readout.
    /// </summary>
    public void StartDrift()
    {
        if (driftingProfile == null)
        {
            Debug.LogWarning("[TutorialSpectrum_NEW] No drifting profile assigned, so D3 has " +
                             "nothing to switch to and the spectrum stays still.", this);
            return;
        }

        if (field != null) field.Configure(driftingProfile);
        if (hud != null) hud.Configure(driftingProfile);

        if (debugLog)
            Debug.Log("[TutorialSpectrum_NEW] Drift on, from " + driftingProfile.name + ".", this);
    }

    /// <summary>
    /// Empty the spectrum. D2 opens on this: a continuum with nothing cut into it, so
    /// the first line the player sees is the one their own collision made.
    /// </summary>
    public void Clear()
    {
        _cut = 0;

        if (field != null) field.ClearLines();

        if (debugLog) Debug.Log("[TutorialSpectrum_NEW] Cleared.", this);
    }

    /// <summary>
    /// Cut the next authored line. D2 calls this once; D4 calls it per atom passed.
    ///
    /// Beyond the authored list this does nothing rather than wrapping round, because
    /// wrapping would deepen a line that is already there and read as the spectrum
    /// glitching rather than as another absorption.
    /// </summary>
    public void CutLine()
    {
        if (field == null) return;

        if (linePositions == null || _cut >= linePositions.Length)
        {
            Debug.LogWarning("[TutorialSpectrum_NEW] Asked for line " + (_cut + 1) +
                             " but only " + LineCapacity + " are authored. Add positions " +
                             "to linePositions.", this);
            return;
        }

        CutLineAt(linePositions[_cut]);
    }

    /// <summary>
    /// Cut a line at an explicit position, 0 to 1 across the spectrum. For a beat that
    /// wants a specific wavelength rather than the next one in the list.
    /// </summary>
    public void CutLineAt(float uvPosition)
    {
        if (field == null) return;

        field.StampLine(Mathf.Clamp01(uvPosition), lineDepth, lineWidth, pulseOnCut);
        _cut++;

        if (debugLog)
            Debug.Log("[TutorialSpectrum_NEW] Cut line " + _cut + " at " +
                      uvPosition.ToString("F3") + ".", this);
    }

    /// <summary>Bring the bar on screen. D2's onEnter calls this.</summary>
    public void Show()
    {
        if (barRoot != null) barRoot.SetActive(true);
    }

    /// <summary>Take the bar off screen. Only the attract reset needs this.</summary>
    public void Hide()
    {
        if (barRoot != null) barRoot.SetActive(false);
    }

    /// <summary>
    /// Back to the state before D2: no bar, no lines.
    ///
    /// GDD §5's restart has to be total. A spectrum still carrying the last visitor's
    /// lines is exactly the kind of thing that only shows up on the exhibition floor,
    /// hours in, with a queue behind it.
    /// </summary>
    public void ResetForAttract()
    {
        Hide();
        Clear();

        // Back to the still profile too, or the next visitor's D1 opens on a spectrum
        // already drifting — which is a thing D3 is supposed to introduce.
        Configure();
    }

    /// <summary>
    /// Shout if the assigned profile still spawns a background forest.
    ///
    /// Checked at runtime rather than trusted, for the reason recorded in
    /// TutorialZoom_NEW.WarnIfSharingAStick: the way this goes wrong is invisible.
    /// Someone duplicates a journey profile to make the tutorial's one, leaves the
    /// spawn fields alone because they are not obviously about the tutorial, and D2
    /// looks correct for the first second and wrong by the third.
    /// </summary>
    void WarnIfProfileSpawns()
    {
        if (profile == null) return;

        if (profile.spawnRatePerSecond > 0f)
            Debug.LogError("[TutorialSpectrum_NEW] " + profile.name + " spawns " +
                           profile.spawnRatePerSecond.ToString("F1") + " background lines a " +
                           "second, so D2's single hydrogen line will be buried within " +
                           "seconds. Set spawnRatePerSecond to 0 on the tutorial's profile.",
                           this);

        if (profile.initialLineCount > 0)
            Debug.LogError("[TutorialSpectrum_NEW] " + profile.name + " has an " +
                           "initialLineCount of " + profile.initialLineCount + ". The " +
                           "tutorial's spectrum has to arrive empty — D2 is the first line " +
                           "the player has ever seen. Set it to 0.", this);
    }

    // ── Debug overlay ────────────────────────────────────────────────────────

    void OnGUI()
    {
        if (!DebugView_NEW.Overlay) return;

        GUI.Label(DebugOverlayRows_NEW.Row(DebugOverlayRows_NEW.Spectrum),
                  string.Format("SPECTRUM  {0}   lines {1}/{2}   profile {3}",
                                IsVisible ? "visible" : "hidden",
                                _cut, LineCapacity,
                                profile != null ? profile.name : "NONE"));
    }
}

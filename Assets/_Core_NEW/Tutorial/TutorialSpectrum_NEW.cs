using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

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
             "grows a forest under the player's own lines. See the class summary.\n\n" +
             "driftPerSecond is NOT 0 here any more. The spectrum slides redward from the " +
             "moment the bar is on screen, which is what makes it read as a live " +
             "measurement rather than a picture of one — and it is what lets every atom " +
             "cut its own line instead of deepening one shared mark.")]
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

    [Tooltip("How dark EVERY hydrogen absorption cuts, 0 to 1.\n\n" +
             "One number, not two. Every atom is the same atom doing the same thing, and " +
             "the player has to be able to see that: an atom that cut a weaker mark than " +
             "the one before it would be saying the light is running out of something to " +
             "lose, which is not what happens and not what §3 item 3 says.\n\n" +
             "Strong, because each one has to be seen at all — on the bar and on the " +
             "trail at once — and a faint line blinks into nothing.\n\n" +
             "Two atoms that strike before the drift has carried the first mark clear " +
             "stack, and the buffer saturates at 1: a line cannot remove more than all " +
             "of the light at its wavelength.")]
    [Range(0.05f, 1f)]
    [FormerlySerializedAs("firstAbsorbDepth")]
    [SerializeField] float atomAbsorbDepth = 0.7f;

    [Header("Events")]
    [Tooltip("Fires once for every absorption, whatever caused it — D5's beat, each of " +
             "D6's atoms, D8's atom. The impact hangs here: flash, jolt, rumble, the line " +
             "blinking on the light and the bar.\n\n" +
             "Here and not on the atoms, because this is the one place every absorption " +
             "already passes through. Hung on the atoms instead, D5's line — which is cut by " +
             "its beat, not by its atom — would have been the one absorption that did not " +
             "rumble.")]
    [SerializeField] UnityEvent onAbsorbed = new UnityEvent();

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    bool _warnedNoImpact;

    /// <summary>How many of linePositions have been used since the last Clear.</summary>
    int _cut;

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Lines cut so far. Read by the debug overlay and by D4's beat.</summary>
    public int LinesCut { get { return _cut; } }

    /// <summary>
    /// Where hydrogen absorbs, on the 0 (UV) to 1 (IR) axis, before any redshift: the
    /// first entry of linePositions.
    ///
    /// Every atom absorbs here, whenever it is hit — ground-state hydrogen always takes
    /// the same wavelength, Lyman-alpha. That is the whole mechanism of the forest: with
    /// no redshift, more atoms only deepen this one line (D6); once the spectrum drifts
    /// (D7), the earlier lines have moved on and a new atom cuts a fresh line back here
    /// (D8). A list of different positions would teach the wrong thing.
    ///
    /// Where on the axis it sits — the visible band, or inside the UV as the fact check
    /// recommends — is a design decision, and is only this number.
    /// </summary>
    public float RestFramePosition
    {
        get { return linePositions != null && linePositions.Length > 0 ? linePositions[0] : 0.412f; }
    }

    /// <summary>True from the first absorption until the line drifts off the red end.</summary>
    public bool HasTrackedLine { get { return _tracking; } }

    /// <summary>
    /// Where the first absorption line is right now, on the 0 (UV) to 1 (IR) axis —
    /// its rest-frame position plus the redshift drift since. The line indicator reads it.
    ///
    /// Tracked here rather than asked of AbsorptionField_NEW, which exposes the drift
    /// rate but not how far anything has moved. It integrates the same rate on the same
    /// clock the field drifts on (scaled time), so a pause holds it and slow motion
    /// slows it, exactly as they do the line itself.
    /// </summary>
    public float TrackedLinePosition { get { return _trackedPosition; } }

    bool _tracking;
    float _trackedPosition;

    void Update()
    {
        if (!_tracking || field == null) return;

        _trackedPosition += field.CurrentDriftPerSecond * Time.deltaTime;

        // AbsorptionField_NEW drops lines off the red end; the indicator goes with it.
        if (_trackedPosition > 1f) _tracking = false;
    }

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
        _tracking = false;

        if (field != null) field.ClearLines();

        // THE CURVE GOES BACK TOO, not just the lines. The spectrum drifts from the
        // moment the scene loads — that is what makes it read as a live measurement
        // rather than a picture — but the drift only accumulates, so by the time the bar
        // arrives the Ly-alpha peak has slid by however long this visitor spent on
        // Phases 0 to 2. Left alone, the bar would open on a different-looking curve
        // every run, the authored line positions would land somewhere new each time, and
        // there would be nothing to rehearse against. GDD §5's restart has to be total,
        // and that includes the clock the curve is drawn from.
        if (hud != null) hud.ResetRedshift();

        if (debugLog) Debug.Log("[TutorialSpectrum_NEW] Cleared.", this);
    }

    /// <summary>
    /// Cut the next authored line. D2 calls this once; D4 calls it per atom passed.
    ///
    /// Beyond the authored list this does nothing rather than wrapping round, because
    /// wrapping would deepen a line that is already there and read as the spectrum
    /// glitching rather than as another absorption.
    /// </summary>
    /// <summary>
    /// One hydrogen atom absorbs at the rest-frame wavelength. Every atom in the piece
    /// calls this, and every atom gets the same result: a new black line, cut at the
    /// same depth, at the wavelength ground-state hydrogen always takes.
    ///
    /// THE SAME EVENT EVERY TIME, and that is the lesson rather than a simplification.
    /// §3 item 3 is "hitting hydrogen costs you colour", and it is one rule: the tenth
    /// atom does what the first did. An effect that changed with the count would be
    /// teaching that the light gets used up, or that the first collision was special,
    /// and neither is true.
    ///
    /// SO WHY A NEW LINE RATHER THAN A DEEPER ONE. Because the spectrum is always
    /// drifting. The wavelength is the same every time — that is the physics, and the
    /// whole mechanism of the forest — but by the time the next atom arrives, the mark
    /// the last one made has been carried redward and is no longer here. The atom cuts
    /// into clean spectrum and a second line appears beside the first. Run it for long
    /// enough and what accumulates is a forest: a row of lines whose spacing is a record
    /// of how much time and distance passed between the atoms that made them.
    ///
    /// Two atoms close enough together that the drift has not separated them still
    /// stack, which is also correct — that is one absorber twice as thick, and the
    /// buffer saturates because a line cannot take more than all of the light.
    /// </summary>
    public void AbsorbAtRestFrame()
    {
        if (field == null) return;

        field.StampLine(Mathf.Clamp01(RestFramePosition), atomAbsorbDepth, lineWidth, pulseOnCut);
        _cut++;

        // The indicator follows the NEWEST mark, not the first. The newest is the one the
        // player just caused, and it is the one that has to be paired with the collision
        // they have this second watched — the earlier lines have said their piece and are
        // on their way red.
        _tracking = true;
        _trackedPosition = RestFramePosition;

        if (debugLog) Debug.Log("[TutorialSpectrum_NEW] Absorption " + _cut + " at rest frame.", this);

        // No rumble here. Every absorption is caused by an atom touching the light, and the
        // atom rumbles the pad itself — a fraction BEFORE contact, to cover the motor's
        // spin-up. Firing it from here instead put it on whatever frame the absorption was
        // called on, which for D5 was the beat opening half a second after the atom had
        // already hit. See TutorialAtom_NEW.rumbleLeadSeconds.

        // The rest of the impact — the flash and the blinks — hangs on onAbsorbed, and
        // nothing hung there means the scene predates it. Said once, so it is not a mystery.
        if (!_warnedNoImpact && onAbsorbed.GetPersistentEventCount() == 0)
        {
            _warnedNoImpact = true;
            Debug.LogWarning("[TutorialSpectrum_NEW] An absorption happened but onAbsorbed has " +
                             "nothing wired to it, so there is no flash (the pad still " +
                             "rumbles). Run Tools > Journey NEW > Tutorial > Build or Update and " +
                             "SAVE the scene.", this);
        }

        // Last, so everything hung here runs against a line that is already in the buffer
        // — the trail's blink has something to blink.
        onAbsorbed.Invoke();
    }

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
                  string.Format("SPECTRUM  {0}   absorptions {1}   rest {2:F3}   profile {3}",
                                IsVisible ? "visible" : "hidden",
                                _cut, RestFramePosition,
                                profile != null ? profile.name : "NONE"));
    }
}

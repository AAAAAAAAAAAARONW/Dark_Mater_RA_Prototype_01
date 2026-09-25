using UnityEngine;

/// <summary>
/// The control scheme, in one place, for the tutorial AND the journey.
///
/// GDD 4 makes the control scheme the single source of truth and says that if the build
/// disagrees, the build changes. That only stays true if there is exactly one file to
/// change. TutorialInput_NEW made that true for the tutorial; the journey went on
/// reading Input.GetAxis directly in DarkMatterPlayerControllerTest, with its own axis
/// names and its own deadband, so there were two answers to "what does the right stick
/// do" and they were only accidentally the same.
///
/// This file is that one place. TutorialInput_NEW now forwards to it and keeps its
/// public shape, so the tutorial is untouched; JourneyPlayerRig_NEW reads it directly.
///
/// WHAT IS SETTLED:
///
///   R stick  Look. Live from the first frame, never taken away, identical in the
///            tutorial and the journey. There is no move stick - the player never
///            translates under their own control.
///   L stick  Zoom.
///   A        Confirm / recentre / emit. One button, one meaning, everywhere.
///   B        Pause. Introduced in the tutorial, live everywhere after that.
///
/// WHAT THIS FILE DOES NOT OWN: sensitivity, recentre speed, smoothing. Those are feel,
/// they live on the rig, and they must not vary per pad. This file answers only "which
/// axis, which button, which letter is printed on it" - see PadProfile_NEW.
///
/// Mouse is kept alongside the stick so the whole thing is testable at a desk. The
/// combination rule - whichever source is larger in magnitude wins, so only one device
/// drives at a time - is the one the build already used, carried over unchanged.
/// </summary>
public static class InputScheme_NEW
{
    /// <summary>
    /// Which physical stick a control reads. One of them, not both.
    ///
    /// GDD 4: "Stick - Look. The only stick." A build where either stick works is not
    /// that: it is two controls that happen to do the same thing, and the player learns
    /// neither. It also makes the prompt a lie in one direction or the other.
    /// </summary>
    public enum Stick
    {
        /// <summary>The left stick. Zoom. On Xbox this also carries WASD and the arrow keys.</summary>
        Left,

        /// <summary>The right stick. Look. What both the tutorial and the journey use.</summary>
        Right,

        /// <summary>Both. Only for idle detection, where any input should count.</summary>
        Either
    }

    // -- Keyboard stand-ins --------------------------------------------------
    //
    // Not per-pad: a keyboard is a keyboard. Kept so the piece runs at a desk with no
    // controller attached at all, which is how most of it gets built.

    /// <summary>Keyboard stand-in for A.</summary>
    public const KeyCode ConfirmKey = KeyCode.Space;

    /// <summary>Keyboard stand-in for B.</summary>
    public const KeyCode PauseKey = KeyCode.P;

    public const string MouseXAxis = "Mouse X";
    public const string MouseYAxis = "Mouse Y";

    /// <summary>
    /// The Submit axis, accepted as confirm alongside the pad button so Return works.
    /// Read through a try/catch because GetButtonDown throws on an axis that is not
    /// defined, and a scene copied into a test project should cost one input, not the
    /// whole Update.
    /// </summary>
    const string ConfirmSubmitAxis = "Submit";

    // -- Which pad ------------------------------------------------------------

    static PadProfile_NEW _pad;
    static bool _resolved;
    static bool _pinned;

    /// <summary>
    /// The profile in force.
    ///
    /// Reading it resolves once, from the connected pad's reported name. Assigning it
    /// pins the answer and stops detection running again.
    /// </summary>
    public static PadProfile_NEW Pad
    {
        get
        {
            if (!_resolved) Detect();
            return _pad;
        }
        set
        {
            if (value == null) return;
            _pad = value;
            _resolved = true;
            _pinned = true;
        }
    }

    /// <summary>True once something has pinned the profile, so detection will not run again.</summary>
    public static bool IsPinned { get { return _pinned; } }

    /// <summary>
    /// Pin by id, for a serialized field or a command line argument. Unknown ids are
    /// ignored with a warning rather than silently falling back, because a typo in an
    /// exhibition build that quietly runs the wrong profile is the failure this whole
    /// file exists to prevent.
    /// </summary>
    public static void Pin(string profileId)
    {
        PadProfile_NEW profile = PadProfile_NEW.ById(profileId);

        if (profile == null)
        {
            Debug.LogWarning("[InputScheme_NEW] No pad profile with id '" + profileId +
                             "'. Leaving the profile as " + Pad.id + ".");
            return;
        }

        Pad = profile;
    }

    /// <summary>
    /// Guess the profile from the connected pad's reported name, once.
    ///
    /// Deliberately not re-run per frame: GetJoystickNames allocates an array of strings.
    /// A pad hot-plugged mid-session is what Cycle and Pin are for.
    ///
    /// AT THE OBSERVATORIES, DO NOT RELY ON THIS. Detection failing on site means the
    /// exhibit is subtly wrong and nobody present can fix it. The build should pin the
    /// Vizlab profile explicitly - see InputProfilePinner_NEW - and leave detection as
    /// the desk convenience it is.
    /// </summary>
    public static void Detect()
    {
        _resolved = true;

        if (_pinned) return;

        _pad = PadProfile_NEW.Fallback;

        string[] names = Input.GetJoystickNames();

        for (int p = 0; p < PadProfile_NEW.All.Length; p++)
        {
            PadProfile_NEW candidate = PadProfile_NEW.All[p];

            for (int n = 0; n < names.Length; n++)
            {
                if (string.IsNullOrEmpty(names[n])) continue;

                string name = names[n].ToLowerInvariant();

                for (int f = 0; f < candidate.detectNameFragments.Length; f++)
                {
                    if (name.Contains(candidate.detectNameFragments[f]))
                    {
                        _pad = candidate;
                        return;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Step to the next profile and pin it. The debug key calls this when the guess is
    /// wrong and there is no time to work out why.
    /// </summary>
    public static void Cycle()
    {
        PadProfile_NEW current = Pad;
        int index = 0;

        for (int i = 0; i < PadProfile_NEW.All.Length; i++)
        {
            if (PadProfile_NEW.All[i] == current) { index = i; break; }
        }

        Pad = PadProfile_NEW.All[(index + 1) % PadProfile_NEW.All.Length];
    }

    /// <summary>
    /// Forget the pin and re-detect. Only for the diagnostic page - nothing in the piece
    /// should be un-pinning a profile somebody chose.
    /// </summary>
    public static void ClearPin()
    {
        _pinned = false;
        _resolved = false;
    }

    // -- Look ----------------------------------------------------------------

    /// <summary>
    /// Horizontal look, in degrees for this frame.
    ///
    /// Stick input is deadbanded, mouse input is not, and whichever source is larger in
    /// magnitude wins so only one device drives at a time.
    ///
    /// Mouse is scaled by the same sensitivity and the same delta time as the stick,
    /// which is what the build does. That makes mouse look frame-rate dependent, and it
    /// is kept rather than corrected: matching the exhibition feel is the point, and a
    /// desk build that handles better than the thing on the wall is the wrong kind of
    /// improvement.
    /// </summary>
    public static float LookX(Stick which, float sensitivity, float deadband, float dt)
    {
        float stick = StickX(which, deadband) * sensitivity * dt;

        // GetAxisRaw sidesteps Unity's own smoothing, which fights a per-frame apply.
        float mouse = Input.GetAxisRaw(MouseXAxis) * sensitivity * dt;

        return Mathf.Abs(stick) > Mathf.Abs(mouse) ? stick : mouse;
    }

    /// <summary>Vertical look, in degrees for this frame. Positive is up.</summary>
    public static float LookY(Stick which, float sensitivity, float deadband, float dt)
    {
        float stick = StickY(which, deadband) * sensitivity * dt;
        float mouse = Input.GetAxisRaw(MouseYAxis) * sensitivity * dt;

        return Mathf.Abs(stick) > Mathf.Abs(mouse) ? stick : mouse;
    }

    /// <summary>
    /// How hard the stick is being pushed, 0 to about 1, deadbanded.
    ///
    /// Separate from LookX/LookY because "is the player actively looking?" is a different
    /// question from "how far should the view move this frame?", and answering the first
    /// with the second is what broke the tutorial's recentre: a per-frame degree delta
    /// divided by delta time reads mouse sensor noise as a deliberate 30 degrees per
    /// second, so the recentre cancelled itself on the frame it started. This reads the
    /// device, not the result.
    /// </summary>
    public static float StickDeflection(Stick which, float deadband)
    {
        return Mathf.Max(Mathf.Abs(StickX(which, deadband)), Mathf.Abs(StickY(which, deadband)));
    }

    /// <summary>
    /// Raw mouse movement this frame, in mouse units. Not deadbanded and not scaled by
    /// time - callers wanting "did the player deliberately move the mouse" should compare
    /// this against a threshold of a unit or two, well above sensor noise.
    /// </summary>
    public static float MouseDeflection()
    {
        return Mathf.Max(Mathf.Abs(Input.GetAxisRaw(MouseXAxis)),
                         Mathf.Abs(Input.GetAxisRaw(MouseYAxis)));
    }

    /// <summary>Horizontal deflection of one stick, deadbanded. Raw, not scaled.</summary>
    public static float StickX(Stick which, float deadband)
    {
        PadProfile_NEW pad = Pad;

        if (which == Stick.Left) return Deadband(Axis(pad.leftStickXAxis), deadband);
        if (which == Stick.Right) return Deadband(Axis(pad.rightStickXAxis), deadband);

        return Larger(Deadband(Axis(pad.leftStickXAxis), deadband),
                      Deadband(Axis(pad.rightStickXAxis), deadband));
    }

    /// <summary>Vertical deflection of one stick, deadbanded. Positive is away from the player.</summary>
    public static float StickY(Stick which, float deadband)
    {
        PadProfile_NEW pad = Pad;

        if (which == Stick.Left) return Deadband(Signed(pad.leftStickYAxis, pad.leftStickYInvert), deadband);
        if (which == Stick.Right) return Deadband(Signed(pad.rightStickYAxis, pad.rightStickYInvert), deadband);

        return Larger(Deadband(Signed(pad.leftStickYAxis, pad.leftStickYInvert), deadband),
                      Deadband(Signed(pad.rightStickYAxis, pad.rightStickYInvert), deadband));
    }

    /// <summary>
    /// The D-pad, for attendant-only controls.
    ///
    /// Exposed rather than left to a caller's Input.GetAxis("Vertical") because on the
    /// Vizlab pad those two names ARE the D-pad, and a caller that does not know which
    /// pad it is on cannot get this right. Returns 0 on a pad that reports its D-pad as
    /// buttons.
    /// </summary>
    public static float DPadX() { return Axis(Pad.dpadXAxis); }

    /// <summary>The D-pad's vertical axis. See <see cref="DPadX"/>.</summary>
    public static float DPadY() { return Axis(Pad.dpadYAxis); }

    /// <summary>The deadband this pad wants, for callers that have no reason to override it.</summary>
    public static float Deadband() { return Pad.stickDeadband; }

    static float Signed(string axis, bool invert)
    {
        float raw = Axis(axis);
        return invert ? -raw : raw;
    }

    static float Larger(float a, float b)
    {
        return Mathf.Abs(a) >= Mathf.Abs(b) ? a : b;
    }

    static float Deadband(float raw, float deadband)
    {
        return Mathf.Abs(raw) > deadband ? raw : 0f;
    }

    /// <summary>
    /// Read an axis, returning 0 rather than throwing if it is not defined.
    ///
    /// GetAxis throws ArgumentException on an unknown axis name. Every axis named by a
    /// profile is defined in this project, but a profile is data and a typo in it should
    /// cost one control, not every Update in the scene. An empty name is a profile
    /// saying "this pad has no such axis" and is not an error.
    /// </summary>
    static float Axis(string name)
    {
        if (string.IsNullOrEmpty(name)) return 0f;

        try { return Input.GetAxis(name); }
        catch (System.ArgumentException) { return 0f; }
    }

    // -- Buttons -------------------------------------------------------------

    /// <summary>A, pressed this frame. Confirm, recentre and emit are all this.</summary>
    public static bool ConfirmDown()
    {
        if (Input.GetKeyDown(ConfirmKey)) return true;

        PadProfile_NEW pad = Pad;

        if (pad.confirmButton != KeyCode.None && Input.GetKeyDown(pad.confirmButton)) return true;
        if (pad.confirmAltButton != KeyCode.None && Input.GetKeyDown(pad.confirmAltButton)) return true;

        try { return Input.GetButtonDown(ConfirmSubmitAxis); }
        catch (System.ArgumentException) { return false; }
    }

    /// <summary>B, pressed this frame. Pause.</summary>
    public static bool PauseDown()
    {
        if (Input.GetKeyDown(PauseKey)) return true;

        PadProfile_NEW pad = Pad;

        return pad.pauseButton != KeyCode.None && Input.GetKeyDown(pad.pauseButton);
    }

    /// <summary>The letter printed on this pad for confirm, for a prompt to draw.</summary>
    public static string ConfirmGlyph() { return Pad.confirmGlyph; }

    /// <summary>The letter printed on this pad for pause, for a prompt to draw.</summary>
    public static string PauseGlyph() { return Pad.pauseGlyph; }

    // -- Idle detection ------------------------------------------------------

    /// <summary>
    /// Any deliberate input this frame. Used by the 90 second idle return to attract
    /// (GDD 5). Stick motion counts only past the deadband, so a pad resting on a plinth
    /// at the Observatories does not hold the piece open all afternoon.
    /// </summary>
    public static bool AnyInput(float deadband)
    {
        if (Input.anyKeyDown) return true;

        // Either, deliberately: a visitor pushing the stick that does not look is still
        // a visitor who is present, and the piece should not reset under them.
        if (StickDeflection(Stick.Either, deadband) > 0f) return true;

        if (Mathf.Abs(Input.GetAxisRaw(MouseXAxis)) > 0.01f) return true;
        if (Mathf.Abs(Input.GetAxisRaw(MouseYAxis)) > 0.01f) return true;

        return false;
    }
}

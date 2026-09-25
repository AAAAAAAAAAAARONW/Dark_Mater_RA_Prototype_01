using UnityEngine;

/// <summary>
/// The tutorial's view of the control scheme. A FORWARDER - the scheme itself now lives
/// in InputScheme_NEW, one directory up, and the journey reads the same file.
///
/// WHY IT STILL EXISTS. Two reasons, both about not breaking things that already work:
///
///   1. LookStick is a SERIALIZED ENUM. FirstPersonLookRig_NEW stores it, and Unity
///      serializes an enum by the field's name and an int. Deleting the type would not
///      lose the value, but every scene and every script that names it would have to
///      change in the same commit, and TutorialSceneBuilder_NEW writes it by name.
///   2. Nine tutorial files call into here. A forwarder makes the move a no-op for all
///      of them, which is what lets the journey start using the scheme today instead of
///      after a nine-file refactor lands and settles.
///
/// WHAT MOVED AND WHY. The tutorial's input file was already the single source of truth
/// for the tutorial, and that was the right idea - but the journey never read it. The
/// journey's controller (DarkMatterPlayerControllerTest) called Input.GetAxis directly
/// with its own axis names, so there were two answers to "what does the right stick do"
/// and they agreed only by coincidence. The exhibition pad broke the coincidence: see
/// PadProfile_NEW.Vizlab.
///
/// NOTHING HERE DECIDES ANYTHING ANY MORE. Every member below either forwards or is a
/// constant kept for source compatibility. New code - including new tutorial code -
/// should call InputScheme_NEW directly.
/// </summary>
public static class TutorialInput_NEW
{
    /// <summary>
    /// Which stick looks. One of them, not both. Serialized on FirstPersonLookRig_NEW.
    ///
    /// Kept in lockstep with InputScheme_NEW.Stick, value for value, so the cast between
    /// them is safe. They are two names for one idea and would be one type if this one
    /// were not already saved into scenes.
    /// </summary>
    public enum LookStick
    {
        /// <summary>The left stick. Zoom.</summary>
        Left = 0,

        /// <summary>The right stick. Look. What the journey uses.</summary>
        Right = 1,

        /// <summary>Both. Only for idle detection.</summary>
        Either = 2
    }

    /// <summary>
    /// Which physical pad the axis and button numbers are being read for.
    ///
    /// Now a thin view onto PadProfile_NEW, which is where a pad is actually described.
    /// Vizlab was added when the Carnegie Observatories pad turned out to be a Logitech
    /// that disagrees with Xbox on nearly every axis.
    /// </summary>
    public enum PadLayout
    {
        /// <summary>XInput numbering. The fallback, and what the piece was tuned on.</summary>
        Xbox = 0,

        /// <summary>DirectInput numbering, as a DualSense reports on Windows. Desk only.</summary>
        PlayStation = 1,

        /// <summary>The Logitech pad in the Vizlab. THE EXHIBITION PAD.</summary>
        Vizlab = 2
    }

    // -- Constants kept for source compatibility ------------------------------
    //
    // These were the Xbox axis and button names, read directly by this file back when it
    // chose them. Nothing here chooses any more - PadProfile_NEW does, per pad - but the
    // names are referenced from comments and the odd test, and removing them buys
    // nothing. Treat them as documentation of the Xbox profile, not as the answer.

    public const string LeftStickXAxis = "Horizontal";
    public const string LeftStickYAxis = "Vertical";
    public const string RightStickXAxis = "RightStickX";
    public const string RightStickYAxis = "RightStickY";
    public const string MouseXAxis = InputScheme_NEW.MouseXAxis;
    public const string MouseYAxis = InputScheme_NEW.MouseYAxis;
    public const string ConfirmButton = "Submit";

    /// <summary>Keyboard stand-in for A, so the tutorial is playable without a pad.</summary>
    public const KeyCode ConfirmKey = InputScheme_NEW.ConfirmKey;

    /// <summary>Gamepad A under XInput. On the Vizlab pad the button PRINTED A sends button 1.</summary>
    public const KeyCode ConfirmPadButton = KeyCode.JoystickButton0;

    /// <summary>Xbox B, as an axis name. Kept for reference; the profile holds the real answer.</summary>
    public const string PauseButton = "Fire2";

    /// <summary>Triangle on a PlayStation pad under DirectInput.</summary>
    public const KeyCode PausePlayStationButton = KeyCode.JoystickButton3;

    /// <summary>Keyboard stand-in for B.</summary>
    public const KeyCode PauseKey = InputScheme_NEW.PauseKey;

    /// <summary>Right stick on a DirectInput pad. Axis 3, where the Xbox one is axis 4.</summary>
    public const string PadRightStickXAxis = "PadRightStickX";

    /// <summary>Right stick on a DirectInput pad. Axis 6 - axis 5 is R2.</summary>
    public const string PadRightStickYAxis = "PadRightStickY";

    /// <summary>Cross on a DualSense. Accepted alongside ConfirmPadButton, never instead.</summary>
    public const KeyCode ConfirmPadCrossButton = KeyCode.JoystickButton1;

    // -- Pad ------------------------------------------------------------------

    /// <summary>
    /// The layout in force. Reading it resolves once from the connected pad's name;
    /// assigning it pins the answer, which is what the director's debug toggle does when
    /// the guess is wrong.
    /// </summary>
    public static PadLayout Pad
    {
        get { return ToLayout(InputScheme_NEW.Pad); }
        set { InputScheme_NEW.Pad = ToProfile(value); }
    }

    /// <summary>Guess the layout from the connected pad's reported name, once.</summary>
    public static void DetectPad() { InputScheme_NEW.Detect(); }

    /// <summary>
    /// Step to the next layout and pin it. Three values now, not two - the director's
    /// F3 cycles Vizlab, PlayStation, Xbox rather than flipping between a pair.
    /// </summary>
    public static void TogglePad() { InputScheme_NEW.Cycle(); }

    static PadLayout ToLayout(PadProfile_NEW profile)
    {
        if (profile == PadProfile_NEW.PlayStation) return PadLayout.PlayStation;
        if (profile == PadProfile_NEW.Vizlab) return PadLayout.Vizlab;
        return PadLayout.Xbox;
    }

    static PadProfile_NEW ToProfile(PadLayout layout)
    {
        if (layout == PadLayout.PlayStation) return PadProfile_NEW.PlayStation;
        if (layout == PadLayout.Vizlab) return PadProfile_NEW.Vizlab;
        return PadProfile_NEW.Xbox;
    }

    static InputScheme_NEW.Stick ToStick(LookStick which)
    {
        return (InputScheme_NEW.Stick)(int)which;
    }

    // -- Look -----------------------------------------------------------------

    /// <summary>Horizontal look, in degrees for this frame.</summary>
    public static float LookX(LookStick which, float sensitivity, float deadband, float dt)
    {
        return InputScheme_NEW.LookX(ToStick(which), sensitivity, deadband, dt);
    }

    /// <summary>Vertical look, in degrees for this frame. Positive is up.</summary>
    public static float LookY(LookStick which, float sensitivity, float deadband, float dt)
    {
        return InputScheme_NEW.LookY(ToStick(which), sensitivity, deadband, dt);
    }

    /// <summary>How hard the stick is being pushed, 0 to about 1, deadbanded.</summary>
    public static float StickDeflection(LookStick which, float deadband)
    {
        return InputScheme_NEW.StickDeflection(ToStick(which), deadband);
    }

    /// <summary>Raw mouse movement this frame, in mouse units.</summary>
    public static float MouseDeflection() { return InputScheme_NEW.MouseDeflection(); }

    /// <summary>Horizontal deflection of one stick, deadbanded. Raw, not scaled.</summary>
    public static float StickX(LookStick which, float deadband)
    {
        return InputScheme_NEW.StickX(ToStick(which), deadband);
    }

    /// <summary>Vertical deflection of one stick, deadbanded. TutorialZoom_NEW reads this.</summary>
    public static float StickY(LookStick which, float deadband)
    {
        return InputScheme_NEW.StickY(ToStick(which), deadband);
    }

    // -- Buttons --------------------------------------------------------------

    /// <summary>A, pressed this frame. Confirm, recentre and emit are all this.</summary>
    public static bool ConfirmDown() { return InputScheme_NEW.ConfirmDown(); }

    /// <summary>Pause, pressed this frame.</summary>
    public static bool PauseDown() { return InputScheme_NEW.PauseDown(); }

    // -- Idle detection -------------------------------------------------------

    /// <summary>Any deliberate input this frame. Used by the 90 second idle return.</summary>
    public static bool AnyInput(float deadband) { return InputScheme_NEW.AnyInput(deadband); }
}

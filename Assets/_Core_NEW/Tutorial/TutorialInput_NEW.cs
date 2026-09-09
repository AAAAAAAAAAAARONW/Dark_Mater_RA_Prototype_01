using UnityEngine;

/// <summary>
/// The tutorial control scheme, in one place.
///
/// GDD §4 makes the control scheme the single source of truth and says that if the build
/// disagrees, the build changes. That only stays true if there is exactly one file to
/// change. Every tutorial script reads input through here and never calls Input directly.
///
/// What is settled:
///
///   R stick  Look. Live from the first frame, never taken away, carries into the
///            journey unchanged. There is no move stick — the player never translates
///            under their own control, which is what removes the "why is nothing
///            happening" read from the May 2026 playtest by design rather than by text.
///   L stick  Zoom. A DEPARTURE from GDD §4, which says "Stick. Look. The only stick."
///            Added because Phase 0 is twenty seconds of being told you can look around
///            in a scene whose only subject is a distant bright point. See
///            TutorialZoom_NEW. Nothing reads both sticks, so the two controls stay
///            separable if this is reverted.
///   A        Confirm / recentre / emit. One button, one meaning, everywhere.
///
///   B        Inspect. D5 only. Read through the Fire2 axis rather than a key code —
///            see InspectButton for why that distinction matters on a PlayStation pad.
///
/// GDD §10 item 1 listed the three face buttons as blocked. An audit of the build
/// settled it: A is Fire1 and taken, X is Fire3 and shows the journey HUD, and B and Y
/// are free. B is inspect. Confirm it on a running pad before D5's prompt copy is
/// final — the audit covered C# source, and a binding made through a UnityEvent in a
/// prefab would not appear in it. The director's overlay prints which buttons the pad
/// is sending, for exactly that check.
///
/// Mouse is kept alongside the stick because the whole thing has to be testable at a
/// desk. The combination rule — whichever source is larger in magnitude wins, so only
/// one device drives at a time — is copied from OrbitCameraRig_NEW so the tutorial and
/// the journey feel the same in the hand.
/// </summary>
public static class TutorialInput_NEW
{
    // ── Axis and button names ────────────────────────────────────────────────
    // These match the axes already defined in ProjectSettings/InputManager.asset.
    // Nothing here invents a new axis; adding one means editing that asset too.

    /// <summary>
    /// Which stick looks. One of them, not both.
    ///
    /// GDD §4: "Stick — Look. The only stick." A build where either stick works is not
    /// that: it is two controls that happen to do the same thing, and the player learns
    /// neither. It also makes the prompt a lie in one direction or the other.
    ///
    /// RIGHT is the default, and the answer for this project: it is what PlaytestBuild
    /// uses, so the tutorial and the journey agree, and the left stick is free for the
    /// zoom. The storyboard's prompt art says LEFT STICK; the build was the tie-breaker
    /// and every prompt string was changed to match.
    ///
    /// CHANGING THIS DOES NOT REACH SCENES THAT ALREADY EXIST. It is a serialized field,
    /// so a scene keeps whatever was saved into it and a new default is invisible to it —
    /// which is exactly how look and zoom once ended up sharing the left stick with the
    /// right stick doing nothing. TutorialSceneBuilder_NEW.SeparateTheSticks repairs that
    /// case; anything else needs its own migration.
    /// </summary>
    public enum LookStick
    {
        /// <summary>Horizontal / Vertical. Joystick axes 1 and 2. Also WASD and arrows.</summary>
        Left,

        /// <summary>RightStickX / RightStickY. Joystick axes 4 and 5. What the journey uses.</summary>
        Right,

        /// <summary>Both. Only for idle detection, where any input should count.</summary>
        Either
    }

    // Horizontal/Vertical also carry WASD and the arrow keys, which is why the tutorial
    // is playable at a desk.

    public const string LeftStickXAxis = "Horizontal";
    public const string LeftStickYAxis = "Vertical";
    public const string RightStickXAxis = "RightStickX";
    public const string RightStickYAxis = "RightStickY";
    public const string MouseXAxis = "Mouse X";
    public const string MouseYAxis = "Mouse Y";
    public const string ConfirmButton = "Submit";

    /// <summary>Keyboard stand-in for A, so the tutorial is playable without a pad.</summary>
    public const KeyCode ConfirmKey = KeyCode.Space;

    /// <summary>Gamepad A on every pad Unity's legacy input maps.</summary>
    public const KeyCode ConfirmPadButton = KeyCode.JoystickButton0;

    /// <summary>
    /// Inspect — D5. B on the pad, E on the keyboard.
    ///
    /// RESOLVED, and read through an axis name rather than a key code. Joystick button 1
    /// is B on an Xbox pad and Cross on a PlayStation one, and ConfirmDown already
    /// accepts Cross so that A keeps working when the layout detection guesses wrong. A
    /// raw KeyCode.JoystickButton1 here would therefore make one button both confirm and
    /// inspect on a PlayStation pad. Fire2 is bound to joystick button 1 in
    /// InputManager.asset and carries no such double meaning.
    ///
    /// Xbox is the only supported target; the PlayStation layout is a development
    /// convenience. See §12 for the audit that freed this button.
    /// </summary>
    public const string InspectButton = "Fire2";

    /// <summary>Keyboard stand-in for B, so D5 is playable without a pad.</summary>
    public const KeyCode InspectKey = KeyCode.E;

    /// <summary>
    /// False since the face-button audit: inspect is B, and D5's prompt copy can be
    /// written. Kept as a constant because callers were told to check it before drawing
    /// a label they could not write.
    /// </summary>
    public const bool InspectIsPlaceholder = false;

    // ── Pad layout ───────────────────────────────────────────────────────────

    /// <summary>
    /// Which physical pad the axis and button numbers are being read for.
    ///
    /// DEBUG ONLY. The exhibition pad is the Xbox-layout one the journey was tuned on,
    /// and Xbox is the default here and stays the default. This exists so the tutorial
    /// can be driven from whatever pad is on the desk during development, which on this
    /// project is usually a DualSense.
    ///
    /// Unity's legacy Input Manager has no notion of a pad model: it numbers axes and
    /// buttons by hardware index, and a PlayStation pad on Windows reports through
    /// DirectInput with a different numbering from an XInput pad. Two things move:
    ///
    ///   Right stick  XInput is axes 4 and 5, which is what RightStickX/Y read.
    ///                DirectInput is axes 3 and 6, which is what PadRightStickX/Y read.
    ///                Axis 4 on a DualSense is L2, so a build reading the Xbox names on
    ///                a PlayStation pad turns the view when you pull the triggers.
    ///   Confirm      XInput A is button 0. On a DualSense, button 0 is Square and Cross
    ///                is button 1. Both are accepted rather than swapped, because there
    ///                is no beat where Square means anything else and accepting one
    ///                extra button costs nothing — and it keeps the confirm path free of
    ///                the layout, so A still works if the detection guesses wrong.
    ///
    /// Left stick, mouse and keyboard are identical on both, so nothing else moves.
    /// </summary>
    public enum PadLayout
    {
        /// <summary>XInput numbering. The exhibition pad, and the default.</summary>
        Xbox,

        /// <summary>DirectInput numbering, as a DualSense or DualShock reports on Windows.</summary>
        PlayStation
    }

    /// <summary>Right stick on a DirectInput pad. Axis 3, where the Xbox one is axis 4.</summary>
    public const string PadRightStickXAxis = "PadRightStickX";

    /// <summary>Right stick on a DirectInput pad. Axis 6 — axis 5 is R2.</summary>
    public const string PadRightStickYAxis = "PadRightStickY";

    /// <summary>Cross on a DualSense. Accepted alongside ConfirmPadButton, never instead.</summary>
    public const KeyCode ConfirmPadCrossButton = KeyCode.JoystickButton1;

    static PadLayout _pad = PadLayout.Xbox;
    static bool _padDetected;

    /// <summary>
    /// The layout in force. Reading it detects once from the connected pad's name;
    /// assigning it pins the answer and stops the detection running again, which is what
    /// the director's debug toggle does when the guess is wrong.
    /// </summary>
    public static PadLayout Pad
    {
        get
        {
            if (!_padDetected) DetectPad();
            return _pad;
        }
        set
        {
            _pad = value;
            _padDetected = true;
        }
    }

    /// <summary>
    /// Guess the layout from the connected pad's reported name, once.
    ///
    /// Windows names a DualSense "Wireless Controller" and an Xbox pad "Controller
    /// (Xbox ...)", so the test is for the PlayStation names and everything else falls
    /// through to Xbox. Deliberately not re-run per frame: GetJoystickNames allocates an
    /// array of strings, and a pad hot-plugged mid-session is what the toggle is for.
    /// </summary>
    public static void DetectPad()
    {
        _padDetected = true;
        _pad = PadLayout.Xbox;

        string[] names = Input.GetJoystickNames();

        for (int i = 0; i < names.Length; i++)
        {
            if (string.IsNullOrEmpty(names[i])) continue;

            string name = names[i].ToLowerInvariant();

            if (name.Contains("dualsense") || name.Contains("dualshock") ||
                name.Contains("wireless controller") ||
                name.Contains("ps5") || name.Contains("ps4"))
            {
                _pad = PadLayout.PlayStation;
                return;
            }
        }
    }

    /// <summary>Flip the layout by hand. The director's debug key calls this.</summary>
    public static void TogglePad()
    {
        Pad = Pad == PadLayout.Xbox ? PadLayout.PlayStation : PadLayout.Xbox;
    }

    /// <summary>The right stick X axis name for the layout in force.</summary>
    static string RightStickX
    {
        get { return Pad == PadLayout.PlayStation ? PadRightStickXAxis : RightStickXAxis; }
    }

    /// <summary>The right stick Y axis name for the layout in force.</summary>
    static string RightStickY
    {
        get { return Pad == PadLayout.PlayStation ? PadRightStickYAxis : RightStickYAxis; }
    }

    // ── Look ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Horizontal look, in degrees for this frame.
    ///
    /// The combination rule is DarkMatterPlayerControllerTest's, unchanged: stick input
    /// is deadbanded, mouse input is not, and whichever source is larger in magnitude
    /// wins so only one device drives at a time.
    ///
    /// Mouse is scaled by the same sensitivity and the same delta time as the stick,
    /// which is what the build does. That makes mouse look frame-rate dependent, and it
    /// is kept rather than corrected: matching the journey's feel is the point, and a
    /// tutorial that handles better than the thing it teaches is the wrong kind of
    /// improvement.
    /// </summary>
    public static float LookX(LookStick which, float sensitivity, float deadband, float dt)
    {
        float stick = StickX(which, deadband) * sensitivity * dt;

        // GetAxisRaw sidesteps Unity's own smoothing, which fights a per-frame apply.
        float mouse = Input.GetAxisRaw(MouseXAxis) * sensitivity * dt;

        return Mathf.Abs(stick) > Mathf.Abs(mouse) ? stick : mouse;
    }

    /// <summary>Vertical look, in degrees for this frame. Positive is up.</summary>
    public static float LookY(LookStick which, float sensitivity, float deadband, float dt)
    {
        float stick = StickY(which, deadband) * sensitivity * dt;
        float mouse = Input.GetAxisRaw(MouseYAxis) * sensitivity * dt;

        return Mathf.Abs(stick) > Mathf.Abs(mouse) ? stick : mouse;
    }

    /// <summary>
    /// How hard the stick is being pushed, 0 to about 1, deadbanded.
    ///
    /// Separate from LookX/LookY because "is the player actively looking?" is a
    /// different question from "how far should the view move this frame?", and
    /// answering the first with the second is what broke the B4 recentre: a
    /// per-frame degree delta divided by delta time reads mouse sensor noise as a
    /// deliberate 30 degrees per second, so the recentre cancelled itself on the
    /// frame it started. This reads the device, not the result.
    /// </summary>
    public static float StickDeflection(LookStick which, float deadband)
    {
        return Mathf.Max(Mathf.Abs(StickX(which, deadband)), Mathf.Abs(StickY(which, deadband)));
    }

    /// <summary>
    /// Raw mouse movement this frame, in mouse units. Not deadbanded and not scaled
    /// by time — callers wanting "did the player deliberately move the mouse" should
    /// compare this against a threshold of a unit or two, well above sensor noise.
    /// </summary>
    public static float MouseDeflection()
    {
        return Mathf.Max(Mathf.Abs(Input.GetAxisRaw(MouseXAxis)),
                         Mathf.Abs(Input.GetAxisRaw(MouseYAxis)));
    }

    /// <summary>Horizontal deflection of one stick, deadbanded. Raw, not scaled.</summary>
    public static float StickX(LookStick which, float deadband)
    {
        return Stick(which, LeftStickXAxis, RightStickX, deadband);
    }

    /// <summary>Vertical deflection of one stick, deadbanded. TutorialZoom_NEW reads this.</summary>
    public static float StickY(LookStick which, float deadband)
    {
        return Stick(which, LeftStickYAxis, RightStickY, deadband);
    }

    static float Stick(LookStick which, string leftAxis, string rightAxis, float deadband)
    {
        if (which == LookStick.Left) return Deadband(Axis(leftAxis), deadband);
        if (which == LookStick.Right) return Deadband(Axis(rightAxis), deadband);

        float left = Deadband(Axis(leftAxis), deadband);
        float right = Deadband(Axis(rightAxis), deadband);

        return Mathf.Abs(left) >= Mathf.Abs(right) ? left : right;
    }

    static float Deadband(float raw, float deadband)
    {
        return Mathf.Abs(raw) > deadband ? raw : 0f;
    }

    /// <summary>
    /// Read an axis, returning 0 rather than throwing if it is not defined.
    ///
    /// GetAxis throws ArgumentException on an unknown axis name. Every axis used here is
    /// defined in this project, but the tutorial scene gets copied into test projects and
    /// a missing axis should cost one input, not the whole Update.
    /// </summary>
    static float Axis(string name)
    {
        try { return Input.GetAxis(name); }
        catch (System.ArgumentException) { return 0f; }
    }

    // ── Buttons ──────────────────────────────────────────────────────────────

    /// <summary>A, pressed this frame. Confirm, recentre and emit are all this.</summary>
    public static bool ConfirmDown()
    {
        if (Input.GetKeyDown(ConfirmKey)) return true;
        if (Input.GetKeyDown(ConfirmPadButton)) return true;

        // Cross on a DualSense is button 1, not button 0. Accepted unconditionally
        // rather than switched on the layout, so A keeps working on a PlayStation pad
        // even when the detection has guessed Xbox. See PadLayout.
        if (Input.GetKeyDown(ConfirmPadCrossButton)) return true;

        // GetButtonDown throws if the axis is missing from InputManager.asset. Submit is
        // defined in this project, but a scene copied elsewhere should not hard-fail on a
        // tutorial that is otherwise playable from the keyboard.
        try { return Input.GetButtonDown(ConfirmButton); }
        catch (System.ArgumentException) { return false; }
    }

    /// <summary>Inspect, pressed this frame. B on the pad, E at a desk.</summary>
    public static bool InspectDown()
    {
        if (Input.GetKeyDown(InspectKey)) return true;

        // Fire2 is defined in this project, but a scene copied elsewhere should not
        // hard-fail on a tutorial that is otherwise playable from the keyboard. Same
        // reasoning as ConfirmDown.
        try { return Input.GetButtonDown(InspectButton); }
        catch (System.ArgumentException) { return false; }
    }

    // ── Idle detection ───────────────────────────────────────────────────────

    /// <summary>
    /// Any deliberate input this frame. Used by the 90 second idle return to attract
    /// (GDD §5). Stick motion counts only past the deadband, so a pad resting on a
    /// plinth at the Observatories does not hold the tutorial open all afternoon.
    /// </summary>
    public static bool AnyInput(float deadband)
    {
        if (Input.anyKeyDown) return true;

        // Either, deliberately: a visitor pushing the stick that does not look is still
        // a visitor who is present, and the piece should not reset the piece under them.
        if (StickDeflection(LookStick.Either, deadband) > 0f) return true;

        if (Mathf.Abs(Input.GetAxisRaw(MouseXAxis)) > 0.01f) return true;
        if (Mathf.Abs(Input.GetAxisRaw(MouseYAxis)) > 0.01f) return true;

        return false;
    }
}

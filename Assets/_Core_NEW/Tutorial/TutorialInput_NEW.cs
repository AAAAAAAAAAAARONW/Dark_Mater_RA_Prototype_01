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
///   Stick   Look. The only stick. Live from B1, never taken away, carries into the
///           journey unchanged. There is no move stick — the player never translates
///           under their own control, which is what removes the "why is nothing
///           happening" read from the May 2026 playtest by design rather than by text.
///   A       Confirm / recentre / emit. One button, one meaning, everywhere.
///
/// What is not settled: the three face buttons (GDD §10, item 1, blocked on Aaron).
/// D5 needs one of them for inspect. Rather than guess a binding and have it quietly
/// disagree with the build later, InspectDown is wired to a placeholder key and reports
/// InspectIsPlaceholder so callers can refuse to draw a label they cannot write yet.
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
    /// Left is the default because that is what the storyboard's B1 prompt says, and a
    /// prompt reading LEFT STICK while only the right stick turns the view is the worst
    /// of the available wrongs.
    ///
    /// Worth knowing for the handoff: PlaytestBuild puts look on the RIGHT stick
    /// (RightStickX/Y, joystick axes 4 and 5) and uses Vertical for speed. The tutorial
    /// has no speed control so nothing collides, but tutorial and journey currently
    /// disagree about which stick looks. Changing this enum and the prompt text together
    /// is how that gets settled.
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
    /// Placeholder for the inspect action used in D5. Not a decision — see the class
    /// summary and GDD §10. Anything that draws a prompt for this must check
    /// InspectIsPlaceholder first.
    /// </summary>
    public const KeyCode InspectPlaceholderKey = KeyCode.E;

    /// <summary>True until the three face buttons are confirmed against the build.</summary>
    public const bool InspectIsPlaceholder = true;

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

    static float StickX(LookStick which, float deadband)
    {
        return Stick(which, LeftStickXAxis, RightStickXAxis, deadband);
    }

    static float StickY(LookStick which, float deadband)
    {
        return Stick(which, LeftStickYAxis, RightStickYAxis, deadband);
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

        // GetButtonDown throws if the axis is missing from InputManager.asset. Submit is
        // defined in this project, but a scene copied elsewhere should not hard-fail on a
        // tutorial that is otherwise playable from the keyboard.
        try { return Input.GetButtonDown(ConfirmButton); }
        catch (System.ArgumentException) { return false; }
    }

    /// <summary>Inspect, pressed this frame. Placeholder binding — see the summary.</summary>
    public static bool InspectDown()
    {
        return Input.GetKeyDown(InspectPlaceholderKey);
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

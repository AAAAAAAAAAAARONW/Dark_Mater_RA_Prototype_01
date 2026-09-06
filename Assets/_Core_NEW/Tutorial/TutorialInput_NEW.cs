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

    // Both sticks drive look, and this is deliberate.
    //
    // The storyboard labels the B1 prompt LEFT STICK · LOOK, while the axes this project
    // already defines put the right stick on RightStickX/Y (joystick axis 4 and 5) and
    // the left stick on Horizontal/Vertical (axis 1 and 2). GDD §4 says there is only
    // one stick and it looks — it does not say which. Reading both means the exhibition
    // pad works whichever way round its driver reports, a visitor who grabs the wrong
    // stick is not punished for it, and the prompt can say what the storyboard says.
    //
    // Horizontal/Vertical also carry WASD and the arrow keys, which is why the tutorial
    // is playable at a desk. Nothing else in the tutorial reads them, so there is no
    // conflict with the journey, where they are the speed control.

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
    public static float LookX(float sensitivity, float deadband, float dt)
    {
        float stick = Stick(RightStickXAxis, LeftStickXAxis, deadband) * sensitivity * dt;

        // GetAxisRaw sidesteps Unity's own smoothing, which fights a per-frame apply.
        float mouse = Input.GetAxisRaw(MouseXAxis) * sensitivity * dt;

        return Mathf.Abs(stick) > Mathf.Abs(mouse) ? stick : mouse;
    }

    /// <summary>Vertical look, in degrees for this frame. Positive is up.</summary>
    public static float LookY(float sensitivity, float deadband, float dt)
    {
        float stick = Stick(RightStickYAxis, LeftStickYAxis, deadband) * sensitivity * dt;
        float mouse = Input.GetAxisRaw(MouseYAxis) * sensitivity * dt;

        return Mathf.Abs(stick) > Mathf.Abs(mouse) ? stick : mouse;
    }

    /// <summary>Whichever stick is being pushed hardest, deadbanded.</summary>
    static float Stick(string primaryAxis, string secondaryAxis, float deadband)
    {
        float primary = Deadband(Axis(primaryAxis), deadband);
        float secondary = Deadband(Axis(secondaryAxis), deadband);

        return Mathf.Abs(primary) >= Mathf.Abs(secondary) ? primary : secondary;
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

        if (Mathf.Abs(Stick(RightStickXAxis, LeftStickXAxis, deadband)) > 0f) return true;
        if (Mathf.Abs(Stick(RightStickYAxis, LeftStickYAxis, deadband)) > 0f) return true;

        if (Mathf.Abs(Input.GetAxisRaw(MouseXAxis)) > 0.01f) return true;
        if (Mathf.Abs(Input.GetAxisRaw(MouseYAxis)) > 0.01f) return true;

        return false;
    }
}

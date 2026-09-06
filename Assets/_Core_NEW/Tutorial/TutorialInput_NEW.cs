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

    public const string StickXAxis = "RightStickX";
    public const string StickYAxis = "RightStickY";
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
    /// Horizontal look, already deadbanded and scaled. Pass the sensitivity the rig
    /// wants; this returns degrees for this frame, not a raw axis.
    /// </summary>
    public static float LookX(float stickSensitivity, float mouseSensitivity, float deadband, float dt)
    {
        float stick = Deadband(Input.GetAxis(StickXAxis), deadband) * stickSensitivity * dt;

        // GetAxisRaw sidesteps Unity's own smoothing, which fights a per-frame apply.
        float mouse = Input.GetAxisRaw(MouseXAxis) * mouseSensitivity;

        return Mathf.Abs(stick) > Mathf.Abs(mouse) ? stick : mouse;
    }

    /// <summary>Vertical look. Same contract as LookX. Positive is up.</summary>
    public static float LookY(float stickSensitivity, float mouseSensitivity, float deadband, float dt)
    {
        float stick = Deadband(Input.GetAxis(StickYAxis), deadband) * stickSensitivity * dt;
        float mouse = Input.GetAxisRaw(MouseYAxis) * mouseSensitivity;

        return Mathf.Abs(stick) > Mathf.Abs(mouse) ? stick : mouse;
    }

    static float Deadband(float raw, float deadband)
    {
        return Mathf.Abs(raw) > deadband ? raw : 0f;
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
        if (Mathf.Abs(Input.GetAxis(StickXAxis)) > deadband) return true;
        if (Mathf.Abs(Input.GetAxis(StickYAxis)) > deadband) return true;
        if (Mathf.Abs(Input.GetAxisRaw(MouseXAxis)) > 0.01f) return true;
        if (Mathf.Abs(Input.GetAxisRaw(MouseYAxis)) > 0.01f) return true;

        return false;
    }
}

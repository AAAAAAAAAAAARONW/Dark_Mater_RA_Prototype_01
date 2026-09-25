using UnityEngine;

/// <summary>
/// One physical controller, described as data.
///
/// WHY THIS EXISTS. The project is on Unity 2019.4's legacy Input Manager, which has no
/// notion of a pad model: it numbers axes and buttons by hardware index, and two pads
/// that look identical in the hand report completely different numbers. Until now that
/// was handled by a two-value enum and a pair of `if` branches inside the tutorial's
/// input file, which worked for exactly two pads and grew a branch per pad after that.
///
/// The exhibition pad at Carnegie Observatories is a third one - a Logitech, and it
/// disagrees with Xbox on almost every axis. On the current build it does not fail
/// cleanly; it fails SCRAMBLED, which is the worst kind:
///
///     RightStickX reads joystick axis 4, which on the Logitech is the right stick's Y
///     RightStickY reads joystick axis 5, which on the Logitech is the LEFT stick's X
///     Horizontal/Vertical read axes 1 and 2, which on the Logitech is the D-PAD
///
/// So a visitor turns left by pushing the right stick UP, looks up by pushing the LEFT
/// stick sideways, and changes the journey speed with the D-pad. Nothing is dead, so
/// nothing reads as broken - visitors conclude they are holding it wrong.
///
/// WHAT A PROFILE OWNS. Axis names, button codes, the letters printed on the plastic,
/// and the deadband. Not sensitivity, not recentre speed, not anything that is a feel
/// decision - those belong to the rig and must stay identical across pads, or the piece
/// handles differently depending on which controller is plugged in.
///
/// BUTTONS ARE MAPPED BY THE LETTER PRINTED ON THE PAD, NOT BY THE NUMBER IT SENDS.
/// The Logitech's face buttons report one position rotated from Xbox: the button with A
/// printed on it sends button 1 (Xbox's B), the one with B printed on it sends button 2
/// (Xbox's X). The design is authored against Xbox, where A is confirm and B is pause,
/// and a visitor reads the letter, not the number. So on the Logitech profile confirm
/// listens to button 1 and pause listens to button 2, and the HUD still says A and B.
/// The offset happens to be +1 for A and B and 0 for Y, which is why this is a table and
/// not an arithmetic shift.
/// </summary>
public sealed class PadProfile_NEW
{
    // -- Identity ------------------------------------------------------------

    /// <summary>Stable key, used by the pinning API and printed in the debug overlay.</summary>
    public readonly string id;

    /// <summary>What to call it in front of a human.</summary>
    public readonly string displayName;

    /// <summary>
    /// Lower-case fragments matched against Input.GetJoystickNames() during detection.
    /// First profile with a hit wins, so these must not overlap.
    /// </summary>
    public readonly string[] detectNameFragments;

    // -- Sticks --------------------------------------------------------------
    //
    // Named by WHICH PHYSICAL STICK, not by what it does. What each stick does is the
    // rig's decision (look on the right, zoom on the left) and is the same on every pad;
    // where that stick lives in Unity's axis numbering is this profile's decision.

    public readonly string leftStickXAxis;
    public readonly string leftStickYAxis;
    public readonly string rightStickXAxis;
    public readonly string rightStickYAxis;

    /// <summary>
    /// Flip the sign of a stick's Y so that pushing the stick away from the player is
    /// positive, which every consumer here treats as "up".
    ///
    /// Some of the axes this project already defines carry `invert: 1` inside
    /// InputManager.asset (RightStickY and PadRightStickY both do). Those profiles
    /// therefore leave these false - inverting twice is upside down, and the asset is
    /// the harder of the two places to notice. The neutral Joy1..Joy10 axes are raw, so
    /// profiles built on them state the sign here, where it is readable.
    /// </summary>
    public readonly bool leftStickYInvert;
    public readonly bool rightStickYInvert;

    // -- D-pad ---------------------------------------------------------------

    /// <summary>
    /// Where the D-pad lives. Recorded even though no gameplay control reads it, for two
    /// reasons: on the Logitech it collides with Horizontal/Vertical (so anyone adding a
    /// control there can see the collision), and it is the natural home for an
    /// attendant-only control that must never appear in a prompt.
    ///
    /// Empty string means "this pad reports its D-pad as buttons, not axes".
    /// </summary>
    public readonly string dpadXAxis;
    public readonly string dpadYAxis;

    // -- Buttons -------------------------------------------------------------

    /// <summary>Confirm / recentre / emit. One button, one meaning, tutorial and journey alike.</summary>
    public readonly KeyCode confirmButton;

    /// <summary>
    /// A second code also accepted as confirm, or None.
    ///
    /// Exists for the PlayStation pad, where Square is button 0 and Cross is button 1
    /// and guessing wrong makes confirm look broken. Accepting both costs nothing
    /// because no beat gives Square another meaning.
    /// </summary>
    public readonly KeyCode confirmAltButton;

    /// <summary>Pause. Introduced in the tutorial, live everywhere after that.</summary>
    public readonly KeyCode pauseButton;

    /// <summary>
    /// The letter printed on the plastic for confirm, for the HUD to draw.
    ///
    /// This is the whole reason buttons are mapped by letter: the prompt has to name
    /// what the visitor can see in their hands. A prompt that says A while the code
    /// listens to a button labelled something else is a prompt that does not work, and
    /// it fails silently.
    /// </summary>
    public readonly string confirmGlyph;

    /// <summary>The letter printed on the plastic for pause.</summary>
    public readonly string pauseGlyph;

    // -- Feel ----------------------------------------------------------------

    /// <summary>
    /// Stick deadband for this pad.
    ///
    /// Per-pad because it is a property of the hardware, not of the design: a worn
    /// exhibition stick resting at 0.12 will hold the 90-second idle return open all
    /// afternoon, and the next visitor walks up to wherever the last one stopped
    /// instead of to the opening.
    /// </summary>
    public readonly float stickDeadband;

    PadProfile_NEW(
        string id, string displayName, string[] detectNameFragments,
        string leftStickXAxis, string leftStickYAxis, bool leftStickYInvert,
        string rightStickXAxis, string rightStickYAxis, bool rightStickYInvert,
        string dpadXAxis, string dpadYAxis,
        KeyCode confirmButton, KeyCode confirmAltButton, KeyCode pauseButton,
        string confirmGlyph, string pauseGlyph,
        float stickDeadband)
    {
        this.id = id;
        this.displayName = displayName;
        this.detectNameFragments = detectNameFragments;
        this.leftStickXAxis = leftStickXAxis;
        this.leftStickYAxis = leftStickYAxis;
        this.leftStickYInvert = leftStickYInvert;
        this.rightStickXAxis = rightStickXAxis;
        this.rightStickYAxis = rightStickYAxis;
        this.rightStickYInvert = rightStickYInvert;
        this.dpadXAxis = dpadXAxis;
        this.dpadYAxis = dpadYAxis;
        this.confirmButton = confirmButton;
        this.confirmAltButton = confirmAltButton;
        this.pauseButton = pauseButton;
        this.confirmGlyph = confirmGlyph;
        this.pauseGlyph = pauseGlyph;
        this.stickDeadband = stickDeadband;
    }

    // -- The profiles --------------------------------------------------------

    /// <summary>
    /// XInput numbering. What the journey and the tutorial were tuned on, and the
    /// fallback when detection recognises nothing.
    ///
    /// Left stick is Horizontal/Vertical, which also carry WASD and the arrow keys -
    /// that is what makes the piece playable at a desk without a pad.
    /// </summary>
    public static readonly PadProfile_NEW Xbox = new PadProfile_NEW(
        "Xbox", "Xbox / XInput",
        new[] { "xbox", "xinput" },
        "Horizontal", "Vertical", false,
        // RightStickY already carries invert: 1 in InputManager.asset.
        "RightStickX", "RightStickY", false,
        // Xbox reports its D-pad on axes 6 and 7.
        "Joy6", "Joy7",
        KeyCode.JoystickButton0, KeyCode.None, KeyCode.JoystickButton1,
        "A", "B",
        0.1f);

    /// <summary>
    /// DirectInput numbering, as a DualSense or DualShock reports on Windows.
    ///
    /// A development convenience - this pad is not going to the Observatories. Right
    /// stick is axes 3 and 6 because axes 4 and 5 are the triggers, which is why a build
    /// reading the Xbox names on this pad turns the view when you pull L2.
    ///
    /// Triangle is pause because it is the one face button nothing else claims: Square
    /// is button 0 (accepted as confirm), Cross is button 1 (confirm), Circle is button
    /// 2 (X's slot on Xbox, which the journey uses for its HUD).
    /// </summary>
    public static readonly PadProfile_NEW PlayStation = new PadProfile_NEW(
        "PlayStation", "DualSense / DualShock",
        new[] { "dualsense", "dualshock", "wireless controller", "ps5", "ps4" },
        "Horizontal", "Vertical", false,
        // PadRightStickY already carries invert: 1 in InputManager.asset.
        "PadRightStickX", "PadRightStickY", false,
        "", "",
        KeyCode.JoystickButton1, KeyCode.JoystickButton0, KeyCode.JoystickButton3,
        "✕", "△",
        0.1f);

    /// <summary>
    /// The Logitech pad in the Carnegie Observatories Vizlab. THE EXHIBITION PAD.
    ///
    /// Axes, from the mapping taken off the real hardware:
    ///
    ///     right stick   axes 3 and 4   (Xbox puts its right stick on 4 and 5)
    ///     left stick    axes 5 and 6
    ///     D-pad         axes 1 and 2   (Xbox puts its LEFT STICK there)
    ///
    /// That last line is the trap. Horizontal/Vertical are axes 1 and 2, so on this pad
    /// they are the D-pad - and the legacy DarkMatterPlayerControllerTest reads Vertical
    /// to change the journey speed. Anything gameplay-facing that reads Horizontal or
    /// Vertical is, on this pad, wired to the D-pad. The neutral Joy* axes exist so this
    /// profile never has to touch those two names.
    ///
    /// Buttons, by the letter printed on the plastic:
    ///
    ///     A  sends button 1   (Xbox's B)   -> confirm
    ///     B  sends button 2   (Xbox's X)   -> pause
    ///     X  sends button 0   (Xbox's A)   -> unused
    ///     Y  sends button 3   (Xbox's Y)   -> unused
    ///
    /// UNVERIFIED, AND IT MATTERS: rightStickYInvert and leftStickYInvert are set to the
    /// same convention as every other pad here (push away = up), but the Joy* axes are
    /// raw and this pad's sign has not been read off the hardware. If the view goes the
    /// wrong way vertically on site, it is one bool on this line. InputDiagnostics_NEW
    /// shows the live sign, so this is a thirty-second check, not a rebuild.
    /// </summary>
    public static readonly PadProfile_NEW Vizlab = new PadProfile_NEW(
        "Vizlab", "Vizlab Logitech",
        new[] { "logitech", "wingman", "gamepad f310", "f310", "f710" },
        "Joy5", "Joy6", true,
        "Joy3", "Joy4", true,
        "Joy1", "Joy2",
        KeyCode.JoystickButton1, KeyCode.None, KeyCode.JoystickButton2,
        "A", "B",
        0.15f);

    /// <summary>
    /// Every profile, in detection order.
    ///
    /// Vizlab is first because it is the one that has to be right. Xbox last because it
    /// is the fallback, and because "xinput" appears in the names of pads that are
    /// pretending to be an Xbox controller - including, on some drivers, this Logitech.
    /// </summary>
    public static readonly PadProfile_NEW[] All = { Vizlab, PlayStation, Xbox };

    /// <summary>The profile used when detection recognises nothing.</summary>
    public static readonly PadProfile_NEW Fallback = Xbox;

    /// <summary>Look up by <see cref="id"/>, case-insensitively. Null if there is no such profile.</summary>
    public static PadProfile_NEW ById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        for (int i = 0; i < All.Length; i++)
        {
            if (string.Equals(All[i].id, id, System.StringComparison.OrdinalIgnoreCase))
                return All[i];
        }

        return null;
    }

    public override string ToString() { return id; }
}

using System.Text;
using UnityEngine;

/// <summary>
/// A live readout of every joystick axis and button, and of what the active profile
/// makes of them.
///
/// WHAT IT IS FOR. Installing at the Observatories means plugging in a pad nobody here
/// has held and finding out, in the room, whether the mapping is right. Without this the
/// answer costs a Unity install, a rebuild, or a guess. With it the check is: press F4,
/// waggle each stick, press each face button, read the screen.
///
/// The two things it is designed to settle, because they are the two that bit us:
///
///   WHICH AXIS IS WHICH. The Logitech puts its right stick on axes 3 and 4, its left on
///   5 and 6, and its D-pad on 1 and 2 - where Xbox puts its left stick. Reading raw
///   axes 1 to 10 side by side makes that visible in one waggle, and the resolved rows
///   underneath say whether the profile agrees.
///
///   WHICH SIGN IS UP. PadProfile_NEW.Vizlab guesses the vertical sign, because the pad
///   was not in the room when it was written. The resolved LOOK Y row shows the value
///   after the profile's invert, so pushing the stick away from you should read
///   positive. If it reads negative, flip one bool in the profile.
///
/// Drawn with OnGUI on purpose: no canvas, no font asset, no prefab, nothing that can be
/// missing from a scene it gets dropped into at the last minute.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("INPUT DIAG", "#E09E38")]
public class InputDiagnostics_NEW : MonoBehaviour
{
    [Header("Toggle")]
    [Tooltip("Shows and hides the readout. F4 by default - F1 to F3 are already taken by " +
             "the layer jump tool and the tutorial director.")]
    [SerializeField] KeyCode toggleKey = KeyCode.F4;

    [Tooltip("Cycle the pad profile and pin it. Same action as the tutorial director's " +
             "F3, repeated here so the diagnostic page is self-contained in a build " +
             "that has no director.")]
    [SerializeField] KeyCode cycleProfileKey = KeyCode.F5;

    [Tooltip("Visible from the first frame. Leave off for anything a visitor can see.")]
    [SerializeField] bool startVisible = false;

    [Header("Readout")]
    [Tooltip("How many joystick axes to print. The project defines Joy1 to Joy10.")]
    [Range(1, 10)]
    [SerializeField] int axisCount = 10;

    [Tooltip("How many joystick buttons to test. Unity exposes 20 per pad.")]
    [Range(1, 20)]
    [SerializeField] int buttonCount = 12;

    [Tooltip("Treat an axis below this as resting, so a drifting stick does not read as " +
             "held. Display only - it does not affect any control.")]
    [Range(0f, 0.5f)]
    [SerializeField] float restThreshold = 0.05f;

    bool _visible;
    GUIStyle _style;
    readonly StringBuilder _sb = new StringBuilder(1024);

    void Awake()
    {
        _visible = startVisible;
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey)) _visible = !_visible;

        // Cycling is allowed while hidden: somebody who knows the shortcut should not
        // have to show a diagnostic panel to a room full of visitors to use it.
        if (Input.GetKeyDown(cycleProfileKey)) InputScheme_NEW.Cycle();
    }

    void OnGUI()
    {
        if (!_visible) return;

        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label);
            _style.fontSize = 14;
            _style.richText = false;
            _style.wordWrap = false;
            // Monospaced would be better and is not guaranteed to exist; the columns are
            // padded by hand below instead.
            _style.alignment = TextAnchor.UpperLeft;
        }

        PadProfile_NEW pad = InputScheme_NEW.Pad;

        _sb.Length = 0;

        _sb.Append("INPUT DIAGNOSTICS    ").Append(toggleKey).Append(" hide    ")
           .Append(cycleProfileKey).Append(" next profile\n\n");

        _sb.Append("PROFILE   ").Append(pad.displayName)
           .Append(InputScheme_NEW.IsPinned ? "   (pinned)" : "   (detected)").Append('\n');

        string[] names = Input.GetJoystickNames();
        _sb.Append("PADS      ");
        bool anyPad = false;
        for (int i = 0; i < names.Length; i++)
        {
            if (string.IsNullOrEmpty(names[i])) continue;
            if (anyPad) _sb.Append(" | ");
            _sb.Append(names[i]);
            anyPad = true;
        }
        if (!anyPad) _sb.Append("(none connected)");
        _sb.Append("\n\n");

        // -- Raw axes --------------------------------------------------------
        //
        // The whole point: raw, unnamed, in hardware order, so the reader can see which
        // physical stick moves which number without believing anything this code says.

        _sb.Append("RAW AXES\n");
        for (int i = 1; i <= axisCount; i++)
        {
            float v = SafeAxis("Joy" + i);
            _sb.Append("  Joy").Append(i);
            if (i < 10) _sb.Append(' ');
            _sb.Append("  ").Append(Bar(v)).Append("  ").Append(v.ToString("+0.00;-0.00; 0.00"));
            if (Mathf.Abs(v) > restThreshold) _sb.Append("   <-");
            _sb.Append('\n');
        }

        // -- Resolved --------------------------------------------------------
        //
        // What the profile makes of the raw values. If a stick moves a Joy row above and
        // nothing moves here, the profile is pointing at the wrong axis.

        _sb.Append("\nRESOLVED BY PROFILE\n");
        Resolved("LOOK X  (right stick)", InputScheme_NEW.StickX(InputScheme_NEW.Stick.Right, 0f), pad.rightStickXAxis, false);
        Resolved("LOOK Y  (right stick)", InputScheme_NEW.StickY(InputScheme_NEW.Stick.Right, 0f), pad.rightStickYAxis, pad.rightStickYInvert);
        Resolved("ZOOM X  (left stick)", InputScheme_NEW.StickX(InputScheme_NEW.Stick.Left, 0f), pad.leftStickXAxis, false);
        Resolved("ZOOM Y  (left stick)", InputScheme_NEW.StickY(InputScheme_NEW.Stick.Left, 0f), pad.leftStickYAxis, pad.leftStickYInvert);
        Resolved("D-PAD X (attendant)", InputScheme_NEW.DPadX(), pad.dpadXAxis, false);
        Resolved("D-PAD Y (attendant)", InputScheme_NEW.DPadY(), pad.dpadYAxis, false);

        _sb.Append("\n  push a stick AWAY from you: LOOK Y and ZOOM Y should read POSITIVE.\n");
        _sb.Append("  if not, flip the invert flag on this profile in PadProfile_NEW.\n");

        // -- Buttons ---------------------------------------------------------

        _sb.Append("\nBUTTONS HELD   ");
        bool anyButton = false;
        for (int i = 0; i < buttonCount; i++)
        {
            if (!Input.GetKey(KeyCode.JoystickButton0 + i)) continue;
            if (anyButton) _sb.Append(", ");
            _sb.Append(i);
            anyButton = true;
        }
        if (!anyButton) _sb.Append("(none)");
        _sb.Append('\n');

        _sb.Append("  CONFIRM  listens to button ").Append(ButtonNumber(pad.confirmButton));
        if (pad.confirmAltButton != KeyCode.None)
            _sb.Append(" or ").Append(ButtonNumber(pad.confirmAltButton));
        _sb.Append("   prompt draws \"").Append(pad.confirmGlyph).Append('"');
        if (InputScheme_NEW.ConfirmDown()) _sb.Append("   <- DOWN");
        _sb.Append('\n');

        _sb.Append("  PAUSE    listens to button ").Append(ButtonNumber(pad.pauseButton));
        _sb.Append("   prompt draws \"").Append(pad.pauseGlyph).Append('"');
        if (InputScheme_NEW.PauseDown()) _sb.Append("   <- DOWN");
        _sb.Append('\n');

        _sb.Append("\n  press the button PRINTED A: CONFIRM should say DOWN.\n");
        _sb.Append("  press the button PRINTED B: PAUSE should say DOWN.\n");

        // Drawn as one label rather than a scroll view, because the panel has to survive
        // being opened in a build where nothing else about the UI is guaranteed.
        GUI.Label(new Rect(16f, 16f, 760f, Screen.height - 32f), _sb.ToString(), _style);
    }

    void Resolved(string label, float value, string axisName, bool inverted)
    {
        _sb.Append("  ").Append(label).Append("  ").Append(Bar(value)).Append("  ")
           .Append(value.ToString("+0.00;-0.00; 0.00"))
           .Append("   ").Append(string.IsNullOrEmpty(axisName) ? "(unmapped)" : axisName);

        if (inverted) _sb.Append(" inverted");

        _sb.Append('\n');
    }

    /// <summary>A crude eleven-cell meter, so a glance is enough. Centre is rest.</summary>
    static string Bar(float v)
    {
        int cell = Mathf.Clamp(Mathf.RoundToInt(v * 5f) + 5, 0, 10);

        char[] cells = new char[11];
        for (int i = 0; i < 11; i++) cells[i] = i == 5 ? '|' : '.';
        cells[cell] = '#';

        return new string(cells);
    }

    static int ButtonNumber(KeyCode code)
    {
        return code - KeyCode.JoystickButton0;
    }

    /// <summary>
    /// GetAxis throws on an axis that is not defined. The Joy1..Joy10 axes were added to
    /// InputManager.asset alongside this file, but a project that has not imported that
    /// change should see an empty row, not an exception per frame per axis.
    /// </summary>
    static float SafeAxis(string name)
    {
        try { return Input.GetAxis(name); }
        catch (System.ArgumentException) { return 0f; }
    }
}

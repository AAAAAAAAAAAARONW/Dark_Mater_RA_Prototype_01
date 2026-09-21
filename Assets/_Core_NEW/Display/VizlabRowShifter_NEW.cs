using UnityEngine;

/// <summary>
/// Moves a UI element between the display's seven rows at runtime, so the right row can be
/// found by trying it on the wall instead of by guessing in the editor.
///
/// Put it on the same object as a VizlabRowAnchor_NEW - on the LAF HUD, for instance - press
/// up or down, and the element steps a row. It only changes that anchor's Row, so it goes
/// through exactly the same placement path as the editor does; nothing here is a separate
/// runtime layout system.
///
/// BINDINGS, and why these ones. This project is on the legacy Input Manager, where the
/// Vertical and Horizontal axes already drive flight speed and movement, the right stick
/// drives look, and the number keys, T, Z and Space are all taken. So:
///
///   Keyboard      Page Up / Page Down, and Home to go back to the starting row
///   Xbox pad      RB / LB   (right bumper up, left bumper down)
///
/// Buttons 0 to 3 are taken - Fire1, Fire2, Fire3, Jump, Submit and Cancel all map onto the
/// face buttons - but 4 and 5 are bound to nothing, in the Input Manager or in any script.
///
/// The bumpers are joystick buttons 4 and 5 on Windows, which Unity reads through KeyCode
/// with no Input Manager entry at all - so the pad works out of the box.
///
/// THE D-PAD is the more natural control, but on Windows it arrives as a joystick *axis*,
/// not buttons, and legacy Input cannot read an axis that has no Input Manager entry. So it
/// is off by default and opt-in: add an axis, put its name in D Pad Axis, and it starts
/// working. Edit > Project Settings > Input Manager > add an axis named DPadVertical, Type
/// "Joystick Axis", Axis "7th axis", Gravity 0, Dead 0.2, Sensitivity 1. Nothing breaks if
/// you do not - the component probes the axis once and quietly falls back to the bumpers.
///
/// Row 1 is the top of the display and row 7 is the floor, so pressing up lowers the row
/// number. The component works in that direction so the binding matches what you see.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("ROW SHIFT", "#C08A2E")]
public class VizlabRowShifter_NEW : MonoBehaviour
{
    [Header("What to move")]
    [Tooltip("The anchor to drive. Found on this object or its children if left empty.")]
    [SerializeField] VizlabRowAnchor_NEW anchor;

    [Header("Starting row")]
    [Tooltip("The row to snap to when play starts, and the row the reset key returns to. " +
             "0 means leave it alone: the element starts wherever the anchor's own Row is " +
             "set in the scene, and reset goes back to that. Use 0 unless you specifically " +
             "want play mode to start somewhere other than where the scene has it.")]
    [Range(0, VizlabDisplay_NEW.RowCount)]
    [SerializeField] int startRow = 0;

    [Tooltip("Snaps back to the starting row.")]
    [SerializeField] KeyCode resetKey = KeyCode.Home;

    [Tooltip("Optional pad button for the same. None by default - set it to a free button " +
             "such as Joystick Button 9 (right stick click) if you want one.")]
    [SerializeField] KeyCode resetPadButton = KeyCode.None;

    [Header("Keyboard")]
    [SerializeField] KeyCode keyUp = KeyCode.PageUp;
    [SerializeField] KeyCode keyDown = KeyCode.PageDown;

    [Header("Xbox pad")]
    [Tooltip("Right bumper on Windows. Readable with no Input Manager entry.")]
    [SerializeField] KeyCode padUp = KeyCode.JoystickButton5;

    [Tooltip("Left bumper on Windows.")]
    [SerializeField] KeyCode padDown = KeyCode.JoystickButton4;

    [Tooltip("Optional. The name of a Joystick Axis entry for the D-pad's vertical axis. " +
             "Leave empty to use only the bumpers. If the axis does not exist the component " +
             "says so once and carries on with the bumpers.")]
    [SerializeField] string dPadAxis = "";

    [Tooltip("How far the D-pad axis has to move before it counts as a press.")]
    [Range(0.1f, 0.9f)]
    [SerializeField] float dPadThreshold = 0.5f;

    [Header("Behaviour")]
    [Tooltip("On: stepping past row 7 wraps to row 1. Off: it stops at the ends.")]
    [SerializeField] bool wrap = false;

    [Tooltip("Print the new row to the console on every change. Useful during a playtest, " +
             "when the point of moving the HUD is to find out which row reads best.")]
    [SerializeField] bool logChanges = true;

    /// <summary>Null until the axis has been probed; then true if it exists and false if not.</summary>
    bool? _dPadUsable;

    /// <summary>The axis is edge-detected, so holding the D-pad steps one row rather than racing.</summary>
    bool _dPadHeld;

    /// <summary>Where Reset goes back to. Resolved once at Start so it survives shifting.</summary>
    int _homeRow;

    void Awake()
    {
        if (anchor == null) anchor = GetComponentInChildren<VizlabRowAnchor_NEW>();

        if (anchor == null)
        {
            Debug.LogWarning("VizlabRowShifter_NEW on '" + name + "' has no VizlabRowAnchor_NEW " +
                             "to drive, so it will do nothing. Add one to the element you want " +
                             "to move.", this);
            enabled = false;
        }
    }

    void Start()
    {
        // Start, not Awake: the anchor places itself in OnEnable, and the order of that
        // against another component's Awake is not guaranteed. By Start it has settled.
        if (anchor == null) return;

        _homeRow = startRow > 0 ? startRow : anchor.Row;

        if (anchor.Row != _homeRow) anchor.Row = _homeRow;
    }

    void Update()
    {
        if (Input.GetKeyDown(resetKey) || Input.GetKeyDown(resetPadButton))
        {
            ResetToStartRow();
            return;
        }

        int step = ReadStep();
        if (step != 0) Shift(step);
    }

    /// <summary>Back to the starting row. Also callable from a UI button or another script.</summary>
    public void ResetToStartRow()
    {
        if (anchor == null || anchor.Row == _homeRow) return;

        anchor.Row = _homeRow;

        if (logChanges) Debug.Log(anchor.name + " -> row " + _homeRow + " (reset)", anchor);
    }

    /// <summary>-1 to move up the display, +1 to move down, 0 for nothing. Up means a lower row number.</summary>
    int ReadStep()
    {
        if (Input.GetKeyDown(keyUp) || Input.GetKeyDown(padUp)) return -1;
        if (Input.GetKeyDown(keyDown) || Input.GetKeyDown(padDown)) return 1;

        float dPad = ReadDPad();

        if (Mathf.Abs(dPad) < dPadThreshold)
        {
            _dPadHeld = false;
            return 0;
        }

        if (_dPadHeld) return 0;   // already counted this press
        _dPadHeld = true;

        return dPad > 0f ? -1 : 1;
    }

    /// <summary>
    /// The D-pad's vertical axis, or 0 if it is not configured. Legacy Input throws when
    /// asked for an axis that does not exist, so the first read is guarded and the answer
    /// cached - a try/catch every frame would be its own problem.
    /// </summary>
    float ReadDPad()
    {
        if (string.IsNullOrEmpty(dPadAxis)) return 0f;
        if (_dPadUsable == false) return 0f;

        if (_dPadUsable == null)
        {
            try
            {
                float probe = Input.GetAxisRaw(dPadAxis);
                _dPadUsable = true;
                return probe;
            }
            catch (System.Exception)
            {
                _dPadUsable = false;

                Debug.LogWarning("VizlabRowShifter_NEW: no input axis named '" + dPadAxis +
                                 "', so the D-pad is off and the bumpers still work. Add it in " +
                                 "Project Settings > Input Manager as a Joystick Axis on the " +
                                 "7th axis, or clear the D Pad Axis field to stop asking.", this);
                return 0f;
            }
        }

        return Input.GetAxisRaw(dPadAxis);
    }

    /// <summary>Steps the anchor by one row. Negative moves up the display.</summary>
    public void Shift(int step)
    {
        if (anchor == null) return;

        int from = anchor.Row;
        int to = from + step;

        if (wrap)
        {
            // Rows are 1-based, so wrap through a 0-based space and come back.
            to = ((to - 1 + VizlabDisplay_NEW.RowCount) % VizlabDisplay_NEW.RowCount) + 1;
        }
        else
        {
            to = Mathf.Clamp(to, 1, VizlabDisplay_NEW.RowCount);
        }

        if (to == from) return;

        anchor.Row = to;   // the setter re-applies the placement

        if (!logChanges) return;

        VizlabDisplay_NEW.Row data = VizlabDisplay_NEW.GetRow(to);

        Debug.Log(string.Format("{0} -> row {1} (tilt {2:0.#}°{3})",
                                anchor.name, to, data.TiltDeg,
                                to == VizlabDisplay_NEW.SafeAreaRow ? ", safe area"
                                    : VizlabDisplay_NEW.IsPoorPlacement(to) ? ", avoid" : ""),
                  anchor);
    }

    /// <summary>Jumps straight to a row. For a UI button, a debug menu, or another script.</summary>
    public void GoToRow(int rowNumber)
    {
        if (anchor == null) return;
        anchor.Row = Mathf.Clamp(rowNumber, 1, VizlabDisplay_NEW.RowCount);
    }
}

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

    /// <summary>Keyboard stand-in for Y.</summary>
    public const KeyCode ToggleKey = KeyCode.Y;

    public const string MouseXAxis = "Mouse X";
    public const string MouseYAxis = "Mouse Y";

    /// <summary>
    /// The Submit axis, accepted as confirm alongside the pad button so Return works.
    /// Read through a try/catch because GetButtonDown throws on an axis that is not
    /// defined, and a scene copied into a test project should cost one input, not the
    /// whole Update.
    /// </summary>
    const string ConfirmSubmitAxis = "Submit";

    // -- Where input is read from ---------------------------------------------
    //
    // On the Vizlab wall the piece runs as several copies at once, one per display node,
    // and getReal3D keeps them in step. UnityEngine.Input only sees the pad on the master
    // node, so a look rig reading it would turn on the master and stand still on the
    // others, and the wall would tear at the seams. getReal3D.Input reads the master's pad
    // and hands every node the same value on the same frame, which is the only way the
    // copies can stay one picture.
    //
    // It is read by LOGICAL NAME, and the names are getReal3D's own defaults rather than
    // ours: the Vizlab's cluster configuration binds its controller to those names for
    // every getReal3D game, so using them is what makes the site pad work without a
    // mapping of our own. Assets/Resources/mappings.xml fills in the two the defaults
    // leave empty (Pitch's stick, and keyboard keys that match the desk stand-ins).
    //
    // Which pad profile is in force still decides the printed letters, not the axes.

    /// <summary>Where every control below is read from.</summary>
    public enum Source
    {
        /// <summary>UnityEngine.Input, through the pad profile. Desk, Tutorial.unity, the journey.</summary>
        Unity,

        /// <summary>getReal3D.Input, by logical name, synchronised across the cluster. The wall.</summary>
        GetReal3D
    }

    /// <summary>
    /// Set by GetReal3DView_NEW while a getReal3D view is live in the scene, and put back
    /// when it goes. Nothing else should be writing this.
    /// </summary>
    public static Source InputSource { get; set; }

    const string Gr3dLookXAxis = "Yaw";         // right stick X
    const string Gr3dLookYAxis = "Pitch";       // right stick Y; bound in mappings.xml
    const string Gr3dLeftXAxis = "Strafe";      // left stick X
    const string Gr3dLeftYAxis = "Forward";     // left stick Y: zoom
    const string Gr3dConfirmButton = "WandButton";  // A
    const string Gr3dPauseButton = "Jump";          // B
    const string Gr3dToggleButton = "Reset";        // Y
    const string Gr3dShowHudButton = "ChangeWand";  // X; the journey's tracker HUD

    static bool FromGetReal3D { get { return InputSource == Source.GetReal3D; } }

    /// <summary>
    /// True when devices only the master node can see - the mouse, the keyboard through
    /// UnityEngine.Input - must be left alone, because reading them would make the nodes
    /// disagree. A single getReal3D instance (the editor) has nobody to disagree with.
    ///
    /// NOT getReal3D.Cluster.isOn. Its getter is a bare `return true` (read off the IL in
    /// getReal3D.dll 4.1.0.4003), so testing it switched the mouse off in the editor too -
    /// mouse look dead, and the idle return blind to a visitor using the mouse.
    ///
    /// Public for the few readers outside this class that still ask UnityEngine.Input for
    /// something with no logical name - VizlabRowShifter_NEW's bumpers - so they can stand
    /// down on the wall rather than move one node's picture.
    /// </summary>
    public static bool MasterOnlyDevicesUnsafe
    {
        get { return FromGetReal3D && RunningAsSeveralCopies; }
    }

    /// <summary>
    /// Started by the getReal3D launcher (which sets GETREAL_CONFIG - the same test
    /// getReal3D's own VRToolkitChoice uses), a client node, or a config with more than one
    /// machine. The launcher test comes first because the Vizlab's copies may share one
    /// machine, where the node count alone would say one.
    ///
    /// Public because it is also the answer to "is this the wall": GetReal3DHud_NEW moves
    /// the HUD onto the rows on exactly the same test, so input and HUD never disagree.
    /// </summary>
    public static bool RunningAsSeveralCopies
    {
        get
        {
            // Asked several times a frame by the look and idle checks; the native call
            // behind getNodeCount is answered once per frame.
            int frame = Time.frameCount;
            if (frame == _severalCopiesFrame) return _severalCopies;
            _severalCopiesFrame = frame;

            if (LaunchedByGetReal3D || getReal3D.Cluster.isClientAndClusterOn)
                return _severalCopies = true;

            try { _severalCopies = getReal3D.Plugin.getNodeCount() > 1; }
            catch (System.Exception) { _severalCopies = false; }
            return _severalCopies;
        }
    }

    static int _severalCopiesFrame = -1;
    static bool _severalCopies;

    static readonly bool LaunchedByGetReal3D =
        System.Environment.GetEnvironmentVariable("GETREAL_CONFIG") != null;

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

        _pad = Guess() ?? PadProfile_NEW.Fallback;
    }

    /// <summary>
    /// Which profile the connected pad's name points at, or null if no name matches.
    ///
    /// Separate from <see cref="Detect"/>, and changing nothing, so a readout can say what
    /// the hardware looks like NEXT TO what the profile in force actually is. On an
    /// exhibition build those two are allowed to differ - the profile is pinned on purpose -
    /// but a difference nobody chose is the failure this file exists to catch, and it can
    /// only be seen if both halves are on screen.
    /// </summary>
    public static PadProfile_NEW Guess()
    {
        string[] names = PadNames();

        for (int p = 0; p < PadProfile_NEW.All.Length; p++)
        {
            PadProfile_NEW candidate = PadProfile_NEW.All[p];

            for (int n = 0; n < names.Length; n++)
            {
                if (string.IsNullOrEmpty(names[n])) continue;

                string name = names[n].ToLowerInvariant();

                for (int f = 0; f < candidate.detectNameFragments.Length; f++)
                {
                    if (name.Contains(candidate.detectNameFragments[f])) return candidate;
                }
            }
        }

        return null;
    }

    // -- What is plugged in --------------------------------------------------

    static string[] _names;
    static string _namesLine;
    static float _namesReadAt = -99f;

    /// <summary>
    /// Seconds a read of the pad names is reused for. Long enough that a per-frame readout
    /// costs nothing, short enough that plugging a pad in shows up while you are still
    /// standing there.
    /// </summary>
    const float NamesCacheSeconds = 2f;

    /// <summary>
    /// The connected pads' reported names.
    ///
    /// Cached because GetJoystickNames ALLOCATES AN ARRAY OF STRINGS on every call, and
    /// its callers are a debug readout drawn every frame and a profile guess made beside
    /// it. Two allocations per frame for an answer that changes when somebody walks over
    /// with a controller is the kind of cost that never shows up in a profile and never
    /// goes away either.
    /// </summary>
    static string[] PadNames()
    {
        float now = Time.unscaledTime;

        if (_names != null && now - _namesReadAt < NamesCacheSeconds) return _names;

        _namesReadAt = now;
        _names = Input.GetJoystickNames();
        _namesLine = null;

        return _names;
    }

    /// <summary>
    /// The connected pads' reported names, as one line - the model string Windows gives
    /// Unity. This is the ONLY identification the legacy Input Manager offers, and the
    /// thing every profile is matched against, so it is what a readout has to show when
    /// the question is "which pad is this".
    ///
    /// Empty slots are dropped: Unity pads the array out to its maximum length and the
    /// holes say nothing.
    /// </summary>
    public static string ConnectedPadNames()
    {
        string[] names = PadNames();

        if (_namesLine != null) return _namesLine;

        string line = "";
        int found = 0;

        for (int i = 0; i < names.Length; i++)
        {
            if (string.IsNullOrEmpty(names[i])) continue;

            if (found > 0) line += "  |  ";
            line += names[i];
            found++;
        }

        _namesLine = found == 0 ? "none connected" : line;
        return _namesLine;
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
        float mouse = Mouse(MouseXAxis) * sensitivity * dt;

        return Mathf.Abs(stick) > Mathf.Abs(mouse) ? stick : mouse;
    }

    /// <summary>Vertical look, in degrees for this frame. Positive is up.</summary>
    public static float LookY(Stick which, float sensitivity, float deadband, float dt)
    {
        float stick = StickY(which, deadband) * sensitivity * dt;
        float mouse = Mouse(MouseYAxis) * sensitivity * dt;

        return Mathf.Abs(stick) > Mathf.Abs(mouse) ? stick : mouse;
    }

    /// <summary>A mouse axis, or 0 on a cluster, where only the master has a mouse.</summary>
    static float Mouse(string axis)
    {
        return MasterOnlyDevicesUnsafe ? 0f : Input.GetAxisRaw(axis);
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
        return Mathf.Max(Mathf.Abs(Mouse(MouseXAxis)),
                         Mathf.Abs(Mouse(MouseYAxis)));
    }

    /// <summary>Horizontal deflection of one stick, deadbanded. Raw, not scaled.</summary>
    public static float StickX(Stick which, float deadband)
    {
        if (FromGetReal3D)
        {
            float left = Deadband(Gr3dAxis(Gr3dLeftXAxis), deadband);
            float right = Deadband(Gr3dAxis(Gr3dLookXAxis), deadband);

            if (which == Stick.Left) return left;
            if (which == Stick.Right) return right;
            return Larger(left, right);
        }

        PadProfile_NEW pad = Pad;

        if (which == Stick.Left) return Deadband(Axis(pad.leftStickXAxis), deadband);
        if (which == Stick.Right) return Deadband(Axis(pad.rightStickXAxis), deadband);

        return Larger(Deadband(Axis(pad.leftStickXAxis), deadband),
                      Deadband(Axis(pad.rightStickXAxis), deadband));
    }

    /// <summary>Vertical deflection of one stick, deadbanded. Positive is away from the player.</summary>
    public static float StickY(Stick which, float deadband)
    {
        // getReal3D reports a stick pushed away from the player as positive already, the
        // same convention as every profile here, so nothing is inverted on this path.
        if (FromGetReal3D)
        {
            float left = Deadband(Gr3dAxis(Gr3dLeftYAxis), deadband);
            float right = Deadband(Gr3dAxis(Gr3dLookYAxis), deadband);

            if (which == Stick.Left) return left;
            if (which == Stick.Right) return right;
            return Larger(left, right);
        }

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
    public static float DPadX() { return MasterOnlyDevicesUnsafe ? 0f : Axis(Pad.dpadXAxis); }

    /// <summary>
    /// The D-pad's vertical axis. See <see cref="DPadX"/>. Both read 0 on a cluster:
    /// getReal3D has no logical name for the D-pad, and a master-only read would move one
    /// node and not the rest.
    /// </summary>
    public static float DPadY() { return MasterOnlyDevicesUnsafe ? 0f : Axis(Pad.dpadYAxis); }

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

    /// <summary>
    /// A getReal3D logical axis, 0 rather than an exception if the name is not mapped -
    /// the same bargain as <see cref="Axis"/>: a mapping missing on site costs one control.
    /// </summary>
    static float Gr3dAxis(string name)
    {
        try { return getReal3D.Input.GetAxis(name); }
        catch (System.Exception) { return 0f; }
    }

    static bool Gr3dButtonDown(string name)
    {
        try { return getReal3D.Input.GetButtonDown(name); }
        catch (System.Exception) { return false; }
    }

    // -- Buttons -------------------------------------------------------------

    /// <summary>A, pressed this frame. Confirm, recentre and emit are all this.</summary>
    public static bool ConfirmDown()
    {
        // The keyboard stand-ins are bound inside the logical buttons (mappings.xml), so
        // on this path Space still confirms, and does it on every node at once.
        if (FromGetReal3D) return Gr3dButtonDown(Gr3dConfirmButton);

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
        if (FromGetReal3D) return Gr3dButtonDown(Gr3dPauseButton);

        if (Input.GetKeyDown(PauseKey)) return true;

        PadProfile_NEW pad = Pad;

        return pad.pauseButton != KeyCode.None && Input.GetKeyDown(pad.pauseButton);
    }

    /// <summary>Y, pressed this frame. Shows or hides dark matter.</summary>
    public static bool ToggleDown()
    {
        if (FromGetReal3D) return Gr3dButtonDown(Gr3dToggleButton);

        if (Input.GetKeyDown(ToggleKey)) return true;

        PadProfile_NEW pad = Pad;

        return pad.toggleButton != KeyCode.None && Input.GetKeyDown(pad.toggleButton);
    }

    /// <summary>
    /// X, pressed this frame. Shows the journey's tracker HUD (JourneyTrackerHUD_NEW). On the
    /// wall it is getReal3D's ChangeWand, which mappings.xml binds to X and Left Shift; at the
    /// desk it is the Input Manager button the caller names, as that HUD has always read it.
    /// </summary>
    public static bool ShowHudDown(string unityButton)
    {
        if (FromGetReal3D) return Gr3dButtonDown(Gr3dShowHudButton);

        if (string.IsNullOrEmpty(unityButton)) return false;
        try { return Input.GetButtonDown(unityButton); }
        catch (System.ArgumentException) { return false; }   // not in the Input Manager
    }

    /// <summary>The letter printed on this pad for the dark matter toggle.</summary>
    public static string ToggleGlyph() { return Pad.toggleGlyph; }

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
        if (FromGetReal3D)
        {
            // Every node has to reach the same answer, or one copy of the piece returns to
            // attract while the others carry on: only the synchronised controls count.
            if (Gr3dButtonDown(Gr3dConfirmButton) || Gr3dButtonDown(Gr3dPauseButton) ||
                Gr3dButtonDown(Gr3dToggleButton)) return true;
            if (StickDeflection(Stick.Either, deadband) > 0f) return true;
            if (MasterOnlyDevicesUnsafe) return false;
        }
        else if (Input.anyKeyDown) return true;

        // Either, deliberately: a visitor pushing the stick that does not look is still
        // a visitor who is present, and the piece should not reset under them.
        if (StickDeflection(Stick.Either, deadband) > 0f) return true;

        if (Mathf.Abs(Input.GetAxisRaw(MouseXAxis)) > 0.01f) return true;
        if (Mathf.Abs(Input.GetAxisRaw(MouseYAxis)) > 0.01f) return true;

        return false;
    }
}

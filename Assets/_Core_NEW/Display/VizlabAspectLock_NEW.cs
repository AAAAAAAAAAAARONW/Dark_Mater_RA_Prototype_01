using System;
using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
#endif

/// <summary>
/// Makes a build render in the Vizlab surface's shape, 9600 x 7560 or about 1.27:1, on
/// whatever monitor it runs on. A playtest at a desk then shows the layout the wall will
/// show.
///
/// WHY IT MATTERS: every overlay canvas lays out against the screen, and the row system
/// cuts the screen's height into seven. On a 16:9 monitor each row comes out 40% wider for
/// its height than on the wall, so the HUD's proportions against its row are wrong, and
/// the cameras show a wider slice of the world than the wall will. Nothing reports an
/// error. The desk playtest just ends up judging the wrong picture.
///
/// HOW: at start it works out the largest 1.27:1 resolution that fits the monitor and asks
/// for it as a fullscreen window. Unity renders at that size and scales it up to the
/// screen with black bars at the sides, so the picture keeps the wall's proportions. The
/// canvases, the row anchors and the blackout band all follow, because they lay out
/// against the rendered resolution, not the monitor.
///
/// MODES
///   Auto      Native if the monitor already has the wall's shape (the wall itself),
///             otherwise Fit. The default.
///   Fit       Fullscreen, wall-shaped, with black bars.
///   Windowed  A wall-shaped window. Nothing is scaled, so the shape is exact whatever
///             the Unity version does with fullscreen scaling.
///   Native    Fill the monitor, as the build did before this component. Proportions are
///             off wherever the monitor is not wall-shaped.
///
/// A NOTE ON 2019.4: Unity's 2020.3 docs say outright that Fullscreen Window adds black
/// bars rather than stretching. The 2019.4 page only says the resolution is "scaled to
/// fit". The log reports the resolution actually in use a frame after the switch; if a
/// desk build ever looks stretched, Windowed renders the exact shape unscaled.
///
/// CONTROLS
///   Mouse     Fit / Windowed / Native buttons at the top centre, the current one lit.
///             Hidden by F1 along with the other debug readouts.
///   F5        cycles Fit, Windowed, Native.
///   Build.exe -vizlab-aspect fit|windowed|native|auto overrides the Inspector, so the
///   same build can be forced into a mode on a machine whose desktop is not what we expect
///   (a wall PC that reports an odd resolution, for instance).
///
/// IN THE EDITOR: Screen.SetResolution does nothing there, so the buttons drive the Game
/// View instead. Fit selects the 'Vizlab Surface 1:5' size (registering it first through
/// GameObject > Vizlab > Add Vizlab Sizes to Game View if it is missing), and Native puts
/// back the size the Game View had before. Windowed only exists in a build. Unity keeps
/// no public API for this, so it goes through reflection like VizlabDisplayMenu_NEW does,
/// and a failure logs how to do it by hand rather than throwing.
///
/// SETUP: GameObject > Vizlab > Add Aspect Lock to This Scene puts it in the open scene.
///
/// Unity remembers the last resolution per product name, so another build of this project
/// launched afterwards on the same machine may start in the same shape until it sets its
/// own.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("ASPECT", "#C08A2E")]
public class VizlabAspectLock_NEW : MonoBehaviour
{
    public enum Mode { Auto, Fit, Windowed, Native }

    [Tooltip("What to do at start in a build. Auto leaves a wall-shaped monitor alone and " +
             "fits the wall's shape into anything else. The -vizlab-aspect command-line flag " +
             "overrides this. In the editor nothing changes at start; use the buttons.")]
    [SerializeField] Mode modeOnStart = Mode.Auto;

    [Tooltip("How close a monitor's aspect ratio must be to the wall's for Auto to treat it " +
             "as the wall and leave it native. 0.02 is 2%.")]
    [Range(0f, 0.1f)]
    [SerializeField] float wallTolerance = 0.02f;

    [Tooltip("Windowed mode's size, as a fraction of the largest wall-shaped rectangle that " +
             "fits the monitor. Leaves room for the title bar and the taskbar.")]
    [Range(0.3f, 1f)]
    [SerializeField] float windowedScale = 0.85f;

    [SerializeField] KeyCode cycleKey = KeyCode.F5;

    [SerializeField] bool logChanges = true;

    [Header("Buttons")]
    [Tooltip("The Fit / Windowed / Native buttons at the top centre, for switching with the " +
             "mouse during a test. They also respect DebugView_NEW.Overlay, so F1 hides them.")]
    [SerializeField] bool showButtons = true;

    [Tooltip("Scale the buttons with the rendered height, so they are the same share of the " +
             "screen at a desk as on the wall.")]
    [SerializeField] bool autoScale = true;

    [SerializeField] int buttonFontSize = 14;

    [Tooltip("The on-screen note shown for a few seconds after a change. Builds have no " +
             "console, so this is how a playtester sees what happened.")]
    [SerializeField] int noteFontSize = 18;

    const string LogPrefix = "[VizlabAspectLock_NEW] ";
    const string CommandLineFlag = "-vizlab-aspect";
    const float NoteSeconds = 3f;

    static readonly Mode[] ButtonModes = { Mode.Fit, Mode.Windowed, Mode.Native };

    string _note = "";
    float _noteTime = -999f;

    /// <summary>
    /// A button press waits for the next Update. In the editor the switch resizes the Game
    /// View, and doing that from inside the Game View's own OnGUI is asking for trouble.
    /// </summary>
    Mode? _pending;

    /// <summary>The wall's width over its height, from VizlabDisplay_NEW. About 1.27.</summary>
    public static float WallAspect
    {
        get { return VizlabDisplay_NEW.CanvasWidthPx / (float)VizlabDisplay_NEW.CanvasHeightPx; }
    }

    /// <summary>The mode last applied. Auto is resolved, so this is never Auto after Start.</summary>
    public Mode Current { get; private set; }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Start()
    {
        if (Application.isEditor)
        {
            Current = IsWallShaped(Screen.width, Screen.height) ? Mode.Fit : Mode.Native;
            CheckGameView();
            return;
        }

        Mode mode = modeOnStart;
        string source = "Inspector";

        Mode fromArgs;
        if (TryReadCommandLine(out fromArgs))
        {
            mode = fromArgs;
            source = "command line";
        }

        ApplyNow(mode, source);
    }

    void Update()
    {
        if (_pending.HasValue)
        {
            Mode mode = _pending.Value;
            _pending = null;
            ApplyNow(mode, "button");
            return;
        }

        if (cycleKey == KeyCode.None || !Input.GetKeyDown(cycleKey)) return;

        // The editor has no windowed mode to offer, so there the key flips Fit and Native.
        Mode next = Application.isEditor ? (Current == Mode.Fit ? Mode.Native : Mode.Fit)
                  : Current == Mode.Fit ? Mode.Windowed
                  : Current == Mode.Windowed ? Mode.Native
                  : Mode.Fit;
        ApplyNow(next, cycleKey.ToString());
    }

    // ── Public surface ───────────────────────────────────────────────────────

    /// <summary>Switches mode on the next frame. For a UI button, a debug menu or another script.</summary>
    public void Apply(Mode mode)
    {
        _pending = mode;
    }

    [ContextMenu("Aspect: Fit")]
    public void Fit() { Apply(Mode.Fit); }

    [ContextMenu("Aspect: Windowed")]
    public void Windowed() { Apply(Mode.Windowed); }

    [ContextMenu("Aspect: Native")]
    public void Native() { Apply(Mode.Native); }

    /// <summary>
    /// The largest rectangle with the wall's shape that fits inside the given size. On a
    /// 3840 x 2160 monitor that is 2743 x 2160, with 548 px of black at each side.
    /// </summary>
    public static void FitWallAspect(int width, int height, out int fitWidth, out int fitHeight)
    {
        fitHeight = height;
        fitWidth = Mathf.RoundToInt(height * WallAspect);

        if (fitWidth > width)
        {
            fitWidth = width;
            fitHeight = Mathf.RoundToInt(width / WallAspect);
        }
    }

    /// <summary>True when a size is within the tolerance of the wall's aspect ratio.</summary>
    public bool IsWallShaped(int width, int height)
    {
        if (width <= 0 || height <= 0) return false;
        float aspect = width / (float)height;
        return Mathf.Abs(aspect - WallAspect) / WallAspect <= wallTolerance;
    }

    // ── Applying ─────────────────────────────────────────────────────────────

    void ApplyNow(Mode requested, string source)
    {
#if UNITY_EDITOR
        if (Application.isEditor)
        {
            ApplyInEditor(requested, source);
            return;
        }
#endif
        ApplyInPlayer(requested, source);
    }

    void ApplyInPlayer(Mode requested, string source)
    {
        // In a fullscreen window this is always the desktop, whatever we render at, so it
        // stays a stable reference across repeated switches.
        Resolution desktop = Screen.currentResolution;
        int dw = desktop.width, dh = desktop.height;

        Mode mode = requested;
        if (mode == Mode.Auto) mode = IsWallShaped(dw, dh) ? Mode.Native : Mode.Fit;

        int w, h;
        FitWallAspect(dw, dh, out w, out h);
        FullScreenMode screenMode = FullScreenMode.FullScreenWindow;

        switch (mode)
        {
            case Mode.Native:
                w = dw;
                h = dh;
                break;

            case Mode.Windowed:
                w = Mathf.RoundToInt(w * windowedScale);
                h = Mathf.RoundToInt(h * windowedScale);
                screenMode = FullScreenMode.Windowed;
                break;
        }

        Screen.SetResolution(w, h, screenMode);
        Current = mode;

        string resolved = requested == Mode.Auto ? "Auto -> " + mode : mode.ToString();
        string bars = mode == Mode.Fit && w < dw ? string.Format(", black bars {0} px each side", (dw - w) / 2)
                    : mode == Mode.Fit && h < dh ? string.Format(", black bars {0} px top and bottom", (dh - h) / 2)
                    : "";

        Note(string.Format("{0} ({1}): monitor {2} x {3} ({4:0.00}:1), wall {5} x {6} ({7:0.00}:1). " +
                           "Rendering {8} x {9} {10}{11}; 1 rendered px = {12:0.00} wall px.",
                           resolved, source, dw, dh, dw / (float)dh,
                           VizlabDisplay_NEW.CanvasWidthPx, VizlabDisplay_NEW.CanvasHeightPx, WallAspect,
                           w, h, screenMode == FullScreenMode.Windowed ? "in a window" : "fullscreen", bars,
                           VizlabDisplay_NEW.CanvasHeightPx / (float)h),
             string.Format("ASPECT  {0}   {1} x {2}", mode, w, h));

        // SetResolution lands at the end of the frame, so confirm what actually took effect.
        StopAllCoroutines();
        StartCoroutine(ConfirmNextFrame(w, h));
    }

    IEnumerator ConfirmNextFrame(int wantW, int wantH)
    {
        yield return null;
        yield return null;

        bool matched = Screen.width == wantW && Screen.height == wantH;
        string line = string.Format("now rendering {0} x {1} ({2:0.000}:1, wall {3:0.000}:1){4}",
                                    Screen.width, Screen.height, Screen.width / (float)Screen.height,
                                    WallAspect, matched ? "" : ", NOT the size requested");

        if (!matched) Debug.LogWarning(LogPrefix + line, this);
        else if (logChanges) Debug.Log(LogPrefix + line, this);
    }

    // ── Buttons ──────────────────────────────────────────────────────────────

    void OnGUI()
    {
        float scale = VizlabDebugGUI_NEW.BeginScaled(autoScale);
        Vector2 screen = VizlabDebugGUI_NEW.ScaledScreen(scale);

        if (showButtons && DebugView_NEW.Overlay) DrawButtons(screen);
        DrawNote(screen);
    }

    /// <summary>
    /// A strip at the top centre. LayerDebugJump_NEW has the top left and the blackout
    /// readout the top right, and on a wall-shaped screen this fits between them.
    /// </summary>
    void DrawButtons(Vector2 screen)
    {
        GUIStyle label = VizlabDebugGUI_NEW.Label(buttonFontSize);
        GUIStyle small = VizlabDebugGUI_NEW.Label(Mathf.Max(10, buttonFontSize - 3));

        const string Title = "ASPECT";
        Vector2 titleSize = label.CalcSize(new GUIContent(Title));
        Vector2 widest = VizlabDebugGUI_NEW.Button(buttonFontSize, false).CalcSize(new GUIContent("Windowed"));
        float bw = widest.x + 8f;
        float bh = widest.y + 4f;
        const float Gap = 6f, Pad = 10f;

        string info = string.Format("{0}{1} x {2}  ({3:0.00}:1, wall {4:0.00}:1)   {5} cycles",
                                    Application.isEditor ? "Game View " : "", Screen.width, Screen.height,
                                    Screen.width / (float)Screen.height, WallAspect, cycleKey);
        Vector2 infoSize = small.CalcSize(new GUIContent(info));

        float rowWidth = titleSize.x + Gap + ButtonModes.Length * (bw + Gap) - Gap;
        float width = Mathf.Max(rowWidth, infoSize.x) + Pad * 2f;
        float height = Pad + bh + 4f + infoSize.y + Pad * 0.6f;

        var panel = new Rect((screen.x - width) * 0.5f, 10f, width, height);
        GUI.Box(panel, GUIContent.none, VizlabDebugGUI_NEW.Panel);

        float x = panel.x + Pad;
        float y = panel.y + Pad;

        GUI.Label(new Rect(x, y + (bh - titleSize.y) * 0.5f, titleSize.x, titleSize.y), Title, label);
        x += titleSize.x + Gap;

        foreach (Mode mode in ButtonModes)
        {
            // There is no windowed mode in the editor's Game View.
            GUI.enabled = !(Application.isEditor && mode == Mode.Windowed);

            if (GUI.Button(new Rect(x, y, bw, bh), mode.ToString(), VizlabDebugGUI_NEW.Button(buttonFontSize, Current == mode)))
                Apply(mode);

            GUI.enabled = true;
            x += bw + Gap;
        }

        GUI.Label(new Rect(panel.x + Pad, y + bh + 4f, infoSize.x, infoSize.y), info, small);
    }

    void DrawNote(Vector2 screen)
    {
        if (Time.unscaledTime - _noteTime > NoteSeconds || string.IsNullOrEmpty(_note)) return;

        GUIStyle style = VizlabDebugGUI_NEW.Label(noteFontSize);
        Vector2 size = style.CalcSize(new GUIContent(_note));
        var box = new Rect((screen.x - size.x) * 0.5f - 14f, screen.y * 0.5f - size.y * 0.5f - 8f,
                           size.x + 28f, size.y + 16f);

        GUI.Box(box, GUIContent.none, VizlabDebugGUI_NEW.Panel);
        GUI.Label(new Rect(box.x + 14f, box.y + 8f, size.x, size.y), _note, style);
    }

    void Note(string logLine, string screenLine)
    {
        _note = screenLine;
        _noteTime = Time.unscaledTime;

        if (logChanges) Debug.Log(LogPrefix + logLine, this);
    }

    // ── Command line ─────────────────────────────────────────────────────────

    bool TryReadCommandLine(out Mode mode)
    {
        mode = Mode.Auto;
        string[] args = Environment.GetCommandLineArgs();

        for (int i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], CommandLineFlag, StringComparison.OrdinalIgnoreCase)) continue;

            if (i + 1 < args.Length && Enum.TryParse(args[i + 1], true, out mode) && Enum.IsDefined(typeof(Mode), mode))
                return true;

            Debug.LogWarning(LogPrefix + CommandLineFlag + " needs one of auto, fit, windowed, native. " +
                             "Using the Inspector's " + modeOnStart + ".", this);
            mode = Mode.Auto;
            return false;
        }

        return false;
    }

    // ── Editor: the Game View stands in for the screen ──────────────────────

    void CheckGameView()
    {
        int w = Screen.width, h = Screen.height;

        if (IsWallShaped(w, h))
        {
            if (logChanges)
                Debug.Log(string.Format(LogPrefix + "Game View is {0} x {1} ({2:0.00}:1), the wall's shape.",
                                        w, h, w / (float)h), this);
            return;
        }

        Debug.LogWarning(string.Format(
            LogPrefix + "Game View is {0} x {1} ({2:0.00}:1), not the wall's {3:0.00}:1, so the rows and " +
            "the HUD are out of proportion. Click Fit at the top of the Game View, or pick " +
            "'Vizlab Surface 1:5' in its size menu. A build sets the shape itself.",
            w, h, w / (float)h, WallAspect), this);
    }

#if UNITY_EDITOR
    const string VizlabGameViewSize = "Vizlab Surface 1:5";
    const string AddSizesMenu = "GameObject/Vizlab/Add Vizlab Sizes to Game View";
    const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>The Game View size in use before Fit, so Native can put it back. Survives the play session.</summary>
    static int _sizeBeforeFit = -1;

    void ApplyInEditor(Mode requested, string source)
    {
        Mode mode = requested == Mode.Auto ? Mode.Fit : requested;

        if (mode == Mode.Windowed)
        {
            Note("Windowed only exists in a build; the Game View has no window to shrink.",
                 "ASPECT  Windowed is build-only");
            return;
        }

        try
        {
            EditorWindow gameView = FindGameView();
            if (gameView == null) throw new InvalidOperationException("no Game View is open");

            int current = GetSelectedSize(gameView);
            int target;

            if (mode == Mode.Fit)
            {
                target = FindSizeIndex(VizlabGameViewSize);

                if (target < 0)
                {
                    // The size is registered by the colleague's menu item; run it and look again.
                    EditorApplication.ExecuteMenuItem(AddSizesMenu);
                    target = FindSizeIndex(VizlabGameViewSize);
                }

                if (target < 0)
                    throw new InvalidOperationException("the '" + VizlabGameViewSize + "' size is not registered");

                if (current != target) _sizeBeforeFit = current;
            }
            else
            {
                // Back to whatever was in use before Fit; Free Aspect if that is not known.
                target = _sizeBeforeFit >= 0 ? _sizeBeforeFit : 0;
            }

            SelectSize(gameView, target);
            Current = mode;

            Note(string.Format("{0} ({1}): Game View -> {2}.", mode, source,
                               mode == Mode.Fit ? "'" + VizlabGameViewSize + "' (1920 x 1512, the wall's shape)"
                                                : "the size it had before Fit"),
                 mode == Mode.Fit ? "ASPECT  Fit   Game View 1920 x 1512" : "ASPECT  Native   previous Game View size");

            StopAllCoroutines();
            StartCoroutine(ConfirmGameViewNextFrame(mode));
        }
        catch (Exception e)
        {
            Exception inner = e is TargetInvocationException && e.InnerException != null ? e.InnerException : e;
            Debug.LogWarning(LogPrefix + "could not switch the Game View (" + inner.Message + "). Do it by hand: " +
                             "run " + AddSizesMenu.Replace("/", " > ") + ", then pick '" + VizlabGameViewSize +
                             "' in the Game View's size menu.", this);
        }
    }

    IEnumerator ConfirmGameViewNextFrame(Mode mode)
    {
        yield return null;
        yield return null;

        bool wall = IsWallShaped(Screen.width, Screen.height);
        string line = string.Format("Game View now {0} x {1} ({2:0.000}:1, wall {3:0.000}:1)",
                                    Screen.width, Screen.height, Screen.width / (float)Screen.height, WallAspect);

        if (mode == Mode.Fit && !wall) Debug.LogWarning(LogPrefix + line + ", NOT the wall's shape", this);
        else if (logChanges) Debug.Log(LogPrefix + line, this);
    }

    static Type GameViewType
    {
        get { return typeof(Editor).Assembly.GetType("UnityEditor.GameView"); }
    }

    /// <summary>The open Game View, without opening one if there is none.</summary>
    static EditorWindow FindGameView()
    {
        Type type = GameViewType;
        if (type == null) return null;

        UnityEngine.Object[] views = Resources.FindObjectsOfTypeAll(type);
        return views.Length > 0 ? views[0] as EditorWindow : null;
    }

    static int GetSelectedSize(EditorWindow gameView)
    {
        PropertyInfo selected = GameViewType.GetProperty("selectedSizeIndex", AnyInstance);
        return selected != null ? (int)selected.GetValue(gameView, null) : -1;
    }

    /// <summary>
    /// SizeSelectionCallback is what the Game View's own size menu calls, so it also redoes
    /// the zoom and the layout. The property is the fallback if a Unity version renames it.
    /// </summary>
    static void SelectSize(EditorWindow gameView, int index)
    {
        MethodInfo callback = GameViewType.GetMethod("SizeSelectionCallback", AnyInstance);
        if (callback != null)
        {
            callback.Invoke(gameView, new object[] { index, null });
            return;
        }

        PropertyInfo selected = GameViewType.GetProperty("selectedSizeIndex", AnyInstance);
        if (selected == null || !selected.CanWrite)
            throw new MissingMemberException("GameView has neither SizeSelectionCallback nor a writable selectedSizeIndex");

        selected.SetValue(gameView, index, null);
        gameView.Repaint();
    }

    /// <summary>The index of a size in the Game View's current group by its label, or -1.</summary>
    static int FindSizeIndex(string label)
    {
        Assembly editorAsm = typeof(Editor).Assembly;
        Type sizesType = editorAsm.GetType("UnityEditor.GameViewSizes");
        Type sizeType = editorAsm.GetType("UnityEditor.GameViewSize");
        if (sizesType == null || sizeType == null) return -1;

        Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        object sizes = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null, null);

        PropertyInfo groupTypeProp = sizesType.GetProperty("currentGroupType",
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        object groupType = groupTypeProp != null ? groupTypeProp.GetValue(sizes, null) : GameViewSizeGroupType.Standalone;

        object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { groupType });
        int total = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
        MethodInfo get = group.GetType().GetMethod("GetGameViewSize");
        PropertyInfo baseText = sizeType.GetProperty("baseText");

        for (int i = 0; i < total; i++)
        {
            object size = get.Invoke(group, new object[] { i });
            if (size == null) continue;

            string text = baseText != null ? baseText.GetValue(size, null) as string : size.ToString();
            if (text == label) return i;
        }

        return -1;
    }
#endif
}

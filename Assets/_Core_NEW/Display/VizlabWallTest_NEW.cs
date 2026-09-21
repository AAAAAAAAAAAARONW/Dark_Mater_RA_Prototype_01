using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
#endif

/// <summary>
/// The panel for testing a build on the Vizlab curved display: it renders the build in the
/// wall's shape, and shows and logs what a wall test needs to know - frame rate, the
/// resolution and aspect ratio actually in use, the monitor and every display Windows
/// reports, the GPU, and whether a pad is connected.
///
/// NO SETUP: it installs itself when a scene is laid out for the wall, meaning it uses the
/// Vizlab row system (any VizlabRowAnchor_NEW) or has a Canvas Scaler set to the full 9600 x
/// 7560 surface. Today that is PlaytestBuild_NEW, PlaytestBuild_NEW_Row3 (where it is also
/// placed in the scene) and PlaytestBuild_NEW_Row7Blackout, out of 33 scenes. So building one
/// of those scenes is all it takes; nothing has to be added in the editor. To opt a scene
/// out, put this component in it and untick it. To opt a scene in, use GameObject > Vizlab >
/// Add Wall Test Panel to This Scene.
///
/// WHY THE SHAPE MATTERS: every overlay canvas lays out against the screen, and the row
/// system cuts the screen's height into seven. On a 16:9 monitor each row comes out 40%
/// wider for its height than on the wall, so a desk playtest judges the wrong picture.
///
/// ASPECT MODES
///   Auto      Native if the monitor already has the wall's shape (the wall itself),
///             otherwise Fit. The default.
///   Fit       Fullscreen at the largest wall-shaped resolution, with black bars. On a
///             3840 x 2160 monitor that is 2743 x 2160 with 548 px of black each side.
///   Windowed  A wall-shaped window. Nothing is scaled, so the shape is exact whatever the
///             Unity version does with fullscreen scaling.
///   Native    Fill the monitor. Right on the wall; out of proportion on anything else.
/// Unity 2020.3's docs say Fullscreen Window letterboxes rather than stretches; 2019.4's
/// only say "scaled to fit". The panel shows the resolution actually in use, so a stretched
/// picture shows up as a mismatch; Windowed is the fallback.
///
/// CONTROLS
///   Mouse     Fit / Windowed / Native; Up / Down move the panels between display rows
///             (row 1 is overhead on the wall), remembered between launches; - and +
///             collapse and expand. The blackout panel follows the same row.
///   F5        cycles the aspect mode.   F1 hides every debug panel.
///   Build.exe -vizlab-aspect fit|windowed|native|auto forces a mode without a rebuild.
///
/// LOG (Player.log in a build, the Console in the editor, path shown on the panel):
///   at start  the whole system: product, OS, CPU, RAM, GPU, every display, command line
///   on change every aspect switch, and the resolution that actually took effect
///   every 10 s one line of frame rate, resolution, aspect and mode (Stats Log Seconds)
///
/// IN THE EDITOR: Screen.SetResolution does nothing there, so the aspect buttons drive the
/// Game View instead: Fit selects 'Vizlab Surface 1:5' (registering it through
/// VizlabDisplayMenu_NEW's menu item if missing), Native puts back the previous size.
/// That goes through reflection, and a failure logs how to do it by hand.
///
/// Unity remembers the last resolution per product name, so another build of this project
/// launched afterwards on the same machine may start in the same shape until it sets its
/// own.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("WALL TEST", "#C08A2E")]
public class VizlabWallTest_NEW : MonoBehaviour
{
    public enum Mode { Auto, Fit, Windowed, Native }

    [Header("Aspect")]
    [Tooltip("What to do at start in a build. Auto leaves a wall-shaped monitor alone and " +
             "fits the wall's shape into anything else. The -vizlab-aspect command-line flag " +
             "overrides this. In the editor nothing changes at start; use the buttons.")]
    [SerializeField] Mode modeOnStart = Mode.Auto;

    [Tooltip("How close a monitor's aspect ratio must be to the wall's to count as the wall. " +
             "0.02 is 2%.")]
    [Range(0f, 0.1f)]
    [SerializeField] float wallTolerance = 0.02f;

    [Tooltip("Windowed mode's size, as a fraction of the largest wall-shaped rectangle that " +
             "fits the monitor. Leaves room for the title bar and the taskbar.")]
    [Range(0.3f, 1f)]
    [SerializeField] float windowedScale = 0.85f;

    [SerializeField] KeyCode cycleKey = KeyCode.F5;

    [Header("Panel")]
    [Tooltip("The panel at the top centre. It also respects DebugView_NEW.Overlay, so F1 hides it.")]
    [SerializeField] bool showPanel = true;

    [SerializeField] bool startCollapsed = false;

    [Tooltip("Scale the panel with the rendered height, so it is the same share of the " +
             "screen at a desk as on the wall.")]
    [SerializeField] bool autoScale = true;

    [Tooltip("In 1080p units; Auto Scale takes it from there.")]
    [SerializeField] int fontSize = 13;

    [Tooltip("The note shown mid-screen for a few seconds after an aspect change.")]
    [SerializeField] int noteFontSize = 18;

    [Header("Log")]
    [SerializeField] bool logChanges = true;

    [Tooltip("Log the whole system once at start: OS, CPU, RAM, GPU, displays, command line.")]
    [SerializeField] bool logSystemAtStart = true;

    [Tooltip("Seconds between one-line stats entries in the log. 0 turns them off.")]
    [Range(0f, 120f)]
    [SerializeField] float statsLogSeconds = 10f;

    const string LogPrefix = "[VizlabWallTest_NEW] ";
    const string CommandLineFlag = "-vizlab-aspect";
    const float NoteSeconds = 3f;
    const int FrameWindow = 240;
    const float StatsRefreshSeconds = 0.5f;
    const float Pad = 10f, Gap = 6f;

    static readonly Mode[] ButtonModes = { Mode.Fit, Mode.Windowed, Mode.Native };

    string _note = "";
    float _noteTime = -999f;
    bool _collapsed;

    /// <summary>Whether the scene has a blackout band, so the key hint only offers F4 where it does something.</summary>
    bool _hasBlackout;

    /// <summary>
    /// A button press waits for the next Update. In the editor the switch resizes the Game
    /// View, and doing that from inside the Game View's own OnGUI is asking for trouble.
    /// </summary>
    Mode? _pending;

    // Frame statistics over the last FrameWindow frames, refreshed twice a second.
    readonly float[] _frameMs = new float[FrameWindow];
    readonly float[] _sorted = new float[FrameWindow];
    int _frameCursor, _frameCount;
    float _avgMs, _onePercentLowFps, _worstMs;
    float _nextStatsRefresh, _nextStatsLog;

    readonly StringBuilder _sb = new StringBuilder(1024);

    /// <summary>The wall's width over its height, from VizlabDisplay_NEW. About 1.27.</summary>
    public static float WallAspect
    {
        get { return VizlabDisplay_NEW.CanvasWidthPx / (float)VizlabDisplay_NEW.CanvasHeightPx; }
    }

    /// <summary>The mode last applied. Auto is resolved, so this is never Auto after Start.</summary>
    public Mode Current { get; private set; }

    /// <summary>Average frames per second over the last few seconds.</summary>
    public float Fps { get { return _avgMs > 0f ? 1000f / _avgMs : 0f; } }

    // ── Installing itself ────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInstall()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        InstallIfWallScene();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InstallIfWallScene();
    }

    /// <summary>
    /// A scene laid out for the wall gets the panel. A scene that already has one, ticked or
    /// not, is left to decide for itself.
    /// </summary>
    static void InstallIfWallScene()
    {
        if (FindObjectOfType<VizlabWallTest_NEW>() != null) return;
        if (!IsLaidOutForTheWall()) return;

        new GameObject("Vizlab Wall Test (auto)").AddComponent<VizlabWallTest_NEW>();
    }

    /// <summary>
    /// Either sign that a scene is designed for the wall: it places UI with the Vizlab row
    /// system, or a Canvas Scaler is set to the full surface, which is what GameObject >
    /// Vizlab > Set Canvas to Full Surface does.
    /// </summary>
    static bool IsLaidOutForTheWall()
    {
        if (FindObjectOfType<VizlabRowAnchor_NEW>() != null) return true;

        var surface = new Vector2(VizlabDisplay_NEW.CanvasWidthPx, VizlabDisplay_NEW.CanvasHeightPx);
        foreach (CanvasScaler scaler in FindObjectsOfType<CanvasScaler>())
        {
            if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize && scaler.referenceResolution == surface)
                return true;
        }

        return false;
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Start()
    {
        _collapsed = startCollapsed;
        _hasBlackout = FindObjectOfType<VizlabRowBlackout_NEW>() != null;
        _nextStatsLog = Time.unscaledTime + statsLogSeconds;

        if (logSystemAtStart) LogSystem();

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
        SampleFrame();

        if (statsLogSeconds > 0f && Time.unscaledTime >= _nextStatsLog)
        {
            _nextStatsLog = Time.unscaledTime + statsLogSeconds;
            if (logChanges && _frameCount > 0) Debug.Log(LogPrefix + StatsLine(), this);
        }

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

    [ContextMenu("Log system")]
    public void LogSystem()
    {
        Resolution desktop = Screen.currentResolution;

        _sb.Length = 0;
        _sb.Append(LogPrefix).Append("system\n");
        _sb.Append("  product   ").Append(Application.productName).Append(' ').Append(Application.version)
           .Append(", Unity ").Append(Application.unityVersion)
           .Append(Application.isEditor ? ", editor" : Debug.isDebugBuild ? ", development build" : ", release build").Append('\n');
        _sb.Append("  scene     ").Append(SceneManager.GetActiveScene().name).Append('\n');
        _sb.Append("  os        ").Append(SystemInfo.operatingSystem).Append('\n');
        _sb.Append("  cpu       ").Append(SystemInfo.processorType).Append(", ").Append(SystemInfo.processorCount).Append(" threads\n");
        _sb.Append("  ram       ").Append(SystemInfo.systemMemorySize).Append(" MB\n");
        _sb.Append("  gpu       ").Append(SystemInfo.graphicsDeviceName).Append(" (").Append(SystemInfo.graphicsDeviceVendor)
           .Append("), ").Append(SystemInfo.graphicsDeviceType).Append(", ").Append(SystemInfo.graphicsMemorySize).Append(" MB\n");
        _sb.Append("  monitor   ").Append(desktop.width).Append(" x ").Append(desktop.height).Append(" @ ")
           .Append(desktop.refreshRate).Append(" Hz (").Append(Aspect(desktop.width, desktop.height)).Append(")\n");
        _sb.Append("  displays  ").Append(DescribeDisplays(8)).Append('\n');
        _sb.Append("  render    ").Append(Screen.width).Append(" x ").Append(Screen.height).Append(' ')
           .Append(Screen.fullScreenMode).Append(" (").Append(Aspect(Screen.width, Screen.height)).Append(")\n");
        _sb.Append("  wall      ").Append(VizlabDisplay_NEW.CanvasWidthPx).Append(" x ").Append(VizlabDisplay_NEW.CanvasHeightPx)
           .Append(" (").Append(WallAspect.ToString("0.000")).Append(":1); per-row pixel width still unconfirmed\n");
        _sb.Append("  quality   ").Append(QualityName()).Append(", vSync ").Append(QualitySettings.vSyncCount)
           .Append(", target fps ").Append(Application.targetFrameRate).Append('\n');
        _sb.Append("  command   ").Append(Environment.CommandLine).Append('\n');
        _sb.Append("  log file  ").Append(LogPath());

        Debug.Log(_sb.ToString(), this);
    }

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
        return Mathf.Abs(width / (float)height - WallAspect) / WallAspect <= wallTolerance;
    }

    // ── Frame statistics ─────────────────────────────────────────────────────

    void SampleFrame()
    {
        float ms = Time.unscaledDeltaTime * 1000f;
        if (ms <= 0f) return;

        _frameMs[_frameCursor] = ms;
        _frameCursor = (_frameCursor + 1) % FrameWindow;
        if (_frameCount < FrameWindow) _frameCount++;

        if (Time.unscaledTime < _nextStatsRefresh) return;
        _nextStatsRefresh = Time.unscaledTime + StatsRefreshSeconds;

        // Average, the slowest 1% and the single worst frame. A 240-float sort twice a
        // second is nothing, and it is what makes "1% low" mean something.
        Array.Copy(_frameMs, _sorted, _frameCount);
        Array.Sort(_sorted, 0, _frameCount);

        float sum = 0f;
        for (int i = 0; i < _frameCount; i++) sum += _sorted[i];

        _avgMs = sum / _frameCount;
        _worstMs = _sorted[_frameCount - 1];
        int p99 = Mathf.Clamp(Mathf.CeilToInt(_frameCount * 0.99f) - 1, 0, _frameCount - 1);
        _onePercentLowFps = 1000f / _sorted[p99];
    }

    /// <summary>Green at 90% of the refresh rate or better, amber down to half, red below.</summary>
    string FpsColour(float fps)
    {
        int hz = Screen.currentResolution.refreshRate;
        float target = hz > 0 ? hz : 60f;
        return fps >= target * 0.9f ? VizlabDebugGUI_NEW.Good
             : fps >= target * 0.5f ? VizlabDebugGUI_NEW.Warn
             : VizlabDebugGUI_NEW.Bad;
    }

    string StatsLine()
    {
        Resolution desktop = Screen.currentResolution;
        return string.Format(
            "stats: fps {0:0.0} (1% low {1:0.0}, worst frame {2:0} ms) | render {3} x {4} {5} ({6}) | " +
            "monitor {7} x {8} @ {9} Hz | mode {10}, {11} | displays {12} | memory {13}",
            Fps, _onePercentLowFps, _worstMs,
            Screen.width, Screen.height, Aspect(Screen.width, Screen.height),
            IsWallShaped(Screen.width, Screen.height) ? "wall shape" : "NOT wall shape",
            desktop.width, desktop.height, desktop.refreshRate,
            Current, Screen.fullScreenMode, Display.displays.Length, MemoryText());
    }

    // ── Applying the aspect ──────────────────────────────────────────────────

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

        Note(string.Format("aspect {0} ({1}): monitor {2} x {3} ({4}), wall {5} x {6} ({7:0.000}:1). " +
                           "Rendering {8} x {9} {10}{11}; 1 rendered px = {12:0.00} wall px.",
                           resolved, source, dw, dh, Aspect(dw, dh),
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
        string line = string.Format("now rendering {0} x {1} ({2}, wall {3:0.000}:1){4}",
                                    Screen.width, Screen.height, Aspect(Screen.width, Screen.height),
                                    WallAspect, matched ? "" : ", NOT the size requested");

        if (!matched) Debug.LogWarning(LogPrefix + line, this);
        else if (logChanges) Debug.Log(LogPrefix + line, this);
    }

    // ── Panel ────────────────────────────────────────────────────────────────

    void OnGUI()
    {
        float scale = VizlabDebugGUI_NEW.BeginScaled(autoScale);
        Vector2 screen = VizlabDebugGUI_NEW.ScaledScreen(scale);

        if (showPanel && DebugView_NEW.Overlay)
        {
            if (_collapsed) DrawCollapsed(screen);
            else DrawPanel(screen);
        }

        DrawNote(screen);
    }

    /// <summary>
    /// Top centre of the shared panel row. LayerDebugJump_NEW has the top left and the
    /// blackout readout the top right, and on a wall-shaped screen this fits between them.
    /// </summary>
    void DrawPanel(Vector2 screen)
    {
        GUIStyle label = VizlabDebugGUI_NEW.Label(fontSize);
        GUIStyle rich = VizlabDebugGUI_NEW.RichLabel(fontSize);
        GUIStyle button = VizlabDebugGUI_NEW.Button(fontSize, false);

        const string Title = "VIZLAB WALL TEST";
        const string AspectTitle = "ASPECT";

        string body = BuildBody();
        Vector2 bodySize = rich.CalcSize(new GUIContent(body));
        Vector2 titleSize = label.CalcSize(new GUIContent(Title));
        Vector2 aspectSize = label.CalcSize(new GUIContent(AspectTitle));

        Vector2 modeButton = button.CalcSize(new GUIContent("Windowed"));
        float bw = modeButton.x + 8f;
        float bh = modeButton.y + 4f;
        float small = button.CalcSize(new GUIContent("Down")).x + 6f;

        float aspectRow = aspectSize.x + Gap + ButtonModes.Length * (bw + Gap) - Gap;
        float headerRow = titleSize.x + Gap * 4f + small * 3f + Gap * 2f;
        float width = Mathf.Max(bodySize.x, Mathf.Max(aspectRow, headerRow)) + Pad * 2f;
        float height = Pad + bh + Gap + bh + Gap + bodySize.y + Pad;

        var panel = new Rect((screen.x - width) * 0.5f, VizlabDebugGUI_NEW.PanelTop(screen.y, height, 10f), width, height);
        GUI.Box(panel, GUIContent.none, VizlabDebugGUI_NEW.Panel);

        float x = panel.x + Pad;
        float y = panel.y + Pad;

        // Header: title, then the panel's own controls at the right.
        GUI.Label(new Rect(x, y + (bh - titleSize.y) * 0.5f, titleSize.x, titleSize.y), Title, label);
        float right = panel.xMax - Pad;
        if (GUI.Button(new Rect(right - small, y, small, bh), "-", button)) _collapsed = true;
        if (GUI.Button(new Rect(right - small * 2f - Gap, y, small, bh), "Down", button)) MovePanel(1);
        if (GUI.Button(new Rect(right - small * 3f - Gap * 2f, y, small, bh), "Up", button)) MovePanel(-1);
        y += bh + Gap;

        // Aspect buttons, the one in effect lit.
        GUI.Label(new Rect(x, y + (bh - aspectSize.y) * 0.5f, aspectSize.x, aspectSize.y), AspectTitle, label);
        float bx = x + aspectSize.x + Gap;

        foreach (Mode mode in ButtonModes)
        {
            // There is no windowed mode in the editor's Game View.
            GUI.enabled = !(Application.isEditor && mode == Mode.Windowed);

            if (GUI.Button(new Rect(bx, y, bw, bh), mode.ToString(), VizlabDebugGUI_NEW.Button(fontSize, Current == mode)))
                Apply(mode);

            GUI.enabled = true;
            bx += bw + Gap;
        }
        y += bh + Gap;

        GUI.Label(new Rect(x, y, bodySize.x, bodySize.y), body, rich);
    }

    /// <summary>One line: the numbers that matter most at a glance, and + to expand.</summary>
    void DrawCollapsed(Vector2 screen)
    {
        GUIStyle rich = VizlabDebugGUI_NEW.RichLabel(fontSize);
        GUIStyle button = VizlabDebugGUI_NEW.Button(fontSize, false);

        string line = string.Format("VIZLAB  {0} fps   {1} x {2}  {3}   {4}",
                                    VizlabDebugGUI_NEW.Colour(Fps.ToString("0.0"), FpsColour(Fps)),
                                    Screen.width, Screen.height, ShapeVerdict(), Current);
        Vector2 size = rich.CalcSize(new GUIContent(line));
        float bh = button.CalcSize(new GUIContent("+")).y + 4f;
        float bw = button.CalcSize(new GUIContent("+")).x + 12f;

        float width = Pad + size.x + Gap + bw + Pad;
        float height = Pad * 0.6f + bh + Pad * 0.6f;
        var panel = new Rect((screen.x - width) * 0.5f, VizlabDebugGUI_NEW.PanelTop(screen.y, height, 10f), width, height);
        GUI.Box(panel, GUIContent.none, VizlabDebugGUI_NEW.Panel);

        float y = panel.y + Pad * 0.6f;
        GUI.Label(new Rect(panel.x + Pad, y + (bh - size.y) * 0.5f, size.x, size.y), line, rich);
        if (GUI.Button(new Rect(panel.xMax - Pad - bw, y, bw, bh), "+", button)) _collapsed = false;
    }

    string BuildBody()
    {
        Resolution desktop = Screen.currentResolution;
        float fps = Fps;

        _sb.Length = 0;
        _sb.Append("fps       ").Append(VizlabDebugGUI_NEW.Colour(fps.ToString("0.0"), FpsColour(fps)))
           .Append("   1% low ").Append(VizlabDebugGUI_NEW.Colour(_onePercentLowFps.ToString("0.0"), FpsColour(_onePercentLowFps)))
           .Append("   frame ").Append(_avgMs.ToString("0.0")).Append(" ms   worst ").Append(_worstMs.ToString("0")).Append(" ms\n");

        _sb.Append(Application.isEditor ? "game view " : "render    ")
           .Append(Screen.width).Append(" x ").Append(Screen.height).Append("   ")
           .Append(Aspect(Screen.width, Screen.height)).Append("   wall ").Append(WallAspect.ToString("0.000"))
           .Append(":1   ").Append(ShapeVerdict()).Append('\n');

        _sb.Append("monitor   ").Append(desktop.width).Append(" x ").Append(desktop.height).Append(" @ ")
           .Append(desktop.refreshRate).Append(" Hz   ").Append(Aspect(desktop.width, desktop.height))
           .Append("   mode ").Append(Current);
        if (!Application.isEditor) _sb.Append(", ").Append(Screen.fullScreenMode);
        _sb.Append('\n');

        _sb.Append("displays  ").Append(DescribeDisplays(3)).Append('\n');

        _sb.Append("gpu       ").Append(SystemInfo.graphicsDeviceName).Append(" (")
           .Append(SystemInfo.graphicsMemorySize).Append(" MB)   vSync ").Append(QualitySettings.vSyncCount).Append('\n');

        _sb.Append("memory    ").Append(MemoryText()).Append("   RAM ")
           .Append((SystemInfo.systemMemorySize / 1024f).ToString("0")).Append(" GB   quality ").Append(QualityName()).Append('\n');

        string pad = PadNames();
        _sb.Append("pad       ").Append(pad.Length > 0 ? pad : VizlabDebugGUI_NEW.Colour("none connected", VizlabDebugGUI_NEW.Warn)).Append('\n');

        TimeSpan up = TimeSpan.FromSeconds(Time.realtimeSinceStartup);
        _sb.Append("scene     ").Append(SceneManager.GetActiveScene().name).Append("   up ")
           .Append(((int)up.TotalHours).ToString("00")).Append(':').Append(up.Minutes.ToString("00"))
           .Append(':').Append(up.Seconds.ToString("00")).Append('\n');

        _sb.Append("log       ").Append(Shorten(LogPath(), 56)).Append('\n');
        _sb.Append("keys      ").Append(cycleKey).Append(" aspect   ")
           .Append(_hasBlackout ? "F4 blackout panel   " : "").Append("F1 debug panels on/off");

        return _sb.ToString();
    }

    string ShapeVerdict()
    {
        int w = Screen.width, h = Screen.height;
        if (IsWallShaped(w, h)) return VizlabDebugGUI_NEW.Colour("WALL SHAPE", VizlabDebugGUI_NEW.Good);

        float off = (w / (float)h - WallAspect) / WallAspect * 100f;
        return VizlabDebugGUI_NEW.Colour(string.Format("{0}{1:0}% OFF", off > 0f ? "+" : "", off), VizlabDebugGUI_NEW.Bad);
    }

    void MovePanel(int step)
    {
        VizlabDebugGUI_NEW.PanelRow = VizlabDebugGUI_NEW.PanelRow + step;
        if (logChanges) Debug.Log(LogPrefix + "panels now hang from row " + VizlabDebugGUI_NEW.PanelRow, this);
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

    // ── Small readings ───────────────────────────────────────────────────────

    static string Aspect(int w, int h)
    {
        return h > 0 ? (w / (float)h).ToString("0.000") + ":1" : "-";
    }

    /// <summary>
    /// Every display Windows reports, by native size. The first wall test should settle
    /// whether the wall is one 9600 x 7560 desktop or several outputs; Unity only draws
    /// on the first unless told otherwise.
    /// </summary>
    static string DescribeDisplays(int max)
    {
        Display[] displays = Display.displays;
        var sb = new StringBuilder();
        sb.Append(displays.Length).Append(':');

        for (int i = 0; i < displays.Length && i < max; i++)
            sb.Append("  [").Append(i + 1).Append("] ").Append(displays[i].systemWidth).Append('x').Append(displays[i].systemHeight);

        if (displays.Length > max) sb.Append("  +").Append(displays.Length - max).Append(" more");
        return sb.ToString();
    }

    static string MemoryText()
    {
        long bytes = Profiler.GetTotalAllocatedMemoryLong();
        return bytes > 0 ? (bytes / (1024f * 1024f)).ToString("0") + " MB used" : "n/a";
    }

    static string QualityName()
    {
        string[] names = QualitySettings.names;
        int level = QualitySettings.GetQualityLevel();
        return level >= 0 && level < names.Length ? names[level] : level.ToString();
    }

    static string PadNames()
    {
        var sb = new StringBuilder();
        foreach (string name in Input.GetJoystickNames())
        {
            if (string.IsNullOrEmpty(name)) continue;
            if (sb.Length > 0) sb.Append("; ");
            sb.Append(name.Trim());
        }
        return sb.ToString();
    }

    static string LogPath()
    {
        string path = Application.consoleLogPath;
        return string.IsNullOrEmpty(path) ? "(no log file on this platform)" : path;
    }

    /// <summary>Keeps the end of a long path, which is the part that identifies it.</summary>
    static string Shorten(string text, int max)
    {
        return text.Length <= max ? text : "..." + text.Substring(text.Length - (max - 3));
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
                Debug.Log(string.Format(LogPrefix + "Game View is {0} x {1} ({2}), the wall's shape.",
                                        w, h, Aspect(w, h)), this);
            return;
        }

        Debug.LogWarning(string.Format(
            LogPrefix + "Game View is {0} x {1} ({2}), not the wall's {3:0.000}:1, so the rows and the " +
            "HUD are out of proportion. Click Fit on the panel, or pick 'Vizlab Surface 1:5' in the " +
            "Game View's size menu. A build sets the shape itself.",
            w, h, Aspect(w, h), WallAspect), this);
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
                    // The size is registered by VizlabDisplayMenu_NEW's menu item; run it and look again.
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

            Note(string.Format("aspect {0} ({1}): Game View -> {2}.", mode, source,
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
        string line = string.Format("Game View now {0} x {1} ({2}, wall {3:0.000}:1)",
                                    Screen.width, Screen.height, Aspect(Screen.width, Screen.height), WallAspect);

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

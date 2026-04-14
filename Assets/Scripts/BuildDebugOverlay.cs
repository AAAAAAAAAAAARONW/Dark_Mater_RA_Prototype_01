using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

/// <summary>
/// Runtime debug overlay for Editor and Build.
/// F1: toggle overlay, F2: force CSV flush/export.
/// </summary>
public class BuildDebugOverlay : MonoBehaviour
{
    private const float DefaultFrameBudgetMs60Fps = 16.6667f;
    private const int FrameWindowSize = 120;

    private readonly StringBuilder _sb = new StringBuilder(1024);
    private readonly float[] _frameTimes = new float[FrameWindowSize];
    private readonly float[] _frameScratch = new float[FrameWindowSize];
    private readonly FrameTiming[] _timings = new FrameTiming[1];

    private int _frameCursor;
    private bool _frameBufferFilled;

    private float _fpsSmoothed;
    private float _frameMsSmoothed;
    private float _cpuMsSmoothed;
    private float _gpuMsSmoothed;

    private string _lastInput = "None";
    private float _lastInputTime;
    private float _nextConsoleLogTime;
    private float _nextCsvSampleTime;
    private float _nextCsvFlushTime;

    private bool _showOverlay = true;
    private bool _minimized;
    private string _csvPath;
    private string _lastCsvStatus = "CSV: idle";
    private string _unityVersionText;
    private string _renderPipelineText;

    private GUIStyle _panelStyle;
    private GUIStyle _textStyle;
    private GUIStyle _titleStyle;
    private GUIStyle _topBarStyle;
    private GUIStyle _buttonStyle;
    private Rect _rect;
    private Rect _topBarRect;

    [Header("Hotkeys")]
    [SerializeField] private KeyCode toggleOverlayKey = KeyCode.F1;
    [SerializeField] private KeyCode forceExportKey = KeyCode.F2;
    [Tooltip("Gamepad button name (Input Manager) that toggles minimized/expanded. " +
             "Default 'Cancel' = B on Xbox / Circle on PS.")]
    [SerializeField] private string gamepadCollapseButton = "Cancel";

    [Header("Logging")]
    [SerializeField] private bool logInputToConsole = false;
    [SerializeField] private float consoleLogInterval = 0.2f;
    [SerializeField] private bool enableCsvLogging = true;
    [SerializeField] private float csvSampleInterval = 0.5f;
    [SerializeField] private float csvFlushInterval = 3.0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        BuildDebugOverlay existing = FindObjectOfType<BuildDebugOverlay>();
        if (existing != null) return;

        GameObject go = new GameObject("BuildDebugOverlay");
        DontDestroyOnLoad(go);
        go.AddComponent<BuildDebugOverlay>();
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        _rect = new Rect(12f, 12f, 840f, 395f);
        _topBarRect = new Rect(_rect.x, _rect.y, _rect.width, 34f);
        _unityVersionText = Application.unityVersion;
        _renderPipelineText = ResolveRenderPipelineName();
        InitializeCsvFile();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleOverlayKey))
        {
            _showOverlay = !_showOverlay;
        }
        if (Input.GetKeyDown(forceExportKey))
        {
            FlushCsvStatus();
        }

        // Gamepad B button — toggle minimized/expanded (overlay must be visible)
        if (_showOverlay && !string.IsNullOrEmpty(gamepadCollapseButton))
        {
            try
            {
                if (Input.GetButtonDown(gamepadCollapseButton))
                    _minimized = !_minimized;
            }
            catch { /* axis not defined in Input Manager — silently ignore */ }
        }

        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.00001f);
        float frameMs = dt * 1000f;
        float fps = 1f / dt;

        _frameTimes[_frameCursor] = frameMs;
        _frameCursor++;
        if (_frameCursor >= _frameTimes.Length)
        {
            _frameCursor = 0;
            _frameBufferFilled = true;
        }

        _frameMsSmoothed = Mathf.Lerp(_frameMsSmoothed <= 0f ? frameMs : _frameMsSmoothed, frameMs, 0.12f);
        _fpsSmoothed = Mathf.Lerp(_fpsSmoothed <= 0f ? fps : _fpsSmoothed, fps, 0.12f);

        UpdateFrameTimings();
        CaptureInput();
        TickCsvLogging();
    }

    private void OnGUI()
    {
        if (!_showOverlay) return;
        EnsureGuiStyles();

        float avgMs = GetAverageFrameMs();
        float onePercentLowFps = GetOnePercentLowFps();
        float budgetMs = GetFrameBudgetMs();

        float cpuLoadPct = budgetMs > 0.001f ? Mathf.Clamp01(_cpuMsSmoothed / budgetMs) * 100f : 0f;
        float gpuLoadPct = budgetMs > 0.001f ? Mathf.Clamp01(_gpuMsSmoothed / budgetMs) * 100f : 0f;
        float totalLoadPct = budgetMs > 0.001f ? Mathf.Clamp01(avgMs / budgetMs) * 100f : 0f;

        float inputAgeMs = _lastInputTime > 0f ? (Time.realtimeSinceStartup - _lastInputTime) * 1000f : -1f;
        float baseRenderMs = Mathf.Max(_frameMsSmoothed, Mathf.Max(_cpuMsSmoothed, _gpuMsSmoothed));
        float estimatedLatencyMs = inputAgeMs < 0f ? baseRenderMs : (baseRenderMs + inputAgeMs);

        string[] joystickNames = Input.GetJoystickNames();
        int connectedControllerCount = 0;
        _sb.Length = 0;
        for (int i = 0; i < joystickNames.Length; i++)
        {
            if (!string.IsNullOrEmpty(joystickNames[i]))
            {
                connectedControllerCount++;
                if (_sb.Length < 150) _sb.Append(joystickNames[i]).Append("; ");
            }
        }
        string controllerNamePreview = _sb.Length > 0 ? _sb.ToString() : "None";

        long memBytes = Profiler.GetTotalAllocatedMemoryLong();
        float memMb = memBytes / (1024f * 1024f);

        _sb.Length = 0;
        _sb.AppendLine("<b>BUILD DEBUG OVERLAY</b>");
        _sb.Append("<color=#9FE4FF>Unity</color>: ").Append(_unityVersionText).Append("  |  ");
        _sb.Append("<color=#9FE4FF>RenderPipeline</color>: ").AppendLine(_renderPipelineText);
        _sb.Append("<color=#8FD3FF>Hotkeys</color>: ").Append(toggleOverlayKey).Append(" show/hide, ")
           .Append(forceExportKey).Append(" force CSV flush, ")
           .Append(gamepadCollapseButton).AppendLine(" (gamepad B) collapse/expand");
        _sb.Append("<color=#A0FFA0>Controller</color>: ").Append(connectedControllerCount > 0 ? "Connected" : "Not Connected");
        _sb.Append(" (").Append(connectedControllerCount).AppendLine(")");
        _sb.Append("Device: ").AppendLine(controllerNamePreview);
        _sb.Append("<color=#FFD580>Input</color>: ").AppendLine(_lastInput);
        _sb.Append("<color=#A8C8FF>FPS</color>: ").Append(_fpsSmoothed.ToString("F1"));
        _sb.Append(" | 1% Low: ").Append(onePercentLowFps.ToString("F1"));
        _sb.Append(" | Frame: ").Append(_frameMsSmoothed.ToString("F2"));
        _sb.Append(" ms | Avg120: ").Append(avgMs.ToString("F2")).AppendLine(" ms");
        _sb.Append("<color=#FFC8A8>CPU/GPU</color>: ");
        _sb.Append("CPU ").Append(_cpuMsSmoothed.ToString("F2")).Append(" ms (").Append(cpuLoadPct.ToString("F0")).Append("%)");
        _sb.Append(" | GPU ").Append(_gpuMsSmoothed.ToString("F2")).Append(" ms (").Append(gpuLoadPct.ToString("F0")).AppendLine("%)");
        _sb.Append("<color=#FFB3C8>Load</color>: ").Append(totalLoadPct.ToString("F1")).Append("% (budget ").Append(budgetMs.ToString("F2")).AppendLine(" ms)");
        _sb.Append("<color=#D2B7FF>Latency(est)</color>: ").Append(estimatedLatencyMs.ToString("F1")).Append(" ms");
        _sb.Append(" | InputAge: ").Append(inputAgeMs < 0f ? "N/A" : inputAgeMs.ToString("F1") + " ms").AppendLine();
        _sb.Append("<color=#C0E6FF>Sys</color>: ").Append(Screen.width).Append("x").Append(Screen.height);
        _sb.Append(" @").Append(Screen.currentResolution.refreshRate).Append("Hz");
        _sb.Append(" | vSync: ").Append(QualitySettings.vSyncCount);
        _sb.Append(" | targetFPS: ").Append(Application.targetFrameRate);
        _sb.Append(" | mem: ").Append(memMb.ToString("F1")).AppendLine(" MB");
        _sb.Append("<color=#E0E0E0>").Append(_lastCsvStatus).Append("</color>");

        float panelHeight = _minimized ? 66f : _rect.height;
        Rect panelRect = new Rect(_rect.x, _rect.y, _rect.width, panelHeight);
        Rect topRect = new Rect(_topBarRect.x, _topBarRect.y, _topBarRect.width, _topBarRect.height);
        Rect buttonRect = new Rect(topRect.xMax - 108f, topRect.y + 4f, 96f, 26f);

        GUI.Box(panelRect, GUIContent.none, _panelStyle);
        GUI.Box(topRect, GUIContent.none, _topBarStyle);
        GUI.Label(new Rect(_rect.x + 14f, _rect.y + 6f, _rect.width - 130f, 24f), "Build Runtime Diagnostics", _titleStyle);

        if (GUI.Button(buttonRect, _minimized ? "Expand" : "Minimize", _buttonStyle))
        {
            _minimized = !_minimized;
        }

        if (_minimized)
        {
            string mini = string.Format(
                CultureInfo.InvariantCulture,
                "Unity {0} | {1} | FPS {2:F1} | CPU {3:F2} ms | GPU {4:F2} ms",
                _unityVersionText, _renderPipelineText, _fpsSmoothed, _cpuMsSmoothed, _gpuMsSmoothed
            );
            GUI.Label(new Rect(_rect.x + 14f, _rect.y + 38f, _rect.width - 28f, 24f), mini, _textStyle);
            return;
        }

        GUI.Label(new Rect(_rect.x + 14f, _rect.y + 42f, _rect.width - 28f, _rect.height - 52f), _sb.ToString(), _textStyle);
    }

    private void OnApplicationQuit()
    {
        FlushCsvStatus();
    }

    private void EnsureGuiStyles()
    {
        if (_panelStyle == null)
        {
            Texture2D bg = new Texture2D(1, 1);
            bg.SetPixel(0, 0, new Color(0.03f, 0.04f, 0.08f, 0.82f));
            bg.Apply();

            _panelStyle = new GUIStyle(GUI.skin.box);
            _panelStyle.normal.background = bg;
            _panelStyle.border = new RectOffset(10, 10, 10, 10);
            _panelStyle.padding = new RectOffset(10, 10, 10, 10);
        }
        if (_topBarStyle == null)
        {
            Texture2D top = new Texture2D(1, 1);
            top.SetPixel(0, 0, new Color(0.10f, 0.18f, 0.30f, 0.95f));
            top.Apply();

            _topBarStyle = new GUIStyle(GUI.skin.box);
            _topBarStyle.normal.background = top;
            _topBarStyle.border = new RectOffset(8, 8, 8, 8);
        }
        if (_textStyle == null)
        {
            _textStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 16,
                richText = true,
                wordWrap = true
            };
            _textStyle.normal.textColor = Color.white;
        }
        if (_titleStyle == null)
        {
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                richText = true
            };
            _titleStyle.normal.textColor = new Color(0.82f, 0.93f, 1f);
        }
        if (_buttonStyle == null)
        {
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }
    }

    private static string ResolveRenderPipelineName()
    {
        RenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline;
        if (asset == null) return "Built-in Render Pipeline";

        string typeName = asset.GetType().Name;
        if (string.IsNullOrEmpty(typeName)) return "Scriptable Render Pipeline";
        return typeName;
    }

    private void UpdateFrameTimings()
    {
        FrameTimingManager.CaptureFrameTimings();
        uint count = FrameTimingManager.GetLatestTimings(1, _timings);
        if (count > 0)
        {
            float cpu = (float)_timings[0].cpuFrameTime;
            float gpu = (float)_timings[0].gpuFrameTime;

            if (cpu > 0.01f) _cpuMsSmoothed = Mathf.Lerp(_cpuMsSmoothed <= 0f ? cpu : _cpuMsSmoothed, cpu, 0.2f);
            if (gpu > 0.01f) _gpuMsSmoothed = Mathf.Lerp(_gpuMsSmoothed <= 0f ? gpu : _gpuMsSmoothed, gpu, 0.2f);
        }

        if (_cpuMsSmoothed <= 0f) _cpuMsSmoothed = _frameMsSmoothed;
        if (_gpuMsSmoothed <= 0f) _gpuMsSmoothed = _frameMsSmoothed;
    }

    private void CaptureInput()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        float mx = Input.GetAxisRaw("Mouse X");
        float my = Input.GetAxisRaw("Mouse Y");
        bool jump = Input.GetButton("Jump");

        bool hasAxisInput = Mathf.Abs(h) > 0.001f || Mathf.Abs(v) > 0.001f || Mathf.Abs(mx) > 0.001f || Mathf.Abs(my) > 0.001f;
        bool hasButtonInput = jump || Input.anyKey;

        float jx = TryGetAxis("JoyX");
        float jy = TryGetAxis("JoyY");
        float jrx = TryGetAxis("Joy4");
        float jry = TryGetAxis("Joy5");
        bool hasJoystickInput = Mathf.Abs(jx) > 0.05f || Mathf.Abs(jy) > 0.05f || Mathf.Abs(jrx) > 0.05f || Mathf.Abs(jry) > 0.05f;

        if (hasAxisInput || hasButtonInput || hasJoystickInput)
        {
            _lastInput = string.Format(
                CultureInfo.InvariantCulture,
                "H:{0:F2} V:{1:F2} MX:{2:F2} MY:{3:F2} Jump:{4} Joy({5:F2},{6:F2},{7:F2},{8:F2}) AnyKey:{9}",
                h, v, mx, my, jump ? 1 : 0, jx, jy, jrx, jry, Input.anyKey ? 1 : 0
            );
            _lastInputTime = Time.realtimeSinceStartup;

            if (logInputToConsole && Time.realtimeSinceStartup >= _nextConsoleLogTime)
            {
                Debug.Log("[BuildDebugOverlay] Input " + _lastInput);
                _nextConsoleLogTime = Time.realtimeSinceStartup + Mathf.Max(0.02f, consoleLogInterval);
            }
        }
    }

    private void TickCsvLogging()
    {
        if (!enableCsvLogging || string.IsNullOrEmpty(_csvPath)) return;

        float now = Time.realtimeSinceStartup;
        if (now >= _nextCsvSampleTime)
        {
            AppendCsvSample();
            _nextCsvSampleTime = now + Mathf.Max(0.05f, csvSampleInterval);
        }

        if (now >= _nextCsvFlushTime)
        {
            FlushCsvStatus();
            _nextCsvFlushTime = now + Mathf.Max(0.5f, csvFlushInterval);
        }
    }

    private void InitializeCsvFile()
    {
        if (!enableCsvLogging) return;

        try
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string day = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            _csvPath = Path.Combine(desktop, "BuildDebugOverlay_" + day + ".csv");

            if (!File.Exists(_csvPath))
            {
                string header = "time,frame,fps,avg120,oneLow,cpuMs,gpuMs,loadPct,latencyMs,inputAgeMs,controllers,memMb,width,height,refresh,targetFps,vSync\n";
                File.WriteAllText(_csvPath, header, Encoding.UTF8);
            }

            _lastCsvStatus = "CSV: " + _csvPath;
        }
        catch (Exception ex)
        {
            _lastCsvStatus = "CSV init failed: " + ex.Message;
            _csvPath = null;
        }
    }

    private void AppendCsvSample()
    {
        try
        {
            int controllers = GetConnectedControllerCount();
            float avgMs = GetAverageFrameMs();
            float oneLow = GetOnePercentLowFps();
            float budgetMs = GetFrameBudgetMs();
            float loadPct = budgetMs > 0.001f ? Mathf.Clamp01(avgMs / budgetMs) * 100f : 0f;
            float inputAgeMs = _lastInputTime > 0f ? (Time.realtimeSinceStartup - _lastInputTime) * 1000f : -1f;
            float latencyMs = inputAgeMs < 0f ? _frameMsSmoothed : inputAgeMs + Mathf.Max(_cpuMsSmoothed, _gpuMsSmoothed);
            float memMb = Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f);

            string line =
                DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + "," +
                _frameMsSmoothed.ToString("F3", CultureInfo.InvariantCulture) + "," +
                _fpsSmoothed.ToString("F2", CultureInfo.InvariantCulture) + "," +
                avgMs.ToString("F3", CultureInfo.InvariantCulture) + "," +
                oneLow.ToString("F2", CultureInfo.InvariantCulture) + "," +
                _cpuMsSmoothed.ToString("F3", CultureInfo.InvariantCulture) + "," +
                _gpuMsSmoothed.ToString("F3", CultureInfo.InvariantCulture) + "," +
                loadPct.ToString("F1", CultureInfo.InvariantCulture) + "," +
                latencyMs.ToString("F2", CultureInfo.InvariantCulture) + "," +
                inputAgeMs.ToString("F2", CultureInfo.InvariantCulture) + "," +
                controllers + "," +
                memMb.ToString("F1", CultureInfo.InvariantCulture) + "," +
                Screen.width + "," +
                Screen.height + "," +
                Screen.currentResolution.refreshRate + "," +
                Application.targetFrameRate + "," +
                QualitySettings.vSyncCount + "\n";

            File.AppendAllText(_csvPath, line, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            _lastCsvStatus = "CSV write failed: " + ex.Message;
        }
    }

    private void FlushCsvStatus()
    {
        if (string.IsNullOrEmpty(_csvPath))
        {
            _lastCsvStatus = "CSV: disabled or unavailable";
            return;
        }

        try
        {
            using (FileStream fs = new FileStream(_csvPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                fs.Flush(true);
            }
            _lastCsvStatus = "CSV flushed: " + _csvPath;
        }
        catch (Exception ex)
        {
            _lastCsvStatus = "CSV flush failed: " + ex.Message;
        }
    }

    private int GetConnectedControllerCount()
    {
        string[] names = Input.GetJoystickNames();
        int count = 0;
        for (int i = 0; i < names.Length; i++)
        {
            if (!string.IsNullOrEmpty(names[i])) count++;
        }
        return count;
    }

    private static float TryGetAxis(string axisName)
    {
        try { return Input.GetAxisRaw(axisName); }
        catch { return 0f; }
    }

    private float GetAverageFrameMs()
    {
        int count = _frameBufferFilled ? _frameTimes.Length : _frameCursor;
        if (count <= 0) return _frameMsSmoothed;

        float sum = 0f;
        for (int i = 0; i < count; i++) sum += _frameTimes[i];
        return sum / count;
    }

    private float GetOnePercentLowFps()
    {
        int count = _frameBufferFilled ? _frameTimes.Length : _frameCursor;
        if (count <= 5) return _fpsSmoothed;

        for (int i = 0; i < count; i++) _frameScratch[i] = _frameTimes[i];
        Array.Sort(_frameScratch, 0, count);
        int index = Mathf.Clamp(Mathf.CeilToInt(count * 0.99f) - 1, 0, count - 1);
        float msAt99 = _frameScratch[index];
        return msAt99 > 0.001f ? 1000f / msAt99 : _fpsSmoothed;
    }

    private static float GetFrameBudgetMs()
    {
        int targetFps = Application.targetFrameRate;
        if (targetFps > 0) return 1000f / targetFps;

        if (QualitySettings.vSyncCount > 0)
        {
            int refresh = Screen.currentResolution.refreshRate;
            if (refresh > 0) return 1000f * QualitySettings.vSyncCount / refresh;
        }

        return DefaultFrameBudgetMs60Fps;
    }
}

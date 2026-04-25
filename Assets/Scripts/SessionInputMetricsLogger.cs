using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Records per-session input metrics into a local CSV file.
/// Captures keyboard, mouse, joystick buttons, and common axes.
/// </summary>
public class SessionInputMetricsLogger : MonoBehaviour
{
    private static SessionInputMetricsLogger _instance;

    private static readonly string[] TrackedAxes =
    {
        "Horizontal",
        "Vertical",
        "Mouse X",
        "Mouse Y",
        "Mouse ScrollWheel",
        "RightStickX",
        "RightStickY",
        "JoyX",
        "JoyY",
        "Joy4",
        "Joy5",
        "Submit",
        "Cancel",
        "Jump",
        "Fire1",
        "Fire2",
        "Fire3"
    };

    private readonly Dictionary<string, float> _lastAxisValue = new Dictionary<string, float>(32);
    private readonly List<string> _buffer = new List<string>(512);
    private readonly StringBuilder _lineBuilder = new StringBuilder(512);
    private KeyCode[] _allKeyCodes;

    private string _csvPath;
    private string _sessionId;
    private string _sessionStartUtc;
    private float _sessionStartRealtime;
    private bool _sessionEnded;
    private float _nextFlushTime;
    private string _lastJoystickSignature = string.Empty;

    [Header("Capture")]
    [SerializeField] private float axisNoiseThreshold = 0.0001f;
    [SerializeField] private float axisDeltaThreshold = 0.02f;

    [Header("Disk")]
    [SerializeField] private bool flushEveryNSeconds = true;
    [SerializeField] private float flushIntervalSeconds = 2.0f;
    [SerializeField] private int maxBufferedLines = 256;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (_instance != null) return;

        SessionInputMetricsLogger existing = FindObjectOfType<SessionInputMetricsLogger>();
        if (existing != null)
        {
            _instance = existing;
            return;
        }

        GameObject go = new GameObject("SessionInputMetricsLogger");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<SessionInputMetricsLogger>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        _sessionId = Guid.NewGuid().ToString("N");
        _sessionStartUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        _sessionStartRealtime = Time.realtimeSinceStartup;
        _allKeyCodes = (KeyCode[])Enum.GetValues(typeof(KeyCode));

        InitializeCsvFile();
        LogEvent("session", "system", "session_start", "1", Application.productName + " started");
        CaptureJoystickConnectionChanges(forceLog: true);
    }

    private void Update()
    {
        if (_sessionEnded) return;

        CaptureKeyboardAndJoystickButtons();
        CaptureMouse();
        CaptureAxes();
        CaptureJoystickConnectionChanges(forceLog: false);

        if (_buffer.Count >= maxBufferedLines)
            FlushToDisk();

        if (flushEveryNSeconds && Time.unscaledTime >= _nextFlushTime)
        {
            FlushToDisk();
            _nextFlushTime = Time.unscaledTime + Mathf.Max(0.25f, flushIntervalSeconds);
        }
    }

    private void OnApplicationQuit()
    {
        EndSession("application_quit");
    }

    private void OnDestroy()
    {
        if (_instance == this)
            EndSession("destroy");
    }

    private void EndSession(string reason)
    {
        if (_sessionEnded) return;

        _sessionEnded = true;
        LogEvent("session", "system", "session_end", "1", reason);
        FlushToDisk();
    }

    private void InitializeCsvFile()
    {
        string dir = Path.Combine(Application.persistentDataPath, "InputMetrics");
        Directory.CreateDirectory(dir);

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
        _csvPath = Path.Combine(dir, "session_input_metrics_" + timestamp + ".csv");

        const string header =
            "session_id,session_start_utc,event_time_utc,elapsed_sec,frame,event_type,device,input_name,value,details\n";
        File.WriteAllText(_csvPath, header, Encoding.UTF8);
        _nextFlushTime = Time.unscaledTime + Mathf.Max(0.25f, flushIntervalSeconds);

        Debug.Log("[SessionInputMetricsLogger] CSV: " + _csvPath);
    }

    private void CaptureKeyboardAndJoystickButtons()
    {
        for (int i = 0; i < _allKeyCodes.Length; i++)
        {
            KeyCode code = _allKeyCodes[i];
            if (Input.GetKeyDown(code))
            {
                string device = IsJoystickKey(code) ? "gamepad" : "keyboard";
                LogEvent("button_down", device, code.ToString(), "1", string.Empty);
            }

            if (Input.GetKeyUp(code))
            {
                string device = IsJoystickKey(code) ? "gamepad" : "keyboard";
                LogEvent("button_up", device, code.ToString(), "0", string.Empty);
            }
        }
    }

    private void CaptureMouse()
    {
        float mouseX = Input.GetAxisRaw("Mouse X");
        float mouseY = Input.GetAxisRaw("Mouse Y");
        float mouseScroll = Input.GetAxisRaw("Mouse ScrollWheel");

        if (Mathf.Abs(mouseX) > axisNoiseThreshold || Mathf.Abs(mouseY) > axisNoiseThreshold)
        {
            LogEvent(
                "pointer_move",
                "mouse",
                "MouseDelta",
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:F4}|{1:F4}",
                    mouseX,
                    mouseY),
                string.Empty);
        }

        if (Mathf.Abs(mouseScroll) > axisNoiseThreshold)
        {
            LogEvent(
                "axis",
                "mouse",
                "Mouse ScrollWheel",
                mouseScroll.ToString("F4", CultureInfo.InvariantCulture),
                string.Empty);
        }
    }

    private void CaptureAxes()
    {
        for (int i = 0; i < TrackedAxes.Length; i++)
        {
            string axis = TrackedAxes[i];
            float value;
            if (!TryGetAxisRaw(axis, out value))
                continue;

            float last;
            bool hadLast = _lastAxisValue.TryGetValue(axis, out last);
            _lastAxisValue[axis] = value;

            bool nearZeroNow = Mathf.Abs(value) <= axisNoiseThreshold;
            bool nearZeroBefore = !hadLast || Mathf.Abs(last) <= axisNoiseThreshold;
            bool activated = nearZeroBefore && !nearZeroNow;
            bool changed = hadLast && Mathf.Abs(value - last) >= axisDeltaThreshold;
            bool released = hadLast && !nearZeroBefore && nearZeroNow;

            if (!(activated || changed || released))
                continue;

            string device = IsGamepadAxis(axis) ? "gamepad_axis" : "axis";
            LogEvent(
                "axis",
                device,
                axis,
                value.ToString("F4", CultureInfo.InvariantCulture),
                hadLast ? ("prev=" + last.ToString("F4", CultureInfo.InvariantCulture)) : string.Empty);
        }
    }

    private void CaptureJoystickConnectionChanges(bool forceLog)
    {
        string[] names = Input.GetJoystickNames();
        StringBuilder sb = new StringBuilder(128);
        int connected = 0;
        for (int i = 0; i < names.Length; i++)
        {
            if (string.IsNullOrEmpty(names[i])) continue;
            connected++;
            if (sb.Length > 0) sb.Append(" | ");
            sb.Append(names[i]);
        }

        string signature = connected + ":" + sb;
        if (!forceLog && signature == _lastJoystickSignature)
            return;

        _lastJoystickSignature = signature;
        LogEvent(
            "device",
            "gamepad",
            "joystick_connection",
            connected.ToString(CultureInfo.InvariantCulture),
            sb.Length > 0 ? sb.ToString() : "none");
    }

    private void LogEvent(
        string eventType,
        string device,
        string inputName,
        string value,
        string details)
    {
        _lineBuilder.Length = 0;

        string eventUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        float elapsed = Mathf.Max(0f, Time.realtimeSinceStartup - _sessionStartRealtime);

        AppendCsv(_sessionId);
        AppendCsv(_sessionStartUtc);
        AppendCsv(eventUtc);
        AppendCsv(elapsed.ToString("F4", CultureInfo.InvariantCulture));
        AppendCsv(Time.frameCount.ToString(CultureInfo.InvariantCulture));
        AppendCsv(eventType);
        AppendCsv(device);
        AppendCsv(inputName);
        AppendCsv(value);
        AppendCsv(details);

        _lineBuilder.Append('\n');
        _buffer.Add(_lineBuilder.ToString());
    }

    private void AppendCsv(string raw)
    {
        if (_lineBuilder.Length > 0) _lineBuilder.Append(',');
        _lineBuilder.Append(EscapeCsv(raw ?? string.Empty));
    }

    private static string EscapeCsv(string s)
    {
        bool mustQuote = s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
        if (!mustQuote) return s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    private void FlushToDisk()
    {
        if (_buffer.Count == 0 || string.IsNullOrEmpty(_csvPath))
            return;

        try
        {
            File.AppendAllLines(_csvPath, _buffer, Encoding.UTF8);
            _buffer.Clear();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[SessionInputMetricsLogger] CSV write failed: " + ex.Message);
        }
    }

    private static bool TryGetAxisRaw(string axisName, out float value)
    {
        try
        {
            value = Input.GetAxisRaw(axisName);
            return true;
        }
        catch
        {
            value = 0f;
            return false;
        }
    }

    private static bool IsJoystickKey(KeyCode code)
    {
        string name = code.ToString();
        return name.IndexOf("Joystick", StringComparison.Ordinal) >= 0;
    }

    private static bool IsGamepadAxis(string axis)
    {
        return axis.StartsWith("RightStick", StringComparison.Ordinal)
            || axis.StartsWith("Joy", StringComparison.Ordinal);
    }
}

using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Writes down, on every copy of the game on the Vizlab wall, what that copy believes is
/// happening - so the copies can be laid side by side afterwards and checked for agreeing.
/// Tools > Journey NEW > Vizlab > Compare Cluster Probe Logs does the laying side by side.
///
/// WHY. On the wall the piece is four copies (row 1, rows 2-3, 4-5, 6-7) kept in step by getReal3D, and when they fall
/// out of step the symptom is only a picture that does not meet at the seams. Whether that
/// is a late start, clocks that differ, a load that landed on different frames or a stick
/// read on one copy only cannot be told from looking. Each of those leaves a different
/// trace in these files.
///
/// WHAT. One CSV per copy, in persistentDataPath/ClusterProbe (on Windows,
/// %USERPROFILE%\AppData\LocalLow\&lt;company&gt;\&lt;product&gt;\ClusterProbe), named with
/// GetReal3DRuntime_NEW.InstanceTag. The header has the copy's identity and its startup:
/// when the process started and how long it took to reach its first frame, which is where
/// a popup that held one copy up shows. Then a row every everyClusterFrames frames of the
/// cluster's own frame counter, so rows from different copies line up by number:
///
///   both clocks     Time.time / deltaTime and getReal3D.Cluster.time / deltaTime. If
///                   Unity's differ between copies and the cluster's do not, anything timed
///                   with Time drifts apart on the wall.
///   the picture     the light's position, the camera's position, yaw, pitch and field of
///                   view, the layer, the scene.
///   the rest        the Random state (a hash), the input source, the window size.
///
/// WHEN. Only when the getReal3D launcher started the process, or with -cluster-probe on
/// the command line (to try it at a desk). Never otherwise: at a desk it does not exist.
/// </summary>
[DisallowMultipleComponent]
public class ClusterSyncProbe_NEW : MonoBehaviour
{
    const string CommandLineFlag = "-cluster-probe";

    [Tooltip("A row every this many cluster frames. 30 is about two a second at 60 fps.")]
    [Min(1)]
    [SerializeField] int everyClusterFrames = 30;

    [Tooltip("Seconds between writes to disk. The rows are kept in memory in between.")]
    [Min(0.5f)]
    [SerializeField] float flushSeconds = 2f;

    StreamWriter _writer;
    readonly StringBuilder _sb = new StringBuilder(512);
    float _nextFlush;
    bool _firstFrameWritten;
    int _lastRowFrame = -1;

    PlayerRig_NEW _light;
    LayerState_NEW _layers;

    /// <summary>Where the files go. Shown in the log at start.</summary>
    public static string Folder { get { return Path.Combine(Application.persistentDataPath, "ClusterProbe"); } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoInstall()
    {
        if (!GetReal3DRuntime_NEW.LaunchedByGetReal3D && !HasFlag()) return;
        if (FindObjectOfType<ClusterSyncProbe_NEW>() != null) return;

        var go = new GameObject("Cluster Sync Probe (auto)");
        DontDestroyOnLoad(go);
        go.AddComponent<ClusterSyncProbe_NEW>();
    }

    static bool HasFlag()
    {
        foreach (string arg in Environment.GetCommandLineArgs())
            if (string.Equals(arg, CommandLineFlag, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    void Awake()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string tag = GetReal3DRuntime_NEW.InstanceTag;
            if (tag.Length == 0) tag = "_desk_" + System.Diagnostics.Process.GetCurrentProcess().Id;
            string path = Path.Combine(Folder, "probe_" + stamp + tag + ".csv");

            _writer = new StreamWriter(path, false, new UTF8Encoding(false));
            WriteHeader();
            Debug.Log("[ClusterSyncProbe_NEW] writing " + path, this);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ClusterSyncProbe_NEW] cannot write the probe file, so it is off: " + e.Message, this);
            enabled = false;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
        FindSubjects();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Close();
    }

    void OnApplicationQuit() { Close(); }

    void Close()
    {
        if (_writer == null) return;
        try { _writer.Flush(); _writer.Dispose(); } catch (Exception) { }
        _writer = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        FindSubjects();
        LogEvent("scene_loaded", scene.name);
    }

    void FindSubjects()
    {
        _light = FindObjectOfType<PlayerRig_NEW>();
        _layers = FindObjectOfType<LayerState_NEW>();
    }

    // ── Header ───────────────────────────────────────────────────────────────

    void WriteHeader()
    {
        var p = System.Diagnostics.Process.GetCurrentProcess();

        Meta("format", "ClusterSyncProbe_NEW 1");
        Meta("instance", GetReal3DRuntime_NEW.InstanceTag.Length > 0 ? GetReal3DRuntime_NEW.InstanceTag.TrimStart('_') : "desk");
        Meta("master", GetReal3DRuntime_NEW.IsMaster ? "1" : "0");
        Meta("pid", p.Id.ToString(CultureInfo.InvariantCulture));
        Meta("host", Environment.MachineName);
        Meta("getreal_config", Environment.GetEnvironmentVariable("GETREAL_CONFIG") ?? "");
        Meta("process_start_utc", Utc(p.StartTime.ToUniversalTime()));
        Meta("probe_start_utc", Utc(DateTime.UtcNow));
        Meta("probe_start_realtime", F(Time.realtimeSinceStartup));
        Meta("product", Application.productName + " " + Application.version);
        Meta("command_line", Environment.CommandLine.Replace(',', ' '));

        _writer.WriteLine("kind,cluster_frame,unity_frame,time,cluster_time,dt,cluster_dt,unscaled_time,realtime," +
                          "scene,layer,light_x,light_y,light_z,cam_x,cam_y,cam_z,cam_yaw,cam_pitch,cam_fov," +
                          "random,input,screen_w,screen_h,window_w,window_h,note");
    }

    void Meta(string key, string value)
    {
        _writer.WriteLine("# " + key + "=" + value);
    }

    // ── Rows ─────────────────────────────────────────────────────────────────

    void LateUpdate()
    {
        if (_writer == null) return;

        int clusterFrame = ClusterFrame();

        if (!_firstFrameWritten)
        {
            _firstFrameWritten = true;
            FindSubjects();   // the first scene's objects exist by now, whatever sceneLoaded did
            // The process's first frame: how long it took to get here is the startup delay,
            // and the UTC moment is what lines the copies' starts up against each other.
            LogEvent("first_frame", "utc " + Utc(DateTime.UtcNow));
        }

        if (clusterFrame != _lastRowFrame && clusterFrame % everyClusterFrames == 0)
        {
            _lastRowFrame = clusterFrame;
            Row("row", clusterFrame, null);
        }

        if (Time.unscaledTime >= _nextFlush)
        {
            _nextFlush = Time.unscaledTime + flushSeconds;
            try { _writer.Flush(); } catch (Exception) { }
        }
    }

    void LogEvent(string kind, string note)
    {
        if (_writer == null) return;
        Row(kind, ClusterFrame(), note);
    }

    void Row(string kind, int clusterFrame, string note)
    {
        Vector3 light = _light != null ? _light.transform.position : Vector3.zero;

        Camera cam = GetReal3DView_NEW.ScreenCamera;
        Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;
        Vector3 camEuler = cam != null ? cam.transform.eulerAngles : Vector3.zero;
        float fov = cam != null ? cam.fieldOfView : 0f;

        int windowW = 0, windowH = 0;
        if (GetReal3DRuntime_NEW.LaunchedByGetReal3D)
        {
            try
            {
                windowW = (int)getReal3D.Plugin.getAppWindowWidth();
                windowH = (int)getReal3D.Plugin.getAppWindowHeight();
            }
            catch (Exception) { }
        }

        _sb.Length = 0;
        _sb.Append(kind).Append(',')
           .Append(clusterFrame).Append(',')
           .Append(Time.frameCount).Append(',')
           .Append(F(Time.time)).Append(',')
           .Append(F(ClusterTime())).Append(',')
           .Append(F(Time.deltaTime)).Append(',')
           .Append(F(ClusterDeltaTime())).Append(',')
           .Append(F(Time.unscaledTime)).Append(',')
           .Append(F(Time.realtimeSinceStartup)).Append(',')
           .Append(Clean(SceneManager.GetActiveScene().name)).Append(',')
           .Append(Clean(_layers != null ? _layers.CurrentLayerId : "")).Append(',')
           .Append(F(light.x)).Append(',').Append(F(light.y)).Append(',').Append(F(light.z)).Append(',')
           .Append(F(camPos.x)).Append(',').Append(F(camPos.y)).Append(',').Append(F(camPos.z)).Append(',')
           .Append(F(camEuler.y)).Append(',').Append(F(camEuler.x)).Append(',').Append(F(fov)).Append(',')
           .Append(RandomHash()).Append(',')
           .Append(InputScheme_NEW.InputSource).Append(',')
           .Append(Screen.width).Append(',').Append(Screen.height).Append(',')
           .Append(windowW).Append(',').Append(windowH).Append(',')
           .Append(Clean(note));

        _writer.WriteLine(_sb.ToString());
    }

    // ── Reading the cluster, safely ──────────────────────────────────────────

    /// <summary>getReal3D's frame counter, the same on every copy; Unity's at a desk.</summary>
    static int ClusterFrame()
    {
        if (!GetReal3DRuntime_NEW.LaunchedByGetReal3D) return Time.frameCount;
        try { return (int)getReal3D.Cluster.frameCount; }
        catch (Exception) { return Time.frameCount; }
    }

    static float ClusterTime()
    {
        if (!GetReal3DRuntime_NEW.LaunchedByGetReal3D) return Time.time;
        try { return (float)getReal3D.Cluster.time; }
        catch (Exception) { return -1f; }
    }

    static float ClusterDeltaTime()
    {
        if (!GetReal3DRuntime_NEW.LaunchedByGetReal3D) return Time.deltaTime;
        try { return (float)getReal3D.Cluster.deltaTime; }
        catch (Exception) { return -1f; }
    }

    /// <summary>
    /// A hash of the Random state, read without drawing from it. getReal3D seeds Random the
    /// same on every copy; if the hashes part, something drew a number on one copy only.
    /// </summary>
    static string RandomHash()
    {
        string json = JsonUtility.ToJson(UnityEngine.Random.state);
        uint h = 2166136261;
        for (int i = 0; i < json.Length; i++) { h ^= json[i]; h *= 16777619; }
        return h.ToString("x8", CultureInfo.InvariantCulture);
    }

    static string F(float v) { return v.ToString("0.#####", CultureInfo.InvariantCulture); }

    static string Utc(DateTime t) { return t.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture); }

    static string Clean(string s) { return string.IsNullOrEmpty(s) ? "" : s.Replace(',', ' ').Replace('\n', ' '); }
}

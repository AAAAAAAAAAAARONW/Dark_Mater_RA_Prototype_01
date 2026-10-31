using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > Vizlab > Compare Cluster Probe Logs. Reads the CSVs ClusterSyncProbe_NEW wrote on
/// each copy of the game in one wall run, lines them up by the cluster's frame number and
/// says where the copies disagreed, and when it started. The report goes to the Console and
/// to probe_report.txt in the folder.
///
/// Pick a folder with one run's files in it (copy them off the Vizlab machine from
/// ClusterSyncProbe_NEW.Folder). If it holds several runs, the newest is taken: files whose
/// probe started within two minutes of the newest one.
///
/// WHAT IT LOOKS AT, in the order a wall problem usually shows:
///   startup     how far apart the copies' processes started, and how long each took to
///               its first frame. A copy held up by a popup is the one that took longest.
///   windows     each copy's window size, from getReal3D and from Unity. A window Unity
///               resized itself does not match getReal3D's.
///   clocks      Time.time against getReal3D.Cluster.time, across copies. If Unity's own
///               clock differs between copies, anything timed by it drifts on the wall.
///   picture     the light, the camera and its field of view, the layer and the scene,
///               frame by frame; the first frame they part is the one to look at.
///   random      the Random state; it parts when one copy alone draws a number.
/// </summary>
static class ClusterProbeCompareMenu_NEW
{
    const float PositionTolerance = 0.01f;   // world units
    const float AngleTolerance = 0.05f;      // degrees
    const float TimeTolerance = 0.002f;      // seconds

    class Probe
    {
        public string file;
        public readonly Dictionary<string, string> meta = new Dictionary<string, string>();
        public readonly Dictionary<int, string[]> rows = new Dictionary<int, string[]>();
        public readonly List<string[]> events = new List<string[]>();
        public string Name { get { string n; return meta.TryGetValue("instance", out n) ? n : Path.GetFileName(file); } }
        public DateTime Started { get { return Utc("probe_start_utc"); } }

        public DateTime Utc(string key)
        {
            string v;
            DateTime t;
            if (meta.TryGetValue(key, out v) &&
                DateTime.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out t))
                return t;
            return DateTime.MinValue;
        }
    }

    // Column indices in a row (see ClusterSyncProbe_NEW.WriteHeader).
    const int Kind = 0, ClusterFrame = 1, UnityFrame = 2, TimeCol = 3, ClusterTime = 4, Dt = 5, ClusterDt = 6,
              Realtime = 8, Scene = 9, Layer = 10, LightX = 11, CamX = 14, CamYaw = 17, CamPitch = 18, CamFov = 19,
              RandomCol = 20, InputCol = 21, ScreenW = 22, ScreenH = 23, WindowW = 24, WindowH = 25, Note = 26;

    [MenuItem("Tools/Vizlab/Compare Cluster Probe Logs...")]
    static void Compare()
    {
        string folder = EditorUtility.OpenFolderPanel("Folder with one wall run's probe_*.csv files", "", "");
        if (string.IsNullOrEmpty(folder)) return;

        List<Probe> probes = Directory.GetFiles(folder, "probe_*.csv").Select(Read).Where(p => p != null).ToList();
        if (probes.Count == 0)
        {
            Debug.LogWarning("Vizlab: no probe_*.csv files in " + folder + ".");
            return;
        }

        // The newest run only.
        DateTime newest = probes.Max(p => p.Started);
        probes = probes.Where(p => (newest - p.Started).TotalMinutes <= 2).OrderByDescending(p => p.meta.ContainsKey("master") && p.meta["master"] == "1").ToList();

        string report = Report(probes);
        File.WriteAllText(Path.Combine(folder, "probe_report.txt"), report, new UTF8Encoding(false));
        Debug.Log("Vizlab: cluster probe report (also in " + Path.Combine(folder, "probe_report.txt") + ")\n" + report);
    }

    static Probe Read(string file)
    {
        var p = new Probe { file = file };
        try
        {
            foreach (string line in File.ReadAllLines(file))
            {
                if (line.StartsWith("# "))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 2) p.meta[line.Substring(2, eq - 2)] = line.Substring(eq + 1);
                    continue;
                }
                if (line.StartsWith("kind,")) continue;

                string[] c = line.Split(',');
                if (c.Length <= Note) continue;
                if (c[Kind] == "row") p.rows[I(c[ClusterFrame])] = c;
                else p.events.Add(c);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("Vizlab: could not read " + file + ": " + e.Message);
            return null;
        }
        return p;
    }

    static string Report(List<Probe> probes)
    {
        var sb = new StringBuilder();
        sb.AppendLine(probes.Count + " cop" + (probes.Count == 1 ? "y" : "ies") + ": " + string.Join(", ", probes.Select(p => p.Name).ToArray()));
        if (probes.Count == 1) sb.AppendLine("Only one copy in this run: nothing to compare against, but its startup and window are below.");
        sb.AppendLine();

        // ── Startup ──
        sb.AppendLine("STARTUP");
        DateTime firstStart = probes.Min(p => p.Utc("process_start_utc"));
        foreach (Probe p in probes)
        {
            string[] ff = p.events.FirstOrDefault(e => e[Kind] == "first_frame");
            string toFirst = ff != null ? ff[Realtime] + " s" : "?";
            sb.AppendFormat("  {0,-22} process started +{1:0.00} s after the first copy, first frame {2} after its own start{3}\n",
                            p.Name, (p.Utc("process_start_utc") - firstStart).TotalSeconds, toFirst,
                            p.meta.ContainsKey("master") && p.meta["master"] == "1" ? "  (master)" : "");
        }
        double spread = (probes.Max(p => p.Utc("process_start_utc")) - firstStart).TotalSeconds;
        if (spread > 1.0)
            sb.AppendLine("  !! The copies started " + spread.ToString("0.0") + " s apart. Something held one back (a popup, a slow disk, the launcher).");
        sb.AppendLine();

        // ── Windows ──
        sb.AppendLine("WINDOWS");
        foreach (Probe p in probes)
        {
            string[] last = p.rows.Count > 0 ? p.rows[p.rows.Keys.Max()] : null;
            if (last == null) { sb.AppendLine("  " + p.Name + ": no rows"); continue; }
            bool mismatch = I(last[WindowW]) > 0 && (I(last[WindowW]) != I(last[ScreenW]) || I(last[WindowH]) != I(last[ScreenH]));
            sb.AppendFormat("  {0,-22} Unity {1} x {2}, getReal3D {3} x {4}{5}\n", p.Name, last[ScreenW], last[ScreenH],
                            last[WindowW], last[WindowH], mismatch ? "   !! differ: something resized this window after getReal3D placed it" : "");
        }
        sb.AppendLine();

        if (probes.Count < 2) return sb.ToString();

        // ── Frame by frame ──
        List<int> common = probes[0].rows.Keys.Where(f => probes.All(p => p.rows.ContainsKey(f))).OrderBy(f => f).ToList();
        sb.AppendLine("FRAME BY FRAME (" + common.Count + " cluster frames every copy wrote)");
        if (common.Count == 0)
        {
            sb.AppendLine("  !! No cluster frame in common. The copies are not counting the same frames: they are not one cluster.");
            return sb.ToString();
        }

        var worst = new Dictionary<string, float>();
        var first = new Dictionary<string, int>();
        foreach (int f in common)
        {
            string[][] rs = probes.Select(p => p.rows[f]).ToArray();
            Check(worst, first, f, "Time.time", Spread(rs, TimeCol), TimeTolerance);
            Check(worst, first, f, "Time.deltaTime", Spread(rs, Dt), TimeTolerance);
            Check(worst, first, f, "Cluster.time", Spread(rs, ClusterTime), TimeTolerance);
            Check(worst, first, f, "Unity frame number", Spread(rs, UnityFrame), 0.5f);
            Check(worst, first, f, "light position", SpreadVector(rs, LightX), PositionTolerance);
            Check(worst, first, f, "camera position", SpreadVector(rs, CamX), PositionTolerance);
            Check(worst, first, f, "camera yaw", SpreadAngle(rs, CamYaw), AngleTolerance);
            Check(worst, first, f, "camera pitch", SpreadAngle(rs, CamPitch), AngleTolerance);
            Check(worst, first, f, "field of view", Spread(rs, CamFov), AngleTolerance);
            Check(worst, first, f, "scene", Differs(rs, Scene), 0.5f);
            Check(worst, first, f, "layer", Differs(rs, Layer), 0.5f);
            Check(worst, first, f, "Random state", Differs(rs, RandomCol), 0.5f);
            Check(worst, first, f, "input source", Differs(rs, InputCol), 0.5f);
        }

        foreach (var kv in worst)
        {
            int at;
            bool parted = first.TryGetValue(kv.Key, out at);
            sb.AppendFormat("  {0,-20} {1}\n", kv.Key,
                            parted ? "!! parts at cluster frame " + at + ", worst spread " + kv.Value.ToString("0.#####", CultureInfo.InvariantCulture)
                                   : "agrees");
        }
        sb.AppendLine();

        // ── What it means ──
        sb.AppendLine("READING IT");
        if (first.ContainsKey("Time.time") && !first.ContainsKey("Cluster.time"))
            sb.AppendLine("  Unity's Time differs between copies while getReal3D's agrees: Time is NOT shared on this cluster. " +
                          "Motion and timers driven by Time.time / Time.deltaTime will drift apart; they need Cluster.time / Cluster.deltaTime.");
        if (!first.ContainsKey("Time.time"))
            sb.AppendLine("  Unity's Time agrees across copies: getReal3D is sharing the clock.");
        if (first.ContainsKey("Cluster.time"))
            sb.AppendLine("  Even getReal3D's own clock differs between copies: they are not running as one cluster " +
                          "(a copy that never joined, or joined late). Fix the startup before anything else.");
        if (first.ContainsKey("light position") || first.ContainsKey("camera position") || first.ContainsKey("camera yaw"))
            sb.AppendLine("  The picture parted at cluster frame " +
                          new[] { "light position", "camera position", "camera yaw" }.Where(first.ContainsKey).Min(k => first[k]) +
                          ". Look at what happened around that frame: input, a scene load, a timer.");
        if (first.ContainsKey("scene"))
            sb.AppendLine("  The copies changed scene on different frames: a scene load that is not synchronised.");
        if (first.ContainsKey("Random state"))
            sb.AppendLine("  The Random state parted: something drew a random number on some copies only (often code behind a " +
                          "check that differs per copy - a camera, a window size, a key).");
        return sb.ToString();
    }

    static void Check(Dictionary<string, float> worst, Dictionary<string, int> first, int frame, string what, float spread, float tolerance)
    {
        float w;
        if (!worst.TryGetValue(what, out w) || spread > w) worst[what] = spread;
        if (spread > tolerance && !first.ContainsKey(what)) first[what] = frame;
    }

    static float Spread(string[][] rs, int col)
    {
        float lo = float.MaxValue, hi = float.MinValue;
        foreach (string[] r in rs) { float v = Fl(r[col]); lo = Mathf.Min(lo, v); hi = Mathf.Max(hi, v); }
        return hi - lo;
    }

    static float SpreadVector(string[][] rs, int col)
    {
        float worst = 0f;
        Vector3 a = V(rs[0], col);
        for (int i = 1; i < rs.Length; i++) worst = Mathf.Max(worst, Vector3.Distance(a, V(rs[i], col)));
        return worst;
    }

    static float SpreadAngle(string[][] rs, int col)
    {
        float worst = 0f;
        for (int i = 1; i < rs.Length; i++) worst = Mathf.Max(worst, Mathf.Abs(Mathf.DeltaAngle(Fl(rs[0][col]), Fl(rs[i][col]))));
        return worst;
    }

    static float Differs(string[][] rs, int col)
    {
        for (int i = 1; i < rs.Length; i++) if (rs[i][col] != rs[0][col]) return 1f;
        return 0f;
    }

    static Vector3 V(string[] r, int col) { return new Vector3(Fl(r[col]), Fl(r[col + 1]), Fl(r[col + 2])); }

    static float Fl(string s)
    {
        float v;
        return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0f;
    }

    static int I(string s)
    {
        int v;
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : 0;
    }
}

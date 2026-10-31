using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > Vizlab > Make Local Test Copy of a getReal3D Config. Takes the configuration the
/// Vizlab's getReal3D launcher uses and writes a copy that runs on one desk PC, so the
/// wall's whole startup - every copy of the game, their windows, the cluster forming, the
/// sync - can be rehearsed away from the wall with the getReal3D launcher and daemon.
///
/// WHAT CHANGES, and nothing else:
///   hostname      every computer becomes localhost, so every copy starts on this PC.
///   windows       every channel (one window, one copy of the game) is scaled down by one
///                 factor and laid out to fit this monitor: each computer's windows keep
///                 their arrangement, and computers sit side by side. One factor for all,
///                 so every window keeps its shape and the seams still meet.
///   fullScreen    off, and topMost off, so the editor and the launcher stay reachable.
///   displayId     1: everything on the main monitor.
///   name          " (local test)" on the end, to tell it apart in the launcher.
/// The screens, their projections, the trackers, the users and the inputs are the wall's,
/// untouched, so the picture each window shows is the picture that copy shows on the wall,
/// only smaller.
///
/// It edits the text in place rather than loading and re-saving the XML, so getReal3D's
/// boost serialisation - its DOCTYPE, ids, order and number formats - is kept exactly.
///
/// GETTING THE REAL CONFIG: on the Vizlab machine, the .gr3d the launcher is set to use
/// (the launcher's game settings show it). Keep a copy of it in the repo beside this file's
/// notes (GETREAL3D_GUIDE.md) so the team works from the same one.
/// </summary>
static class GetReal3DLocalConfigMenu_NEW
{
    const int Margin = 40;

    // A channel: its computer back-reference, then its window rectangle in whole pixels.
    // Vectors elsewhere in the file are floats with exponents and never follow </computer>.
    static readonly Regex Channel = new Regex(
        @"(</computer>\s*<x>)(-?\d+)(</x>\s*<y>)(-?\d+)(</y>\s*<width>)(\d+)(</width>\s*<height>)(\d+)(</height>)",
        RegexOptions.Compiled);

    static readonly Regex Hostname = new Regex(@"<hostname>([^<]*)</hostname>", RegexOptions.Compiled);

    class Window { public int computer, x, y, w, h; public Match match; }

    [MenuItem("Tools/Vizlab/Make Local Test Copy of a getReal3D Config...")]
    static void Make()
    {
        string source = EditorUtility.OpenFilePanel("The Vizlab's getReal3D config", "", "gr3d");
        if (string.IsNullOrEmpty(source)) return;

        string text = File.ReadAllText(source);

        // Where each computer starts in the file, so a channel can be put with its computer.
        MatchCollection hosts = Hostname.Matches(text);
        if (hosts.Count == 0)
        {
            Debug.LogError("Vizlab: no <hostname> in " + source + ". Is it a getReal3D config?");
            return;
        }

        var windows = new List<Window>();
        foreach (Match m in Channel.Matches(text))
        {
            int computer = 0;
            for (int i = 0; i < hosts.Count; i++) if (hosts[i].Index < m.Index) computer = i;
            windows.Add(new Window
            {
                computer = computer, match = m,
                x = int.Parse(m.Groups[2].Value), y = int.Parse(m.Groups[4].Value),
                w = int.Parse(m.Groups[6].Value), h = int.Parse(m.Groups[8].Value)
            });
        }
        if (windows.Count == 0)
        {
            Debug.LogError("Vizlab: found no channels (windows) in " + source + ".");
            return;
        }

        // Each computer's windows as one block; blocks side by side.
        var blockMinX = new Dictionary<int, int>();
        var blockMinY = new Dictionary<int, int>();
        var blockOffset = new Dictionary<int, int>();
        int totalW = 0, totalH = 0;
        for (int c = 0; c < hosts.Count; c++)
        {
            List<Window> mine = windows.FindAll(w => w.computer == c);
            if (mine.Count == 0) continue;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (Window w in mine)
            {
                minX = Mathf.Min(minX, w.x); minY = Mathf.Min(minY, w.y);
                maxX = Mathf.Max(maxX, w.x + w.w); maxY = Mathf.Max(maxY, w.y + w.h);
            }
            blockMinX[c] = minX;
            blockMinY[c] = minY;
            blockOffset[c] = totalW;
            totalW += maxX - minX;
            totalH = Mathf.Max(totalH, maxY - minY);
        }

        Resolution monitor = Screen.currentResolution;
        float fitW = monitor.width * 0.9f - 2 * Margin, fitH = monitor.height * 0.85f - 2 * Margin;
        float scale = Mathf.Min(1f, fitW / totalW, fitH / totalH);

        // Replace from the end, so earlier match positions stay valid.
        var sb = new StringBuilder(text);
        var log = new StringBuilder();
        windows.Sort((a, b) => b.match.Index.CompareTo(a.match.Index));
        foreach (Window w in windows)
        {
            int x = Margin + Mathf.RoundToInt((blockOffset[w.computer] + w.x - blockMinX[w.computer]) * scale);
            int y = Margin + Mathf.RoundToInt((w.y - blockMinY[w.computer]) * scale);
            int width = Mathf.Max(64, Mathf.RoundToInt(w.w * scale));
            int height = Mathf.Max(64, Mathf.RoundToInt(w.h * scale));

            Match m = w.match;
            string replaced = m.Groups[1].Value + x + m.Groups[3].Value + y + m.Groups[5].Value + width +
                              m.Groups[7].Value + height + m.Groups[9].Value;
            sb.Remove(m.Index, m.Length).Insert(m.Index, replaced);
            log.Insert(0, string.Format("  computer {0} ({1}): {2} x {3} at {4},{5}  ->  {6} x {7} at {8},{9}\n",
                                        w.computer + 1, hosts[w.computer].Groups[1].Value, w.w, w.h, w.x, w.y, width, height, x, y));
        }

        string result = sb.ToString();
        result = Hostname.Replace(result, "<hostname>localhost</hostname>");
        result = Regex.Replace(result, @"<fullScreen>\d+</fullScreen>", "<fullScreen>0</fullScreen>");
        result = Regex.Replace(result, @"<topMost>\d+</topMost>", "<topMost>0</topMost>");
        result = Regex.Replace(result, @"<displayId>\d+</displayId>", "<displayId>1</displayId>");

        // The display system's own name is the first <name> in the file.
        var firstName = new Regex(@"<name>([^<]*)</name>");
        result = firstName.Replace(result, m => "<name>" + m.Groups[1].Value + " (local test)</name>", 1);

        string target = EditorUtility.SaveFilePanel("Save the local test copy", Path.GetDirectoryName(source),
                                                    Path.GetFileNameWithoutExtension(source) + "_local", "gr3d");
        if (string.IsNullOrEmpty(target)) return;
        File.WriteAllText(target, result, new UTF8Encoding(false));

        Debug.Log("Vizlab: wrote " + target + "\n" +
                  hosts.Count + " computer(s) -> localhost, " + windows.Count + " window(s) = " + windows.Count +
                  " cop" + (windows.Count == 1 ? "y" : "ies") + " of the game on this PC, scaled x" + scale.ToString("0.###") +
                  " to fit " + monitor.width + " x " + monitor.height + ":\n" + log +
                  "Add it in the getReal3D launcher as the configuration for the Vizlab build, and launch as on the wall. " +
                  "ClusterSyncProbe_NEW writes each copy's log; Tools > Vizlab > Compare Cluster Probe Logs reads them.");
    }
}

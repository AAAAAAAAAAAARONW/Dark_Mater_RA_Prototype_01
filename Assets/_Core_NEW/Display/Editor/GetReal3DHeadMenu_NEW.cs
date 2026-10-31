using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > Vizlab > Pin the Viewer's Head in a getReal3D Config. Fixes "everything shows on
/// the bottom row of the wall".
///
/// WHY EVERYTHING WAS AT THE BOTTOM. getReal3D draws each screen from where it believes the
/// viewer's eyes are - its Head sensor. With no head tracking running, that sensor reads its
/// default, the floor: (0, 0, 0). getReal3D's own guide says as much ("raise your head and
/// hand sensors above their default position on the floor"), and every sample config in
/// Assets/getReal3D/Resources has the head there. The camera rigs keep the light and what the
/// camera follows near the middle of their view - straight ahead - and straight ahead from
/// the floor meets the Vizlab surface below its lowest row. So the whole piece sat at the
/// very bottom of the wall. At a desk nobody notices: the editor's sample screen is centred
/// at height 0, level with a head on the floor.
///
/// THE FIX IS IN THE CONFIG, NOT THE GAME. getReal3D has no call to move the head from a
/// script; it builds every screen's projection from the head in the config. A static head
/// at the height the wall was designed around puts straight ahead on the middle rows, with
/// the perspective the curve was built for:
///
///   Centre of curvature   (0, 1238.8, -41.1) mm. Rows 1-6 are equidistant from here; the
///                         design point (VizlabDisplay_NEW.CentreOfCurvatureMm).
///   Standing              (0, 1600, 0) mm. A standing adult (VizlabDisplay_NEW.StandingEyeMm).
///
/// If the Vizlab has head tracking that visitors actually wear, this is not the fix: the
/// tracker has to be running. Ask the lab.
///
/// WHAT IT DOES. Reads the config's screens and logs, for the head as it is and for the
/// pinned one, which screen straight ahead lands on and how far up it - so the log alone
/// confirms the diagnosis on the real file before anything is written. Then saves a copy
/// with every Head sensor static at the chosen point. Text edits only, as in
/// GetReal3DLocalConfigMenu_NEW, so getReal3D's file format is kept exactly.
///
/// ASSUMED: the config's room space is VizlabDisplay_NEW's - millimetres, origin on the
/// floor, the wall towards -Z. The screen list in the log shows whether that holds: the
/// seven rows should come out with centres near VizlabDisplay_NEW.Rows.
/// </summary>
static class GetReal3DHeadMenu_NEW
{
    static readonly Regex HeadSensor = new Regex(@"<static>(\d)</static>(\s*)<name>(Head[^<]*)</name>", RegexOptions.Compiled);
    static readonly Regex TrackerTag = new Regex(@"<tracker[ >]|</tracker>", RegexOptions.Compiled);
    static readonly Regex StaticPosition = new Regex(
        @"(<staticPosition>\s*<x>)([^<]*)(</x>\s*<y>)([^<]*)(</y>\s*<z>)([^<]*)(</z>)", RegexOptions.Compiled);

    // A screen: its name, size and centre, then (further down its block) its four corners.
    static readonly Regex Screen = new Regex(
        @"<name>([^<]*)</name>\s*<width>([^<]*)</width>\s*<height>([^<]*)</height>\s*<offset[^>]*>[\s\S]*?" +
        @"<ll>\s*<x>([^<]*)</x>\s*<y>([^<]*)</y>\s*<z>([^<]*)</z>\s*</ll>\s*" +
        @"<lr>\s*<x>([^<]*)</x>\s*<y>([^<]*)</y>\s*<z>([^<]*)</z>\s*</lr>\s*" +
        @"<ur>\s*<x>([^<]*)</x>\s*<y>([^<]*)</y>\s*<z>([^<]*)</z>\s*</ur>\s*" +
        @"<ul>\s*<x>([^<]*)</x>\s*<y>([^<]*)</y>\s*<z>([^<]*)</z>\s*</ul>", RegexOptions.Compiled);

    struct Head { public string name; public bool isStatic; public Match staticFlag; public Match position; public Vector3 mm; }
    struct Panel { public string name; public Vector3 ll, lr, ul; }

    [MenuItem("Tools/Vizlab/Pin the Viewer's Head in a getReal3D Config...")]
    static void Pin()
    {
        string source = EditorUtility.OpenFilePanel("The Vizlab's getReal3D config", "", "gr3d");
        if (string.IsNullOrEmpty(source)) return;
        string text = File.ReadAllText(source);

        List<Head> heads = FindHeads(text);
        List<Panel> panels = FindPanels(text);
        if (heads.Count == 0)
        {
            Debug.LogError("Vizlab: no Head sensor in " + source + ".");
            return;
        }

        var log = new StringBuilder();
        log.AppendLine("Vizlab: " + Path.GetFileName(source) + ": " + panels.Count + " screen(s)");
        foreach (Panel p in panels)
        {
            Vector3 c = (p.lr + p.ul) * 0.5f;
            log.AppendFormat("  {0,-16} centre ({1:0}, {2:0}, {3:0}) mm\n", p.name, c.x, c.y, c.z);
        }
        foreach (Head h in heads)
            log.AppendFormat("  {0}: {1} at ({2:0.#}, {3:0.#}, {4:0.#}) mm -> straight ahead lands on {5}\n",
                             h.name, h.isStatic ? "static" : "tracked (reads its default when nothing tracks it)",
                             h.mm.x, h.mm.y, h.mm.z, Lands(h.mm, panels));

        log.AppendLine("  pinned at the centre of curvature (1.24 m): straight ahead would land on " + Lands(VizlabDisplay_NEW.CentreOfCurvatureMm, panels));
        log.AppendLine("  pinned standing (1.60 m): straight ahead would land on " + Lands(VizlabDisplay_NEW.StandingEyeMm, panels));

        int choice = EditorUtility.DisplayDialogComplex(
            "Pin the viewer's head",
            log + "\nWhere should getReal3D put the viewer's eyes? (The analysis is also in the Console.)",
            "Centre of curvature (1.24 m)", "Cancel", "Standing (1.60 m)");
        Debug.Log(log.ToString());
        if (choice == 1) return;

        Vector3 pinned = choice == 0 ? VizlabDisplay_NEW.CentreOfCurvatureMm : VizlabDisplay_NEW.StandingEyeMm;

        // Edit from the end so earlier positions stay valid.
        var sb = new StringBuilder(text);
        for (int i = heads.Count - 1; i >= 0; i--)
        {
            Head h = heads[i];
            Match p = h.position;
            sb.Remove(p.Index, p.Length).Insert(p.Index,
                p.Groups[1].Value + Num(pinned.x) + p.Groups[3].Value + Num(pinned.y) + p.Groups[5].Value + Num(pinned.z) + p.Groups[7].Value);
            Match s = h.staticFlag;
            sb.Remove(s.Groups[1].Index, s.Groups[1].Length).Insert(s.Groups[1].Index, "1");
        }

        string target = EditorUtility.SaveFilePanel("Save the config with the head pinned", Path.GetDirectoryName(source),
                                                    Path.GetFileNameWithoutExtension(source) + "_head", "gr3d");
        if (string.IsNullOrEmpty(target)) return;
        File.WriteAllText(target, sb.ToString(), new UTF8Encoding(false));

        Debug.Log("Vizlab: wrote " + target + " with " + heads.Count + " head(s) static at (" + pinned.x + ", " + pinned.y + ", " + pinned.z +
                  ") mm -> straight ahead now lands on " + Lands(pinned, panels) + ". Use it in the getReal3D launcher " +
                  "(and run Make Local Test Copy on it for a desk rehearsal).");
    }

    static List<Head> FindHeads(string text)
    {
        var heads = new List<Head>();
        foreach (Match m in HeadSensor.Matches(text))
        {
            // The head's own staticPosition follows its <tracker> block, which holds the wand
            // sensor and the wand's staticPosition, so skip the whole block first.
            Match open = TrackerTag.Match(text, m.Index + m.Length);
            if (!open.Success) continue;
            int depth = 0, end = -1;
            for (Match t = open; t.Success; t = t.NextMatch())
            {
                depth += t.Value.StartsWith("</") ? -1 : 1;
                if (depth == 0) { end = t.Index + t.Length; break; }
            }
            if (end < 0) continue;

            Match pos = StaticPosition.Match(text, end);
            if (!pos.Success || text.Substring(end, pos.Index - end).Trim().Length > 0) continue;

            heads.Add(new Head
            {
                name = m.Groups[3].Value, isStatic = m.Groups[1].Value == "1", staticFlag = m, position = pos,
                mm = new Vector3(F(pos.Groups[2].Value), F(pos.Groups[4].Value), F(pos.Groups[6].Value))
            });
        }
        return heads;
    }

    static List<Panel> FindPanels(string text)
    {
        var panels = new List<Panel>();
        foreach (Match m in Screen.Matches(text))
        {
            panels.Add(new Panel
            {
                name = m.Groups[1].Value,
                ll = new Vector3(F(m.Groups[4].Value), F(m.Groups[5].Value), F(m.Groups[6].Value)),
                lr = new Vector3(F(m.Groups[7].Value), F(m.Groups[8].Value), F(m.Groups[9].Value)),
                ul = new Vector3(F(m.Groups[13].Value), F(m.Groups[14].Value), F(m.Groups[15].Value)),
            });
        }
        return panels;
    }

    /// <summary>Which screen a level line from the head towards -Z meets, and how far up it.</summary>
    static string Lands(Vector3 head, List<Panel> panels)
    {
        Vector3 dir = Vector3.back;   // -Z: towards the wall
        string best = null;
        float bestT = float.MaxValue;
        foreach (Panel p in panels)
        {
            Vector3 u = p.lr - p.ll, v = p.ul - p.ll, n = Vector3.Cross(u, v);
            float denom = Vector3.Dot(dir, n);
            if (Mathf.Abs(denom) < 1e-6f) continue;
            float t = Vector3.Dot(p.ll - head, n) / denom;
            if (t <= 0f || t >= bestT) continue;
            Vector3 q = head + dir * t - p.ll;
            float a = Vector3.Dot(q, u) / u.sqrMagnitude, b = Vector3.Dot(q, v) / v.sqrMagnitude;
            if (a < 0f || a > 1f || b < 0f || b > 1f) continue;
            bestT = t;
            best = "'" + p.name + "', " + Mathf.RoundToInt(b * 100f) + "% of the way up it";
        }
        return best ?? "NO screen (it passes below or above the wall)";
    }

    static float F(string s)
    {
        float v;
        return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0f;
    }

    static string Num(float v) { return ((double)v).ToString("0.00000000000000000e+00", CultureInfo.InvariantCulture); }
}

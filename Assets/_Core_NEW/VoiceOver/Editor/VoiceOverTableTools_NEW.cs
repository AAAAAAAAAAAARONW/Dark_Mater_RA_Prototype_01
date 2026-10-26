using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// What keeps the voice-over table in step with the two things outside Unity that feed
/// it: the markdown script, and the folder of recordings.
///
///   ImportScript       script → text and estimates. One way: the script is the source of
///                      the words, so text is overwritten and nothing else is. A line whose
///                      text changed after its recording was checked is reported as
///                      needing a re-record.
///   ScanRecordings     folder → clips, by file name. Fills empty slots only; a clip that
///                      somebody put on a line by hand stays. Overwriting a file with a new
///                      take of the same name needs no scan at all — Unity keeps the GUID.
///   Report             what is missing, unverified, needing a re-record, or has a cue to
///                      review, for the current language and variant.
///   EnsureTutorialTable  creates the whole thing from the two, for a scene that has none.
///
/// File names: &lt;lineId&gt;[_anything][_A|_B].ext. Line ids are A1, A1_2 (second narration
/// line), N_A1 (nudge), N_Pause, Ending. The longest line id that prefixes the name wins,
/// so A1_2_Second goes to A1_2 and not to A1.
/// </summary>
public static class VoiceOverTableTools_NEW
{
    public const string DefaultTablePath = "Assets/VoiceOver/Tables/VO_Tutorial.asset";
    const string ScriptFolder = "Assets/VoiceOver";

    // -- Create -------------------------------------------------------------------

    /// <summary>
    /// The tutorial table, created from the newest script and the recordings folder if it
    /// does not exist yet. Called by the tutorial builder, so a scene that predates the
    /// table migrates on its next Build or Update.
    /// </summary>
    public static VoiceOverTable_NEW EnsureTutorialTable()
    {
        VoiceOverTable_NEW table = AssetDatabase.LoadAssetAtPath<VoiceOverTable_NEW>(DefaultTablePath);
        if (table != null) return table;

        string folder = Path.GetDirectoryName(DefaultTablePath).Replace('\\', '/');
        EnsureFolder(folder);

        table = ScriptableObject.CreateInstance<VoiceOverTable_NEW>();
        table.script = FindNewestScript();
        AssetDatabase.CreateAsset(table, DefaultTablePath);

        Debug.Log("[VoiceOverTableTools_NEW] Created " + DefaultTablePath + ".", table);

        if (table.script != null) ImportScript(table, false);
        else Debug.LogWarning("[VoiceOverTableTools_NEW] No VO_Script*.md in " + ScriptFolder +
                              ". The table starts empty; assign its script and import.");

        ScanRecordings(table, false);
        AssetDatabase.SaveAssets();

        return table;
    }

    static TextAsset FindNewestScript()
    {
        string[] guids = AssetDatabase.FindAssets("VO_Script t:TextAsset", new[] { ScriptFolder });
        string best = null;

        // Names carry the version (VO_Script_v7), so the last one by name is
        // the newest one. Good enough for the one time this is used.
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) continue;
            if (best == null || string.CompareOrdinal(path, best) > 0) best = path;
        }

        return best != null ? AssetDatabase.LoadAssetAtPath<TextAsset>(best) : null;
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;

        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    // -- Sheets ---------------------------------------------------------------------

    /// <summary>Which sheet a beat lives on, by its phase letter.</summary>
    public static string SheetTitleFor(string beatId)
    {
        if (string.Equals(beatId, VoiceOverBeat_NEW.EndingId, StringComparison.OrdinalIgnoreCase)) return "Ending";
        if (string.Equals(beatId, VoiceOverBeat_NEW.PauseId, StringComparison.OrdinalIgnoreCase)) return "Ending";
        if (string.IsNullOrEmpty(beatId)) return "Other";

        switch (char.ToUpperInvariant(beatId[0]))
        {
            case 'A': case 'B': case 'C': return "Phase 0-2";
            case 'D': return "Phase 3";
            case 'E': return "Phase 4";
            default: return "Other";
        }
    }

    static readonly string[] SheetOrder = { "Phase 0-2", "Phase 3", "Phase 4", "Ending", "Other" };

    public static VoiceOverSheet_NEW SheetFor(VoiceOverTable_NEW table, string beatId)
    {
        string title = SheetTitleFor(beatId);

        for (int i = 0; i < table.sheets.Count; i++)
            if (table.sheets[i] != null && table.sheets[i].title == title) return table.sheets[i];

        VoiceOverSheet_NEW sheet = ScriptableObject.CreateInstance<VoiceOverSheet_NEW>();
        sheet.title = title;

        string dir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(table)).Replace('\\', '/');
        string path = AssetDatabase.GenerateUniqueAssetPath(dir + "/" + table.name + "_" +
                                                            title.Replace(" ", "") + ".asset");
        AssetDatabase.CreateAsset(sheet, path);

        Undo.RecordObject(table, "Add voice-over sheet");
        table.sheets.Add(sheet);
        table.sheets.Sort((a, b) => Array.IndexOf(SheetOrder, a != null ? a.title : "Other")
                                    .CompareTo(Array.IndexOf(SheetOrder, b != null ? b.title : "Other")));
        EditorUtility.SetDirty(table);

        return sheet;
    }

    /// <summary>The sheet holding a beat entry, or null.</summary>
    public static VoiceOverSheet_NEW SheetHolding(VoiceOverTable_NEW table, VoiceOverBeat_NEW entry)
    {
        for (int i = 0; i < table.sheets.Count; i++)
            if (table.sheets[i] != null && table.sheets[i].beats.Contains(entry)) return table.sheets[i];

        return null;
    }

    public static VoiceOverBeat_NEW FindOrCreateEntry(VoiceOverTable_NEW table, string beatId, out bool created)
    {
        VoiceOverBeat_NEW entry = table.Find(beatId);
        created = entry == null;
        if (entry != null) return entry;

        VoiceOverSheet_NEW sheet = SheetFor(table, beatId);
        Undo.RecordObject(sheet, "Add voice-over beat");

        entry = new VoiceOverBeat_NEW { beatId = beatId };
        sheet.beats.Add(entry);
        EditorUtility.SetDirty(sheet);

        return entry;
    }

    // -- Import the script ------------------------------------------------------------

    class Parsed
    {
        public string beatId;
        public string lineId;
        public VoiceOverRole_NEW role;
        public string variant = "";
        public string text;
        public float estimate;
    }

    // | ID | When | Line | ≈ |  — one row per line, under "## Part 1 · Tutorial".
    static readonly Regex IdRe = new Regex(@"^(?:N_)?[A-Za-z][A-Za-z0-9]*(?:_[A-Za-z0-9]+)*$");
    static readonly Regex CueRe = new Regex(@"\bCue\s*(\d+(?:\.\d+)?)\s*s\b", RegexOptions.IgnoreCase);
    static readonly Regex SecondsRe = new Regex(@"(\d+(?:\.\d+)?)\s*s\b");
    static readonly Regex RepeatRe = new Regex(@"^([A-E]\d{1,2})_\d+$");

    /// <summary>
    /// Read the tutorial half of the script (the rows under "## Part 1") into the table.
    /// Returns the report it also logs.
    /// </summary>
    public static string ImportScript(VoiceOverTable_NEW table, bool dialog)
    {
        if (table.script == null)
        {
            const string none = "The table has no script assigned.";
            if (dialog) EditorUtility.DisplayDialog("Import script", none, "OK");
            return none;
        }

        Dictionary<string, float> delays;
        List<Parsed> parsed = Parse(table.script.text, out delays);

        string language = table.languages.Count > 0 ? table.languages[0] : "en";

        List<string> added = new List<string>();
        List<string> changed = new List<string>();
        List<string> reRecord = new List<string>();
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Parsed p in parsed)
        {
            bool newEntry;
            VoiceOverBeat_NEW entry = FindOrCreateEntry(table, p.beatId, out newEntry);
            VoiceOverSheet_NEW sheet = SheetHolding(table, entry);
            Undo.RecordObject(sheet, "Import voice-over script");

            float delay;
            if (newEntry && delays.TryGetValue(p.beatId, out delay)) entry.settings.narrationDelay = delay;

            // The pause line: once per pause, after two seconds of it. A visitor who paused
            // on purpose to look around gets a moment before being told how to leave.
            if (newEntry && p.beatId == VoiceOverBeat_NEW.PauseId)
            {
                entry.settings.nudgeDelay = 2f;
                entry.settings.nudgeRepeats = 0;
            }

            VoiceOverLine_NEW line = entry.FindLine(p.lineId);
            if (line == null)
            {
                line = new VoiceOverLine_NEW { id = p.lineId, role = p.role };
                entry.lines.Add(line);
            }

            VoiceOverTake_NEW take = line.FindTake(language, p.variant);
            string key = p.lineId + (string.IsNullOrEmpty(p.variant) ? "" : " (" + p.variant + ")");
            seen.Add(p.lineId + "|" + p.variant);

            if (take == null)
            {
                take = new VoiceOverTake_NEW { language = language, variant = p.variant };
                line.takes.Add(take);
                added.Add(key);
            }
            else if (take.text != p.text)
            {
                changed.Add(key);
            }

            take.text = p.text;
            if (p.estimate > 0f) take.estimatedSeconds = p.estimate;

            if (take.NeedsReRecord) reRecord.Add(key);

            EditorUtility.SetDirty(sheet);
        }

        // Lines in the table the script no longer has. Reported, never deleted: a line
        // with a recording and tuned cues is work, and the script may just be mid-edit.
        List<string> orphans = new List<string>();
        foreach (VoiceOverSheet_NEW sheet in table.sheets)
        {
            if (sheet == null) continue;

            foreach (VoiceOverBeat_NEW b in sheet.beats)
                foreach (VoiceOverLine_NEW l in b.lines)
                    foreach (VoiceOverTake_NEW t in l.takes)
                        if (t.language == language && !seen.Contains(l.id + "|" + t.variant))
                            orphans.Add(l.id + (string.IsNullOrEmpty(t.variant) ? "" : " (" + t.variant + ")"));
        }

        AssetDatabase.SaveAssets();

        StringBuilder sb = new StringBuilder();
        sb.Append("Imported ").Append(parsed.Count).Append(" lines from ").Append(table.script.name).Append('.');
        AppendList(sb, "New", added);
        AppendList(sb, "Text changed", changed);
        AppendList(sb, "Needs a re-record (text changed after the recording was checked)", reRecord);
        AppendList(sb, "In the table but not in the script (kept)", orphans);

        string report = sb.ToString();
        Debug.Log("[VoiceOverTableTools_NEW] " + report, table);
        if (dialog) EditorUtility.DisplayDialog("Import script", Shorten(report), "OK");

        return report;
    }

    /// <summary>
    /// The tutorial's rows: every table row under "## Part 1" whose first cell is a line id.
    /// The beat is the id without its N_ prefix (N_A1 → A1, N_Pause → Pause) or its repeat
    /// suffix (A1_2 → A1). "Cue 0.5 s" in the When cell is the beat's narration delay.
    /// </summary>
    static List<Parsed> Parse(string script, out Dictionary<string, float> delays)
    {
        List<Parsed> result = new List<Parsed>();
        delays = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        bool tutorial = false;

        foreach (string raw in script.Replace("\r", "").Split('\n'))
        {
            string row = Clean(raw);

            if (row.StartsWith("## "))
            {
                if (row.StartsWith("## Part 2")) break;
                tutorial = row.StartsWith("## Part 1");
                continue;
            }

            if (!tutorial || !row.StartsWith("|")) continue;

            string[] cells = row.Trim('|').Split('|');
            if (cells.Length < 4) continue;
            for (int i = 0; i < cells.Length; i++) cells[i] = cells[i].Trim();

            string lineId = cells[0].Trim('*', '`');
            string text = cells[2];
            if (lineId == "ID" || !IdRe.IsMatch(lineId) || text.Length == 0) continue;

            bool nudge = lineId.StartsWith("N_");
            string beatId = nudge ? lineId.Substring(2) : lineId;
            Match repeat = RepeatRe.Match(beatId);
            if (repeat.Success) beatId = repeat.Groups[1].Value;

            Match cue = CueRe.Match(cells[1]);
            if (cue.Success) delays[beatId] = ParseFloat(cue.Groups[1].Value);

            float estimate = 0f;
            Match seconds = SecondsRe.Match(cells[3]);
            if (seconds.Success) estimate = ParseFloat(seconds.Groups[1].Value);

            result.Add(new Parsed
            {
                beatId = beatId,
                lineId = lineId,
                role = nudge ? VoiceOverRole_NEW.Nudge : VoiceOverRole_NEW.Narration,
                text = text,
                estimate = estimate
            });
        }

        return result;
    }

    /// <summary>Markdown noise off a row: the [v3] change markers and stray whitespace.</summary>
    static string Clean(string row)
    {
        // Change markers of any draft: **[v3]**, [v4]...
        row = Regex.Replace(row, @"\*\*\[v\d+\]\*\*|\[v\d+\]", "");
        row = Regex.Replace(row, @"\s{2,}", " ");
        return row.Trim();
    }

    static float ParseFloat(string s)
    {
        float f;
        return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f) ? f : 0f;
    }

    // -- Scan the recordings ---------------------------------------------------------

    /// <summary>Put every recording in the table's folder on its line, by file name. Empty slots only.</summary>
    public static string ScanRecordings(VoiceOverTable_NEW table, bool dialog)
    {
        string root = table.recordingsFolder.TrimEnd('/');
        string firstLanguage = table.languages.Count > 0 ? table.languages[0] : "en";

        List<VoiceOverLine_NEW> lines = new List<VoiceOverLine_NEW>();
        Dictionary<VoiceOverLine_NEW, VoiceOverSheet_NEW> owner = new Dictionary<VoiceOverLine_NEW, VoiceOverSheet_NEW>();

        foreach (VoiceOverSheet_NEW sheet in table.sheets)
        {
            if (sheet == null) continue;
            foreach (VoiceOverBeat_NEW b in sheet.beats)
                foreach (VoiceOverLine_NEW l in b.lines)
                {
                    lines.Add(l);
                    owner[l] = sheet;
                }
        }

        // Longest id first, so E2_Before claims E2_Before_A before E2 can.
        lines.Sort((a, b) => b.id.Length.CompareTo(a.id.Length));

        List<string> filled = new List<string>();
        List<string> kept = new List<string>();
        List<string> unmatched = new List<string>();

        if (!AssetDatabase.IsValidFolder(root))
            return Finish(table, dialog, "No recordings folder at " + root + ".");

        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { root }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string dir = Path.GetDirectoryName(path).Replace('\\', '/');

            string language;
            if (dir == root) language = firstLanguage;
            else if (Path.GetDirectoryName(dir).Replace('\\', '/') == root) language = Path.GetFileName(dir);
            else continue;

            string name = Path.GetFileNameWithoutExtension(path);
            string variant = "";

            foreach (string v in table.variants)
            {
                if (!string.IsNullOrEmpty(v) && name.EndsWith("_" + v, StringComparison.Ordinal))
                {
                    variant = v;
                    name = name.Substring(0, name.Length - v.Length - 1);
                    break;
                }
            }

            VoiceOverLine_NEW line = null;
            foreach (VoiceOverLine_NEW l in lines)
            {
                if (string.Equals(name, l.id, StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith(l.id + "_", StringComparison.OrdinalIgnoreCase))
                {
                    line = l;
                    break;
                }
            }

            if (line == null)
            {
                unmatched.Add(Path.GetFileName(path));
                continue;
            }

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            VoiceOverSheet_NEW sheet = owner[line];
            Undo.RecordObject(sheet, "Scan voice-over recordings");

            VoiceOverTake_NEW take = line.FindTake(language, variant);

            // A file with no _A/_B on a line the script splits into versions (E1): it was
            // recorded before the split, so it is the first version, and is said to be.
            if (take == null && variant.Length == 0)
            {
                foreach (VoiceOverTake_NEW t in line.takes)
                {
                    if (t.language != language || string.IsNullOrEmpty(t.variant) || t.clip != null) continue;

                    take = t;
                    kept.Add(line.id + ": " + clip.name + " has no version suffix; put on version " +
                             t.variant + ". Rename it if that is wrong.");
                    break;
                }
            }

            if (take == null)
            {
                // A recording ahead of the script: keep it, with the text to come.
                take = new VoiceOverTake_NEW { language = language, variant = variant };
                line.takes.Add(take);
            }

            if (take.clip == clip) continue;

            if (take.clip != null)
            {
                kept.Add(line.id + ": kept " + take.clip.name + ", found " + clip.name);
                continue;
            }

            take.clip = clip;
            if (take.status == VoiceOverStatus_NEW.Estimate) take.status = VoiceOverStatus_NEW.Scratch;

            filled.Add(line.id + (variant.Length > 0 ? " (" + variant + ")" : "") +
                       (language != firstLanguage ? " [" + language + "]" : "") + " ← " + clip.name);

            EditorUtility.SetDirty(sheet);
        }

        AssetDatabase.SaveAssets();

        StringBuilder sb = new StringBuilder();
        sb.Append("Scanned ").Append(root).Append('.');
        AppendList(sb, "Filled", filled);
        AppendList(sb, "Notes (nothing is replaced by a scan; drag a new take onto a line in the timeline to replace one)", kept);
        AppendList(sb, "Files that match no line", unmatched);

        return Finish(table, dialog, sb.ToString());
    }

    static string Finish(VoiceOverTable_NEW table, bool dialog, string report)
    {
        Debug.Log("[VoiceOverTableTools_NEW] " + report, table);
        if (dialog) EditorUtility.DisplayDialog("Scan recordings", Shorten(report), "OK");
        return report;
    }

    // -- Report -------------------------------------------------------------------------

    /// <summary>Everything that needs a person, for the table's current language and variant.</summary>
    public static string Report(VoiceOverTable_NEW table)
    {
        List<string> reRecord = new List<string>();
        List<string> unverified = new List<string>();
        List<string> missing = new List<string>();
        List<string> review = new List<string>();

        foreach (VoiceOverSheet_NEW sheet in table.sheets)
        {
            if (sheet == null) continue;

            foreach (VoiceOverBeat_NEW b in sheet.beats)
                foreach (VoiceOverLine_NEW l in b.lines)
                {
                    VoiceOverTake_NEW t = table.Resolve(l);
                    if (t == null) continue;

                    if (t.NeedsReRecord) reRecord.Add(l.id + ": \"" + t.text + "\"");
                    else if (t.Unverified) unverified.Add(l.id);

                    if (t.clip == null) missing.Add(l.id);

                    foreach (VoiceOverCue_NEW c in l.cues)
                        if (c.NeedsReview(t.Length)) review.Add(l.id + " · " + c.key);
                }
        }

        StringBuilder sb = new StringBuilder();
        sb.Append("Voice-over report, ").Append(table.language)
          .Append(string.IsNullOrEmpty(table.variant) ? "" : " version " + table.variant).Append('.');
        AppendList(sb, "Re-record (script changed)", reRecord);
        AppendList(sb, "No recording yet (plays as silence for its estimate)", missing);
        AppendList(sb, "Recording not checked against the script", unverified);
        AppendList(sb, "Cues to review (the line changed length)", review);

        return sb.ToString();
    }

    // -- Helpers -------------------------------------------------------------------------

    static void AppendList(StringBuilder sb, string title, List<string> items)
    {
        if (items.Count == 0) return;

        sb.Append("\n\n").Append(title).Append(" (").Append(items.Count).Append("):");
        foreach (string s in items) sb.Append("\n  ").Append(s);
    }

    static string Shorten(string s)
    {
        return s.Length <= 1500 ? s : s.Substring(0, 1500) + "\n\n… the rest is in the Console.";
    }
}

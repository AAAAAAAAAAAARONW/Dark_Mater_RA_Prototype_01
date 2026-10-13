using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Journey NEW > Voice Over > Timeline. Where the voice-over is tuned.
///
/// LEFT: every beat in storyboard order, with its narration drawn against its duration
/// — a line longer than the frame it is in shows red — and a flag for anything that
/// needs a person: no recording, recording not checked, script changed since it was
/// recorded, cue to review.
///
/// RIGHT: one beat. Its timing settings; each line with its script text, its recording
/// (drag a new take onto the slot to replace it), a play button and its waveform. Cues
/// sit on the waveform as markers: drag one to move it, double-click empty space to add
/// one, click to play from there. Under each cue, the scene event it fires.
///
/// What this window never does is change a line's WORDS. Those come from the script, by
/// Import Script; editing them here would leave two versions of every line.
/// </summary>
public class VoiceOverTimelineWindow_NEW : EditorWindow
{
    [MenuItem("Tools/Journey NEW/Voice Over/Timeline", false, 60)]
    static void Open()
    {
        GetWindow<VoiceOverTimelineWindow_NEW>("VO Timeline").Show();
    }

    class Row
    {
        public string id;
        public TutorialBeat_NEW beat;
        public VoiceOverBeat_NEW entry;
        public VoiceOverSheet_NEW sheet;
    }

    VoiceOverTable_NEW _table;
    string _selected;
    Vector2 _listScroll;
    Vector2 _detailScroll;
    string _report;
    bool _showMoveCalls;
    readonly Dictionary<int, int> _moveChoice = new Dictionary<int, int>();

    // A cue being dragged on a waveform.
    VoiceOverCue_NEW _dragCue;
    VoiceOverSheet_NEW _dragSheet;

    SerializedObject _beatSO;

    static readonly Color Background = new Color(0.16f, 0.16f, 0.16f);
    static readonly Color Wave = new Color(0.45f, 0.75f, 1f);
    static readonly Color FinalColor = new Color(0.35f, 0.75f, 0.45f);
    static readonly Color ScratchColor = new Color(0.35f, 0.6f, 0.95f);
    static readonly Color EstimateColor = new Color(0.55f, 0.55f, 0.55f);
    static readonly Color Overflow = new Color(0.95f, 0.3f, 0.25f);
    static readonly Color CueColor = new Color(0.3f, 0.95f, 0.95f);
    static readonly Color ReviewColor = new Color(1f, 0.8f, 0.2f);
    static readonly Color HintColor = new Color(1f, 0.55f, 0.95f);

    // -- Lifecycle ----------------------------------------------------------------

    void OnEnable()
    {
        FindTable();
        EditorApplication.update += Tick;
        Undo.undoRedoPerformed += Repaint;
    }

    void OnDisable()
    {
        EditorApplication.update -= Tick;
        Undo.undoRedoPerformed -= Repaint;
        VoiceOverAudio_NEW.Stop();
    }

    bool _wasPlaying;

    void Tick()
    {
        bool playing = VoiceOverAudio_NEW.Playing != null;
        if (playing || _wasPlaying || EditorApplication.isPlaying) Repaint();
        _wasPlaying = playing;
    }

    void FindTable()
    {
        if (_table != null) return;

        TutorialVoiceOver_NEW vo = FindObjectOfType<TutorialVoiceOver_NEW>();
        if (vo != null && vo.Table != null) _table = vo.Table;

        if (_table == null)
            _table = AssetDatabase.LoadAssetAtPath<VoiceOverTable_NEW>(VoiceOverTableTools_NEW.DefaultTablePath);
    }

    // -- Layout -------------------------------------------------------------------

    void OnGUI()
    {
        DrawToolbar();

        if (_table == null)
        {
            EditorGUILayout.HelpBox("No voice-over table. Build or Update on the tutorial scene creates " +
                                    "one from the script and the recordings folder, or create it here.",
                                    MessageType.Info);

            if (GUILayout.Button("Create the tutorial table", GUILayout.Width(220)))
                _table = VoiceOverTableTools_NEW.EnsureTutorialTable();

            return;
        }

        List<Row> rows = BuildRows();
        Row selected = rows.Find(r => r.id == _selected);

        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.BeginVertical(GUILayout.Width(Mathf.Clamp(position.width * 0.36f, 280f, 460f)));
        DrawList(rows);
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical();
        _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);

        if (!string.IsNullOrEmpty(_report))
        {
            EditorGUILayout.HelpBox(_report, MessageType.None);
            if (GUILayout.Button("Close report", GUILayout.Width(120))) _report = null;
            EditorGUILayout.Space();
        }

        if (selected != null) DrawDetail(selected);
        else EditorGUILayout.HelpBox("Pick a beat on the left.", MessageType.None);

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        _table = (VoiceOverTable_NEW)EditorGUILayout.ObjectField(_table, typeof(VoiceOverTable_NEW), false,
                                                                 GUILayout.Width(220));

        if (_table != null)
        {
            GUILayout.Label("Language", EditorStyles.miniLabel, GUILayout.Width(56));

            string[] languages = _table.languages.Count > 0 ? _table.languages.ToArray() : new[] { "en" };
            int li = Mathf.Max(0, Array.IndexOf(languages, _table.language));
            int nli = EditorGUILayout.Popup(li, languages, EditorStyles.toolbarPopup, GUILayout.Width(50));
            if (nli != li) SetTable(t => t.language = languages[nli]);

            GUILayout.Label("Version", EditorStyles.miniLabel, GUILayout.Width(46));

            List<string> variantList = new List<string> { "(shared)" };
            variantList.AddRange(_table.variants);
            string[] variants = variantList.ToArray();

            int vi = string.IsNullOrEmpty(_table.variant) ? 0 : Mathf.Max(0, variantList.IndexOf(_table.variant));
            int nvi = EditorGUILayout.Popup(vi, variants, EditorStyles.toolbarPopup, GUILayout.Width(70));
            if (nvi != vi) SetTable(t => t.variant = nvi == 0 ? "" : variants[nvi]);

            GUILayout.Space(8);

            if (GUILayout.Button(new GUIContent("Import Script", "Words and estimates from the markdown script."),
                                 EditorStyles.toolbarButton))
                _report = VoiceOverTableTools_NEW.ImportScript(_table, false);

            if (GUILayout.Button(new GUIContent("Scan Recordings", "Fill empty slots from the recordings folder, by file name."),
                                 EditorStyles.toolbarButton))
                _report = VoiceOverTableTools_NEW.ScanRecordings(_table, false);

            if (GUILayout.Button(new GUIContent("Report", "Missing, unchecked and re-record lines, and cues to review."),
                                 EditorStyles.toolbarButton))
                _report = VoiceOverTableTools_NEW.Report(_table);

            if (GUILayout.Button("Save", EditorStyles.toolbarButton)) AssetDatabase.SaveAssets();
        }

        GUILayout.FlexibleSpace();

        if (VoiceOverAudio_NEW.Playing != null && GUILayout.Button("■ Stop", EditorStyles.toolbarButton))
            VoiceOverAudio_NEW.Stop();

        EditorGUILayout.EndHorizontal();
    }

    void SetTable(Action<VoiceOverTable_NEW> change)
    {
        Undo.RecordObject(_table, "Voice-over table");
        change(_table);
        EditorUtility.SetDirty(_table);
    }

    // -- Rows ---------------------------------------------------------------------

    /// <summary>Scene beats in Hierarchy order, then table rows with no beat yet, the ending last.</summary>
    List<Row> BuildRows()
    {
        List<Row> rows = new List<Row>();
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        TutorialDirector_NEW director = FindObjectOfType<TutorialDirector_NEW>();
        TutorialBeat_NEW[] beats = director != null
            ? director.GetComponentsInChildren<TutorialBeat_NEW>(true)
            : new TutorialBeat_NEW[0];

        foreach (TutorialBeat_NEW b in beats)
        {
            if (b == null || string.IsNullOrEmpty(b.BeatId) || !seen.Add(b.BeatId)) continue;
            rows.Add(MakeRow(b.BeatId, b));
        }

        Row ending = null;

        foreach (VoiceOverSheet_NEW sheet in _table.sheets)
        {
            if (sheet == null) continue;

            foreach (VoiceOverBeat_NEW e in sheet.beats)
            {
                if (e == null || !seen.Add(e.beatId)) continue;

                Row r = MakeRow(e.beatId, null);
                if (e.beatId == VoiceOverBeat_NEW.EndingId) ending = r;
                else rows.Add(r);
            }
        }

        if (ending != null) rows.Add(ending);
        return rows;
    }

    Row MakeRow(string id, TutorialBeat_NEW beat)
    {
        VoiceOverBeat_NEW entry = _table.Find(id);

        return new Row
        {
            id = id,
            beat = beat,
            entry = entry,
            sheet = entry != null ? VoiceOverTableTools_NEW.SheetHolding(_table, entry) : null
        };
    }

    // -- Timing, as the runtime will play it ---------------------------------------

    float LineLength(VoiceOverLine_NEW line)
    {
        VoiceOverTake_NEW t = _table.Resolve(line);
        return t != null ? t.Length : 0f;
    }

    float NarrationSeconds(VoiceOverBeat_NEW entry)
    {
        if (entry == null) return 0f;

        float total = entry.settings.narrationDelay;
        int n = 0;

        foreach (VoiceOverLine_NEW l in entry.lines)
        {
            if (l.role != VoiceOverRole_NEW.Narration) continue;

            float len = LineLength(l);
            if (len <= 0f) continue;

            if (n++ > 0) total += 0.4f;
            total += len;
        }

        return total;
    }

    /// <summary>Seconds a timed beat runs, or -1 for a gated one.</summary>
    float BeatSeconds(Row row)
    {
        if (row.beat == null || row.beat.Mode != TutorialBeat_NEW.AdvanceMode.Duration) return -1f;

        if (row.entry != null && row.entry.settings.durationMode == VoiceOverDurationMode_NEW.FollowNarration &&
            NarrationSeconds(row.entry) > 0f)
            return NarrationSeconds(row.entry) + row.entry.settings.tailSeconds;

        return row.beat.AuthoredDuration;
    }

    // -- List ----------------------------------------------------------------------

    void DrawList(List<Row> rows)
    {
        float scale = 10f;
        foreach (Row r in rows) scale = Mathf.Max(scale, Mathf.Max(BeatSeconds(r), NarrationSeconds(r.entry)));

        _listScroll = EditorGUILayout.BeginScrollView(_listScroll);

        float timed = 0f;

        foreach (Row r in rows)
        {
            DrawRow(r, scale);

            float s = BeatSeconds(r);
            timed += s >= 0f ? s : NarrationSeconds(r.entry);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(string.Format("About {0:F0}:{1:00} of frames and lines, plus however long players " +
                                                 "take on the gated beats.", Mathf.Floor(timed / 60f), timed % 60f),
                                   EditorStyles.wordWrappedMiniLabel);

        EditorGUILayout.EndScrollView();
    }

    void DrawRow(Row row, float scale)
    {
        Rect r = GUILayoutUtility.GetRect(0f, 38f, GUILayout.ExpandWidth(true));

        bool isSelected = row.id == _selected;
        if (isSelected) EditorGUI.DrawRect(r, new Color(0.24f, 0.37f, 0.59f, 0.5f));

        if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
        {
            _selected = row.id;
            _beatSO = null;
            GUI.FocusControl(null);
            Event.current.Use();
        }

        GUI.Label(new Rect(r.x + 4, r.y + 2, 60, 16), row.id, EditorStyles.boldLabel);

        string kind = row.beat == null ? (row.id == VoiceOverBeat_NEW.EndingId ? "ending"
                                        : row.id == VoiceOverBeat_NEW.PauseId ? "while paused" : "not in scene")
                    : row.beat.Mode == TutorialBeat_NEW.AdvanceMode.PlayerAction ? "gated" : "timed";
        GUI.Label(new Rect(r.x + 54, r.y + 3, 80, 16), kind, EditorStyles.miniLabel);

        GUI.Label(new Rect(r.x + 130, r.y + 3, r.width - 134, 16), Flags(row), EditorStyles.miniLabel);

        Rect bar = new Rect(r.x + 4, r.y + 21, r.width - 8, 11);
        EditorGUI.DrawRect(bar, Background);

        float px = bar.width / scale;
        float beatSeconds = BeatSeconds(row);

        if (row.entry != null)
        {
            float x = row.entry.settings.narrationDelay;
            int n = 0;

            foreach (VoiceOverLine_NEW l in row.entry.lines)
            {
                if (l.role != VoiceOverRole_NEW.Narration) continue;

                VoiceOverTake_NEW t = _table.Resolve(l);
                if (t == null || t.Length <= 0f) continue;

                if (n++ > 0) x += 0.4f;

                Rect seg = new Rect(bar.x + x * px, bar.y + 2, t.Length * px, bar.height - 4);
                EditorGUI.DrawRect(seg, StatusColor(t));
                x += t.Length;
            }

            // The part of the narration the frame does not have room for.
            if (beatSeconds >= 0f && x > beatSeconds + 0.05f)
                EditorGUI.DrawRect(new Rect(bar.x + beatSeconds * px, bar.y, (x - beatSeconds) * px, bar.height),
                                   new Color(Overflow.r, Overflow.g, Overflow.b, 0.55f));
        }

        // The frame's own length, as an outline.
        if (beatSeconds >= 0f)
        {
            Rect frame = new Rect(bar.x, bar.y, beatSeconds * px, bar.height);
            DrawOutline(frame, new Color(1f, 1f, 1f, 0.55f));
        }
    }

    string Flags(Row row)
    {
        if (row.entry == null) return "no lines";

        List<string> flags = new List<string>();
        int missing = 0, reRecord = 0, unverified = 0, review = 0;

        foreach (VoiceOverLine_NEW l in row.entry.lines)
        {
            VoiceOverTake_NEW t = _table.Resolve(l);
            if (t == null) continue;

            if (t.clip == null) missing++;
            if (t.NeedsReRecord) reRecord++;
            else if (t.Unverified) unverified++;

            foreach (VoiceOverCue_NEW c in l.cues) if (c.NeedsReview(t.Length)) review++;
        }

        if (reRecord > 0) flags.Add("re-record " + reRecord);
        if (missing > 0) flags.Add("no take " + missing);
        if (review > 0) flags.Add("cue review " + review);
        if (unverified > 0) flags.Add("unchecked " + unverified);

        float beatSeconds = BeatSeconds(row);
        if (beatSeconds >= 0f && NarrationSeconds(row.entry) > beatSeconds + 0.05f) flags.Add("too long");

        return string.Join(" · ", flags.ToArray());
    }

    static Color StatusColor(VoiceOverTake_NEW t)
    {
        if (t.clip == null) return EstimateColor;
        return t.status == VoiceOverStatus_NEW.Final ? FinalColor : ScratchColor;
    }

    static void DrawOutline(Rect r, Color c)
    {
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1), c);
        EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1, r.width, 1), c);
        EditorGUI.DrawRect(new Rect(r.x, r.y, 1, r.height), c);
        EditorGUI.DrawRect(new Rect(r.xMax - 1, r.y, 1, r.height), c);
    }

    // -- Detail ----------------------------------------------------------------------

    void DrawDetail(Row row)
    {
        EditorGUILayout.LabelField(row.id, EditorStyles.boldLabel);

        if (row.beat != null)
        {
            if (!string.IsNullOrEmpty(row.beat.Description))
                EditorGUILayout.LabelField(row.beat.Description, EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.BeginHorizontal();

            string mode = row.beat.Mode == TutorialBeat_NEW.AdvanceMode.PlayerAction
                ? "Gated: waits for the player."
                : string.Format("Timed: runs {0:F1} s{1}.", BeatSeconds(row),
                                row.entry != null && row.entry.settings.durationMode == VoiceOverDurationMode_NEW.FollowNarration
                                    ? " (follows its narration)" : " (its own duration field)");
            EditorGUILayout.LabelField(mode, EditorStyles.miniLabel);

            if (GUILayout.Button("Select beat", EditorStyles.miniButton, GUILayout.Width(80)))
            {
                Selection.activeObject = row.beat;
                EditorGUIUtility.PingObject(row.beat);
            }

            EditorGUILayout.EndHorizontal();
        }
        else if (row.id == VoiceOverBeat_NEW.PauseId)
        {
            EditorGUILayout.HelpBox("Said while the piece is paused, in any beat. Uses this row's nudge settings: " +
                                    "Nudge Delay is the seconds of pause before it, Nudge Repeats how many more " +
                                    "times in one pause.", MessageType.None);
        }
        else if (row.id != VoiceOverBeat_NEW.EndingId)
        {
            EditorGUILayout.HelpBox("No beat with this id in the open scene. Its lines are kept and will " +
                                    "play once the beat exists.", MessageType.None);
        }

        if (row.entry == null)
        {
            if (GUILayout.Button("Add voice-over for " + row.id, GUILayout.Width(220)))
            {
                bool created;
                VoiceOverTableTools_NEW.FindOrCreateEntry(_table, row.id, out created);
            }
            return;
        }

        if (row.beat != null && (_beatSO == null || _beatSO.targetObject != row.beat))
            _beatSO = new SerializedObject(row.beat);

        if (_beatSO != null) _beatSO.Update();

        EditorGUILayout.Space();
        DrawSettings(row);

        for (int i = 0; i < row.entry.lines.Count; i++)
        {
            EditorGUILayout.Space();
            if (DrawLine(row, row.entry.lines[i])) break;   // a line was removed
        }

        EditorGUILayout.Space();
        DrawAddLine(row);

        if (row.beat != null) DrawMoveCalls(row);

        if (_beatSO != null) _beatSO.ApplyModifiedProperties();
    }

    void DrawSettings(Row row)
    {
        SerializedObject so = new SerializedObject(row.sheet);
        SerializedProperty beats = so.FindProperty("beats");

        int index = row.sheet.beats.IndexOf(row.entry);
        if (index < 0) return;

        SerializedProperty settings = beats.GetArrayElementAtIndex(index).FindPropertyRelative("settings");
        EditorGUILayout.PropertyField(settings, new GUIContent("Timing"), true);

        so.ApplyModifiedProperties();
    }

    // -- One line ----------------------------------------------------------------------

    /// <summary>Returns true if the line was removed, so the caller stops iterating.</summary>
    bool DrawLine(Row row, VoiceOverLine_NEW line)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(line.id, EditorStyles.boldLabel, GUILayout.Width(110));

        VoiceOverRole_NEW role = (VoiceOverRole_NEW)EditorGUILayout.EnumPopup(line.role, GUILayout.Width(100));
        if (role != line.role) Change(row.sheet, () => line.role = role);

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(60)) &&
            EditorUtility.DisplayDialog("Remove line", "Remove " + line.id + " and all its takes and cues from the " +
                                        "table? The audio files are not touched.", "Remove", "Cancel"))
        {
            Change(row.sheet, () => row.entry.lines.Remove(line));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return true;
        }

        EditorGUILayout.EndHorizontal();

        VoiceOverTake_NEW take = _table.Resolve(line);

        if (take == null)
        {
            EditorGUILayout.LabelField("No take for " + _table.language +
                                       (string.IsNullOrEmpty(_table.variant) ? "" : " / version " + _table.variant) +
                                       ". This line is silent here.", EditorStyles.miniLabel);

            if (GUILayout.Button("Add a take for this language and version", EditorStyles.miniButton, GUILayout.Width(260)))
                Change(row.sheet, () => line.takes.Add(new VoiceOverTake_NEW
                {
                    language = _table.language,
                    variant = _table.variant ?? ""
                }));

            EditorGUILayout.EndVertical();
            return false;
        }

        GUIStyle text = new GUIStyle(EditorStyles.wordWrappedLabel) { fontStyle = FontStyle.Italic };
        EditorGUILayout.LabelField(string.IsNullOrEmpty(take.text) ? "(no script text — add it to the script and import)" : "“" + take.text + "”", text);

        if (!string.IsNullOrEmpty(take.variant))
            EditorGUILayout.LabelField("Version " + take.variant + " only", EditorStyles.miniLabel);

        DrawTakeRow(row, take);
        DrawTakeWarnings(row, take);

        float length = take.Length;
        Rect wave = GUILayoutUtility.GetRect(0f, 70f, GUILayout.ExpandWidth(true));
        DrawWaveform(wave, row, line, take, length);

        DrawCues(row, line, take, length);

        EditorGUILayout.EndVertical();
        return false;
    }

    void DrawTakeRow(Row row, VoiceOverTake_NEW take)
    {
        EditorGUILayout.BeginHorizontal();

        AudioClip clip = (AudioClip)EditorGUILayout.ObjectField(take.clip, typeof(AudioClip), false, GUILayout.MinWidth(160));

        if (clip != take.clip)
        {
            Change(row.sheet, () =>
            {
                VoiceOverAudio_NEW.Stop();
                take.clip = clip;

                // A take dropped in now was recorded from the script as it is now.
                take.recordedText = clip != null ? take.text : "";

                if (clip != null && take.status == VoiceOverStatus_NEW.Estimate) take.status = VoiceOverStatus_NEW.Scratch;
                if (clip == null) take.status = VoiceOverStatus_NEW.Estimate;
            });
        }

        VoiceOverStatus_NEW status = (VoiceOverStatus_NEW)EditorGUILayout.EnumPopup(take.status, GUILayout.Width(80));
        if (status != take.status) Change(row.sheet, () => take.status = status);

        GUILayout.Label(take.clip != null
                            ? take.Length.ToString("F1") + " s"
                            : "≈" + take.Length.ToString("F1") + " s (estimate)",
                        EditorStyles.miniLabel, GUILayout.Width(100));

        using (new EditorGUI.DisabledScope(take.clip == null || !VoiceOverAudio_NEW.CanPreview))
        {
            bool playing = VoiceOverAudio_NEW.Playing == take.clip && take.clip != null;
            if (GUILayout.Button(playing ? "■" : "▶", EditorStyles.miniButton, GUILayout.Width(28)))
            {
                if (playing) VoiceOverAudio_NEW.Stop();
                else VoiceOverAudio_NEW.Play(take.clip, 0f);
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    void DrawTakeWarnings(Row row, VoiceOverTake_NEW take)
    {
        if (take.NeedsReRecord)
        {
            EditorGUILayout.HelpBox("The script changed after this recording was checked. It says:\n“" +
                                    take.recordedText + "”", MessageType.Warning);

            if (GUILayout.Button("The recording matches the new text", EditorStyles.miniButton, GUILayout.Width(240)))
                Change(row.sheet, () => take.recordedText = take.text);
        }
        else if (take.Unverified)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Not checked against the script yet.", EditorStyles.miniLabel);

            if (GUILayout.Button("It matches", EditorStyles.miniButton, GUILayout.Width(90)))
                Change(row.sheet, () => take.recordedText = take.text);

            EditorGUILayout.EndHorizontal();
        }
    }

    // -- Waveform and cues ----------------------------------------------------------------

    void DrawWaveform(Rect r, Row row, VoiceOverLine_NEW line, VoiceOverTake_NEW take, float length)
    {
        EditorGUI.DrawRect(r, Background);
        if (length <= 0f) return;

        // Seconds along the bottom.
        for (int s = 1; s < length; s++)
        {
            float x = r.x + s / length * r.width;
            EditorGUI.DrawRect(new Rect(x, r.yMax - (s % 5 == 0 ? 8 : 4), 1, s % 5 == 0 ? 8 : 4), new Color(1, 1, 1, 0.3f));
        }

        float mid = r.y + r.height * 0.5f;

        if (take.clip != null)
        {
            float[] peaks = VoiceOverAudio_NEW.Peaks(take.clip, Mathf.Max(1, (int)r.width));

            if (peaks != null)
            {
                for (int x = 0; x < peaks.Length; x++)
                {
                    float h = Mathf.Max(1f, peaks[x] * (r.height - 14f));
                    EditorGUI.DrawRect(new Rect(r.x + x, mid - h * 0.5f, 1, h), Wave);
                }
            }
            else
            {
                EditorGUI.DrawRect(new Rect(r.x, mid - 6, r.width, 12), ScratchColor);
            }
        }
        else
        {
            EditorGUI.DrawRect(new Rect(r.x, mid - 3, r.width, 6), EstimateColor);
            GUI.Label(new Rect(r.x + 4, r.y + 2, r.width, 16), "no recording: silence for the estimate", EditorStyles.miniLabel);
        }

        // Where the hint line comes up, if it is on this line.
        VoiceOverBeatSettings_NEW settings = row.entry.settings;
        bool gated = row.beat != null && row.beat.Mode == TutorialBeat_NEW.AdvanceMode.PlayerAction;

        if (gated && line.role == VoiceOverRole_NEW.Narration &&
            settings.hintTiming == VoiceOverHintTiming_NEW.AfterNarration && IsLastNarration(row.entry, line))
            Marker(r, r.xMax - 2, HintColor, "hint", true);

        // Cues.
        Event e = Event.current;

        for (int i = 0; i < line.cues.Count; i++)
        {
            VoiceOverCue_NEW cue = line.cues[i];
            float x = r.x + cue.Resolve(length) / length * r.width;

            bool isHint = gated && settings.hintTiming == VoiceOverHintTiming_NEW.AtCue &&
                          string.Equals(settings.hintCue, cue.key, StringComparison.OrdinalIgnoreCase);

            Color c = cue.NeedsReview(length) ? ReviewColor : CueColor;
            Marker(r, x, c, cue.key + (isHint ? " · hint" : ""), false);

            if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition) &&
                Mathf.Abs(e.mousePosition.x - x) <= 5f && _dragCue == null)
            {
                Undo.RecordObject(row.sheet, "Move cue");
                _dragCue = cue;
                _dragSheet = row.sheet;
                e.Use();
            }
        }

        // The play head.
        if (take.clip != null && VoiceOverAudio_NEW.Playing == take.clip)
        {
            float pos = VoiceOverAudio_NEW.Position(take.clip);
            if (pos >= 0f) EditorGUI.DrawRect(new Rect(r.x + pos / length * r.width, r.y, 2, r.height), Overflow);
        }

        HandleWaveformMouse(r, row, line, take, length);
    }

    void HandleWaveformMouse(Rect r, Row row, VoiceOverLine_NEW line, VoiceOverTake_NEW take, float length)
    {
        Event e = Event.current;

        if (_dragCue != null && line.cues.Contains(_dragCue))
        {
            if (e.type == EventType.MouseDrag)
            {
                float t = Mathf.Clamp((e.mousePosition.x - r.x) / r.width, 0f, 1f) * length;
                SetCueSeconds(_dragCue, t, length);
                e.Use();
                Repaint();
            }
            else if (e.rawType == EventType.MouseUp)
            {
                _dragCue.authoredLength = length;
                EditorUtility.SetDirty(_dragSheet);
                _dragCue = null;
                _dragSheet = null;
                e.Use();
            }
            return;
        }

        if (e.type != EventType.MouseDown || e.button != 0 || !r.Contains(e.mousePosition)) return;

        float at = Mathf.Clamp((e.mousePosition.x - r.x) / r.width, 0f, 1f) * length;

        if (e.clickCount == 2)
        {
            Change(row.sheet, () => line.cues.Add(NewCue(row, at, length)));
        }
        else if (take.clip != null)
        {
            VoiceOverAudio_NEW.Play(take.clip, at);
        }

        e.Use();
    }

    static void Marker(Rect r, float x, Color c, string label, bool alignRight)
    {
        EditorGUI.DrawRect(new Rect(x - 1, r.y, 2, r.height), c);

        GUIStyle style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = c } };
        float w = style.CalcSize(new GUIContent(label)).x;

        GUI.Label(new Rect(alignRight ? x - w - 3 : x + 3, r.y + 1, w, 14), label, style);
    }

    static bool IsLastNarration(VoiceOverBeat_NEW entry, VoiceOverLine_NEW line)
    {
        VoiceOverLine_NEW last = null;
        foreach (VoiceOverLine_NEW l in entry.lines)
            if (l.role == VoiceOverRole_NEW.Narration) last = l;

        return last == line;
    }

    /// <summary>Put a cue at this many seconds into a line, in whatever anchor it uses.</summary>
    static void SetCueSeconds(VoiceOverCue_NEW cue, float seconds, float length)
    {
        switch (cue.anchor)
        {
            case VoiceOverCueAnchor_NEW.FromEnd: cue.time = length - seconds; break;
            case VoiceOverCueAnchor_NEW.Proportion: cue.time = length > 0f ? seconds / length : 0f; break;
            default: cue.time = seconds; break;
        }
    }

    VoiceOverCue_NEW NewCue(Row row, float seconds, float length)
    {
        int n = 1;
        foreach (VoiceOverLine_NEW l in row.entry.lines) n += l.cues.Count;

        VoiceOverCue_NEW cue = new VoiceOverCue_NEW { key = "cue" + n, authoredLength = length };
        SetCueSeconds(cue, seconds, length);
        return cue;
    }

    void DrawCues(Row row, VoiceOverLine_NEW line, VoiceOverTake_NEW take, float length)
    {
        EditorGUILayout.LabelField("Click the waveform to play from there. Double-click to add a cue; drag a cue to move it.",
                                   EditorStyles.centeredGreyMiniLabel);

        for (int i = 0; i < line.cues.Count; i++)
        {
            VoiceOverCue_NEW cue = line.cues[i];

            EditorGUILayout.BeginHorizontal();

            string key = EditorGUILayout.DelayedTextField(cue.key, GUILayout.Width(110));
            if (key != cue.key && !string.IsNullOrEmpty(key))
            {
                string old = cue.key;
                Change(row.sheet, () => cue.key = key);
                RenameCueEvent(row, old, key);
            }

            VoiceOverCueAnchor_NEW anchor = (VoiceOverCueAnchor_NEW)EditorGUILayout.EnumPopup(cue.anchor, GUILayout.Width(90));
            if (anchor != cue.anchor)
            {
                // Same moment, measured a different way.
                float seconds = cue.Resolve(length);
                Change(row.sheet, () =>
                {
                    cue.anchor = anchor;
                    SetCueSeconds(cue, seconds, length);
                });
            }

            float time = EditorGUILayout.FloatField(cue.time, GUILayout.Width(55));
            if (!Mathf.Approximately(time, cue.time)) Change(row.sheet, () =>
            {
                cue.time = time;
                cue.authoredLength = length;
            });

            GUILayout.Label("= " + cue.Resolve(length).ToString("F2") + " s", EditorStyles.miniLabel, GUILayout.Width(60));

            if (cue.NeedsReview(length))
            {
                GUIStyle warn = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = ReviewColor } };
                GUILayout.Label("line was " + cue.authoredLength.ToString("F1") + " s", warn, GUILayout.Width(80));

                if (GUILayout.Button("OK", EditorStyles.miniButton, GUILayout.Width(32)))
                    Change(row.sheet, () => cue.authoredLength = length);
            }

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(take.clip == null))
                if (GUILayout.Button("▶", EditorStyles.miniButton, GUILayout.Width(24)))
                    VoiceOverAudio_NEW.Play(take.clip, Mathf.Max(0f, cue.Resolve(length) - 1f));

            if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(24)))
            {
                Change(row.sheet, () => line.cues.Remove(cue));
                EditorGUILayout.EndHorizontal();
                break;
            }

            EditorGUILayout.EndHorizontal();

            DrawCueEvent(row, cue.key);
        }

        if (GUILayout.Button("+ Cue", EditorStyles.miniButton, GUILayout.Width(60)))
        {
            float at = take.clip != null && VoiceOverAudio_NEW.Playing == take.clip
                ? Mathf.Max(0f, VoiceOverAudio_NEW.Position(take.clip))
                : length * 0.5f;

            Change(row.sheet, () => line.cues.Add(NewCue(row, at, length)));
        }
    }

    // -- The scene half of a cue -------------------------------------------------------------

    SerializedProperty FindCueEvent(string key)
    {
        if (_beatSO == null) return null;

        SerializedProperty events = _beatSO.FindProperty("cueEvents");
        if (events == null) return null;

        for (int i = 0; i < events.arraySize; i++)
        {
            SerializedProperty e = events.GetArrayElementAtIndex(i);
            if (string.Equals(e.FindPropertyRelative("key").stringValue, key, StringComparison.OrdinalIgnoreCase))
                return e;
        }

        return null;
    }

    SerializedProperty AddCueEvent(string key)
    {
        SerializedProperty events = _beatSO.FindProperty("cueEvents");
        events.arraySize++;

        SerializedProperty e = events.GetArrayElementAtIndex(events.arraySize - 1);
        e.FindPropertyRelative("key").stringValue = key;

        // A new array element is a copy of the one before it, listeners included.
        e.FindPropertyRelative("onCue.m_PersistentCalls.m_Calls").arraySize = 0;

        return e;
    }

    void DrawCueEvent(Row row, string key)
    {
        if (row.beat == null)
        {
            EditorGUILayout.LabelField("No scene beat, so this cue only marks a moment (a hint cue still works).",
                                       EditorStyles.miniLabel);
            return;
        }

        SerializedProperty e = FindCueEvent(key);

        EditorGUI.indentLevel++;

        if (e == null)
        {
            if (GUILayout.Button("Add a scene event for '" + key + "' on " + row.beat.name, EditorStyles.miniButton,
                                 GUILayout.Width(280)))
            {
                AddCueEvent(key);
                MarkSceneDirty(row.beat);
            }
        }
        else
        {
            EditorGUILayout.PropertyField(e.FindPropertyRelative("onCue"), new GUIContent("On '" + key + "'"));
        }

        EditorGUI.indentLevel--;
    }

    void RenameCueEvent(Row row, string from, string to)
    {
        if (row.beat == null || _beatSO == null) return;

        SerializedProperty e = FindCueEvent(from);
        if (e == null) return;

        e.FindPropertyRelative("key").stringValue = to;
        _beatSO.ApplyModifiedProperties();
        MarkSceneDirty(row.beat);
    }

    /// <summary>
    /// Move a listener the builder (or anyone) put on the beat's onEnter to a cue, so it
    /// happens at a moment in the line instead of when the beat opens. The builder knows
    /// to look in the cue events before putting its own calls back on onEnter.
    /// </summary>
    void DrawMoveCalls(Row row)
    {
        List<string> keys = new List<string>();
        foreach (VoiceOverLine_NEW l in row.entry.lines)
            foreach (VoiceOverCue_NEW c in l.cues)
                if (!keys.Contains(c.key)) keys.Add(c.key);

        SerializedProperty calls = _beatSO.FindProperty("onEnter.m_PersistentCalls.m_Calls");
        if (calls == null || calls.arraySize == 0) return;

        EditorGUILayout.Space();
        _showMoveCalls = EditorGUILayout.Foldout(_showMoveCalls, "onEnter calls (" + calls.arraySize + ") — move one to a cue", true);
        if (!_showMoveCalls) return;

        if (keys.Count == 0)
        {
            EditorGUILayout.LabelField("Add a cue to a line first.", EditorStyles.miniLabel);
            return;
        }

        for (int i = 0; i < calls.arraySize; i++)
        {
            SerializedProperty call = calls.GetArrayElementAtIndex(i);
            UnityEngine.Object target = call.FindPropertyRelative("m_Target").objectReferenceValue;
            string method = call.FindPropertyRelative("m_MethodName").stringValue;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField((target != null ? target.name : "(none)") + " → " + method, EditorStyles.miniLabel);

            int chosen;
            _moveChoice.TryGetValue(i, out chosen);
            int k = EditorGUILayout.Popup(Mathf.Clamp(chosen, 0, keys.Count - 1), keys.ToArray(), GUILayout.Width(100));
            _moveChoice[i] = k;

            if (GUILayout.Button("Move", EditorStyles.miniButton, GUILayout.Width(50)))
            {
                MoveCall(calls, i, keys[k]);
                MarkSceneDirty(row.beat);
                EditorGUILayout.EndHorizontal();
                break;
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    void MoveCall(SerializedProperty calls, int index, string key)
    {
        SerializedProperty e = FindCueEvent(key) ?? AddCueEvent(key);
        SerializedProperty dstCalls = e.FindPropertyRelative("onCue.m_PersistentCalls.m_Calls");

        dstCalls.arraySize++;
        SerializedProperty dst = dstCalls.GetArrayElementAtIndex(dstCalls.arraySize - 1);
        SerializedProperty src = calls.GetArrayElementAtIndex(index);

        dst.FindPropertyRelative("m_Target").objectReferenceValue = src.FindPropertyRelative("m_Target").objectReferenceValue;
        dst.FindPropertyRelative("m_MethodName").stringValue = src.FindPropertyRelative("m_MethodName").stringValue;
        dst.FindPropertyRelative("m_Mode").intValue = src.FindPropertyRelative("m_Mode").intValue;
        dst.FindPropertyRelative("m_CallState").intValue = src.FindPropertyRelative("m_CallState").intValue;

        SerializedProperty sa = src.FindPropertyRelative("m_Arguments");
        SerializedProperty da = dst.FindPropertyRelative("m_Arguments");

        da.FindPropertyRelative("m_ObjectArgument").objectReferenceValue = sa.FindPropertyRelative("m_ObjectArgument").objectReferenceValue;
        da.FindPropertyRelative("m_ObjectArgumentAssemblyTypeName").stringValue = sa.FindPropertyRelative("m_ObjectArgumentAssemblyTypeName").stringValue;
        da.FindPropertyRelative("m_IntArgument").intValue = sa.FindPropertyRelative("m_IntArgument").intValue;
        da.FindPropertyRelative("m_FloatArgument").floatValue = sa.FindPropertyRelative("m_FloatArgument").floatValue;
        da.FindPropertyRelative("m_StringArgument").stringValue = sa.FindPropertyRelative("m_StringArgument").stringValue;
        da.FindPropertyRelative("m_BoolArgument").boolValue = sa.FindPropertyRelative("m_BoolArgument").boolValue;

        calls.DeleteArrayElementAtIndex(index);
        _beatSO.ApplyModifiedProperties();
    }

    // -- Adding lines -------------------------------------------------------------------------

    void DrawAddLine(Row row)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Add a line:", EditorStyles.miniLabel, GUILayout.Width(70));

        if (GUILayout.Button("Narration", EditorStyles.miniButtonLeft, GUILayout.Width(80)))
            AddLine(row, VoiceOverRole_NEW.Narration);
        if (GUILayout.Button("Nudge", EditorStyles.miniButtonMid, GUILayout.Width(60)))
            AddLine(row, VoiceOverRole_NEW.Nudge);
        if (GUILayout.Button("After action", EditorStyles.miniButtonRight, GUILayout.Width(90)))
            AddLine(row, VoiceOverRole_NEW.AfterAction);

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField("A new line has no words until the script has it and is imported. Until it has a " +
                                   "recording it plays as silence for its estimate.", EditorStyles.wordWrappedMiniLabel);
    }

    void AddLine(Row row, VoiceOverRole_NEW role)
    {
        string stem = role == VoiceOverRole_NEW.Nudge ? "N_" + row.id
                    : role == VoiceOverRole_NEW.AfterAction ? row.id + "_After"
                    : row.id;

        string id = stem;
        for (int n = 2; row.entry.FindLine(id) != null; n++) id = stem + "_" + n;

        Change(row.sheet, () => row.entry.lines.Add(new VoiceOverLine_NEW
        {
            id = id,
            role = role,
            takes = new List<VoiceOverTake_NEW>
            {
                new VoiceOverTake_NEW { language = _table.language, variant = "", estimatedSeconds = 3f }
            }
        }));
    }

    // -- Helpers -------------------------------------------------------------------------------

    static void Change(UnityEngine.Object target, Action change)
    {
        Undo.RecordObject(target, "Voice-over");
        change();
        EditorUtility.SetDirty(target);
    }

    static void MarkSceneDirty(Component c)
    {
        if (c != null && !EditorApplication.isPlaying) EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
    }
}

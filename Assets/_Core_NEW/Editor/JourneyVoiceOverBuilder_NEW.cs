using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Puts the journey's voice-over into the open scene: one JourneyVoiceOver_NEW, its rows
/// filled from Assets/VoiceOver/Journey by file name, and the redshift sequence's steps
/// given their lines.
///
/// A menu item rather than a one-off scene edit, for the reason TutorialSceneBuilder_NEW
/// gives: two gameplay scenes exist (Ana's and Aaron's), they get rebuilt, and hand
/// wiring a dozen clips into each is a dozen chances to put J4 on the wrong layer.
///
/// SAME OWNERSHIP RULE AS THE TUTORIAL BUILDER. It creates what is missing and fills
/// what is empty; anything already set is somebody's decision and is left alone. A run
/// on a scene that is already wired logs that it had nothing to do. The two places it
/// changes an existing value — the sequence's startOnLayerId and two step durations —
/// it only does while they still hold the value it is replacing, and says so.
///
/// THE MAPPING, which is the content decision this file encodes:
///
///   Quasar        J0, J1. 1 s in, as JourneyOpening_NEW's second of black fades up.
///                 The camera's look-back is the journey's own, not cued from here.
///   Macro         J3_CosmicWeb1
///   Micro         J4_Galaxy, J4b (why the journey slows), then J2_JourneyHUD and
///                 J2b_Readout - moved here from the opening, which is only 14 seconds long
///   CosmicWeb     J5_CosmicWeb2 — then the redshift sequence starts
///     step F1     J6_1_Redshift
///     step F4     J6_2_Redshift    (Spread: the lines move apart)
///     step F5     J6_3_Forest      (ShowAll: the forest)
///   MilkyWay      J7_MilkyWay
///   SolarSystem   J8_SolarSystem
///   Earth         J9_Earth, then Closing over the end video (which has no sound of its own)
///
///   J3b           said by FilamentAbsorber_NEW, the first time the light crosses a placed
///                 yellow filament - which also cuts and highlights a line. A placeholder
///                 filament trigger is made before the galaxy gate; move it onto real art.
///
/// Layer rows wait for LookUpStart — the new world coming back on screen.
/// </summary>
public static class JourneyVoiceOverBuilder_NEW
{
    const string Folder = "Assets/VoiceOver/Journey";

    /// <summary>Seconds into the start layer before J1. Lets the intro camera's push in land first.</summary>
    const float StartLayerDelay = 1f;

    static readonly string[][] LayerClips =
    {
        new[] { "Quasar", "J0", "J1_LeavingQuasar" },
        new[] { "Macro", "J3_CosmicWeb1" },
        new[] { "Micro", "J4_Galaxy", "J4b", "J2_JourneyHUD", "J2b_Readout" },
        new[] { "CosmicWeb", "J5_CosmicWeb2" },
        new[] { "MilkyWay", "J7_MilkyWay" },
        new[] { "SolarSystem", "J8_SolarSystem" },
        new[] { "Earth", "J9_Earth", "Closing" },
    };

    static readonly string[][] StepClips =
    {
        new[] { "F1", "J6_1_Redshift" },
        new[] { "F4", "J6_2_Redshift" },
        new[] { "F5", "J6_3_Forest" },
    };

    static int _changes;

    [MenuItem("Tools/Journey NEW/Voice Over/Wire Journey Voice Over (open scene)")]
    public static void Run()
    {
        _changes = 0;

        CameraDirector_NEW director = UnityEngine.Object.FindObjectOfType<CameraDirector_NEW>();
        LayerState_NEW state = UnityEngine.Object.FindObjectOfType<LayerState_NEW>();

        if (director == null || state == null)
        {
            EditorUtility.DisplayDialog("Journey voice-over",
                "This scene has no CameraDirector_NEW or LayerState_NEW, so it is not a journey " +
                "scene. Open Gameplay_Scene_Ana (or Aaron) and run it again.", "OK");
            return;
        }

        Undo.SetCurrentGroupName("Wire journey voice-over");
        int group = Undo.GetCurrentGroup();

        JourneyVoiceOver_NEW voiceOver = BuildVoiceOver(director, state);
        FillLayerRows(voiceOver);

        JourneySequence_NEW sequence = UnityEngine.Object.FindObjectOfType<JourneySequence_NEW>();
        if (sequence != null) WireSequence(voiceOver, sequence);
        else Debug.LogWarning("[JourneyVoiceOverBuilder_NEW] No JourneySequence_NEW in the scene. " +
                              "J6_1–J6_3 are not wired.");

        RemoveLookBackCue(voiceOver);
        WireOpening(voiceOver);
        RetireOldCallout(voiceOver);
        WireFilament(voiceOver);
        WirePace(voiceOver);
        WireMilkyWayCleanup(voiceOver, director);

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(voiceOver.gameObject.scene);
        Selection.activeGameObject = voiceOver.gameObject;

        Debug.Log("[JourneyVoiceOverBuilder_NEW] " +
                  (_changes == 0 ? "Already wired; nothing to do." : _changes + " change(s). Save the scene."),
                  voiceOver);
    }

    // -- Object ---------------------------------------------------------------

    static JourneyVoiceOver_NEW BuildVoiceOver(CameraDirector_NEW director, LayerState_NEW state)
    {
        JourneyVoiceOver_NEW existing = UnityEngine.Object.FindObjectOfType<JourneyVoiceOver_NEW>();
        if (existing != null) return existing;

        // Beside the other responders when the scene has the README's layout; next to
        // the camera director otherwise.
        GameObject responders = GameObject.Find("LayerResponders");
        Transform parent = responders != null ? responders.transform : director.transform.parent;

        GameObject go = new GameObject("VoiceOver");
        Undo.RegisterCreatedObjectUndo(go, "Create VoiceOver");
        if (parent != null) go.transform.SetParent(parent, false);

        AudioSource source = Undo.AddComponent<AudioSource>(go);
        source.spatialBlend = 0f;
        source.playOnAwake = false;
        source.loop = false;
        source.priority = 0;

        JourneyVoiceOver_NEW voiceOver = Undo.AddComponent<JourneyVoiceOver_NEW>(go);

        SerializedObject so = new SerializedObject(voiceOver);
        so.FindProperty("director").objectReferenceValue = director;
        so.FindProperty("layerState").objectReferenceValue = state;
        so.FindProperty("source").objectReferenceValue = source;
        so.ApplyModifiedPropertiesWithoutUndo();

        _changes++;
        Debug.Log("[JourneyVoiceOverBuilder_NEW] Created " + go.name + " under " +
                  (parent != null ? parent.name : "the scene root") + ".", go);

        return voiceOver;
    }

    /// <summary>
    /// A row per layer in the mapping. A missing row is added; an existing row with no
    /// clips is filled; a row with clips is left exactly as it is.
    /// </summary>
    static void FillLayerRows(JourneyVoiceOver_NEW voiceOver)
    {
        SerializedObject so = new SerializedObject(voiceOver);
        SerializedProperty rows = so.FindProperty("layers");

        for (int i = 0; i < LayerClips.Length; i++)
        {
            string layerId = LayerClips[i][0];
            SerializedProperty row = FindRow(rows, layerId);

            if (row == null)
            {
                rows.arraySize++;
                row = rows.GetArrayElementAtIndex(rows.arraySize - 1);

                row.FindPropertyRelative("layerId").stringValue = layerId;
                row.FindPropertyRelative("anchor").enumValueIndex = (int)TransitionAnchor_NEW.LookUpStart;
                row.FindPropertyRelative("delay").floatValue = layerId == "Quasar" ? StartLayerDelay : 0f;
                row.FindPropertyRelative("clips").arraySize = 0;

                // A new array element copies the previous one, events included.
                row.FindPropertyRelative("onFinished").FindPropertyRelative("m_PersistentCalls.m_Calls").arraySize = 0;

                _changes++;
            }

            // Clips in the mapping that the row does not have yet are put in - in the
            // mapping's order, straight after the mapping clip before them (J0 goes in front
            // of J1, Closing after J9). A clip somebody removed by hand comes back on the next
            // run; take it out of the mapping above instead.
            SerializedProperty clips = row.FindPropertyRelative("clips");
            int insertAt = 0;

            for (int c = 1; c < LayerClips[i].Length; c++)
            {
                AudioClip clip = Load(LayerClips[i][c]);
                if (clip == null) continue;

                int at = IndexOfClip(clips, clip);
                if (at >= 0)
                {
                    insertAt = at + 1;
                    continue;
                }

                clips.InsertArrayElementAtIndex(insertAt);
                clips.GetArrayElementAtIndex(insertAt).objectReferenceValue = clip;
                insertAt++;
                _changes++;

                Debug.Log("[JourneyVoiceOverBuilder_NEW] " + layerId + ": added " + clip.name + ".");
            }
        }

        // Lines that moved to another layer: out of the old one.
        for (int m = 0; m < MovedOut.Length; m++)
        {
            SerializedProperty row = FindRow(rows, MovedOut[m][0]);
            if (row == null) continue;

            SerializedProperty clips = row.FindPropertyRelative("clips");
            for (int c = 1; c < MovedOut[m].Length; c++)
            {
                AudioClip clip = Load(MovedOut[m][c]);
                int at = clip != null ? IndexOfClip(clips, clip) : -1;
                if (at < 0) continue;

                clips.GetArrayElementAtIndex(at).objectReferenceValue = null;
                clips.DeleteArrayElementAtIndex(at);
                _changes++;
                Debug.Log("[JourneyVoiceOverBuilder_NEW] " + MovedOut[m][0] + ": took out " + clip.name + " (moved to another layer).");
            }
        }

        // The opening's delay was 3 s when the line after it played in the light; now J0 is
        // said over black, and waiting three seconds in the dark is only dead air.
        SerializedProperty quasarRow = FindRow(rows, "Quasar");
        if (quasarRow != null && Mathf.Approximately(quasarRow.FindPropertyRelative("delay").floatValue, 3f))
        {
            quasarRow.FindPropertyRelative("delay").floatValue = StartLayerDelay;
            _changes++;
        }

        so.ApplyModifiedProperties();
    }

    /// <summary>Lines that left a layer: { layerId, clips... }.</summary>
    static readonly string[][] MovedOut =
    {
        new[] { "Quasar", "J2_JourneyHUD", "J2b_Readout" },
    };

    static int IndexOfClip(SerializedProperty clips, AudioClip clip)
    {
        for (int i = 0; i < clips.arraySize; i++)
            if (clips.GetArrayElementAtIndex(i).objectReferenceValue == clip) return i;

        return -1;
    }

    static SerializedProperty FindRow(SerializedProperty rows, string layerId)
    {
        for (int i = 0; i < rows.arraySize; i++)
        {
            SerializedProperty row = rows.GetArrayElementAtIndex(i);
            if (string.Equals(row.FindPropertyRelative("layerId").stringValue, layerId,
                              StringComparison.OrdinalIgnoreCase))
                return row;
        }

        return null;
    }

    // -- The opening: J0 over black, J1 fades it up -----------------------------------

    static void WireOpening(JourneyVoiceOver_NEW voiceOver)
    {
        JourneyOpening_NEW opening = UnityEngine.Object.FindObjectOfType<JourneyOpening_NEW>();
        if (opening == null)
        {
            opening = Undo.AddComponent<JourneyOpening_NEW>(voiceOver.gameObject);
            _changes++;
            Debug.Log("[JourneyVoiceOverBuilder_NEW] Added JourneyOpening_NEW: a beat of black, then " +
                      "the picture fades up and the light sets off.", opening);
        }

        // 0.1 s of black, not the first version's 1 s: a scene that has the old value gets the new.
        SerializedObject openingSo = new SerializedObject(opening);
        SerializedProperty black = openingSo.FindProperty("blackSeconds");
        if (black != null && Mathf.Approximately(black.floatValue, 1f))
        {
            black.floatValue = 0.1f;
            openingSo.ApplyModifiedProperties();
            _changes++;
        }

        // The opening reveals itself after its moment of black now. A first version waited
        // for a cue at the start of J1 (twelve seconds of black); that cue comes out.
        JourneyVoiceOver_NEW.LayerLines quasar = FindRowObject(voiceOver, "Quasar");
        if (quasar == null) return;

        var kept = new System.Collections.Generic.List<JourneyVoiceOver_NEW.LineCue>();
        foreach (JourneyVoiceOver_NEW.LineCue c in quasar.cues)
            if (c == null || !HasCall(c.onCue, opening, "Reveal")) kept.Add(c);

        if (kept.Count == quasar.cues.Length) return;

        Undo.RecordObject(voiceOver, "Unwire the opening cue");
        quasar.cues = kept.ToArray();
        EditorUtility.SetDirty(voiceOver);
        _changes++;
    }

    // -- J1's look back: removed ----------------------------------------------------

    /// <summary>
    /// An earlier version hung CameraDirector_NEW.PlayLookBack on J1, which turned the camera
    /// round a second time on top of the journey's own look-back. Gone; this takes it out
    /// of a scene that already has it.
    /// </summary>
    static void RemoveLookBackCue(JourneyVoiceOver_NEW voiceOver)
    {
        JourneyVoiceOver_NEW.LayerLines quasar = FindRowObject(voiceOver, "Quasar");
        if (quasar == null || quasar.cues.Length == 0) return;

        var kept = new System.Collections.Generic.List<JourneyVoiceOver_NEW.LineCue>();
        foreach (JourneyVoiceOver_NEW.LineCue c in quasar.cues)
        {
            bool lookBack = false;
            if (c != null)
                for (int i = 0; i < c.onCue.GetPersistentEventCount(); i++)
                    if (c.onCue.GetPersistentMethodName(i) == "PlayLookBack") lookBack = true;

            if (!lookBack) kept.Add(c);
        }

        if (kept.Count == quasar.cues.Length) return;

        Undo.RecordObject(voiceOver, "Remove J1 look back");
        quasar.cues = kept.ToArray();
        EditorUtility.SetDirty(voiceOver);
        _changes++;
        Debug.Log("[JourneyVoiceOverBuilder_NEW] Quasar: removed the extra look-back on J1.");
    }

    // -- J3b ------------------------------------------------------------------

    /// <summary>
    /// The first version waited for the bake to cut a line in the first cosmic web, which it
    /// never does (the forest freezes before it). Its component and the Macro row's call to
    /// it go; FilamentAbsorber_NEW replaces both.
    /// </summary>
    static void RetireOldCallout(JourneyVoiceOver_NEW voiceOver)
    {
        FirstAbsorptionCallout_NEW old = UnityEngine.Object.FindObjectOfType<FirstAbsorptionCallout_NEW>();
        if (old == null) return;

        JourneyVoiceOver_NEW.LayerLines macro = FindRowObject(voiceOver, "Macro");
        if (macro != null)
        {
            Undo.RecordObject(voiceOver, "Retire first-absorption callout");
            for (int i = macro.onFinished.GetPersistentEventCount() - 1; i >= 0; i--)
                if (macro.onFinished.GetPersistentTarget(i) == old)
                    UnityEventTools.RemovePersistentListener(macro.onFinished, i);
            EditorUtility.SetDirty(voiceOver);
        }

        Undo.DestroyObjectImmediate(old);
        _changes++;
        Debug.Log("[JourneyVoiceOverBuilder_NEW] Removed FirstAbsorptionCallout_NEW; J3b now comes from a filament.");
    }

    /// <summary>
    /// A FilamentAbsorber_NEW trigger before the galaxy, if the scene has none: a placeholder
    /// box across the light's path, a little before the Macro-to-Micro gate, saying J3b. Its
    /// place is a guess — move and size it onto a yellow filament the light visibly flies
    /// through, with the photon trail in shot.
    /// </summary>
    static void WireFilament(JourneyVoiceOver_NEW voiceOver)
    {
        FilamentAbsorber_NEW filament = UnityEngine.Object.FindObjectOfType<FilamentAbsorber_NEW>();

        if (filament == null)
        {
            Collider gate = FindGateCollider("Micro");

            GameObject go = new GameObject("Filament_BeforeGalaxy");
            Undo.RegisterCreatedObjectUndo(go, "Create filament trigger");
            if (gate != null && gate.transform.parent != null) go.transform.SetParent(gate.transform.parent, true);

            BoxCollider box = Undo.AddComponent<BoxCollider>(go);
            box.isTrigger = true;

            if (gate != null)
            {
                Bounds b = gate.bounds;
                go.transform.position = b.center - Vector3.forward * PlaceholderLeadDistance;
                box.size = new Vector3(Mathf.Max(b.size.x, 4f), Mathf.Max(b.size.y, 4f), 3f);
            }
            else
            {
                box.size = new Vector3(20f, 20f, 3f);
            }

            filament = Undo.AddComponent<FilamentAbsorber_NEW>(go);
            _changes++;

            Debug.LogWarning("[JourneyVoiceOverBuilder_NEW] Made a PLACEHOLDER filament trigger, " +
                             PlaceholderLeadDistance + " units before the galaxy gate" +
                             (gate == null ? " (no gate found: at the origin)" : "") +
                             ". Move it onto a yellow filament the light flies through.", go);
        }

        SerializedObject so = new SerializedObject(filament);
        Fill(so, "voiceOver", voiceOver);
        Fill(so, "highlight", UnityEngine.Object.FindObjectOfType<AbsorberHighlight_NEW>());
        Fill(so, "source", UnityEngine.Object.FindObjectOfType<BakedSpectrumSource_NEW>());
        Fill(so, "firstTimeLine", Load("J3b"));
        so.ApplyModifiedProperties();
    }

    /// <summary>Before the galaxy gate by this much: Macro runs at 2x, about nine seconds of flight.</summary>
    const float PlaceholderLeadDistance = 45f;

    static Collider FindGateCollider(string layerId)
    {
        foreach (LayerGate_NEW g in UnityEngine.Object.FindObjectsOfType<LayerGate_NEW>())
        {
            SerializedProperty p = new SerializedObject(g).FindProperty("layerId");
            if (p != null && p.stringValue == layerId) return g.GetComponent<Collider>();
        }

        return null;
    }

    // -- Pace: slow for the words, quick for the rest --------------------------------

    /// <summary>
    /// The galaxy and the Milky Way stay at their slow speed while their lines are said, then
    /// the light speeds up to leave (LayerPace_NEW). Hung on each row's onFinished.
    /// </summary>
    static void WirePace(JourneyVoiceOver_NEW voiceOver)
    {
        LayerPace_NEW pace = UnityEngine.Object.FindObjectOfType<LayerPace_NEW>();
        if (pace == null)
        {
            pace = Undo.AddComponent<LayerPace_NEW>(voiceOver.gameObject);
            _changes++;
        }

        // The start layer is NOT slowed: slowing the light changes how its trail is drawn.
        // A scene that got the Quasar entry from an earlier run of this tool loses it here,
        // with the Quasar row's call to Hurry.
        FieldInfo layersField = typeof(LayerPace_NEW).GetField("layers", BindingFlags.Instance | BindingFlags.NonPublic);
        LayerPace_NEW.Layer[] paced = (LayerPace_NEW.Layer[])layersField.GetValue(pace);
        if (System.Array.Find(paced, l => l != null && l.layerId == "Quasar") != null)
        {
            Undo.RecordObject(pace, "Unpace the start layer");
            layersField.SetValue(pace, System.Array.FindAll(paced, l => l == null || l.layerId != "Quasar"));
            EditorUtility.SetDirty(pace);
            _changes++;
            Debug.Log("[JourneyVoiceOverBuilder_NEW] Quasar: removed the slow start.");
        }

        JourneyVoiceOver_NEW.LayerLines quasarRow = FindRowObject(voiceOver, "Quasar");
        if (quasarRow != null)
            for (int i = quasarRow.onFinished.GetPersistentEventCount() - 1; i >= 0; i--)
                if (quasarRow.onFinished.GetPersistentTarget(i) == pace)
                {
                    Undo.RecordObject(voiceOver, "Unpace the start layer");
                    UnityEventTools.RemovePersistentListener(quasarRow.onFinished, i);
                    EditorUtility.SetDirty(voiceOver);
                    _changes++;
                }

        foreach (string layerId in new[] { "Micro", "MilkyWay" })
        {
            JourneyVoiceOver_NEW.LayerLines row = FindRowObject(voiceOver, layerId);
            if (row == null || HasCall(row.onFinished, pace, "Hurry")) continue;

            Undo.RecordObject(voiceOver, "Wire layer pace");
            UnityEventTools.AddPersistentListener(row.onFinished, new UnityAction(pace.Hurry));
            EditorUtility.SetDirty(voiceOver);
            _changes++;

            Debug.Log("[JourneyVoiceOverBuilder_NEW] " + layerId + ": speeds up after its lines.");
        }
    }

    // -- Milky Way: the redshift demo is over ------------------------------------------

    /// <summary>Entering the Milky Way hides the demo's marks and any highlight still on.</summary>
    static void WireMilkyWayCleanup(JourneyVoiceOver_NEW voiceOver, CameraDirector_NEW director)
    {
        RedshiftMarks_NEW marks = UnityEngine.Object.FindObjectOfType<RedshiftMarks_NEW>();
        AbsorberHighlight_NEW highlight = UnityEngine.Object.FindObjectOfType<AbsorberHighlight_NEW>();
        if (marks == null && highlight == null) return;

        LayerEvents_NEW events = UnityEngine.Object.FindObjectOfType<LayerEvents_NEW>();
        if (events == null)
        {
            events = Undo.AddComponent<LayerEvents_NEW>(voiceOver.gameObject);
            _changes++;
        }

        FieldInfo field = typeof(LayerEvents_NEW).GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic);
        LayerEvents_NEW.Entry[] entries = (LayerEvents_NEW.Entry[])field.GetValue(events);

        LayerEvents_NEW.Entry entry = null;
        foreach (LayerEvents_NEW.Entry e in entries)
            if (e != null && e.layerId == "MilkyWay") entry = e;

        Undo.RecordObject(events, "Milky Way cleanup");

        if (entry == null)
        {
            entry = new LayerEvents_NEW.Entry { layerId = "MilkyWay", anchor = TransitionAnchor_NEW.CoverReached };
            var list = new System.Collections.Generic.List<LayerEvents_NEW.Entry>(entries) { entry };
            field.SetValue(events, list.ToArray());
            _changes++;
        }

        if (marks != null && !HasCall(entry.onReached, marks, "Hide"))
        {
            UnityEventTools.AddPersistentListener(entry.onReached, new UnityAction(marks.Hide));
            _changes++;
        }

        if (highlight != null && !HasCall(entry.onReached, highlight, "StopHighlight"))
        {
            UnityEventTools.AddPersistentListener(entry.onReached, new UnityAction(highlight.StopHighlight));
            _changes++;
        }

        EditorUtility.SetDirty(events);
    }

    /// <summary>An empty reference is filled; one somebody set is left alone.</summary>
    static void Fill(SerializedObject so, string field, UnityEngine.Object value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p == null || p.objectReferenceValue != null || value == null) return;

        p.objectReferenceValue = value;
        _changes++;
    }

    // -- Redshift sequence ----------------------------------------------------

    static void WireSequence(JourneyVoiceOver_NEW voiceOver, JourneySequence_NEW sequence)
    {
        // 1. The sequence starts when the cosmic web's introduction has been said, not on
        //    entering the layer — otherwise J6_1 queues behind J5 and every line after it
        //    lands a step late.
        JourneyVoiceOver_NEW.LayerLines cosmicWeb = FindRowObject(voiceOver, "CosmicWeb");

        if (cosmicWeb != null && !HasCall(cosmicWeb.onFinished, sequence, "Play"))
        {
            Undo.RecordObject(voiceOver, "Wire sequence start");
            UnityEventTools.AddPersistentListener(cosmicWeb.onFinished, new UnityAction(sequence.Play));
            EditorUtility.SetDirty(voiceOver);
            _changes++;
        }

        SerializedObject so = new SerializedObject(sequence);
        SerializedProperty startOn = so.FindProperty("startOnLayerId");

        if (string.Equals(startOn.stringValue, "CosmicWeb", StringComparison.OrdinalIgnoreCase))
        {
            // Two starts would be one start and one ignored call (playOnce), and the one that
            // wins is the early one. Cleared so the voice-over is the only trigger. F7 still
            // plays it by hand.
            startOn.stringValue = "";
            so.ApplyModifiedProperties();
            _changes++;

            Debug.Log("[JourneyVoiceOverBuilder_NEW] JourneySequence_NEW.startOnLayerId 'CosmicWeb' -> ''. " +
                      "The sequence now starts when J5 has finished (VoiceOver's CosmicWeb row, " +
                      "onFinished). Its startDelay still applies after that.", sequence);
        }

        // 2. Lines on steps, and two durations that were starting values for exactly this.
        Undo.RecordObject(sequence, "Wire sequence lines");

        FieldInfo stepsField = typeof(JourneySequence_NEW).GetField("steps",
            BindingFlags.Instance | BindingFlags.NonPublic);
        JourneySequence_NEW.Step[] steps = stepsField != null
            ? stepsField.GetValue(sequence) as JourneySequence_NEW.Step[]
            : null;

        if (steps == null)
        {
            Debug.LogError("[JourneyVoiceOverBuilder_NEW] JourneySequence_NEW has no 'steps' field. " +
                           "This tool is out of date with the script.");
            return;
        }

        for (int i = 0; i < StepClips.Length; i++)
        {
            JourneySequence_NEW.Step step = FindStep(steps, StepClips[i][0]);
            AudioClip clip = Load(StepClips[i][1]);

            if (step == null)
            {
                Debug.LogWarning("[JourneyVoiceOverBuilder_NEW] No step " + StepClips[i][0] +
                                 " for " + StepClips[i][1] + ".", sequence);
                continue;
            }

            if (clip == null || HasListenerFor(step.onEnter, voiceOver)) continue;

            UnityEventTools.AddObjectPersistentListener(step.onEnter,
                new UnityAction<AudioClip>(voiceOver.Say), clip);
            _changes++;
        }

        // J6_2 is 11.2s against F4's 10, and J6_3 is 17.9s against F5 + F6's 14. Lengthened
        // so each picture holds until its line is done — but only from the values the
        // sequence was authored with, so a duration somebody tuned is not touched.
        RepairDuration(sequence, steps, "F4", 10f, 11.6f, "J6_2_Redshift is 11.2s");
        RepairDuration(sequence, steps, "F6", 6f, 10.3f, "J6_3_Forest is 17.9s across F5 and F6");

        EditorUtility.SetDirty(sequence);
    }

    static void RepairDuration(JourneySequence_NEW sequence, JourneySequence_NEW.Step[] steps,
                               string id, float from, float to, string why)
    {
        JourneySequence_NEW.Step step = FindStep(steps, id);
        if (step == null || !Mathf.Approximately(step.duration, from)) return;

        step.duration = to;
        _changes++;

        Debug.Log("[JourneyVoiceOverBuilder_NEW] Step " + id + " duration " + from + "s -> " + to +
                  "s: " + why + ".", sequence);
    }

    static JourneySequence_NEW.Step FindStep(JourneySequence_NEW.Step[] steps, string id)
    {
        for (int i = 0; i < steps.Length; i++)
            if (steps[i] != null && string.Equals(steps[i].id, id, StringComparison.OrdinalIgnoreCase))
                return steps[i];

        return null;
    }

    static JourneyVoiceOver_NEW.LayerLines FindRowObject(JourneyVoiceOver_NEW voiceOver, string layerId)
    {
        FieldInfo field = typeof(JourneyVoiceOver_NEW).GetField("layers",
            BindingFlags.Instance | BindingFlags.NonPublic);
        JourneyVoiceOver_NEW.LayerLines[] rows = field != null
            ? field.GetValue(voiceOver) as JourneyVoiceOver_NEW.LayerLines[]
            : null;

        if (rows == null) return null;

        for (int i = 0; i < rows.Length; i++)
            if (rows[i] != null && string.Equals(rows[i].layerId, layerId, StringComparison.OrdinalIgnoreCase))
                return rows[i];

        return null;
    }

    // -- Helpers --------------------------------------------------------------

    static AudioClip Load(string name)
    {
        string path = Folder + "/" + name + ".mp3";
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);

        if (clip == null)
            Debug.LogWarning("[JourneyVoiceOverBuilder_NEW] No clip at " + path + ". That line is left empty.");

        return clip;
    }

    static bool HasCall(UnityEventBase e, UnityEngine.Object target, string method)
    {
        for (int i = 0; i < e.GetPersistentEventCount(); i++)
            if (e.GetPersistentTarget(i) == target && e.GetPersistentMethodName(i) == method)
                return true;

        return false;
    }

    /// <summary>Anything on this step already calling the voice-over is somebody's choice of line.</summary>
    static bool HasListenerFor(UnityEventBase e, UnityEngine.Object target)
    {
        for (int i = 0; i < e.GetPersistentEventCount(); i++)
            if (e.GetPersistentTarget(i) == target) return true;

        return false;
    }
}

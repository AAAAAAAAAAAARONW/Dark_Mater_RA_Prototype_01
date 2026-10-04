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
///   Quasar        J1_LeavingQuasar, J2_JourneyHUD, J2b_Readout   in order, 3s in
///   Macro         J3_CosmicWeb1
///   Micro         J4_Galaxy
///   CosmicWeb     J5_CosmicWeb2 — then the redshift sequence starts
///     step F1     J6_1_Redshift
///     step F4     J6_2_Redshift    (Spread: the lines move apart)
///     step F5     J6_3_Forest      (ShowAll: the forest)
///   MilkyWay      J7_MilkyWay
///   SolarSystem   J8_SolarSystem
///   Earth         J9_Earth
///
/// Layer rows wait for LookUpStart — the new world coming back on screen.
/// </summary>
public static class JourneyVoiceOverBuilder_NEW
{
    const string Folder = "Assets/VoiceOver/Journey";

    /// <summary>Seconds into the start layer before J1. Lets the intro camera's push in land first.</summary>
    const float StartLayerDelay = 3f;

    static readonly string[][] LayerClips =
    {
        new[] { "Quasar", "J1_LeavingQuasar", "J2_JourneyHUD", "J2b_Readout" },
        new[] { "Macro", "J3_CosmicWeb1" },
        new[] { "Micro", "J4_Galaxy" },
        new[] { "CosmicWeb", "J5_CosmicWeb2" },
        new[] { "MilkyWay", "J7_MilkyWay" },
        new[] { "SolarSystem", "J8_SolarSystem" },
        new[] { "Earth", "J9_Earth" },
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

            SerializedProperty clips = row.FindPropertyRelative("clips");
            if (clips.arraySize > 0) continue;

            for (int c = 1; c < LayerClips[i].Length; c++)
            {
                AudioClip clip = Load(LayerClips[i][c]);
                if (clip == null) continue;

                clips.arraySize++;
                clips.GetArrayElementAtIndex(clips.arraySize - 1).objectReferenceValue = clip;
                _changes++;
            }
        }

        so.ApplyModifiedProperties();
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

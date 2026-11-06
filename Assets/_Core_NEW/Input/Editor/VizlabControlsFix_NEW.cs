using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Brings the two Vizlab scenes' controls into line with the control scheme. Safe to run
/// again: each step checks before it changes anything and logs what it did.
///
///   Vizlab_Journey   adds JourneyPause_NEW (B / P pause) beside the input diagnostics;
///                    moves the diagnostics' profile cycle off F5 (VizlabWallTest_NEW's)
///                    to F3; moves DarkMatterMoment_NEW's debug start off F8
///                    (RedshiftBudget_NEW's) to F2.
///   Vizlab_tut       adds InputProfilePinner_NEW (Xbox, as the journey) and
///                    InputDiagnostics_NEW (F4 page; no cycle key, the director's F3
///                    already cycles) beside the director; moves TutorialRumble_NEW's test
///                    pulse off F4 to F6.
///
/// A menu rather than an edit of the .unity files, so it goes through Unity's own
/// serialization and does not fight a scene that is open with unsaved changes.
/// </summary>
static class VizlabControlsFix_NEW
{
    const string JourneyPath = "Assets/_Core_NEW/Vizlab_Journey.unity";
    const string TutorialPath = "Assets/_Core_NEW/Vizlab_tut.unity";
    const string Tag = "[VizlabControlsFix_NEW] ";

    [MenuItem("Tools/Journey NEW/Input/Fix Vizlab Scene Controls")]
    static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning(Tag + "Leave Play mode first.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Apply(JourneyPath, FixJourney);
        Apply(TutorialPath, FixTutorial);
    }

    static void Apply(string path, System.Func<Scene, bool> fix)
    {
        Scene scene = SceneManager.GetSceneByPath(path);
        bool wasOpen = scene.IsValid() && scene.isLoaded;
        if (!wasOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

        if (fix(scene))
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(Tag + "Saved " + path + ".");
        }
        else Debug.Log(Tag + path + " already up to date.");

        if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
    }

    // -- Vizlab_Journey -------------------------------------------------------

    static bool FixJourney(Scene scene)
    {
        bool changed = false;

        InputDiagnostics_NEW diagnostics = First<InputDiagnostics_NEW>(scene);
        if (diagnostics == null)
        {
            Debug.LogWarning(Tag + "No InputDiagnostics_NEW in " + scene.name + "; JourneyPause_NEW not added.");
        }
        else
        {
            if (First<JourneyPause_NEW>(scene) == null)
            {
                diagnostics.gameObject.AddComponent<JourneyPause_NEW>();
                Debug.Log(Tag + scene.name + ": added JourneyPause_NEW to '" + diagnostics.gameObject.name + "'.");
                changed = true;
            }

            changed |= SetKey(diagnostics, "cycleProfileKey", KeyCode.F5, KeyCode.F3);
        }

        foreach (DarkMatterMoment_NEW moment in All<DarkMatterMoment_NEW>(scene))
            changed |= SetKey(moment, "debugBeginKey", KeyCode.F8, KeyCode.F2);

        return changed;
    }

    // -- Vizlab_tut -----------------------------------------------------------

    static bool FixTutorial(Scene scene)
    {
        bool changed = false;

        TutorialDirector_NEW director = First<TutorialDirector_NEW>(scene);
        if (director == null)
        {
            Debug.LogWarning(Tag + "No TutorialDirector_NEW in " + scene.name + "; nothing added.");
        }
        else
        {
            GameObject root = director.gameObject;

            if (First<InputProfilePinner_NEW>(scene) == null)
            {
                InputProfilePinner_NEW pinner = root.AddComponent<InputProfilePinner_NEW>();
                SerializedObject so = new SerializedObject(pinner);
                so.FindProperty("profileId").stringValue = "Xbox";
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log(Tag + scene.name + ": added InputProfilePinner_NEW (Xbox) to '" + root.name + "'.");
                changed = true;
            }

            if (First<InputDiagnostics_NEW>(scene) == null)
            {
                InputDiagnostics_NEW diagnostics = root.AddComponent<InputDiagnostics_NEW>();
                SerializedObject so = new SerializedObject(diagnostics);
                so.FindProperty("cycleProfileKey").intValue = (int)KeyCode.None;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log(Tag + scene.name + ": added InputDiagnostics_NEW to '" + root.name +
                          "' (F4 page; cycle left to the director's F3).");
                changed = true;
            }
        }

        foreach (TutorialRumble_NEW rumble in All<TutorialRumble_NEW>(scene))
            changed |= SetKey(rumble, "testKey", KeyCode.F4, KeyCode.F6);

        return changed;
    }

    // -- Helpers --------------------------------------------------------------

    /// <summary>Change a KeyCode field only if it still holds the clashing key.</summary>
    static bool SetKey(Object target, string field, KeyCode from, KeyCode to)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null)
        {
            Debug.LogWarning(Tag + target.GetType().Name + " has no field '" + field + "'.", target);
            return false;
        }
        if (p.intValue != (int)from) return false;

        p.intValue = (int)to;
        so.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log(Tag + target.GetType().Name + "." + field + ": " + from + " -> " + to + ".", target);
        return true;
    }

    static T First<T>(Scene scene) where T : Component
    {
        List<T> all = All<T>(scene);
        return all.Count > 0 ? all[0] : null;
    }

    static List<T> All<T>(Scene scene) where T : Component
    {
        List<T> found = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
            found.AddRange(root.GetComponentsInChildren<T>(true));
        return found;
    }
}

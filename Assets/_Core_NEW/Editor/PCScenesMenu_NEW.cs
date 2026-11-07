using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tools > Journey NEW > Refresh PC Scenes from the Vizlab Scenes. The PC piece runs
/// PC_Cover → PC_Tutorial → PC_Journey → PC_Credits; the middle two are copies of Vizlab_tut
/// and Vizlab_Journey, where the work happens. This makes them again from those, so a change
/// made for the wall reaches the PC with one click instead of being made twice:
///
///   copy        Vizlab_tut → PC_Tutorial, Vizlab_Journey → PC_Journey, whole. The PC files
///               keep their .meta, so Build Settings still point at them.
///   getReal3D   every [getReal3D] (GetReal3DView_NEW) switched off: the rig's own camera
///               renders, input is the pad's, the HUD is an ordinary overlay - the desk.
///   the flow    the tutorial hands off to PC_Journey and sends an idle visitor to PC_Cover;
///               the journey's ending goes on to PC_Credits.
///
/// ANYTHING CHANGED BY HAND IN PC_Tutorial OR PC_Journey IS LOST: make changes in the Vizlab
/// scenes and refresh. PC_Cover and PC_Credits are their own and are never touched here.
/// </summary>
static class PCScenesMenu_NEW
{
    const string Core = "Assets/_Core_NEW/";
    const string VizlabTutorial = Core + "Vizlab_tut.unity";
    const string VizlabJourney = Core + "Vizlab_Journey.unity";
    const string PCCover = Core + "PC_Cover.unity";
    const string PCTutorial = Core + "PC_Tutorial.unity";
    const string PCJourney = Core + "PC_Journey.unity";
    const string PCCredits = Core + "PC_Credits.unity";

    [MenuItem("Tools/Journey NEW/Refresh PC Scenes from the Vizlab Scenes")]
    static void Refresh()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!EditorUtility.DisplayDialog("Refresh PC scenes",
                "PC_Tutorial and PC_Journey will be made again from Vizlab_tut and Vizlab_Journey, with getReal3D " +
                "off and the PC flow wired in.\n\nAnything changed by hand in the two PC scenes is lost.",
                "Refresh", "Cancel")) return;

        string previous = SceneManager.GetActiveScene().path;

        Copy(VizlabTutorial, PCTutorial, () =>
        {
            Set<SceneHandoff_NEW>("journeyScenePath", PCJourney);
            Set<TutorialAttract_NEW>("coverScenePath", PCCover);
        });
        Copy(VizlabJourney, PCJourney, () => Set<EarthEnding_NEW>("nextScenePath", PCCredits));

        if (!string.IsNullOrEmpty(previous) && File.Exists(previous)) EditorSceneManager.OpenScene(previous);
        Debug.Log("PC scenes: PC_Tutorial and PC_Journey refreshed from the Vizlab scenes. " +
                  "The PC piece is PC_Cover → PC_Tutorial → PC_Journey → PC_Credits.");
    }

    static void Copy(string source, string target, Action wire)
    {
        if (!File.Exists(source))
        {
            Debug.LogError("PC scenes: " + source + " is missing.");
            return;
        }

        File.Copy(source, target, true);
        AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceUpdate);

        Scene scene = EditorSceneManager.OpenScene(target, OpenSceneMode.Single);
        int off = 0;
        foreach (GetReal3DView_NEW view in Resources.FindObjectsOfTypeAll<GetReal3DView_NEW>())
        {
            if (view.gameObject.scene != scene) continue;
            view.gameObject.SetActive(false);
            off++;
        }
        wire();
        EditorSceneManager.SaveScene(scene);
        Debug.Log("PC scenes: " + target + " from " + source + ", " + off + " getReal3D view(s) switched off.");
    }

    /// <summary>A string field on every component of type T in the open scene.</summary>
    static void Set<T>(string field, string value) where T : Component
    {
        int n = 0;
        foreach (T c in Resources.FindObjectsOfTypeAll<T>())
        {
            if (EditorUtility.IsPersistent(c) || !c.gameObject.scene.IsValid()) continue;
            var so = new SerializedObject(c);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) continue;
            p.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            n++;
        }
        if (n == 0) Debug.LogWarning("PC scenes: no " + typeof(T).Name + " with a '" + field + "' in this scene; the flow is not wired there.");
    }
}

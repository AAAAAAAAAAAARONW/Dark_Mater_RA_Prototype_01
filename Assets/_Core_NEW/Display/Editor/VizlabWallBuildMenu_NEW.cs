using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Tools > Vizlab > Build for the Wall (getReal3D). The Vizlab's build, kept apart from the PC
/// build so neither can change the other:
///
///   scenes        Vizlab_tut then Vizlab_Journey, whatever File > Build Settings lists. The
///                 PC build goes on using Build Settings exactly as before.
///   product name  the project's, plus " Vizlab", for this build only. Unity keys the saved
///                 window size, PlayerPrefs, Player.log and persistentDataPath by product
///                 name, so a PC run on the same machine can never leave the wall starting
///                 in its window size, and the wall's logs are in their own folder.
///   splash        off for this build where the licence allows it (Unity Personal forces it
///                 on). It is one more thing between launch and the first frame.
///   target        Windows 64-bit, which getReal3D requires.
/// Every setting it changes is put back when the build ends, failed or not.
///
/// It checks first, and stops on what would put a broken build on the wall: a scene that has
/// not had GameObject > Vizlab > Adapt ... Camera to getReal3D run on it, and a tutorial that
/// hands off to anything but Vizlab_Journey. It warns about the player settings getReal3D
/// lists as required, without changing them (they are the PC build's too).
/// </summary>
static class VizlabWallBuildMenu_NEW
{
    const string TutorialScene = "Assets/_Core_NEW/Vizlab_tut.unity";
    const string JourneyScene = "Assets/_Core_NEW/Vizlab_Journey.unity";
    const string ProductSuffix = " Vizlab";

    [MenuItem("Tools/Vizlab/Build for the Wall (getReal3D)...")]
    static void Build()
    {
        if (!Check()) return;

        string folder = EditorUtility.SaveFolderPanel("Folder for the Vizlab build", "", "");
        if (string.IsNullOrEmpty(folder)) return;

        string productName = PlayerSettings.productName;
        bool splash = PlayerSettings.SplashScreen.show;
        string exe = Path.Combine(folder, productName + ProductSuffix + ".exe");

        try
        {
            PlayerSettings.productName = productName + ProductSuffix;
            if (PlayerSettings.advancedLicense) PlayerSettings.SplashScreen.show = false;

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { TutorialScene, JourneyScene },
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });

            if (report.summary.result == BuildResult.Succeeded)
                Debug.Log("Vizlab: wall build at " + exe + ". Add it to the getReal3D launcher.");
            else
                Debug.LogError("Vizlab: wall build " + report.summary.result + " (" + report.summary.totalErrors + " errors). See the Console above.");
        }
        finally
        {
            PlayerSettings.productName = productName;
            PlayerSettings.SplashScreen.show = splash;
        }
    }

    /// <summary>False, with the reasons in the Console, if the build would not work on the wall.</summary>
    static bool Check()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        string viewGuid = ScriptGuid(typeof(GetReal3DView_NEW));
        foreach (string scene in new[] { TutorialScene, JourneyScene })
        {
            if (!File.Exists(scene)) { errors.Add(scene + " is missing."); continue; }
            string text = File.ReadAllText(scene);
            if (viewGuid != null && !text.Contains(viewGuid))
                errors.Add(Path.GetFileNameWithoutExtension(scene) + " has no getReal3D view. Open it and run GameObject > Vizlab > Adapt " +
                           (scene == TutorialScene ? "Tutorial" : "Journey") + " Camera to getReal3D, then save.");
        }

        if (File.Exists(TutorialScene) && !File.ReadAllText(TutorialScene).Contains("journeyScenePath: " + JourneyScene))
            errors.Add("Vizlab_tut's SceneHandoff_NEW does not hand off to " + JourneyScene + ".");

        if (!PlayerSettings.runInBackground) warnings.Add("Run In Background is off; getReal3D needs every copy to keep running unfocused.");
        if (PlayerSettings.resizableWindow) warnings.Add("Resizable Window is on; getReal3D asks for it off.");
        if (PlayerSettings.forceSingleInstance) warnings.Add("Force Single Instance is on; the wall runs several copies on one machine.");
        if (PlayerSettings.fullScreenMode != FullScreenMode.Windowed) warnings.Add("Fullscreen Mode is " + PlayerSettings.fullScreenMode + "; getReal3D places windowed copies.");
        // Read from the file rather than by switching quality levels, which would change the editor's.
        const string quality = "ProjectSettings/QualitySettings.asset";
        if (File.Exists(quality) && System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(quality), @"vSyncCount: [1-9]"))
            warnings.Add("VSync is on in at least one quality level; getReal3D does its own swap sync.");
        if (!PlayerSettings.advancedLicense) warnings.Add("The Unity splash screen stays on (Unity Personal). It adds a few seconds before each copy's first frame.");

        foreach (string w in warnings) Debug.LogWarning("Vizlab wall build: " + w);
        foreach (string e in errors) Debug.LogError("Vizlab wall build: " + e);
        return errors.Count == 0;
    }

    static string ScriptGuid(System.Type type)
    {
        foreach (string guid in AssetDatabase.FindAssets(type.Name + " t:MonoScript"))
        {
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
            if (script != null && script.GetClass() == type) return guid;
        }
        return null;
    }
}

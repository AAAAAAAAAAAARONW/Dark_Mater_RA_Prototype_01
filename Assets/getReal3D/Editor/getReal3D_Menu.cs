using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;
using System;

public class getReal3D_Menu {

    [UnityEditor.Callbacks.PostProcessScene(101)]
    [MenuItem("getReal3D/Advanced/getReal3D Script Execution Order", false, 105)]
    static public void FixExecutionOrder()
    {
        getReal3D.Editor.Utils.FixScriptExecutionOrder();
    }

    [UnityEditor.Callbacks.PostProcessScene(102)]
    static public void CheckForMultisampling()
    {
        if(Application.isPlaying) {
            return;
        }

        System.IO.StringWriter errors = new System.IO.StringWriter();
        errors.WriteLine("This build might fail when running with getReal3D for Unity:\n");

        bool hasWarning = false;
        int currentLevel = UnityEngine.QualitySettings.GetQualityLevel();
        int levelCount = UnityEngine.QualitySettings.names.Length;
        for(int i=0; i<levelCount; ++i) {
            UnityEngine.QualitySettings.SetQualityLevel(i);
            if(UnityEngine.QualitySettings.vSyncCount != 0) {
                string err = "VSync is activated for quality settings " +
                    UnityEngine.QualitySettings.names[i] + ".";
                errors.WriteLine(err);
                hasWarning = true;
            }
        }

        UnityEngine.QualitySettings.SetQualityLevel(currentLevel);

        if(hasWarning) {
            ShowBuildError(errors.ToString());
        }
    }

    static private bool CameraHasHdr(Camera c)
    {
#if UNITY_5_6_OR_NEWER
        return c && c.allowHDR;
#else
        return c && c.hdr;
#endif
    }

    static private void ShowBuildError(string message)
    {
        if(UnityEditorInternal.InternalEditorUtility.inBatchMode) {
            Debug.LogError(message);
        }
        else {
            EditorUtility.DisplayDialog("getReal3D", message, "Ok");
        }
    }

    [UnityEditor.Callbacks.PostProcessScene(103)]
    static public void CheckDuplicatedNetworkIdentities()
    {
        if(Application.isPlaying) {
            return;
        }
        var duplicates = getReal3D.MultiCluster.NetworkIdentity.GetDuplicated();
        if(duplicates.Count != 0) {
            ShowBuildError("Duplicated NetworkIdentity found. " +
                "Please check the scene in the getReal3D menu.");
        }
    }

    [UnityEditor.Callbacks.PostProcessScene(105)]
    static public void CheckScriptPriority()
    {
        if (Application.isPlaying) {
            return;
        }
        if (getReal3D.SceneChecker.GetScriptPriorityErrors().Count() != 0) {
            var message = "Script priority error(s) detected. Please use getReal3D scene checker.";
            ShowBuildError(message);
        }
    }

    private static bool ObjectUsesGetReal3D(GameObject go)
    {
        return
            go.GetComponentInChildren<getRealCameraUpdater>(true) != null ||
            go.GetComponentInChildren<getRealHeadUpdater>(true) != null ||
            go.GetComponentInChildren<getRealSensorUpdater>(true) != null ||
            go.GetComponentInChildren<getRealWandUpdater>(true) != null ||
            go.GetComponentInChildren<getReal3D.MultiCluster.MultiClusterCameraUpdater>(true) != null ||
            go.GetComponentInChildren<getReal3D.MultiCluster.MultiClusterCameraDuplicator>(true) != null ||
            go.GetComponentInChildren<getReal3D.MultiCluster.MultiClusterHeadUpdater>(true) != null ||
            go.GetComponentInChildren<getReal3D.MultiCluster.MultiClusterWandUpdater>(true) != null
            ;
    }

    private static bool SceneCheckFunctor(Func<GameObject, bool> testFunctor)
    {
        if(Roots.Any(go => testFunctor(go))) {
            return true;
        }

        var networkManager = UnityEngine.Object.FindObjectOfType
            <getReal3D.MultiCluster.NetworkManager>();
        if(!networkManager) {
            return false;
        }
        foreach(var identity in networkManager.m_spawnPrefabs) {
            if(testFunctor(identity.gameObject)) {
                return true;
            }
        }
        return false;
    }

    private static bool SceneIsUsingGetReal3D()
    {
        return SceneCheckFunctor(ObjectUsesGetReal3D);
    }

    private static GameObject[] Roots {
        get { return UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects(); }
    }

    static T[] FindObjectsOfTypeInScene<T>() where T : Behaviour
    {
        List<T> res = new List<T>();
        T[] allComponents = Resources.FindObjectsOfTypeAll<T>();
        foreach(var monoBehaviour in allComponents) {
            if(monoBehaviour.hideFlags != HideFlags.None) {
                continue;
            }
            res.Add(monoBehaviour);
        }
        return res.ToArray();
    }

    public static void BuildPlayerImpl(string[] levels, string output, bool arch64 = false)
    {
        BuildTarget currentTarget = EditorUserBuildSettings.activeBuildTarget;
        SwitchActiveBuildStandaloneTarget(BuildTarget.StandaloneWindows);

        BuildTarget buildTarget = arch64 ? BuildTarget.StandaloneWindows64 :
            BuildTarget.StandaloneWindows;
        AddGraphicApi(buildTarget, UnityEngine.Rendering.GraphicsDeviceType.Direct3D11);
#if !UNITY_2017_2_OR_NEWER
        AddGraphicApi(buildTarget, UnityEngine.Rendering.GraphicsDeviceType.Direct3D9);
#endif
        UnityEditor.BuildOptions options = BuildOptions.None;
        BuildPipeline.BuildPlayer(levels, output, buildTarget, options);
        SwitchActiveBuildStandaloneTarget(currentTarget);
    }

    public static void BuildPlayerImpl(string output, bool arch64 = false)
    {
        BuildPlayerImpl(getEnabledScenes(), output, arch64);
    }

    public static string[] getEnabledScenes()
    {
        List<string> temp = new List<string>();
        foreach(UnityEditor.EditorBuildSettingsScene S in UnityEditor.EditorBuildSettings.scenes) {
            if(S.enabled) {
                temp.Add(S.path);
            }
        }
        return temp.ToArray();
    }

    private static void AddGraphicApi(BuildTarget target,
        UnityEngine.Rendering.GraphicsDeviceType type)
    {
        List<UnityEngine.Rendering.GraphicsDeviceType> deviceTypes = PlayerSettings.GetGraphicsAPIs
            (BuildTarget.StandaloneWindows).ToList<UnityEngine.Rendering.GraphicsDeviceType>();
        if(!deviceTypes.Contains(type)) {
            deviceTypes.Add(type);
            PlayerSettings.SetGraphicsAPIs(target, deviceTypes.ToArray());
        }
    }

    public static void SwitchActiveBuildStandaloneTarget(BuildTarget target)
    {
#if UNITY_5_6_OR_NEWER
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, target);
#else
        EditorUserBuildSettings.SwitchActiveBuildTarget(target);
#endif
    }
}

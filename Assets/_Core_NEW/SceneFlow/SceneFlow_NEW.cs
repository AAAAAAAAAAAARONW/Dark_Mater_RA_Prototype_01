using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The PC piece in order: PC_Cover → PC_Tutorial → PC_Journey → PC_Credits → back to PC_Cover.
/// Each scene names the next by asset path in its own component (CoverScreen_NEW,
/// SceneHandoff_NEW, EarthEnding_NEW, CreditsRoll_NEW; TutorialAttract_NEW for an idle
/// visitor); this is only the loading they share.
///
/// Synchronous on purpose: every hand-over happens under black, so the hitch is never seen,
/// and a scene that is the active one on the next frame behaves the same on a desk and on a
/// getReal3D cluster.
/// </summary>
public static class SceneFlow_NEW
{
    /// <summary>
    /// Load <paramref name="path"/> in place of everything. In a build it must be in Build
    /// Settings; in the editor a scene that is not is still played into, with a warning, so the
    /// flow can be tried before the build list is settled. False, with the reason logged, when
    /// it cannot be loaded.
    /// </summary>
    public static bool Load(string path, Object context)
    {
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("[SceneFlow_NEW] No scene to go on to.", context);
            return false;
        }

        if (SceneUtility.GetBuildIndexByScenePath(path) >= 0)
        {
            SceneManager.LoadScene(path, LoadSceneMode.Single);
            return true;
        }

#if UNITY_EDITOR
        Debug.LogWarning("[SceneFlow_NEW] '" + path + "' is not in Build Settings; playing into it anyway because " +
                         "this is the editor. Add it before building.", context);
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
        return true;
#else
        Debug.LogError("[SceneFlow_NEW] '" + path + "' is not in this build.", context);
        return false;
#endif
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The PC piece in order: PC_Cover → PC_Tutorial → PC_Journey → PC_Credits → back to PC_Cover.
/// Each scene names the next by asset path in its own component (CoverScreen_NEW,
/// SceneHandoff_NEW, EarthEnding_NEW, CreditsRoll_NEW; TutorialAttract_NEW for an idle
/// visitor); this is only the loading they share.
///
/// Synchronous on purpose: every hand-over happens under black, so the hitch is never seen,
/// and a scene that is the active one on the next frame behaves the same on a desk and on a
/// getReal3D cluster.
///
/// OUT OF BLACK, EVERY TIME. Each scene that leaves through here has faded to black first;
/// the next used to start however it started - the tutorial at full brightness on its first
/// frame, the cover's sky at once behind a card still fading in: a cut. So a black sheet is
/// carried across the load and lifted over openSeconds once the new scene has had a moment to
/// settle. A scene with a fade-in of its own (the credits) simply fades under it.
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
            CarryBlack();
            SceneManager.LoadScene(path, LoadSceneMode.Single);
            return true;
        }

#if UNITY_EDITOR
        Debug.LogWarning("[SceneFlow_NEW] '" + path + "' is not in Build Settings; playing into it anyway because " +
                         "this is the editor. Add it before building.", context);
        CarryBlack();
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
        return true;
#else
        Debug.LogError("[SceneFlow_NEW] '" + path + "' is not in this build.", context);
        return false;
#endif
    }

    /// <summary>Seconds of black after the new scene is in, and seconds for the black to lift.</summary>
    public const float SettleSeconds = 0.35f, OpenSeconds = 1.6f;

    /// <summary>A black sheet over everything that outlives the load and lifts itself in the next scene.</summary>
    static void CarryBlack()
    {
        var go = new GameObject("Scene Flow black");
        Object.DontDestroyOnLoad(go);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32500;   // over every HUD and every scene's own fade
        var sheet = new GameObject("Black", typeof(RectTransform)).AddComponent<Image>();
        sheet.transform.SetParent(go.transform, false);
        var rt = (RectTransform)sheet.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        sheet.color = Color.black;
        sheet.raycastTarget = false;
        go.AddComponent<SceneFlowBlack_NEW>().sheet = sheet;
    }
}

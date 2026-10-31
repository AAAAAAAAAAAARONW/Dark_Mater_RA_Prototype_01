using UnityEngine;
using UnityEngine.SceneManagement;
using PP = UnityEngine.Rendering.PostProcessing;

/// <summary>
/// On the wall, switches off the post effects that are drawn around the middle of a window:
/// vignette, chromatic aberration and lens distortion.
///
/// WHY. On the Vizlab the picture is four windows, one per copy of the game - row 1, rows
/// 2-3, rows 4-5, rows 6-7 - and each copy runs its own post-processing on its own window.
/// An effect centred on the window is then drawn four times: the vignette darkens the edges
/// of every window, which puts dark bands across the wall where rows 1/2, 3/4 and 5/6 meet,
/// and a dive's funnel and lens bend repeat once per window instead of once on the wall.
/// The scenes' main profile has a vignette, and LayerDive_NEW adds all three during dives.
/// Bloom, motion blur and colour grading do not depend on where the window's middle is, and
/// stay.
///
/// HOW. A global volume at a priority above everything else in the scenes (LayerDive_NEW's
/// is 100), with the three effects overridden off, created as each scene loads on a layer
/// the scene's cameras listen to.
///
/// Installs itself only when the getReal3D launcher started the process. At a desk, and in
/// the editor, every effect is as it was.
/// </summary>
public static class ClusterScreenEffects_NEW
{
    const float Priority = 10000f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if (!GetReal3DRuntime_NEW.LaunchedByGetReal3D) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Every layer any camera in the scene listens to: the eye's post layer is a copy
        // of the rig's, but read them all rather than assume.
        int mask = 0;
        foreach (PP.PostProcessLayer layer in Object.FindObjectsOfType<PP.PostProcessLayer>())
            mask |= layer.volumeLayer.value;
        if (mask == 0) return;

        int count = 0;
        for (int bit = 0; bit < 32; bit++)
        {
            if ((mask & (1 << bit)) == 0) continue;

            var vignette = ScriptableObject.CreateInstance<PP.Vignette>();
            vignette.enabled.Override(false);
            vignette.intensity.Override(0f);

            var chroma = ScriptableObject.CreateInstance<PP.ChromaticAberration>();
            chroma.enabled.Override(false);
            chroma.intensity.Override(0f);

            var lens = ScriptableObject.CreateInstance<PP.LensDistortion>();
            lens.enabled.Override(false);
            lens.intensity.Override(0f);

            PP.PostProcessVolume volume = PP.PostProcessManager.instance.QuickVolume(bit, Priority, vignette, chroma, lens);
            volume.gameObject.name = "Wall: no window-centred effects";
            count++;
        }

        Debug.Log("[ClusterScreenEffects_NEW] " + scene.name + ": vignette, chromatic aberration and lens distortion off " +
                  "on " + count + " volume layer(s); each window would draw them around its own middle.");
    }
}

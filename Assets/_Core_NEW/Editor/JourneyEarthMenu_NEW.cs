using UnityEditor;
using UnityEngine;

/// <summary>
/// Puts the cloud of Earth's atmosphere into the open scene: EarthClouds_NEW next to the
/// other layer responders, and the Earth video held back until the camera is out under the
/// cloud, so the cloud is seen first. Run again after an update to put the video's delay
/// back in step with the cloud.
///
/// It works on the scene as loaded in the editor, not on the file, so unsaved changes stay
/// and this is one more undoable step. Save the scene to keep it.
/// </summary>
static class JourneyEarthMenu_NEW
{
    const string MenuPath = "Tools/Journey NEW/Add Earth Clouds to Open Scene";

    [MenuItem(MenuPath)]
    static void AddEarthClouds()
    {
        EarthClouds_NEW clouds = Object.FindObjectOfType<EarthClouds_NEW>();
        if (clouds != null)
        {
            Debug.Log("Journey NEW: '" + clouds.name + "' already has EarthClouds_NEW.", clouds);
        }
        else
        {
            WorldSwitcher_NEW worlds = Object.FindObjectOfType<WorldSwitcher_NEW>();
            if (worlds == null)
            {
                Debug.LogWarning("Journey NEW: no WorldSwitcher_NEW in the open scene, so no clouds were added. " +
                                 "They hide the Solar System through it once the camera is in them.");
                return;
            }

            clouds = Undo.AddComponent<EarthClouds_NEW>(worlds.gameObject);
            Debug.Log("Journey NEW: added EarthClouds_NEW to '" + worlds.name + "'. Arriving at Earth now " +
                      "falls through a deck of cloud.", clouds);
        }

        // The video comes up as the camera comes out under the cloud, not over it.
        EarthCutscene_NEW cutscene = Object.FindObjectOfType<EarthCutscene_NEW>();
        if (cutscene != null)
        {
            float delay = Mathf.Max(0f, clouds.ClearedAt - 0.4f);
            Undo.RecordObject(cutscene, "Hold the Earth video for the clouds");
            cutscene.StartDelay = delay;
            EditorUtility.SetDirty(cutscene);
            Debug.Log("Journey NEW: the Earth video now fades in " + delay.ToString("0.0") + "s after arriving, " +
                      "as the camera comes out under the cloud.", cutscene);
        }

        Debug.Log("Journey NEW: save the scene to keep it.");
    }

    [MenuItem(MenuPath, true)]
    static bool AddEarthCloudsValidate() => !Application.isPlaying;
}

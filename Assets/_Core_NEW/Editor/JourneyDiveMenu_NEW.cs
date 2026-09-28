using UnityEditor;
using UnityEngine;

/// <summary>
/// Puts the journey's left-stick zoom and the layer dive into the open scene.
///
/// Both find everything else they need at runtime, so adding the two components is the
/// whole setup: JourneyZoom_NEW goes on the player, LayerDive_NEW next to
/// WorldSwitcher_NEW (the responders object), and CameraDirector_NEW picks the dive up
/// by itself. Each is skipped if the scene already has one, so running this twice is
/// harmless.
///
/// It works on the scene as loaded in the editor, not on the file, so unsaved changes
/// stay and the additions are one more undoable step. Nothing here ships.
/// </summary>
static class JourneyDiveMenu_NEW
{
    const string MenuPath = "Tools/Journey NEW/Add Zoom and Dive to Open Scene";

    [MenuItem(MenuPath)]
    static void AddZoomAndDive()
    {
        int added = 0;

        PlayerRig_NEW player = Object.FindObjectOfType<PlayerRig_NEW>();
        if (player == null)
        {
            Debug.LogWarning("Journey NEW: no PlayerRig_NEW in the open scene, so no zoom was added.");
        }
        else if (player.GetComponent<JourneyZoom_NEW>() != null)
        {
            Debug.Log("Journey NEW: '" + player.name + "' already has JourneyZoom_NEW.", player);
        }
        else
        {
            Undo.AddComponent<JourneyZoom_NEW>(player.gameObject);
            Debug.Log("Journey NEW: added JourneyZoom_NEW to '" + player.name + "'. Left stick zooms.", player);
            added++;
        }

        LayerDive_NEW existingDive = Object.FindObjectOfType<LayerDive_NEW>();
        WorldSwitcher_NEW worlds = Object.FindObjectOfType<WorldSwitcher_NEW>();
        if (existingDive != null)
        {
            Debug.Log("Journey NEW: '" + existingDive.name + "' already has LayerDive_NEW.", existingDive);
        }
        else if (worlds == null)
        {
            Debug.LogWarning("Journey NEW: no WorldSwitcher_NEW in the open scene, so no dive was added. " +
                             "The dive borrows its render groups.");
        }
        else
        {
            Undo.AddComponent<LayerDive_NEW>(worlds.gameObject);
            Debug.Log("Journey NEW: added LayerDive_NEW to '" + worlds.name + "'. The gate into Micro " +
                      "now dives instead of looking down; add rows there for other gates.", worlds);
            added++;
        }

        if (added > 0)
            Debug.Log("Journey NEW: save the scene to keep " + (added == 1 ? "it" : "them") + ".");
    }

    [MenuItem(MenuPath, true)]
    static bool AddZoomAndDiveValidate() => !Application.isPlaying;
}

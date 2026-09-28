using UnityEditor;
using UnityEngine;

/// <summary>
/// Puts the journey's left-stick zoom, the layer dive and the photon layering into the
/// open scene.
///
/// All of them find everything else they need at runtime, so adding the components is the
/// whole setup: JourneyZoom_NEW and PhotonLayering_NEW go on the player, LayerDive_NEW next
/// to WorldSwitcher_NEW (the responders object), and CameraDirector_NEW picks the dive up
/// by itself. Each is skipped if the scene already has one, so running this again after an
/// update adds only what is new.
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

        if (player != null && Object.FindObjectOfType<PhotonLayering_NEW>() == null)
        {
            Undo.AddComponent<PhotonLayering_NEW>(player.gameObject);
            Debug.Log("Journey NEW: added PhotonLayering_NEW to '" + player.name + "'. Web volumes " +
                      "with Layer Around Photon ticked now pass in front of and behind the light.", player);
            added++;
        }

        LayerDive_NEW dive = Object.FindObjectOfType<LayerDive_NEW>();
        WorldSwitcher_NEW worlds = Object.FindObjectOfType<WorldSwitcher_NEW>();
        if (dive != null)
        {
            Debug.Log("Journey NEW: '" + dive.name + "' already has LayerDive_NEW.", dive);
        }
        else if (worlds == null)
        {
            Debug.LogWarning("Journey NEW: no WorldSwitcher_NEW in the open scene, so no dive was added. " +
                             "The dive borrows its render groups.");
        }
        else
        {
            dive = Undo.AddComponent<LayerDive_NEW>(worlds.gameObject);
            Debug.Log("Journey NEW: added LayerDive_NEW to '" + worlds.name + "'. The gate into Micro " +
                      "now dives instead of looking down; add rows there for other gates.", worlds);
            added++;
        }

        // The two gates tuned so far, each added only if the scene has no row for it yet —
        // so running this after an update brings in a new gate without touching tuned ones.
        if (dive != null)
        {
            added += AddRowIfMissing(dive, new LayerDive_NEW.Dive(), "dives in (cosmic web to galaxy)");
            added += AddRowIfMissing(dive, LayerDive_NEW.Dive.PullOut("CosmicWeb"), "pulls out (galaxy back to the web)");
        }

        if (added > 0)
            Debug.Log("Journey NEW: save the scene to keep " + (added == 1 ? "it" : "them") + ".");
    }

    static int AddRowIfMissing(LayerDive_NEW dive, LayerDive_NEW.Dive row, string what)
    {
        if (dive.HasRow(row.toLayerId)) return 0;

        Undo.RecordObject(dive, "Add dive row");
        dive.AddRow(row);
        EditorUtility.SetDirty(dive);

        Debug.Log("Journey NEW: added a row for the gate into " + row.toLayerId + ", which now " + what + ".", dive);
        return 1;
    }

    [MenuItem(MenuPath, true)]
    static bool AddZoomAndDiveValidate() => !Application.isPlaying;
}

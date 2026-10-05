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
    const string ResetPath = "Tools/Journey NEW/Reset Dive Row to Starting Values/";

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

        // The gates tuned so far, each added only if the scene has no row for it yet — so
        // running this after an update brings in a new gate without touching tuned ones. A
        // gate whose starting values have changed since is reset with the menus below.
        if (dive != null)
        {
            foreach (Preset preset in Presets)
                added += AddRowIfMissing(dive, preset.row(), preset.what);
        }

        if (added > 0)
            Debug.Log("Journey NEW: save the scene to keep " + (added == 1 ? "it" : "them") + ".");
    }

    // One gate at a time: a row for another gate may carry tuning that has to survive.
    [MenuItem(ResetPath + "Into Micro (dive in)")]
    static void ResetMicroRow() => ResetRow(Presets[0]);

    [MenuItem(ResetPath + "Into CosmicWeb (leave the cluster)")]
    static void ResetCosmicWebRow() => ResetRow(Presets[1]);

    [MenuItem(ResetPath + "Into MilkyWay (dive in)")]
    static void ResetMilkyWayRow() => ResetRow(Presets[2]);

    /// <summary>
    /// Puts one gate's row back to its current starting values — for when those have
    /// changed since the row was added. Every other row is left alone; one undoable step.
    /// </summary>
    static void ResetRow(Preset preset)
    {
        LayerDive_NEW dive = Object.FindObjectOfType<LayerDive_NEW>();
        if (dive == null)
        {
            Debug.LogWarning("Journey NEW: no LayerDive_NEW in the open scene. Run '" + MenuPath + "' first.");
            return;
        }

        LayerDive_NEW.Dive row = preset.row();
        Undo.RecordObject(dive, "Reset dive row");
        dive.ReplaceRow(row);
        EditorUtility.SetDirty(dive);

        Debug.Log("Journey NEW: the gate into " + row.toLayerId + " " + preset.what + ", from its starting " +
                  "values. Save the scene to keep it.", dive);
    }

    struct Preset
    {
        public System.Func<LayerDive_NEW.Dive> row;
        public string what;
    }

    static readonly Preset[] Presets =
    {
        new Preset { row = () => new LayerDive_NEW.Dive(), what = "dives in (cosmic web to galaxy)" },
        new Preset { row = () => LayerDive_NEW.Dive.ComingOut("CosmicWeb"), what = "leaves the cluster (galaxy back up to the web)" },

        // The same dive as into Micro, into the Milky Way. The point: 12 past the gate, on the
        // line, inside the bright Core knot of the x6000 web there (centre 3.4 off the line,
        // 7 across), with yellow knots just beyond; the approach starts once the photon is
        // out of the yellow cluster it came out into. dissolveFrom as tuned on Micro.
        new Preset
        {
            row = () => new LayerDive_NEW.Dive { toLayerId = "MilkyWay", focusPastGate = 12f, approachDistance = 30f, dissolveFrom = 0.5f },
            what = "dives in (cosmic web to the Milky Way)"
        }
    };

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

    [MenuItem(ResetPath + "Into Micro (dive in)", true)]
    [MenuItem(ResetPath + "Into CosmicWeb (leave the cluster)", true)]
    [MenuItem(ResetPath + "Into MilkyWay (dive in)", true)]
    static bool ResetRowValidate() => !Application.isPlaying;
}

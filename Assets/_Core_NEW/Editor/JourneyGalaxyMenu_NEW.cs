using UnityEditor;
using UnityEngine;

/// <summary>
/// Puts GalaxyMotion_NEW on the galaxies of the open scene: every active child of the Micro
/// and MilkyWay render groups that is made of particle systems and has none yet. The Galaxy2
/// prefab carries one already, so its instances are skipped; the Milky Way's copy of it is
/// not a prefab instance and gets its own.
///
/// It works on the scene as loaded in the editor, not on the file, so unsaved changes stay
/// and this is one more undoable step. Save the scene to keep it.
/// </summary>
static class JourneyGalaxyMenu_NEW
{
    const string MenuPath = "Tools/Journey NEW/Add Galaxy Motion to Open Scene";

    static readonly string[] GalaxyLayers = { "Micro", "MilkyWay" };

    // Fewer than this many particle systems under it and it is not a galaxy.
    const int MinSystems = 5;

    [MenuItem(MenuPath)]
    static void AddGalaxyMotion()
    {
        WorldSwitcher_NEW worlds = Object.FindObjectOfType<WorldSwitcher_NEW>();
        if (worlds == null)
        {
            Debug.LogWarning("Journey NEW: no WorldSwitcher_NEW in the open scene, so no galaxies to find. " +
                             "Add GalaxyMotion_NEW to a galaxy's root by hand.");
            return;
        }

        int added = 0;
        foreach (string layer in GalaxyLayers)
        {
            Transform group = worlds.GroupRoot(layer);
            if (group == null) continue;

            foreach (Transform child in group)
            {
                if (!child.gameObject.activeInHierarchy) continue;
                if (child.GetComponentsInChildren<ParticleSystem>(true).Length < MinSystems) continue;

                GalaxyMotion_NEW existing = child.GetComponent<GalaxyMotion_NEW>();
                if (existing != null)
                {
                    Debug.Log("Journey NEW: '" + child.name + "' in " + layer + " already moves (GalaxyMotion_NEW).", existing);
                    continue;
                }

                Undo.AddComponent<GalaxyMotion_NEW>(child.gameObject);
                Debug.Log("Journey NEW: added GalaxyMotion_NEW to '" + child.name + "' in " + layer + ". It turns " +
                          "as one and runs faster in play.", child);
                added++;
            }
        }

        if (added > 0) Debug.Log("Journey NEW: save the scene to keep " + (added == 1 ? "it" : "them") + ".");
        else Debug.Log("Journey NEW: every galaxy in the open scene already has GalaxyMotion_NEW.");
    }

    [MenuItem(MenuPath, true)]
    static bool AddGalaxyMotionValidate() => !Application.isPlaying;
}

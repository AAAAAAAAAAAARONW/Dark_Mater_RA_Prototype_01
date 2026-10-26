using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Journey NEW > Solar System > Add Earth Zoom: puts EarthZoom_NEW on the open
/// scene's SolarSystem group root, so Earth grows until its air fills the screen and the
/// light goes in. Everything it needs it finds itself. Undoable (Ctrl+Z); save the scene
/// to keep it.
/// </summary>
public static class EarthZoomMenu_NEW
{
    const string MenuPath = "Tools/Journey NEW/Solar System/Add Earth Zoom";

    [MenuItem(MenuPath)]
    static void Add()
    {
        WorldSwitcher_NEW worlds = Object.FindObjectOfType<WorldSwitcher_NEW>();
        Transform group = worlds != null ? worlds.GroupRoot("SolarSystem") : null;
        if (group == null)
        {
            GameObject go = GameObject.Find("SolarSystem");
            group = go != null ? go.transform : null;
        }
        if (group == null)
        {
            Debug.LogWarning("Journey NEW: no SolarSystem group in the open scene; no Earth zoom added.");
            return;
        }

        EarthZoom_NEW zoom = group.GetComponent<EarthZoom_NEW>();
        if (zoom == null) zoom = Undo.AddComponent<EarthZoom_NEW>(group.gameObject);

        EditorSceneManager.MarkSceneDirty(group.gameObject.scene);
        Selection.activeObject = zoom;
        Debug.Log("Journey NEW: Earth zoom on '" + group.name + "'. Save the scene to keep it.", zoom);
    }

    [MenuItem(MenuPath, true)]
    static bool AddValidate() => !Application.isPlaying;
}

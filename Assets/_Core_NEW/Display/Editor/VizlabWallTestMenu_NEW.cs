using UnityEditor;
using UnityEngine;

/// <summary>
/// Puts VizlabWallTest_NEW into the open scene by hand. Scenes laid out for the wall (the
/// Vizlab row system, or a Canvas Scaler set to the full surface) get it automatically in
/// play mode and in builds, so this is for a scene that is neither but should still get the
/// wall panel, or for one where the panel's settings should live in the scene.
///
/// It works on the scene as loaded in the editor, not on the file, so it is safe to run
/// with unsaved changes open: they stay, and the addition is one more undoable change.
///
/// Where it goes: a "Display" object under the systems root, which is found as the parent
/// of LayerState_NEW's object rather than by name. The root is unnamed in some scenes.
/// With no LayerState_NEW, it goes at the scene root.
///
/// Nothing here renders or ships.
/// </summary>
static class VizlabWallTestMenu_NEW
{
    [MenuItem("GameObject/Vizlab/Add Wall Test Panel to This Scene", false, 15)]
    static void AddWallTest()
    {
        VizlabWallTest_NEW existing = Object.FindObjectOfType<VizlabWallTest_NEW>();
        if (existing != null)
        {
            Selection.activeObject = existing.gameObject;
            Debug.Log("Vizlab: this scene already has a wall test panel on '" + existing.name +
                      "'. Selected it rather than adding another.", existing);
            return;
        }

        var go = new GameObject("Display");

        LayerState_NEW state = Object.FindObjectOfType<LayerState_NEW>();
        Transform systems = state != null ? state.transform.parent : null;
        if (systems != null) go.transform.SetParent(systems, false);

        go.AddComponent<VizlabWallTest_NEW>();

        // Registered once complete, so one undo removes the object and its component together.
        Undo.RegisterCreatedObjectUndo(go, "Add Vizlab Wall Test Panel");
        Selection.activeObject = go;

        Debug.Log("Vizlab: wall test panel added" + (systems != null ? " under '" + systems.name + "'" : " at the scene root") +
                  ". Save the scene to keep it. In play mode and in a build it shows frame rate, resolution, " +
                  "aspect and displays at the top centre, with the Fit / Windowed / Native buttons.", go);
    }
}

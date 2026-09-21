using UnityEditor;
using UnityEngine;

/// <summary>
/// Adds VizlabAspectLock_NEW to the open scene with one menu item, so a build of that scene
/// renders in the wall's shape and has the Fit / Windowed / Native buttons.
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
static class VizlabAspectMenu_NEW
{
    [MenuItem("GameObject/Vizlab/Add Aspect Lock to This Scene", false, 15)]
    static void AddAspectLock()
    {
        VizlabAspectLock_NEW existing = Object.FindObjectOfType<VizlabAspectLock_NEW>();
        if (existing != null)
        {
            Selection.activeObject = existing.gameObject;
            Debug.Log("Vizlab: this scene already has an aspect lock on '" + existing.name +
                      "'. Selected it rather than adding another.", existing);
            return;
        }

        var go = new GameObject("Display");

        LayerState_NEW state = Object.FindObjectOfType<LayerState_NEW>();
        Transform systems = state != null ? state.transform.parent : null;
        if (systems != null) go.transform.SetParent(systems, false);

        go.AddComponent<VizlabAspectLock_NEW>();

        // Registered once complete, so one undo removes the object and its component together.
        Undo.RegisterCreatedObjectUndo(go, "Add Vizlab Aspect Lock");
        Selection.activeObject = go;

        Debug.Log("Vizlab: aspect lock added" + (systems != null ? " under '" + systems.name + "'" : " at the scene root") +
                  ". Save the scene to keep it. In play mode the Fit / Windowed / Native buttons sit at the top " +
                  "centre; in the editor Fit switches the Game View to 'Vizlab Surface 1:5'. A build renders in " +
                  "the wall's shape by itself.", go);
    }
}

using UnityEditor;
using UnityEngine;

/// <summary>
/// The two things needed to bring a piece of UI into the art panel, as menu items, so they
/// can be done to a scene that is open with unsaved work in it. Both change the scene as
/// loaded in the editor, not the file, and both are one undo.
///
/// Nothing here renders or ships.
/// </summary>
static class UIArtMenu_NEW
{
    /// <summary>
    /// Puts a size handle on the selected UI element: UIArtVariant_NEW with no versions, so
    /// the panel offers show and hide, the display row where there is a row anchor, and the
    /// size, with the height it comes to on the wall in millimetres. Use it on the HUD the
    /// art is meant to replace, to compare the two at the same size.
    /// </summary>
    [MenuItem("GameObject/Vizlab/Add UI Size Handle", true, 16)]
    static bool ValidateAddSizeHandle()
    {
        return Selection.activeGameObject != null
            && Selection.activeGameObject.GetComponent<RectTransform>() != null;
    }

    [MenuItem("GameObject/Vizlab/Add UI Size Handle", false, 16)]
    static void AddSizeHandle()
    {
        GameObject go = Selection.activeGameObject;

        var existing = go.GetComponent<UIArtVariant_NEW>();
        if (existing != null)
        {
            Debug.Log("Vizlab: '" + go.name + "' is already in the art panel.", existing);
            return;
        }

        Undo.AddComponent<UIArtVariant_NEW>(go);
        EditorUtility.SetDirty(go);

        Debug.Log("Vizlab: '" + go.name + "' now has a size handle. In play mode the art panel " +
                  "at the bottom left can resize it, hide it, and move it between display rows, " +
                  "and it reports the height it comes to on the wall. Add sprites to its Versions " +
                  "list if this element also has art versions to compare.", go);
    }

    // ---------------------------------------------------------------------------------

    /// <summary>
    /// The panel itself, for a scene that has art handles but nothing drawing them.
    /// It goes next to the wall test panel when there is one, so the debug tools stay together.
    /// </summary>
    [MenuItem("GameObject/Vizlab/Add Art Panel to This Scene", false, 17)]
    static void AddArtPanel()
    {
        var existing = Object.FindObjectOfType<UIArtVariantPanel_NEW>();
        if (existing != null)
        {
            Selection.activeObject = existing.gameObject;
            Debug.Log("Vizlab: this scene already has the art panel on '" + existing.name + "'.", existing);
            return;
        }

        var wallTest = Object.FindObjectOfType<VizlabWallTest_NEW>();
        GameObject host = wallTest != null ? wallTest.gameObject : new GameObject("Display");

        Undo.AddComponent<UIArtVariantPanel_NEW>(host);
        if (wallTest == null) Undo.RegisterCreatedObjectUndo(host, "Add Vizlab Art Panel");

        Selection.activeObject = host;
        Debug.Log("Vizlab: art panel added to '" + host.name + "'. It lists every element with a " +
                  "UIArtVariant_NEW in the scene; F6 hides it.", host);
    }
}

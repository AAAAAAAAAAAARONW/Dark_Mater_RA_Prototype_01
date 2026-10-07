using UnityEditor;
using UnityEngine;

/// <summary>
/// Puts the quasar the journey starts from into the open scene: a QuasarVFX_NEW where the
/// old sphere was, inside the Quasar render group, so it fades — and shrinks, on the way
/// out — with that world. The sphere is switched off, not deleted: switch it back on to
/// compare. Run it once; it leaves a quasar that is already there alone.
///
/// Placed for the way out (JourneyDiveMenu_NEW's QuasarDive). The light starts 50 in front
/// of the quasar and leaves along +Z, so the opening, facing forward, never sees it; past
/// the gate the camera swings round and finds it beyond the light, from about 28° above its
/// disc, its jets across the frame on a slant — the view the reference pictures draw.
///
/// It works on the scene as loaded in the editor, not on the file, so unsaved changes stay
/// and this is one more undoable step. Save the scene to keep it.
/// </summary>
static class JourneyQuasarMenu_NEW
{
    const string MenuPath = "Tools/Journey NEW/Add Quasar VFX to Open Scene";

    /// <summary>
    /// The disc's radius. The gas round it reaches 2.6 times as far, 52 — past the light's
    /// start, 50 out, but in sheets tipped away from it: the light starts 10 or more off each,
    /// level with where it has faded to nothing, so the opening shows none of it. Seen from
    /// past the gate, about 80 away and already shrinking as the camera comes round, the disc
    /// is about half the frame's height across.
    /// </summary>
    const float Radius = 20f;

    /// <summary>
    /// Tipped about X towards +Z, where the camera looks back from, so it sees the disc's top
    /// from about 28° above; rolled so the jets slant across the frame.
    /// </summary>
    static readonly Vector3 Tilt = new Vector3(27f, 0f, -12f);

    /// <summary>Where the quasar is if the sphere is gone: where it was, in the group.</summary>
    static readonly Vector3 FallbackPosition = new Vector3(0f, 0f, 66f);

    [MenuItem(MenuPath)]
    static void AddQuasar()
    {
        WorldSwitcher_NEW worlds = Object.FindObjectOfType<WorldSwitcher_NEW>();
        Transform group = worlds != null ? worlds.GroupRoot("Quasar") : null;
        if (group == null)
        {
            Debug.LogWarning("Journey NEW: no WorldSwitcher_NEW with a 'Quasar' group in the open scene, so no " +
                             "quasar was added. It goes in that group, to fade and shrink with it.");
            return;
        }

        QuasarVFX_NEW existing = group.GetComponentInChildren<QuasarVFX_NEW>(true);
        if (existing != null)
        {
            Debug.Log("Journey NEW: '" + existing.name + "' is already the quasar; left as it is.", existing);
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        // The sphere it replaces: the group's 'Quasar'.
        Transform sphere = group.Find("Quasar");

        var go = new GameObject("Quasar VFX");
        Undo.RegisterCreatedObjectUndo(go, "Add Quasar VFX");
        go.layer = sphere != null ? sphere.gameObject.layer : group.gameObject.layer;
        Transform t = go.transform;
        t.SetParent(group, false);
        t.localPosition = sphere != null ? sphere.localPosition : FallbackPosition;
        t.localRotation = Quaternion.Euler(Tilt);
        t.localScale = Vector3.one * Radius;
        Undo.AddComponent<QuasarVFX_NEW>(go);

        if (sphere != null && sphere.gameObject.activeSelf)
        {
            Undo.RecordObject(sphere.gameObject, "Add Quasar VFX");
            sphere.gameObject.SetActive(false);
        }

        Selection.activeGameObject = go;
        Debug.Log("Journey NEW: added 'Quasar VFX' to '" + group.name + "'" +
                  (sphere != null ? ", where the sphere was, and switched the sphere off" : "") +
                  ". It is seen when the camera looks back past the gate out of the quasar " +
                  "(Tools > Journey NEW > Reset Dive Row to Starting Values > Into Macro (leave the quasar)). " +
                  "Save the scene to keep it.", go);
    }

    [MenuItem(MenuPath, true)]
    static bool AddQuasarValidate() => !Application.isPlaying;
}

using UnityEditor;
using UnityEngine;

/// <summary>
/// Puts the Milky Way (GalaxyVFX_NEW) into the open scene's MilkyWay render group, with the Sun
/// on SolarDivePoint: the dive into the Solar System then starts from where the Sun really is,
/// on the Orion spur between the Sagittarius and Perseus arms.
///
/// It takes the galaxy's centre and the tilt of its disc from the Milky Way already there
/// ('Milky Way - Spiral Arms'), turns the new one in that plane until its Sun lies towards
/// SolarDivePoint, and sizes it so the Sun is at its real fraction of the way out (0.39) —
/// which, for the existing layout, is the same radius as before. The old one is switched
/// off, not deleted: switch it back on to compare. Without an old one it builds the plane
/// from the group and keeps SolarDivePoint where it is.
///
/// It works on the scene as loaded in the editor, so unsaved changes stay and this is one
/// more undoable step. Save the scene to keep it.
/// </summary>
static class JourneyGalaxyMenu_NEW
{
    const string MenuPath = "Tools/Journey NEW/Add Milky Way VFX to Open Scene";
    const string OldName = "Milky Way - Spiral Arms";
    const string SunName = "SolarDivePoint";

    /// <summary>Without an old galaxy to measure: its radius, and its disc tipped 25° back, as that one was.</summary>
    const float FallbackRadius = 40f;
    static readonly Vector3 FallbackTilt = new Vector3(-25f, 0f, 0f);

    [MenuItem(MenuPath)]
    static void AddGalaxy()
    {
        WorldSwitcher_NEW worlds = Object.FindObjectOfType<WorldSwitcher_NEW>();
        Transform group = worlds != null ? worlds.GroupRoot("MilkyWay") : null;
        if (group == null)
        {
            Debug.LogWarning("Journey NEW: no WorldSwitcher_NEW with a 'MilkyWay' group in the open scene, so no " +
                             "galaxy was added. It goes in that group, to fade and scale with it.");
            return;
        }

        GalaxyVFX_NEW existing = group.GetComponentInChildren<GalaxyVFX_NEW>(true);
        if (existing != null)
        {
            Debug.Log("Journey NEW: '" + existing.name + "' is already the Milky Way; left as it is.", existing);
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        Transform old = Find(group, OldName);
        Transform sun = Find(group, SunName);
        if (sun == null)
        {
            GameObject found = GameObject.Find(SunName);
            if (found != null) sun = found.transform;
        }

        // The plane: the old galaxy's, or the group's tipped back as the old one was.
        Vector3 normal = old != null ? old.up : group.rotation * Quaternion.Euler(FallbackTilt) * Vector3.up;
        float sunFraction = 0.39f;
        Vector3 centre;
        Vector3 towardsSun;
        float radius;

        if (old != null && sun != null)
        {
            centre = old.position;
            Vector3 offset = Vector3.ProjectOnPlane(sun.position - centre, normal);
            towardsSun = offset.sqrMagnitude > 1e-6f ? offset.normalized : Vector3.ProjectOnPlane(old.right, normal).normalized;
            radius = offset.sqrMagnitude > 1e-6f ? offset.magnitude / sunFraction : old.lossyScale.x * 0.5f;
        }
        else if (sun != null)
        {
            towardsSun = Vector3.ProjectOnPlane(group.right, normal).normalized;
            radius = FallbackRadius;
            centre = sun.position - towardsSun * (radius * sunFraction);
        }
        else
        {
            centre = old != null ? old.position : group.position;
            towardsSun = Vector3.ProjectOnPlane(old != null ? old.right : group.right, normal).normalized;
            radius = old != null ? old.lossyScale.x * 0.5f : FallbackRadius;
        }

        var go = new GameObject("Milky Way VFX");
        Undo.RegisterCreatedObjectUndo(go, "Add Milky Way VFX");
        go.layer = old != null ? old.gameObject.layer : group.gameObject.layer;
        Transform t = go.transform;
        t.SetParent(group, false);
        t.position = centre;
        // Local +Y the disc's normal, local +X towards the Sun.
        t.rotation = Quaternion.LookRotation(Vector3.Cross(towardsSun, normal), normal);
        float parentScale = Mathf.Max(group.lossyScale.x, 1e-6f);
        t.localScale = Vector3.one * (radius / parentScale);
        Undo.AddComponent<GalaxyVFX_NEW>(go);

        if (old != null && old.gameObject.activeSelf)
        {
            Undo.RecordObject(old.gameObject, "Add Milky Way VFX");
            old.gameObject.SetActive(false);
        }

        Selection.activeGameObject = go;
        Debug.Log(string.Format(
            "Journey NEW: added 'Milky Way VFX' to '{0}', radius {1:0.#}{2}{3}. Save the scene to keep it.",
            group.name, radius,
            sun != null ? ", with the Sun on " + SunName : ", with no " + SunName + " found to put the Sun on",
            old != null ? "; '" + OldName + "' switched off" : ""), go);
    }

    [MenuItem(MenuPath, true)]
    static bool AddGalaxyValidate() => !Application.isPlaying;

    static Transform Find(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}

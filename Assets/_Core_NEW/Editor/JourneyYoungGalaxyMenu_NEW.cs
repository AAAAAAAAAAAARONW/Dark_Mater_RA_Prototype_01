using UnityEditor;
using UnityEngine;

/// <summary>
/// Puts the young galaxy (YoungGalaxyVFX_NEW) into the open journey scene's Micro render
/// group, across the light's path: Tools > Journey NEW > Add Young Galaxy VFX to Open Scene.
///
/// WHERE. The light only goes straight on, so the galaxy is placed round its line. Its disc
/// crosses the line CrossingPastGate past the gate into the galaxy (Micro), off to the side of
/// the line by ImpactParameter of its radius (so the light passes among the clumps, not
/// through the core), and tipped TiltDegrees out of the line's plane towards the light. So the
/// light comes in over the disc among the clumps for the J4 lines (14.5 s at the layer's half
/// speed is 18 units), crosses it as J4b ends, and leaves with the galaxy behind — where the
/// player can turn and watch it go, "less than the blink of an eye".
///
/// The old galaxy art under the group (the GalaxyPrototype sprites and particles, and the
/// Galaxy2 planes) is switched off if it is on, not deleted: switch it back on to compare. The
/// galaxy-edge absorber (Absorber_GalaxyEdge, just inside the gate) stays where it is: it
/// marks the light entering the galaxy's outskirts, which is where the galaxy now begins.
///
/// It works on the scene as loaded in the editor, as one undoable step. Save the scene to
/// keep it. If the group already has a young galaxy, this selects it and changes nothing.
/// </summary>
static class JourneyYoungGalaxyMenu_NEW
{
    const string MenuPath = "Tools/Journey NEW/Add Young Galaxy VFX to Open Scene";
    const string ObjectName = "Young Galaxy VFX";

    /// <summary>The galaxy's radius, in world units: about a third of the Micro stretch.</summary>
    const float Radius = 28f;

    /// <summary>How far past the Micro gate, along the light's line, the disc's plane crosses it.</summary>
    const float CrossingPastGate = 35f;

    /// <summary>How far off the line the galaxy's centre is, of its radius: among the clumps, not through the core.</summary>
    const float ImpactParameter = 0.4f;

    /// <summary>How far the disc is tipped out of the line's plane, degrees: enough to see it as a disc on the way in.</summary>
    const float TiltDegrees = 28f;

    static readonly string[] OldArt = { "GalaxyPrototype", "Galaxy2" };

    [MenuItem(MenuPath)]
    static void AddGalaxy()
    {
        WorldSwitcher_NEW worlds = Object.FindObjectOfType<WorldSwitcher_NEW>();
        Transform group = worlds != null ? worlds.GroupRoot("Micro") : null;
        if (group == null)
        {
            Debug.LogWarning("Journey NEW: no WorldSwitcher_NEW with a 'Micro' group in the open scene, so no young " +
                             "galaxy was added. It goes in that group, to fade and scale with it.");
            return;
        }

        YoungGalaxyVFX_NEW existing = group.GetComponentInChildren<YoungGalaxyVFX_NEW>(true);
        if (existing != null)
        {
            Debug.Log("Journey NEW: '" + existing.name + "' is already the young galaxy; left as it is.", existing);
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        Vector3 origin, heading;
        if (!LightsLine(out origin, out heading))
        {
            Debug.LogWarning("Journey NEW: no gate into 'Micro', so there is no line to place the young galaxy on.");
            return;
        }

        Vector3 right = Vector3.Cross(Vector3.up, heading).normalized;
        if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
        Vector3 crossing = origin + heading * CrossingPastGate;
        Vector3 centre = crossing + right * (ImpactParameter * Radius);
        Quaternion tilt = Quaternion.AngleAxis(-TiltDegrees, right) * Quaternion.LookRotation(heading, Vector3.up);

        var go = new GameObject(ObjectName);
        Undo.RegisterCreatedObjectUndo(go, "Add young galaxy");
        go.layer = group.gameObject.layer;
        Transform t = go.transform;
        t.SetParent(group, false);
        t.position = centre;
        t.rotation = tilt;
        float parentScale = Mathf.Max(group.lossyScale.x, 1e-6f);
        t.localScale = Vector3.one * (Radius / parentScale);
        Undo.AddComponent<YoungGalaxyVFX_NEW>(go);

        int hidden = 0;
        foreach (string name in OldArt)
        {
            Transform old = Find(group, name);
            if (old == null || !old.gameObject.activeSelf) continue;
            Undo.RecordObject(old.gameObject, "Add young galaxy");
            old.gameObject.SetActive(false);
            hidden++;
        }

        Selection.activeGameObject = go;
        Debug.Log(string.Format(
            "Journey NEW: added '{0}' to '{1}', radius {2:0.#}, its disc crossing the light's line {3:0.#} past the Micro gate" +
            "{4}. Save the scene to keep it.",
            ObjectName, group.name, Radius, CrossingPastGate,
            hidden > 0 ? "; the old galaxy art (" + hidden + ") switched off" : ""), go);
    }

    [MenuItem(MenuPath, true)]
    static bool AddGalaxyValidate() => !Application.isPlaying;

    /// <summary>The light's line: the Micro gate's centre, and the way the light flies.</summary>
    static bool LightsLine(out Vector3 origin, out Vector3 heading)
    {
        origin = Vector3.zero;
        heading = Vector3.forward;

        LayerGate_NEW gate = null;
        foreach (LayerGate_NEW g in Object.FindObjectsOfType<LayerGate_NEW>())
            if (g.LayerId == "Micro") gate = g;
        if (gate == null) return false;

        Collider c = gate.GetComponent<Collider>();
        origin = c != null ? c.bounds.center : gate.transform.position;

        PlayerRig_NEW player = Object.FindObjectOfType<PlayerRig_NEW>();
        if (player != null && player.MovementDirection.sqrMagnitude > 1e-6f) heading = player.MovementDirection.normalized;
        return true;
    }

    static Transform Find(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}

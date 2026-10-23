using UnityEditor;
using UnityEngine;

/// <summary>
/// Puts the ring galaxy (RingGalaxyVFX_NEW) in place of the old first galaxy, in the open journey
/// scene's Micro render group: Tools > Journey NEW > Replace First Galaxy with Ring Galaxy.
///
/// IN PLACE. The first galaxy the light meets was Winnie's particle spiral, the Galaxy2 prefab:
/// a level disc about 46 units in radius, its middle 12.5 below the light's line and about 79
/// past the Micro gate, so the light flies in over it for J4 and J4b. The ring galaxy goes
/// exactly there — the centre of the old disc (its "Disk" child), level, the same size — and the
/// old one is switched off, not deleted: switch it back on to compare. Only the Micro group is
/// touched; the Milky Way (MilkyWayLayer) and its own legacy galaxy art are not.
///
/// If the old galaxy is not there, the ring galaxy goes where it would have been, from the Micro
/// gate and the light's line. If a ring galaxy is already in the group, it is moved rather than a
/// second one added.
///
/// It works on the scene as loaded in the editor, as one undoable step. Save the scene to keep it.
/// </summary>
static class JourneyRingGalaxyMenu_NEW
{
    const string MenuPath = "Tools/Journey NEW/Replace First Galaxy with Ring Galaxy";
    const string ObjectName = "Ring Galaxy VFX";

    /// <summary>The old first galaxy's radius, in world units: its disc's 46, and its stars' size on top.</summary>
    const float Radius = 48f;

    /// <summary>Where the old disc's middle was, if it is not in the scene to measure: past the Micro gate along the light's line, below it, to its right.</summary>
    const float CentrePastGate = 79.4f;
    const float CentreBelowLine = 12.5f;
    const float CentreRightOfLine = 0.49f;

    /// <summary>The old first galaxy's top-level objects in the Micro group (the Galaxy2 prefab and the earlier particle layout).</summary>
    static readonly string[] OldArt = { "Galaxy2", "GalaxyPrototype" };

    [MenuItem(MenuPath)]
    static void Replace()
    {
        WorldSwitcher_NEW worlds = Object.FindObjectOfType<WorldSwitcher_NEW>();
        Transform group = worlds != null ? worlds.GroupRoot("Micro") : null;
        if (group == null)
        {
            Debug.LogWarning("Journey NEW: no WorldSwitcher_NEW with a 'Micro' group in the open scene, so the first " +
                             "galaxy was not replaced. It lives in that group, to fade and scale with it.");
            return;
        }

        Undo.SetCurrentGroupName("Replace first galaxy with ring galaxy");

        // Where the old galaxy's disc is, measured off the old galaxy itself.
        Vector3 centre;
        bool measured = OldDiscCentre(group, out centre);
        Vector3 heading = Heading();
        if (!measured && !FromGate(heading, out centre))
        {
            Debug.LogWarning("Journey NEW: no old first galaxy in the Micro group and no gate into 'Micro', so there " +
                             "is nowhere to put the ring galaxy.");
            return;
        }

        RingGalaxyVFX_NEW galaxy = group.GetComponentInChildren<RingGalaxyVFX_NEW>(true);
        GameObject go;
        if (galaxy != null)
        {
            go = galaxy.gameObject;
            Undo.RecordObject(go, "Replace first galaxy");
            Undo.RecordObject(go.transform, "Replace first galaxy");
            go.name = ObjectName;
            go.SetActive(true);
        }
        else
        {
            go = new GameObject(ObjectName);
            Undo.RegisterCreatedObjectUndo(go, "Replace first galaxy");
            go.layer = group.gameObject.layer;
            go.transform.SetParent(group, false);
            Undo.AddComponent<RingGalaxyVFX_NEW>(go);
        }

        // Level, as the old disc was, the intruder (+X) off to the light's right.
        Transform t = go.transform;
        t.position = centre;
        Vector3 flat = Vector3.ProjectOnPlane(heading, Vector3.up);
        t.rotation = flat.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(flat.normalized, Vector3.up) : Quaternion.identity;
        float parentScale = Mathf.Max(group.lossyScale.x, 1e-6f);
        t.localScale = Vector3.one * (Radius / parentScale);

        // The old galaxy off, not deleted. Top-level children only, so nothing inside a prefab is overridden.
        int hidden = 0;
        foreach (Transform child in group)
        {
            if (child == t || !child.gameObject.activeSelf) continue;
            if (System.Array.IndexOf(OldArt, child.name) < 0) continue;
            Undo.RecordObject(child.gameObject, "Replace first galaxy");
            child.gameObject.SetActive(false);
            hidden++;
        }

        Selection.activeGameObject = go;
        Debug.Log(string.Format(
            "Journey NEW: '{0}' is the first galaxy now, radius {1:0}, level, centred at {2} ({3}){4}. Save the scene to keep it.",
            ObjectName, Radius, centre, measured ? "where the old disc's middle was" : "from the Micro gate",
            hidden > 0 ? "; the old galaxy (" + hidden + ") switched off" : ""), go);
    }

    [MenuItem(MenuPath, true)]
    static bool ReplaceValidate() => !Application.isPlaying;

    /// <summary>The middle of the old galaxy's disc: its "Disk" child, on the first old galaxy still switched on.</summary>
    static bool OldDiscCentre(Transform group, out Vector3 centre)
    {
        centre = Vector3.zero;
        foreach (Transform child in group)
        {
            if (!child.gameObject.activeSelf || System.Array.IndexOf(OldArt, child.name) < 0) continue;
            foreach (Transform t in child.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "Disk") continue;
                centre = t.position;
                return true;
            }
        }
        return false;
    }

    /// <summary>Where the old disc's middle would be, from the Micro gate and the light's line.</summary>
    static bool FromGate(Vector3 heading, out Vector3 centre)
    {
        centre = Vector3.zero;
        LayerGate_NEW gate = null;
        foreach (LayerGate_NEW g in Object.FindObjectsOfType<LayerGate_NEW>())
            if (g.LayerId == "Micro") gate = g;
        if (gate == null) return false;

        Collider c = gate.GetComponent<Collider>();
        Vector3 origin = c != null ? c.bounds.center : gate.transform.position;
        Vector3 right = Vector3.Cross(Vector3.up, heading).normalized;
        if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
        centre = origin + heading * CentrePastGate - Vector3.up * CentreBelowLine + right * CentreRightOfLine;
        return true;
    }

    /// <summary>The way the light flies.</summary>
    static Vector3 Heading()
    {
        PlayerRig_NEW player = Object.FindObjectOfType<PlayerRig_NEW>();
        if (player != null && player.MovementDirection.sqrMagnitude > 1e-6f) return player.MovementDirection.normalized;
        return Vector3.forward;
    }
}

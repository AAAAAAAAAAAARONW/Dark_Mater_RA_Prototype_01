using UnityEditor;
using UnityEngine;

/// <summary>
/// Puts the ring galaxy (RingGalaxyVFX_NEW) in place of the old first galaxy, in the open journey
/// scene's Micro render group: Tools > Journey NEW > Replace First Galaxy with Ring Galaxy.
///
/// The first galaxy the light meets was Winnie's particle spiral, the Galaxy2 prefab: a level
/// disc about 46 units in radius only 12.5 below the light's line, so big and so close that the
/// light skimmed it and it was never seen whole. The ring galaxy replaces it, the old one
/// switched off, not deleted (switch it back on to compare). Only the Micro group is touched; the
/// Milky Way (MilkyWayLayer) and its own legacy galaxy art are not.
///
/// SEEN WHOLE, THEN FLOWN OVER. Radius 15, centred 80 past the Micro gate along the light's line,
/// 21 below it and 4 to its right, and tipped 18° about the light's right to face it. From the
/// game camera (behind and a little above the light, looking along it) the whole galaxy sits in
/// the lower half of the screen as the light comes through the gate, an oval with the intruder's
/// streak running into its left side, as in the Hubble picture of Arp 148; it grows as the light
/// comes on and slides away under it, and the light passes over it — 16 or more above its
/// highest point, never through it — before the gate out into the cosmic web. The intruder's own
/// turn (RingGalaxyVFX_NEW.intruderTilt, -35 by default) keeps it edge on to the light.
///
/// It is placed from the Micro gate and the light's line; with no gate, from where the old disc's
/// middle was (its "Disk" child). If a ring galaxy is already in the group, it is moved rather
/// than a second one added.
///
/// It works on the scene as loaded in the editor, as one undoable step. Save the scene to keep it.
/// </summary>
static class JourneyRingGalaxyMenu_NEW
{
    const string MenuPath = "Tools/Journey NEW/Replace First Galaxy with Ring Galaxy";
    const string ObjectName = "Ring Galaxy VFX";

    /// <summary>The galaxy's radius, in world units: about a third of the old one's, to be seen whole.</summary>
    const float Radius = 15f;

    /// <summary>How far the ring is tipped up to face the light, degrees. Keep RingGalaxyVFX_NEW.intruderTilt about -(this + 17).</summary>
    const float Tilt = 18f;

    /// <summary>Where its middle is: past the Micro gate along the light's line, below it, to its right.</summary>
    const float CentrePastGate = 80f;
    const float CentreBelowLine = 21f;
    const float CentreRightOfLine = 4f;

    /// <summary>Where the old disc's middle was, the same way, for when there is no gate to measure from.</summary>
    static readonly Vector3 OldDiscFromGate = new Vector3(0.49f, -12.5f, 79.4f);

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

        // From the Micro gate and the light's line, or else from where the old galaxy's disc was.
        Vector3 heading = Heading();
        Vector3 right = Vector3.Cross(Vector3.up, heading).normalized;
        if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
        Vector3 offset = heading * CentrePastGate - Vector3.up * CentreBelowLine + right * CentreRightOfLine;
        Vector3 origin;
        bool fromGate = GateOrigin(out origin);
        if (!fromGate)
        {
            Vector3 disc;
            if (!OldDiscCentre(group, out disc))
            {
                Debug.LogWarning("Journey NEW: no gate into 'Micro' and no old first galaxy in the Micro group, so there " +
                                 "is nowhere to put the ring galaxy.");
                return;
            }
            origin = disc - (heading * OldDiscFromGate.z + Vector3.up * OldDiscFromGate.y + right * OldDiscFromGate.x);
        }
        Vector3 centre = origin + offset;

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

        // Facing along the light, the intruder (-X) off to its left, then tipped up towards it.
        Transform t = go.transform;
        t.position = centre;
        Vector3 flat = Vector3.ProjectOnPlane(heading, Vector3.up);
        Quaternion facing = flat.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(flat.normalized, Vector3.up) : Quaternion.identity;
        t.rotation = Quaternion.AngleAxis(-Tilt, facing * Vector3.right) * facing;
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
            "Journey NEW: '{0}' is the first galaxy now, radius {1:0}, tipped {5:0}° to face the light, centred at {2}, " +
            "{6:0} below the light's line ({3}){4}. Save the scene to keep it.",
            ObjectName, Radius, centre, fromGate ? "from the Micro gate" : "from where the old disc's middle was",
            hidden > 0 ? "; the old galaxy (" + hidden + ") switched off" : "", Tilt, CentreBelowLine), go);
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

    /// <summary>The middle of the gate into Micro, on the light's line.</summary>
    static bool GateOrigin(out Vector3 origin)
    {
        origin = Vector3.zero;
        LayerGate_NEW gate = null;
        foreach (LayerGate_NEW g in Object.FindObjectsOfType<LayerGate_NEW>())
            if (g.LayerId == "Micro") gate = g;
        if (gate == null) return false;

        Collider c = gate.GetComponent<Collider>();
        origin = c != null ? c.bounds.center : gate.transform.position;
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

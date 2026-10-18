using UnityEditor;
using UnityEngine;

/// <summary>
/// Scatters clouds of hydrogen gas (NebulaCloudVFX_NEW) through the second cosmic web of the
/// open journey scene: Tools > Journey NEW > Add Nebulae To Second Cosmic Web.
///
/// WHY. The second cosmic web is where the light's spectrum fills with the Lyman-alpha
/// forest — "thousands of hydrogen clouds have cut lines into your light" (J6) — and until
/// now that gas was only the web's threads. The scientists asked for hydrogen to be met as a
/// cloud, translucent and reddish-purple, the Orion Nebula the reference (LARC, 14 October).
/// So here are a few, among the threads: two on the light's path, which it flies through,
/// the rest off to the sides at different distances and sizes. They are art only: the lines
/// on the spectrum are the bake's, as before.
///
/// WHERE. Along the light's line between the gate into the second web (CosmicWeb) and the
/// gate out of it (MilkyWay), at fractions of that stretch, so lengthening the web keeps them
/// in proportion; across the line in the line's own right and up. All under one object,
/// "Nebulae", in the CosmicWeb render group, so they fade and scale with the web.
///
/// It works on the scene as loaded in the editor, as one undoable step. Save the scene to
/// keep it. If the group already has the Nebulae object, this selects it and changes nothing.
/// </summary>
static class JourneyNebulaMenu_NEW
{
    const string MenuPath = "Tools/Journey NEW/Add Nebulae To Second Cosmic Web";
    const string ParentName = "Nebulae";

    struct Cloud
    {
        public float along;    // of the stretch between the two gates
        public float right;    // across the line, world units
        public float up;
        public float radius;
        public int seed;

        public Cloud(float along, float right, float up, float radius, int seed)
        {
            this.along = along; this.right = right; this.up = up; this.radius = radius; this.seed = seed;
        }
    }

    // The two on the path first. The rest sit between the web's threads near the path, big
    // ones further off, so there is always one in view and none fills the frame for long.
    static readonly Cloud[] Clouds =
    {
        new Cloud(0.30f,   0f,   0f,  8f, 0),
        new Cloud(0.63f,   3f,  -2f,  9f, 1),
        new Cloud(0.16f,  14f,  10f,  7f, 6),
        new Cloud(0.41f, -22f,   9f, 14f, 2),
        new Cloud(0.57f,  26f,  -6f, 12f, 3),
        new Cloud(0.82f, -18f, -12f, 16f, 4),
        new Cloud(0.87f,  20f,  14f, 11f, 5),
    };

    [MenuItem(MenuPath)]
    static void AddNebulae()
    {
        WorldSwitcher_NEW worlds = Object.FindObjectOfType<WorldSwitcher_NEW>();
        Transform group = worlds != null ? worlds.GroupRoot("CosmicWeb") : null;
        if (group == null)
        {
            Debug.LogWarning("Journey NEW: no WorldSwitcher_NEW with a 'CosmicWeb' group in the open scene, so no " +
                             "nebulae were added. They go in that group, to fade and scale with the web.");
            return;
        }

        Transform existing = Find(group, ParentName);
        if (existing != null)
        {
            Debug.Log("Journey NEW: '" + ParentName + "' is already in the second cosmic web; left as it is.", existing);
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        Vector3 from, to;
        if (!GateCentre("CosmicWeb", out from) || !GateCentre("MilkyWay", out to))
        {
            Debug.LogWarning("Journey NEW: no gates into 'CosmicWeb' and 'MilkyWay', so there is no stretch to put the nebulae along.");
            return;
        }

        Vector3 heading = (to - from).normalized;
        float length = Vector3.Distance(from, to);
        Vector3 right = Vector3.Cross(Vector3.up, heading).normalized;
        if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
        Vector3 up = Vector3.Cross(heading, right).normalized;

        var parent = new GameObject(ParentName);
        Undo.RegisterCreatedObjectUndo(parent, "Add nebulae");
        parent.layer = group.gameObject.layer;
        parent.transform.SetParent(group, false);
        parent.transform.position = from;
        parent.transform.rotation = Quaternion.identity;
        float parentScale = Mathf.Max(group.lossyScale.x, 1e-6f);
        parent.transform.localScale = Vector3.one;

        for (int i = 0; i < Clouds.Length; i++)
        {
            Cloud c = Clouds[i];
            var go = new GameObject("Nebula " + (i + 1));
            go.layer = group.gameObject.layer;
            Transform t = go.transform;
            t.SetParent(parent.transform, false);
            t.position = from + heading * (c.along * length) + right * c.right + up * c.up;
            // Each streak at its own angle: the cloud's shape lies along its own x.
            t.rotation = Lie(c.seed);
            t.localScale = Vector3.one * (c.radius / parentScale);
            NebulaCloudVFX_NEW cloud = go.AddComponent<NebulaCloudVFX_NEW>();
            var so = new SerializedObject(cloud);
            so.FindProperty("seed").intValue = c.seed;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        Selection.activeGameObject = parent;
        Debug.Log(string.Format("Journey NEW: added {0} nebulae to '{1}' along the {2:0} units between the CosmicWeb and MilkyWay " +
                                "gates, two of them on the light's path. Save the scene to keep them.", Clouds.Length, group.name, length), parent);
    }

    [MenuItem(MenuPath, true)]
    static bool AddNebulaeValidate() => !Application.isPlaying;

    /// <summary>How a cloud lies, from its seed: the same for the menu and the scene as patched.</summary>
    static Quaternion Lie(int seed)
    {
        return Quaternion.Euler(40f * Mathf.Sin(seed * 1.7f), 53f * seed, 30f * Mathf.Cos(seed * 2.3f));
    }

    static bool GateCentre(string layerId, out Vector3 centre)
    {
        centre = Vector3.zero;
        foreach (LayerGate_NEW g in Object.FindObjectsOfType<LayerGate_NEW>())
        {
            if (g.LayerId != layerId) continue;
            Collider c = g.GetComponent<Collider>();
            centre = c != null ? c.bounds.center : g.transform.position;
            return true;
        }
        return false;
    }

    static Transform Find(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}

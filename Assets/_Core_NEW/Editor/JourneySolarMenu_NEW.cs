using UnityEditor;
using UnityEngine;

/// <summary>
/// Gives every planet and moon in the open scene's SolarSystem group a CelestialBody_NEW set to
/// its kind — relief and sunlight from where the Sun is for all of them; oceans, city lights,
/// clouds and a thin air for Earth; thick air for Venus, thin dusty air for Mars; flowing bands
/// and limb darkening for the giants.
///
/// Bodies are found by name. Ones that already have the component are set to their kind's look
/// again (one undoable step). The extra shells an earlier pass put under Earth — a sphere
/// drawing an atmosphere or clouds — are switched off, not deleted, since CelestialBody_NEW
/// draws both; switch them back on to compare. Saturn's ring is left alone.
///
/// It works on the scene as loaded in the editor. Save the scene to keep it.
/// </summary>
static class JourneySolarMenu_NEW
{
    const string MenuPath = "Tools/Journey NEW/Polish Solar System (open scene)";

    static readonly (string name, CelestialBody_NEW.Kind kind)[] Bodies =
    {
        ("Mercury", CelestialBody_NEW.Kind.Rocky),
        ("Venus", CelestialBody_NEW.Kind.Venus),
        ("Earth", CelestialBody_NEW.Kind.Earth),
        ("Moon", CelestialBody_NEW.Kind.Rocky),
        ("Mars", CelestialBody_NEW.Kind.Mars),
        ("Jupiter", CelestialBody_NEW.Kind.GasGiant),
        ("Saturn", CelestialBody_NEW.Kind.GasGiant),
        ("Uranus", CelestialBody_NEW.Kind.IceGiant),
        ("Neptune", CelestialBody_NEW.Kind.IceGiant),
    };

    [MenuItem(MenuPath)]
    static void Polish()
    {
        WorldSwitcher_NEW worlds = Object.FindObjectOfType<WorldSwitcher_NEW>();
        Transform group = worlds != null ? worlds.GroupRoot("SolarSystem") : null;
        if (group == null)
        {
            Debug.LogWarning("Journey NEW: no WorldSwitcher_NEW with a 'SolarSystem' group in the open scene; nothing polished.");
            return;
        }

        Undo.SetCurrentGroupName("Polish Solar System");
        int undo = Undo.GetCurrentGroup();
        var report = new System.Text.StringBuilder();
        int done = 0;

        foreach (var (name, kind) in Bodies)
        {
            Transform body = Find(group, name);
            if (body == null || body.GetComponent<MeshFilter>() == null || body.GetComponent<Renderer>() == null)
            {
                report.Append("\n  ").Append(name).Append(": not found as a sphere in the group, skipped");
                continue;
            }

            CelestialBody_NEW celestial = body.GetComponent<CelestialBody_NEW>();
            if (celestial == null) celestial = Undo.AddComponent<CelestialBody_NEW>(body.gameObject);
            Undo.RecordObject(celestial, "Polish Solar System");
            celestial.ApplyPreset(kind);
            EditorUtility.SetDirty(celestial);
            done++;
            report.Append("\n  ").Append(name).Append(": ").Append(kind);

            // An earlier pass's air and cloud shells: CelestialBody_NEW draws both now.
            foreach (Transform child in body)
            {
                Renderer r = child.GetComponent<Renderer>();
                if (r == null || r.sharedMaterial == null || r.sharedMaterial.shader == null) continue;
                string shader = r.sharedMaterial.shader.name;
                if (!shader.Contains("Atmosphere") && !shader.Contains("Clouds")) continue;
                if (!child.gameObject.activeSelf) continue;

                Undo.RecordObject(child.gameObject, "Polish Solar System");
                child.gameObject.SetActive(false);
                report.Append(", switched off its old '").Append(child.name).Append("'");
            }
        }

        Undo.CollapseUndoOperations(undo);
        Debug.Log("Journey NEW: polished " + done + " bodies in '" + group.name + "'." + report + "\nSave the scene to keep it.", group);
    }

    [MenuItem(MenuPath, true)]
    static bool PolishValidate() => !Application.isPlaying;

    static Transform Find(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}

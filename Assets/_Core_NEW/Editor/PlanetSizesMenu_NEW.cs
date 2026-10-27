using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Journey NEW > Solar System > Sizes from NASA: sets the Sun and every planet in the
/// open scene's SolarSystem group to their true sizes against each other, from NASA's
/// planetary fact sheet (equatorial diameters). Only the distances here are not true — the
/// orbits are squeezed, each at Earth's distance times its AU to the power 0.4.
///
/// THE SUN SETS THE SCALE: it is made SunAcross across, as big as fits, and everything else
/// follows it. True to Earth it would be 109 Earths across, half again wider than Earth's
/// orbit here; at 100 it sits inside Mercury's orbit (68 from it), and the light, which passes
/// 80 from its middle, goes by 30 clear of it. That makes Earth 0.92 across (it was 2.37) and
/// Jupiter 10.3 (it was 11.4).
///
/// Every body here is the same unit sphere, so its scale is its diameter. Each planet's point
/// light, whose range was its diameter, keeps to it. Rings and anything else under a planet
/// change with it.
///
/// Then the system is laid out along the light again (SolarSystemPath_NEW): the planets the
/// light passes are kept clear of it by their size. One undoable step; save the scene to keep it.
/// </summary>
public static class PlanetSizesMenu_NEW
{
    const string MenuPath = "Tools/Journey NEW/Solar System/Sizes from NASA";

    /// <summary>The Sun's diameter here, in the Solar System's units.</summary>
    const float SunAcross = 100f;

    // Equatorial diameter, km (NASA Planetary Fact Sheet; the Sun's from its Sun Fact Sheet).
    const float SunKm = 1392700f;
    static readonly (string name, float km)[] Diameters =
    {
        ("Mercury", 4879f),
        ("Venus", 12104f),
        ("Earth", 12756f),
        ("Mars", 6792f),
        ("Jupiter", 142984f),
        ("Saturn", 120536f),
        ("Uranus", 51118f),
        ("Neptune", 49528f),
    };

    [MenuItem(MenuPath)]
    static void Apply()
    {
        WorldSwitcher_NEW worlds = Object.FindObjectOfType<WorldSwitcher_NEW>();
        Transform group = worlds != null ? worlds.GroupRoot("SolarSystem") : null;
        if (group == null)
        {
            GameObject go = GameObject.Find("SolarSystem");
            group = go != null ? go.transform : null;
        }
        Transform sun = group != null ? group.Find("Sun") : null;
        if (sun == null)
        {
            Debug.LogWarning("Journey NEW: no SolarSystem group with a 'Sun' in the open scene; no sizes changed.");
            return;
        }

        Undo.SetCurrentGroupName("Sizes from NASA");
        int undo = Undo.GetCurrentGroup();

        string report = "\n  Sun: " + Resize(sun, SunAcross);
        foreach ((string name, float km) in Diameters)
        {
            Transform body = group.Find(name);
            report += "\n  " + name + ": " + (body != null ? Resize(body, SunAcross * km / SunKm) : "not found");
        }

        SolarSystemPath_NEW path = group.GetComponent<SolarSystemPath_NEW>();
        if (path != null) path.LayOutMenu();

        Undo.CollapseUndoOperations(undo);
        EditorSceneManager.MarkSceneDirty(group.gameObject.scene);

        Debug.Log("Journey NEW: the Sun and planets at their true sizes against each other, the Sun " + SunAcross + " across:" +
                  report + (path != null ? "\nLaid out along the light again." : "\nNo SolarSystemPath_NEW; not laid out again.") +
                  "\nCtrl+Z to undo. Save the scene to keep it.", group);
    }

    /// <summary>Sets <paramref name="body"/> to <paramref name="across"/>, its point lights' ranges with it; says what changed.</summary>
    static string Resize(Transform body, float across)
    {
        var touched = new List<Object> { body };
        foreach (Light l in body.GetComponentsInChildren<Light>(true)) touched.Add(l);
        Undo.RecordObjects(touched.ToArray(), "Sizes from NASA");

        float was = body.localScale.x;
        body.localScale = Vector3.one * across;

        // A planet's point light's range was its diameter; it keeps to it.
        foreach (Light l in body.GetComponentsInChildren<Light>(true))
            if (l.type == LightType.Point && was > 1e-4f) l.range *= across / was;

        return was.ToString("0.00") + " -> " + across.ToString("0.00");
    }

    [MenuItem(MenuPath, true)]
    static bool ApplyValidate() => !Application.isPlaying;
}

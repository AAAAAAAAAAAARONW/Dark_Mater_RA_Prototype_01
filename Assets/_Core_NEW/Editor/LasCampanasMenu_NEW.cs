using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Journey NEW > Las Campanas > Move the Ending to Las Campanas: in the open scene, the
/// light ends at the Magellan telescope in Chile instead of on Mount Wilson.
///   SolarSystemPath_NEW   the arrival at Las Campanas (Earth turned to it, laid out again)
///   EarthSummit_NEW       site Las Campanas, the telescope (Models/Magellan_LCO, built first if
///                         it is not there), the summit's height, La Serena for the city's glow
///   EarthZoom_NEW         the camera's stand just over the summit
///   CelestialBody_NEW     Earth's clear sky round the arrival, and its close-up night map: Chile
///                         (Earth_Night_Chile_NEW, cut from Black Marble 2016 tile B2) or the West US
/// Undoable (Ctrl+Z); save the scene to keep it. ...Back to Mount Wilson puts it all back.
/// </summary>
public static class LasCampanasMenu_NEW
{
    const string Root = "Tools/Journey NEW/Las Campanas/";
    const string PrefabPath = "Assets/_Core_NEW/Models/Magellan_LCO/Magellan_NEW.prefab";

    // The Magellan telescopes, Las Campanas Observatory, 2380 m; La Serena, on the coast to the south-west.
    static readonly Vector2 LasCampanas = new Vector2(-29.0146f, -70.6926f);
    static readonly Vector2 LaSerena = new Vector2(-29.9045f, -71.2489f);
    const float LasCampanasKm = 2.38f;

    // As they were: Mount Wilson, and downtown Los Angeles.
    static readonly Vector2 MountWilson = new Vector2(34.22f, -118.06f);
    static readonly Vector2 LosAngeles = new Vector2(34.05f, -118.24f);
    const float MountWilsonKm = 1.74f;

    // The close-up night maps and where they lie: degrees, west, south, east, north.
    const string ChileMap = "Assets/_Core_NEW/Assets/Celestial/Earth_Night_Chile_NEW.jpg";
    const string WestUSMap = "Assets/_Core_NEW/Assets/Celestial/Earth_Night_WestUS_NEW.jpg";
    static readonly Vector4 ChileRect = new Vector4(-79.225f, -37.55f, -62.158333f, -20.483333f);
    static readonly Vector4 WestUSRect = new Vector4(-126.59167f, 25.6875f, -109.525f, 42.75417f);

    [MenuItem(Root + "Move the Ending to Las Campanas")]
    static void ToLasCampanas()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            MagellanBuilder_NEW.Build();
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }
        if (prefab == null)
        {
            Debug.LogWarning("Journey NEW: no " + PrefabPath + ", and it could not be built; the ending is left where it is.");
            return;
        }
        if (Apply(EarthSummit_NEW.Site.LasCampanas, LasCampanas, LaSerena, LasCampanasKm, prefab))
            Debug.Log("Journey NEW: the ending is at Las Campanas, the Magellan telescope. Save the scene to keep it.");
    }

    [MenuItem(Root + "Back to Mount Wilson")]
    static void ToMountWilson()
    {
        if (Apply(EarthSummit_NEW.Site.MountWilson, MountWilson, LosAngeles, MountWilsonKm, null))
            Debug.Log("Journey NEW: the ending is back on Mount Wilson. Save the scene to keep it.");
    }

    [MenuItem(Root + "Move the Ending to Las Campanas", true)]
    [MenuItem(Root + "Back to Mount Wilson", true)]
    static bool CanApply() => !EditorApplication.isPlaying && Object.FindObjectOfType<EarthSummit_NEW>() != null;

    static bool Apply(EarthSummit_NEW.Site site, Vector2 arrival, Vector2 city, float summitKm, GameObject prefab)
    {
        var summit = Object.FindObjectOfType<EarthSummit_NEW>();
        var path = Object.FindObjectOfType<SolarSystemPath_NEW>();
        if (summit == null || path == null)
        {
            Debug.LogWarning("Journey NEW: the open scene has no " + (summit == null ? "EarthSummit_NEW" : "SolarSystemPath_NEW") +
                             "; nothing changed.");
            return false;
        }

        var so = new SerializedObject(summit);
        so.FindProperty("site").enumValueIndex = (int)site;
        so.FindProperty("city").vector2Value = city;
        so.FindProperty("summitKm").floatValue = summitKm;
        if (prefab != null) so.FindProperty("magellan").objectReferenceValue = prefab;
        so.ApplyModifiedProperties();

        var zoom = summit.GetComponent<EarthZoom_NEW>();
        if (zoom != null)
        {
            var z = new SerializedObject(zoom);
            z.FindProperty("summitCameraKm").floatValue = summitKm + 0.06f;
            z.ApplyModifiedProperties();
        }

        // Earth's clear sky round the arrival (CelestialBody_NEW kind Earth).
        foreach (CelestialBody_NEW body in Object.FindObjectsOfType<CelestialBody_NEW>())
        {
            var b = new SerializedObject(body);
            if (b.FindProperty("kind").enumValueIndex != (int)CelestialBody_NEW.Kind.Earth) continue;
            b.FindProperty("clearSkyAt").vector2Value = arrival;
            bool chile = site == EarthSummit_NEW.Site.LasCampanas;
            var map = AssetDatabase.LoadAssetAtPath<Texture>(chile ? ChileMap : WestUSMap);
            if (map != null)
            {
                b.FindProperty("regionMap").objectReferenceValue = map;
                b.FindProperty("regionRect").vector4Value = chile ? ChileRect : WestUSRect;
            }
            else Debug.LogWarning("Journey NEW: no " + (chile ? ChileMap : WestUSMap) + "; Earth's close-up night map is left as it is.");
            b.ApplyModifiedProperties();
        }

        // The arrival itself, and the Solar System laid out again so Earth turns it to the light.
        var p = new SerializedObject(path);
        p.FindProperty("arrivalLatitude").floatValue = arrival.x;
        p.FindProperty("arrivalLongitude").floatValue = arrival.y;
        p.ApplyModifiedProperties();
        path.LayOutMenu();

        EditorSceneManager.MarkSceneDirty(summit.gameObject.scene);
        return true;
    }
}

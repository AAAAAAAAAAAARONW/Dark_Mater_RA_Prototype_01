using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Journey NEW > Solar System > Add Asteroid Belt: puts an "Asteroid Belt" under the
/// open scene's SolarSystem group root, a particle system with AsteroidBelt_NEW on it. The
/// rocks are placed when play starts; in the editor it is an empty system. Undoable (Ctrl+Z);
/// save the scene to keep it.
/// </summary>
public static class AsteroidBeltMenu_NEW
{
    const string MenuPath = "Tools/Journey NEW/Solar System/Add Asteroid Belt";
    const string BeltName = "Asteroid Belt";

    [MenuItem(MenuPath)]
    static void Add()
    {
        WorldSwitcher_NEW worlds = Object.FindObjectOfType<WorldSwitcher_NEW>();
        Transform group = worlds != null ? worlds.GroupRoot("SolarSystem") : null;
        if (group == null)
        {
            GameObject go = GameObject.Find("SolarSystem");
            group = go != null ? go.transform : null;
        }
        if (group == null)
        {
            Debug.LogWarning("Journey NEW: no SolarSystem group in the open scene; no asteroid belt added.");
            return;
        }

        Transform existing = group.Find(BeltName);
        if (existing != null)
        {
            Selection.activeObject = existing.gameObject;
            Debug.Log("Journey NEW: '" + group.name + "' already has an asteroid belt.", existing);
            return;
        }

        var belt = new GameObject(BeltName);
        Undo.RegisterCreatedObjectUndo(belt, "Add Asteroid Belt");
        belt.layer = group.gameObject.layer;
        belt.transform.SetParent(group, false);

        Transform sun = group.Find("Sun");
        if (sun != null) belt.transform.localPosition = sun.localPosition;

        // An empty system until play: no emission, nothing on awake.
        ParticleSystem ps = belt.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = false;

        belt.AddComponent<AsteroidBelt_NEW>();

        EditorSceneManager.MarkSceneDirty(group.gameObject.scene);
        Selection.activeObject = belt;
        Debug.Log("Journey NEW: asteroid belt added under '" + group.name + "'. Save the scene to keep it.", belt);
    }

    [MenuItem(MenuPath, true)]
    static bool AddValidate() => !Application.isPlaying;
}

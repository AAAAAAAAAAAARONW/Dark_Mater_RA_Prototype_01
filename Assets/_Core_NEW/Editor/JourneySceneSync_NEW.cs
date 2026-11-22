using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Tools > Journey NEW > Copy Ending and Looks from Vizlab_Journey: makes the open journey scene
/// (PC_Journey) end and look as Vizlab_Journey does, where the work is done. For each of the
/// components below in Vizlab_Journey it finds the object at the same place in the hierarchy
/// here, adds the component if it is missing, and copies its settings over:
///   EarthSummit_NEW, EarthZoom_NEW, EarthEnding_NEW     the ending (Las Campanas or Mount Wilson)
///   SolarSystemPath_NEW, CelestialBody_NEW              the arrival, Earth's maps and clear sky
///   SunLook_NEW, TrailLook_NEW                          the Sun, the light's trail
/// Then the Solar System is laid out again along the photon's path.
///
/// WHAT IS KEPT HERE. Links to things in this scene (the photon, the HUD, the voice-over...) are
/// never copied, nor emptied: only links to assets (the Magellan prefab, the night maps) are.
/// EarthEnding_NEW's nextScenePath stays this scene's own (the PC journey goes on to the credits).
/// Save Vizlab_Journey first: what is copied is what it has on disk. Undoable; save to keep it.
/// </summary>
public static class JourneySceneSync_NEW
{
    const string MenuPath = "Tools/Journey NEW/Copy Ending and Looks from Vizlab_Journey";
    const string SourcePath = "Assets/_Core_NEW/Vizlab_Journey.unity";

    static readonly Type[] Types =
    {
        typeof(EarthSummit_NEW), typeof(EarthZoom_NEW), typeof(EarthEnding_NEW),
        typeof(SolarSystemPath_NEW), typeof(CelestialBody_NEW), typeof(SunLook_NEW), typeof(TrailLook_NEW),
    };

    // Settings each scene keeps as its own.
    static readonly Dictionary<Type, string[]> Keep = new Dictionary<Type, string[]>
    {
        { typeof(EarthEnding_NEW), new[] { "nextScenePath" } },
    };

    [MenuItem(MenuPath)]
    static void Copy()
    {
        Scene target = SceneManager.GetActiveScene();
        if (target.path == SourcePath)
        {
            Debug.LogWarning("Journey NEW: open the scene to copy into (PC_Journey) and make it the active one; this is Vizlab_Journey itself.");
            return;
        }

        Scene source = SceneManager.GetSceneByPath(SourcePath);
        bool opened = !source.IsValid() || !source.isLoaded;
        if (opened) source = EditorSceneManager.OpenScene(SourcePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(target);

        int copied = 0, added = 0;
        var missing = new List<string>();
        try
        {
            foreach (Type type in Types)
            foreach (GameObject root in source.GetRootGameObjects())
            foreach (Component from in root.GetComponentsInChildren(type, true))
            {
                string path = PathOf(from.transform);
                Transform here = Find(target, path);
                if (here == null) { missing.Add(type.Name + " on " + path); continue; }

                Component to = here.GetComponent(type);
                if (to == null) { to = Undo.AddComponent(here.gameObject, type); added++; }
                Keep.TryGetValue(type, out string[] keep);
                CopySettings(from, to, keep ?? new string[0]);
                copied++;
            }

            var layout = Object.FindObjectsOfType<SolarSystemPath_NEW>();
            foreach (SolarSystemPath_NEW p in layout)
                if (p.gameObject.scene == target) p.LayOutMenu();
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(source, true);
        }

        EditorSceneManager.MarkSceneDirty(target);
        Debug.Log("Journey NEW: copied " + copied + " components from Vizlab_Journey into " + target.name +
                  (added > 0 ? " (" + added + " added)" : "") + ". Save the scene to keep it." +
                  (missing.Count > 0 ? "\nNot found here, left out:\n  " + string.Join("\n  ", missing) : ""));
    }

    [MenuItem(MenuPath, true)]
    static bool CanCopy() => !EditorApplication.isPlaying;

    /// <summary>Every setting but links to scene objects, and the ones this scene keeps.</summary>
    static void CopySettings(Component from, Component to, string[] keep)
    {
        var src = new SerializedObject(from);
        var dst = new SerializedObject(to);
        SerializedProperty it = src.GetIterator();
        bool enter = true;
        while (it.NextVisible(enter))
        {
            enter = false;
            if (it.name == "m_Script" || Array.IndexOf(keep, it.name) >= 0) continue;
            if (!OnlyAssets(it)) continue;
            dst.CopyFromSerializedProperty(it);
        }
        dst.ApplyModifiedProperties();
    }

    /// <summary>False if the property, or anything in it, links to a scene object or to nothing.</summary>
    static bool OnlyAssets(SerializedProperty property)
    {
        SerializedProperty p = property.Copy();
        SerializedProperty end = p.GetEndProperty();
        bool enter = true;
        do
        {
            if (p.propertyType == SerializedPropertyType.ObjectReference)
            {
                Object o = p.objectReferenceValue;
                if (o == null || !EditorUtility.IsPersistent(o)) return false;
            }
            if (!p.NextVisible(enter)) break;
            enter = true;
        }
        while (!SerializedProperty.EqualContents(p, end));
        return true;
    }

    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

    static Transform Find(Scene scene, string path)
    {
        int slash = path.IndexOf('/');
        string rootName = slash < 0 ? path : path.Substring(0, slash);
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != rootName) continue;
            Transform found = slash < 0 ? root.transform : root.transform.Find(path.Substring(slash + 1));
            if (found != null) return found;
        }
        return null;
    }
}

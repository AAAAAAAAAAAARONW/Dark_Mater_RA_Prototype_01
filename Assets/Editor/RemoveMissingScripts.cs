using UnityEngine;
using UnityEditor;

/// <summary>
/// Removes missing script components from selected GameObjects (and their children)
/// so you can then save or apply prefabs. Use when you see "references a missing script".
/// </summary>
public static class RemoveMissingScripts
{
    [MenuItem("Tools/Remove Missing Scripts from Selection (including children)")]
    public static void RemoveFromSelectionRecursive()
    {
        RemoveFromSelection(includeChildren: true);
    }

    [MenuItem("Tools/Remove Missing Scripts from Selection (this object only)")]
    public static void RemoveFromSelectionOnly()
    {
        RemoveFromSelection(includeChildren: false);
    }

    static void RemoveFromSelection(bool includeChildren)
    {
        if (Selection.gameObjects.Length == 0)
        {
            Debug.LogWarning("Select one or more GameObjects in the Hierarchy (e.g. Particle_Star1 or its parent prefab).");
            return;
        }

        int totalRemoved = 0;
        foreach (GameObject go in Selection.gameObjects)
        {
            if (includeChildren)
            {
                foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                    totalRemoved += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            }
            else
            {
                totalRemoved += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            }
        }

        if (totalRemoved > 0)
        {
            Debug.Log($"Removed {totalRemoved} missing script(s). Save the scene or Apply the prefab to keep changes.");
        }
        else
        {
            Debug.Log("No missing scripts found on the selected object(s).");
        }
    }
}

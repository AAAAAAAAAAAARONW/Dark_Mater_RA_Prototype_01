using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Removes screens the PC flow used to leave behind. The credits, the cover and the journey's
/// ending built their overlays with HideFlags.DontSave, and Unity destroys such an object
/// neither when the scene changes nor when Play stops: an old run's end card stayed over the
/// next run, and over the scene in edit mode. They are built without it now; this clears any
/// left from before, when scripts reload and whenever Play starts or stops. Only root objects
/// with these names that carry DontSave and belong to no asset are touched.
/// </summary>
[InitializeOnLoad]
static class ClearRuntimeLeftovers_NEW
{
    static readonly HashSet<string> Names = new HashSet<string> { "Credits overlay", "Credits sky", "Ending fade" };

    static ClearRuntimeLeftovers_NEW()
    {
        EditorApplication.delayCall += Clear;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.ExitingEditMode) Clear();
        };
    }

    [MenuItem("Tools/Journey NEW/Clear Leftover Credits Screens")]
    static void Clear()
    {
        int n = 0;
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go == null || go.transform.parent != null || !Names.Contains(go.name)) continue;
            if ((go.hideFlags & HideFlags.DontSaveInEditor) == 0 || EditorUtility.IsPersistent(go)) continue;
            Object.DestroyImmediate(go);
            n++;
        }
        if (n > 0) Debug.Log("[ClearRuntimeLeftovers_NEW] Removed " + n + " screen(s) left over from an earlier run.");
    }
}

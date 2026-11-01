using UnityEditor;
using UnityEngine;

/// <summary>
/// Puts the Game view in the wall's shape whenever Play starts in a wall scene (one with a
/// GetReal3DView_NEW: Vizlab_tut, Vizlab_Journey): 'Vizlab Surface 1:5', 1920 x 1512, the
/// surface's 1.27 : 1. Before Play, so the first frame is already that shape and the HUD,
/// which is laid out against the unrolled wall, comes out as on site.
///
/// VizlabWallTest_NEW only reports the Game view's shape in the editor; it does not change
/// it. PC scenes have no getReal3D view and are left alone.
/// </summary>
[InitializeOnLoad]
static class VizlabWallGameView_NEW
{
    static VizlabWallGameView_NEW()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.ExitingEditMode) return;
        if (Object.FindObjectOfType<GetReal3DView_NEW>() == null) return;

        string error;
        if (!VizlabWallTest_NEW.SelectWallGameView(out error))
            Debug.LogWarning("Vizlab: could not put the Game view in the wall's shape (" + error + "). Pick " +
                             "'Vizlab Surface 1:5' in the Game view's size menu by hand.");
    }
}

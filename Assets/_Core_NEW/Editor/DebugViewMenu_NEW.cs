using UnityEditor;
using UnityEngine;

/// <summary>
/// Menu and persistence for DebugView_NEW.
///
/// The runtime class is plain statics, which reset on every domain reload. This restores
/// them from EditorPrefs on load and writes them back when toggled, so a choice survives
/// entering play mode, recompiling and restarting the editor.
/// </summary>
[InitializeOnLoad]
public static class DebugViewMenu_NEW
{
    const string Root = "Tools/Journey NEW/Debug View/";
    const string GizmosItem = Root + "Gizmos";
    const string OverlayItem = Root + "Overlay";
    const string VerboseItem = Root + "Verbose Logging";

    const string GizmosPref = "JourneyNEW.DebugView.Gizmos";
    const string OverlayPref = "JourneyNEW.DebugView.Overlay";
    const string VerbosePref = "JourneyNEW.DebugView.Verbose";

    static DebugViewMenu_NEW() => Restore();

    [InitializeOnLoadMethod]
    static void Restore()
    {
        DebugView_NEW.Gizmos = EditorPrefs.GetBool(GizmosPref, true);
        DebugView_NEW.Overlay = EditorPrefs.GetBool(OverlayPref, true);
        DebugView_NEW.Verbose = EditorPrefs.GetBool(VerbosePref, false);
    }

    static void Set(string pref, bool value)
    {
        EditorPrefs.SetBool(pref, value);
        Restore();
        SceneView.RepaintAll();
    }

    [MenuItem(GizmosItem)]
    static void ToggleGizmos() => Set(GizmosPref, !DebugView_NEW.Gizmos);

    [MenuItem(GizmosItem, true)]
    static bool ValidateGizmos()
    {
        Menu.SetChecked(GizmosItem, DebugView_NEW.Gizmos);
        return true;
    }

    [MenuItem(OverlayItem)]
    static void ToggleOverlay() => Set(OverlayPref, !DebugView_NEW.Overlay);

    [MenuItem(OverlayItem, true)]
    static bool ValidateOverlay()
    {
        Menu.SetChecked(OverlayItem, DebugView_NEW.Overlay);
        return true;
    }

    [MenuItem(VerboseItem)]
    static void ToggleVerbose() => Set(VerbosePref, !DebugView_NEW.Verbose);

    [MenuItem(VerboseItem, true)]
    static bool ValidateVerbose()
    {
        Menu.SetChecked(VerboseItem, DebugView_NEW.Verbose);
        return true;
    }

    [MenuItem(Root + "All Off")]
    static void AllOff()
    {
        EditorPrefs.SetBool(GizmosPref, false);
        EditorPrefs.SetBool(OverlayPref, false);
        EditorPrefs.SetBool(VerbosePref, false);
        Restore();
        SceneView.RepaintAll();
    }
}

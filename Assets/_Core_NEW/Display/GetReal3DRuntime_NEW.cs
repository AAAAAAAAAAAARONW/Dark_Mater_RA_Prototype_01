using UnityEngine;

/// <summary>
/// The one answer to "is this copy of the game running on the Vizlab wall under getReal3D?",
/// and the few things that change at startup when it is.
///
/// THE RULE THIS HOLDS FOR THE WHOLE PROJECT. A PC run - the editor, a desk build, a
/// playtest - must behave exactly as it did before getReal3D was added. So everything
/// wall-only hangs off one of two tests, and nothing else:
///
///   LaunchedByGetReal3D   the getReal3D launcher started this process. Process-wide,
///                         known before the first scene. Use it for anything about the
///                         window, files, logs and startup.
///   GetReal3DView_NEW     a getReal3D view is live in the open scene (Vizlab_tut,
///                         Vizlab_Journey). Use it for camera and input: it is what
///                         InputScheme_NEW and the HUD key off.
///
/// Neither is ever true in a PC scene or a desk build, so code behind them cannot reach a
/// PC run. GETREAL3D_GUIDE.md beside this file lists every place that uses them.
///
/// LaunchedByGetReal3D is the launcher's GETREAL_CONFIG environment variable, the same
/// test getReal3D's own VRToolkitChoice uses. Not getReal3D.Cluster.isOn, which is a bare
/// `return true` in getReal3D.dll 4.1.0.4003, and not Plugin.getNodeCount(), which counts
/// machines: the Vizlab's four copies share one machine (one per GPU) and it says 1.
/// </summary>
public static class GetReal3DRuntime_NEW
{
    /// <summary>True when the getReal3D launcher started this process. Never true at a desk.</summary>
    public static readonly bool LaunchedByGetReal3D =
        System.Environment.GetEnvironmentVariable("GETREAL_CONFIG") != null;

    /// <summary>
    /// This copy is the cluster's master - the one that owns the keyboard, the mouse and the
    /// sound. True at a desk, where there is only one copy.
    /// </summary>
    public static bool IsMaster
    {
        get
        {
            if (!LaunchedByGetReal3D) return true;
            try { return getReal3D.Cluster.isMaster; }
            catch (System.Exception) { return true; }
        }
    }

    /// <summary>
    /// Empty at a desk. On the wall, "_master_1234" or "_node_5678" (the process id), to put
    /// in the name of any file this copy writes: the four copies share one machine, one
    /// Desktop and one persistentDataPath, and a shared file name means they write over one
    /// another, or wait on one another's locks.
    /// </summary>
    public static string InstanceTag
    {
        get
        {
            if (!LaunchedByGetReal3D) return "";
            return (IsMaster ? "_master_" : "_node_") + System.Diagnostics.Process.GetCurrentProcess().Id;
        }
    }

    /// <summary>
    /// Before the first scene, on the wall only: the debug readouts start hidden. Each copy
    /// draws its own IMGUI into its own window, so a readout that is on by default appears
    /// once per window across the wall. F1 on the master's keyboard still shows them (on the
    /// master's part of the wall only, which is what an attendant wants).
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void OnLaunch()
    {
        if (!LaunchedByGetReal3D) return;

        DebugView_NEW.Overlay = false;

        Debug.Log("[GetReal3DRuntime_NEW] launched by getReal3D (GETREAL_CONFIG=" +
                  System.Environment.GetEnvironmentVariable("GETREAL_CONFIG") + "), " +
                  (IsMaster ? "master" : "node") + ", pid " + System.Diagnostics.Process.GetCurrentProcess().Id +
                  ", " + Time.realtimeSinceStartup.ToString("0.00") + " s after process start. " +
                  "Debug readouts start hidden; the window is getReal3D's to place.");
    }
}

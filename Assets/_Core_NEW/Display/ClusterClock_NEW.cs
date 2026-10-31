using UnityEngine;

/// <summary>
/// Makes every copy on the wall step its game time by the master's frame time, so that
/// Time.time and Time.deltaTime - which the whole piece moves by - are the same number on
/// every copy. OFF unless asked for: add -cluster-clock to the game's command line in the
/// getReal3D launcher.
///
/// WHY IT IS A SWITCH, NOT A FIX. getReal3D's guide says it synchronises "most Unity
/// components automatically", and its config has interceptors that suggest it shares the
/// clock itself; but it also offers Cluster.time / Cluster.deltaTime as "Time from the master
/// instance", which it would not need to if Time were always shared. Which is true on the
/// Vizlab is for ClusterSyncProbe_NEW to say (Compare Cluster Probe Logs: "Unity's Time
/// differs between copies while getReal3D's agrees"). If it says so, turn this on; nothing
/// has to be rebuilt.
///
/// HOW. Time.captureDeltaTime: Unity then advances game time by exactly that much each frame,
/// whatever the real time was. Set from Cluster.deltaTime - the master's - every frame, on
/// every copy, so all of them advance by the same amount. It lags the master by a frame,
/// which is the same frame on every copy and so invisible. Real time
/// (realtimeSinceStartup) is untouched.
///
/// It never runs at a desk: it needs the getReal3D launcher as well as the flag.
/// </summary>
[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
public class ClusterClock_NEW : MonoBehaviour
{
    const string CommandLineFlag = "-cluster-clock";

    /// <summary>A frame longer than this (a hitch, a load) is clamped, as Unity clamps its own.</summary>
    const float MaxStep = 1f / 10f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoInstall()
    {
        if (!GetReal3DRuntime_NEW.LaunchedByGetReal3D || !HasFlag()) return;

        var go = new GameObject("Cluster Clock (auto)");
        DontDestroyOnLoad(go);
        go.AddComponent<ClusterClock_NEW>();
        Debug.Log("[ClusterClock_NEW] " + CommandLineFlag + ": game time steps by the master's frame time on every copy.");
    }

    static bool HasFlag()
    {
        foreach (string arg in System.Environment.GetCommandLineArgs())
            if (string.Equals(arg, CommandLineFlag, System.StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    void Update()
    {
        float dt;
        try { dt = (float)getReal3D.Cluster.deltaTime; }
        catch (System.Exception) { return; }

        if (dt > 0f) Time.captureDeltaTime = Mathf.Min(dt, MaxStep);
    }

    void OnDisable()
    {
        Time.captureDeltaTime = 0f;
    }
}

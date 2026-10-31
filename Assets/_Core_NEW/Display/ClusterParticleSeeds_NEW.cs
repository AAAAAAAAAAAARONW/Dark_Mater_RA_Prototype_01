using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// On the wall, gives every particle system that would pick its own random seed a fixed one
/// instead, the same on every copy, so a galaxy's stars or a cloud's dust are laid out the
/// same in every window and meet at the seams.
///
/// WHY. "Auto Random Seed" (on for 75 systems in the Vizlab scenes) draws a new seed each
/// time a system starts, from Unity's own generator rather than the UnityEngine.Random that
/// getReal3D seeds the same everywhere. Three copies, three layouts. GalaxyMotion_NEW also
/// measures its spin direction from its particles, so a galaxy could even turn opposite
/// ways on neighbouring rows.
///
/// HOW. As each scene loads - before its first frame is drawn - every system in it with
/// Auto Random Seed on, switched on or not, is stopped and cleared, given a seed hashed from
/// the scene's name and its place in the hierarchy, and restarted if it was playing. The
/// hash is the same on every copy because the scene is.
///
/// Installs itself only when the getReal3D launcher started the process; at a desk
/// particles keep their random seeds, as before.
/// </summary>
public static class ClusterParticleSeeds_NEW
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if (!GetReal3DRuntime_NEW.LaunchedByGetReal3D) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        int seeded = 0;
        foreach (ParticleSystem ps in Resources.FindObjectsOfTypeAll<ParticleSystem>())
        {
            if (ps == null || ps.gameObject.scene != scene || !ps.useAutoRandomSeed) continue;

            bool wasPlaying = ps.isPlaying;
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.useAutoRandomSeed = false;
            ps.randomSeed = Seed(scene.name, ps.transform);
            if (wasPlaying) ps.Play(false);
            seeded++;
        }

        if (seeded > 0)
            Debug.Log("[ClusterParticleSeeds_NEW] " + scene.name + ": " + seeded + " particle systems given fixed seeds, the same on every copy.");
    }

    /// <summary>FNV-1a of the scene name and the path of sibling indices and names to the system.</summary>
    static uint Seed(string sceneName, Transform t)
    {
        uint h = 2166136261;
        Mix(ref h, sceneName);
        for (; t != null; t = t.parent)
        {
            Mix(ref h, t.name);
            Mix(ref h, t.GetSiblingIndex().ToString());
        }
        return h == 0 ? 1u : h;
    }

    static void Mix(ref uint h, string s)
    {
        for (int i = 0; i < s.Length; i++) { h ^= s[i]; h *= 16777619; }
        h ^= '/'; h *= 16777619;
    }
}

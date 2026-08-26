using UnityEngine;

/// <summary>
/// Caches the scene's DarkMatter components so the player does not call
/// FindObjectsOfType every frame.
///
/// The original DarkMatterPlayerControllerTest ran FindObjectsOfType&lt;DarkMatter&gt;()
/// inside Update — one of the most expensive calls in Unity, on the hot path, once per
/// frame. Its cost scales with the total object count in the scene, not with how many
/// DarkMatter components exist, so it is paid in full even in PlaytestBuild where the
/// answer is an empty array.
///
/// The result is identical: the same components, and deflection is summed, so ordering
/// does not matter. Call Rebuild() if dark matter is ever spawned at runtime.
///
/// This is a cache rather than self-registration on DarkMatter because DarkMatter.cs is
/// shared with eleven other scenes and is deliberately left untouched.
/// </summary>
public static class DarkMatterRegistry_NEW
{
    static DarkMatter_NEW[] _cached;
    static bool _built;

    /// <summary>Every DarkMatter in the loaded scene. Never null.</summary>
    public static DarkMatter_NEW[] All
    {
        get
        {
            if (!_built) Rebuild();
            return _cached;
        }
    }

    /// <summary>Re-scan. Call after spawning or destroying DarkMatter at runtime.</summary>
    public static void Rebuild()
    {
        _cached = Object.FindObjectsOfType<DarkMatter_NEW>() ?? new DarkMatter_NEW[0];
        _built = true;
    }

    /// <summary>Drop the cache so the next access re-scans. Used on scene teardown.</summary>
    public static void Invalidate()
    {
        _cached = null;
        _built = false;
    }
}

using UnityEngine;

/// <summary>
/// The tutorial's world clock: real time that stops when the piece is paused.
///
/// The tutorial has always run on Time.unscaledDeltaTime, deliberately, so that D2's
/// slow motion slows the particles and the spectrum without stopping the light, the
/// atoms, or the beat clocks. That choice has a consequence the moment the piece can be
/// paused: nothing on unscaled time notices a pause at all. Drop the time scale to zero
/// and the light flies on, atoms keep arriving, and — worst — beats keep ticking, so a
/// player pressing A while paused would fire C2's emission behind the pause screen.
///
/// So there are now two kinds of clock in the tutorial, and every Update is on one of
/// them:
///
///   WORLD — this clock. Ignores slow motion, stops on pause. The light's travel, the
///           atoms, the beat clocks and gates, the emission ramps, the guide motes, the
///           camera shake, the spectrum bar's slide and the opening fade.
///
///   INTERFACE — Time.unscaledDeltaTime, as before. Ignores both. The camera and zoom
///           (GDD §5: the camera is never locked, and that includes a paused frame), the
///           HUD and its hint line, the map, the attract card, the slow-motion ramp
///           itself, and full-screen flashes — which would otherwise hold a white frame
///           over the words telling the player how to resume.
///
/// The rule for anything new: if it is something happening IN the universe, read this;
/// if it is the player's view of it or an instruction to them, read unscaled time.
///
/// Static, like DebugView_NEW, because it is read from a dozen places in the middle of
/// their Updates and a reference to a component in each would be a dozen ways to be
/// null. Only TutorialPause_NEW sets it.
/// </summary>
public static class TutorialClock_NEW
{
    static bool _paused;
    static float _pausedTotal;
    static float _pauseStartedAt;

    /// <summary>True while the piece is paused.</summary>
    public static bool Paused { get { return _paused; } }

    /// <summary>World seconds this frame. Real seconds, or zero while paused.</summary>
    public static float DeltaTime
    {
        get { return _paused ? 0f : UnityEngine.Time.unscaledDeltaTime; }
    }

    /// <summary>
    /// World seconds since startup, not counting time spent paused.
    ///
    /// For anything that animates from an absolute time — a sine bob, a noise walk —
    /// rather than by accumulating deltas. Without it a paused mote would keep breathing
    /// and a paused camera would keep shaking.
    /// </summary>
    public static float Time
    {
        get
        {
            float now = UnityEngine.Time.unscaledTime;
            float t = now - _pausedTotal;

            if (_paused) t -= now - _pauseStartedAt;

            return t;
        }
    }

    /// <summary>Start or end a pause. TutorialPause_NEW is the only caller.</summary>
    internal static void SetPaused(bool paused)
    {
        if (paused == _paused) return;

        float now = UnityEngine.Time.unscaledTime;

        if (paused) _pauseStartedAt = now;
        else _pausedTotal += now - _pauseStartedAt;

        _paused = paused;
    }

    /// <summary>
    /// Statics survive a Play mode that skips the domain reload, and a piece that was
    /// paused when Play stopped must not start the next session already paused.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        _paused = false;
        _pausedTotal = 0f;
        _pauseStartedAt = 0f;
    }
}

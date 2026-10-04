using UnityEngine;

/// <summary>
/// Where the tutorial's light got to, carried across the scene load so the journey's bar
/// opens on exactly the frame the tutorial's bar closed on.
///
/// The tutorial plays the first stretch of the real journey (time-compressed). It writes
/// its position here every frame; the journey's BakedSpectrumSource_NEW reads it once on
/// activation and then HOLDS the spectrum there until the tracker catches up, so nothing
/// on the bar jumps across the black.
///
/// Consumed on read: a journey started on its own, or started a second time, sees nothing
/// and behaves exactly as before.
/// </summary>
public static class SpectrumHandoff_NEW
{
    /// <summary>Lookback (Gyr) the tutorial's light reached. Negative = no handoff.</summary>
    public static float EndLookbackGyr = -1f;

    /// <summary>Redshift of the first line the player's own atom cut. NaN = none.</summary>
    public static float FirstLineZ = float.NaN;

    public static bool HasHandoff { get { return EndLookbackGyr >= 0f; } }

    public static void Clear()
    {
        EndLookbackGyr = -1f;
        FirstLineZ = float.NaN;
    }

    // Statics survive a play session when domain reload is off (Enter Play Mode Options).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Clear();
    }
}

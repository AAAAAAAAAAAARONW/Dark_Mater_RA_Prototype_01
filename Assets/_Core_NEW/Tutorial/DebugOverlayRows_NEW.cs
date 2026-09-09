using UnityEngine;

/// <summary>
/// Where each line of the tutorial's debug readout goes. One list, one authority.
///
/// The overlay is drawn by six components, each with its own OnGUI, because each one
/// knows something the others do not — the director knows the beat, the rig knows
/// whether it saw the confirm press, the spectrum knows how many lines are cut. That is
/// the right split. What was wrong was that each of them also chose its own Y
/// coordinate, inline, as a literal.
///
/// Two of them chose 70. TutorialTravel_NEW got there first and TutorialSpectrum_NEW
/// picked the same number months later, so the two lines drew on top of each other and
/// neither was readable. Nothing catches that: it is not a compile error, it is not a
/// warning, and it only shows up as a smear on screen that reads as a rendering bug
/// rather than as two labels in one place.
///
/// So the coordinates live here instead. Adding a line means adding a constant to this
/// list, where a collision is a duplicate value sitting next to its twin and is visible
/// at a glance. Nothing inlines a rectangle any more.
///
/// The order is the order the piece runs in: what the storyboard is doing, then the
/// controls, then the world, then Phase 3's own state. A reader scanning down the
/// screen is reading roughly outwards from the director.
/// </summary>
public static class DebugOverlayRows_NEW
{
    /// <summary>Left margin, in pixels.</summary>
    public const float X = 10f;

    /// <summary>First row's top edge.</summary>
    public const float Top = 10f;

    /// <summary>Distance between rows. A couple of pixels more than the line height.</summary>
    public const float Step = 20f;

    public const float Width = 900f;
    public const float Height = 22f;

    // ── The rows ─────────────────────────────────────────────────────────────
    // One entry per line that can be on screen at once. Keep them unique, and keep
    // them in the order they should read down the screen.

    /// <summary>TutorialDirector_NEW — current beat, index, mode, gate status.</summary>
    public const int DirectorState = 0;

    /// <summary>TutorialDirector_NEW — the debug keys and the idle timer.</summary>
    public const int DirectorKeys = 1;

    /// <summary>FirstPersonLookRig_NEW — recentring, off-axis angle, last A press.</summary>
    public const int Camera = 2;

    /// <summary>TutorialTravel_NEW — heading and speed.</summary>
    public const int Travel = 3;

    /// <summary>TutorialSpectrum_NEW — bar visibility, lines cut, profile in use.</summary>
    public const int Spectrum = 4;

    /// <summary>TutorialAtom_NEW — closing or hit, distance, progress.</summary>
    public const int Atom = 5;

    /// <summary>TutorialSlowMotion_NEW — current and target time scale.</summary>
    public const int Time = 6;

    /// <summary>TutorialAtomCluster_NEW — how many of D4's group are away.</summary>
    public const int Cluster = 7;

    /// <summary>TutorialDirector_NEW — which pad buttons are down right now.</summary>
    public const int Buttons = 8;

    /// <summary>The rectangle for one row. Use this rather than writing a Rect inline.</summary>
    public static Rect Row(int index)
    {
        return new Rect(X, Top + index * Step, Width, Height);
    }
}

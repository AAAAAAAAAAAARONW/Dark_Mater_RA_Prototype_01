/// <summary>
/// One switch for every debug visual in the project.
///
/// Gizmos, the on-screen readout and verbose logging used to be three unrelated booleans
/// scattered across components, each defaulting to on. The result was a Scene view buried
/// under 138 gizmo calls per repaint and a Console that scrolled away anything real.
///
/// Everything debug-facing now asks here first. In the editor the flags are backed by
/// EditorPrefs and driven from Tools > Journey NEW > Debug View; while playing,
/// LayerDebugJump_NEW can drive them so the debug tool and its visuals turn on together.
///
/// Worth knowing about gizmos in general: OnDrawGizmos only ever runs in the Scene view,
/// or in the Game view when its own Gizmos toggle is on, and never in a build. Turning
/// Gizmos on here does not make anything appear in a shipped game.
///
/// Plain statics with no Unity dependency, so this compiles into the runtime assembly and
/// costs three bytes.
/// </summary>
public static class DebugView_NEW
{
    /// <summary>Scene-view annotations: gate volumes, camera aim, influence radii.</summary>
    public static bool Gizmos = true;

    /// <summary>The on-screen readout drawn by LayerDebugJump_NEW.</summary>
    public static bool Overlay = true;

    /// <summary>Per-transition logging from the responders. Off by default; it is loud.</summary>
    public static bool Verbose = false;
}

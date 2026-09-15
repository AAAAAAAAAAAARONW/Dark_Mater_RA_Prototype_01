using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// B pauses the piece and B resumes it, at any point once the tutorial is running.
///
/// GLOBAL, NOT A D5 BEHAVIOUR. D5 is where pause is taught, but GDD §4's rule is one
/// button, one meaning, everywhere — and FirstPersonLookRig_NEW records the failure a
/// beat-shaped control produces: A that only recentred during B4 did nothing when
/// pressed at any other moment, and was reported as broken. A pause that only worked in
/// D5 would fail the same way the first time a visitor pressed it during D2.
///
/// What a pause does, and does not:
///
///   Stops the world clock (TutorialClock_NEW) — the light, the atoms, the beat clocks
///       and gates, the emission, the motes.
///   Stops the time scale, through TutorialSlowMotion_NEW, which owns it — particles,
///       the trail's fade, the spectrum drift.
///   Leaves the camera and zoom live. GDD §5: the camera is never locked, and a paused
///       frame is exactly the moment someone wants to look around.
///   Puts B TO RESUME on the hint line, through TutorialHUD_NEW, for as long as it
///       lasts. A pause with no visible way out reads as a crash, and on a display in a
///       public gallery that is the moment a visitor walks away.
///
/// Not available on the attract card. Nothing is running there to pause, and B on a
/// title card that says PRESS A TO BEGIN would only show an instruction for a state
/// the visitor never meant to be in.
///
/// Bound to B on Xbox, Triangle on PlayStation and P on the keyboard — see
/// TutorialInput_NEW.PauseButton.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("PAUSE", "#33B3B3")]
public class TutorialPause_NEW : MonoBehaviour
{
    [Header("Wiring")]
    [Tooltip("Leave empty to find it. Pause is unavailable while it is on the attract card.")]
    [SerializeField] TutorialDirector_NEW director;

    [Tooltip("Leave empty to find it. Owns Time.timeScale; the pause borrows it and hands " +
             "back whatever it was doing, so pausing inside D2's slow motion resumes into " +
             "slow motion rather than out of it.")]
    [SerializeField] TutorialSlowMotion_NEW slowMotion;

    [Header("Words")]
    [Tooltip("The hint line while paused. Replaces whatever the current beat is saying, " +
             "and hands the line back on resume.")]
    [SerializeField] string resumeHintText = "B  TO  RESUME";

    [Tooltip("Let the builder keep this wording in step with the control scheme. Untick " +
             "to write your own and have it left alone.")]
    [SerializeField] bool builderOwnsCopy = true;

    [Header("Events")]
    [SerializeField] UnityEvent onPaused = new UnityEvent();
    [SerializeField] UnityEvent onResumed = new UnityEvent();

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    int _resumeCount;

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>True while the piece is paused.</summary>
    public bool IsPaused { get { return TutorialClock_NEW.Paused; } }

    /// <summary>The hint line while paused. Read by TutorialHUD_NEW.</summary>
    public string ResumeHintText { get { return resumeHintText; } }

    /// <summary>
    /// How many times the piece has been resumed. D5 reads it.
    ///
    /// A count rather than an event, because D5 cannot watch the pause happen: beats do
    /// not tick while the world is paused. It notes the count when it opens and is
    /// satisfied once the count has moved — which also means a pause taken just before
    /// D5 opened cannot satisfy it.
    /// </summary>
    public int ResumeCount { get { return _resumeCount; } }

    /// <summary>Whether Build or Update may rewrite the wording.</summary>
    public bool BuilderOwnsCopy { get { return builderOwnsCopy; } }

    public void Pause()
    {
        if (IsPaused || !CanPause()) return;

        TutorialClock_NEW.SetPaused(true);
        if (slowMotion != null) slowMotion.Hold();

        onPaused.Invoke();

        if (debugLog) Debug.Log("[TutorialPause_NEW] Paused.", this);
    }

    public void Resume()
    {
        if (!IsPaused) return;

        TutorialClock_NEW.SetPaused(false);
        if (slowMotion != null) slowMotion.Release();

        _resumeCount++;
        onResumed.Invoke();

        if (debugLog) Debug.Log("[TutorialPause_NEW] Resumed.", this);
    }

    /// <summary>
    /// Out of any pause immediately. The attract reset calls this; so does teardown.
    /// Does not count as a resume — nobody pressed anything.
    /// </summary>
    public void ResumeNow()
    {
        if (!IsPaused) return;

        TutorialClock_NEW.SetPaused(false);
        if (slowMotion != null) slowMotion.Release();
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (director == null) director = FindObjectOfType<TutorialDirector_NEW>();
        if (slowMotion == null) slowMotion = FindObjectOfType<TutorialSlowMotion_NEW>();
    }

    void Update()
    {
        // Leaving attract by any route — including the idle timeout while paused — must
        // not leave the clock stopped behind the title card.
        if (IsPaused && !CanPause())
        {
            ResumeNow();
            return;
        }

        if (!TutorialInput_NEW.PauseDown()) return;

        if (IsPaused) Resume();
        else Pause();
    }

    void OnDisable()
    {
        ResumeNow();
    }

    bool CanPause()
    {
        return director == null || director.CurrentState != TutorialDirector_NEW.State.Attract;
    }
}

using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// B pauses the journey and B resumes it. P on the keyboard.
///
/// WHY THE JOURNEY HAS ONE. GDD 4: B is pause, "introduced in the tutorial, live everywhere
/// after that". Until this existed only TutorialPause_NEW read InputScheme_NEW.PauseDown, so
/// the button the tutorial had just taught did nothing once the journey began - which is
/// the "one button, one meaning, everywhere" failure the scheme exists to prevent.
///
/// WHAT A PAUSE DOES:
///
///   Stops the time scale. The rig's flight, the sequences (JourneySequence_NEW runs on
///       scaled time on purpose, so a pause holds it), the VFX and the layer pacing all
///       move by Time.deltaTime and so all stop.
///   Pauses all audio (AudioListener.pause). The voice-over counts its own time on
///       Time.deltaTime, so pausing the sound with the clock keeps the two in step.
///   Puts "B  TO  RESUME" on the journey's hint line, and gives the line back as it was on
///       resume - the dark matter moment's "Y" prompt included.
///
/// WHAT IT DOES NOT: the view is held with the rest of the world. Unlike the tutorial,
/// whose look rig runs on unscaled time, the journey's camera is driven by Cinemachine on
/// scaled time, so it is a still frame until B is pressed again.
///
/// On the wall the press comes through getReal3D.Input (InputScheme_NEW.PauseDown), so every
/// copy pauses on the same frame.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("PAUSE", "#33B3B3")]
public class JourneyPause_NEW : MonoBehaviour
{
    [Tooltip("Leave empty to find it. The line that says how to resume.")]
    [SerializeField] JourneyHint_NEW hint;

    [Tooltip("The hint while paused. {0} is the letter printed on the pad for pause.")]
    [SerializeField] string resumeHintFormat = "{0}  TO  RESUME";

    [Tooltip("Pause every AudioSource with the clock, so the voice-over stays in step.")]
    [SerializeField] bool pauseAudio = true;

    [Header("Events")]
    [SerializeField] UnityEvent onPaused = new UnityEvent();
    [SerializeField] UnityEvent onResumed = new UnityEvent();

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    float _timeScaleBefore = 1f;
    bool _hintWasShown;
    string _hintTextBefore;

    static JourneyPause_NEW _paused;

    /// <summary>
    /// True while the journey is paused. Controls that would otherwise act behind the pause
    /// (the dark matter Y) check this and stand still.
    /// </summary>
    public static bool IsPaused { get { return _paused != null; } }

    public void Pause()
    {
        if (IsPaused) return;

        _paused = this;

        _timeScaleBefore = Time.timeScale;
        Time.timeScale = 0f;
        if (pauseAudio) AudioListener.pause = true;

        if (hint != null)
        {
            _hintWasShown = hint.IsShown;
            _hintTextBefore = hint.Text;
            hint.Show(string.Format(resumeHintFormat, InputScheme_NEW.PauseGlyph()));
        }

        onPaused.Invoke();
        if (debugLog) Debug.Log("[JourneyPause_NEW] Paused.", this);
    }

    public void Resume()
    {
        if (_paused != this) return;

        ResumeNow();

        onResumed.Invoke();
        if (debugLog) Debug.Log("[JourneyPause_NEW] Resumed.", this);
    }

    /// <summary>Out of the pause with no event. Teardown and scene changes use this.</summary>
    public void ResumeNow()
    {
        if (_paused != this) return;

        _paused = null;

        Time.timeScale = _timeScaleBefore > 0f ? _timeScaleBefore : 1f;
        if (pauseAudio) AudioListener.pause = false;

        if (hint != null)
        {
            if (_hintWasShown) hint.Show(_hintTextBefore);
            else hint.Hide();
        }
    }

    void Awake()
    {
        if (hint == null) hint = FindObjectOfType<JourneyHint_NEW>();
    }

    void Update()
    {
        if (!InputScheme_NEW.PauseDown()) return;

        if (IsPaused) Resume();
        else Pause();
    }

    void OnDisable()
    {
        ResumeNow();
    }
}

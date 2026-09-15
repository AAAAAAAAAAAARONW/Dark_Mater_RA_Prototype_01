using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Opens on pure black and reveals the world underneath it.
///
/// A1 is "near black", and the piece has to begin from actual black rather than from
/// whatever the sky happens to look like — a walk-up visitor at the Observatories should
/// see the exhibit arrive, not find it already running. This is a full-screen black
/// image over the world and under the rest of the HUD, taken from opaque to clear.
///
/// Every number is exposed because this is a timing that gets tuned against the audio
/// and the VO, not decided once in code:
///
///   holdSeconds   how long full black holds before anything happens
///   fadeSeconds   how long the reveal takes
///   curve         the shape of it, so the reveal can ease rather than ramp
///
/// It is triggered by a named beat rather than by Start, so the black holds through the
/// attract card and begins when the tutorial actually begins. onFadeStarted and
/// onFadeComplete are there for the audio bed and the first VO line, which need to land
/// against the picture rather than against a stopwatch.
///
/// The image sits under the other HUD elements in the hierarchy, so the attract card
/// reads as white text on black rather than being hidden behind the fade.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("FADE", "#7A7A8C")]
public class TutorialFadeIn_NEW : MonoBehaviour
{
    [Header("Wiring")]
    [Tooltip("Leave empty to find the director in the scene.")]
    [SerializeField] TutorialDirector_NEW director;

    [Tooltip("Full-screen black image. Its alpha is what this component drives.")]
    [SerializeField] Image blackout;

    [Header("Trigger")]
    [Tooltip("The fade starts when this beat opens. A1 by default, so full black holds " +
             "through the attract card and lifts as the piece begins.")]
    [SerializeField] string startBeatId = "A1";

    [Header("Timing")]
    [Tooltip("Seconds of full black before the reveal starts.")]
    [SerializeField] float holdSeconds = 1.5f;

    [Tooltip("Seconds the reveal takes.")]
    [SerializeField] float fadeSeconds = 4f;

    [Tooltip("Shape of the reveal. X is normalised time, Y is how much of the world is " +
             "visible. The default eases out, so the last of the black lifts slowly.")]
    [SerializeField] AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Events")]
    [Tooltip("Fires when the hold ends and the reveal begins. Audio bed goes here.")]
    [SerializeField] UnityEvent onFadeStarted = new UnityEvent();

    [Tooltip("Fires when the world is fully visible.")]
    [SerializeField] UnityEvent onFadeComplete = new UnityEvent();

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    bool _running;
    bool _started;
    float _elapsed;

    /// <summary>0 = fully black, 1 = world fully visible.</summary>
    public float Revealed
    {
        get { return blackout != null ? 1f - blackout.color.a : 1f; }
    }

    void Awake()
    {
        if (director == null) director = FindObjectOfType<TutorialDirector_NEW>();
        if (blackout == null) blackout = GetComponent<Image>();

        if (blackout == null)
            Debug.LogError("[TutorialFadeIn_NEW] No blackout Image. The piece will not " +
                           "open on black.", this);

        SetAlpha(1f);
    }

    void OnEnable()
    {
        if (director == null) return;

        director.OnBeatEntered += HandleBeatEntered;
        director.OnIdleTimeout += ResetToBlack;
    }

    void OnDisable()
    {
        if (director == null) return;

        director.OnBeatEntered -= HandleBeatEntered;
        director.OnIdleTimeout -= ResetToBlack;
    }

    void Update()
    {
        if (!_running) return;

        _elapsed += TutorialClock_NEW.DeltaTime;

        if (_elapsed < holdSeconds)
        {
            SetAlpha(1f);
            return;
        }

        if (!_started)
        {
            _started = true;
            onFadeStarted.Invoke();

            if (debugLog) Debug.Log("[TutorialFadeIn_NEW] Reveal started.", this);
        }

        if (fadeSeconds <= 0f)
        {
            Finish();
            return;
        }

        float t = Mathf.Clamp01((_elapsed - holdSeconds) / fadeSeconds);
        SetAlpha(1f - curve.Evaluate(t));

        if (t >= 1f) Finish();
    }

    void HandleBeatEntered(TutorialBeat_NEW beat)
    {
        if (beat == null || _running) return;
        if (!string.Equals(beat.BeatId, startBeatId, System.StringComparison.OrdinalIgnoreCase)) return;

        Begin();
    }

    /// <summary>Start the hold-then-reveal. Safe to call from a UnityEvent.</summary>
    public void Begin()
    {
        _running = true;
        _started = false;
        _elapsed = 0f;

        SetAlpha(1f);

        if (debugLog) Debug.Log("[TutorialFadeIn_NEW] Holding black.", this);
    }

    /// <summary>Back to full black, ready for the next visitor.</summary>
    public void ResetToBlack()
    {
        _running = false;
        _started = false;
        _elapsed = 0f;

        SetAlpha(1f);
    }

    void Finish()
    {
        _running = false;
        SetAlpha(0f);

        onFadeComplete.Invoke();

        if (debugLog) Debug.Log("[TutorialFadeIn_NEW] Reveal complete.", this);
    }

    void SetAlpha(float alpha)
    {
        if (blackout == null) return;

        Color c = blackout.color;
        c.a = Mathf.Clamp01(alpha);
        blackout.color = c;

        // A fully transparent full-screen image still costs a screen of overdraw on the
        // Observatories' curved display. Switch it off once it has nothing to hide.
        bool needed = c.a > 0.001f;
        if (blackout.enabled != needed) blackout.enabled = needed;
    }
}

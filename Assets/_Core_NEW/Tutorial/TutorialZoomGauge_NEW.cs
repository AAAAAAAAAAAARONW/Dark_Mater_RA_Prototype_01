using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A gauge showing how far in the view is zoomed. The visible half of the zoom lesson.
///
/// A4 tells the player LEFT STICK · LOOK CLOSER and waits for them to do it, and that
/// still was not enough teaching, because a line of text and a picture that gets bigger
/// do not obviously belong to each other. A non-gamer pushing a stick and watching the
/// whole image scale has no reason to conclude that a control with a range exists — it
/// could as easily be the scene moving.
///
/// A gauge fixes that by making the state visible: a thing that fills as you push and
/// empties as you let go, sitting still on the screen while the world changes behind it.
/// That is what turns "something happened" into "I am doing this, and there is more of
/// it available".
///
/// It appears when the zoom lesson opens and stays for the rest of the piece, the same
/// way the control legend does. A readout that vanishes after its lesson teaches the
/// player that the control went away with it.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("ZOOM UI", "#6FA8DC")]
public class TutorialZoomGauge_NEW : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] TutorialZoom_NEW zoom;
    [SerializeField] TutorialDirector_NEW director;

    [Tooltip("The bar that fills. Its height is driven between nothing and full.")]
    [SerializeField] RectTransform fill;

    [Tooltip("The track the fill sits in. Its height is what full means.")]
    [SerializeField] RectTransform track;

    [Tooltip("Optional field-of-view readout, in degrees.")]
    [SerializeField] TMP_Text degreesLabel;

    [SerializeField] CanvasGroup group;

    [Header("Appearance")]
    [Tooltip("Seconds to fade in when the lesson opens, and to fade out on the attract " +
             "return.")]
    [SerializeField] float fadeDuration = 0.4f;

    [Tooltip("Alpha while the player is not touching the zoom. Present but quiet: the " +
             "control still exists, it is just not being used.")]
    [Range(0.1f, 1f)]
    [SerializeField] float restAlpha = 0.45f;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    bool _revealed;
    float _alpha;
    float _trackHeight;

    void Awake()
    {
        if (zoom == null) zoom = FindObjectOfType<TutorialZoom_NEW>();
        if (director == null) director = FindObjectOfType<TutorialDirector_NEW>();
        if (group == null) group = GetComponent<CanvasGroup>();

        if (track != null) _trackHeight = track.rect.height;

        SetAlpha(0f);
        SetFill(0f);
    }

    void OnEnable()
    {
        if (director == null) return;

        director.OnBeatEntered += HandleBeatEntered;
        director.OnIdleTimeout += Hide;
    }

    void OnDisable()
    {
        if (director == null) return;

        director.OnBeatEntered -= HandleBeatEntered;
        director.OnIdleTimeout -= Hide;
    }

    /// <summary>
    /// Show from the moment a zoom beat opens.
    ///
    /// Keyed on the beat's TYPE rather than on its id, so renaming A4 or adding a second
    /// zoom lesson later needs no change here.
    /// </summary>
    void HandleBeatEntered(TutorialBeat_NEW beat)
    {
        if (_revealed || beat == null) return;
        if (beat.GetComponent<Beat_Zoom_NEW>() == null) return;

        _revealed = true;

        if (debugLog) Debug.Log("[TutorialZoomGauge_NEW] Revealed at " + beat.BeatId + ".", this);
    }

    /// <summary>Back to hidden, for the next visitor.</summary>
    public void Hide()
    {
        _revealed = false;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float amount = zoom != null ? zoom.Amount : 0f;

        // Full alpha while the player is on the stick, quiet when they are not — so the
        // gauge answers "you are doing this" and then gets out of the way.
        float target = _revealed ? Mathf.Lerp(restAlpha, 1f, Mathf.Clamp01(amount * 3f)) : 0f;

        _alpha = fadeDuration <= 0f
            ? target
            : Mathf.MoveTowards(_alpha, target, dt / fadeDuration);

        SetAlpha(_alpha);
        SetFill(amount);
    }

    void SetFill(float amount)
    {
        if (fill == null) return;

        if (_trackHeight <= 0f && track != null) _trackHeight = track.rect.height;

        Vector2 size = fill.sizeDelta;
        size.y = _trackHeight * Mathf.Clamp01(amount);
        fill.sizeDelta = size;

        if (degreesLabel != null && zoom != null)
            degreesLabel.text = Mathf.RoundToInt(zoom.FieldOfView) + "°";
    }

    void SetAlpha(float alpha)
    {
        if (group == null) return;

        // Alpha only. Deactivating the object would stop this component's own Update,
        // so it could never fade back in — the gauge would work exactly once.
        group.alpha = alpha;
    }
}

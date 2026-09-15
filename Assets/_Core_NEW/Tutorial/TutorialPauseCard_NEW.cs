using TMPro;
using UnityEngine;

/// <summary>
/// What the screen looks like while the piece is paused: dimmed, with PAUSED and
/// B TO RESUME in the middle.
///
/// The resume instruction used to go on the hint line. That line sits low in the frame,
/// and since the camera moved behind the light it sits right over the photon trail — so
/// the one instruction a paused visitor must be able to read was printed across the
/// busiest part of the picture, as though it were a stray caption.
///
/// A card is the convention a visitor already knows, and it says two things at once:
/// the dimming says the world has stopped, and the words say how to start it. Text over
/// the middle of the frame reads as deliberate once the frame behind it is dimmed.
///
/// It polls TutorialPause_NEW rather than being told, the same way the HUD polls its
/// beats, so nothing has to remember to call it. The camera stays live behind it — GDD §5,
/// the camera is never locked — which is why the dim stops short of black.
///
/// Fades on real time: the world clock is stopped for exactly as long as this is up.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("PAUSE CARD", "#33B3B3")]
public class TutorialPauseCard_NEW : MonoBehaviour
{
    [Header("Wiring")]
    [Tooltip("Leave empty to find it.")]
    [SerializeField] TutorialPause_NEW pause;

    [Tooltip("Covers the whole card: the dim, the title and the resume line fade together.")]
    [SerializeField] CanvasGroup group;

    [Tooltip("Takes its words from TutorialPause_NEW.ResumeHintText, so there is one place " +
             "the wording lives.")]
    [SerializeField] TMP_Text resumeLabel;

    [Header("Timing")]
    [Tooltip("Seconds to fade in and out. Short: a pause should feel immediate.")]
    [SerializeField] float fadeSeconds = 0.2f;

    float _alpha;

    /// <summary>True while the card is on screen or fading.</summary>
    public bool IsShowing { get { return _alpha > 0.001f; } }

    void Awake()
    {
        if (pause == null) pause = FindObjectOfType<TutorialPause_NEW>();

        Apply(0f);
    }

    void Update()
    {
        bool paused = pause != null && pause.IsPaused;

        _alpha = Mathf.MoveTowards(_alpha, paused ? 1f : 0f,
                                   fadeSeconds > 0f ? Time.unscaledDeltaTime / fadeSeconds : 1f);

        if (resumeLabel != null && pause != null && resumeLabel.text != pause.ResumeHintText)
            resumeLabel.text = pause.ResumeHintText;

        Apply(_alpha);
    }

    void Apply(float alpha)
    {
        if (group == null) return;

        group.alpha = alpha;

        // Never catches clicks or taps, even when fully shown: nothing on the card is a
        // control, and a full-screen raycast target would swallow the attract card's.
        group.blocksRaycasts = false;
        group.interactable = false;
    }
}

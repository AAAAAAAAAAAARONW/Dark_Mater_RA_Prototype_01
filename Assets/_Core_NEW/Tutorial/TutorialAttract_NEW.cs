using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// The attract state that sits in front of A1: title card, "press A to begin", and the
/// return path from a 90 second idle.
///
/// Specified in Tutorial Storyboard v1 and not redrawn in v2, per GDD §5. It matters
/// more than a title card usually does because of the audience: a walk-up, non-gamer
/// visitor at the Carnegie Science Observatories arrives at whatever state the last
/// visitor left behind. If the piece does not return to a legible starting point on its
/// own, the second visitor of the day sees the middle of somebody else's tutorial.
///
/// Restarting is deliberately total. On the return to attract every mote is reset,
/// the HUD is cleared, the view goes back to the jet axis and the beat index goes to
/// −1. Anything that survives a restart is a bug that only shows up on the exhibition
/// floor, hours in, with a queue.
///
/// The one thing this does not do is fade the world. What the tutorial backdrop should
/// be — live universe or slow drift — is still open (GDD §10, item 3), and a fade
/// written now would have to be rewritten either way.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("ATTRACT", "#E0A030")]
public class TutorialAttract_NEW : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] TutorialDirector_NEW director;

    [Tooltip("Root of the title card. Active in attract, inactive once the piece runs.")]
    [SerializeField] GameObject cardRoot;

    [SerializeField] CanvasGroup cardGroup;

    [Header("Text")]
    [SerializeField] TMP_Text titleText;

    [SerializeField] string title = "THE JOURNEY OF LIGHT";

    [SerializeField] TMP_Text callToActionText;

    [Tooltip("The only instruction in the piece that is written rather than shown.")]
    [SerializeField] string callToAction = "PRESS A TO BEGIN";

    [Header("Feel")]
    [SerializeField] float fadeDuration = 0.5f;

    [Tooltip("Seconds of card before A is accepted. Stops the press that ended the last " +
             "run from immediately starting the next one.")]
    [SerializeField] float inputGraceSeconds = 0.4f;

    [Tooltip("Pulse period for the call to action, in seconds. 0 for a steady label.")]
    [SerializeField] float pulsePeriod = 1.8f;

    [Header("Restart")]
    [Tooltip("Reset every mote in the scene on the return to attract. See the summary — " +
             "a mote left mid-drift starts the next visitor's B1 already off screen.")]
    [SerializeField] bool resetMotes = true;

    [Tooltip("Send the view back to the direction of travel on the return to attract.")]
    [SerializeField] FirstPersonLookRig_NEW lookRig;

    [Tooltip("Put the light back at the start of its run. Without this the next visitor " +
             "begins wherever the last one drifted to.")]
    [SerializeField] TutorialTravel_NEW travel;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    bool _showing;
    float _shownFor;

    void Awake()
    {
        if (director == null) director = FindObjectOfType<TutorialDirector_NEW>();
        if (lookRig == null) lookRig = FindObjectOfType<FirstPersonLookRig_NEW>();
        if (travel == null) travel = FindObjectOfType<TutorialTravel_NEW>();

        if (director == null)
            Debug.LogError("[TutorialAttract_NEW] No TutorialDirector_NEW. Nothing can start.", this);

        if (titleText != null) titleText.text = title;
        if (callToActionText != null) callToActionText.text = callToAction;
    }

    void OnEnable()
    {
        if (director != null) director.OnIdleTimeout += Show;
    }

    void OnDisable()
    {
        if (director != null) director.OnIdleTimeout -= Show;
    }

    void Start()
    {
        // StartsInAttract rather than CurrentState: the director's Start may not have
        // run yet, and Unity gives no ordering between components on different objects.
        if (director == null || director.StartsInAttract) Show();
        else HideImmediate();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        if (cardGroup != null)
            cardGroup.alpha = fadeDuration <= 0f
                ? (_showing ? 1f : 0f)
                : Mathf.MoveTowards(cardGroup.alpha, _showing ? 1f : 0f, dt / fadeDuration);

        if (!_showing)
        {
            if (cardRoot != null && cardGroup != null && cardGroup.alpha <= 0.01f && cardRoot.activeSelf)
                cardRoot.SetActive(false);
            return;
        }

        _shownFor += dt;

        Pulse();

        if (_shownFor < inputGraceSeconds) return;
        if (!TutorialInput_NEW.ConfirmDown()) return;

        Begin();
    }

    void Pulse()
    {
        if (callToActionText == null || pulsePeriod <= 0f) return;

        float t = Mathf.Sin(Time.unscaledTime / pulsePeriod * Mathf.PI * 2f) * 0.5f + 0.5f;

        Color c = callToActionText.color;
        c.a = Mathf.Lerp(0.45f, 1f, t);
        callToActionText.color = c;
    }

    /// <summary>Show the card and put the piece back to its starting state.</summary>
    public void Show()
    {
        _showing = true;
        _shownFor = 0f;

        if (cardRoot != null) cardRoot.SetActive(true);

        if (director != null) director.ReturnToAttract();

        // Snap rather than recentre: the card is up, nobody is watching the view move,
        // and a 50 deg/s lerp from wherever the last visitor left it takes long enough
        // that the next press can land mid-turn.
        if (lookRig != null) lookRig.SnapToForward();

        if (travel != null) travel.ResetToStart();

        if (resetMotes) ResetAllMotes();

        if (debugLog) Debug.Log("[TutorialAttract_NEW] Attract shown.", this);
    }

    /// <summary>Dismiss the card and open A1.</summary>
    public void Begin()
    {
        Hide();

        if (director != null) director.StartTutorial();

        if (debugLog) Debug.Log("[TutorialAttract_NEW] Begun.", this);
    }

    void Hide()
    {
        _showing = false;
    }

    /// <summary>Skip the fade. Used when the piece is configured not to open on the card.</summary>
    void HideImmediate()
    {
        _showing = false;

        if (cardGroup != null) cardGroup.alpha = 0f;
        if (cardRoot != null) cardRoot.SetActive(false);
    }

    /// <summary>
    /// Reset every mote in the scene, including the ones a beat has not switched on yet.
    ///
    /// FindObjectsOfType skips inactive objects, and Unity 2019.4 has no generic
    /// includeInactive overload — that arrived in 2020.1. Resources.FindObjectsOfTypeAll
    /// does see them, at the cost of also returning prefab assets and anything in an
    /// editor preview scene, so the scene check filters those back out.
    /// </summary>
    static void ResetAllMotes()
    {
        GuideMote_NEW[] motes = Resources.FindObjectsOfTypeAll<GuideMote_NEW>();

        for (int i = 0; i < motes.Length; i++)
        {
            GuideMote_NEW mote = motes[i];
            if (mote == null) continue;
            if (!mote.gameObject.scene.IsValid()) continue;

            mote.ResetMote();
        }
    }
}

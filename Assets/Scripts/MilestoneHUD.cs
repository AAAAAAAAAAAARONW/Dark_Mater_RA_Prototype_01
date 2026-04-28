using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Milestone HUD — rich card popup.
///
/// Card layout (set up in the Unity scene):
///   [CardRoot]          ← CanvasGroup for alpha fade, RectTransform for slide
///     [IconBackground]  ← Image — tinted with MilestoneData.iconColor
///       [IconImage]     ← Image — optional Sprite from MilestoneData.icon
///     [MetaText]        ← TMP "12.5B ly  ·  1.3B years ago"
///     [TitleText]       ← TMP milestone name
///     [DescriptionText] ← TMP one-sentence description
///
/// Multiple milestones queue automatically — never overlap.
/// All timing values are exposed in the inspector.
/// </summary>
public class MilestoneHUD : MonoBehaviour
{
    // ─────────────────────────────────────────────
    // INSPECTOR
    // ─────────────────────────────────────────────

    [Header("References")]
    [SerializeField] UniverseJourneyTracker tracker;

    [Header("Card UI — wire up in the scene")]
    [Tooltip("CanvasGroup on the card root — used for alpha fade.")]
    [SerializeField] CanvasGroup cardCanvasGroup;

    [Tooltip("Image used as the colored circle behind the icon.\n" +
             "Its color will be set to MilestoneData.iconColor.")]
    [SerializeField] Image iconBackground;

    [Tooltip("Image for the icon sprite inside the circle.\n" +
             "Hidden automatically when MilestoneData.icon is null.")]
    [SerializeField] Image iconImage;

    [Tooltip("TMP text: 'X ly  ·  X years ago'")]
    [SerializeField] TextMeshProUGUI metaText;

    [Tooltip("TMP text: milestone name / title")]
    [SerializeField] TextMeshProUGUI titleText;

    [Tooltip("TMP text: one-sentence description")]
    [SerializeField] TextMeshProUGUI descriptionText;

    [Header("Timing")]
    [SerializeField] float displayDuration  = 3.5f;
    [SerializeField] float fadeInDuration   = 0.4f;
    [SerializeField] float fadeOutDuration  = 0.4f;

    [Header("Slide Animation (optional)")]
    [Tooltip("Slide the card in from this offset (local pixels). Set to zero to disable.")]
    [SerializeField] Vector2 slideInOffset  = new Vector2(-40f, 0f);
    [SerializeField] float   slideDuration  = 0.35f;

    // ─────────────────────────────────────────────
    // PRIVATE STATE
    // ─────────────────────────────────────────────

    readonly Queue<UniverseJourneyTracker.MilestoneData> _queue =
        new Queue<UniverseJourneyTracker.MilestoneData>();

    Coroutine   _showCoroutine;
    RectTransform _cardRect;
    Vector2     _anchoredPosHome;
    bool        _initialized;

    // ─────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────

    void Awake()
    {
        HideCard();
    }

    void Start()
    {
        if (tracker == null)
        {
            Debug.LogWarning("[MilestoneHUD] Tracker not assigned.");
            return;
        }
        if (cardCanvasGroup == null)
        {
            Debug.LogWarning("[MilestoneHUD] Card CanvasGroup not assigned.");
            return;
        }

        _cardRect = cardCanvasGroup.GetComponent<RectTransform>();
        if (_cardRect != null)
            _anchoredPosHome = _cardRect.anchoredPosition;

        HideCard();
        tracker.OnMilestoneReached += HandleMilestoneReached;
        _initialized = true;

        Debug.Log($"[MilestoneHUD] Ready. {tracker.Milestones?.Length ?? 0} milestones registered.");
    }

    void OnDestroy()
    {
        if (tracker != null)
            tracker.OnMilestoneReached -= HandleMilestoneReached;
    }

    // ─────────────────────────────────────────────
    // EVENT HANDLER
    // ─────────────────────────────────────────────

    void HandleMilestoneReached(UniverseJourneyTracker.MilestoneData data)
    {
        if (!_initialized) return;

        _queue.Enqueue(data);

        if (_showCoroutine == null)
            _showCoroutine = StartCoroutine(ProcessQueue());
    }

    // ─────────────────────────────────────────────
    // QUEUE PROCESSOR
    // ─────────────────────────────────────────────

    IEnumerator ProcessQueue()
    {
        while (_queue.Count > 0)
        {
            var data = _queue.Dequeue();
            yield return ShowCard(data);
        }
        _showCoroutine = null;
    }

    IEnumerator ShowCard(UniverseJourneyTracker.MilestoneData data)
    {
        PopulateCard(data);
        cardCanvasGroup.gameObject.SetActive(true);

        // Slide + fade in
        float t = 0f;
        Vector2 startPos = _anchoredPosHome + slideInOffset;
        while (t < fadeInDuration)
        {
            t += Time.deltaTime;
            float pct = Mathf.Clamp01(t / fadeInDuration);
            float eased = EaseOut(pct);
            cardCanvasGroup.alpha = eased;
            if (_cardRect != null && slideInOffset != Vector2.zero)
                _cardRect.anchoredPosition = Vector2.Lerp(startPos, _anchoredPosHome, eased);
            yield return null;
        }
        cardCanvasGroup.alpha = 1f;
        if (_cardRect != null) _cardRect.anchoredPosition = _anchoredPosHome;

        Debug.Log($"[MilestoneHUD] Showing: {data.label}");
        yield return new WaitForSeconds(displayDuration);

        // Fade out
        t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;
            cardCanvasGroup.alpha = 1f - Mathf.Clamp01(t / fadeOutDuration);
            yield return null;
        }

        HideCard();
    }

    // ─────────────────────────────────────────────
    // CARD POPULATION
    // ─────────────────────────────────────────────

    void PopulateCard(UniverseJourneyTracker.MilestoneData data)
    {
        // Meta line: "12.5B ly  ·  1.3B years ago"
        if (metaText != null)
        {
            string distStr  = UniverseJourneyTracker.FormatDistance(data.remainingDistanceThresholdLy);
            string yearsStr = UniverseJourneyTracker.FormatDistance(data.yearsAgo);
            metaText.text = $"{distStr}  ·  {yearsStr} years ago";
        }

        if (titleText != null)
            titleText.text = data.label;

        if (descriptionText != null)
            descriptionText.text = data.description;

        // Icon circle
        if (iconBackground != null)
            iconBackground.color = data.iconColor;

        if (iconImage != null)
        {
            iconImage.sprite  = data.icon;
            iconImage.enabled = data.icon != null;
        }
    }

    // ─────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────

    void HideCard()
    {
        if (cardCanvasGroup != null)
        {
            cardCanvasGroup.alpha = 0f;
            cardCanvasGroup.gameObject.SetActive(false);
        }
        if (_cardRect != null)
            _cardRect.anchoredPosition = _anchoredPosHome;
    }

    static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

#if UNITY_EDITOR
    void OnValidate()
    {
        displayDuration = Mathf.Max(0.5f, displayDuration);
        fadeInDuration  = Mathf.Max(0.05f, fadeInDuration);
        fadeOutDuration = Mathf.Max(0.05f, fadeOutDuration);
        slideDuration   = Mathf.Max(0.05f, slideDuration);
    }

    [ContextMenu("Test: Fire Milestone 0 (Big Bang)")]
    void DebugFireFirst()
    {
        if (tracker == null || tracker.Milestones == null || tracker.Milestones.Length == 0)
        { Debug.LogWarning("[MilestoneHUD] No tracker / milestones found."); return; }
        HandleMilestoneReached(tracker.Milestones[0]);
    }

    [ContextMenu("Test: Fire All Milestones (queue)")]
    void DebugFireAll()
    {
        if (tracker?.Milestones == null) return;
        foreach (var m in tracker.Milestones)
            HandleMilestoneReached(m);
    }
#endif
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Milestone HUD — single image popup.
///
/// Each milestone fires one pre-designed sprite (text baked in).
/// Sprites are matched by index — sprite[0] = milestones[0], etc.
/// Multiple milestones queue automatically — never overlap.
/// </summary>
public class MilestoneHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] UniverseJourneyTracker tracker;

    [Tooltip("Image component on PopupCard.")]
    [SerializeField] Image popupImage;

    [Tooltip("CanvasGroup on PopupCard — used for alpha fade.")]
    [SerializeField] CanvasGroup canvasGroup;

    [Header("Milestone Sprites")]
    [Tooltip("One sprite per milestone in the SAME ORDER as milestones[]\n" +
             "in UniverseJourneyTracker:\n" +
             "[0] First Stars Form\n" +
             "[1] Milky Way Forms\n" +
             "[2] Cosmic Noon\n" +
             "[3] Dark Energy Takes Over\n" +
             "[4] Sun Born\n" +
             "[5] Earth Born\n" +
             "[6] Dinosaurs Roam\n" +
             "[7] Humans Appear\n" +
             "[8] First Radio Signal\n" +
             "...and so on for all 20")]
    [SerializeField] Sprite[] milestoneSprites;

    [Header("Timing")]
    [SerializeField] float displayDuration = 3.5f;
    [SerializeField] float fadeInDuration = 0.4f;
    [SerializeField] float fadeOutDuration = 0.4f;

    readonly Queue<(string label, Sprite sprite, int index)> _queue =
        new Queue<(string, Sprite, int)>();

    Coroutine _showCoroutine;
    int _milestonesFired;
    bool _initialized;

    void Awake()
    {
        if (popupImage != null) popupImage.gameObject.SetActive(false);
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    void Start()
    {
        if (tracker == null)
        {
            Debug.LogWarning("[MilestoneHUD] Tracker not assigned.");
            return;
        }
        if (popupImage == null)
        {
            Debug.LogWarning("[MilestoneHUD] Popup Image not assigned.");
            return;
        }

        if (canvasGroup == null)
        {
            canvasGroup = popupImage.GetComponent<CanvasGroup>()
                       ?? popupImage.gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.alpha = 0f;
        popupImage.gameObject.SetActive(false);

        tracker.OnMilestoneReached += HandleMilestoneReached;
        _initialized = true;

        Debug.Log($"[MilestoneHUD] Ready. " +
                  $"{milestoneSprites?.Length ?? 0} sprites / " +
                  $"{tracker.Milestones?.Length ?? 0} milestones.");
    }

    void OnDestroy()
    {
        if (tracker != null)
            tracker.OnMilestoneReached -= HandleMilestoneReached;
    }

    void HandleMilestoneReached(string label, Sprite iconOverride)
    {
        if (!_initialized) return;

        int index = _milestonesFired++;
        Sprite sprite = iconOverride;

        if (sprite == null && milestoneSprites != null && index < milestoneSprites.Length)
            sprite = milestoneSprites[index];

        if (sprite == null)
        {
            Debug.Log($"[MilestoneHUD] '{label}' (index {index}) — no sprite, skipping.");
            return;
        }

        _queue.Enqueue((label, sprite, index));
        if (_showCoroutine == null)
            _showCoroutine = StartCoroutine(ProcessQueue());
    }

    IEnumerator ProcessQueue()
    {
        while (_queue.Count > 0)
        {
            var (label, sprite, index) = _queue.Dequeue();
            yield return ShowPopup(sprite, label, index);
        }
        _showCoroutine = null;
    }

    IEnumerator ShowPopup(Sprite sprite, string label, int index)
    {
        popupImage.sprite = sprite;
        popupImage.gameObject.SetActive(true);

        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(t / fadeInDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;
        Debug.Log($"[MilestoneHUD] Showing [{index}]: {label}");

        yield return new WaitForSeconds(displayDuration);

        t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = 1f - Mathf.Clamp01(t / fadeOutDuration);
            yield return null;
        }
        canvasGroup.alpha = 0f;
        popupImage.gameObject.SetActive(false);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        displayDuration = Mathf.Max(0.5f, displayDuration);
        fadeInDuration = Mathf.Max(0.1f, fadeInDuration);
        fadeOutDuration = Mathf.Max(0.1f, fadeOutDuration);

        if (tracker != null && milestoneSprites != null)
        {
            int mc = tracker.Milestones?.Length ?? 0;
            if (mc > 0 && milestoneSprites.Length != mc)
                Debug.LogWarning(
                    $"[MilestoneHUD] {milestoneSprites.Length} sprites but " +
                    $"{mc} milestones — sprite[i] must match milestones[i].");
        }
    }
#endif
}


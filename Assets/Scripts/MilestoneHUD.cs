using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Milestone HUD — sprite-based card popup.
///
/// Drop one pre-designed card Sprite per milestone into milestoneSprites[].
/// Sprites are matched by order: milestoneSprites[0] = first milestone, etc.
/// Multiple milestones queue automatically — never overlap.
/// </summary>
public class MilestoneHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] UniverseJourneyTracker tracker;

    [Tooltip("Image component that shows the card sprite.")]
    [SerializeField] Image popupImage;

    [Tooltip("CanvasGroup on the popup — used for alpha fade.")]
    [SerializeField] CanvasGroup canvasGroup;

    [Header("Milestone Sprites")]
    [Tooltip(
        "One pre-designed card sprite per milestone, in the same order as\n" +
        "UniverseJourneyTracker.milestones[] (24 total):\n\n" +
        "── Pre-Journey (fires at game start) ──\n" +
        "[0]  Big Bang\n" +
        "[1]  Cosmic Microwave Background\n" +
        "── Quasar Phase  13B → 11B ly ──\n" +
        "[2]  First Stars Ignite\n" +
        "[3]  Cosmic Reionization\n" +
        "── Cosmic Web 1  11B → 7B ly ──\n" +
        "[4]  Milky Way Forms\n" +
        "[5]  Peak Quasar Activity\n" +
        "[6]  Cosmic Star Formation Noon\n" +
        "[7]  Dark Energy Dominates\n" +
        "── Galaxy Phase  7B → 6.9999B ly ──\n" +
        "[8]  Galaxy Mergers Peak\n" +
        "── Cosmic Web 2  6.9999B → 24K ly ──\n" +
        "[9]  Sun Ignites\n" +
        "[10] Earth Is Born\n" +
        "[11] Giant Impact — Moon Forms\n" +
        "[12] First Life Emerges\n" +
        "[13] Oxygen Revolution\n" +
        "[14] Multicellular Life\n" +
        "[15] Dinosaurs Rise\n" +
        "[16] Mass Extinction\n" +
        "[17] Homo Sapiens Appear\n" +
        "── Milky Way Phase  24K → 1 ly ──\n" +
        "[18] First Cities Rise\n" +
        "[19] First Telescope\n" +
        "[20] First Radio Signals Leave Earth\n" +
        "── Solar System Phase  1 → 1e-12 ly ──\n" +
        "[21] Edge of the Solar System\n" +
        "[22] Crossing the Heliopause\n" +
        "── Earth Phase  1e-12 → 0 ly ──\n" +
        "[23] Earth")]
    [SerializeField] Sprite[] milestoneSprites;

    [Header("Timing")]
    [SerializeField] float displayDuration  = 3.5f;
    [SerializeField] float fadeInDuration   = 0.4f;
    [SerializeField] float fadeOutDuration  = 0.4f;

    // ─────────────────────────────────────────────
    // PRIVATE STATE
    // ─────────────────────────────────────────────

    readonly Queue<(int index, Sprite sprite)> _queue =
        new Queue<(int, Sprite)>();

    Coroutine _showCoroutine;
    int  _milestonesFired;
    bool _initialized;

    // ─────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────

    void Awake()
    {
        if (popupImage != null) popupImage.gameObject.SetActive(false);
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    void Start()
    {
        if (tracker == null)
        { Debug.LogWarning("[MilestoneHUD] Tracker not assigned."); return; }
        if (popupImage == null)
        { Debug.LogWarning("[MilestoneHUD] Popup Image not assigned."); return; }

        if (canvasGroup == null)
            canvasGroup = popupImage.GetComponent<CanvasGroup>()
                       ?? popupImage.gameObject.AddComponent<CanvasGroup>();

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

    // ─────────────────────────────────────────────
    // EVENT HANDLER
    // ─────────────────────────────────────────────

    void HandleMilestoneReached(UniverseJourneyTracker.MilestoneData data)
    {
        if (!_initialized) return;

        int index = _milestonesFired++;

        // Use icon embedded in MilestoneData first; fall back to the sprite array.
        Sprite sprite = data.icon;
        if (sprite == null && milestoneSprites != null && index < milestoneSprites.Length)
            sprite = milestoneSprites[index];

        if (sprite == null)
        {
            Debug.Log($"[MilestoneHUD] '{data.label}' (index {index}) — no sprite, skipping.");
            return;
        }

        _queue.Enqueue((index, sprite));
        if (_showCoroutine == null)
            _showCoroutine = StartCoroutine(ProcessQueue());
    }

    // ─────────────────────────────────────────────
    // QUEUE
    // ─────────────────────────────────────────────

    IEnumerator ProcessQueue()
    {
        while (_queue.Count > 0)
        {
            var (index, sprite) = _queue.Dequeue();
            yield return ShowPopup(sprite, index);
        }
        _showCoroutine = null;
    }

    IEnumerator ShowPopup(Sprite sprite, int index)
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
        Debug.Log($"[MilestoneHUD] Showing [{index}]");

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

    // ─────────────────────────────────────────────
    // EDITOR HELPERS
    // ─────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("Test: Fire Milestone 0")]
    void DebugFireFirst()
    {
        if (tracker?.Milestones == null || tracker.Milestones.Length == 0) return;
        HandleMilestoneReached(tracker.Milestones[0]);
    }

    [ContextMenu("Test: Fire All Milestones (queue)")]
    void DebugFireAll()
    {
        if (tracker?.Milestones == null) return;
        foreach (var m in tracker.Milestones)
            HandleMilestoneReached(m);
    }

    void OnValidate()
    {
        displayDuration = Mathf.Max(0.5f, displayDuration);
        fadeInDuration  = Mathf.Max(0.1f, fadeInDuration);
        fadeOutDuration = Mathf.Max(0.1f, fadeOutDuration);

        if (tracker != null && milestoneSprites != null)
        {
            int mc = tracker.Milestones?.Length ?? 0;
            if (mc > 0 && milestoneSprites.Length != mc)
                Debug.LogWarning(
                    $"[MilestoneHUD] {milestoneSprites.Length} sprites but " +
                    $"{mc} milestones — counts should match.");
        }
    }
#endif
}

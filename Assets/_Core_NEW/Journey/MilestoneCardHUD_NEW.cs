using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows one milestone card at a time, queueing any that overlap.
///
/// Sprites come from the MilestoneSet_NEW entry that fired, so there is no parallel
/// array and no index matching. An entry with no card art is skipped with a warning
/// rather than silently dropped.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("CARD", "#33B3B3")]
public class MilestoneCardHUD_NEW : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] MilestoneRelay_NEW relay;

    [Tooltip("Image that displays the card sprite.")]
    [SerializeField] Image cardImage;

    [Tooltip("CanvasGroup used for the fade. Usually on the same object as the Image.")]
    [SerializeField] CanvasGroup canvasGroup;

    [Header("Timing")]
    [SerializeField] float fadeInDuration = 0.4f;
    [SerializeField] float holdDuration = 3.5f;
    [SerializeField] float fadeOutDuration = 0.4f;

    [Tooltip("Ignore Time.timeScale, so cards still play if the game is paused.")]
    [SerializeField] bool useUnscaledTime = true;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    readonly Queue<MilestoneSet_NEW.Entry> _queue = new Queue<MilestoneSet_NEW.Entry>();
    Coroutine _pump;

    void Awake()
    {
        if (relay == null) relay = FindObjectOfType<MilestoneRelay_NEW>();

        if (cardImage == null) cardImage = GetComponentInChildren<Image>(true);
        if (canvasGroup == null) canvasGroup = GetComponentInChildren<CanvasGroup>(true);

        if (relay == null) Debug.LogError("[MilestoneCardHUD_NEW] No MilestoneRelay_NEW found.", this);
        if (cardImage == null) Debug.LogError("[MilestoneCardHUD_NEW] No Image assigned.", this);
        if (canvasGroup == null) Debug.LogError("[MilestoneCardHUD_NEW] No CanvasGroup assigned.", this);

        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    void OnEnable()
    {
        if (relay != null) relay.OnMilestoneReached += Enqueue;
    }

    void OnDisable()
    {
        if (relay != null) relay.OnMilestoneReached -= Enqueue;

        if (_pump != null) { StopCoroutine(_pump); _pump = null; }
        _queue.Clear();
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    void Enqueue(MilestoneSet_NEW.Entry entry)
    {
        if (entry == null) return;

        if (entry.card == null)
        {
            Debug.LogWarning($"[MilestoneCardHUD_NEW] '{entry.label}' has no card sprite. Skipped.", this);
            return;
        }

        _queue.Enqueue(entry);
        if (_pump == null) _pump = StartCoroutine(Pump());
    }

    IEnumerator Pump()
    {
        while (_queue.Count > 0)
            yield return Show(_queue.Dequeue());

        _pump = null;
    }

    IEnumerator Show(MilestoneSet_NEW.Entry entry)
    {
        if (cardImage == null || canvasGroup == null) yield break;

        if (debugLog) Debug.Log($"[MilestoneCardHUD_NEW] Showing '{entry.label}'.", this);

        cardImage.sprite = entry.card;

        yield return Fade(0f, 1f, fadeInDuration);
        yield return Wait(holdDuration);
        yield return Fade(1f, 0f, fadeOutDuration);
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        float d = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < d)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / d);
            canvasGroup.alpha = Mathf.Lerp(from, to, t * t * (3f - 2f * t));
            yield return null;
        }

        canvasGroup.alpha = to;
    }

    IEnumerator Wait(float seconds)
    {
        if (!useUnscaledTime) { yield return new WaitForSeconds(seconds); yield break; }

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }
}

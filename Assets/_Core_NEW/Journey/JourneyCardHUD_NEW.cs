using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The journey's station cards (_Core_NEW/UI/Milestones/JourneyMilestone_*.png): one at a
/// time on row 3 of the display, right beneath the LAF on row 2, against the row's right
/// edge, sliding in a short way from the right, holding, sliding back out.
/// Replaces the world-space cards (World Space UI) and MilestoneHUD's history cards in
/// Gameplay_Scene_Ana, which are hidden, not deleted.
///
/// Each card says when it shows:
///   AtStart     a few seconds into the journey (Quasar, "journey's origin").
///   BeforeGate  when the player is leadDistance short of a layer trigger, so the card
///               names the place before the photon gets there.
///   AfterGate   delaySeconds after the player crosses a layer trigger, for a place that is
///               only there once the dive into it has opened (Galaxy).
///   Manual      only when something calls Show(id): Light Absorption from the redshift
///               sequence's F2, Dark Matter once that section exists.
///
/// Gates are the tracker's layer trigger transforms, read by Z like the tracker reads them,
/// so moving a door moves its card with it. Automatic cards show strictly in list order,
/// and ones that come due together queue; jumping past several gates at once (the debug
/// layer jump) keeps only the last maxQueued of them.
///
/// Tools > Journey NEW > Journey Cards > Apply Art builds this in the scene.
/// </summary>
[DisallowMultipleComponent]
public class JourneyCardHUD_NEW : MonoBehaviour
{
    public enum Trigger { AtStart, BeforeGate, AfterGate, Manual }

    [Serializable]
    public class Card
    {
        [Tooltip("Name for Show(id) and the debug menu. Two cards may share art but not an id.")]
        public string id;
        public Sprite sprite;
        public Trigger trigger = Trigger.BeforeGate;

        [Tooltip("BeforeGate / AfterGate: the layer trigger.")]
        public Transform gate;

        [Tooltip("BeforeGate: how far short of the gate (Z units) the card shows.")]
        [Min(0f)] public float leadDistance = 25f;

        [Tooltip("AtStart / AfterGate: seconds after the journey starts / the gate is crossed.")]
        [Min(0f)] public float delaySeconds = 0f;

        [NonSerialized] public bool fired;
        [NonSerialized] public float dueTime;
    }

    [Header("References")]
    [Tooltip("The photon. Its Z is compared with the gates.")]
    [SerializeField] Transform player;

    [Tooltip("The sliding card. Placed in its parent, which is the slot it shows in: " +
             "LAF HUD/Row_3/JourneyCards, stretched over the row.")]
    [SerializeField] RectTransform cardRect;
    [SerializeField] Image cardImage;
    [SerializeField] CanvasGroup canvasGroup;

    [Header("Cards (in journey order)")]
    [SerializeField] Card[] cards = new Card[0];

    [Header("Layout")]
    [Tooltip("Card height as a fraction of the slot's (the row's) height. Width follows " +
             "each card's art.")]
    [Range(0.2f, 1f)]
    [SerializeField] float heightInSlot = 0.9f;

    [Tooltip("Canvas units between the card and the row's right edge.")]
    [SerializeField] float rightMargin = 0f;

    [Tooltip("Canvas units the card slides in from, to the right of where it rests.")]
    [SerializeField] float slideDistance = 40f;

    [Header("Timing")]
    [SerializeField] float slideInSeconds = 0.45f;
    [SerializeField] float holdSeconds = 4f;
    [SerializeField] float slideOutSeconds = 0.35f;

    [Tooltip("Cards waiting beyond this many are dropped, oldest first, so the corner " +
             "never runs a backlog that no longer matches where the photon is.")]
    [SerializeField] int maxQueued = 2;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    readonly Queue<Card> _queue = new Queue<Card>();
    Coroutine _pump;
    float _startTime;

    void Awake()
    {
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (cardRect != null) cardRect.gameObject.SetActive(false);
    }

    void Start()
    {
        _startTime = Time.time;
        if (player == null)
            Debug.LogWarning("[JourneyCardHUD_NEW] No player assigned: gate cards will not fire.", this);
    }

    void Update()
    {
        float z = player != null ? player.position.z : float.NegativeInfinity;

        // In list order, and none ahead of an earlier card that has yet to show: the photon
        // spawns only 19 units short of the Cosmic Web door, so Cosmic Web is due on the
        // first frame, before Quasar's start delay is up. Manual cards never hold others up.
        foreach (Card c in cards)
        {
            if (c == null || c.trigger == Trigger.Manual) continue;
            if (c.fired) continue;
            if (!IsDue(c, z))
            {
                if (CanFire(c)) break;   // still to come: everything after it waits
                continue;                // can never fire (no gate): does not block
            }
            c.fired = true;
            Enqueue(c);
        }
    }

    static bool CanFire(Card c) => c.trigger == Trigger.AtStart || c.gate != null;

    bool IsDue(Card c, float z)
    {
        switch (c.trigger)
        {
            case Trigger.AtStart:
                return Time.time - _startTime >= c.delaySeconds;

            case Trigger.BeforeGate:
                return c.gate != null && z >= c.gate.position.z - c.leadDistance;

            case Trigger.AfterGate:
                if (c.gate == null) return false;
                if (c.dueTime <= 0f)
                {
                    if (z < c.gate.position.z) return false;
                    c.dueTime = Time.time + c.delaySeconds;
                }
                return Time.time >= c.dueTime;

            default:
                return false;
        }
    }

    /// <summary>Shows the card with this id now (or after the one showing). For UnityEvents.</summary>
    public void Show(string id)
    {
        foreach (Card c in cards)
        {
            if (c == null || c.id != id) continue;
            c.fired = true;
            Enqueue(c);
            return;
        }
        Debug.LogWarning($"[JourneyCardHUD_NEW] No card with id '{id}'.", this);
    }

    /// <summary>Re-arms every card, for a restart.</summary>
    public void ResetAll()
    {
        foreach (Card c in cards)
            if (c != null) { c.fired = false; c.dueTime = 0f; }
        _startTime = Time.time;
    }

    void Enqueue(Card c)
    {
        if (c.sprite == null)
        {
            Debug.LogWarning($"[JourneyCardHUD_NEW] Card '{c.id}' has no art. Skipped.", this);
            return;
        }
        if (cardRect == null || cardImage == null) return;

        if (debugLog) Debug.Log($"[JourneyCardHUD_NEW] '{c.id}'.", this);

        _queue.Enqueue(c);
        while (_queue.Count > Mathf.Max(1, maxQueued)) _queue.Dequeue();

        if (_pump == null) _pump = StartCoroutine(Pump());
    }

    IEnumerator Pump()
    {
        while (_queue.Count > 0)
            yield return Play(_queue.Dequeue());
        _pump = null;
    }

    IEnumerator Play(Card c)
    {
        cardImage.sprite = c.sprite;
        Vector2 size = c.sprite.rect.size;
        var slot = cardRect.parent as RectTransform;
        float height = (slot != null ? slot.rect.height : 90f) * heightInSlot;
        cardRect.anchorMin = cardRect.anchorMax = cardRect.pivot = new Vector2(1f, 0.5f);
        cardRect.sizeDelta = new Vector2(height * size.x / size.y, height);

        Vector2 shown = new Vector2(-rightMargin, 0f);
        Vector2 hidden = shown + new Vector2(slideDistance, 0f);

        cardRect.gameObject.SetActive(true);
        yield return Slide(hidden, shown, 0f, 1f, slideInSeconds, easeOut: true);
        yield return new WaitForSeconds(holdSeconds);
        yield return Slide(shown, hidden, 1f, 0f, slideOutSeconds, easeOut: false);
        cardRect.gameObject.SetActive(false);
    }

    IEnumerator Slide(Vector2 from, Vector2 to, float alphaFrom, float alphaTo, float seconds, bool easeOut)
    {
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float k = t / seconds;
            k = easeOut ? 1f - (1f - k) * (1f - k) * (1f - k) : k * k;
            cardRect.anchoredPosition = Vector2.LerpUnclamped(from, to, k);
            if (canvasGroup != null) canvasGroup.alpha = Mathf.Lerp(alphaFrom, alphaTo, k);
            yield return null;
        }
        cardRect.anchoredPosition = to;
        if (canvasGroup != null) canvasGroup.alpha = alphaTo;
    }

#if UNITY_EDITOR
    [ContextMenu("Debug: Show the next card")]
    void DebugShowNext()
    {
        foreach (Card c in cards)
            if (c != null && !c.fired) { c.fired = true; Enqueue(c); return; }
        Debug.Log("[JourneyCardHUD_NEW] Every card has shown. Reset All to start over.", this);
    }

    [ContextMenu("Debug: Reset All")]
    void DebugReset() => ResetAll();
#endif
}

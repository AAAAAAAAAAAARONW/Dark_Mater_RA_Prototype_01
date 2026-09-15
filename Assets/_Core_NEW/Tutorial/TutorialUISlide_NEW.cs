using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Moves one RectTransform from one place to another, slowly, when told to.
///
/// D1 and D2 need it: the spectrum bar arrives at the start of D1, empty, near the
/// middle of the screen, and drifts up to its docked position over the next few seconds
/// while the atom closes.
///
/// WHY THE BAR ARRIVES BEFORE THE IMPACT RATHER THAN ON IT. GDD §6 has the bar appear
/// at D2, on contact, with the line cut into it in the same instant. That asks the
/// player to read two new things at once — here is a spectrum, and here is a gap in it
/// — and the second only means anything against the first. Showing the bar empty for
/// ten seconds first turns the impact into a CHANGE to something already on screen,
/// which is the only form in which §3 items 3 and 4 can be read at all: you cannot see
/// that a colour is missing unless you saw it there.
///
/// It also removes a plain readability problem. The bar's own arrival is a large
/// movement, and it would otherwise be competing for the eye with the one frame in the
/// piece that has to land.
///
/// THE MOVE IS SLOW ON PURPOSE. A HUD element that snaps into place reads as an
/// interface being switched on; one that drifts reads as something settling into where
/// it lives. The player is watching the atom, not the bar, so the bar has the whole of
/// D1 to get where it is going without ever being the thing in frame.
///
/// Generic rather than spectrum-specific because Phase 4 needs the same move twice —
/// E2's dark matter slider and E3's journey readout both arrive and settle. A component
/// that only knew about the spectrum would be rewritten twice.
///
/// On the world clock (TutorialClock_NEW): unaffected by D2's slow motion, so the bar
/// does not lag the frame that owns it, but held still while the piece is paused so it
/// stays in step with the atom it arrives alongside.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[HierarchyBadge_NEW("UI SLIDE", "#6FA8DC")]
public class TutorialUISlide_NEW : MonoBehaviour
{
    [Header("Where")]
    [Tooltip("Anchored position it starts from. Taken as the position it is built at " +
             "when captureFromCurrent is on, which is the normal case.")]
    [SerializeField] Vector2 from;

    [Tooltip("Anchored position it settles at. This is the element's real home — the " +
             "place it occupies for the rest of the piece.")]
    [SerializeField] Vector2 to;

    [Tooltip("Read `from` off the RectTransform as built instead of the field above, so " +
             "moving the object in the Scene view moves where it comes in from.")]
    [SerializeField] bool captureFromCurrent = true;

    [Header("Timing")]
    [Tooltip("Seconds the move takes, in real time. Long: this is a drift, not a " +
             "transition. D1 runs ten seconds and the bar has all of it.")]
    [SerializeField] float seconds = 7f;

    [Tooltip("Seconds to wait after Play() before anything moves. Lets the element be " +
             "seen where it arrived before it starts going anywhere.")]
    [SerializeField] float delaySeconds = 1.5f;

    [Tooltip("Shape of the move. Ease in and out so it neither starts nor stops abruptly.")]
    [SerializeField] AnimationCurve shape = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Events")]
    [Tooltip("Fires once the element has arrived.")]
    [SerializeField] UnityEvent onArrived = new UnityEvent();

    RectTransform _rect;
    bool _running;
    float _elapsed;
    bool _arrivedFired;

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Progress of the move, 0 at the start and 1 once settled.</summary>
    public float Progress
    {
        get
        {
            if (seconds <= 0f) return 1f;
            return Mathf.Clamp01((_elapsed - delaySeconds) / seconds);
        }
    }

    /// <summary>True once the element is at its home position.</summary>
    public bool HasArrived { get { return Progress >= 1f; } }

    /// <summary>Start the move from the beginning. D1's onEnter calls this.</summary>
    public void Play()
    {
        Resolve();

        _running = true;
        _elapsed = 0f;
        _arrivedFired = false;

        _rect.anchoredPosition = from;
    }

    /// <summary>Put it at its home position with no move at all. The attract reset uses this.</summary>
    public void SnapToEnd()
    {
        Resolve();

        _running = false;
        _elapsed = delaySeconds + seconds;
        _arrivedFired = true;

        _rect.anchoredPosition = to;
    }

    /// <summary>Put it back where it comes in from, without starting. Attract uses this.</summary>
    public void ResetToStart()
    {
        Resolve();

        _running = false;
        _elapsed = 0f;
        _arrivedFired = false;

        _rect.anchoredPosition = from;
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        Resolve();
    }

    void Resolve()
    {
        if (_rect != null) return;

        _rect = GetComponent<RectTransform>();

        // Read the built position once, before anything has moved it.
        if (captureFromCurrent) from = _rect.anchoredPosition;
    }

    void Update()
    {
        if (!_running) return;

        _elapsed += TutorialClock_NEW.DeltaTime;

        if (_elapsed < delaySeconds) return;

        float t = Progress;
        float shaped = shape != null ? shape.Evaluate(t) : t;

        _rect.anchoredPosition = Vector2.LerpUnclamped(from, to, shaped);

        if (t < 1f || _arrivedFired) return;

        _arrivedFired = true;
        _running = false;
        onArrived.Invoke();
    }
}

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The close look at the spectrum. D5's "pressing it freezes the scene and enlarges the
/// spectrum".
///
/// It takes the bar from its docked corner of the screen to the middle, large, over a
/// backdrop that dims the world behind it — and puts it back.
///
/// WHY THE PIECE ENDS BY LOOKING AT THE READOUT. Phase 3 has spent forty seconds saying
/// that the marks on the bar are the player's own: D2 cut one on contact, D3 set them
/// moving, D4 added three more. §3 item 5 is "the lines are the only evidence you carry
/// to Earth", and that only lands if the player is given a moment to actually read the
/// thing they have been accumulating. Every frame before this one had something else in
/// it worth watching.
///
/// FREEZING IS NOT LOCKING THE CAMERA. The world stops; the view does not. Everything
/// in the tutorial runs on unscaled time, so a time scale of nearly zero holds the
/// atoms, the travel and the drift still while the player keeps full control of where
/// they are looking. GDD §5's "the camera is never locked" survives a frame whose whole
/// content is standing still, which is the only reason this frame can exist at all.
///
/// The freeze itself belongs to TutorialSlowMotion_NEW, which already owns
/// Time.timeScale; this owns only what the bar looks like. The beat connects them.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("INSPECT", "#33B3B3")]
public class TutorialInspectView_NEW : MonoBehaviour
{
    [Header("What moves")]
    [Tooltip("The spectrum bar. Its docked position and size are read on Awake, so " +
             "moving the bar in the Scene view moves where it returns to.")]
    [SerializeField] RectTransform bar;

    [Tooltip("Anchored position while inspecting. Centre screen — the bar is the only " +
             "thing being looked at, so it takes the place the reticle usually holds.")]
    [SerializeField] Vector2 inspectPosition = new Vector2(0f, 40f);

    [Tooltip("Scale while inspecting. Large enough that a single absorption line is a " +
             "band rather than a hairline, which is the point of looking closer.")]
    [SerializeField] float inspectScale = 1.9f;

    [Header("Backdrop")]
    [Tooltip("Full-screen image behind the bar, faded in while inspecting. Optional, " +
             "but without it an enlarged graph sits on top of a live starfield and the " +
             "eye has nowhere to rest.")]
    [SerializeField] Image backdrop;

    [SerializeField] Color backdropColor = new Color(0.02f, 0.02f, 0.05f, 0.82f);

    [Header("Timing")]
    [Tooltip("Seconds the open and close take, in real time. Quick — this is a control " +
             "the player is holding, not a cinematic.")]
    [SerializeField] float seconds = 0.28f;

    [SerializeField] AnimationCurve shape = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    Vector2 _dockedPosition;
    Vector3 _dockedScale = Vector3.one;
    bool _captured;

    /// <summary>0 docked, 1 fully enlarged.</summary>
    float _openness;
    float _target;

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>True while the enlarged view is open or opening.</summary>
    public bool IsOpen { get { return _target > 0.5f; } }

    /// <summary>True once the view has finished moving, either way.</summary>
    public bool IsSettled { get { return Mathf.Approximately(_openness, _target); } }

    /// <summary>Enlarge. The inspect beat calls this on the press.</summary>
    public void Open()
    {
        Capture();
        _target = 1f;

        if (debugLog) Debug.Log("[TutorialInspectView_NEW] Open.", this);
    }

    /// <summary>Back to the docked bar. The second press calls this.</summary>
    public void Close()
    {
        Capture();
        _target = 0f;

        if (debugLog) Debug.Log("[TutorialInspectView_NEW] Close.", this);
    }

    /// <summary>
    /// Docked, immediately, with no move. The attract reset uses this — a bar left
    /// enlarged would be the first thing the next visitor sees.
    /// </summary>
    public void ResetForAttract()
    {
        Capture();

        _target = 0f;
        _openness = 0f;

        Apply();
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        Capture();

        // Nothing is enlarged before D5, and the backdrop is not on screen at all.
        _openness = 0f;
        _target = 0f;

        Apply();
    }

    /// <summary>
    /// Read the docked position and size once, before anything has moved them.
    ///
    /// Read rather than authored because D1's slide already owns where the bar lives,
    /// and two components holding one position is how they end up disagreeing.
    /// </summary>
    void Capture()
    {
        if (_captured || bar == null) return;

        _dockedPosition = bar.anchoredPosition;
        _dockedScale = bar.localScale;
        _captured = true;
    }

    void Update()
    {
        if (IsSettled) return;

        // Unscaled: the whole point of this frame is that the world is stopped, and a
        // transition measured in scaled time would never finish.
        float step = seconds <= 0f ? 1f : Time.unscaledDeltaTime / seconds;

        _openness = Mathf.MoveTowards(_openness, _target, step);
        Apply();
    }

    void Apply()
    {
        float t = shape != null ? shape.Evaluate(_openness) : _openness;

        if (bar != null)
        {
            bar.anchoredPosition = Vector2.LerpUnclamped(_dockedPosition, inspectPosition, t);
            bar.localScale = Vector3.LerpUnclamped(_dockedScale, _dockedScale * inspectScale, t);
        }

        if (backdrop == null) return;

        Color c = backdropColor;
        c.a *= t;
        backdrop.color = c;

        // Off entirely when closed, so it costs no fill rate through the rest of the
        // piece and cannot swallow anything behind it.
        if (backdrop.gameObject.activeSelf != (t > 0.001f))
            backdrop.gameObject.SetActive(t > 0.001f);
    }
}

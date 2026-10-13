using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The journey opens on a beat of black (0.1 s), then fades up: blackSeconds of black with the
/// light waiting at the spawn, then the picture fades in, the light sets off, and the intro
/// camera pushes in. J0 starts as the fade does (the Quasar voice-over row is 1 s in).
///
/// WHY HOLD THE LIGHT. The first layer is short — 35 units, about 14 seconds at full speed —
/// and must not be slowed, because the trail is drawn from the light's motion. Holding it
/// during the black costs nothing that can be seen and keeps every second of flight for the
/// voice: J0 then starts J1 before the first gate, and a line already started is never cut.
/// (It was first tried with all of J0 over black, about 12 seconds; too long.)
///
/// HOW IT FITS WITH THE OTHER OPENINGS.
///   SceneHandoff_NEW (from the tutorial) and JourneyIntroCamera_NEW (opened directly) each
///   fade up from black. This overlay sits above both, so their fades happen unseen under
///   it, and JourneyIntroCamera_NEW waits for IsHolding to clear before its push in.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-300)]
[HierarchyBadge_NEW("OPENING", "#B36AE2")]
public class JourneyOpening_NEW : MonoBehaviour
{
    [Header("Switch")]
    [Tooltip("Off = the journey opens as it did, with no black hold.")]
    [SerializeField] bool openingEnabled = true;

    [Header("Wiring (found if empty)")]
    [SerializeField] PlayerRig_NEW player;

    [Header("Fade")]
    [Tooltip("Seconds of black before the fade up begins.")]
    [Min(0f)]
    [SerializeField] float blackSeconds = 0.1f;

    [Tooltip("Seconds for the fade up from black.")]
    [Min(0f)]
    [SerializeField] float fadeSeconds = 2.5f;

    [Tooltip("Above JourneyIntroCamera_NEW's entry fade (32000) and every HUD canvas.")]
    [SerializeField] int overlaySortOrder = 32100;

    [SerializeField] bool debugLog = false;

    /// <summary>True while the screen is held black and the light held still.</summary>
    public static bool IsHolding { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { IsHolding = false; }

    GameObject _overlay;
    Image _black;
    float _held;
    float _fade = -1f;

    /// <summary>Fade up and let the light go now, without waiting out blackSeconds.</summary>
    public void Reveal()
    {
        if (!IsHolding) return;

        IsHolding = false;
        if (player != null) player.SetHeld(false);
        _fade = 0f;

        if (debugLog) Debug.Log("[JourneyOpening_NEW] Reveal after " + _held.ToString("0.0") + " s.", this);
    }

    void Awake()
    {
        if (!openingEnabled) return;

        if (player == null) player = FindObjectOfType<PlayerRig_NEW>();

        // Awake, so the black is up before the first frame is drawn.
        _overlay = new GameObject("JourneyOpening (black)", typeof(RectTransform));
        Canvas canvas = _overlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = overlaySortOrder;

        GameObject img = new GameObject("Black", typeof(RectTransform));
        img.transform.SetParent(_overlay.transform, false);
        RectTransform rt = (RectTransform)img.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _black = img.AddComponent<Image>();
        _black.raycastTarget = false;
        _black.color = Color.black;

        IsHolding = true;
        if (player != null) player.SetHeld(true);
    }

    void Update()
    {
        if (IsHolding)
        {
            _held += Time.unscaledDeltaTime;
            if (_held >= blackSeconds) Reveal();
            return;
        }

        if (_fade < 0f || _black == null) return;

        _fade += Time.unscaledDeltaTime;
        float k = fadeSeconds > 0f ? Mathf.Clamp01(_fade / fadeSeconds) : 1f;
        _black.color = new Color(0f, 0f, 0f, 1f - k * k * (3f - 2f * k));

        if (k >= 1f)
        {
            Destroy(_overlay);
            _overlay = null;
            _black = null;
            _fade = -1f;
        }
    }

    void OnDestroy()
    {
        if (IsHolding)
        {
            IsHolding = false;
            if (player != null) player.SetHeld(false);
        }

        if (_overlay != null) Destroy(_overlay);
    }
}

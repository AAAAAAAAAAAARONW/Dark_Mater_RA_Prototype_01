using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach directly to a World Space Canvas, or to any Image / child
/// GameObject inside one. Controls fade-in/out by player proximity.
///
/// Fade target priority:
///   1. CanvasGroup on this GameObject or its parent Canvas  (preferred)
///   2. Image component on this GameObject                   (fallback)
///
/// "Passed" detection:
///   Once the player has approached and then moves BEHIND the panel
///   (negative dot with the panel's forward axis), the UI hides permanently.
///   Rotate the GameObject so its blue Z-axis (+forward) points in the
///   direction the player is travelling.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class WorldSpaceUIPopup : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // INSPECTOR
    // ─────────────────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("Leave blank — auto-finds DarkMatterPlayerController in the scene.")]
    [SerializeField] Transform player;

    [Header("Trigger")]
    [Tooltip("Sphere radius: UI becomes visible when player enters this range.")]
    [SerializeField] float triggerDistance = 30f;

    [Tooltip("How far behind the panel the player must travel before it hides.\n" +
             "Small buffer prevents flickering right at the crossing point.")]
    [SerializeField] float passBehindBuffer = 3f;
    [Tooltip("Flip pass-behind axis when this panel's forward is opposite to travel direction.")]
    [SerializeField] bool invertForward = false;

    [Header("Fade")]
    [SerializeField] float fadeInDuration = 0.4f;
    [SerializeField] float fadeOutDuration = 0.25f;

    [Header("Audio")]
    [Tooltip("Clip played when the UI pops up. Leave null for silence.")]
    [SerializeField] AudioClip popupClip;

    [Range(0f, 1f)]
    [SerializeField] float volume = 1f;

    [Header("Debug")]
    [Tooltip("Prints distance + positions to Console every second. Turn off after diagnosing.")]
    [SerializeField] bool debugMode = false;

    // ─────────────────────────────────────────────────────────────────────────
    // PRIVATE STATE
    // ─────────────────────────────────────────────────────────────────────────

    AudioSource _audio;
    CanvasGroup _group;
    Graphic[] _graphics;
    bool _visible;
    bool _passed;
    Coroutine _fade;
    float _debugTimer;
    bool _warnedInRange;
    bool _warnedAwaitPass;

    // ─────────────────────────────────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        _audio = GetComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 1f; // 3D positional audio

        // Try CanvasGroup on self, then walk up to the root Canvas
        _group = GetComponent<CanvasGroup>();
        if (_group == null)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                _group = canvas.GetComponent<CanvasGroup>();
        }

        // Fallback: collect all Graphic components (Image, Text, RawImage…)
        // on this GameObject and its children so we can fade their alpha.
        if (_group == null)
            _graphics = GetComponentsInChildren<Graphic>(includeInactive: true);

        SetAlpha(0f);
    }

    void Start()
    {
        if (player != null) return;

        Debug.LogError(
            $"[WorldSpaceUIPopup] '{name}': player is not assigned. " +
            "Please drag the player Transform into the 'player' field in Inspector. " +
            "Component is disabled to avoid silent non-trigger behavior.",
            this
        );
        enabled = false;
    }

    void Update()
    {
        if (player == null || _passed) return;

        Vector3 toPlayer = player.position - transform.position;
        float dist = toPlayer.magnitude;

        // dot < 0  → player is behind the panel (has passed it)
        Vector3 panelForward = GetPanelForward();
        float dot = Vector3.Dot(panelForward, toPlayer.normalized);

        if (debugMode)
        {
            _debugTimer -= Time.deltaTime;
            if (_debugTimer <= 0f)
            {
                _debugTimer = 1f;
                Debug.Log($"[WorldSpaceUIPopup] '{name}' | " +
                          $"UIPos={transform.position:F1}  " +
                          $"PlayerPos={player.position:F1}  " +
                          $"dist={dist:F1}  triggerDist={triggerDistance}  " +
                          $"dot={dot:F3}  invertForward={invertForward}  " +
                          $"visible={_visible}  passed={_passed}", this);
            }
        }

        if (!_visible && dist <= triggerDistance)
        {
            if (debugMode)
                Debug.Log($"[WorldSpaceUIPopup] '{name}' -> Show (entered trigger). dist={dist:F2}", this);
            Show();
            _warnedInRange = false;
        }
        else if (_visible && dot < 0f && dist > passBehindBuffer)
        {
            if (debugMode)
                Debug.Log($"[WorldSpaceUIPopup] '{name}' -> HidePermanently (passed behind). dot={dot:F3}, dist={dist:F2}", this);
            HidePermanently();
            _warnedAwaitPass = false;
        }
        else if (debugMode)
        {
            if (!_visible && dist > triggerDistance)
            {
                _warnedInRange = false;
            }
            else if (!_visible && dist <= triggerDistance && !_warnedInRange)
            {
                Debug.Log($"[WorldSpaceUIPopup] '{name}' is in trigger range but currently hidden. Waiting for Show path.", this);
                _warnedInRange = true;
            }

            if (_visible && !(dot < 0f && dist > passBehindBuffer) && !_warnedAwaitPass)
            {
                Debug.Log(
                    $"[WorldSpaceUIPopup] '{name}' is visible. Waiting pass-behind condition: dot<0 and dist>{passBehindBuffer:F2}. " +
                    $"Current dot={dot:F3}, dist={dist:F2}.",
                    this
                );
                _warnedAwaitPass = true;
            }
            else if (_visible && (dot < 0f && dist > passBehindBuffer))
            {
                _warnedAwaitPass = false;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SHOW / HIDE
    // ─────────────────────────────────────────────────────────────────────────

    void Show()
    {
        _visible = true;
        if (popupClip != null)
            _audio.PlayOneShot(popupClip, volume);
        StartFade(1f, fadeInDuration);
    }

    void HidePermanently()
    {
        _visible = false;
        _passed = true;
        StartFade(0f, fadeOutDuration);
    }

    public void ForceShow() => Show();
    public void ForceHide() => HidePermanently();
    public void ResetPassedState() { _passed = false; _visible = false; }

    // ─────────────────────────────────────────────────────────────────────────
    // FADE
    // ─────────────────────────────────────────────────────────────────────────

    void StartFade(float target, float duration)
    {
        if (_fade != null) StopCoroutine(_fade);
        _fade = StartCoroutine(FadeRoutine(target, duration));
    }

    IEnumerator FadeRoutine(float target, float duration)
    {
        float start = GetAlpha();
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(Mathf.Lerp(start, target, elapsed / duration));
            yield return null;
        }

        SetAlpha(target);
    }

    // ── Alpha read / write ───────────────────────────────────────────────────

    float GetAlpha()
    {
        if (_group != null) return _group.alpha;
        if (_graphics != null && _graphics.Length > 0) return _graphics[0].color.a;
        return 0f;
    }

    void SetAlpha(float a)
    {
        if (_group != null)
        {
            _group.alpha = a;
            _group.interactable = a > 0f;
            _group.blocksRaycasts = a > 0f;
            return;
        }

        if (_graphics != null)
        {
            foreach (var g in _graphics)
            {
                var c = g.color;
                c.a = a;
                g.color = c;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // EDITOR GIZMOS
    // ─────────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Vector3 panelForward = GetPanelForward();

        Gizmos.color = new Color(0f, 1f, 1f, 0.15f);
        Gizmos.DrawSphere(transform.position, triggerDistance);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, triggerDistance);

        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, panelForward * triggerDistance * 0.5f);

        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, passBehindBuffer);

        if (player != null)
        {
            Vector3 toPlayer = player.position - transform.position;
            float dot = Vector3.Dot(panelForward, toPlayer.normalized);
            Gizmos.color = dot >= 0f ? Color.green : Color.red;
            Gizmos.DrawLine(transform.position, player.position);

            Vector3 mid = transform.position + toPlayer * 0.5f;
            Gizmos.DrawWireSphere(mid, 0.25f);
        }
    }

    void OnValidate()
    {
        triggerDistance = Mathf.Max(0.1f, triggerDistance);
        passBehindBuffer = Mathf.Max(0f, passBehindBuffer);
        fadeInDuration = Mathf.Max(0.01f, fadeInDuration);
        fadeOutDuration = Mathf.Max(0.01f, fadeOutDuration);
        volume = Mathf.Clamp01(volume);
    }
#endif

    Vector3 GetPanelForward()
    {
        return invertForward ? -transform.forward : transform.forward;
    }
}

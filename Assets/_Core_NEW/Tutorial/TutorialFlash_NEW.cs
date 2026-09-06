using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A full-screen colour flash. One call, one flash.
///
/// C2 is "near blow-out white" and C3 opens on "one white frame", so Phase 2 needs this
/// twice with different durations. Phase 3's D2 will want it again, dimmer, on the
/// absorption impact — which is why this takes its colour and timing from the call site
/// rather than knowing anything about emission.
///
/// Separate from TutorialFadeIn_NEW even though both drive a full-screen Image, because
/// they are different shapes of the same idea: the fade holds and then reveals once at
/// the start of the piece, this punches and decays any number of times. Merging them
/// would produce one component with two modes and two sets of unused fields.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("FLASH", "#E8E8F0")]
public class TutorialFlash_NEW : MonoBehaviour
{
    [Header("Wiring")]
    [Tooltip("Full-screen Image. Leave empty to use one on this object.")]
    [SerializeField] Image screen;

    [Header("Default flash")]
    [Tooltip("Used by the no-argument Flash(), which is what a UnityEvent can call.")]
    [SerializeField] Color defaultColor = Color.white;

    [SerializeField] float defaultHoldSeconds = 0.05f;
    [SerializeField] float defaultDecaySeconds = 0.5f;

    [Tooltip("Shape of the decay. X is normalised time, Y is how much flash remains.")]
    [SerializeField] AnimationCurve decay = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    Color _color;
    float _hold;
    float _decaySeconds;
    float _elapsed;
    bool _running;

    void Awake()
    {
        if (screen == null) screen = GetComponent<Image>();

        if (screen == null)
            Debug.LogError("[TutorialFlash_NEW] No Image to flash.", this);

        SetAlpha(0f);
    }

    /// <summary>Flash with the serialized defaults. Wire this to a beat's onEnter.</summary>
    public void Flash()
    {
        Flash(defaultColor, defaultHoldSeconds, defaultDecaySeconds);
    }

    /// <summary>Flash with explicit values, for a caller that wants its own timing.</summary>
    public void Flash(Color color, float holdSeconds, float decaySeconds)
    {
        _color = color;
        _hold = Mathf.Max(0f, holdSeconds);
        _decaySeconds = Mathf.Max(0f, decaySeconds);
        _elapsed = 0f;
        _running = true;

        SetAlpha(1f);
    }

    /// <summary>Cut it short. The attract reset uses this.</summary>
    public void Clear()
    {
        _running = false;
        SetAlpha(0f);
    }

    void Update()
    {
        if (!_running) return;

        _elapsed += Time.unscaledDeltaTime;

        if (_elapsed < _hold)
        {
            SetAlpha(1f);
            return;
        }

        if (_decaySeconds <= 0f)
        {
            Clear();
            return;
        }

        float t = Mathf.Clamp01((_elapsed - _hold) / _decaySeconds);
        SetAlpha(decay.Evaluate(t));

        if (t >= 1f) Clear();
    }

    void SetAlpha(float alpha)
    {
        if (screen == null) return;

        Color c = _color;
        c.a = Mathf.Clamp01(alpha);
        screen.color = c;

        // A transparent full-screen image is still a screen of overdraw on the curved
        // display. Switch it off when it has nothing to show.
        bool needed = c.a > 0.001f;
        if (screen.enabled != needed) screen.enabled = needed;
    }
}

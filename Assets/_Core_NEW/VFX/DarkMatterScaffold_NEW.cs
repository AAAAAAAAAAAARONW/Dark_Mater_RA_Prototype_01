using System;
using UnityEngine;

/// <summary>
/// Dark matter, drawn: shows and hides the scaffold under the first cosmic web's gas.
///
/// WHAT IS ON SCREEN. One volume (Custom/DarkMatterScaffold_NEW) baked from the web's own
/// volumes by Tools > Journey NEW > Dark Matter > Build Dark Matter Stop: the same shape as
/// the gas, blurred wider and softer, in one colour. It sits in the Macro render group, so
/// it arrives, dives and dissolves with the web like any other part of it.
///
/// WHAT THIS DOES. Owns one number, the reveal (0 hidden, 1 shown), and eases it toward
/// the state asked for. The shader reads it as a global (_DarkMatterReveal) and grows the
/// scaffold out of the densest threads rather than fading it evenly, so showing it reads
/// as "this was under the gas all along" and not as a second web switching on.
///
/// WHO ASKS. DarkMatterStop_NEW: the default state on entering the layer (version A shown,
/// version B hidden), then the visitor's Y. Nothing here reads input, so the toggle is
/// taught by the stop and nowhere else decides what Y means.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("DARK MATTER", "#5C8CFF")]
public class DarkMatterScaffold_NEW : MonoBehaviour
{
    static readonly int RevealId = Shader.PropertyToID("_DarkMatterReveal");

    [Tooltip("Seconds for a full show or hide. Long enough to watch it grow out of the " +
             "threads; short enough that the press and the picture still read as cause and " +
             "effect.")]
    [Min(0.05f)]
    [SerializeField] float revealSeconds = 2.5f;

    [Tooltip("Shown when the scene starts. DarkMatterStop_NEW sets the real default on " +
             "entering the first cosmic web; this only decides the frames before that.")]
    [SerializeField] bool startShown = false;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    bool _shown;
    float _reveal;
    float _from;
    float _t = 1f;

    /// <summary>The state asked for (not where the reveal is right now).</summary>
    public bool IsShown { get { return _shown; } }

    /// <summary>0 hidden … 1 shown, as drawn this frame.</summary>
    public float Reveal { get { return _reveal; } }

    /// <summary>Fired when the state asked for changes, with the new state.</summary>
    public event Action<bool> OnChanged;

    /// <summary>Show or hide, easing over revealSeconds. From wherever it is now.</summary>
    public void Show(bool shown)
    {
        if (shown == _shown) return;

        _shown = shown;
        _from = _reveal;
        _t = 0f;

        if (debugLog) Debug.Log("[DarkMatterScaffold_NEW] " + (shown ? "Show" : "Hide") + ".", this);
        if (OnChanged != null) OnChanged(shown);
    }

    /// <summary>The other state. What Y does.</summary>
    public void Toggle() { Show(!_shown); }

    /// <summary>Jump straight to a state, no easing. For a layer's default, set unseen.</summary>
    public void SetImmediate(bool shown)
    {
        bool changed = shown != _shown;

        _shown = shown;
        _reveal = shown ? 1f : 0f;
        _from = _reveal;
        _t = 1f;
        Shader.SetGlobalFloat(RevealId, _reveal);

        if (changed && OnChanged != null) OnChanged(shown);
    }

    void OnEnable()
    {
        SetImmediate(startShown);
    }

    void OnDisable()
    {
        // A scene without the scaffold draws none of it, rather than whatever was last set.
        Shader.SetGlobalFloat(RevealId, 0f);
    }

    void Update()
    {
        if (_t >= 1f) return;

        _t = Mathf.Min(1f, _t + Time.deltaTime / revealSeconds);
        float k = _t * _t * (3f - 2f * _t);
        _reveal = Mathf.Lerp(_from, _shown ? 1f : 0f, k);

        Shader.SetGlobalFloat(RevealId, _reveal);
    }
}

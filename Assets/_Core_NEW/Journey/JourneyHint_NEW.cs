using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The journey's control hint: one line of words on a dark plate, e.g. "Y · SHOW DARK
/// MATTER". The tutorial's hint, for the one place the journey asks for a button.
///
/// The journey otherwise has no hint line, because it asks for nothing. DarkMatterStop_NEW
/// is the exception, and it is the only caller: it writes the words, so the letter is
/// always the one printed on the pad that is plugged in (InputScheme_NEW.ToggleGlyph).
///
/// Off by default. A plate in the scene is not a reason for it to be on screen; a stop
/// asking for something is. Fades in and out rather than popping, at the same pace as the
/// tutorial's hint.
///
/// The dark matter toggle also has a drawn version (PRESS Y - TOGGLE DARK MATTER,
/// UI/Hint_ToggleDarkMatter.png). ShowArt puts that up in place of the plate and the words
/// when it is assigned and the pad's toggle letter is the Y it has drawn on it; anywhere
/// else (another pad, no art) it falls back to the words, so the letter is never wrong.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
[HierarchyBadge_NEW("HINT", "#E2B36A")]
public class JourneyHint_NEW : MonoBehaviour
{
    [Tooltip("The line. Found in the children if empty.")]
    [SerializeField] TMP_Text label;

    [Tooltip("The drawn prompt. Optional; Tools > Journey NEW > Dark Matter Hint Art adds it.")]
    [SerializeField] Image art;

    [Tooltip("The letter drawn on the art. The art is only used when the pad's toggle " +
             "letter is this one.")]
    [SerializeField] string artGlyph = "Y";

    [Tooltip("The dark plate behind the words. Hidden while the art is up.")]
    [SerializeField] Graphic plate;

    [Tooltip("Seconds to fade in or out.")]
    [Min(0f)]
    [SerializeField] float fadeSeconds = 0.4f;

    CanvasGroup _group;
    float _target;

    /// <summary>Put these words up, or change them if the hint is already up.</summary>
    public void Show(string text)
    {
        Init();
        UseArt(false);
        if (label != null) label.text = text;
        _target = 1f;
        enabled = true;
    }

    /// <summary>
    /// The drawn prompt if there is one for this pad, otherwise these words. The words are
    /// kept either way, so Text still says what the hint means.
    /// </summary>
    public void ShowArt(string fallbackText)
    {
        Init();
        if (label != null) label.text = fallbackText;
        UseArt(art != null && art.sprite != null && InputScheme_NEW.ToggleGlyph() == artGlyph);
        _target = 1f;
        enabled = true;
    }

    /// <summary>True while the drawn prompt, not the words, is what is up.</summary>
    public bool ShowingArt { get { return art != null && art.enabled; } }

    void UseArt(bool on)
    {
        if (art != null) art.enabled = on;
        if (plate != null) plate.enabled = !on;
        if (label != null) label.enabled = !on;
    }

    /// <summary>Fade the hint out.</summary>
    public void Hide()
    {
        Init();
        _target = 0f;
        enabled = true;
    }

    /// <summary>True while the hint is up or on its way up.</summary>
    public bool IsShown { get { return _target > 0f; } }

    /// <summary>The words on the line now. JourneyPause_NEW puts them back after a pause.</summary>
    public string Text
    {
        get { Init(); return label != null ? label.text : ""; }
    }

    void Init()
    {
        if (_group != null) return;

        _group = GetComponent<CanvasGroup>();
        if (label == null) label = GetComponentInChildren<TMP_Text>(true);
    }

    void Awake()
    {
        Init();
        _group.alpha = 0f;
        _group.interactable = false;
        _group.blocksRaycasts = false;
        _target = 0f;
    }

    void Update()
    {
        // Unscaled: the pause stops the time scale and still has to put "B TO RESUME" up.
        float step = fadeSeconds > 0f ? Time.unscaledDeltaTime / fadeSeconds : 1f;
        _group.alpha = Mathf.MoveTowards(_group.alpha, _target, step);

        if (Mathf.Approximately(_group.alpha, _target)) enabled = false;
    }
}

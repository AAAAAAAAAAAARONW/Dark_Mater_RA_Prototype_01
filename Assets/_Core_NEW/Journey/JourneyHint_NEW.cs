using TMPro;
using UnityEngine;

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
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
[HierarchyBadge_NEW("HINT", "#E2B36A")]
public class JourneyHint_NEW : MonoBehaviour
{
    [Tooltip("The line. Found in the children if empty.")]
    [SerializeField] TMP_Text label;

    [Tooltip("Seconds to fade in or out.")]
    [Min(0f)]
    [SerializeField] float fadeSeconds = 0.4f;

    CanvasGroup _group;
    float _target;

    /// <summary>Put these words up, or change them if the hint is already up.</summary>
    public void Show(string text)
    {
        Init();
        if (label != null) label.text = text;
        _target = 1f;
        enabled = true;
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
        float step = fadeSeconds > 0f ? Time.deltaTime / fadeSeconds : 1f;
        _group.alpha = Mathf.MoveTowards(_group.alpha, _target, step);

        if (Mathf.Approximately(_group.alpha, _target)) enabled = false;
    }
}

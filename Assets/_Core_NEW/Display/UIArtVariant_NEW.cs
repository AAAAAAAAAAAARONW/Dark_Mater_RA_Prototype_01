using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One piece of HUD art that the artist delivered in more than one version, with the
/// versions switchable while the piece is running.
///
/// WHY: the art for this wall arrives as sets, not as single files - the journey tracker
/// came as two horizontal layouts and a vertical one, each at 60% and 80% panel opacity.
/// Which one to keep is a judgement about legibility over moving imagery at 2 m on a
/// curved wall, and that judgement cannot be made from a file browser or from a Figma
/// frame. It has to be made in place, by flipping between them on the wall itself and
/// looking. So the versions ship together in the scene and a button swaps them, rather
/// than one being picked in the editor and the rest being argued about from memory.
///
/// WHAT IT IS: a list of sprites for one Image, plus which one is showing. Nothing else.
/// The art is a still, so a tracker swapped in this way does not move with the journey -
/// this is for judging the look, not for wiring the HUD up.
///
/// It registers itself in a static list so UIArtVariantPanel_NEW can draw one panel for
/// every piece in the scene without being wired to any of them.
///
/// Switching is also available from the component's context menu and from these public
/// methods, so a debug menu or another script can drive it.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
[HierarchyBadge_NEW("ART", "#B478FF")]
public class UIArtVariant_NEW : MonoBehaviour
{
    [Serializable]
    public class Variant
    {
        [Tooltip("What the button says. Keep it short: '80%', 'H2 60%'.")]
        public string label = "";

        public Sprite sprite;
    }

    [Header("What this is")]
    [Tooltip("The name on the panel. The object's name is used if this is empty.")]
    [SerializeField] string title = "";

    [Tooltip("The Image to swap. This object's own Image if left empty.")]
    [SerializeField] Image target;

    [Header("Versions")]
    [SerializeField] Variant[] variants = new Variant[0];

    [Tooltip("Which one to show at start, counting from 0.")]
    [SerializeField] int startVariant = 0;

    [Tooltip("Whether the art is on screen at start. Off is useful for the alternative " +
             "layout of the same element, so the panel can bring it in for a comparison.")]
    [SerializeField] bool startVisible = true;

    [Header("Look")]
    [Tooltip("Fit the sprite inside its rectangle without distorting it. Keep this on: " +
             "these assets are not the shape of a display row to the pixel.")]
    [SerializeField] bool preserveAspect = true;

    [Tooltip("Optional. Cycles this element's versions on its own, without the panel.")]
    [SerializeField] KeyCode cycleKey = KeyCode.None;

    [SerializeField] bool logChanges = true;

    const string LogPrefix = "[UIArtVariant_NEW] ";

    /// <summary>Every enabled piece of variant art, in the order they woke up. The panel reads this.</summary>
    static readonly List<UIArtVariant_NEW> _active = new List<UIArtVariant_NEW>();
    public static IList<UIArtVariant_NEW> Active { get { return _active; } }

    int _current;

    public string Title { get { return string.IsNullOrEmpty(title) ? name : title; } }
    public int Count { get { return variants != null ? variants.Length : 0; } }
    public int Current { get { return _current; } }
    public bool IsVisible { get { return target != null && target.enabled; } }

    /// <summary>The row anchor on this object, if it has one, so the panel can offer row buttons.</summary>
    public VizlabRowAnchor_NEW Row { get { return GetComponent<VizlabRowAnchor_NEW>(); } }

    public string LabelOf(int index)
    {
        if (index < 0 || index >= Count) return "-";
        Variant v = variants[index];
        return !string.IsNullOrEmpty(v.label) ? v.label
             : v.sprite != null ? v.sprite.name
             : "(empty)";
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void OnEnable()
    {
        if (target == null) target = GetComponent<Image>();
        if (!_active.Contains(this)) _active.Add(this);

        _current = Mathf.Clamp(startVariant, 0, Mathf.Max(0, Count - 1));
        Apply();
        SetVisible(startVisible, false);
    }

    void OnDisable()
    {
        _active.Remove(this);
    }

    void Update()
    {
        if (!Application.isPlaying || cycleKey == KeyCode.None) return;
        if (Input.GetKeyDown(cycleKey)) Next();
    }

    // ── Switching ────────────────────────────────────────────────────────────

    [ContextMenu("Next version")]
    public void Next()
    {
        if (Count == 0) return;
        Select((_current + 1) % Count);
    }

    public void Select(int index)
    {
        if (Count == 0 || index < 0 || index >= Count) return;

        _current = index;
        Apply();
        SetVisible(true, false);

        if (logChanges)
            Debug.Log(LogPrefix + Title + " -> " + LabelOf(index) + " (" +
                      (variants[index].sprite != null ? variants[index].sprite.name : "no sprite") + ")", this);
    }

    [ContextMenu("Show / hide")]
    public void ToggleVisible()
    {
        SetVisible(!IsVisible, true);
    }

    public void SetVisible(bool visible, bool log)
    {
        if (target == null) return;

        // The Image is disabled rather than the object, so the component stays registered
        // with the panel and can be brought back from it.
        target.enabled = visible;

        if (log && logChanges) Debug.Log(LogPrefix + Title + (visible ? " shown" : " hidden"), this);
    }

    /// <summary>Puts the current sprite on the Image. Safe to call repeatedly.</summary>
    [ContextMenu("Re-apply")]
    public void Apply()
    {
        if (target == null) target = GetComponent<Image>();
        if (target == null || Count == 0) return;

        _current = Mathf.Clamp(_current, 0, Count - 1);
        target.sprite = variants[_current].sprite;
        target.preserveAspect = preserveAspect;

        // White, so the sprite's own colours are what shows.
        target.color = new Color(1f, 1f, 1f, target.color.a <= 0f ? 1f : target.color.a);
    }

    /// <summary>Moves this element a display row, for the panel's row buttons. Negative is up.</summary>
    public void StepRow(int step)
    {
        VizlabRowAnchor_NEW row = Row;
        if (row == null) return;

        row.Row = row.Row + step;
        if (logChanges) Debug.Log(LogPrefix + Title + " -> row " + row.Row, this);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        startVariant = Mathf.Clamp(startVariant, 0, Mathf.Max(0, Count - 1));

        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || !isActiveAndEnabled) return;
            _current = Mathf.Clamp(startVariant, 0, Mathf.Max(0, Count - 1));
            Apply();
        };
    }
#endif
}

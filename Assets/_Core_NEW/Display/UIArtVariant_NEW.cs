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
/// WHAT IT IS: a list of sprites for one Image, which one is showing, and how big it is.
/// The art is a still, so a tracker swapped in this way does not move with the journey -
/// this is for judging the look, not for wiring the HUD up.
///
/// SIZE: the panel scales the element while the build runs, and reports the height it
/// comes to on the wall in millimetres, because "how big should this be" is answered at
/// 2 m from a curved wall and not in a layout view. The size found is remembered for the
/// next launch. As nothing here needs an Image or any versions, dropping this on any UI
/// element with an empty version list makes it a size handle for that element.
///
/// It registers itself in a static list so UIArtVariantPanel_NEW can draw one panel for
/// every piece in the scene without being wired to any of them.
///
/// Switching is also available from the component's context menu and from these public
/// methods, so a debug menu or another script can drive it.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
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

    [Header("Size")]
    [Tooltip("A multiplier on this element's size, changed from the panel while the piece " +
             "runs. 1 is the size the layout gives it.")]
    [Range(0.2f, 3f)]
    [SerializeField] float scale = 1f;

    [Tooltip("How much one press of - or + moves it.")]
    [Range(0.01f, 0.25f)]
    [SerializeField] float scaleStep = 0.05f;

    [Tooltip("Keep the size found on the wall for the next launch, so a session of sizing " +
             "does not have to be repeated. Reset clears it.")]
    [SerializeField] bool rememberScale = true;

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

    /// <summary>With no Image to switch off, visibility is the object itself.</summary>
    public bool IsVisible { get { return target != null ? target.enabled : gameObject.activeSelf; } }

    /// <summary>The size multiplier in effect, 1 being the size the layout gives it.</summary>
    public float Scale { get { return scale; } }

    RectTransform Rt { get { return (RectTransform)transform; } }

    /// <summary>
    /// How tall this element is on the wall, in millimetres. The screen's height is the
    /// wall's height whatever the aspect ratio in use, so the fraction of the screen the
    /// element covers is the fraction of the wall's 4027 mm it would cover - which is the
    /// number to judge a size against, rather than a pixel count that means nothing at 2 m.
    /// </summary>
    public float WallHeightMm
    {
        get
        {
            if (Screen.height <= 0) return 0f;
            float heightPx = Rt.rect.height * Rt.lossyScale.y;
            return heightPx / Screen.height * VizlabDisplay_NEW.RowHeightMm * VizlabDisplay_NEW.RowCount;
        }
    }

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

        // A size found on the wall last time beats the one saved in the scene.
        if (rememberScale && Application.isPlaying)
            scale = Mathf.Clamp(PlayerPrefs.GetFloat(ScalePref, scale), 0.2f, 3f);

        Apply();
        ApplyScale();
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
        // The Image is disabled rather than the object, so the component stays registered
        // with the panel and can be brought back from it. With no Image - this component
        // used as a size handle on somebody else's UI - the children are hidden instead,
        // which leaves this object, and so the registration, alone.
        if (target != null) target.enabled = visible;
        else foreach (Transform child in transform) child.gameObject.SetActive(visible);

        if (log && logChanges) Debug.Log(LogPrefix + Title + (visible ? " shown" : " hidden"), this);
    }

    // ── Size ─────────────────────────────────────────────────────────────────

    string ScalePref { get { return "Vizlab.UIScale." + Title; } }

    /// <summary>One press of the panel's - or +. Negative shrinks.</summary>
    public void StepScale(int direction)
    {
        SetScale(scale + direction * scaleStep);
    }

    public void SetScale(float value)
    {
        scale = Mathf.Clamp(value, 0.2f, 3f);
        ApplyScale();

        if (rememberScale && Application.isPlaying) PlayerPrefs.SetFloat(ScalePref, scale);

        if (logChanges)
            Debug.Log(string.Format("{0}{1} size {2:0}% ({3:0} mm tall on the wall)",
                                    LogPrefix, Title, scale * 100f, WallHeightMm), this);
    }

    [ContextMenu("Size: back to 100%")]
    public void ResetScale()
    {
        if (rememberScale && Application.isPlaying) PlayerPrefs.DeleteKey(ScalePref);
        SetScale(1f);
    }

    void ApplyScale()
    {
        // localScale rather than the rect, so it works the same whether the element is
        // sized by a row anchor, by its sprite or by hand, and so nothing it contains has
        // to be laid out again.
        Rt.localScale = new Vector3(scale, scale, 1f);
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
            ApplyScale();
        };
    }
#endif
}

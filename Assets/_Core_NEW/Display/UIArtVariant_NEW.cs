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
/// TUNING, all of it continuous, from sliders on the panel while the build runs: size,
/// which reports the height it comes to on the wall in millimetres; opacity, which goes
/// between and past the fixed 60% and 80% the art was exported at; and height, in
/// millimetres up or down the wall, for the position between two rows that a row number
/// cannot express. None of those questions has a natural step size - they are answered at
/// 2 m from a curved wall by dragging until it looks right - so none of them is stepped.
/// What is found is remembered for the next launch.
///
/// As nothing here needs an Image or any versions, dropping this on any UI element with an
/// empty version list makes it a tuning handle for that element.
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

    [Header("Live adjustment")]
    [Tooltip("A multiplier on this element's size. The panel's slider drives it while the " +
             "piece runs; 1 is the size the layout gives it.")]
    [Range(MinScale, MaxScale)]
    [SerializeField] float scale = 1f;

    [Tooltip("How solid the element is, 1 being as the artist drew it. The delivered art " +
             "comes at fixed 60% and 80% panel opacities; this dials the whole element " +
             "instead, so a value between them can be found before asking for another export.")]
    [Range(0f, 1f)]
    [SerializeField] float opacity = 1f;

    [Tooltip("Moves the element up and down in millimetres on the wall, for the height " +
             "between two display rows that a row number cannot express.")]
    [Range(-ShiftRangeMm, ShiftRangeMm)]
    [SerializeField] float shiftMm = 0f;

    [Tooltip("Keep the size, opacity and height found on the wall for the next launch, so a " +
             "session of tuning does not have to be repeated. Reset clears them.")]
    [SerializeField] bool remember = true;

    [Header("Look")]
    [Tooltip("Fit the sprite inside its rectangle without distorting it. Keep this on: " +
             "these assets are not the shape of a display row to the pixel.")]
    [SerializeField] bool preserveAspect = true;

    [Tooltip("Optional. Cycles this element's versions on its own, without the panel.")]
    [SerializeField] KeyCode cycleKey = KeyCode.None;

    [SerializeField] bool logChanges = true;

    const string LogPrefix = "[UIArtVariant_NEW] ";

    public const float MinScale = 0.2f, MaxScale = 3f;

    /// <summary>How far up or down the shift slider reaches: one display row either way.</summary>
    public const float ShiftRangeMm = VizlabDisplay_NEW.RowHeightMm;

    /// <summary>Every enabled piece of variant art, in the order they woke up. The panel reads this.</summary>
    static readonly List<UIArtVariant_NEW> _active = new List<UIArtVariant_NEW>();
    public static IList<UIArtVariant_NEW> Active { get { return _active; } }

    int _current;

    /// <summary>Where the layout put this element, before the shift slider moved it.</summary>
    Vector2 _basePosition;

    const float LogSettleSeconds = 0.4f;
    string _pendingLog;
    float _pendingLogTime;

    /// <summary>The row the base position belongs to, so a row change re-reads it.</summary>
    int _baseRow = -2;

    public string Title { get { return string.IsNullOrEmpty(title) ? name : title; } }
    public int Count { get { return variants != null ? variants.Length : 0; } }
    public int Current { get { return _current; } }

    /// <summary>With no Image to switch off, visibility is the object itself.</summary>
    public bool IsVisible { get { return target != null ? target.enabled : gameObject.activeSelf; } }

    /// <summary>The size multiplier in effect, 1 being the size the layout gives it.</summary>
    public float Scale { get { return scale; } }

    /// <summary>How solid the element is, 0 to 1.</summary>
    public float Opacity { get { return opacity; } }

    /// <summary>How far the element is moved up or down the wall, in millimetres.</summary>
    public float ShiftMm { get { return shiftMm; } }

    RectTransform Rt { get { return (RectTransform)transform; } }

    /// <summary>Canvas units per millimetre of wall, for turning the shift into a position.</summary>
    float UnitsPerMm
    {
        get
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return 0f;

            var canvasRt = canvas.rootCanvas.transform as RectTransform;
            if (canvasRt == null || canvasRt.rect.height <= 0f) return 0f;

            // The canvas's height is the wall's height, so one is the other's scale.
            float parentScale = Rt.parent != null ? Rt.parent.lossyScale.y / canvasRt.lossyScale.y : 1f;
            if (parentScale <= 0f) parentScale = 1f;

            return canvasRt.rect.height / (VizlabDisplay_NEW.RowHeightMm * VizlabDisplay_NEW.RowCount) / parentScale;
        }
    }

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

        // What was found on the wall last time beats what the scene was saved with.
        if (remember && Application.isPlaying)
        {
            scale = Mathf.Clamp(PlayerPrefs.GetFloat(Pref("Scale"), scale), MinScale, MaxScale);
            opacity = Mathf.Clamp01(PlayerPrefs.GetFloat(Pref("Opacity"), opacity));
            shiftMm = Mathf.Clamp(PlayerPrefs.GetFloat(Pref("Shift"), shiftMm), -ShiftRangeMm, ShiftRangeMm);
        }

        // Taken before the shift is re-applied, and with any shift already in the saved
        // position taken back out, so it cannot accumulate across a save and a reload.
        VizlabRowAnchor_NEW row = Row;
        _baseRow = row != null ? row.Row : -1;
        _basePosition = Rt.anchoredPosition - new Vector2(0f, shiftMm * UnitsPerMm);

        Apply();
        ApplyScale();
        ApplyOpacity();
        ApplyShift();
        SetVisible(startVisible, false);
    }

    void LateUpdate()
    {
        FlushPendingLog();

        // A row change re-places the element, which wipes the shift; take the new base and
        // put the shift back on top of it.
        VizlabRowAnchor_NEW row = Row;
        int current = row != null ? row.Row : -1;
        if (current == _baseRow) return;

        _baseRow = current;
        _basePosition = Rt.anchoredPosition;
        ApplyShift();
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

    // ── Live adjustment ──────────────────────────────────────────────────────
    //
    // Size, opacity and height are all continuous: the panel drives them from sliders, and
    // these setters take any value in range rather than a step, because the question they
    // answer ("how big does this have to be to read at 2 m") has no natural step size.
    // Each is written as it is dragged, so what is on the wall is what the value says.

    string Pref(string what) { return "Vizlab.UI." + what + "." + Title; }

    public void SetScale(float value)
    {
        value = Mathf.Clamp(value, MinScale, MaxScale);
        if (Mathf.Approximately(value, scale)) return;

        scale = value;
        ApplyScale();
        Remember("Scale", scale);
        Report(string.Format("size {0:0.#}% ({1:0} mm tall on the wall)", scale * 100f, WallHeightMm));
    }

    public void SetOpacity(float value)
    {
        value = Mathf.Clamp01(value);
        if (Mathf.Approximately(value, opacity)) return;

        opacity = value;
        ApplyOpacity();
        Remember("Opacity", opacity);
        Report(string.Format("opacity {0:0}%", opacity * 100f));
    }

    public void SetShiftMm(float value)
    {
        value = Mathf.Clamp(value, -ShiftRangeMm, ShiftRangeMm);
        if (Mathf.Approximately(value, shiftMm)) return;

        shiftMm = value;
        ApplyShift();
        Remember("Shift", shiftMm);
        Report(string.Format("shifted {0:+0;-0;0} mm", shiftMm));
    }

    /// <summary>A step, for a key binding or another script. The panel uses the sliders.</summary>
    public void StepScale(int direction)
    {
        SetScale(scale + direction * 0.05f);
    }

    [ContextMenu("Back to as laid out")]
    public void ResetAdjustments()
    {
        if (remember && Application.isPlaying)
        {
            PlayerPrefs.DeleteKey(Pref("Scale"));
            PlayerPrefs.DeleteKey(Pref("Opacity"));
            PlayerPrefs.DeleteKey(Pref("Shift"));
        }

        scale = 1f;
        opacity = 1f;
        shiftMm = 0f;
        ApplyScale();
        ApplyOpacity();
        ApplyShift();
        Report("back to as laid out");
    }

    void Remember(string what, float value)
    {
        if (remember && Application.isPlaying) PlayerPrefs.SetFloat(Pref(what), value);
    }

    /// <summary>
    /// Sliders change every frame they are dragged, and a line per frame would bury the
    /// console and the player log in a hundred entries for one decision. So the message
    /// waits until the dragging stops and only the value settled on is written.
    /// </summary>
    void Report(string message)
    {
        if (!logChanges) return;

        _pendingLog = message;
        _pendingLogTime = Time.unscaledTime;
    }

    void FlushPendingLog()
    {
        if (_pendingLog == null || Time.unscaledTime - _pendingLogTime < LogSettleSeconds) return;

        Debug.Log(LogPrefix + Title + " " + _pendingLog, this);
        _pendingLog = null;
    }

    void ApplyScale()
    {
        // localScale rather than the rect, so it works the same whether the element is
        // sized by a row anchor, by its sprite or by hand, and so nothing it contains has
        // to be laid out again.
        Rt.localScale = new Vector3(scale, scale, 1f);
    }

    void ApplyOpacity()
    {
        if (target != null)
        {
            Color c = target.color;
            c.a = opacity;
            target.color = c;
            return;
        }

        // No Image of its own - this is a handle on somebody else's UI - so the whole
        // subtree is faded through a CanvasGroup, which is what that component is for.
        var group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = opacity;
    }

    /// <summary>
    /// The shift rides on top of whatever placed the element. VizlabRowAnchor_NEW writes
    /// anchoredPosition when the row changes, so the base is re-read whenever that happens
    /// rather than being captured once and drifting.
    /// </summary>
    void ApplyShift()
    {
        float units = UnitsPerMm;
        if (units <= 0f) return;

        Rt.anchoredPosition = _basePosition + new Vector2(0f, shiftMm * units);
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

        // White, so the sprite's own colours are what shows; the opacity slider owns alpha.
        target.color = new Color(1f, 1f, 1f, opacity);
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
            ApplyOpacity();
            ApplyShift();
        };
    }
#endif
}

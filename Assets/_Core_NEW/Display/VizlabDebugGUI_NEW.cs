using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The look and the scale of the Vizlab debug panels, in one place, so the aspect buttons
/// and the blackout buttons read as one tool and click the same way.
///
/// SCALE: the panels are laid out in 1080p units and scaled by GUI.matrix to the rendered
/// height. That way they are the same share of the screen at a desk (1512 or 2160 tall)
/// as on the wall (7560 tall). IMGUI transforms mouse input through the same matrix, so
/// buttons stay clickable at any scale. Callers draw in the scaled space, whose size is
/// ScaledScreen.
///
/// Immediate-mode GUI rather than a uGUI canvas: it needs no scene objects and no
/// EventSystem, it draws on top of every canvas, and it matches LayerDebugJump_NEW's
/// readout, which the playtest already uses.
/// </summary>
public static class VizlabDebugGUI_NEW
{
    /// <summary>The height the panels are designed at.</summary>
    public const float DesignHeight = 1080f;

    const string PanelRowPref = "Vizlab.DebugPanelRow";
    static int _panelRow = -1;

    /// <summary>
    /// The display row the Vizlab panels hang from, 1 to 7, shared so they move together.
    /// Row 1 suits a desk, where the whole wall is on one monitor. On the wall itself row 1
    /// is overhead at 78 degrees, so the panels can be brought down to eye level. Remembered
    /// between launches, so the wall does not need setting up again every run.
    /// </summary>
    public static int PanelRow
    {
        get
        {
            if (_panelRow < 1) _panelRow = Mathf.Clamp(PlayerPrefs.GetInt(PanelRowPref, 1), 1, VizlabDisplay_NEW.RowCount);
            return _panelRow;
        }
        set
        {
            _panelRow = Mathf.Clamp(value, 1, VizlabDisplay_NEW.RowCount);
            PlayerPrefs.SetInt(PanelRowPref, _panelRow);
        }
    }

    /// <summary>
    /// Where a panel of this height should start, in the scaled space: the top of PanelRow,
    /// pulled up if needed so the panel stays on screen.
    /// </summary>
    public static float PanelTop(float scaledScreenHeight, float panelHeight, float margin)
    {
        float yMin, yMax;
        VizlabDisplay_NEW.GetRowAnchorFractions(PanelRow, out yMin, out yMax);

        // Row fractions count up from the bottom; GUI y counts down from the top.
        float top = (1f - yMax) * scaledScreenHeight + margin;
        return Mathf.Max(margin, Mathf.Min(top, scaledScreenHeight - panelHeight - margin));
    }

    static Texture2D _panelTex, _buttonTex, _buttonHoverTex, _buttonOnTex, _sliderBarTex, _sliderThumbTex;
    static GUIStyle _sliderBar, _sliderThumb;
    static GUIStyle _panel;
    static readonly Dictionary<int, GUIStyle> _labels = new Dictionary<int, GUIStyle>();
    static readonly Dictionary<int, GUIStyle> _richLabels = new Dictionary<int, GUIStyle>();
    static readonly Dictionary<int, GUIStyle> _buttons = new Dictionary<int, GUIStyle>();
    static readonly Dictionary<int, GUIStyle> _buttonsOn = new Dictionary<int, GUIStyle>();
    static readonly Dictionary<int, Font> _fonts = new Dictionary<int, Font>();

    static readonly Color Text = new Color(0.88f, 0.85f, 0.96f, 1f);
    static readonly Color TextOn = new Color(0.02f, 0.13f, 0.17f, 1f);

    /// <summary>
    /// Scales everything drawn after it to the rendered height. Returns the scale. Call at
    /// the top of OnGUI; the matrix only lasts for that OnGUI call.
    /// </summary>
    public static float BeginScaled(bool autoScale)
    {
        float scale = autoScale ? Mathf.Max(1f, Screen.height / DesignHeight) : 1f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        return scale;
    }

    /// <summary>The screen size in the scaled space BeginScaled set up.</summary>
    public static Vector2 ScaledScreen(float scale)
    {
        return new Vector2(Screen.width / scale, Screen.height / scale);
    }

    public static GUIStyle Panel
    {
        get
        {
            if (_panel == null || _panelTex == null)
            {
                _panelTex = Solid(new Color(0.05f, 0.03f, 0.11f, 0.82f));
                _panel = new GUIStyle(GUIStyle.none) { normal = { background = _panelTex } };
            }
            return _panel;
        }
    }

    public static GUIStyle Label(int fontSize)
    {
        GUIStyle style;
        if (_labels.TryGetValue(fontSize, out style) && style.font != null) return style;

        style = new GUIStyle(GUI.skin.label)
        {
            font = FontOf(fontSize),
            fontSize = fontSize,
            richText = false,
            wordWrap = false,
            normal = { textColor = Text }
        };
        _labels[fontSize] = style;
        return style;
    }

    /// <summary>A label that reads &lt;color&gt; tags, for readings that should turn green or red.</summary>
    public static GUIStyle RichLabel(int fontSize)
    {
        GUIStyle style;
        if (_richLabels.TryGetValue(fontSize, out style) && style.font != null) return style;

        style = new GUIStyle(Label(fontSize)) { richText = true };
        _richLabels[fontSize] = style;
        return style;
    }

    public const string Good = "#59F28C";
    public const string Warn = "#FFC056";
    public const string Bad = "#FF6D84";

    public static string Colour(string text, string hex)
    {
        return "<color=" + hex + ">" + text + "</color>";
    }

    /// <summary>A flat button. on draws it highlighted, for the mode currently in effect.</summary>
    public static GUIStyle Button(int fontSize, bool on)
    {
        Dictionary<int, GUIStyle> cache = on ? _buttonsOn : _buttons;

        GUIStyle style;
        if (cache.TryGetValue(fontSize, out style) && style.normal.background != null) return style;

        if (_buttonTex == null) _buttonTex = Solid(new Color(0.16f, 0.13f, 0.30f, 0.95f));
        if (_buttonHoverTex == null) _buttonHoverTex = Solid(new Color(0.26f, 0.22f, 0.46f, 1f));
        if (_buttonOnTex == null) _buttonOnTex = Solid(new Color(0.31f, 0.88f, 1f, 1f));

        Texture2D idle = on ? _buttonOnTex : _buttonTex;
        Color text = on ? TextOn : Text;

        style = new GUIStyle(GUI.skin.button)
        {
            font = FontOf(fontSize),
            fontSize = fontSize,
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(10, 10, 4, 4),
            margin = new RectOffset(3, 3, 3, 3),
            normal = { background = idle, textColor = text },
            hover = { background = on ? idle : _buttonHoverTex, textColor = text },
            active = { background = _buttonOnTex, textColor = TextOn },
            focused = { background = idle, textColor = text },
        };
        style.onNormal = style.normal;
        style.onHover = style.hover;
        style.onActive = style.active;

        cache[fontSize] = style;
        return style;
    }

    /// <summary>
    /// A slider sized for a wall: a thick bar and a thumb wide enough to be grabbed with a
    /// mouse from across a room. Returns the new value, so it reads like GUI.HorizontalSlider.
    /// </summary>
    public static float Slider(Rect rect, float value, float min, float max, int fontSize)
    {
        if (_sliderBarTex == null) _sliderBarTex = Solid(new Color(0.16f, 0.13f, 0.30f, 1f));
        if (_sliderThumbTex == null) _sliderThumbTex = Solid(new Color(0.31f, 0.88f, 1f, 1f));

        if (_sliderBar == null || _sliderBar.normal.background == null)
        {
            _sliderBar = new GUIStyle(GUI.skin.horizontalSlider)
            {
                normal = { background = _sliderBarTex },
                fixedHeight = 0f,
                border = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0),
            };
        }

        if (_sliderThumb == null || _sliderThumb.normal.background == null)
        {
            _sliderThumb = new GUIStyle(GUI.skin.horizontalSliderThumb)
            {
                normal = { background = _sliderThumbTex },
                active = { background = _sliderThumbTex },
                hover = { background = _sliderThumbTex },
                focused = { background = _sliderThumbTex },
                border = new RectOffset(0, 0, 0, 0),
                overflow = new RectOffset(0, 0, 0, 0),
                fixedWidth = 10f,
                fixedHeight = 0f,
            };
        }

        // The bar is drawn as a thin line through the middle of the rect; the thumb fills it.
        float barHeight = Mathf.Max(4f, fontSize * 0.45f);
        _sliderBar.fixedHeight = barHeight;
        _sliderThumb.fixedHeight = fontSize + 8f;
        _sliderThumb.fixedWidth = Mathf.Max(10f, fontSize * 0.8f);

        return GUI.HorizontalSlider(rect, value, min, max, _sliderBar, _sliderThumb);
    }

    static Font FontOf(int size)
    {
        Font font;
        if (_fonts.TryGetValue(size, out font) && font != null) return font;

        font = Font.CreateDynamicFontFromOSFont("Consolas", size);
        _fonts[size] = font;
        return font;
    }

    static Texture2D Solid(Color c)
    {
        var tex = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        tex.SetPixel(0, 0, c);
        tex.Apply();
        return tex;
    }
}

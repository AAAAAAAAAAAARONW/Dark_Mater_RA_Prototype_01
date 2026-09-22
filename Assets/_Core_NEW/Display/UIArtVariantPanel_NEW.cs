using UnityEngine;

/// <summary>
/// One panel, listing every UIArtVariant_NEW in the scene, so the art can be compared and
/// tuned with a mouse while the piece runs.
///
/// Two lines per element. The first is what is discrete and so is buttons: a button for
/// each version the artist delivered, with the one in effect lit, show and hide, and the
/// display row, which is a physical panel and therefore a whole number. The second is what
/// is continuous and so is sliders: size, opacity and height up or down the wall. Dragging
/// a slider changes what is on the wall as it moves, which is the only way the question
/// "how big, how solid, how high" gets answered at 2 m from a curved display.
///
/// Size reads out in millimetres on the wall as well as a percentage, because a percentage
/// of a layout nobody has seen at scale means nothing. Height reads out in millimetres for
/// the same reason. A last line does size and opacity to everything at once.
///
/// It finds the elements through UIArtVariant_NEW's static list rather than through
/// references, so adding a second piece of art to the scene needs no wiring here.
///
/// Bottom left, because the art being judged is usually a band across the middle or a
/// column at the left of the wall, and because on the wall itself the bottom rows are the
/// ones within reach. F6 hides it; F1 hides every debug panel at once.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("ART PANEL", "#B478FF")]
public class UIArtVariantPanel_NEW : MonoBehaviour
{
    [SerializeField] bool show = true;

    [SerializeField] KeyCode toggleKey = KeyCode.F6;

    [Tooltip("Scale the panel with the rendered height, so it is the same share of the " +
             "screen at a desk as on the wall.")]
    [SerializeField] bool autoScale = true;

    [Tooltip("In 1080p units; Auto Scale takes it from there.")]
    [SerializeField] int fontSize = 13;

    [Tooltip("How wide each slider is, in the same units. Wider is easier to land on from " +
             "a distance, and resolves finer.")]
    [Range(80f, 400f)]
    [SerializeField] float sliderWidth = 170f;

    [SerializeField] Vector2 margin = new Vector2(12f, 12f);

    const float Pad = 10f, Gap = 6f;

    bool _collapsed;

    void Update()
    {
        if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey)) show = !show;
    }

    void OnGUI()
    {
        if (!Application.isPlaying || !show || !DebugView_NEW.Overlay) return;
        if (UIArtVariant_NEW.Active.Count == 0) return;

        float scale = VizlabDebugGUI_NEW.BeginScaled(autoScale);
        Vector2 screen = VizlabDebugGUI_NEW.ScaledScreen(scale);

        GUIStyle label = VizlabDebugGUI_NEW.Label(fontSize);
        GUIStyle button = VizlabDebugGUI_NEW.Button(fontSize, false);

        float bh = button.CalcSize(new GUIContent("Hide")).y + 4f;

        if (_collapsed)
        {
            var strip = new Rect(margin.x, screen.y - margin.y - bh - Pad, 180f, bh + Pad);
            GUI.Box(strip, GUIContent.none, VizlabDebugGUI_NEW.Panel);
            if (GUI.Button(new Rect(strip.x + Pad * 0.5f, strip.y + Pad * 0.5f, 160f, bh), "ART VARIANTS  +", button))
                _collapsed = false;
            return;
        }

        // Column widths, measured once so every element's controls line up down the panel.
        float nameWidth = label.CalcSize(new GUIContent("all of it")).x;
        float versionWidth = button.CalcSize(new GUIContent("Hide")).x;
        int maxVersions = 0;

        foreach (UIArtVariant_NEW art in UIArtVariant_NEW.Active)
        {
            nameWidth = Mathf.Max(nameWidth, label.CalcSize(new GUIContent(art.Title)).x);
            maxVersions = Mathf.Max(maxVersions, art.Count);
            for (int i = 0; i < art.Count; i++)
                versionWidth = Mathf.Max(versionWidth, button.CalcSize(new GUIContent(art.LabelOf(i))).x);
        }
        versionWidth += 10f;

        float showWidth = button.CalcSize(new GUIContent("Hide")).x + 10f;
        float rowBtn = button.CalcSize(new GUIContent("Row -")).x + 8f;
        float knobLabel = label.CalcSize(new GUIContent("opacity")).x + 6f;
        float readout = label.CalcSize(new GUIContent("300%  1234 mm")).x + 8f;
        float resetWidth = button.CalcSize(new GUIContent("Reset")).x + 10f;

        float buttonsWidth = nameWidth + Gap + maxVersions * (versionWidth + Gap) + showWidth + Gap + rowBtn * 2f + 2f;
        float slidersWidth = nameWidth + Gap
                           + (knobLabel + sliderWidth + Gap + readout + Gap) * 2f
                           + knobLabel + sliderWidth + Gap + readout + Gap + resetWidth;

        Vector2 titleSize = label.CalcSize(new GUIContent("ART VARIANTS"));
        float width = Pad + Mathf.Max(titleSize.x + 40f, Mathf.Max(buttonsWidth, slidersWidth)) + Pad;
        float lineH = bh + Gap;
        // Two lines per element, plus the title and the line that drives all of them.
        float height = Pad + lineH + UIArtVariant_NEW.Active.Count * lineH * 2f + lineH + Pad * 0.4f;

        var panel = new Rect(margin.x, screen.y - margin.y - height, width, height);
        GUI.Box(panel, GUIContent.none, VizlabDebugGUI_NEW.Panel);

        float y = panel.y + Pad;
        GUI.Label(new Rect(panel.x + Pad, y + (bh - titleSize.y) * 0.5f, titleSize.x, titleSize.y), "ART VARIANTS", label);
        if (GUI.Button(new Rect(panel.xMax - Pad - 30f, y, 30f, bh), "-", button)) _collapsed = true;
        y += lineH;

        foreach (UIArtVariant_NEW art in UIArtVariant_NEW.Active)
        {
            // ── Line 1: the discrete choices ──────────────────────────────────
            float x = panel.x + Pad;
            Label(x, y, bh, nameWidth, art.Title, label);
            x += nameWidth + Gap;

            for (int i = 0; i < art.Count; i++)
            {
                bool on = art.IsVisible && art.Current == i;
                if (GUI.Button(new Rect(x, y, versionWidth, bh), art.LabelOf(i), VizlabDebugGUI_NEW.Button(fontSize, on)))
                    art.Select(i);
                x += versionWidth + Gap;
            }

            // Every element keeps its slot, so the rows do not jump about as versions differ.
            x += (maxVersions - art.Count) * (versionWidth + Gap);

            if (GUI.Button(new Rect(x, y, showWidth, bh), art.IsVisible ? "Hide" : "Show",
                           VizlabDebugGUI_NEW.Button(fontSize, !art.IsVisible)))
                art.ToggleVisible();
            x += showWidth + Gap;

            // Row buttons only where the element is actually placed by row.
            GUI.enabled = art.Row != null;
            if (GUI.Button(new Rect(x, y, rowBtn, bh), "Row -", button)) art.StepRow(-1);
            if (GUI.Button(new Rect(x + rowBtn + 2f, y, rowBtn, bh), "Row +", button)) art.StepRow(1);
            GUI.enabled = true;
            y += lineH;

            // ── Line 2: the continuous ones ───────────────────────────────────
            x = panel.x + Pad + nameWidth + Gap;

            art.SetScale(Knob(ref x, y, bh, "size", art.Scale, UIArtVariant_NEW.MinScale, UIArtVariant_NEW.MaxScale,
                              string.Format("{0:0}%  {1:0} mm", art.Scale * 100f, art.WallHeightMm),
                              knobLabel, readout, label, fontSize));

            art.SetOpacity(Knob(ref x, y, bh, "opacity", art.Opacity, 0f, 1f,
                                string.Format("{0:0}%", art.Opacity * 100f),
                                knobLabel, readout, label, fontSize));

            art.SetShiftMm(Knob(ref x, y, bh, "height", art.ShiftMm,
                                -UIArtVariant_NEW.ShiftRangeMm, UIArtVariant_NEW.ShiftRangeMm,
                                string.Format("{0:+0;-0;0} mm", art.ShiftMm),
                                knobLabel, readout, label, fontSize));

            if (GUI.Button(new Rect(x, y, resetWidth, bh), "Reset", button)) art.ResetAdjustments();
            y += lineH;
        }

        DrawAllLine(panel, y, bh, label, button, nameWidth, knobLabel, readout, resetWidth);
    }

    /// <summary>A labelled slider with its reading, laid out left to right from x.</summary>
    float Knob(ref float x, float y, float bh, string name, float value, float min, float max,
               string reading, float knobLabel, float readout, GUIStyle label, int fontSize)
    {
        Label(x, y, bh, knobLabel, name, label);
        x += knobLabel;

        float result = VizlabDebugGUI_NEW.Slider(new Rect(x, y, sliderWidth, bh), value, min, max, fontSize);
        x += sliderWidth + Gap;

        Label(x, y, bh, readout, reading, label);
        x += readout + Gap;

        return result;
    }

    static void Label(float x, float y, float bh, float width, string text, GUIStyle style)
    {
        Vector2 size = style.CalcSize(new GUIContent(text));
        GUI.Label(new Rect(x, y + (bh - size.y) * 0.5f, width, size.y), text, style);
    }

    /// <summary>
    /// Size and opacity applied to every element at once, for the question that comes before
    /// any single element: is the whole HUD too big, or too faint, on the wall.
    /// </summary>
    void DrawAllLine(Rect panel, float y, float bh, GUIStyle label, GUIStyle button,
                     float nameWidth, float knobLabel, float readout, float resetWidth)
    {
        float x = panel.x + Pad;
        Label(x, y, bh, nameWidth, "all of it", label);
        x += nameWidth + Gap;

        float averageScale = 0f, averageOpacity = 0f;
        foreach (UIArtVariant_NEW art in UIArtVariant_NEW.Active)
        {
            averageScale += art.Scale;
            averageOpacity += art.Opacity;
        }
        averageScale /= UIArtVariant_NEW.Active.Count;
        averageOpacity /= UIArtVariant_NEW.Active.Count;

        // The sliders start where the elements average, and a drag takes them all to the
        // value dragged to, which is what "all of it" has to mean once they have drifted apart.
        float newScale = Knob(ref x, y, bh, "size", averageScale, UIArtVariant_NEW.MinScale, UIArtVariant_NEW.MaxScale,
                              string.Format("{0:0}%", averageScale * 100f), knobLabel, readout, label, fontSize);
        if (!Mathf.Approximately(newScale, averageScale))
            foreach (UIArtVariant_NEW art in UIArtVariant_NEW.Active) art.SetScale(newScale);

        float newOpacity = Knob(ref x, y, bh, "opacity", averageOpacity, 0f, 1f,
                                string.Format("{0:0}%", averageOpacity * 100f), knobLabel, readout, label, fontSize);
        if (!Mathf.Approximately(newOpacity, averageOpacity))
            foreach (UIArtVariant_NEW art in UIArtVariant_NEW.Active) art.SetOpacity(newOpacity);

        // The height slot is skipped: shifting everything by the same amount is not a
        // question anybody asks, and a stray drag there would undo per-element placement.
        x += knobLabel + sliderWidth + Gap + readout + Gap;

        if (GUI.Button(new Rect(x, y, resetWidth, bh), "Reset", button))
            foreach (UIArtVariant_NEW art in UIArtVariant_NEW.Active) art.ResetAdjustments();
    }
}

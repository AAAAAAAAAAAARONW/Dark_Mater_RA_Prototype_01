using UnityEngine;

/// <summary>
/// One panel, listing every UIArtVariant_NEW in the scene, so the art versions can be
/// compared with a mouse while the piece runs.
///
/// One row per element: its name, a button for each version the artist delivered, a show
/// and hide button, buttons to walk it up and down the display's rows when it is placed
/// with VizlabRowAnchor_NEW, and - and +, which resize it and report the height it comes
/// to on the wall in millimetres. The version in effect is lit. A last row does the same
/// resizing to everything at once, for the question of whether the whole HUD is too big
/// or too small before the question of any one element.
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
        float rowH = bh + Gap;

        if (_collapsed)
        {
            var strip = new Rect(margin.x, screen.y - margin.y - bh - Pad, 180f, bh + Pad);
            GUI.Box(strip, GUIContent.none, VizlabDebugGUI_NEW.Panel);
            if (GUI.Button(new Rect(strip.x + Pad * 0.5f, strip.y + Pad * 0.5f, 160f, bh), "ART VARIANTS  +", button))
                _collapsed = false;
            return;
        }

        // Widest name, so the version buttons line up down the panel.
        float nameWidth = 0f;
        foreach (UIArtVariant_NEW art in UIArtVariant_NEW.Active)
            nameWidth = Mathf.Max(nameWidth, label.CalcSize(new GUIContent(art.Title)).x);

        // Widest version label, so every version button is the same size.
        float versionWidth = button.CalcSize(new GUIContent("Hide")).x;
        int maxVersions = 0;
        foreach (UIArtVariant_NEW art in UIArtVariant_NEW.Active)
        {
            maxVersions = Mathf.Max(maxVersions, art.Count);
            for (int i = 0; i < art.Count; i++)
                versionWidth = Mathf.Max(versionWidth, button.CalcSize(new GUIContent(art.LabelOf(i))).x);
        }
        versionWidth += 10f;

        float showWidth = button.CalcSize(new GUIContent("Hide")).x + 10f;
        float rowBtn = button.CalcSize(new GUIContent("Row -")).x + 8f;
        float stepBtn = button.CalcSize(new GUIContent("+")).x + 12f;
        float sizeRead = label.CalcSize(new GUIContent("100%  1234 mm")).x + 8f;

        Vector2 titleSize = label.CalcSize(new GUIContent("ART VARIANTS"));
        float width = Pad + Mathf.Max(titleSize.x + 40f,
                                      nameWidth + Gap + maxVersions * (versionWidth + Gap) + showWidth + Gap
                                      + rowBtn * 2f + Gap + stepBtn * 2f + 2f + Gap + sizeRead) + Pad;
        // One row per element, then the row that sizes them all together.
        float height = Pad + bh + Gap + (UIArtVariant_NEW.Active.Count + 1) * rowH + Pad * 0.4f;

        var panel = new Rect(margin.x, screen.y - margin.y - height, width, height);
        GUI.Box(panel, GUIContent.none, VizlabDebugGUI_NEW.Panel);

        float y = panel.y + Pad;
        GUI.Label(new Rect(panel.x + Pad, y + (bh - titleSize.y) * 0.5f, titleSize.x, titleSize.y), "ART VARIANTS", label);
        if (GUI.Button(new Rect(panel.xMax - Pad - 30f, y, 30f, bh), "-", button)) _collapsed = true;
        y += bh + Gap;

        foreach (UIArtVariant_NEW art in UIArtVariant_NEW.Active)
        {
            float x = panel.x + Pad;
            Vector2 nameSize = label.CalcSize(new GUIContent(art.Title));
            GUI.Label(new Rect(x, y + (bh - nameSize.y) * 0.5f, nameWidth, nameSize.y), art.Title, label);
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
            x += rowBtn * 2f + 2f + Gap;

            // Size, and what that size comes to on the wall.
            if (GUI.Button(new Rect(x, y, stepBtn, bh), "-", button)) art.StepScale(-1);
            if (GUI.Button(new Rect(x + stepBtn + 2f, y, stepBtn, bh), "+", button)) art.StepScale(1);
            x += stepBtn * 2f + 2f + Gap;

            string size = string.Format("{0:0}%  {1:0} mm", art.Scale * 100f, art.WallHeightMm);
            Vector2 sizeSize = label.CalcSize(new GUIContent(size));
            GUI.Label(new Rect(x, y + (bh - sizeSize.y) * 0.5f, sizeRead, sizeSize.y), size, label);

            y += rowH;
        }

        DrawAllRow(panel, y, bh, label, button, nameWidth, stepBtn);
    }

    /// <summary>
    /// The same size controls applied to every element at once, for the question that comes
    /// before "how big is this one": is the whole HUD too big or too small on the wall.
    /// </summary>
    void DrawAllRow(Rect panel, float y, float bh, GUIStyle label, GUIStyle button, float nameWidth, float stepBtn)
    {
        float x = panel.x + Pad;
        Vector2 nameSize = label.CalcSize(new GUIContent("all of it"));
        GUI.Label(new Rect(x, y + (bh - nameSize.y) * 0.5f, nameWidth, nameSize.y), "all of it", label);
        x += nameWidth + Gap;

        float wide = stepBtn * 2f;
        if (GUI.Button(new Rect(x, y, wide, bh), "Smaller", button)) StepAll(-1);
        if (GUI.Button(new Rect(x + wide + Gap, y, wide, bh), "Bigger", button)) StepAll(1);
        if (GUI.Button(new Rect(x + (wide + Gap) * 2f, y, wide, bh), "100%", button))
        {
            foreach (UIArtVariant_NEW art in UIArtVariant_NEW.Active) art.ResetScale();
        }
    }

    static void StepAll(int direction)
    {
        foreach (UIArtVariant_NEW art in UIArtVariant_NEW.Active) art.StepScale(direction);
    }
}

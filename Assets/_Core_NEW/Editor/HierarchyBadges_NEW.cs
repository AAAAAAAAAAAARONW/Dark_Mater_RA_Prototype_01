using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Draws a tag in the Hierarchy for the scripts on each GameObject.
///
/// A component is invisible in the Hierarchy: finding out what lives where means clicking
/// every object and reading the Inspector. That is how the old scene ended up with the
/// render controller on Main Camera and the ending cutscene on a trigger volume — the
/// layout could not be seen, so it drifted.
///
/// Nothing here is a hardcoded list of known scripts. Every MonoBehaviour can be tagged,
/// and a label is resolved in this order:
///
///   1. BadgeRegistry_NEW      an asset you edit; also where you hide noisy types
///   2. [HierarchyBadge_NEW]   an attribute on the class, so it travels with the script
///   3. automatic              label shortened from the class name, colour from its hash
///
/// Step 3 means a script written next year shows up with no setup at all. Steps 1 and 2
/// are for when you want it to read better than the class name.
///
/// Editor-only — this folder is named Editor, so none of it ships.
/// Modes and the toggle live under Tools > Journey NEW > Hierarchy Tags.
/// </summary>
[InitializeOnLoad]
public static class HierarchyBadges_NEW
{
    enum Mode
    {
        Off = 0,
        Annotated = 1,   // only registry entries and attributed classes
        AllScripts = 2,  // every MonoBehaviour
    }

    const string PrefKey = "JourneyNEW.HierarchyTags.Mode";
    const string Root = "Tools/Journey NEW/Hierarchy Tags/";

    const int MaxTagsPerRow = 4;
    const float MinNameRoom = 90f;

    struct Tag
    {
        public string label;
        public string tooltip;
        public Color color;
        public bool dim;
    }

    static readonly Dictionary<int, Tag[]> RowCache = new Dictionary<int, Tag[]>();
    static readonly Dictionary<Type, Tag?> TypeCache = new Dictionary<Type, Tag?>();

    static BadgeRegistry_NEW _registry;
    static bool _registrySearched;
    static GUIStyle _style;

    static HierarchyBadges_NEW()
    {
        EditorApplication.hierarchyWindowItemOnGUI -= OnItem;
        EditorApplication.hierarchyWindowItemOnGUI += OnItem;

        EditorApplication.hierarchyChanged -= InvalidateRows;
        EditorApplication.hierarchyChanged += InvalidateRows;
    }

    static Mode CurrentMode
    {
        get => (Mode)EditorPrefs.GetInt(PrefKey, (int)Mode.AllScripts);
        set { EditorPrefs.SetInt(PrefKey, (int)value); InvalidateAll(); }
    }

    // ── Menu ─────────────────────────────────────────────────────────────────

    [MenuItem(Root + "Off")] static void SetOff() => CurrentMode = Mode.Off;
    [MenuItem(Root + "Off", true)] static bool ValOff() { Menu.SetChecked(Root + "Off", CurrentMode == Mode.Off); return true; }

    [MenuItem(Root + "Annotated only")] static void SetAnn() => CurrentMode = Mode.Annotated;
    [MenuItem(Root + "Annotated only", true)] static bool ValAnn() { Menu.SetChecked(Root + "Annotated only", CurrentMode == Mode.Annotated); return true; }

    [MenuItem(Root + "All scripts")] static void SetAll() => CurrentMode = Mode.AllScripts;
    [MenuItem(Root + "All scripts", true)] static bool ValAll() { Menu.SetChecked(Root + "All scripts", CurrentMode == Mode.AllScripts); return true; }

    [MenuItem(Root + "Refresh")]
    static void ForceRefresh()
    {
        _registrySearched = false;
        _registry = null;
        InvalidateAll();
    }

    static void InvalidateRows() => RowCache.Clear();

    static void InvalidateAll()
    {
        RowCache.Clear();
        TypeCache.Clear();
        EditorApplication.RepaintHierarchyWindow();
    }

    // ── Drawing ──────────────────────────────────────────────────────────────

    static void OnItem(int instanceID, Rect rect)
    {
        if (CurrentMode == Mode.Off || Event.current.type != EventType.Repaint) return;

        Tag[] tags = RowTags(instanceID);
        if (tags == null || tags.Length == 0) return;

        EnsureStyle();

        float x = rect.xMax - 2f;
        int drawn = 0;

        for (int i = tags.Length - 1; i >= 0; i--)
        {
            bool last = drawn == MaxTagsPerRow - 1 && i > 0;
            Tag t = last
                ? new Tag { label = "+" + (i + 1), tooltip = OverflowTooltip(tags, i), color = new Color(0.5f, 0.5f, 0.55f), dim = true }
                : tags[i];

            var content = new GUIContent(t.label, t.tooltip);
            float w = _style.CalcSize(content).x + 8f;

            var chip = new Rect(x - w, rect.y + 1f, w, rect.height - 2f);
            if (chip.x < rect.x + MinNameRoom) break;   // out of room; never overlap the name

            Color fill = t.color;
            fill.a = t.dim ? 0.15f : 0.28f;
            EditorGUI.DrawRect(chip, fill);

            Color prev = GUI.color;
            GUI.color = t.dim
                ? new Color(t.color.r, t.color.g, t.color.b, 0.55f)
                : Color.Lerp(t.color, Color.white, 0.4f);
            GUI.Label(chip, content, _style);
            GUI.color = prev;

            x -= w + 3f;
            drawn++;
            if (last) break;
        }
    }

    static string OverflowTooltip(Tag[] tags, int upTo)
    {
        var sb = new StringBuilder();
        for (int i = 0; i <= upTo; i++)
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(tags[i].tooltip);
        }
        return sb.ToString();
    }

    // ── Resolution ───────────────────────────────────────────────────────────

    static Tag[] RowTags(int instanceID)
    {
        if (RowCache.TryGetValue(instanceID, out Tag[] cached)) return cached;

        var go = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
        if (go == null)
        {
            RowCache[instanceID] = null;
            return null;
        }

        List<Tag> list = null;

        foreach (MonoBehaviour mb in go.GetComponents<MonoBehaviour>())
        {
            if (mb == null)
            {
                // A missing script is the single most useful thing a tag can point out.
                (list ?? (list = new List<Tag>())).Add(new Tag
                {
                    label = "MISSING",
                    tooltip = "Missing script — the class was renamed or deleted.",
                    color = new Color(0.90f, 0.30f, 0.28f),
                });
                continue;
            }

            Tag? tag = ForType(mb.GetType());
            if (tag == null) continue;

            Tag t = tag.Value;
            if (!mb.enabled) t.dim = true;

            (list ?? (list = new List<Tag>())).Add(t);
        }

        Tag[] result = list?.ToArray();
        RowCache[instanceID] = result;
        return result;
    }

    static Tag? ForType(Type type)
    {
        if (TypeCache.TryGetValue(type, out Tag? cached)) return cached;

        Tag? resolved = Resolve(type);
        TypeCache[type] = resolved;
        return resolved;
    }

    static Tag? Resolve(Type type)
    {
        string name = type.Name;

        // 1 · registry, which also decides what to hide
        BadgeRegistry_NEW registry = Registry();
        BadgeRegistry_NEW.Entry entry = registry != null ? registry.Lookup(name) : null;

        if (entry != null)
        {
            if (entry.hide) return null;

            return new Tag
            {
                label = string.IsNullOrEmpty(entry.label) ? AutoLabel(name) : entry.label,
                tooltip = name,
                color = entry.color,
                dim = entry.dim,
            };
        }

        // 2 · attribute on the class
        var attr = (HierarchyBadge_NEW)Attribute.GetCustomAttribute(type, typeof(HierarchyBadge_NEW));
        if (attr != null)
        {
            Color c = ParseHex(attr.ColorHex) ?? AutoColor(name);
            return new Tag
            {
                label = string.IsNullOrEmpty(attr.Label) ? AutoLabel(name) : attr.Label,
                tooltip = name,
                color = c,
            };
        }

        // 3 · automatic, but only when the mode asks for everything
        if (CurrentMode != Mode.AllScripts) return null;

        return new Tag
        {
            label = AutoLabel(name),
            tooltip = name,
            color = AutoColor(name),
            dim = true,   // unannotated types read quieter than ones someone named
        };
    }

    static BadgeRegistry_NEW Registry()
    {
        if (_registrySearched) return _registry;
        _registrySearched = true;

        string[] guids = AssetDatabase.FindAssets("t:BadgeRegistry_NEW");
        if (guids.Length == 0) return _registry = null;

        if (guids.Length > 1)
            Debug.LogWarning($"[HierarchyBadges_NEW] {guids.Length} BadgeRegistry_NEW assets exist. " +
                             "Using the first; delete the others to avoid confusion.");

        return _registry = AssetDatabase.LoadAssetAtPath<BadgeRegistry_NEW>(
            AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    // ── Automatic label and colour ───────────────────────────────────────────

    static readonly string[] NoiseSuffixes =
        { "Controller", "Manager", "Behaviour", "Component", "Handler", "System" };

    /// <summary>
    /// Shorten a class name into something that fits a hierarchy row. Trims the _NEW and
    /// Test markers and one noise suffix, then falls back to capital-letter initials when
    /// the result is still long.
    /// </summary>
    static string AutoLabel(string typeName)
    {
        string s = typeName;

        if (s.EndsWith("_NEW", StringComparison.Ordinal)) s = s.Substring(0, s.Length - 4);
        if (s.EndsWith("Test", StringComparison.Ordinal) && s.Length > 6) s = s.Substring(0, s.Length - 4);

        foreach (string suffix in NoiseSuffixes)
        {
            if (s.Length <= suffix.Length + 2 || !s.EndsWith(suffix, StringComparison.Ordinal)) continue;
            s = s.Substring(0, s.Length - suffix.Length);
            break;
        }

        if (s.Length <= 12) return s;

        var sb = new StringBuilder(6);
        foreach (char c in s)
            if (char.IsUpper(c) || char.IsDigit(c)) sb.Append(c);

        return sb.Length >= 2 ? sb.ToString() : s.Substring(0, 12);
    }

    /// <summary>Stable hue per class name, so every type gets its own colour for free.</summary>
    static Color AutoColor(string typeName)
    {
        unchecked
        {
            int h = 17;
            foreach (char c in typeName) h = h * 31 + c;
            float hue = (Mathf.Abs(h) % 360) / 360f;
            return Color.HSVToRGB(hue, 0.55f, 0.95f);
        }
    }

    static Color? ParseHex(string hex)
    {
        if (string.IsNullOrEmpty(hex)) return null;
        return ColorUtility.TryParseHtmlString(hex, out Color c) ? c : (Color?)null;
    }

    static void EnsureStyle()
    {
        if (_style != null) return;

        _style = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 9,
            padding = new RectOffset(0, 0, 0, 0),
        };
    }
}

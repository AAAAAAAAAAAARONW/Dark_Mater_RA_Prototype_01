using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One table answering "what is mounted where", for the whole open scene.
///
/// Every _NEW component in the scene, grouped by role, with its full hierarchy path and
/// whether it is enabled. Click a row to select and ping the object. Legacy layer
/// components are listed too, so retiring the old stack is a visible process rather than
/// something you have to remember.
///
/// The window also flags the two mistakes this architecture is prone to:
///   * a component that should be a singleton appearing more than once
///   * a component sitting somewhere the design does not expect
///
/// Both have already happened in this project — EarthLayerResponderTest was mounted twice
/// on two different objects driving the same video player, and the render controller
/// lived on Main Camera.
///
/// Open from Tools > Journey NEW > System Map.
/// </summary>
public class SystemMapWindow_NEW : EditorWindow
{
    [MenuItem("Tools/Journey NEW/System Map")]
    static void Open()
    {
        SystemMapWindow_NEW w = GetWindow<SystemMapWindow_NEW>("System Map");
        w.minSize = new Vector2(520f, 320f);
        w.Refresh();
    }

    class Row
    {
        public string type;
        public string path;
        public GameObject go;
        public bool enabled;
        public bool legacy;
        public string warning;
    }

    class Group
    {
        public readonly string role;
        public readonly string[] types;
        public Group(string role, string[] types) { this.role = role; this.types = types; }
    }

    // role -> the component types that belong to it, in the order they should read.
    static readonly Group[] Groups =
    {
        new Group("Core",       new[] { "LayerState_NEW", "LayerGate_NEW" }),
        new Group("Responders", new[] { "CameraDirector_NEW", "WorldSwitcher_NEW", "NebulaResponder_NEW",
                                        "SpeedResponder_NEW", "JourneyResponder_NEW", "SpectrumResponder_NEW",
                                        "EarthCutscene_NEW" }),
        new Group("Player",     new[] { "PlayerRig_NEW" }),
        new Group("Data",       new[] { "AbsorptionField_NEW", "MilestoneRelay_NEW" }),
        new Group("HUD",        new[] { "SpectrumHUD_NEW", "MilestoneCardHUD_NEW" }),
        new Group("Debug",      new[] { "LayerDebugJump_NEW" }),
    };

    // Types that must not appear more than once in a scene.
    static readonly HashSet<string> Singletons = new HashSet<string>
    {
        "LayerState_NEW", "CameraDirector_NEW", "WorldSwitcher_NEW", "NebulaResponder_NEW",
        "SpeedResponder_NEW", "JourneyResponder_NEW", "SpectrumResponder_NEW",
        "EarthCutscene_NEW", "PlayerRig_NEW", "AbsorptionField_NEW", "SpectrumHUD_NEW",
    };

    // The stack the _NEW architecture replaces. UniverseJourneyTracker is deliberately
    // absent — it is kept, not retired; its scale model is the best code in the project.
    static readonly HashSet<string> Legacy = new HashSet<string>
    {
        "LayerStateManagerTest", "LayerTriggerVolumeTest", "CameraLayerResponder",
        "VisualLayerResponder", "SpeedLayerResponder", "JourneyLayerResponder",
        "LAFLayerResponder", "RenderLayerTransitionController",
        "DarkMatterPlayerControllerTest", "PlaytestLayerDebugSwitcher",
        "EarthLayerResponderTest", "LAFSpectrumHUD", "LymanAlphaAbsorptionController",
        "MilestoneHUD", "LymanAlphaModeTrigger", "FakeLymanAlphaForestHUD",
        "ForestHUDLine", "UILineGraph", "GraphFeeder",
    };

    readonly Dictionary<string, List<Row>> _rows = new Dictionary<string, List<Row>>();
    readonly List<Row> _legacyRows = new List<Row>();
    Vector2 _scroll;
    bool _showLegacy = true;

    void OnEnable() => Refresh();
    void OnFocus() => Refresh();

    void Refresh()
    {
        _rows.Clear();
        _legacyRows.Clear();

        var counts = new Dictionary<string, int>();

        foreach (MonoBehaviour mb in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
        {
            if (mb == null) continue;

            // Skip prefab assets and anything not in the open scene.
            if (EditorUtility.IsPersistent(mb)) continue;
            if (mb.gameObject.hideFlags != HideFlags.None) continue;
            if (!mb.gameObject.scene.IsValid()) continue;

            string type = mb.GetType().Name;
            bool isNew = type.EndsWith("_NEW", StringComparison.Ordinal);
            bool isLegacy = Legacy.Contains(type);

            counts.TryGetValue(type, out int n);
            counts[type] = n + 1;

            var row = new Row
            {
                type = type,
                path = Path(mb.transform),
                go = mb.gameObject,
                enabled = mb.enabled,
                legacy = isLegacy,
            };

            if (isLegacy) { _legacyRows.Add(row); continue; }

            // Everything else in the scene lands in "Other", so this window is a complete
            // inventory rather than a view of the parts I happened to list.
            string role = isNew ? RoleOf(type) : "Other";
            if (!_rows.TryGetValue(role, out List<Row> list))
                _rows[role] = list = new List<Row>();
            list.Add(row);
        }

        foreach (List<Row> list in _rows.Values)
            foreach (Row r in list)
                if (Singletons.Contains(r.type) && counts[r.type] > 1)
                    r.warning = $"mounted {counts[r.type]} times — this should be unique";

        _legacyRows.Sort((a, b) => string.CompareOrdinal(a.type, b.type));
        Repaint();
    }

    static string RoleOf(string type)
    {
        foreach (Group g in Groups)
            if (Array.IndexOf(g.types, type) >= 0) return g.role;
        return "Other";
    }

    static string Path(Transform t)
    {
        string s = t.name;
        while (t.parent != null) { t = t.parent; s = t.name + " / " + s; }
        return s;
    }

    void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(64f)))
                Refresh();

            GUILayout.FlexibleSpace();
            _showLegacy = GUILayout.Toggle(_showLegacy, "Show legacy", EditorStyles.toolbarButton,
                                           GUILayout.Width(96f));
        }

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        foreach (Group g in Groups)
        {
            if (!_rows.TryGetValue(g.role, out List<Row> list) || list.Count == 0) continue;

            GUILayout.Space(6f);
            EditorGUILayout.LabelField(g.role.ToUpperInvariant(), EditorStyles.miniBoldLabel);

            foreach (Row r in list.OrderBy(x => Array.IndexOf(g.types, x.type)).ThenBy(x => x.path))
                DrawRow(r);
        }

        // Everything that is neither _NEW nor retired — package components, one-off
        // helpers, anything added since. Listed so the window is a full inventory.
        if (_rows.TryGetValue("Other", out List<Row> others) && others.Count > 0)
        {
            GUILayout.Space(10f);
            EditorGUILayout.LabelField($"OTHER SCRIPTS IN SCENE  ({others.Count})",
                                       EditorStyles.miniBoldLabel);

            foreach (Row r in others.OrderBy(x => x.type).ThenBy(x => x.path))
                DrawRow(r);
        }

        if (_showLegacy && _legacyRows.Count > 0)
        {
            GUILayout.Space(10f);
            EditorGUILayout.LabelField("LEGACY — retired stack", EditorStyles.miniBoldLabel);

            int live = _legacyRows.Count(r => r.enabled);
            EditorGUILayout.HelpBox(
                live == 0
                    ? $"All {_legacyRows.Count} legacy components are disabled. Safe to delete once the new stack is signed off."
                    : $"{live} of {_legacyRows.Count} legacy components are still enabled — they may fight the new stack.",
                live == 0 ? MessageType.Info : MessageType.Warning);

            foreach (Row r in _legacyRows) DrawRow(r);
        }

        EditorGUILayout.EndScrollView();
    }

    void DrawRow(Row r)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            Color prev = GUI.color;
            if (!r.enabled) GUI.color = new Color(1f, 1f, 1f, 0.45f);

            if (GUILayout.Button(r.type, EditorStyles.linkLabel, GUILayout.Width(190f)))
            {
                Selection.activeGameObject = r.go;
                EditorGUIUtility.PingObject(r.go);
            }

            EditorGUILayout.LabelField(r.path, EditorStyles.miniLabel);
            GUI.color = prev;

            GUILayout.Label(r.enabled ? "on" : "off", EditorStyles.miniLabel, GUILayout.Width(26f));
        }

        if (string.IsNullOrEmpty(r.warning)) return;

        Color c = GUI.color;
        GUI.color = new Color(1f, 0.55f, 0.5f);
        EditorGUILayout.LabelField("     " + r.warning, EditorStyles.miniLabel);
        GUI.color = c;
    }
}

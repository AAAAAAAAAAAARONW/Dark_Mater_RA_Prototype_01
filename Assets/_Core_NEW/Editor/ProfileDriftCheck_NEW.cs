using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Compares the new layer assets against the old ones they were copied from.
///
/// The migration copied every value verbatim out of the old layer definition asset, which
/// means the same numbers now live in two places. Nothing links them: tuning the old asset
/// leaves the new one behind, silently, and the two scenes drift apart with no error.
///
/// The convention is that the old assets are frozen — edit only the new ones, and delete
/// the old stack once the new scene is signed off. Conventions get forgotten, so this
/// checks instead of trusting.
///
/// It reads the old assets through SerializedObject rather than referencing their class.
/// That keeps _Core_NEW free of any compile-time tie to the old stack — this window is the
/// one place that has to look at both sides, and it does so by field name. It also means
/// the tool simply finds nothing once the old assets are deleted, instead of failing to
/// compile.
///
/// Assets are paired on layerId, not on filename, so renaming a file does not break the
/// comparison and an unmatched pair is reported rather than silently skipped.
///
/// Open from Tools > Journey NEW > Profile Drift Check.
/// </summary>
public class ProfileDriftCheck_NEW : EditorWindow
{
    const float Tolerance = 1e-4f;

    // Fields that identify an old layer definition asset without naming its type.
    const string IdField = "layerId";
    const string SignatureField = "nebulaColorDark";

    class Issue
    {
        public string layerId;
        public string field;
        public string oldValue;
        public string newValue;
        public Object asset;
        public bool intentional;
        public string note;
    }

    readonly List<Issue> _issues = new List<Issue>();
    readonly List<string> _unpairedNew = new List<string>();
    readonly List<string> _unpairedOld = new List<string>();

    Vector2 _scroll;
    bool _showIntentional;
    bool _hasRun;

    [MenuItem("Tools/Journey NEW/Profile Drift Check")]
    static void Open()
    {
        ProfileDriftCheck_NEW w = GetWindow<ProfileDriftCheck_NEW>("Drift Check");
        w.minSize = new Vector2(560f, 320f);
        w.Run();
    }

    // ── Comparison ───────────────────────────────────────────────────────────

    void Run()
    {
        _issues.Clear();
        _unpairedNew.Clear();
        _unpairedOld.Clear();
        _hasRun = true;

        Dictionary<string, SerializedObject> oldById = FindLegacyProfiles();
        var pairedOld = new HashSet<string>();

        foreach (LayerProfile_NEW np in LoadNew())
        {
            if (np == null || string.IsNullOrEmpty(np.layerId)) continue;

            if (!oldById.TryGetValue(np.layerId, out SerializedObject old))
            {
                _unpairedNew.Add($"{np.name}  (layerId '{np.layerId}')");
                continue;
            }

            pairedOld.Add(np.layerId);
            CompareLayer(np, old);
        }

        foreach (KeyValuePair<string, SerializedObject> kv in oldById)
            if (!pairedOld.Contains(kv.Key))
                _unpairedOld.Add($"{kv.Value.targetObject.name}  (layerId '{kv.Key}')");

        Repaint();
    }

    /// <summary>
    /// Every ScriptableObject carrying both a layerId and the nebula signature field.
    /// Identifying by schema rather than by type is what keeps this window from importing
    /// the old stack.
    /// </summary>
    static Dictionary<string, SerializedObject> FindLegacyProfiles()
    {
        var found = new Dictionary<string, SerializedObject>();

        foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (so == null) continue;

            var serialized = new SerializedObject(so);
            SerializedProperty id = serialized.FindProperty(IdField);
            if (id == null || serialized.FindProperty(SignatureField) == null) continue;
            if (string.IsNullOrEmpty(id.stringValue)) continue;

            found[id.stringValue] = serialized;
        }

        return found;
    }

    static List<LayerProfile_NEW> LoadNew()
    {
        return AssetDatabase.FindAssets("t:LayerProfile_NEW")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<LayerProfile_NEW>)
            .Where(a => a != null)
            .ToList();
    }

    void CompareLayer(LayerProfile_NEW np, SerializedObject old)
    {
        string id = np.layerId;

        Bool(id, np, old, "isDefaultLayer", "isStartLayer", np.isStartLayer);
        Float(id, np, old, "zoneSpeedMultiplier", "zoneSpeedMultiplier", np.zoneSpeedMultiplier);
        Float(id, np, old, "speedBlendDuration", "speedBlendDuration", np.speedBlendDuration);
        Bool(id, np, old, "lockLookInputDuringLookDown", "lockLookInput", np.lockLookInput);
        Float(id, np, old, "lookDownHoldDuration", "lookDownHoldDuration", np.lookDownHoldDuration);
        Float(id, np, old, "lookUpStartDelay", "lookUpStartDelay", np.lookUpStartDelay);
        Bool(id, np, old, "useLookBackSequence", "useLookBackSequence", np.useLookBackSequence);
        Float(id, np, old, "lookBackOrbitCountdown", "lookBackOrbitCountdown", np.lookBackOrbitCountdown);
        Float(id, np, old, "lookBackOrbitDuration", "lookBackOrbitDuration", np.lookBackOrbitDuration);
        Float(id, np, old, "transitionDuration", "renderSwapDuration", np.renderSwapDuration);
        Enum(id, np, old, "phaseOnEnter", "phaseOnEnter", (int)np.phaseOnEnter);

        if (np.nebula == null)
        {
            _issues.Add(new Issue
            {
                layerId = id,
                field = "nebula",
                oldValue = "(14 fields on the old asset)",
                newValue = "no NebulaProfile_NEW linked",
                asset = np,
            });
            return;
        }

        NebulaProfile_NEW n = np.nebula;

        Colour(id, n, old, "nebulaColorDark", "colorDark", n.colorDark);
        Colour(id, n, old, "nebulaColorMid", "colorMid", n.colorMid);
        Colour(id, n, old, "nebulaColorBright", "colorBright", n.colorBright);
        Colour(id, n, old, "nebulaColorStar", "colorStar", n.colorStar);
        Float(id, n, old, "nebulaScale", "scale", n.scale);
        Float(id, n, old, "nebulaOctaves", "octaves", n.octaves);
        Float(id, n, old, "nebulaPersistence", "persistence", n.persistence);
        Float(id, n, old, "nebulaDensity", "density", n.density);
        Float(id, n, old, "nebulaSharpness", "sharpness", n.sharpness);
        Float(id, n, old, "nebulaStarScale", "starScale", n.starScale);
        Float(id, n, old, "nebulaStarThreshold", "starThreshold", n.starThreshold);
        Float(id, n, old, "nebulaStarBrightness", "starBrightness", n.starBrightness);
        Float(id, n, old, "nebulaAnimate", "animate", n.animate);
        Float(id, n, old, "nebulaSpeed", "speed", n.speed);

        // The one difference that is on purpose: the swap happens under the top-down
        // cover, so tweening the sky is work the player never sees, and a tween still
        // running when the camera comes back up fights the finished world swap.
        Float(id, n, old, "nebulaBlendDuration", "blendDuration", n.blendDuration,
              intentional: true,
              note: "deliberate: snap under cover instead of tweening");
    }

    // ── Comparators ──────────────────────────────────────────────────────────

    SerializedProperty Prop(SerializedObject old, string oldField, string id, Object asset, string label)
    {
        SerializedProperty p = old.FindProperty(oldField);
        if (p == null)
            Add(id, asset, label, "(field absent on old asset)", "—", true,
                "the old asset predates this field; nothing to compare");
        return p;
    }

    void Float(string id, Object asset, SerializedObject old, string oldField, string label,
               float newV, bool intentional = false, string note = null)
    {
        SerializedProperty p = Prop(old, oldField, id, asset, label);
        if (p == null) return;

        float oldV = p.floatValue;
        if (Mathf.Abs(oldV - newV) <= Tolerance) return;

        Add(id, asset, label, oldV.ToString("0.#####"), newV.ToString("0.#####"), intentional, note);
    }

    void Bool(string id, Object asset, SerializedObject old, string oldField, string label, bool newV)
    {
        SerializedProperty p = Prop(old, oldField, id, asset, label);
        if (p == null || p.boolValue == newV) return;

        Add(id, asset, label, p.boolValue.ToString(), newV.ToString(), false, null);
    }

    void Enum(string id, Object asset, SerializedObject old, string oldField, string label, int newV)
    {
        SerializedProperty p = Prop(old, oldField, id, asset, label);
        if (p == null || p.enumValueIndex == newV) return;

        Add(id, asset, label, p.enumValueIndex.ToString(), newV.ToString(), false, null);
    }

    void Colour(string id, Object asset, SerializedObject old, string oldField, string label, Color newV)
    {
        SerializedProperty p = Prop(old, oldField, id, asset, label);
        if (p == null) return;

        Color oldV = p.colorValue;
        if (Mathf.Abs(oldV.r - newV.r) <= Tolerance &&
            Mathf.Abs(oldV.g - newV.g) <= Tolerance &&
            Mathf.Abs(oldV.b - newV.b) <= Tolerance &&
            Mathf.Abs(oldV.a - newV.a) <= Tolerance) return;

        Add(id, asset, label, Fmt(oldV), Fmt(newV), false, null);
    }

    static string Fmt(Color c) => $"({c.r:0.###}, {c.g:0.###}, {c.b:0.###}, {c.a:0.###})";

    void Add(string id, Object asset, string field, string oldV, string newV,
             bool intentional, string note)
    {
        _issues.Add(new Issue
        {
            layerId = id,
            field = field,
            oldValue = oldV,
            newValue = newV,
            asset = asset,
            intentional = intentional,
            note = note,
        });
    }

    // ── UI ───────────────────────────────────────────────────────────────────

    void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Run", EditorStyles.toolbarButton, GUILayout.Width(48f))) Run();

            GUILayout.FlexibleSpace();
            _showIntentional = GUILayout.Toggle(_showIntentional, "Show intentional",
                                                EditorStyles.toolbarButton, GUILayout.Width(120f));
        }

        if (!_hasRun)
        {
            EditorGUILayout.HelpBox("Press Run.", MessageType.Info);
            return;
        }

        List<Issue> real = _issues.Where(i => !i.intentional).ToList();
        List<Issue> known = _issues.Where(i => i.intentional).ToList();

        EditorGUILayout.HelpBox(
            real.Count == 0
                ? "No drift. Every migrated value still matches the asset it came from" +
                  (known.Count > 0 ? $", apart from {known.Count} deliberate difference(s)." : ".")
                : $"{real.Count} field(s) have drifted apart. Decide which side is right, " +
                  "then copy it across.",
            real.Count == 0 ? MessageType.Info : MessageType.Warning);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        foreach (Issue i in real) DrawIssue(i);

        if (_showIntentional && known.Count > 0)
        {
            GUILayout.Space(8f);
            EditorGUILayout.LabelField("DELIBERATE DIFFERENCES", EditorStyles.miniBoldLabel);
            foreach (Issue i in known) DrawIssue(i);
        }

        DrawList("NEW PROFILES WITH NO OLD COUNTERPART  (fine — a layer added since, " +
                 "or the old assets are gone)", _unpairedNew);
        DrawList("OLD PROFILES NOT YET MIGRATED", _unpairedOld);

        EditorGUILayout.EndScrollView();
    }

    void DrawIssue(Issue i)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(i.layerId, EditorStyles.miniButton, GUILayout.Width(90f)))
            {
                Selection.activeObject = i.asset;
                EditorGUIUtility.PingObject(i.asset);
            }

            EditorGUILayout.LabelField(i.field, GUILayout.Width(230f));
            EditorGUILayout.LabelField($"old {i.oldValue}   →   new {i.newValue}",
                                       EditorStyles.miniLabel);
        }

        if (string.IsNullOrEmpty(i.note)) return;

        Color c = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.6f);
        EditorGUILayout.LabelField("          " + i.note, EditorStyles.miniLabel);
        GUI.color = c;
    }

    void DrawList(string title, List<string> items)
    {
        if (items.Count == 0) return;

        GUILayout.Space(8f);
        EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
        foreach (string s in items)
            EditorGUILayout.LabelField("    " + s, EditorStyles.miniLabel);
    }
}

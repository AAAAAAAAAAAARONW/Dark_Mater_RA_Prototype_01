using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > Journey NEW > Layout > Stages
///
/// The journey scene, sorted by stage, so placing things for one stage cannot disturb another.
///
/// GROUP BY STAGE (one Undo step). Makes a "Journey Stages" folder with one folder per stage —
/// 1 · Quasar … 7 · Earth — and moves into each:
///
///   world        The stage's render layer (the WorldSwitcher_NEW group root): the web, the
///                galaxy, the planets. Big, and seldom meant to move.
///   exit door    The gate that ends the stage. Moving it is how a stage gets longer or shorter.
///   triggers     Everything else placed along the path inside the stage, by where it is: the J3b
///                filament, the galaxy edge, the Earth arrival, the Earth approach.
///
/// Nothing moves in the world: the folders sit at the origin, unturned and unscaled (and put
/// themselves back if dragged — StageFolder_NEW), and every object keeps its world position.
/// Nothing in the code finds these objects by hierarchy path, so the journey runs the same.
///
/// FOCUS. Locks the Scene view to one stage: only its door and triggers can be clicked, so the
/// huge Houdini web, the other stages and the camera rigs cannot be grabbed by mistake. "Whole
/// stage" unlocks its world too, for when the web itself is what you are moving. "Unlock all"
/// puts everything back. Picking only — nothing is hidden unless "Show only this stage" is on.
///
/// The list under each stage is everything to tune for it, placed or not: the things above,
/// plus the settings that live elsewhere (the dark matter moment, the redshift demo, the dive)
/// with a Select button each.
/// </summary>
public class StagesWindow_NEW : EditorWindow
{
    struct Def
    {
        public string layerId, title;
        public Def(string id, string t) { layerId = id; title = t; }
    }

    static readonly Def[] Defs =
    {
        new Def("Quasar", "Quasar"),
        new Def("Macro", "First Cosmic Web"),
        new Def("Micro", "Galaxy"),
        new Def("CosmicWeb", "Second Cosmic Web"),
        new Def("MilkyWay", "Milky Way"),
        new Def("SolarSystem", "Solar System"),
        new Def("Earth", "Earth")
    };

    const string RootName = "Journey Stages";
    const string FocusPref = "JourneyNEW.Stages.Focus";

    class Stage
    {
        public Def def;
        public int index;
        public StageFolder_NEW folder;
        public Transform world;
        public Transform exitDoor;
        public float entryZ = float.NegativeInfinity, exitZ = float.PositiveInfinity;
        public readonly List<Transform> triggers = new List<Transform>();
        public readonly List<UnityEngine.Object> settings = new List<UnityEngine.Object>();
        public float speed = float.NaN;
    }

    readonly List<Stage> _stages = new List<Stage>();
    Vector2 _scroll;
    bool _isolate;

    [MenuItem("Tools/Journey NEW/Layout/Stages")]
    static void Open()
    {
        StagesWindow_NEW w = GetWindow<StagesWindow_NEW>("Stages");
        w.minSize = new Vector2(440f, 420f);
        w.Collect();
    }

    void OnEnable() { Collect(); EditorApplication.hierarchyChanged += Collect; }
    void OnDisable() { EditorApplication.hierarchyChanged -= Collect; }
    void OnFocus() { Collect(); }

    // -- What belongs to which stage ---------------------------------------------------------

    void Collect()
    {
        _stages.Clear();
        if (FindObjectOfType<LayerState_NEW>() == null) { Repaint(); return; }

        Dictionary<string, Transform> worlds = WorldRoots();
        Dictionary<string, LayerGate_NEW> gates = new Dictionary<string, LayerGate_NEW>(StringComparer.OrdinalIgnoreCase);
        // Two gates into one layer (an arrival trigger as well as the door): the door is the first one met.
        foreach (LayerGate_NEW g in FindObjectsOfType<LayerGate_NEW>())
        {
            if (string.IsNullOrEmpty(g.LayerId)) continue;
            LayerGate_NEW had;
            if (!gates.TryGetValue(g.LayerId, out had) || ZOf(g.transform) < ZOf(had.transform)) gates[g.LayerId] = g;
        }

        Dictionary<string, StageFolder_NEW> folders = new Dictionary<string, StageFolder_NEW>(StringComparer.OrdinalIgnoreCase);
        foreach (StageFolder_NEW f in FindObjectsOfType<StageFolder_NEW>())
            if (!folders.ContainsKey(f.layerId)) folders.Add(f.layerId, f);

        float baseSpeed = float.NaN;
        LayerCatalog_NEW catalog = null;
        PlayerRig_NEW rig = FindObjectOfType<PlayerRig_NEW>();
        LayerState_NEW state = FindObjectOfType<LayerState_NEW>();
        if (rig != null) baseSpeed = new SerializedObject(rig).FindProperty("speed").floatValue;
        if (state != null) catalog = new SerializedObject(state).FindProperty("catalog").objectReferenceValue as LayerCatalog_NEW;

        for (int i = 0; i < Defs.Length; i++)
        {
            Stage s = new Stage { def = Defs[i], index = i };
            Transform w;
            if (worlds.TryGetValue(s.def.layerId, out w)) s.world = w;
            StageFolder_NEW f;
            if (folders.TryGetValue(s.def.layerId, out f)) s.folder = f;

            // The door into the next stage is the one that ends this one.
            LayerGate_NEW exit;
            if (i + 1 < Defs.Length && gates.TryGetValue(Defs[i + 1].layerId, out exit))
            {
                s.exitDoor = exit.transform;
                s.exitZ = ZOf(exit.transform);
            }
            if (i > 0 && _stages[i - 1].exitDoor != null) s.entryZ = _stages[i - 1].exitZ;

            LayerProfile_NEW profile = catalog != null ? catalog.Get(s.def.layerId) : null;
            if (!float.IsNaN(baseSpeed)) s.speed = baseSpeed * (profile != null ? profile.zoneSpeedMultiplier : 1f);

            _stages.Add(s);
        }

        // Triggers: things placed along the path that are neither a world nor a door.
        foreach (Transform t in TriggerCandidates(worlds))
        {
            Stage s = StageAtZ(ZOf(t));
            if (s != null) s.triggers.Add(t);
        }
        foreach (Stage s in _stages) s.triggers.Sort((a, b) => ZOf(a).CompareTo(ZOf(b)));

        AddSettings();
        Repaint();
    }

    Stage StageAtZ(float z)
    {
        foreach (Stage s in _stages)
            if (z >= s.entryZ && z < s.exitZ) return s;
        return _stages.Count > 0 ? _stages[_stages.Count - 1] : null;
    }

    static float ZOf(Transform t)
    {
        Collider c = t.GetComponent<Collider>();
        return c != null ? c.bounds.center.z : t.position.z;
    }

    static Dictionary<string, Transform> WorldRoots()
    {
        Dictionary<string, Transform> d = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
        WorldSwitcher_NEW worlds = FindObjectOfType<WorldSwitcher_NEW>();
        if (worlds == null) return d;

        SerializedProperty groups = new SerializedObject(worlds).FindProperty("groups");
        for (int i = 0; groups != null && i < groups.arraySize; i++)
        {
            SerializedProperty g = groups.GetArrayElementAtIndex(i);
            Transform root = g.FindPropertyRelative("root").objectReferenceValue as Transform;
            string id = g.FindPropertyRelative("layerId").stringValue;
            if (root != null && !d.ContainsKey(id)) d.Add(id, root);
        }
        return d;
    }

    /// <summary>
    /// The loose things along the path: every other child of the folders that hold the worlds and
    /// the doors (the original Render Layers and LayerTriggers, or the stage folders), plus every
    /// absorber trigger wherever it is — never inside a world, the player, a camera or a canvas.
    /// </summary>
    List<Transform> TriggerCandidates(Dictionary<string, Transform> worlds)
    {
        HashSet<Transform> worldSet = new HashSet<Transform>(worlds.Values);
        HashSet<Transform> doors = new HashSet<Transform>();
        foreach (Stage s in _stages) if (s.exitDoor != null) doors.Add(s.exitDoor);

        HashSet<Transform> holders = new HashSet<Transform>();
        foreach (Transform w in worldSet) if (w.parent != null) holders.Add(w.parent);
        foreach (Transform d in doors) if (d.parent != null) holders.Add(d.parent);

        HashSet<Transform> found = new HashSet<Transform>();
        foreach (Transform h in holders)
            foreach (Transform c in h)
                found.Add(c);
        foreach (FilamentAbsorber_NEW a in FindObjectsOfType<FilamentAbsorber_NEW>()) found.Add(a.transform);
        foreach (LayerGate_NEW g in FindObjectsOfType<LayerGate_NEW>()) if (!doors.Contains(g.transform)) found.Add(g.transform);

        List<Transform> list = new List<Transform>();
        foreach (Transform t in found)
        {
            if (t == null || worldSet.Contains(t) || doors.Contains(t)) continue;
            if (t.GetComponent<StageFolder_NEW>() != null) continue;
            bool inside = false;
            foreach (Transform w in worldSet) if (t.IsChildOf(w)) { inside = true; break; }
            if (inside) continue;
            if (t.GetComponentInParent<PlayerRig_NEW>() != null || t.GetComponentInParent<Camera>() != null ||
                t.GetComponentInParent<Canvas>() != null) continue;
            list.Add(t);
        }
        return list;
    }

    void AddSettings()
    {
        Add("Quasar", FindObjectOfType<JourneyOpening_NEW>(), FindObjectOfType<JourneyIntroCamera_NEW>());
        Add("Macro", FindObjectOfType<DarkMatterMoment_NEW>(), FindObjectOfType<DarkMatterScaffold_NEW>(), FindObjectOfType<JourneyHint_NEW>());
        Add("Micro", FindObjectOfType<LayerPace_NEW>());
        Add("CosmicWeb", FindObjectOfType<JourneySequence_NEW>(), FindObjectOfType<NarrationSlowdown_NEW>(), FindObjectOfType<RedshiftMarks_NEW>());
        Add("MilkyWay", FindObjectOfType<LayerPace_NEW>());
        Add("SolarSystem", GameObject.Find("VCam_SolarSystem"));
        Add("Earth", FindObjectOfType<EarthApproach_NEW>(), FindObjectOfType<EarthCutscene_NEW>());

        LayerDive_NEW dive = FindObjectOfType<LayerDive_NEW>();
        JourneyVoiceOver_NEW voice = FindObjectOfType<JourneyVoiceOver_NEW>();
        foreach (Stage s in _stages)
        {
            if (voice != null) s.settings.Add(voice);
            if (dive != null && s.index > 0) s.settings.Add(dive);
        }
    }

    void Add(string layerId, params UnityEngine.Object[] things)
    {
        Stage s = _stages.Find(x => x.def.layerId == layerId);
        if (s == null) return;
        foreach (UnityEngine.Object o in things) if (o != null) s.settings.Add(o);
    }

    // -- Grouping -------------------------------------------------------------------------

    void Group()
    {
        Collect();
        Undo.SetCurrentGroupName("Group scene by stage");
        int undo = Undo.GetCurrentGroup();

        GameObject rootGo = GameObject.Find(RootName);
        if (rootGo == null)
        {
            rootGo = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(rootGo, "Create stage folders");
        }

        int moved = 0;
        foreach (Stage s in _stages)
        {
            if (s.folder == null)
            {
                GameObject go = new GameObject((s.index + 1) + " · " + s.def.title);
                Undo.RegisterCreatedObjectUndo(go, "Create stage folder");
                Undo.SetTransformParent(go.transform, rootGo.transform, "Create stage folder");
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                s.folder = Undo.AddComponent<StageFolder_NEW>(go);
                s.folder.layerId = s.def.layerId;
                s.folder.order = s.index + 1;
            }
            s.folder.transform.SetSiblingIndex(s.index);

            List<Transform> into = new List<Transform>();
            if (s.world != null) into.Add(s.world);
            if (s.exitDoor != null) into.Add(s.exitDoor);
            into.AddRange(s.triggers);

            for (int i = 0; i < into.Count; i++)
            {
                Transform t = into[i];
                if (t.parent != s.folder.transform)
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(t) && !PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject))
                    {
                        Debug.LogWarning("[StagesWindow_NEW] '" + t.name + "' is inside a prefab and cannot move; left where it is.", t);
                        continue;
                    }
                    Vector3 p = t.position; Quaternion r = t.rotation;
                    Undo.SetTransformParent(t, s.folder.transform, "Group scene by stage");
                    t.position = p; t.rotation = r;
                    moved++;
                }
                t.SetSiblingIndex(i);
            }
        }

        Undo.CollapseUndoOperations(undo);
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("[StagesWindow_NEW] Grouped by stage: " + moved + " object(s) moved under '" + RootName +
                  "'. World positions unchanged. Save the scene; Ctrl+Z undoes it.", rootGo);
        Collect();
    }

    bool Grouped { get { return _stages.Exists(s => s.folder != null); } }

    // -- Focus ----------------------------------------------------------------------------

    void Focus(Stage s, bool wholeStage)
    {
        SceneVisibilityManager v = SceneVisibilityManager.instance;
        v.DisableAllPicking();

        List<GameObject> open = new List<GameObject>();
        if (s.exitDoor != null) open.Add(s.exitDoor.gameObject);
        foreach (Transform t in s.triggers) open.Add(t.gameObject);
        if (wholeStage && s.world != null) open.Add(s.world.gameObject);
        foreach (GameObject go in open) v.EnablePicking(go, true);

        if (_isolate && s.folder != null) v.Isolate(s.folder.gameObject, true);
        else v.ExitIsolation();

        EditorPrefs.SetString(FocusPref, s.def.layerId + (wholeStage ? " (whole)" : ""));
        SceneView.RepaintAll();
    }

    void UnlockAll()
    {
        SceneVisibilityManager.instance.EnableAllPicking();
        SceneVisibilityManager.instance.ExitIsolation();
        EditorPrefs.SetString(FocusPref, "");
        SceneView.RepaintAll();
    }

    // -- Window ---------------------------------------------------------------------------

    void OnGUI()
    {
        if (_stages.Count == 0)
        {
            EditorGUILayout.HelpBox("Open a journey scene (Gameplay_Scene_Ana).", MessageType.Info);
            return;
        }

        EditorGUILayout.HelpBox(
            "Focus a stage to lock the Scene view to its door and triggers: nothing else can be clicked, so the web " +
            "and the other stages are safe. 'Whole stage' unlocks its world as well. Group by stage puts each stage's " +
            "things under one folder in the Hierarchy; nothing moves in the world.", MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = !EditorApplication.isPlaying;
            if (GUILayout.Button(Grouped ? "Re-group (put new things in their stage)" : "Group scene by stage")) Group();
            GUI.enabled = true;
            if (GUILayout.Button("Unlock all", GUILayout.Width(90f))) UnlockAll();
        }
        _isolate = EditorGUILayout.ToggleLeft("Show only this stage while focused", _isolate);

        string focus = EditorPrefs.GetString(FocusPref, "");
        EditorGUILayout.LabelField(string.IsNullOrEmpty(focus) ? "Focus: none — everything can be clicked." : "Focus: " + focus,
                                   EditorStyles.miniBoldLabel);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        foreach (Stage s in _stages) DrawStage(s);
        EditorGUILayout.EndScrollView();
    }

    void DrawStage(Stage s)
    {
        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            string span = "";
            if (!float.IsInfinity(s.entryZ) && !float.IsInfinity(s.exitZ))
            {
                float len = s.exitZ - s.entryZ;
                span = string.Format("   z {0:0} → {1:0}   {2:0} units", s.entryZ, s.exitZ, len);
                if (!float.IsNaN(s.speed) && s.speed > 0f) span += string.Format("  ≈{0:0} s", len / s.speed);
            }
            EditorGUILayout.LabelField((s.index + 1) + " · " + s.def.title + span, EditorStyles.boldLabel);
            if (GUILayout.Button("Focus", EditorStyles.miniButtonLeft, GUILayout.Width(50f))) Focus(s, false);
            if (GUILayout.Button("Whole stage", EditorStyles.miniButtonRight, GUILayout.Width(80f))) Focus(s, true);
        }

        using (new EditorGUI.IndentLevelScope())
        {
            Row("World", s.world, "the render layer — locked unless 'Whole stage'");
            Row("Exit door", s.exitDoor, s.exitDoor != null ? string.Format("z {0:0.0} — move to lengthen or shorten the stage", ZOf(s.exitDoor)) : null);
            foreach (Transform t in s.triggers) Row("Trigger", t, string.Format("z {0:0.0}", ZOf(t)));
            foreach (UnityEngine.Object o in s.settings) Row("Settings", o, o.GetType().Name);
        }
    }

    static void Row(string kind, UnityEngine.Object o, string note)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(kind, o != null ? o.name : "—", EditorStyles.label);
            if (!string.IsNullOrEmpty(note)) GUILayout.Label(note, EditorStyles.miniLabel);
            GUI.enabled = o != null;
            if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(50f)))
            {
                Selection.activeObject = o is Component c ? c.gameObject : o;
                EditorGUIUtility.PingObject(Selection.activeObject);
                if (o is Component || o is GameObject) SceneView.FrameLastActiveSceneView();
            }
            GUI.enabled = true;
        }
    }
}

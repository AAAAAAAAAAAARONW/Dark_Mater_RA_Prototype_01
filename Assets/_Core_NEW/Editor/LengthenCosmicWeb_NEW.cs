using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Make a cosmic web longer, so its lines fit at its speed:
/// Tools > Journey NEW > Layout > Lengthen First Cosmic Web… / Lengthen Second Cosmic Web…
///
/// WHY. A layer's time is its length over its speed, and the speed is part of how the piece
/// feels, so when a layer's lines do not fit, the layer gets longer. The second web was first
/// (J5 and the redshift demo, about a minute, in 55 units at 2.5 units/s). The first web is
/// next: J3, the dark matter moment and J3b need about a minute, and it was 130 units at
/// 5 units/s, 26 seconds.
///
/// WHAT MOVES. The door at the far end of the layer (the tracker's layerTriggers), the doors
/// after it, and every object whose world Z is at or beyond that door — everything later in
/// the journey — as the top-most such object in its hierarchy, so a group moves as one. Never
/// moved: the player, cameras, UI, and the web itself (the layer's render group).
///
/// THE WEB ITSELF, three choices:
///   Leave       Only the doors and what is after them move. The web keeps its size, and the
///               new stretch of path may run through nothing — extend the web by hand.
///   Scale       The web grows uniformly about the entry door, by (old + extra) / old, so it
///               reaches the moved door. It looks the same, only bigger: the light feels
///               smaller and slower inside it.
///   Stretch     The web grows only along the path, by the same factor. Its threads along the
///               path get longer; across it, nothing changes.
/// Either way, a dive that aims into the web just past the far door (the galaxy dive aims at
/// the yellow cluster 11 units past the Micro door) has its focusPastGate scaled to match, so
/// it still aims at the same cluster. Filament triggers and placed markers inside the web do
/// not follow: put J3b back on a filament with the Flight Path window.
///
/// The tracker derives light-years per unit from the door positions, so the journey's data
/// mapping follows by itself. One Undo step. Save the scene afterwards.
/// </summary>
public class LengthenCosmicWeb_NEW : EditorWindow
{
    enum WebMode { Leave, Scale, Stretch }

    struct Target
    {
        public string title, layerId, nextLayerId;
        public int endDoor;         // the tracker's layerTriggers index of the door at the layer's far end
        public float defaultExtra;
    }

    static readonly Target First = new Target
    {
        title = "First Cosmic Web", layerId = "Macro", nextLayerId = "Micro", endDoor = 1, defaultExtra = 175f
    };

    static readonly Target Second = new Target
    {
        title = "Second Cosmic Web", layerId = "CosmicWeb", nextLayerId = "MilkyWay", endDoor = 3, defaultExtra = 100f
    };

    Target _target = First;
    float _extra = 175f;
    float _wantSeconds = 62f;
    WebMode _web = WebMode.Scale;
    Vector2 _scroll;
    readonly List<Transform> _preview = new List<Transform>();
    float _doorZ = float.NaN, _entryZ = float.NaN, _speed = float.NaN;
    Transform _entryDoor, _webRoot;
    string _problem;

    [MenuItem("Tools/Journey NEW/Layout/Lengthen First Cosmic Web…")]
    static void OpenFirst() { Open(First); }

    [MenuItem("Tools/Journey NEW/Layout/Lengthen Second Cosmic Web…")]
    static void OpenSecond() { Open(Second); }

    static void Open(Target t)
    {
        LengthenCosmicWeb_NEW w = GetWindow<LengthenCosmicWeb_NEW>(true, "Lengthen " + t.title);
        w.titleContent = new GUIContent("Lengthen " + t.title);
        w.minSize = new Vector2(480f, 480f);
        w._target = t;
        w._extra = t.defaultExtra;
        w._web = t.layerId == "Macro" ? WebMode.Scale : WebMode.Leave;
        w.Refresh();
    }

    void Refresh()
    {
        _preview.Clear();
        _problem = null;
        _doorZ = _entryZ = _speed = float.NaN;
        _entryDoor = _webRoot = null;

        UniverseJourneyTracker tracker = FindObjectOfType<UniverseJourneyTracker>();
        if (tracker == null) { _problem = "No UniverseJourneyTracker in the open scene."; return; }

        SerializedProperty doors = new SerializedObject(tracker).FindProperty("layerTriggers");
        if (doors == null || doors.arraySize <= _target.endDoor)
        { _problem = "The tracker has fewer than " + (_target.endDoor + 1) + " layer triggers."; return; }

        Transform door = doors.GetArrayElementAtIndex(_target.endDoor).objectReferenceValue as Transform;
        _entryDoor = _target.endDoor > 0 ? doors.GetArrayElementAtIndex(_target.endDoor - 1).objectReferenceValue as Transform : null;
        if (door == null) { _problem = "The tracker's door layerTriggers[" + _target.endDoor + "] is empty."; return; }
        _doorZ = door.position.z;
        _entryZ = _entryDoor != null ? _entryDoor.position.z : float.NaN;

        _webRoot = GroupRoot(_target.layerId);
        _speed = LayerSpeed(_target.layerId);

        var chosen = new HashSet<Transform>();
        for (int i = _target.endDoor; i < doors.arraySize; i++)
        {
            Transform t = doors.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
            if (t != null) chosen.Add(t);
        }

        PlayerRig_NEW rig = FindObjectOfType<PlayerRig_NEW>();
        Transform playerRoot = rig != null ? rig.transform.root : null;

        foreach (Transform t in FindObjectsOfType<Transform>())
        {
            if (t is RectTransform) continue;
            if (t.position.z < _doorZ - 0.01f) continue;
            if (playerRoot != null && t.root == playerRoot) continue;
            if (_webRoot != null && (t == _webRoot || t.IsChildOf(_webRoot))) continue;
            if (t.GetComponentInParent<Camera>() != null) continue;
            if (t.GetComponentInParent<Canvas>() != null) continue;
            if (t.GetComponent<Cinemachine.CinemachineVirtualCameraBase>() != null) continue;
            chosen.Add(t);
        }

        // Top-most only: a child of something that already moves would move twice. And never a
        // parent of the web: moving it would move the web.
        foreach (Transform t in chosen)
        {
            bool covered = false;
            for (Transform p = t.parent; p != null; p = p.parent)
                if (chosen.Contains(p)) { covered = true; break; }
            if (covered) continue;
            if (_webRoot != null && _webRoot.IsChildOf(t)) continue;
            _preview.Add(t);
        }
        _preview.Sort((a, b) => a.position.z.CompareTo(b.position.z));
    }

    float Length { get { return _doorZ - _entryZ; } }

    float Factor { get { return Length > 0.01f ? (Length + _extra) / Length : 1f; } }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Moves the door at the far end of the " + _target.title.ToLower() + " (" + _target.layerId + " → " +
            _target.nextLayerId + ") and everything after it along +Z, so the layer lasts longer at the same speed. " +
            "Undo reverts it. Save the scene afterwards.", MessageType.Info);

        if (GUILayout.Button("Refresh")) Refresh();
        if (_problem != null) { EditorGUILayout.HelpBox(_problem, MessageType.Error); return; }

        bool known = !float.IsNaN(_entryZ) && !float.IsNaN(_speed) && _speed > 0f;
        if (known)
        {
            EditorGUILayout.LabelField(string.Format("Now: {0:0} units at {1:0.0} units/s = {2:0} s.", Length, _speed, Length / _speed));
            using (new EditorGUILayout.HorizontalScope())
            {
                _wantSeconds = EditorGUILayout.FloatField("Seconds wanted", _wantSeconds);
                if (GUILayout.Button("Set extra", GUILayout.Width(80f))) _extra = Mathf.Max(0f, _wantSeconds * _speed - Length);
            }
            EditorGUILayout.LabelField("  The Flight Path window (Tools > Journey NEW > Flight Path) shows how long the lines in this layer run.",
                                       EditorStyles.miniLabel);
        }

        _extra = EditorGUILayout.FloatField("Extra length (units)", _extra);
        if (known)
            EditorGUILayout.LabelField(string.Format("After: {0:0} units = {1:0} s.", Length + _extra, (Length + _extra) / _speed));

        EditorGUILayout.Space();
        _web = (WebMode)EditorGUILayout.EnumPopup("The web itself", _web);
        switch (_web)
        {
            case WebMode.Leave:
                EditorGUILayout.HelpBox("The web keeps its size; the new stretch may run through empty space.", MessageType.None); break;
            case WebMode.Scale:
                EditorGUILayout.HelpBox(string.Format("The web grows ×{0:0.00} in every direction, about the entry door. Same look, " +
                                                      "bigger: the light will feel smaller and slower inside it.", Factor), MessageType.None); break;
            case WebMode.Stretch:
                EditorGUILayout.HelpBox(string.Format("The web grows ×{0:0.00} along the path only: threads along the path get longer.", Factor),
                                        MessageType.None); break;
        }
        if (_web != WebMode.Leave && _webRoot == null)
            EditorGUILayout.HelpBox("No render group for '" + _target.layerId + "' in WorldSwitcher_NEW: the web cannot be scaled.", MessageType.Warning);
        if (_web != WebMode.Leave && _entryDoor == null)
            EditorGUILayout.HelpBox("No entry door: nothing to scale about.", MessageType.Warning);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(string.Format("Door at Z {0:0.0} → {1:0.0}.  {2} object(s) move:", _doorZ, _doorZ + _extra, _preview.Count));
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        foreach (Transform t in _preview)
            if (t != null) EditorGUILayout.LabelField(string.Format("Z {0,7:0.0}   {1}", t.position.z, Path(t)));
        EditorGUILayout.EndScrollView();

        GUI.enabled = _preview.Count > 0 && Mathf.Abs(_extra) > 0.001f && !EditorApplication.isPlaying;
        if (GUILayout.Button("Lengthen by " + _extra.ToString("0.0")))
            Apply();
        GUI.enabled = true;
        if (EditorApplication.isPlaying) EditorGUILayout.HelpBox("Stop play mode first.", MessageType.Warning);
    }

    void Apply()
    {
        Undo.SetCurrentGroupName("Lengthen " + _target.title);
        int group = Undo.GetCurrentGroup();
        var log = new StringBuilder("[LengthenCosmicWeb_NEW] " + _target.title + ": moved by " + _extra.ToString("0.0") + " along Z:\n");
        float k = Factor;

        foreach (Transform t in _preview)
        {
            if (t == null) continue;
            Undo.RecordObject(t, "Lengthen " + _target.title);
            Vector3 p = t.position;
            log.AppendLine(string.Format("  {0,7:0.0} → {1,7:0.0}   {2}", p.z, p.z + _extra, Path(t)));
            t.position = new Vector3(p.x, p.y, p.z + _extra);
            EditorUtility.SetDirty(t);
        }

        if (_web != WebMode.Leave && _webRoot != null && _entryDoor != null && Mathf.Abs(k - 1f) > 0.0001f)
        {
            if (ScaleWeb(k, log)) ScaleDiveFocus(k, log);
        }

        Undo.CollapseUndoOperations(group);
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        log.AppendLine("Next: Flight Path window → put J3b back on a filament, and fly the layer again (2) to re-record.");
        Debug.Log(log.ToString());
        Refresh();
    }

    bool ScaleWeb(float k, StringBuilder log)
    {
        Vector3 pivot = _entryDoor.position;
        Transform root = _webRoot;
        Undo.RecordObject(root, "Lengthen " + _target.title);

        if (_web == WebMode.Scale)
        {
            root.position = pivot + (root.position - pivot) * k;
            root.localScale = root.localScale * k;
        }
        else
        {
            // Only along world Z, which is only a local axis if nothing above it is turned.
            for (Transform p = root; p != null; p = p.parent)
                if (Quaternion.Angle(p.localRotation, Quaternion.identity) > 0.01f)
                {
                    Debug.LogWarning("[LengthenCosmicWeb_NEW] '" + Path(p) + "' is rotated, so the web cannot be stretched " +
                                     "along the path alone. Use Scale, or Leave and stretch it by hand.", p);
                    return false;
                }

            Vector3 pos = root.position;
            root.position = new Vector3(pos.x, pos.y, pivot.z + (pos.z - pivot.z) * k);
            Vector3 s = root.localScale;
            root.localScale = new Vector3(s.x, s.y, s.z * k);
        }

        EditorUtility.SetDirty(root);
        log.AppendLine(string.Format("  web '{0}' {1} ×{2:0.00} about Z {3:0.0}", Path(root), _web == WebMode.Scale ? "scaled" : "stretched", k, pivot.z));
        return true;
    }

    /// <summary>The dive into the next layer aims a set distance past the door, into the web: scale that too.</summary>
    void ScaleDiveFocus(float k, StringBuilder log)
    {
        LayerDive_NEW dive = FindObjectOfType<LayerDive_NEW>();
        if (dive == null) return;

        SerializedObject so = new SerializedObject(dive);
        SerializedProperty gates = so.FindProperty("gates");
        if (gates == null) return;

        for (int i = 0; i < gates.arraySize; i++)
        {
            SerializedProperty g = gates.GetArrayElementAtIndex(i);
            if (g.FindPropertyRelative("toLayerId").stringValue != _target.nextLayerId) continue;
            if (g.FindPropertyRelative("direction").enumValueIndex != 0) continue;           // In only
            if (g.FindPropertyRelative("focusOverride").objectReferenceValue != null) continue;

            SerializedProperty focus = g.FindPropertyRelative("focusPastGate");
            float before = focus.floatValue;
            focus.floatValue = before * k;
            log.AppendLine(string.Format("  dive into {0}: focusPastGate {1:0.0} → {2:0.0}, still on the same cluster",
                                         _target.nextLayerId, before, focus.floatValue));
        }
        so.ApplyModifiedProperties();
    }

    static Transform GroupRoot(string layerId)
    {
        WorldSwitcher_NEW worlds = FindObjectOfType<WorldSwitcher_NEW>();
        if (worlds == null) return null;

        SerializedProperty groups = new SerializedObject(worlds).FindProperty("groups");
        for (int i = 0; groups != null && i < groups.arraySize; i++)
        {
            SerializedProperty g = groups.GetArrayElementAtIndex(i);
            if (g.FindPropertyRelative("layerId").stringValue == layerId)
                return g.FindPropertyRelative("root").objectReferenceValue as Transform;
        }
        return null;
    }

    static float LayerSpeed(string layerId)
    {
        PlayerRig_NEW rig = FindObjectOfType<PlayerRig_NEW>();
        LayerState_NEW state = FindObjectOfType<LayerState_NEW>();
        if (rig == null || state == null) return float.NaN;

        float baseSpeed = new SerializedObject(rig).FindProperty("speed").floatValue;
        LayerCatalog_NEW catalog = new SerializedObject(state).FindProperty("catalog").objectReferenceValue as LayerCatalog_NEW;
        LayerProfile_NEW profile = catalog != null ? catalog.Get(layerId) : null;
        return baseSpeed * (profile != null ? profile.zoneSpeedMultiplier : 1f);
    }

    static string Path(Transform t)
    {
        string s = t.name;
        for (Transform p = t.parent; p != null; p = p.parent) s = p.name + "/" + s;
        return s;
    }
}

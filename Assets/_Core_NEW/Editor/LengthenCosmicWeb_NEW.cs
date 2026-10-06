using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Make the second cosmic web longer: move the Milky Way door, and everything beyond it, further
/// along +Z. Tools > Journey NEW > Layout > Lengthen Second Cosmic Web…
///
/// WHY. The redshift explanation (J5, then the demo's F1–F6) runs about a minute; at the
/// cosmic web's speed (2.5 units/s) its 55 units last ~22 s, so the demo never got to play.
///
/// WHAT MOVES. The tracker's doors from the Milky Way door on (layerTriggers[3] onwards), and
/// every other object whose world Z is at or beyond that door — the Milky Way, solar system and
/// Earth content — as the top-most such object in its hierarchy, so a group moves as one.
/// Never moved: the player, cameras, UI. Every move is one Undo step.
///
/// The tracker derives light-years per unit from the door positions, so the journey's data
/// mapping follows by itself. The cosmic web's OWN visuals are not stretched: look along the new
/// stretch afterwards and extend or duplicate them if it runs empty.
/// </summary>
public class LengthenCosmicWeb_NEW : EditorWindow
{
    float _extra = 100f;
    Vector2 _scroll;
    List<Transform> _preview = new List<Transform>();
    float _doorZ = float.NaN;
    string _problem;

    [MenuItem("Tools/Journey NEW/Layout/Lengthen Second Cosmic Web…")]
    static void Open()
    {
        LengthenCosmicWeb_NEW w = GetWindow<LengthenCosmicWeb_NEW>(true, "Lengthen Second Cosmic Web");
        w.minSize = new Vector2(460f, 420f);
        w.Refresh();
    }

    void Refresh()
    {
        _preview.Clear();
        _problem = null;
        _doorZ = float.NaN;

        UniverseJourneyTracker tracker = FindObjectOfType<UniverseJourneyTracker>();
        if (tracker == null) { _problem = "No UniverseJourneyTracker in the open scene."; return; }

        SerializedProperty doors = new SerializedObject(tracker).FindProperty("layerTriggers");
        if (doors == null || doors.arraySize < 4) { _problem = "The tracker has fewer than 4 layer triggers."; return; }

        Transform mwDoor = doors.GetArrayElementAtIndex(3).objectReferenceValue as Transform;
        if (mwDoor == null) { _problem = "The tracker's Milky Way door (layerTriggers[3]) is empty."; return; }
        _doorZ = mwDoor.position.z;

        var chosen = new HashSet<Transform>();
        for (int i = 3; i < doors.arraySize; i++)
        {
            Transform t = doors.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
            if (t != null) chosen.Add(t);
        }

        Transform player = tracker.transform;
        PlayerRig_NEW rig = FindObjectOfType<PlayerRig_NEW>();
        Transform playerRoot = rig != null ? rig.transform.root : null;

        foreach (Transform t in FindObjectsOfType<Transform>())
        {
            if (t is RectTransform) continue;
            if (t.position.z < _doorZ - 0.01f) continue;
            if (playerRoot != null && t.root == playerRoot) continue;
            if (t.GetComponentInParent<Camera>() != null) continue;
            if (t.GetComponentInParent<Canvas>() != null) continue;
            if (t.GetComponent<Cinemachine.CinemachineVirtualCameraBase>() != null) continue;
            chosen.Add(t);
        }

        // Top-most only: a child of something that already moves would move twice.
        foreach (Transform t in chosen)
        {
            bool covered = false;
            for (Transform p = t.parent; p != null; p = p.parent)
                if (chosen.Contains(p)) { covered = true; break; }
            if (!covered) _preview.Add(t);
        }
        _preview.Sort((a, b) => a.position.z.CompareTo(b.position.z));
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Moves the Milky Way door and everything beyond it along +Z, so the second cosmic web lasts " +
            "longer. At 2.5 units/s, +100 ≈ +40 s (55 → 155 units ≈ 62 s: room for J5 and the whole " +
            "redshift demo). Undo reverts it. Save the scene afterwards.", MessageType.Info);

        _extra = EditorGUILayout.FloatField("Extra length (units)", _extra);
        if (GUILayout.Button("Refresh list")) Refresh();

        if (_problem != null) { EditorGUILayout.HelpBox(_problem, MessageType.Error); return; }

        EditorGUILayout.LabelField(string.Format("Milky Way door at Z {0:0.0} → {1:0.0}.  {2} object(s) move:",
                                                 _doorZ, _doorZ + _extra, _preview.Count));
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        foreach (Transform t in _preview)
            if (t != null) EditorGUILayout.LabelField(string.Format("Z {0,7:0.0}   {1}", t.position.z, Path(t)));
        EditorGUILayout.EndScrollView();

        GUI.enabled = _preview.Count > 0 && Mathf.Abs(_extra) > 0.001f && !EditorApplication.isPlaying;
        if (GUILayout.Button("Move " + _preview.Count + " object(s) by " + _extra.ToString("0.0")))
            Apply();
        GUI.enabled = true;
        if (EditorApplication.isPlaying) EditorGUILayout.HelpBox("Stop play mode first.", MessageType.Warning);
    }

    void Apply()
    {
        Undo.SetCurrentGroupName("Lengthen Second Cosmic Web");
        int group = Undo.GetCurrentGroup();
        var log = new StringBuilder("[LengthenCosmicWeb_NEW] Moved by " + _extra.ToString("0.0") + " along Z:\n");

        foreach (Transform t in _preview)
        {
            if (t == null) continue;
            Undo.RecordObject(t, "Lengthen Second Cosmic Web");
            Vector3 p = t.position;
            log.AppendLine(string.Format("  {0,7:0.0} → {1,7:0.0}   {2}", p.z, p.z + _extra, Path(t)));
            t.position = new Vector3(p.x, p.y, p.z + _extra);
            EditorUtility.SetDirty(t);
        }

        Undo.CollapseUndoOperations(group);
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log(log.ToString());
        Refresh();
    }

    static string Path(Transform t)
    {
        string s = t.name;
        for (Transform p = t.parent; p != null; p = p.parent) s = p.name + "/" + s;
        return s;
    }
}

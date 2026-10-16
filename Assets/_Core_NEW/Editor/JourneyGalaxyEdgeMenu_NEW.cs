using UnityEditor;
using UnityEngine;

/// <summary>
/// Puts the galaxy-edge absorber into the open journey scene: a FilamentAbsorber_NEW of kind
/// GalaxyEdge on a thin trigger across the light's path, just inside the gate into the galaxy
/// (Micro), where the light enters the galaxy's outskirts. Crossing it cuts a damped line —
/// broad and black, next to the filament's thin one — and blinks it in its own colour. It
/// says nothing: J4 is said as the light arrives.
///
/// Its place is a first guess, as the filament's is. Move it along Z to where the light is
/// visibly passing the galaxy's edge, with the trail in shot. If the scene already has one,
/// this selects it and changes nothing.
///
/// It works on the scene as loaded in the editor, as one undoable step. Save the scene to keep it.
/// </summary>
static class JourneyGalaxyEdgeMenu_NEW
{
    const string MenuPath = "Tools/Journey NEW/Spectrum/Add Galaxy-Edge Absorber (open scene)";
    const string ObjectName = "Absorber_GalaxyEdge";

    /// <summary>Past the galaxy gate by this much: Micro runs at half speed, about two seconds of flight.</summary>
    const float InsideGate = 6f;

    const float Depth = 1f;
    const float WidthKmS = 3200f;

    [MenuItem(MenuPath)]
    static void AddEdgeAbsorber()
    {
        foreach (FilamentAbsorber_NEW a in Object.FindObjectsOfType<FilamentAbsorber_NEW>())
        {
            if (a.AbsorberKind != FilamentAbsorber_NEW.Kind.GalaxyEdge) continue;
            Debug.Log("Journey NEW: '" + a.name + "' is already the galaxy-edge absorber; left as it is.", a);
            Selection.activeGameObject = a.gameObject;
            return;
        }

        Collider gate = FindGateCollider("Micro");
        if (gate == null)
        {
            EditorUtility.DisplayDialog("Galaxy-edge absorber",
                "No gate into the 'Micro' layer in the open scene, so there is nowhere to put it. " +
                "Open Gameplay_Scene_Ana and run it again.", "OK");
            return;
        }

        GameObject go = new GameObject(ObjectName);
        Undo.RegisterCreatedObjectUndo(go, "Add galaxy-edge absorber");
        if (gate.transform.parent != null) go.transform.SetParent(gate.transform.parent, true);

        Bounds b = gate.bounds;
        go.transform.position = b.center + Vector3.forward * InsideGate;

        BoxCollider box = Undo.AddComponent<BoxCollider>(go);
        box.isTrigger = true;
        box.size = new Vector3(Mathf.Max(b.size.x, 4f), Mathf.Max(b.size.y, 4f), 3f);

        FilamentAbsorber_NEW absorber = Undo.AddComponent<FilamentAbsorber_NEW>(go);

        SerializedObject so = new SerializedObject(absorber);
        so.FindProperty("kind").enumValueIndex = (int)FilamentAbsorber_NEW.Kind.GalaxyEdge;
        so.FindProperty("depth").floatValue = Depth;
        so.FindProperty("widthKmS").floatValue = WidthKmS;
        so.FindProperty("source").objectReferenceValue = Object.FindObjectOfType<BakedSpectrumSource_NEW>();
        so.FindProperty("highlight").objectReferenceValue = Object.FindObjectOfType<AbsorberHighlight_NEW>();
        so.ApplyModifiedProperties();

        Selection.activeGameObject = go;
        Debug.Log("Journey NEW: added '" + ObjectName + "', " + InsideGate + " units inside the galaxy gate. " +
                  "Move it along Z to where the light passes the galaxy's edge, then save the scene.", go);
    }

    static Collider FindGateCollider(string layerId)
    {
        foreach (LayerGate_NEW g in Object.FindObjectsOfType<LayerGate_NEW>())
            if (g.LayerId == layerId) return g.GetComponent<Collider>();
        return null;
    }
}

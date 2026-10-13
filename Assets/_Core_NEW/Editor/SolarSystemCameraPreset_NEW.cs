using Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Journey NEW > Solar System > ... : a better first look at the Solar System, as
/// two undoable steps you can try separately.
///
/// WHAT WAS WRONG (from the two screenshots, before and after recentre):
///
///   1. Edge-on. Every planet and the Sun sit on y = 0, the plane the light flies in. The
///      system is seen exactly side-on: one horizontal line, half of it behind the trail.
///   2. Too close and too narrow. VCam_SolarSystem was 3 units behind and 1 above the
///      light with a 20 degree lens, so the trail filled the bottom half of the frame and,
///      after recentring, the Sun was cropped at the top-left and the planets were gone.
///
/// "Camera Preset" fixes 2: a wider lens and the camera further back and higher, at about
/// the same angle, so the trail shrinks and the Sun and planets come into frame.
/// "Tilt the System" fixes 1: the SolarSystem group is turned about the Sun so its plane
/// is inclined to the flight, and the orbits read as orbits rather than as a line. That one
/// moves art, so it is separate, and either can be undone on its own (Ctrl+Z).
///
/// The values are a starting point to judge by eye. Every old value goes to the Console.
/// </summary>
public static class SolarSystemCameraPreset_NEW
{
    const float FieldOfView = 30f;

    // (height, radius) for the top, middle and bottom rigs. The middle rig is what the
    // camera rests on: 1.6 up, 5 back, the same ~18 degrees as before but further away.
    static readonly Vector2[] Orbits = { new Vector2(4.5f, 2f), new Vector2(1.6f, 5f), new Vector2(-2.5f, 2f) };

    const float TiltDegrees = 15f;

    [MenuItem("Tools/Journey NEW/Solar System/Camera Preset (wider, further back)")]
    static void ApplyCamera()
    {
        CinemachineFreeLook vcam = FindSolarCamera();
        if (vcam == null) return;

        Undo.RecordObject(vcam, "Solar System camera preset");

        Debug.Log("[SolarSystemCameraPreset_NEW] Was: field of view " + vcam.m_Lens.FieldOfView +
                  ", rigs " + Describe(vcam) + ".", vcam);

        vcam.m_Lens.FieldOfView = FieldOfView;
        for (int i = 0; i < 3 && i < vcam.m_Orbits.Length; i++)
        {
            vcam.m_Orbits[i].m_Height = Orbits[i].x;
            vcam.m_Orbits[i].m_Radius = Orbits[i].y;
        }

        EditorUtility.SetDirty(vcam);
        EditorSceneManager.MarkSceneDirty(vcam.gameObject.scene);

        Debug.Log("[SolarSystemCameraPreset_NEW] Now: field of view " + FieldOfView + ", rigs " + Describe(vcam) +
                  ". Ctrl+Z to undo. Save the scene to keep it.", vcam);
    }

    [MenuItem("Tools/Journey NEW/Solar System/Tilt the System (see its orbits)")]
    static void Tilt()
    {
        GameObject system = GameObject.Find("SolarSystem");
        if (system == null)
        {
            EditorUtility.DisplayDialog("Solar System", "No object named SolarSystem in the open scene.", "OK");
            return;
        }

        GameObject sun = GameObject.Find("Sun");
        Vector3 pivot = sun != null ? sun.transform.position : system.transform.position;

        Undo.RecordObject(system.transform, "Tilt the Solar System");

        // Tipped towards the camera, which comes in from -Z: the far side of the system rises,
        // the near side drops below the light's path, and the trail stops covering the planets.
        system.transform.RotateAround(pivot, Vector3.right, -TiltDegrees);

        EditorSceneManager.MarkSceneDirty(system.scene);
        Debug.Log("[SolarSystemCameraPreset_NEW] Tilted SolarSystem " + TiltDegrees + " degrees about the Sun. " +
                  "Ctrl+Z to undo. Check that the Earth gate is still where the light reaches Earth.", system);
    }

    [MenuItem("Tools/Journey NEW/Solar System/Aim the Light at Earth")]
    static void AimAtEarth()
    {
        EarthApproach_NEW existing = Object.FindObjectOfType<EarthApproach_NEW>();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            Debug.Log("[SolarSystemCameraPreset_NEW] EarthApproach_NEW is already in the scene.", existing);
            return;
        }

        GameObject system = GameObject.Find("SolarSystem");

        GameObject go = new GameObject("EarthApproach");
        Undo.RegisterCreatedObjectUndo(go, "Aim the light at Earth");
        if (system != null && system.transform.parent != null) go.transform.SetParent(system.transform.parent, false);

        Undo.AddComponent<EarthApproach_NEW>(go);
        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(go.scene);

        Debug.Log("[SolarSystemCameraPreset_NEW] Added EarthApproach: across the Solar System the light now bends onto " +
                  "the Earth, and the Earth gate moves to just past it at the start of Play. Save the scene.", go);
    }

    static CinemachineFreeLook FindSolarCamera()
    {
        GameObject go = GameObject.Find("VCam_SolarSystem");
        CinemachineFreeLook vcam = go != null ? go.GetComponent<CinemachineFreeLook>() : null;

        if (vcam == null)
            EditorUtility.DisplayDialog("Solar System", "No CinemachineFreeLook named VCam_SolarSystem in the open scene. " +
                                        "Open Gameplay_Scene_Ana.", "OK");
        return vcam;
    }

    static string Describe(CinemachineFreeLook vcam)
    {
        string s = "";
        for (int i = 0; i < vcam.m_Orbits.Length; i++)
            s += (i > 0 ? ", " : "") + "(h " + vcam.m_Orbits[i].m_Height + ", r " + vcam.m_Orbits[i].m_Radius + ")";
        return s;
    }
}

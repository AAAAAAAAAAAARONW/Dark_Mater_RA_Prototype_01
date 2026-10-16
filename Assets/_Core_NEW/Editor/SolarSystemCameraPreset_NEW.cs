using Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Journey NEW > Solar System > Camera Preset: a better first look at the Solar
/// System. VCam_SolarSystem was 3 units behind and 1 above the light with a 20 degree lens,
/// so the trail filled the bottom half of the frame and, after recentring, the Sun was
/// cropped at the top-left and the planets were gone. The preset gives a wider lens and the
/// camera further back and higher, at about the same angle, so the trail shrinks and the Sun
/// and planets come into frame. Undoable (Ctrl+Z); every old value goes to the Console.
///
/// LATER: NO TILT, NO STEERING. Two more steps used to be here. "Tilt the System" turned the
/// SolarSystem group 15 degrees about the Sun so the orbits read as ellipses, and "Aim the
/// Light at Earth" added EarthApproach_NEW's steering to make up for it. Together they left
/// the planets at different heights and Earth 1.5 above the light, and the light turning
/// onto it. The light only goes straight on, and the planets sit level in its plane, laid
/// out along its line by SolarSystemPath_NEW; both steps are gone.
/// </summary>
public static class SolarSystemCameraPreset_NEW
{
    const float FieldOfView = 30f;

    // (height, radius) for the top, middle and bottom rigs. The middle rig is what the
    // camera rests on: 1.6 up, 5 back, the same ~18 degrees as before but further away.
    static readonly Vector2[] Orbits = { new Vector2(4.5f, 2f), new Vector2(1.6f, 5f), new Vector2(-2.5f, 2f) };

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

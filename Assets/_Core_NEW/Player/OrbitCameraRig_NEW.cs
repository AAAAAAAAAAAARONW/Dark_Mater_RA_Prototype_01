using System;
using Cinemachine;
using UnityEngine;

/// <summary>
/// Drives the FreeLook orbit axes directly and handles the recentre-view action.
///
/// Extracted from DarkMatterPlayerControllerTest, where the same twelve lines were
/// pasted once per camera in DriveFreeLookInput and again in TickResetView. This holds
/// the cameras in a list and loops, which is the same work in the same order.
///
/// It is a plain serializable class owned by PlayerRig_NEW, not a component, so the
/// call sites stay at the exact positions they occupied in the original Update.
///
/// Cinemachine's own input is short-circuited by PlayerRig_NEW setting
/// CinemachineCore.GetInputAxis to a constant zero — that stops the built-in
/// Mouse X / Mouse Y fallback from spinning the rig after a domain reload. All axis
/// motion below is applied by hand as a consequence.
/// </summary>
[Serializable]
public class OrbitCameraRig_NEW
{
    [Tooltip("FreeLook rigs the player can orbit. Order does not affect behaviour; " +
             "all listed rigs receive the same input every frame.")]
    [SerializeField] CinemachineFreeLook[] orbitCameras = Array.Empty<CinemachineFreeLook>();

    [Header("Sensitivity")]
    [Tooltip("Horizontal sensitivity, degrees per second at full deflection.")]
    [SerializeField] float xSensitivity = 300f;

    [Tooltip("Vertical sensitivity. The FreeLook Y axis is 0-1, so keep this small.")]
    [SerializeField] float ySensitivity = 2f;

    [Tooltip("Stick magnitude below this is ignored, to stop an idle pad drifting the view.")]
    [SerializeField] float stickDeadband = 0.1f;

    [Header("Recentre")]
    [SerializeField] float resetXSpeed = 180f;
    [SerializeField] float resetYSpeed = 1.5f;

    [Tooltip("Dead zone restored on both axes once the recentre finishes.")]
    [SerializeField] float normalDeadZoneWidth = 0.3f;
    [SerializeField] float normalDeadZoneHeight = 0.3f;

    [Header("Input axes")]
    [SerializeField] string stickXAxis = "RightStickX";
    [SerializeField] string stickYAxis = "RightStickY";
    [SerializeField] string mouseXAxis = "Mouse X";
    [SerializeField] string mouseYAxis = "Mouse Y";

    bool _resetting;

    /// <summary>True while a recentre is in progress.</summary>
    public bool IsResetting => _resetting;

    public void BeginReset() => _resetting = true;
    public void CancelReset() => _resetting = false;

    /// <summary>
    /// Apply one frame of look input. Controller input uses a deadband; mouse input does
    /// not. Whichever source is larger in magnitude wins, so only one device drives at a
    /// time — identical to the original combination rule.
    /// </summary>
    public void DriveInput(float dt)
    {
        float rawStickX = Input.GetAxis(stickXAxis);
        float rawStickY = Input.GetAxis(stickYAxis);

        float stickX = Mathf.Abs(rawStickX) > stickDeadband ? rawStickX * xSensitivity * dt : 0f;
        float stickY = Mathf.Abs(rawStickY) > stickDeadband ? rawStickY * ySensitivity * dt : 0f;

        // GetAxisRaw sidesteps Unity's own smoothing, which fought the per-frame apply.
        float mouseX = Input.GetAxisRaw(mouseXAxis) * xSensitivity * dt;
        float mouseY = Input.GetAxisRaw(mouseYAxis) * ySensitivity * dt;

        float x = Mathf.Abs(stickX) > Mathf.Abs(mouseX) ? stickX : mouseX;
        float y = Mathf.Abs(stickY) > Mathf.Abs(mouseY) ? stickY : mouseY;

        for (int i = 0; i < orbitCameras.Length; i++)
        {
            CinemachineFreeLook vcam = orbitCameras[i];
            if (vcam == null) continue;

            vcam.m_XAxis.Value += x;
            vcam.m_YAxis.Value = Mathf.Clamp01(vcam.m_YAxis.Value + y);
        }
    }

    /// <summary>
    /// Step the recentre. X returns to 0 (behind the player), Y to 0.5 (middle ring).
    /// Dead zones are zeroed while it runs so the Composer frames the player exactly,
    /// then restored. Returns true once every rig has arrived.
    /// </summary>
    public bool TickReset(float dt)
    {
        bool xDone = true;
        bool yDone = true;

        for (int i = 0; i < orbitCameras.Length; i++)
        {
            CinemachineFreeLook vcam = orbitCameras[i];
            if (vcam == null) continue;

            SetDeadZones(vcam, 0f, 0f);

            vcam.m_XAxis.Value = Mathf.MoveTowardsAngle(vcam.m_XAxis.Value, 0f, resetXSpeed * dt);
            vcam.m_YAxis.Value = Mathf.MoveTowards(vcam.m_YAxis.Value, 0.5f, resetYSpeed * dt);

            xDone &= Mathf.Abs(Mathf.DeltaAngle(vcam.m_XAxis.Value, 0f)) < 0.5f;
            yDone &= Mathf.Abs(vcam.m_YAxis.Value - 0.5f) < 0.01f;
        }

        if (!xDone || !yDone) return false;

        for (int i = 0; i < orbitCameras.Length; i++)
            if (orbitCameras[i] != null)
                SetDeadZones(orbitCameras[i], normalDeadZoneWidth, normalDeadZoneHeight);

        _resetting = false;
        return true;
    }

    static void SetDeadZones(CinemachineFreeLook vcam, float width, float height)
    {
        for (int rig = 0; rig < 3; rig++)
        {
            CinemachineVirtualCamera r = vcam.GetRig(rig);
            if (r == null) continue;

            CinemachineComposer composer = r.GetCinemachineComponent<CinemachineComposer>();
            if (composer == null) continue;

            composer.m_DeadZoneWidth = width;
            composer.m_DeadZoneHeight = height;
        }
    }
}

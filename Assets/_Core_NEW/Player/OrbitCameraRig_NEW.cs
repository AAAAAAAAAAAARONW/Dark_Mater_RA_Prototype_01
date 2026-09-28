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

    [Tooltip("Stick magnitude below this is ignored, to stop an idle pad drifting the view.\n\n" +
             "Only read when useProfileDeadband is off.")]
    [SerializeField] float stickDeadband = 0.1f;

    [Tooltip("Take the deadband from the active pad profile instead of the field above.\n\n" +
             "On by default, because a deadband is a property of the hardware rather than " +
             "of the design: the worn Logitech in the Vizlab rests further from centre " +
             "than the Xbox pad this was tuned on, and a stick that rests above the " +
             "threshold drifts the view on its own all afternoon.")]
    [SerializeField] bool useProfileDeadband = true;

    [Header("Recentre")]
    [SerializeField] float resetXSpeed = 180f;
    [SerializeField] float resetYSpeed = 1.5f;

    [Tooltip("Dead zone restored on both axes once the recentre finishes.")]
    [SerializeField] float normalDeadZoneWidth = 0.3f;
    [SerializeField] float normalDeadZoneHeight = 0.3f;

    // WHERE THE AXIS NAMES WENT. This used to carry four serialized strings —
    // stickXAxis, stickYAxis, mouseXAxis, mouseYAxis — naming the Xbox axes directly.
    // They are gone, not hidden: the pad profile answers that question now, because the
    // right answer differs per controller and a per-scene string cannot know which
    // controller is plugged in. On the Vizlab Logitech, "RightStickX" is the right
    // stick's Y and "RightStickY" is the LEFT stick's X, so those defaults were not
    // merely unhelpful there, they were scrambled. See PadProfile_NEW.
    //
    // Deliberately deleted rather than left in place and ignored: a field that can be
    // edited, reports no error and changes nothing is the most expensive kind of trap.

    bool _resetting;

    /// <summary>True while a recentre is in progress.</summary>
    public bool IsResetting => _resetting;

    public void BeginReset() => _resetting = true;
    public void CancelReset() => _resetting = false;

    /// <summary>
    /// Apply one frame of look input. Controller input uses a deadband; mouse input does
    /// not. Whichever source is larger in magnitude wins, so only one device drives at a
    /// time — identical to the original combination rule, which now lives in
    /// InputScheme_NEW so the tutorial and the journey cannot drift apart.
    ///
    /// The maths is unchanged. What changed is who decides which axis the right stick is
    /// on: this used to answer it with a serialized string per scene, and now asks the
    /// active pad profile, which is the only thing that knows what is plugged in.
    ///
    /// <paramref name="sensitivityScale"/> is the zoom's correction (JourneyZoom_NEW): at a
    /// narrower field the same stick sweeps more of the picture, so it turns slower.
    /// </summary>
    public void DriveInput(float dt, float sensitivityScale = 1f)
    {
        float deadband = useProfileDeadband ? InputScheme_NEW.Deadband() : stickDeadband;

        float x = InputScheme_NEW.LookX(InputScheme_NEW.Stick.Right, xSensitivity * sensitivityScale, deadband, dt);
        float y = InputScheme_NEW.LookY(InputScheme_NEW.Stick.Right, ySensitivity * sensitivityScale, deadband, dt);

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

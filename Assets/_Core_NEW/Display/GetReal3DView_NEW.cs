using UnityEngine;

/// <summary>
/// Puts a getReal3D view on an existing camera rig, so the Vizlab wall sees what the rig
/// sees. Lives on the "[getReal3D]" object under the rig; the getReal3D eye camera is its
/// child. GameObject > Vizlab > Adapt Tutorial Camera to getReal3D builds the whole thing,
/// and Adapt Journey Camera to getReal3D does the same on the journey's Cinemachine camera
/// (GetReal3DJourneyMenu_NEW).
///
/// WHY NOT THE GenericPlayer PREFAB. The VizLab guide says to replace the scene's cameras
/// with getReal3D's GenericPlayer, and for a walk-around VR scene that is right. The
/// tutorial is not one: the light carries the player on rails, and FirstPersonLookRig_NEW
/// owns where the eye is and where it looks - a dozen scripts read that rig to decide what
/// is under the reticle. GenericPlayer would bring its own navigation (a character
/// controller that walks and collides), a wand and a 3D menu, all fighting the rig. So this
/// takes only the part of it the wall actually needs, the camera updater, and hangs it off
/// the rig as the navigation frame:
///
///     Camera                FirstPersonLookRig_NEW, TutorialZoom_NEW, shake, the lens
///       [getReal3D]         this; getRealUser                      (the tracker origin)
///         Eye               Camera, getRealCameraUpdater, GetReal3DLens_NEW, post
///
/// The rig keeps its Camera component as the LENS: TutorialZoom_NEW and TutorialOutro_NEW
/// go on writing its field of view, and the scripts that read its pose or project through
/// it keep working. It just stops rendering while this is enabled. Untick this object and
/// the scene is back on the desktop camera, unchanged.
///
/// THE EYE IS ZEROED ON THE RIG. getReal3D places its camera at the viewer's position in
/// the room, measured from the floor - its Head sensor. That is only where a viewer is if
/// the config says so: untracked and not pinned, the head reads its default, the floor, and
/// straight ahead lands below the wall's lowest row (everything shows at the bottom). The
/// config should pin it at the curve's centre, about 1.24 m up; Tools > Vizlab > Pin the
/// Viewer's Head in a getReal3D Config does that, and this object warns at startup when the
/// eyes are on the floor. Left alone, the eye would sit that far above the rig and
/// the follow view would be framed from the wrong place. So on the first frames the
/// updater has placed the eye, this object moves the other way by the same amount, and the
/// viewer's nominal position lands exactly where the rig puts the eye. Done once, not every
/// frame: if the site has head tracking, moving your head should still move the view.
///
/// INPUT. While this is enabled InputScheme_NEW reads getReal3D.Input, which the cluster
/// keeps identical on every node. See InputScheme_NEW.Source.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("GETREAL3D", "#4A7BD0")]
public class GetReal3DView_NEW : MonoBehaviour
{
    [Tooltip("The rig's own camera. It stops rendering while this view is live and goes on " +
             "acting as the lens: its field of view is what the zoom and the outro write, and " +
             "the eye magnifies to match it.")]
    [SerializeField] Camera rigCamera;

    [Header("Fitting the wall to the rig")]
    [Tooltip("Move the eye's nominal position onto the rig. Off to leave the eye where " +
             "getReal3D puts it, measured from the floor of the room.")]
    [SerializeField] bool zeroEyeOnRig = true;

    [Tooltip("Degrees to turn the wall about the vertical. 0 if the centre of the wall shows " +
             "the rig's forward; 180 if it shows what is behind. Anything but 0 or 180 moves " +
             "the picture away from the reticle the tutorial gates on.")]
    [SerializeField] float wallYaw = 0f;

    [Tooltip("Degrees to tip the wall's centre up (negative) or down (positive) against the " +
             "rig's forward. Like wallYaw, this moves the picture off the rig's reticle.")]
    [SerializeField] float wallPitch = 0f;

    [Tooltip("Frames to wait for the updater to place the eye before zeroing anyway. On a " +
             "config with the eye at the floor origin it never moves, and that is fine.")]
    [SerializeField] int zeroWithinFrames = 10;

    bool _rigCameraWasEnabled;
    InputScheme_NEW.Source _previousSource;
    Transform _eye;
    bool _zeroed;
    int _framesWaited;

    /// <summary>The eye's offset from the rig that was cancelled out, metres, in this frame.</summary>
    public Vector3 ZeroedOffset { get; private set; }

    static GetReal3DView_NEW _live;

    /// <summary>Eyes lower than this, metres, are getReal3D's floor default, not a viewer.</summary>
    const float MinEyeHeight = 0.5f;

    /// <summary>
    /// The camera to ask "is this on screen" through: the rig's own camera while a wall
    /// view is live, Camera.main otherwise. Not Camera.main on the wall: that is the eye,
    /// and every node's eye has the projection of its own screens, so each node would give
    /// a different answer and the copies of the piece would stop agreeing. The rig's camera
    /// is the same on every node and looks where the centre of the wall does.
    /// </summary>
    public static Camera ScreenCamera
    {
        get { return _live != null && _live.rigCamera != null ? _live.rigCamera : Camera.main; }
    }

    void OnEnable()
    {
        _live = this;

        _previousSource = InputScheme_NEW.InputSource;
        InputScheme_NEW.InputSource = InputScheme_NEW.Source.GetReal3D;

        if (rigCamera != null)
        {
            _rigCameraWasEnabled = rigCamera.enabled;
            rigCamera.enabled = false;
        }

        transform.localRotation = Quaternion.Euler(wallPitch, wallYaw, 0f);
        _zeroed = !zeroEyeOnRig;
        _framesWaited = 0;
    }

    void OnDisable()
    {
        if (_live == this) _live = null;

        InputScheme_NEW.InputSource = _previousSource;

        if (rigCamera != null) rigCamera.enabled = _rigCameraWasEnabled;
    }

    /// <summary>
    /// Zero after the updater has had its say. It writes the eye in OnPreCull, which is
    /// after every LateUpdate, so the earliest this can see the eye's real position is the
    /// LateUpdate of the following frame.
    /// </summary>
    void LateUpdate()
    {
        if (_zeroed) return;

        if (_eye == null) _eye = FindEye();
        if (_eye == null) return;

        _framesWaited++;
        bool placed = _eye.localPosition.sqrMagnitude > 1e-6f;
        if (!placed && _framesWaited < zeroWithinFrames) return;

        ZeroedOffset = _eye.localPosition;
        transform.localPosition = -(transform.localRotation * ZeroedOffset);
        _zeroed = true;

        // getReal3D draws every screen from this point. If it is on the floor, straight ahead
        // - where the rig keeps the light - meets the wall below its lowest row, and the whole
        // piece sits at the very bottom. It is set in the getReal3D config, not here.
        if (GetReal3DRuntime_NEW.LaunchedByGetReal3D && ZeroedOffset.y < MinEyeHeight)
            Debug.LogWarning("[GetReal3DView_NEW] getReal3D puts the viewer's eyes " + ZeroedOffset.y.ToString("0.00") +
                             " m above the floor: its Head sensor is at its default, on the floor (no head tracking, no static " +
                             "head in the config). Everything straight ahead will show at the bottom of the wall. Fix the config " +
                             "with Tools > Vizlab > Pin the Viewer's Head in a getReal3D Config.", this);
        else
            Debug.Log("[GetReal3DView_NEW] viewer's eyes at (" + ZeroedOffset.x.ToString("0.00") + ", " +
                      ZeroedOffset.y.ToString("0.00") + ", " + ZeroedOffset.z.ToString("0.00") + ") in the room.", this);
    }

    /// <summary>
    /// The first getReal3D camera under this object. getReal3D clones that camera per eye
    /// and per screen, and the clones sit beside it, so any one of them will do.
    /// </summary>
    Transform FindEye()
    {
        getRealCameraUpdater updater = GetComponentInChildren<getRealCameraUpdater>();
        return updater != null ? updater.transform : null;
    }

    void OnGUI()
    {
        if (!DebugView_NEW.Overlay) return;
        if (!getReal3D.GUI.BeginGUI()) return;

        GUI.Label(DebugOverlayRows_NEW.Row(DebugOverlayRows_NEW.GetReal3D),
                  // Not Cluster.isOn: it is hard-wired to true. See InputScheme_NEW.
                  string.Format("GETREAL3D  nodes {0}   master {1}   eye zeroed {2} ({3:F2}, {4:F2}, {5:F2})",
                                getReal3D.Plugin.getNodeCount(),
                                getReal3D.Cluster.isMaster ? "yes" : "no",
                                _zeroed ? "yes" : "waiting",
                                ZeroedOffset.x, ZeroedOffset.y, ZeroedOffset.z));

        getReal3D.GUI.EndGUI();
    }
}

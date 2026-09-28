using Cinemachine;
using UnityEngine;

/// <summary>
/// Left stick narrows the field of view in the journey — the same verb the tutorial
/// teaches in Phase 0 (TutorialZoom_NEW), with the same numbers and the same feel: push
/// up to look closer, let go and the view stays where you left it.
///
/// WHY IT DOES NOT SET Camera.fieldOfView. The tutorial's camera is its own. The
/// journey's belongs to Cinemachine: the brain writes the live virtual camera's lens into
/// the camera every frame, so a direct write is undone on the next one. This scales the
/// brain's output instead — CinemachineCore.CameraUpdatedEvent fires right after the lens
/// has been pushed, and the zoom multiplies the field of view there. Every virtual
/// camera, every blend and the intro push-in keep working unmodified, and the zoom rides
/// on top of whichever of them is live.
///
/// It is a RATIO for the same reason: the journey's field is not always 40 (the Macro
/// rig runs at 30, the intro shot opens at 72), and 40 → 12 has to mean "three and a
/// third times closer" on all of them.
///
/// SENSITIVITY FOLLOWS THE ZOOM, as in the tutorial: at a third of the field the same
/// stick sweeps three times as much of the picture, so look is slowed by the same ratio.
///
/// TRANSITIONS EASE IT BACK OUT. A visitor who does not know they are zoomed in would
/// otherwise meet the next world through a 12° keyhole, and a dive looks like nothing
/// through one. While a transition has look input locked, the stick is ignored too.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("ZOOM", "#6FA8DC")]
public class JourneyZoom_NEW : MonoBehaviour
{
    [Header("Range")]
    [Tooltip("Field of view the zoom is measured against. Only its ratio to the zoomed " +
             "field matters — 40 to 12 is the tutorial's range.")]
    [SerializeField] float baseFieldOfView = 40f;

    [Tooltip("Field of view at full zoom, measured against baseFieldOfView. 12 matches the " +
             "tutorial: narrow enough to pick out a distant structure, wide enough to steer.")]
    [SerializeField] float zoomedFieldOfView = 12f;

    [Header("Feel (the tutorial's values)")]
    [Tooltip("How fast the zoom follows the stick, in fractions of the range per second.")]
    [SerializeField] float zoomRate = 0.9f;

    [Tooltip("Seconds of smoothing on the applied zoom. A zoom that snaps reads as the " +
             "camera changing lens; one that eases reads as leaning in.")]
    [SerializeField] float smoothTime = 0.12f;

    [Tooltip("Take the deadband from the active pad profile, as the look rig does.")]
    [SerializeField] bool useProfileDeadband = true;

    [Tooltip("Stick magnitude below this is ignored. Only read when useProfileDeadband is off.")]
    [SerializeField] float stickDeadband = 0.1f;

    [Tooltip("Push up to zoom in. Tick to pull down instead.")]
    [SerializeField] bool invert = false;

    [Header("Transitions")]
    [Tooltip("Ease back to the full view when a gate is crossed, so the next world is " +
             "revealed at the framing it was composed for.")]
    [SerializeField] bool zoomOutOnTransition = true;

    [Tooltip("Seconds of smoothing for that ease-out. Slower than smoothTime on purpose: " +
             "the visitor did not ask for it, so it should not feel like a snap.")]
    [SerializeField] float transitionSmoothTime = 0.8f;

    [Header("Wiring (found if empty)")]
    [SerializeField] PlayerRig_NEW player;
    [SerializeField] CinemachineBrain brain;
    [SerializeField] LayerState_NEW layerState;

    float _target;
    float _applied;
    float _velocity;
    bool _easingOut;

    /// <summary>0 at the full view, 1 at full zoom.</summary>
    public float Amount => _applied;

    /// <summary>What the field of view is multiplied by right now. 1 when not zoomed.</summary>
    public float FovMultiplier =>
        Mathf.Lerp(1f, zoomedFieldOfView / Mathf.Max(1f, baseFieldOfView), _applied);

    void Awake()
    {
        if (player == null) player = FindObjectOfType<PlayerRig_NEW>();
        if (brain == null) brain = FindObjectOfType<CinemachineBrain>();
        if (layerState == null) layerState = FindObjectOfType<LayerState_NEW>();

        WarnIfSharingAStick();
    }

    /// <summary>
    /// The left stick also drives the journey speed when PlayerRig_NEW is set to Visitor.
    /// Then one push would zoom and slow the light at once, and neither would be
    /// learnable — the same class of quiet clash TutorialZoom_NEW checks for.
    /// </summary>
    void WarnIfSharingAStick()
    {
        if (player == null || player.SpeedInputMode != PlayerRig_NEW.SpeedInput.Visitor) return;

        Debug.LogWarning("[JourneyZoom_NEW] PlayerRig_NEW.speedInput is Visitor, which also " +
                         "reads the left stick: one push will zoom AND change the speed. Set " +
                         "speedInput to Off or Attendant, or disable this component.", this);
    }

    void OnEnable()
    {
        CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);
        if (layerState != null) layerState.OnLayerChanged += HandleLayerChanged;
    }

    void OnDisable()
    {
        CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
        if (layerState != null) layerState.OnLayerChanged -= HandleLayerChanged;

        // The brain puts the unzoomed lens back on its next update; the look speed is ours.
        if (player != null) player.SetLookSensitivityScale(1f);
    }

    /// <summary>Back to the full view at once.</summary>
    public void ResetZoom()
    {
        _target = 0f;
        _applied = 0f;
        _velocity = 0f;
        _easingOut = false;
    }

    void HandleLayerChanged(LayerProfile_NEW previous, LayerProfile_NEW current)
    {
        // previous is null only for the start layer's initial emit, which is not a gate.
        if (!zoomOutOnTransition || previous == null) return;

        _target = 0f;
        _easingOut = true;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // A transition that has taken the look stick has taken this one too.
        bool locked = player != null && player.CameraInputLocked;

        if (!locked)
        {
            float deadband = useProfileDeadband ? InputScheme_NEW.Deadband() : stickDeadband;
            float push = InputScheme_NEW.StickY(InputScheme_NEW.Stick.Left, deadband);
            if (invert) push = -push;

            if (push != 0f) _easingOut = false;

            _target = Mathf.Clamp01(_target + push * zoomRate * dt);
        }

        float smoothing = _easingOut ? transitionSmoothTime : smoothTime;

        _applied = smoothing <= 0f
            ? _target
            : Mathf.SmoothDamp(_applied, _target, ref _velocity, smoothing, Mathf.Infinity, dt);

        if (_easingOut && _applied < 0.001f)
        {
            _applied = 0f;
            _easingOut = false;
        }

        if (player != null) player.SetLookSensitivityScale(FovMultiplier);
    }

    /// <summary>
    /// Runs right after the brain has written the live camera's lens, every time it does.
    /// The brain resets the field each call, so multiplying here never accumulates.
    /// </summary>
    void OnCameraUpdated(CinemachineBrain updated)
    {
        if (brain != null && updated != brain) return;

        float multiplier = FovMultiplier;
        if (Mathf.Approximately(multiplier, 1f)) return;

        Camera cam = updated.OutputCamera;
        if (cam == null || cam.orthographic) return;

        cam.fieldOfView = Mathf.Clamp(cam.fieldOfView * multiplier, 1f, 179f);
    }
}

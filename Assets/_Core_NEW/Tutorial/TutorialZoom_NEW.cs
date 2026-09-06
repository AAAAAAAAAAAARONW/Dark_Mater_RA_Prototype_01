using UnityEngine;

/// <summary>
/// Left stick narrows the field of view. Look closer at something a long way off.
///
/// This is a departure from GDD §4, which says "Stick — Look. The only stick." It was
/// added because the opening has a problem the storyboard does not: Phase 0 is twenty
/// seconds of being told you can look around in a scene where the only thing to look at
/// is a bright point that does not get much bigger. A second control, taught in that
/// gap, gives those twenty seconds something to be — and "look closer at the distant
/// thing" is the one verb an astronomy exhibit can hand a visitor that needs no
/// explaining.
///
/// It does not touch the look scheme. Look is the right stick, zoom is the left, and
/// nothing reads both.
///
/// SENSITIVITY HAS TO FOLLOW THE ZOOM, and this is the part that is easy to leave out.
/// Degrees per second is a constant, but degrees per SCREEN is not: at 12° of field the
/// same stick deflection sweeps three times as much of the picture as it does at 40°,
/// so a rig that ignores the zoom feels broken exactly when the player is trying to be
/// precise. The rig's sensitivity is scaled by the current field over the base field,
/// which keeps the apparent speed constant.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[HierarchyBadge_NEW("ZOOM", "#6FA8DC")]
public class TutorialZoom_NEW : MonoBehaviour
{
    [Header("Range")]
    [Tooltip("Field of view when not zoomed. Should match the camera's built value, 40 — " +
             "which is PlaytestBuild's.")]
    [SerializeField] float baseFieldOfView = 40f;

    [Tooltip("Field of view at full zoom. Narrow enough to make the quasar a disc rather " +
             "than a point, wide enough that the view is still navigable.")]
    [SerializeField] float zoomedFieldOfView = 12f;

    [Header("Feel")]
    [Tooltip("How fast the zoom follows the stick, in fractions of the range per second.")]
    [SerializeField] float zoomRate = 0.9f;

    [Tooltip("Seconds of smoothing on the applied field of view. A zoom that snaps reads " +
             "as the camera changing lens; one that eases reads as leaning in.")]
    [SerializeField] float smoothTime = 0.12f;

    [Tooltip("Stick magnitude below this is ignored. Same value as the look deadband.")]
    [SerializeField] float stickDeadband = 0.1f;

    [Tooltip("Push up to zoom in. Off if the exhibition pad reads the other way round.")]
    [SerializeField] bool invert = false;

    [Header("Wiring")]
    [Tooltip("Leave empty to use the rig on this object. Its look sensitivity is scaled " +
             "by the zoom so the view does not feel three times faster when narrowed.")]
    [SerializeField] FirstPersonLookRig_NEW lookRig;

    Camera _camera;

    /// <summary>0 = base field of view, 1 = fully zoomed.</summary>
    float _target;
    float _applied;
    float _velocity;

    /// <summary>0 at the base field of view, 1 at full zoom.</summary>
    public float Amount { get { return _applied; } }

    /// <summary>The field of view right now, in degrees. Read by the gauge.</summary>
    public float FieldOfView
    {
        get { return Mathf.Lerp(baseFieldOfView, zoomedFieldOfView, _applied); }
    }

    void Awake()
    {
        _camera = GetComponent<Camera>();

        if (lookRig == null) lookRig = GetComponent<FirstPersonLookRig_NEW>();

        // Take the base from the camera as built, so retuning the FOV in the Inspector
        // does not leave this component pulling towards a stale number.
        if (_camera != null && _camera.fieldOfView > 0f) baseFieldOfView = _camera.fieldOfView;

        WarnIfSharingAStick();

        Apply(0f);
    }

    /// <summary>
    /// Zoom reads the left stick. If look does too, the right stick does nothing and
    /// both prompts are lying.
    ///
    /// This is checked at runtime as well as in the builder because the way it happened
    /// was invisible to both: look was moved to the right stick by changing the field's
    /// C# default, which does nothing to a scene that already had the old value saved.
    /// A scene can therefore be wrong without anyone having done anything wrong to it.
    /// </summary>
    void WarnIfSharingAStick()
    {
        if (lookRig == null) return;
        if (lookRig.LookStickSetting != TutorialInput_NEW.LookStick.Left) return;

        Debug.LogError("[TutorialZoom_NEW] Look and zoom are both on the LEFT stick, so " +
                       "the right stick does nothing and every RIGHT STICK prompt is wrong. " +
                       "Set FirstPersonLookRig_NEW.lookStick to Right, or run " +
                       "Tools > Journey NEW > Tutorial > Build or Update, which repairs it.",
                       this);
    }

    /// <summary>Back to the unzoomed view. The attract reset uses this.</summary>
    public void ResetZoom()
    {
        _target = 0f;
        _applied = 0f;
        _velocity = 0f;

        Apply(0f);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // Left stick only. Look is on the right, and nothing reads both.
        float push = TutorialInput_NEW.StickY(TutorialInput_NEW.LookStick.Left, stickDeadband);
        if (invert) push = -push;

        _target = Mathf.Clamp01(_target + push * zoomRate * dt);

        _applied = smoothTime <= 0f
            ? _target
            : Mathf.SmoothDamp(_applied, _target, ref _velocity, smoothTime, Mathf.Infinity, dt);

        Apply(_applied);
    }

    void Apply(float amount)
    {
        float fov = Mathf.Lerp(baseFieldOfView, zoomedFieldOfView, amount);

        if (_camera != null) _camera.fieldOfView = fov;

        // See the class summary: constant degrees per second is not constant apparent
        // speed once the field narrows.
        if (lookRig != null && baseFieldOfView > 0f)
            lookRig.SensitivityScale = fov / baseFieldOfView;
    }
}

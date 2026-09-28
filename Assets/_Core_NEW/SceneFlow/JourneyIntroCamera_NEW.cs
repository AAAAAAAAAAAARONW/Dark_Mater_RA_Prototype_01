using System.Collections;
using Cinemachine;
using UnityEngine;

/// <summary>
/// The journey's first shot: the camera the tutorial ended on — high, far back, wide —
/// pushing in to the journey's normal framing.
///
/// WHY. The tutorial's outro pulls the camera up and back until the light is a small
/// thing crossing a large space, then fades to black. If the journey then opened on its
/// ordinary close orbit, the black would read as a cut between two unrelated shots.
/// Opening on the same wide, high view and pushing in makes the black read as one
/// continuous move — out, then back in — and turns the last image of the tutorial into
/// the first image of the journey.
///
/// HOW. A temporary Cinemachine virtual camera, created at startup, placed where the
/// outro left off (scaled to this scene), aimed at the player and given a priority above
/// every other camera. When the reveal starts it drops out, and the brain blends from it
/// to whichever zone camera CameraDirector_NEW made live — the blend IS the push in.
/// Then it deletes itself. CameraDirector_NEW is not modified and does not know this
/// exists.
///
/// TIMING. If the visitor arrived through SceneHandoff_NEW, the push starts with the
/// fade up. If the journey scene was opened directly (development), it starts after
/// directStartDelay.
///
/// SWITCH: introEnabled. Off = the journey opens on its normal framing, as before.
/// </summary>
[DefaultExecutionOrder(1000)]   // Start after CameraDirector_NEW's first sequence has set priorities
[DisallowMultipleComponent]
[HierarchyBadge_NEW("INTRO CAM", "#B36AE2")]
public class JourneyIntroCamera_NEW : MonoBehaviour
{
    [Header("Switch")]
    [Tooltip("Off = no intro shot; the journey opens on its normal camera.")]
    [SerializeField] bool introEnabled = true;

    [Header("Wiring (found if empty)")]
    [SerializeField] PlayerRig_NEW player;
    [SerializeField] CinemachineBrain brain;

    [Header("Start framing — match the tutorial's last frame")]
    [Tooltip("Distance behind the player, along its heading, in this scene's units.\n\n" +
             "The tutorial outro ends 34 behind and 30 above the light, in a world that " +
             "moves at 25 units/s. The journey moves at 2.5, so the same framing is about " +
             "a tenth of that. Tune by eye: the last frame before black and the first " +
             "frame after should read as the same shot.")]
    [SerializeField] float startDistance = 3.4f;

    // Renamed from startHeight (was 3.0) WITHOUT FormerlySerializedAs, so the scene's saved 3.0
    // is dropped and this default applies: the opening shot was asked to sit higher. Tune here.
    [Tooltip("Height above the player, in this scene's units. See startDistance. 4.5 = raised " +
             "half again over the first derived value (3.0), for a steadier look down onto the " +
             "light at the start.")]
    [SerializeField] float startHeightAbovePlayer = 4.5f;

    [Tooltip("Field of view at the start. The tutorial outro ends at 72; the journey's " +
             "cameras run at 40. Most of the 'zoom in' is this narrowing.")]
    [Range(10f, 120f)]
    [SerializeField] float startFieldOfView = 72f;

    [Header("Push in")]
    [Tooltip("Seconds held on the wide shot after the reveal begins, before pushing in. " +
             "Long enough to register the frame as the one the tutorial ended on.")]
    [Min(0f)]
    [SerializeField] float holdSeconds = 0.75f;

    [Tooltip("Seconds for the push in to the journey's normal framing.")]
    [Min(0.1f)]
    [SerializeField] float pushSeconds = 3.5f;

    [Tooltip("Delay before the push when the journey was opened directly, without a handoff.")]
    [Min(0f)]
    [SerializeField] float directStartDelay = 0.5f;

    [Tooltip("Priority while the intro shot is live. Must be above every camera " +
             "CameraDirector_NEW manages (its high priority is 50).")]
    [SerializeField] int livePriority = 100;

    [Header("Entry fade (when the journey scene is opened directly)")]
    [Tooltip("Open on black and fade up, with the push-in starting as the fade does.\n\n" +
             "Arriving from the tutorial, SceneHandoff_NEW already does this, so this only " +
             "runs when the journey scene starts on its own. Works even with the intro shot off.")]
    [SerializeField] bool fadeInOnDirectStart = true;

    [Tooltip("Seconds of full black before the fade begins.")]
    [Min(0f)]
    [SerializeField] float blackHoldSeconds = 0.5f;

    [Tooltip("Seconds for the fade from black.")]
    [Min(0.05f)]
    [SerializeField] float fadeInSeconds = 2.5f;

    [Tooltip("Sort order of the black overlay. Must be above every HUD canvas.")]
    [SerializeField] int overlaySortOrder = 32000;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    CinemachineVirtualCamera _vcam;
    bool _revealed;
    UnityEngine.UI.Image _black;

    void Awake()
    {
        if (player == null) player = FindObjectOfType<PlayerRig_NEW>();
        if (brain == null) brain = FindObjectOfType<CinemachineBrain>();
    }

    void Start()
    {
        // Start runs before the first frame renders, so the overlay is up before anything
        // is seen: no flash of the scene before the black.
        bool fading = fadeInOnDirectStart && !SceneHandoff_NEW.IsHandingOff;
        if (fading) StartCoroutine(FadeFromBlack());

        if (!introEnabled) return;

        if (player == null || brain == null)
        {
            Debug.LogWarning("[JourneyIntroCamera_NEW] Needs a PlayerRig_NEW and a CinemachineBrain; skipping the intro shot.", this);
            return;
        }

        // Created here rather than placed in the scene: it exists for a few seconds, and a
        // high-priority camera left in a scene is exactly the failure CameraDirector_NEW's
        // unmanaged-camera warning was written about. Created after the director's Awake,
        // so that warning does not fire for it.
        _vcam = CreateIntroCamera();

        if (SceneHandoff_NEW.IsHandingOff)
        {
            SceneHandoff_NEW.RevealStarting += OnReveal;
            Log("Waiting for the handoff's reveal.");
        }
        else if (!fading)
        {
            _revealed = true;
        }
        // (fading: FadeFromBlack sets _revealed when the fade begins, like the handoff does.)

        StartCoroutine(Run());
    }

    /// <summary>Black, hold, then fade up — the journey's own version of the handoff's reveal.</summary>
    IEnumerator FadeFromBlack()
    {
        GameObject root = new GameObject("JourneyEntryFade (temporary)", typeof(RectTransform));
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = overlaySortOrder;

        GameObject img = new GameObject("Black", typeof(RectTransform));
        img.transform.SetParent(root.transform, false);
        RectTransform rt = (RectTransform)img.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _black = img.AddComponent<UnityEngine.UI.Image>();
        _black.raycastTarget = false;
        _black.color = Color.black;

        for (float t = 0f; t < blackHoldSeconds; t += Time.unscaledDeltaTime) yield return null;

        _revealed = true;
        Log("Fading up from black.");

        for (float t = 0f; t < fadeInSeconds; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Clamp01(t / fadeInSeconds);
            _black.color = new Color(0f, 0f, 0f, 1f - k * k * (3f - 2f * k));
            yield return null;
        }

        Destroy(root);
        _black = null;
    }

    void OnDestroy()
    {
        SceneHandoff_NEW.RevealStarting -= OnReveal;
        if (_vcam != null) Destroy(_vcam.gameObject);
    }

    void OnReveal()
    {
        _revealed = true;
    }

    IEnumerator Run()
    {
        while (!_revealed) yield return null;

        // After a reveal (handoff or our own fade) only the hold; a bare direct start also
        // waits directStartDelay, as it has no fade to give the eye a moment.
        bool afterReveal = SceneHandoff_NEW.IsHandingOff || fadeInOnDirectStart;
        float wait = afterReveal ? holdSeconds : directStartDelay + holdSeconds;
        for (float t = 0f; t < wait; t += Time.unscaledDeltaTime) yield return null;

        // The brain blends with its default blend when the live camera changes. Borrow it
        // for the push, then hand it back — every other transition in the journey keeps
        // the blend it was authored with.
        CinemachineBlendDefinition previous = brain.m_DefaultBlend;
        brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.EaseInOut, pushSeconds);

        Log("Pushing in over " + pushSeconds + "s.");
        _vcam.Priority = 0;

        // Wait out the blend (plus a frame) before restoring anything.
        for (float t = 0f; t < pushSeconds + 0.1f; t += Time.unscaledDeltaTime) yield return null;

        brain.m_DefaultBlend = previous;
        SceneHandoff_NEW.RevealStarting -= OnReveal;

        Destroy(_vcam.gameObject);
        _vcam = null;
        Log("Intro shot done.");
    }

    CinemachineVirtualCamera CreateIntroCamera()
    {
        GameObject go = new GameObject("JourneyIntroVcam (temporary)");
        CinemachineVirtualCamera vcam = go.AddComponent<CinemachineVirtualCamera>();

        Transform target = player.transform;
        vcam.Follow = target;
        vcam.LookAt = target;
        vcam.Priority = livePriority;
        vcam.m_Lens.FieldOfView = startFieldOfView;

        // Behind along the heading and above, fixed in world space — the tutorial outro's
        // geometry. Follows the player so the framing holds while it flies.
        Vector3 heading = player.MovementDirection.sqrMagnitude > 0.0001f ? player.MovementDirection.normalized : Vector3.forward;

        CinemachineTransposer body = vcam.AddCinemachineComponent<CinemachineTransposer>();
        body.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;
        body.m_FollowOffset = -heading * startDistance + Vector3.up * startHeightAbovePlayer;
        body.m_XDamping = 0f;
        body.m_YDamping = 0f;
        body.m_ZDamping = 0f;

        CinemachineComposer aim = vcam.AddCinemachineComponent<CinemachineComposer>();
        aim.m_HorizontalDamping = 0f;
        aim.m_VerticalDamping = 0f;
        aim.m_DeadZoneWidth = 0f;
        aim.m_DeadZoneHeight = 0f;

        // Place it now so the very first rendered frame is already the intro framing.
        go.transform.position = target.position + body.m_FollowOffset;
        go.transform.rotation = Quaternion.LookRotation(target.position - go.transform.position, Vector3.up);

        Log("Intro camera at " + startDistance + " back, " + startHeightAbovePlayer + " up, FOV " + startFieldOfView + ".");
        return vcam;
    }

    void Log(string message)
    {
        if (debugLog) Debug.Log("[JourneyIntroCamera_NEW] " + message, this);
    }
}

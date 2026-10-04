using UnityEngine;
using TMPro;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// How the tutorial ends: the camera pulls back and up until the light is a small thing
/// crossing a large space, the frame fades to black, and WELCOME TO THE JOURNEY comes
/// up on it.
///
/// UNTIL NOW THERE WAS NO ENDING AT ALL. The director reached State.Complete, raised
/// OnTutorialComplete, and nothing anywhere listened. The last beat's prompt stayed on
/// screen asking for a button that had already been pressed, the light flew on, and the
/// piece sat in that state until the ninety second idle timer took it back to attract.
/// From in front of it that reads as the last frame having failed rather than as the
/// tutorial being over.
///
/// WHY THE CAMERA LEAVES FIRST PERSON TO DO IT. Everything before this moment is the
/// player being the light, and the whole piece has been shot from just behind it — so
/// the one thing they have never seen is themselves. Pulling out to look down on the
/// light says the thing the words then repeat: that what they have been doing was a
/// journey, and it was going somewhere.
///
/// It is also the only place in the piece where taking the camera is allowed. GDD §5's
/// rule is that the camera is never locked, and the reason is that a visitor who cannot
/// look is a visitor who does not believe they are in control. That reason expires with
/// the last gate: there is nothing left to control, nothing left to look for, and the
/// alternative to a scripted exit is a frozen prompt over a light still flying at a
/// quasar it has already left.
///
/// SO IT MUST BE IMPOSSIBLE TO ENTER EARLY, and the only trigger is the director's own
/// completion event. It cannot be reached by a debug jump, by the attract timer, or by
/// pausing — and ResetForAttract puts every borrowed thing back, because the next
/// visitor has to get a camera they can move.
///
/// Real time throughout (see TutorialClock_NEW): this is the frame telling the player
/// the piece is over, and nothing about it is happening in the universe any more.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("OUTRO", "#E0A030")]
public class TutorialOutro_NEW : MonoBehaviour
{
    [Header("Wiring")]
    [Tooltip("Leave empty to find it. The only thing that starts this is its completion.")]
    [SerializeField] TutorialDirector_NEW director;

    [Tooltip("Leave empty to find it. What the ending hands back to.\n\n" +
             "THE ATTRACT AND NOT THE DIRECTOR, and the difference is the whole restart. " +
             "TutorialDirector_NEW.ReturnToAttract only moves the director's own state " +
             "back; it does not put the card up, reset the travel, recentre the view or " +
             "fire the onReset every phase hangs its teardown on — including this " +
             "component's, which is what gives the camera back. Calling the director " +
             "directly would end the piece with a rig still switched off, and the next " +
             "visitor's first lesson would silently not work.")]
    [SerializeField] TutorialAttract_NEW attract;

    [Tooltip("The camera the pull-back drives. Leave empty to use the main camera.")]
    [SerializeField] Camera viewCamera;

    [Tooltip("Switched off while the outro holds the camera, and back on afterwards. " +
             "It writes the camera's whole pose every Update, so the two cannot both be " +
             "driving.")]
    [SerializeField] FirstPersonLookRig_NEW lookRig;

    [Tooltip("Also switched off, for the same reason: it owns the field of view and the " +
             "pull-back is a field of view change.")]
    [SerializeField] TutorialZoom_NEW zoom;

    [Tooltip("The light. The camera pulls back from it and keeps looking at it, so it " +
             "stays in frame while it flies.")]
    [SerializeField] TutorialTravel_NEW travel;

    [Header("Pull back")]
    [Tooltip("Seconds the camera takes to get from behind the light to above it.")]
    [SerializeField] float pullSeconds = 5f;

    [Tooltip("Metres behind the light at the end, along its course.")]
    [SerializeField] float endDistance = 34f;

    [Tooltip("Metres above it. Height against distance is what sets how far over the top " +
             "the final view is — at these values the camera looks down about 40 degrees, " +
             "which reads as looking down on the light rather than as hovering over it.")]
    [SerializeField] float endHeight = 30f;

    [Tooltip("Field of view at the end. Wider than the piece has used anywhere, so the " +
             "pull-back is a widening as well as a retreat and the light ends up small.")]
    [Range(20f, 110f)]
    [SerializeField] float endFieldOfView = 72f;

    [Tooltip("Shape of the move. Easing out lands it gently instead of stopping dead.")]
    [SerializeField] AnimationCurve pullShape = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Fade")]
    [Tooltip("Seconds of pull-back before the fade starts, as a fraction of pullSeconds. " +
             "Under 1 so the two overlap — a fade that waits for the camera to stop reads " +
             "as two separate events.")]
    [Range(0.1f, 1f)]
    [SerializeField] float fadeStartsAt = 0.55f;

    [Tooltip("Seconds to reach full black.")]
    [SerializeField] float fadeSeconds = 3.5f;

    [Tooltip("Full-screen black. Over every other HUD element, including the pause card.")]
    [SerializeField] Image blackout;

    [Header("Title")]
    [SerializeField] TMP_Text title;

    [Tooltip("GDD: the handover line into the journey proper.")]
    [SerializeField] string titleText = "WELCOME  TO  THE  JOURNEY";

    [Tooltip("Let the builder keep this wording in step. Untick to write your own.")]
    [SerializeField] bool builderOwnsCopy = true;

    [Tooltip("Drawn title, in place of the words above. Assigning one switches the text " +
             "off and fades the picture instead; leaving it empty is the ending exactly " +
             "as it was.")]
    [SerializeField] Image titleImage;

    [Tooltip("Seconds after the screen is black before the words arrive. A beat of " +
             "nothing is what makes them read as a title rather than as a caption on the " +
             "frame that just ended.")]
    [SerializeField] float titleDelaySeconds = 0.6f;

    [SerializeField] float titleFadeSeconds = 1.6f;

    [Header("After")]
    [Tooltip("Seconds the title holds before the piece goes back to its attract card. " +
             "0 leaves it up, and the director's idle timer takes it back eventually.\n\n" +
             "This is the exhibition loop closing: the next visitor should find the title " +
             "card, not somebody else's ending.")]
    [SerializeField] float holdBeforeAttractSeconds = 7f;

    [Tooltip("Fires when the screen is black and the words are up. The handover to the " +
             "journey scene goes here when there is one.")]
    [SerializeField] UnityEvent onFinished = new UnityEvent();

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    /// <summary>Whether Build or Update may rewrite the wording.</summary>
    public bool BuilderOwnsCopy { get { return builderOwnsCopy; } }

    bool _running;
    float _elapsed;
    bool _finished;

    /// <summary>Where the camera was when the outro took it, as an offset from the light.</summary>
    Vector3 _startOffset;
    Quaternion _startRotation;
    float _startFieldOfView;

    /// <summary>The course at the moment it took over. It does not change after that.</summary>
    Vector3 _courseAtStart = Vector3.forward;

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>True while the outro owns the camera.</summary>
    public bool IsRunning { get { return _running; } }

    /// <summary>
    /// Start the ending. Wired to the director's completion event, and public so a beat
    /// can be given it directly if the piece ever ends somewhere other than the last
    /// beat.
    /// </summary>
    public void Begin()
    {
        if (_running) return;
        if (viewCamera == null) return;

        _running = true;
        _finished = false;
        _elapsed = 0f;

        // The pose it is starting from, expressed against the light so the blend holds
        // while the light keeps flying. The light's transform never rotates — travel only
        // ever writes position — so an offset in world axes is also an offset in its
        // local ones, and this stays correct through C3's course reversal.
        Transform light = LightTransform();

        _startOffset = light != null
            ? viewCamera.transform.position - light.position
            : Vector3.zero;

        _startRotation = viewCamera.transform.rotation;
        _startFieldOfView = viewCamera.fieldOfView;

        _courseAtStart = travel != null && travel.Direction.sqrMagnitude > 0.0001f
            ? travel.Direction.normalized
            : viewCamera.transform.forward;

        // The rig writes the camera's whole pose every Update and the zoom writes its
        // field of view. Both have to stop before this writes either, or the last one to
        // run each frame wins and the pull-back stutters.
        if (lookRig != null) lookRig.enabled = false;
        if (zoom != null) zoom.enabled = false;

        if (debugLog) Debug.Log("[TutorialOutro_NEW] Begin.", this);
    }

    /// <summary>
    /// Everything back, for the next visitor. The attract reset calls this.
    ///
    /// The camera is the part that matters: a rig left disabled is a tutorial whose
    /// first lesson — that the stick moves the view — silently does not work, and it
    /// would only show up on the second run of the day.
    /// </summary>
    public void ResetForAttract()
    {
        _running = false;
        _finished = false;
        _elapsed = 0f;
        _returnToAttractCancelled = false;

        if (lookRig != null) lookRig.enabled = true;
        if (zoom != null) zoom.enabled = true;

        if (blackout != null) SetAlpha(blackout, 0f);
        SetAlpha(TitleGraphic, 0f);

        // The field of view is NOT put back here, deliberately. TutorialZoom_NEW writes
        // it every frame from its own state, and the attract has already called its
        // ResetZoom by the time this runs — so restoring whatever the FOV happened to be
        // when the ending started would be overwriting a value that is already correct
        // with one that is a second out of date.
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (director == null) director = FindObjectOfType<TutorialDirector_NEW>();
        if (attract == null) attract = FindObjectOfType<TutorialAttract_NEW>();
        if (viewCamera == null) viewCamera = Camera.main;
        if (lookRig == null) lookRig = FindObjectOfType<FirstPersonLookRig_NEW>();
        if (zoom == null) zoom = FindObjectOfType<TutorialZoom_NEW>();
        if (travel == null) travel = FindObjectOfType<TutorialTravel_NEW>();

        if (title != null) title.text = titleText;

        // Art wins where there is art; the words stay in the scene as the fallback.
        if (HasTitleArt && title != null) title.gameObject.SetActive(false);

        ResetForAttract();
    }

    void OnEnable()
    {
        if (director != null) director.OnTutorialComplete += Begin;
    }

    void OnDisable()
    {
        if (director != null) director.OnTutorialComplete -= Begin;
    }

    /// <summary>
    /// LateUpdate, so whatever else still moves the light this frame has already moved
    /// it and the camera is placed against where it actually ended up.
    /// </summary>
    void LateUpdate()
    {
        if (!_running) return;

        // Real time. The world clock is not the authority on a frame that is about the
        // piece being over rather than about anything happening in it.
        _elapsed += Time.unscaledDeltaTime;

        TickCamera();
        TickFade();
    }

    void TickCamera()
    {
        if (viewCamera == null) return;

        float t = pullSeconds > 0f ? Mathf.Clamp01(_elapsed / pullSeconds) : 1f;
        float k = pullShape != null ? pullShape.Evaluate(t) : t;

        Transform light = LightTransform();
        if (light == null) return;

        // Behind along the course and above it. Built from the course rather than from
        // the camera's own facing, so the light ends up crossing the frame on its own
        // axis however the player happened to leave the view pointing.
        Vector3 endOffset = -_courseAtStart * endDistance + Vector3.up * endHeight;

        Vector3 offset = Vector3.Lerp(_startOffset, endOffset, k);
        Vector3 eye = light.position + offset;

        Quaternion endRotation = offset.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(-offset, Vector3.up)
            : _startRotation;

        viewCamera.transform.position = eye;
        viewCamera.transform.rotation = Quaternion.Slerp(_startRotation, endRotation, k);
        viewCamera.fieldOfView = Mathf.Lerp(_startFieldOfView, endFieldOfView, k);
    }

    void TickFade()
    {
        float startsAt = pullSeconds * fadeStartsAt;
        float fade = fadeSeconds > 0f
            ? Mathf.Clamp01((_elapsed - startsAt) / fadeSeconds)
            : (_elapsed >= startsAt ? 1f : 0f);

        if (blackout != null) SetAlpha(blackout, fade);

        if (fade < 1f) return;

        float sinceBlack = _elapsed - (startsAt + fadeSeconds);

        float titleAlpha = titleFadeSeconds > 0f
            ? Mathf.Clamp01((sinceBlack - titleDelaySeconds) / titleFadeSeconds)
            : (sinceBlack >= titleDelaySeconds ? 1f : 0f);

        SetAlpha(TitleGraphic, titleAlpha);

        if (titleAlpha < 1f) return;

        if (!_finished)
        {
            _finished = true;
            onFinished.Invoke();

            if (debugLog) Debug.Log("[TutorialOutro_NEW] Finished.", this);
        }

        if (_returnToAttractCancelled) return;
        if (holdBeforeAttractSeconds <= 0f || attract == null) return;

        float held = sinceBlack - titleDelaySeconds - titleFadeSeconds;
        if (held < holdBeforeAttractSeconds) return;

        // Back to the title card, through the attract rather than the director. Show()
        // ends with onReset, and this component's ResetForAttract is hung on it — so the
        // camera comes back as part of the same teardown everything else uses, and there
        // is no path that restarts the piece without it.
        attract.Show();
    }

    /// <summary>
    /// The same event as the onFinished field, for code to subscribe to. SceneHandoff_NEW
    /// listens here so the tutorial-to-journey handoff needs no Inspector wiring — and so
    /// forgetting to wire it cannot silently leave the piece looping on the tutorial.
    /// </summary>
    public UnityEvent OnFinishedEvent { get { return onFinished; } }

    bool _returnToAttractCancelled;

    /// <summary>
    /// Do not go back to the title card after the hold — something else is taking over.
    ///
    /// For SceneHandoff_NEW, which loads the journey from this black frame. Without it
    /// the outro would call attract.Show() partway through the load and reset the
    /// tutorial underneath a scene that is about to replace it.
    ///
    /// Only suppresses the automatic return; it is cleared by ResetForAttract, so the
    /// next visitor's tutorial ends the normal way unless the handoff asks again.
    /// </summary>
    public void CancelReturnToAttract()
    {
        _returnToAttractCancelled = true;
    }

    Transform LightTransform()
    {
        if (travel != null) return travel.transform;

        // No travel component is a misconfigured scene, not a state worth handling — but
        // the camera's own parent is the light in every scene this builder makes, so it
        // is a better guess than giving up.
        return viewCamera != null ? viewCamera.transform.parent : null;
    }

    bool HasTitleArt { get { return titleImage != null && titleImage.sprite != null; } }

    /// <summary>Whichever of the picture and the words is the title this run.</summary>
    Graphic TitleGraphic { get { return HasTitleArt ? (Graphic)titleImage : title; } }

    static void SetAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null) return;

        Color c = graphic.color;
        if (Mathf.Approximately(c.a, alpha)) return;

        c.a = alpha;
        graphic.color = c;
    }
}

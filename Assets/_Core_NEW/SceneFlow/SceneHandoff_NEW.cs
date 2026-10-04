using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Carries the piece from the end of the tutorial into the journey, through black.
///
/// UNTIL NOW THERE WAS NO HANDOFF AT ALL. TutorialOutro_NEW faded to black, put up
/// WELCOME TO THE JOURNEY, and then went back to the tutorial's own title card. Nothing
/// anywhere in _Core_NEW loaded another scene.
///
/// THE SEQUENCE:
///
///   1  Outro finishes (title fully up)   → start loading the journey in the background,
///                                          and tell the outro not to return to attract
///   2  Title holds for titleHoldSeconds  → and the load reaches 90%
///   3  Fade this overlay to black        → covers the title; the tutorial's own black
///                                          is about to be unloaded with its scene
///   4  Activate the journey scene        → under full black
///   5  Hold black for settleSeconds      → Cinemachine blends and the first layer's
///                                          anchors fire where nobody can see them
///   6  Fade up                           → onto the journey's opening frame
///   7  Destroy self
///
/// WHY BLACK IS THE SEAM. The two scenes differ in almost everything that could show a
/// cut: camera rig (first person vs orbit), world scale (25 u/s vs 2.5), sky, HUD. Black
/// hides all of it at once, plus the load hitch. The only thing it cannot hide is a
/// framing mismatch between the last frame before black and the first after — which is
/// why the outro's pull-back should end on roughly the journey's default camera framing.
/// That is a tuning job on TutorialOutro_NEW's endDistance/endHeight, not code.
///
/// SWITCH: handoffEnabled. Off = the outro returns to attract exactly as before.
///
/// This must sit on its OWN root GameObject in the tutorial scene: at step 1 it moves
/// itself to DontDestroyOnLoad so it survives the scene it lives in being unloaded.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("HANDOFF", "#B36AE2")]
public class SceneHandoff_NEW : MonoBehaviour
{
    [Header("Switch")]
    [Tooltip("Off = the tutorial ends and returns to its title card, exactly as before " +
             "this component existed.")]
    [SerializeField] bool handoffEnabled = true;

    [Header("Wiring")]
    [Tooltip("The tutorial's outro. Found in the scene if empty. The handoff subscribes " +
             "to its onFinished event, so no Inspector wiring is needed.")]
    [SerializeField] TutorialOutro_NEW outro;

    [Tooltip("Scene to load, as an asset path.\n\n" +
             "In a BUILD this scene must be in File > Build Settings, or the handoff logs " +
             "an error and the tutorial returns to attract as before. In the Editor it is " +
             "loaded even if it is not listed, with a warning — so play mode works before " +
             "the build list is settled.")]
    [SerializeField] string journeyScenePath = "Assets/_Core_NEW/Gameplay_Scene_Ana.unity";

    [Header("Timing (real seconds)")]
    [Tooltip("How long WELCOME TO THE JOURNEY stays up after it has fully faded in. The " +
             "load runs underneath; if it is slower than this, the title simply holds " +
             "until it is ready.\n\n" +
             "4, not 3, because the tutorial's Closing line is still being said here: " +
             "the journey loads Single and takes the tutorial's AudioSource with it, so " +
             "a shorter hold cuts the last half second off the line.")]
    [Min(0f)]
    [SerializeField] float titleHoldSeconds = 4f;

    [Tooltip("Fade from the title to full black.")]
    [Min(0f)]
    [SerializeField] float fadeToBlackSeconds = 1f;

    [Tooltip("Black held after the journey scene activates, before fading up. Covers " +
             "the first-frame camera blend and any one-off startup work.")]
    [Min(0f)]
    [SerializeField] float settleSeconds = 0.75f;

    [Tooltip("Fade up from black onto the journey.")]
    [Min(0f)]
    [SerializeField] float fadeInSeconds = 2.5f;

    [Header("Overlay")]
    [Tooltip("Sort order of the black overlay canvas. Must be above every HUD canvas in " +
             "both scenes, including the Vizlab display overlays.")]
    [SerializeField] int overlaySortOrder = 32000;

    [Header("Debug")]
    [SerializeField] bool debugLog = true;

    bool _started;
    Image _overlay;

    /// <summary>
    /// True from the moment a handoff starts until it has faded up and finished. Read by
    /// things in the journey scene that should behave differently when the visitor has
    /// just come from the tutorial — JourneyIntroCamera_NEW waits for the reveal.
    /// </summary>
    public static bool IsHandingOff { get; private set; }

    /// <summary>Fired once, at the start of the fade up onto the journey.</summary>
    public static event System.Action RevealStarting;

    void OnEnable()
    {
        if (outro == null) outro = FindObjectOfType<TutorialOutro_NEW>();

        if (outro != null) outro.OnFinishedEvent.AddListener(Begin);
        else if (handoffEnabled) Debug.LogWarning("[SceneHandoff_NEW] No TutorialOutro_NEW in the scene; the handoff will never start.", this);
    }

    void OnDisable()
    {
        if (outro != null) outro.OnFinishedEvent.RemoveListener(Begin);
    }

    /// <summary>
    /// Start the handoff. Called by the outro's onFinished; public so it can also be
    /// driven from a debug key or a UnityEvent.
    /// </summary>
    public void Begin()
    {
        if (!handoffEnabled || _started) return;

        AsyncOperation load = StartLoad();
        if (load == null) return;   // StartLoad logged why; the outro returns to attract as before.

        _started = true;
        IsHandingOff = true;
        load.allowSceneActivation = false;

        // Only now that the load is really running: stop the outro going back to attract.
        if (outro != null) outro.CancelReturnToAttract();

        // Survive our own scene being unloaded.
        if (outro != null) outro.OnFinishedEvent.RemoveListener(Begin);
        transform.SetParent(null, false);
        DontDestroyOnLoad(gameObject);

        BuildOverlay();
        StartCoroutine(Run(load));
    }

    AsyncOperation StartLoad()
    {
        if (string.IsNullOrEmpty(journeyScenePath))
        {
            Debug.LogError("[SceneHandoff_NEW] journeyScenePath is empty.", this);
            return null;
        }

#if UNITY_EDITOR
        // A path does not follow a rename. Say so here, rather than failing inside the load.
        if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(journeyScenePath) == null)
        {
            Debug.LogError("[SceneHandoff_NEW] There is no scene at '" + journeyScenePath + "'. " +
                           "Was it renamed or moved? journeyScenePath is a path, so it does not " +
                           "follow a rename; set it to the journey scene's new path.", this);
            return null;
        }
#endif

        if (SceneUtility.GetBuildIndexByScenePath(journeyScenePath) >= 0)
            return SceneManager.LoadSceneAsync(journeyScenePath, LoadSceneMode.Single);

#if UNITY_EDITOR
        Debug.LogWarning("[SceneHandoff_NEW] '" + journeyScenePath + "' is not in Build Settings. " +
                         "Loading it anyway because this is the Editor — add it before building, " +
                         "or the handoff will not run in the build.", this);
        return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
            journeyScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
        Debug.LogError("[SceneHandoff_NEW] '" + journeyScenePath + "' is not in Build Settings; " +
                       "staying in the tutorial.", this);
        return null;
#endif
    }

    IEnumerator Run(AsyncOperation load)
    {
        Log("Loading the journey under the title.");

        // 2 · title holds, load runs
        float held = 0f;
        while (held < titleHoldSeconds || load.progress < 0.9f)
        {
            held += Time.unscaledDeltaTime;
            yield return null;
        }

        // 3 · to black
        yield return Fade(0f, 1f, fadeToBlackSeconds);

        // Anything the tutorial left slowed down (slow motion, pause) must not leak into
        // the journey, which assumes normal time from its first frame.
        if (!Mathf.Approximately(Time.timeScale, 1f))
        {
            Log("Time scale was " + Time.timeScale + " at the handoff; restoring 1.");
            Time.timeScale = 1f;
        }

        // 4 · activate under black
        load.allowSceneActivation = true;
        while (!load.isDone) yield return null;

        Log("Journey scene active.");

        // 5 · settle
        float settled = 0f;
        while (settled < settleSeconds)
        {
            settled += Time.unscaledDeltaTime;
            yield return null;
        }

        // 6 · up
        System.Action reveal = RevealStarting;
        if (reveal != null) reveal();

        yield return Fade(1f, 0f, fadeInSeconds);

        Log("Handoff complete.");

        // 7
        Destroy(gameObject);
    }

    /// <summary>
    /// Statics survive between play sessions when domain reload is turned off (Enter Play
    /// Mode Options). Clear them at the start of every session so a handoff interrupted
    /// by stopping play cannot leave the next session waiting.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        IsHandingOff = false;
        RevealStarting = null;
    }

    void OnDestroy()
    {
        // However this object goes — finished, or torn down mid-load by a stopped play
        // session — the flag must not outlive it, or the next journey start waits for a
        // reveal that will never come.
        if (_started) IsHandingOff = false;
    }

    IEnumerator Fade(float from, float to, float seconds)
    {
        if (seconds <= 0f)
        {
            SetAlpha(to);
            yield break;
        }

        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / seconds);
            SetAlpha(Mathf.Lerp(from, to, k * k * (3f - 2f * k)));
            yield return null;
        }

        SetAlpha(to);
    }

    void BuildOverlay()
    {
        GameObject go = new GameObject("HandoffOverlay", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = overlaySortOrder;

        GameObject img = new GameObject("Black", typeof(RectTransform));
        img.transform.SetParent(go.transform, false);

        RectTransform rt = (RectTransform)img.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _overlay = img.AddComponent<Image>();
        _overlay.raycastTarget = false;
        SetAlpha(0f);
    }

    void SetAlpha(float a)
    {
        if (_overlay == null) return;
        _overlay.color = new Color(0f, 0f, 0f, a);
    }

    void Log(string message)
    {
        if (debugLog) Debug.Log("[SceneHandoff_NEW] " + message, this);
    }
}

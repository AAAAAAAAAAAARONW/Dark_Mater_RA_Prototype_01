using System.Collections;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Fades in the Earth arrival video when the final layer is entered.
///
/// Replaces EarthLayerResponderTest, which was attached twice in the scene — once on
/// the Earth gate and once on the EarthArrival marker — with both instances pointing at
/// the same VideoPlayer and CanvasGroup. Entering Earth ran two coroutines fading the
/// same group and calling Prepare() on the same player. Only one instance is wired here.
///
/// Also added: null guards on all three references (the original would throw), and a
/// re-entry guard so a repeated trigger cannot stack coroutines.
///
/// This rides LayerState_NEW directly rather than a camera anchor. The video is the
/// ending, not a world swap — it should begin as soon as the player arrives, not after
/// the cover camera has finished a transition the player will never see the end of.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("EARTH", "#4A94F2")]
public class EarthCutscene_NEW : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] LayerState_NEW state;

    [Tooltip("The layerId that triggers the ending.")]
    [SerializeField] string earthLayerId = "Earth";

    [Header("Video")]
    [SerializeField] VideoPlayer videoPlayer;
    [SerializeField] CanvasGroup videoCanvasGroup;
    [SerializeField] GameObject videoCanvas;

    [Header("Fade")]
    [Tooltip("Seconds after reaching Earth before the video fades in. With EarthClouds_NEW, " +
             "about when the camera comes out under its cloud (its ClearedAt), so the clouds are " +
             "seen first — the setup menu sets it. 0 = straight away.")]
    [Min(0f)] [SerializeField] float startDelay = 0f;

    [SerializeField] float fadeDuration = 1.5f;

    [Tooltip("Peak opacity of the overlay. The render layers are hidden behind it by " +
             "then, so anything below 1 simply dims the video against black.")]
    [Range(0f, 1f)]
    [SerializeField] float targetAlpha = 0.85f;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    Coroutine _running;

    /// <summary>Seconds after reaching Earth before the video fades in. For the setup menu.</summary>
    public float StartDelay
    {
        get => startDelay;
        set => startDelay = Mathf.Max(0f, value);
    }

    void Awake()
    {
        if (state == null) state = FindObjectOfType<LayerState_NEW>();

        if (videoPlayer == null) Debug.LogError("[EarthCutscene_NEW] No VideoPlayer assigned.", this);
        if (videoCanvasGroup == null) Debug.LogError("[EarthCutscene_NEW] No CanvasGroup assigned.", this);
        if (videoCanvas == null) Debug.LogError("[EarthCutscene_NEW] No canvas GameObject assigned.", this);
    }

    void Start()
    {
        // Clear the render texture so no stale frame flashes before playback starts.
        if (videoPlayer != null && videoPlayer.targetTexture != null)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = videoPlayer.targetTexture;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = previous;
        }

        if (videoCanvasGroup != null) videoCanvasGroup.alpha = 0f;
        if (videoCanvas != null) videoCanvas.SetActive(false);
    }

    void OnEnable()
    {
        if (state != null) state.OnLayerChanged += HandleLayerChanged;
    }

    void OnDisable()
    {
        if (state != null) state.OnLayerChanged -= HandleLayerChanged;

        if (_running != null) { StopCoroutine(_running); _running = null; }
    }

    void HandleLayerChanged(LayerProfile_NEW previous, LayerProfile_NEW current)
    {
        if (current == null || current.layerId != earthLayerId) return;
        if (_running != null) return;                       // already playing

        if (videoPlayer == null || videoCanvasGroup == null || videoCanvas == null)
        {
            Debug.LogError("[EarthCutscene_NEW] Reached Earth but the video is not wired up.", this);
            return;
        }

        _running = StartCoroutine(Play());
    }

    IEnumerator Play()
    {
        if (debugLog) Debug.Log("[EarthCutscene_NEW] Earth reached — starting the ending.", this);

        videoCanvas.SetActive(true);
        videoCanvasGroup.alpha = 0f;

        // Prepared straight away, played once the delay is up — the clouds first.
        float arrived = Time.time;
        videoPlayer.Prepare();
        yield return new WaitUntil(() => videoPlayer.isPrepared && Time.time - arrived >= startDelay);
        videoPlayer.Play();

        float elapsed = 0f;
        float d = Mathf.Max(0.01f, fadeDuration);
        while (elapsed < d)
        {
            elapsed += Time.deltaTime;
            videoCanvasGroup.alpha = Mathf.Clamp01(elapsed / d) * targetAlpha;
            yield return null;
        }

        videoCanvasGroup.alpha = targetAlpha;
        _running = null;
    }
}

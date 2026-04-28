// EarthLayerResponderTest.cs
using System.Collections;
using UnityEngine;
using UnityEngine.Video;

public class EarthLayerResponderTest : MonoBehaviour
{
    [Header("Layer Manager")]
    public LayerStateManagerTest layerStateManager;

    [Header("Layer Config")]
    public string earthLayerId = "Earth";

    [Header("Video")]
    public VideoPlayer videoPlayer;
    public CanvasGroup videoCanvasGroup;
    public GameObject videoCanvas;

    [Header("Fade")]
    public float fadeDuration = 1.5f;
    [Range(0f, 1f)]
    [Tooltip("Max alpha of the video overlay. Below 1 keeps the 3D scene visible underneath.")]
    public float targetAlpha = 0.85f;

    void Start()
    {
        // Clear the RenderTexture so no stale frame leaks through before the video plays
        if (videoPlayer != null && videoPlayer.targetTexture != null)
        {
            RenderTexture rt = videoPlayer.targetTexture;
            RenderTexture.active = rt;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = null;
        }

        // Ensure canvas starts fully transparent
        if (videoCanvasGroup != null)
            videoCanvasGroup.alpha = 0f;
    }

    void OnEnable()
    {
        if (layerStateManager != null)
            layerStateManager.OnLayerChanged += HandleLayerChanged;
    }

    void OnDisable()
    {
        if (layerStateManager != null)
            layerStateManager.OnLayerChanged -= HandleLayerChanged;
    }

    void HandleLayerChanged(LayerDefinitionTest previous, LayerDefinitionTest current)
    {
        if (current != null && current.layerId == earthLayerId)
        {
            StartCoroutine(PlayEarthSequence());
        }
    }

    IEnumerator PlayEarthSequence()
    {
        // 1. Activate canvas, start transparent
        videoCanvas.SetActive(true);
        videoCanvasGroup.alpha = 0f;

        // 2. Prepare video before fade so it's ready
        videoPlayer.Prepare();
        yield return new WaitUntil(() => videoPlayer.isPrepared);

        // 3. Fade in to targetAlpha (keeps 3D scene visible underneath)
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            videoCanvasGroup.alpha = Mathf.Clamp01(t / fadeDuration) * targetAlpha;
            yield return null;
        }
        videoCanvasGroup.alpha = targetAlpha;

        // 4. Play video and wait for it to finish
        videoPlayer.Play();
        yield return new WaitUntil(() => !videoPlayer.isPlaying);

        // 5. End — uncomment the option you need:
        Application.Quit();                                        // Option A: quit (build)
        // UnityEngine.SceneManagement.SceneManager.LoadScene("CreditsScene");  // Option B: credits scene
        // yield break;                                            // Option C: freeze (editor test)
    }
}


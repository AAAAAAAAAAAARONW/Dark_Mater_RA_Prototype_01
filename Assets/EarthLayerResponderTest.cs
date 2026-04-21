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

        // 2. Prepare video
        videoPlayer.Prepare();
        yield return new WaitUntil(() => videoPlayer.isPrepared);

        // 3. Fade in
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            videoCanvasGroup.alpha = Mathf.Clamp01(t / fadeDuration);
            yield return null;
        }
        videoCanvasGroup.alpha = 1f;

        // 4. Play video and wait for it to finish
        videoPlayer.Play();
        yield return new WaitUntil(() => !videoPlayer.isPlaying);

        // 5. End
        Application.Quit();                                        // Option A: quit (build)
        // SceneManager.LoadScene("CreditsScene");                 // Option B: credits scene
        // yield break;                                            // Option C: freeze (editor test)
    }
}
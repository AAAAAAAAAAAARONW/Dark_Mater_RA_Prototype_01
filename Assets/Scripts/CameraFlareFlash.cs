using System.Collections;
using UnityEngine;

/// <summary>
/// 轻量级相机闪光（全屏叠加）：用于在切换前做一次短促 flare。
/// </summary>
[DisallowMultipleComponent]
public class CameraFlareFlash : MonoBehaviour
{
    [SerializeField] Color defaultColor = Color.white;
    [Range(0f, 1f)]
    [SerializeField] float defaultPeakAlpha = 0.75f;
    [SerializeField] float defaultDuration = 0.2f;

    float _alpha;
    Color _color;
    Coroutine _running;

    public void PlayFlash()
    {
        PlayFlash(defaultDuration, defaultPeakAlpha, defaultColor);
    }

    public void PlayFlash(float duration, float peakAlpha, Color color)
    {
        if (_running != null) StopCoroutine(_running);
        _running = StartCoroutine(FlashRoutine(Mathf.Max(0.01f, duration), Mathf.Clamp01(peakAlpha), color));
    }

    IEnumerator FlashRoutine(float duration, float peakAlpha, Color color)
    {
        _color = color;
        float half = duration * 0.5f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            if (t <= half)
            {
                float p = t / Mathf.Max(0.0001f, half);
                _alpha = Mathf.Lerp(0f, peakAlpha, p);
            }
            else
            {
                float p = (t - half) / Mathf.Max(0.0001f, half);
                _alpha = Mathf.Lerp(peakAlpha, 0f, p);
            }
            yield return null;
        }
        _alpha = 0f;
        _running = null;
    }

    void OnGUI()
    {
        if (_alpha <= 0.001f) return;
        Color old = GUI.color;
        GUI.color = new Color(_color.r, _color.g, _color.b, _alpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = old;
    }
}

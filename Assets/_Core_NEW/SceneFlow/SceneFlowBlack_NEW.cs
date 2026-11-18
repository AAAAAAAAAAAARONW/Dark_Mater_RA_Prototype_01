using UnityEngine;
using UnityEngine.UI;

/// <summary>SceneFlow_NEW's black sheet: holds a moment in the new scene, lifts, and goes.</summary>
public class SceneFlowBlack_NEW : MonoBehaviour
{
    public Image sheet;
    float _t;
    int _frames;

    void Update()
    {
        // The first frames after a load are long ones: they are not counted.
        if (++_frames <= 2) return;
        _t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        float k = Mathf.Clamp01((_t - SceneFlow_NEW.SettleSeconds) / SceneFlow_NEW.OpenSeconds);
        if (sheet != null) sheet.color = new Color(0f, 0f, 0f, 1f - k * k * (3f - 2f * k));
        if (k >= 1f) Destroy(gameObject);
    }
}

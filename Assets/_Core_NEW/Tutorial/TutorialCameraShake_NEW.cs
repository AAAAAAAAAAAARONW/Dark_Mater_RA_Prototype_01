using UnityEngine;

/// <summary>
/// Positional jitter on the camera. C2's "high frequency frame jitter".
///
/// Position and not rotation, deliberately. FirstPersonLookRig_NEW writes
/// transform.rotation every frame, so anything else writing rotation would be fighting
/// it — and losing, since the rig runs in Update and would simply overwrite the shake.
/// Position is unclaimed, so this can own it outright in LateUpdate with nothing to
/// coordinate. It also happens to be the better effect: a shaken position reads as the
/// frame itself being unstable, which is what the storyboard's "double outline = frame
/// jitter, not a second UI layer" is describing.
///
/// The amplitude is driven from outside rather than run as a fixed animation, because
/// C1 rises into C2 over twelve seconds and then C3 cuts it dead. TutorialEmission_NEW
/// owns that curve; this just applies whatever it is told.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("SHAKE", "#C0607A")]
public class TutorialCameraShake_NEW : MonoBehaviour
{
    [Header("Shape")]
    [Tooltip("Metres of displacement at amplitude 1. Small: this is a camera that cannot " +
             "hold still, not a camera being thrown around.")]
    [SerializeField] float maxOffset = 0.12f;

    [Tooltip("How fast the noise moves. High is the point — the storyboard says high " +
             "frequency, and slow jitter reads as drift rather than stress.")]
    [SerializeField] float frequency = 34f;

    [Header("State")]
    [Tooltip("0 to 1. Driven by TutorialEmission_NEW; set it by hand to preview.")]
    [Range(0f, 1f)]
    [SerializeField] float amplitude = 0f;

    Vector3 _restLocalPosition;
    bool _captured;

    /// <summary>0 = still, 1 = full jitter.</summary>
    public float Amplitude
    {
        get { return amplitude; }
        set { amplitude = Mathf.Clamp01(value); }
    }

    void OnEnable()
    {
        Capture();
    }

    void OnDisable()
    {
        if (_captured) transform.localPosition = _restLocalPosition;
    }

    void Capture()
    {
        if (_captured) return;

        _restLocalPosition = transform.localPosition;
        _captured = true;
    }

    /// <summary>
    /// LateUpdate, so the rig has already written this frame's rotation and the offset
    /// lands on top of a settled transform rather than being overwritten by it.
    /// </summary>
    void LateUpdate()
    {
        Capture();

        if (amplitude <= 0.0001f)
        {
            transform.localPosition = _restLocalPosition;
            return;
        }

        // Three uncorrelated Perlin walks. Perlin rather than Random so the motion is
        // continuous — random per frame reads as a broken renderer, not as vibration.
        float t = Time.unscaledTime * frequency;

        Vector3 offset = new Vector3(
            Mathf.PerlinNoise(t, 0.37f) - 0.5f,
            Mathf.PerlinNoise(t, 5.11f) - 0.5f,
            Mathf.PerlinNoise(t, 9.73f) - 0.5f);

        transform.localPosition = _restLocalPosition + offset * (2f * maxOffset * amplitude);
    }
}

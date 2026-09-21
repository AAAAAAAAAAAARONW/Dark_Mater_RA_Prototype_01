using UnityEngine;

/// <summary>
/// Positional jitter on the camera. C2's "high frequency frame jitter".
///
/// Position and not rotation, deliberately. A shaken position reads as the frame itself
/// being unstable, which is what the storyboard's "double outline = frame jitter, not a
/// second UI layer" is describing.
///
/// ADDITIVE, NOT OWNED. This used to capture a rest position once and write
/// rest + jitter into localPosition every LateUpdate, on the grounds that nothing else
/// claimed position. The follow view changed that: FirstPersonLookRig_NEW now places the
/// eye behind and above the light by writing localPosition itself, every Update. A shake
/// that restored a captured rest point would have pulled the camera back onto the light
/// on every frame — including every frame at zero amplitude.
///
/// So the rig owns the pose and this adds on top. The rig rewrites the position each
/// Update, which discards last frame's jitter before this adds the next one, so nothing
/// accumulates and there is nothing to restore.
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

    /// <summary>0 = still, 1 = full jitter.</summary>
    public float Amplitude
    {
        get { return amplitude; }
        set { amplitude = Mathf.Clamp01(value); }
    }

    float _kick;
    float _kickPeak;
    float _kickSeconds;

    /// <summary>
    /// A short jolt ON TOP of whatever Amplitude is doing, falling back to nothing.
    ///
    /// Additive and self-decaying, not a write to Amplitude, because Amplitude already has
    /// an owner — TutorialEmission_NEW ramps it through C1 to C3 — and an impact that set
    /// it would either be overwritten on the next frame or leave the emission's value
    /// wrong after it ended. This way both can happen at once and neither has to know.
    ///
    /// A stronger kick replaces a weaker one still running; a weaker one arriving during
    /// a stronger one is ignored, so four atoms in a row do not stack into a blur.
    /// </summary>
    public void Kick(float amount, float seconds)
    {
        amount = Mathf.Clamp01(amount);
        if (amount < _kick) return;

        _kick = _kickPeak = amount;
        _kickSeconds = Mathf.Max(0.01f, seconds);
    }

    /// <summary>
    /// LateUpdate, so the rig has already written this frame's pose and the jitter lands
    /// on top of it rather than being overwritten by it.
    /// </summary>
    void LateUpdate()
    {
        // Real time, so the jolt is the same length inside D5's slow motion — it is the
        // player's view being struck, not something happening in the slowed world. Held
        // while paused: a camera still trembling behind the pause card reads as a fault.
        if (_kick > 0f && !TutorialClock_NEW.Paused)
            _kick = Mathf.MoveTowards(_kick, 0f, _kickPeak * Time.unscaledDeltaTime / _kickSeconds);

        float strength = Mathf.Clamp01(amplitude + _kick);

        // Nothing to write at rest. The rig has already put the eye where it belongs.
        if (strength <= 0.0001f) return;

        // Three uncorrelated Perlin walks. Perlin rather than Random so the motion is
        // continuous — random per frame reads as a broken renderer, not as vibration.
        float t = TutorialClock_NEW.Time * frequency;

        Vector3 offset = new Vector3(
            Mathf.PerlinNoise(t, 0.37f) - 0.5f,
            Mathf.PerlinNoise(t, 5.11f) - 0.5f,
            Mathf.PerlinNoise(t, 9.73f) - 0.5f);

        transform.localPosition += offset * (2f * maxOffset * strength);
    }
}

using UnityEngine;

/// <summary>
/// The 16 Custom/Nebula shader parameters, extracted out of the layer profile.
///
/// Kept as its own asset for two reasons: the parameters are orthogonal to the
/// layer concept (two layers can share one look), and it drops a third of the
/// fields out of LayerProfile_NEW.
///
/// Property names and value ranges are identical to the originals — this asset
/// changes where the numbers live, never what they mean.
/// </summary>
[CreateAssetMenu(menuName = "Journey NEW/Nebula Profile", fileName = "NP_New")]
public class NebulaProfile_NEW : ScriptableObject
{
    [Header("Colors")]
    public Color colorDark = new Color(0.08f, 0.02f, 0.18f, 1f);
    public Color colorMid = new Color(0.3f, 0.15f, 0.5f, 1f);
    public Color colorBright = new Color(0.6f, 0.4f, 0.9f, 1f);
    public Color colorStar = new Color(1f, 0.95f, 1f, 1f);

    [Header("Noise")]
    public float scale = 1.2f;
    public float octaves = 4f;
    public float persistence = 0.5f;
    public float density = 0.8f;
    public float sharpness = 1.5f;

    [Header("Stars")]
    public float starScale = 80f;
    public float starThreshold = 0.992f;
    public float starBrightness = 1.2f;

    [Header("Star twinkle")]
    [Tooltip("1 = twinkle, 0 = steady. Snapped to one or the other.\n\n" +
             "Defaults to 0, which is what the shader did before twinkle existed, so " +
             "the seven existing profiles keep rendering exactly as they were.")]
    public float starTwinkle = 0f;

    [Tooltip("Cycles per second. Each star gets its own phase from the noise cell it " +
             "sits in, so the sky does not pulse in unison.")]
    public float starTwinkleSpeed = 1.5f;

    [Tooltip("How far the brightness dips at the bottom of the cycle. 0 is no twinkle, " +
             "1 takes stars all the way to black.")]
    public float starTwinkleAmount = 0.5f;

    [Header("Animation")]
    [Tooltip("1 = animate, 0 = frozen. Snapped to one or the other.")]
    public float animate = 1f;
    public float speed = 0.05f;

    [Header("Blend")]
    [Tooltip("Seconds to tween into this look. 0 = snap. Snapping is the default because " +
             "the swap happens under the top-down cover where the player cannot see it.")]
    public float blendDuration = 0f;

    void OnValidate()
    {
        // Same clamps the original LayerDefinitionTest applied.
        scale = Mathf.Clamp(scale, 0.5f, 4f);
        octaves = Mathf.Clamp(octaves, 1f, 6f);
        persistence = Mathf.Clamp(persistence, 0.2f, 0.9f);
        density = Mathf.Clamp(density, 0.2f, 2f);
        sharpness = Mathf.Clamp(sharpness, 0.5f, 4f);
        starScale = Mathf.Clamp(starScale, 20f, 200f);
        starThreshold = Mathf.Clamp(starThreshold, 0.95f, 0.999f);
        starBrightness = Mathf.Clamp(starBrightness, 0.5f, 3f);

        starTwinkle = starTwinkle >= 0.5f ? 1f : 0f;
        starTwinkleSpeed = Mathf.Clamp(starTwinkleSpeed, 0f, 8f);
        starTwinkleAmount = Mathf.Clamp01(starTwinkleAmount);

        animate = animate >= 0.5f ? 1f : 0f;
        speed = Mathf.Clamp(speed, 0f, 0.5f);
        blendDuration = Mathf.Max(0f, blendDuration);
    }
}

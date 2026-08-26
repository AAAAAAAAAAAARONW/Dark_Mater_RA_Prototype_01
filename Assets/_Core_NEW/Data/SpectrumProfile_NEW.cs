using UnityEngine;

/// <summary>
/// Per-layer Lyman-alpha forest parameters.
///
/// The old LayerDefinitionTest carried ten laf* fields, of which exactly one
/// (lafContinuumPeakPosition) ever reached anything. The rest were dead because they
/// were all handed to LAFSpectrumHUD, and the HUD does not generate absorption lines —
/// LymanAlphaAbsorptionController does. The parameters were being delivered to the wrong
/// component.
///
/// This asset splits them by who actually owns the behaviour:
///
///   continuum*  → the HUD, which draws the quasar emission curve
///   dip* / seed → the absorption field, which stamps the forest lines
///   scrollSpeed → the absorption field's drift, which the HUD then follows
///
/// Widths are normalised fractions of the spectrum, not pixels, so changing the
/// absorption field's resolution does not change how wide a line looks.
/// </summary>
[CreateAssetMenu(menuName = "Journey NEW/Spectrum Profile", fileName = "SP_New")]
public class SpectrumProfile_NEW : ScriptableObject
{
    [Header("Continuum shape (drives the HUD curve)")]
    [Tooltip("Normalised position of the Ly-alpha emission peak. 1216A maps to ~0.42 across 800-1800A.")]
    [Range(0f, 1f)]
    public float continuumPeakPosition = 0.42f;

    [Header("Forest lines (drive the absorption field)")]
    [Tooltip("Lines pre-spawned at layer entry so the forest is never empty.")]
    [Range(0, 400)]
    public int initialLineCount = 80;

    [Tooltip("Lines spawned per second at the reference player speed.")]
    public float spawnRatePerSecond = 4f;

    [Tooltip("Seed for this layer's forest. Same seed gives the same forest every run.")]
    public int randomSeed = 42;

    [Tooltip("Line width as a fraction of the whole spectrum. Resolution-independent: " +
             "0.0039 is one texel at 256, eight texels at 2048, and looks the same either way.")]
    public float dipWidthMin = 0.0039f;
    public float dipWidthMax = 0.0117f;

    [Tooltip("Line depth. 0 = invisible, 1 = full absorption.")]
    [Range(0f, 1f)] public float dipDepthMin = 0.20f;
    [Range(0f, 1f)] public float dipDepthMax = 0.65f;

    [Tooltip("Where in the spectrum lines appear. Defaults cover the forest region, " +
             "from the Lyman limit up to just short of Ly-alpha.")]
    public Vector2 spawnRangeUV = new Vector2(0.05f, 0.40f);

    [Header("Drift")]
    [Tooltip("Whole-spectrum redshift drift, in normalised units per second at reference speed. " +
             "The HUD reads this from the field so the two stay locked.")]
    public float driftPerSecond = 0.005f;

    void OnValidate()
    {
        initialLineCount = Mathf.Max(0, initialLineCount);
        spawnRatePerSecond = Mathf.Max(0f, spawnRatePerSecond);

        dipWidthMin = Mathf.Clamp(dipWidthMin, 0.0001f, 0.25f);
        dipWidthMax = Mathf.Clamp(dipWidthMax, dipWidthMin, 0.25f);
        dipDepthMax = Mathf.Max(dipDepthMin, dipDepthMax);

        spawnRangeUV.x = Mathf.Clamp01(spawnRangeUV.x);
        spawnRangeUV.y = Mathf.Clamp(spawnRangeUV.y, spawnRangeUV.x, 1f);

        driftPerSecond = Mathf.Max(0f, driftPerSecond);
    }
}

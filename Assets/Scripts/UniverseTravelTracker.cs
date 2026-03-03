using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Accumulates "universe distance" (light years) over time using the current scale layer.
/// Supports multiple triggers: assign Layer Sources array; first trigger that has player inside wins.
/// Macro slider = total distance, always visible. Micro slider = distance in current micro segment; only visible when in a micro layer. Micro max can be set per-trigger so the bar goes 0→1 across the zone.
///
/// SETUP:
/// 1. Create Canvas and two UI Sliders (macro + micro). Assign Layer Sources.
/// 2. Macro: Max Display Light Years e.g. 5e9. Micro: default Micro Max Display Light Years e.g. 5e4, or set per-trigger on each MicroToMacroTriggerSequenceTest (Micro Slider Max Light Years When Inside) so the bar fills as the player crosses that zone.
/// </summary>
public class UniverseTravelTracker : MonoBehaviour
{
    [Header("Layer source (multiple triggers supported)")]
    [Tooltip("One or more triggers. First trigger that has player inside determines current layer. If empty, uses Layer Source (single) or FindObjectOfType.")]
    [SerializeField] MicroToMacroTriggerSequenceTest[] layerSources;
    [Tooltip("Backward compat: if Layer Sources array is empty, this single trigger is used.")]
    [SerializeField] MicroToMacroTriggerSequenceTest layerSource;

    [Header("Scale per layer (ly per game second)")]
    [Tooltip("Index = (int)UniverseLayer. Macro = 1e9/60, Micro = 1e4/60. Add one entry per new layer.")]
    [SerializeField] float[] lyPerGameSecondByLayer = new float[]
    {
        1e9f / 60f,  // Macro
        1e4f / 60f   // Micro (0.01 million ly per game minute)
    };

    [Header("Macro slider (total universe distance)")]
    [Tooltip("Slider for total distance. If empty and this object has a Slider, it is used.")]
    [SerializeField] Slider slider;
    [Tooltip("Bar fill = totalLy / this value. Use a large number (e.g. 5e9).")]
    [SerializeField] float maxDisplayLightYears = 5e9f;

    [Header("Micro slider (segment in micro layer)")]
    [Tooltip("Optional. Shown only when in a micro layer; shows distance since entering micro (0→max). Hidden when not in micro.")]
    [SerializeField] Slider microSlider;
    [Tooltip("Default micro bar max (ly). Overridden by the current trigger's Micro Slider Max Light Years When Inside if that is > 0.")]
    [SerializeField] float microMaxDisplayLightYears = 5e4f;

    [Header("Distance text (optional)")]
    [Tooltip("Optional. Shows current distance and unit: in Macro e.g. '2.5B ly', in Micro e.g. '12k ly'. Use a TextMeshPro (TMP) Text component.")]
    [SerializeField] TMP_Text distanceText;
    [Tooltip("Unit label in macro (e.g. 'billion ly'). Shown after the numeric value.")]
    [SerializeField] string macroUnitLabel = "billion ly";
    [Tooltip("Unit label in micro (e.g. '10k ly'). Shown after the numeric value.")]
    [SerializeField] string microUnitLabel = "10k ly";

    float _totalUniverseDistanceLy;
    float _microSegmentDistanceLy;

    void Awake()
    {
        if (layerSources == null || layerSources.Length == 0)
        {
            if (layerSource != null)
                layerSources = new[] { layerSource };
            else
            {
                var single = FindObjectOfType<MicroToMacroTriggerSequenceTest>();
                layerSources = single != null ? new[] { single } : new MicroToMacroTriggerSequenceTest[0];
            }
        }
        if (slider == null)
            slider = GetComponent<Slider>();
    }

    void Update()
    {
        UniverseLayer layer = GetCurrentLayer();
        float scale = GetScaleForLayer(layer);
        _totalUniverseDistanceLy += Time.deltaTime * scale;

        if (layer != UniverseLayer.Macro)
            _microSegmentDistanceLy += Time.deltaTime * scale;
        else
            _microSegmentDistanceLy = 0f;

        if (slider != null)
            slider.value = GetNormalizedProgress(maxDisplayLightYears);

        if (microSlider != null)
        {
            bool inMicro = layer != UniverseLayer.Macro;
            microSlider.gameObject.SetActive(inMicro);
            if (inMicro)
            {
                float microMax = GetMicroMaxForCurrentTrigger();
                float microProgress = microMax <= 0f ? 0f : Mathf.Clamp01(_microSegmentDistanceLy / microMax);
                microSlider.value = microProgress;
            }
        }

        if (distanceText != null)
            distanceText.text = FormatDistanceAndUnit(layer);
    }

    string FormatDistanceAndUnit(UniverseLayer layer)
    {
        // One continuous total distance; display it in the unit that matches the current layer.
        float totalLy = _totalUniverseDistanceLy;
        float valueInUnits;
        string unitLabel;

        if (layer == UniverseLayer.Macro)
        {
            // Macro layer: express distance in billions of light years.
            valueInUnits = totalLy / 1e9f;
            unitLabel = string.IsNullOrEmpty(macroUnitLabel) ? "1B light year" : macroUnitLabel;
        }
        else
        {
            // Micro (or other non‑macro) layers: express distance in 10k‑light‑year chunks.
            valueInUnits = totalLy / 1e4f;
            unitLabel = string.IsNullOrEmpty(microUnitLabel) ? "10k light year" : microUnitLabel;
        }

        return "Total Distance: " + valueInUnits.ToString("0.##") + "\nUnit: " + unitLabel;
    }

    float GetMicroMaxForCurrentTrigger()
    {
        MicroToMacroTriggerSequenceTest trigger = GetCurrentTrigger();
        if (trigger != null && trigger.MicroSliderMaxLightYearsWhenInside > 0f)
            return trigger.MicroSliderMaxLightYearsWhenInside;
        return microMaxDisplayLightYears;
    }

    MicroToMacroTriggerSequenceTest GetCurrentTrigger()
    {
        if (layerSources == null || layerSources.Length == 0) return null;
        for (int i = 0; i < layerSources.Length; i++)
        {
            if (layerSources[i] != null && layerSources[i].IsPlayerInside)
                return layerSources[i];
        }
        return null;
    }

    UniverseLayer GetCurrentLayer()
    {
        var trigger = GetCurrentTrigger();
        return trigger != null ? trigger.CurrentLayer : UniverseLayer.Macro;
    }

    float GetScaleForLayer(UniverseLayer layer)
    {
        int index = (int)layer;
        if (lyPerGameSecondByLayer == null || index < 0 || index >= lyPerGameSecondByLayer.Length)
            return lyPerGameSecondByLayer != null && lyPerGameSecondByLayer.Length > 0 ? lyPerGameSecondByLayer[0] : 1e9f / 60f;
        return lyPerGameSecondByLayer[index];
    }

    /// <summary>
    /// Total accumulated universe distance in light years.
    /// </summary>
    public float TotalUniverseDistanceLightYears => _totalUniverseDistanceLy;

    /// <summary>
    /// Distance accumulated in the current micro segment (resets when leaving micro). For debugging.
    /// </summary>
    public float MicroSegmentLightYears => _microSegmentDistanceLy;

    /// <summary>
    /// Current layer from the first trigger that has the player inside. For debugging.
    /// </summary>
    public UniverseLayer CurrentLayer => GetCurrentLayer();

    /// <summary>
    /// Normalized progress 0..1 for a given max light years (for slider or other UI).
    /// </summary>
    public float GetNormalizedProgress(float maxLightYears)
    {
        if (maxLightYears <= 0f) return 0f;
        return Mathf.Clamp01(_totalUniverseDistanceLy / maxLightYears);
    }

    /// <summary>
    /// Reset accumulated distance to zero (e.g. for new run or testing).
    /// </summary>
    public void ResetDistance()
    {
        _totalUniverseDistanceLy = 0f;
    }
}

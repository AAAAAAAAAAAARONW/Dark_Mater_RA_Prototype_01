using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fake HUD: draw a fixed-length X-axis waveform and progressively reveal
/// a synthetic Lyman-alpha forest profile from left to right in a fixed duration.
/// Attach to the same GameObject as UILineGraph (or assign one manually).
/// </summary>
public class FakeLymanAlphaForestHUD : MonoBehaviour
{
    enum DrawSpeedMode
    {
        ByDuration,
        ByXAxisUnitsPerSecond
    }

    [Header("Graph Target")]
    [SerializeField] UILineGraph graph;
    [SerializeField] UICanvasLineGraph canvasGraph;
    [SerializeField] UIImageLineGraphEffect imageGraph;
    [SerializeField] bool useUnscaledTime = true;

    [Header("X Axis (Fixed Length)")]
    [Tooltip("X-axis span shown in label units (visual only).")]
    [SerializeField] float xAxisLength = 120f;
    [Tooltip("How many points are sampled across the fixed X axis.")]
    [SerializeField] int sampleCount = 512;

    [Header("Y Range")]
    [SerializeField] float minValue = 0f;
    [SerializeField] float maxValue = 1f;
    [SerializeField] float baseline = 0.98f;
    [SerializeField] float noiseAmplitude = 0.008f;

    [Header("Progressive Draw")]
    [SerializeField] DrawSpeedMode drawSpeedMode = DrawSpeedMode.ByDuration;
    [Tooltip("Reveal full profile in this fixed duration (seconds). Used when mode = ByDuration.")]
    [SerializeField] float drawDuration = 10f;
    [Tooltip("How many X-axis units are revealed per second. Used when mode = ByXAxisUnitsPerSecond.")]
    [SerializeField] float drawSpeedXAxisUnitsPerSecond = 12f;
    [SerializeField] bool loop = false;

    [Header("Forest Shape")]
    [Tooltip("How many absorption dips to generate.")]
    [SerializeField] int dipCount = 140;
    [Tooltip("Random seed for deterministic fake profile.")]
    [SerializeField] int randomSeed = 42;
    [Tooltip("Minimum dip width in X-axis units.")]
    [SerializeField] float dipWidthMin = 0.035f;
    [Tooltip("Maximum dip width in X-axis units.")]
    [SerializeField] float dipWidthMax = 0.45f;
    [Tooltip("Minimum dip depth.")]
    [SerializeField] float dipDepthMin = 0.08f;
    [Tooltip("Maximum dip depth.")]
    [SerializeField] float dipDepthMax = 0.9f;
    [Tooltip("Larger value makes lines narrower and sharper.")]
    [SerializeField] float lineSharpness = 3.2f;
    [Tooltip("How strong the ultra-narrow core is relative to each dip.")]
    [SerializeField] float narrowCoreStrength = 0.65f;

    readonly List<float> _targetValues = new List<float>();
    readonly List<float> _displayValues = new List<float>();
    float _elapsed;

    struct Dip
    {
        public float centerX;
        public float sigma;
        public float depth;
    }

    readonly List<Dip> _dips = new List<Dip>();

    void Awake()
    {
        if (graph == null)
            graph = GetComponent<UILineGraph>();
        if (canvasGraph == null)
            canvasGraph = GetComponent<UICanvasLineGraph>();
        if (imageGraph == null)
            imageGraph = GetComponent<UIImageLineGraphEffect>();
    }

    void OnEnable()
    {
        RebuildTargetProfile();
        ResetDrawProgress();
        PushToGraph();
    }

    void Update()
    {
        if (!HasGraphTarget() || _targetValues.Count == 0) return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        _elapsed += dt;

        float t = CalculateRevealT();
        float revealIndexF = Mathf.Clamp(t * (sampleCount - 1), 0f, sampleCount - 1);
        int revealWhole = Mathf.FloorToInt(revealIndexF);
        float revealFrac = revealIndexF - revealWhole;

        for (int i = 0; i < sampleCount; i++)
            _displayValues[i] = baseline;

        for (int i = 0; i <= revealWhole; i++)
            _displayValues[i] = _targetValues[i];

        int nextIndex = revealWhole + 1;
        if (nextIndex < sampleCount)
            _displayValues[nextIndex] = Mathf.Lerp(baseline, _targetValues[nextIndex], revealFrac);

        PushToGraph();

        if (t >= 1f && loop)
            ResetDrawProgress();
    }

    float CalculateRevealT()
    {
        if (drawSpeedMode == DrawSpeedMode.ByXAxisUnitsPerSecond)
        {
            if (drawSpeedXAxisUnitsPerSecond <= 0f) return 1f;
            float revealedX = _elapsed * drawSpeedXAxisUnitsPerSecond;
            return Mathf.Clamp01(revealedX / xAxisLength);
        }

        if (drawDuration <= 0f) return 1f;
        return Mathf.Clamp01(_elapsed / drawDuration);
    }

    [ContextMenu("Rebuild Fake Forest")]
    public void RebuildTargetProfile()
    {
        sampleCount = Mathf.Max(2, sampleCount);
        xAxisLength = Mathf.Max(1f, xAxisLength);
        dipCount = Mathf.Max(0, dipCount);
        dipWidthMin = Mathf.Max(0.01f, dipWidthMin);
        dipWidthMax = Mathf.Max(dipWidthMin, dipWidthMax);
        dipDepthMin = Mathf.Clamp01(dipDepthMin);
        dipDepthMax = Mathf.Clamp(dipDepthMax, dipDepthMin, 1f);
        baseline = Mathf.Clamp(baseline, minValue, maxValue);
        lineSharpness = Mathf.Clamp(lineSharpness, 1.2f, 8f);
        narrowCoreStrength = Mathf.Clamp01(narrowCoreStrength);

        _targetValues.Clear();
        _displayValues.Clear();
        _dips.Clear();

        var rng = new System.Random(randomSeed);
        for (int i = 0; i < dipCount; i++)
        {
            float center = Mathf.Lerp(0f, xAxisLength, (float)rng.NextDouble());
            float width = Mathf.Lerp(dipWidthMin, dipWidthMax, (float)rng.NextDouble());
            float sigma = Mathf.Max(0.01f, width * 0.5f);
            float depth = Mathf.Lerp(dipDepthMin, dipDepthMax, Mathf.Pow((float)rng.NextDouble(), 1.25f));
            _dips.Add(new Dip { centerX = center, sigma = sigma, depth = depth });
        }

        for (int i = 0; i < sampleCount; i++)
        {
            float x = (i / (float)(sampleCount - 1)) * xAxisLength;
            float value = baseline;

            // Add tiny continuum noise so the line is not perfectly flat.
            value += ((float)rng.NextDouble() * 2f - 1f) * noiseAmplitude;

            // Subtract a sum of sharper absorption dips.
            // Each line = broad body + ultra-narrow core for "forest-like" spikes.
            for (int d = 0; d < _dips.Count; d++)
            {
                float dx = (x - _dips[d].centerX) / _dips[d].sigma;
                float body = Mathf.Exp(-0.5f * Mathf.Pow(Mathf.Abs(dx), lineSharpness));

                float coreSigma = Mathf.Max(0.003f, _dips[d].sigma * 0.28f);
                float dxCore = (x - _dips[d].centerX) / coreSigma;
                float core = Mathf.Exp(-0.5f * dxCore * dxCore);

                float absorption = _dips[d].depth * body
                                 + (_dips[d].depth * narrowCoreStrength) * core;
                value -= absorption;
            }

            _targetValues.Add(Mathf.Clamp(value, minValue, maxValue));
            _displayValues.Add(baseline);
        }
    }

    [ContextMenu("Restart Draw")]
    public void ResetDrawProgress()
    {
        _elapsed = 0f;
        for (int i = 0; i < _displayValues.Count; i++)
            _displayValues[i] = baseline;
    }

    void RevealAll()
    {
        for (int i = 0; i < _displayValues.Count; i++)
            _displayValues[i] = _targetValues[i];
    }

    void PushToGraph()
    {
        if (graph != null)
            graph.SetValues(_displayValues, minValue, maxValue);
        if (canvasGraph != null)
            canvasGraph.SetValues(_displayValues, minValue, maxValue);
        if (imageGraph != null)
            imageGraph.SetValues(_displayValues, minValue, maxValue);
    }

    bool HasGraphTarget()
    {
        return graph != null || canvasGraph != null || imageGraph != null;
    }
}

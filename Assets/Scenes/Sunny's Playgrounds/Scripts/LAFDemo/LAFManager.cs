using UnityEngine;
/// <summary>
/// Prototype LAF HUD manager: loads master CSV, maintains a chosen sample size sliding window, scrolls left through source indices simulating right shift.
/// Attach to empty game object and wire linegraph and csv references.
/// </summary>
public sealed class LAFManager : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] TextAsset spectrumCsv;
    [Tooltip("Expected full dataset size (your spec: 1000). Used only for validation warning.")]
    [SerializeField] int expectedMasterSamples = 1000;
    [Header("Window")]
    [SerializeField] int windowSize = 100;
    [SerializeField] bool loopWhenReachingStart = false;
    [Header("Scroll")]
    [Tooltip("How many window steps per second (each step moves one master index left).")]
    [SerializeField] float scrollSpeedStepsPerSecond = 5f;
    [Header("View")]
    [SerializeField] SpectrumLineGraph lineGraph;
    SpectrumSlidingWindow _buffer;
    float _accum;
    void Awake()
    {
        if (lineGraph == null)
            lineGraph = GetComponentInChildren<SpectrumLineGraph>();
        var master = SpectrumDataLoader.LoadFromTextAsset(spectrumCsv);
        if (expectedMasterSamples > 0 && master.Count != expectedMasterSamples)
            Debug.LogWarning($"Expected {expectedMasterSamples} samples, CSV has {master.Count}.");
        _buffer = new SpectrumSlidingWindow(master, windowSize, startAtEnd: true);
        Redraw();
    }
    void Update()
    {
        if (_buffer == null || scrollSpeedStepsPerSecond <= 0f) return;
        _accum += Time.deltaTime * scrollSpeedStepsPerSecond;
        int steps = Mathf.FloorToInt(_accum);
        if (steps <= 0) return;
        _accum -= steps;
        for (int i = 0; i < steps; i++)
        {
            if (!_buffer.TryStepLeft(loopWhenReachingStart))
                break;
        }
        Redraw();
    }
    void Redraw()
    {
        if (lineGraph != null)
            lineGraph.SetFluxSeries(_buffer.Visible);
    }
#if UNITY_EDITOR
    void OnValidate()
    {
        if (windowSize < 2) windowSize = 2;
        if (scrollSpeedStepsPerSecond < 0f) scrollSpeedStepsPerSecond = 0f;
    }
#endif
}
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HUD line graph for absorption value over time.
/// Attach to a UI object with UILineGraph.
/// </summary>
public class AbsorptionHUDGraph : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] PhotonTrailController source;

    [Header("Graph")]
    [SerializeField] UILineGraph graph;
    [Tooltip("Time window shown on X axis (seconds).")]
    [SerializeField] float windowSeconds = 100f;
    [SerializeField] float sampleInterval = 0.05f;
    [SerializeField] float minValue = 0f;
    [SerializeField] float maxValue = 1f;
    [Tooltip("Delta absorption that maps to 0 (full drop). 0 means no drop.")]
    [SerializeField] float maxDeltaForZero = 0.2f;
    [Tooltip("Amplify drop amount to make changes more visible.")]
    [SerializeField] float dropAmplify = 3f;
    [SerializeField] bool useUnscaledTime = true;

    readonly List<float> _values = new List<float>();
    float _timer;
    float _lastAbsorb;

    void Awake()
    {
        if (graph == null)
            graph = GetComponent<UILineGraph>();
    }

    void Update()
    {
        if (graph == null) return;
        if (source == null)
            source = FindObjectOfType<PhotonTrailController>();

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        _timer += dt;
        if (_timer < sampleInterval) return;
        _timer = 0f;

        int targetSamples = Mathf.Max(2, Mathf.RoundToInt(windowSeconds / sampleInterval));
        if (_values.Count != targetSamples)
        {
            _values.Clear();
            for (int i = 0; i < targetSamples; i++)
                _values.Add(0f);
        }

        float currentAbsorb = source != null ? source.CurrentAbsorb : 0f;
        float deltaAbsorb = currentAbsorb - _lastAbsorb;
        _lastAbsorb = currentAbsorb;
        if (deltaAbsorb < 0f) deltaAbsorb = 0f;

        _values.RemoveAt(0);
        float drop = maxDeltaForZero <= 0f ? 0f : Mathf.Clamp01(deltaAbsorb / maxDeltaForZero);
        drop = Mathf.Clamp01(drop * dropAmplify);
        float value = 1f - drop; // default 1, absorption drop toward 0
        _values.Add(value);

        graph.SetValues(_values, minValue, maxValue);
    }
}

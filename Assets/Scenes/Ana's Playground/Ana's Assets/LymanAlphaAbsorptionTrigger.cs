using UnityEngine;

/// <summary>
/// Trigger volume that injects DRAMATIC absorption events into the shared
/// LymanAlphaAbsorptionController while the player is inside the volume.
///
/// Place along the journey at hydrogen cloud positions. Bigger volumes →
/// player stays inside longer → more lines accumulate. Fine-grained intensity
/// is controlled by intensityMultiplier and burst settings.
///
/// SETUP:
/// 1. Add a Collider to this GameObject and set isTrigger = true.
/// 2. Assign LymanAlphaAbsorptionController reference (auto-found if blank).
/// 3. Tune burst settings — depth/width ranges, lines per burst, cluster spread.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LymanAlphaAbsorptionTrigger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] LymanAlphaAbsorptionController absorptionController;

    [Header("Filter")]
    [SerializeField] string targetTag = "Player";

    [Header("Intensity")]
    [Tooltip("Multiplier on burst depths. 0.5 = half-strength cloud, 2 = doubled (capped at 1).")]
    [Range(0f, 2f)]
    [SerializeField] float intensityMultiplier = 1f;

    [Header("Burst Pattern")]
    [Tooltip("How often a burst fires while the player is inside (seconds between bursts).")]
    [Range(0.05f, 5f)]
    [SerializeField] float burstInterval = 0.5f;
    [Tooltip("How many absorption lines per burst. >1 makes a cluster (DLA-style).")]
    [Range(1, 10)]
    [SerializeField] int linesPerBurst = 3;
    [Tooltip("Per-line depth range before intensityMultiplier is applied.")]
    [SerializeField] Vector2 depthRange = new Vector2(0.7f, 0.95f);
    [Tooltip("Per-line width range in absorption-buffer pixels.")]
    [SerializeField] Vector2Int widthRange = new Vector2Int(4, 8);

    [Header("Spawn Position")]
    [Tooltip("UV-end position [0..1] where this trigger's lines spawn. 0 = far UV.")]
    [Range(0f, 0.4f)]
    [SerializeField] float spawnPositionUV = 0.05f;
    [Tooltip("Cluster spread around the spawn position (in pixels). Each line jitters within this range.")]
    [Range(0, 30)]
    [SerializeField] int clusterSpreadPixels = 10;

    [Header("Timing")]
    [Tooltip("Fire one burst immediately on first enter (without waiting for the first interval).")]
    [SerializeField] bool burstOnEnter = true;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    float _stayTimer;
    bool  _wasInside;

    void Awake()
    {
        var col = GetComponent<Collider>();
        if (!col.isTrigger) col.isTrigger = true;

        if (absorptionController == null)
            absorptionController = FindObjectOfType<LymanAlphaAbsorptionController>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(targetTag)) return;
        _wasInside = true;
        _stayTimer = burstOnEnter ? burstInterval : 0f; // arm so the first Stay tick fires
    }

    void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag(targetTag)) return;
        if (absorptionController == null) return;

        _stayTimer += Time.deltaTime;
        if (_stayTimer < burstInterval) return;
        _stayTimer -= burstInterval;

        FireBurst();
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(targetTag)) return;
        _wasInside = false;
        _stayTimer = 0f;
    }

    void FireBurst()
    {
        int count = Mathf.Max(1, linesPerBurst);
        float clusterSpreadNorm = clusterSpreadPixels / (float)LymanAlphaAbsorptionController.TexWidth;

        for (int i = 0; i < count; i++)
        {
            float jitter   = clusterSpreadPixels > 0 ? (Random.value - 0.5f) * 2f * clusterSpreadNorm : 0f;
            float pos      = Mathf.Clamp01(spawnPositionUV + jitter);
            float depth    = Mathf.Clamp01(Random.Range(depthRange.x, depthRange.y) * intensityMultiplier);
            int   width    = Mathf.Clamp(Random.Range(widthRange.x, widthRange.y + 1), 1, 64);

            absorptionController.StampLine(pos, depth, width);
        }

        if (debugLog)
            Debug.Log($"[LyaAbsorptionTrigger] '{name}' burst: {count} lines, intensity ×{intensityMultiplier:F2}");
    }
}

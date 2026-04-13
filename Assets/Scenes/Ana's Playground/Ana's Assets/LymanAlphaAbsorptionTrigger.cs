using UnityEngine;

/// <summary>
/// Trigger volume that stamps a Lyman-alpha absorption line when the player enters.
///
/// Place along the journey at hydrogen cloud positions.
/// Each trigger fires once — re-entering does NOT add a duplicate line.
///
/// SETUP:
/// 1. Add a Collider to this GameObject and set isTrigger = true.
/// 2. Assign LymanAlphaAbsorptionController reference.
/// 3. Set Intensity (0 = faint, 1 = fully dark).
/// </summary>
[RequireComponent(typeof(Collider))]
public class LymanAlphaAbsorptionTrigger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] LymanAlphaAbsorptionController absorptionController;

    [Header("Absorption")]
    [Tooltip("How dark this absorption line is. 0 = barely visible, 1 = fully opaque.")]
    [Range(0f, 1f)]
    [SerializeField] float intensity = 0.8f;

    [Header("Filter")]
    [SerializeField] string targetTag = "Player";

    [Header("Debug")]
    [SerializeField] bool debugLog = true;

    bool _fired = false;

    void Awake()
    {
        var col = GetComponent<Collider>();
        if (!col.isTrigger) col.isTrigger = true;

        if (absorptionController == null)
            absorptionController = FindObjectOfType<LymanAlphaAbsorptionController>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (_fired) return;
        if (!other.CompareTag(targetTag)) return;
        if (absorptionController == null) return;

        _fired = true;
        absorptionController.AddAbsorptionLine(intensity);

        if (debugLog)
            Debug.Log($"[LyaAbsorptionTrigger] '{name}' fired — intensity {intensity:F2}");
    }
}

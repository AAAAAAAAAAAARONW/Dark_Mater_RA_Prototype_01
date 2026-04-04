using UnityEngine;

/// <summary>
/// Test version of LayerTriggerVolume.
/// Changes from original:
///   - References LayerStateManagerTest instead of LayerStateManager
/// Everything else is identical to the original.
/// Attach one instance to each trigger volume GameObject in the scene.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LayerTriggerVolumeTest : MonoBehaviour
{
    [Header("State")]
    [SerializeField] LayerStateManagerTest stateManager;
    [Tooltip("The layerId this trigger volume represents. Must match an entry in the LayerCatalogTest.")]
    [SerializeField] string layerId = "Macro";

    [Header("Priority (overlap resolution — reserved for future use)")]
    [SerializeField] int priority = 0;

    [Header("Filter")]
    [SerializeField] bool requireTag = true;
    [SerializeField] string targetTag = "Player";

    Collider _collider;

    void Reset()
    {
        _collider = GetComponent<Collider>();
        if (_collider != null) _collider.isTrigger = true;
    }

    void Awake()
    {
        _collider = GetComponent<Collider>();
        if (_collider != null && !_collider.isTrigger)
            _collider.isTrigger = true;

        if (stateManager == null)
            stateManager = FindObjectOfType<LayerStateManagerTest>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsValidSource(other.gameObject)) return;
        stateManager.RequestEnter(layerId, this, priority);
    }

    // OnTriggerExit intentionally omitted.
    // Layer only changes when entering a new trigger, not when leaving one.

    bool IsValidSource(GameObject go)
    {
        if (go == null) return false;
        if (requireTag && !go.CompareTag(targetTag)) return false;
        return true;
    }
}
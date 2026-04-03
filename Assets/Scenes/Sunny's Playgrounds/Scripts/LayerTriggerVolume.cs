using UnityEngine;

/// <summary>
/// Script for trigger that detects player in out to request layer changes.
/// Does NOT apply camera/speed/visual transition logic anymore in trigger.
/// </summary>

[RequireComponent(typeof(Collider))]
public class LayerTriggerVolume : MonoBehaviour
{
    // Overall state of this triggerbox 
    [Header("State")]
    [SerializeField] LayerStateManager stateManager; // reference to manager
    [SerializeField] string layerId = "Macro"; // layer type of this trigger

    [Header("Priority (overlap resolution)")]
    [SerializeField] int priority = 0;

    [Header("Filter")]
    [SerializeField] bool requireTag = true; // must match tag if on
    [SerializeField] string targetTag = "Player";

    [Header("Detection")]
    [SerializeField] float pollingInterval = 0.1f;

    Collider _collider; // ref to trigger collider
    DarkMatterPlayerControllerTest _playerController; // ref to player for polling
    bool _isInside; // checks if inside
    bool _hasInit;
    float _nextPollTime;

    void Reset()
    {
        _collider = GetComponent<Collider>(); // grab collider
        if (_collider != null) _collider.isTrigger = true;
    }

    void Awake()
    {
        _collider = GetComponent<Collider>();
        if (_collider != null && !_collider.isTrigger) // if exist, toggle trigger
            _collider.isTrigger = true;
        if (stateManager == null) // find layer manager if not set
            stateManager = FindObjectOfType<LayerStateManager>();
        if (_playerController == null) // find playercontroller for polling
            _playerController = FindObjectOfType<DarkMatterPlayerControllerTest>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsValidSource(other.gameObject)) return;
            stateManager.RequestEnter(layerId, this, priority);
    }
    
    void OnTriggerExit(Collider other)
    {
        if (!IsValidSource(other.gameObject)) return;
            stateManager.RequestExit(this);
    }

    bool IsValidSource(GameObject go)
    {
        if (go == null) return false;
        if (requireTag && !go.CompareTag(targetTag)) return false;
        return true;
    }
}
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

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

    [Header("Debug")]
    [Tooltip("If enabled, always draw this trigger gizmo + layer name text in editor.")]
    [SerializeField] bool showDebugGizmo = true;
    [SerializeField] Color gizmoColor = new Color(0.3f, 0.9f, 1f, 0.35f);

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

    void OnDrawGizmos()
    {
        if (!showDebugGizmo) return;

        var c = GetComponent<Collider>();
        if (c == null) return;

        Gizmos.color = gizmoColor;
        DrawColliderGizmo(c);

#if UNITY_EDITOR
        Handles.color = Color.white;
        Handles.Label(GetLabelPosition(c), string.IsNullOrEmpty(layerId) ? "(no layerId)" : layerId);
#endif
    }

    static void DrawColliderGizmo(Collider c)
    {
        if (c is BoxCollider box)
        {
            Matrix4x4 old = Gizmos.matrix;
            Gizmos.matrix = box.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = old;
            return;
        }

        if (c is SphereCollider sphere)
        {
            Matrix4x4 old = Gizmos.matrix;
            Gizmos.matrix = sphere.transform.localToWorldMatrix;
            Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            Gizmos.matrix = old;
            return;
        }

        // Fallback for capsule/mesh/other collider types.
        Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
    }

    static Vector3 GetLabelPosition(Collider c)
    {
        Bounds b = c.bounds;
        return new Vector3(b.center.x, b.max.y + 0.35f, b.center.z);
    }
}
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// A one-way gate plane the player flies through.
///
/// Replaces LayerTriggerVolumeTest. The model this encodes, made explicit:
/// gates are thin planes laid out in journey order, the player crosses each one
/// exactly once, and there is no "inside a zone" state. The active layer is simply
/// whatever the last gate said. That is why there is no OnTriggerExit and no
/// priority field — the old `priority` was marked "reserved for future use" but
/// overlap resolution is not a missing feature, it is a case that cannot occur.
///
/// Changes from the original: the state reference is null-checked before use
/// (the old OnTriggerEnter would throw if the auto-find failed), and the gizmo
/// reports the gate's world thickness so tunnelling headroom is visible in-editor.
/// </summary>
[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
[HierarchyBadge_NEW("GATE", "#8C5CEB")]
public class LayerGate_NEW : MonoBehaviour
{
    [Header("State")]
    [SerializeField] LayerState_NEW state;

    [Tooltip("Must match a LayerCatalog_NEW entry.")]
    [SerializeField] string layerId = "Macro";

    [Header("Filter")]
    [SerializeField] bool requireTag = true;
    [SerializeField] string targetTag = "Player";

    [Header("Gizmo")]
    [SerializeField] bool drawGizmo = true;
    [SerializeField] Color gizmoColor = new Color(0.3f, 0.9f, 1f, 0.35f);

    Collider _collider;
    bool _warnedMissingState;

    public string LayerId => layerId;

    void Reset()
    {
        Collider c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
    }

    void Awake()
    {
        _collider = GetComponent<Collider>();
        if (_collider != null && !_collider.isTrigger)
        {
            Debug.LogWarning($"[LayerGate_NEW] '{name}' collider was not a trigger. Forcing isTrigger.", this);
            _collider.isTrigger = true;
        }

        if (state == null) state = FindObjectOfType<LayerState_NEW>();
        if (state == null)
            Debug.LogError($"[LayerGate_NEW] '{name}' has no LayerState_NEW. This gate will do nothing.", this);
    }

    void OnTriggerEnter(Collider other)
    {
        if (state == null)
        {
            if (!_warnedMissingState)
            {
                _warnedMissingState = true;
                Debug.LogError($"[LayerGate_NEW] '{name}' fired with no LayerState_NEW assigned.", this);
            }
            return;
        }

        if (other == null) return;
        if (requireTag && !other.CompareTag(targetTag)) return;

        state.RequestEnter(layerId, this);
    }

    // OnTriggerExit is intentionally absent. See the class summary.

    void OnDrawGizmos()
    {
        // One switch for every debug visual — see DebugView_NEW.
        if (!drawGizmo || !DebugView_NEW.Gizmos) return;

        Collider c = GetComponent<Collider>();
        if (c == null) return;

        Gizmos.color = gizmoColor;
        DrawColliderWire(c);

#if UNITY_EDITOR
        Bounds b = c.bounds;
        Handles.color = Color.white;
        string label = string.IsNullOrEmpty(layerId) ? "(no layerId)" : layerId;
        Handles.Label(new Vector3(b.center.x, b.max.y + 0.35f, b.center.z),
                      $"{label}   thickness {b.size.z:F2}");
#endif
    }

    static void DrawColliderWire(Collider c)
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

        Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
    }
}

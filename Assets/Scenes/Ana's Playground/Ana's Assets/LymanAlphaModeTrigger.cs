using UnityEngine;

/// <summary>
/// DEPRECATED. The mode switch (TriggerBox vs FakeForest) was removed when
/// background forest and trigger-driven events became simultaneous. This shell
/// remains so existing scene references don't break compilation. Safe to
/// remove from scenes when you get a chance.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LymanAlphaModeTrigger : MonoBehaviour
{
    [Header("Deprecated")]
    [Tooltip("This trigger no longer does anything. Replace with LymanAlphaAbsorptionTrigger.")]
    [SerializeField] bool _ = false; // visual marker only

    void Awake()
    {
        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger) col.isTrigger = true;
    }
}

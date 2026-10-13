using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// "When the journey reaches this layer, do this" — for the small things that belong to a
/// layer and to nothing else, wired in the Inspector instead of a component each.
///
/// The first one: entering the Milky Way hides the redshift demonstration's marks
/// (RedshiftMarks_NEW.Hide) and any highlight still on, because they belong to the second
/// cosmic web's explanation and have nothing to point at any more.
///
/// Rides CameraDirector_NEW's anchors like every responder, so it never decides WHEN a layer
/// has arrived. CoverReached is the default: the old world is hidden, the swap happens there.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("LAYER EVENTS", "#4A94F2")]
public class LayerEvents_NEW : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        public string layerId = "MilkyWay";
        public TransitionAnchor_NEW anchor = TransitionAnchor_NEW.CoverReached;
        public UnityEvent onReached = new UnityEvent();
    }

    [SerializeField] CameraDirector_NEW director;
    [SerializeField] Entry[] entries = new Entry[0];

    void Awake()
    {
        if (director == null) director = FindObjectOfType<CameraDirector_NEW>();
    }

    void OnEnable()
    {
        if (director != null) director.OnAnchor += HandleAnchor;
    }

    void OnDisable()
    {
        if (director != null) director.OnAnchor -= HandleAnchor;
    }

    void HandleAnchor(AnchorEvent_NEW e)
    {
        if (e.profile == null) return;

        foreach (Entry entry in entries)
            if (entry != null && entry.anchor == e.anchor &&
                string.Equals(entry.layerId, e.profile.layerId, StringComparison.OrdinalIgnoreCase))
                entry.onReached.Invoke();
    }
}

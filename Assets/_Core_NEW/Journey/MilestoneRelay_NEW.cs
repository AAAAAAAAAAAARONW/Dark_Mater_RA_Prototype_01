using System;
using UnityEngine;

/// <summary>
/// Watches the journey distance and fires milestones from a MilestoneSet_NEW asset.
///
/// It reads UniverseJourneyTracker.RemainingDistanceLy, which is public, and does not
/// touch the tracker in any other way. The tracker keeps computing distance exactly as
/// it does today; this just replaces where the milestone list is authored.
///
/// The tracker's own milestone array can stay populated — the two do not interfere,
/// they simply produce two independent streams. Once the new HUD is wired up, empty the
/// tracker's array so cards do not fire twice.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("MILESTONE", "#E09E38")]
public class MilestoneRelay_NEW : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] UniverseJourneyTracker tracker;
    [SerializeField] MilestoneSet_NEW milestones;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    bool[] _fired;

    /// <summary>Fired once per entry, in the order they are crossed.</summary>
    public event Action<MilestoneSet_NEW.Entry> OnMilestoneReached;

    void Awake()
    {
        if (tracker == null) tracker = FindObjectOfType<UniverseJourneyTracker>();

        if (tracker == null)
            Debug.LogError("[MilestoneRelay_NEW] No UniverseJourneyTracker found.", this);
        if (milestones == null)
            Debug.LogError("[MilestoneRelay_NEW] No MilestoneSet_NEW assigned.", this);

        _fired = new bool[milestones != null ? milestones.Count : 0];
    }

    void Update()
    {
        if (tracker == null || milestones == null || _fired == null) return;

        double remaining = tracker.RemainingDistanceLy;

        for (int i = 0; i < _fired.Length; i++)
        {
            if (_fired[i]) continue;

            MilestoneSet_NEW.Entry e = milestones.Get(i);
            if (e == null) { _fired[i] = true; continue; }

            if (remaining > e.remainingDistanceThresholdLy) continue;

            _fired[i] = true;
            if (debugLog)
                Debug.Log($"[MilestoneRelay_NEW] '{e.label}' at {remaining:F0} ly remaining.", this);

            OnMilestoneReached?.Invoke(e);
        }
    }

    /// <summary>Re-arm every milestone. For a fresh run or for testing.</summary>
    public void ResetAll()
    {
        if (_fired == null) return;
        for (int i = 0; i < _fired.Length; i++) _fired[i] = false;
    }

#if UNITY_EDITOR
    [ContextMenu("Debug: Fire the first un-fired milestone")]
    void DebugFireNext()
    {
        if (_fired == null) return;
        for (int i = 0; i < _fired.Length; i++)
        {
            if (_fired[i]) continue;
            _fired[i] = true;
            OnMilestoneReached?.Invoke(milestones.Get(i));
            return;
        }
    }
#endif
}

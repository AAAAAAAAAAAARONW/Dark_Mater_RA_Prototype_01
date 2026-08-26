using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The journey's milestone cards, as a project asset.
///
/// This exists because the old arrangement had three copies of the list that had
/// drifted apart: UniverseJourneyTracker's code defaults held 25 entries, the scene's
/// serialized override held 7, and MilestoneHUD carried a parallel array of 18 sprites
/// matched by index. Serialized scene values always win over code defaults in Unity, so
/// the 25 carefully written entries had never once run.
///
/// One list, one asset, and each entry carries its own sprite — no index matching, so
/// reordering or inserting an entry cannot desync the art. Editing it does not require
/// opening a scene, it diffs in version control, and resetting it cannot wipe unrelated
/// scene references the way resetting the tracker component could.
/// </summary>
[CreateAssetMenu(menuName = "Journey NEW/Milestone Set", fileName = "MilestoneSet_New")]
public class MilestoneSet_NEW : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("Card title.")]
        public string label;

        [Tooltip("One sentence shown on the card body.")]
        [TextArea(2, 3)]
        public string description;

        [Tooltip("The finished card art for this milestone. Lives here, not in a parallel array.")]
        public Sprite card;

        [Tooltip("Accent colour, for HUDs that tint rather than use full card art.")]
        public Color accent = new Color(0.35f, 0.15f, 0.6f, 1f);

        [Tooltip("Fires when remaining distance drops below this, in light years. " +
                 "A threshold above the journey's start distance fires immediately on play.")]
        public double remainingDistanceThresholdLy;

        [Tooltip("How long ago the event actually happened, in years. Display only.")]
        public double yearsAgo;
    }

    [Tooltip("Authored in descending threshold order — furthest from Earth first. " +
             "OnValidate warns if the order is wrong.")]
    [SerializeField] List<Entry> entries = new List<Entry>();

    public IReadOnlyList<Entry> Entries => entries;
    public int Count => entries != null ? entries.Count : 0;

    public Entry Get(int index) =>
        entries != null && index >= 0 && index < entries.Count ? entries[index] : null;

    void OnValidate()
    {
        if (entries == null) return;

        for (int i = 1; i < entries.Count; i++)
        {
            Entry prev = entries[i - 1];
            Entry cur = entries[i];
            if (prev == null || cur == null) continue;

            if (cur.remainingDistanceThresholdLy > prev.remainingDistanceThresholdLy)
            {
                Debug.LogWarning(
                    $"[MilestoneSet_NEW] Entry {i} ('{cur.label}') has a larger threshold than " +
                    $"entry {i - 1} ('{prev.label}'). Sort descending, furthest first.", this);
                break;
            }
        }

        for (int i = 0; i < entries.Count; i++)
            if (entries[i] != null && entries[i].card == null)
                Debug.LogWarning($"[MilestoneSet_NEW] Entry {i} ('{entries[i].label}') has no card sprite.", this);
    }
}

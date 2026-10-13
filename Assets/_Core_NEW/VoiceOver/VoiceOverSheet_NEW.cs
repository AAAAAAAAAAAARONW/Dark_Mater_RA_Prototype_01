using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One phase's worth of the voice-over table: the beats of that phase, their timing
/// settings, their lines and recordings.
///
/// A sheet per phase rather than one asset for everything, because the voice-over is
/// going to be tuned for months by more than one person, and two people adjusting two
/// phases in one serialized asset is a merge conflict every time. The master
/// VoiceOverTable_NEW only lists the sheets.
///
/// Edited through Tools > Journey NEW > Voice Over > Timeline rather than in the
/// Inspector — the Inspector works, but it cannot play a line or show where a cue
/// falls in it.
/// </summary>
[CreateAssetMenu(menuName = "Journey NEW/Voice Over Sheet", fileName = "VO_Sheet")]
public class VoiceOverSheet_NEW : ScriptableObject
{
    [Tooltip("Shown in the timeline window, e.g. Phase 3.")]
    public string title = "";

    public List<VoiceOverBeat_NEW> beats = new List<VoiceOverBeat_NEW>();

    public VoiceOverBeat_NEW Find(string beatId)
    {
        for (int i = 0; i < beats.Count; i++)
            if (beats[i] != null && string.Equals(beats[i].beatId, beatId, System.StringComparison.OrdinalIgnoreCase))
                return beats[i];

        return null;
    }
}

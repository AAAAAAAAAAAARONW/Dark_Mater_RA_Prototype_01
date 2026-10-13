using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The tutorial's voice-over, in one place: which language and which version of the
/// script is playing, the sheets that hold the lines, and the script they come from.
///
/// THE SCRIPT IS THE SOURCE OF THE WORDS. Text here is imported from the markdown
/// script and is not edited in Unity — two copies of a line that can each be edited
/// drift apart within a week. Unity owns everything the script does not: recordings,
/// timing, cues, and how each beat waits for its lines.
///
/// LANGUAGE AND VARIANT. Every line can have a take per language and per variant (the
/// A and B versions of the dark matter toggle). Switching either here switches the
/// whole piece; a line with no take for the current pair is silent, and the timeline
/// window lists it.
/// </summary>
[CreateAssetMenu(menuName = "Journey NEW/Voice Over Table", fileName = "VO_Tutorial")]
public class VoiceOverTable_NEW : ScriptableObject
{
    [Tooltip("The language being played, e.g. en.")]
    public string language = "en";

    [Tooltip("The version of the script being played. Empty plays only takes every version " +
             "shares; A or B also plays that version's own takes.")]
    public string variant = "A";

    [Tooltip("In storyboard order.")]
    public List<VoiceOverSheet_NEW> sheets = new List<VoiceOverSheet_NEW>();

    [Tooltip("The markdown script the words are imported from.")]
    public TextAsset script;

    [Tooltip("Where recordings are looked for. Files directly in it are the first language " +
             "in this list; a subfolder named after a language code holds that language.")]
    public string recordingsFolder = "Assets/VoiceOver/Tutorial";

    [Tooltip("Languages this table has, first one = the files at the top of the recordings folder.")]
    public List<string> languages = new List<string> { "en" };

    [Tooltip("Script versions this table has. Empty is always available.")]
    public List<string> variants = new List<string> { "A", "B" };

    /// <summary>The beat entry for an id, from whichever sheet holds it, or null.</summary>
    public VoiceOverBeat_NEW Find(string beatId)
    {
        if (string.IsNullOrEmpty(beatId)) return null;

        for (int i = 0; i < sheets.Count; i++)
        {
            if (sheets[i] == null) continue;

            VoiceOverBeat_NEW b = sheets[i].Find(beatId);
            if (b != null) return b;
        }

        return null;
    }

    /// <summary>The take of a line that is playing with the current language and variant.</summary>
    public VoiceOverTake_NEW Resolve(VoiceOverLine_NEW line)
    {
        return line != null ? line.Resolve(language, variant) : null;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

// The rows of the voice-over table. Plain serializable classes, held by
// VoiceOverSheet_NEW assets and read by TutorialVoiceOver_NEW at runtime.
//
// The shape, from the outside in:
//
//   VoiceOverTable_NEW    the master: language, variant, the sheets, the script file
//   VoiceOverSheet_NEW    one per phase, so two people tuning two phases do not
//                         collide in one asset in git
//   VoiceOverBeat_NEW     one beat: its timing settings and its lines
//   VoiceOverLine_NEW     one thing said: narration, a nudge, or a line after the action
//   VoiceOverTake_NEW     one recording of that line, for one language and variant —
//                         with the script text it is supposed to say
//   VoiceOverCue_NEW      a moment inside the line where the beat does something
//
// The table holds timing and content. What a cue actually DOES lives on the beat in
// the scene (TutorialBeat_NEW cue events), because an asset cannot point at objects
// in a scene. The two are joined by the cue's key.

/// <summary>What a line is for, which decides when it is said.</summary>
public enum VoiceOverRole_NEW
{
    /// <summary>The story. Said when the beat opens (after its delay), in list order.</summary>
    Narration,

    /// <summary>
    /// The spoken instruction. Said on a gated beat once the hint is up and the player
    /// has not done it yet, and again on the beat's repeat timer. Cut off the moment
    /// the gate is met.
    /// </summary>
    Nudge,

    /// <summary>
    /// Said once the gate is met, before the beat lets go — the "after you press B"
    /// line of the dark matter toggle. The beat waits for it.
    /// </summary>
    AfterAction
}

/// <summary>How far along a recording is. Shown in the timeline window; drives nothing.</summary>
public enum VoiceOverStatus_NEW
{
    /// <summary>No recording yet. Timed from the estimate, said as silence.</summary>
    Estimate,

    /// <summary>A placeholder or scratch recording.</summary>
    Scratch,

    /// <summary>The final recording.</summary>
    Final
}

/// <summary>How a cue's time is measured, so it survives a re-record as well as it can.</summary>
public enum VoiceOverCueAnchor_NEW
{
    /// <summary>Seconds from the start of the line.</summary>
    FromStart,

    /// <summary>Seconds before the end of the line. For "just before it finishes".</summary>
    FromEnd,

    /// <summary>A fraction of the line, 0 to 1. Follows a line read faster or slower.</summary>
    Proportion
}

/// <summary>Duration beats only: what decides how long the beat runs.</summary>
public enum VoiceOverDurationMode_NEW
{
    /// <summary>The beat's own duration field, as authored in the scene.</summary>
    Fixed,

    /// <summary>The beat's narration plus tailSeconds. Re-record the line and the beat follows.</summary>
    FollowNarration
}

/// <summary>When the hint line (and the stick diagram and the A prompt) comes up.</summary>
public enum VoiceOverHintTiming_NEW
{
    /// <summary>When the beat opens, as before the voice-over table existed.</summary>
    OnEnter,

    /// <summary>When the beat's narration has finished.</summary>
    AfterNarration,

    /// <summary>At one of the narration's cues, named by hintCue.</summary>
    AtCue
}

[Serializable]
public class VoiceOverTake_NEW
{
    [Tooltip("Language code, e.g. en or zh.")]
    public string language = "en";

    [Tooltip("Empty for a take every variant shares. A or B for a take only that version of " +
             "the script uses.")]
    public string variant = "";

    [Tooltip("What the line says. Imported from the script — edit the script, not this.")]
    [TextArea(2, 6)]
    public string text = "";

    public AudioClip clip;

    [Tooltip("Seconds, from the script. Used to time the line while there is no recording.")]
    [Min(0f)]
    public float estimatedSeconds;

    public VoiceOverStatus_NEW status = VoiceOverStatus_NEW.Estimate;

    [Tooltip("The text the recording was checked against. When the script changes and this " +
             "no longer matches, the line is listed as needing a re-record. Empty means " +
             "nobody has checked yet.")]
    [TextArea(1, 4)]
    public string recordedText = "";

    /// <summary>Seconds this take lasts: the recording, else the estimate, else a read at 2.3 words a second.</summary>
    public float Length
    {
        get
        {
            if (clip != null) return clip.length;
            if (estimatedSeconds > 0f) return estimatedSeconds;
            return EstimateFromWords(text);
        }
    }

    /// <summary>The script changed after this recording was checked against it.</summary>
    public bool NeedsReRecord
    {
        get { return clip != null && !string.IsNullOrEmpty(recordedText) && Normalise(recordedText) != Normalise(text); }
    }

    /// <summary>There is a recording, and nobody has said which text it was made from.</summary>
    public bool Unverified
    {
        get { return clip != null && string.IsNullOrEmpty(recordedText); }
    }

    /// <summary>140 words a minute, the script's own pacing rule.</summary>
    public static float EstimateFromWords(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0f;

        int words = text.Split(new[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
        return words / 2.3f;
    }

    static string Normalise(string s)
    {
        return string.Join(" ", (s ?? "").Split(new[] { ' ', '\n', '\r', '\t' },
                                               StringSplitOptions.RemoveEmptyEntries));
    }
}

[Serializable]
public class VoiceOverCue_NEW
{
    [Tooltip("Joins this moment to the beat's cue event with the same key, in the scene. " +
             "Also what a beat's hintCue names.")]
    public string key = "cue";

    public VoiceOverCueAnchor_NEW anchor = VoiceOverCueAnchor_NEW.FromStart;

    [Tooltip("Seconds (FromStart, FromEnd) or a fraction 0–1 (Proportion).")]
    public float time;

    [Tooltip("How long the line was when this cue was placed. When the recording changes " +
             "length, the cue is flagged for review instead of drifting silently.")]
    public float authoredLength;

    /// <summary>Seconds into a line of this length that the cue falls on.</summary>
    public float Resolve(float length)
    {
        switch (anchor)
        {
            case VoiceOverCueAnchor_NEW.FromEnd: return Mathf.Clamp(length - time, 0f, length);
            case VoiceOverCueAnchor_NEW.Proportion: return Mathf.Clamp01(time) * length;
            default: return Mathf.Clamp(time, 0f, length);
        }
    }

    /// <summary>The line is a different length from when this cue was placed.</summary>
    public bool NeedsReview(float length)
    {
        if (anchor == VoiceOverCueAnchor_NEW.Proportion) return false;
        return authoredLength > 0f && Mathf.Abs(authoredLength - length) > 0.05f;
    }
}

[Serializable]
public class VoiceOverLine_NEW
{
    [Tooltip("A1, A1_2, N_A1, E2_After… Unique within the table. Recordings are matched " +
             "to lines by this, as a file name prefix.")]
    public string id = "";

    public VoiceOverRole_NEW role = VoiceOverRole_NEW.Narration;

    public List<VoiceOverTake_NEW> takes = new List<VoiceOverTake_NEW>();

    public List<VoiceOverCue_NEW> cues = new List<VoiceOverCue_NEW>();

    /// <summary>
    /// The take for this language and variant: an exact match, else the take every
    /// variant shares, else none. No fallback to another language — a missing line in
    /// the Chinese build should be silent, not English.
    /// </summary>
    public VoiceOverTake_NEW Resolve(string language, string variant)
    {
        VoiceOverTake_NEW shared = null;

        for (int i = 0; i < takes.Count; i++)
        {
            VoiceOverTake_NEW t = takes[i];
            if (t == null || !string.Equals(t.language, language, StringComparison.OrdinalIgnoreCase)) continue;

            if (string.Equals(t.variant ?? "", variant ?? "", StringComparison.OrdinalIgnoreCase)) return t;
            if (string.IsNullOrEmpty(t.variant)) shared = t;
        }

        return shared;
    }

    public VoiceOverTake_NEW FindTake(string language, string variant)
    {
        for (int i = 0; i < takes.Count; i++)
        {
            VoiceOverTake_NEW t = takes[i];
            if (t == null) continue;
            if (string.Equals(t.language, language, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(t.variant ?? "", variant ?? "", StringComparison.OrdinalIgnoreCase))
                return t;
        }

        return null;
    }
}

[Serializable]
public class VoiceOverBeatSettings_NEW
{
    [Tooltip("Seconds after the beat opens before its first line. The script's \"Cue: 2 s in\".")]
    [Min(0f)]
    public float narrationDelay = 0f;

    [Header("Duration beats")]
    public VoiceOverDurationMode_NEW durationMode = VoiceOverDurationMode_NEW.Fixed;

    [Tooltip("FollowNarration only: seconds the beat holds after its narration ends.")]
    [Min(0f)]
    public float tailSeconds = 0.5f;

    [Header("Gated beats")]
    public VoiceOverHintTiming_NEW hintTiming = VoiceOverHintTiming_NEW.AfterNarration;

    [Tooltip("AtCue only: the key of a cue on this beat's narration.")]
    public string hintCue = "";

    [Tooltip("Seconds after the hint comes up before the first nudge, if the player has " +
             "not done it yet.")]
    [Min(0f)]
    public float nudgeDelay = 0f;

    [Tooltip("Seconds of silence after a nudge before it is said again.")]
    [Min(1f)]
    public float nudgeRepeatSeconds = 12f;

    [Tooltip("How many times the nudge is repeated after the first. 0 says it once.")]
    [Min(0)]
    public int nudgeRepeats = 2;

    [Header("Every beat")]
    [Tooltip("The beat does not end while its narration is still being said. A gated beat " +
             "whose player acts early remembers it and moves on when the line is done.")]
    public bool waitForNarration = true;
}

[Serializable]
public class VoiceOverBeat_NEW
{
    [Tooltip("The scene beat's id. \"Ending\" is the pull-back after the last beat.")]
    public string beatId = "";

    public VoiceOverBeatSettings_NEW settings = new VoiceOverBeatSettings_NEW();

    public List<VoiceOverLine_NEW> lines = new List<VoiceOverLine_NEW>();

    /// <summary>The pseudo-beat for the ending, said when the director completes.</summary>
    public const string EndingId = "Ending";

    /// <summary>
    /// The pseudo-beat for the pause, in any beat. Its Nudge lines are said while the
    /// piece is paused, nudgeDelay after the pause, on their own AudioSource so the line
    /// that was paused waits and carries on.
    /// </summary>
    public const string PauseId = "Pause";

    public VoiceOverLine_NEW FindLine(string id)
    {
        for (int i = 0; i < lines.Count; i++)
            if (lines[i] != null && string.Equals(lines[i].id, id, StringComparison.OrdinalIgnoreCase))
                return lines[i];

        return null;
    }
}

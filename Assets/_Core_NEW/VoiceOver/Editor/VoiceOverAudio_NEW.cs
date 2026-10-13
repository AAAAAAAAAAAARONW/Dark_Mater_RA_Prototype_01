using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Play a clip in the editor and read its waveform, for the timeline window.
///
/// Both go through UnityEditor.AudioUtil, which is internal — it is what the AudioClip
/// Inspector uses, and the only way to hear a clip outside Play mode. Reached by
/// reflection and matched by name and parameter count, which is safe for as long as the
/// project stays on 2019.4 (the decision is that it will). If a method is not found the
/// feature it serves switches off quietly: no preview, or a plain bar instead of a
/// waveform. Nothing else in the window depends on it.
/// </summary>
public static class VoiceOverAudio_NEW
{
    static readonly Type AudioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");

    static MethodInfo Find(string name, int parameters)
    {
        if (AudioUtil == null) return null;

        foreach (MethodInfo m in AudioUtil.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            if (m.Name == name && m.GetParameters().Length == parameters) return m;

        return null;
    }

    static readonly MethodInfo PlayClipMethod = Find("PlayClip", 3) ?? Find("PlayClip", 1);
    static readonly MethodInfo StopAllMethod = Find("StopAllClips", 0);
    static readonly MethodInfo IsPlayingMethod = Find("IsClipPlaying", 1);
    static readonly MethodInfo PositionMethod = Find("GetClipPosition", 1);
    static readonly MethodInfo MinMaxMethod = Find("GetMinMaxData", 1);

    public static bool CanPreview { get { return PlayClipMethod != null && StopAllMethod != null; } }

    static AudioClip _playing;

    public static AudioClip Playing
    {
        get
        {
            if (_playing != null && !IsPlaying(_playing)) _playing = null;
            return _playing;
        }
    }

    /// <summary>Play from a point in the clip. Stops anything else first.</summary>
    public static void Play(AudioClip clip, float fromSeconds)
    {
        if (clip == null || PlayClipMethod == null) return;

        Stop();

        int startSample = Mathf.Clamp(Mathf.RoundToInt(fromSeconds * clip.frequency), 0, Mathf.Max(0, clip.samples - 1));

        object[] args = PlayClipMethod.GetParameters().Length == 3
            ? new object[] { clip, startSample, false }
            : new object[] { clip };

        PlayClipMethod.Invoke(null, args);
        _playing = clip;
    }

    public static void Stop()
    {
        if (StopAllMethod != null) StopAllMethod.Invoke(null, null);
        _playing = null;
    }

    public static bool IsPlaying(AudioClip clip)
    {
        if (clip == null || IsPlayingMethod == null) return false;
        return (bool)IsPlayingMethod.Invoke(null, new object[] { clip });
    }

    /// <summary>Seconds into the clip being previewed, or -1.</summary>
    public static float Position(AudioClip clip)
    {
        if (clip == null || PositionMethod == null || !IsPlaying(clip)) return -1f;

        object result = PositionMethod.Invoke(null, new object[] { clip });
        return result is float ? (float)result : -1f;
    }

    // -- Waveform ---------------------------------------------------------------------

    struct Cached
    {
        public float length;
        public float[] peaks;
    }

    static readonly Dictionary<int, Cached> Waveforms = new Dictionary<int, Cached>();

    /// <summary>
    /// Peak amplitude, 0–1, in `columns` buckets across the clip — or null if it cannot be
    /// read. Cached per clip, and thrown away when the clip's length changes (a re-take
    /// overwritten on disk keeps its instance).
    /// </summary>
    public static float[] Peaks(AudioClip clip, int columns)
    {
        if (clip == null || columns <= 0) return null;

        Cached c;
        if (Waveforms.TryGetValue(clip.GetInstanceID(), out c) && Mathf.Approximately(c.length, clip.length) &&
            c.peaks != null && c.peaks.Length == columns)
            return c.peaks;

        float[] peaks = ReadPeaks(clip, columns);
        Waveforms[clip.GetInstanceID()] = new Cached { length = clip.length, peaks = peaks };
        return peaks;
    }

    static float[] ReadPeaks(AudioClip clip, int columns)
    {
        // The importer's min/max data works whatever the clip's load type is, which
        // AudioClip.GetData does not.
        if (MinMaxMethod != null)
        {
            AudioImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip)) as AudioImporter;

            if (importer != null)
            {
                float[] minMax = MinMaxMethod.Invoke(null, new object[] { importer }) as float[];
                if (minMax != null && minMax.Length >= 2) return FromMinMax(minMax, Mathf.Max(1, clip.channels), columns);
            }
        }

        float[] samples = new float[clip.samples * clip.channels];
        if (!clip.GetData(samples, 0)) return null;

        float[] peaks = new float[columns];
        for (int i = 0; i < samples.Length; i++)
        {
            int col = Mathf.Min(columns - 1, (int)((long)i * columns / samples.Length));
            peaks[col] = Mathf.Max(peaks[col], Mathf.Abs(samples[i]));
        }

        return peaks;
    }

    /// <summary>The importer's data is (min, max) pairs, interleaved by channel.</summary>
    static float[] FromMinMax(float[] minMax, int channels, int columns)
    {
        int frames = minMax.Length / (2 * channels);
        float[] peaks = new float[columns];
        if (frames == 0) return peaks;

        for (int f = 0; f < frames; f++)
        {
            int col = Mathf.Min(columns - 1, f * columns / frames);

            for (int ch = 0; ch < channels; ch++)
            {
                int i = (f * channels + ch) * 2;
                float peak = Mathf.Max(Mathf.Abs(minMax[i]), Mathf.Abs(minMax[i + 1]));
                peaks[col] = Mathf.Max(peaks[col], peak);
            }
        }

        return peaks;
    }
}

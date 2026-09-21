using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Controller rumble, for a project that has no rumble API.
///
/// Unity 2019.4 on the legacy Input Manager cannot vibrate a pad at all — TutorialBeat_NEW
/// has carried an onRumble seam since Phase 0 for exactly this reason, waiting "for the day
/// a haptics path exists". This is that path. It talks to XInput directly, which is the
/// API an Xbox controller on Windows actually speaks, so it needs no package and changes
/// nothing about how input is read.
///
/// WHAT IT IS FOR. The absorption is the moment the whole tutorial is built around: a
/// single atom takes a colour out of the player's light. The screen can show that, but
/// the player is holding the light in their hands — the pad is the one part of the piece
/// that is physically theirs — and a jolt through it says "that happened to you" in a way
/// no amount of HUD can. So every absorption rumbles, the same way every time.
///
/// WHAT IT CANNOT DO. XInput is Xbox-family only. A DualSense on a desk will not rumble
/// through this, and there is nothing to fix: it simply is not an XInput device. The
/// exhibition pad is the Xbox one, which is the only one that has to.
///
/// Windows only, and quiet about it: on any other platform, or if the XInput DLL is
/// missing, Pulse does nothing and says so once. A haptic that fails must not take the
/// piece down with it.
///
/// ALWAYS STOPS. A motor left running is the worst failure a haptic can have — a pad
/// buzzing on a plinth with nobody holding it — so the motors are cut when the envelope
/// ends, on pause, on disable, on focus loss and on quit. Every exit path, not just the
/// one that was drawn.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("RUMBLE", "#C0607A")]
public class TutorialRumble_NEW : MonoBehaviour
{
    [Header("Default pulse")]
    [Tooltip("Left motor, 0 to 1. The heavy, low-frequency one — this is the thud.")]
    [Range(0f, 1f)]
    [SerializeField] float lowFrequency = 0.85f;

    [Tooltip("Right motor, 0 to 1. The light, high-frequency one — this is the crack on " +
             "top of the thud. Together they read as an impact rather than a buzz.")]
    [Range(0f, 1f)]
    [SerializeField] float highFrequency = 0.55f;

    [Tooltip("Seconds from full strength to nothing. Short: an impact, not an engine.")]
    [SerializeField] float seconds = 0.32f;

    [Tooltip("How the strength falls. Front-loaded, so it hits hard and gets out of the way.")]
    [SerializeField] AnimationCurve envelope = new AnimationCurve(
        new Keyframe(0f, 1f, 0f, -2.2f),
        new Keyframe(1f, 0f, -0.4f, 0f));

    [Header("Switch")]
    [Tooltip("Master switch. Off, every Pulse is ignored — for a gallery that asks for a " +
             "silent pad, or a visitor who finds it uncomfortable.")]
    [SerializeField] bool rumbleEnabled = true;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    float _low;
    float _high;
    float _duration;
    float _elapsed = -1f;

    bool _unavailable;
    bool _motorsOn;

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// One impact with the serialized defaults. No arguments, so it can be hung on any
    /// UnityEvent — an atom's onImpact, a beat's onRumble, the absorption.
    /// </summary>
    public void Pulse()
    {
        Pulse(lowFrequency, highFrequency, seconds);
    }

    /// <summary>
    /// One impact with explicit strengths and length. A stronger pulse arriving while a
    /// weaker one is still running replaces it; a weaker one is ignored until the strong
    /// one has died down, so a burst of absorptions reads as a series of hits and not as
    /// one continuous grind.
    /// </summary>
    public void Pulse(float low, float high, float durationSeconds)
    {
        if (!rumbleEnabled || _unavailable) return;

        low = Mathf.Clamp01(low);
        high = Mathf.Clamp01(high);

        if (_elapsed >= 0f && CurrentScale() * Mathf.Max(_low, _high) > Mathf.Max(low, high))
            return;

        _low = low;
        _high = high;
        _duration = Mathf.Max(0.02f, durationSeconds);
        _elapsed = 0f;

        Drive(1f);

        if (debugLog)
            Debug.Log("[TutorialRumble_NEW] Pulse " + low.ToString("F2") + " / " +
                      high.ToString("F2") + " for " + _duration.ToString("F2") + "s.", this);
    }

    /// <summary>Motors off, now. The attract reset and every teardown path call this.</summary>
    public void Stop()
    {
        _elapsed = -1f;
        Send(0f, 0f);
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Update()
    {
        if (_elapsed < 0f) return;

        // A paused piece does not buzz. The pulse is dropped rather than held: resuming
        // into the tail of a jolt that belonged to a moment the player has since stepped
        // out of would be a vibration with no cause.
        if (TutorialClock_NEW.Paused)
        {
            Stop();
            return;
        }

        // Real time. The jolt is what the player's hands feel, and inside D5's slow
        // motion it has to land at full speed — stretched five times it stops being an
        // impact and becomes a hum.
        _elapsed += Time.unscaledDeltaTime;

        if (_elapsed >= _duration)
        {
            Stop();
            return;
        }

        Drive(CurrentScale());
    }

    void OnDisable() { Stop(); }

    void OnApplicationQuit() { Stop(); }

    void OnApplicationFocus(bool focused)
    {
        if (!focused) Stop();
    }

    // ── Motors ───────────────────────────────────────────────────────────────

    float CurrentScale()
    {
        if (_elapsed < 0f || _duration <= 0f) return 0f;

        float t = Mathf.Clamp01(_elapsed / _duration);
        return envelope != null ? Mathf.Clamp01(envelope.Evaluate(t)) : 1f - t;
    }

    void Drive(float scale)
    {
        Send(_low * scale, _high * scale);
    }

    void Send(float low, float high)
    {
        // Skip the call entirely when there is nothing to change — XInputSetState is a
        // driver round trip, and at rest this would otherwise be four of them a frame.
        bool on = low > 0.0001f || high > 0.0001f;
        if (!on && !_motorsOn) return;
        _motorsOn = on;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        XInputVibration v = new XInputVibration
        {
            leftMotor = (ushort)(Mathf.Clamp01(low) * ushort.MaxValue),
            rightMotor = (ushort)(Mathf.Clamp01(high) * ushort.MaxValue)
        };

        // All four slots. The pad is not guaranteed to be player one — a second pad
        // plugged in for testing, or the exhibition pad re-paired, moves it — and a slot
        // with nothing in it answers ERROR_DEVICE_NOT_CONNECTED straight away.
        for (uint slot = 0; slot < 4; slot++)
        {
            if (!SetState(slot, ref v)) return;
        }
#else
        if (!_unavailable)
        {
            _unavailable = true;
            Debug.Log("[TutorialRumble_NEW] Rumble is Windows-only (XInput). Pulses will " +
                      "be ignored on this platform.", this);
        }
#endif
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [StructLayout(LayoutKind.Sequential)]
    struct XInputVibration
    {
        public ushort leftMotor;
        public ushort rightMotor;
    }

    // Two DLLs for one function. xinput1_4 ships with Windows 8 and later; xinput9_1_0 is
    // the one Windows 7 has. The first that loads is used from then on.
    [DllImport("xinput1_4", EntryPoint = "XInputSetState")]
    static extern uint XInputSetState14(uint userIndex, ref XInputVibration vibration);

    [DllImport("xinput9_1_0", EntryPoint = "XInputSetState")]
    static extern uint XInputSetState910(uint userIndex, ref XInputVibration vibration);

    static int _dll;   // 0 untried, 1 = 1_4, 2 = 9_1_0

    /// <summary>
    /// One call to the driver. Returns false, and switches rumble off for the session,
    /// if neither DLL can be loaded — the piece carries on without haptics.
    /// </summary>
    bool SetState(uint slot, ref XInputVibration v)
    {
        if (_dll != 2)
        {
            try
            {
                XInputSetState14(slot, ref v);
                _dll = 1;
                return true;
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }

        try
        {
            XInputSetState910(slot, ref v);
            _dll = 2;
            return true;
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }

        _unavailable = true;
        _motorsOn = false;
        Debug.LogWarning("[TutorialRumble_NEW] No XInput DLL on this machine, so the pad " +
                         "will not rumble. Everything else is unaffected.", this);
        return false;
    }
#endif
}

using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Controller rumble, for a project that has no rumble API.
///
/// Unity 2019.4 on the legacy Input Manager cannot vibrate a pad at all — TutorialBeat_NEW
/// has carried an onRumble seam since Phase 0 for exactly this reason. This talks to XInput
/// directly, which is the API an Xbox controller on Windows actually speaks, so it needs no
/// package and changes nothing about how input is read.
///
/// WHAT IT CANNOT DO. XInput is Xbox-family only. A DualSense or DualShock is not an XInput
/// device, and nothing sent through here reaches it — which matters on this project,
/// because the pad on the development desk is usually a DualSense (see
/// TutorialInput_NEW.PadLayout). DS4Windows or Steam Input presents one as an XInput pad,
/// and then this works unchanged; that is a desk convenience, not something to install on
/// site.
///
/// THE EXHIBITION PAD IS THE LOGITECH (PadProfile_NEW.Vizlab), and it does have motors.
/// Whether they can be reached from here depends on the switch on the back of the pad:
///
///   X mode (XInput)       reports as an Xbox pad — this rumbles it, and the AXES are
///                         Xbox-numbered, so the Vizlab profile's axis mapping is wrong
///   D mode (DirectInput)  the mapping the Vizlab profile was taken from — but it is not
///                         an XInput device in this mode, so nothing here reaches it
///
/// So axes and rumble currently want opposite switch positions. UNRESOLVED, and it has to
/// be settled on the real pad: see §12. The likely answer is X mode with the Xbox profile
/// pinned, which gets both, but the Logitech's letters sit where Xbox's do in that mode
/// and that needs reading off the hardware, not guessing.
///
/// SO IT SAYS WHY WHEN IT DOES NOTHING. A haptic that silently fails is indistinguishable
/// from one that was never wired, and the first time this was tried the only report was
/// "the pad did not rumble". Now:
///
///   F4 (with the debug overlay on) fires a test pulse, as does right-clicking this
///       component in the Inspector during Play → Test pulse. No need to play to D5.
///   The overlay's RUMBLE row names which XInput slots have a pad in them, and what the
///       last pulse did.
///   A pulse with no XInput pad connected logs a warning ONCE, naming every joystick Unity
///       can see — so a DualSense on the desk reads as "that is a PlayStation pad" and not
///       as a mystery.
///
/// ALWAYS STOPS. A motor left running is the worst failure a haptic can have, so the motors
/// are cut when the envelope ends, on pause, on disable, on focus loss and on quit.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("RUMBLE", "#C0607A")]
public class TutorialRumble_NEW : MonoBehaviour
{
    [Header("Default pulse")]
    [Tooltip("Left motor, 0 to 1. The heavy, low-frequency one — this is the thud.")]
    [Range(0f, 1f)]
    [SerializeField] float lowFrequency = 0.85f;

    [Tooltip("Right motor, 0 to 1. The light, high-frequency one — the crack on top of the " +
             "thud. Together they read as an impact rather than a buzz.")]
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
    [Tooltip("Fires a test pulse while the debug overlay is on. Not in the exhibition build: " +
             "it sits behind DebugView_NEW.Overlay like the director's keys.")]
    [SerializeField] KeyCode testKey = KeyCode.F4;

    [SerializeField] bool debugLog = false;

    float _low;
    float _high;
    float _duration;
    float _elapsed = -1f;

    /// <summary>
    /// No XInput on this machine. Static because it is a fact about the machine — the DLL
    /// is there or it is not — and because XInputSummary answers for the build, not for
    /// one component.
    /// </summary>
    static bool _unavailable;

    bool _motorsOn;
    bool _warnedNoPad;

    /// <summary>Bit per XInput slot with a pad in it, as of the last check.</summary>
    int _connected;

    /// <summary>
    /// The last probe's answer, readable without a reference to the component: -1 means
    /// nothing has probed yet, 0 means XInput sees no pad, otherwise a bit per slot.
    ///
    /// Static because the question is about the machine, not about this object, and
    /// because the pad readout asking it is drawn by the director - which must not have to
    /// find the Rumble object, or create one, to say what is plugged in. Probing is slow
    /// on empty slots (see Pulse), so nothing re-probes to answer: readers get the
    /// background refresh's last answer or the honest "not probed yet".
    /// </summary>
    public static int LastXInputSlots { get { return _xinputSlots; } }

    static int _xinputSlots = -1;

    /// <summary>
    /// XInput's view of what is plugged in, for a debug readout. One short phrase.
    ///
    /// THE QUESTION THIS ANSWERS is the Logitech's X/D switch. In X mode the pad is an
    /// XInput device and can be rumbled; in D mode it is not, and nothing here reaches it.
    /// Both modes look identical in the hand and report a similar name, so "does XInput
    /// see it" is the only way to tell from inside the build - and it is the same check
    /// that says whether a DualSense is being presented through DS4Windows.
    /// </summary>
    public static string XInputSummary()
    {
        // Probed here only when nothing has probed yet, which is the case in a scene with
        // no Rumble object — the journey's, where the F4 page still has to answer. Where
        // there is one it refreshes in the background every couple of seconds, and this
        // reads that answer rather than paying for a fresh query per frame.
        if (!_unavailable && _xinputSlots < 0) ConnectedSlots();

        if (_unavailable) return "no XInput on this machine";
        if (_xinputSlots < 0) return "not probed yet";
        if (_xinputSlots == 0) return "sees no pad";

        return "sees " + SlotList(_xinputSlots);
    }

    /// <summary>
    /// Re-ask XInput now, rather than waiting for the background refresh. For the moment
    /// after a pad is plugged in, or a mode switch flicked, with a readout open.
    /// </summary>
    public static void RefreshXInput()
    {
        if (_unavailable) return;
        ConnectedSlots();
    }

    string _lastPulse = "none yet";

    static TutorialRumble_NEW _instance;

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// The light is being absorbed: rumble, whatever the scene has or has not wired.
    ///
    /// CALLED FROM CODE, not hung on an event, and that is the fix for the first version
    /// not rumbling at all. That version needed a Rumble object and a listener to exist in
    /// the saved scene, and both only arrive when Build or Update is run AND the scene is
    /// saved. The pad itself was fine: the same XInput call made from outside Unity rumbled
    /// it on the first try.
    ///
    /// The caller is TutorialAtom_NEW, a moment before contact — every absorption in the
    /// piece is an atom touching the light, and the atom is the only thing that knows
    /// when that is about to happen, which is what lets the motor start early enough to
    /// be felt on the frame of the hit. The scene's Rumble object is used when there is
    /// one, so its Inspector values still tune it; when there is not, one is made.
    /// </summary>
    public static void Absorption()
    {
        TutorialRumble_NEW rumble = Instance();
        if (rumble != null) rumble.Pulse();
    }

    static TutorialRumble_NEW Instance()
    {
        if (_instance != null) return _instance;

        _instance = FindObjectOfType<TutorialRumble_NEW>();
        if (_instance != null) return _instance;

        GameObject go = new GameObject("[Rumble]");
        _instance = go.AddComponent<TutorialRumble_NEW>();

        Debug.Log("[TutorialRumble_NEW] The scene has no Rumble object, so one was made with " +
                  "the default settings. Run Build or Update and save the scene to get one you " +
                  "can tune.", go);

        return _instance;
    }

    /// <summary>
    /// One impact with the serialized defaults. No arguments, so it can be hung on any
    /// UnityEvent — an atom's onImpact, a beat's onRumble.
    /// </summary>
    public void Pulse()
    {
        Pulse(lowFrequency, highFrequency, seconds);
    }

    /// <summary>
    /// One impact with explicit strengths and length. A stronger pulse arriving while a
    /// weaker one is still running replaces it; a weaker one is ignored until the strong
    /// one has died down, so a burst of absorptions reads as a series of hits.
    /// </summary>
    public void Pulse(float low, float high, float durationSeconds)
    {
        if (!rumbleEnabled)
        {
            _lastPulse = "ignored — rumbleEnabled is off";
            return;
        }

        if (_unavailable)
        {
            _lastPulse = "ignored — no XInput on this machine";
            return;
        }

        low = Mathf.Clamp01(low);
        high = Mathf.Clamp01(high);

        if (_elapsed >= 0f && CurrentScale() * Mathf.Max(_low, _high) > Mathf.Max(low, high))
            return;

        // From the cache, not probed here. Asking XInput about an EMPTY slot is slow — it
        // goes looking for a device that is not there — and three of the four slots are
        // empty, so probing on the pulse frame put that search between the hit and the
        // motor. The cache is refreshed in the background (see Update); it is only probed
        // here when it says nothing is connected, which is the case where a pad has just
        // been switched on and there is no motor to delay anyway.
        if (_connected == 0) _connected = ConnectedSlots();

        if (_connected == 0)
        {
            _lastPulse = "no XInput pad connected at " + Time.unscaledTime.ToString("F1") + "s";
            WarnNoPad();
            return;
        }

        _low = low;
        _high = high;
        _duration = Mathf.Max(0.02f, durationSeconds);
        _elapsed = 0f;

        Drive(1f);

        _lastPulse = "pulsed " + SlotList(_connected) + " at " + Time.unscaledTime.ToString("F1") + "s";

        if (debugLog) Debug.Log("[TutorialRumble_NEW] " + _lastPulse + ".", this);
    }

    /// <summary>Motors off, now. The attract reset and every teardown path call this.</summary>
    public void Stop()
    {
        _elapsed = -1f;
        Send(0f, 0f);
    }

    /// <summary>Right-click the component during Play. Rumbles once, or says why it cannot.</summary>
    [ContextMenu("Test pulse")]
    void TestPulse()
    {
        _warnedNoPad = false;
        Pulse();
        Debug.Log("[TutorialRumble_NEW] Test pulse: " + _lastPulse + ".", this);
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (_instance == null) _instance = this;
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    void Start()
    {
        _connected = ConnectedSlots();
    }

    /// <summary>Seconds between background checks for which slots have a pad.</summary>
    const float ProbeInterval = 2f;

    float _sinceProbe;

    void Update()
    {
        if (DebugView_NEW.Overlay && Input.GetKeyDown(testKey)) TestPulse();

        // Background refresh of which slots have a pad, so a pulse never has to ask.
        // Never while rumbling — the whole point is to keep the slow query off the
        // frames where timing is felt.
        if (_elapsed < 0f)
        {
            _sinceProbe += Time.unscaledDeltaTime;

            if (_sinceProbe >= ProbeInterval)
            {
                _sinceProbe = 0f;
                _connected = ConnectedSlots();
            }
        }

        if (_elapsed < 0f) return;

        // A paused piece does not buzz. Dropped rather than held: resuming into the tail
        // of a jolt that belonged to a moment the player has stepped out of would be a
        // vibration with no cause.
        if (TutorialClock_NEW.Paused)
        {
            Stop();
            return;
        }

        // Real time. Inside D5's slow motion a jolt stretched five times stops being an
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
        bool on = low > 0.0001f || high > 0.0001f;
        if (!on && !_motorsOn) return;
        _motorsOn = on;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        XInputVibration v = new XInputVibration
        {
            leftMotor = (ushort)(Mathf.Clamp01(low) * ushort.MaxValue),
            rightMotor = (ushort)(Mathf.Clamp01(high) * ushort.MaxValue)
        };

        // Only the slots that have a pad in them. Stopping is sent to all four, so a pad
        // that disconnected mid-pulse and comes back is not left running.
        int slots = on ? _connected : 0xF;

        for (uint slot = 0; slot < 4; slot++)
        {
            if ((slots & (1 << (int)slot)) == 0) continue;
            if (!SetState(slot, ref v)) return;
        }
#endif
    }

    // ── Diagnostics ──────────────────────────────────────────────────────────

    void WarnNoPad()
    {
        if (_warnedNoPad) return;
        _warnedNoPad = true;

        string[] names = Input.GetJoystickNames();
        string seen = "";

        for (int i = 0; i < names.Length; i++)
        {
            if (string.IsNullOrEmpty(names[i])) continue;
            if (seen.Length > 0) seen += ", ";
            seen += "\"" + names[i] + "\"";
        }

        if (seen.Length == 0) seen = "none";

        Debug.LogWarning("[TutorialRumble_NEW] Asked to rumble, but no XInput controller is " +
                         "connected, so nothing was sent. Joysticks Unity can see: " + seen + ". " +
                         "An Xbox pad rumbles through XInput; a PlayStation pad (DualSense, " +
                         "DualShock — Windows calls it \"Wireless Controller\") is not an XInput " +
                         "device and cannot be rumbled from here — run DS4Windows or Steam Input " +
                         "to present it as one. The exhibition pad is the Logitech, which has " +
                         "motors but only reaches XInput with the switch on its back in X mode. " +
                         "Press F4 with the overlay on to test again.", this);
    }

    static string SlotList(int mask)
    {
        string s = "";

        for (int i = 0; i < 4; i++)
        {
            if ((mask & (1 << i)) == 0) continue;
            if (s.Length > 0) s += "+";
            s += "slot " + i;
        }

        return s.Length > 0 ? s : "none";
    }

    void OnGUI()
    {
        if (!DebugView_NEW.Overlay) return;

        string state = _unavailable ? "NO XINPUT"
                     : !rumbleEnabled ? "OFF"
                     : _elapsed >= 0f ? "RUMBLING"
                     : "idle";

        GUI.Label(DebugOverlayRows_NEW.Row(DebugOverlayRows_NEW.Rumble),
                  "RUMBLE  " + state + "   xinput pads: " + SlotList(_connected) +
                  "   last: " + _lastPulse + "   (" + testKey + " to test)");
    }

    // ── XInput ───────────────────────────────────────────────────────────────

    static int ConnectedSlots()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (_unavailable) return 0;

        int mask = 0;

        for (uint slot = 0; slot < 4; slot++)
        {
            XInputState state;
            uint result;

            if (!GetState(slot, out state, out result)) return 0;

            // 0 is ERROR_SUCCESS; anything else — ERROR_DEVICE_NOT_CONNECTED, 1167, being
            // the usual — means there is no pad in this slot.
            if (result == 0) mask |= 1 << (int)slot;
        }

        // Published for the pad readout, which must not probe for itself.
        _xinputSlots = mask;
        return mask;
#else
        if (!_unavailable)
        {
            _unavailable = true;
            _xinputSlots = 0;
            Debug.Log("[TutorialRumble_NEW] Rumble is Windows-only (XInput). Pulses will be " +
                      "ignored on this platform.");
        }
        return 0;
#endif
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [StructLayout(LayoutKind.Sequential)]
    struct XInputVibration
    {
        public ushort leftMotor;
        public ushort rightMotor;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct XInputState
    {
        public uint packetNumber;
        public ushort buttons;
        public byte leftTrigger;
        public byte rightTrigger;
        public short thumbLX;
        public short thumbLY;
        public short thumbRX;
        public short thumbRY;
    }

    // Two DLLs for one API. xinput1_4 ships with Windows 8 and later; xinput9_1_0 is the one
    // Windows 7 has. The first that loads is used from then on.
    [DllImport("xinput1_4", EntryPoint = "XInputSetState")]
    static extern uint XInputSetState14(uint userIndex, ref XInputVibration vibration);

    [DllImport("xinput9_1_0", EntryPoint = "XInputSetState")]
    static extern uint XInputSetState910(uint userIndex, ref XInputVibration vibration);

    [DllImport("xinput1_4", EntryPoint = "XInputGetState")]
    static extern uint XInputGetState14(uint userIndex, out XInputState state);

    [DllImport("xinput9_1_0", EntryPoint = "XInputGetState")]
    static extern uint XInputGetState910(uint userIndex, out XInputState state);

    static int _dll;   // 0 untried, 1 = xinput1_4, 2 = xinput9_1_0

    static bool SetState(uint slot, ref XInputVibration v)
    {
        if (_dll != 2)
        {
            try { XInputSetState14(slot, ref v); _dll = 1; return true; }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }

        try { XInputSetState910(slot, ref v); _dll = 2; return true; }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }

        MarkUnavailable();
        return false;
    }

    static bool GetState(uint slot, out XInputState state, out uint result)
    {
        state = default(XInputState);
        result = 1167;

        if (_dll != 2)
        {
            try { result = XInputGetState14(slot, out state); _dll = 1; return true; }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }

        try { result = XInputGetState910(slot, out state); _dll = 2; return true; }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }

        MarkUnavailable();
        return false;
    }

    /// <summary>
    /// Static, like the interop it guards: whether the DLL is on this machine is a fact
    /// about the machine, and the probe that discovers it runs whether or not a Rumble
    /// object exists in the scene. No motor can be running when no call ever reached one,
    /// so there is nothing per-component to tidy up here.
    /// </summary>
    static void MarkUnavailable()
    {
        if (_unavailable) return;

        _unavailable = true;
        _xinputSlots = 0;

        Debug.LogWarning("[TutorialRumble_NEW] No XInput DLL on this machine, so the pad " +
                         "will not rumble. Everything else is unaffected.");
    }
#endif
}

using System;
using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;

/// <summary>
/// The only script in the project that owns time.
///
/// Replaces CameraLayerResponder. Everything else reacts to an anchor this class fires;
/// nothing else runs its own transition clock. Three substantive changes:
///
///   1. CLOSED-LOOP WAITING. The old version waited a fixed WaitForSeconds derived from
///      GetBlendDuration(vcamMacro, vcamTopDown) — with the source camera hardcoded, so
///      four of the five real transitions used the wrong duration and three of them
///      swapped the world about a second before the cover was actually in place. This
///      version polls CinemachineBrain.ActiveBlend.BlendWeight and fires when the cover
///      is genuinely there. It no longer needs to know which camera it came from, it is
///      immune to frame-rate hitches, and it does not care that the brain blends in
///      FixedUpdate while coroutines run in Update.
///
///   2. ANCHORS INSTEAD OF DIRECT CALLS. The old code reached into VisualLayerResponder
///      via `visualResponder?.CrossfadeTo(...)` — a null-conditional call, so a broken
///      reference degraded into "the camera moves but the world never changes" with no
///      error. Now it broadcasts OnAnchor and any number of responders subscribe.
///
///   3. ZONE CAMERAS ARE DATA. ResolveZoneCamera() used a hardcoded switch on layerId,
///      the one place in the layer system that required a code edit to add a layer.
///      It is now a serialized list.
///
/// The exit sequence is gone. Gates are one-way, so "returning to a layer" is not a
/// state the player can reach; the old LookDownExitSequence was unreachable code.
///
/// LATER: DIVES. A gate that LayerDive_NEW lists plays DiveSequence instead of the cover:
/// the view zooms into the world being left rather than looking away from it. Same
/// anchors, same clock owner — see DiveSequence.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("CAMERA", "#4A94F2")]
public class CameraDirector_NEW : MonoBehaviour
{
    [Serializable]
    public class ZoneCamera
    {
        [Tooltip("Matches LayerProfile_NEW.layerId.")]
        public string layerId;
        public CinemachineFreeLook vcam;
    }

    [Header("Wiring")]
    [SerializeField] LayerState_NEW state;
    [SerializeField] PlayerRig_NEW player;
    [SerializeField] CinemachineBrain brain;

    [Header("Cameras")]
    [Tooltip("Cover camera blended to while the world swaps. Usually the top-down rig.")]
    [SerializeField] CinemachineVirtualCameraBase coverVcam;

    [Tooltip("Backward-facing rig used by the start layer's look-back sequence.")]
    [SerializeField] CinemachineFreeLook lookBackVcam;

    [Tooltip("Used for any layerId not listed below.")]
    [SerializeField] CinemachineFreeLook defaultZoneVcam;

    [Tooltip("Per-layer overrides. Add a row instead of editing code.")]
    [SerializeField] ZoneCamera[] zoneCameras = Array.Empty<ZoneCamera>();

    [Header("Dive")]
    [Tooltip("Gates this lists dive into the world instead of blending to the cover camera. " +
             "Found in the scene if empty; with none, every gate uses the cover as before.")]
    [SerializeField] LayerDive_NEW dive;

    [Header("Cover detection")]
    [Tooltip("Blend weight at which the cover counts as complete. 1 = wait for the very end.")]
    [Range(0.5f, 1f)]
    [SerializeField] float coverThreshold = 0.98f;

    [Tooltip("Safety cap so a mis-wired camera cannot stall the sequence forever.")]
    [SerializeField] float coverTimeout = 12f;

    [Header("Priorities")]
    [SerializeField] int priorityHigh = 50;
    [SerializeField] int priorityIdle = 10;
    [SerializeField] int priorityOff = 0;

    [Header("Look-back orbit")]
    [Tooltip("X-axis value that means 'facing forward' on the look-back rig. " +
             "180 when the rig has a heading bias of 180.")]
    [SerializeField] float lookBackForwardAngle = 180f;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    /// <summary>Fired at each transition anchor. Responders subscribe through LayerResponder_NEW.</summary>
    public event Action<AnchorEvent_NEW> OnAnchor;

    readonly List<CinemachineFreeLook> _allZoneVcams = new List<CinemachineFreeLook>();
    Coroutine _sequence;
    Coroutine _orbit;
    bool _inputLockedBySequence;
    bool _diving;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (state == null) state = FindObjectOfType<LayerState_NEW>();
        if (player == null) player = FindObjectOfType<PlayerRig_NEW>();
        if (brain == null) brain = FindObjectOfType<CinemachineBrain>();
        if (dive == null) dive = FindObjectOfType<LayerDive_NEW>();

        if (brain == null)
            Debug.LogError("[CameraDirector_NEW] No CinemachineBrain in scene. Cover detection " +
                           "will fall back to a fixed wait.", this);
        if (coverVcam == null)
            Debug.LogError("[CameraDirector_NEW] No cover camera assigned.", this);

        CollectZoneVcams();
        WarnAboutUnmanagedCameras();
    }

    /// <summary>
    /// Reports any virtual camera in the scene whose authored Priority would outrank the
    /// transition cameras while not being managed here.
    ///
    /// This diagnostic exists because of a real failure: VCam_Macro ships with Priority 100,
    /// and the moment it was left out of this component's camera set it silently outranked
    /// the look-back rig at 50. The opening backward shot simply never appeared, with no
    /// error anywhere. Priority conflicts are invisible at runtime, so they get named here.
    /// </summary>
    void WarnAboutUnmanagedCameras()
    {
        var managed = new HashSet<CinemachineVirtualCameraBase>();
        if (coverVcam != null) managed.Add(coverVcam);
        if (lookBackVcam != null) managed.Add(lookBackVcam);
        foreach (CinemachineFreeLook v in _allZoneVcams)
            if (v != null) managed.Add(v);

        foreach (CinemachineVirtualCameraBase vcam in FindObjectsOfType<CinemachineVirtualCameraBase>())
        {
            if (vcam == null || managed.Contains(vcam)) continue;

            // FreeLook rigs are hidden child cameras owned by their parent — not standalone.
            if (vcam.ParentCamera != null) continue;

            if (vcam.Priority >= priorityHigh)
                Debug.LogWarning(
                    $"[CameraDirector_NEW] '{vcam.Name}' has Priority {vcam.Priority} but is not managed " +
                    $"by this director, so it will outrank the transition cameras and the sequence will " +
                    $"appear to do nothing. Add it to Zone Cameras, or lower its Priority below " +
                    $"{priorityHigh}.", vcam);
        }
    }

    void OnEnable()
    {
        if (state != null) state.OnLayerChanged += HandleLayerChanged;
    }

    void OnDisable()
    {
        if (state != null) state.OnLayerChanged -= HandleLayerChanged;
        StopSequence(restoreInput: true);
    }

    void CollectZoneVcams()
    {
        _allZoneVcams.Clear();
        if (defaultZoneVcam != null) _allZoneVcams.Add(defaultZoneVcam);

        for (int i = 0; i < zoneCameras.Length; i++)
        {
            ZoneCamera z = zoneCameras[i];
            if (z == null || z.vcam == null) continue;
            if (!_allZoneVcams.Contains(z.vcam)) _allZoneVcams.Add(z.vcam);
        }
    }

    // ── Routing ──────────────────────────────────────────────────────────────

    void HandleLayerChanged(LayerProfile_NEW previous, LayerProfile_NEW current)
    {
        if (current == null) return;

        StopSequence(restoreInput: true);
        _sequence = StartCoroutine(RunSequence(previous, current));
    }

    void StopSequence(bool restoreInput)
    {
        if (_sequence != null)
        {
            StopCoroutine(_sequence);
            _sequence = null;
        }

        // A stopped coroutine runs no cleanup, so a dive cut short is put back by hand —
        // otherwise its world stays scaled and its light stays on screen.
        if (_diving)
        {
            _diving = false;
            if (dive != null) dive.Abort();
        }

        // Only release the lock this class took. The orbit routine manages its own lock,
        // so an interrupted sequence no longer unlocks input mid-orbit — a real bug in
        // the old HandleLayerChanged, which called SetCameraInputLocked(false) blindly.
        if (restoreInput && _inputLockedBySequence)
        {
            _inputLockedBySequence = false;
            if (player != null) player.SetCameraInputLocked(false);
        }
    }

    IEnumerator RunSequence(LayerProfile_NEW previous, LayerProfile_NEW current)
    {
        if (debugLog)
            Debug.Log($"[CameraDirector_NEW] '{(previous != null ? previous.layerId : "start")}' " +
                      $"-> '{current.layerId}'.", this);

        if (current.lockLookInput && player != null)
        {
            _inputLockedBySequence = true;
            player.SetCameraInputLocked(true,
                "transition into " + current.layerId + " (LP_" + current.layerId + ".lockLookInput)");
        }

        Fire(TransitionAnchor_NEW.SequenceStart, current, -1f);

        bool isInitialEmit = previous == null;

        if (current.useLookBackSequence)
            yield return LookBackSequence(current);
        else if (isInitialEmit)
            yield return InitialSequence(current);
        else if (previous.useLookBackSequence)
            yield return FirstGateSequence(current);
        else if (dive != null && dive.TryGetDive(current.layerId, out LayerDive_NEW.Dive spec))
            yield return DiveSequence(previous, current, spec);
        else
            yield return ZoneEnterSequence(current);

        if (_inputLockedBySequence && player != null)
        {
            _inputLockedBySequence = false;
            player.SetCameraInputLocked(false);
        }

        if (debugLog) Debug.Log($"[CameraDirector_NEW] '{current.layerId}' sequence complete.", this);
        _sequence = null;
    }

    // ── Sequences ────────────────────────────────────────────────────────────

    /// <summary>
    /// Start layer: face backward, count down, then orbit to forward.
    /// The player may reach the first gate before the orbit finishes; FirstGateSequence waits.
    /// </summary>
    IEnumerator LookBackSequence(LayerProfile_NEW profile)
    {
        SetPriority(lookBackVcam, priorityHigh);
        SetPriority(defaultZoneVcam, priorityIdle);
        SetPriority(coverVcam, priorityOff);
        foreach (CinemachineFreeLook v in _allZoneVcams)
            if (v != defaultZoneVcam) SetPriority(v, priorityOff);

        // The start layer has no cover phase, so CoverReached fires immediately: the
        // world is already correct at startup and responders need their initial apply.
        Fire(TransitionAnchor_NEW.CoverReached, profile, 0f);
        Fire(TransitionAnchor_NEW.LookUpStart, profile, -1f);

        yield return new WaitForSeconds(profile.lookBackOrbitCountdown);

        if (_orbit != null) StopCoroutine(_orbit);
        _orbit = StartCoroutine(OrbitToForward(profile.lookBackOrbitDuration));
        yield return _orbit;
    }

    /// <summary>
    /// Startup for a start layer that does not use the look-back sequence.
    ///
    /// There is nothing to transition from, so there is no cover phase: put the zone
    /// camera live and fire every anchor on the spot so responders get their initial
    /// apply. Without this branch the initial emit would fall through to
    /// ZoneEnterSequence and blend the player to the top-down camera on play.
    /// </summary>
    IEnumerator InitialSequence(LayerProfile_NEW profile)
    {
        CinemachineFreeLook zone = ResolveZoneCamera(profile.layerId);

        SetPriority(coverVcam, priorityOff);
        SetPriority(lookBackVcam, priorityOff);
        foreach (CinemachineFreeLook v in _allZoneVcams) SetPriority(v, priorityOff);
        SetPriority(zone, priorityHigh);

        Fire(TransitionAnchor_NEW.CoverReached, profile, 0f);
        Fire(TransitionAnchor_NEW.LookUpStart, profile, -1f);

        yield break;
    }

    /// <summary>
    /// First gate after the start layer. The world swaps right away so it is already
    /// correct behind the player, then we wait out the orbit before blending forward.
    /// </summary>
    IEnumerator FirstGateSequence(LayerProfile_NEW profile)
    {
        SetPriority(lookBackVcam, priorityHigh);
        SetPriority(defaultZoneVcam, priorityIdle);
        SetPriority(coverVcam, priorityOff);

        // The look-back rig is pointing away from the world, so it is its own cover.
        Fire(TransitionAnchor_NEW.CoverReached, profile, 0f);

        if (_orbit != null)
        {
            if (debugLog) Debug.Log("[CameraDirector_NEW] Waiting for the orbit to finish.", this);
            while (_orbit != null) yield return null;
        }
        else if (lookBackVcam != null &&
                 !Mathf.Approximately(lookBackVcam.m_XAxis.Value, lookBackForwardAngle))
        {
            // The player beat the countdown. Run the orbit now instead of snapping.
            if (debugLog) Debug.Log("[CameraDirector_NEW] Orbit had not started; running it now.", this);
            yield return OrbitToForward(profile.lookBackOrbitDuration);
        }

        Fire(TransitionAnchor_NEW.LookUpStart, profile, -1f);

        CinemachineFreeLook zone = ResolveZoneCamera(profile.layerId);
        SetPriority(zone, priorityHigh);
        SetPriority(lookBackVcam, priorityOff);

        yield return WaitUntilLive(zone);
    }

    /// <summary>
    /// Every other gate: blend to the cover camera, swap the world under it, hold, look up.
    /// </summary>
    IEnumerator ZoneEnterSequence(LayerProfile_NEW profile)
    {
        CinemachineFreeLook zone = ResolveZoneCamera(profile.layerId);

        // Explicitly set every camera every time rather than relying on leftover state.
        SetPriority(coverVcam, priorityHigh);
        SetPriority(lookBackVcam, priorityOff);
        foreach (CinemachineFreeLook v in _allZoneVcams) SetPriority(v, priorityOff);
        SetPriority(defaultZoneVcam, priorityIdle);

        yield return WaitUntilLive(coverVcam);

        float untilLookUp = Mathf.Max(0f, profile.lookDownHoldDuration)
                          + Mathf.Max(0f, profile.lookUpStartDelay);
        Fire(TransitionAnchor_NEW.CoverReached, profile, untilLookUp);

        if (profile.lookDownHoldDuration > 0f)
            yield return new WaitForSeconds(profile.lookDownHoldDuration);
        if (profile.lookUpStartDelay > 0f)
            yield return new WaitForSeconds(profile.lookUpStartDelay);

        Fire(TransitionAnchor_NEW.LookUpStart, profile, -1f);

        SetPriority(zone, priorityHigh);
        SetPriority(coverVcam, priorityOff);

        yield return WaitUntilLive(zone);
    }

    /// <summary>
    /// A gate LayerDive_NEW lists: zoom into the world being left instead of looking away.
    ///
    /// Same anchors as ZoneEnterSequence, so every responder works unchanged. CoverReached
    /// fires at the peak of the dive, under full light — the moment the cover used to
    /// reach full weight — and the next zone camera goes live there too, so its blend is
    /// hidden by the light. (Coming out of a cluster there is no light: the camera is
    /// looking back by then, and the blend happens under its swing.) This method keeps the
    /// clock, the hold at the peak included; LayerDive_NEW only draws the frame for a given
    /// progress.
    /// </summary>
    IEnumerator DiveSequence(LayerProfile_NEW previous, LayerProfile_NEW current, LayerDive_NEW.Dive spec)
    {
        CinemachineFreeLook zone = ResolveZoneCamera(current.layerId);

        // Nothing looks away: the zone camera the player is on stays live for the dive.
        SetPriority(coverVcam, priorityOff);
        SetPriority(lookBackVcam, priorityOff);

        _diving = true;
        dive.Begin(previous, current, spec);

        for (float t = 0f; t < spec.diveSeconds; t += Time.deltaTime)
        {
            dive.TickDive(t / spec.diveSeconds);
            yield return null;
        }

        dive.TickDive(1f);
        dive.Crossover();

        foreach (CinemachineFreeLook v in _allZoneVcams) SetPriority(v, priorityOff);
        SetPriority(zone, priorityHigh);

        Fire(TransitionAnchor_NEW.CoverReached, current, spec.peakHoldSeconds + spec.emergeSeconds);

        // Ticked, not waited out: coming out, the cluster keeps collapsing through the hold.
        for (float t = 0f; t < spec.peakHoldSeconds; t += Time.deltaTime)
        {
            dive.TickHold(t / spec.peakHoldSeconds);
            yield return null;
        }

        for (float t = 0f; t < spec.emergeSeconds; t += Time.deltaTime)
        {
            dive.TickEmerge(t / spec.emergeSeconds);
            yield return null;
        }

        dive.TickEmerge(1f);

        Fire(TransitionAnchor_NEW.LookUpStart, current, -1f);

        _diving = false;
        dive.End();
    }

    IEnumerator OrbitToForward(float duration)
    {
        if (lookBackVcam == null) { _orbit = null; yield break; }

        if (player != null) player.SetCameraInputLocked(true, "look-back orbit to forward");

        float elapsed = 0f;
        float start = lookBackVcam.m_XAxis.Value;
        float d = Mathf.Max(0.01f, duration);

        while (elapsed < d)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / d));
            lookBackVcam.m_XAxis.Value = Mathf.Lerp(start, lookBackForwardAngle, t);
            yield return null;
        }

        lookBackVcam.m_XAxis.Value = lookBackForwardAngle;

        // Only release if the running sequence is not itself holding the lock.
        if (player != null && !_inputLockedBySequence) player.SetCameraInputLocked(false);

        _orbit = null;
    }

    // ── Closed-loop cover detection ──────────────────────────────────────────

    /// <summary>
    /// Waits until <paramref name="target"/> is the live camera at full blend weight.
    /// Returns immediately when the brain reports no blend, which is the correct
    /// behaviour for a cut or for a camera that is already live.
    /// </summary>
    IEnumerator WaitUntilLive(ICinemachineCamera target)
    {
        if (target == null) yield break;

        if (brain == null)
        {
            // Degraded path: no brain means we cannot observe the blend at all.
            yield return new WaitForSeconds(1f);
            yield break;
        }

        // One frame for the brain to notice the priority change.
        yield return null;

        float deadline = Time.time + Mathf.Max(0.5f, coverTimeout);

        while (Time.time < deadline)
        {
            CinemachineBlend blend = brain.ActiveBlend;

            if (blend == null)
            {
                // Not blending. Either we arrived, or another camera won — either way
                // there is nothing left to wait for.
                yield break;
            }

            if (ReferenceEquals(blend.CamB, target) && blend.BlendWeight >= coverThreshold)
                yield break;

            yield return null;
        }

        Debug.LogWarning($"[CameraDirector_NEW] Timed out waiting for '{target.Name}' to become live. " +
                         "Check its Priority and the Cinemachine blend settings.", this);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    void Fire(TransitionAnchor_NEW anchor, LayerProfile_NEW profile, float secondsUntilLookUp)
    {
        OnAnchor?.Invoke(new AnchorEvent_NEW
        {
            anchor = anchor,
            profile = profile,
            secondsUntilLookUp = secondsUntilLookUp
        });
    }

    public CinemachineFreeLook ResolveZoneCamera(string layerId)
    {
        for (int i = 0; i < zoneCameras.Length; i++)
        {
            ZoneCamera z = zoneCameras[i];
            if (z != null && z.vcam != null && string.Equals(z.layerId, layerId, StringComparison.Ordinal))
                return z.vcam;
        }
        return defaultZoneVcam;
    }

    void SetPriority(CinemachineVirtualCameraBase vcam, int priority)
    {
        if (vcam != null) vcam.Priority = priority;
    }
}

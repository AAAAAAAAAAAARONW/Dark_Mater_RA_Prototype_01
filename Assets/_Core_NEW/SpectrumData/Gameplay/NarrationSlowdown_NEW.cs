using System;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Slows the flight while the redshift is being explained, so the explanation fits inside
/// the second cosmic web.
///
/// WHY. The explanation (J5, then the demo's F1–F6) runs about a minute; the second cosmic web
/// takes about 22 s to fly through at normal speed. The VO only starts the demo if J5 ends
/// while the player is still in that layer, so at full speed the demo never started at all.
///
/// WHAT. On entering slowLayerId the flight eases down to slowFactor; it eases back to normal
/// when the demo finishes, when the layer is left, or after maxSlowSeconds — whichever first.
/// The spectrum follows the tracker, so it simply advances slowly while it is talked about.
///
/// ISOLATION. Uses PlayerRig_NEW's own narration multiplier, separate from the per-layer one
/// SpeedResponder_NEW drives, so the two never fight. Untick slowdownEnabled: no effect.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("SLOW FOR VO", "#FFE8A0")]
public class NarrationSlowdown_NEW : MonoBehaviour
{
    [Header("Switch")]
    [SerializeField] bool slowdownEnabled = true;

    [Header("Wiring (found in the scene if empty)")]
    [SerializeField] PlayerRig_NEW player;
    [SerializeField] LayerState_NEW layerState;
    [SerializeField] JourneySequence_NEW sequence;

    [Header("When")]
    [Tooltip("Entering this layer starts the slowdown. The second cosmic web is 'CosmicWeb' (the first is 'Macro').")]
    [SerializeField] string slowLayerId = "CosmicWeb";

    [Tooltip("Safety: back to normal after this long however the demo is doing.")]
    [Min(1f)]
    [SerializeField] float maxSlowSeconds = 90f;

    [Header("How much")]
    [Tooltip("Speed while explaining, as a fraction of normal. 0.3 makes the second cosmic web last " +
             "about 70 s instead of 22 — room for J5 and the whole demo.")]
    [Range(0.05f, 1f)]
    [SerializeField] float slowFactor = 0.3f;

    [Tooltip("Seconds to ease down, and back up.")]
    [Min(0f)]
    [SerializeField] float easeSeconds = 2f;

    [SerializeField] bool debugLog = true;

    float _value = 1f;
    float _target = 1f;
    float _slowFor;
    bool _slowing;

    void Awake()
    {
        if (player == null) player = FindObjectOfType<PlayerRig_NEW>();
        if (layerState == null) layerState = FindObjectOfType<LayerState_NEW>();
        if (sequence == null) sequence = FindObjectOfType<JourneySequence_NEW>();
    }

    void OnEnable()
    {
        if (layerState != null) layerState.OnLayerChanged += HandleLayer;
        if (sequence != null) sequence.OnFinished += HandleFinished;
    }

    void OnDisable()
    {
        if (layerState != null) layerState.OnLayerChanged -= HandleLayer;
        if (sequence != null) sequence.OnFinished -= HandleFinished;
        _value = _target = 1f;
        _slowing = false;
        if (player != null) player.SetNarrationSpeedMultiplier(1f);
    }

    void HandleLayer(LayerProfile_NEW previous, LayerProfile_NEW current)
    {
        bool entering = current != null && string.Equals(current.layerId, slowLayerId, StringComparison.OrdinalIgnoreCase);
        if (entering && slowdownEnabled) Begin("entered '" + slowLayerId + "'");
        else if (_slowing) End("left '" + slowLayerId + "'");
    }

    void HandleFinished()
    {
        if (_slowing) End("the redshift demo finished");
    }

    void Begin(string why)
    {
        _slowing = true;
        _slowFor = 0f;
        _target = slowFactor;
        if (debugLog) Debug.Log("[NarrationSlowdown_NEW] Slowing to " + slowFactor.ToString("0.00") + "×: " + why + ".", this);
    }

    void End(string why)
    {
        _slowing = false;
        _target = 1f;
        if (debugLog) Debug.Log("[NarrationSlowdown_NEW] Back to normal speed: " + why + ".", this);
    }

    void Update()
    {
        if (player == null) return;
        if (!slowdownEnabled && _slowing) End("switched off");

        if (_slowing)
        {
            _slowFor += Time.deltaTime;
            if (_slowFor >= maxSlowSeconds) End("maxSlowSeconds reached");
        }

        float step = easeSeconds <= 0f ? 1f : Time.deltaTime / easeSeconds;
        float next = Mathf.MoveTowards(_value, _target, step);
        if (!Mathf.Approximately(next, _value) || !Mathf.Approximately(player.NarrationSpeedMultiplier, _value))
        {
            _value = next;
            player.SetNarrationSpeedMultiplier(_value);
        }
    }
}

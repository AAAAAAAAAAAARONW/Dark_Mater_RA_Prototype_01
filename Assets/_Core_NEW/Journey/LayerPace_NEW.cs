using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Slow in, then out: a layer the journey slows down for (the galaxy, the Milky Way) stays
/// slow while it is being talked about, and then the light speeds up to leave.
///
/// WHY. Both galaxies run at half speed (LP_Micro, LP_MilkyWay zoneSpeedMultiplier 0.5),
/// which is right for arriving — J4b says why the journey slows — but at half speed each
/// took about 75 seconds to fly through with 10 to 25 seconds of voice in it. Moving the
/// gates closer would have meant moving everything after them. This changes the pace
/// instead: slow for the words, quick for the rest.
///
/// WHEN. Hurry() starts the speed-up. It is meant to hang on the layer's voice-over row
/// (JourneyVoiceOver_NEW onFinished), so the light speeds up after the last line rather
/// than after a number of seconds that stops matching the day a line is re-recorded.
/// holdAfterLines is a moment of quiet before it does. maxSlowSeconds is the backstop: if
/// the lines never finish (missing clip, skipped), it hurries anyway.
///
/// Leaving the layer eases back to normal speed. Uses PlayerRig_NEW's pace multiplier,
/// which nothing else writes.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("PACE", "#4A94F2")]
public class LayerPace_NEW : MonoBehaviour
{
    [Serializable]
    public class Layer
    {
        public string layerId = "Micro";

        [Tooltip("Pace on arrival, as a multiple of the layer's own speed. 1 = as the layer " +
                 "profile has it. Below 1 changes how the trail looks (it is drawn from the " +
                 "light's motion), which is why the start layer is NOT slowed this way.")]
        [Min(0.01f)]
        public float arriveMultiplier = 1f;

        [Tooltip("Seconds of quiet after the last line before speeding up.")]
        [Min(0f)]
        public float holdAfterLines = 2f;

        [Tooltip("Pace while leaving, as a multiple of the layer's own speed. In the galaxies " +
                 "(their own speed 0.5x) 4 = twice the normal journey speed. In Quasar, 1 = " +
                 "back to normal.")]
        [Min(1f)]
        public float hurryMultiplier = 4f;

        [Tooltip("Seconds to ease up to that speed.")]
        [Min(0.1f)]
        public float easeSeconds = 3f;

        [Tooltip("Hurry anyway after this long in the layer, in case the lines never finish.")]
        [Min(1f)]
        public float maxSlowSeconds = 60f;
    }

    [Header("Wiring (found if empty)")]
    [SerializeField] PlayerRig_NEW player;
    [SerializeField] LayerState_NEW layerState;

    [SerializeField] Layer[] layers =
    {
        new Layer { layerId = "Micro" },
        new Layer { layerId = "MilkyWay" }
    };

    [SerializeField] bool debugLog = false;

    Layer _current;
    float _inLayer;
    float _holdLeft = -1f;
    float _from = 1f, _to = 1f, _easeT = 1f, _easeSeconds = 1f;
    bool _hurried;

    /// <summary>The current layer's lines are done: speed up, after its hold.</summary>
    public void Hurry()
    {
        if (_current == null || _hurried || _holdLeft >= 0f) return;
        _holdLeft = _current.holdAfterLines;
    }

    void Awake()
    {
        if (player == null) player = FindObjectOfType<PlayerRig_NEW>();
        if (layerState == null) layerState = FindObjectOfType<LayerState_NEW>();
    }

    void OnEnable()
    {
        if (layerState != null) layerState.OnLayerChanged += HandleLayerChanged;
    }

    void OnDisable()
    {
        if (layerState != null) layerState.OnLayerChanged -= HandleLayerChanged;
        if (player != null) player.SetPaceSpeedMultiplier(1f);
    }

    void HandleLayerChanged(LayerProfile_NEW previous, LayerProfile_NEW current)
    {
        _current = null;
        if (current != null)
            foreach (Layer l in layers)
                if (l != null && string.Equals(l.layerId, current.layerId, StringComparison.OrdinalIgnoreCase))
                    _current = l;

        _inLayer = 0f;
        _holdLeft = -1f;
        _hurried = false;

        // Whatever the new layer is, it starts at its own arrival pace (normally its own speed).
        // The start layer snaps there: easing from full speed would spend most of its short
        // length getting slow.
        float arrive = _current != null ? _current.arriveMultiplier : 1f;
        if (previous == null && player != null)
        {
            player.SetPaceSpeedMultiplier(arrive);
            _easeT = 1f;
        }
        else EaseTo(arrive, 2f);
    }

    void Update()
    {
        if (player == null) return;

        float dt = Time.deltaTime;

        if (_current != null && !_hurried)
        {
            _inLayer += dt;

            if (_holdLeft >= 0f)
            {
                _holdLeft -= dt;
                if (_holdLeft <= 0f) StartHurry("its lines are done");
            }
            else if (_inLayer >= _current.maxSlowSeconds)
            {
                StartHurry("maxSlowSeconds passed");
            }
        }

        if (_easeT < 1f)
        {
            _easeT = Mathf.Min(1f, _easeT + dt / _easeSeconds);
            player.SetPaceSpeedMultiplier(Mathf.Lerp(_from, _to, Mathf.SmoothStep(0f, 1f, _easeT)));
        }
    }

    void StartHurry(string why)
    {
        _hurried = true;
        _holdLeft = -1f;
        EaseTo(_current.hurryMultiplier, _current.easeSeconds);

        if (debugLog) Debug.Log("[LayerPace_NEW] " + _current.layerId + ": speeding up (" + why + ").", this);
    }

    void EaseTo(float target, float seconds)
    {
        _from = player != null ? player.PaceSpeedMultiplier : 1f;
        _to = target;
        _easeSeconds = Mathf.Max(0.1f, seconds);
        _easeT = Mathf.Approximately(_from, _to) ? 1f : 0f;
    }
}

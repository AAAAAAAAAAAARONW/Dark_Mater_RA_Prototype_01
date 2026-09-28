using System;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
// Aliased: the post-processing namespace has its own MinAttribute, which would make
// [Min] below ambiguous with UnityEngine.MinAttribute.
using PP = UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;

/// <summary>
/// A gate that dives INTO the world you are in, instead of looking away while it is swapped.
///
/// WHY. Every gate played the cover transition: blend to the top-down camera, swap the
/// render groups on the frame the world is out of view, hold, look back up. It hides the
/// swap and says nothing about scale. Going from the cosmic web to a galaxy is not a
/// change of scenery, it is a change of MAGNIFICATION — one small part of one cluster in
/// the web IS a galaxy — and a camera that looks at the floor while the web vanishes and
/// a galaxy appears reads as a cut.
///
/// WHERE IT DIVES. Light only travels in a straight line, so whatever the photon is flying
/// into when it reaches a gate is on that line: the point is the gate's centre carried
/// focusPastGate further along the direction of travel. At the Micro gate that is the
/// bright yellow cluster — the densest knot of the Core and Coreyellow meshes sits eleven
/// units past the trigger, dead ahead.
///
/// THE RHYTHM:
///
///   approach   Before the gate, by distance: a light appears in the cluster
///              approachDistance before the trigger and grows as the photon closes in, so
///              the dive has a destination before it starts.
///   dive       From the gate: the old world grows around the point at a CONSTANT rate —
///              every second magnifies by the same factor, so it holds its speed instead
///              of rushing — brightens and dissolves, while the light swells. The photon
///              keeps flying into the point and arrives about as the dive ends.
///   peak       Under full light, CoverReached fires: sky, speed and spectrum change, and
///              the next zone camera goes live — the frame the cover camera used to give.
///   emerge     The light settles into the middle of the next world and fades as that
///              world fades in around it.
///
/// THE LIGHT is a camera-facing glow in the world, drawn after the web and before the
/// photon trail, so it sits inside the cluster and behind the photon. Only the full-screen
/// whiteout at the peak is an overlay.
///
/// WHO OWNS WHAT. CameraDirector_NEW still owns time: it runs the clock, fires the anchors
/// and moves the cameras, and asks this component only how the frame looks at a given
/// progress. The approach is the one exception, and it is driven by distance, not time.
/// The render groups are borrowed from WorldSwitcher_NEW for the length of the dive
/// (Hold / Release), so its anchor-driven swap cannot snap a world halfway through fading.
///
/// SCALING A WORLD. The baked volumes scale cleanly. Particle systems in "Local" scaling
/// mode ignore their parents' scale, so for the length of a scale they are switched to
/// "Hierarchy" with the parents divided back out. Systems that simulate in world space
/// cannot follow a moving root at all and only fade — which is why enterScale defaults
/// to 1: the galaxy is mostly world-space particles.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("DIVE", "#E07A5F")]
public class LayerDive_NEW : MonoBehaviour
{
    [Serializable]
    public class Dive
    {
        [Tooltip("The layer being entered. The dive plays on the gate into this layer.")]
        public string toLayerId = "Micro";

        [Tooltip("Untick to put this gate back on the cover transition without losing the tuning.")]
        public bool enabled = true;

        [Header("Where")]
        [Tooltip("How far past the gate, along the direction of travel, the point sits. " +
                 "Light only travels in a straight line, so the gate's position is enough: " +
                 "at the Micro gate the yellow cluster's densest knot is 11 past the trigger.")]
        public float focusPastGate = 11f;

        [Tooltip("Optional. Dive into this object's position instead, for a target that is " +
                 "not on the line.")]
        public Transform focusOverride;

        [Header("Approach (before the gate)")]
        [Tooltip("The light appears at the point this far before the gate, in world units, " +
                 "and grows as the photon closes in. 0 = it appears at the gate.")]
        [Min(0f)] public float approachDistance = 40f;

        [Header("Timing (seconds)")]
        [Tooltip("From the gate to the peak. About the time the photon takes to reach the " +
                 "point, so the peak lands as it arrives: 16 units at Macro's 5 per second.")]
        [Min(0.1f)] public float diveSeconds = 3f;

        [Tooltip("Held at full light. The swap, the sky and the camera change happen at its start.")]
        [Min(0f)] public float peakHoldSeconds = 0.2f;

        [Tooltip("From the peak to the end: the next world appearing around the light.")]
        [Min(0.1f)] public float emergeSeconds = 2.8f;

        [Header("The world being left")]
        [Tooltip("Scale the old world reaches around the point by the peak, at a constant " +
                 "rate. Above 1 dives in; below 1 pulls out, for gates that go up in scale.")]
        [Min(0.01f)] public float diveZoom = 4f;

        [Tooltip("How much brighter the old world glows at the peak, just before it is gone.")]
        [Min(0f)] public float leaveGlow = 2f;

        [Tooltip("Fraction of the dive at which the old world starts to dissolve.")]
        [Range(0f, 1f)] public float dissolveFrom = 0.5f;

        [Header("The world being entered")]
        [Tooltip("Scale the new world starts at around its centre, settling to 1. 1 = fade " +
                 "in only. World-space particle systems cannot follow a scale and only fade.")]
        [Min(0.01f)] public float enterScale = 1f;

        [Header("Light")]
        public Color lightColor = new Color(1f, 0.88f, 0.62f, 1f);

        [Tooltip("Brightness of the light, which is added to what is behind it.")]
        [Range(0f, 4f)] public float lightIntensity = 1.5f;

        [Tooltip("World-space diameter when the light first appears, approachDistance before the gate.")]
        [Min(0f)] public float pointSize = 3f;

        [Tooltip("Diameter as the photon reaches the gate.")]
        [Min(0f)] public float gateSize = 8f;

        [Tooltip("Diameter at the peak.")]
        [Min(0f)] public float peakSize = 40f;

        [Tooltip("Opacity of the full-screen light at the peak. 1 hides the swap completely.")]
        [Range(0f, 1f)] public float whiteout = 1f;

        [Header("Lens (blended over the scene's own post-processing)")]
        [Tooltip("Field of view multiplier at the peak. 1 = unchanged. Above 1 widens, which " +
                 "reads as speeding up.")]
        [Range(0.5f, 2f)] public float fieldOfViewScale = 1f;

        [Tooltip("Bloom intensity at the peak. The scene's own is 0.8.")]
        [Min(0f)] public float peakBloom = 3f;

        [Range(0f, 1f)] public float peakChromaticAberration = 0.15f;

        [Tooltip("Lens distortion at the peak, centred on the point. 0 = off.")]
        [Range(-100f, 100f)] public float peakLensDistortion = 0f;
    }

    // Renamed from 'dives', deliberately without FormerlySerializedAs: a row saved with the
    // first version's rushing defaults (zoom 40, lens widening 1.3) is dropped, and the
    // gate starts from these.
    [Tooltip("One row per gate that dives. Matched on the layer being entered.")]
    [SerializeField] Dive[] gates = { new Dive() };

    [Header("Wiring (found if empty)")]
    [SerializeField] WorldSwitcher_NEW worlds;
    [SerializeField] CinemachineBrain brain;
    [SerializeField] LayerState_NEW state;
    [SerializeField] PlayerRig_NEW player;

    [Header("Overlay")]
    [Tooltip("Sort order of the whiteout. Below the HUD canvases (0) keeps the HUD readable " +
             "through it; the entry fade sits far above at 32000.")]
    [SerializeField] int overlaySortOrder = -50;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    /// <summary>A group root being scaled, and how to put it back.</summary>
    class Scaled
    {
        public Transform root;
        public Vector3 position;
        public Vector3 localScale;
        public bool particlesPrepared;
        public readonly List<ParticleState> particles = new List<ParticleState>();
    }

    struct ParticleState
    {
        public ParticleSystem system;
        public Vector3 localScale;
        public ParticleSystemScalingMode mode;
    }

    static readonly int ColorId = Shader.PropertyToID("_Color");

    readonly Dictionary<string, LayerGate_NEW> _gatesByLayer = new Dictionary<string, LayerGate_NEW>();

    Dive _dive;
    string _fromId;
    string _toId;
    Vector3 _focus;
    Vector3 _emergeFocus;
    Scaled _leave;
    Scaled _enter;
    Camera _camera;

    GameObject _light;
    Material _lightMaterial;
    Mesh _lightQuad;
    Vector3 _lightPosition;
    float _lightSize;
    bool _warnedNoLightShader;

    GameObject _overlay;
    Image _veil;

    PP.PostProcessVolume _volume;
    PP.LensDistortion _lens;

    float _fovMultiplier = 1f;

    /// <summary>True from Begin until End or Abort.</summary>
    public bool IsActive { get; private set; }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (worlds == null) worlds = FindObjectOfType<WorldSwitcher_NEW>();
        if (brain == null) brain = FindObjectOfType<CinemachineBrain>();
        if (state == null) state = FindObjectOfType<LayerState_NEW>();
        if (player == null) player = FindObjectOfType<PlayerRig_NEW>();

        foreach (LayerGate_NEW gate in FindObjectsOfType<LayerGate_NEW>())
            if (gate != null && !string.IsNullOrEmpty(gate.LayerId) && !_gatesByLayer.ContainsKey(gate.LayerId))
                _gatesByLayer.Add(gate.LayerId, gate);

        if (worlds == null)
            Debug.LogWarning("[LayerDive_NEW] No WorldSwitcher_NEW in the scene; a dive will " +
                             "play its light but cannot move or fade the worlds.", this);
    }

    void OnDisable()
    {
        Abort();
        HideLight();
    }

    void OnDestroy() => DestroyLight();

    /// <summary>The approach: the light in the cluster ahead, before the gate is reached.</summary>
    void Update()
    {
        if (IsActive) return;   // the dive draws the light itself

        if (TryApproach(out Dive dive, out Vector3 focus, out float closeness))
            SetLight(dive, focus, Mathf.Lerp(dive.pointSize, dive.gateSize, closeness), Smooth(0f, 0.4f, closeness));
        else
            HideLight();
    }

    void LateUpdate()
    {
        if (_light != null && _light.activeSelf) PlaceLight();
    }

    // ── Called by CameraDirector_NEW ─────────────────────────────────────────

    /// <summary>The enabled row for the gate into <paramref name="toLayerId"/>, if any.</summary>
    public bool TryGetDive(string toLayerId, out Dive dive)
    {
        for (int i = 0; i < gates.Length; i++)
        {
            Dive d = gates[i];
            if (d != null && d.enabled && string.Equals(d.toLayerId, toLayerId, StringComparison.Ordinal))
            {
                dive = d;
                return true;
            }
        }

        dive = null;
        return false;
    }

    public void Begin(LayerProfile_NEW from, LayerProfile_NEW to, Dive dive)
    {
        if (IsActive) Abort();

        _dive = dive;
        _fromId = from != null ? from.layerId : null;
        _toId = to.layerId;
        _camera = brain != null ? brain.OutputCamera : Camera.main;
        IsActive = true;

        if (worlds != null) worlds.Hold(this);

        _focus = DiveFocus();
        _emergeFocus = NextWorldCentre();
        _leave = Capture(worlds != null ? worlds.GroupRoot(_fromId) : null);
        _enter = Capture(worlds != null ? worlds.GroupRoot(_toId) : null);

        // The next world stays out of sight until the peak.
        if (worlds != null) worlds.SetGroupLook(_toId, 0f);

        BuildOverlay();
        BuildEffects();
        CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);

        if (debugLog)
            Debug.Log($"[LayerDive_NEW] '{_fromId}' -> '{_toId}', diving into {_focus}, " +
                      $"the next world appears at {_emergeFocus}.", this);

        TickDive(0f);
    }

    /// <summary>The old world opening up and dissolving into light. <paramref name="u"/> runs 0 to 1.</summary>
    public void TickDive(float u)
    {
        if (!IsActive) return;
        u = Mathf.Clamp01(u);

        ScaleAround(_leave, _focus, Mathf.Pow(_dive.diveZoom, SteadyRamp(u)));

        float dissolve = Smooth(_dive.dissolveFrom, 1f, u);
        float glow = Mathf.Lerp(1f, _dive.leaveGlow, Smooth(0f, 0.85f, u));
        if (worlds != null) worlds.SetGroupLook(_fromId, 1f - dissolve, glow);

        // The light swells steadily from its size at the gate, and hands over to the
        // whiteout as the camera arrives at it: a quad at the camera would cut through the
        // near plane.
        float size = Mathf.Lerp(_dive.gateSize, _dive.peakSize, SteadyRamp(u));
        float arriving = _camera != null ? Smooth(1.5f, 6f, Vector3.Distance(_camera.transform.position, _focus)) : 1f;
        SetLight(_dive, _focus, size, arriving);
        SetVeil(_dive.whiteout * Smooth(0.7f, 1f, u));

        _fovMultiplier = Mathf.Lerp(1f, _dive.fieldOfViewScale, Smooth(0f, 1f, u));
        SetEffects(Smooth(0.2f, 1f, u));
    }

    /// <summary>The peak: the old world goes back where it was, out of sight, and the new one is readied.</summary>
    public void Crossover()
    {
        if (!IsActive) return;

        Restore(_leave);
        if (worlds != null) worlds.SetGroupLook(_fromId, 0f);

        ScaleAround(_enter, _emergeFocus, _dive.enterScale);
        if (worlds != null) worlds.SetGroupLook(_toId, 0f);

        // Under the whiteout, the light moves to where the next world appears.
        SetVeil(_dive.whiteout);
        SetLight(_dive, _emergeFocus, _dive.peakSize * 0.5f, 1f);
    }

    /// <summary>The next world appearing around the light. <paramref name="v"/> runs 0 to 1.</summary>
    public void TickEmerge(float v)
    {
        if (!IsActive) return;
        v = Mathf.Clamp01(v);

        float settle = EaseOut(v);

        // In log space, so growing from a hundredth reads as evenly as growing from a half.
        ScaleAround(_enter, _emergeFocus, Mathf.Pow(_dive.enterScale, 1f - settle));

        if (worlds != null) worlds.SetGroupLook(_toId, Smooth(0f, 0.7f, v));

        SetLight(_dive, _emergeFocus, Mathf.Lerp(_dive.peakSize * 0.5f, _dive.pointSize, settle), 1f - Smooth(0.3f, 1f, v));
        SetVeil(_dive.whiteout * (1f - Smooth(0f, 0.45f, v)));

        _fovMultiplier = Mathf.Lerp(_dive.fieldOfViewScale, 1f, Smooth(0f, 1f, v));
        SetEffects(1f - Smooth(0f, 0.9f, v));
    }

    /// <summary>Finished: hand the worlds back and remove everything the dive made.</summary>
    public void End()
    {
        if (!IsActive) return;
        if (debugLog) Debug.Log($"[LayerDive_NEW] Into '{_toId}'.", this);
        Cleanup();
    }

    /// <summary>
    /// Stopped part-way — another gate, the object disabled, play mode ending. Puts every
    /// world back to its built transform and lets WorldSwitcher_NEW settle what shows.
    /// </summary>
    public void Abort()
    {
        if (!IsActive) return;
        if (debugLog) Debug.Log($"[LayerDive_NEW] Dive into '{_toId}' stopped part-way.", this);
        Cleanup();
    }

    void Cleanup()
    {
        Restore(_leave);
        Restore(_enter);
        _leave = null;
        _enter = null;

        CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
        _fovMultiplier = 1f;

        DestroyOverlay();
        DestroyEffects();
        HideLight();

        IsActive = false;

        if (worlds != null) worlds.Release(this, _toId);
    }

    // ── Where ────────────────────────────────────────────────────────────────

    /// <summary>The row whose gate the photon is closing on, within its approach distance.</summary>
    bool TryApproach(out Dive dive, out Vector3 focus, out float closeness)
    {
        dive = null;
        focus = default;
        closeness = 0f;
        if (player == null) return false;

        string current = state != null ? state.CurrentLayerId : null;

        for (int i = 0; i < gates.Length; i++)
        {
            Dive d = gates[i];
            if (d == null || !d.enabled || d.approachDistance <= 0f) continue;
            if (string.Equals(current, d.toLayerId, StringComparison.Ordinal)) continue;   // already through
            if (!_gatesByLayer.TryGetValue(d.toLayerId, out LayerGate_NEW gate) || gate == null) continue;

            float before = Vector3.Dot(GateCentre(gate) - player.transform.position, TravelDirection());
            if (before <= 0f || before > d.approachDistance) continue;

            dive = d;
            focus = FocusFor(d, gate);
            closeness = 1f - before / d.approachDistance;
            return true;
        }

        return false;
    }

    Vector3 DiveFocus()
    {
        if (_dive.focusOverride != null) return _dive.focusOverride.position;

        if (_gatesByLayer.TryGetValue(_toId, out LayerGate_NEW gate) && gate != null)
            return FocusFor(_dive, gate);

        // No trigger to measure from — a jump straight into the layer. Straight ahead.
        if (player != null) return player.transform.position + TravelDirection() * _dive.focusPastGate;
        return _camera != null ? _camera.transform.position + _camera.transform.forward * _dive.focusPastGate : transform.position;
    }

    Vector3 FocusFor(Dive d, LayerGate_NEW gate)
    {
        if (d.focusOverride != null) return d.focusOverride.position;
        return GateCentre(gate) + TravelDirection() * d.focusPastGate;
    }

    /// <summary>The light's straight line: the photon's heading.</summary>
    Vector3 TravelDirection()
    {
        Vector3 dir = player != null ? player.MovementDirection : Vector3.forward;
        return dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward;
    }

    static Vector3 GateCentre(LayerGate_NEW gate)
    {
        Collider c = gate.GetComponent<Collider>();
        return c != null ? c.bounds.center : gate.transform.position;
    }

    /// <summary>
    /// The middle of what the next world will show. Active objects only: groups keep
    /// disabled prototypes around, and those would pull the point off the real thing.
    /// </summary>
    Vector3 NextWorldCentre()
    {
        Transform root = worlds != null ? worlds.GroupRoot(_toId) : null;
        if (root == null) return _focus;

        Vector3 sum = Vector3.zero;
        int n = 0;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(false))
        {
            sum += r.transform.position;
            n++;
        }

        return n > 0 ? sum / n : root.position;
    }

    // ── Scaling a world around a point ───────────────────────────────────────

    static Scaled Capture(Transform root)
    {
        if (root == null) return null;
        return new Scaled { root = root, position = root.position, localScale = root.localScale };
    }

    static void ScaleAround(Scaled s, Vector3 pivot, float k)
    {
        if (s == null || s.root == null) return;

        // Prepared on the first real scale, while the root is still where it was built —
        // the parents' scale has to be measured at 1.
        if (!s.particlesPrepared && !Mathf.Approximately(k, 1f)) PrepareParticles(s);

        s.root.position = pivot + (s.position - pivot) * k;
        s.root.localScale = s.localScale * k;
    }

    /// <summary>See the class summary: Local-scaling systems follow the root for the length of a scale.</summary>
    static void PrepareParticles(Scaled s)
    {
        s.particlesPrepared = true;

        foreach (ParticleSystem ps in s.root.GetComponentsInChildren<ParticleSystem>(true))
        {
            Transform t = ps.transform;
            ParticleSystem.MainModule main = ps.main;

            // Leaves only: resizing a transform that has children would move them.
            if (main.scalingMode != ParticleSystemScalingMode.Local || t.childCount > 0) continue;

            Vector3 parent = t.parent != null ? t.parent.lossyScale : Vector3.one;
            if (Mathf.Abs(parent.x) < 1e-6f || Mathf.Abs(parent.y) < 1e-6f || Mathf.Abs(parent.z) < 1e-6f)
                continue;

            s.particles.Add(new ParticleState { system = ps, localScale = t.localScale, mode = main.scalingMode });

            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            t.localScale = new Vector3(t.localScale.x / parent.x, t.localScale.y / parent.y, t.localScale.z / parent.z);
        }
    }

    static void Restore(Scaled s)
    {
        if (s == null || s.root == null) return;

        s.root.position = s.position;
        s.root.localScale = s.localScale;

        for (int i = 0; i < s.particles.Count; i++)
        {
            ParticleState p = s.particles[i];
            if (p.system == null) continue;

            ParticleSystem.MainModule main = p.system.main;
            main.scalingMode = p.mode;
            p.system.transform.localScale = p.localScale;
        }

        s.particles.Clear();
        s.particlesPrepared = false;
    }

    // ── The light ────────────────────────────────────────────────────────────

    void SetLight(Dive d, Vector3 at, float size, float fade)
    {
        EnsureLight();
        if (_light == null) return;

        float intensity = d.lightIntensity * Mathf.Clamp01(fade);
        bool on = intensity > 0.001f && size > 0f;
        if (_light.activeSelf != on) _light.SetActive(on);
        if (!on) return;

        _lightPosition = at;
        _lightSize = size;
        _lightMaterial.SetColor(ColorId, new Color(d.lightColor.r, d.lightColor.g, d.lightColor.b, intensity));
        PlaceLight();
    }

    void HideLight()
    {
        if (_light != null && _light.activeSelf) _light.SetActive(false);
    }

    /// <summary>Faces the camera. Round, so a frame of lag in the facing never shows.</summary>
    void PlaceLight()
    {
        Camera cam = _camera != null ? _camera : (brain != null ? brain.OutputCamera : Camera.main);

        Transform t = _light.transform;
        t.position = _lightPosition;
        t.localScale = new Vector3(_lightSize, _lightSize, _lightSize);
        if (cam != null) t.rotation = cam.transform.rotation;
    }

    void EnsureLight()
    {
        if (_light != null) return;

        Shader shader = Resources.Load<Shader>("DiveGlow_NEW");
        if (shader == null)
        {
            if (!_warnedNoLightShader)
                Debug.LogWarning("[LayerDive_NEW] Resources/DiveGlow_NEW.shader is missing; the dive " +
                                 "plays without its light.", this);
            _warnedNoLightShader = true;
            return;
        }

        _lightMaterial = new Material(shader) { name = "DiveGlow (runtime)" };

        _lightQuad = new Mesh { name = "DiveLight quad" };
        _lightQuad.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f)
        };
        _lightQuad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
        _lightQuad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        _lightQuad.RecalculateBounds();

        _light = new GameObject("DiveLight (temporary)");
        // The player's layer: whatever draws the photon draws this.
        if (player != null) _light.layer = player.gameObject.layer;

        _light.AddComponent<MeshFilter>().sharedMesh = _lightQuad;
        MeshRenderer mr = _light.AddComponent<MeshRenderer>();
        mr.sharedMaterial = _lightMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        _light.SetActive(false);
    }

    void DestroyLight()
    {
        if (_light != null) Destroy(_light);
        if (_lightMaterial != null) Destroy(_lightMaterial);
        if (_lightQuad != null) Destroy(_lightQuad);
        _light = null;
        _lightMaterial = null;
        _lightQuad = null;
    }

    // ── Whiteout ─────────────────────────────────────────────────────────────

    void BuildOverlay()
    {
        _overlay = new GameObject("DiveWhiteout (temporary)", typeof(RectTransform));

        Canvas canvas = _overlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = overlaySortOrder;

        GameObject go = new GameObject("Veil", typeof(RectTransform));
        go.transform.SetParent(_overlay.transform, false);

        _veil = go.AddComponent<Image>();
        _veil.raycastTarget = false;
        _veil.color = Color.clear;

        RectTransform rt = _veil.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    void SetVeil(float alpha)
    {
        if (_veil == null) return;
        Color c = _dive.lightColor;
        _veil.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(alpha));
    }

    void DestroyOverlay()
    {
        if (_overlay != null) Destroy(_overlay);
        _overlay = null;
        _veil = null;
    }

    // ── Lens ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Scales the brain's field of view. Runs right after the brain has written the lens,
    /// which it does from scratch every time, so this never accumulates and composes with
    /// JourneyZoom_NEW doing the same.
    /// </summary>
    void OnCameraUpdated(CinemachineBrain updated)
    {
        if (brain != null && updated != brain) return;
        if (Mathf.Approximately(_fovMultiplier, 1f)) return;

        Camera cam = updated.OutputCamera;
        if (cam == null || cam.orthographic) return;

        cam.fieldOfView = Mathf.Clamp(cam.fieldOfView * _fovMultiplier, 1f, 179f);
    }

    void BuildEffects()
    {
        if (_camera == null) return;

        PP.PostProcessLayer layer = _camera.GetComponent<PP.PostProcessLayer>();
        if (layer == null || !layer.enabled) return;

        // The volume has to sit on a layer the camera listens to.
        int mask = layer.volumeLayer.value;
        if (mask == 0) return;

        int bit = 0;
        while (bit < 31 && (mask & (1 << bit)) == 0) bit++;

        var settings = new List<PP.PostProcessEffectSettings>();

        if (_dive.peakBloom > 0f)
        {
            PP.Bloom bloom = ScriptableObject.CreateInstance<PP.Bloom>();
            bloom.enabled.Override(true);
            bloom.intensity.Override(_dive.peakBloom);
            settings.Add(bloom);
        }

        if (_dive.peakChromaticAberration > 0f)
        {
            PP.ChromaticAberration chroma = ScriptableObject.CreateInstance<PP.ChromaticAberration>();
            chroma.enabled.Override(true);
            chroma.intensity.Override(_dive.peakChromaticAberration);
            settings.Add(chroma);
        }

        if (!Mathf.Approximately(_dive.peakLensDistortion, 0f))
        {
            _lens = ScriptableObject.CreateInstance<PP.LensDistortion>();
            _lens.enabled.Override(true);
            _lens.intensity.Override(_dive.peakLensDistortion);
            _lens.centerX.Override(0f);
            _lens.centerY.Override(0f);
            settings.Add(_lens);
        }

        if (settings.Count == 0) return;

        _volume = PP.PostProcessManager.instance.QuickVolume(bit, 100f, settings.ToArray());
        _volume.weight = 0f;
    }

    void SetEffects(float weight)
    {
        if (_volume == null) return;

        _volume.weight = Mathf.Clamp01(weight);

        // Distort around the point being dived into, not the middle of the screen.
        if (_lens != null && _camera != null)
        {
            Vector3 vp = _camera.WorldToViewportPoint(_focus);
            if (vp.z > 0f)
            {
                _lens.centerX.value = Mathf.Clamp(vp.x * 2f - 1f, -1f, 1f);
                _lens.centerY.value = Mathf.Clamp(vp.y * 2f - 1f, -1f, 1f);
            }
        }
    }

    void DestroyEffects()
    {
        if (_volume != null) PP.RuntimeUtilities.DestroyVolume(_volume, true, true);
        _volume = null;
        _lens = null;
    }

    // ── Curves ───────────────────────────────────────────────────────────────

    /// <summary>
    /// 0 to 1 at a constant rate after a short ease-in, so the magnification — exponential
    /// in this — grows by the same factor every second. The first version squared time
    /// here, which is what read as a sudden rush.
    /// </summary>
    static float SteadyRamp(float u)
    {
        const float easeIn = 0.12f;
        u = Mathf.Clamp01(u);
        float p = u < easeIn ? u * u / (2f * easeIn) : u - easeIn * 0.5f;
        return p / (1f - easeIn * 0.5f);
    }

    /// <summary>Smoothstep of x between from and to.</summary>
    static float Smooth(float from, float to, float x)
    {
        float t = Mathf.Clamp01((x - from) / Mathf.Max(1e-5f, to - from));
        return t * t * (3f - 2f * t);
    }

    static float EaseOut(float x)
    {
        x = 1f - Mathf.Clamp01(x);
        return 1f - x * x * x;
    }
}

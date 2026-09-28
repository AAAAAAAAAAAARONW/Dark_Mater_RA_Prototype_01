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
/// a galaxy appears reads as a cut. The web could not even fade: its baked volumes have
/// no colour property, so WorldSwitcher_NEW could only switch it on and off (fixed there).
///
/// WHAT A DIVE DOES, on the gates listed below:
///
///   dive     The world being left grows around one point — exponentially in time², so
///            the magnification itself accelerates, a drift that ends as a rush — while
///            it brightens and then dissolves. A core of light at that point grows with
///            it until the view is light. The lens widens as the world streams past.
///   peak     Under the light, CoverReached fires: sky, speed and spectrum change, and
///            the next zone camera goes live. This is the frame the cover camera used to
///            provide, so every responder works unchanged.
///   emerge   The light condenses back into the point it came from and the next world
///            fades in around it, while the lens settles from wide to normal — which
///            magnifies the new world as it appears.
///
/// The point defaults to the middle of the next world, so the galaxy appears exactly
/// where the web was rushing towards: the web's one small part becoming the galaxy.
///
/// WHO OWNS WHAT. CameraDirector_NEW still owns time: it runs the clock, fires the anchors
/// and moves the cameras, and asks this component only how the frame looks at a given
/// progress. The render groups are borrowed from WorldSwitcher_NEW for the length of the
/// dive (Hold / Release), so its anchor-driven swap cannot snap a world that is halfway
/// through fading.
///
/// SCALING A WORLD. The baked volumes scale cleanly. Particle systems in "Local" scaling
/// mode ignore their parents' scale, so for the length of a scale they are switched to
/// "Hierarchy" with the parents divided back out — same size at scale 1, and they follow
/// the root. Systems that simulate in world space cannot follow a moving root at all and
/// only fade; that is why enterScale defaults to 1 (fade only) — the galaxy is mostly
/// world-space particles.
///
/// TRYING IT. One row ships enabled, for the gate into Micro — the first galaxy. Add rows
/// to dive on other gates; a gate with no row keeps the cover transition. The start
/// layer's first gate always keeps its look-back handover.
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

        [Tooltip("The point to dive into, in the world being left.\n\n" +
                 "EMPTY = the middle of the next world's visible objects, so the next world " +
                 "grows out of exactly the place the old one rushes towards. Set it to put the " +
                 "dive on a particular knot of the web — the next world then fades in where it " +
                 "was built, with the light condensing there.")]
        public Transform focus;

        [Header("Timing (seconds)")]
        [Tooltip("From the gate to the peak: the old world rushing in and dissolving into light.")]
        [Min(0.1f)] public float diveSeconds = 2.4f;

        [Tooltip("Held at full light. The swap, the sky, and the camera change happen at its start.")]
        [Min(0f)] public float peakHoldSeconds = 0.2f;

        [Tooltip("From the peak to the end: the light condensing and the new world appearing.")]
        [Min(0.1f)] public float emergeSeconds = 2.8f;

        [Header("The world being left")]
        [Tooltip("Scale the old world reaches around the point by the peak.\n\n" +
                 "Above 1 dives IN (the web opens up past the camera). Below 1 pulls OUT " +
                 "(the old world shrinks into the point) — for the gates that go back up in " +
                 "scale, galaxy to web.")]
        [Min(0.01f)] public float leaveScale = 40f;

        [Tooltip("How much brighter the old world glows at the peak, just before it is gone. " +
                 "Multiplies _Emission on the baked volumes and emission colours elsewhere.")]
        [Min(0f)] public float leaveGlow = 2.5f;

        [Tooltip("Fraction of the dive at which the old world starts to dissolve. Earlier " +
                 "leaves more of the rush to the light; later shows more of the web opening.")]
        [Range(0f, 1f)] public float dissolveFrom = 0.5f;

        [Header("The world being entered")]
        [Tooltip("Scale the new world starts at around the point, settling to 1 as it appears.\n\n" +
                 "1 = fade in only. Below 1 grows out of the point; above 1 closes in from " +
                 "around the camera. World-space particle systems cannot follow a scale and " +
                 "only fade — see the class summary.")]
        [Min(0.01f)] public float enterScale = 1f;

        [Header("Light")]
        [Tooltip("Colour of the core and of the full-screen light at the peak.")]
        public Color lightColor = new Color(1f, 0.94f, 0.88f, 1f);

        [Tooltip("Diameter of the core as the dive starts, in screen heights.")]
        [Min(0f)] public float coreStartSize = 0.06f;

        [Tooltip("Diameter the core condenses back to at the end, in screen heights.")]
        [Min(0f)] public float coreEndSize = 0.12f;

        [Tooltip("Opacity of the full-screen light at the peak. 1 hides the swap completely; " +
                 "lower lets both worlds show through each other for a moment.")]
        [Range(0f, 1f)] public float whiteout = 1f;

        [Header("Lens (blended over the scene's own post-processing)")]
        [Tooltip("Field of view multiplier at the peak. Above 1 widens as the world rushes past, " +
                 "which reads as speed; settling back to 1 magnifies the new world as it appears.")]
        [Range(0.5f, 2f)] public float fovKick = 1.3f;

        [Tooltip("Bloom intensity at the peak. The scene's own is 0.8.")]
        [Min(0f)] public float peakBloom = 5f;

        [Range(0f, 1f)] public float peakChromaticAberration = 0.6f;

        [Tooltip("Lens distortion at the peak, centred on the point. Positive bulges the " +
                 "point towards the viewer; negative pulls the edges in.")]
        [Range(-100f, 100f)] public float peakLensDistortion = 30f;
    }

    [Tooltip("One row per gate that dives. Matched on the layer being entered.")]
    [SerializeField] Dive[] dives = { new Dive() };

    [Header("Wiring (found if empty)")]
    [SerializeField] WorldSwitcher_NEW worlds;
    [SerializeField] CinemachineBrain brain;

    [Header("Overlay")]
    [Tooltip("Sort order of the light. Below the HUD canvases (0) keeps the HUD readable " +
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

    Dive _dive;
    string _fromId;
    string _toId;
    Vector3 _focus;
    Scaled _leave;
    Scaled _enter;
    Camera _camera;

    GameObject _overlay;
    RectTransform _core;
    Image _coreImage;
    Image _veil;
    float _coverSize;

    PP.PostProcessVolume _volume;
    PP.LensDistortion _lens;

    float _fovMultiplier = 1f;

    static Sprite s_coreSprite;

    /// <summary>True from Begin until End or Abort.</summary>
    public bool IsActive { get; private set; }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (worlds == null) worlds = FindObjectOfType<WorldSwitcher_NEW>();
        if (brain == null) brain = FindObjectOfType<CinemachineBrain>();

        if (worlds == null)
            Debug.LogWarning("[LayerDive_NEW] No WorldSwitcher_NEW in the scene; a dive will " +
                             "play its light but cannot move or fade the worlds.", this);
    }

    void OnDisable() => Abort();

    // ── Called by CameraDirector_NEW ─────────────────────────────────────────

    /// <summary>The enabled row for the gate into <paramref name="toLayerId"/>, if any.</summary>
    public bool TryGetDive(string toLayerId, out Dive dive)
    {
        for (int i = 0; i < dives.Length; i++)
        {
            Dive d = dives[i];
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

        _focus = ResolveFocus();
        _leave = Capture(worlds != null ? worlds.GroupRoot(_fromId) : null);
        _enter = Capture(worlds != null ? worlds.GroupRoot(_toId) : null);

        // The next world stays out of sight until the peak.
        if (worlds != null) worlds.SetGroupLook(_toId, 0f);

        BuildOverlay();
        BuildEffects();
        CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);

        if (debugLog)
            Debug.Log($"[LayerDive_NEW] '{_fromId}' -> '{_toId}', diving into {_focus} " +
                      $"({(dive.focus != null ? dive.focus.name : "middle of the next world")}).", this);

        TickDive(0f);
    }

    /// <summary>The old world rushing in and dissolving. <paramref name="u"/> runs 0 to 1.</summary>
    public void TickDive(float u)
    {
        if (!IsActive) return;
        u = Mathf.Clamp01(u);

        ScaleAround(_leave, Mathf.Pow(_dive.leaveScale, u * u));

        float dissolve = Smooth(_dive.dissolveFrom, 1f, u);
        float glow = Mathf.Lerp(1f, _dive.leaveGlow, Smooth(0f, 0.85f, u));
        if (worlds != null) worlds.SetGroupLook(_fromId, 1f - dissolve, glow);

        // The core stays a point while the web is visibly opening and only floods the view
        // at the very end (u⁴), and the veil comes in late for the same reason: the rush
        // between half and nine-tenths of the dive is the part worth seeing.
        float core = Mathf.Lerp(_dive.coreStartSize, _coverSize, Mathf.Pow(u, 4f));
        float coreAlpha = Smooth(0f, 0.4f, u) * Mathf.Lerp(0.85f, 1f, Smooth(0.7f, 1f, u));
        DrawLight(core, coreAlpha, _dive.whiteout * Smooth(0.72f, 1f, u));

        _fovMultiplier = Mathf.Lerp(1f, _dive.fovKick, Smooth(0f, 1f, u));
        SetEffects(Smooth(0.1f, 1f, u));
    }

    /// <summary>The peak: the old world goes back where it was, out of sight, and the new one is readied.</summary>
    public void Crossover()
    {
        if (!IsActive) return;

        Restore(_leave);
        if (worlds != null) worlds.SetGroupLook(_fromId, 0f);

        ScaleAround(_enter, _dive.enterScale);
        if (worlds != null) worlds.SetGroupLook(_toId, 0f);

        DrawLight(_coverSize, 1f, _dive.whiteout);
    }

    /// <summary>The light condensing and the new world appearing. <paramref name="v"/> runs 0 to 1.</summary>
    public void TickEmerge(float v)
    {
        if (!IsActive) return;
        v = Mathf.Clamp01(v);

        float settle = EaseOut(v);

        // In log space, so growing from a hundredth reads as evenly as growing from a half.
        ScaleAround(_enter, Mathf.Pow(_dive.enterScale, 1f - settle));

        if (worlds != null) worlds.SetGroupLook(_toId, Smooth(0f, 0.7f, v));

        float core = Mathf.Lerp(_coverSize, _dive.coreEndSize, settle);
        DrawLight(core, 1f - Smooth(0.3f, 1f, v), _dive.whiteout * (1f - Smooth(0f, 0.45f, v)));

        _fovMultiplier = Mathf.Lerp(_dive.fovKick, 1f, Smooth(0f, 1f, v));
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

        IsActive = false;

        if (worlds != null) worlds.Release(this, _toId);
    }

    // ── Where to dive ────────────────────────────────────────────────────────

    Vector3 ResolveFocus()
    {
        if (_dive.focus != null) return _dive.focus.position;

        // The middle of what the next world will show. Active objects only: groups keep
        // disabled prototypes around, and those would pull the point off the real thing.
        Transform root = worlds != null ? worlds.GroupRoot(_toId) : null;
        if (root != null)
        {
            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(false))
            {
                sum += r.transform.position;
                n++;
            }

            return n > 0 ? sum / n : root.position;
        }

        if (_camera != null) return _camera.transform.position + _camera.transform.forward * 60f;
        return transform.position;
    }

    // ── Scaling a world around the point ─────────────────────────────────────

    static Scaled Capture(Transform root)
    {
        if (root == null) return null;
        return new Scaled { root = root, position = root.position, localScale = root.localScale };
    }

    void ScaleAround(Scaled s, float k)
    {
        if (s == null || s.root == null) return;

        // Prepared on the first real scale, while the root is still where it was built —
        // the parents' scale has to be measured at 1.
        if (!s.particlesPrepared && !Mathf.Approximately(k, 1f)) PrepareParticles(s);

        s.root.position = _focus + (s.position - _focus) * k;
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

    // ── Lens ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Widens the brain's field of view by the dive's kick. Runs right after the brain has
    /// written the lens, which it does from scratch every time, so this never accumulates
    /// and composes with JourneyZoom_NEW doing the same.
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

        PP.Bloom bloom = ScriptableObject.CreateInstance<PP.Bloom>();
        bloom.enabled.Override(true);
        bloom.intensity.Override(_dive.peakBloom);

        PP.ChromaticAberration chroma = ScriptableObject.CreateInstance<PP.ChromaticAberration>();
        chroma.enabled.Override(true);
        chroma.intensity.Override(_dive.peakChromaticAberration);

        _lens = ScriptableObject.CreateInstance<PP.LensDistortion>();
        _lens.enabled.Override(true);
        _lens.intensity.Override(_dive.peakLensDistortion);
        _lens.centerX.Override(0f);
        _lens.centerY.Override(0f);

        _volume = PP.PostProcessManager.instance.QuickVolume(bit, 100f, bloom, chroma, _lens);
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

    // ── Light ────────────────────────────────────────────────────────────────

    void BuildOverlay()
    {
        _overlay = new GameObject("DiveLight (temporary)", typeof(RectTransform));

        Canvas canvas = _overlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = overlaySortOrder;

        // Veil first so the core draws over it.
        _veil = NewImage("Veil", null);
        RectTransform veil = _veil.rectTransform;
        veil.anchorMin = Vector2.zero;
        veil.anchorMax = Vector2.one;
        veil.offsetMin = Vector2.zero;
        veil.offsetMax = Vector2.zero;

        _coreImage = NewImage("Core", CoreSprite());
        _core = _coreImage.rectTransform;
        _core.anchorMin = Vector2.zero;
        _core.anchorMax = Vector2.zero;
        _core.pivot = new Vector2(0.5f, 0.5f);

        // Big enough to spill past every corner from anywhere on screen.
        float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
        _coverSize = 2.5f * Mathf.Sqrt(aspect * aspect + 1f);
    }

    Image NewImage(string name, Sprite sprite)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(_overlay.transform, false);

        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.color = Color.clear;
        return image;
    }

    /// <param name="size">Core diameter in screen heights.</param>
    void DrawLight(float size, float coreAlpha, float veilAlpha)
    {
        if (_overlay == null) return;

        Color c = _dive.lightColor;
        _veil.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(veilAlpha));

        // The core sits on the point wherever it is on screen, and hides if it falls
        // behind the camera rather than mirroring through.
        bool onScreen = false;
        if (_camera != null)
        {
            Vector3 sp = _camera.WorldToScreenPoint(_focus);
            if (sp.z > 0f)
            {
                float px = Mathf.Max(0f, size) * Screen.height;
                _core.anchoredPosition = new Vector2(sp.x, sp.y);
                _core.sizeDelta = new Vector2(px, px);
                onScreen = true;
            }
        }

        _coreImage.color = new Color(c.r, c.g, c.b, onScreen ? Mathf.Clamp01(coreAlpha) : 0f);
    }

    void DestroyOverlay()
    {
        if (_overlay != null) Destroy(_overlay);
        _overlay = null;
        _core = null;
        _coreImage = null;
        _veil = null;
    }

    /// <summary>A soft radial glow, made once. Drawn by the default UI shader, which every build has.</summary>
    static Sprite CoreSprite()
    {
        if (s_coreSprite != null) return s_coreSprite;

        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "DiveCore (generated)",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float a = Smooth(0f, 1f, 1f - Mathf.Sqrt(dx * dx + dy * dy));
                a = Mathf.Pow(a, 1.5f);   // brighter, tighter middle
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);

        s_coreSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        return s_coreSprite;
    }

    // ── Curves ───────────────────────────────────────────────────────────────

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

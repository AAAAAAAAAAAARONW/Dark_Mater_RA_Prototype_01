using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shows exactly one render group and hides the rest.
///
/// Replaces RenderLayerTransitionController plus the forwarding half of
/// VisualLayerResponder, which was a coroutine wrapper that never yielded.
///
/// The MaterialPropertyBlock approach is carried over unchanged — it was the one
/// piece of material handling the old codebase got right. It overrides colours
/// per-renderer without instantiating a single material, so batching survives and
/// nothing on disk is touched. Shaders and material assets are never modified.
///
/// Fixes relative to the original:
///   * SetImmediately() warns when no group matches instead of silently blanking the
///     entire world. That failure mode is what an unlisted layerId used to produce.
///   * Material slot counts are cached, so a crossfade no longer allocates a fresh
///     array from Renderer.sharedMaterials for every renderer every frame.
///   * Optional particle-system suspension for hidden groups. Disabling a
///     ParticleSystemRenderer stops it drawing but the system keeps simulating;
///     this scene carries ~72 of them across six groups. Off by default because
///     turning it on changes CPU behaviour, which is not a like-for-like swap.
///
/// LATER: THE COSMIC WEB COULD NOT FADE. Its volumes use Custom/PreBakedCloudVolume, which
/// has no colour property at all — only _Opacity and _Emission floats — so the alpha
/// written above reached none of it, and the web could only be switched on or off. Both
/// floats are now scaled too (alpha on _Opacity, glow on _Emission), which is what lets
/// LayerDive_NEW dissolve the web instead of cutting it.
///
/// LATER STILL: NOR COULD THE GALAXIES. Their particles use Mobile/Particles/Additive and
/// Alpha Blended, which declare nothing but a texture. So a galaxy stayed whole until its
/// alpha crossed disableThreshold and then went in one frame — the Milky Way vanished at
/// the peak of the dive into the Solar System instead of dissolving. For as long as such a
/// group is fading, those renderers now draw with a stand-in (Resources/FadeParticle_NEW)
/// that draws the same thing and can fade; at full alpha the originals are back. A dive can
/// also ask a group to fade the particles near the camera (SetGroupNearFade), and to keep
/// what is too small on screen to draw cleanly out of sight (SetGroupSizeFade).
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("WORLD", "#4A94F2")]
public class WorldSwitcher_NEW : LayerResponder_NEW
{
    [Serializable]
    public class RenderGroup
    {
        [Tooltip("Must match LayerProfile_NEW.layerId.")]
        public string layerId;

        [Tooltip("Every Renderer and Light beneath this transform belongs to the group.")]
        public Transform root;

        [Tooltip("Visible before the first swap. Set this on the start layer's group.")]
        public bool visibleOnStart = false;

        [HideInInspector] public List<CachedRenderer> renderers = new List<CachedRenderer>();
        [HideInInspector] public List<CachedLight> lights = new List<CachedLight>();
        [HideInInspector] public List<ParticleSystem> particles = new List<ParticleSystem>();
        [HideInInspector] public float currentAlpha;

        // Set for the length of a dive (SetGroupNearFade, SetGroupSizeFade); never saved.
        [NonSerialized] public float currentGlow = 1f;
        [NonSerialized] public Vector2 nearFade;
        [NonSerialized] public Camera sizeFadeCamera;
        [NonSerialized] public Vector2 sizeFadePixels;
    }

    public struct CachedRenderer
    {
        public Renderer renderer;
        public int slotCount;
        public bool[] hasColor, hasTint, hasBase, hasEmission;
        public Color[] baseColor, baseTint, baseBase, baseEmission;

        // Float-typed _Opacity / _Emission, as the baked cloud volumes declare them.
        public bool[] hasOpacity, hasEmissionScale;
        public float[] baseOpacity, baseEmissionScale;

        // Slots whose shader cannot fade, with a stand-in that can. Null if there are none.
        public StandIns standIns;
    }

    /// <summary>A renderer's own materials, the same with stand-ins where they cannot fade, and which it is drawing with.</summary>
    public class StandIns
    {
        public Material[] originals;
        public Material[] swapped;
        public bool[] isStandIn;
        public bool on;
    }

    public struct CachedLight
    {
        public Light light;
        public float baseIntensity;
    }

    [Header("Groups")]
    [SerializeField] RenderGroup[] groups = Array.Empty<RenderGroup>();

    [Header("Swap")]
    [Tooltip("Instant swap. The transition happens under the top-down cover, so a crossfade " +
             "is invisible work. This is the shipping configuration.")]
    [SerializeField] bool instantSwap = true;

    [Tooltip("Renderers below this alpha are disabled outright to avoid residual pixels.")]
    [Range(0f, 0.1f)]
    [SerializeField] float disableThreshold = 0.02f;

    [Header("Particles (opt-in — changes CPU behaviour)")]
    [Tooltip("Also pause ParticleSystems in hidden groups. Disabling a renderer stops drawing " +
             "but not simulating, so hidden groups keep costing CPU. Leave off for a " +
             "like-for-like swap; turn on once you have profiled the difference.")]
    [SerializeField] bool suspendHiddenParticles = false;

    MaterialPropertyBlock _block;
    Coroutine _fade;

    UnityEngine.Object _holder;
    string _heldLayerId;

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int TintColorId = Shader.PropertyToID("_TintColor");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    static readonly int OpacityId = Shader.PropertyToID("_Opacity");
    static readonly int EmissionScaleId = Shader.PropertyToID("_Emission");
    static readonly int NearFadeId = Shader.PropertyToID("_NearFade");
    static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
    static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");

    // One stand-in per original material, shared by every renderer that uses it.
    readonly Dictionary<Material, Material> _standIns = new Dictionary<Material, Material>();
    Shader _standInShader;
    bool _standInShaderLoaded;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        _block = new MaterialPropertyBlock();
        RebuildCaches();
        ApplyStartState();
    }

    void OnDestroy()
    {
        RestoreAll();
        foreach (Material m in _standIns.Values)
            if (m != null) Destroy(m);
        _standIns.Clear();
    }

    // ── Responder contract ───────────────────────────────────────────────────

    protected override TimedChannel_NEW SelectChannel(LayerProfile_NEW profile) => profile.timing.renderSwap;

    protected override void Apply(LayerProfile_NEW profile)
    {
        // A holder is animating the groups itself; remember the answer for Release.
        if (_holder != null)
        {
            _heldLayerId = profile.layerId;
            return;
        }

        if (instantSwap) SetImmediately(profile.layerId);
        else Crossfade(profile.layerId, profile.renderSwapDuration);
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Lend the groups to something that animates them itself — LayerDive_NEW, for the
    /// length of a dive. While held, the anchor-driven swap is recorded instead of
    /// applied, so it cannot snap a world the holder is halfway through fading.
    /// </summary>
    public void Hold(UnityEngine.Object holder)
    {
        if (_fade != null) { StopCoroutine(_fade); _fade = null; }
        _holder = holder;
        _heldLayerId = null;
    }

    /// <summary>
    /// End a Hold and settle every group: to the layer the anchors asked for while held,
    /// or to <paramref name="fallbackLayerId"/> if they asked for nothing (a dive stopped
    /// before its peak). Settling also puts every property block back to its base values.
    /// </summary>
    public void Release(UnityEngine.Object holder, string fallbackLayerId)
    {
        if (!ReferenceEquals(_holder, holder)) return;

        string layerId = !string.IsNullOrEmpty(_heldLayerId) ? _heldLayerId : fallbackLayerId;
        _holder = null;
        _heldLayerId = null;

        if (!string.IsNullOrEmpty(layerId)) SetImmediately(layerId);
    }

    /// <summary>How much of a layer's group is showing, 0 to 1 (0 if there is no such group).</summary>
    public float GroupAlpha(string layerId)
    {
        RenderGroup g = FindGroup(layerId);
        return g != null ? g.currentAlpha : 0f;
    }

    /// <summary>The root of a layer's group, or null if there is no such group.</summary>
    public Transform GroupRoot(string layerId)
    {
        RenderGroup g = FindGroup(layerId);
        return g != null ? g.root : null;
    }

    /// <summary>
    /// Draw one group at <paramref name="alpha"/>, leaving the others alone. Glow scales
    /// emission only — a world can brighten as it dissolves. For a holder mid-animation;
    /// everything else goes through SetImmediately or Crossfade.
    /// </summary>
    public void SetGroupLook(string layerId, float alpha, float glow = 1f)
    {
        RenderGroup g = FindGroup(layerId);
        if (g == null) return;

        g.currentAlpha = alpha;
        ApplyGroup(g, alpha, glow);
    }

    /// <summary>
    /// For the length of a dive through a world: its particles fade out as they come within
    /// <paramref name="to"/> of the camera, gone by <paramref name="from"/> (world units), so a
    /// magnified galaxy streaming past does not flash sprite by sprite as each reaches the
    /// near plane. Both 0 turns it off. Applied at once, at the group's current look — or, with
    /// <paramref name="applyNow"/> false, from the next look the group is given.
    /// </summary>
    public void SetGroupNearFade(string layerId, float from, float to, bool applyNow = true)
    {
        RenderGroup g = FindGroup(layerId);
        if (g == null) return;

        g.nearFade = to > from ? new Vector2(from, to) : Vector2.zero;
        if (applyNow) ApplyGroup(g, g.currentAlpha, g.currentGlow);
    }

    /// <summary>
    /// For the length of a dive that grows a world out of a point: each of its renderers is
    /// out of sight until it is <paramref name="fromPixels"/> across on
    /// <paramref name="camera"/>'s screen and fades in by <paramref name="toPixels"/>. A
    /// planet a pixel across lands on a pixel one frame and between pixels the next, and an
    /// emissive one under the bloom blinks. Null turns it off; it takes effect from the next
    /// look the group is given.
    /// </summary>
    public void SetGroupSizeFade(string layerId, Camera camera, float fromPixels, float toPixels)
    {
        RenderGroup g = FindGroup(layerId);
        if (g == null) return;

        g.sizeFadeCamera = camera;
        g.sizeFadePixels = new Vector2(fromPixels, Mathf.Max(fromPixels + 0.01f, toPixels));
    }

    public void SetImmediately(string layerId)
    {
        if (FindGroup(layerId) == null)
        {
            Debug.LogWarning($"[WorldSwitcher_NEW] No render group for layerId '{layerId}'. " +
                             "Keeping the current world rather than hiding everything. " +
                             "Add a group, or leave this deliberate if the layer is a cutscene.", this);
            return;
        }

        if (_fade != null) { StopCoroutine(_fade); _fade = null; }

        for (int i = 0; i < groups.Length; i++)
        {
            float target = string.Equals(groups[i].layerId, layerId, StringComparison.Ordinal) ? 1f : 0f;
            groups[i].currentAlpha = target;
            ApplyGroup(groups[i], target);
        }

        if (debugLog) Debug.Log($"[WorldSwitcher_NEW] Snapped to '{layerId}'.", this);
    }

    public void Crossfade(string layerId, float duration)
    {
        RenderGroup target = FindGroup(layerId);
        if (target == null)
        {
            Debug.LogWarning($"[WorldSwitcher_NEW] No render group for layerId '{layerId}'. " +
                             "Crossfade skipped.", this);
            return;
        }

        if (_fade != null) StopCoroutine(_fade);
        _fade = StartCoroutine(RunCrossfade(target, duration));
    }

    /// <summary>Re-scan every group root. Call after spawning content at runtime.</summary>
    public void RebuildCaches()
    {
        if (_block == null) _block = new MaterialPropertyBlock();

        for (int i = 0; i < groups.Length; i++)
        {
            RenderGroup g = groups[i];
            for (int j = 0; j < g.renderers.Count; j++) UseStandIns(g.renderers[j], false);
            g.renderers.Clear();
            g.lights.Clear();
            g.particles.Clear();

            if (g.root == null)
            {
                Debug.LogWarning($"[WorldSwitcher_NEW] Group '{g.layerId}' has no root.", this);
                continue;
            }

            CollectRenderers(g);
            g.root.GetComponentsInChildren(true, g.particles);

            foreach (Light l in g.root.GetComponentsInChildren<Light>(true))
                if (l != null) g.lights.Add(new CachedLight { light = l, baseIntensity = l.intensity });

            if (debugLog)
                Debug.Log($"[WorldSwitcher_NEW] '{g.layerId}': {g.renderers.Count} renderers, " +
                          $"{g.lights.Count} lights, {g.particles.Count} particle systems.", this);
        }
    }

    // ── Internals ────────────────────────────────────────────────────────────

    void ApplyStartState()
    {
        for (int i = 0; i < groups.Length; i++)
        {
            float a = groups[i].visibleOnStart ? 1f : 0f;
            groups[i].currentAlpha = a;
            ApplyGroup(groups[i], a);
        }
    }

    RenderGroup FindGroup(string layerId)
    {
        for (int i = 0; i < groups.Length; i++)
            if (string.Equals(groups[i].layerId, layerId, StringComparison.Ordinal))
                return groups[i];
        return null;
    }

    IEnumerator RunCrossfade(RenderGroup target, float duration)
    {
        float d = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        float[] start = new float[groups.Length];
        for (int i = 0; i < groups.Length; i++) start[i] = groups[i].currentAlpha;

        while (elapsed < d)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / d);
            float eased = t * t * (3f - 2f * t);

            for (int i = 0; i < groups.Length; i++)
            {
                float goal = ReferenceEquals(groups[i], target) ? 1f : 0f;
                float a = Mathf.Lerp(start[i], goal, eased);
                groups[i].currentAlpha = a;
                ApplyGroup(groups[i], a);
            }
            yield return null;
        }

        for (int i = 0; i < groups.Length; i++)
        {
            float goal = ReferenceEquals(groups[i], target) ? 1f : 0f;
            groups[i].currentAlpha = goal;
            ApplyGroup(groups[i], goal);
        }

        _fade = null;
    }

    void ApplyGroup(RenderGroup group, float alpha, float glow = 1f)
    {
        group.currentGlow = glow;
        bool nearFading = group.nearFade.y > group.nearFade.x;
        bool fading = alpha < 0.999f || Mathf.Abs(glow - 1f) > 1e-3f || nearFading;
        Camera sizeCamera = group.sizeFadeCamera;

        for (int i = 0; i < group.renderers.Count; i++)
        {
            CachedRenderer e = group.renderers[i];
            if (e.renderer == null) continue;

            float a = alpha;
            if (sizeCamera != null && a > disableThreshold) a *= SizeFade(e.renderer, sizeCamera, group.sizeFadePixels);

            bool visible = a > disableThreshold;
            e.renderer.enabled = visible;
            if (e.standIns != null) UseStandIns(e, visible && fading);
            if (!visible) continue;

            for (int m = 0; m < e.slotCount; m++)
            {
                _block.Clear();

                if (e.hasBase[m]) _block.SetColor(BaseColorId, e.baseBase[m] * a);
                if (e.hasColor[m]) _block.SetColor(ColorId, ScaleRGBA(e.baseColor[m], a));
                if (e.hasTint[m]) _block.SetColor(TintColorId, ScaleRGBA(e.baseTint[m], a));
                if (e.hasEmission[m]) _block.SetColor(EmissionColorId, e.baseEmission[m] * (a * glow));
                if (e.hasOpacity[m]) _block.SetFloat(OpacityId, e.baseOpacity[m] * a);
                if (e.hasEmissionScale[m]) _block.SetFloat(EmissionScaleId, e.baseEmissionScale[m] * glow);

                if (e.standIns != null && e.standIns.on && e.standIns.isStandIn[m])
                {
                    _block.SetColor(ColorId, new Color(glow, glow, glow, a));
                    _block.SetVector(NearFadeId, nearFading ? new Vector4(group.nearFade.x, group.nearFade.y, 0f, 0f) : Vector4.zero);
                }

                e.renderer.SetPropertyBlock(_block, m);
            }
        }

        bool groupVisible = alpha > disableThreshold;

        for (int i = 0; i < group.lights.Count; i++)
        {
            CachedLight e = group.lights[i];
            if (e.light == null) continue;
            e.light.intensity = e.baseIntensity * alpha;
            e.light.enabled = e.light.intensity > 0.001f;
        }

        if (!suspendHiddenParticles) return;

        for (int i = 0; i < group.particles.Count; i++)
        {
            ParticleSystem ps = group.particles[i];
            if (ps == null) continue;

            if (groupVisible)
            {
                if (!ps.isPlaying) ps.Play(false);
            }
            else if (ps.isPlaying)
            {
                ps.Pause(false);
            }
        }
    }

    void CollectRenderers(RenderGroup group)
    {
        foreach (Renderer r in group.root.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null) continue;

            Material[] mats = r.sharedMaterials;   // read-only access; never instantiates
            int n = mats.Length;

            CachedRenderer c = new CachedRenderer
            {
                renderer = r,
                slotCount = n,
                hasColor = new bool[n], baseColor = new Color[n],
                hasTint = new bool[n], baseTint = new Color[n],
                hasBase = new bool[n], baseBase = new Color[n],
                hasEmission = new bool[n], baseEmission = new Color[n],
                hasOpacity = new bool[n], baseOpacity = new float[n],
                hasEmissionScale = new bool[n], baseEmissionScale = new float[n],
            };

            for (int m = 0; m < n; m++)
            {
                Material mat = mats[m];
                if (mat == null) continue;

                if (mat.HasProperty(ColorId)) { c.hasColor[m] = true; c.baseColor[m] = mat.GetColor(ColorId); }
                if (mat.HasProperty(TintColorId)) { c.hasTint[m] = true; c.baseTint[m] = mat.GetColor(TintColorId); }
                if (mat.HasProperty(BaseColorId)) { c.hasBase[m] = true; c.baseBase[m] = mat.GetColor(BaseColorId); }
                if (mat.HasProperty(EmissionColorId)) { c.hasEmission[m] = true; c.baseEmission[m] = mat.GetColor(EmissionColorId); }
                if (HasFloat(mat, "_Opacity")) { c.hasOpacity[m] = true; c.baseOpacity[m] = mat.GetFloat(OpacityId); }
                if (HasFloat(mat, "_Emission")) { c.hasEmissionScale[m] = true; c.baseEmissionScale[m] = mat.GetFloat(EmissionScaleId); }

                bool canFade = c.hasColor[m] || c.hasTint[m] || c.hasBase[m] || c.hasEmission[m] || c.hasOpacity[m];
                Material standIn = canFade ? null : StandInFor(mat);
                if (standIn == null) continue;

                if (c.standIns == null)
                {
                    c.standIns = new StandIns
                    {
                        originals = (Material[])mats.Clone(),
                        swapped = (Material[])mats.Clone(),
                        isStandIn = new bool[n]
                    };
                }
                c.standIns.swapped[m] = standIn;
                c.standIns.isStandIn[m] = true;
            }

            group.renderers.Add(c);
        }
    }

    /// <summary>
    /// A material that draws what <paramref name="original"/> draws and can fade, for the
    /// simple particle shaders that cannot: an additive or alpha-blended texture times the
    /// particle's colour. Null for anything else — it is left to switch on and off.
    /// </summary>
    Material StandInFor(Material original)
    {
        if (original == null || original.shader == null) return null;
        if (_standIns.TryGetValue(original, out Material known)) return known;

        string name = original.shader.name;
        bool particles = name.IndexOf("Particles/", StringComparison.Ordinal) >= 0;
        bool additive = name.EndsWith("/Additive", StringComparison.Ordinal);
        bool blended = name.EndsWith("/Alpha Blended", StringComparison.Ordinal);

        Material standIn = null;
        if (particles && (additive || blended))
        {
            if (!_standInShaderLoaded)
            {
                _standInShader = Resources.Load<Shader>("FadeParticle_NEW");
                _standInShaderLoaded = true;
                if (_standInShader == null)
                    Debug.LogWarning("[WorldSwitcher_NEW] Resources/FadeParticle_NEW.shader is missing; " +
                                     "particle worlds will switch on and off instead of fading.", this);
            }

            if (_standInShader != null)
            {
                standIn = new Material(_standInShader)
                {
                    name = original.name + " (fade stand-in)",
                    renderQueue = original.renderQueue
                };
                if (original.HasProperty(MainTexId))
                {
                    standIn.SetTexture(MainTexId, original.GetTexture(MainTexId));
                    standIn.SetTextureScale(MainTexId, original.GetTextureScale(MainTexId));
                    standIn.SetTextureOffset(MainTexId, original.GetTextureOffset(MainTexId));
                }
                standIn.SetFloat(SrcBlendId, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                standIn.SetFloat(DstBlendId, (float)(additive ? UnityEngine.Rendering.BlendMode.One
                                                             : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            }
        }

        _standIns[original] = standIn;
        return standIn;
    }

    /// <summary>Draw with the stand-ins, or with the renderer's own materials.</summary>
    static void UseStandIns(CachedRenderer e, bool on)
    {
        StandIns s = e.standIns;
        if (s == null || s.on == on || e.renderer == null) return;
        e.renderer.sharedMaterials = on ? s.swapped : s.originals;
        s.on = on;
    }

    /// <summary>
    /// 0 to 1 by how big <paramref name="r"/> is on <paramref name="camera"/>'s screen, in
    /// pixels across: 0 below pixels.x, 1 from pixels.y.
    /// </summary>
    static float SizeFade(Renderer r, Camera camera, Vector2 pixels)
    {
        Bounds b = r.bounds;
        float radius = Mathf.Max(b.extents.x, Mathf.Max(b.extents.y, b.extents.z));
        float distance = Vector3.Distance(camera.transform.position, b.center);
        if (distance <= radius) return 1f;

        float across = radius / (distance * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad)) * camera.pixelHeight;
        float t = Mathf.Clamp01((across - pixels.x) / Mathf.Max(1e-3f, pixels.y - pixels.x));
        return t * t * (3f - 2f * t);
    }

    /// <summary>
    /// True only when the shader declares <paramref name="name"/> as a float or range.
    /// HasProperty alone is not enough here: another shader calling a colour _Emission
    /// would be handed a float and render wrong without an error.
    /// </summary>
    static bool HasFloat(Material mat, string name)
    {
        Shader shader = mat.shader;
        if (shader == null) return false;

        int index = shader.FindPropertyIndex(name);
        if (index < 0) return false;

        UnityEngine.Rendering.ShaderPropertyType type = shader.GetPropertyType(index);
        return type == UnityEngine.Rendering.ShaderPropertyType.Float
            || type == UnityEngine.Rendering.ShaderPropertyType.Range;
    }

    static Color ScaleRGBA(Color c, float a) => new Color(c.r * a, c.g * a, c.b * a, c.a * a);

    void RestoreAll()
    {
        // Clear every property block so nothing we set at runtime leaks into the
        // editor scene view after exiting play mode.
        for (int i = 0; i < groups.Length; i++)
        {
            RenderGroup g = groups[i];
            for (int j = 0; j < g.renderers.Count; j++)
            {
                Renderer r = g.renderers[j].renderer;
                if (r == null) continue;
                UseStandIns(g.renderers[j], false);
                r.enabled = true;
                r.SetPropertyBlock(null);
            }
            for (int j = 0; j < g.lights.Count; j++)
            {
                CachedLight e = g.lights[j];
                if (e.light == null) continue;
                e.light.intensity = e.baseIntensity;
                e.light.enabled = true;
            }
            for (int j = 0; j < g.particles.Count; j++)
                if (g.particles[j] != null && !g.particles[j].isPlaying) g.particles[j].Play(false);
        }
    }
}

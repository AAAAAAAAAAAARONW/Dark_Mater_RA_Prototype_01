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
    }

    public struct CachedRenderer
    {
        public Renderer renderer;
        public int slotCount;
        public bool[] hasColor, hasTint, hasBase, hasEmission;
        public Color[] baseColor, baseTint, baseBase, baseEmission;
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

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int TintColorId = Shader.PropertyToID("_TintColor");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        _block = new MaterialPropertyBlock();
        RebuildCaches();
        ApplyStartState();
    }

    void OnDestroy() => RestoreAll();

    // ── Responder contract ───────────────────────────────────────────────────

    protected override TimedChannel_NEW SelectChannel(LayerProfile_NEW profile) => profile.timing.renderSwap;

    protected override void Apply(LayerProfile_NEW profile)
    {
        if (instantSwap) SetImmediately(profile.layerId);
        else Crossfade(profile.layerId, profile.renderSwapDuration);
    }

    // ── Public API ───────────────────────────────────────────────────────────

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

    void ApplyGroup(RenderGroup group, float alpha)
    {
        bool visible = alpha > disableThreshold;

        for (int i = 0; i < group.renderers.Count; i++)
        {
            CachedRenderer e = group.renderers[i];
            if (e.renderer == null) continue;

            e.renderer.enabled = visible;
            if (!visible) continue;

            for (int m = 0; m < e.slotCount; m++)
            {
                _block.Clear();

                if (e.hasBase[m]) _block.SetColor(BaseColorId, e.baseBase[m] * alpha);
                if (e.hasColor[m]) _block.SetColor(ColorId, ScaleRGBA(e.baseColor[m], alpha));
                if (e.hasTint[m]) _block.SetColor(TintColorId, ScaleRGBA(e.baseTint[m], alpha));
                if (e.hasEmission[m]) _block.SetColor(EmissionColorId, e.baseEmission[m] * alpha);

                e.renderer.SetPropertyBlock(_block, m);
            }
        }

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

            if (visible)
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
            };

            for (int m = 0; m < n; m++)
            {
                Material mat = mats[m];
                if (mat == null) continue;

                if (mat.HasProperty(ColorId)) { c.hasColor[m] = true; c.baseColor[m] = mat.GetColor(ColorId); }
                if (mat.HasProperty(TintColorId)) { c.hasTint[m] = true; c.baseTint[m] = mat.GetColor(TintColorId); }
                if (mat.HasProperty(BaseColorId)) { c.hasBase[m] = true; c.baseBase[m] = mat.GetColor(BaseColorId); }
                if (mat.HasProperty(EmissionColorId)) { c.hasEmission[m] = true; c.baseEmission[m] = mat.GetColor(EmissionColorId); }
            }

            group.renderers.Add(c);
        }
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

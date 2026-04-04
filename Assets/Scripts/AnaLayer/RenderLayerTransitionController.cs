using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// General N-layer render group crossfader.
/// Each render group maps a layerId to a scene root transform.
/// When CrossfadeTo() is called, the target group fades in while
/// all others fade out simultaneously.
///
/// Replaces MacroMicroTransitionControllerTest with a general solution
/// that supports any number of render layers.
///
/// Attach to any persistent GameObject (e.g. GameManager).
/// Driven by VisualLayerResponder via CrossfadeTo().
/// </summary>
public class RenderLayerTransitionController : MonoBehaviour
{
    // ── Render Group Definition ───────────────────────────────────────────────

    [Serializable]
    public class RenderGroup
    {
        [Tooltip("Must match the layerId in the corresponding LayerDefinitionTest asset.")]
        public string layerId;
        [Tooltip("Root transform — all Renderers and Lights under this are included.")]
        public Transform root;
        [Tooltip("If true, this group is visible at startup before any transition fires.")]
        public bool visibleOnStart = false;

        // Runtime cache — not shown in inspector
        [HideInInspector] public List<CachedRenderer> renderers = new List<CachedRenderer>();
        [HideInInspector] public List<CachedLight>    lights    = new List<CachedLight>();
        [HideInInspector] public float currentAlpha = 0f;
    }

    // ── Cached Types ──────────────────────────────────────────────────────────

    public struct CachedRenderer
    {
        public Renderer renderer;
        public bool[]   hasColor,     hasTintColor,     hasBaseColor,     hasEmission;
        public Color[]  baseColor,    baseTintColor,    baseBaseColor,    baseEmission;
    }

    public struct CachedLight
    {
        public Light light;
        public float baseIntensity;
    }

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Render Groups")]
    [Tooltip("One entry per layer. layerId must match LayerDefinitionTest.layerId exactly.")]
    [SerializeField] RenderGroup[] groups = Array.Empty<RenderGroup>();

    [Header("Settings")]
    [Tooltip("Renderers with alpha below this are disabled to avoid residual rendering.")]
    [Range(0f, 0.1f)]
    [SerializeField] float disableThreshold = 0.02f;

    [Header("Debug")]
    [SerializeField] bool debugLog = true;

    // ── Private ───────────────────────────────────────────────────────────────

    MaterialPropertyBlock _block;
    Coroutine _fadeRoutine;

    static readonly int ColorId        = Shader.PropertyToID("_Color");
    static readonly int TintColorId    = Shader.PropertyToID("_TintColor");
    static readonly int BaseColorId    = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        _block = new MaterialPropertyBlock();
        RebuildCaches();
        ApplyInitialState();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Crossfade to the group matching layerId over duration seconds.
    /// Target group fades in, all others fade out simultaneously.
    /// </summary>
    public void CrossfadeTo(string layerId, float duration)
    {
        RenderGroup target = FindGroup(layerId);
        if (target == null)
        {
            Debug.LogWarning($"[RenderLayerTransitionController] No group found for layerId '{layerId}'.");
            return;
        }

        if (_fadeRoutine != null)
            StopCoroutine(_fadeRoutine);

        _fadeRoutine = StartCoroutine(RunCrossfade(target, duration));

        if (debugLog)
            Debug.Log($"[RenderLayerTransitionController] Crossfading to '{layerId}' over {duration}s.");
    }

    /// <summary>
    /// Immediately snap to a group with no transition.
    /// </summary>
    public void SetImmediately(string layerId)
    {
        if (_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }

        foreach (var g in groups)
        {
            float target = string.Equals(g.layerId, layerId, StringComparison.Ordinal) ? 1f : 0f;
            g.currentAlpha = target;
            ApplyGroupVisuals(g, target);
        }

        if (debugLog)
            Debug.Log($"[RenderLayerTransitionController] Snapped immediately to '{layerId}'.");
    }

    /// <summary>
    /// Rebuild renderer and light caches from each group's root transform.
    /// Call this if scene objects change at runtime.
    /// </summary>
    public void RebuildCaches()
    {
        if (_block == null) _block = new MaterialPropertyBlock();

        foreach (var g in groups)
        {
            g.renderers.Clear();
            g.lights.Clear();
            if (g.root == null) continue;
            CollectRenderers(g.root, g.renderers);
            CollectLights(g.root, g.lights);

            if (debugLog)
                Debug.Log($"[RenderLayerTransitionController] Group '{g.layerId}' — " +
                          $"Renderers={g.renderers.Count}, Lights={g.lights.Count}");
        }
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    void ApplyInitialState()
    {
        foreach (var g in groups)
        {
            float alpha = g.visibleOnStart ? 1f : 0f;
            g.currentAlpha = alpha;
            ApplyGroupVisuals(g, alpha);
        }
    }

    RenderGroup FindGroup(string layerId)
    {
        foreach (var g in groups)
            if (string.Equals(g.layerId, layerId, StringComparison.Ordinal))
                return g;
        return null;
    }

    IEnumerator RunCrossfade(RenderGroup target, float duration)
    {
        float d = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        // Record starting alphas for all groups
        float[] startAlphas = new float[groups.Length];
        for (int i = 0; i < groups.Length; i++)
            startAlphas[i] = groups[i].currentAlpha;

        while (elapsed < d)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / d);
            float eased = t * t * (3f - 2f * t); // smoothstep

            for (int i = 0; i < groups.Length; i++)
            {
                float targetAlpha = ReferenceEquals(groups[i], target) ? 1f : 0f;
                float alpha = Mathf.Lerp(startAlphas[i], targetAlpha, eased);
                groups[i].currentAlpha = alpha;
                ApplyGroupVisuals(groups[i], alpha);
            }

            yield return null;
        }

        // Snap to clean final state
        for (int i = 0; i < groups.Length; i++)
        {
            float finalAlpha = ReferenceEquals(groups[i], target) ? 1f : 0f;
            groups[i].currentAlpha = finalAlpha;
            ApplyGroupVisuals(groups[i], finalAlpha);
        }

        _fadeRoutine = null;
    }

    void ApplyGroupVisuals(RenderGroup group, float alpha)
    {
        bool visible = alpha > disableThreshold;

        foreach (var entry in group.renderers)
        {
            if (entry.renderer == null) continue;
            entry.renderer.enabled = visible;
            if (!visible) continue;

            int matCount = entry.renderer.sharedMaterials.Length;
            for (int m = 0; m < matCount; m++)
            {
                _block.Clear();

                if (m < entry.hasBaseColor.Length && entry.hasBaseColor[m])
                    _block.SetColor(BaseColorId, entry.baseBaseColor[m] * alpha);

                if (m < entry.hasColor.Length && entry.hasColor[m])
                    _block.SetColor(ColorId, MultiplyAlpha(entry.baseColor[m], alpha));

                if (m < entry.hasTintColor.Length && entry.hasTintColor[m])
                    _block.SetColor(TintColorId, MultiplyAlpha(entry.baseTintColor[m], alpha));

                if (m < entry.hasEmission.Length && entry.hasEmission[m])
                    _block.SetColor(EmissionColorId, entry.baseEmission[m] * alpha);

                entry.renderer.SetPropertyBlock(_block, m);
            }
        }

        foreach (var entry in group.lights)
        {
            if (entry.light == null) continue;
            entry.light.intensity = entry.baseIntensity * alpha;
            entry.light.enabled   = entry.light.intensity > 0.001f;
        }
    }

    Color MultiplyAlpha(Color c, float alpha)
    {
        return new Color(c.r * alpha, c.g * alpha, c.b * alpha, c.a * alpha);
    }

    void CollectRenderers(Transform root, List<CachedRenderer> output)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r == null) continue;
            var mats = r.sharedMaterials;
            var cached = new CachedRenderer
            {
                renderer     = r,
                hasColor     = new bool[mats.Length],
                baseColor    = new Color[mats.Length],
                hasTintColor = new bool[mats.Length],
                baseTintColor = new Color[mats.Length],
                hasBaseColor  = new bool[mats.Length],
                baseBaseColor = new Color[mats.Length],
                hasEmission   = new bool[mats.Length],
                baseEmission  = new Color[mats.Length],
            };
            for (int m = 0; m < mats.Length; m++)
            {
                var mat = mats[m];
                if (mat == null) continue;
                if (mat.HasProperty(ColorId))        { cached.hasColor[m]     = true; cached.baseColor[m]     = mat.GetColor(ColorId); }
                if (mat.HasProperty(TintColorId))    { cached.hasTintColor[m] = true; cached.baseTintColor[m] = mat.GetColor(TintColorId); }
                if (mat.HasProperty(BaseColorId))    { cached.hasBaseColor[m] = true; cached.baseBaseColor[m] = mat.GetColor(BaseColorId); }
                if (mat.HasProperty(EmissionColorId)){ cached.hasEmission[m]  = true; cached.baseEmission[m]  = mat.GetColor(EmissionColorId); }
            }
            output.Add(cached);
        }
    }

    void CollectLights(Transform root, List<CachedLight> output)
    {
        var lights = root.GetComponentsInChildren<Light>(true);
        foreach (var l in lights)
        {
            if (l == null) continue;
            output.Add(new CachedLight { light = l, baseIntensity = l.intensity });
        }
    }

    void OnDisable()
    {
        // Restore all renderers and lights to base state on disable
        foreach (var g in groups)
        {
            foreach (var entry in g.renderers)
            {
                if (entry.renderer == null) continue;
                entry.renderer.enabled = true;
                entry.renderer.SetPropertyBlock(null);
            }
            foreach (var entry in g.lights)
            {
                if (entry.light == null) continue;
                entry.light.intensity = entry.baseIntensity;
                entry.light.enabled   = true;
            }
        }
    }
}

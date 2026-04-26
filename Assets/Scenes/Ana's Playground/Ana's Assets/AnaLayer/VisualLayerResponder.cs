using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives RenderLayerTransitionController crossfades.
///
/// No longer reacts to OnLayerChanged directly for transitions.
/// CameraLayerResponder calls CrossfadeTo() at the right moment in its
/// coroutine — when the camera reaches TopDown — so the render swap
/// happens while the player is looking down, not before.
///
/// Still handles startup snap via Start().
///
/// Attach to any persistent GameObject (e.g. GameManager).
/// </summary>
public class VisualLayerResponder : MonoBehaviour
{
    [Header("References")]
    [SerializeField] LayerStateManagerTest stateManager;
    [SerializeField] RenderLayerTransitionController transitionController;

    [Header("Transition")]
    [Tooltip("If true, previous layer hides and new layer shows at exactly the same frame — no crossfade. " +
             "If false, uses transitionDuration from the LayerDefinitionTest asset.")]
    [SerializeField] bool instantSwap = true;

    [Header("Nebula (Custom/Nebula)")]
    [Tooltip("Enable layer-driven Nebula shader tween in this responder.")]
    [SerializeField] bool enableNebulaResponse = true;
    [SerializeField] bool autoFindNebulaMaterials = true;
    [SerializeField] Material[] manualNebulaMaterials;
    [SerializeField] string nebulaShaderName = "Custom/Nebula";

    [Header("Debug")]
    [SerializeField] bool debugLog = true;

    Coroutine _transitionRoutine;
    Coroutine _nebulaTweenRoutine;
    readonly List<Material> _nebulaMaterials = new List<Material>();
    bool _nebulaCacheBuilt;

    struct NebulaState
    {
        public Color colorDark;
        public Color colorMid;
        public Color colorBright;
        public Color colorStar;
        public float scale;
        public float octaves;
        public float persistence;
        public float density;
        public float sharpness;
        public float starScale;
        public float starThreshold;
        public float starBrightness;
        public float animate;
        public float speed;
    }

    static readonly int ColorDarkId = Shader.PropertyToID("_ColorDark");
    static readonly int ColorMidId = Shader.PropertyToID("_ColorMid");
    static readonly int ColorBrightId = Shader.PropertyToID("_ColorBright");
    static readonly int ColorStarId = Shader.PropertyToID("_ColorStar");
    static readonly int ScaleId = Shader.PropertyToID("_Scale");
    static readonly int OctavesId = Shader.PropertyToID("_Octaves");
    static readonly int PersistenceId = Shader.PropertyToID("_Persistence");
    static readonly int DensityId = Shader.PropertyToID("_Density");
    static readonly int SharpnessId = Shader.PropertyToID("_Sharpness");
    static readonly int StarScaleId = Shader.PropertyToID("_StarScale");
    static readonly int StarThresholdId = Shader.PropertyToID("_StarThreshold");
    static readonly int StarBrightnessId = Shader.PropertyToID("_StarBrightness");
    static readonly int AnimateId = Shader.PropertyToID("_Animate");
    static readonly int SpeedId = Shader.PropertyToID("_Speed");

    void Awake()
    {
        if (stateManager == null)
            stateManager = FindObjectOfType<LayerStateManagerTest>();
        if (transitionController == null)
            transitionController = FindObjectOfType<RenderLayerTransitionController>();
    }

    void OnEnable()
    {
        if (stateManager != null)
            stateManager.OnLayerChanged += HandleLayerChanged;
    }

    void OnDisable()
    {
        if (stateManager != null)
            stateManager.OnLayerChanged -= HandleLayerChanged;
    }

    void Start()
    {
        // Snap to the starting layer immediately on play
        if (transitionController != null && stateManager != null)
        {
            transitionController.RebuildCaches();
            transitionController.SetImmediately(stateManager.CurrentLayerId);
            if (debugLog)
                Debug.Log($"[VisualLayerResponder] Initialized — snapped to '{stateManager.CurrentLayerId}'.");
        }

        if (enableNebulaResponse && stateManager != null && stateManager.CurrentDefinition != null)
        {
            EnsureNebulaMaterialCache();
            ApplyNebulaImmediately(stateManager.CurrentDefinition);
        }
    }

    // ── Public API — called by CameraLayerResponder at the right moment ────────

    /// <summary>
    /// Trigger the crossfade to the given layer's render group.
    /// Called by CameraLayerResponder when the camera reaches TopDown,
    /// so the swap happens while the player is looking down.
    /// </summary>
    public void CrossfadeTo(LayerDefinitionTest layerDef)
    {
        if (layerDef == null || transitionController == null) return;

        if (_transitionRoutine != null)
            StopCoroutine(_transitionRoutine);

        _transitionRoutine = StartCoroutine(RunCrossfade(layerDef));
    }

    IEnumerator RunCrossfade(LayerDefinitionTest layerDef)
    {
        if (instantSwap)
        {
            transitionController.SetImmediately(layerDef.layerId);
            if (debugLog)
                Debug.Log($"[VisualLayerResponder] Instant swap to '{layerDef.layerId}'.");
        }
        else
        {
            transitionController.CrossfadeTo(layerDef.layerId, layerDef.transitionDuration);
            if (debugLog)
                Debug.Log($"[VisualLayerResponder] Crossfading to '{layerDef.layerId}' " +
                          $"over {layerDef.transitionDuration}s.");
        }

        _transitionRoutine = null;
        yield break;
    }

    void HandleLayerChanged(LayerDefinitionTest previous, LayerDefinitionTest current)
    {
        if (!enableNebulaResponse || current == null || !current.driveNebulaShader)
        {
            if (debugLog && current != null && !current.driveNebulaShader)
                Debug.Log($"[VisualLayerResponder] Nebula skipped on layer '{current.layerId}' (driveNebulaShader=false).");
            return;
        }

        EnsureNebulaMaterialCache();
        if (_nebulaMaterials.Count == 0)
        {
            if (debugLog)
                Debug.LogWarning("[VisualLayerResponder] Nebula enabled but no Custom/Nebula materials were found.");
            return;
        }

        if (_nebulaTweenRoutine != null)
            StopCoroutine(_nebulaTweenRoutine);
        _nebulaTweenRoutine = StartCoroutine(TweenNebulaTo(current));
    }

    void EnsureNebulaMaterialCache()
    {
        if (_nebulaCacheBuilt) return;
        BuildNebulaMaterialCache();
    }

    void BuildNebulaMaterialCache()
    {
        _nebulaMaterials.Clear();
        var seen = new HashSet<Material>();

        if (autoFindNebulaMaterials)
        {
            Renderer[] renderers = FindObjectsOfType<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                Material[] mats = r.materials;
                for (int j = 0; j < mats.Length; j++)
                {
                    Material mat = mats[j];
                    if (mat == null || mat.shader == null) continue;
                    if (mat.shader.name != nebulaShaderName) continue;
                    if (seen.Add(mat)) _nebulaMaterials.Add(mat);
                }
            }

            // Also support Nebula used as skybox material (common case, no Renderer).
            Material skyboxMat = RenderSettings.skybox;
            if (skyboxMat != null && skyboxMat.shader != null && skyboxMat.shader.name == nebulaShaderName)
            {
                if (seen.Add(skyboxMat))
                    _nebulaMaterials.Add(skyboxMat);
            }
        }
        else if (manualNebulaMaterials != null)
        {
            for (int i = 0; i < manualNebulaMaterials.Length; i++)
            {
                Material mat = manualNebulaMaterials[i];
                if (mat == null || mat.shader == null) continue;
                if (mat.shader.name != nebulaShaderName) continue;
                if (seen.Add(mat)) _nebulaMaterials.Add(mat);
            }
        }

        _nebulaCacheBuilt = true;

        if (debugLog)
            Debug.Log($"[VisualLayerResponder] Nebula material cache built: {_nebulaMaterials.Count} material(s).");
    }

    IEnumerator TweenNebulaTo(LayerDefinitionTest def)
    {
        NebulaState from = ReadCurrentNebulaState();
        NebulaState to = FromDefinition(def);
        float d = Mathf.Max(0.01f, def.nebulaBlendDuration);
        float elapsed = 0f;

        while (elapsed < d)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / d);
            float eased = t * t * (3f - 2f * t);
            ApplyNebulaState(LerpNebula(from, to, eased));
            yield return null;
        }

        ApplyNebulaState(to);
        _nebulaTweenRoutine = null;
    }

    void ApplyNebulaImmediately(LayerDefinitionTest def)
    {
        if (def == null || !def.driveNebulaShader) return;
        ApplyNebulaState(FromDefinition(def));
    }

    NebulaState ReadCurrentNebulaState()
    {
        NebulaState fallback = FromDefinition(stateManager != null ? stateManager.CurrentDefinition : null);
        if (_nebulaMaterials.Count == 0) return fallback;

        Material m = _nebulaMaterials[0];
        return new NebulaState
        {
            colorDark = m.HasProperty(ColorDarkId) ? m.GetColor(ColorDarkId) : fallback.colorDark,
            colorMid = m.HasProperty(ColorMidId) ? m.GetColor(ColorMidId) : fallback.colorMid,
            colorBright = m.HasProperty(ColorBrightId) ? m.GetColor(ColorBrightId) : fallback.colorBright,
            colorStar = m.HasProperty(ColorStarId) ? m.GetColor(ColorStarId) : fallback.colorStar,
            scale = m.HasProperty(ScaleId) ? m.GetFloat(ScaleId) : fallback.scale,
            octaves = m.HasProperty(OctavesId) ? m.GetFloat(OctavesId) : fallback.octaves,
            persistence = m.HasProperty(PersistenceId) ? m.GetFloat(PersistenceId) : fallback.persistence,
            density = m.HasProperty(DensityId) ? m.GetFloat(DensityId) : fallback.density,
            sharpness = m.HasProperty(SharpnessId) ? m.GetFloat(SharpnessId) : fallback.sharpness,
            starScale = m.HasProperty(StarScaleId) ? m.GetFloat(StarScaleId) : fallback.starScale,
            starThreshold = m.HasProperty(StarThresholdId) ? m.GetFloat(StarThresholdId) : fallback.starThreshold,
            starBrightness = m.HasProperty(StarBrightnessId) ? m.GetFloat(StarBrightnessId) : fallback.starBrightness,
            animate = m.HasProperty(AnimateId) ? m.GetFloat(AnimateId) : fallback.animate,
            speed = m.HasProperty(SpeedId) ? m.GetFloat(SpeedId) : fallback.speed
        };
    }

    static NebulaState FromDefinition(LayerDefinitionTest def)
    {
        if (def == null)
        {
            return new NebulaState
            {
                colorDark = new Color(0.08f, 0.02f, 0.18f, 1f),
                colorMid = new Color(0.3f, 0.15f, 0.5f, 1f),
                colorBright = new Color(0.6f, 0.4f, 0.9f, 1f),
                colorStar = new Color(1f, 0.95f, 1f, 1f),
                scale = 1.2f,
                octaves = 4f,
                persistence = 0.5f,
                density = 0.8f,
                sharpness = 1.5f,
                starScale = 80f,
                starThreshold = 0.992f,
                starBrightness = 1.2f,
                animate = 1f,
                speed = 0.05f
            };
        }

        return new NebulaState
        {
            colorDark = def.nebulaColorDark,
            colorMid = def.nebulaColorMid,
            colorBright = def.nebulaColorBright,
            colorStar = def.nebulaColorStar,
            scale = def.nebulaScale,
            octaves = def.nebulaOctaves,
            persistence = def.nebulaPersistence,
            density = def.nebulaDensity,
            sharpness = def.nebulaSharpness,
            starScale = def.nebulaStarScale,
            starThreshold = def.nebulaStarThreshold,
            starBrightness = def.nebulaStarBrightness,
            animate = def.nebulaAnimate,
            speed = def.nebulaSpeed
        };
    }

    static NebulaState LerpNebula(NebulaState a, NebulaState b, float t)
    {
        return new NebulaState
        {
            colorDark = Color.Lerp(a.colorDark, b.colorDark, t),
            colorMid = Color.Lerp(a.colorMid, b.colorMid, t),
            colorBright = Color.Lerp(a.colorBright, b.colorBright, t),
            colorStar = Color.Lerp(a.colorStar, b.colorStar, t),
            scale = Mathf.Lerp(a.scale, b.scale, t),
            octaves = Mathf.Lerp(a.octaves, b.octaves, t),
            persistence = Mathf.Lerp(a.persistence, b.persistence, t),
            density = Mathf.Lerp(a.density, b.density, t),
            sharpness = Mathf.Lerp(a.sharpness, b.sharpness, t),
            starScale = Mathf.Lerp(a.starScale, b.starScale, t),
            starThreshold = Mathf.Lerp(a.starThreshold, b.starThreshold, t),
            starBrightness = Mathf.Lerp(a.starBrightness, b.starBrightness, t),
            animate = Mathf.Lerp(a.animate, b.animate, t),
            speed = Mathf.Lerp(a.speed, b.speed, t)
        };
    }

    void ApplyNebulaState(NebulaState s)
    {
        for (int i = 0; i < _nebulaMaterials.Count; i++)
        {
            Material m = _nebulaMaterials[i];
            if (m == null) continue;

            if (m.HasProperty(ColorDarkId)) m.SetColor(ColorDarkId, s.colorDark);
            if (m.HasProperty(ColorMidId)) m.SetColor(ColorMidId, s.colorMid);
            if (m.HasProperty(ColorBrightId)) m.SetColor(ColorBrightId, s.colorBright);
            if (m.HasProperty(ColorStarId)) m.SetColor(ColorStarId, s.colorStar);
            if (m.HasProperty(ScaleId)) m.SetFloat(ScaleId, s.scale);
            if (m.HasProperty(OctavesId)) m.SetFloat(OctavesId, s.octaves);
            if (m.HasProperty(PersistenceId)) m.SetFloat(PersistenceId, s.persistence);
            if (m.HasProperty(DensityId)) m.SetFloat(DensityId, s.density);
            if (m.HasProperty(SharpnessId)) m.SetFloat(SharpnessId, s.sharpness);
            if (m.HasProperty(StarScaleId)) m.SetFloat(StarScaleId, s.starScale);
            if (m.HasProperty(StarThresholdId)) m.SetFloat(StarThresholdId, s.starThreshold);
            if (m.HasProperty(StarBrightnessId)) m.SetFloat(StarBrightnessId, s.starBrightness);
            if (m.HasProperty(AnimateId)) m.SetFloat(AnimateId, s.animate >= 0.5f ? 1f : 0f);
            if (m.HasProperty(SpeedId)) m.SetFloat(SpeedId, s.speed);
        }
    }
}
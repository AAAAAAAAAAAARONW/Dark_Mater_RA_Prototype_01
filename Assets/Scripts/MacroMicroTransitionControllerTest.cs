using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 单相机宏观/微观叠化控制器：
/// - 一个相机同时观察 Macro/Galaxy 两套 Layer
/// - 通过 Renderer/Light 的可见度与光强控制实现"宏观->微观"叠化
/// - Camera transform/FOV/blends are handled by Cinemachine
/// </summary>
[RequireComponent(typeof(Camera))]
public class MacroMicroTransitionControllerTest : MonoBehaviour
{
    struct CachedRenderer
    {
        public Renderer renderer;
        public bool[] hasColor;
        public Color[] baseColor;
        public bool[] hasTintColor;
        public Color[] baseTintColor;
        public bool[] hasBaseColor;
        public Color[] baseBaseColor;
        public bool[] hasEmission;
        public Color[] baseEmission;
    }

    struct CachedLight
    {
        public Light light;
        public float baseIntensity;
    }

    [Header("Single Camera")]
    [SerializeField] Camera targetCamera;

    [Header("Layer Setup")]
    [Tooltip("宏观世界根节点（宇宙网）")]
    [SerializeField] Transform macroRoot;
    [Tooltip("银河世界根节点")]
    [SerializeField] Transform galaxyRoot;
    [Tooltip("Macro 层（建议 CosmicWeb）")]
    [SerializeField] LayerMask macroLayers = ~0;
    [Tooltip("Galaxy 层（建议 GalaxyWorld）")]
    [SerializeField] LayerMask galaxyLayers = ~0;
    [Tooltip("自动收集 root 下所有 Renderer/Light")]
    [SerializeField] bool autoCollectOnEnable = true;

    [Header("Blend")]
    [Range(0f, 1f)]
    [SerializeField] float transition01;
    [Min(0.05f)]
    [SerializeField] float transitionDuration = 4f;
    [SerializeField] AnimationCurve transitionEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("可见度权重曲线。默认中段更柔和，避免线性变化太硬。")]
    [SerializeField] AnimationCurve visibilityWeightCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("播放时按下该键，触发 Macro->Galaxy 或 Galaxy->Macro 切换")]
    [SerializeField] KeyCode toggleKey = KeyCode.T;

    [Header("FOV Change")]
    [Tooltip("Allow FOV override (disabled by default - Cinemachine handles FOV). Only enable if needed for legacy compatibility.")]
    [SerializeField] bool allowFovOverride = false;
    [Tooltip("Macro FOV: X=过渡前, Y=过渡后")]
    [SerializeField] Vector2 macroFovRange = new Vector2(70f, 38f);
    [Tooltip("Galaxy FOV: X=过渡前, Y=过渡后")]
    [SerializeField] Vector2 galaxyFovRange = new Vector2(32f, 58f);

    [Header("Visibility")]
    [Tooltip("当组权重低于该值时直接关闭 Renderer，避免残留")]
    [Range(0f, 0.2f)]
    [SerializeField] float rendererEnableThreshold = 0.02f;
    [Tooltip("单相机+不透明材质时建议开启：在过渡中点做可见组切换，避免“alpha 不生效看起来没变化”")]
    [SerializeField] bool useHardSwitchForOpaque = false;
    [Range(0.1f, 0.9f)]
    [SerializeField] float hardSwitchPoint = 0.5f;
    [Tooltip("软切换时，靠近两端才关闭另一组；中段两组都保持可见，减少跳变")]
    [Range(0f, 0.2f)]
    [SerializeField] float endpointCullEpsilon = 0.02f;
    [Tooltip("软切换时始终保持两组 Renderer 可见，仅靠权重/光强/FOV 过渡。更柔和，但会有短时重叠。")]
    [SerializeField] bool keepBothGroupsVisibleInSoftBlend = false;
    [Tooltip("Macro 光强倍率：X=过渡前, Y=过渡后")]
    [SerializeField] Vector2 macroLightExposure = new Vector2(1f, 0f);
    [Tooltip("Galaxy 光强倍率：X=过渡前, Y=过渡后")]
    [SerializeField] Vector2 galaxyLightExposure = new Vector2(0f, 1f);
    [SerializeField] bool debugLog;

    readonly List<CachedRenderer> _macroRenderers = new List<CachedRenderer>();
    readonly List<CachedRenderer> _galaxyRenderers = new List<CachedRenderer>();
    readonly List<CachedLight> _macroLights = new List<CachedLight>();
    readonly List<CachedLight> _galaxyLights = new List<CachedLight>();
    MaterialPropertyBlock _block;

    bool _isAnimating;
    float _animStart;
    float _animFrom;
    float _animTo;

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int TintColorId = Shader.PropertyToID("_TintColor");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public float Transition01 => transition01;
    public float TransitionDuration
    {
        get => transitionDuration;
        set => transitionDuration = Mathf.Max(0.05f, value);
    }

    public string GetDebugState()
    {
        string macroRootName = macroRoot != null ? macroRoot.name : "null";
        string galaxyRootName = galaxyRoot != null ? galaxyRoot.name : "null";
        return $"MacroRoot={macroRootName}, GalaxyRoot={galaxyRootName}, MacroRenderers={_macroRenderers.Count}, GalaxyRenderers={_galaxyRenderers.Count}, t={transition01:0.00}, hardSwitch={useHardSwitchForOpaque}";
    }

    void Awake()
    {
        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();
        if (_block == null)
            _block = new MaterialPropertyBlock();
    }

    void OnEnable()
    {
        if (_block == null)
            _block = new MaterialPropertyBlock();
        ApplyCameraMask();
        if (autoCollectOnEnable)
            RebuildCaches();
        ApplyTransitionImmediate(transition01);
        if (debugLog)
            Debug.Log($"[MacroMicroTransitionController] OnEnable -> {GetDebugState()}");
    }

    void OnDisable()
    {
        RestoreAndEnableAll();
    }

    void LateUpdate()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            if (transition01 < 0.5f) StartTransitionToGalaxy();
            else StartTransitionToMacro();
        }

        if (_isAnimating)
        {
            float p = Mathf.Clamp01((Time.time - _animStart) / transitionDuration);
            float eased = transitionEase.Evaluate(p);
            ApplyTransitionImmediate(Mathf.Lerp(_animFrom, _animTo, eased));
            if (p >= 1f) _isAnimating = false;
        }
    }

    public void RebuildCaches()
    {
        _macroRenderers.Clear();
        _galaxyRenderers.Clear();
        _macroLights.Clear();
        _galaxyLights.Clear();

        CollectGroup(macroRoot, _macroRenderers, _macroLights);
        CollectGroup(galaxyRoot, _galaxyRenderers, _galaxyLights);
        if (debugLog)
            Debug.Log($"[MacroMicroTransitionController] RebuildCaches -> {GetDebugState()}");
    }

    void CollectGroup(Transform root, List<CachedRenderer> outRenderers, List<CachedLight> outLights)
    {
        if (root == null) return;

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            var mats = r.sharedMaterials;
            var cached = new CachedRenderer
            {
                renderer = r,
                hasColor = new bool[mats.Length],
                baseColor = new Color[mats.Length],
                hasTintColor = new bool[mats.Length],
                baseTintColor = new Color[mats.Length],
                hasBaseColor = new bool[mats.Length],
                baseBaseColor = new Color[mats.Length],
                hasEmission = new bool[mats.Length],
                baseEmission = new Color[mats.Length]
            };
            for (int m = 0; m < mats.Length; m++)
            {
                var mat = mats[m];
                if (mat == null) continue;
                if (mat.HasProperty(ColorId))
                {
                    cached.hasColor[m] = true;
                    cached.baseColor[m] = mat.GetColor(ColorId);
                }
                if (mat.HasProperty(TintColorId))
                {
                    cached.hasTintColor[m] = true;
                    cached.baseTintColor[m] = mat.GetColor(TintColorId);
                }
                if (mat.HasProperty(BaseColorId))
                {
                    cached.hasBaseColor[m] = true;
                    cached.baseBaseColor[m] = mat.GetColor(BaseColorId);
                }
                if (mat.HasProperty(EmissionColorId))
                {
                    cached.hasEmission[m] = true;
                    cached.baseEmission[m] = mat.GetColor(EmissionColorId);
                }
            }
            outRenderers.Add(cached);
        }

        var lights = root.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            var l = lights[i];
            if (l == null) continue;
            outLights.Add(new CachedLight
            {
                light = l,
                baseIntensity = l.intensity
            });
        }
    }

    void ApplyCameraMask()
    {
        if (targetCamera == null) return;
        targetCamera.cullingMask |= macroLayers.value | galaxyLayers.value;
    }

    void ApplyTransitionImmediate(float value01)
    {
        transition01 = Mathf.Clamp01(value01);

        // FOV control is gated - Cinemachine handles FOV by default
        if (allowFovOverride && targetCamera != null)
            targetCamera.fieldOfView = Mathf.Lerp(macroFovRange.x, galaxyFovRange.y, transition01);

        float curveT = Mathf.Clamp01(visibilityWeightCurve.Evaluate(transition01));
        float macroWeight = 1f - curveT;
        float galaxyWeight = curveT;
        float macroExposure = Mathf.Lerp(macroLightExposure.x, macroLightExposure.y, transition01);
        float galaxyExposure = Mathf.Lerp(galaxyLightExposure.x, galaxyLightExposure.y, transition01);

        // 保证两端状态绝对干净：t=0 只看 Macro，t=1 只看 Galaxy
        if (transition01 <= 0.0001f)
        {
            ApplyGroupVisuals(_macroRenderers, _macroLights, 1f, macroLightExposure.x, true);
            ApplyGroupVisuals(_galaxyRenderers, _galaxyLights, 0f, galaxyLightExposure.x, false);
            return;
        }
        if (transition01 >= 0.9999f)
        {
            ApplyGroupVisuals(_macroRenderers, _macroLights, 0f, macroLightExposure.y, false);
            ApplyGroupVisuals(_galaxyRenderers, _galaxyLights, 1f, galaxyLightExposure.y, true);
            return;
        }

        bool macroVisible;
        bool galaxyVisible;
        if (useHardSwitchForOpaque)
        {
            macroVisible = transition01 < hardSwitchPoint;
            galaxyVisible = transition01 >= hardSwitchPoint;
        }
        else
        {
            if (keepBothGroupsVisibleInSoftBlend)
            {
                macroVisible = true;
                galaxyVisible = true;
                ApplyGroupVisuals(_macroRenderers, _macroLights, macroWeight, macroExposure, macroVisible);
                ApplyGroupVisuals(_galaxyRenderers, _galaxyLights, galaxyWeight, galaxyExposure, galaxyVisible);
                return;
            }

            // 过渡中段两组都可见，避免 layer 组在中途硬跳。
            bool inMiddleBlend = transition01 > endpointCullEpsilon && transition01 < 1f - endpointCullEpsilon;
            if (inMiddleBlend)
            {
                macroVisible = true;
                galaxyVisible = true;
            }
            else
            {
                macroVisible = transition01 <= endpointCullEpsilon || macroWeight > rendererEnableThreshold;
                galaxyVisible = transition01 >= 1f - endpointCullEpsilon || galaxyWeight > rendererEnableThreshold;
            }
        }

        ApplyGroupVisuals(_macroRenderers, _macroLights, macroWeight, macroExposure, macroVisible);
        ApplyGroupVisuals(_galaxyRenderers, _galaxyLights, galaxyWeight, galaxyExposure, galaxyVisible);
    }

    void ApplyGroupVisuals(List<CachedRenderer> renderers, List<CachedLight> lights, float alpha, float exposure, bool visible)
    {
        if (_block == null)
            _block = new MaterialPropertyBlock();

        for (int i = 0; i < renderers.Count; i++)
        {
            var entry = renderers[i];
            if (entry.renderer == null) continue;
            entry.renderer.enabled = visible;
            if (!visible) continue;

            int matCount = entry.renderer.sharedMaterials.Length;
            for (int m = 0; m < matCount; m++)
            {
                _block.Clear();

                if (m < entry.hasBaseColor.Length && entry.hasBaseColor[m])
                {
                    Color c = entry.baseBaseColor[m];
                    c.r *= alpha;
                    c.g *= alpha;
                    c.b *= alpha;
                    c.a *= alpha;
                    _block.SetColor(BaseColorId, c);
                }
                if (m < entry.hasColor.Length && entry.hasColor[m])
                {
                    Color c = entry.baseColor[m];
                    c.r *= alpha;
                    c.g *= alpha;
                    c.b *= alpha;
                    c.a *= alpha;
                    _block.SetColor(ColorId, c);
                }
                if (m < entry.hasTintColor.Length && entry.hasTintColor[m])
                {
                    Color c = entry.baseTintColor[m];
                    c.r *= alpha;
                    c.g *= alpha;
                    c.b *= alpha;
                    c.a *= alpha;
                    _block.SetColor(TintColorId, c);
                }
                if (m < entry.hasEmission.Length && entry.hasEmission[m])
                {
                    Color e = entry.baseEmission[m] * Mathf.Max(0f, exposure) * alpha;
                    _block.SetColor(EmissionColorId, e);
                }

                entry.renderer.SetPropertyBlock(_block, m);
            }
        }

        for (int i = 0; i < lights.Count; i++)
        {
            var l = lights[i];
            if (l.light == null) continue;
            l.light.intensity = l.baseIntensity * Mathf.Max(0f, exposure) * alpha;
            l.light.enabled = l.light.intensity > 0.001f;
        }
    }

    void RestoreAndEnableAll()
    {
        for (int i = 0; i < _macroRenderers.Count; i++)
        {
            if (_macroRenderers[i].renderer == null) continue;
            _macroRenderers[i].renderer.enabled = true;
            _macroRenderers[i].renderer.SetPropertyBlock(null);
        }
        for (int i = 0; i < _galaxyRenderers.Count; i++)
        {
            if (_galaxyRenderers[i].renderer == null) continue;
            _galaxyRenderers[i].renderer.enabled = true;
            _galaxyRenderers[i].renderer.SetPropertyBlock(null);
        }
        for (int i = 0; i < _macroLights.Count; i++)
        {
            var l = _macroLights[i];
            if (l.light == null) continue;
            l.light.intensity = l.baseIntensity;
            l.light.enabled = true;
        }
        for (int i = 0; i < _galaxyLights.Count; i++)
        {
            var l = _galaxyLights[i];
            if (l.light == null) continue;
            l.light.intensity = l.baseIntensity;
            l.light.enabled = true;
        }
    }

    public void StartTransitionToGalaxy()
    {
        StartTransition(1f);
    }

    public void StartTransitionToMacro()
    {
        StartTransition(0f);
    }

    public void StartTransition(float target01)
    {
        StartTransition(target01, transitionDuration);
    }

    public void StartTransition(float target01, float duration)
    {
        if (_macroRenderers.Count == 0 && _galaxyRenderers.Count == 0)
            RebuildCaches();

        _animFrom = transition01;
        _animTo = Mathf.Clamp01(target01);
        transitionDuration = Mathf.Max(0.05f, duration);
        _animStart = Time.time;
        _isAnimating = true;
        if (debugLog)
            Debug.Log($"[MacroMicroTransitionController] StartTransition from={_animFrom:0.00} to={_animTo:0.00} duration={transitionDuration:0.00}");
    }

    public void SetTransitionImmediate(float value01)
    {
        _isAnimating = false;
        ApplyTransitionImmediate(value01);
    }

    void OnValidate()
    {
        if (transitionDuration < 0.05f) transitionDuration = 0.05f;
        transition01 = Mathf.Clamp01(transition01);
        endpointCullEpsilon = Mathf.Clamp(endpointCullEpsilon, 0f, 0.2f);
        if (visibilityWeightCurve == null || visibilityWeightCurve.length < 2)
            visibilityWeightCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();
        ApplyCameraMask();
    }
}

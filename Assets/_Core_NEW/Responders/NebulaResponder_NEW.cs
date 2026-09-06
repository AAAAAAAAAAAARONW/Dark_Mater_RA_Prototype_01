using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Applies a NebulaProfile_NEW to every Custom/Nebula material when its channel fires.
///
/// Split out of VisualLayerResponder, which bundled this with render-group forwarding
/// and startup snapping — three jobs on three different clocks in one file.
///
/// Two behavioural corrections, neither of which changes what the player sees:
///
///   TIMING. The old version tweened the sky the instant the gate fired, while the
///   player was still looking at the old world, so the sky visibly shifted before the
///   cover arrived. Here the channel defaults to CoverReached, so the sky changes on
///   the same frame the world does — under the cover.
///
///   ASSET SAFETY. The old version wrote straight into RenderSettings.skybox, which is
///   the material asset on disk: one playtest permanently altered the .mat and the scene
///   stopped being reproducible. This clones the skybox into a runtime instance and
///   restores the original on teardown. Same shader, same values, same pixels — the
///   only difference is that nothing is written back to the project.
///
/// Material discovery uses sharedMaterials. The old code used Renderer.materials purely
/// to read shader names, which clones a material for every renderer in the scene.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("NEBULA", "#4A94F2")]
public class NebulaResponder_NEW : LayerResponder_NEW
{
    [Header("Discovery")]
    [Tooltip("Shader name that marks a material as a nebula surface.")]
    [SerializeField] string shaderName = "Custom/Nebula";

    [Tooltip("Scan every Renderer in the scene at startup. Turn off to use the explicit list only.")]
    [SerializeField] bool autoDiscover = true;

    [Tooltip("Extra materials to drive. Used on their own when auto discovery is off.")]
    [SerializeField] Material[] explicitMaterials;

    [Header("Skybox")]
    [Tooltip("Drive RenderSettings.skybox when it uses the nebula shader.")]
    [SerializeField] bool includeSkybox = true;

    [Tooltip("Clone the skybox material at runtime so the project asset is never written to. " +
             "Turn off only to reproduce the old in-place behaviour.")]
    [SerializeField] bool protectSkyboxAsset = true;

    readonly List<Material> _targets = new List<Material>();
    Material _skyboxOriginal;
    Material _skyboxInstance;
    Coroutine _tween;
    NebulaState _current;
    bool _hasCurrent;

    // Property ids, identical to the originals.
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
    static readonly int StarTwinkleId = Shader.PropertyToID("_StarTwinkle");
    static readonly int StarTwinkleSpeedId = Shader.PropertyToID("_StarTwinkleSpeed");
    static readonly int StarTwinkleAmountId = Shader.PropertyToID("_StarTwinkleAmount");
    static readonly int AnimateId = Shader.PropertyToID("_Animate");
    static readonly int SpeedId = Shader.PropertyToID("_Speed");

    struct NebulaState
    {
        public Color dark, mid, bright, star;
        public float scale, octaves, persistence, density, sharpness;
        public float starScale, starThreshold, starBrightness;
        public float starTwinkle, starTwinkleSpeed, starTwinkleAmount;
        public float animate, speed;
    }

    protected override void Awake()
    {
        base.Awake();
        BuildTargets();
    }

    void OnDestroy() => RestoreSkybox();

    protected override TimedChannel_NEW SelectChannel(LayerProfile_NEW profile) => profile.timing.nebula;

    protected override void Apply(LayerProfile_NEW profile)
    {
        NebulaProfile_NEW np = profile.nebula;
        if (np == null)
        {
            if (debugLog) Debug.Log($"[NebulaResponder_NEW] '{profile.layerId}' has no nebula profile. " +
                                    "Sky left as-is.", this);
            return;
        }

        if (_targets.Count == 0) return;

        if (_tween != null) { StopCoroutine(_tween); _tween = null; }

        NebulaState goal = FromProfile(np);

        if (np.blendDuration <= 0f || !_hasCurrent)
        {
            Write(goal);
            return;
        }

        _tween = StartCoroutine(TweenTo(goal, np.blendDuration));
    }

    // ── Discovery ────────────────────────────────────────────────────────────

    void BuildTargets()
    {
        _targets.Clear();
        HashSet<Material> seen = new HashSet<Material>();

        if (autoDiscover)
        {
            foreach (Renderer r in FindObjectsOfType<Renderer>())
            {
                if (r == null) continue;
                foreach (Material m in r.sharedMaterials)   // shared: reading never clones
                {
                    if (m == null || m.shader == null) continue;
                    if (m.shader.name != shaderName) continue;
                    if (seen.Add(m)) _targets.Add(m);
                }
            }
        }

        if (explicitMaterials != null)
        {
            foreach (Material m in explicitMaterials)
            {
                if (m == null || m.shader == null) continue;
                if (m.shader.name != shaderName) continue;
                if (seen.Add(m)) _targets.Add(m);
            }
        }

        if (includeSkybox) AddSkyboxTarget(seen);

        if (debugLog)
            Debug.Log($"[NebulaResponder_NEW] {_targets.Count} nebula material(s) under control.", this);

        if (_targets.Count > 0)
        {
            _current = Read(_targets[0]);
            _hasCurrent = true;
        }
    }

    void AddSkyboxTarget(HashSet<Material> seen)
    {
        Material sky = RenderSettings.skybox;
        if (sky == null || sky.shader == null || sky.shader.name != shaderName) return;

        if (!protectSkyboxAsset)
        {
            if (seen.Add(sky)) _targets.Add(sky);
            return;
        }

        _skyboxOriginal = sky;
        _skyboxInstance = new Material(sky) { name = sky.name + " (runtime)" };
        RenderSettings.skybox = _skyboxInstance;

        if (seen.Add(_skyboxInstance)) _targets.Add(_skyboxInstance);

        if (debugLog)
            Debug.Log("[NebulaResponder_NEW] Skybox cloned for runtime. The project asset is untouched.", this);
    }

    void RestoreSkybox()
    {
        if (_skyboxOriginal == null) return;

        RenderSettings.skybox = _skyboxOriginal;

        if (_skyboxInstance != null)
        {
            if (Application.isPlaying) Destroy(_skyboxInstance);
            else DestroyImmediate(_skyboxInstance);
        }

        _skyboxOriginal = null;
        _skyboxInstance = null;
    }

    // ── Read / write ─────────────────────────────────────────────────────────

    static NebulaState FromProfile(NebulaProfile_NEW p) => new NebulaState
    {
        dark = p.colorDark,
        mid = p.colorMid,
        bright = p.colorBright,
        star = p.colorStar,
        scale = p.scale,
        octaves = p.octaves,
        persistence = p.persistence,
        density = p.density,
        sharpness = p.sharpness,
        starScale = p.starScale,
        starThreshold = p.starThreshold,
        starBrightness = p.starBrightness,
        starTwinkle = p.starTwinkle,
        starTwinkleSpeed = p.starTwinkleSpeed,
        starTwinkleAmount = p.starTwinkleAmount,
        animate = p.animate,
        speed = p.speed,
    };

    static NebulaState Read(Material m) => new NebulaState
    {
        dark = m.HasProperty(ColorDarkId) ? m.GetColor(ColorDarkId) : Color.black,
        mid = m.HasProperty(ColorMidId) ? m.GetColor(ColorMidId) : Color.black,
        bright = m.HasProperty(ColorBrightId) ? m.GetColor(ColorBrightId) : Color.black,
        star = m.HasProperty(ColorStarId) ? m.GetColor(ColorStarId) : Color.white,
        scale = m.HasProperty(ScaleId) ? m.GetFloat(ScaleId) : 1.2f,
        octaves = m.HasProperty(OctavesId) ? m.GetFloat(OctavesId) : 4f,
        persistence = m.HasProperty(PersistenceId) ? m.GetFloat(PersistenceId) : 0.5f,
        density = m.HasProperty(DensityId) ? m.GetFloat(DensityId) : 0.8f,
        sharpness = m.HasProperty(SharpnessId) ? m.GetFloat(SharpnessId) : 1.5f,
        starScale = m.HasProperty(StarScaleId) ? m.GetFloat(StarScaleId) : 80f,
        starThreshold = m.HasProperty(StarThresholdId) ? m.GetFloat(StarThresholdId) : 0.992f,
        starBrightness = m.HasProperty(StarBrightnessId) ? m.GetFloat(StarBrightnessId) : 1.2f,
        starTwinkle = m.HasProperty(StarTwinkleId) ? m.GetFloat(StarTwinkleId) : 0f,
        starTwinkleSpeed = m.HasProperty(StarTwinkleSpeedId) ? m.GetFloat(StarTwinkleSpeedId) : 1.5f,
        starTwinkleAmount = m.HasProperty(StarTwinkleAmountId) ? m.GetFloat(StarTwinkleAmountId) : 0.5f,
        animate = m.HasProperty(AnimateId) ? m.GetFloat(AnimateId) : 1f,
        speed = m.HasProperty(SpeedId) ? m.GetFloat(SpeedId) : 0.05f,
    };

    void Write(NebulaState s)
    {
        for (int i = 0; i < _targets.Count; i++)
        {
            Material m = _targets[i];
            if (m == null) continue;

            if (m.HasProperty(ColorDarkId)) m.SetColor(ColorDarkId, s.dark);
            if (m.HasProperty(ColorMidId)) m.SetColor(ColorMidId, s.mid);
            if (m.HasProperty(ColorBrightId)) m.SetColor(ColorBrightId, s.bright);
            if (m.HasProperty(ColorStarId)) m.SetColor(ColorStarId, s.star);
            if (m.HasProperty(ScaleId)) m.SetFloat(ScaleId, s.scale);
            if (m.HasProperty(OctavesId)) m.SetFloat(OctavesId, s.octaves);
            if (m.HasProperty(PersistenceId)) m.SetFloat(PersistenceId, s.persistence);
            if (m.HasProperty(DensityId)) m.SetFloat(DensityId, s.density);
            if (m.HasProperty(SharpnessId)) m.SetFloat(SharpnessId, s.sharpness);
            if (m.HasProperty(StarScaleId)) m.SetFloat(StarScaleId, s.starScale);
            if (m.HasProperty(StarThresholdId)) m.SetFloat(StarThresholdId, s.starThreshold);
            if (m.HasProperty(StarBrightnessId)) m.SetFloat(StarBrightnessId, s.starBrightness);
            if (m.HasProperty(StarTwinkleId)) m.SetFloat(StarTwinkleId, s.starTwinkle >= 0.5f ? 1f : 0f);
            if (m.HasProperty(StarTwinkleSpeedId)) m.SetFloat(StarTwinkleSpeedId, s.starTwinkleSpeed);
            if (m.HasProperty(StarTwinkleAmountId)) m.SetFloat(StarTwinkleAmountId, s.starTwinkleAmount);
            if (m.HasProperty(AnimateId)) m.SetFloat(AnimateId, s.animate >= 0.5f ? 1f : 0f);
            if (m.HasProperty(SpeedId)) m.SetFloat(SpeedId, s.speed);
        }

        _current = s;
        _hasCurrent = true;
    }

    IEnumerator TweenTo(NebulaState goal, float duration)
    {
        NebulaState from = _current;
        float d = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < d)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / d);
            float e = t * t * (3f - 2f * t);
            Write(Lerp(from, goal, e));
            yield return null;
        }

        Write(goal);
        _tween = null;
    }

    static NebulaState Lerp(NebulaState a, NebulaState b, float t) => new NebulaState
    {
        dark = Color.Lerp(a.dark, b.dark, t),
        mid = Color.Lerp(a.mid, b.mid, t),
        bright = Color.Lerp(a.bright, b.bright, t),
        star = Color.Lerp(a.star, b.star, t),
        scale = Mathf.Lerp(a.scale, b.scale, t),
        octaves = Mathf.Lerp(a.octaves, b.octaves, t),
        persistence = Mathf.Lerp(a.persistence, b.persistence, t),
        density = Mathf.Lerp(a.density, b.density, t),
        sharpness = Mathf.Lerp(a.sharpness, b.sharpness, t),
        starScale = Mathf.Lerp(a.starScale, b.starScale, t),
        starThreshold = Mathf.Lerp(a.starThreshold, b.starThreshold, t),
        starBrightness = Mathf.Lerp(a.starBrightness, b.starBrightness, t),
        starTwinkle = Mathf.Lerp(a.starTwinkle, b.starTwinkle, t),
        starTwinkleSpeed = Mathf.Lerp(a.starTwinkleSpeed, b.starTwinkleSpeed, t),
        starTwinkleAmount = Mathf.Lerp(a.starTwinkleAmount, b.starTwinkleAmount, t),
        animate = Mathf.Lerp(a.animate, b.animate, t),
        speed = Mathf.Lerp(a.speed, b.speed, t),
    };
}

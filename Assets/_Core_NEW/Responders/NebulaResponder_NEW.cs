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
///
/// STARS ARE CROSSFADED, NOT BLENDED. A star is where a noise sampled at dir * starScale
/// passes starThreshold. Tweening either samples a different stretch of noise every frame,
/// so for the length of a blend every star on the sky blinks on and off — the Milky Way's
/// stars (scale 80) going to the Solar System's (20) over the five seconds of that dive did
/// exactly that. When two profiles' fields differ, the old field stays as it is and fades
/// out while the new one, drawn in the shader's second star layer, fades in.
///
/// LATER: STAR BY STAR. Faded as a whole, the old field's stars all dimmed together — on the
/// way into the Solar System, the Milky Way's starry sky simply went. Now each star goes out
/// at its own moment over the crossfade (the shader's _StarFade), and the new field's come
/// in the same way (_StarFade2): the sky thins out star by star.
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
    float _nextBlend = -1f;

    // How much of the old field is left, star by star, while crossfading; 1 otherwise.
    float _outgoing = 1f;

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
    static readonly int StarScale2Id = Shader.PropertyToID("_StarScale2");
    static readonly int StarThreshold2Id = Shader.PropertyToID("_StarThreshold2");
    static readonly int StarBrightness2Id = Shader.PropertyToID("_StarBrightness2");
    static readonly int StarFadeId = Shader.PropertyToID("_StarFade");
    static readonly int StarFade2Id = Shader.PropertyToID("_StarFade2");
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

    void OnDestroy()
    {
        // The materials other than the skybox are the assets themselves: leave every star there.
        WriteStarFades(1f, 1f);
        RestoreSkybox();
    }

    protected override TimedChannel_NEW SelectChannel(LayerProfile_NEW profile) => profile.timing.nebula;

    /// <summary>
    /// The next change blends over this many seconds, whatever its profile says. Every profile
    /// snaps (blend 0), which is right under a cover or a whiteout; a transition that hides
    /// nothing — LayerDive_NEW crossfading, or coming out of a cluster — asks for this just
    /// before the sky changes, so the snap does not show.
    /// </summary>
    public void BlendNextOver(float seconds) => _nextBlend = seconds;

    protected override void Apply(LayerProfile_NEW profile)
    {
        float requested = _nextBlend;
        _nextBlend = -1f;

        NebulaProfile_NEW np = profile.nebula;
        if (np == null)
        {
            if (debugLog) Debug.Log($"[NebulaResponder_NEW] '{profile.layerId}' has no nebula profile. " +
                                    "Sky left as-is.", this);
            return;
        }

        if (_targets.Count == 0) return;

        if (_tween != null) { StopCoroutine(_tween); _tween = null; }
        // Cut off mid-crossfade: what was left of the old field, all its stars dimmed by that much.
        if (_outgoing < 1f)
        {
            _current.starBrightness *= _outgoing;
            Write(_current);
        }
        WriteStarFades(1f, 1f);
        WriteIncomingStars(_current, 0f);

        NebulaState goal = FromProfile(np);
        float blend = requested > 0f ? requested : np.blendDuration;

        if (blend <= 0f || !_hasCurrent)
        {
            Write(goal);
            return;
        }

        _tween = StartCoroutine(TweenTo(goal, blend));
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

    /// <summary>
    /// The second star layer: <paramref name="field"/>'s stars at <paramref name="brightness"/>.
    /// 0 turns it off. Materials whose shader has no second layer are left alone.
    /// </summary>
    void WriteIncomingStars(NebulaState field, float brightness)
    {
        for (int i = 0; i < _targets.Count; i++)
        {
            Material m = _targets[i];
            if (m == null || !m.HasProperty(StarBrightness2Id)) continue;

            if (m.HasProperty(StarScale2Id)) m.SetFloat(StarScale2Id, field.starScale);
            if (m.HasProperty(StarThreshold2Id)) m.SetFloat(StarThreshold2Id, field.starThreshold);
            m.SetFloat(StarBrightness2Id, Mathf.Max(0f, brightness));
        }
    }

    /// <summary>
    /// How far each field is through a crossfade, star by star (the shader's _StarFade and
    /// _StarFade2): 1 every star there. Materials whose shader has no such fade are left alone.
    /// </summary>
    void WriteStarFades(float outgoing, float incoming)
    {
        _outgoing = outgoing;
        for (int i = 0; i < _targets.Count; i++)
        {
            Material m = _targets[i];
            if (m == null) continue;
            if (m.HasProperty(StarFadeId)) m.SetFloat(StarFadeId, outgoing);
            if (m.HasProperty(StarFade2Id)) m.SetFloat(StarFade2Id, incoming);
        }
    }

    /// <summary>True if every target can draw a second star layer to crossfade into.</summary>
    bool CanCrossfadeStars()
    {
        for (int i = 0; i < _targets.Count; i++)
            if (_targets[i] != null && !_targets[i].HasProperty(StarBrightness2Id)) return false;
        return _targets.Count > 0;
    }

    /// <summary>True if every target's stars can go out one by one.</summary>
    bool CanFadeStarByStar()
    {
        for (int i = 0; i < _targets.Count; i++)
            if (_targets[i] != null && !(_targets[i].HasProperty(StarFadeId) && _targets[i].HasProperty(StarFade2Id))) return false;
        return _targets.Count > 0;
    }

    /// <summary>The same stars in the same places: only their brightness and colour may differ.</summary>
    static bool SameStarField(NebulaState a, NebulaState b) =>
        Mathf.Approximately(a.starScale, b.starScale) && Mathf.Approximately(a.starThreshold, b.starThreshold);

    IEnumerator TweenTo(NebulaState goal, float duration)
    {
        NebulaState from = _current;
        float d = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        // Different fields: the old one keeps its stars where they are and they go out one by
        // one, the new one's come in on the second layer. See STARS ARE CROSSFADED, and STAR
        // BY STAR, on the class. A shader without the star-by-star fade dims them together.
        bool crossfade = !SameStarField(from, goal) && CanCrossfadeStars();
        bool byStar = crossfade && CanFadeStarByStar();

        while (elapsed < d)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / d);
            float e = t * t * (3f - 2f * t);

            NebulaState now = Lerp(from, goal, e);
            if (byStar)
            {
                now.starScale = from.starScale;
                now.starThreshold = from.starThreshold;
                now.starBrightness = from.starBrightness;
                Write(now);
                WriteStarFades(1f - e, e);
                WriteIncomingStars(goal, goal.starBrightness);
            }
            else if (crossfade)
            {
                now.starScale = from.starScale;
                now.starThreshold = from.starThreshold;
                now.starBrightness = from.starBrightness * (1f - e);
                Write(now);
                WriteIncomingStars(goal, goal.starBrightness * e);
            }
            else
            {
                Write(now);
            }
            yield return null;
        }

        Write(goal);
        WriteStarFades(1f, 1f);
        if (crossfade) WriteIncomingStars(goal, 0f);
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

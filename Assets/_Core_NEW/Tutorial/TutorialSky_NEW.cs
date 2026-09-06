using UnityEngine;

/// <summary>
/// Applies one NebulaProfile_NEW to the Custom/Nebula skybox, once, at startup.
///
/// The tutorial needs the quasar sky that PlaytestBuild has, and nothing else the sky
/// system does. NebulaResponder_NEW is the component that normally drives it, but it is
/// a LayerResponder_NEW: it subscribes to LayerState_NEW, waits on CameraDirector_NEW's
/// anchors and switches look on a gate crossing. Dragging that stack into a scene with
/// one layer and no gates would mean wiring four components to get one material set.
///
/// So this is the same write, minus the layer machinery. The values come from the same
/// NebulaProfile_NEW asset (NP_Quasar), so the tutorial sky and the journey's quasar sky
/// cannot drift apart — there is one set of numbers, in one asset.
///
/// The asset-safety behaviour is copied deliberately rather than simplified away.
/// NebulaResponder_NEW's summary records why: an earlier version wrote straight into
/// RenderSettings.skybox, which is the material asset on disk, and one playtest
/// permanently altered the .mat so the scene stopped being reproducible. This clones the
/// skybox into a runtime instance and restores the original on teardown.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("TUT SKY", "#4A94F2")]
public class TutorialSky_NEW : MonoBehaviour
{
    [Header("Look")]
    [Tooltip("Usually NP_Quasar. The tutorial opens inside the accretion disc, so it " +
             "wants the same sky the journey's quasar layer has.")]
    [SerializeField] NebulaProfile_NEW profile;

    [Tooltip("Shader name that marks a material as a nebula surface. Matches " +
             "NebulaResponder_NEW so both find the same materials.")]
    [SerializeField] string shaderName = "Custom/Nebula";

    [Header("Ambient")]
    [Tooltip("Also drive the ambient light, so unlit-looking geometry sits in the same " +
             "darkness as the sky. Off leaves the scene's own lighting alone.")]
    [SerializeField] bool driveAmbient = true;

    [SerializeField] Color ambientSky = new Color(0.06f, 0.04f, 0.10f, 1f);
    [SerializeField] Color ambientEquator = new Color(0.04f, 0.02f, 0.06f, 1f);
    [SerializeField] Color ambientGround = new Color(0.01f, 0.01f, 0.02f, 1f);

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    Material _original;
    Material _instance;

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

    void Awake()
    {
        Apply();
    }

    void OnDestroy()
    {
        Restore();
    }

    /// <summary>Write the profile into a runtime clone of the skybox.</summary>
    public void Apply()
    {
        Material sky = RenderSettings.skybox;

        if (sky == null)
        {
            Debug.LogWarning("[TutorialSky_NEW] No skybox assigned in this scene. " +
                             "Lighting > Environment > Skybox Material.", this);
            return;
        }

        if (sky.shader == null || sky.shader.name != shaderName)
        {
            Debug.LogWarning("[TutorialSky_NEW] The skybox is '" + sky.name + "' which does not " +
                             "use " + shaderName + ". Expected Custom_Nebula.mat.", this);
            return;
        }

        if (profile == null)
        {
            Debug.LogWarning("[TutorialSky_NEW] No NebulaProfile_NEW assigned. " +
                             "The sky keeps whatever the material already had.", this);
            return;
        }

        _original = sky;
        _instance = new Material(sky);
        _instance.name = sky.name + " (tutorial runtime)";
        RenderSettings.skybox = _instance;

        Write(_instance, profile);

        if (driveAmbient)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
        }

        if (debugLog)
            Debug.Log("[TutorialSky_NEW] Applied " + profile.name + " to a runtime clone of " +
                      _original.name + ". The project asset is untouched.", this);
    }

    void Restore()
    {
        if (_original == null) return;

        RenderSettings.skybox = _original;

        if (_instance != null)
        {
            if (Application.isPlaying) Destroy(_instance);
            else DestroyImmediate(_instance);
        }

        _original = null;
        _instance = null;
    }

    static void Write(Material m, NebulaProfile_NEW p)
    {
        m.SetColor(ColorDarkId, p.colorDark);
        m.SetColor(ColorMidId, p.colorMid);
        m.SetColor(ColorBrightId, p.colorBright);
        m.SetColor(ColorStarId, p.colorStar);

        m.SetFloat(ScaleId, p.scale);
        m.SetFloat(OctavesId, p.octaves);
        m.SetFloat(PersistenceId, p.persistence);
        m.SetFloat(DensityId, p.density);
        m.SetFloat(SharpnessId, p.sharpness);

        m.SetFloat(StarScaleId, p.starScale);
        m.SetFloat(StarThresholdId, p.starThreshold);
        m.SetFloat(StarBrightnessId, p.starBrightness);

        m.SetFloat(StarTwinkleId, p.starTwinkle >= 0.5f ? 1f : 0f);
        m.SetFloat(StarTwinkleSpeedId, p.starTwinkleSpeed);
        m.SetFloat(StarTwinkleAmountId, p.starTwinkleAmount);

        m.SetFloat(AnimateId, p.animate);
        m.SetFloat(SpeedId, p.speed);
    }
}

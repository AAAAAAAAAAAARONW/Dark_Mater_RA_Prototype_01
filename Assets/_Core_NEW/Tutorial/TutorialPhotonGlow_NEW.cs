using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// The light itself, seen: a point brighter than the white-hot gas round the black hole,
/// with the spikes and the streak a lens draws on a point that bright. Until C2 the camera is
/// the light and there is nothing to see of it; then TutorialDiscOrbit_NEW holds the camera
/// back, and this is what it watches — waiting at the black hole's edge, pulsing — and what it
/// follows as it leaves, until the trail it draws has taken over and this fades.
///
/// On the light, at its trail's head. Drawn by Resources/DiveRemnant_NEW.shader, sized on the
/// screen (a point however near or far), after the quasar (queue 3005) so the disc's gas never
/// dims it. Its own quad and material, made at Awake, never saved.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("PHOTON GLOW", "#BFD8FF")]
public class TutorialPhotonGlow_NEW : MonoBehaviour
{
    [Header("Look")]
    [Tooltip("Colour times brightness. A little bluer than the gas, so it stands out of the white round the hole.")]
    [ColorUsage(false, true)] [SerializeField] Color colour = new Color(3.5f, 3.8f, 4.8f, 1f);

    [Tooltip("Core radius, as a fraction of the screen's height.")]
    [Min(0f)] [SerializeField] float size = 0.0035f;

    [Tooltip("Halo radius, as a fraction of the screen's height, and its brightness.")]
    [SerializeField] Vector2 halo = new Vector2(0.025f, 0.18f);

    [Tooltip("Diffraction spikes: brightness, length (of half the screen's height) and angle in degrees.")]
    [SerializeField] Vector3 spikes = new Vector3(1.5f, 0.2f, 45f);

    [Tooltip("The horizontal streak a wide lens draws through a point that bright.")]
    [Min(0f)] [SerializeField] float streak = 0.8f;

    [Header("Waiting")]
    [Tooltip("How much it swells and dims while it waits to be let go, and how many times a second.")]
    [SerializeField] Vector2 pulse = new Vector2(0.35f, 1.1f);

    [SerializeField] int renderQueue = 3005;

    Material _material;
    Mesh _quad;
    MeshRenderer _renderer;
    float _level, _target, _rate = 1f;
    bool _pulsing;
    float _pulsePhase;

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int FadeId = Shader.PropertyToID("_Fade");
    static readonly int SizeId = Shader.PropertyToID("_Size");
    static readonly int HaloId = Shader.PropertyToID("_Halo");
    static readonly int HaloAmountId = Shader.PropertyToID("_HaloAmount");
    static readonly int SpikesId = Shader.PropertyToID("_Spikes");
    static readonly int SpikeLengthId = Shader.PropertyToID("_SpikeLength");
    static readonly int SpikeAngleId = Shader.PropertyToID("_SpikeAngle");
    static readonly int StreakId = Shader.PropertyToID("_Streak");

    /// <summary>Swell and dim while waiting (C2); steady otherwise.</summary>
    public bool Pulsing { get { return _pulsing; } set { _pulsing = value; } }

    /// <summary>Come up over this many seconds.</summary>
    public void Show(float seconds) { _target = 1f; _rate = seconds > 0f ? 1f / seconds : 1e6f; }

    /// <summary>Go over this many seconds.</summary>
    public void Hide(float seconds) { _target = 0f; _rate = seconds > 0f ? 1f / seconds : 1e6f; }

    /// <summary>Gone at once: the attract reset.</summary>
    public void HideNow()
    {
        _level = _target = 0f;
        _pulsing = false;
        Apply();
    }

    void Awake()
    {
        Shader shader = Resources.Load<Shader>("DiveRemnant_NEW");
        if (shader == null)
        {
            Debug.LogWarning("[TutorialPhotonGlow_NEW] Resources/DiveRemnant_NEW.shader is missing; the light is not drawn.", this);
            return;
        }

        _material = new Material(shader) { name = "Photon glow (runtime)", renderQueue = renderQueue };

        // A unit quad at the light; the shader turns it to the camera and sizes it on the
        // screen, so its bounds are made big enough never to be culled while it is in view.
        _quad = new Mesh { name = "Photon glow quad" };
        _quad.vertices = new[] { new Vector3(-1f, -1f, 0f), new Vector3(1f, -1f, 0f), new Vector3(1f, 1f, 0f), new Vector3(-1f, 1f, 0f) };
        _quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        _quad.bounds = new Bounds(Vector3.zero, Vector3.one * 1e5f);

        MeshFilter filter = GetComponent<MeshFilter>();
        if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
        filter.sharedMesh = _quad;
        _renderer = GetComponent<MeshRenderer>();
        if (_renderer == null) _renderer = gameObject.AddComponent<MeshRenderer>();
        _renderer.sharedMaterial = _material;
        _renderer.shadowCastingMode = ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
        _renderer.lightProbeUsage = LightProbeUsage.Off;
        _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        Apply();
    }

    void Update()
    {
        float dt = TutorialClock_NEW.DeltaTime;
        _level = Mathf.MoveTowards(_level, _target, _rate * dt);
        if (_pulsing) _pulsePhase += dt * pulse.y * Mathf.PI * 2f;
        Apply();
    }

    void Apply()
    {
        if (_material == null) return;

        bool on = _level > 0.001f;
        if (_renderer != null && _renderer.enabled != on) _renderer.enabled = on;
        if (!on) return;

        // Swelling: brighter and its spikes longer, never dimmer than it is when steady.
        float swell = _pulsing ? 1f + pulse.x * (0.5f + 0.5f * Mathf.Sin(_pulsePhase)) : 1f;

        _material.SetColor(ColorId, colour * swell);
        _material.SetFloat(FadeId, _level);
        _material.SetFloat(SizeId, size);
        _material.SetFloat(HaloId, halo.x * swell);
        _material.SetFloat(HaloAmountId, halo.y);
        _material.SetFloat(SpikesId, spikes.x);
        _material.SetFloat(SpikeLengthId, spikes.y * swell);
        _material.SetFloat(SpikeAngleId, spikes.z);
        _material.SetFloat(StreakId, streak);
    }

    void OnDestroy()
    {
        if (_material != null) Destroy(_material);
        if (_quad != null) Destroy(_quad);
    }
}

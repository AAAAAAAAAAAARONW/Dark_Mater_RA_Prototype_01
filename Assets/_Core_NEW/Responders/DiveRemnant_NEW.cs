using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// What a world becomes once the photon has left it for the scale above: a point of light in
/// the new world. The quasar, collapsing behind the light as it comes out into the cosmic web,
/// ends as one bright point among the web's knots — and stays there, part of the web.
///
/// LayerDive_NEW makes it (Create) at the centre of the world being left, as a child of the new
/// world's group root, so it moves and scales with that world in later dives; and hands over to
/// it (SetAppear) as the old world shrinks under a few pixels. The moment it has taken over it
/// flares — brighter, its spikes thrown out, a halo — and settles into a steady point.
///
/// Once the dive has ended (Settle) it fades with its world, by reading WorldSwitcher_NEW's
/// look for that world each frame, rather than being one of its renderers: it is made
/// mid-dive, after the groups were gathered, and gathering them again then would take the
/// dimmed lights as their own. Until then the dive alone says how much of it shows: it is
/// the old world, not part of the new one fading in round it.
///
/// Drawn by Resources/DiveRemnant_NEW.shader, on the screen: a point however far off.
/// </summary>
[DisallowMultipleComponent]
public class DiveRemnant_NEW : MonoBehaviour
{
    /// <summary>Seconds over which the flare, as it takes over, rises and dies away.</summary>
    const float FlareRise = 0.12f, FlareFall = 1.1f;

    WorldSwitcher_NEW _worlds;
    string _layerId;
    Material _material;
    Mesh _quad;
    Renderer _renderer;
    Color _colour;
    float _size, _spikes, _spikeLength, _streak;
    float _appear;
    float _flareAt = -1f;
    bool _settled;

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int FadeId = Shader.PropertyToID("_Fade");
    static readonly int SizeId = Shader.PropertyToID("_Size");
    static readonly int HaloId = Shader.PropertyToID("_Halo");
    static readonly int HaloAmountId = Shader.PropertyToID("_HaloAmount");
    static readonly int SpikesId = Shader.PropertyToID("_Spikes");
    static readonly int SpikeLengthId = Shader.PropertyToID("_SpikeLength");
    static readonly int SpikeAngleId = Shader.PropertyToID("_SpikeAngle");
    static readonly int SpikeTintId = Shader.PropertyToID("_SpikeTint");
    static readonly int StreakId = Shader.PropertyToID("_Streak");

    /// <summary>The layer whose world it is part of.</summary>
    public string LayerId => _layerId;

    /// <summary>
    /// A remnant at <paramref name="position"/>, part of <paramref name="layerId"/>'s world (a
    /// child of <paramref name="parent"/>, that world's root). Out of sight until SetAppear.
    /// <paramref name="size"/> is the core's radius as a fraction of the screen's height;
    /// <paramref name="spikes"/> the diffraction spikes' brightness, 0 for none, at
    /// <paramref name="spikeAngle"/> degrees and <paramref name="spikeLength"/> of half the screen's
    /// height long; <paramref name="streak"/> the horizontal streak's — as QuasarCore_NEW draws
    /// them. Null without its shader.
    /// </summary>
    public static DiveRemnant_NEW Create(string name, Transform parent, Vector3 position, WorldSwitcher_NEW worlds,
                                         string layerId, Color colour, float size, float spikes, float spikeAngle,
                                         float spikeLength, float streak)
    {
        Shader shader = Resources.Load<Shader>("DiveRemnant_NEW");
        if (shader == null)
        {
            Debug.LogWarning("[DiveRemnant_NEW] Resources/DiveRemnant_NEW.shader is missing; no remnant.");
            return null;
        }

        var go = new GameObject(name);
        if (parent != null)
        {
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, true);
        }
        go.transform.position = position;

        var remnant = go.AddComponent<DiveRemnant_NEW>();
        remnant._worlds = worlds;
        remnant._layerId = layerId;
        remnant._colour = colour;
        remnant._size = size;
        remnant._spikes = spikes;
        remnant._spikeLength = spikeLength;
        remnant._streak = streak;

        remnant._material = new Material(shader) { name = name + " (runtime)" };
        remnant._material.SetFloat(SpikeAngleId, spikeAngle);

        // A unit quad at the point; the shader turns it to the camera and sizes it on the
        // screen, so its bounds are made big enough never to be culled while it is in view.
        remnant._quad = new Mesh { name = name + " quad" };
        remnant._quad.vertices = new[] { new Vector3(-1f, -1f, 0f), new Vector3(1f, -1f, 0f), new Vector3(1f, 1f, 0f), new Vector3(-1f, 1f, 0f) };
        remnant._quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        remnant._quad.bounds = new Bounds(Vector3.zero, Vector3.one * 1e5f);

        go.AddComponent<MeshFilter>().sharedMesh = remnant._quad;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = remnant._material;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = LightProbeUsage.Off;
        mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        remnant._renderer = mr;

        remnant.Apply();
        return remnant;
    }

    /// <summary>
    /// How far it has taken over from the world it was, 0 to 1. It flares as it passes two
    /// thirds of the way.
    /// </summary>
    public void SetAppear(float appear)
    {
        _appear = Mathf.Clamp01(appear);
        if (_flareAt < 0f && _appear >= 0.66f) _flareAt = Time.time;
        Apply();
    }

    /// <summary>The dive is over: all there, and from now on as much as its world is.</summary>
    public void Settle()
    {
        _settled = true;
        SetAppear(1f);
    }

    void LateUpdate() => Apply();

    void Apply()
    {
        if (_material == null) return;

        float world = _settled && _worlds != null && !string.IsNullOrEmpty(_layerId) ? _worlds.GroupAlpha(_layerId) : 1f;
        float fade = _appear * Mathf.Clamp01(world);
        bool on = fade > 0.001f;
        if (_renderer != null && _renderer.enabled != on) _renderer.enabled = on;
        if (!on) return;

        // The flare: up in a moment, dying away over about a second.
        float since = _flareAt >= 0f ? Time.time - _flareAt : -1f;
        float flare = since >= 0f ? (1f - Mathf.Exp(-since / FlareRise)) * Mathf.Exp(-since / FlareFall) : 0f;

        _material.SetColor(ColorId, _colour * (1f + 1.6f * flare));
        _material.SetFloat(FadeId, fade);
        _material.SetFloat(SizeId, _size * (1f + 0.6f * flare));
        _material.SetFloat(HaloId, _size * (4f + 8f * flare));
        _material.SetFloat(HaloAmountId, 0.03f + 0.07f * flare);
        // The spikes thrown out, as the quasar's own are when it flashes.
        _material.SetFloat(SpikesId, _spikes * (1f + 0.8f * flare));
        _material.SetFloat(SpikeLengthId, _spikeLength * (1f + 1.5f * flare));
        _material.SetFloat(StreakId, _streak * (1f + flare));
    }

    void OnDestroy()
    {
        if (_material != null) Destroy(_material);
        if (_quad != null) Destroy(_quad);
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// How the light's trail looks in the journey: four layers on top of the ribbon, each one a
/// switch here so they can be compared on and off while flying.
///
///   A · HALO    a soft blue-white light around the ribbon, a few times its width. Built
///               by TrailFacing_NEW from the same points as the ribbon, so it follows
///               everything done to the trail (the long tether to the quasar, clears,
///               EarthSummit's narrowing); with the beam it is a soft round cloud (n·v), not
///               a band. No spectrum, and kept faint over the trail itself
///               so it never washes over the colours or fills an absorption line.
///   B · HEAD    a soft round glow at the head with a faint glow trailing back along the
///               ribbon (nothing reaching out ahead); the ribbon narrows from the head to
///               about 0.6 at the tail; the head cap is round and soft, sized in world units
///               (it was a share of the trail's length, so during the tether it was huge).
///   D · TUBE    the cross-section lit like a round beam with a soft edge (shader _Tube).
///   E · FLOW    a faint brightness ripple fixed to where the light has been, drifting back
///               along the trail (shader _Flow*). Multiplies, so absorbed wavelengths stay dark.
///
/// D and E are properties of PhotonTrailRainbow that default to 0, so the tutorial and every
/// other material using that shader look exactly as before.
///
/// Previewed in the "Photon Trail Looks" page before being built here; the numbers below are
/// that page's, converted to the trail's own width.
///
/// NOTHING TO WIRE. It adds itself at run time to the PhotonSpectrumTrail under a PlayerRig_NEW.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(1100)]
public class TrailLook_NEW : MonoBehaviour
{
    [Header("Soft beam (PhotonTrailRainbow _Soft)")]
    [Tooltip("The soft rainbow-like beam: CIE colours on the journey's wavelength axis, no rim, narrowing into the head.")]
    [SerializeField] bool soft = true;
    [Tooltip("World distance, in trail widths, over which the light narrows into the head.")]
    [Range(0.2f, 5f)] [SerializeField] float converge = 1.5f;
    [Tooltip("Head light size as a share of the screen height (the same however close the camera is).")]
    [Range(0.005f, 0.2f)] [SerializeField] float headScreenSize = 0.045f;

    [Header("A · Halo")]
    [SerializeField] bool halo = false;
    [Tooltip("Halo width, in trail widths.")]
    [Range(1.2f, 5f)] [SerializeField] float haloWidth = 2.6f;
    [Range(0f, 2f)] [SerializeField] float haloIntensity = 0.3f;
    [SerializeField] Color haloColor = new Color(0.69f, 0.84f, 1f, 1f);

    [Header("B · Head")]
    [SerializeField] bool head = true;
    [Tooltip("Diameter of the head glow, in trail widths.")]
    [Range(0.5f, 6f)] [SerializeField] float headSize = 1.2f;
    [Range(0f, 3f)] [SerializeField] float headIntensity = 0.9f;
    [Tooltip("The faint glow trailing back from the head along the ribbon.")]
    [Range(0f, 1f)] [SerializeField] float tailGlowIntensity = 0f;
    [SerializeField] Color headColor = new Color(0.67f, 0.84f, 1f, 1f);
    [Tooltip("Ribbon width at the tail, as a share of its width at the head.")]
    [Range(0.2f, 1f)] [SerializeField] float tailWidth = 0.6f;
    [Tooltip("Length of the round head cap, in trail widths. 0.5 is a half circle.")]
    [Range(0.1f, 2f)] [SerializeField] float capLength = 0.6f;

    [Header("D · Tube")]
    [Range(0f, 1f)] [SerializeField] float tube = 1f;

    [Header("E · Flow")]
    [Range(0f, 0.5f)] [SerializeField] float flowAmount = 0.22f;
    [Tooltip("World units from one bright ripple to the next.")]
    [Min(0.1f)] [SerializeField] float flowWavelength = 5f;
    [SerializeField] float flowSpeed = 2.2f;

    static readonly int TubeId = Shader.PropertyToID("_Tube");
    static readonly int HeadSoftId = Shader.PropertyToID("_HeadSoft");
    static readonly int HeadWidthId = Shader.PropertyToID("_HeadWidth");
    static readonly int FlowAmountId = Shader.PropertyToID("_FlowAmount");
    static readonly int FlowWavelengthId = Shader.PropertyToID("_FlowWavelength");
    static readonly int FlowSpeedId = Shader.PropertyToID("_FlowSpeed");
    static readonly int TrailLengthId = Shader.PropertyToID("_TrailLength");
    static readonly int FlowDistanceId = Shader.PropertyToID("_FlowDistance");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    static readonly int HollowId = Shader.PropertyToID("_Hollow");
    static readonly int CoreId = Shader.PropertyToID("_Core");
    static readonly int FalloffId = Shader.PropertyToID("_Falloff");
    static readonly int CoreFalloffId = Shader.PropertyToID("_CoreFalloff");
    static readonly int BeamId = Shader.PropertyToID("_Beam");
    static readonly int CullId = Shader.PropertyToID("_Cull");
    static readonly int HeadFadeId = Shader.PropertyToID("_HeadFade");
    static readonly int SoftId = Shader.PropertyToID("_Soft");
    static readonly int SoftPaletteId = Shader.PropertyToID("_SoftPalette");
    static readonly int SoftConvergeId = Shader.PropertyToID("_SoftConverge");
    static readonly int SoftDistanceId = Shader.PropertyToID("_SoftDistance");

    BakedSpectrumSource_NEW _source;
    Texture2D _palette;
    float _flown60;

    TrailRenderer _trail;
    TrailFacing_NEW _ribbon;
    PlayerRig_NEW _player;
    AnimationCurve _ownCurve;
    int _ownCorners;
    bool _tapered;
    float _headWidthFactor = 1f;

    MeshRenderer _halo;
    Mesh _haloMesh;
    Material _haloMat;
    Transform _core, _tail;
    MeshRenderer _coreRenderer, _tailRenderer;
    Material _coreMat, _tailMat;
    Mesh _quad;

    Vector3 _lastHead;
    float _flown;
    bool _ready;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Install();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { Install(); }

    static void Install()
    {
        // Vizlab_Journey only: the other journey scenes (PC_Journey, Test_Journey...) keep the trail as it was.
        if (SceneManager.GetActiveScene().name != "Vizlab_Journey") return;
        PlayerRig_NEW player = FindObjectOfType<PlayerRig_NEW>();
        if (player == null) return;
        PhotonSpectrumTrail spectrum = player.GetComponentInChildren<PhotonSpectrumTrail>(true);
        if (spectrum == null || spectrum.GetComponent<TrailLook_NEW>() != null) return;
        spectrum.gameObject.AddComponent<TrailLook_NEW>();
    }

    void Start()
    {
        // Start, not Awake: PhotonSpectrumTrail has set its width curve and gradient by now.
        _trail = GetComponent<TrailRenderer>();
        _ribbon = GetComponent<TrailFacing_NEW>();
        if (_ribbon == null) _ribbon = gameObject.AddComponent<TrailFacing_NEW>();
        _player = GetComponentInParent<PlayerRig_NEW>();
        if (_trail == null) { enabled = false; return; }

        _ownCurve = _trail.widthCurve;
        _ownCorners = _trail.numCornerVertices;
        _headWidthFactor = Mathf.Max(0.01f, _ownCurve.Evaluate(0f));
        _trail.numCornerVertices = Mathf.Max(_ownCorners, 2);   // no hard corners where it bends
        _lastHead = transform.position;

        Shader haloShader = Resources.Load<Shader>("PhotonTrailHalo_NEW");
        Shader glowShader = Resources.Load<Shader>("PhotonHeadGlow_NEW");

        if (haloShader != null)
        {
            _haloMesh = TrailFacing_NEW.NewMesh("Trail halo");
            _halo = _ribbon.MakeRenderer("Trail Halo (look)", _haloMesh);
            _haloMat = new Material(haloShader) { name = "Trail Halo (runtime)" };
            // Before the trail, so the trail's black absorption pass still lands on top.
            Material trailMat = _trail.sharedMaterial;
            if (trailMat != null) _haloMat.renderQueue = trailMat.renderQueue - 1;
            _halo.sharedMaterial = _haloMat;
        }
        else Debug.LogWarning("[TrailLook_NEW] PhotonTrailHalo_NEW shader not found in Resources; no halo.", this);

        if (glowShader != null)
        {
            _quad = BuildQuad();
            _coreMat = new Material(glowShader) { name = "Head Glow (runtime)" };
            _coreMat.SetFloat(FalloffId, 7f);
            _coreMat.SetFloat(CoreFalloffId, 40f);
            _tailMat = new Material(glowShader) { name = "Head Tail Glow (runtime)" };
            _tailMat.SetFloat(FalloffId, 3f);
            _tailMat.SetFloat(CoreId, 0f);
            _core = MakeQuad("Head Glow (look)", _coreMat, out _coreRenderer);
            _tail = MakeQuad("Head Tail Glow (look)", _tailMat, out _tailRenderer);
        }
        else Debug.LogWarning("[TrailLook_NEW] PhotonHeadGlow_NEW shader not found in Resources; no head glow.", this);

        _ready = true;
    }

    Transform MakeQuad(string name, Material mat, out MeshRenderer renderer)
    {
        var go = new GameObject(name) { layer = gameObject.layer };
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = _quad;
        renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return go.transform;
    }

    static Mesh BuildQuad()
    {
        var m = new Mesh { name = "Head glow quad" };
        m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0) };
        m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        m.RecalculateBounds();
        return m;
    }

    void LateUpdate()
    {
        if (!_ready) return;

        Vector3 headPos = transform.position;
        float step = (headPos - _lastHead).magnitude;
        _lastHead = headPos;
        _flown = Mathf.Repeat(_flown + step, flowWavelength);
        _flown60 = Mathf.Repeat(_flown60 + step, 60f);      // the soft beam's drifts repeat every 60 units
        if (soft && _palette == null) BuildPalette();   // only the phase matters; keeps the shader's sin precise

        // TrailFacing_NEW (earlier in LateUpdate) has gathered this frame's ribbon.
        int n = _ribbon != null && _ribbon.isActiveAndEnabled ? _ribbon.Count : 0;
        float length = n > 0 ? _ribbon.Length : 0.01f;
        bool visible = _trail.enabled && n >= 2;

        // B · taper (applied only when it changes; widthCurve allocates).
        bool wantTaper = head;
        if (wantTaper != _tapered)
        {
            AnimationCurve curve = wantTaper ? new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, tailWidth)) : _ownCurve;
            _trail.widthCurve = curve;
            if (_ribbon != null) _ribbon.SetWidthCurve(curve);
            _tapered = wantTaper;
        }
        float width = _trail.widthMultiplier * (_tapered ? 1f : _headWidthFactor);

        // D, E and the head cap, on the trail's own material.
        Material mat = _trail.sharedMaterial;
        if (mat != null && mat.HasProperty(TubeId))
        {
            mat.SetFloat(TubeId, tube);
            mat.SetFloat(FlowAmountId, flowAmount);
            mat.SetFloat(FlowWavelengthId, flowWavelength);
            mat.SetFloat(FlowSpeedId, flowSpeed);
            mat.SetFloat(TrailLengthId, length);
            mat.SetFloat(FlowDistanceId, _flown);
            mat.SetFloat(HeadSoftId, head ? 1f : 0f);
            bool softOn = soft && _palette != null;
            mat.SetFloat(SoftId, softOn ? 1f : 0f);
            if (softOn)
            {
                mat.SetTexture(SoftPaletteId, _palette);
                mat.SetFloat(SoftConvergeId, converge * width);
                mat.SetFloat(SoftDistanceId, _flown60);
            }
            // After PhotonSpectrumTrail's Update, which writes its own share-of-length value.
            // A beam is rounded by its own geometry (TrailFacing_NEW), so the shader's cap is put out of the way.
            if (head) mat.SetFloat(HeadWidthId, _ribbon != null && _ribbon.Beam ? 0.001f : Mathf.Clamp(capLength * width / length, 0.001f, 0.5f));
        }

        // A · halo.
        if (_halo != null)
        {
            bool on = halo && visible;
            _halo.enabled = on;
            if (on)
            {
                _ribbon.Build(_haloMesh, haloWidth);
                bool beam = _ribbon.Beam;
                _haloMat.SetFloat(BeamId, beam ? 1f : 0f);
                _haloMat.SetFloat(CullId, beam ? (float)UnityEngine.Rendering.CullMode.Back : (float)UnityEngine.Rendering.CullMode.Off);
                _haloMat.SetFloat(HeadFadeId, Mathf.Clamp01(0.5f * haloWidth * width / Mathf.Max(length, 1e-4f)));
                _haloMat.SetColor(ColorId, haloColor);
                _haloMat.SetFloat(IntensityId, haloIntensity);
                _haloMat.SetFloat(HollowId, 1f / haloWidth);
            }
        }

        // B · head glow, facing the camera.
        if (_core != null)
        {
            Camera cam = Camera.main;
            bool on = head && visible && cam != null;
            _coreRenderer.enabled = on;
            _tailRenderer.enabled = on && tailGlowIntensity > 0f;
            if (on)
            {
                Vector3 camF = cam.transform.forward;
                Vector3 parentScale = transform.lossyScale;
                // Soft beam: a small point of light, the same size on screen however near.
                float d = soft
                    ? Vector3.Distance(cam.transform.position, headPos) * 2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * headScreenSize
                    : width * headSize;

                _core.position = headPos;
                _core.rotation = cam.transform.rotation;
                _core.localScale = Divide(new Vector3(d, d, 1f), parentScale);
                _coreMat.SetColor(ColorId, headColor);
                _coreMat.SetFloat(IntensityId, headIntensity);

                Vector3 travel = _player != null ? _player.MovementDirection : Vector3.forward;
                if (travel.sqrMagnitude < 1e-8f) travel = Vector3.forward;
                Vector3 across = travel - Vector3.Dot(travel, camF) * camF;   // the flight as seen on screen
                if (across.sqrMagnitude < 1e-6f) across = cam.transform.right;
                across.Normalize();
                _tail.position = headPos - travel.normalized * (d * 0.45f);
                _tail.rotation = Quaternion.LookRotation(camF, Vector3.Cross(camF, across));
                _tail.localScale = Divide(new Vector3(d * 2f, d * 0.7f, 1f), parentScale);
                _tailMat.SetColor(ColorId, headColor);
                _tailMat.SetFloat(IntensityId, tailGlowIntensity);
            }
        }
    }

    /// <summary>
    /// The soft beam's colours, on the same axis as the spectrum texture (BakedSpectrumSource_NEW
    /// TrailLambda): UV icy blue to violet, visible from the CIE 1931 matching functions (Wyman,
    /// Sloan & Shirley 2013 fit), IR ember red. Brightness in alpha. As in the Photon Soft Beam page.
    /// </summary>
    void BuildPalette()
    {
        if (_source == null) _source = FindObjectOfType<BakedSpectrumSource_NEW>();
        if (_source == null || double.IsNaN(_source.TrailLambda(0.5f))) return;
        const int W = 512;
        _palette = new Texture2D(W, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Soft beam palette" };
        var px = new Color[W];
        Color uvHi = new Color(0.62f, 0.86f, 1f), uvLo = new Color(0.58f, 0.46f, 1f), irA = new Color(0.95f, 0.22f, 0.10f), irB = new Color(0.55f, 0.05f, 0.04f);
        for (int x = 0; x < W; x++)
        {
            float t = (x + 0.5f) / W;
            double lam = _source.TrailLambda(t);
            Color c; float br;
            if (lam < 3900) { c = Color.Lerp(uvHi, uvLo, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Mathf.Log(1300f), Mathf.Log(4000f), Mathf.Log((float)lam)))); br = 0.7f; }
            else if (lam < 4200) { float k = (float)(lam - 3900) / 300f; c = Color.Lerp(uvLo, Cie(420f), k); br = 0.7f + 0.55f * k; }
            else if (lam <= 6900) { c = Cie((float)lam / 10f); br = 1.25f; }
            else if (lam < 7200) { float k = (float)(lam - 6900) / 300f; c = Color.Lerp(Cie(690f), irA, k); br = 1.25f - 0.4f * k; }
            else { c = Color.Lerp(irA, irB, Mathf.InverseLerp(7200f, 9000f, (float)lam)); br = 0.85f; }
            br *= Mathf.SmoothStep(0f, 1f, t / 0.1f);                     // far-UV margin
            br *= 1f - 0.75f * Mathf.SmoothStep(0f, 1f, (t - 0.93f) / 0.07f);
            c.a = Mathf.Clamp01(br / 1.25f);                              // shader: rgb * a; brightness 1.25 at most
            px[x] = c;
        }
        _palette.SetPixels(px);
        _palette.Apply(false);
    }

    static float G(float x, float mu, float s1, float s2) { float d = (x - mu) / (x < mu ? s1 : s2); return Mathf.Exp(-0.5f * d * d); }

    /// <summary>Visible colour at nm from the CIE 1931 2° functions, out-of-gamut lifted with white, max channel 1, sRGB.</summary>
    static Color Cie(float nm)
    {
        float X = 1.056f * G(nm, 599.8f, 37.9f, 31.0f) + 0.362f * G(nm, 442.0f, 16.0f, 26.7f) - 0.065f * G(nm, 501.1f, 20.4f, 26.2f);
        float Y = 0.821f * G(nm, 568.8f, 46.9f, 40.5f) + 0.286f * G(nm, 530.9f, 16.3f, 31.1f);
        float Z = 1.217f * G(nm, 437.0f, 11.8f, 36.0f) + 0.681f * G(nm, 459.0f, 26.0f, 13.8f);
        float r = 3.2406f * X - 1.5372f * Y - 0.4986f * Z, g = -0.9689f * X + 1.8758f * Y + 0.0415f * Z, b = 0.0557f * X - 0.2040f * Y + 1.0570f * Z;
        float m = Mathf.Min(r, Mathf.Min(g, b)); if (m < 0f) { r -= m; g -= m; b -= m; }
        float M = Mathf.Max(1e-6f, Mathf.Max(r, Mathf.Max(g, b)));
        return new Color(Enc(r / M), Enc(g / M), Enc(b / M), 1f);
    }

    static float Enc(float c) { return c <= 0.0031308f ? 12.92f * c : 1.055f * Mathf.Pow(c, 1f / 2.4f) - 0.055f; }

    static Vector3 Divide(Vector3 a, Vector3 b)
    {
        return new Vector3(a.x / Mathf.Max(1e-4f, Mathf.Abs(b.x)), a.y / Mathf.Max(1e-4f, Mathf.Abs(b.y)), a.z / Mathf.Max(1e-4f, Mathf.Abs(b.z)));
    }

    void OnDisable()
    {
        if (_halo != null) _halo.enabled = false;
        if (_coreRenderer != null) _coreRenderer.enabled = false;
        if (_tailRenderer != null) _tailRenderer.enabled = false;
        if (_trail == null) return;
        Material mat = _trail.sharedMaterial;
        if (mat != null && mat.HasProperty(TubeId))
        {
            mat.SetFloat(TubeId, 0f);
            mat.SetFloat(FlowAmountId, 0f);
            mat.SetFloat(HeadSoftId, 0f);
        }
        if (_tapered && _ownCurve != null)
        {
            _trail.widthCurve = _ownCurve;
            if (_ribbon != null) _ribbon.SetWidthCurve(_ownCurve);
            _tapered = false;
        }
    }

    void OnDestroy()
    {
        if (_trail != null) _trail.numCornerVertices = _ownCorners;
        if (_haloMat != null) Destroy(_haloMat);
        if (_coreMat != null) Destroy(_coreMat);
        if (_tailMat != null) Destroy(_tailMat);
        if (_quad != null) Destroy(_quad);
        if (_palette != null) Destroy(_palette);
        if (_haloMesh != null) Destroy(_haloMesh);
    }
}

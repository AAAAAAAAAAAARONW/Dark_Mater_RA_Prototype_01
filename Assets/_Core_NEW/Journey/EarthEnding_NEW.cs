using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The journey's last moment: the light comes down out of the sky into the 100-inch's dome,
/// and the spectrum it has gathered all the way from the quasar fills the screen while the
/// Closing line is said.
///
/// WHEN. Once EarthSummit_NEW has the camera standing on the summit (Landed), after
/// lightAfter seconds a streak of light falls from high behind the camera into the open
/// slit, faster as it comes, over flightSeconds. As it goes in, the dome's red inside light
/// flashes, and the Closing line starts. spectrumAfter seconds later the picture darkens and
/// the spectrum bar grows from its place on the HUD to the middle of the screen.
///
/// THE SPECTRUM. It is the HUD's own bar (SpectrumHUD_NEW's panel, the 'LAF HUD' on row 2),
/// with every line the light crossed cut into it, not a copy: taken out of its row (its row
/// components are paused) onto the same canvas's root and grown there, a black backdrop
/// fading in behind it on that canvas. On the wall the HUD is the wall's own; nothing here
/// draws per copy.
///
/// THE VOICE. Closing is said from here, not from the Earth row of JourneyVoiceOver_NEW
/// (which keeps J9), so it lands on the spectrum rather than on the way down.
///
/// Replaces EarthCutscene_NEW's video, which is switched off. Put it on the Solar System's
/// group root, beside EarthSummit_NEW.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EarthSummit_NEW))]
[HierarchyBadge_NEW("ENDING", "#4A94F2")]
public class EarthEnding_NEW : MonoBehaviour
{
    [Header("The light into the dome")]
    [Tooltip("Seconds after the camera stands on the summit before the light comes down.")]
    [Min(0f)] [SerializeField] float lightAfter = 1f;

    [Tooltip("Seconds the light takes to come down into the slit.")]
    [Min(0.3f)] [SerializeField] float flightSeconds = 3.5f;

    [Tooltip("Where it starts: km above the slit, and km behind the camera's view.")]
    [SerializeField] Vector2 fromKm = new Vector2(1.2f, 0.6f);

    [Tooltip("How wide the streak is, metres.")]
    [Min(0.1f)] [SerializeField] float lightWidthMetres = 10f;

    [SerializeField] Color lightColour = new Color(1f, 0.97f, 0.9f, 1f);

    [Tooltip("How bright the dome's inside flashes as the light goes in, times its usual light.")]
    [Min(1f)] [SerializeField] float flash = 5f;

    [Header("The spectrum")]
    [Tooltip("Seconds after the light goes in before the spectrum comes up.")]
    [Min(0f)] [SerializeField] float spectrumAfter = 1f;

    [Tooltip("Seconds it takes to grow to the middle of the screen.")]
    [Min(0.1f)] [SerializeField] float growSeconds = 2.5f;

    [Tooltip("Where it ends, as a fraction of the screen: x min, y min, x max, y max.")]
    [SerializeField] Vector4 spectrumRect = new Vector4(0.06f, 0.3f, 0.94f, 0.7f);

    [Tooltip("How dark the picture goes behind it.")]
    [Range(0f, 1f)] [SerializeField] float backdrop = 0.9f;

    [Header("Voice")]
    [Tooltip("Said as the light goes in.")]
    [SerializeField] AudioClip closing;

    [Header("Wiring (found if empty)")]
    [SerializeField] JourneyVoiceOver_NEW voiceOver;
    [SerializeField] SpectrumHUD_NEW spectrum;

    [SerializeField] bool debugLog = false;

    enum Stage { Waiting, Falling, Inside, Growing, Done }

    EarthSummit_NEW _summit;
    Stage _stage = Stage.Waiting;
    float _clock;

    GameObject _light;
    TrailRenderer _trail;
    Material _lightMaterial;
    Vector3 _from, _to;

    RectTransform _panel, _panelParent, _root;
    int _panelSibling;
    Vector2 _anchorMinFrom, _anchorMaxFrom;                       // on the root, as it starts to grow
    Vector2 _origAnchorMin, _origAnchorMax, _origOffsetMin, _origOffsetMax, _origPivot;   // in its row
    Vector2 _growMin, _growMax;
    Behaviour[] _rowParts;
    Image _black;

    void Awake()
    {
        _summit = GetComponent<EarthSummit_NEW>();
        if (voiceOver == null) voiceOver = FindObjectOfType<JourneyVoiceOver_NEW>();
        if (spectrum == null) spectrum = FindObjectOfType<SpectrumHUD_NEW>();
    }

    void Update()
    {
        switch (_stage)
        {
            case Stage.Waiting:
                if (!_summit.Landed) return;
                _clock += Time.deltaTime;
                if (_clock >= lightAfter) BeginFall();
                break;

            case Stage.Falling:
                _clock += Time.deltaTime;
                float p = Mathf.Clamp01(_clock / flightSeconds);
                // Faster as it comes, as a thing falling does.
                _light.transform.position = Vector3.Lerp(_from, _to, p * p * p);
                if (p >= 1f) GoIn();
                break;

            case Stage.Inside:
                _clock += Time.deltaTime;
                _summit.SetInsideLight(Mathf.Lerp(flash, 1f, Mathf.Clamp01(_clock / 1.5f)));
                if (_clock >= spectrumAfter) BeginGrow();
                break;

            case Stage.Growing:
                _clock += Time.deltaTime;
                _summit.SetInsideLight(1f);
                float g = Mathf.SmoothStep(0f, 1f, _clock / growSeconds);
                _panel.anchorMin = Vector2.Lerp(_anchorMinFrom, _growMin, g);
                _panel.anchorMax = Vector2.Lerp(_anchorMaxFrom, _growMax, g);
                if (_black != null) _black.color = new Color(0f, 0f, 0f, backdrop * g);
                if (g >= 1f) _stage = Stage.Done;
                break;
        }
    }

    // ── The light ───────────────────────────────────────────────────────────

    void BeginFall()
    {
        _stage = Stage.Falling;
        _clock = 0f;

        float km = _summit.KmToWorld;
        _to = _summit.SlitPoint;
        _from = _to + _summit.Up * fromKm.x * km - _summit.ViewLevel * fromKm.y * km;

        _light = new GameObject("Light into the dome") { hideFlags = HideFlags.DontSave };
        _light.layer = gameObject.layer;
        _light.transform.position = _from;

        _trail = _light.AddComponent<TrailRenderer>();
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        _lightMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
        _trail.sharedMaterial = _lightMaterial;
        _trail.time = 0.8f;
        _trail.minVertexDistance = 0.001f * km;
        _trail.widthMultiplier = lightWidthMetres * 0.001f * km;
        _trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(lightColour, 0f), new GradientColorKey(lightColour, 1f) },
                     new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        _trail.colorGradient = fade;
        _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _trail.receiveShadows = false;

        if (debugLog) Debug.Log("[EarthEnding_NEW] The light comes down into the dome.", this);
    }

    void GoIn()
    {
        _stage = Stage.Inside;
        _clock = 0f;
        _trail.emitting = false;
        _summit.SetInsideLight(flash);

        if (voiceOver != null && closing != null) voiceOver.Say(closing);
        else if (closing == null) Debug.LogWarning("[EarthEnding_NEW] No Closing clip assigned; nothing is said.", this);
    }

    // ── The spectrum ────────────────────────────────────────────────────────

    void BeginGrow()
    {
        _stage = Stage.Done;
        if (spectrum == null || spectrum.transform.parent == null)
        {
            Debug.LogWarning("[EarthEnding_NEW] No SpectrumHUD_NEW to show.", this);
            return;
        }

        _panel = spectrum.transform.parent as RectTransform;
        Canvas canvas = _panel != null ? _panel.GetComponentInParent<Canvas>() : null;
        if (_panel == null || canvas == null) return;
        _root = canvas.rootCanvas.transform as RectTransform;

        // Out of its row, onto the canvas's root, keeping where it is.
        _rowParts = new Behaviour[] { _panel.GetComponent<VizlabRowAnchor_NEW>(), _panel.GetComponent<VizlabRowShifter_NEW>() };
        foreach (Behaviour b in _rowParts) if (b != null) b.enabled = false;
        _panelParent = _panel.parent as RectTransform;
        _panelSibling = _panel.GetSiblingIndex();
        _origAnchorMin = _panel.anchorMin; _origAnchorMax = _panel.anchorMax;
        _origOffsetMin = _panel.offsetMin; _origOffsetMax = _panel.offsetMax;
        _origPivot = _panel.pivot;

        var corners = new Vector3[4];
        _panel.GetWorldCorners(corners);
        _panel.SetParent(_root, true);
        Rect r = _root.rect;
        Vector2 a = (Vector2)_root.InverseTransformPoint(corners[0]), b2 = (Vector2)_root.InverseTransformPoint(corners[2]);
        Vector2 min = new Vector2((a.x - r.xMin) / r.width, (a.y - r.yMin) / r.height);
        Vector2 max = new Vector2((b2.x - r.xMin) / r.width, (b2.y - r.yMin) / r.height);
        _panel.anchorMin = min; _panel.anchorMax = max;
        _panel.offsetMin = Vector2.zero; _panel.offsetMax = Vector2.zero;
        _anchorMinFrom = min; _anchorMaxFrom = max;
        _growMin = new Vector2(spectrumRect.x, spectrumRect.y);
        _growMax = new Vector2(spectrumRect.z, spectrumRect.w);

        // The picture darkens behind it, on the same canvas.
        var go = new GameObject("Ending backdrop", typeof(RectTransform)) { hideFlags = HideFlags.DontSave };
        go.layer = _panel.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(_root, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.SetSiblingIndex(_panel.GetSiblingIndex());
        _black = go.AddComponent<Image>();
        _black.color = new Color(0f, 0f, 0f, 0f);
        _black.raycastTarget = false;

        _stage = Stage.Growing;
        _clock = 0f;
        if (debugLog) Debug.Log("[EarthEnding_NEW] The spectrum comes up.", this);
    }

    void OnDisable()
    {
        if (_light != null) Destroy(_light);
        if (_lightMaterial != null) Destroy(_lightMaterial);
        if (_black != null) Destroy(_black.gameObject);
        if (_panel != null && _panelParent != null)
        {
            _panel.SetParent(_panelParent, false);
            _panel.SetSiblingIndex(_panelSibling);
            _panel.anchorMin = _origAnchorMin; _panel.anchorMax = _origAnchorMax;
            _panel.offsetMin = _origOffsetMin; _panel.offsetMax = _origOffsetMax;
            _panel.pivot = _origPivot;
            foreach (Behaviour b in _rowParts) if (b != null) b.enabled = true;
        }
        if (_summit != null) _summit.SetInsideLight(1f);
    }
}

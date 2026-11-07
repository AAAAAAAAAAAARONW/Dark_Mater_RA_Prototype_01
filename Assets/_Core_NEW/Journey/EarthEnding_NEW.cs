using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The journey's last moment: as the light - the player - goes into the 100-inch's dome
/// (EarthSummit_NEW flies it in), the red light inside flashes and the Closing line is
/// said; once the picture has cut to the observatory from outside, the spectrum the light
/// gathered all the way from the quasar fills the screen.
///
/// THE SPECTRUM. It is the HUD's own bar (SpectrumHUD_NEW's panel, the 'LAF HUD' on row 2),
/// with every line the light crossed cut into it, not a copy: taken out of its row (its row
/// components are paused) onto the same canvas's root and grown there, a black backdrop
/// fading in behind it on that canvas. On the wall the HUD is the wall's own; nothing here
/// draws per copy.
///
/// THE VOICE. Closing is said from here, not from the Earth row of JourneyVoiceOver_NEW
/// (which keeps J9), so it lands on the light going in rather than on the way down.
///
/// Replaces EarthCutscene_NEW's video, which is switched off. Put it on the Solar System's
/// group root, beside EarthSummit_NEW.
///
/// AFTER. With nextScenePath set (the PC journey sets it to the credits), once the spectrum
/// is up and the Closing line has been said, the picture holds, fades to black and the next
/// scene loads. Empty, as in Vizlab_Journey, and the piece stays on the spectrum as before.
///
/// 6 Nov: it used to send a streak of its own down into the dome while the camera looked on
/// from the summit; now the light is the player's own, ridden in from behind.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EarthSummit_NEW))]
[HierarchyBadge_NEW("ENDING", "#4A94F2")]
public class EarthEnding_NEW : MonoBehaviour
{
    [Header("The light going in")]
    [Tooltip("How bright the dome's inside flashes as the light goes in, times its usual light.")]
    [Min(1f)] [SerializeField] float flash = 5f;

    [Tooltip("Seconds the flash takes to die down.")]
    [Min(0.1f)] [SerializeField] float flashSeconds = 1.5f;

    [Header("The spectrum")]
    [Tooltip("Seconds after the cut to the observatory from outside before the spectrum comes up.")]
    [Min(0f)] [SerializeField] float spectrumAfter = 2.5f;

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

    [Header("After")]
    [Tooltip("The scene to go on to once the spectrum is up and Closing has been said, as an " +
             "asset path (e.g. Assets/_Core_NEW/PC_Credits.unity). Empty = stay on the spectrum.")]
    [SerializeField] string nextScenePath = "";

    [Tooltip("Seconds the spectrum holds, after Closing has finished, before the fade.")]
    [Min(0f)] [SerializeField] float holdSeconds = 6f;

    [Tooltip("Seconds of the fade to black before the next scene.")]
    [Min(0.1f)] [SerializeField] float fadeSeconds = 2f;

    [Tooltip("Longest wait for the voice-over to finish before the hold starts anyway. A line " +
             "queued and never played must not keep the piece on the spectrum for good.")]
    [Min(0f)] [SerializeField] float maxVoiceWaitSeconds = 30f;

    [SerializeField] bool debugLog = false;

    enum Stage { Waiting, Inside, Outside, Growing, Done }

    float _afterClock = -1f, _voiceWait;
    Image _fade;

    EarthSummit_NEW _summit;
    Stage _stage = Stage.Waiting;
    float _clock, _flashClock;

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
        if (_stage != Stage.Waiting && _flashClock < flashSeconds)
        {
            _flashClock += Time.deltaTime;
            _summit.SetInsideLight(Mathf.Lerp(flash, 1f, Mathf.Clamp01(_flashClock / flashSeconds)));
        }

        switch (_stage)
        {
            case Stage.Waiting:
                if (!_summit.Entered) return;
                _stage = Stage.Inside;
                _flashClock = 0f;
                _summit.SetInsideLight(flash);
                if (voiceOver != null && closing != null) voiceOver.Say(closing);
                else if (closing == null) Debug.LogWarning("[EarthEnding_NEW] No Closing clip assigned; nothing is said.", this);
                if (debugLog) Debug.Log("[EarthEnding_NEW] The light is in.", this);
                break;

            case Stage.Inside:
                if (!_summit.CutToOutside) return;
                _stage = Stage.Outside;
                _clock = 0f;
                break;

            case Stage.Outside:
                _clock += Time.deltaTime;
                if (_clock >= spectrumAfter) BeginGrow();
                break;

            case Stage.Growing:
                _clock += Time.deltaTime;
                float g = Mathf.SmoothStep(0f, 1f, _clock / growSeconds);
                _panel.anchorMin = Vector2.Lerp(_anchorMinFrom, _growMin, g);
                _panel.anchorMax = Vector2.Lerp(_anchorMaxFrom, _growMax, g);
                if (_black != null) _black.color = new Color(0f, 0f, 0f, backdrop * g);
                if (g >= 1f) { _stage = Stage.Done; _afterClock = 0f; }
                break;

            case Stage.Done:
                TickAfter();
                break;
        }
    }

    // ── On to the next scene ────────────────────────────────────────────────

    void TickAfter()
    {
        if (string.IsNullOrEmpty(nextScenePath) || _afterClock < 0f) return;

        // The hold starts counting only once the Closing line is over - or after a limit.
        if (voiceOver != null && voiceOver.IsSpeaking && _voiceWait < maxVoiceWaitSeconds)
        {
            _voiceWait += Time.unscaledDeltaTime;
            return;
        }

        _afterClock += Time.unscaledDeltaTime;
        if (_afterClock < holdSeconds) return;

        if (_fade == null) _fade = BuildFade();
        float f = Mathf.Clamp01((_afterClock - holdSeconds) / fadeSeconds);
        _fade.color = new Color(0f, 0f, 0f, f);
        if (f < 1f) return;

        _afterClock = -1f;
        if (debugLog) Debug.Log("[EarthEnding_NEW] On to " + nextScenePath + ".", this);
        SceneFlow_NEW.Load(nextScenePath, this);
    }

    /// <summary>A black screen over everything, its own canvas above every HUD.</summary>
    static Image BuildFade()
    {
        var go = new GameObject("Ending fade");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        var img = new GameObject("Black", typeof(RectTransform)).AddComponent<Image>();
        img.transform.SetParent(go.transform, false);
        var rt = (RectTransform)img.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = false;
        return img;
    }

    // ── The spectrum ────────────────────────────────────────────────────────

    void BeginGrow()
    {
        _stage = Stage.Done;
        _afterClock = 0f;   // with nothing to grow, the ending still goes on to the next scene
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
        var go = new GameObject("Ending backdrop", typeof(RectTransform));
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
        _afterClock = -1f;
        _clock = 0f;
        if (debugLog) Debug.Log("[EarthEnding_NEW] The spectrum comes up.", this);
    }

    void OnDisable()
    {
        if (_fade != null) Destroy(_fade.transform.parent.gameObject);
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

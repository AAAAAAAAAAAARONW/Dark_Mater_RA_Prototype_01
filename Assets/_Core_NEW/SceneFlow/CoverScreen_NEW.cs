using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The cover: the piece's first screen and the place it comes back to between visitors. The
/// card is the tutorial's own title card (TutorialAttract_NEW's), copied into this scene, over
/// a slowly drifting night sky. A starts: the card fades, the tutorial loads and opens straight
/// on A1 (TutorialAttract_NEW.SkipCardOnce), since this was its title.
///
/// WHY A SCENE OF ITS OWN. The credits come back here, and an idle visitor in the tutorial is
/// sent back here: loading a scene afresh puts everything back where it started, which no
/// reset written by hand can promise - and on the Vizlab wall it is the moment every copy of
/// the piece starts the next run together (LARC meeting, 4 Nov).
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("COVER", "#E0A030")]
public class CoverScreen_NEW : MonoBehaviour
{
    [Header("Card (the tutorial's title card)")]
    [SerializeField] CanvasGroup card;

    [Tooltip("Drawn title. With a sprite it replaces the words below it.")]
    [SerializeField] Image titleImage;
    [SerializeField] TMP_Text titleText;

    [Tooltip("Drawn 'press A to begin'. With a sprite it replaces the words below it.")]
    [SerializeField] Image startImage;
    [SerializeField] TMP_Text startText;

    [Header("Next")]
    [Tooltip("The tutorial, as an asset path.")]
    [SerializeField] string tutorialScenePath = "Assets/_Core_NEW/PC_Tutorial.unity";

    [Header("Feel")]
    [Min(0f)] [SerializeField] float fadeInSeconds = 2f;
    [Min(0.1f)] [SerializeField] float fadeOutSeconds = 1.2f;

    [Tooltip("Seconds before A is taken: the press that ended the credits must not start the next run.")]
    [Min(0f)] [SerializeField] float inputGraceSeconds = 1.5f;

    [Tooltip("Pulse period of 'press A', seconds. 0 for a steady label.")]
    [Min(0f)] [SerializeField] float pulsePeriod = 1.8f;

    [Header("Sky")]
    [SerializeField] bool starfield = true;

    [Tooltip("How fast the sky drifts, in screens per second.")]
    [SerializeField] Vector2 drift = new Vector2(0.004f, 0.0015f);

    Graphic _pulse;
    RawImage _sky;
    Image _black;
    float _t, _leaving = -1f;

    void Awake()
    {
        // As on the tutorial's card: the art wins where there is art, the words are the fallback.
        if (titleImage != null && titleImage.sprite != null && titleText != null) titleText.gameObject.SetActive(false);
        if (startImage != null && startImage.sprite != null && startText != null) startText.gameObject.SetActive(false);
        _pulse = startImage != null && startImage.sprite != null ? (Graphic)startImage : startText;

        if (card != null)
        {
            card.gameObject.SetActive(true);
            card.alpha = 0f;
        }
    }

    void Start()
    {
        RectTransform root = card != null ? card.GetComponentInParent<Canvas>().rootCanvas.transform as RectTransform : null;
        if (root == null)
        {
            Debug.LogError("[CoverScreen_NEW] No card on a canvas; nothing to show.", this);
            return;
        }

        if (starfield) _sky = Starfield_NEW.AddBehind(root, Starfield_NEW.Make(1024, 1017));

        // A black sheet over everything for the way out.
        var go = new GameObject("Fade", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(root, false);
        rt.SetAsLastSibling();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        _black = go.AddComponent<Image>();
        _black.color = new Color(0f, 0f, 0f, 0f);
        _black.raycastTarget = false;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        _t += dt;

        if (_sky != null)
        {
            Rect uv = _sky.uvRect;
            uv.position += drift * dt;
            _sky.uvRect = uv;
        }

        if (_leaving >= 0f)
        {
            _leaving += dt;
            float f = Mathf.Clamp01(_leaving / fadeOutSeconds);
            if (_black != null) _black.color = new Color(0f, 0f, 0f, f);
            if (f >= 1f)
            {
                _leaving = -1f;
                enabled = false;
                TutorialAttract_NEW.SkipCardOnce = true;
                if (!SceneFlow_NEW.Load(tutorialScenePath, this)) { TutorialAttract_NEW.SkipCardOnce = false; enabled = true; _black.color = Color.clear; }
            }
            return;
        }

        if (card != null) card.alpha = fadeInSeconds <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, _t / fadeInSeconds);
        Pulse();

        if (_t < inputGraceSeconds) return;
        if (InputScheme_NEW.ConfirmDown()) _leaving = 0f;
    }

    void Pulse()
    {
        if (_pulse == null || pulsePeriod <= 0f) return;
        float t = Mathf.Sin(_t / pulsePeriod * Mathf.PI * 2f) * 0.5f + 0.5f;
        Color c = _pulse.color;
        c.a = Mathf.Lerp(0.45f, 1f, t);
        _pulse.color = c;
    }
}

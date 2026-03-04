using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Universe Journey HUD
/// - Log-scale node chain: Quasar (left) → Earth (right), dots only
/// - Moving dot indicator slides along the track line showing exact progress
/// - Micro bar (sliced image) appears below track when inside non-Cosmic-Web phases
/// - Milestone popups fade in/out
/// </summary>
public class UniverseJourneyHUD : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] UniverseJourneyTracker tracker;

    [Header("Node Chain")]
    [SerializeField] RectTransform nodeContainer;
    [SerializeField] GameObject nodePrefab;
    [SerializeField] float chainPadding = 24f;
    [SerializeField] RectTransform trackLine;
    [SerializeField] RectTransform progressLine;
    [SerializeField] RectTransform indicatorDot;

    [Header("Micro Bar")]
    [SerializeField] RectTransform microBarPanel;
    [SerializeField] Image microBarBackground;
    [SerializeField] RectTransform microBarFillRT;
    [SerializeField] TMP_Text microBarLabel;

    [Header("Info Text")]
    [SerializeField] TMP_Text phaseNameText;
    [SerializeField] TMP_Text distanceText;
    [SerializeField] TMP_Text scaleText;

    [Header("Milestone Popup")]
    [SerializeField] RectTransform milestonePopup;
    [SerializeField] TMP_Text milestoneText;
    [SerializeField] float milestoneDisplayDuration = 3f;
    [SerializeField] float milestoneFadeDuration = 0.4f;

    [Header("Node Visuals")]
    [SerializeField] Color nodeInactiveColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] Color nodeActiveColor = new Color(1f, 0.9f, 0.4f, 1f);
    [SerializeField] Color nodeCompleteColor = new Color(0.5f, 1f, 0.7f, 1f);
    [SerializeField] float nodePulseScale = 1.3f;
    [SerializeField] float nodePulseSpeed = 2f;
    [SerializeField] float nodeSize = 12f;

    RectTransform[] _nodeRTs;
    Image[] _nodeImages;
    float[] _nodeNormalizedX;

    float _containerWidth;
    float _usableWidth;
    float _microBarTotalWidth;
    float _labelUpdateTimer;        // throttle label updates to reduce layout thrashing
    const float LabelUpdateInterval = 0.2f;  // update text at most 5x per second
    bool _initialized;

    Coroutine _milestoneCoroutine;
    CanvasGroup _milestoneCanvasGroup;

    void Awake()
    {
        if (milestonePopup != null)
        {
            _milestoneCanvasGroup = milestonePopup.GetComponent<CanvasGroup>();
            if (_milestoneCanvasGroup == null)
                _milestoneCanvasGroup = milestonePopup.gameObject.AddComponent<CanvasGroup>();
            _milestoneCanvasGroup.alpha = 0f;
            milestonePopup.gameObject.SetActive(false);
        }

        if (microBarPanel != null)
            microBarPanel.gameObject.SetActive(false);
    }

    void Start()
    {
        if (tracker == null)
        {
            Debug.LogWarning("[UniverseJourneyHUD] Tracker not assigned.");
            return;
        }

        tracker.OnMilestoneReached += ShowMilestonePopup;
        tracker.OnPhaseChanged += OnPhaseChanged;

        BuildNodeChain();

        // Fix label shaking: disable auto-sizing on micro bar label so
        // changing text length doesn't cause layout recalculation every frame
        if (microBarLabel != null)
        {
            microBarLabel.enableAutoSizing = false;
            microBarLabel.overflowMode = TMPro.TextOverflowModes.Ellipsis;
        }

        _initialized = true;
    }

    void OnDestroy()
    {
        if (tracker == null) return;
        tracker.OnMilestoneReached -= ShowMilestonePopup;
        tracker.OnPhaseChanged -= OnPhaseChanged;
    }

    void Update()
    {
        if (!_initialized) return;
        UpdateNodeVisuals();
        UpdateProgressLine();
        UpdateIndicatorDot();
        UpdateMicroBar();
        UpdateInfoText();
    }

    void BuildNodeChain()
    {
        if (nodeContainer == null || nodePrefab == null) return;

        Canvas.ForceUpdateCanvases();
        _containerWidth = nodeContainer.rect.width;
        _usableWidth = _containerWidth - chainPadding * 2f;

        var phases = tracker.Phases;
        int count = phases.Length;

        _nodeRTs = new RectTransform[count];
        _nodeImages = new Image[count];
        _nodeNormalizedX = new float[count];

        double[] logDist = new double[count];
        double total = 0;
        // Use a minimum log floor so even tiny phases (Solar System, Earth)
        // get a visible segment on the bar rather than collapsing to a point
        const double MIN_LOG = 2.0;
        for (int i = 0; i < count; i++)
        {
            double raw = System.Math.Log10(System.Math.Max(phases[i].TotalDistanceLy, 1));
            logDist[i] = System.Math.Max(raw, MIN_LOG);
            total += logDist[i];
        }

        double cumulative = 0;
        for (int i = 0; i < count; i++)
        {
            cumulative += logDist[i];
            _nodeNormalizedX[i] = (float)(cumulative / total);
        }

        for (int i = 0; i < count; i++)
        {
            GameObject go = Instantiate(nodePrefab, nodeContainer);
            RectTransform rt = go.GetComponent<RectTransform>();

            rt.sizeDelta = new Vector2(nodeSize, nodeSize);
            rt.anchorMin = new Vector2(0, 0.5f);
            rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(chainPadding + _nodeNormalizedX[i] * _usableWidth, 0f);

            _nodeRTs[i] = rt;
            _nodeImages[i] = go.GetComponent<Image>();

            foreach (var tmp in go.GetComponentsInChildren<TMP_Text>())
                tmp.gameObject.SetActive(false);

            if (_nodeImages[i] != null)
                _nodeImages[i].color = nodeInactiveColor;
        }

        if (trackLine != null)
        {
            trackLine.anchorMin = new Vector2(0, 0.5f);
            trackLine.anchorMax = new Vector2(0, 0.5f);
            trackLine.pivot = new Vector2(0, 0.5f);
            trackLine.anchoredPosition = new Vector2(chainPadding, 0f);
            trackLine.sizeDelta = new Vector2(_usableWidth, trackLine.sizeDelta.y);
        }

        if (progressLine != null)
        {
            progressLine.anchorMin = new Vector2(0, 0.5f);
            progressLine.anchorMax = new Vector2(0, 0.5f);
            progressLine.pivot = new Vector2(0, 0.5f);
            progressLine.anchoredPosition = new Vector2(chainPadding, 0f);
            progressLine.sizeDelta = new Vector2(0f, progressLine.sizeDelta.y);
        }

        // FIX 1: Cache micro bar width here after ForceUpdateCanvases
        if (microBarBackground != null)
            _microBarTotalWidth = microBarBackground.rectTransform.rect.width;
    }

    void UpdateNodeVisuals()
    {
        int currentIndex = (int)tracker.CurrentPhase;
        float pulse = 1f + Mathf.Sin(Time.time * nodePulseSpeed) * (nodePulseScale - 1f) * 0.5f;

        for (int i = 0; i < _nodeImages.Length; i++)
        {
            if (_nodeImages[i] == null) continue;

            if (i < currentIndex)
            {
                _nodeImages[i].color = nodeCompleteColor;
                _nodeRTs[i].localScale = Vector3.one;
            }
            else if (i == currentIndex)
            {
                _nodeImages[i].color = nodeActiveColor;
                _nodeRTs[i].localScale = Vector3.one * pulse;
            }
            else
            {
                _nodeImages[i].color = nodeInactiveColor;
                _nodeRTs[i].localScale = Vector3.one;
            }
        }
    }

    void UpdateProgressLine()
    {
        if (progressLine == null || _nodeNormalizedX == null) return;
        float currentX = GetCurrentProgressX();
        progressLine.sizeDelta = new Vector2(currentX, progressLine.sizeDelta.y);
    }

    void UpdateIndicatorDot()
    {
        if (indicatorDot == null || _nodeNormalizedX == null) return;
        float currentX = GetCurrentProgressX();
        indicatorDot.anchorMin = new Vector2(0, 0.5f);
        indicatorDot.anchorMax = new Vector2(0, 0.5f);
        indicatorDot.pivot = new Vector2(0.5f, 0.5f);
        indicatorDot.anchoredPosition = new Vector2(chainPadding + currentX, 0f);
    }

    void UpdateMicroBar()
    {
        if (microBarPanel == null) return;

        bool inMicro = tracker.CurrentPhase != UniverseJourneyTracker.JourneyPhase.CosmicWeb1;
                   // && tracker.CurrentPhase != UniverseJourneyTracker.JourneyPhase.CosmicWeb2;

        microBarPanel.gameObject.SetActive(inMicro);
        if (!inMicro) return;

        if (microBarFillRT != null && _microBarTotalWidth > 0f)
        {
            // FIX 1: Use cached width instead of reading rect.width every frame
            float fillWidth = _microBarTotalWidth * tracker.PhaseProgress;
            microBarFillRT.anchorMin = new Vector2(0, 0f);
            microBarFillRT.anchorMax = new Vector2(0, 1f);
            microBarFillRT.pivot = new Vector2(0, 0.5f);
            microBarFillRT.anchoredPosition = Vector2.zero;
            microBarFillRT.sizeDelta = new Vector2(fillWidth, 0f);
        }

        if (microBarLabel != null)
        {
            // Throttle: only update text a few times per second
            // Changing TMP text every frame causes layout recalculation = shaking
            _labelUpdateTimer -= Time.deltaTime;
            if (_labelUpdateTimer <= 0f)
            {
                _labelUpdateTimer = LabelUpdateInterval;

                double remainingInPhase = System.Math.Max(0,
                    tracker.CurrentPhaseTotalLy - tracker.PhaseDistanceLy);

                microBarLabel.text = tracker.CurrentPhaseName
                                   + "   "
                                   + UniverseJourneyTracker.FormatDistance(remainingInPhase)
                                   + " remaining";
            }
        }
    }

    void UpdateInfoText()
    {
        if (phaseNameText != null)
            phaseNameText.text = tracker.CurrentPhaseName;

        if (distanceText != null)
            distanceText.text = tracker.FormattedRemainingDistance;

        // FIX 3: ScaleLabel was assuming 60 units/sec fixed speed.
        // Now reads actual player speed from tracker instead.
        if (scaleText != null)
            scaleText.text = tracker.ScaleLabel;
    }

    float GetCurrentProgressX()
    {
        int currentIndex = (int)tracker.CurrentPhase;

        float endNodeX = currentIndex < _nodeNormalizedX.Length
            ? _nodeNormalizedX[currentIndex] * _usableWidth
            : _usableWidth;

        float prevNodeX = currentIndex > 0
            ? _nodeNormalizedX[currentIndex - 1] * _usableWidth
            : 0f;

        return Mathf.Lerp(prevNodeX, endNodeX, tracker.PhaseProgress);
    }

    void OnPhaseChanged(UniverseJourneyTracker.JourneyPhase newPhase,
                        UniverseJourneyTracker.PhaseData data)
    {
        // Re-cache micro bar width in case layout changed
        if (microBarBackground != null)
            _microBarTotalWidth = microBarBackground.rectTransform.rect.width;
    }

    void ShowMilestonePopup(string label)
    {
        if (milestonePopup == null || milestoneText == null) return;
        if (_milestoneCoroutine != null) StopCoroutine(_milestoneCoroutine);
        milestoneText.text = label;
        _milestoneCoroutine = StartCoroutine(MilestonePopupRoutine());
    }

    IEnumerator MilestonePopupRoutine()
    {
        milestonePopup.gameObject.SetActive(true);

        float t = 0f;
        while (t < milestoneFadeDuration)
        {
            t += Time.deltaTime;
            _milestoneCanvasGroup.alpha = Mathf.Clamp01(t / milestoneFadeDuration);
            yield return null;
        }
        _milestoneCanvasGroup.alpha = 1f;

        yield return new WaitForSeconds(milestoneDisplayDuration);

        t = 0f;
        while (t < milestoneFadeDuration)
        {
            t += Time.deltaTime;
            _milestoneCanvasGroup.alpha = 1f - Mathf.Clamp01(t / milestoneFadeDuration);
            yield return null;
        }

        _milestoneCanvasGroup.alpha = 0f;
        milestonePopup.gameObject.SetActive(false);
        _milestoneCoroutine = null;
    }
}
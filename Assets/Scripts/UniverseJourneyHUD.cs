using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Universe Journey HUD — vertical bar, bottom (Quasar) to top (Earth).
///
/// Two states:
///   Macro — full journey visible, nodes at log-scale positions.
///   Micro — current phase expands, past/future compress to clusters.
///
/// Driven entirely by UniverseJourneyTracker (position-based, no extra wiring).
/// Switches state automatically when a phase with isMacroPhase=false becomes active.
///
/// Setup: see setup guide in comments at bottom of file.
/// </summary>
public class UniverseJourneyHUD : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // PHASE HUD CONFIG
    // ─────────────────────────────────────────────────────────────────────────

    [Serializable]
    public class PhaseHUDConfig
    {
        [Tooltip("True = show full journey bar when this phase is active.\n" +
                 "False = zoom into this phase (micro view).")]
        public bool isMacroPhase = true;

        [Tooltip("Optional icon sprite. Leave null to use auto-generated circle.")]
        public Sprite icon;

        [Tooltip("Overrides the display name from UniverseJourneyTracker.\n" +
                 "Leave blank to use the tracker's displayName.")]
        public string displayNameOverride;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // INSPECTOR
    // ─────────────────────────────────────────────────────────────────────────

    [Header("References")]
    [SerializeField] UniverseJourneyTracker tracker;

    [Tooltip("Empty RectTransform that acts as the parent for all node GameObjects.\n" +
             "Should be 2px wide, trackHeight tall, anchored bottom-center of the bar.")]
    [SerializeField] RectTransform trackContainer;

    [Tooltip("The dark background line behind the track.\n" +
             "Image component, 2px wide, stretch-height inside trackContainer.")]
    [SerializeField] Image trackBackground;

    [Tooltip("The gradient fill RawImage. Anchor: bottom-center, pivot: (0.5, 0).\n" +
             "Width: 4px. Height: driven by script.")]
    [SerializeField] RawImage fillImage;

    [Tooltip("TMP_Text showing remaining distance (e.g. '10.4B ly to Earth').")]
    [SerializeField] TMP_Text distanceText;

    [Tooltip("TMP_Text showing current phase name.")]
    [SerializeField] TMP_Text phaseNameText;

    [Header("Phase Configuration (7 entries, one per JourneyPhase)")]
    [SerializeField]
    PhaseHUDConfig[] phaseConfigs = new PhaseHUDConfig[]
    {
        // Index 0 — Quasar
        new PhaseHUDConfig { isMacroPhase = true,  displayNameOverride = "Quasar"       },
        // Index 1 — CosmicWeb1
        new PhaseHUDConfig { isMacroPhase = true,  displayNameOverride = "Cosmic Web"   },
        // Index 2 — Galaxy
        new PhaseHUDConfig { isMacroPhase = false, displayNameOverride = "Galaxy"       },
        // Index 3 — CosmicWeb2
        new PhaseHUDConfig { isMacroPhase = true,  displayNameOverride = "Cosmic Web"   },
        // Index 4 — MilkyWay
        new PhaseHUDConfig { isMacroPhase = false, displayNameOverride = "Milky Way"    },
        // Index 5 — SolarSystem
        new PhaseHUDConfig { isMacroPhase = false, displayNameOverride = "Solar System" },
        // Index 6 — Earth
        new PhaseHUDConfig { isMacroPhase = false, displayNameOverride = "Earth"        },
    };

    // ─────────────────────────────────────────────────────────────────────────
    // TRACK VISUAL SETTINGS
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Track")]
    [Tooltip("Height of the track in Unity UI pixels. Match your TrackContainer height.")]
    [SerializeField] float trackHeight = 350f;

    [Tooltip("Gradient colors from Quasar (index 0) to Earth (last index).\n" +
             "Baked into a Texture2D at startup — no shader needed.")]
    [SerializeField]
    Color[] gradientColors = new Color[]
    {
        new Color(0.267f, 0.133f, 0.667f, 1f), // violet  — Quasar
        new Color(0.114f, 0.478f, 0.620f, 1f), // blue    — early CW
        new Color(0.114f, 0.620f, 0.459f, 1f), // teal    — late CW
        new Color(0.980f, 0.780f, 0.459f, 1f), // amber   — Galaxy area
        new Color(0.800f, 0.200f, 0.133f, 1f), // red     — Earth
    };

    [Header("Nodes")]
    [SerializeField] float nodeActiveDiameter = 20f;
    [SerializeField] float nodeDefaultDiameter = 12f;
    [SerializeField] float nodeMicroDiameter = 7f;
    [SerializeField] Color colorDone = new Color(0.114f, 0.620f, 0.459f, 1f); // teal
    [SerializeField] Color colorActive = new Color(0.980f, 0.780f, 0.459f, 1f); // amber
    [SerializeField] Color colorFuture = new Color(0.082f, 0.082f, 0.141f, 1f); // very dark
    [SerializeField] float activePulseSpeed = 2.5f;
    [SerializeField] float activePulseAmount = 0.08f;

    [Header("Labels")]
    [Tooltip("Font for node labels. Assign a TextMeshPro font asset.")]
    [SerializeField] TMP_FontAsset labelFont;
    [SerializeField] float labelFontSize = 8f;
    [Tooltip("X offset of label from node center (pixels).")]
    [SerializeField] float labelOffsetX = 8f;
    [Tooltip("Width of each node label RectTransform.")]
    [SerializeField] float labelWidth = 62f;

    [Header("Transition")]
    [Tooltip("Duration of the macro ↔ micro smooth transition in seconds.")]
    [SerializeField] float transitionDuration = 0.5f;
    [Tooltip("Fraction of trackHeight reserved for the compressed past/future clusters in micro mode.")]
    [Range(0.08f, 0.30f)]
    [SerializeField] float microCompressedFraction = 0.18f;

    [Header("Auto-Hide")]
    [Tooltip("When ON: the HUD slides off-screen to the left and only appears briefly\n" +
             "when the player enters a new phase or presses the gamepad show button.")]
    [SerializeField] bool autoHide = false;

    [Tooltip("The RectTransform to slide. Drag the root HUD panel here.\n" +
             "If left empty, uses this GameObject's own RectTransform.")]
    [SerializeField] RectTransform hudPanel;

    [Tooltip("How many seconds the HUD stays visible before sliding away.")]
    [SerializeField] float hudVisibleDuration = 8f;

    [Tooltip("Pixel offset applied to anchoredPosition when hidden (e.g. -400 slides left).")]
    [SerializeField] Vector2 hudHideOffset = new Vector2(-400f, 0f);

    [Tooltip("Duration of the slide in / slide out animation.")]
    [SerializeField] float hudSlideDuration = 0.35f;

    [Tooltip("Input Manager button name for 'show HUD' (gamepad X button).\n" +
             "Xbox default mapping: Fire3 = X button.\n" +
             "Leave empty to disable gamepad trigger.")]
    [SerializeField] string hudShowButton = "Fire3";

    // ─────────────────────────────────────────────────────────────────────────
    // PRIVATE STATE
    // ─────────────────────────────────────────────────────────────────────────

    const int NODE_COUNT = 7;
    const double MIN_LOG = 2.0; // floor for log10 calculation (same as HUD tracker)

    RectTransform[] _nodeRTs;
    Image[] _nodeImages;
    TMP_Text[] _nodeLabels;
    float[] _macroPositionsY;

    Texture2D _gradientTex;
    Sprite _defaultCircleSprite;
    bool _isMacroState = true;
    Coroutine _transitionCoroutine;
    bool _initialized;

    // ── Auto-hide state ──────────────────────────────────────────────────
    RectTransform _rootRT;
    Vector2       _shownAnchoredPos;
    bool          _hudVisible = true;
    float         _hideTimer;
    Coroutine     _slideCoroutine;

    float _labelUpdateTimer;
    const float LABEL_INTERVAL = 0.15f;

    // ─────────────────────────────────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        BuildGradientTexture();
        _defaultCircleSprite = CreateCircleSprite(64);
    }

    void Start()
    {
        if (tracker == null)
        {
            Debug.LogWarning("[UniverseJourneyHUD] UniverseJourneyTracker not assigned.");
            return;
        }
        if (trackContainer == null)
        {
            Debug.LogWarning("[UniverseJourneyHUD] TrackContainer not assigned.");
            return;
        }

        BuildNodes();
        CalculateMacroPositions();
        SetNodePositionsImmediate(_macroPositionsY);
        UpdateFill();
        UpdateDistanceText();

        InitAutoHide();
        tracker.OnPhaseChanged += HandlePhaseChanged;
        _initialized = true;
    }

    void OnDestroy()
    {
        if (tracker != null)
            tracker.OnPhaseChanged -= HandlePhaseChanged;
        if (_gradientTex != null)
            Destroy(_gradientTex);
        if (_defaultCircleSprite != null)
            Destroy(_defaultCircleSprite.texture);
    }

    void Update()
    {
        if (!_initialized) return;

        UpdateFill();
        UpdateNodeVisuals();

        _labelUpdateTimer -= Time.deltaTime;
        if (_labelUpdateTimer <= 0f)
        {
            _labelUpdateTimer = LABEL_INTERVAL;
            UpdateDistanceText();
        }

        TickAutoHide();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GRADIENT TEXTURE
    // Built once at Awake. No shader needed — works in Built-in RP.
    // ─────────────────────────────────────────────────────────────────────────

    void BuildGradientTexture()
    {
        const int texHeight = 256;
        _gradientTex = new Texture2D(2, texHeight, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        for (int y = 0; y < texHeight; y++)
        {
            float t = (float)y / (texHeight - 1);
            Color c = SampleGradient(t);
            _gradientTex.SetPixel(0, y, c);
            _gradientTex.SetPixel(1, y, c);
        }
        _gradientTex.Apply();

        if (fillImage != null)
        {
            fillImage.texture = _gradientTex;
            fillImage.uvRect = new Rect(0f, 0f, 1f, 0.001f);
        }
    }

    Color SampleGradient(float t)
    {
        if (gradientColors == null || gradientColors.Length == 0) return Color.white;
        if (gradientColors.Length == 1) return gradientColors[0];
        float scaled = t * (gradientColors.Length - 1);
        int lo = Mathf.FloorToInt(scaled);
        int hi = Mathf.Min(lo + 1, gradientColors.Length - 1);
        return Color.Lerp(gradientColors[lo], gradientColors[hi], scaled - lo);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // NODE CREATION
    // Creates 7 node GameObjects inside trackContainer at Start().
    // ─────────────────────────────────────────────────────────────────────────

    void BuildNodes()
    {
        _nodeRTs = new RectTransform[NODE_COUNT];
        _nodeImages = new Image[NODE_COUNT];
        _nodeLabels = new TMP_Text[NODE_COUNT];

        for (int i = 0; i < NODE_COUNT; i++)
        {
            // ── Node root ──
            var nodeGO = new GameObject($"Node_{(UniverseJourneyTracker.JourneyPhase)i}");
            nodeGO.transform.SetParent(trackContainer, false);

            var rt = nodeGO.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f); // bottom-centre of track
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.one * nodeDefaultDiameter;
            _nodeRTs[i] = rt;

            // ── Icon image ──
            var img = nodeGO.AddComponent<Image>();
            img.color = (i == 0) ? colorActive : colorFuture;
            img.sprite = (phaseConfigs != null && i < phaseConfigs.Length && phaseConfigs[i].icon != null)
                ? phaseConfigs[i].icon
                : _defaultCircleSprite;
            _nodeImages[i] = img;

            // ── Label (to the right of node) ──
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(nodeGO.transform, false);

            var labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(1f, 0.5f);
            labelRT.anchorMax = new Vector2(1f, 0.5f);
            labelRT.pivot = new Vector2(0f, 0.5f);
            labelRT.anchoredPosition = new Vector2(labelOffsetX, 0f);
            labelRT.sizeDelta = new Vector2(labelWidth, 16f);

            var lbl = labelGO.AddComponent<TextMeshProUGUI>();
            lbl.fontSize = labelFontSize;
            lbl.color = colorFuture;
            lbl.alignment = TextAlignmentOptions.Left;
            lbl.overflowMode = TextOverflowModes.Ellipsis;
            lbl.enableWordWrapping = false;
            if (labelFont != null) lbl.font = labelFont;
            lbl.text = GetPhaseName(i);
            _nodeLabels[i] = lbl;
        }
    }

    string GetPhaseName(int i)
    {
        if (phaseConfigs != null && i < phaseConfigs.Length &&
            !string.IsNullOrEmpty(phaseConfigs[i].displayNameOverride))
            return phaseConfigs[i].displayNameOverride;

        if (tracker?.Phases != null && i < tracker.Phases.Length)
            return tracker.Phases[i].displayName;

        return $"Phase {i}";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // LOG-SCALE POSITION CALCULATION
    // ─────────────────────────────────────────────────────────────────────────

    void CalculateMacroPositions()
    {
        _macroPositionsY = new float[NODE_COUNT];

        double[] logSpans = new double[NODE_COUNT];
        double totalLog = 0.0;

        for (int i = 0; i < NODE_COUNT; i++)
        {
            double span = (tracker?.Phases != null && i < tracker.Phases.Length)
                ? tracker.Phases[i].TotalDistanceLy : 1.0;
            logSpans[i] = Math.Max(Math.Log10(Math.Max(span, 1.0)), MIN_LOG);
            totalLog += logSpans[i];
        }

        double cumulative = 0.0;
        for (int i = 0; i < NODE_COUNT; i++)
        {
            // Node positioned at start of its phase segment
            _macroPositionsY[i] = (float)(cumulative / totalLog) * trackHeight;
            cumulative += logSpans[i];
        }
    }

    float[] CalculateMicroPositions(int activeIndex)
    {
        var positions = new float[NODE_COUNT];
        float compressed = trackHeight * microCompressedFraction;
        float expandedStart = compressed;
        float expandedEnd = trackHeight - compressed;
        float padding = 8f;

        int pastCount = activeIndex;
        int futureCount = NODE_COUNT - 1 - activeIndex;

        // Past nodes — bottom compressed zone
        for (int i = 0; i < pastCount; i++)
        {
            float t = pastCount <= 1 ? 0.5f : (float)i / (pastCount - 1);
            positions[i] = Mathf.Lerp(padding, compressed - padding, t);
        }

        // Active node — bottom edge of expanded section
        positions[activeIndex] = expandedStart + padding;

        // Future nodes — top compressed zone
        for (int j = 0; j < futureCount; j++)
        {
            int i = activeIndex + 1 + j;
            float t = futureCount <= 1 ? 0.5f : (float)j / (futureCount - 1);
            positions[i] = Mathf.Lerp(expandedEnd + padding, trackHeight - padding, t);
        }

        return positions;
    }

    void SetNodePositionsImmediate(float[] positions)
    {
        for (int i = 0; i < NODE_COUNT && i < positions.Length; i++)
        {
            if (_nodeRTs[i] != null)
                _nodeRTs[i].anchoredPosition = new Vector2(0f, positions[i]);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // STATE TRANSITION — smooth coroutine
    // ─────────────────────────────────────────────────────────────────────────

    void HandlePhaseChanged(UniverseJourneyTracker.JourneyPhase newPhase,
                            UniverseJourneyTracker.PhaseData data)
    {
        int index = (int)newPhase;
        bool wantMacro = (phaseConfigs == null || index >= phaseConfigs.Length)
                            || phaseConfigs[index].isMacroPhase;

        if (wantMacro != _isMacroState)
        {
            _isMacroState = wantMacro;
            if (_transitionCoroutine != null) StopCoroutine(_transitionCoroutine);
            _transitionCoroutine = StartCoroutine(TransitionToState(wantMacro, index));
        }

        // Refresh labels in case displayNameOverride changed
        for (int i = 0; i < NODE_COUNT; i++)
            if (_nodeLabels[i] != null)
                _nodeLabels[i].text = GetPhaseName(i);

        // Show HUD briefly on every phase change
        ShowHUD();
    }

    IEnumerator TransitionToState(bool toMacro, int activeIndex)
    {
        // Snapshot current positions
        var startPositions = new float[NODE_COUNT];
        for (int i = 0; i < NODE_COUNT; i++)
            startPositions[i] = _nodeRTs[i] != null ? _nodeRTs[i].anchoredPosition.y : 0f;

        float[] targetPositions = toMacro
            ? _macroPositionsY
            : CalculateMicroPositions(activeIndex);

        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / transitionDuration));
            for (int i = 0; i < NODE_COUNT; i++)
            {
                if (_nodeRTs[i] == null) continue;
                _nodeRTs[i].anchoredPosition = new Vector2(
                    0f,
                    Mathf.Lerp(startPositions[i], targetPositions[i], t)
                );
            }
            yield return null;
        }

        SetNodePositionsImmediate(targetPositions);
        _transitionCoroutine = null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FILL BAR UPDATE
    // ─────────────────────────────────────────────────────────────────────────

    void UpdateFill()
    {
        if (fillImage == null || tracker == null) return;

        float fillH;

        if (_isMacroState)
        {
            // ── Macro view: fill represents total journey progress (log scale) ──
            // Grows from Quasar (bottom, violet) toward Earth (top, red).
            float progress = Mathf.Clamp01(tracker.TotalProgressLog);
            fillH = progress * trackHeight;
        }
        else
        {
            // ── Micro view: fill represents progress within the expanded phase ──
            //
            // Layout in micro mode (bottom to top):
            //   [0, compressed]          = past nodes cluster
            //   [compressed, trackH-compressed] = expanded section for active phase
            //   [trackH-compressed, trackH]     = future nodes cluster
            //
            // The fill bar should reach:
            //   from y=0 (bottom)
            //   up to activeNodeY + PhaseProgress × (expandedEnd - activeNodeY)
            //
            // This keeps the gradient visually consistent with macro —
            // the colour at the pip always reflects where we are in the full journey.

            int activeIndex = (int)tracker.CurrentPhase;
            float compressed = trackHeight * microCompressedFraction;
            float expandedStart = compressed;
            float expandedEnd = trackHeight - compressed;
            float padding = 8f;

            // Active node sits at the bottom of the expanded section
            float activeNodeY = expandedStart + padding;

            // Available height inside the expanded section above the active node
            float expandedRange = expandedEnd - activeNodeY - padding;

            fillH = activeNodeY + Mathf.Clamp01(tracker.PhaseProgress) * expandedRange;
        }

        var fillRT = fillImage.rectTransform;
        fillRT.sizeDelta = new Vector2(fillRT.sizeDelta.x, Mathf.Max(0f, fillH));

        // UV rect: sample the correct colour band from the gradient texture.
        // Always driven by TotalProgressLog so the colour matches the
        // player's actual position in the full journey regardless of HUD state.
        float uvH = Mathf.Max(Mathf.Clamp01(tracker.TotalProgressLog), 0.001f);
        fillImage.uvRect = new Rect(0f, 0f, 1f, uvH);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // NODE VISUALS — color, size, label visibility
    // ─────────────────────────────────────────────────────────────────────────

    void UpdateNodeVisuals()
    {
        if (tracker == null) return;
        int activeIndex = (int)tracker.CurrentPhase;
        float pulse = 1f + Mathf.Sin(Time.time * activePulseSpeed) * activePulseAmount;

        for (int i = 0; i < NODE_COUNT; i++)
        {
            if (_nodeImages[i] == null) continue;

            bool isDone = i < activeIndex;
            bool isActive = i == activeIndex;
            bool isMicro = !_isMacroState;

            // ── Color ──
            Color targetColor;
            Color labelColor;
            if (isDone)
            {
                targetColor = colorDone;
                labelColor = new Color(colorDone.r, colorDone.g, colorDone.b, 0.4f);
            }
            else if (isActive)
            {
                targetColor = colorActive;
                labelColor = colorActive;
            }
            else
            {
                targetColor = colorFuture;
                labelColor = new Color(0.13f, 0.13f, 0.22f, 0.6f);
            }

            _nodeImages[i].color = targetColor;
            if (_nodeLabels[i] != null) _nodeLabels[i].color = labelColor;

            // ── Size ──
            float diameter;
            if (isActive)
                diameter = nodeActiveDiameter * pulse;
            else if (isMicro)
                diameter = nodeMicroDiameter;
            else
                diameter = nodeDefaultDiameter;

            _nodeRTs[i].sizeDelta = Vector2.one * diameter;

            // ── Label visibility ──
            // In micro mode hide labels for non-active nodes to reduce clutter
            if (_nodeLabels[i] != null)
                _nodeLabels[i].gameObject.SetActive(!isMicro || isActive);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DISTANCE TEXT
    // ─────────────────────────────────────────────────────────────────────────

    void UpdateDistanceText()
    {
        if (distanceText != null && tracker != null)
            distanceText.text = tracker.FormattedRemainingDistance;

        if (phaseNameText != null && tracker != null)
            phaseNameText.text = tracker.CurrentPhaseName;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a white anti-aliased circle sprite at runtime.
    /// Used as the default node icon when no sprite is assigned.
    /// </summary>
    static Sprite CreateCircleSprite(int diameter)
    {
        var tex = new Texture2D(diameter, diameter, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float r = diameter * 0.5f;
        float cx = r - 0.5f;
        float cy = r - 0.5f;

        for (int x = 0; x < diameter; x++)
            for (int y = 0; y < diameter; y++)
            {
                float dx = x - cx;
                float dy = y - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(r - dist + 0.5f); // soft edge
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        tex.Apply();

        return Sprite.Create(
            tex,
            new Rect(0, 0, diameter, diameter),
            new Vector2(0.5f, 0.5f),
            diameter
        );
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AUTO-HIDE
    // ─────────────────────────────────────────────────────────────────────────

    void InitAutoHide()
    {
        _rootRT = hudPanel != null ? hudPanel : GetComponent<RectTransform>();
        if (_rootRT == null) { Debug.LogWarning("[JourneyHUD] No RectTransform for auto-hide."); return; }

        _shownAnchoredPos = _rootRT.anchoredPosition;

        if (autoHide)
        {
            _rootRT.anchoredPosition = _shownAnchoredPos + hudHideOffset;
            _hudVisible = false;
            Debug.Log($"[JourneyHUD] Auto-hide ON. Panel: '{_rootRT.name}' " +
                      $"shown={_shownAnchoredPos} hidden={_shownAnchoredPos + hudHideOffset}");
        }
    }

    void TickAutoHide()
    {
        if (!autoHide || _rootRT == null) return;

        // Gamepad show button (configurable, Fire3 = X on Xbox by default)
        if (!string.IsNullOrEmpty(hudShowButton))
        {
            try { if (Input.GetButtonDown(hudShowButton)) ShowHUD(); }
            catch { /* button not mapped in Input Manager */ }
        }

        // Hide timer countdown
        if (_hudVisible)
        {
            _hideTimer -= Time.deltaTime;
            if (_hideTimer <= 0f) SlideHUD(visible: false);
        }
    }

    void ShowHUD()
    {
        if (!autoHide || _rootRT == null) return;
        _hideTimer = hudVisibleDuration;
        if (!_hudVisible) SlideHUD(visible: true);
        _hudVisible = true;
    }

    void SlideHUD(bool visible)
    {
        if (_slideCoroutine != null) StopCoroutine(_slideCoroutine);
        _hudVisible = visible;
        _slideCoroutine = StartCoroutine(SlideCoroutine(visible));
    }

    IEnumerator SlideCoroutine(bool visible)
    {
        Vector2 from = _rootRT.anchoredPosition;
        Vector2 to   = visible ? _shownAnchoredPos : _shownAnchoredPos + hudHideOffset;
        float t = 0f;
        while (t < hudSlideDuration)
        {
            t += Time.deltaTime;
            float pct = Mathf.Clamp01(t / hudSlideDuration);
            float ease = pct * pct * (3f - 2f * pct); // smoothstep
            _rootRT.anchoredPosition = Vector2.Lerp(from, to, ease);
            yield return null;
        }
        _rootRT.anchoredPosition = to;
        _slideCoroutine = null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // EDITOR VALIDATION
    // ─────────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    void OnValidate()
    {
        // Keep phaseConfigs exactly 7 entries
        if (phaseConfigs == null || phaseConfigs.Length != NODE_COUNT)
        {
            var old = phaseConfigs ?? Array.Empty<PhaseHUDConfig>();
            phaseConfigs = new PhaseHUDConfig[NODE_COUNT];
            for (int i = 0; i < NODE_COUNT; i++)
                phaseConfigs[i] = (i < old.Length && old[i] != null)
                    ? old[i]
                    : new PhaseHUDConfig();
        }

        trackHeight = Mathf.Max(100f, trackHeight);
        transitionDuration = Mathf.Max(0.1f, transitionDuration);
        microCompressedFraction = Mathf.Clamp(microCompressedFraction, 0.08f, 0.30f);
        nodeActiveDiameter = Mathf.Max(8f, nodeActiveDiameter);
        nodeDefaultDiameter = Mathf.Max(4f, nodeDefaultDiameter);
        nodeMicroDiameter = Mathf.Max(4f, nodeMicroDiameter);
    }

    [ContextMenu("Auto-Hide: Test Show HUD")]
    void DebugShowHUD() => ShowHUD();

    [ContextMenu("Auto-Hide: Test Hide HUD")]
    void DebugHideHUD()
    {
        if (_rootRT == null)
            _rootRT = hudPanel != null ? hudPanel : GetComponent<RectTransform>();
        if (_rootRT != null)
            SlideHUD(visible: false);
    }
#endif
}

/*
 * ═══════════════════════════════════════════════════════════════════════════
 * SETUP GUIDE — follow these steps in order
 * ═══════════════════════════════════════════════════════════════════════════
 *
 * STEP 1 — HIERARCHY
 * ──────────────────
 * Create this structure in your scene under your existing Canvas:
 *
 *   Canvas
 *   └── JourneyHUD                    [GameObject]
 *       ├── Background                [GameObject]
 *       └── HUDContent                [GameObject]
 *           ├── TrackContainer        [GameObject]
 *           │   ├── TrackBackground   [GameObject]
 *           │   └── FillBar           [GameObject]
 *           ├── DistanceText          [GameObject]
 *           └── PhaseNameText         [GameObject]
 *
 * STEP 2 — RECTTRANSFORM SETTINGS
 * ─────────────────────────────────
 * JourneyHUD:
 *   Anchor preset: Left, Stretch (top and bottom)
 *   Width: 88
 *   Left: 0, Top: 0, Bottom: 0
 *   Pivot: (0, 0.5)
 *
 * Background (Image component):
 *   Anchor: Stretch all
 *   Color: RGBA (8, 8, 15, 240) — near black
 *   Image Type: Simple
 *   Raycast Target: OFF
 *
 * HUDContent (RectTransform only):
 *   Anchor: Stretch all, Padding: 12px all sides
 *
 * TrackContainer (RectTransform only):
 *   Anchor: Middle-center
 *   Width: 2
 *   Height: 350         ← must match trackHeight field on the script
 *   Pos X: -20          ← offset left to leave room for labels on the right
 *   Anchor Min/Max Y: set to centre (0.5, 0.5)
 *   Pivot: (0.5, 0.5)
 *
 * TrackBackground (Image component):
 *   Anchor: Stretch (all four sides to 0)
 *   Color: RGBA (20, 20, 36, 255)
 *   Raycast Target: OFF
 *
 * FillBar (RawImage component):
 *   Anchor Min: (0.5, 0)   ← anchored to BOTTOM centre
 *   Anchor Max: (0.5, 0)
 *   Pivot: (0.5, 0)
 *   Width: 4
 *   Height: 0              ← script sets this each frame
 *   Pos X: 0, Pos Y: 0
 *   Raycast Target: OFF
 *
 * DistanceText (TextMeshProUGUI):
 *   Anchor: Bottom-centre of HUDContent
 *   Font Size: 14
 *   Alignment: Centre
 *   Color: RGBA (220, 220, 240, 255)
 *   Height: 20
 *   Pos Y: 14 (above bottom edge)
 *
 * PhaseNameText (TextMeshProUGUI):
 *   Anchor: Bottom-centre, just above DistanceText
 *   Font Size: 8
 *   Alignment: Centre
 *   Color: RGBA (150, 150, 190, 255)
 *   Pos Y: 32
 *
 * STEP 3 — ADD THE SCRIPT
 * ────────────────────────
 * Add UniverseJourneyHUD.cs to the JourneyHUD GameObject.
 *
 * STEP 4 — ASSIGN REFERENCES
 * ───────────────────────────
 * In the Inspector, drag:
 *   Tracker          → your UniverseJourneyTracker GameObject
 *   Track Container  → the TrackContainer RectTransform
 *   Track Background → the TrackBackground Image component
 *   Fill Image       → the FillBar RawImage component
 *   Distance Text    → the DistanceText TMP_Text component
 *   Phase Name Text  → the PhaseNameText TMP_Text component
 *
 * STEP 5 — CONFIGURE PHASE SETTINGS
 * ───────────────────────────────────
 * In Phase Configuration (7 entries):
 *   [0] Quasar       isMacroPhase ✓   icon: drag your quasar sprite (or leave null)
 *   [1] Cosmic Web   isMacroPhase ✓   icon: drag your cosmic web sprite
 *   [2] Galaxy       isMacroPhase ✗   icon: drag your galaxy sprite
 *   [3] Cosmic Web   isMacroPhase ✓   icon: (reuse cosmic web sprite)
 *   [4] Milky Way    isMacroPhase ✗   icon: drag your milky way sprite
 *   [5] Solar System isMacroPhase ✗   icon: drag your solar system sprite
 *   [6] Earth        isMacroPhase ✗   icon: drag your earth sprite
 *
 * STEP 6 — MATCH TRACK HEIGHT
 * ─────────────────────────────
 * Set trackHeight on the script = the Height of your TrackContainer (default 350).
 * If you change TrackContainer height, update this field to match.
 *
 * STEP 7 — VERIFY IN PLAY MODE
 * ──────────────────────────────
 * Press Play. You should see:
 *   • 7 nodes appear along the track (bottom = Quasar, top = Earth)
 *   • The violet-to-red gradient fill growing upward as the player moves
 *   • The active node pulsing in amber
 *   • When the player enters Galaxy/MilkyWay/SolarSystem/Earth:
 *       the bar smoothly transitions to micro view (current phase expands)
 *   • When returning to a Cosmic Web phase: bar transitions back to macro view
 *
 * COMMON ISSUES
 * ──────────────
 * Nodes not visible:  Check TrackContainer is assigned and has correct height.
 * Fill not growing:   Check FillBar RawImage pivot is (0.5, 0) and anchor is bottom.
 * Labels cut off:     Increase labelWidth or reduce labelFontSize in the inspector.
 * No transition:      Confirm JourneyLayerResponder fires OnPhaseChanged on the tracker.
 */


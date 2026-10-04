using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Everything that is on screen during the tutorial, and — more importantly — when it
/// is not.
///
/// The default state of every element here is off, and each one is switched on by a
/// named beat rather than by being present in the scene.
///
/// Four elements exist in Phase 0–1:
///
///   1. Control hint      What to do right now, e.g. "RIGHT STICK · LOOK UP". First seen
///                        at A1, because the piece should tell a walk-up visitor they can
///                        move the view before it asks them to — a deliberate departure
///                        from GDD §7, which puts the first prompt at B1. Every gated
///                        beat then rewrites it through HintMode, so the line always
///                        states the current ask rather than the first one. A stick
///                        diagram hangs off its left end — TutorialStickGuide_NEW — as
///                        a child of the plate, so the picture and the words arrive,
///                        fade and leave as one thing.
///
///                        MOSTLY DRAWN, NOT TYPED. Where hintArt holds a picture for what
///                        the beat is asking, the plate and the line of text give way to
///                        it; where it does not, the words are still there and still
///                        correct. So art arrives one plate at a time rather than in one
///                        commit, and a beat whose words somebody rewrites tomorrow
///                        degrades to a legible line instead of the wrong picture.
///   2. Control legend    First seen at B1 and never dismissed. It persists past the
///                        end of the tutorial into Phase −1, so there is deliberately
///                        no code path that hides it again.
///   3. Reticle           Not in the GDD's UI list, because it is not an interface
///                        element the player has to learn — but B1's gate is "mote held
///                        inside the reticle", which is unplayable if the reticle is
///                        invisible. It arrives with the legend.
///   4. A prompt          First seen at B4. Owned here rather than by the beat because
///                        GDD §5 requires the continue affordance to be the same shape
///                        in the same position every time; only the words come from the
///                        beat.
///
/// The HUD is driven by the director's events rather than polled, so a beat that is
/// skipped with F2 or jumped to with JumpToBeat leaves the HUD in the right state.
/// The legend uses "first seen at or after B1" — an index comparison, not a string
/// match on B1 — so jumping straight to B3 during whitebox still shows it.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("TUT HUD", "#E0A030")]
public class TutorialHUD_NEW : MonoBehaviour
{
    [Header("Wiring")]
    [Tooltip("Leave empty to find the director in the scene.")]
    [SerializeField] TutorialDirector_NEW director;

    [Tooltip("Leave empty to find it in the scene. While it holds the piece, the hint " +
             "line reads B TO RESUME in every beat.")]
    [SerializeField] TutorialPause_NEW pause;

    [Tooltip("Leave empty to find it. When present, it shows B TO RESUME and the hint line " +
             "stays clear while paused.")]
    [SerializeField] TutorialPauseCard_NEW pauseCard;

    [Header("Control legend")]
    [Tooltip("Root of the legend. Hidden until the legend beat is reached, then never " +
             "hidden again.")]
    [SerializeField] GameObject legendRoot;

    [SerializeField] TMP_Text legendText;

    [Tooltip("GDD §4 gives this string verbatim.")]
    [SerializeField] string legendContent = "RIGHT STICK = LOOK    LEFT STICK = ZOOM    A = CONFIRM / RECENTRE";

    [Tooltip("Let the builder keep the legend wording in step with the control scheme. " +
             "Untick to write your own and have it left alone.")]
    [SerializeField] bool builderOwnsCopy = true;

    [Tooltip("Beat at which the legend arrives and stays. GDD §7 says B1.")]
    [SerializeField] string legendFirstBeatId = "B1";

    [Header("Reticle")]
    [Tooltip("Arrives with the legend. B1's gate is unplayable without it.")]
    [SerializeField] GameObject reticleRoot;

    [Header("Range map")]
    [Tooltip("RETIRED, and normally empty. The corner map answered 'am I going anywhere?' " +
             "with a second diagram to learn, in a piece whose whole subject is learning to " +
             "read one — the spectrum bar. The builder no longer creates it or fills this.\n\n" +
             "Everything that touches it here is null-guarded, so the field is harmless: " +
             "drop a map back in and it comes up with the legend again, exactly as before.")]
    [SerializeField] GameObject mapRoot;

    [Header("Control hint")]
    [Tooltip("The line that says what to do with the stick. First seen at B1, and the " +
             "storyboard keeps it up through B2 and B3 with no new prompt.")]
    [SerializeField] GameObject hintRoot;

    [SerializeField] TMP_Text hintLabel;
    [SerializeField] CanvasGroup hintGroup;

    [Tooltip("Where a drawn hint plate is shown. Empty means every hint is words on a " +
             "plate, exactly as before.")]
    [SerializeField] Image hintArtImage;

    [Tooltip("The dark plate behind the words. Hidden while a drawn hint is up, because " +
             "the art brings its own background and a plate behind it would box it in.")]
    [SerializeField] GameObject hintPlate;

    [Tooltip("Height of a drawn hint, in canvas pixels. The width follows from the " +
             "picture's own proportions, so plates of different widths all sit on one " +
             "line rather than each being its own size.")]
    [SerializeField] float hintArtHeight = 170f;

    [Tooltip("A picture for a beat's hint line. Anything with no picture here stays as " +
             "words on a plate — see the class summary.")]
    [SerializeField] HintArt[] hintArt = new HintArt[0];

    [Tooltip("The stick diagram beside the hint line. Optional — with none in the scene " +
             "the piece is exactly what it was, a line of text.\n\n" +
             "Driven from the CURRENT beat rather than from whichever beat last wrote the " +
             "hint line: B2 and B3 keep B1's words and ask for a different movement, so " +
             "following the words would leave the diagram pointing at a mote the player " +
             "has already caught.")]
    [SerializeField] TutorialStickGuide_NEW stickGuide;

    [Header("A prompt")]
    [Tooltip("Root of the continue affordance. Same shape, same position, every time.")]
    [SerializeField] GameObject confirmPromptRoot;

    [SerializeField] TMP_Text confirmPromptText;

    [Header("Fade")]
    [Tooltip("Seconds for elements to fade in. Text that snaps on is read as a glitch; " +
             "text that arrives is read as an instruction.")]
    [SerializeField] float fadeDuration = 0.35f;

    [SerializeField] CanvasGroup legendGroup;
    [SerializeField] CanvasGroup promptGroup;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    /// <summary>
    /// One drawn hint plate, and the words it stands in for.
    ///
    /// Keyed on the beat's own hint line rather than on its beat id, because two beats
    /// ask for the same thing in the same words — B4 and C5 both say A TO RECENTRE — and
    /// a picture is a picture of the ask, not of the frame.
    /// </summary>
    [System.Serializable]
    public class HintArt
    {
        [Tooltip("The hint line this picture replaces, e.g. \"RIGHT STICK  ·  LOOK UP\".\n\n" +
                 "Matched loosely: case, spaces and punctuation are ignored, so somebody " +
                 "changing the separator in a beat's copy does not silently drop the picture.")]
        public string hintText;

        public Sprite sprite;

        [Tooltip("Tick where the picture already draws the A button. The separate A prompt " +
                 "at the lower edge then stays down for as long as this picture is up, so " +
                 "one screen never asks for the same button twice.")]
        public bool carriesTheButton;

        [Tooltip("Put the stick diagram ON this picture — over the pad drawn in it — " +
                 "instead of hanging it off the plate's left edge.")]
        public bool stickOnArt;

        [Tooltip("Where the centre of the stick ring sits on the picture, 0 to 1: x from " +
                 "the left edge, y from the BOTTOM edge. Read every frame, so it can be " +
                 "dragged into place in Play mode (copy the value out before stopping).")]
        public Vector2 stickPosition = new Vector2(0.5f, 0.5f);

        [Tooltip("Size of the whole diagram — ring, knob and its travel together. 1 is " +
                 "the authored size.")]
        public float stickScale = 1f;
    }

    /// <summary>Whether Build or Update may rewrite the legend wording.</summary>
    public bool BuilderOwnsCopy { get { return builderOwnsCopy; } }

    bool _legendShown;
    bool _hintShown;
    string _shownHintText;

    /// <summary>The picture on the hint line, or null while it is carrying words.</summary>
    HintArt _shownArt;

    RectTransform _hintRect;

    /// <summary>
    /// The hint's authored size, which is the size of the plate the words sit on.
    ///
    /// Read once, before anything has resized it: a drawn hint is a different width for
    /// every plate, and the stick diagram is anchored to the hint's left edge, so the
    /// hint has to become the size of whatever is actually on it and go back afterwards.
    /// </summary>
    Vector2 _plateSize;

    /// <summary>The stick diagram's authored placement, put back whenever a hint does
    /// not ask for it to sit on the picture.</summary>
    RectTransform _stickRect;
    Vector2 _stickAnchorMin, _stickAnchorMax, _stickPosition;
    Vector3 _stickScale;

    /// <summary>The beat whose words are on the hint line, polled for LiveHintText.</summary>
    TutorialBeat_NEW _hintBeat;

    /// <summary>
    /// The beat that is open, whatever it did to the hint line. The stick diagram
    /// follows this one — see the stickGuide tooltip for why the two are not the same.
    /// </summary>
    TutorialBeat_NEW _currentBeat;

    /// <summary>What the beats want on the hint line when the piece is not paused.</summary>
    string _beatHintText;

    int _legendBeatIndex = -1;
    Beat_Confirm_NEW _promptBeat;

    void Awake()
    {
        if (director == null) director = FindObjectOfType<TutorialDirector_NEW>();
        if (pause == null) pause = FindObjectOfType<TutorialPause_NEW>();
        if (pauseCard == null) pauseCard = FindObjectOfType<TutorialPauseCard_NEW>();

        if (director == null)
            Debug.LogError("[TutorialHUD_NEW] No TutorialDirector_NEW. The HUD will stay dark.", this);

        if (legendText != null) legendText.text = legendContent;

        if (hintRoot != null) _hintRect = hintRoot.GetComponent<RectTransform>();
        if (_hintRect != null) _plateSize = _hintRect.sizeDelta;

        if (stickGuide != null)
        {
            _stickRect = stickGuide.GetComponent<RectTransform>();
            _stickAnchorMin = _stickRect.anchorMin;
            _stickAnchorMax = _stickRect.anchorMax;
            _stickPosition = _stickRect.anchoredPosition;
            _stickScale = _stickRect.localScale;
        }

        HideAll();
    }

    void OnEnable()
    {
        if (director == null) return;

        director.OnBeatEntered += HandleBeatEntered;
        director.OnBeatSatisfied += HandleBeatSatisfied;
        director.OnIdleTimeout += HandleReset;
        director.OnTutorialComplete += HandleComplete;
    }

    void OnDisable()
    {
        if (director == null) return;

        director.OnBeatEntered -= HandleBeatEntered;
        director.OnBeatSatisfied -= HandleBeatSatisfied;
        director.OnIdleTimeout -= HandleReset;
        director.OnTutorialComplete -= HandleComplete;
    }

    void Update()
    {
        TickPrompt(Time.unscaledDeltaTime);
        TickLegendFade(Time.unscaledDeltaTime);
        TickLiveHint();
        TickHintFade(Time.unscaledDeltaTime);
        TickStickGuide();
    }

    /// <summary>
    /// Hand the stick diagram the current beat's gesture, every frame.
    ///
    /// Polled rather than pushed, because most beats compute their gesture from the
    /// world — the bearing to a mote, which side the disc is on — and there is no event
    /// for "the player has turned a bit". See TutorialBeat_NEW.StickGesture.
    ///
    /// Nothing while paused. The pause card owns the screen and the one instruction that
    /// matters there is B TO RESUME; a stick diagram behind it would be asking for a
    /// movement that is not going to do anything.
    /// </summary>
    void TickStickGuide()
    {
        if (stickGuide == null) return;

        bool paused = pause != null && pause.IsPaused;

        bool live = !paused && _currentBeat != null && _currentBeat.IsActive;

        stickGuide.SetGesture(live
            ? _currentBeat.StickGesture
            : TutorialStickGuide_NEW.Gesture.None);

        PlaceStickGuide();
    }

    /// <summary>
    /// On the picture where the picture asks for it, otherwise where the builder put it.
    /// Every frame rather than on a change, so stickPosition and stickScale can be tuned
    /// in Play mode against the actual art.
    /// </summary>
    void PlaceStickGuide()
    {
        if (_stickRect == null) return;

        bool onArt = _shownArt != null && _shownArt.stickOnArt;

        Vector2 anchor = onArt ? _shownArt.stickPosition : Vector2.zero;

        _stickRect.anchorMin = onArt ? anchor : _stickAnchorMin;
        _stickRect.anchorMax = onArt ? anchor : _stickAnchorMax;
        _stickRect.anchoredPosition = onArt ? Vector2.zero : _stickPosition;
        _stickRect.localScale = onArt ? Vector3.one * _shownArt.stickScale : _stickScale;

        stickGuide.ShowStickLabel(!onArt);
    }

    /// <summary>
    /// Decide what the hint line says this frame, and change it only if that differs.
    ///
    /// Two sources, in priority order:
    ///
    ///   The pause. While TutorialPause_NEW holds the piece, the line reads B TO RESUME
    ///       in every beat, including the ones that normally show no hint at all. A
    ///       pause with no visible way out reads as a crash.
    ///   The current beat. Otherwise, whatever the beat that last set the line wants —
    ///       polled through LiveHintText, so a beat can change its own words mid-beat
    ///       without holding a reference to the HUD. A beat set to Keep leaves the
    ///       previous beat's words standing, which is what "no new prompt" means.
    ///
    /// Compared as strings every frame, which is cheap and needs no events: resuming
    /// hands the line straight back to the beat, cleared or not.
    /// </summary>
    void TickLiveHint()
    {
        if (_hintBeat != null && _hintBeat.IsActive) _beatHintText = _hintBeat.LiveHintText;

        bool paused = pause != null && pause.IsPaused;

        // With a pause card in the scene the card carries B TO RESUME, and the hint line
        // clears so the same words are not on screen twice. Without one, the hint line
        // still carries it, so a pause is never left with no way out on screen.
        string wanted = paused
            ? (pauseCard != null ? null : pause.ResumeHintText)
            : _beatHintText;

        if (wanted == _shownHintText) return;

        // Blank and cleared are the same state; without this a beat asking for an empty
        // line would be cleared again every frame.
        if (string.IsNullOrEmpty(wanted) && string.IsNullOrEmpty(_shownHintText)) return;

        ShowHint(wanted);
    }

    // ── Director events ──────────────────────────────────────────────────────

    void HandleBeatEntered(TutorialBeat_NEW beat)
    {
        if (beat == null) return;

        _currentBeat = beat;

        if (!_legendShown && ReachedLegendBeat(beat)) ShowLegend();

        // Record what the beat wants; TickLiveHint puts it on screen, so a beat that opens
        // while the piece is paused (a debug jump) does not cover B TO RESUME.
        if (beat.Hint == TutorialBeat_NEW.HintMode.Show)
        {
            _hintBeat = beat;
            _beatHintText = beat.LiveHintText;
        }
        else if (beat.Hint == TutorialBeat_NEW.HintMode.Clear)
        {
            _hintBeat = null;
            _beatHintText = null;
        }

        // Keep leaves the line and its owner alone, which is what "no new prompt" means.

        TickLiveHint();

        // Only a confirm beat asks for the prompt, and it decides when within the beat.
        _promptBeat = beat.GetComponent<Beat_Confirm_NEW>();

        if (_promptBeat != null && confirmPromptText != null)
            confirmPromptText.text = _promptBeat.PromptText;
    }

    void HandleBeatSatisfied(TutorialBeat_NEW beat)
    {
        if (_promptBeat != null && beat == _promptBeat) _promptBeat = null;
    }

    /// <summary>
    /// The last beat is done. Everything goes.
    ///
    /// THIS IS WHAT MADE THE LAST FRAME LOOK BROKEN. A satisfied beat's hint line is
    /// only ever replaced by the next beat's, and after the last one there is no next
    /// beat — so B TO PAUSE stayed on screen, asking for a button that had already been
    /// pressed and done its job. From in front of the piece that is indistinguishable
    /// from a gate that did not fire, and the only thing to do about it is press B
    /// again, which does nothing.
    ///
    /// Clearing it here rather than in the outro because it is the HUD's own business:
    /// the tutorial being over is a fact about the director, and every element in here
    /// is switched on by a beat that no longer exists.
    /// </summary>
    void HandleComplete()
    {
        HandleReset();

        if (debugLog) Debug.Log("[TutorialHUD_NEW] Tutorial complete; HUD cleared.", this);
    }

    /// <summary>
    /// Back to attract. The legend goes with it — the next visitor has to see it arrive
    /// at B1 or it is not teaching anything, it is just decoration that was already there.
    /// </summary>
    void HandleReset()
    {
        _legendShown = false;
        _hintShown = false;
        _shownHintText = null;
        _shownArt = null;
        _promptBeat = null;
        _hintBeat = null;
        _currentBeat = null;
        _beatHintText = null;

        HideAll();
    }

    // ── Elements ─────────────────────────────────────────────────────────────

    bool ReachedLegendBeat(TutorialBeat_NEW beat)
    {
        if (string.IsNullOrEmpty(legendFirstBeatId)) return false;

        if (_legendBeatIndex < 0) _legendBeatIndex = IndexOfBeat(legendFirstBeatId);

        // Unknown id: fall back to an exact match so a typo shows the legend late
        // rather than never.
        if (_legendBeatIndex < 0)
            return string.Equals(beat.BeatId, legendFirstBeatId, System.StringComparison.OrdinalIgnoreCase);

        return director != null && director.BeatIndex >= _legendBeatIndex;
    }

    int IndexOfBeat(string id)
    {
        if (director == null) return -1;

        for (int i = 0; i < director.BeatCount; i++)
        {
            TutorialBeat_NEW b = BeatAt(i);
            if (b == null) continue;

            if (string.Equals(b.BeatId, id, System.StringComparison.OrdinalIgnoreCase)) return i;
        }

        return -1;
    }

    TutorialBeat_NEW BeatAt(int index)
    {
        // The director owns its list; this reads it through the only public handle
        // there is, which is the current beat. For indexing, walk the children in the
        // same order the director collected them.
        TutorialBeat_NEW[] all = director.GetComponentsInChildren<TutorialBeat_NEW>(true);
        if (index < 0 || index >= all.Length) return null;

        return all[index];
    }

    void ShowLegend()
    {
        _legendShown = true;

        if (legendRoot != null) legendRoot.SetActive(true);
        if (reticleRoot != null) reticleRoot.SetActive(true);
        if (mapRoot != null) mapRoot.SetActive(true);

        if (debugLog) Debug.Log("[TutorialHUD_NEW] Legend and reticle on.", this);
    }

    void ShowHint(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Trim().Length == 0)
        {
            // A beat set to Show with nothing to show meant Clear.
            ClearHint();
            return;
        }

        _hintShown = true;
        _shownHintText = text;

        HintArt art = FindArt(text);
        _shownArt = art != null && art.sprite != null && hintArtImage != null ? art : null;

        if (_shownArt != null) ShowArt(_shownArt);
        else ShowWords(text);

        if (hintRoot != null) hintRoot.SetActive(true);

        if (debugLog)
            Debug.Log("[TutorialHUD_NEW] Hint: '" + text + "'" +
                      (_shownArt != null ? " (drawn)." : " (words)."), this);
    }

    /// <summary>The drawn plate for this ask, in place of the words and their background.</summary>
    void ShowArt(HintArt art)
    {
        hintArtImage.sprite = art.sprite;
        hintArtImage.preserveAspect = true;

        Vector2 size = ArtSize(art.sprite);

        hintArtImage.rectTransform.sizeDelta = size;

        // The hint becomes the size of the picture, because the stick diagram hangs off
        // its left edge — see _plateSize. Leaving it at the plate's width would leave the
        // diagram floating a couple of hundred pixels away from the thing it illustrates.
        if (_hintRect != null) _hintRect.sizeDelta = size;

        hintArtImage.gameObject.SetActive(true);

        if (hintLabel != null) hintLabel.gameObject.SetActive(false);
        if (hintPlate != null) hintPlate.SetActive(false);
    }

    /// <summary>The words on their plate: what every hint was, and what any hint with no
    /// picture still is.</summary>
    void ShowWords(string text)
    {
        if (hintLabel != null)
        {
            hintLabel.text = text;
            hintLabel.gameObject.SetActive(true);
        }

        if (hintPlate != null) hintPlate.SetActive(true);
        if (hintArtImage != null) hintArtImage.gameObject.SetActive(false);

        if (_hintRect != null && _plateSize.sqrMagnitude > 0f) _hintRect.sizeDelta = _plateSize;
    }

    /// <summary>Canvas size of a drawn plate: the authored height, and the width its own
    /// proportions ask for.</summary>
    Vector2 ArtSize(Sprite sprite)
    {
        Rect rect = sprite.rect;

        float aspect = rect.height > 0.001f ? rect.width / rect.height : 1f;

        return new Vector2(hintArtHeight * aspect, hintArtHeight);
    }

    HintArt FindArt(string text)
    {
        if (hintArt == null) return null;

        string wanted = ArtKey(text);
        if (wanted.Length == 0) return null;

        for (int i = 0; i < hintArt.Length; i++)
        {
            HintArt art = hintArt[i];
            if (art == null) continue;

            if (ArtKey(art.hintText) == wanted) return art;
        }

        return null;
    }

    /// <summary>
    /// Letters and digits only, upper case.
    ///
    /// So "RIGHT STICK  ·  LOOK UP", "Right Stick - Look Up" and "RIGHTSTICK LOOKUP" are
    /// one key. The separator between the control and the action is a typographic choice
    /// somebody is entitled to change, and it should not decide whether the picture for
    /// that ask can still be found.
    ///
    /// Public because the builder matches its own table against this one — one rule for
    /// what counts as the same ask, rather than two that can disagree.
    /// </summary>
    public static string ArtKey(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        System.Text.StringBuilder key = new System.Text.StringBuilder(text.Length);

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (char.IsLetterOrDigit(c)) key.Append(char.ToUpperInvariant(c));
        }

        return key.ToString();
    }

    void ClearHint()
    {
        _hintShown = false;
        _shownHintText = null;
        _shownArt = null;

        if (debugLog) Debug.Log("[TutorialHUD_NEW] Hint cleared.", this);
    }

    void TickHintFade(float dt)
    {
        if (hintGroup == null) return;

        hintGroup.alpha = Step(hintGroup.alpha, _hintShown ? 1f : 0f, dt);

        if (!_hintShown && hintRoot != null && hintGroup.alpha <= 0.01f && hintRoot.activeSelf)
            hintRoot.SetActive(false);
    }

    void TickLegendFade(float dt)
    {
        if (legendGroup == null) return;

        float target = _legendShown ? 1f : 0f;
        legendGroup.alpha = Step(legendGroup.alpha, target, dt);
    }

    /// <summary>
    /// Whether the picture on the hint line is already showing the A button.
    ///
    /// B4, C2 and C5 each put A TO RECENTRE or A TO EMIT on the hint line AND raise the
    /// prompt at the lower edge, which was two ways of saying one thing and read as two
    /// things while the words were only words. A drawn plate carries the button itself,
    /// so the second one goes.
    /// </summary>
    bool ArtCarriesTheButton
    {
        get { return _hintShown && _shownArt != null && _shownArt.carriesTheButton; }
    }

    void TickPrompt(float dt)
    {
        bool wanted = _promptBeat != null && _promptBeat.PromptVisible && !ArtCarriesTheButton;

        if (confirmPromptRoot != null && confirmPromptRoot.activeSelf != wanted)
        {
            // Keep the object alive while the group fades out, so the prompt does not
            // vanish on the frame of the press.
            if (wanted) confirmPromptRoot.SetActive(true);
            else if (promptGroup == null || promptGroup.alpha <= 0.01f) confirmPromptRoot.SetActive(false);
        }

        if (promptGroup != null)
            promptGroup.alpha = Step(promptGroup.alpha, wanted ? 1f : 0f, dt);
    }

    float Step(float current, float target, float dt)
    {
        if (fadeDuration <= 0f) return target;
        return Mathf.MoveTowards(current, target, dt / fadeDuration);
    }

    void HideAll()
    {
        if (legendRoot != null) legendRoot.SetActive(false);
        if (reticleRoot != null) reticleRoot.SetActive(false);
        if (mapRoot != null) mapRoot.SetActive(false);
        if (hintRoot != null) hintRoot.SetActive(false);
        if (confirmPromptRoot != null) confirmPromptRoot.SetActive(false);

        if (legendGroup != null) legendGroup.alpha = 0f;
        if (hintGroup != null) hintGroup.alpha = 0f;
        if (promptGroup != null) promptGroup.alpha = 0f;

        if (stickGuide != null) stickGuide.ResetForAttract();
    }
}

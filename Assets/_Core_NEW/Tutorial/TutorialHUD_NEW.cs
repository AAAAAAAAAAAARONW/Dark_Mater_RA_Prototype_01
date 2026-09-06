using UnityEngine;
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
///                        states the current ask rather than the first one.
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

    [Header("Control legend")]
    [Tooltip("Root of the legend. Hidden until the legend beat is reached, then never " +
             "hidden again.")]
    [SerializeField] GameObject legendRoot;

    [SerializeField] Text legendText;

    [Tooltip("GDD §4 gives this string verbatim.")]
    [SerializeField] string legendContent = "RIGHT STICK = LOOK      A = CONFIRM / RECENTRE";

    [Tooltip("Beat at which the legend arrives and stays. GDD §7 says B1.")]
    [SerializeField] string legendFirstBeatId = "B1";

    [Header("Reticle")]
    [Tooltip("Arrives with the legend. B1's gate is unplayable without it.")]
    [SerializeField] GameObject reticleRoot;

    [Header("Control hint")]
    [Tooltip("The line that says what to do with the stick. First seen at B1, and the " +
             "storyboard keeps it up through B2 and B3 with no new prompt.")]
    [SerializeField] GameObject hintRoot;

    [SerializeField] Text hintLabel;
    [SerializeField] CanvasGroup hintGroup;

    [Header("A prompt")]
    [Tooltip("Root of the continue affordance. Same shape, same position, every time.")]
    [SerializeField] GameObject confirmPromptRoot;

    [SerializeField] Text confirmPromptText;

    [Header("Fade")]
    [Tooltip("Seconds for elements to fade in. Text that snaps on is read as a glitch; " +
             "text that arrives is read as an instruction.")]
    [SerializeField] float fadeDuration = 0.35f;

    [SerializeField] CanvasGroup legendGroup;
    [SerializeField] CanvasGroup promptGroup;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    bool _legendShown;
    bool _hintShown;
    int _legendBeatIndex = -1;
    Beat_Confirm_NEW _promptBeat;

    void Awake()
    {
        if (director == null) director = FindObjectOfType<TutorialDirector_NEW>();

        if (director == null)
            Debug.LogError("[TutorialHUD_NEW] No TutorialDirector_NEW. The HUD will stay dark.", this);

        if (legendText != null) legendText.text = legendContent;

        HideAll();
    }

    void OnEnable()
    {
        if (director == null) return;

        director.OnBeatEntered += HandleBeatEntered;
        director.OnBeatSatisfied += HandleBeatSatisfied;
        director.OnIdleTimeout += HandleReset;
    }

    void OnDisable()
    {
        if (director == null) return;

        director.OnBeatEntered -= HandleBeatEntered;
        director.OnBeatSatisfied -= HandleBeatSatisfied;
        director.OnIdleTimeout -= HandleReset;
    }

    void Update()
    {
        TickPrompt(Time.unscaledDeltaTime);
        TickLegendFade(Time.unscaledDeltaTime);
        TickHintFade(Time.unscaledDeltaTime);
    }

    // ── Director events ──────────────────────────────────────────────────────

    void HandleBeatEntered(TutorialBeat_NEW beat)
    {
        if (beat == null) return;

        if (!_legendShown && ReachedLegendBeat(beat)) ShowLegend();

        if (beat.Hint == TutorialBeat_NEW.HintMode.Show) ShowHint(beat.HintText);
        else if (beat.Hint == TutorialBeat_NEW.HintMode.Clear) ClearHint();

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
    /// Back to attract. The legend goes with it — the next visitor has to see it arrive
    /// at B1 or it is not teaching anything, it is just decoration that was already there.
    /// </summary>
    void HandleReset()
    {
        _legendShown = false;
        _hintShown = false;
        _promptBeat = null;

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

        if (hintLabel != null) hintLabel.text = text;
        if (hintRoot != null) hintRoot.SetActive(true);

        if (debugLog) Debug.Log("[TutorialHUD_NEW] Hint: '" + text + "'.", this);
    }

    void ClearHint()
    {
        _hintShown = false;

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

    void TickPrompt(float dt)
    {
        bool wanted = _promptBeat != null && _promptBeat.PromptVisible;

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
        if (hintRoot != null) hintRoot.SetActive(false);
        if (confirmPromptRoot != null) confirmPromptRoot.SetActive(false);

        if (legendGroup != null) legendGroup.alpha = 0f;
        if (hintGroup != null) hintGroup.alpha = 0f;
        if (promptGroup != null) promptGroup.alpha = 0f;
    }
}

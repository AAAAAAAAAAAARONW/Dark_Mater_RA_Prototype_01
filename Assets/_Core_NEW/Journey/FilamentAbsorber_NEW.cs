using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A yellow filament the light flies through, and the line it cuts — the tutorial's lesson
/// ("passing gas cuts a line"), shown once more in the journey where the player can see the
/// gas and the line together.
///
/// Put it on a trigger collider that covers a filament on the light's path (a filament the
/// level is arranged to pass through, before the galaxy). When the light enters it:
///
///   1. BakedSpectrumSource_NEW.AddAbsorber cuts a line at the light's present redshift. It
///      is born at the birth tick and slides redward with the rest, on the bar and on the
///      trail — the same picture as the tutorial.
///   2. AbsorberHighlight_NEW blinks that line and points at it, the tutorial's highlight.
///   3. The first time only, the voice says J3b. If J3 is still being said, it waits for it
///      (JourneyVoiceOver_NEW queues), and if the layer changes first, it is dropped.
///
/// WHY A PLACED FILAMENT AND NOT THE BAKE. In the faithful bake the forest freezes at
/// z 2.46, just before the first cosmic web, so no line forms there on its own. This is
/// the one deliberate teaching representation in the journey's spectrum; the bake is not
/// changed, and every other line is still the bake's.
///
/// Same trigger rule as LayerGate_NEW: the player's collider, tagged Player.
/// </summary>
[RequireComponent(typeof(Collider))]
[HierarchyBadge_NEW("FILAMENT", "#E0C040")]
public class FilamentAbsorber_NEW : MonoBehaviour
{
    [Header("Wiring (found if empty)")]
    [SerializeField] BakedSpectrumSource_NEW source;
    [SerializeField] AbsorberHighlight_NEW highlight;
    [SerializeField] JourneyVoiceOver_NEW voiceOver;

    [Header("The line")]
    [Tooltip("How deep, 0–1. 0.9 reads as a clear dark line without a hard black gap.")]
    [Range(0f, 1f)]
    [SerializeField] float depth = 0.9f;

    [Tooltip("Width in km/s. About 600 is one column of the bar; under that it can vanish " +
             "between columns.")]
    [Min(50f)]
    [SerializeField] float widthKmS = 700f;

    [Tooltip("Blink it and point at it, as the tutorial does.")]
    [SerializeField] bool highlightLine = true;

    [Header("The voice")]
    [Tooltip("Said the first time any filament is crossed in a run: J3b. Leave empty on the " +
             "others.")]
    [SerializeField] AudioClip firstTimeLine;

    [Header("Trigger")]
    [SerializeField] string playerTag = "Player";

    [Tooltip("Also fired on each crossing, for anything else that should react.")]
    [SerializeField] UnityEvent onCrossed = new UnityEvent();

    [SerializeField] bool debugLog = false;

    /// <summary>Shared by every filament: J3b is said once per run, whichever is crossed first.</summary>
    static bool _lineSaid;

    bool _crossed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { _lineSaid = false; }

    void Awake()
    {
        if (source == null) source = FindObjectOfType<BakedSpectrumSource_NEW>();
        if (highlight == null) highlight = FindObjectOfType<AbsorberHighlight_NEW>();
        if (voiceOver == null) voiceOver = FindObjectOfType<JourneyVoiceOver_NEW>();

        Collider c = GetComponent<Collider>();
        if (c != null && !c.isTrigger)
        {
            Debug.LogWarning("[FilamentAbsorber_NEW] '" + name + "' collider was not a trigger. Forcing isTrigger.", this);
            c.isTrigger = true;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (_crossed || other == null) return;
        if (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag)) return;

        _crossed = true;

        float z = source != null ? source.AddAbsorber(depth, widthKmS) : float.NaN;

        if (highlightLine && highlight != null && !float.IsNaN(z)) highlight.HighlightLine(z);

        if (!_lineSaid && firstTimeLine != null && voiceOver != null)
        {
            _lineSaid = true;
            voiceOver.Say(firstTimeLine);
        }

        onCrossed.Invoke();

        if (debugLog)
            Debug.Log("[FilamentAbsorber_NEW] '" + name + "' crossed; line at z " + z.ToString("0.0000") + ".", this);
    }

    /// <summary>Let this filament cut its line again. For a debug jump back to the layer.</summary>
    public void ResetFilament()
    {
        _crossed = false;
    }

    void OnDrawGizmos()
    {
        if (!DebugView_NEW.Gizmos) return;

        Collider c = GetComponent<Collider>();
        if (c == null) return;

        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.35f);
        Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
    }
}

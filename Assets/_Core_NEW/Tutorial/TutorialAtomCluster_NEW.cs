using UnityEngine;

/// <summary>
/// D4 — the small group of hydrogen atoms, arriving one after another.
///
/// GDD §6: "A small group of atoms, not a cloud. Passing through cuts three or four
/// more lines. No slow motion."
///
/// A GROUP, NOT A CLOUD, and the distinction is the frame's whole content. D2 has just
/// established one atom, one line. D4's job is to say that this keeps happening and
/// that the marks accumulate — so the player has to be able to count them. Four atoms
/// arriving a second and a half apart, each cutting a line the player can watch appear,
/// says that. A cloud says "lots of things happened" and leaves the spectrum looking
/// like the Lyman-alpha forest graph from the May 2026 playtest, which arrived already
/// populated and which 4 of 5 players could not read.
///
/// NO SLOW MOTION, also from §6, and it follows from the same reasoning. D2 bought time
/// to look because there was one thing to understand. D4 is the same thing four times,
/// at speed, and slowing it down would say that each one is its own event again.
///
/// This owns only the timing. Each atom is a TutorialAtom_NEW doing exactly what it
/// does in D1, and each one's onImpact cuts a line — so the atoms do not know about the
/// spectrum, the spectrum does not know about the atoms, and the wiring is visible in
/// the Inspector next to the beat it belongs to.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("CLUSTER", "#8FD4E8")]
public class TutorialAtomCluster_NEW : MonoBehaviour
{
    [Header("The group")]
    [Tooltip("The atoms, armed in this order. Three or four — §6 says a group, and a " +
             "fifth is where a group starts becoming a cloud.\n\n" +
             "Leave empty to collect TutorialAtom_NEW children on Awake, which is the " +
             "intended setup.")]
    [SerializeField] TutorialAtom_NEW[] atoms;

    [Tooltip("Seconds between one atom being armed and the next.\n\n" +
             "They each take their own approachSeconds to arrive, so this is also the " +
             "gap between impacts. Long enough to see each line appear on its own, short " +
             "enough that the four read as one passage through a group.")]
    [SerializeField] float secondsApart = 1.6f;

    [Header("Debug")]
    [SerializeField] bool debugLog = false;

    int _armed;
    float _elapsed;
    bool _running;

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>How many have been sent on their way. Read by the debug overlay.</summary>
    public int ArmedCount { get { return _armed; } }

    /// <summary>How many atoms the group holds.</summary>
    public int Count { get { return atoms != null ? atoms.Length : 0; } }

    /// <summary>True once every atom in the group has landed.</summary>
    public bool AllHit
    {
        get
        {
            if (atoms == null) return true;

            for (int i = 0; i < atoms.Length; i++)
                if (atoms[i] != null && !atoms[i].HasHit) return false;

            return true;
        }
    }

    /// <summary>Send the group. D4's onEnter calls this.</summary>
    public void Arm()
    {
        _armed = 0;
        _elapsed = 0f;
        _running = true;

        // The first one leaves immediately, so the beat opens on something moving
        // rather than on an empty frame waiting for a timer.
        ArmNext();
    }

    /// <summary>Put every atom back and stop sending. The attract reset uses this.</summary>
    public void ResetForAttract()
    {
        _running = false;
        _armed = 0;
        _elapsed = 0f;

        if (atoms == null) return;

        for (int i = 0; i < atoms.Length; i++)
            if (atoms[i] != null) atoms[i].ResetForAttract();
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (atoms == null || atoms.Length == 0)
            atoms = GetComponentsInChildren<TutorialAtom_NEW>(true);

        if (atoms.Length == 0)
            Debug.LogError("[TutorialAtomCluster_NEW] No atoms. D4 will cut no lines.", this);
    }

    void Update()
    {
        if (!_running) return;

        // Unscaled, like every other clock in the tutorial. D4 has no slow motion of its
        // own, but D2's can still be easing out as this opens.
        _elapsed += Time.unscaledDeltaTime;

        if (_elapsed < secondsApart) return;

        _elapsed = 0f;
        ArmNext();
    }

    void ArmNext()
    {
        if (atoms == null || _armed >= atoms.Length)
        {
            _running = false;
            return;
        }

        TutorialAtom_NEW atom = atoms[_armed];
        _armed++;

        if (atom != null) atom.Arm();

        if (debugLog)
            Debug.Log("[TutorialAtomCluster_NEW] Armed " + _armed + " of " + Count + ".", this);
    }

    // ── Debug overlay ────────────────────────────────────────────────────────

    void OnGUI()
    {
        if (!DebugView_NEW.Overlay) return;
        if (_armed == 0) return;

        GUI.Label(DebugOverlayRows_NEW.Row(DebugOverlayRows_NEW.Cluster),
                  string.Format("CLUSTER  armed {0}/{1}   {2}",
                                _armed, Count, AllHit ? "all hit" : "in flight"));
    }
}

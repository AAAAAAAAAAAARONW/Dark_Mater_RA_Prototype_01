using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// D5 — the inspect prompt appears, the player opens the close look at the spectrum,
/// and closes it again.
///
/// GDD §6 gives the gate as "Inspect opened and closed once", and the two halves are
/// both load-bearing. Opening proves the player found the control. CLOSING proves they
/// know how to get back out, which is the same lesson B4 teaches about A: in the May
/// 2026 playtest one participant only started experimenting freely once he had found
/// reset. A frame that opens a new view and then advances on its own would leave the
/// player holding a control they have never seen dismissed.
///
/// It is the last new control in the piece, and the only beat that uses a face button
/// other than A. §10 item 1 listed the three face buttons as blocked; an audit of the
/// build settled it — see TutorialInput_NEW.InspectButton. The binding reads the Fire2
/// axis rather than a raw key code, because joystick button 1 is B on an Xbox pad and
/// Cross on a PlayStation one, and Cross is already a confirm alternative.
///
/// The beat owns the gate. TutorialInspectView_NEW owns what the bar
/// does and TutorialSlowMotion_NEW owns the freeze, both wired to onOpened and onClosed
/// in the Inspector — so this file never mentions a time scale or a RectTransform.
/// </summary>
[HierarchyBadge_NEW("BEAT LOOK", "#33B3B3")]
public class Beat_Inspect_NEW : TutorialBeat_NEW
{
    // NO A-PROMPT FIELD HERE, deliberately. §5 requires the continue affordance to be
    // the same shape in the same position every time, and that shape has an A in it —
    // TutorialHUD_NEW draws a disc with the letter A and takes its words from a
    // Beat_Confirm_NEW. D5 is not a continue, it is a different button, and borrowing
    // the A prompt to say B would break the one affordance the piece has kept identical
    // since B4.
    //
    // So D5 asks through the hint line, which is where every non-A instruction has been
    // written since A1: "B TO INSPECT". The builder owns that copy like all the rest.

    [Header("Wiring")]
    [Tooltip("Leave empty to find it in the scene.")]
    [SerializeField] TutorialInspectView_NEW view;

    [Tooltip("Hold the beat open until the view has finished closing, so the next frame " +
             "does not begin over a bar that is still moving.")]
    [SerializeField] bool waitForClose = true;

    [Header("Events")]
    [Tooltip("Fires on the press that opens the view. The freeze and the enlargement " +
             "hang here.")]
    [SerializeField] UnityEvent onOpened = new UnityEvent();

    [Tooltip("Fires on the press that closes it. Whatever onOpened started is undone here.")]
    [SerializeField] UnityEvent onClosed = new UnityEvent();

    bool _opened;
    bool _closed;
    bool _forced;

    protected override void OnBeatEnter()
    {
        _opened = false;
        _closed = false;
        _forced = false;

        if (view == null) view = FindObjectOfType<TutorialInspectView_NEW>();

        if (view == null)
            Debug.LogError("[Beat_Inspect_NEW] " + BeatId + " has no TutorialInspectView_NEW. " +
                           "The hint will appear and pressing B will do nothing visible.", this);
    }

    protected override void OnBeatTick(float dt)
    {
        if (_closed) return;
        if (!TutorialInput_NEW.InspectDown()) return;

        if (!_opened)
        {
            _opened = true;
            if (view != null) view.Open();
            onOpened.Invoke();
            return;
        }

        _closed = true;
        if (view != null) view.Close();
        onClosed.Invoke();
    }

    protected override void OnBeatExit()
    {
        // However this beat ended — the gate, F2, a jump, the attract restart — the
        // world does not get left frozen behind an enlarged readout.
        if (_opened && !_closed)
        {
            if (view != null) view.Close();
            onClosed.Invoke();
        }
    }

    protected override bool GateSatisfied()
    {
        if (_forced) return true;
        if (!_closed) return false;

        if (waitForClose && view != null && !view.IsSettled) return false;

        return true;
    }

    protected override void OnForceSatisfy()
    {
        _forced = true;
    }

    public override string GateStatus()
    {
        if (!_opened) return "waiting for inspect";
        if (!_closed) return "open — waiting for it to be closed";
        if (waitForClose && view != null && !view.IsSettled) return "closing";

        return "opened and closed";
    }
}

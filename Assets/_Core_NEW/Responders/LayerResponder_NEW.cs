using System.Collections;
using UnityEngine;

/// <summary>
/// Base class for anything that reacts to a layer transition.
///
/// It exists to remove three pieces of boilerplate that were copy-pasted into every
/// responder in the old codebase, and to make the anchor contract impossible to get wrong:
///
///   * subscribe / unsubscribe symmetry
///   * "the start layer never fires an event" compensation (LayerState_NEW.EmitInitialLayer
///     handles it now, so subclasses get startup for free)
///   * offset scheduling, including the pre-schedule needed for a negative LookUpStart offset
///
/// A subclass implements SelectChannel() to say which TimedChannel_NEW it listens on,
/// and Apply() to do its one job. It never decides *when* — CameraDirector_NEW owns time.
/// </summary>
public abstract class LayerResponder_NEW : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] protected CameraDirector_NEW director;

    [Header("Debug")]
    [SerializeField] protected bool debugLog = false;

    Coroutine _pending;

    protected virtual void Awake()
    {
        if (director == null) director = FindObjectOfType<CameraDirector_NEW>();
        if (director == null)
            Debug.LogError($"[{GetType().Name}] No CameraDirector_NEW found. This responder will never fire.", this);
    }

    protected virtual void OnEnable()
    {
        if (director != null) director.OnAnchor += HandleAnchor;
    }

    protected virtual void OnDisable()
    {
        if (director != null) director.OnAnchor -= HandleAnchor;
        CancelPending();
    }

    /// <summary>Which channel of the profile's schedule this responder rides on.</summary>
    protected abstract TimedChannel_NEW SelectChannel(LayerProfile_NEW profile);

    /// <summary>Do the work. Called on the scheduled frame.</summary>
    protected abstract void Apply(LayerProfile_NEW profile);

    void HandleAnchor(AnchorEvent_NEW e)
    {
        if (e.profile == null) return;

        TimedChannel_NEW ch = SelectChannel(e.profile);
        if (ch == null) return;

        if (ch.MatchesDirectly(e.anchor))
        {
            Schedule(ch.offset, e.profile);
            return;
        }

        // A negative offset on LookUpStart lands before that anchor is reached, so it has
        // to be queued the moment CoverReached fires — that is when the remaining time
        // becomes computable.
        if (ch.NeedsPreSchedule(e.anchor) && e.secondsUntilLookUp >= 0f)
            Schedule(Mathf.Max(0f, e.secondsUntilLookUp + ch.offset), e.profile);
    }

    void Schedule(float delay, LayerProfile_NEW profile)
    {
        CancelPending();

        if (delay <= 0f)
        {
            Fire(profile);
            return;
        }

        _pending = StartCoroutine(FireAfter(delay, profile));
    }

    IEnumerator FireAfter(float delay, LayerProfile_NEW profile)
    {
        yield return new WaitForSeconds(delay);
        _pending = null;
        Fire(profile);
    }

    void Fire(LayerProfile_NEW profile)
    {
        if (debugLog) Debug.Log($"[{GetType().Name}] Applying '{profile.layerId}'.", this);
        Apply(profile);
    }

    void CancelPending()
    {
        if (_pending == null) return;
        StopCoroutine(_pending);
        _pending = null;
    }
}

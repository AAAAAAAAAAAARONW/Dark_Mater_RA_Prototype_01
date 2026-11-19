using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The light's trail, all the way back to the quasar it came from: ONE trail.
///
/// The journey opens with the light already a long way out and its trail only as long as the
/// last few seconds of flight: a short tail joined to nothing. The tutorial ended with the
/// light leaving the quasar, so its trail should still run back to it.
///
/// FIRST TRY, AND WHY NOT. A second line was drawn from the quasar to the light with the
/// trail's material. Two ribbons, each turned to the camera on its own, each with its own
/// width and its own fade along its length: they never sat on each other, and it read as two
/// trails. So now there is only the trail itself, made long:
///
///   at the start   its points are laid from the middle of the quasar to the light, as if it
///                  had flown that way, and its lifetime is made long enough that none of them
///                  dies. The trail's own shader draws it, head at the light, far end fading
///                  at the quasar.
///   after the      (the first gate, and the dive out through it, over) the old points are
///   first world    drawn in towards the light, a little every frame, until the trail is its
///                  own length again; then its lifetime is put back. Nothing is cut off.
///
/// NOTHING TO WIRE. It adds itself, at run time, to a scene that has a PlayerRig_NEW with a
/// trail and a QuasarVFX_NEW.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("TETHER", "#BFD8FF")]
public class QuasarTether_NEW : MonoBehaviour
{
    [Header("Wiring (found if left empty)")]
    [SerializeField] TrailRenderer trail;
    [SerializeField] Transform quasar;
    [SerializeField] LayerState_NEW state;
    [SerializeField] LayerDive_NEW dive;

    [Tooltip("Seconds to draw the trail back in to its own length, once the first world is behind.")]
    [Min(0.5f)] [SerializeField] float drawInSeconds = 6f;

    [Tooltip("How many points the stretch from the quasar to the light is laid with.")]
    [Range(8, 256)] [SerializeField] int points = 96;

    const float LongTime = 100000f;

    float _ownTime, _ownLength, _drawIn;
    string _firstLayer;
    bool _ready, _leaving, _done;
    Vector3[] _buffer = new Vector3[0];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Install();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { Install(); }

    static void Install()
    {
        if (FindObjectOfType<QuasarTether_NEW>() != null) return;
        if (FindObjectOfType<PlayerRig_NEW>() == null || FindObjectOfType<QuasarVFX_NEW>() == null) return;
        new GameObject("Quasar Tether").AddComponent<QuasarTether_NEW>();
    }

    IEnumerator Start()
    {
        // A frame late: everything that sets the trail up at the start has done so.
        yield return null;

        PlayerRig_NEW player = FindObjectOfType<PlayerRig_NEW>();
        if (trail == null && player != null) trail = player.GetComponentInChildren<TrailRenderer>(true);
        if (quasar == null)
        {
            QuasarVFX_NEW vfx = FindObjectOfType<QuasarVFX_NEW>();
            if (vfx != null) quasar = vfx.transform;
        }
        if (state == null) state = FindObjectOfType<LayerState_NEW>();
        if (dive == null) dive = FindObjectOfType<LayerDive_NEW>();

        if (trail == null || quasar == null)
        {
            enabled = false;
            yield break;
        }

        Vector3 from = quasar.position, to = trail.transform.position;
        float speed = player != null ? Mathf.Max(0.1f, player.Speed) : 2.5f;
        _ownTime = trail.time;
        _ownLength = Mathf.Max(2f, _ownTime * speed);

        // As if it had flown here from the quasar: oldest point first.
        int n = Mathf.Max(8, points);
        var laid = new Vector3[n];
        for (int i = 0; i < n; i++) laid[i] = Vector3.Lerp(from, to, (float)i / n);
        trail.Clear();
        trail.time = LongTime;
        trail.AddPositions(laid);

        _ready = true;
    }

    void LateUpdate()
    {
        if (!_ready || _done || trail == null) return;

        // The first world the light is in is the quasar's. Once it is in another, and the dive
        // that took it there (which looks back at the quasar along this trail) is over, the
        // trail is drawn in.
        string layer = state != null ? state.CurrentLayerId : null;
        if (_firstLayer == null) _firstLayer = layer;
        else if (layer != null && layer != _firstLayer && (dive == null || !dive.IsActive)) _leaving = true;
        if (!_leaving) return;

        float before = _drawIn;
        _drawIn = Mathf.Min(1f, _drawIn + Time.deltaTime / drawInSeconds);

        // Every point further behind the light than the trail's own length is brought in
        // towards that length, by the same share of what is left each frame.
        float keep = before < 1f ? (1f - _drawIn) / (1f - before) : 0f;
        int count = trail.positionCount;
        if (_buffer.Length < count) _buffer = new Vector3[Mathf.NextPowerOfTwo(count)];
        count = trail.GetPositions(_buffer);
        Vector3 head = trail.transform.position;
        for (int i = 0; i < count; i++)
        {
            Vector3 back = _buffer[i] - head;
            float distance = back.magnitude;
            if (distance <= _ownLength) continue;
            trail.SetPosition(i, head + back * ((_ownLength + (distance - _ownLength) * keep) / distance));
        }

        if (_drawIn >= 1f)
        {
            trail.time = _ownTime;      // what was drawn in is all in one place, and goes unseen
            _done = true;
            enabled = false;
        }
    }

    void OnDisable()
    {
        // Stopped part-way (the scene closing, the object switched off): the trail its own again.
        if (_ready && !_done && trail != null) trail.time = _ownTime;
    }
}

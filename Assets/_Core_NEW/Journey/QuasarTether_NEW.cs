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
/// LATER: IT REACHES THE QUASAR (LARC review, 25 November: "the tail feels like it comes from
/// the wrong direction; it doesn't feel like the light has left the quasar"). The trail fades
/// by share of its length, 1 at the light to 0 at the far end, so stretched over three hundred
/// and sixty units it was a quarter as bright a hundred units back and gone well short of the
/// quasar: in perspective it stopped beside it. While the trail runs back to the quasar it is
/// drawn with a fade of its own (tetherFade: bright at the light, holding most of that the
/// whole way, out only at the quasar), handed to TrailFacing_NEW, which draws the trail in
/// the journey; the trail's own fade comes back as it is drawn in.
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

    [Tooltip("How bright the trail is along its length while it runs back to the quasar, by share " +
             "of its length (0 at the light, 1 at the quasar): x at the middle, y just short of the " +
             "quasar. Its own fade (1 to 0, evenly) would leave the far half unseen.")]
    [SerializeField] Vector2 tetherFade = new Vector2(0.75f, 0.55f);

    const float LongTime = 100000f;

    float _ownTime, _ownLength, _drawIn;
    string _firstLayer;
    bool _ready, _leaving, _done;
    TrailFacing_NEW _facing;
    Gradient _ownGradient, _tetherGradient;
    float _appliedOwn = -1f;
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

        _facing = trail.GetComponent<TrailFacing_NEW>();
        _ready = true;
    }

    /// <summary>
    /// The trail's fade while it runs back to the quasar: bright at the light, held at
    /// tetherFade the whole way, out only at the quasar. <paramref name="own"/>: 0 that, 1 the
    /// trail's own fade back again, for the draw-in. The trail's colours are kept.
    /// </summary>
    void ApplyTetherFade(float own)
    {
        if (_facing == null) return;
        if (_ownGradient == null)
        {
            _ownGradient = _facing.CurrentGradient;     // null until TrailFacing_NEW's Start has run
            if (_ownGradient == null) return;
        }
        if (_tetherGradient == null) _tetherGradient = new Gradient();

        // Only when it changes (every frame of the draw-in, once before it).
        float k = Mathf.Clamp01(own);
        if (Mathf.Approximately(k, _appliedOwn) && _facing.CurrentGradient == _tetherGradient) return;
        _appliedOwn = k;
        _tetherGradient.SetKeys(_ownGradient.colorKeys, new[]
        {
            new GradientAlphaKey(Mathf.Lerp(1f, _ownGradient.Evaluate(0f).a, k), 0f),
            new GradientAlphaKey(Mathf.Lerp(tetherFade.x, _ownGradient.Evaluate(0.5f).a, k), 0.5f),
            new GradientAlphaKey(Mathf.Lerp(tetherFade.y, _ownGradient.Evaluate(0.97f).a, k), 0.97f),
            new GradientAlphaKey(Mathf.Lerp(0f, _ownGradient.Evaluate(1f).a, k), 1f),
        });
        _facing.SetGradient(_tetherGradient);
    }

    void LateUpdate()
    {
        if (!_ready || _done || trail == null) return;

        ApplyTetherFade(_drawIn);

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
            if (_facing != null && _ownGradient != null) _facing.SetGradient(_ownGradient);
            _done = true;
            enabled = false;
        }
    }

    void OnDisable()
    {
        // Stopped part-way (the scene closing, the object switched off): the trail its own again.
        if (_ready && !_done && trail != null) trail.time = _ownTime;
        if (_ready && !_done && _facing != null && _ownGradient != null) _facing.SetGradient(_ownGradient);
    }
}

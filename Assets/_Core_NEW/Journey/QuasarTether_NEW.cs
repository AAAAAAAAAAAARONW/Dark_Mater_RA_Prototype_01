using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// The light's trail, all the way back to the quasar it came from.
///
/// The journey opens with the light already a long way out, and its trail only as long as the
/// last few seconds of flight: a short tail in empty space, joined to nothing. The tutorial
/// ended with the light leaving the quasar, so the trail should still run back to it. This
/// draws that: one line from the middle of the quasar to the light, with the trail's own
/// material and about its width, so it reads as the same trail, very long. It thins towards
/// the quasar, and fades out over the stretch nearest the light where the real trail is, so
/// the two are not drawn on top of each other.
///
/// It is there for the first world only. When the light leaves it (the first gate), the line
/// fades out over fadeSeconds: from then on the quasar is a point far behind.
///
/// NOTHING TO WIRE. It adds itself, at run time, to a scene that has a PlayerRig_NEW with a
/// trail and a QuasarVFX_NEW. To tune it, select "Quasar Tether" in Play mode; to keep values,
/// put this component on an object in the scene and it uses that one.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("TETHER", "#BFD8FF")]
public class QuasarTether_NEW : MonoBehaviour
{
    [Header("Wiring (found if left empty)")]
    [SerializeField] TrailRenderer trail;
    [SerializeField] Transform quasar;
    [SerializeField] LayerState_NEW state;

    [Header("Look")]
    [Tooltip("How bright the line is beside the trail it continues (the trail's own colour times this).")]
    [Range(0f, 1f)] [SerializeField] float brightness = 0.75f;

    [Tooltip("Its width where it meets the trail, as a fraction of the trail's own.")]
    [Range(0.05f, 2f)] [SerializeField] float width = 0.8f;

    [Tooltip("Its width at the quasar, as a fraction of its width at the trail: it thins with distance.")]
    [Range(0f, 1f)] [SerializeField] float farWidth = 0.3f;

    [Header("Coming and going")]
    [Tooltip("Seconds to come up when the journey opens.")]
    [Min(0f)] [SerializeField] float appearSeconds = 1.5f;

    [Tooltip("Seconds to go once the light has left its first world.")]
    [Min(0.05f)] [SerializeField] float fadeSeconds = 4f;

    const int Segments = 48;

    LineRenderer _line;
    Gradient _gradient;
    readonly GradientColorKey[] _colours = { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) };
    readonly GradientAlphaKey[] _alphas = new GradientAlphaKey[4];
    readonly Vector3[] _points = new Vector3[Segments + 1];
    string _firstLayer;
    float _fade;
    bool _leaving, _ready;

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
        // A frame late: the trail has made its own material by then, and that is the one to share.
        yield return null;

        if (trail == null)
        {
            PlayerRig_NEW player = FindObjectOfType<PlayerRig_NEW>();
            if (player != null) trail = player.GetComponentInChildren<TrailRenderer>(true);
        }
        if (quasar == null)
        {
            QuasarVFX_NEW vfx = FindObjectOfType<QuasarVFX_NEW>();
            if (vfx != null) quasar = vfx.transform;
        }
        if (state == null) state = FindObjectOfType<LayerState_NEW>();

        if (trail == null || quasar == null)
        {
            enabled = false;
            yield break;
        }

        gameObject.layer = trail.gameObject.layer;
        _line = gameObject.GetComponent<LineRenderer>();
        if (_line == null) _line = gameObject.AddComponent<LineRenderer>();
        _line.useWorldSpace = true;
        _line.positionCount = Segments + 1;
        _line.alignment = LineAlignment.View;
        _line.textureMode = trail.textureMode;
        _line.numCapVertices = 0;
        _line.numCornerVertices = 0;
        _line.shadowCastingMode = ShadowCastingMode.Off;
        _line.receiveShadows = false;
        _line.lightProbeUsage = LightProbeUsage.Off;
        _line.reflectionProbeUsage = ReflectionProbeUsage.Off;
        _line.sortingLayerID = trail.sortingLayerID;
        _line.sortingOrder = trail.sortingOrder - 1;
        _line.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, farWidth));
        _line.enabled = false;

        _gradient = new Gradient();
        _ready = true;
    }

    void LateUpdate()
    {
        if (!_ready || trail == null || quasar == null) return;

        // The first world the light is in is the quasar's; once it is in another, this goes.
        string layer = state != null ? state.CurrentLayerId : null;
        if (_firstLayer == null) _firstLayer = layer;
        else if (layer != null && layer != _firstLayer) _leaving = true;

        float target = _leaving || !trail.gameObject.activeInHierarchy ? 0f : 1f;
        float seconds = target > _fade ? appearSeconds : fadeSeconds;
        _fade = Mathf.MoveTowards(_fade, target, seconds > 0f ? Time.deltaTime / seconds : 1f);

        bool on = _fade > 0.001f;
        if (_line.enabled != on) _line.enabled = on;
        if (!on)
        {
            if (_leaving) enabled = false;      // its work is done for this scene
            return;
        }

        Vector3 from = quasar.position, to = trail.transform.position;
        float length = Vector3.Distance(from, to);
        if (length < 0.01f) { _line.enabled = false; return; }

        // From the light back to the quasar: the trail's shader has its head at the start of the
        // strip and fades its far end, so the line is laid the way a trail is.
        for (int i = 0; i <= Segments; i++) _points[i] = Vector3.Lerp(to, from, (float)i / Segments);
        _line.SetPositions(_points);

        if (_line.sharedMaterial != trail.sharedMaterial) _line.sharedMaterial = trail.sharedMaterial;
        _line.widthMultiplier = trail.widthMultiplier * trail.widthCurve.Evaluate(0.5f) * width;

        // Out of the quasar's own glare over the first few hundredths, even along the way, and
        // out again over the stretch the real trail covers (however long that is just now).
        float covered = 2f;
        int count = trail.positionCount;
        if (count > 0)
            covered = Mathf.Max(covered, Mathf.Max(Vector3.Distance(to, trail.GetPosition(0)),
                                                   Vector3.Distance(to, trail.GetPosition(count - 1))));
        float handover = Mathf.Clamp(covered / length, 0.02f, 0.9f);
        float a = brightness * _fade * _fade * (3f - 2f * _fade);

        _alphas[0] = new GradientAlphaKey(0f, 0f);
        _alphas[1] = new GradientAlphaKey(a, handover);
        _alphas[2] = new GradientAlphaKey(a, Mathf.Max(0.96f, handover + 0.01f));
        _alphas[3] = new GradientAlphaKey(0f, 1f);
        _gradient.SetKeys(_colours, _alphas);
        _line.colorGradient = _gradient;
    }
}

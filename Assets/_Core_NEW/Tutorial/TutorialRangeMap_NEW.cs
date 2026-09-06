using TMPro;
using UnityEngine;

/// <summary>
/// A corner map: how far the light still is from the quasar, and where it is along the
/// way.
///
/// The tutorial's opening is a long approach to a bright point that grows very slowly,
/// and a bright point that grows very slowly is indistinguishable from a bright point
/// that is not moving. The map is what turns "am I going anywhere?" into a question with
/// a visible answer — the same job the journey's own progress bar does for the main
/// experience, at the scale of one approach.
///
/// It is a track with a marker on it and a number beside it, not a top-down radar.
/// There is one axis of travel in this piece and nothing to navigate around; a map that
/// implied otherwise would be answering a question nobody has.
///
/// AFTER THE EMISSION the marker runs backwards and the distance grows, because that is
/// what is happening — the light is leaving. Nothing special-cases C3; the map reads the
/// same two positions it always did.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("MAP", "#6FA8DC")]
public class TutorialRangeMap_NEW : MonoBehaviour
{
    [Header("Wiring")]
    [Tooltip("Leave empty to find the travel in the scene. The map reads its position " +
             "and its destination, nothing else.")]
    [SerializeField] TutorialTravel_NEW travel;

    [Tooltip("The track the marker slides along. Its width is the whole journey.")]
    [SerializeField] RectTransform track;

    [Tooltip("The marker. Its anchored X is driven between the ends of the track.")]
    [SerializeField] RectTransform marker;

    [Tooltip("The distance readout.")]
    [SerializeField] TMP_Text distanceLabel;

    [Header("Readout")]
    [Tooltip("Scene units are meaningless to a visitor. Multiply by this before showing, " +
             "and put the real unit in the suffix, once the fiction settles what the " +
             "approach is supposed to be measured in.")]
    [SerializeField] float displayScale = 1f;

    [SerializeField] string unitSuffix = "";

    [Tooltip("Seconds between readout updates. A number that changes every frame is a " +
             "number nobody can read.")]
    [SerializeField] float refreshSeconds = 0.15f;

    [Header("Feel")]
    [Tooltip("Seconds of smoothing on the marker, so it glides rather than stepping with " +
             "the readout.")]
    [SerializeField] float markerSmoothTime = 0.2f;

    float _startDistance;
    float _fraction;
    float _fractionVelocity;
    float _sinceRefresh;
    bool _captured;

    /// <summary>0 at the start of the run, 1 at the quasar. Clamped.</summary>
    public float Fraction { get { return _fraction; } }

    void Awake()
    {
        if (travel == null) travel = FindObjectOfType<TutorialTravel_NEW>();

        if (travel == null)
            Debug.LogError("[TutorialRangeMap_NEW] No TutorialTravel_NEW. The map has " +
                           "nothing to measure.", this);
    }

    void OnEnable()
    {
        // Re-captured on enable as well as on the first frame, so the attract return —
        // which puts the light back at its start — gets a fresh baseline.
        _captured = false;
    }

    /// <summary>Take the start distance again. Called by the attract return.</summary>
    public void ResetMap()
    {
        _captured = false;
        _fraction = 0f;
        _fractionVelocity = 0f;
    }

    void Capture()
    {
        if (_captured || travel == null || travel.Destination == null) return;

        _startDistance = Vector3.Distance(travel.transform.position, travel.Destination.position);
        _captured = _startDistance > 0.01f;
    }

    void Update()
    {
        Capture();

        if (travel == null || travel.Destination == null || !_captured) return;

        float dt = Time.unscaledDeltaTime;
        float distance = Vector3.Distance(travel.transform.position, travel.Destination.position);

        float target = Mathf.Clamp01(1f - distance / _startDistance);

        _fraction = markerSmoothTime <= 0f
            ? target
            : Mathf.SmoothDamp(_fraction, target, ref _fractionVelocity, markerSmoothTime,
                               Mathf.Infinity, dt);

        PlaceMarker();

        _sinceRefresh += dt;
        if (_sinceRefresh < refreshSeconds) return;

        _sinceRefresh = 0f;
        WriteDistance(distance);
    }

    void PlaceMarker()
    {
        if (marker == null || track == null) return;

        float width = track.rect.width;

        Vector2 position = marker.anchoredPosition;
        position.x = Mathf.Lerp(-width * 0.5f, width * 0.5f, _fraction);
        marker.anchoredPosition = position;
    }

    void WriteDistance(float distance)
    {
        if (distanceLabel == null) return;

        float shown = distance * displayScale;

        // Thin space between the number and the unit, and no decimals: a readout that
        // twitches in its last digit reads as unstable rather than as precise.
        distanceLabel.text = shown.ToString("N0") + unitSuffix;
    }
}

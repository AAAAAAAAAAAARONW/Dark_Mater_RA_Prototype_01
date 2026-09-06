using TMPro;
using UnityEngine;

/// <summary>
/// A corner minimap: where the light is, which way it is facing, and where the quasar
/// is relative to it.
///
/// The first version of this was a progress track — a line with a marker sliding along
/// it. That is a timeline, not a map: it can only answer "how far through am I", and it
/// cannot answer the question the player actually acquires the moment B3 asks them to
/// turn around, which is "where is everything now that I am not facing forward".
///
/// THE FRAME IS THE WORLD, NOT THE PLAYER. The second version pinned the light to the
/// centre and let the quasar slide towards it, which shows the quasar approaching the
/// player — the opposite of what is happening and the opposite of what the piece is
/// about. The quasar is fixed in space; the light is the thing that moves. So the map
/// holds a fixed frame containing the start and the quasar, the quasar sits still in it,
/// and the player's marker travels across it.
///
/// Plan view — the XZ plane — because the travel is horizontal and the piece has no
/// vertical structure to lose. B2 looks up at the jet, and the map having nothing to say
/// about that is correct: it is a map of where you are, not of where you are pointing.
///
/// North up, with a stem on the player's marker showing where they are looking. Turn the
/// view and the stem swings; turn all the way round at B3 and it points back down the
/// map at where you came from. That correspondence is the whole point of a map, and it
/// is worth more here than a percentage.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("MAP", "#6FA8DC")]
public class TutorialRangeMap_NEW : MonoBehaviour
{
    [Header("Wiring")]
    [Tooltip("Leave empty to find the travel in the scene. The map reads its position " +
             "and its destination, nothing else.")]
    [SerializeField] TutorialTravel_NEW travel;

    [Tooltip("Leave empty to find the rig. Only its yaw is used, for the facing wedge.")]
    [SerializeField] FirstPersonLookRig_NEW lookRig;

    [Tooltip("The round area the map is drawn in. Its width sets the map's radius.")]
    [SerializeField] RectTransform frame;

    [Tooltip("The player's marker. It is the thing that moves — see the class summary.")]
    [SerializeField] RectTransform playerMarker;

    [Tooltip("Rotates to show where the light is looking. Parent it to the player marker.")]
    [SerializeField] RectTransform facing;

    [Tooltip("The quasar's marker. Fixed, because the quasar is.")]
    [SerializeField] RectTransform destinationMarker;

    [Tooltip("The distance readout.")]
    [SerializeField] TMP_Text distanceLabel;

    [Header("Frame")]
    [Tooltip("Fraction of the frame's radius the run occupies, measured on the first " +
             "frame from the start position to the quasar. Below 1 leaves margin, so the " +
             "two markers are not sitting on the rim.")]
    [Range(0.3f, 1f)]
    [SerializeField] float journeyFitsIn = 0.78f;

    [Header("Readout")]
    [Tooltip("Scene units are meaningless to a visitor. Multiply by this before showing, " +
             "and put the real unit in the suffix, once the fiction settles what this " +
             "approach is measured in.")]
    [SerializeField] float displayScale = 1f;

    [SerializeField] string unitSuffix = "";

    [Tooltip("Seconds between readout updates. A number that changes every frame is a " +
             "number nobody can read.")]
    [SerializeField] float refreshSeconds = 0.15f;

    [Header("Feel")]
    [Tooltip("Seconds of smoothing on the marker and the wedge.")]
    [SerializeField] float smoothTime = 0.12f;

    // The fixed frame: where the map is centred in the world, and how many world units
    // fit in a map pixel. Captured once, from the run's own geometry.
    Vector3 _centre;
    float _unitsPerPixel;
    bool _framed;

    Vector2 _markerPosition;
    Vector2 _markerVelocity;

    float _facingAngle;
    float _facingVelocity;
    float _sinceRefresh;

    void Awake()
    {
        if (travel == null) travel = FindObjectOfType<TutorialTravel_NEW>();
        if (lookRig == null) lookRig = FindObjectOfType<FirstPersonLookRig_NEW>();

        if (travel == null)
            Debug.LogError("[TutorialRangeMap_NEW] No TutorialTravel_NEW. The map has " +
                           "nothing to place.", this);
    }

    /// <summary>Snap everything back to the start state. The attract return calls this.</summary>
    public void ResetMap()
    {
        _framed = false;
        _markerPosition = Vector2.zero;
        _markerVelocity = Vector2.zero;
        _facingAngle = 0f;
        _facingVelocity = 0f;
    }

    /// <summary>
    /// Fix the frame from the run's own geometry: centred between where the light starts
    /// and where the quasar is, scaled so that span fills most of the circle.
    ///
    /// Done once. A frame that rescaled as the player moved would be a zoom, and a map
    /// that zooms while you cross it tells you nothing about how far you have come.
    /// </summary>
    void Frame()
    {
        if (_framed || travel == null || travel.Destination == null || frame == null) return;

        Vector3 start = travel.transform.position;
        Vector3 target = travel.Destination.position;

        _centre = (start + target) * 0.5f;

        float halfSpan = Vector3.Distance(new Vector3(start.x, 0f, start.z),
                                          new Vector3(target.x, 0f, target.z)) * 0.5f;

        float radius = frame.rect.width * 0.5f * journeyFitsIn;
        if (radius <= 1f || halfSpan <= 0.01f) return;

        _unitsPerPixel = halfSpan / radius;
        _framed = true;
    }

    void Update()
    {
        Frame();

        if (!_framed || travel == null || travel.Destination == null) return;

        float dt = Time.unscaledDeltaTime;

        PlaceMarkers(dt);
        PointTheStem(dt);

        _sinceRefresh += dt;
        if (_sinceRefresh < refreshSeconds) return;

        _sinceRefresh = 0f;
        WriteDistance(Vector3.Distance(travel.transform.position, travel.Destination.position));
    }

    void PlaceMarkers(float dt)
    {
        // Plan view: world X is map X, world Z is map Y. North up, so the map does not
        // spin under the player — the stem is what moves when they turn.
        Vector2 playerOnMap = ToMap(travel.transform.position);

        _markerPosition = smoothTime <= 0f
            ? playerOnMap
            : Vector2.SmoothDamp(_markerPosition, playerOnMap, ref _markerVelocity, smoothTime,
                                 Mathf.Infinity, dt);

        if (playerMarker != null) playerMarker.anchoredPosition = _markerPosition;

        // The quasar does not move, so this is the same value every frame. It is written
        // anyway rather than cached, so that dragging the quasar in the Scene view while
        // playing shows up on the map.
        if (destinationMarker != null)
            destinationMarker.anchoredPosition = ToMap(travel.Destination.position);
    }

    Vector2 ToMap(Vector3 world)
    {
        Vector3 offset = world - _centre;
        return new Vector2(offset.x, offset.z) / Mathf.Max(0.01f, _unitsPerPixel);
    }

    void PointTheStem(float dt)
    {
        if (facing == null || lookRig == null) return;

        // A yaw of 0 looks down world +Z, which on this map is straight up, and UI
        // rotation runs the other way round from compass bearing — hence the negation.
        float target = -lookRig.Yaw;

        _facingAngle = smoothTime <= 0f
            ? target
            : Mathf.SmoothDampAngle(_facingAngle, target, ref _facingVelocity, smoothTime,
                                    Mathf.Infinity, dt);

        facing.localRotation = Quaternion.Euler(0f, 0f, _facingAngle);
    }

    void WriteDistance(float distance)
    {
        if (distanceLabel == null) return;

        float shown = distance * displayScale;

        // No decimals: a readout that twitches in its last digit reads as unstable
        // rather than as precise.
        distanceLabel.text = shown.ToString("N0") + unitSuffix;
    }
}

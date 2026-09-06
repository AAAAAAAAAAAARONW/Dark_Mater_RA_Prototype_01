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
/// North up, with an arrowhead on the player's marker showing where they are looking.
/// Turn the view and the arrow swings; turn all the way round at B3 and it points back
/// down the map at where you came from. That correspondence is the whole point of a map,
/// and it is worth more here than a percentage — which also means an arrow that is 180
/// degrees out is worse than no arrow at all. See PointTheStem for how that is kept honest.
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

    [Tooltip("The box the markers are positioned inside. Its shorter side sets how much " +
             "world fits on the map, so the map is told its size once instead of twice.")]
    [SerializeField] RectTransform frame;

    [Tooltip("The player's marker. It is the thing that moves — see the class summary.")]
    [SerializeField] RectTransform playerMarker;

    [Tooltip("Rotates to show where the light is looking. Parent it to the player marker; " +
             "it turns around its own centre, so that centre has to be the marker.")]
    [SerializeField] RectTransform facing;

    [Tooltip("The arrowhead drawn inside Facing. Left empty, the first child is used.\n\n" +
             "Its position is not yours to set — PlaceArrow parks it above the marker on " +
             "every Awake, because where it sits relative to the point it rotates around " +
             "IS which way it means. Size, sprite and colour are yours.")]
    [SerializeField] RectTransform arrow;

    [Tooltip("Pixels between the centre of the dot and the base of the arrow. Negative " +
             "overlaps them, which reads as one glyph rather than two.")]
    [SerializeField] float arrowGap = 3f;

    [Tooltip("The quasar's marker. Fixed, because the quasar is.")]
    [SerializeField] RectTransform destinationMarker;

    [Tooltip("The distance readout.")]
    [SerializeField] TMP_Text distanceLabel;

    [Header("Frame")]
    [Tooltip("Fraction of the frame's half-height the run occupies, measured on the first " +
             "frame from the start position to the quasar. Below 1 leaves margin, so the " +
             "two markers are not sitting on the edge.")]
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
    bool _checked;

    void Awake()
    {
        if (travel == null) travel = FindObjectOfType<TutorialTravel_NEW>();
        if (lookRig == null) lookRig = FindObjectOfType<FirstPersonLookRig_NEW>();

        PlaceArrow();

        if (travel == null)
            Debug.LogError("[TutorialRangeMap_NEW] No TutorialTravel_NEW. The map has " +
                           "nothing to place.", this);
    }

    /// <summary>
    /// Park the arrow directly above the point it rotates around.
    ///
    /// This is enforced rather than authored, and it is the one piece of layout in the
    /// tutorial that is. PointTheStem turns the Facing rect to a bearing measured from
    /// map +Y, so an arrow drawn anywhere but straight up above the marker points at the
    /// wrong part of the map — and the first version was drawn BELOW the marker, which is
    /// 180 degrees out: it showed where the player had come from and swung the wrong way
    /// when they turned.
    ///
    /// Two conventions that have to agree is one convention too many, so the component
    /// owns it. Public because the builder calls it too, so the map looks right in the
    /// Scene view and not only once you press Play.
    ///
    /// Size, sprite and colour are untouched.
    /// </summary>
    public void PlaceArrow()
    {
        if (arrow == null && facing != null && facing.childCount > 0)
            arrow = facing.GetChild(0) as RectTransform;

        if (arrow == null) return;

        Vector2 centre = new Vector2(0.5f, 0.5f);

        arrow.anchorMin = centre;
        arrow.anchorMax = centre;
        arrow.pivot = centre;
        arrow.anchoredPosition = new Vector2(0f, arrow.sizeDelta.y * 0.5f + arrowGap);
    }

    /// <summary>Snap everything back to the start state. The attract return calls this.</summary>
    public void ResetMap()
    {
        _framed = false;
        _markerPosition = Vector2.zero;
        _markerVelocity = Vector2.zero;
        _facingAngle = 0f;
        _facingVelocity = 0f;
        _checked = false;
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

        // The shorter side, not the width. The run is vertical on a north-up map — the
        // light travels along world Z — so on a landscape frame it is the height that
        // decides what fits, and using the width would push both markers off the top and
        // bottom edges.
        float reach = Mathf.Min(frame.rect.width, frame.rect.height) * 0.5f * journeyFitsIn;
        if (reach <= 1f || halfSpan <= 0.01f) return;

        _unitsPerPixel = halfSpan / reach;
        _framed = true;
    }

    void Update()
    {
        Frame();

        if (!_framed || travel == null || travel.Destination == null) return;

        float dt = Time.unscaledDeltaTime;

        PlaceMarkers(dt);
        PointTheStem(dt);
        CheckTheArrowAgrees();

        _sinceRefresh += dt;
        if (_sinceRefresh < refreshSeconds) return;

        _sinceRefresh = 0f;
        WriteDistance(Vector3.Distance(travel.transform.position, travel.Destination.position));
    }

    void PlaceMarkers(float dt)
    {
        // Plan view: world X is map X, world Z is map Y. North up, so the map does not
        // spin under the player — the arrow is what moves when they turn.
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

    /// <summary>
    /// Turn the stem to where the light is looking.
    ///
    /// The angle comes from the camera's own forward vector put through THE SAME
    /// PROJECTION as the markers — world X to map X, world Z to map Y. The earlier
    /// version worked from lookRig.Yaw and a negation, which is two conventions that have
    /// to agree with each other and with ToMap, and they did not: the stem pointed back
    /// down the map at where the player had come from. Reading the direction off the
    /// transform means the stem cannot disagree with the markers, because both are built
    /// out of the same two numbers.
    ///
    /// The arrow rests pointing along map +Y, which PlaceArrow guarantees. Anything else
    /// drawn inside that rect wants to point the same way.
    /// </summary>
    void PointTheStem(float dt)
    {
        if (facing == null || lookRig == null) return;

        Vector3 forward = lookRig.transform.forward;
        Vector2 onMap = new Vector2(forward.x, forward.z);

        // Looking exactly straight up or down. Nothing on a plan view to say about that,
        // so hold the last bearing rather than snapping to an arbitrary one.
        if (onMap.sqrMagnitude > 0.000001f)
        {
            // Atan2 is degrees counter-clockwise from map +X; the stem rests at +Y,
            // which is a quarter turn further round.
            float target = Mathf.Atan2(onMap.y, onMap.x) * Mathf.Rad2Deg - 90f;

            _facingAngle = smoothTime <= 0f
                ? target
                : Mathf.SmoothDampAngle(_facingAngle, target, ref _facingVelocity, smoothTime,
                                        Mathf.Infinity, dt);
        }

        facing.localRotation = Quaternion.Euler(0f, 0f, _facingAngle);
    }

    /// <summary>
    /// Once, at the start: does the arrow point the same way the marker is about to
    /// travel?
    ///
    /// It has to. Before the player turns, the light is looking along its own course, so
    /// "which way am I facing" and "which way am I going" are the same arrow on the map.
    /// Any sign error, any arrow drawn on the wrong side of its pivot, any disagreement
    /// between this and ToMap comes out as a dot product below zero — and this map has
    /// been 180 degrees out twice, both times in a way that looked fine until somebody
    /// watched it during a turn.
    ///
    /// Only run while the view is still near forward, because after that the two arrows
    /// are supposed to differ.
    /// </summary>
    void CheckTheArrowAgrees()
    {
        if (_checked || facing == null || arrow == null || lookRig == null) return;
        if (Mathf.Abs(lookRig.YawFromForward) > 5f) return;

        _checked = true;

        Vector2 course = ToMap(travel.Destination.position) - ToMap(travel.transform.position);
        if (course.sqrMagnitude < 0.01f) return;

        // Where the arrow actually is on screen, not where it is meant to be: its offset
        // inside Facing, turned by whatever Facing is turned to.
        Vector3 offset = new Vector3(arrow.anchoredPosition.x, arrow.anchoredPosition.y, 0f);
        Vector3 drawn = facing.localRotation * offset;

        if (Vector2.Dot(new Vector2(drawn.x, drawn.y).normalized, course.normalized) > 0f) return;

        Debug.LogError("[TutorialRangeMap_NEW] The facing arrow points away from the " +
                       "direction of travel while the light is still looking forward, so " +
                       "the map is telling the player the opposite of the truth. Check that " +
                       "the arrow sits ABOVE the marker (PlaceArrow) and that PointTheStem " +
                       "and ToMap use the same two world axes.", this);
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

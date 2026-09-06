using TMPro;
using UnityEngine.UI;
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
/// So this is a plan view. North up, the light at the centre, a wedge showing where it
/// is looking, and the quasar placed by its real offset. Turn the view and the wedge
/// swings; turn all the way round at B3 and the quasar is behind you on the map as well
/// as on the screen. That correspondence is the whole point of a map, and it is worth
/// more here than a percentage.
///
/// Plan view — the XZ plane — because the travel is horizontal and the piece has no
/// vertical structure to lose. B2 looks up at the jet, and the map having nothing to say
/// about that is correct: it is a map of where you are, not of where you are pointing.
///
/// The quasar is clamped to the edge of the frame when it is further away than the map
/// reaches, which is most of the approach. That is the normal minimap convention and it
/// is what makes the marker's arrival at the frame edge mean something.
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

    [Tooltip("Rotates to show where the light is looking. Sits at the centre.")]
    [SerializeField] RectTransform facing;

    [Tooltip("The quasar's marker, placed by its real offset and clamped to the frame.")]
    [SerializeField] RectTransform destinationMarker;

    [Tooltip("The distance readout.")]
    [SerializeField] TMP_Text distanceLabel;

    [Header("Scale")]
    [Tooltip("World units per map pixel. Larger shows more space in the same circle.\n\n" +
             "At 20 a 90 pixel radius reaches 1800 units, so the quasar sits pinned to " +
             "the edge for the first part of the approach and then comes inside — which " +
             "is the point at which the marker starts meaning something.")]
    [SerializeField] float unitsPerPixel = 20f;

    [Tooltip("Alpha of the destination marker while it is off the edge of the map, so " +
             "'pinned to the rim' and 'actually there' do not look the same.")]
    [Range(0.1f, 1f)]
    [SerializeField] float clampedMarkerAlpha = 0.45f;

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
        _markerPosition = Vector2.zero;
        _markerVelocity = Vector2.zero;
        _facingAngle = 0f;
        _facingVelocity = 0f;
    }

    void Update()
    {
        if (travel == null || travel.Destination == null) return;

        float dt = Time.unscaledDeltaTime;

        PlaceDestination(dt);
        PointTheWedge(dt);

        _sinceRefresh += dt;
        if (_sinceRefresh < refreshSeconds) return;

        _sinceRefresh = 0f;
        WriteDistance(Vector3.Distance(travel.transform.position, travel.Destination.position));
    }

    void PlaceDestination(float dt)
    {
        if (destinationMarker == null || frame == null) return;

        // Plan view: world X is map X, world Z is map Y. North up, so the map does not
        // spin under the player — the wedge is what moves when they turn.
        Vector3 offset = travel.Destination.position - travel.transform.position;
        Vector2 onMap = new Vector2(offset.x, offset.z) / Mathf.Max(0.01f, unitsPerPixel);

        float radius = frame.rect.width * 0.5f;
        bool clamped = onMap.magnitude > radius;

        if (clamped) onMap = onMap.normalized * radius;

        _markerPosition = smoothTime <= 0f
            ? onMap
            : Vector2.SmoothDamp(_markerPosition, onMap, ref _markerVelocity, smoothTime,
                                 Mathf.Infinity, dt);

        destinationMarker.anchoredPosition = _markerPosition;

        Graphic marker = destinationMarker.GetComponent<Graphic>();

        if (marker != null)
        {
            Color c = marker.color;
            c.a = clamped ? clampedMarkerAlpha : 1f;
            marker.color = c;
        }
    }

    void PointTheWedge(float dt)
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

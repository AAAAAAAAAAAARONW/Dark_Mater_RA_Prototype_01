using UnityEngine;

/// <summary>
/// The light ends its journey IN the Earth: across the Solar System it bends its course onto
/// the Earth, and the gate into the Earth layer sits just past it, so the switch to the
/// ending happens the moment the light goes in.
///
/// WHY IT IS NEEDED. The light flies straight along +Z. The Solar System's Earth is tiny
/// (about 0.12 units across in the world) and, since the system was tilted so its orbits
/// read as orbits, sits a few units off that line. And the Earth gate was a flat plane at
/// z 743.6, a few units BEFORE the Earth, so the light switched to the ending without ever
/// reaching it. Nothing in the picture said "it arrives here".
///
/// HOW.
///   Steering: in steerLayerId, every frame the course turns towards the Earth's actual
///     position (so it stays right if the system is moved or tilted again), smoothly at
///     turnRate. Within lockDistance it aims straight at the centre, so a target this small
///     is not missed by a hair.
///   Gate: on Awake the Earth gate is moved along Z to gateBeyondEarth past the Earth's
///     centre. Awake, because UniverseJourneyTracker_NEW reads every gate's Z in its Start
///     for the distance readout; moved here, it reads the new one. Only Z changes: the gate
///     is a wide plane, so it is still crossed wherever the course goes.
///
/// Uses PlayerRig_NEW.SetMovementDirection; DarkMatterBend_NEW treats an external direction
/// as its new baseline when it is not bending, so the two do not fight. The Solar System has
/// no dark matter in it.
/// </summary>
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
[HierarchyBadge_NEW("EARTH APPROACH", "#4A94F2")]
public class EarthApproach_NEW : MonoBehaviour
{
    [Header("Wiring (found if empty)")]
    [Tooltip("The Earth the light flies into: the one inside the SolarSystem group.")]
    [SerializeField] Transform earth;

    [Tooltip("The gate into the Earth layer.")]
    [SerializeField] LayerGate_NEW earthGate;

    [SerializeField] PlayerRig_NEW player;
    [SerializeField] LayerState_NEW layerState;

    [Header("Steering")]
    [Tooltip("The layer in which the course bends onto the Earth.")]
    [SerializeField] string steerLayerId = "SolarSystem";

    [Tooltip("How fast the course turns towards the Earth, per second. Higher = a tighter, " +
             "more visible turn; lower = a gentle drift.")]
    [Min(0.05f)]
    [SerializeField] float turnRate = 1.2f;

    [Tooltip("Closer than this, the course points straight at the Earth's centre.")]
    [Min(0.1f)]
    [SerializeField] float lockDistance = 6f;

    [Header("Gate")]
    [SerializeField] bool moveGate = true;

    [Tooltip("How far past the Earth's centre, along Z, the gate sits. Small: the ending starts " +
             "as the light goes in.")]
    [SerializeField] float gateBeyondEarth = 0.25f;

    [SerializeField] bool debugLog = false;

    void Awake()
    {
        if (player == null) player = FindObjectOfType<PlayerRig_NEW>();
        if (layerState == null) layerState = FindObjectOfType<LayerState_NEW>();
        if (earth == null) earth = FindEarth();
        if (earthGate == null) earthGate = FindGate("Earth");

        if (earth == null)
        {
            Debug.LogWarning("[EarthApproach_NEW] No Earth found. The light flies straight.", this);
            enabled = false;
            return;
        }

        if (moveGate && earthGate != null)
        {
            Vector3 p = earthGate.transform.position;
            float z = earth.position.z + gateBeyondEarth;

            if (debugLog) Debug.Log("[EarthApproach_NEW] Earth gate z " + p.z.ToString("0.00") + " -> " + z.ToString("0.00") + ".", this);

            earthGate.transform.position = new Vector3(p.x, p.y, z);
        }
    }

    void Update()
    {
        if (player == null || earth == null || layerState == null) return;
        if (!string.Equals(layerState.CurrentLayerId, steerLayerId, System.StringComparison.OrdinalIgnoreCase)) return;

        Vector3 to = earth.position - player.transform.position;
        if (to.z <= 0f) return;    // past it: the gate has the rest

        Vector3 want = to.normalized;
        Vector3 now = player.MovementDirection;

        Vector3 dir = to.magnitude <= lockDistance
            ? want
            : Vector3.Slerp(now, want, 1f - Mathf.Exp(-turnRate * Time.deltaTime));

        player.SetMovementDirection(dir);
    }

    static Transform FindEarth()
    {
        // The Earth that is a planet of the SolarSystem group, not any other object of that name.
        GameObject system = GameObject.Find("SolarSystem");
        if (system != null)
            foreach (Transform t in system.GetComponentsInChildren<Transform>(true))
                if (t.name == "Earth") return t;

        GameObject any = GameObject.Find("Earth");
        return any != null ? any.transform : null;
    }

    static LayerGate_NEW FindGate(string layerId)
    {
        foreach (LayerGate_NEW g in FindObjectsOfType<LayerGate_NEW>())
            if (g.LayerId == layerId) return g;
        return null;
    }

    void OnDrawGizmos()
    {
        if (!DebugView_NEW.Gizmos || earth == null) return;

        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.8f);
        Gizmos.DrawWireSphere(earth.position, lockDistance);
    }
}

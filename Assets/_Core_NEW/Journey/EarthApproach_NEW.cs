using UnityEngine;

/// <summary>
/// The light ends its journey IN the Earth: the gate into the Earth layer sits just past
/// Earth's centre, so the switch to the ending happens the moment the light goes in.
///
/// THE LIGHT NEVER TURNS. It used to: in the Solar System layer this bent the light's course
/// onto Earth every frame, and aimed it straight at Earth's centre over the last few units.
/// So whenever Earth was off the line — the Solar System tilted, as it once was, by 15
/// degrees, leaving Earth 1.5 above the light — the light visibly turned. Light only goes
/// straight on. SolarSystemPath_NEW puts Earth on the light's line instead (in the editor,
/// and in play while the system is out of sight), so there is nothing here to steer.
///
/// Gate: on Awake the Earth gate is moved along Z to gateBeyondEarth past the Earth's
/// centre. Awake, because UniverseJourneyTracker_NEW reads every gate's Z in its Start for
/// the distance readout; moved here, it reads the new one. Only Z changes: the gate is a
/// wide plane, so it is still crossed wherever Earth sits across it.
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

    [Header("Gate")]
    [SerializeField] bool moveGate = true;

    [Tooltip("How far past the Earth's centre, along Z, the gate sits. Small: the ending starts " +
             "as the light goes in.")]
    [SerializeField] float gateBeyondEarth = 0.25f;

    [SerializeField] bool debugLog = false;

    void Awake()
    {
        if (earth == null) earth = FindEarth();
        if (earthGate == null) earthGate = FindGate("Earth");

        if (earth == null)
        {
            Debug.LogWarning("[EarthApproach_NEW] No Earth found; the Earth gate is left where it is.", this);
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
        Gizmos.DrawWireSphere(earth.position, 1f);
    }
}

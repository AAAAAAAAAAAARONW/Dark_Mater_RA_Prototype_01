using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Lays the Solar System out along the photon's path. The light only ever goes straight on,
/// so it is the Solar System that is placed: the light ends dead centre on Earth, and on the
/// way in it passes the other planets one by one instead of flying straight to Earth.
///
/// THE PATH. Earth's centre is on the photon's line, earthPastGate beyond the gate into the
/// Earth layer — as far as it was authored, so the arrival there keeps its timing. The Sun is
/// off to one side of the line by sunAside of Earth's orbit, and short of Earth along it, so
/// the line comes in from outside the system, crosses the orbits of the outer planets, Mars
/// and Venus on the way in, sweeps past the Sun and Mercury, and goes out through Earth's
/// orbit just where Earth is. So the light reaches Earth from the Sun's side, and Earth ahead
/// is seen mostly lit rather than as a dark disc against the Sun.
///
/// Each other planet is put where the line crosses its orbit, moved round the orbit until the
/// line passes passClearance clear of it (beyond two and a half of its radii, or its rings),
/// on alternate sides, outermost first: the photon flies past each in turn. A planet whose
/// orbit the line never reaches waits at the point of its orbit nearest the line. Every orbit
/// keeps its authored radius, and the bodies their sizes, turns and moons. The orbits lie in
/// the plane through the line square to the Solar System's up.
///
/// WHEN. In the editor, from this component's menu (Lay Out Along the Photon's Path): from
/// where the photon starts and the way it flies; save the scene to keep it. Laying it out
/// again changes nothing unless the photon, the gate or the settings have.
///
/// In play a dark matter bend turns the photon back to its heading afterwards but can leave
/// it on a line beside the first. So while the Solar System is out of sight and no dive is
/// running, the whole system is slid across to keep Earth on the line the photon is really
/// on. From the moment the dive into it begins, it stays where it is.
///
/// Put it on the Solar System's group root, the Sun's and the planets' parent.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("SOLAR PATH", "#F2C14E")]
public class SolarSystemPath_NEW : MonoBehaviour
{
    [Header("What")]
    [Tooltip("The photon. Found if empty.")]
    [SerializeField] PlayerRig_NEW photon;

    [Tooltip("The Sun. The child named 'Sun' if empty.")]
    [SerializeField] Transform sun;

    [Tooltip("Where the light ends. The child named 'Earth' if empty.")]
    [SerializeField] Transform earth;

    [Tooltip("The layer of the gate the photon crosses just before Earth.")]
    [SerializeField] string earthLayerId = "Earth";

    [Tooltip("This system's render group, in WorldSwitcher_NEW.")]
    [SerializeField] string layerId = "SolarSystem";

    [Header("Layout")]
    [Tooltip("Earth's centre, this far past the middle of the Earth gate, along the line.")]
    [Min(0f)] [SerializeField] float earthPastGate = 1.86f;

    [Tooltip("How far to the side of the line the Sun is, as a fraction of Earth's orbit. Higher: " +
             "the photon passes the Sun further off and comes to Earth from more to its side, so " +
             "less of Earth is lit; lower: closer past the Sun, Earth fuller. Over about 0.88 " +
             "the line misses Venus's orbit.")]
    [Range(0.3f, 0.95f)] [SerializeField] float sunAside = 0.8f;

    [Tooltip("How far clear of each planet the line passes, beyond two and a half of its radii.")]
    [Min(0f)] [SerializeField] float passClearance = 0.6f;

    [Header("In play")]
    [Tooltip("While the system is out of sight, keep Earth on the line the photon is really on.")]
    [SerializeField] bool followPhoton = true;

    [Tooltip("The way the photon flies, as laid out. Set by the menu.")]
    [SerializeField] Vector3 pathDirection = Vector3.forward;

    WorldSwitcher_NEW _worlds;
    LayerDive_NEW _dive;
    bool _settled;

    void Awake()
    {
        Find();
        _worlds = FindObjectOfType<WorldSwitcher_NEW>();
        _dive = FindObjectOfType<LayerDive_NEW>();
    }

    void LateUpdate()
    {
        if (!followPhoton || _settled || photon == null || earth == null) return;

        // Once it shows, or a dive has it, it is where it is for good.
        if ((_dive != null && _dive.IsActive) || (_worlds != null && _worlds.GroupAlpha(layerId) > 0f))
        {
            _settled = true;
            return;
        }

        Vector3 d = pathDirection.sqrMagnitude > 1e-8f ? pathDirection.normalized : Vector3.forward;
        Vector3 off = earth.position - photon.transform.position;
        Vector3 aside = off - d * Vector3.Dot(off, d);
        if (aside.sqrMagnitude > 1e-8f) transform.position -= aside;
    }

    bool Find()
    {
        if (photon == null) photon = FindObjectOfType<PlayerRig_NEW>();
        if (sun == null) sun = transform.Find("Sun");
        if (earth == null) earth = transform.Find("Earth");
        return photon != null && sun != null && earth != null;
    }

    static LayerGate_NEW FindGate(string layer)
    {
        foreach (LayerGate_NEW gate in FindObjectsOfType<LayerGate_NEW>())
            if (gate.LayerId == layer) return gate;
        return null;
    }

    /// <summary>
    /// Places the Sun (with this root on it, as it is authored) and the planets along the line
    /// from <paramref name="origin"/> in <paramref name="direction"/>. Returns what it did, or
    /// null if something it needs is missing.
    /// </summary>
    string LayOut(Vector3 origin, Vector3 direction, LayerGate_NEW gate)
    {
        Vector3 d = direction.normalized;
        Vector3 up = Vector3.ProjectOnPlane(transform.up, d);
        up = up.sqrMagnitude > 1e-6f ? up.normalized : Vector3.up;
        Vector3 side = Vector3.Cross(up, d).normalized;

        // The orbits as authored: each body's distance from the Sun, in the ecliptic.
        Vector3 sunWas = sun.position;
        float earthOrbit = Vector3.ProjectOnPlane(earth.position - sunWas, up).magnitude;
        if (earthOrbit < 1e-4f) return null;

        // The Sun stays on the side of the line it was on.
        float towardsSun = Vector3.Dot(sunWas - origin, side) < 0f ? -1f : 1f;

        // Where the line meets the Earth gate's plane, and Earth past it.
        Collider gateCollider = gate.GetComponent<Collider>();
        Vector3 gateAt = gateCollider != null ? gateCollider.bounds.center : gate.transform.position;
        Vector3 normal = gate.transform.forward;
        float facing = Vector3.Dot(d, normal);
        float sGate = Mathf.Abs(facing) > 1e-4f ? Vector3.Dot(gateAt - origin, normal) / facing
                                                : Vector3.Dot(gateAt - origin, d);
        float sEarth = sGate + earthPastGate;

        // The Sun to the side, and short of Earth: the line leaves Earth's orbit at Earth.
        float b = sunAside * earthOrbit;
        float sSun = sEarth - Mathf.Sqrt(Mathf.Max(0f, earthOrbit * earthOrbit - b * b));

        // A point s along the line and y across it, y measured towards the Sun.
        Vector3 At(float s, float y) => origin + d * s + side * (towardsSun * y);

        // The planets: the root's other children that CelestialBody_NEW draws, outermost first.
        var planets = new List<KeyValuePair<float, Transform>>();
        foreach (Transform child in transform)
        {
            if (child == sun || child == earth || child.GetComponent<CelestialBody_NEW>() == null) continue;
            planets.Add(new KeyValuePair<float, Transform>(Vector3.ProjectOnPlane(child.position - sunWas, up).magnitude, child));
        }
        planets.Sort((x, y) => y.Key.CompareTo(x.Key));

        var targets = new List<KeyValuePair<Transform, Vector3>>();
        var report = new System.Text.StringBuilder();
        for (int i = 0; i < planets.Count; i++)
        {
            float orbit = planets[i].Key;
            Transform planet = planets[i].Value;
            float clear = Clearance(planet);

            // Where the line, moved clear of it, comes in across its orbit: the far side of the
            // line from the Sun first, then the near, and the other way round for the next.
            float first = i % 2 == 0 ? -clear : clear;
            Vector3 at;
            if (Crossing(orbit, first, b, sSun, out float s)) at = At(s, first);
            else if (Crossing(orbit, -first, b, sSun, out s)) at = At(s, -first);
            else at = At(sSun, b - orbit);   // never reached: the orbit's nearest point
            targets.Add(new KeyValuePair<Transform, Vector3>(planet, at));
            report.Append($"\n  {planet.name}: {Vector3.Dot(at - origin, d) - sEarth:0.0} before Earth, " +
                          $"{Vector3.Distance(at, origin + d * Vector3.Dot(at - origin, d)):0.00} off the line");
        }

        // The root rides on the Sun, as authored; then each body to its place.
        Vector3 rootFromSun = transform.position - sunWas;
        transform.position = At(sSun, b) + rootFromSun;
        sun.position = At(sSun, b);
        earth.position = At(sEarth, 0f);
        foreach (KeyValuePair<Transform, Vector3> t in targets) t.Key.position = t.Value;
        pathDirection = d;

        return $"Sun {b:0.0} to the side of the line, {sEarth - sSun:0.0} short of Earth; Earth on the line, " +
               $"{earthPastGate:0.00} past the gate.{report}";
    }

    /// <summary>
    /// Where along the line, coming in, a point <paramref name="y"/> across it (towards the Sun,
    /// which is <paramref name="b"/> across at <paramref name="sSun"/>) is on an orbit of
    /// radius <paramref name="orbit"/>.
    /// </summary>
    static bool Crossing(float orbit, float y, float b, float sSun, out float s)
    {
        float across = y - b;
        float q = orbit * orbit - across * across;
        s = sSun - Mathf.Sqrt(Mathf.Max(0f, q));
        return q >= 0f;
    }

    /// <summary>How far off the line a planet's centre has to be for the photon to pass it cleanly.</summary>
    float Clearance(Transform planet)
    {
        MeshFilter mf = planet.GetComponent<MeshFilter>();
        float radius = (mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.extents.x : 0.5f) * planet.lossyScale.x;
        float reach = radius * 2.5f;

        // Rings, and anything else it carries that draws.
        foreach (Renderer r in planet.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r.transform.GetComponent<CelestialBody_NEW>() != null && r.transform != planet) continue;
            Bounds bounds = r.bounds;
            if (bounds.size == Vector3.zero) continue;
            Vector3 e = bounds.extents;
            reach = Mathf.Max(reach, Vector3.Distance(bounds.center, planet.position) + Mathf.Max(e.x, Mathf.Max(e.y, e.z)));
        }
        return reach + passClearance;
    }

#if UNITY_EDITOR
    [ContextMenu("Lay Out Along the Photon's Path")]
    void LayOutMenu()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[SolarSystemPath_NEW] Lay it out in edit mode, then save the scene.", this);
            return;
        }

        LayerGate_NEW gate = FindGate(earthLayerId);
        if (!Find() || gate == null)
        {
            Debug.LogWarning($"[SolarSystemPath_NEW] Needs the photon (PlayerRig_NEW), a 'Sun' and an 'Earth' under " +
                             $"this object, and a gate into '{earthLayerId}'; nothing moved.", this);
            return;
        }

        var moved = new List<Object> { this, transform };
        foreach (Transform child in transform) moved.Add(child);
        Undo.RecordObjects(moved.ToArray(), "Lay Out Solar System Along the Photon's Path");

        string report = LayOut(photon.transform.position, photon.MovementDirection, gate);
        if (report == null)
        {
            Debug.LogWarning("[SolarSystemPath_NEW] Earth is on the Sun; nothing to lay out.", this);
            return;
        }
        Debug.Log("[SolarSystemPath_NEW] " + report + "\nSave the scene to keep it.", this);
    }
#endif
}

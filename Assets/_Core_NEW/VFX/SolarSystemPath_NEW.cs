using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Lays the Solar System out along the photon's path. The light only ever goes straight on,
/// so it is the Solar System that is placed: the light ends dead centre on Earth, and on the
/// way in it passes a couple of the other planets.
///
/// THE PATH. Earth's centre is where the photon's line meets the gate into the Earth layer —
/// at the gate itself: the gate is moved across onto the line with it, so Earth sits right
/// on the trigger and the light goes through the middle of both. The Sun is off to one side
/// of the line by sunAside of Earth's orbit, and short of Earth along it, so the line comes in
/// from outside the system and goes out through Earth's orbit just where Earth is. So the
/// light reaches Earth from the Sun's side, and Earth ahead is seen mostly lit.
///
/// The planets in passBy are put where the line crosses their orbits, moved round them until
/// the line passes passClearance clear (beyond two and a half of their radii, or their rings),
/// on alternate sides, outermost first: the photon flies past each in turn. The rest are
/// spread evenly round the Sun — round the part of the circle the line and the planets on it
/// leave free — each on its own orbit, the biggest orbits where they are furthest from the
/// line, so the system reads as orbits seen at an angle, not as a queue. Every orbit keeps its
/// authored radius, and the bodies their sizes, turns and moons. The orbits lie in the plane
/// through the line square to the Solar System's up.
///
/// WHEN. In the editor, from this component's menu (Lay Out Along the Photon's Path): from
/// where the photon starts and the way it flies; save the scene to keep it. Laying it out
/// again changes nothing unless the photon, the gate or the settings have.
///
/// In play a dark matter bend, or a debug jump, can leave the photon on a line beside the
/// first. So while the Solar System is out of sight and no dive is running, the system and
/// the Earth gate are slid across together to keep Earth on the line the photon is really on.
/// From the moment the dive into it begins, they stay where they are.
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

    [Tooltip("The gate into the Earth layer: Earth's centre is put on it. Found by earthLayerId if empty.")]
    [SerializeField] LayerGate_NEW earthGate;

    [Tooltip("The layer of the gate Earth sits on.")]
    [SerializeField] string earthLayerId = "Earth";

    [Tooltip("This system's render group, in WorldSwitcher_NEW.")]
    [SerializeField] string layerId = "SolarSystem";

    [Header("Layout")]
    [Tooltip("How far to the side of the line the Sun is, as a fraction of Earth's orbit. Higher: " +
             "the photon passes the Sun further off and comes to Earth from more to its side, so " +
             "less of Earth is lit; lower: closer past the Sun, Earth fuller.")]
    [Range(0.3f, 0.95f)] [SerializeField] float sunAside = 0.8f;

    [Tooltip("The planets the light passes on its way to Earth. Each needs an orbit bigger than " +
             "the Sun's distance from the line. The rest are spread round their orbits.")]
    [SerializeField] Transform[] passBy = new Transform[0];

    [Tooltip("How far clear of each planet it passes the line goes, beyond two and a half of its radii.")]
    [Min(0f)] [SerializeField] float passClearance = 0.6f;

    [Tooltip("How far from the line the other planets are kept, at least.")]
    [Min(0f)] [SerializeField] float keepClear = 4f;

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
        if (aside.sqrMagnitude < 1e-8f) return;

        transform.position -= aside;
        if (earthGate != null) earthGate.transform.position -= aside;
    }

    bool Find()
    {
        if (photon == null) photon = FindObjectOfType<PlayerRig_NEW>();
        if (sun == null) sun = transform.Find("Sun");
        if (earth == null) earth = transform.Find("Earth");
        if (earthGate == null)
        {
            foreach (LayerGate_NEW gate in FindObjectsOfType<LayerGate_NEW>())
                if (gate.LayerId == earthLayerId) { earthGate = gate; break; }
        }
        return photon != null && sun != null && earth != null && earthGate != null;
    }

    /// <summary>
    /// Places the Sun (with this root on it, as it is authored), Earth on the gate, and the
    /// planets, along the line from <paramref name="origin"/> in <paramref name="direction"/>.
    /// Returns what it did, or null if Earth is on the Sun.
    /// </summary>
    string LayOut(Vector3 origin, Vector3 direction)
    {
        Vector3 d = direction.normalized;
        Vector3 up = Vector3.ProjectOnPlane(transform.up, d);
        up = up.sqrMagnitude > 1e-6f ? up.normalized : Vector3.up;
        Vector3 side = Vector3.Cross(up, d).normalized;

        // The orbits as authored: each body's distance from the Sun, in the ecliptic.
        Vector3 sunWas = sun.position;
        float Orbit(Transform body) => Vector3.ProjectOnPlane(body.position - sunWas, up).magnitude;
        float earthOrbit = Orbit(earth);
        if (earthOrbit < 1e-4f) return null;

        // The Sun stays on the side of the line it was on.
        float towardsSun = Vector3.Dot(sunWas - origin, side) < 0f ? -1f : 1f;

        // Earth where the line meets the gate's plane, and the gate there with it.
        Transform gate = earthGate.transform;
        Vector3 normal = gate.forward;
        float facing = Vector3.Dot(d, normal);
        float sEarth = Mathf.Abs(facing) > 1e-4f ? Vector3.Dot(gate.position - origin, normal) / facing
                                                 : Vector3.Dot(gate.position - origin, d);

        // The Sun to the side, and short of Earth: the line leaves Earth's orbit at Earth.
        float b = sunAside * earthOrbit;
        float sSun = sEarth - Mathf.Sqrt(Mathf.Max(0f, earthOrbit * earthOrbit - b * b));

        // A point s along the line and y across it, y measured towards the Sun.
        Vector3 At(float s, float y) => origin + d * s + side * (towardsSun * y);
        Vector3 sunAt = At(sSun, b);

        // Round the Sun: 0 towards the line, a quarter turn along it.
        Vector3 toLine = -side * towardsSun;
        float AngleOf(Vector3 p) => Mathf.Atan2(Vector3.Dot(p - sunAt, d), Vector3.Dot(p - sunAt, toLine));
        Vector3 OnOrbit(float orbit, float angle) => sunAt + (toLine * Mathf.Cos(angle) + d * Mathf.Sin(angle)) * orbit;

        // The planets: the root's other children that CelestialBody_NEW draws.
        var passing = new List<KeyValuePair<float, Transform>>();
        var others = new List<KeyValuePair<float, Transform>>();
        var wanted = new HashSet<Transform>(passBy ?? new Transform[0]);
        foreach (Transform child in transform)
        {
            if (child == sun || child == earth || child.GetComponent<CelestialBody_NEW>() == null) continue;
            var entry = new KeyValuePair<float, Transform>(Orbit(child), child);
            (wanted.Contains(child) ? passing : others).Add(entry);
        }
        passing.Sort((x, y) => y.Key.CompareTo(x.Key));
        others.Sort((x, y) => y.Key.CompareTo(x.Key));

        var targets = new List<KeyValuePair<Transform, Vector3>>();
        var report = new System.Text.StringBuilder();
        Vector3 earthAt = At(sEarth, 0f);
        var taken = new List<float> { AngleOf(earthAt) };

        // Those the light passes: where the line, moved clear of each, comes in across its
        // orbit; the far side of the line from the Sun first, then the near, and so on.
        for (int i = 0; i < passing.Count; i++)
        {
            float orbit = passing[i].Key;
            Transform planet = passing[i].Value;
            float clear = Clearance(planet);
            float first = i % 2 == 0 ? -clear : clear;
            Vector3 at;
            if (Crossing(orbit, first, b, sSun, out float s)) at = At(s, first);
            else if (Crossing(orbit, -first, b, sSun, out s)) at = At(s, -first);
            else
            {
                // The line never reaches its orbit: it goes with the others instead.
                others.Add(passing[i]);
                report.Append($"\n  {planet.name}: its orbit is inside the Sun's distance from the line; not passed");
                continue;
            }
            targets.Add(new KeyValuePair<Transform, Vector3>(planet, at));
            taken.Add(AngleOf(at));
            report.Append($"\n  {planet.name}: passed {Vector3.Dot(sEarth * d - (at - origin), d):0.0} before Earth, " +
                          $"{clear:0.00} off the line");
        }
        others.Sort((x, y) => y.Key.CompareTo(x.Key));

        // The rest, spread evenly round what the line and the planets on it leave free — not
        // the stretch facing the line, between them — the biggest orbits first, each to the
        // free place furthest from the line.
        List<float> places = EvenPlaces(taken, others.Count, 0f);
        foreach (KeyValuePair<float, Transform> entry in others)
        {
            float orbit = entry.Key;
            int best = 0;
            float bestOff = -1f;
            for (int k = 0; k < places.Count; k++)
            {
                float off = Mathf.Abs(orbit * Mathf.Cos(places[k]) - b);
                if (off > bestOff) { bestOff = off; best = k; }
            }
            Vector3 at = OnOrbit(orbit, places[best]);
            places.RemoveAt(best);
            targets.Add(new KeyValuePair<Transform, Vector3>(entry.Value, at));

            float need = Mathf.Max(keepClear, Clearance(entry.Value));
            report.Append($"\n  {entry.Value.name}: round its orbit, {bestOff:0.0} from the line" +
                          (bestOff < need ? $" (closer than {need:0.0}: try another sunAside or passBy)" : ""));
        }

        // The root rides on the Sun, as authored; then each body to its place, and the gate.
        Vector3 rootFromSun = transform.position - sunWas;
        transform.position = sunAt + rootFromSun;
        sun.position = sunAt;
        earth.position = earthAt;
        foreach (KeyValuePair<Transform, Vector3> t in targets) t.Key.position = t.Value;
        gate.position = earthAt;
        pathDirection = d;

        return $"Earth on '{gate.name}' at {earthAt}, on the line; the Sun {b:0.0} to the side, " +
               $"{sEarth - sSun:0.0} short of it.{report}";
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

    /// <summary>
    /// <paramref name="count"/> angles spread evenly through the gaps between the angles
    /// <paramref name="taken"/>, except the gap the angle <paramref name="avoid"/> is in
    /// (unless it is the only one): each gap gets places in turn to whichever would leave the
    /// widest spacing, and its places are spaced evenly through it.
    /// </summary>
    static List<float> EvenPlaces(List<float> taken, int count, float avoid)
    {
        var places = new List<float>();
        if (count <= 0) return places;

        var sorted = new List<float>(taken);
        sorted.Sort();
        int gaps = sorted.Count;
        var starts = new float[gaps];
        var sizes = new float[gaps];
        var counts = new int[gaps];
        for (int i = 0; i < gaps; i++)
        {
            starts[i] = sorted[i];
            float next = i + 1 < gaps ? sorted[i + 1] : sorted[0] + 2f * Mathf.PI;
            sizes[i] = next - sorted[i];
        }
        if (gaps > 1)
        {
            for (int i = 0; i < gaps; i++)
            {
                float into = Mathf.Repeat(avoid - starts[i], 2f * Mathf.PI);
                if (into < sizes[i]) sizes[i] = 0f;
            }
        }

        for (int n = 0; n < count; n++)
        {
            int widest = 0;
            for (int i = 1; i < gaps; i++)
                if (sizes[i] / (counts[i] + 1) > sizes[widest] / (counts[widest] + 1)) widest = i;
            counts[widest]++;
        }

        for (int i = 0; i < gaps; i++)
            for (int k = 1; k <= counts[i]; k++)
                places.Add(starts[i] + sizes[i] * k / (counts[i] + 1));
        return places;
    }

    /// <summary>How far off the line a planet's centre has to be for the photon to pass it cleanly.</summary>
    float Clearance(Transform planet)
    {
        MeshFilter mf = planet.GetComponent<MeshFilter>();
        float radius = (mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.extents.x : 0.5f) * planet.lossyScale.x;
        float reach = radius * 2.5f;

        // Rings, and anything else it carries that draws (not a moon).
        foreach (Renderer r in planet.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer) continue;
            if (r.transform != planet && r.GetComponent<CelestialBody_NEW>() != null) continue;
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

        if (!Find())
        {
            Debug.LogWarning($"[SolarSystemPath_NEW] Needs the photon (PlayerRig_NEW), a 'Sun' and an 'Earth' under " +
                             $"this object, and a gate into '{earthLayerId}'; nothing moved.", this);
            return;
        }

        var moved = new List<Object> { this, transform, earthGate.transform };
        foreach (Transform child in transform) moved.Add(child);
        Undo.RecordObjects(moved.ToArray(), "Lay Out Solar System Along the Photon's Path");

        string report = LayOut(photon.transform.position, photon.MovementDirection);
        if (report == null)
        {
            Debug.LogWarning("[SolarSystemPath_NEW] Earth is on the Sun; nothing to lay out.", this);
            return;
        }
        Debug.Log("[SolarSystemPath_NEW] " + report + "\nSave the scene to keep it.", this);
    }
#endif
}

using UnityEditor;
using UnityEngine;

/// <summary>
/// Puts the journey's left-stick zoom, the layer dive and the photon layering into the
/// open scene.
///
/// All of them find everything else they need at runtime, so adding the components is the
/// whole setup: JourneyZoom_NEW and PhotonLayering_NEW go on the player, LayerDive_NEW next
/// to WorldSwitcher_NEW (the responders object), and CameraDirector_NEW picks the dive up
/// by itself. Each is skipped if the scene already has one, so running this again after an
/// update adds only what is new.
///
/// It works on the scene as loaded in the editor, not on the file, so unsaved changes
/// stay and the additions are one more undoable step. Nothing here ships.
/// </summary>
static class JourneyDiveMenu_NEW
{
    const string MenuPath = "Tools/Journey NEW/Add Zoom and Dive to Open Scene";
    const string ResetPath = "Tools/Journey NEW/Reset Dive Row to Starting Values/";

    [MenuItem(MenuPath)]
    static void AddZoomAndDive()
    {
        int added = 0;

        PlayerRig_NEW player = Object.FindObjectOfType<PlayerRig_NEW>();
        if (player == null)
        {
            Debug.LogWarning("Journey NEW: no PlayerRig_NEW in the open scene, so no zoom was added.");
        }
        else if (player.GetComponent<JourneyZoom_NEW>() != null)
        {
            Debug.Log("Journey NEW: '" + player.name + "' already has JourneyZoom_NEW.", player);
        }
        else
        {
            Undo.AddComponent<JourneyZoom_NEW>(player.gameObject);
            Debug.Log("Journey NEW: added JourneyZoom_NEW to '" + player.name + "'. Left stick zooms.", player);
            added++;
        }

        if (player != null && Object.FindObjectOfType<PhotonLayering_NEW>() == null)
        {
            Undo.AddComponent<PhotonLayering_NEW>(player.gameObject);
            Debug.Log("Journey NEW: added PhotonLayering_NEW to '" + player.name + "'. Web volumes " +
                      "with Layer Around Photon ticked now pass in front of and behind the light.", player);
            added++;
        }

        LayerDive_NEW dive = Object.FindObjectOfType<LayerDive_NEW>();
        WorldSwitcher_NEW worlds = Object.FindObjectOfType<WorldSwitcher_NEW>();
        if (dive != null)
        {
            Debug.Log("Journey NEW: '" + dive.name + "' already has LayerDive_NEW.", dive);
        }
        else if (worlds == null)
        {
            Debug.LogWarning("Journey NEW: no WorldSwitcher_NEW in the open scene, so no dive was added. " +
                             "The dive borrows its render groups.");
        }
        else
        {
            dive = Undo.AddComponent<LayerDive_NEW>(worlds.gameObject);
            Debug.Log("Journey NEW: added LayerDive_NEW to '" + worlds.name + "'. The gate into Micro " +
                      "now dives instead of looking down; add rows there for other gates.", worlds);
            added++;
        }

        // The gates tuned so far, each added only if the scene has no row for it yet — so
        // running this after an update brings in a new gate without touching tuned ones. A
        // gate whose starting values have changed since is reset with the menus below.
        if (dive != null)
        {
            foreach (Preset preset in Presets)
                added += AddRowIfMissing(dive, preset.row(), preset.what);
        }

        if (added > 0)
            Debug.Log("Journey NEW: save the scene to keep " + (added == 1 ? "it" : "them") + ".");
    }

    // One gate at a time: a row for another gate may carry tuning that has to survive.
    [MenuItem(ResetPath + "Into Micro (dive in)")]
    static void ResetMicroRow() => ResetRow(Presets[0]);

    [MenuItem(ResetPath + "Into CosmicWeb (leave the cluster)")]
    static void ResetCosmicWebRow() => ResetRow(Presets[1]);

    [MenuItem(ResetPath + "Into MilkyWay (dive in)")]
    static void ResetMilkyWayRow() => ResetRow(Presets[2]);

    // Its point back where it was tuned for, too: the values are only right for that.
    [MenuItem(ResetPath + "Into SolarSystem (dive in)")]
    static void ResetSolarRow() => ResetRow(new Preset { row = () => SolarDive(true), what = Presets[3].what });

    /// <summary>
    /// Puts one gate's row back to its current starting values — for when those have
    /// changed since the row was added. Every other row is left alone; one undoable step.
    /// </summary>
    static void ResetRow(Preset preset)
    {
        LayerDive_NEW dive = Object.FindObjectOfType<LayerDive_NEW>();
        if (dive == null)
        {
            Debug.LogWarning("Journey NEW: no LayerDive_NEW in the open scene. Run '" + MenuPath + "' first.");
            return;
        }

        LayerDive_NEW.Dive row = preset.row();
        Undo.RecordObject(dive, "Reset dive row");
        dive.ReplaceRow(row);
        EditorUtility.SetDirty(dive);

        Debug.Log("Journey NEW: the gate into " + row.toLayerId + " " + preset.what + ", from its starting " +
                  "values. Save the scene to keep it.", dive);
    }

    struct Preset
    {
        public System.Func<LayerDive_NEW.Dive> row;
        public string what;
    }

    static readonly Preset[] Presets =
    {
        new Preset { row = () => new LayerDive_NEW.Dive(), what = "dives in (cosmic web to galaxy)" },
        new Preset { row = () => LayerDive_NEW.Dive.ComingOut("CosmicWeb"), what = "leaves the cluster (galaxy back up to the web)" },

        // The same dive as into Micro, into the Milky Way. The point: 12 past the gate, on the
        // line, inside the bright Core knot of the x6000 web there (centre 3.4 off the line,
        // 7 across), with yellow knots just beyond; the approach starts once the photon is
        // out of the yellow cluster it came out into. dissolveFrom as tuned on Micro.
        new Preset
        {
            row = () => new LayerDive_NEW.Dive { toLayerId = "MilkyWay", focusPastGate = 12f, approachDistance = 30f, dissolveFrom = 0.5f },
            what = "dives in (cosmic web to the Milky Way)"
        },

        new Preset { row = () => SolarDive(false), what = "dives in (the Milky Way to the Solar System)" }
    };

    /// <summary>
    /// The dive from the Milky Way into the Solar System: one zoom into one star of the
    /// galaxy's disc, which turns out to be the Sun (Dive.continuous; CONTINUOUS on
    /// LayerDive_NEW).
    ///
    /// Five earlier tries taught the rules. A marker light standing on the galaxy reads as a
    /// marker; a point anywhere but inside the galaxy's particles is a portal somewhere else;
    /// sharp stars scattered round a point look stuck on. And the last three all swelled a
    /// light into a whiteout and grew the Solar System under it round the Sun — which the
    /// Solar camera, 18 degrees down at the photon with a 20-degree lens, has above the top of
    /// the frame. None of it was ever on screen: a yellow light was all there was to see. So
    /// nothing swells and nothing is hidden, and the camera keeps the star in frame.
    ///
    ///   the star   SolarDivePoint, in the galaxy's disc 15 out from its middle, 4 left of the
    ///              flight line and 20 past the gate — where, from the camera, it sits beside
    ///              the photon, not behind it — is where the Sun is: a point the size of the
    ///              galaxy's own stars (starSize), warm white like them, from the gate on.
    ///   spiral     5.5 seconds. The galaxy grows twelve times round it while turning 90
    ///              degrees anticlockwise seen from above — the way the planets go — so its
    ///              stars stream out of the frame along curves, as if the camera spiralled
    ///              down into the disc; it dissolves from 45%. Its stars resolve out of it in
    ///              its own white, blue and pink, streaking along the same curves; the camera
    ///              leans 4 degrees into the turn and levels out by the peak; the lens pushes
    ///              in from 40 to 30, with a touch of colour fringing at the fastest; the
    ///              camera turns half way towards the star, which stays a fifth of the frame
    ///              left of the middle while the photon sits as far right.
    ///   the flare  As the galaxy thins, the star grows diffraction spikes — four long, four
    ///              short, cooling to blue at their tips, turning slowly — until it is the one
    ///              thing left. The Solar System has been growing out of it since the gate,
    ///              from 1/400 of its size; at 6.5 seconds the Sun comes out from under it, and
    ///              it flares, spikes longest, then fades into the Sun with them drawn back in.
    ///   orbits     The planets come away from the Sun, the system still turning — another 60
    ///              degrees, slowing — and from 6.8 seconds each draws its orbit, from itself
    ///              round the way it goes, innermost first, a thin blue line with a bright
    ///              head; seen from above at an angle while the system is small and close.
    ///   landing    Faster once the galaxy is gone, it grows on into its own place, size and
    ///              turn by 12.3 seconds — the Sun 100 pixels across in the upper half, the
    ///              photon below. The orbits fade as, over the last 2.2, the camera turns back
    ///              to the photon and the lens goes to the Solar camera's 20, the Sun rising
    ///              out of the top.
    /// </summary>
    /// <param name="placePoint">Put SolarDivePoint back where these values were tuned for, if
    /// it has been moved. Resetting the row does; adding a missing one leaves it be.</param>
    static LayerDive_NEW.Dive SolarDive(bool placePoint)
    {
        WorldSwitcher_NEW worlds = Object.FindObjectOfType<WorldSwitcher_NEW>();
        Transform system = worlds != null ? worlds.GroupRoot("SolarSystem") : null;
        Transform sun = system != null ? system.Find("Sun") : null;
        if (sun == null)
            Debug.LogWarning("Journey NEW: no 'Sun' under the SolarSystem group. Set Emerge Override on " +
                             "the SolarSystem dive row by hand.");

        return new LayerDive_NEW.Dive
        {
            toLayerId = "SolarSystem",
            continuous = true,
            focusOverride = SolarDivePoint(worlds, placePoint),
            emergeOverride = sun,
            approachDistance = 0f,
            diveSeconds = 5.5f,
            peakHoldSeconds = 0.3f,
            emergeSeconds = 6.5f,
            diveZoom = 12f,
            leaveGlow = 1f,
            dissolveFrom = 0.45f,
            enterScale = 0.0025f,
            lightColor = new Color(1f, 0.95f, 0.84f, 1f),
            lightIntensity = 0.7f,
            whiteout = 0f,
            starSize = 0.024f,
            starSpikes = 0.6f,
            orbits = 0.35f,
            watch = 0.5f,
            watchRelease = 2.2f,
            spin = -90f,
            bank = 4f,
            resolve = true,
            memberCount = 900,
            crowdRadius = 10f,
            memberSize = 0.25f,
            resolveZoom = 40f,
            memberGrowth = 0.4f,
            streak = 2f,
            crowdFlatten = 0.12f,
            memberCore = 0.5f,
            approachCrowd = 0f,
            diveCrowd = 0.7f,
            memberWarm = new Color(1f, 0.93f, 0.86f, 1f),
            memberCool = new Color(0.72f, 0.82f, 1f, 1f),
            coolShare = 0.4f,
            memberAccent = new Color(0.96f, 0.69f, 0.92f, 1f),
            accentShare = 0.2f,
            dollyZoom = 1f,
            photonShrink = 0.5f,
            peakVignette = 0.3f,
            targetFieldOfView = 30f,
            peakBloom = 0f,
            peakChromaticAberration = 0.1f
        };
    }

    const string SolarDivePointName = "SolarDivePoint";

    /// <summary>
    /// The point in the Milky Way's disc the Solar dive goes into, under the MilkyWay group:
    /// found there if it is there, and made if not. Made — or, with <paramref name="place"/>,
    /// moved back — it sits in the plane of the galaxy's Disk particle system, 4 left of the
    /// flight line and 20 past the gate's centre (see SolarDive).
    /// </summary>
    static Transform SolarDivePoint(WorldSwitcher_NEW worlds, bool place)
    {
        Transform galaxy = worlds != null ? worlds.GroupRoot("MilkyWay") : null;
        if (galaxy == null)
        {
            Debug.LogWarning("Journey NEW: no MilkyWay group, so the Solar dive has no point in the galaxy to go into.");
            return null;
        }

        Transform point = null;
        foreach (Transform t in galaxy.GetComponentsInChildren<Transform>(true))
            if (t.name == SolarDivePointName) { point = t; break; }
        if (point != null && !place) return point;

        LayerGate_NEW gate = null;
        foreach (LayerGate_NEW g in Object.FindObjectsOfType<LayerGate_NEW>())
            if (g.LayerId == "SolarSystem") gate = g;

        ParticleSystem disk = null;
        foreach (ParticleSystem ps in galaxy.GetComponentsInChildren<ParticleSystem>(false))
            if (ps.name == "Disk") { disk = ps; break; }

        if (gate == null || disk == null)
        {
            Debug.LogWarning("Journey NEW: no SolarSystem gate or no Disk particle system in the Milky Way, so " +
                             "the Solar dive point was not placed. Make an object named " + SolarDivePointName +
                             " under the MilkyWay group and set it as the row's Focus Override.");
            return point;
        }

        Collider c = gate.GetComponent<Collider>();
        Vector3 at = c != null ? c.bounds.center : gate.transform.position;
        Vector3 spot = new Vector3(at.x - 4f, disk.transform.position.y, at.z + 20f);

        if (point == null)
        {
            var go = new GameObject(SolarDivePointName);
            Undo.RegisterCreatedObjectUndo(go, "Place Solar dive point");
            go.transform.SetParent(galaxy, true);
            point = go.transform;
        }
        else if ((point.position - spot).sqrMagnitude < 1e-4f)
        {
            return point;
        }
        else
        {
            Undo.RecordObject(point, "Place Solar dive point");
            Debug.Log("Journey NEW: " + SolarDivePointName + " was at " + point.position + ". From there the " +
                      "camera could not see the dive: it has to be in the disc and in frame, beside the photon.", point);
        }

        point.position = spot;
        Debug.Log("Journey NEW: " + SolarDivePointName + " is in the Milky Way's disc at " + spot + " — the Solar " +
                  "dive goes into it. Move it within the disc to dive elsewhere; keep it in frame and off the photon.", point);
        return point;
    }

    static int AddRowIfMissing(LayerDive_NEW dive, LayerDive_NEW.Dive row, string what)
    {
        if (dive.HasRow(row.toLayerId)) return 0;

        Undo.RecordObject(dive, "Add dive row");
        dive.AddRow(row);
        EditorUtility.SetDirty(dive);

        Debug.Log("Journey NEW: added a row for the gate into " + row.toLayerId + ", which now " + what + ".", dive);
        return 1;
    }

    [MenuItem(MenuPath, true)]
    static bool AddZoomAndDiveValidate() => !Application.isPlaying;

    [MenuItem(ResetPath + "Into Micro (dive in)", true)]
    [MenuItem(ResetPath + "Into CosmicWeb (leave the cluster)", true)]
    [MenuItem(ResetPath + "Into MilkyWay (dive in)", true)]
    [MenuItem(ResetPath + "Into SolarSystem (dive in)", true)]
    static bool ResetRowValidate() => !Application.isPlaying;
}

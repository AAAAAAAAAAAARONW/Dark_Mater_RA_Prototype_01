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

            // The way out of the quasar keeps the camera it opens on (see QuasarDive).
            if (dive.HasRow("Macro")) QuasarUsesWebCamera();
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

    // And the camera it needs: the quasar on the web's camera.
    [MenuItem(ResetPath + "Into Macro (leave the quasar)")]
    static void ResetQuasarRow()
    {
        ResetRow(Presets[4]);
        QuasarUsesWebCamera();
    }

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

        new Preset { row = () => SolarDive(false), what = "dives in (the Milky Way to the Solar System)" },

        new Preset { row = QuasarDive, what = "leaves the quasar (out into the early cosmic web)" }
    };

    /// <summary>
    /// The journey's first gate: out of the quasar into the early cosmic web — the layer
    /// called 'Macro'.
    ///
    /// It played the cover. The journey opens on the tutorial's last frame and pushes in
    /// behind the light (JourneyIntroCamera_NEW); two seconds later the light reached this
    /// gate and the camera turned to look straight down for twelve seconds while the web was
    /// swapped in out of sight, then looked up somewhere else. Right after the tutorial's
    /// pull-back that read as the piece losing its place. Then it was ComingOut facing
    /// forward, with the quasar's cluster round the light as soft dots — which read as wrong
    /// for the scale: nothing like galaxies sits round a quasar's disc. Now it is ComingOut
    /// with the look back, and what the camera finds is the quasar (QuasarVFX_NEW, put in by
    /// Tools > Journey NEW > Add Quasar VFX to Open Scene):
    ///
    ///   opening   Nothing round the light. It has just left the quasar, which is behind the
    ///             camera, under the quasar's sky; the journey opens on the tutorial's last
    ///             frame and pushes in, facing forward, as before.
    ///   look back Past the gate the camera swings round the light, 0.8 to 2.2 seconds, and
    ///             finds the quasar beyond it: the disc, its dust, the gas, the jets, about
    ///             half the frame's height across, upper right, with the light lower
    ///             left and its trail running back to it. Further round, lower and aimed
    ///             closer to the light than leaving the galaxy (ComingOut's 150°, 2 up, 0.45
    ///             of the way): the quasar is 70 behind, not 12 below, and aimed that far
    ///             back the light would be out of the frame.
    ///   collapse  The quasar shrinks into its core as the light pulls away — 6 times over
    ///             the dive, and twice more by the dolly zoom facing back — to a point among
    ///             the early web's filaments fading in round it, and fades out from 3.9
    ///             seconds to 6.5.
    ///   forward   From 6.2 seconds the camera swings forward into the web, the sky going
    ///             from the quasar's to the web's and the light speeding up.
    ///
    /// The tutorial's last beat looked back at its quasar, already a point, and that was the
    /// reason this one faced forward. But facing forward the journey's quasar is never on
    /// screen at all: it is behind the camera from the first frame. A lookBackYaw of 0 puts
    /// the forward-facing way back (without the dots). And the Quasar layer uses the web's
    /// camera (QuasarUsesWebCamera), so the opening push lands on the framing the web keeps.
    /// </summary>
    static LayerDive_NEW.Dive QuasarDive()
    {
        LayerDive_NEW.Dive d = LayerDive_NEW.Dive.ComingOut("Macro");
        d.approachDistance = 5f;
        d.diveSeconds = 6.5f;
        d.peakHoldSeconds = 0.2f;
        d.emergeSeconds = 5f;
        d.diveZoom = 6f;
        d.dissolveFrom = 0.6f;
        d.resolve = false;
        d.memberCount = 0;
        d.haloRadius = 0f;
        d.lookBackYaw = 165f;
        d.lookBackLift = 0.5f;
        d.lookBackFrame = 0.06f;
        d.lookBackUntil = 7.6f;
        d.peakBloom = 1.2f;
        return d;
    }

    /// <summary>
    /// The quasar is the journey's first few seconds, and its camera is what the opening
    /// shot pushes in to. Give it the early web's camera, so the push lands on the framing
    /// the web keeps and the gate out of the quasar changes nothing about the camera: a
    /// Quasar entry in CameraDirector_NEW's zone cameras, with the Macro entry's camera.
    /// Left alone if it is already so.
    /// </summary>
    static void QuasarUsesWebCamera()
    {
        CameraDirector_NEW director = Object.FindObjectOfType<CameraDirector_NEW>();
        if (director == null) return;

        var so = new SerializedObject(director);
        SerializedProperty list = so.FindProperty("zoneCameras");
        if (list == null || !list.isArray) return;

        Object webCamera = null;
        int quasar = -1;
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            string id = entry.FindPropertyRelative("layerId").stringValue;
            if (id == "Macro") webCamera = entry.FindPropertyRelative("vcam").objectReferenceValue;
            if (id == "Quasar") quasar = i;
        }

        if (webCamera == null)
        {
            Debug.LogWarning("Journey NEW: CameraDirector_NEW has no camera for the Macro layer, so the Quasar " +
                             "layer keeps its own; the camera will change at the first gate.", director);
            return;
        }

        if (quasar < 0)
        {
            list.arraySize++;
            quasar = list.arraySize - 1;
            list.GetArrayElementAtIndex(quasar).FindPropertyRelative("layerId").stringValue = "Quasar";
        }

        SerializedProperty vcam = list.GetArrayElementAtIndex(quasar).FindPropertyRelative("vcam");
        if (vcam.objectReferenceValue == webCamera) return;

        vcam.objectReferenceValue = webCamera;
        so.ApplyModifiedProperties();
        Debug.Log("Journey NEW: the Quasar layer now uses the early web's camera ('" + webCamera.name + "'), so the " +
                  "opening shot pushes in to the framing the web keeps.", director);
    }

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
    ///              to the photon and the lens goes to the Solar camera's 20.
    ///   settle     The Solar camera as authored sits 1 above the photon and looks down at it,
    ///              with the Sun above the top of the frame: the transition used to end with
    ///              the system sliding out of view. So over the last 3 seconds it comes down
    ///              its orbit (Y axis 0.5 to about 0.39, from 1 above the photon to 0.2) and
    ///              looks ahead instead: the Sun stays in the upper half all the way down and
    ///              ends a fifth of the way up from the middle, the planets in a band across
    ///              the frame, the photon just below the middle. It is left there for the
    ///              player — until they look elsewhere or recentre.
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
            peakChromaticAberration = 0.1f,
            endFacingStar = true,
            endStarHeight = 0.2f,
            endSettleSeconds = 3f
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

        // The polished galaxy has a tilted volumetric disc, not a Disk particle system.
        // Keep the legacy lookup for the other scenes that still use the old galaxy.
        MilkyWayStars_NEW spiral = galaxy.GetComponentInChildren<MilkyWayStars_NEW>(false);
        if (gate == null || (disk == null && spiral == null))
        {
            Debug.LogWarning("Journey NEW: no SolarSystem gate or galactic disc in the Milky Way, so " +
                             "the Solar dive point was not placed. Make an object named " + SolarDivePointName +
                             " under the MilkyWay group and set it as the row's Focus Override.");
            return point;
        }

        Collider c = gate.GetComponent<Collider>();
        Vector3 at = c != null ? c.bounds.center : gate.transform.position;
        Vector3 centre = spiral != null ? spiral.transform.position : disk.transform.position;
        Vector3 normal = spiral != null ? spiral.transform.up : Vector3.up;
        Vector3 spot = new Vector3(at.x - 4f, centre.y, at.z + 20f);
        if (Mathf.Abs(normal.y) > 0.01f)
            spot.y = centre.y - (normal.x * (spot.x - centre.x) + normal.z * (spot.z - centre.z)) / normal.y;

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
    [MenuItem(ResetPath + "Into Macro (leave the quasar)", true)]
    static bool ResetRowValidate() => !Application.isPlaying;
}

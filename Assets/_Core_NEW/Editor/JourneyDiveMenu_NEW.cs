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

    [MenuItem(ResetPath + "Into SolarSystem (dive in)")]
    static void ResetSolarRow() => ResetRow(Presets[3]);

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

        new Preset { row = SolarDive, what = "dives in (the Milky Way to the Solar System)" }
    };

    /// <summary>
    /// The dive from the Milky Way into the Solar System: down into the galaxy's own disc.
    ///
    /// Two earlier tries taught the rules. A marker light standing on the galaxy reads as a
    /// marker, not as part of it; a point anywhere but inside the galaxy's particles is a
    /// portal somewhere else; and added stars, however soft, look stuck on. So the dive goes
    /// into the galaxy itself and nothing is added to it but the light it dives towards.
    ///
    ///   the point  SolarDivePoint, an empty object placed in the galaxy's disc — inside its
    ///              particle systems, 9.7 from the centre — where the camera's own sight line
    ///              through the photon (one down in three, from 3 behind and 1 above) meets
    ///              the disc midway through the dive. So from the camera the photon sits in
    ///              front of the point the whole way, within 4 degrees: it is seen plunging
    ///              into it, and the camera never has to turn. Move it to dive elsewhere.
    ///   dive       No marker beforehand. From the gate, five seconds: the lens pushes in from
    ///              40 to 20 (the Solar camera's own, so there is no handover); the galaxy
    ///              magnifies eight times round the point, its own disc and stars swelling
    ///              and flowing out round the photon; a warm light rises out of the point in
    ///              the disc and swells; the photon's trail narrows to half; a soft vignette;
    ///              the galaxy dissolves late, into sunlight.
    ///   emerge     Under the light the Solar camera takes over and the light moves to the Sun
    ///              (emergeOverride); the Solar System grows out of it from 0.15 of its size,
    ///              the planets moving out to their orbits, the Earth ending almost dead ahead.
    /// </summary>
    static LayerDive_NEW.Dive SolarDive()
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
            focusOverride = SolarDivePoint(worlds),
            emergeOverride = sun,
            approachDistance = 0f,
            diveSeconds = 5f,
            peakHoldSeconds = 0.2f,
            emergeSeconds = 4.5f,
            diveZoom = 8f,
            leaveGlow = 1.5f,
            dissolveFrom = 0.55f,
            enterScale = 0.15f,
            lightColor = new Color(1f, 0.93f, 0.78f, 1f),
            lightIntensity = 1.5f,
            pointSize = 0.6f,
            gateSize = 1f,
            peakSize = 40f,
            whiteout = 1f,
            resolve = false,
            dollyZoom = 1f,
            photonShrink = 0.5f,
            peakVignette = 0.3f,
            targetFieldOfView = 20f,
            peakBloom = 2f,
            peakChromaticAberration = 0f
        };
    }

    const string SolarDivePointName = "SolarDivePoint";

    /// <summary>
    /// The point in the Milky Way's disc the Solar dive goes into: found under the MilkyWay
    /// group if it is there — it may have been moved — or made. Made, it sits in the plane of
    /// the galaxy's Disk particle system, under the flight line, where the camera's sight
    /// line through the photon meets that plane midway through the dive (see SolarDive).
    /// </summary>
    static Transform SolarDivePoint(WorldSwitcher_NEW worlds)
    {
        Transform galaxy = worlds != null ? worlds.GroupRoot("MilkyWay") : null;
        if (galaxy == null)
        {
            Debug.LogWarning("Journey NEW: no MilkyWay group, so the Solar dive has no point in the galaxy to go into.");
            return null;
        }

        foreach (Transform t in galaxy.GetComponentsInChildren<Transform>(true))
            if (t.name == SolarDivePointName) return t;

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
            return null;
        }

        Collider c = gate.GetComponent<Collider>();
        Vector3 at = c != null ? c.bounds.center : gate.transform.position;
        float plane = disk.transform.position.y;

        // The photon enters the trigger half its depth early and flies about three units in
        // the first half of the dive; from there the camera's sight line drops one in three
        // to the disc.
        float half = c != null ? c.bounds.extents.z : 0f;
        Vector3 point = new Vector3(at.x, plane, at.z - half + 3f + 3f * (at.y - plane));

        var go = new GameObject(SolarDivePointName);
        Undo.RegisterCreatedObjectUndo(go, "Place Solar dive point");
        go.transform.SetParent(galaxy, true);
        go.transform.position = point;
        Debug.Log("Journey NEW: placed " + SolarDivePointName + " in the Milky Way's disc at " + point +
                  " — the Solar dive goes into it. Move it to dive elsewhere.", go);
        return go.transform;
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

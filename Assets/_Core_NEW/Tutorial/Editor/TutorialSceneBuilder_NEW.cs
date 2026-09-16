using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;

/// <summary>
/// Builds the Phase 0–1 tutorial rig into the open scene, wired, lit and playable.
///
/// Tutorial.unity is an empty scene. Phase 0–1 is seven beats, a first person rig, three
/// HUD elements, an attract card, two motes and a quasar, and every one of those has
/// references into the others. Hand-wiring that is around forty Inspector drags, which
/// is forty chances to point B2's gate at B1's mote and spend an afternoon on it. The
/// _Core_NEW README already records what happens when scene layout is assembled by hand
/// over time: it drifts, and nobody can see that it has.
///
/// So this is the layout, in code, where it can be read and re-run.
///
/// OWNERSHIP — the rule that makes running it twice safe, and the one to keep if you
/// extend this file. There are two menu items:
///
///   Build or Update             the normal one. Additive. It creates what is missing
///                               and fills references that are empty, and it does not
///                               touch anything that already exists. Run it after
///                               pulling a change that adds a phase.
///   Rebuild From Scratch        deletes the rig first, and says so in a dialog.
///
/// The split is by kind, not by field:
///
///   The builder owns EXISTENCE and WIRING — which objects are there, which gate points
///   at which mote, which director the HUD listens to. A null reference is a gap, not a
///   decision, usually because a later run added the thing it should point at, so those
///   get filled and the fill is logged.
///
///   You own VALUES — transforms, tuned numbers, materials, extra children, hookups on
///   the UnityEvents. Numbers are never written to a component that already existed,
///   because there is no way to tell "nobody set this" from "somebody set it to exactly
///   that".
///
/// Anything you add here has to hold that line: go through FindOrCreate / AddIfMissing /
/// Wire, and guard direct property writes with IsFresh.
///
/// WHAT IT REUSES. Everything visible comes from assets the project already has:
///
///   BlazingQuasar.mat                 core, halo, accretion disc and bipolar jets in
///                                     one shader — the distant target, and B2's jet
///                                     channel, are the same object
///   Custom_PhotonTrail.mat            the player's own light, with PhotonSpectrumTrail
///   Main Camera Profile.asset         the journey's post-process grade, shared so the
///                                     tutorial and the journey cannot look different
///
/// See TutorialWorldAssets_NEW for the paths and for the whitebox assets it generates.
///
/// WHAT IT DOES NOT TOUCH. RenderSettings, including the skybox. That is assigned by
/// hand. TutorialSky_NEW exists if the tutorial ever wants the journey's exact quasar
/// sky driven from NP_Quasar, but the builder does not add it.
///
/// STAGING. The positions below are a default. Everything is a serialized field with a
/// gizmo, so moving it is a drag in the Scene view.
/// </summary>
public static class TutorialSceneBuilder_NEW
{
    const string RootName = "[Tutorial]";

    // ── Staging ──────────────────────────────────────────────────────────────
    //
    // The player sits at the origin facing +Z, which is the neutral view and the axis A
    // recentres to. Everything else is placed relative to that.

    static readonly Vector3 PlayerPosition = Vector3.zero;

    /// <summary>
    /// A long way ahead, on the travel axis. The light is approaching it, not sitting
    /// inside it — the experience is an approach, so the quasar is a distant bright
    /// object that grows, not a wall the player is standing in.
    ///
    /// The distance is worked backwards from the timeline rather than picked:
    ///
    ///   Phase 0    20s of cruise at 25 u/s covers 500, so the quasar has visibly grown
    ///              before the first prompt.
    ///   Phase 1    player-gated and unbounded. TravelHoldDistance stops the cruise at
    ///              900 from the core, so a visitor who explores for two minutes parks
    ///              there instead of flying through the thing they are heading for.
    ///   C1         closes whatever is left, in exactly 12 seconds, ending at rest close
    ///              enough that the quasar overfills the frame. TutorialEmission_NEW
    ///              works that distance out from the quasar's size and the camera's
    ///              field of view rather than storing it, so rescaling the quasar cannot
    ///              silently leave the player parked too far away.
    ///
    /// So a rushing visitor arrives at C1 from about 2100 out and an unhurried one from
    /// 900, and both reach the quasar as C2 opens.
    /// </summary>
    static readonly Vector3 QuasarPosition = new Vector3(0f, 0f, 2600f);

    /// <summary>Closest the cruise may get. C1's approach is what goes inside it.</summary>
    const float TravelHoldDistance = 900f;

    /// <summary>
    /// No tilt. BlazingQuasar draws its bipolar jets along the object's local Y, so
    /// upright means the jets run vertically and B2's "looking up reveals the jet
    /// channel" is literally what happens.
    /// </summary>
    static readonly Vector3 QuasarEuler = Vector3.zero;

    /// <summary>Radius 450 at 6000 away subtends about 8.6°: distinctly distant, clearly there.</summary>
    static readonly Vector3 QuasarScale = new Vector3(900f, 900f, 900f);

    /// <summary>Units per second, constant. Closes roughly 1400 units over Phase 0–1.</summary>
    const float TravelSpeed = 25f;

    /// <summary>
    /// Field of view, copied from PlaytestBuild_NEW's FreeLook rigs. Unity's default 60
    /// reads as a different game.
    /// </summary>
    const float FieldOfView = 40f;

    // Mote positions are LOCAL to the player: they are parented to the light and travel
    // with it. A mote pinned in world space would be behind a 25 unit/second player in
    // four seconds, which makes B1 ungateable.

    /// <summary>Right, just inside frame at FOV 40, so A3 can drift it out of the edge.</summary>
    static readonly Vector3 MoteAPosition = new Vector3(26f, 1f, 40f);

    /// <summary>Overhead. The storyboard's B2 is "the mote passes overhead".</summary>
    static readonly Vector3 MoteBPosition = new Vector3(4f, 34f, 26f);

    /// <summary>Behind. The storyboard's B3 is "third mote, behind".</summary>
    static readonly Vector3 MoteCPosition = new Vector3(-6f, 2f, -40f);

    /// <summary>
    /// True when this run started with no rig in the scene, so everything it touches
    /// is its own to configure. False on an update, where values belong to whoever
    /// set them. See the OWNERSHIP note at the top of the file.
    /// </summary>
    static bool _freshBuild;

    /// <summary>Objects created by this run. Only these get their values written.</summary>
    static readonly System.Collections.Generic.HashSet<int> _fresh =
        new System.Collections.Generic.HashSet<int>();

    static int _created;
    static int _wired;

    /// <summary>The last beat the builder walked past. See Beat.</summary>
    static Transform _beatCursor;

    /// <summary>Build the rig, or fill in whatever is missing from the one that is there.</summary>
    [MenuItem("Tools/Journey NEW/Tutorial/Build or Update", false, 40)]
    public static void BuildOrUpdate()
    {
        Run();
    }

    /// <summary>
    /// Delete the rig and build it again. Destructive, and says so before doing it.
    /// </summary>
    [MenuItem("Tools/Journey NEW/Tutorial/Rebuild From Scratch (deletes your edits)", false, 41)]
    public static void RebuildFromScratch()
    {
        GameObject existing = GameObject.Find(RootName);

        if (existing != null)
        {
            bool ok = EditorUtility.DisplayDialog(
                "Delete the existing tutorial rig?",
                "This deletes " + RootName + " and everything under it, including any " +
                "positions, tuned values, materials, child objects and event hookups you " +
                "or anyone else has changed by hand.\n\n" +
                "Build or Update does the same job without destroying anything. Use this " +
                "only when you actually want to start over.",
                "Delete and rebuild", "Cancel");

            if (!ok) return;

            // The camera is the scene's, not the rig's — it was adopted on the first
            // build — so lift it out rather than deleting it with everything else.
            //
            // But strip what the builder bolted onto it. Without this the rescue
            // defeats the rebuild: the look rig, the shake and the post-process stack
            // all ride on the camera, AddIfMissing finds them still attached and
            // therefore not fresh, and every tuned value on them survives a "rebuild
            // from scratch". The camera is yours; what we put on it is ours.
            Camera main = Camera.main;

            if (main != null && main.transform.IsChildOf(existing.transform))
            {
                Undo.SetTransformParent(main.transform, null, "Lift camera out of the rig");
                StripBuilderComponents(main.gameObject);
            }

            Undo.DestroyObjectImmediate(existing);
        }

        Run();
    }

    /// <summary>
    /// Remove the components this builder adds to an object it does not own.
    ///
    /// Only the camera needs this, and only on a rebuild. The list is every type the
    /// builder attaches to the camera — keep it in step with AcquireCamera,
    /// ApplyPostProcessing and the Phase 2 rig, or a rebuild will quietly stop being one
    /// for whatever gets left off.
    /// </summary>
    static void StripBuilderComponents(GameObject go)
    {
        Remove<FirstPersonLookRig_NEW>(go);
        Remove<TutorialCameraShake_NEW>(go);
        Remove<PostProcessLayer>(go);
        Remove<PostProcessVolume>(go);
    }

    static void Remove<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        if (component == null) return;

        Undo.DestroyObjectImmediate(component);
    }

    /// <summary>
    /// One entry point, and one question asked once: is there a rig here already?
    ///
    /// If there is not, this run made everything it touches and may configure all of
    /// it, including the scene's existing Main Camera. If there is, everything older
    /// than this run belongs to whoever set it. Rebuild From Scratch does not need a
    /// flag of its own — it deletes the rig first, so the answer becomes "no" by itself.
    /// </summary>
    static void Run()
    {
        _freshBuild = GameObject.Find(RootName) == null;

        _fresh.Clear();
        _created = 0;
        _wired = 0;
        _beatCursor = null;

        Undo.SetCurrentGroupName(_freshBuild ? "Build tutorial scene" : "Update tutorial scene");
        int group = Undo.GetCurrentGroup();

        GameObject root = FindOrCreate(RootName, null, Vector3.zero);

        // ── Environment ──────────────────────────────────────────────────────
        DimSceneLights();

        // ── Player ───────────────────────────────────────────────────────────
        GameObject player = FindOrCreate("Photon", root.transform, PlayerPosition);
        Camera camera = AcquireCamera(player.transform);
        FirstPersonLookRig_NEW lookRig = AddIfMissing<FirstPersonLookRig_NEW>(camera.gameObject);

        ApplyPostProcessing(camera);

        // ── World ────────────────────────────────────────────────────────────
        GameObject world = FindOrCreate("World", root.transform, Vector3.zero);

        GameObject quasar = BuildQuasar(world.transform);

        BuildTravel(player, quasar.transform, lookRig);
        BuildDust(player.transform);
        BuildSpeedStreaks(player.transform);

        // Held onto: Phase 3's absorption buffer writes its lines into this trail's
        // material, so D2's missing colour shows on the light itself and not only on
        // the bar.
        TrailRenderer photonTrail = BuildPhotonTrail(player.transform);

        // Motes ride with the light. See MoteAPosition.
        GameObject motes = FindOrCreate("Motes", player.transform, PlayerPosition);

        GameObject moteA = Mote("Mote_A3_B1", motes.transform, MoteAPosition, Vector3.right, lookRig);

        // B2 and B3 reveal their own motes, so these start off — but only if this run
        // made them. Re-hiding a mote somebody switched on to look at is not the
        // builder's business.
        GameObject moteB = Mote("Mote_B2", motes.transform, MoteBPosition, Vector3.up, lookRig);
        if (IsFresh(moteB)) moteB.SetActive(false);

        GameObject moteC = Mote("Mote_B3", motes.transform, MoteCPosition, Vector3.left, lookRig);
        if (IsFresh(moteC)) moteC.SetActive(false);

        // ── HUD ──────────────────────────────────────────────────────────────
        GameObject canvas = BuildCanvas(root.transform);

        // Order matters: the blackout is first so everything else draws over it, and the
        // attract card reads as white text on black rather than being hidden by the fade.
        GameObject blackout = BuildBlackout(canvas.transform);

        GameObject reticle = BuildReticle(canvas.transform);
        GameObject legend = BuildLegend(canvas.transform);

        // ── Retired: the range map ───────────────────────────────────────────
        // A corner plate with the quasar, the player, a facing needle and a distance
        // readout. It answered "am I going anywhere?", which is a real question in a
        // piece whose nearest object is 40 units away and whose destination is 2600 —
        // but it answered it with a second diagram to learn, in the corner, in a
        // tutorial whose whole subject is learning to read one readout. The spectrum bar
        // is the thing the player has to be able to read by the end, and it now shares
        // the top strip with nothing.
        //
        // TutorialRangeMap_NEW and BuildRangeMap below are both left in place. They work,
        // nothing calls them, and putting the map back is one line here — which is a
        // smaller decision than writing the whole thing again.
        RetireObject(canvas.transform, "RangeMap",
                     "the corner map is a second readout to learn in a piece about learning to " +
                     "read one");

        GameObject hint = BuildHint(canvas.transform);
        GameObject stickGuide = BuildStickGuide(hint);
        GameObject prompt = BuildConfirmPrompt(canvas.transform);
        GameObject card = BuildAttractCard(canvas.transform);

        if (IsFresh(reticle)) reticle.SetActive(false);
        if (IsFresh(legend)) legend.SetActive(false);
        if (IsFresh(hint)) hint.SetActive(false);
        if (IsFresh(prompt)) prompt.SetActive(false);

        // ── Director and beats ───────────────────────────────────────────────
        GameObject directorObject = FindOrCreate("Director", root.transform, Vector3.zero);
        TutorialDirector_NEW director = AddIfMissing<TutorialDirector_NEW>(directorObject);

        GameObject beats = FindOrCreate("Beats", directorObject.transform, Vector3.zero);

        // ── Phase 2 rig ──────────────────────────────────────────────────────
        GameObject flashObject = BuildFlash(canvas.transform);
        TutorialCameraShake_NEW shake = AddIfMissing<TutorialCameraShake_NEW>(camera.gameObject);

        // Left stick zooms. See TutorialZoom_NEW for why this exists and where it
        // departs from GDD �4.
        TutorialZoom_NEW zoom = AddIfMissing<TutorialZoom_NEW>(camera.gameObject);
        Wire(zoom).Ref("lookRig", lookRig).Num("baseFieldOfView", FieldOfView).Apply();

        SeparateTheSticks(lookRig);
        TutorialEmission_NEW emission = BuildEmission(player, lookRig, shake,
                                                      flashObject.GetComponent<TutorialFlash_NEW>(),
                                                      quasar.transform);

        // ── Phase 3 rig ──────────────────────────────────────────────────────
        TutorialUISlide_NEW spectrumSlide;

        TutorialSpectrum_NEW spectrum = BuildSpectrum(root.transform, canvas.transform, photonTrail,
                                                      out spectrumSlide);
        TutorialSlowMotion_NEW slowMotion = BuildSlowMotion(root.transform);
        TutorialPause_NEW pause = BuildPause(root.transform, director, slowMotion);
        TutorialTravel_NEW travel = player.GetComponent<TutorialTravel_NEW>();

        // Before any atom is built, so the three that follow all pick up the corrected
        // materials rather than the first one repairing an asset the other two have
        // already been handed.
        RepairAtomCoreTranslucency();
        RepairAtomHaloBrightness();

        TutorialAtom_NEW atom = BuildAtom(world.transform, camera.transform, travel);
        TutorialAtomCluster_NEW cluster = BuildAtomCluster(world.transform, camera.transform,
                                                           travel, spectrum);

        TutorialTrailBands_NEW trailBands = BuildTrailBands(photonTrail);
        TutorialSpectrumBands_NEW spectrumBands = BuildSpectrumBands(canvas.transform, trailBands, spectrum);
        TutorialAtom_NEW forestAtom = BuildForestAtom(world.transform, camera.transform, travel,
                                                      spectrum, spectrumBands);
        TutorialLineIndicator_NEW lineIndicator = BuildLineIndicator(canvas.transform, spectrum, trailBands,
                                                                     photonTrail, travel, camera);

        Phase3Rig phase3 = new Phase3Rig
        {
            trailBands = trailBands,
            spectrumBands = spectrumBands,
            spectrum = spectrum,
            slide = spectrumSlide,
            atom = atom,
            slowMotion = slowMotion,
            cluster = cluster,
            forestAtom = forestAtom,
            pause = pause,
            lineIndicator = lineIndicator
        };

        BuildPhase0(beats.transform, moteA);
        BuildPhase1(beats.transform, lookRig, moteA, moteB, moteC);
        BuildPhase2(beats.transform, lookRig, quasar, emission);
        BuildPhase3(beats.transform, phase3);

        // ── HUD and attract components ───────────────────────────────────────
        TutorialHUD_NEW hud = AddIfMissing<TutorialHUD_NEW>(canvas);
        WireHud(hud, director, legend, reticle, hint, prompt,
                stickGuide.GetComponent<TutorialStickGuide_NEW>());

        TutorialAttract_NEW attract = AddIfMissing<TutorialAttract_NEW>(canvas);
        WireAttract(attract, director, card, lookRig, player.GetComponent<TutorialTravel_NEW>());

        // Phase 3's state has to go back too, or the second visitor of the day inherits
        // the first one's spectrum, a half-flown atom, and — worst of the three — a
        // world still running at a fifth speed.
        AddCall(attract, "onReset", spectrum, "ResetForAttract");
        AddCall(attract, "onReset", atom, "ResetForAttract");
        AddCall(attract, "onReset", slowMotion, "RestoreNow");

        // The bar has to go back to where it comes in from, not just be hidden. A
        // restart that left it parked at the top would have the next visitor's D1 show
        // a bar that never travelled.
        AddCall(attract, "onReset", spectrumSlide, "ResetToStart");
        AddCall(attract, "onReset", cluster, "ResetForAttract");

        // A restart taken while paused must not leave the next visitor with a stopped world.
        AddCall(attract, "onReset", pause, "ResumeNow");

        // The trail back to the visible band with no redshift; the bar's bands open and
        // still; the mid-redshift atom back out of the world.
        AddCall(attract, "onReset", trailBands, "ResetForAttract");
        AddCall(attract, "onReset", spectrumBands, "ResetForAttract");
        AddCall(attract, "onReset", forestAtom, "ResetForAttract");
        AddCall(attract, "onReset", lineIndicator, "ResetForAttract");

        // Needs the director, so it is built after the director exists.
        BuildZoomGauge(canvas.transform, zoom, director);

        // Last on the canvas, over everything above — see BuildPauseCard.
        BuildPauseCard(canvas.transform, pause);

        // And then this, which is later still: the ending has to cover the pause card
        // too. See BuildOutro.
        TutorialOutro_NEW outro = BuildOutro(canvas.transform, root.transform, director,
                                             attract, camera, lookRig, zoom,
                                             player.GetComponent<TutorialTravel_NEW>());

        // The next visitor has to get a camera they can move. The outro takes the rig and
        // the zoom off while it owns the pull-back, and this is what gives them back —
        // one place puts things back, and it is the same place everything else in the
        // piece is put back from.
        AddCall(attract, "onReset", outro, "ResetForAttract");

        TutorialFadeIn_NEW fade = AddIfMissing<TutorialFadeIn_NEW>(blackout);
        Wire(fade)
            .Ref("director", director)
            .Ref("blackout", blackout.GetComponent<Image>())
            .Str("startBeatId", "A1")
            .Apply();

        Undo.CollapseUndoOperations(group);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;

        Report(root, beats.transform);
    }

    /// <summary>
    /// Say what the rig now contains, not only what changed.
    ///
    /// "Nothing to do" on its own is indistinguishable from a run that fell over before
    /// it got anywhere, which is exactly the doubt a no-op should not leave. Listing the
    /// beats answers the question somebody actually has after running this — did the new
    /// phase land? — without opening the Hierarchy.
    /// </summary>
    static void Report(GameObject root, Transform beats)
    {
        System.Text.StringBuilder ids = new System.Text.StringBuilder();
        int count = 0;

        foreach (TutorialBeat_NEW beat in beats.GetComponentsInChildren<TutorialBeat_NEW>(true))
        {
            if (count > 0) ids.Append(' ');
            ids.Append(beat.BeatId);
            count++;
        }

        string inventory = count + " beats: " + ids;

        if (_freshBuild)
        {
            Debug.Log("[TutorialSceneBuilder_NEW] Built from scratch. " + inventory + ".\n" +
                      "Press Play: the title card waits for A, then A1 runs. " +
                      "F2 skips a beat while the debug overlay is on.", root);
            return;
        }

        if (_created == 0 && _wired == 0)
        {
            Debug.Log("[TutorialSceneBuilder_NEW] Checked the rig, nothing was missing. " +
                      inventory + ". Nothing of yours was touched.", root);
            return;
        }

        Debug.Log("[TutorialSceneBuilder_NEW] Updated: " + _created + " object(s) or " +
                  "component(s) added, " + _wired + " reference(s) filled. " + inventory + ".\n" +
                  "Everything that already existed was left as it was.", root);
    }

    /// <summary>
    /// Make sure look and zoom are not on the same stick, and repair it if they are.
    ///
    /// This overwrites a value on a component the builder did not create, which the
    /// ownership rule otherwise forbids — so it is worth being exact about why it is
    /// allowed here. The rule protects DECISIONS. Look and zoom sharing a stick is not a
    /// decision anybody could have made on purpose: it leaves the other stick doing
    /// nothing and makes both prompts wrong. It is a broken state, and a builder that
    /// can see a broken state and leaves it alone is not being careful, it is being
    /// useless.
    ///
    /// It exists because of exactly one real failure. Look was moved from the left stick
    /// to the right by changing the field's C# DEFAULT — which does nothing at all to a
    /// scene that already had the old value serialised. The scene kept Left, the zoom
    /// took Left too, and the right stick went dead. Changing a default is invisible to
    /// every scene that already exists; only a migration reaches them.
    ///
    /// So the check is on the conflict, not on the default. If somebody deliberately
    /// puts look back on the left stick, this moves the ZOOM, and either way the two end
    /// up separated rather than the builder insisting on its own preference.
    /// </summary>
    static void SeparateTheSticks(FirstPersonLookRig_NEW lookRig)
    {
        if (lookRig == null) return;

        // Zoom always reads the left stick. If look does too, they collide.
        if (lookRig.LookStickSetting != TutorialInput_NEW.LookStick.Left) return;

        SerializedObject rig = new SerializedObject(lookRig);
        SerializedProperty stick = rig.FindProperty("lookStick");

        if (stick == null)
        {
            Debug.LogError("[TutorialSceneBuilder_NEW] FirstPersonLookRig_NEW has no " +
                           "'lookStick' field. Look and zoom cannot be separated.", lookRig);
            return;
        }

        stick.enumValueIndex = (int)TutorialInput_NEW.LookStick.Right;
        rig.ApplyModifiedPropertiesWithoutUndo();

        _wired++;

        Debug.LogWarning("[TutorialSceneBuilder_NEW] Look and zoom were both on the LEFT " +
                         "stick, which leaves the right stick doing nothing. Look moved to " +
                         "the RIGHT stick, matching every prompt in the piece and " +
                         "PlaytestBuild. Put it back on FirstPersonLookRig_NEW if that is " +
                         "wrong — but then move the zoom too.", lookRig);
    }

    // ── Environment ──────────────────────────────────────────────────────────

    /// <summary>
    /// Post-processing, copied from PlaytestBuild_NEW's Main Camera.
    ///
    /// Same three pieces in the same arrangement: the camera on the PostProcessing layer,
    /// a PostProcessLayer on it whose volume mask is that same layer, and a global
    /// PostProcessVolume on the camera itself pointing at the profile the journey uses.
    ///
    /// The profile is shared, not copied. Bloom and grade are most of why the quasar
    /// reads as bright rather than pale, and a tutorial carrying its own grade drifts
    /// away from the journey with nobody noticing until the two are seen side by side.
    ///
    /// The skybox is deliberately untouched — assigning it is the user's, per the brief.
    /// Nothing here writes RenderSettings.
    /// </summary>
    static void ApplyPostProcessing(Camera camera)
    {
        // Already set up by an earlier run, and possibly retuned since. Leave it.
        if (camera.GetComponent<PostProcessLayer>() != null && !_freshBuild) return;

        int layer = LayerMask.NameToLayer(TutorialWorldAssets_NEW.PostProcessLayerName);

        if (layer < 0)
        {
            Debug.LogWarning("[TutorialSceneBuilder_NEW] No layer named '" +
                             TutorialWorldAssets_NEW.PostProcessLayerName +
                             "'. Skipping post-processing; add the layer and run again.");
            return;
        }

        Undo.RecordObject(camera.gameObject, "Set camera layer");
        camera.gameObject.layer = layer;

        PostProcessVolume volume = AddIfMissing<PostProcessVolume>(camera.gameObject);
        volume.isGlobal = true;
        volume.weight = 1f;
        volume.priority = 0f;

        PostProcessProfile profile =
            AssetDatabase.LoadAssetAtPath<PostProcessProfile>(TutorialWorldAssets_NEW.PostProcessProfilePath);

        if (profile != null) volume.sharedProfile = profile;
        else Debug.LogWarning("[TutorialSceneBuilder_NEW] Could not load the post-process " +
                              "profile at " + TutorialWorldAssets_NEW.PostProcessProfilePath +
                              ". The layer is set up but has no grade.");

        PostProcessLayer ppLayer = AddIfMissing<PostProcessLayer>(camera.gameObject);
        ppLayer.volumeTrigger = camera.transform;
        ppLayer.volumeLayer = 1 << layer;
        ppLayer.stopNaNPropagation = true;

        // FXAA, matching PlaytestBuild's antialiasingMode 1. Cheapest of the three,
        // which matters on the Carnegie hardware.
        ppLayer.antialiasingMode = PostProcessLayer.Antialiasing.FastApproximateAntialiasing;
    }

    /// <summary>
    /// Switch off directional lights left over from the default scene.
    ///
    /// Nothing in the tutorial is lit by one: the quasar, the dust and the trail are all
    /// additive and unlit, and the motes are Unlit/Color. A directional light only puts
    /// a daylight terminator on the placeholder geometry and makes a scene that is
    /// supposed to open in near black read as an overcast afternoon.
    /// </summary>
    static void DimSceneLights()
    {
        // Fresh build only. Somebody switching a light back on is a decision, and the
        // earlier version turned it off again on every update — silently, and with a
        // log line each time that made the run look like it had done real work.
        if (!_freshBuild) return;

        foreach (Light light in Object.FindObjectsOfType<Light>())
        {
            if (light == null || light.type != LightType.Directional) continue;
            if (!light.enabled) continue;

            Undo.RecordObject(light, "Disable directional light");
            light.enabled = false;

            Debug.Log("[TutorialSceneBuilder_NEW] Disabled the directional light on '" +
                      light.name + "'. Nothing in the tutorial is lit by one.", light);
        }
    }

    // ── World ────────────────────────────────────────────────────────────────
    //
    // GEOMETRY NOTE, and it is a real open question rather than a detail.
    //
    // A1 and A2 describe the flow and "a patch darker than black" already in frame,
    // while B3 describes the black hole and the full disc as "seen for the first time"
    // on turning around. In a first person scene with free look and no camera lock those
    // two cannot both be literally true: whatever is in the opening frame has been seen.
    //
    // The default below resolves it in B3's favour, because B3 is the beat the GDD tells
    // us to protect and A1's requirement is met by the dust either way. The quasar sits
    // behind, left and below, close enough that the player is inside the outer disc, so
    // the opening view is full of drifting matter with the core itself out of frame.
    //
    // The Figma storyboard is the authority on framing and should settle this. Every
    // position here is a constant at the top of this file and a transform in the scene.

    static GameObject BuildQuasar(Transform parent)
    {
        GameObject go = Primitive("Quasar", parent, QuasarPosition, QuasarScale);
        if (!IsFresh(go)) return go;

        go.transform.rotation = Quaternion.Euler(QuasarEuler);

        Material material = TutorialWorldAssets_NEW.BlazingQuasar;

        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        if (renderer != null && material != null) renderer.sharedMaterial = material;

        // The shader is additive with ZWrite off. Shadows on a transparent, unlit
        // object cost fill rate and produce nothing.
        if (renderer != null)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        return go;
    }

    /// <summary>
    /// Sets the constant heading and speed, and points A's recentre axis along it.
    /// </summary>
    static void BuildTravel(GameObject player, Transform quasar, FirstPersonLookRig_NEW lookRig)
    {
        TutorialTravel_NEW travel = AddIfMissing<TutorialTravel_NEW>(player);

        Wire(travel)
            .Ref("destination", quasar)
            .Ref("lookRig", lookRig)
            .Num("speed", TravelSpeed)
            .Num("holdDistance", TravelHoldDistance)
            .Apply();
    }

    /// <summary>
    /// The matter the light travels through: one particle system, parented to the player.
    ///
    /// Parented, but simulated in world space. That combination is what produces the
    /// sense of travel: new particles keep spawning around the light so the field never
    /// runs out, while the ones already spawned stay put in the world and stream past at
    /// the travel speed. Either half alone fails — world space with a fixed emitter is
    /// left behind in six seconds, local space makes a cloud that moves with the player
    /// and therefore looks completely still.
    ///
    /// One system rather than a ready-made VFX prefab on purpose. The project has
    /// RFX_Nebula prefabs, but they are galaxy looks built from twenty-odd systems each,
    /// and GDD §10 already flags frame cost on the Carnegie hardware as an open worry.
    /// </summary>
    static void BuildDust(Transform parent)
    {
        Material material = TutorialWorldAssets_NEW.DiscDustMaterial();
        if (material == null) return;

        GameObject go = FindOrCreate("Dust", parent, parent.position);
        if (!IsFresh(go)) return;


        ParticleSystem ps = AddIfMissing<ParticleSystem>(go);

        ParticleSystem.MainModule main = ps.main;
        main.duration = 20f;
        main.loop = true;

        // Short lives and a tight shell, both on purpose. Parallax is an angular rate,
        // so it comes from what is CLOSE: a particle 20 units away sweeps past at 25
        // units a second, while one 160 units away barely moves. The first version put
        // the shell at 160 and the dust may as well have been painted on.
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.7f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.55f, 0.20f, 0.12f, 1f),
            new Color(0.85f, 0.55f, 0.35f, 1f));
        main.maxParticles = 2500;
        main.playOnAwake = true;

        // World space: new dust keeps appearing around the light while what is already
        // there stays put and gets left behind. That difference is the parallax.
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        // Unscaled, so D2's future slow motion does not freeze the medium.
        main.useUnscaledTime = true;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 260f;

        // A shell around the light rather than a disc: the player is flying through the
        // medium, not orbiting inside a ring. Radius sized so most of it is near enough
        // to sweep visibly at the travel speed.
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 70f;

        // "Slow rotation", from A1. Small — the dominant motion is the travel.
        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.orbitalZ = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);

        ParticleSystem.ColorOverLifetimeModule colour = ps.colorOverLifetime;
        colour.enabled = true;
        colour.color = new ParticleSystem.MinMaxGradient(FadeInOutGradient());

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingFudge = 50f;
        }
    }

    static Gradient FadeInOutGradient()
    {
        Gradient g = new Gradient();

        g.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.25f),
                new GradientAlphaKey(1f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            });

        return g;
    }

    /// <summary>
    /// Near-field streaks. The layer that makes the travel legible.
    ///
    /// See TutorialSpeedStreaks_NEW for why this is simulated in local space with a real
    /// velocity while the dust is simulated in world space with none. In short: Stretch
    /// draws a particle along its own velocity vector, and a world-space particle sitting
    /// still while the camera flies past has a velocity of zero.
    /// </summary>
    static void BuildSpeedStreaks(Transform parent)
    {
        Material material = TutorialWorldAssets_NEW.DiscDustMaterial();
        if (material == null) return;

        GameObject go = FindOrCreate("Streaks", parent, parent.position);
        if (!IsFresh(go)) return;


        ParticleSystem ps = AddIfMissing<ParticleSystem>(go);

        ParticleSystem.MainModule main = ps.main;
        main.duration = 5f;
        main.loop = true;

        // Long enough to cross the near field once, no longer. These exist to be seen
        // going past, not to accumulate.
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.6f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.22f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.9f, 0.75f, 0.6f, 0.5f),
            new Color(1f, 0.95f, 0.9f, 0.8f));
        main.maxParticles = 400;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.useUnscaledTime = true;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 90f;

        // A hollow-ish sphere ahead of and around the light, so streaks arrive from the
        // front and sweep outwards rather than appearing beside the player.
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 26f;
        shape.radiusThickness = 0.6f;
        shape.position = new Vector3(0f, 0f, 22f);

        ParticleSystem.ColorOverLifetimeModule colour = ps.colorOverLifetime;
        colour.enabled = true;
        colour.color = new ParticleSystem.MinMaxGradient(FadeInOutGradient());

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;

            // Stretch along the particle's own velocity. This is the whole point of the
            // layer, and the reason it cannot be simulated in world space.
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.09f;
            renderer.lengthScale = 1.4f;
            renderer.cameraVelocityScale = 0f;

            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingFudge = 30f;
        }

        // Velocity comes from TutorialTravel_NEW, not from a second copy of the number.
        AddIfMissing<TutorialSpeedStreaks_NEW>(go);
    }

    /// <summary>
    /// The player's own light: a TrailRenderer with the project's PhotonTrail material
    /// and PhotonSpectrumTrail generating the spectrum gradient.
    ///
    /// This is the same pair PlaytestBuild carries on its Player, and it is the reason
    /// TutorialDrift_NEW exists. A TrailRenderer emits nothing while its transform is
    /// still, so a perfectly stationary tutorial player is a player with no visible
    /// light at all — in a piece whose entire premise is that the player IS the light.
    /// </summary>
    /// <summary>
    /// Where the trail emits from, in the player's local space.
    ///
    /// ON THE LIGHT ITSELF, which is what PlaytestBuild does too — its Trail sits at the
    /// player's origin. There the FreeLook orbits about three units behind, so the
    /// ribbon streams away in front of the camera and is the most visible thing on
    /// screen. First person has no vantage behind the light, so the same setup reads as
    /// nothing ahead and the whole ribbon behind you. That is correct: you are the
    /// light, and the light is not in front of itself.
    ///
    /// AN EARLIER VERSION PUT IT 2.6 UNITS FORWARD to make it visible while flying
    /// forward, and that is the bug. The player transform never rotates — travel only
    /// ever writes position — so a local +Z offset is a fixed WORLD offset that does not
    /// follow the course. C3 reverses the heading and the same offset silently becomes
    /// 2.6 units behind, so which side of you the ribbon starts on depends on the phase.
    /// It also put the newest segment 2.6 units from a camera with a 0.1 near plane,
    /// where turning towards it made it flicker.
    ///
    /// The drop on Y stays, and Y is the only axis this can safely use: the travel is
    /// horizontal, so a vertical offset keeps its meaning through a course reversal
    /// where an X or Z one does not. It lifts the newest quad off the eye rather than
    /// letting it degenerate exactly at the camera.
    /// </summary>
    static readonly Vector3 TrailOffset = new Vector3(0f, -0.45f, 0f);

    /// <summary>The forward offset this builder used to write. See TrailOffset.</summary>
    static readonly Vector3 PastTrailOffset = new Vector3(0f, -0.45f, 2.6f);

    static TrailRenderer BuildPhotonTrail(Transform player)
    {
        GameObject go = FindOrCreate("Trail", player, player.position);

        // Returns the renderer either way, because Phase 3's AbsorptionField_NEW needs
        // it and has no auto-find — deliberately, on its own account: reaching for a
        // type just to locate a component was the one thing tying that file to the old
        // stack. So the builder hands it over instead.
        if (!IsFresh(go))
        {
            RepairTrailOffset(go);
            return go.GetComponent<TrailRenderer>();
        }


        go.transform.localPosition = TrailOffset;

        TrailRenderer trail = AddIfMissing<TrailRenderer>(go);

        if (IsFresh(trail))
        {
            // PlaytestBuild's values: time 5, width 1, minVertexDistance 0.05.
            trail.time = 5f;
            trail.widthMultiplier = 1f;
            trail.minVertexDistance = 0.05f;
            trail.autodestruct = false;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;

            // Narrow towards the tail, so it reads as a wake rather than a ribbon.
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.15f));

            Material material = TutorialWorldAssets_NEW.PhotonTrailMaterial();
            if (material != null) trail.sharedMaterial = material;
        }

        // PhotonSpectrumTrail has [RequireComponent(typeof(TrailRenderer))], so the
        // TrailRenderer above must already be on the object.
        AddIfMissing<PhotonSpectrumTrail>(go);

        // Off at the start. The player has no trail in Phase 0–1: the piece opens on a
        // camera and a distant quasar and nothing else, and the light's own spectrum is
        // not something the player has been given a reason to care about yet.
        // TutorialEmission_NEW switches it on at C3.
        if (IsFresh(go)) go.SetActive(false);

        return trail;
    }

    /// <summary>
    /// Move a trail still emitting from 2.6 units ahead back onto the light.
    ///
    /// Same narrow test as RepairRect: only the exact offset this builder used to write
    /// is replaced, so an offset somebody has since tuned is left alone. The trail is
    /// built once and never revisited, so a new C# constant would otherwise never reach
    /// a scene that already exists.
    /// </summary>
    static void RepairTrailOffset(GameObject trail)
    {
        Vector3 local = trail.transform.localPosition;

        if (!Mathf.Approximately(local.x, PastTrailOffset.x)) return;
        if (!Mathf.Approximately(local.y, PastTrailOffset.y)) return;
        if (!Mathf.Approximately(local.z, PastTrailOffset.z)) return;

        Undo.RecordObject(trail.transform, "Repair trail offset");
        trail.transform.localPosition = TrailOffset;
        EditorUtility.SetDirty(trail.transform);

        Debug.LogWarning("[TutorialSceneBuilder_NEW] Moved the photon trail from 2.6 units " +
                         "ahead onto the light itself. The forward offset was a fixed world " +
                         "offset — the player never rotates — so C3's course reversal silently " +
                         "put it behind instead, and the ribbon changed sides mid-piece.", trail);
    }

    // ── Phases ───────────────────────────────────────────────────────────────

    static void BuildPhase0(Transform parent, GameObject moteA)
    {
        // GATED, NOT TIMED, and Beat<T> retypes a scene that still has the cinematic one.
        //
        // A1 used to run for eight seconds and hand on whether the player had touched the
        // pad or not — the same thing Beat_Zoom_NEW exists to have stopped one frame
        // later, and worse here, because this is the frame where a walk-up visitor finds
        // out whether the thing in front of them answers to them at all. It is also why
        // the opening had no stick diagram: the beat refuses to draw one on anything time
        // advances, so the frame that most needed the picture was the only one without it.
        Beat_LookAround_NEW a1 = Beat<Beat_LookAround_NEW>(parent, "A1");
        Wire(a1)
            .Str("beatId", "A1")
            .Copy("description", "Near black. Dark red matter drifts in slow rotation deep in " +
                                "frame. The player is asked to look around, and the piece waits " +
                                "until they have.")
            .Enum("hintMode", (int)TutorialBeat_NEW.HintMode.Show)
            .Copy("hintText", "RIGHT STICK  ·  LOOK AROUND")
            .Enum("advanceMode", (int)TutorialBeat_NEW.AdvanceMode.PlayerAction)
            .Num("degreesToSweep", 120f)
            .Apply();

        // The authored diagram is inert on Beat_LookAround_NEW — it works its own out from
        // how much of the sweep is left — so a value left over from when A1 was cinematic
        // is cleared rather than left showing a setting nothing reads.
        RepairSerialized(a1, "stickGuide",
                         p => p.enumValueIndex != (int)TutorialStickGuide_NEW.GuideKind.None,
                         p => p.enumValueIndex = (int)TutorialStickGuide_NEW.GuideKind.None,
                         "A1 works its own stick diagram out from its gate now.");

        // A2 and A3 are the zoom lesson, in two halves. The GDD has them as silent
        // cinematic frames; teaching the second stick here is the departure, and the
        // reason is in Beat_Zoom_NEW: a prompt nobody has to obey is not teaching, and
        // the opening was twenty seconds of being told to look around a scene with one
        // thing in it.
        Beat_Zoom_NEW a2 = Beat<Beat_Zoom_NEW>(parent, "A2");
        Wire(a2)
            .Str("beatId", "A2")
            .Copy("description", "Look closer. The left stick narrows the view and the quasar " +
                                "stops being a point — the flow reads as orbiting something, " +
                                "with a patch darker than black at the centre.")
            .Enum("hintMode", (int)TutorialBeat_NEW.HintMode.Show)
            .Copy("hintText", "LEFT STICK  ·  ZOOM IN")
            .Enum("advanceMode", (int)TutorialBeat_NEW.AdvanceMode.PlayerAction)
            .Enum("direction", (int)Beat_Zoom_NEW.Direction.In)
            .Num("threshold", 0.55f)
            .Apply();

        Beat_Zoom_NEW a3 = Beat<Beat_Zoom_NEW>(parent, "A3");
        Wire(a3)
            .Str("beatId", "A3")
            .Copy("description", "And back out. A mote drifts in at the right edge as the view " +
                                "widens, with one short controller rumble.")
            .Enum("hintMode", (int)TutorialBeat_NEW.HintMode.Show)
            .Copy("hintText", "LEFT STICK  ·  ZOOM OUT")
            .Enum("advanceMode", (int)TutorialBeat_NEW.AdvanceMode.PlayerAction)
            .Enum("direction", (int)Beat_Zoom_NEW.Direction.Out)
            .Num("threshold", 0.15f)
            .Flag("rumbleOnEnter", true)
            .Apply();

        // A3 is where the mote first appears. Nothing is on screen or in the world
        // before its own frame — a mote visible during A1 is a stray particle. It lands
        // on the beat that asks the player to widen the view, which is the moment a
        // thing at the edge of frame becomes visible at all.
        AddActivateOnEnter(a3, moteA, true);

        // A4 was the zoom lesson before it became A2 and A3. Nothing left for it to do.
        RetireBeat(parent, "A4", "the zoom lesson moved to A2 and A3");
    }

    // ── Where the look gates put their motes ─────────────────────────────────
    //
    // Degrees from the view when the beat opens: x right, y up. These replace the fixed
    // scene positions the motes used to be gated at, and the reason is the prompt. The
    // player transform never rotates, so a mote at a fixed local offset is a fixed WORLD
    // direction — and A1 lets the player look wherever they like before B1 opens. Someone
    // who ended A1 facing 60 degrees right was told LOOK RIGHT about a mote that was by
    // then on their left.
    //
    // The mote positions in the Staging block still decide where the motes sit before
    // their beat and where B3's sits throughout; these two only move theirs on enter.

    /// <summary>B1: right, and a touch up, so it clears the light's own trail.</summary>
    static readonly Vector2 B1Bearing = new Vector2(38f, 4f);

    /// <summary>B2: up, and slightly right, so it is not hidden behind the reticle on the way.</summary>
    static readonly Vector2 B2Bearing = new Vector2(8f, 40f);

    /// <summary>
    /// Switch an existing look beat over to placing its target from the view.
    ///
    /// Wire never writes a flag or a vector to a component that already exists, so the
    /// corrected behaviour would never reach a saved scene. Narrow as always: only a beat
    /// still at the shipped default of "off" is changed, and its bearing only if it is
    /// still at the field's own default.
    /// </summary>
    static void PlaceFromView(Beat_LookAt_NEW beat, Vector2 bearing, string why)
    {
        RepairSerialized(beat, "placeRelativeToView", p => !p.boolValue, p => p.boolValue = true, why);

        RepairSerialized(beat, "bearingFromView",
                         p => Approximately(p.vector2Value, new Vector2(38f, 4f)) &&
                              !Approximately(p.vector2Value, bearing),
                         p => p.vector2Value = bearing,
                         "bearing set to " + bearing + ".");
    }

    static void BuildPhase1(Transform parent, FirstPersonLookRig_NEW lookRig,
                            GameObject moteA, GameObject moteB, GameObject moteC)
    {
        Beat_LookAt_NEW b1 = Beat<Beat_LookAt_NEW>(parent, "B1");
        Wire(b1)
            .Str("beatId", "B1")
            .Copy("description", "Player turns right, catches the mote, it blooms into a ripple.")
            .Enum("hintMode", (int)TutorialBeat_NEW.HintMode.Show)
            .Copy("hintText", "RIGHT STICK  ·  LOOK RIGHT")
            .Enum("advanceMode", (int)TutorialBeat_NEW.AdvanceMode.PlayerAction)
            .Ref("target", moteA.transform)
            .Ref("mote", moteA.GetComponent<GuideMote_NEW>())
            .Ref("lookRig", lookRig)
            .Num("reticleHalfAngle", 12f)
            .Num("holdSeconds", 0f)
            .Flag("placeRelativeToView", true)
            .Vec2("bearingFromView", B1Bearing)
            .Apply();

        PlaceFromView(b1, B1Bearing, "B1 asks the player to look RIGHT, so its mote is now put to " +
                                     "the right of wherever they are looking when the beat opens.");

        // Every gated beat states its own ask.
        //
        // The storyboard writes "No new prompt" against B2 and B3, meaning the control
        // has already been taught and does not need re-teaching. But the hint line is
        // not only teaching a control, it is saying what to do next — and a line reading
        // LOOK RIGHT while the beat is waiting for the player to look up is worse than
        // no line at all. The control half stays constant, which is the part the
        // storyboard is protecting; the action half tracks the beat.
        Beat_LookAt_NEW b2 = Beat<Beat_LookAt_NEW>(parent, "B2");
        Wire(b2)
            .Str("beatId", "B2")
            .Copy("description", "The mote passes overhead. Looking up reveals the jet channel " +
                                "running into the dark, which is the direction of travel.")
            .Enum("hintMode", (int)TutorialBeat_NEW.HintMode.Show)
            .Copy("hintText", "RIGHT STICK  ·  LOOK UP")
            .Enum("advanceMode", (int)TutorialBeat_NEW.AdvanceMode.PlayerAction)
            .Ref("target", moteB.transform)
            .Ref("mote", moteB.GetComponent<GuideMote_NEW>())
            .Ref("lookRig", lookRig)
            .Num("reticleHalfAngle", 14f)
            .Flag("placeRelativeToView", true)
            .Vec2("bearingFromView", B2Bearing)
            .Apply();

        PlaceFromView(b2, B2Bearing, "B2 asks the player to look UP, so its mote is now put above " +
                                     "wherever they are looking when the beat opens.");

        // The storyboard's B3 is a third mote, behind. The turn is what the beat is
        // about; the mote is what makes a 150 degree turn something the player chooses
        // to do rather than something a prompt tells them to do.
        Beat_TurnAround_NEW b3 = Beat<Beat_TurnAround_NEW>(parent, "B3");
        Wire(b3)
            .Str("beatId", "B3")
            .Copy("description", "Third mote, behind. Turning around, the player sees what they " +
                                "are travelling away from. Spatial orientation lands here. Protect it.")
            .Enum("hintMode", (int)TutorialBeat_NEW.HintMode.Show)
            .Copy("hintText", "RIGHT STICK  ·  TURN AROUND")
            .Enum("advanceMode", (int)TutorialBeat_NEW.AdvanceMode.PlayerAction)
            .Ref("disc", moteC.transform)
            .Ref("lookRig", lookRig)
            .Num("minYawDegrees", 150f)
            .Num("discInFrameHalfAngle", 40f)
            .Apply();

        Beat_Confirm_NEW b4 = Beat<Beat_Confirm_NEW>(parent, "B4");
        Wire(b4)
            .Str("beatId", "B4")
            .Copy("description", "The view is left off-axis. The A prompt appears at the lower " +
                                "edge. One press smoothly recentres on the travel axis.")
            // The hint line switches from the look instruction to the recentre one, so
            // the two never sit on screen together. The plate says what to do; the
            // button affordance below it says which control does it.
            .Enum("hintMode", (int)TutorialBeat_NEW.HintMode.Show)
            .Copy("hintText", "A  TO  RECENTRE")
            .Enum("advanceMode", (int)TutorialBeat_NEW.AdvanceMode.PlayerAction)
            .Ref("lookRig", lookRig)
            .Copy("promptText", "RECENTRE")
            .Flag("recentreOnPress", true)
            .Flag("waitForRecentre", false)
            .Apply();

        // B2 and B3 reveal their motes when their own beat opens.
        AddActivateOnEnter(b2, moteB, true);
        AddActivateOnEnter(b3, moteC, true);
    }

    /// <summary>
    /// Phase 2 — Emission. C1 to C5, 1:00 to 1:50.
    ///
    /// No new beat types. C1 and C3 are cinematic runs, C2 and C5 are confirm beats and
    /// C4 is a look-at with the quasar as its target — the same four components Phase 1
    /// uses. What is new is TutorialEmission_NEW, wired to the beats' UnityEvents, which
    /// owns the things a gate cannot express: the speed ramp, the jitter, the course
    /// reversal and the trail switching on.
    ///
    /// The quasar is the C4 target, and by then the player has been flying away from it
    /// since C3, so "turning around, the quasar is already a single bright point" is
    /// literally what is on screen.
    /// </summary>
    static void BuildPhase2(Transform parent, FirstPersonLookRig_NEW lookRig,
                            GameObject quasar, TutorialEmission_NEW emission)
    {
        Beat_Cinematic_NEW c1 = Beat<Beat_Cinematic_NEW>(parent, "C1");
        Wire(c1)
            .Str("beatId", "C1")
            .Copy("description", "Spin-up. The disc accelerates, matter stretches into streaks, " +
                                "brightness and noise rise, audio swells.")
            .Enum("hintMode", (int)TutorialBeat_NEW.HintMode.Clear)
            .Enum("advanceMode", (int)TutorialBeat_NEW.AdvanceMode.Duration)
            .Num("duration", 12f)
            .Apply();

        Beat_Confirm_NEW c2 = Beat<Beat_Confirm_NEW>(parent, "C2");
        Wire(c2)
            .Str("beatId", "C2")
            .Copy("description", "Threshold. Near blow-out white with high frequency frame jitter. " +
                                "A single A prompt pulses at centre.")
            .Enum("hintMode", (int)TutorialBeat_NEW.HintMode.Show)
            .Copy("hintText", "A  TO  EMIT")
            .Enum("advanceMode", (int)TutorialBeat_NEW.AdvanceMode.PlayerAction)
            .Ref("lookRig", lookRig)
            .Copy("promptText", "EMIT")
            // A means emit here, not recentre. Beat_Confirm_NEW switches the rig's
            // binding off for the duration and hands it back on exit.
            .Flag("recentreOnPress", false)
            .Apply();

        Beat_Cinematic_NEW c3 = Beat<Beat_Cinematic_NEW>(parent, "C3");
        Wire(c3)
            .Str("beatId", "C3")
            .Copy("description", "Emission. One white frame, then a hard speed tunnel with matter " +
                                "streaking backwards. CRITICAL: the camera does not lock here — " +
                                "the old build did and playtesters read it as a bug.")
            .Enum("hintMode", (int)TutorialBeat_NEW.HintMode.Clear)
            .Enum("advanceMode", (int)TutorialBeat_NEW.AdvanceMode.Duration)
            .Num("duration", 8f)
            .Apply();

        Beat_LookAt_NEW c4 = Beat<Beat_LookAt_NEW>(parent, "C4");
        Wire(c4)
            .Str("beatId", "C4")
            .Copy("description", "Look back. Speed settles. Turning around, the quasar is already " +
                                "a single bright point. Reuses the B1 lesson with no new control.")
            .Enum("hintMode", (int)TutorialBeat_NEW.HintMode.Show)
            .Copy("hintText", "RIGHT STICK  ·  LOOK BACK")
            .Enum("advanceMode", (int)TutorialBeat_NEW.AdvanceMode.PlayerAction)
            .Ref("target", quasar.transform)
            .Ref("lookRig", lookRig)
            .Num("reticleHalfAngle", 18f)
            .Flag("activateTargetOnEnter", false)
            .Apply();

        Beat_Confirm_NEW c5 = Beat<Beat_Confirm_NEW>(parent, "C5");
        Wire(c5)
            .Str("beatId", "C5")
            .Copy("description", "Orient forward. Facing forward again: empty dark, with very " +
                                "faint filaments a long way ahead.")
            .Enum("hintMode", (int)TutorialBeat_NEW.HintMode.Show)
            .Copy("hintText", "A  TO  RECENTRE")
            .Enum("advanceMode", (int)TutorialBeat_NEW.AdvanceMode.PlayerAction)
            .Ref("lookRig", lookRig)
            .Copy("promptText", "RECENTRE")
            .Flag("recentreOnPress", true)
            // C5's gate is "view recentred on the forward axis", so this one does wait
            // for the lerp rather than being satisfied on the press.
            .Flag("waitForRecentre", true)
            .Apply();

        if (emission == null) return;

        AddEmissionCall(c1, emission, "BeginSpinUp");
        AddEmissionCall(c2, emission, "BeginThreshold");
        AddEmissionCall(c3, emission, "Emit");
    }

    /// <summary>Everything Phase 3's beats drive, gathered so BuildPhase3 takes one argument.</summary>
    sealed class Phase3Rig
    {
        public TutorialTrailBands_NEW trailBands;
        public TutorialSpectrumBands_NEW spectrumBands;
        public TutorialSpectrum_NEW spectrum;
        public TutorialUISlide_NEW slide;
        public TutorialAtom_NEW atom;
        public TutorialSlowMotion_NEW slowMotion;
        public TutorialAtomCluster_NEW cluster;
        public TutorialAtom_NEW forestAtom;
        public TutorialPause_NEW pause;
        public TutorialLineIndicator_NEW lineIndicator;

        /// <summary>
        /// Every object the builder wires Phase 3's beats to. A listener on a Phase 3 beat
        /// that points at one of these is the builder's, and is cleared and rewired each
        /// run; a listener pointing anywhere else is somebody's own and is left alone.
        /// </summary>
        public Object[] All()
        {
            return new Object[]
            {
                trailBands, spectrumBands, spectrum, slide, atom, slowMotion, cluster,
                forestAtom, pause, lineIndicator
            };
        }
    }

    /// <summary>
    /// Phase 3 — light, its spectrum, and what hydrogen does to it. D1 to D10.
    ///
    /// A sequence the voice-over will be laid against, so EVERY STEP IS ITS OWN BEAT
    /// with its own duration. Beat_Cinematic_NEW already carries a voice-over slot; the
    /// timing of each explanation is one number on one beat, and the debug jumps, F2
    /// and the attract restart keep working because nothing here is a coroutine.
    ///
    ///   D1   UV and IR unfold out of the trail's edges.
    ///   D2   The visible band flashes: the part of the light eyes can see.
    ///   D3   The spectrum bar appears, with UV, visible and IR marked on it.
    ///   D4   The bar moves up; an atom closes on the light.
    ///   D5   Contact, 0.2x: a line appears on the light and on the bar, and blinks.
    ///   D6   More atoms. No redshift yet, so they all absorb at the same wavelength and
    ///        the same line deepens.
    ///   D7   Redshift begins. The bands stay put; the spectrum and its line slide red.
    ///   D8   One more atom during the redshift. It absorbs at the same wavelength as
    ///        before, but the old line has moved — a second line. This is how the
    ///        Lyman-alpha forest forms.
    ///   D9   UV and IR fold away.
    ///   D10  Pause is introduced.
    ///
    /// Durations are starting values for the voice-over to replace; the builder never
    /// overwrites a duration somebody has set, except where a beat's meaning changed
    /// underneath it (see RepairPhase3Durations).
    /// </summary>
    static void BuildPhase3(Transform parent, Phase3Rig rig)
    {
        Beat_Cinematic_NEW d1 = Cinematic(parent, "D1", 4f,
            "UV and IR. The trail widens from the colours the eye can see to the whole " +
            "spectrum the light carries, with ultraviolet and infrared at its edges.");

        Beat_Cinematic_NEW d2 = Cinematic(parent, "D2", 5f,
            "The visible band flashes: this is the part of the light human eyes can see.");

        Beat_Cinematic_NEW d3 = Cinematic(parent, "D3", 5f,
            "The spectrum. The bar appears, empty, with UV, visible and IR marked on it.");

        Beat_Cinematic_NEW d4 = Cinematic(parent, "D4", 10f,
            "Approach. The bar moves up out of the way; a single hydrogen atom closes on " +
            "the light.");

        Beat_Cinematic_NEW d5 = Cinematic(parent, "D5", 8f,
            "Contact, at 0.2x. A line appears on the light and on the bar at the same " +
            "wavelength, and blinks: that colour has been absorbed.");

        // Wall clock, not stretched by the slow motion — storyboard timings are seconds
        // the player experiences. See §12.
        Wire(d5).Flag("useScaledTime", false).Apply();

        Beat_Cinematic_NEW d6 = Cinematic(parent, "D6", 10f,
            "More atoms, one after another. Every one absorbs the same wavelength — that " +
            "is what hydrogen does — but the spectrum has carried the last mark redward " +
            "by the time the next arrives, so each atom cuts its own line and a row of " +
            "them builds up. This is a forest forming.");

        Beat_Cinematic_NEW d7 = Cinematic(parent, "D7", 8f,
            "Redshift, named. The curve and the bands hold still — they are the wavelength " +
            "scale, and 500 nm is green whatever the light has been through. What the " +
            "player has been watching slide is the marks: every line the light is carrying, " +
            "moving toward red together.");

        Beat_Cinematic_NEW d8 = Cinematic(parent, "D8", 10f,
            "One more atom, with the row already on screen. It absorbs at the same " +
            "wavelength as all the others and lands at the blue end of the row, which is " +
            "where the newest line always is — the spacing is a record of the distance " +
            "between the atoms that made them.");

        Beat_Cinematic_NEW d9 = Cinematic(parent, "D9", 5f,
            "UV and IR fold away, leaving the visible light and the lines carried in it.");

        Beat_Inspect_NEW d10 = Beat<Beat_Inspect_NEW>(parent, "D10");
        Wire(d10)
            .Str("beatId", "D10")
            .Copy("description", "Pause. B TO PAUSE appears; the player pauses and resumes. Pause " +
                                "works everywhere once the tutorial is running — this is only " +
                                "where it is introduced.")
            .Enum("hintMode", (int)TutorialBeat_NEW.HintMode.Show)
            .Copy("hintText", "B  TO  PAUSE")
            // Gated on the player, both ways: paused AND resumed.
            .Enum("advanceMode", (int)TutorialBeat_NEW.AdvanceMode.PlayerAction)
            .Ref("pause", rig.pause)
            .Apply();

        // ── The hold before the ending ───────────────────────────────────────
        //
        // D10 is satisfied on the frame the player resumes, and until now the ending
        // started on that same frame: the pause card was still fading out as the camera
        // began pulling away. The last thing the player did got no room at all, and the
        // outro arrived as if it had been waiting impatiently for them to finish.
        //
        // Five seconds of the piece simply running. No hint line, nothing to do, nothing
        // switched on — the light flying, the trail carrying its lines, the spectrum
        // drifting. Whatever they were looking at when they resumed is what they get to
        // look at, which is the only moment in the tutorial that is true of.
        //
        // A beat rather than a delay inside TutorialOutro_NEW, because it IS a frame of
        // the storyboard: it has a duration, it shows in the beat list, F2 skips it, and
        // the debug overlay names it like everything else.
        Cinematic(parent, "D11", 5f,
            "Hold. Nothing is asked and nothing is shown — the light flies, the lines drift, " +
            "and the piece lets the last frame land before the ending takes the camera.");

        RepairPhase3Durations(d1, d2, d3);

        // ── Wiring ───────────────────────────────────────────────────────────
        // Cleared and rewired every run, because the frames were renumbered: D1 used to
        // arm the atom, D2 used to cut the line, D3 used to start the drift. Listeners
        // left over from that would fire the wrong effect on the wrong frame. Only
        // listeners pointing at Phase 3's own rig are touched.
        Object[] owned = rig.All();
        TutorialBeat_NEW[] beats = { d1, d2, d3, d4, d5, d6, d7, d8, d9 };

        for (int i = 0; i < beats.Length; i++)
        {
            ClearRigCalls(beats[i], "onEnter", owned);
            ClearRigCalls(beats[i], "onSatisfied", owned);
        }

        AddCall(d1, "onEnter", rig.trailBands, "UnfoldUVIR");

        AddCall(d2, "onEnter", rig.trailBands, "FlashVisible");

        // Order matters and is the order added: on screen, empty, back at its entry
        // position, bands open.
        AddCall(d3, "onEnter", rig.spectrum, "Show");
        AddCall(d3, "onEnter", rig.spectrum, "Clear");
        AddCall(d3, "onEnter", rig.slide, "ResetToStart");
        AddCall(d3, "onEnter", rig.spectrumBands, "UnfoldUVIR");

        AddCall(d4, "onEnter", rig.atom, "Arm");
        AddCall(d4, "onEnter", rig.slide, "Play");

        // The slow motion is D5's, not the atom's: hung on the impact it would start a
        // frame before the beat that owns it, which only misbehaves when the beat is
        // reached some other way — F2, a number key, or the attract restart.
        AddCall(d5, "onEnter", rig.spectrum, "AbsorbAtRestFrame");
        AddCall(d5, "onEnter", rig.slowMotion, "Enter");
        AddCall(d5, "onEnter", rig.trailBands, "BlinkLines");
        AddCall(d5, "onEnter", rig.spectrumBands, "BlinkLine");
        AddCall(d5, "onSatisfied", rig.slowMotion, "Exit");

        // Arrows at the line on the bar and on the trail, from contact until the redshift
        // frame ends — long enough to watch the line move and still be pointed at.
        AddCall(d5, "onEnter", rig.lineIndicator, "Show");

        // Each atom in the group absorbs at the rest frame on impact — see
        // BuildAtomCluster. With no drift yet, that deepens D5's line.
        AddCall(d6, "onEnter", rig.cluster, "Arm");

        // Nothing new for the drift itself: AbsorptionField_NEW slides the lines and
        // SpectrumHUD_NEW slides the curve, both from one driftPerSecond on the profile.
        AddCall(d7, "onEnter", rig.spectrum, "StartDrift");
        AddCall(d7, "onEnter", rig.trailBands, "Redden");
        AddCall(d7, "onSatisfied", rig.lineIndicator, "Hide");

        AddCall(d8, "onEnter", rig.forestAtom, "Arm");

        AddCall(d9, "onEnter", rig.trailBands, "FoldUVIR");
        AddCall(d9, "onEnter", rig.spectrumBands, "FoldUVIR");
    }

    /// <summary>One cinematic Phase 3 frame: an id, a starting duration and the storyboard words.</summary>
    static Beat_Cinematic_NEW Cinematic(Transform parent, string id, float duration, string description)
    {
        Beat_Cinematic_NEW beat = Beat<Beat_Cinematic_NEW>(parent, id);

        Wire(beat)
            .Str("beatId", id)
            .Copy("description", description)
            // Nothing to press in any of these frames, so no hint line; a line would
            // imply otherwise. The pause still puts B TO RESUME up when it is used.
            .Enum("hintMode", (int)TutorialBeat_NEW.HintMode.Clear)
            // Duration, not PlayerAction: there is no action to stand in for.
            .Enum("advanceMode", (int)TutorialBeat_NEW.AdvanceMode.Duration)
            .Num("duration", duration)
            .Apply();

        return beat;
    }

    /// <summary>
    /// D1 to D3 existed before the renumbering with different jobs and different
    /// durations — ten seconds of approach, eight of contact, eight of drift. Those
    /// numbers belong to frames that no longer exist, so where a beat still holds
    /// exactly the old value it takes the new one. Any other value is somebody's tuning
    /// and is left alone.
    /// </summary>
    static void RepairPhase3Durations(TutorialBeat_NEW d1, TutorialBeat_NEW d2, TutorialBeat_NEW d3)
    {
        RepairSerialized(d1, "duration", p => Mathf.Approximately(p.floatValue, 10f),
                         p => p.floatValue = 4f, "was the old approach frame's 10s; now UV/IR, 4s.");
        RepairSerialized(d2, "duration", p => Mathf.Approximately(p.floatValue, 8f),
                         p => p.floatValue = 5f, "was the old contact frame's 8s; now the visible flash, 5s.");
        RepairSerialized(d3, "duration", p => Mathf.Approximately(p.floatValue, 8f),
                         p => p.floatValue = 5f, "was the old drift frame's 8s; now the spectrum, 5s.");
    }

    /// <summary>
    /// Remove the listeners on one event that point at the builder's own objects, or at
    /// nothing. Listeners that point anywhere else — a voice-over cue, an audio source
    /// somebody wired by hand — are kept.
    /// </summary>
    static void ClearRigCalls(Object owner, string eventName, Object[] owned)
    {
        if (owner == null) return;

        UnityEvent e = GetEvent(owner, eventName);
        if (e == null) return;

        bool changed = false;

        for (int i = e.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            Object target = e.GetPersistentTarget(i);
            if (target != null && System.Array.IndexOf(owned, target) < 0) continue;

            UnityEventTools.RemovePersistentListener(e, i);
            changed = true;
        }

        if (changed) EditorUtility.SetDirty(owner);
    }

    /// <summary>
    /// The trail's spectrum display: UV/IR unfold, the visible flash, the line blink and
    /// the redden. On the trail itself, next to the PhotonSpectrumTrail whose band edges
    /// it reads.
    /// </summary>
    static TutorialTrailBands_NEW BuildTrailBands(TrailRenderer trail)
    {
        if (trail == null) return null;

        TutorialTrailBands_NEW bands = AddIfMissing<TutorialTrailBands_NEW>(trail.gameObject);
        PhotonSpectrumTrail spectrumTrail = trail.GetComponent<PhotonSpectrumTrail>();

        Wire(bands)
            .Ref("spectrumTrail", spectrumTrail)
            .Apply();

        // THE TRAIL MUST BE ALLOWED TO SHOW ABSORPTION LINES. PhotonSpectrumTrail writes
        // _UseAbsorptionLine from its own useAbsorptionLines flag every frame, and that flag
        // was off — so AbsorptionField_NEW switched the lines on once in Start and
        // PhotonSpectrumTrail switched them off again on the next frame, forever. The HUD
        // reads the buffer directly, which is why the bar showed the line and the light
        // did not.
        RepairSerialized(spectrumTrail, "useAbsorptionLines", p => !p.boolValue,
                         p => p.boolValue = true,
                         "absorption lines enabled on the trail — PhotonSpectrumTrail was " +
                         "turning them off every frame.");

        // THE LIGHT NEEDS ITS WHITE CAP BACK, and it went the same way the absorption
        // lines did. PhotonTrailRainbow draws the head as a bright white semicircle
        // clipped out of the leading end of the ribbon — that cap IS the light, and
        // without it the player is a ribbon that starts in mid-air at a square edge.
        //
        // The cap is drawn when _ShowHead is 1 and PhotonSpectrumTrail writes that float
        // from its own showHead flag every frame, in PushLiveParams. The flag ships off
        // (it is a per-preset choice, and the journey's presets mostly clear it), so the
        // material's own value never survives a frame no matter what it is set to.
        //
        // Off is a decision the journey gets to make per preset. In the tutorial the
        // light is the subject of the whole piece and has to have a front.
        RepairSerialized(spectrumTrail, "showHead", p => !p.boolValue,
                         p => p.boolValue = true,
                         "white head cap switched back on — PhotonSpectrumTrail was writing " +
                         "_ShowHead = 0 to the material every frame, so the light had no front.");

        // AND IT HAS TO BE TINY, which the shipped default is not — turning the cap on at
        // headWidth 0.04 whited out most of the ribbon.
        //
        // The number is a fraction of the trail's LENGTH, and the trail runs from the
        // light back past the camera. Perspective therefore spends most of the screen on
        // the near end: the first few per cent of the length is the part right in front
        // of the eye and covers far more pixels than the hundred-odd units behind it,
        // which shrink to a thread. So 4 per cent of the length is most of the picture,
        // and since the cap is additive white at 1.8 brightness on a Blend SrcAlpha One
        // shader, that is not a bright tip — it is the rainbow gone.
        //
        // The cap's shape does not change with this: it is always a semicircle spanning
        // the full width, and headWidth only says how far back along the trail it is
        // stretched. Smaller is a flatter, shorter nose, which is what a leading edge is.
        // Two passes, because the first was still too long on screen. 4% was the shipped
        // default and covered most of the ribbon; 0.5% was a big improvement and a bright
        // wedge; 0.15% is a tip.
        //
        // TO TUNE IT: Trail object → PhotonSpectrumTrail → Head Indicator → Head Width.
        // The Inspector slider runs to 0.15, so at this end type the number into the field
        // rather than dragging — one pixel of slider is about twenty times the value.
        // headBrightness is right beside it if the cap is the right size and too hot.
        RepairSerialized(spectrumTrail, "headWidth",
                         p => Mathf.Approximately(p.floatValue, 0.04f) ||
                              Mathf.Approximately(p.floatValue, 0.005f),
                         p => p.floatValue = 0.0015f,
                         "head cap shortened again, to 0.15% of the trail length. The number is a " +
                         "fraction of LENGTH and perspective spends most of the screen on the near " +
                         "end, so it has to be very small to read as a tip.");

        RepairSerialized(spectrumTrail, "headBrightness",
                         p => Mathf.Approximately(p.floatValue, 1.8f),
                         p => p.floatValue = 1f,
                         "head cap dimmed. Additive white at 1.8 blows out whatever it covers.");

        // UV, visible and IR at their share of a log-wavelength axis from 100 nm to
        // 2000 nm: UV 45%, visible 23%, IR 33%. The old 15 / 70 / 15 made the visible
        // band three times too wide — and the point of D1 is that most of the light is
        // light the eye cannot see. Both values are past the 0.4 the Inspector slider
        // allows, which is why the builder writes them.
        RepairSerialized(spectrumTrail, "uvBandWidth", p => Mathf.Approximately(p.floatValue, 0.15f),
                         p => p.floatValue = 0.45f, "UV band 0.15 -> 0.45 (log axis, 100-2000 nm).");
        RepairSerialized(spectrumTrail, "irBandWidth", p => Mathf.Approximately(p.floatValue, 0.15f),
                         p => p.floatValue = 0.33f, "IR band 0.15 -> 0.33 (log axis, 100-2000 nm).");

        // D5's blink takes the line fully out and back, not to 15%. With a strong first
        // absorption that reads as the line flashing; dimming it only part way did not.
        RepairSerialized(bands, "lineBlinkLow", p => Mathf.Approximately(p.floatValue, 0.15f),
                         p => p.floatValue = 0f, "line blink now goes fully off and back.");

        return bands;
    }

    /// <summary>
    /// UV, visible and IR marked on the spectrum bar, plus the marker that blinks over a
    /// new line. Children of the bar so they move with it.
    ///
    /// Tints are faint on purpose: they sit over the curve, and their job is to say which
    /// part of the graph is which, not to compete with it.
    /// </summary>
    static TutorialSpectrumBands_NEW BuildSpectrumBands(Transform canvas, TutorialTrailBands_NEW trailBands,
                                                        TutorialSpectrum_NEW spectrum)
    {
        GameObject bar = FindChild(canvas, "SpectrumBar");

        if (bar == null)
        {
            Debug.LogWarning("[TutorialSceneBuilder_NEW] No SpectrumBar to mark the bands on.");
            return null;
        }

        GameObject root = FullScreenUIObject("Bands", bar.transform);

        GameObject uv = FullScreenUIObject("UV", root.transform);
        GameObject visible = FullScreenUIObject("Visible", root.transform);
        GameObject ir = FullScreenUIObject("IR", root.transform);

        BandTint(uv, new Color(0.55f, 0.20f, 1.00f, 0.10f));
        Image visibleTint = BandTint(visible, new Color(1f, 1f, 1f, 0.06f));
        BandTint(ir, new Color(0.90f, 0.15f, 0.05f, 0.10f));

        BandLabel(uv, "UV");
        BandLabel(visible, "VISIBLE");
        BandLabel(ir, "IR");

        GameObject marker = UIObject("LineMarker", root.transform, new Vector2(0.5f, 0.5f),
                                     Vector2.zero, new Vector2(4f, 0f));

        Image markerImage = AddIfMissing<Image>(marker);

        if (IsFresh(markerImage))
        {
            markerImage.color = new Color(1f, 1f, 1f, 0f);
            markerImage.raycastTarget = false;
        }

        TutorialSpectrumBands_NEW bands = AddIfMissing<TutorialSpectrumBands_NEW>(root);

        Wire(bands)
            .Ref("trailBands", trailBands)
            .Ref("spectrum", spectrum)
            .Ref("uvBand", uv.GetComponent<RectTransform>())
            .Ref("visibleBand", visible.GetComponent<RectTransform>())
            .Ref("irBand", ir.GetComponent<RectTransform>())
            .Ref("uvGroup", AddIfMissing<CanvasGroup>(uv))
            .Ref("irGroup", AddIfMissing<CanvasGroup>(ir))
            .Ref("visibleTint", visibleTint)
            .Ref("lineMarker", marker.GetComponent<RectTransform>())
            .Ref("lineMarkerImage", markerImage)
            .Apply();

        return bands;
    }

    /// <summary>
    /// The two arrows at the first absorption line: under the bar, and on the trail.
    ///
    /// The bar arrow is a child of the bar's bands so it rides the bar's slide; the trail
    /// arrow is on the canvas itself, because it is placed from a world position every
    /// frame and belongs to no HUD element.
    /// </summary>
    static TutorialLineIndicator_NEW BuildLineIndicator(Transform canvas, TutorialSpectrum_NEW spectrum,
                                                        TutorialTrailBands_NEW trailBands,
                                                        TrailRenderer trail, TutorialTravel_NEW travel,
                                                        Camera camera)
    {
        GameObject bar = FindChild(canvas, "SpectrumBar");
        GameObject bands = bar != null ? FindChild(bar.transform, "Bands") : null;

        if (bands == null)
        {
            Debug.LogWarning("[TutorialSceneBuilder_NEW] No SpectrumBar/Bands for the line indicator.");
            return null;
        }

        Color arrowColor = new Color(1f, 0.93f, 0.66f, 1f);

        GameObject barArrow = UIObject("LineArrow", bands.transform, new Vector2(0.5f, 0f),
                                       new Vector2(0f, -6f), new Vector2(18f, 18f));
        IndicatorArrow(barArrow, arrowColor);

        GameObject trailArrow = UIObject("TrailLineArrow", canvas, new Vector2(0.5f, 0.5f),
                                         Vector2.zero, new Vector2(22f, 22f));
        IndicatorArrow(trailArrow, arrowColor);

        // ── Retired: the leader between the two arrows ───────────────────────
        // (See below for why the arrow is back under the bar as well.)
        // It drew a line from the arrow on the trail to the arrow on the bar, on the
        // reading that the pairing needed a connector. It does not. The two marks ARE
        // one wavelength seen twice, and the way to say that is to put them at the same
        // place across the screen and let the eye do it — a diagonal line between two
        // points that do not line up says the opposite, that they are two things needing
        // to be joined. See TutorialTrailBands_NEW for the alignment that replaces it.
        RetireObject(canvas, "LineLeader", "the two marks are aligned now, not joined by " +
                                           "a line drawn between them");

        GameObject holder = FindOrCreate("LineIndicator", canvas, canvas.position);
        TutorialLineIndicator_NEW indicator = AddIfMissing<TutorialLineIndicator_NEW>(holder);

        Wire(indicator)
            .Ref("spectrum", spectrum)
            .Ref("trailBands", trailBands)
            .Ref("trail", trail)
            .Ref("travel", travel)
            .Ref("viewCamera", camera)
            .Ref("canvasRect", canvas.GetComponent<RectTransform>())
            .Ref("barArrow", barArrow.GetComponent<RectTransform>())
            .Ref("barArrowGroup", barArrow.GetComponent<CanvasGroup>())
            .Ref("trailArrow", trailArrow.GetComponent<RectTransform>())
            .Ref("trailArrowGroup", trailArrow.GetComponent<CanvasGroup>())
            .Apply();

        // The arrow hangs under the bar again, where nothing else is, now that the bar is
        // back on the top strip. The wider gap was only there to clear the UV / VISIBLE /
        // IR labels, which overhang the TOP edge.
        RepairSerialized(indicator, "barArrowGap",
                         p => Mathf.Approximately(p.floatValue, 22f),
                         p => p.floatValue = 6f,
                         "line arrow back to a six pixel gap under the bar.");

        return indicator;
    }

    /// <summary>
    /// The pause card: a dim over the whole screen with PAUSED and B TO RESUME centred.
    ///
    /// Last on the canvas, so it sits over every other HUD element — including the hint
    /// line, the spectrum bar and the flash — while the world is stopped.
    /// </summary>
    static TutorialPauseCard_NEW BuildPauseCard(Transform canvas, TutorialPause_NEW pause)
    {
        GameObject card = FullScreenUIObject("PauseCard", canvas);

        Image dim = AddIfMissing<Image>(card);

        if (IsFresh(dim))
        {
            // Well short of black: the camera is still live behind the card.
            dim.color = new Color(0.02f, 0.02f, 0.05f, 0.55f);
            dim.raycastTarget = false;
        }

        CanvasGroup group = AddIfMissing<CanvasGroup>(card);
        if (IsFresh(group)) group.alpha = 0f;

        GameObject title = UIObject("Title", card.transform, new Vector2(0.5f, 0.5f),
                                    new Vector2(0f, 34f), new Vector2(900f, 90f));
        AddText(title, "PAUSED", 64, TextAlignmentOptions.Center);

        GameObject resume = UIObject("Resume", card.transform, new Vector2(0.5f, 0.5f),
                                     new Vector2(0f, -40f), new Vector2(900f, 50f));
        TMP_Text resumeText = AddText(resume, "B  TO  RESUME", 30, TextAlignmentOptions.Center);
        if (IsFresh(resumeText)) resumeText.color = new Color(1f, 1f, 1f, 0.8f);

        // Every run, not only when fresh: canvas elements the builder adds later in the
        // same build (the zoom gauge, anything new) would otherwise land on top of it,
        // and a pause card under the HUD cannot do its one job.
        card.transform.SetAsLastSibling();

        TutorialPauseCard_NEW component = AddIfMissing<TutorialPauseCard_NEW>(card);

        Wire(component)
            .Ref("pause", pause)
            .Ref("group", group)
            .Ref("resumeLabel", resumeText)
            .Apply();

        return component;
    }

    /// <summary>
    /// The ending: a full-screen black with the handover line on it, and the component
    /// that pulls the camera up and out before drawing either.
    ///
    /// AFTER THE PAUSE CARD in the sibling order, which is the only ordering a
    /// ScreenSpaceOverlay canvas has. The pause card deliberately covers everything else
    /// on the HUD; this has to cover that too, because a visitor could be holding the
    /// piece paused when the last gate is met and the ending would otherwise fade to
    /// black underneath a dimmer and the word PAUSED.
    ///
    /// The component lives on the rig root rather than on the canvas, because what it
    /// mostly does is drive the camera — the black and the words are the last two
    /// seconds of it.
    /// </summary>
    static TutorialOutro_NEW BuildOutro(Transform canvas, Transform root,
                                        TutorialDirector_NEW director, TutorialAttract_NEW attract,
                                        Camera camera,
                                        FirstPersonLookRig_NEW lookRig, TutorialZoom_NEW zoom,
                                        TutorialTravel_NEW travel)
    {
        GameObject card = FullScreenUIObject("Outro", canvas);

        Image black = AddIfMissing<Image>(card);

        if (IsFresh(black))
        {
            // Actually black, unlike the pause card's dim: there is nothing behind this
            // that anybody needs to see, and the words have to land on nothing.
            black.color = new Color(0f, 0f, 0f, 0f);
            black.raycastTarget = false;
        }

        GameObject titleObject = UIObject("Title", card.transform, new Vector2(0.5f, 0.5f),
                                          Vector2.zero, new Vector2(1600f, 120f));

        TMP_Text title = AddText(titleObject, "WELCOME  TO  THE  JOURNEY", 64,
                                 TextAlignmentOptions.Center);

        if (IsFresh(title)) title.color = new Color(1f, 1f, 1f, 0f);

        // Every run, not only when fresh: anything the builder adds to the canvas later
        // would otherwise land on top of the one thing that has to be the last frame.
        card.transform.SetAsLastSibling();

        GameObject go = FindOrCreate("Outro", root, Vector3.zero);
        TutorialOutro_NEW outro = AddIfMissing<TutorialOutro_NEW>(go);

        Wire(outro)
            .Ref("director", director)
            .Ref("attract", attract)
            .Ref("viewCamera", camera)
            .Ref("lookRig", lookRig)
            .Ref("zoom", zoom)
            .Ref("travel", travel)
            .Ref("blackout", black)
            .Ref("title", title)
            .Copy("titleText", "WELCOME  TO  THE  JOURNEY")
            .Apply();

        return outro;
    }

    static void IndicatorArrow(GameObject go, Color color)
    {
        Image image = AddIfMissing<Image>(go);

        if (IsFresh(image))
        {
            image.sprite = TutorialWorldAssets_NEW.ArrowSprite();
            image.color = color;
            image.raycastTarget = false;
        }

        CanvasGroup group = AddIfMissing<CanvasGroup>(go);
        if (IsFresh(group)) group.alpha = 0f;
    }

    static Image BandTint(GameObject go, Color color)
    {
        Image image = AddIfMissing<Image>(go);

        if (IsFresh(image))
        {
            image.color = color;
            image.raycastTarget = false;
        }

        return image;
    }

    static void BandLabel(GameObject band, string text)
    {
        GameObject label = UIObject("Label", band.transform, new Vector2(0.5f, 1f),
                                    new Vector2(0f, 10f), new Vector2(160f, 18f));

        TMP_Text t = AddText(label, text, 12, TextAlignmentOptions.Center);
        if (IsFresh(t)) t.color = new Color(1f, 1f, 1f, 0.55f);
    }

    /// <summary>
    /// D8's atom — one more, while the spectrum is already drifting. It absorbs at the
    /// same rest-frame wavelength as every other atom, but the earlier line has moved on,
    /// so it cuts a second, separate line. The marker blinks where the new line forms.
    ///
    /// Six seconds of approach inside a ten second frame, so by impact the first line
    /// has drifted clear and the two read as two.
    /// </summary>
    static TutorialAtom_NEW BuildForestAtom(Transform world, Transform camera, TutorialTravel_NEW travel,
                                            TutorialSpectrum_NEW spectrum,
                                            TutorialSpectrumBands_NEW spectrumBands)
    {
        TutorialAtom_NEW atom = BuildAtom(world, camera, travel, "Atom_D8");

        Wire(atom)
            .Num("spawnDistance", 160f)
            .Num("approachSeconds", 6f)
            .Apply();

        Object[] owned = { spectrum, spectrumBands };
        ClearRigCalls(atom, "onImpact", owned);

        AddCall(atom, "onImpact", spectrum, "AbsorbAtRestFrame");
        if (spectrumBands != null) AddCall(atom, "onImpact", spectrumBands, "BlinkLine");

        return atom;
    }

    // ── Phase 3 objects ──────────────────────────────────────────────────────

    /// <summary>
    /// The tutorial's spectrum: the shared absorption buffer, the bar that draws it, and
    /// the component that owns both.
    ///
    /// The buffer and the HUD are the journey's own components, unchanged. What the
    /// tutorial adds is TutorialSpectrum_NEW in front of them — the same trick
    /// TutorialSky_NEW plays on the nebula, and for the same reason: the journey drives
    /// these through LayerGate → LayerState → SpectrumResponder, and the tutorial has no
    /// layers and no gates to drive them with.
    ///
    /// The buffer is shared, so the line D2 cuts appears on the player's own photon
    /// trail as well as on the bar. That is worth more than it costs: §3 item 3 is
    /// "hitting hydrogen costs you colour", and the light visibly losing a colour is a
    /// better statement of it than a readout is.
    /// </summary>
    static TutorialSpectrum_NEW BuildSpectrum(Transform root, Transform canvas, TrailRenderer trail,
                                              out TutorialUISlide_NEW spectrumSlide)
    {
        SpectrumProfile_NEW profile = TutorialWorldAssets_NEW.TutorialSpectrumProfile;

        // ── The buffer ───────────────────────────────────────────────────────
        GameObject fieldObject = FindOrCreate("Spectrum", root, Vector3.zero);
        AbsorptionField_NEW field = AddIfMissing<AbsorptionField_NEW>(fieldObject);

        Wire(field)
            .Ref("targetTrail", trail)
            .Ref("defaultProfile", profile)
            // The tutorial has no PlayerRig_NEW, so the speed coupling has nothing to
            // read. Off explicitly rather than left to find nothing, so the Inspector
            // says what is happening.
            .Flag("linkToPlayerSpeed", false)
            .Apply();

        // ── The bar ──────────────────────────────────────────────────────────
        // Centre screen, which is where D2 puts it. D3 will move it to its docked
        // position; that is a UI animation on D3's onEnter and not this object's shape.
        //
        // SIZE IS TAKEN FROM THE JOURNEY'S BAR, which is 100x100 carrying a localScale
        // of (5, 0.5) — an effective 500 by 50. The first version here was 720 by 200,
        // four times taller, and a graph that proportion with axes and an area fill
        // reads as a slab across the middle of the screen rather than as a spectrum.
        //
        // Authored at its real size rather than copying the journey's non-uniform
        // scale: scaling a RectTransform 5x horizontally and 0.5x vertically also
        // stretches the line thickness by the same amounts, so the curve is drawn with
        // a ten-to-one anisotropic pen. That is a hack the journey can keep; a new
        // object does not need to inherit it.
        //
        // TOP CENTRE, which is this bar's permanent home. Every other element already
        // has a side of the screen: the reticle owns the middle, the range map the top
        // right, the zoom gauge the right edge, and the legend, hint and confirm prompt
        // the bottom edge.
        //
        // It sits above that bottom stack rather than at the top, which is a change —
        // see SpectrumBarPosition for the reading it buys and for the gap it has to
        // keep.
        GameObject bar = UIObject("SpectrumBar", canvas, SpectrumBarAnchor,
                                  SpectrumBarPosition, SpectrumBarSize);

        PlaceSpectrumBar(bar);

        // LineGraphRenderer_NEW is a BaseMeshEffect and requires a Graphic to rewrite.
        // It calls vh.Clear() first, so this Image never draws its own quad — it is
        // here to be a mesh source, not to be seen.
        Image barImage = AddIfMissing<Image>(bar);
        if (IsFresh(barImage)) barImage.raycastTarget = false;

        LineGraphRenderer_NEW graph = AddIfMissing<LineGraphRenderer_NEW>(bar);

        SpectrumHUD_NEW hud = AddIfMissing<SpectrumHUD_NEW>(bar);
        Wire(hud)
            // The HUD auto-finds the graph on its own object, so this is belt and
            // braces — but a reference the builder set is one the System Map can show,
            // and an auto-find is not.
            .Ref("graph", graph)
            .Ref("field", field)
            .Flag("useUnscaledTime", false)
            .Flag("driftContinuum", false)
            .Num("continuumFloor", TutorialContinuumFloor)
            .Apply();

        // THE CURVE HAS TO HAVE SOMETHING TO TAKE AWAY. Drawn at astronomical
        // proportions the template is a continuum at 0.18 of full height under a Ly-alpha
        // peak at 1.0 — on a 96 pixel bar, a 94 pixel spike standing on a seventeen pixel
        // line. That is what a quasar spectrum is and it does not read as one from the
        // back of a room; it reads as a spike on a baseline.
        //
        // It also decides how visible an absorption can be, because absorption is drawn
        // by removing height. A line taking 70% out of seventeen pixels is a smudge, and
        // that is the whole of the forest region — which is where every line in the piece
        // ends up once it has drifted off the peak.
        //
        // The floor keeps every feature and gives the flat parts height to be bitten out
        // of. The journey draws the real proportions; the tutorial is the one place the
        // bar has to be read by somebody who has never seen one.
        RepairSerialized(hud, "continuumFloor",
                         p => Mathf.Approximately(p.floatValue, 0f),
                         p => p.floatValue = TutorialContinuumFloor,
                         "continuum lifted to " + TutorialContinuumFloor + " of full height, so an " +
                         "absorption anywhere on the bar takes a bite you can see.");

        // THE CURVE HOLDS STILL AND THE LINES MOVE THROUGH IT. Turning the drift on for
        // the whole of Phase 3 slid the continuum as well as the marks, and within fifteen
        // seconds of the bar appearing the Ly-alpha peak had moved from 0.42 to 0.72 —
        // while the atoms went on cutting at the authored 0.412, which by then was flat
        // low forest. The line, the blink and the arrow pointing at it were all still
        // there and all invisible, for exactly the reason RepairLinePositions was written
        // down: a line can only be as visible as the curve it cuts into.
        //
        // The journey keeps the drifting continuum; this is the tutorial's copy.
        RepairSerialized(hud, "driftContinuum",
                         p => p.boolValue,
                         p => p.boolValue = false,
                         "the continuum holds still and the absorption lines drift through it — " +
                         "otherwise the peak slides out from under the wavelength every atom cuts.");

        // THE CURVE AND THE LINES MUST DRIFT ON THE SAME CLOCK. AbsorptionField_NEW slides
        // its lines on scaled time; SpectrumHUD_NEW slides the continuum template on
        // unscaled time by default. They agree only while the time scale is 1. Pause the
        // piece at D5 and the curve keeps scrolling under lines that have stopped — they
        // part company for good, and every line the player has collected ends up sitting
        // on the wrong part of the spectrum. D2's slow-motion ease-out already did this by
        // a few pixels.
        //
        // Scaled time on the tutorial's HUD fixes it. This is a setting on the journey's
        // component in the tutorial scene only; the journey's own HUD is untouched.
        RepairSerialized(hud, "useUnscaledTime",
                         p => p.boolValue,
                         p => p.boolValue = false,
                         "SpectrumHUD now drifts on scaled time, the same clock as the absorption " +
                         "lines — otherwise pausing D5 slid the curve out from under them.");

        // ── The tutorial's front end ─────────────────────────────────────────
        TutorialSpectrum_NEW spectrum = AddIfMissing<TutorialSpectrum_NEW>(fieldObject);

        Wire(spectrum)
            .Ref("profile", profile)
            .Ref("driftingProfile", TutorialWorldAssets_NEW.TutorialSpectrumDriftProfile())
            .Ref("field", field)
            .Ref("hud", hud)
            .Ref("barRoot", bar)
            .Apply();

        RepairLinePositions(spectrum);

        // absorbDepth was the shallow top-up every atom after the first added to one
        // shared line. There is no shared line any more — the spectrum drifts from the
        // start, so each atom cuts its own — and no first-and-later split either: every
        // atom is the same atom and does the same thing. atomAbsorbDepth is the one
        // number, and Unity carries the tuned value across through FormerlySerializedAs.
        //
        // Nothing to repair, then. Left as a note because a field vanishing from the
        // Inspector without explanation is how somebody spends an afternoon looking for it.

        // The spectrum drifts from the start now, so the D3 swap has nothing left to
        // switch on and a scene still pointing at the still profile would hold the curve
        // motionless until D7. Both profiles carry the same rate; this is the one that
        // shipped at zero.
        RepairSerialized(profile, "driftPerSecond",
                         p => Mathf.Approximately(p.floatValue, 0f),
                         p => p.floatValue = TutorialWorldAssets_NEW.DriftPerSecond,
                         "the tutorial's spectrum drifts from the moment it is on screen, " +
                         "not from D7 — so every atom's line is cut into clean spectrum.");

        // ── Retired: the enlarged spectrum on pause ──────────────────────────
        // D5 used to bring the bar to the middle of the screen, enlarged over a dimmed
        // backdrop. D5 became a plain pause, and the pause does not move the HUD, so the
        // close look and its backdrop are removed from scenes built before that.
        TutorialInspectView_NEW retiredView = bar.GetComponent<TutorialInspectView_NEW>();

        if (retiredView != null)
        {
            Debug.LogWarning("[TutorialSceneBuilder_NEW] Removed TutorialInspectView_NEW from " +
                             bar.name + ": pausing no longer enlarges the spectrum. Undo " +
                             "restores it.", bar);

            Undo.DestroyObjectImmediate(retiredView);
            _wired++;
        }

        RetireObject(canvas, "InspectBackdrop", "pausing no longer dims the screen behind an " +
                                                "enlarged spectrum");

        // ── The drift into place ─────────────────────────────────────────────
        // Built at its home position, so `to` is where it already is and `from` is the
        // only value that has to be authored. captureFromCurrent is therefore switched
        // OFF here — the object is not sitting at its entry position, it is sitting at
        // its destination.
        spectrumSlide = AddIfMissing<TutorialUISlide_NEW>(bar);

        Wire(spectrumSlide)
            .Flag("captureFromCurrent", false)
            .Vec2("from", SpectrumBarEntry)
            .Vec2("to", SpectrumBarPosition)
            .Num("seconds", 7f)
            .Num("delaySeconds", 1.5f)
            .Apply();

        // A scene that took the bar's one commit at the bottom of the frame still carries
        // that journey in its slide, so D3 would drift it back down to a dock that no
        // longer exists. Only the exact pair this builder used to write is replaced; a
        // journey somebody has tuned is left alone.
        RepairSerialized(spectrumSlide, "from",
                         p => Approximately(p.vector2Value, PastSpectrumBarEntry),
                         p => p.vector2Value = SpectrumBarEntry,
                         "spectrum bar enters from below the centre again and settles at the top.");

        RepairSerialized(spectrumSlide, "to",
                         p => Approximately(p.vector2Value, PastSpectrumBarPosition),
                         p => p.vector2Value = SpectrumBarPosition,
                         "spectrum bar docks at the top strip again.");

        // Nothing is on screen before D1. TutorialSpectrum_NEW hides it on Awake as
        // well, so a scene saved with the bar up does not flash it for a frame.
        if (IsFresh(bar)) bar.SetActive(false);

        return spectrum;
    }

    /// <summary>
    /// Where the spectrum bar lives, anchored to the top edge of the canvas.
    ///
    /// Every other element has a side: the reticle owns the middle, the range map the
    /// top right, the zoom gauge the right edge, and the legend, hint and A prompt the
    /// bottom. The top strip is the only place a readout can live without moving
    /// something the player has already learned to find.
    ///
    /// IT WAS TRIED AT THE BOTTOM, on the reasoning that a readout of the light belongs
    /// near the light. It does not pay: the bottom already carries the three things the
    /// player is told to act on, and the bar is the one thing they are asked to read.
    /// Putting it in among them buys a few hundred pixels of proximity and costs the
    /// separation between "do this" and "look at this".
    /// </summary>
    static readonly Vector2 SpectrumBarPosition = new Vector2(0f, -78f);

    /// <summary>
    /// Where it comes in from at D1 — lower and nearer the middle, so the drift upward
    /// is a real movement rather than a nudge.
    ///
    /// Still anchored to the top edge, so this is 420 pixels down from it: below the
    /// centre line on a 1080 canvas, clear of the reticle, and plainly not where a HUD
    /// element belongs. Ending up at the top is what makes it read as having found its
    /// place rather than as having been put there.
    /// </summary>
    static readonly Vector2 SpectrumBarEntry = new Vector2(0f, -420f);

    /// <summary>The bottom-edge dock and entry this builder wrote for one commit.</summary>
    static readonly Vector2 PastSpectrumBarPosition = new Vector2(0f, 360f);
    static readonly Vector2 PastSpectrumBarEntry = new Vector2(0f, 700f);

    /// <summary>
    /// How big it is. Wide and shallow, because a spectrum is read left to right and its
    /// height carries only one number.
    ///
    /// The journey's bar is 100x100 carrying a localScale of (5, 0.5) — an effective 500
    /// by 50. This is authored at its real size instead: scaling a RectTransform 5x
    /// horizontally and 0.5x vertically stretches the drawn line thickness by the same
    /// amounts, so the curve comes out of a ten-to-one anisotropic pen. That is a hack
    /// the journey can keep and a new object does not need to inherit.
    /// </summary>
    static readonly Vector2 SpectrumBarSize = new Vector2(720f, 96f);

    /// <summary>
    /// Placements this builder has authored in the past, newest first.
    ///
    /// A bar sitting at any of these was put there by the builder and nobody has touched
    /// it since, so it is safe to move to wherever the current placement says. A bar
    /// anywhere else is somebody's decision and is left alone.
    ///
    /// Each entry is position then size.
    /// </summary>
    static readonly Vector2[] PastSpectrumBarPlacements =
    {
        PastSpectrumBarPosition, SpectrumBarSize,         // bottom edge, fourth guess
        new Vector2(0f, -90f),  new Vector2(560f, 90f),   // below centre, second guess
        new Vector2(0f, -40f),  new Vector2(720f, 200f)   // centre slab, first guess
    };

    /// <summary>
    /// Move a spectrum bar the builder placed badly to where it belongs now.
    ///
    /// UIObject never touches an object that already exists, which is right almost
    /// always: somebody who moved or resized a HUD element made a decision, and the
    /// builder does not undo decisions. This is the exception, and the test is narrow
    /// enough to keep it one — neither of the past placements was a decision anybody
    /// made, they were the builder's own guesses, and both put the bar over the middle
    /// of the screen where the reticle lives.
    ///
    /// Anything not on that list is left exactly as it is, including a placement
    /// somebody has since tuned.
    ///
    /// Same shape as RetireBeat and SeparateTheSticks: the builder can repair what the
    /// builder got wrong, because a new C# constant does not reach a scene that has
    /// already been saved.
    /// </summary>
    static void PlaceSpectrumBar(GameObject bar)
    {
        RepairRect(bar, SpectrumBarAnchor, SpectrumBarPosition, SpectrumBarSize,
                   "SpectrumBar", PastSpectrumBarPlacements);
    }

    /// <summary>Top edge of the canvas. See SpectrumBarPosition.</summary>
    static readonly Vector2 SpectrumBarAnchor = new Vector2(0.5f, 1f);

    /// <summary>
    /// How far off the floor the tutorial lifts the continuum, as a fraction of the bar's
    /// height.
    ///
    /// 0.35 puts the forest region at about 40% of the bar and the peak near the top, so
    /// an absorption of 0.7 takes roughly a third of the bar's height out wherever it
    /// lands. Raise it and the curve flattens towards a block; drop it and the forest
    /// goes back to being a line with nothing to remove.
    /// </summary>
    const float TutorialContinuumFloor = 0.35f;

    /// <summary>
    /// Move a rect the builder placed badly to where it belongs now — but only if it is
    /// still sitting exactly where an earlier version of this builder put it.
    ///
    /// UIObject never touches an object that already exists, which is right almost
    /// always: somebody who moved or resized a HUD element made a decision, and the
    /// builder does not undo decisions. This is the exception. A rect still carrying a
    /// placement this file used to write was not a decision anybody made — it is the
    /// builder's own earlier output, reaching a scene that a new C# constant cannot.
    ///
    /// A rect at any other value is left exactly as it is, including one somebody has
    /// since tuned. The test is equality against a written-down list, so the exception
    /// cannot widen by accident.
    ///
    /// `past` is position then size, repeating.
    /// </summary>
    static void RepairRect(GameObject go, Vector2 anchor, Vector2 position, Vector2 size,
                           string what, params Vector2[] past)
    {
        RectTransform rect = go != null ? go.GetComponent<RectTransform>() : null;
        if (rect == null) return;

        for (int i = 0; i + 1 < past.Length; i += 2)
        {
            if (!Approximately(rect.anchoredPosition, past[i])) continue;
            if (!Approximately(rect.sizeDelta, past[i + 1])) continue;

            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            EditorUtility.SetDirty(rect);

            Debug.LogWarning("[TutorialSceneBuilder_NEW] Repaired " + what + ": moved from " +
                             past[i] + " " + past[i + 1] + " to " + position + " " + size +
                             ". It was still at a placement this builder authored earlier.", go);
            return;
        }
    }

    static bool Approximately(Vector2 a, Vector2 b)
    {
        return Mathf.Approximately(a.x, b.x) && Mathf.Approximately(a.y, b.y);
    }

    /// <summary>The line positions this builder shipped before, and what replaces them.</summary>
    static readonly float[] FirstLinePositions = { 0.30f, 0.22f, 0.36f, 0.15f, 0.39f };
    static readonly float[] CurrentLinePositions = { 0.412f, 0.30f, 0.225f, 0.355f, 0.15f };

    /// <summary>
    /// Move D2's line onto the visible part of the curve.
    ///
    /// The first set put every line blueward in the forest, which is where a real
    /// Lyman-alpha absorber lives and is astronomically right. It is also, on this
    /// continuum, invisible: the template runs at 0.18 of full height below the peak and
    /// 1.0 at it, so cutting 80% out of the forest region removes nine pixels from an
    /// eleven pixel curve. D2 cut its line and the screen did not change.
    ///
    /// Same reasoning as RepairRect, and the same narrow test: only the exact array this
    /// builder used to write is replaced. A set somebody has tuned is left alone.
    /// </summary>
    static void RepairLinePositions(TutorialSpectrum_NEW spectrum)
    {
        if (spectrum == null) return;

        SerializedObject so = new SerializedObject(spectrum);
        SerializedProperty positions = so.FindProperty("linePositions");

        if (positions == null || !positions.isArray) return;
        if (positions.arraySize != FirstLinePositions.Length) return;

        for (int i = 0; i < FirstLinePositions.Length; i++)
            if (!Mathf.Approximately(positions.GetArrayElementAtIndex(i).floatValue, FirstLinePositions[i]))
                return;

        for (int i = 0; i < CurrentLinePositions.Length; i++)
            positions.GetArrayElementAtIndex(i).floatValue = CurrentLinePositions[i];

        so.ApplyModifiedProperties();

        Debug.LogWarning("[TutorialSceneBuilder_NEW] Moved D2's absorption line from 0.30 to " +
                         "0.412, onto the bright flank of the Ly-alpha peak. At 0.30 the " +
                         "continuum is 0.18 of full height, so the line was cut into eleven " +
                         "pixels of curve and could not be seen.", spectrum);
    }

    /// <summary>
    /// Make the atom see-through, on a material generated while it was opaque.
    ///
    /// AtomCoreMaterial only generates the asset when it is missing, which is right —
    /// it is a material somebody may have tuned — but it means a corrected default never
    /// reaches a project that already has one. This is the same exception RepairRect
    /// makes, and the same narrow test: only a material still on Unlit/Color, which is
    /// what this builder used to write and what nobody would choose for a translucent
    /// atom, is changed.
    ///
    /// Run once per build rather than per atom, so three atoms do not log it three times.
    /// </summary>
    static void RepairAtomCoreTranslucency()
    {
        Material core = TutorialWorldAssets_NEW.AtomCoreMaterial();
        if (core == null || core.shader == null) return;
        if (core.shader.name != "Unlit/Color") return;

        Shader replacement = TutorialWorldAssets_NEW.AlphaBlendedParticleShader();
        if (replacement == null) return;

        Undo.RecordObject(core, "Repair atom core translucency");
        core.shader = replacement;
        TutorialWorldAssets_NEW.ApplyAtomCoreLook(core);
        EditorUtility.SetDirty(core);
        AssetDatabase.SaveAssets();

        Debug.LogWarning("[TutorialSceneBuilder_NEW] The hydrogen atom's core is translucent " +
                         "now. It was an opaque near-white sphere, which read as a headlight " +
                         "and punched a hole in whatever was behind it — the light is " +
                         "supposed to be passing THROUGH the atom.", core);
    }

    /// <summary>
    /// Take the glare off the halo, on a material generated while it was bright.
    ///
    /// Same reasoning and the same narrow test as RepairAtomCoreTranslucency: only the
    /// exact tint this builder used to write is replaced.
    /// </summary>
    static readonly Color PastAtomHaloTint = new Color(0.38f, 0.62f, 0.92f, 0.5f);
    static readonly Color AtomHaloTint = new Color(0.22f, 0.34f, 0.52f, 0.30f);

    static void RepairAtomHaloBrightness()
    {
        Material halo = TutorialWorldAssets_NEW.AtomHaloMaterial();
        if (halo == null || !halo.HasProperty("_TintColor")) return;

        Color tint = halo.GetColor("_TintColor");

        if (!Approximately(tint, PastAtomHaloTint)) return;

        Undo.RecordObject(halo, "Repair atom halo brightness");
        halo.SetColor("_TintColor", AtomHaloTint);
        EditorUtility.SetDirty(halo);
        AssetDatabase.SaveAssets();

        Debug.LogWarning("[TutorialSceneBuilder_NEW] Dimmed the atom's halo. Additive stacks, " +
                         "and it overlaps itself at the centre where the core already is.", halo);
    }

    static bool Approximately(Color a, Color b)
    {
        return Mathf.Approximately(a.r, b.r) && Mathf.Approximately(a.g, b.g) &&
               Mathf.Approximately(a.b, b.b) && Mathf.Approximately(a.a, b.a);
    }

    /// <summary>
    /// Give an atom built before the halo existed its own core material.
    ///
    /// The first version of this atom borrowed the guide motes' warm yellow, because it
    /// was a whitebox sphere and the motes already had one. The halo that arrived later
    /// is cold blue, and an atom built by the earlier run keeps the mote material — so
    /// it comes in as a yellow core inside a blue glow, which is neither of the two
    /// things it is meant to be.
    ///
    /// Narrow like the other repairs: only a renderer still carrying the shared mote
    /// material is changed. Anything else is somebody's choice.
    /// </summary>
    static void RepairAtomCoreMaterial(MeshRenderer renderer)
    {
        if (renderer == null) return;

        Material mote = TutorialWorldAssets_NEW.MoteMaterial();
        if (mote == null || renderer.sharedMaterial != mote) return;

        Material core = TutorialWorldAssets_NEW.AtomCoreMaterial();
        if (core == null) return;

        Undo.RecordObject(renderer, "Repair atom core material");
        renderer.sharedMaterial = core;
        EditorUtility.SetDirty(renderer);

        Debug.LogWarning("[TutorialSceneBuilder_NEW] Gave the atom its own core material. It " +
                         "was still using the guide motes' warm yellow, which reads as a " +
                         "fourth mote inside a cold halo — and the atom is the first thing " +
                         "in the piece that is not a guide.", renderer);
    }

    /// <summary>
    /// D2's time scale, on its own object.
    ///
    /// Its own object rather than a component on the director, because it restores the
    /// time scale in OnDisable and OnDestroy — and something that has to run on teardown
    /// should not share a lifetime with the thing that drives the whole piece.
    /// </summary>
    static TutorialSlowMotion_NEW BuildSlowMotion(Transform root)
    {
        GameObject go = FindOrCreate("SlowMotion", root, Vector3.zero);
        TutorialSlowMotion_NEW slowMotion = AddIfMissing<TutorialSlowMotion_NEW>(go);

        // D5 became a pause. The first freeze value was 0.01 — near-still, chosen on a
        // mistaken belief that a time scale of 0 stops Update. It does not; only
        // FixedUpdate stops. A pause should be a pause.
        RepairSerialized(slowMotion, "freezeScale",
                         p => Mathf.Approximately(p.floatValue, 0.01f),
                         p => p.floatValue = 0f,
                         "D5's freeze is now a true pause (time scale 0, was 0.01).");

        return slowMotion;
    }

    /// <summary>
    /// B pauses and resumes the piece, everywhere once it is running.
    ///
    /// On its own object, like the slow motion, because it releases the world clock and
    /// the time scale on disable — and something that has to run on teardown should not
    /// share a lifetime with the thing that drives the whole piece.
    /// </summary>
    static TutorialPause_NEW BuildPause(Transform root, TutorialDirector_NEW director,
                                        TutorialSlowMotion_NEW slowMotion)
    {
        GameObject go = FindOrCreate("Pause", root, Vector3.zero);
        TutorialPause_NEW pause = AddIfMissing<TutorialPause_NEW>(go);

        Wire(pause)
            .Ref("director", director)
            .Ref("slowMotion", slowMotion)
            .Copy("resumeHintText", "B  TO  RESUME")
            .Apply();

        return pause;
    }

    /// <summary>
    /// Change one serialized value on an existing component, but only if it still holds
    /// what this builder used to write.
    ///
    /// The generic form of RepairLinePositions and RepairTrailOffset: Wire only writes to
    /// components this run created, so a corrected default never reaches a scene that
    /// already exists. isStale decides whether the current value is the builder's old
    /// output — anything else is somebody's decision and is left alone.
    /// </summary>
    static void RepairSerialized(Object target, string path,
                                 System.Func<SerializedProperty, bool> isStale,
                                 System.Action<SerializedProperty> fix, string message)
    {
        if (target == null) return;

        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(path);

        if (p == null || !isStale(p)) return;

        fix(p);
        so.ApplyModifiedProperties();

        Debug.LogWarning("[TutorialSceneBuilder_NEW] " + target.name + ": " + message, target);
    }

    /// <summary>
    /// The single hydrogen atom of D1 and D2.
    ///
    /// Parented to the world, not to the light: it has to close the distance, and a
    /// child of the player would ride along and never arrive. TutorialAtom_NEW places it
    /// at Arm() using the travel heading, so its position here is only where it sits
    /// before D1 opens.
    ///
    /// A whitebox sphere with a point light. Replacing the mesh and the material is all
    /// the real asset needs; the builder does not have to change.
    /// </summary>
    /// <summary>
    /// D4's group. Four atoms, spread across the course, arriving one after another.
    ///
    /// Each one is the same TutorialAtom_NEW as D1's, so the cluster owns only the
    /// timing and the spread. Each atom's onImpact cuts a line, which is why the group
    /// leaves three or four notches without anything counting them.
    ///
    /// Spread in metres across the course, not in world axes — see spawnSpread. They
    /// come from different parts of the frame and all arrive at the light, because §6
    /// asks for a group the player passes THROUGH and not one they could miss.
    /// </summary>
    static readonly Vector2[] ClusterSpread =
    {
        new Vector2(-14f,   5f),
        new Vector2( 11f,  -7f),
        new Vector2( -6f, -11f),
        new Vector2( 17f,   8f)
    };

    static TutorialAtomCluster_NEW BuildAtomCluster(Transform world, Transform camera,
                                                    TutorialTravel_NEW travel,
                                                    TutorialSpectrum_NEW spectrum)
    {
        GameObject root = FindOrCreate("Cluster_D4", world, Vector3.zero);
        TutorialAtomCluster_NEW cluster = AddIfMissing<TutorialAtomCluster_NEW>(root);

        for (int i = 0; i < ClusterSpread.Length; i++)
        {
            TutorialAtom_NEW atom = BuildAtom(root.transform, camera, travel, "Atom_D4_" + (i + 1));

            Wire(atom)
                .Vec2("spawnSpread", ClusterSpread[i])
                // Nearer and quicker than D1's. D1 is the frame where one atom arriving
                // is the whole event and it gets ten seconds; here four of them share
                // the frame and each one only has to be seen coming.
                .Num("spawnDistance", 140f)
                .Num("approachSeconds", 4.5f)
                .Apply();

            // The atom does not know what a spectrum is. Every one absorbs at the rest
            // frame: with no redshift yet, D6's group deepens the one line D5 cut rather
            // than scattering new ones — the old CutLine wiring put each at a different
            // wavelength, which no hydrogen at one redshift can do.
            ClearRigCalls(atom, "onImpact", new Object[] { spectrum });
            AddCall(atom, "onImpact", spectrum, "AbsorbAtRestFrame");
        }

        return cluster;
    }

    static TutorialAtom_NEW BuildAtom(Transform world, Transform camera, TutorialTravel_NEW travel)
    {
        return BuildAtom(world, camera, travel, "Atom_D1");
    }

    static TutorialAtom_NEW BuildAtom(Transform world, Transform camera, TutorialTravel_NEW travel,
                                      string name)
    {
        // A hard core inside a soft halo. The core gives it an edge so it still reads as
        // an object at 200 metres; the halo is what makes it read as light rather than
        // as a grey ball. Cold white against the motes' warm yellow, because this is the
        // first thing in the piece that is not a guide.
        GameObject go = Primitive(name, world, Vector3.zero, Vector3.one * 0.5f);

        MeshRenderer renderer = go.GetComponent<MeshRenderer>();

        if (IsFresh(go) && renderer != null)
        {
            Material material = TutorialWorldAssets_NEW.AtomCoreMaterial();
            if (material != null) renderer.sharedMaterial = material;

            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        else
        {
            RepairAtomCoreMaterial(renderer);
        }

        GameObject halo = FindOrCreate("Halo", go.transform, Vector3.zero);

        if (IsFresh(halo))
        {
            // A quad rather than a second sphere: it is billboarded at the camera every
            // frame by TutorialAtom_NEW, so it never needs a third dimension.
            GameObject source = GameObject.CreatePrimitive(PrimitiveType.Quad);
            AddIfMissing<MeshFilter>(halo).sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(source);

            MeshRenderer haloRenderer = AddIfMissing<MeshRenderer>(halo);
            Material haloMaterial = TutorialWorldAssets_NEW.AtomHaloMaterial();
            if (haloMaterial != null) haloRenderer.sharedMaterial = haloMaterial;

            haloRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            haloRenderer.receiveShadows = false;
        }

        GameObject glowObject = FindOrCreate("Light", go.transform, Vector3.zero);
        Light glow = AddIfMissing<Light>(glowObject);

        if (IsFresh(glow))
        {
            glow.type = LightType.Point;
            glow.range = 40f;
            glow.shadows = LightShadows.None;

            // Cooler than a mote's warm yellow. Hydrogen is not one of the guides, and
            // the player should not be waiting for it to bloom the way B1's mote did.
            glow.color = new Color(0.72f, 0.86f, 1f);
        }

        TutorialAtom_NEW atom = AddIfMissing<TutorialAtom_NEW>(go);

        Wire(atom)
            .Ref("viewCamera", camera)
            .Ref("travel", travel)
            .Ref("glow", glow)
            .Ref("halo", halo.transform)
            .Apply();

        // The third thing making the atom a headlight, after the opaque core and the
        // halo: a point light running to 6 at impact, in a scene whose only other light
        // is a quasar 900 units away. It lit the player's own trail from the side.
        //
        // Still there, and still growing with the approach — the atom has to look like
        // it is arriving — just at a level that reads as a glow rather than a flash.
        RepairSerialized(atom, "glowAtImpact",
                         p => Mathf.Approximately(p.floatValue, 6f),
                         p => p.floatValue = 2.2f,
                         "atom's light dimmed at impact. It was brighter than anything else " +
                         "in the frame.");

        RepairSerialized(atom, "glowAtSpawn",
                         p => Mathf.Approximately(p.floatValue, 0.4f),
                         p => p.floatValue = 0.15f,
                         "atom's light dimmed at spawn, to keep the growth through the approach.");

        // Nothing is on screen before D1. TutorialAtom_NEW also hides itself on Awake.
        if (IsFresh(go)) go.SetActive(false);

        return atom;
    }

    // ── Object helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// The beat object for this storyboard frame, created if it is not there yet, and
    /// placed in sequence rather than at the end of the list.
    ///
    /// Beats are children of the director and run in Hierarchy order. FindOrCreate
    /// appends, which is right for a whole new phase arriving after the existing ones,
    /// and wrong for a frame inserted into the middle — A4 was added between A3 and B1
    /// and landed after C5, so the zoom lesson ran at the very end of the piece where
    /// nobody would ever see it.
    ///
    /// So a beat this run created is moved to sit directly after the previous one the
    /// builder walked past. The builder walks them in storyboard order, so that is the
    /// right slot by construction. A beat that already existed is never moved: somebody
    /// reordering the Hierarchy is making a decision, and the builder does not undo
    /// decisions.
    /// </summary>
    static T Beat<T>(Transform parent, string name) where T : TutorialBeat_NEW
    {
        GameObject go = FindOrCreate(name, parent, Vector3.zero);

        RetypeBeat<T>(go);
        T beat = AddIfMissing<T>(go);

        if (IsFresh(go) && _beatCursor != null && _beatCursor.parent == go.transform.parent)
            go.transform.SetSiblingIndex(_beatCursor.GetSiblingIndex() + 1);

        _beatCursor = go.transform;
        return beat;
    }

    /// <summary>
    /// A storyboard frame that changed kind — A2 and A3 went from cinematic runs to the
    /// two halves of the zoom lesson.
    ///
    /// This has to be explicit because TutorialBeat_NEW is DisallowMultipleComponent:
    /// adding Beat_Zoom_NEW to an object that already has Beat_Cinematic_NEW does not
    /// stack them, it fails, and the run would carry on writing values into a component
    /// that is not the one running. So the old one goes first.
    ///
    /// It is a deletion, so it is loud. Anything hooked onto the old component's own
    /// UnityEvents — a VO clip on a cinematic beat, a listener somebody added by hand —
    /// goes with it; what the builder put there it puts back on the next few lines.
    /// Values and events on the GameObject's other components are untouched.
    /// </summary>
    static void RetypeBeat<T>(GameObject go) where T : TutorialBeat_NEW
    {
        TutorialBeat_NEW existing = go.GetComponent<TutorialBeat_NEW>();

        if (existing == null) return;
        if (existing is T) return;

        Debug.LogWarning("[TutorialSceneBuilder_NEW] " + go.name + " was a " +
                         existing.GetType().Name + " and is now a " + typeof(T).Name +
                         ". Anything wired onto the old component itself is gone — the " +
                         "builder re-wires what it put there. Undo restores it.", go);

        Undo.DestroyObjectImmediate(existing);
        _wired++;
    }

    /// <summary>
    /// A beat that no longer has a job. Deleted, with the reason in the Console.
    ///
    /// The builder does not otherwise delete anything, and this is the narrow exception:
    /// a beat left behind after its content moved elsewhere is not somebody's edit, it
    /// is a gate the player has to satisfy twice. A4 asked for a zoom the player had
    /// already done in A2 and A3, so it would have sat there looking broken.
    ///
    /// Guarded on the type, so an object that happens to share the name but is somebody
    /// else's work survives.
    /// </summary>
    static void RetireBeat(Transform parent, string name, string why)
    {
        GameObject go = FindChild(parent, name);
        if (go == null) return;

        if (go.GetComponent<TutorialBeat_NEW>() == null)
        {
            Debug.LogWarning("[TutorialSceneBuilder_NEW] " + name + " should have been " +
                             "retired (" + why + ") but it is not a beat any more. " +
                             "Left alone.", go);
            return;
        }

        Debug.LogWarning("[TutorialSceneBuilder_NEW] Retired the beat " + name + ": " +
                         why + ". Undo brings it back.", parent);

        Undo.DestroyObjectImmediate(go);
        _wired++;
    }

    // ── Find or create ───────────────────────────────────────────────────────
    //
    // Everything the builder makes goes through these three, and they are what make a
    // second run safe. An object that is already there is returned untouched; only
    // objects this run created are marked fresh, and only fresh things get configured.

    /// <summary>
    /// The named child of `parent`, or a new one. Matching is by name and parent, which
    /// is why every object the builder makes has a fixed name.
    /// </summary>
    static GameObject FindOrCreate(string name, Transform parent, Vector3 position)
    {
        GameObject existing = parent != null ? FindChild(parent, name) : FindSceneRoot(name);
        if (existing != null) return existing;

        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);

        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = position;

        MarkFresh(go);
        return go;
    }

    /// <summary>
    /// Sprite, colour and size on one HUD image.
    ///
    /// Normally this only fires on an element the run just made, because a colour or a
    /// swapped sprite is exactly the kind of thing somebody tunes and the builder has no
    /// business overwriting. `redesign` is the escape hatch, and the only caller passing
    /// it true is the map, on the one build that brings it up from the round version — a
    /// design that changed shape cannot be half applied, or the result is neither.
    ///
    /// `sliced` is for the 9-sliced chip, which is the only sprite here that stretches.
    /// </summary>
    static Image Paint(GameObject go, Sprite sprite, Color color, Vector2 size,
                       bool sliced, bool redesign)
    {
        Image image = AddIfMissing<Image>(go);

        if (!IsFresh(image) && !redesign) return image;

        Undo.RecordObject(image, "Style the HUD");

        image.sprite = sprite;
        image.color = color;
        image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        image.raycastTarget = false;

        RectTransform rect = (RectTransform)go.transform;
        Undo.RecordObject(rect, "Style the HUD");
        rect.sizeDelta = size;

        return image;
    }

    /// <summary>
    /// Take the image off an object that has stopped being something you can see.
    ///
    /// The map's Frame used to be the dark circle; it is now nothing but the box the
    /// markers are positioned inside, and leaving the old disc on it would draw a circle
    /// on top of the new plate.
    /// </summary>
    static void Unpaint(GameObject go, bool redesign)
    {
        if (!redesign) return;

        Image image = go.GetComponent<Image>();
        if (image == null) return;

        Debug.LogWarning("[TutorialSceneBuilder_NEW] Removed the image from " + go.name +
                         ": it is a layout box now, not something drawn. Undo restores it.", go);

        Undo.DestroyObjectImmediate(image);
        _wired++;
    }

    /// <summary>
    /// An object the design left behind. Same narrow exception as RetireBeat: it is not
    /// somebody's edit, it is a leftover that draws over the thing that replaced it.
    /// </summary>
    static void RetireObject(Transform parent, string name, string why)
    {
        GameObject go = FindChild(parent, name);
        if (go == null) return;

        Debug.LogWarning("[TutorialSceneBuilder_NEW] Retired " + name + ": " + why +
                         ". Undo brings it back.", parent);

        Undo.DestroyObjectImmediate(go);
        _wired++;
    }

    static GameObject FindChild(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        return t != null ? t.gameObject : null;
    }

    static GameObject FindSceneRoot(string name)
    {
        return GameObject.Find(name);
    }

    /// <summary>
    /// Add the component only if the object does not already have one.
    ///
    /// This is the one place in the file that may call Undo.AddComponent. Everywhere
    /// else goes through here, so that "was this component added by this run?" has a
    /// single answer — see MarkFresh.
    /// </summary>
    static T AddIfMissing<T>(GameObject go) where T : Component
    {
        T existing = go.GetComponent<T>();
        if (existing != null) return existing;

        T added = Undo.AddComponent<T>(go);
        MarkFresh(added);
        return added;
    }

    static void MarkFresh(Object o)
    {
        if (o == null) return;

        _fresh.Add(o.GetInstanceID());
        _created++;
    }

    /// <summary>
    /// Did this run create it? Anything older is somebody's work and stays as it is.
    /// A rebuild treats everything as fresh, because a rebuild deleted the old rig first.
    /// </summary>
    static bool IsFresh(Object o)
    {
        if (_freshBuild) return true;
        return o != null && _fresh.Contains(o.GetInstanceID());
    }

    /// <summary>
    /// Use the scene's existing Main Camera if there is one, rather than adding a second
    /// enabled camera and leaving the user to work out why the view is wrong.
    /// </summary>
    static Camera AcquireCamera(Transform parent)
    {
        // Already under the rig from an earlier run. Its transform is somebody's — an
        // offset eye height, a tilt — so do not touch it. The earlier version reparented
        // and zeroed on every run, which quietly undid exactly that.
        // includeInactive: a camera somebody switched off is still theirs, and missing it
        // here would build a second one alongside it.
        Camera underRig = parent.GetComponentInChildren<Camera>(true);
        if (underRig != null) return Configure(underRig);

        Camera main = Camera.main;

        if (main != null)
        {
            // Taking over the scene's camera. This happens once, on the first build.
            Undo.SetTransformParent(main.transform, parent, "Reparent camera");

            main.transform.localPosition = Vector3.zero;
            main.transform.localRotation = Quaternion.identity;

            return Configure(main);
        }

        GameObject go = FindOrCreate("Camera", parent, parent.position);
        go.tag = "MainCamera";

        Camera created = AddIfMissing<Camera>(go);
        AddIfMissing<AudioListener>(go);

        return Configure(created);
    }

    /// <summary>
    /// FOV, clear flags and clip planes. Fresh build only — these are exactly the kind
    /// of thing somebody retunes, and a second run should not put them back.
    /// </summary>
    static Camera Configure(Camera camera)
    {
        if (!_freshBuild) return camera;

        Undo.RecordObject(camera, "Configure camera");

        camera.clearFlags = CameraClearFlags.Skybox;
        camera.nearClipPlane = 0.1f;

        // Copied from PlaytestBuild_NEW's FreeLook rigs. Unity's default 60 makes the
        // same stick speed feel different and frames the quasar differently.
        camera.fieldOfView = FieldOfView;

        // The quasar starts 6000 units ahead. The default 1000 far plane would hide it
        // entirely, which is exactly the "nothing is there" symptom.
        camera.farClipPlane = 20000f;

        return camera;
    }

    static GameObject Primitive(string name, Transform parent, Vector3 position, Vector3 scale)
    {
        GameObject existing = FindChild(parent, name);
        if (existing != null) return existing;

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        MarkFresh(go);

        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = scale;

        // Visual geometry, not a physics object. A collider here would only ever catch
        // something by accident.
        Collider collider = go.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider);

        return go;
    }

    static GameObject Mote(string name, Transform parent, Vector3 position, Vector3 drift,
                           FirstPersonLookRig_NEW lookRig)
    {
        GameObject go = Primitive(name, parent, position, Vector3.one * 0.5f);
        if (!IsFresh(go)) return go;


        Material material = TutorialWorldAssets_NEW.MoteMaterial();
        MeshRenderer renderer = go.GetComponent<MeshRenderer>();

        if (renderer != null)
        {
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        GameObject lightObject = FindOrCreate("Light", go.transform, position);
        Light light = AddIfMissing<Light>(lightObject);
        light.type = LightType.Point;
        light.range = 18f;
        light.intensity = 0.6f;
        light.color = new Color(1f, 0.92f, 0.72f);

        GuideMote_NEW mote = AddIfMissing<GuideMote_NEW>(go);
        Wire(mote)
            .Vec("driftDirection", drift)
            .Ref("moteLight", light)
            .Ref("lookRig", lookRig)
            .Apply();

        return go;
    }

    // ── UI ───────────────────────────────────────────────────────────────────

    static GameObject BuildCanvas(Transform parent)
    {
        GameObject go = FindOrCreate("HUD", parent, Vector3.zero);

        Canvas canvas = AddIfMissing<Canvas>(go);
        if (IsFresh(canvas)) canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = AddIfMissing<CanvasScaler>(go);

        if (IsFresh(scaler))
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // The Observatories display is curved and very wide. Matching height keeps
            // the legend a constant physical size as the aspect changes.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
        }

        AddIfMissing<GraphicRaycaster>(go);

        return go;
    }

    /// <summary>
    /// The emission sequencer, on the player next to the travel it drives.
    ///
    /// The photon trail reference is what switches the light on at C3, so it is wired
    /// here rather than left for somebody to find — it is the single most forgettable
    /// connection in Phase 2 and its failure mode is silent.
    /// </summary>
    static TutorialEmission_NEW BuildEmission(GameObject player, FirstPersonLookRig_NEW lookRig,
                                              TutorialCameraShake_NEW shake, TutorialFlash_NEW flash,
                                              Transform quasar)
    {
        TutorialEmission_NEW emission = AddIfMissing<TutorialEmission_NEW>(player);

        Transform trail = player.transform.Find("Trail");

        Wire(emission)
            .Ref("travel", player.GetComponent<TutorialTravel_NEW>())
            .Ref("lookRig", lookRig)
            .Ref("shake", shake)
            .Ref("flash", flash)
            .Ref("quasar", quasar)
            .Ref("photonTrail", trail != null ? trail.gameObject : null)
            .Apply();

        return emission;
    }

    /// <summary>
    /// Full-screen white flash, above the blackout and below the readable HUD.
    ///
    /// Above the blackout because the emission happens long after the opening fade has
    /// finished, and below the prompts because a flash that hides the A prompt during
    /// C2 would hide the one thing C2 is asking for.
    /// </summary>
    static GameObject BuildFlash(Transform canvas)
    {
        GameObject go = FullScreenUIObject("Flash", canvas);

        // Straight after Blackout, so it is behind every prompt.
        if (IsFresh(go)) go.transform.SetSiblingIndex(1);

        Image image = AddIfMissing<Image>(go);

        if (IsFresh(image))
        {
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = false;
            image.enabled = false;
        }

        TutorialFlash_NEW flash = AddIfMissing<TutorialFlash_NEW>(go);
        Wire(flash).Ref("screen", image).Apply();

        return go;
    }

    /// <summary>Full-screen black. First child, so every other HUD element draws over it.</summary>
    /// <summary>
    /// A named child stretched to fill its parent, created if it is not there.
    ///
    /// The full-screen overlays used to each build their own GameObject unconditionally,
    /// so every update run added another Blackout and another Plate. This is the same
    /// find-or-create contract as UIObject, for the stretch case.
    /// </summary>
    static GameObject FullScreenUIObject(string name, Transform parent)
    {
        GameObject existing = FindChild(parent, name);
        if (existing != null) return existing;

        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        MarkFresh(go);

        go.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return go;
    }

    static GameObject BuildBlackout(Transform canvas)
    {
        GameObject go = FullScreenUIObject("Blackout", canvas);

        Image image = AddIfMissing<Image>(go);

        if (IsFresh(image))
        {
            image.color = Color.black;
            image.raycastTarget = false;
        }

        return go;
    }

    /// <summary>
    /// A ring, not a dot. A filled square at the centre of a dark screen reads as a dead
    /// pixel; a ring reads as an aiming reticle and leaves the mote visible inside it,
    /// which matters because B1's gate is the mote being in there.
    /// </summary>
    static GameObject BuildReticle(Transform canvas)
    {
        GameObject go = UIObject("Reticle", canvas, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 34f));

        Image image = AddIfMissing<Image>(go);

        if (IsFresh(image))
        {
            image.sprite = TutorialWorldAssets_NEW.RingSprite();
            image.color = new Color(1f, 1f, 1f, 0.42f);
            image.raycastTarget = false;
        }

        return go;
    }

    /// <summary>
    /// The corner range map: a track, a marker on it, and the distance beside it.
    ///
    /// Top right, away from the reticle and from the prompts along the bottom, so it
    /// never sits between the player and the thing they are being asked to look at.
    /// A track and not a radar — there is one axis of travel in this piece.
    /// </summary>
    static GameObject BuildRangeMap(Transform canvas, GameObject player, FirstPersonLookRig_NEW lookRig)
    {
        GameObject go = UIObject("RangeMap", canvas, new Vector2(1f, 1f),
                                 new Vector2(-136f, -140f), new Vector2(200f, 232f));

        // A round map says radar: everything around you, at every bearing. This one is a
        // plan view of a straight line between two points, and a rounded rectangle is the
        // honest shape for that. It also sits square in the corner rather than leaving
        // four gaps.
        //
        // Restyling an existing map is normally the user's business and not the
        // builder's. The exception is a design that changed shape, and the tell is
        // structural rather than a colour comparison: no Plate means this scene is still
        // on the round one, and that is a one-way trip.
        bool roundVersion = FindChild(go.transform, "Plate") == null;


        GameObject edge = UIObject("Edge", go.transform, new Vector2(0.5f, 0.5f),
                                   Vector2.zero, new Vector2(202f, 234f));
        Paint(edge, TutorialWorldAssets_NEW.ChipSprite(), new Color(1f, 1f, 1f, 0.22f),
              new Vector2(202f, 234f), true, roundVersion);

        GameObject plate = UIObject("Plate", go.transform, new Vector2(0.5f, 0.5f),
                                    Vector2.zero, new Vector2(200f, 232f));
        Paint(plate, TutorialWorldAssets_NEW.ChipSprite(), new Color(0.015f, 0.02f, 0.045f, 0.86f),
              new Vector2(200f, 232f), true, roundVersion);

        // The box the two markers live in. No image of its own — the plate behind it is
        // the background. The component reads its reach off this rect, so the map is told
        // its size once rather than twice.
        GameObject frame = UIObject("Frame", go.transform, new Vector2(0.5f, 1f),
                                    new Vector2(0f, -89f), new Vector2(164f, 150f));

        Unpaint(frame, roundVersion);

        // The rim was the ring drawn around the old circle, and it was a child of Frame.
        RetireObject(frame.transform, "Rim", "the round map became a rounded plate");

        // The quasar first, so the player's marker draws over it on arrival.
        GameObject marker = UIObject("Quasar", frame.transform, new Vector2(0.5f, 0.5f),
                                     Vector2.zero, new Vector2(30f, 30f));
        Paint(marker, TutorialWorldAssets_NEW.GlowSprite(), new Color(1f, 0.82f, 0.32f, 0.5f),
              new Vector2(30f, 30f), false, roundVersion);

        GameObject core = UIObject("Core", marker.transform, new Vector2(0.5f, 0.5f),
                                   Vector2.zero, new Vector2(9f, 9f));
        Paint(core, TutorialWorldAssets_NEW.DiscSprite(), new Color(1f, 0.94f, 0.74f, 1f),
              new Vector2(9f, 9f), false, roundVersion);

        // Named, because an unlabelled dot on a map is a dot. To the right of the glow
        // rather than under it: the player's marker travels up the middle and ends up
        // exactly here.
        GameObject markerName = UIObject("Name", marker.transform, new Vector2(1f, 0.5f),
                                         new Vector2(32f, 0f), new Vector2(58f, 16f));
        TMP_Text markerLabel = AddText(markerName, "QUASAR", 11, TextAlignmentOptions.Left);
        if (IsFresh(markerLabel)) markerLabel.color = new Color(1f, 0.86f, 0.5f, 0.85f);

        // The player's marker is the thing that moves — see TutorialRangeMap_NEW on why
        // the frame is the world rather than the player. The arrow is a grandchild, so it
        // travels with the dot and turns independently of it.
        GameObject self = UIObject("Player", frame.transform, new Vector2(0.5f, 0.5f),
                                   Vector2.zero, new Vector2(10f, 10f));
        Paint(self, TutorialWorldAssets_NEW.DiscSprite(), Color.white,
              new Vector2(10f, 10f), false, roundVersion);

        GameObject facing = UIObject("Facing", self.transform, new Vector2(0.5f, 0.5f),
                                     Vector2.zero, new Vector2(10f, 10f));

        // The needle was a bar called Stem before it was an arrowhead. Renamed rather
        // than replaced, so it keeps its place in the hierarchy and anything hanging off
        // it comes along.
        GameObject legacyStem = FindChild(facing.transform, "Stem");
        if (legacyStem != null && FindChild(facing.transform, "Arrow") == null)
        {
            Undo.RecordObject(legacyStem, "Rename the map needle");
            legacyStem.name = "Arrow";
        }

        GameObject arrow = UIObject("Arrow", facing.transform, new Vector2(0.5f, 0.5f),
                                    new Vector2(0f, 8f), new Vector2(12f, 10f));
        Paint(arrow, TutorialWorldAssets_NEW.ArrowSprite(), new Color(1f, 1f, 1f, 0.95f),
              new Vector2(12f, 10f), false, roundVersion);

        GameObject label = UIObject("Label", go.transform, new Vector2(0.5f, 0f),
                                    new Vector2(0f, 44f), new Vector2(180f, 32f));
        AddText(label, "0", 24, TextAlignmentOptions.Center);

        GameObject caption = UIObject("Caption", go.transform, new Vector2(0.5f, 0f),
                                      new Vector2(0f, 16f), new Vector2(180f, 20f));
        // Not "TO THE QUASAR" any more: the dot up there says QUASAR now, and the same
        // word twice sixty pixels apart reads as nobody having looked at it.
        TMP_Text captionText = AddText(caption, "RANGE", 13, TextAlignmentOptions.Center);

        if (IsFresh(captionText) || roundVersion)
        {
            captionText.text = "RANGE";
            captionText.color = new Color(1f, 1f, 1f, 0.45f);
        }

        TutorialRangeMap_NEW map = AddIfMissing<TutorialRangeMap_NEW>(go);

        Wire(map)
            .Ref("travel", player.GetComponent<TutorialTravel_NEW>())
            .Ref("lookRig", lookRig)
            .Ref("frame", frame.GetComponent<RectTransform>())
            .Ref("playerMarker", self.GetComponent<RectTransform>())
            .Ref("facing", facing.GetComponent<RectTransform>())
            .Ref("arrow", arrow.GetComponent<RectTransform>())
            .Ref("destinationMarker", marker.GetComponent<RectTransform>())
            .Ref("distanceLabel", label.GetComponent<TMP_Text>())
            .Apply();

        // After the wiring, because PlaceArrow reads the reference the line above fills.
        //
        // One implementation of "which way does the arrow mean", and it lives on the
        // component — see TutorialRangeMap_NEW.PlaceArrow. Called here as well as from
        // Awake, so the map reads right in the Scene view and not only once you press
        // Play. A map built by an earlier version has the needle BELOW the marker,
        // pointing back at where the player came from; this is what repairs it.
        RectTransform arrowRect = arrow.GetComponent<RectTransform>();
        Vector2 arrowWas = arrowRect.anchoredPosition;

        Undo.RecordObject(arrowRect, "Place the map arrow");
        map.PlaceArrow();

        if ((arrowRect.anchoredPosition - arrowWas).sqrMagnitude > 0.01f)
        {
            Debug.LogWarning("[TutorialSceneBuilder_NEW] Moved the map's facing arrow to sit " +
                             "above the player marker. The map turns it from straight up, so " +
                             "anywhere else it points the wrong way.", arrowRect);
            _wired++;
        }

        return go;
    }

    /// <summary>
    /// The zoom gauge: a vertical track that fills as the view narrows, on the right,
    /// clear of the reticle and of the prompts along the bottom.
    ///
    /// The lesson needs this. A line of text and an image that gets bigger do not
    /// obviously belong to each other — see TutorialZoomGauge_NEW.
    /// </summary>
    static GameObject BuildZoomGauge(Transform canvas, TutorialZoom_NEW zoom,
                                     TutorialDirector_NEW director)
    {
        GameObject go = UIObject("ZoomGauge", canvas, new Vector2(1f, 0.5f),
                                 new Vector2(-90f, 0f), new Vector2(70f, 260f));

        AddIfMissing<CanvasGroup>(go);

        GameObject track = UIObject("Track", go.transform, new Vector2(0.5f, 0.5f),
                                    new Vector2(0f, 14f), new Vector2(6f, 190f));

        Image trackImage = AddIfMissing<Image>(track);

        if (IsFresh(trackImage))
        {
            trackImage.color = new Color(1f, 1f, 1f, 0.18f);
            trackImage.raycastTarget = false;
        }

        // Bottom-anchored, so growing its height fills it upwards.
        GameObject fill = UIObject("Fill", track.transform, new Vector2(0.5f, 0f),
                                   Vector2.zero, new Vector2(6f, 0f));

        if (IsFresh(fill))
        {
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.pivot = new Vector2(0.5f, 0f);
            fillRect.anchoredPosition = Vector2.zero;
        }

        Image fillImage = AddIfMissing<Image>(fill);

        if (IsFresh(fillImage))
        {
            fillImage.color = new Color(1f, 0.92f, 0.76f, 0.95f);
            fillImage.raycastTarget = false;
        }

        GameObject degrees = UIObject("Degrees", go.transform, new Vector2(0.5f, 0f),
                                      new Vector2(0f, 100f), new Vector2(70f, 24f));
        TMP_Text degreesText = AddText(degrees, "40°", 17, TextAlignmentOptions.Center);
        if (IsFresh(degreesText)) degreesText.color = new Color(1f, 1f, 1f, 0.6f);

        GameObject caption = UIObject("Caption", go.transform, new Vector2(0.5f, 1f),
                                      new Vector2(0f, -6f), new Vector2(70f, 22f));
        TMP_Text captionText = AddText(caption, "ZOOM", 15, TextAlignmentOptions.Center);
        if (IsFresh(captionText)) captionText.color = new Color(1f, 1f, 1f, 0.5f);

        TutorialZoomGauge_NEW gauge = AddIfMissing<TutorialZoomGauge_NEW>(go);

        Wire(gauge)
            .Ref("zoom", zoom)
            .Ref("director", director)
            .Ref("track", track.GetComponent<RectTransform>())
            .Ref("fill", fill.GetComponent<RectTransform>())
            .Ref("degreesLabel", degrees.GetComponent<TMP_Text>())
            .Ref("group", go.GetComponent<CanvasGroup>())
            .Apply();

        return go;
    }

    static GameObject BuildLegend(Transform canvas)
    {
        GameObject go = UIObject("Legend", canvas, new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(1200f, 34f));

        AddIfMissing<CanvasGroup>(go);

        TMP_Text text = AddText(go, "RIGHT STICK = LOOK    LEFT STICK = ZOOM    A = CONFIRM / RECENTRE", 22, TextAlignmentOptions.Center);

        // Quiet. This is a reference card that never leaves, not an instruction — it has
        // to survive being on screen for the whole piece without competing with it.
        if (IsFresh(text)) text.color = new Color(1f, 1f, 1f, 0.45f);

        return go;
    }

    /// <summary>
    /// The control hint: a plate with a line of text on it.
    ///
    /// The plate is the difference between a HUD and a debug readout. White text alone
    /// disappears into a bright quasar and a starfield; the same text on a dark plate
    /// holds at the back of a room, which is the only viewing distance that matters at
    /// the Observatories.
    /// </summary>
    static GameObject BuildHint(Transform canvas)
    {
        GameObject go = UIObject("Hint", canvas, new Vector2(0.5f, 0f), new Vector2(0f, 250f), new Vector2(860f, 84f));

        AddIfMissing<CanvasGroup>(go);
        AddChipBackground(go, 0.55f);

        GameObject label = UIObject("Label", go.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 64f));
        // Placeholder only: TutorialHUD_NEW writes the running beat's hintText over this
        // before the line is ever shown. It is here so the object reads correctly in the
        // Scene view rather than as an empty rect.
        AddText(label, "RIGHT STICK  ·  LOOK AROUND", 34, TextAlignmentOptions.Center);

        return go;
    }

    // ── Stick diagram geometry ───────────────────────────────────────────────
    // Written as arithmetic for the same reason the confirm prompt's is: the numbers are
    // not independent. The knob has to stay inside the ring, and the whole diagram has
    // to clear the hint plate it hangs off, so both follow from the ring's size.

    /// <summary>Outside diameter of the ring, in pixels.</summary>
    const float StickRingSize = 104f;

    /// <summary>
    /// The knob. It is the part that moves and therefore the part being read, so it is
    /// sized to be read — at 34 it was a dot inside a ring rather than a thumb on a stick.
    /// </summary>
    const float StickKnobSize = 44f;

    /// <summary>The size this builder wrote before the knob was made legible.</summary>
    const float PastStickKnobSize = 34f;

    /// <summary>
    /// Centre of the ring to centre of the knob at full deflection.
    ///
    /// Derived, not typed: a bigger knob has to travel less or it leaves the ring, and
    /// the two numbers are not independent. Three pixels of clearance, so hard over reads
    /// as the knob against its gate rather than through it.
    /// </summary>
    const float StickTravel = (StickRingSize - StickKnobSize) * 0.5f - 3f;

    /// <summary>The travel that went with the smaller knob.</summary>
    const float PastStickTravel = (StickRingSize - PastStickKnobSize) * 0.5f - 6f;

    /// <summary>Gap between the left edge of the hint plate and the right edge of the ring.</summary>
    const float StickGap = 26f;

    /// <summary>
    /// The stick diagram, hanging off the left end of the hint plate.
    ///
    /// A CHILD OF THE HINT, not a sibling, and that is the whole of its visibility
    /// logic. The diagram and the line it illustrates are one statement: the hint's
    /// CanvasGroup fades them together and the HUD's SetActive takes them away together,
    /// so there is no state in which a picture of a stick is on screen with nothing
    /// asking for it. A separate root would have needed its own copy of all of that, and
    /// the copy would have drifted.
    ///
    /// Left of the plate rather than above or below it. Below is the legend and the A
    /// prompt; above, now, is the spectrum bar. Beside it is also simply how a control
    /// label reads — glyph first, then the words, the same order as the A prompt's disc
    /// and verb.
    /// </summary>
    static GameObject BuildStickGuide(GameObject hint)
    {
        RectTransform hintRect = hint.GetComponent<RectTransform>();
        float plateWidth = hintRect != null ? hintRect.sizeDelta.x : 860f;

        // Anchored to the plate's own left edge, so widening the plate takes the diagram
        // with it instead of leaving it sitting on top of the first word.
        GameObject go = UIObject("StickGuide", hint.transform, new Vector2(0f, 0.5f),
                                 new Vector2(-(StickGap + StickRingSize * 0.5f), 0f),
                                 new Vector2(StickRingSize, StickRingSize));

        // Off the left of a 1920 canvas is the one way this placement can fail, and it
        // fails silently — the diagram is simply not there. Worth a line in the Console.
        float leftEdge = -plateWidth * 0.5f - StickGap - StickRingSize;
        if (leftEdge < -940f)
            Debug.LogWarning("[TutorialSceneBuilder_NEW] The stick diagram reaches " +
                             (-leftEdge).ToString("F0") + " pixels left of centre, which is " +
                             "off a 1920 canvas. Narrow the hint plate or move the diagram.", go);

        AddIfMissing<CanvasGroup>(go);

        GameObject ring = UIObject("Ring", go.transform, new Vector2(0.5f, 0.5f),
                                   Vector2.zero, new Vector2(StickRingSize, StickRingSize));

        Image ringImage = AddIfMissing<Image>(ring);

        if (IsFresh(ringImage))
        {
            // The reticle's own sprite. The player has already learned that this shape
            // means "the thing you aim", and the stick is the thing that aims it.
            ringImage.sprite = TutorialWorldAssets_NEW.RingSprite();
            ringImage.color = new Color(1f, 1f, 1f, 0.38f);
            ringImage.raycastTarget = false;
        }

        GameObject knob = UIObject("Knob", go.transform, new Vector2(0.5f, 0.5f),
                                   Vector2.zero, new Vector2(StickKnobSize, StickKnobSize));

        Image knobImage = AddIfMissing<Image>(knob);

        if (IsFresh(knobImage))
        {
            // Solid and bright against the faint ring: the knob is the part that moves
            // and therefore the part being read.
            knobImage.sprite = TutorialWorldAssets_NEW.DiscSprite();
            knobImage.color = new Color(1f, 1f, 1f, 0.95f);
            knobImage.raycastTarget = false;
        }

        // L or R, under the ring rather than inside it — the knob crosses the middle.
        GameObject side = UIObject("Side", go.transform, new Vector2(0.5f, 0f),
                                   new Vector2(0f, -14f), new Vector2(60f, 24f));

        TMP_Text sideText = AddText(side, "R", 18, TextAlignmentOptions.Center);
        if (IsFresh(sideText)) sideText.color = new Color(1f, 1f, 1f, 0.55f);

        // The knob grew, and its travel with it. UIObject and Wire both leave an object
        // that already exists alone, so neither reaches a saved scene — and the pair has
        // to move together or the knob either rattles around inside the ring or pushes
        // through it. Narrow as always: only the exact values this builder used to write.
        RectTransform knobRect = knob.GetComponent<RectTransform>();

        if (knobRect != null && Approximately(knobRect.sizeDelta,
                                              new Vector2(PastStickKnobSize, PastStickKnobSize)))
        {
            Undo.RecordObject(knobRect, "Resize the stick knob");
            knobRect.sizeDelta = new Vector2(StickKnobSize, StickKnobSize);
            EditorUtility.SetDirty(knobRect);

            Debug.LogWarning("[TutorialSceneBuilder_NEW] Stick knob " + PastStickKnobSize +
                             " -> " + StickKnobSize + " px. It is the part that moves, so it is " +
                             "the part that has to be readable from standing distance.", knobRect);
            _wired++;
        }

        TutorialStickGuide_NEW guide = AddIfMissing<TutorialStickGuide_NEW>(go);

        Wire(guide)
            .Ref("knob", knob.GetComponent<RectTransform>())
            .Ref("stickLabel", sideText)
            .Ref("group", go.GetComponent<CanvasGroup>())
            .Num("travelRadius", StickTravel)
            .Apply();

        RepairSerialized(guide, "travelRadius",
                         p => Mathf.Approximately(p.floatValue, PastStickTravel),
                         p => p.floatValue = StickTravel,
                         "stick travel follows the bigger knob, so hard over still lands inside " +
                         "the ring.");

        return go;
    }

    /// <summary>
    /// The continue affordance: a round button glyph with A in it, and the verb beside it.
    ///
    /// GDD §5 requires this to be the same shape in the same position every time, which
    /// is why the shape lives here and only the words come from the beat. The glyph is a
    /// disc rather than a bare letter because a bare letter is read as text, and text
    /// does not look pressable.
    /// </summary>
    // ── Confirm prompt geometry ──────────────────────────────────────────────
    // Written as arithmetic rather than as four tuned numbers, because the numbers are
    // not independent: the label is left aligned, so its left edge is what has to clear
    // the glyph, and that edge moves whenever either the glyph or the label's width does.

    const float PromptWidth = 520f;
    const float PromptGlyphX = -168f;
    const float PromptGlyphSize = 56f;

    /// <summary>Gap between the disc and the first letter.</summary>
    const float PromptGap = 24f;

    /// <summary>Right edge of the disc, plus the gap. Where text may start.</summary>
    const float PromptTextLeft = PromptGlyphX + PromptGlyphSize * 0.5f + PromptGap;

    /// <summary>From the first letter to the right edge of the plate, less a margin.</summary>
    const float PromptLabelWidth = PromptWidth * 0.5f - PromptTextLeft - 20f;

    /// <summary>Centre of a left-aligned box whose left edge sits at PromptTextLeft.</summary>
    const float PromptLabelX = PromptTextLeft + PromptLabelWidth * 0.5f;

    static GameObject BuildConfirmPrompt(Transform canvas)
    {
        // GDD B4: the A prompt appears at the lower edge.
        GameObject go = UIObject("ConfirmPrompt", canvas, new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(520f, 92f));

        AddIfMissing<CanvasGroup>(go);
        AddChipBackground(go, 0.55f);

        // Button glyph, left of centre.
        GameObject glyph = UIObject("Glyph", go.transform, new Vector2(0.5f, 0.5f), new Vector2(-168f, 0f), new Vector2(56f, 56f));

        Image disc = AddIfMissing<Image>(glyph);

        if (IsFresh(disc))
        {
            disc.sprite = TutorialWorldAssets_NEW.DiscSprite();
            disc.color = new Color(1f, 1f, 1f, 0.92f);
            disc.raycastTarget = false;
        }

        GameObject glyphLabel = UIObject("A", glyph.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(56f, 56f));
        TMP_Text a = AddText(glyphLabel, "A", 30, TextAlignmentOptions.Center);
        if (IsFresh(a)) a.color = new Color(0.04f, 0.04f, 0.07f, 1f);

        // The verb. TutorialHUD_NEW writes the beat's promptText here, so the beat still
        // owns the words and the HUD still owns the shape.
        //
        // THE LEFT EDGE HAS TO CLEAR THE GLYPH, and it did not. The text is left
        // aligned, so it starts at the box's left edge rather than at its centre: a
        // 380-wide box at x=36 begins at -154, and the glyph ends at -140. The first
        // letter was printed 14 pixels inside the disc, which is why A sat on top of the
        // R of RECENTRE.
        //
        // Derived rather than typed, so moving the glyph cannot silently re-open the
        // same gap.
        GameObject label = UIObject("Label", go.transform, new Vector2(0.5f, 0.5f),
                                    new Vector2(PromptLabelX, 0f), new Vector2(PromptLabelWidth, 64f));
        AddText(label, "RECENTRE", 32, TextAlignmentOptions.Left);

        RepairRect(label, new Vector2(0.5f, 0.5f),
                   new Vector2(PromptLabelX, 0f), new Vector2(PromptLabelWidth, 64f),
                   "ConfirmPrompt label",
                   new Vector2(36f, 0f), new Vector2(380f, 64f));

        return go;
    }

    /// <summary>Dark rounded plate behind a prompt. 9-sliced, so it stretches cleanly.</summary>
    static void AddChipBackground(GameObject parent, float alpha)
    {
        GameObject go = FullScreenUIObject("Plate", parent.transform);

        Image image = AddIfMissing<Image>(go);
        if (!IsFresh(image)) return;

        image.sprite = TutorialWorldAssets_NEW.ChipSprite();
        image.type = Image.Type.Sliced;
        image.color = new Color(0.02f, 0.02f, 0.04f, alpha);
        image.raycastTarget = false;

        // Behind the label, whatever order the children ended up in.
        go.transform.SetAsFirstSibling();
    }

    static GameObject BuildAttractCard(Transform canvas)
    {
        GameObject go = UIObject("AttractCard", canvas, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 400f));
        AddIfMissing<CanvasGroup>(go);

        GameObject titleObject = UIObject("Title", go.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1400f, 120f));
        AddText(titleObject, "THE JOURNEY OF LIGHT", 72, TextAlignmentOptions.Center);

        GameObject ctaObject = UIObject("CallToAction", go.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(1400f, 60f));
        AddText(ctaObject, "PRESS A TO BEGIN", 34, TextAlignmentOptions.Center);

        return go;
    }

    static GameObject UIObject(string name, Transform parent, Vector2 anchor, Vector2 offset, Vector2 size)
    {
        GameObject existing = FindChild(parent, name);
        if (existing != null) return existing;

        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        MarkFresh(go);

        go.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = offset;
        rect.sizeDelta = size;

        return go;
    }

    /// <summary>
    /// Put a label on the object, and style it only if this run created it.
    ///
    /// TextMeshPro rather than UI.Text, and the project's own Gontserrat rather than
    /// Arial. Both were making the HUD look like a debug readout: builtin Arial at a
    /// fixed pixel size goes soft the moment the Canvas scales, which on the
    /// Observatories' wide curved display it always does, and it is not the typeface
    /// anything else in the piece is set in. Gontserrat is what PlaytestBuild's UI
    /// already uses, and SDF text stays crisp at any scale.
    ///
    /// Font, size and spacing are the most retuned things in the rig, so an update run
    /// that restyled every label would undo an afternoon of work.
    /// </summary>
    static TMP_Text AddText(GameObject go, string content, float size, TextAlignmentOptions alignment)
    {
        MigrateLegacyText(go);

        TextMeshProUGUI text = AddIfMissing<TextMeshProUGUI>(go);
        if (!IsFresh(text)) return text;

        text.text = content;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;

        // Tracked out. All-caps set solid reads as a block at a distance; a little air
        // between the letters is most of what separates a prompt from a log line.
        text.characterSpacing = 4f;

        TMP_FontAsset font = TutorialWorldAssets_NEW.HudFont();
        if (font != null) text.font = font;

        return text;
    }

    // ── Wiring ───────────────────────────────────────────────────────────────

    static void WireHud(TutorialHUD_NEW hud, TutorialDirector_NEW director,
                        GameObject legend, GameObject reticle,
                        GameObject hint, GameObject prompt,
                        TutorialStickGuide_NEW stickGuide)
    {
        // mapRoot is deliberately not wired any more — see the retirement above. The
        // field stays on the HUD, and every use of it there is already null-guarded.
        Wire(hud)
            .Ref("stickGuide", stickGuide)
            .Ref("director", director)
            .Ref("legendRoot", legend)
            .Ref("legendText", legend.GetComponent<TMP_Text>())
            .Ref("legendGroup", legend.GetComponent<CanvasGroup>())
            .Ref("reticleRoot", reticle)
            .Ref("hintRoot", hint)
            .Ref("hintLabel", LabelIn(hint))
            .Ref("hintGroup", hint.GetComponent<CanvasGroup>())
            .Ref("confirmPromptRoot", prompt)
            .Ref("confirmPromptText", LabelIn(prompt))
            .Ref("promptGroup", prompt.GetComponent<CanvasGroup>())
            .Copy("legendContent", "RIGHT STICK = LOOK    LEFT STICK = ZOOM    A = CONFIRM / RECENTRE")
            .Str("legendFirstBeatId", "B1")
            .Apply();
    }

    static void WireAttract(TutorialAttract_NEW attract, TutorialDirector_NEW director,
                            GameObject card, FirstPersonLookRig_NEW lookRig, TutorialTravel_NEW travel)
    {
        Transform title = card.transform.Find("Title");
        Transform cta = card.transform.Find("CallToAction");

        Wire(attract)
            .Ref("director", director)
            .Ref("cardRoot", card)
            .Ref("cardGroup", card.GetComponent<CanvasGroup>())
            .Ref("titleText", title != null ? title.GetComponent<TMP_Text>() : null)
            .Ref("callToActionText", cta != null ? cta.GetComponent<TMP_Text>() : null)
            .Ref("lookRig", lookRig)
            .Ref("travel", travel)
            .Apply();
    }

    /// <summary>
    /// Add a persistent SetActive listener to a beat's onEnter event.
    ///
    /// Persistent rather than runtime so the wiring is visible in the Inspector — the
    /// point of putting cues on UnityEvents in the first place is that the person
    /// moving the storyboard can see and change them without opening Visual Studio.
    /// </summary>
    static void AddActivateOnEnter(TutorialBeat_NEW beat, GameObject target, bool active)
    {
        UnityEvent onEnter = GetEvent(beat, "onEnter");
        if (onEnter == null || HasListenerFor(onEnter, target)) return;

        UnityEventTools.AddBoolPersistentListener(onEnter, new UnityAction<bool>(target.SetActive), active);
        EditorUtility.SetDirty(beat);
    }

    /// <summary>
    /// Call a method on TutorialEmission_NEW when this beat opens.
    ///
    /// A persistent listener rather than a hard reference from the beat, so the whole
    /// Phase 2 sequence is readable in the Inspector: open C3 and its onEnter says
    /// "TutorialEmission_NEW.Emit". Somebody retiming the emission changes it there
    /// without opening this file.
    /// </summary>
    static void AddEmissionCall(TutorialBeat_NEW beat, TutorialEmission_NEW emission, string method)
    {
        AddCall(beat, "onEnter", emission, method);
    }

    /// <summary>
    /// Wire one no-argument method on one component to one of a beat's UnityEvents.
    ///
    /// This is AddEmissionCall generalised, and the generalisation is not cosmetic: it
    /// matches on the METHOD as well as the target, where HasListenerFor matches on the
    /// target alone. Phase 2 never noticed, because each of its beats drives the
    /// emission exactly once. D2 calls three methods on the same TutorialSpectrum_NEW
    /// from one event — show the bar, empty it, cut the line — and a target-only check
    /// would silently drop the second and the third.
    ///
    /// Still idempotent, which is what the builder needs: running Build or Update twice
    /// does not stack duplicate listeners.
    /// </summary>
    static void AddCall(Object owner, string eventName, Object target, string method)
    {
        if (owner == null || target == null) return;

        UnityEvent e = GetEvent(owner, eventName);
        if (e == null || HasCall(e, target, method)) return;

        UnityAction call = System.Delegate.CreateDelegate(typeof(UnityAction), target, method, false, false)
                           as UnityAction;

        if (call == null)
        {
            Debug.LogWarning("[TutorialSceneBuilder_NEW] " + target.GetType().Name + " has no " +
                             "no-argument method '" + method + "'. " + owner.name + "." + eventName +
                             " will not call it.", owner);
            return;
        }

        UnityEventTools.AddPersistentListener(e, call);
        EditorUtility.SetDirty(owner);
    }

    /// <summary>Is this exact target and method already on this event?</summary>
    static bool HasCall(UnityEventBase e, Object target, string method)
    {
        for (int i = 0; i < e.GetPersistentEventCount(); i++)
            if (e.GetPersistentTarget(i) == target && e.GetPersistentMethodName(i) == method)
                return true;

        return false;
    }

    /// <summary>
    /// Is this event already calling something on that object?
    ///
    /// Without this, every Build or Update run would add another copy of the same
    /// listener and C3 would emit four times. Matching on the target rather than the
    /// method is deliberate: a listener somebody repointed at a different method on the
    /// same component is a decision, and re-adding ours next to it would be an argument.
    /// </summary>
    static bool HasListenerFor(UnityEventBase e, Object target)
    {
        for (int i = 0; i < e.GetPersistentEventCount(); i++)
            if (e.GetPersistentTarget(i) == target) return true;

        return false;
    }

    /// <summary>
    /// Fetch a private serialized UnityEvent by name, walking up the type hierarchy.
    ///
    /// onEnter is declared private on TutorialBeat_NEW, and reflection over a derived
    /// type does not see a base type's private fields — FlattenHierarchy does not change
    /// that. Hence the walk.
    /// </summary>
    static UnityEvent GetEvent(object owner, string fieldName)
    {
        const System.Reflection.BindingFlags Flags =
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public;

        for (System.Type type = owner.GetType(); type != null; type = type.BaseType)
        {
            System.Reflection.FieldInfo field = type.GetField(fieldName, Flags);
            if (field == null) continue;

            return field.GetValue(owner) as UnityEvent;
        }

        Debug.LogWarning("[TutorialSceneBuilder_NEW] No field '" + fieldName + "' on " +
                         owner.GetType().Name + " or its base types.");
        return null;
    }

    /// <summary>
    /// Take a UI.Text off a label that predates the move to TextMeshPro.
    ///
    /// This is the one place the builder deliberately destroys somebody's work, and it
    /// is here because the alternative is worse. Scenes built before the switch carry a
    /// UI.Text on every label; TutorialHUD_NEW's fields are TMP_Text now, so those
    /// references deserialise to null, and simply adding a TMP component would leave two
    /// labels stacked on the same rect drawing the same words twice.
    ///
    /// It is loud about it. A migration that happens silently is one nobody can explain
    /// three weeks later.
    /// </summary>
    static void MigrateLegacyText(GameObject go)
    {
        Text legacy = go.GetComponent<Text>();
        if (legacy == null) return;

        Debug.Log("[TutorialSceneBuilder_NEW] Replaced the UI.Text on '" + go.name +
                  "' with TextMeshPro. Any styling on the old component is gone; the new " +
                  "one is set from the builder's defaults.", go);

        Undo.DestroyObjectImmediate(legacy);
    }

    /// <summary>
    /// The Text named "Label" inside a prompt.
    ///
    /// Prompts are now a plate plus a glyph plus a label rather than one Text on the
    /// root, so a GetComponent on the root finds nothing and a GetComponentInChildren
    /// can find the A inside the button glyph instead. Named lookup, so adding another
    /// piece of art to a prompt cannot silently repoint the HUD at it.
    /// </summary>
    static TMP_Text LabelIn(GameObject prompt)
    {
        Transform label = prompt.transform.Find("Label");

        if (label == null)
        {
            Debug.LogError("[TutorialSceneBuilder_NEW] " + prompt.name + " has no child named " +
                           "'Label'. The HUD will have nowhere to write its text.", prompt);
            return null;
        }

        return label.GetComponent<TMP_Text>();
    }

    static Wiring Wire(Object target)
    {
        return new Wiring(target);
    }

    /// <summary>
    /// Sets private [SerializeField] fields through SerializedObject.
    ///
    /// The fields being private is the point — nothing outside a component should be
    /// setting them at runtime. A builder is the one legitimate exception, and going
    /// through SerializedObject keeps it honest: a renamed field fails loudly here
    /// instead of silently leaving a null reference in the scene.
    /// </summary>
    class Wiring
    {
        readonly SerializedObject _so;
        readonly string _name;

        /// <summary>
        /// False when the component already existed before this run. Values are then
        /// left alone and only empty references are filled — see the OWNERSHIP note.
        /// </summary>
        readonly bool _fresh;

        public Wiring(Object target)
        {
            _so = new SerializedObject(target);
            _name = target != null ? target.GetType().Name : "(null)";
            _fresh = IsFresh(target);
        }

        /// <summary>
        /// Object references are filled when empty even on an existing component.
        /// A null reference is not a decision, it is a gap — usually because a later
        /// run added the thing it should point at. Anything already pointing somewhere
        /// is left alone.
        /// </summary>
        public Wiring Ref(string path, Object value)
        {
            SerializedProperty p = Find(path);
            if (p == null) return this;

            if (!_fresh && p.objectReferenceValue != null) return this;
            if (p.objectReferenceValue == value) return this;

            p.objectReferenceValue = value;
            Note(path);
            return this;
        }

        /// <summary>Strings are filled when empty, on the same reasoning as Ref.</summary>
        public Wiring Str(string path, string value)
        {
            SerializedProperty p = Find(path);
            if (p == null) return this;

            if (!_fresh && !string.IsNullOrEmpty(p.stringValue)) return this;
            if (p.stringValue == value) return this;

            p.stringValue = value;
            Note(path);
            return this;
        }

        /// <summary>
        /// Prompt copy: rewritten even on a component that already existed.
        ///
        /// Str's "fill only when empty" rule is right for a reference and wrong for
        /// wording, and the difference is who wrote it. A prompt string was authored by
        /// the builder, so a scene built last week holds last week's copy forever — which
        /// is how a prompt reading LEFT STICK survived look moving to the right stick,
        /// with nothing in the Console to say so.
        ///
        /// The target opts out with a `builderOwnsCopy` field set false, which is how
        /// somebody writes their own wording and keeps it. A target with no such field
        /// is treated as owning nothing, since only beats carry copy.
        /// </summary>
        public Wiring Copy(string path, string value)
        {
            SerializedProperty p = Find(path);
            if (p == null) return this;
            if (p.stringValue == value) return this;

            if (!_fresh)
            {
                SerializedProperty owned = _so.FindProperty("builderOwnsCopy");
                if (owned == null || !owned.boolValue) return this;

                Debug.Log("[TutorialSceneBuilder_NEW] " + _name + "." + path + ": '" +
                          p.stringValue + "' -> '" + value + "'. Untick builderOwnsCopy " +
                          "on that beat to keep your own wording.");
            }

            p.stringValue = value;
            _wired++;
            return this;
        }

        // Numbers, flags, enums and vectors are never written to an existing component.
        // There is no way to tell "nobody set this" from "somebody set it to exactly
        // that", so the only safe answer is to leave it.

        public Wiring Num(string path, float value)
        {
            if (!_fresh) return this;

            SerializedProperty p = Find(path);
            if (p != null) p.floatValue = value;
            return this;
        }

        public Wiring Flag(string path, bool value)
        {
            if (!_fresh) return this;

            SerializedProperty p = Find(path);
            if (p != null) p.boolValue = value;
            return this;
        }

        public Wiring Enum(string path, int value)
        {
            if (!_fresh) return this;

            SerializedProperty p = Find(path);
            if (p != null) p.enumValueIndex = value;
            return this;
        }

        public Wiring Vec(string path, Vector3 value)
        {
            if (!_fresh) return this;

            SerializedProperty p = Find(path);
            if (p != null) p.vector3Value = value;
            return this;
        }
        public Wiring Vec2(string path, Vector2 value)
        {
            if (!_fresh) return this;

            SerializedProperty p = Find(path);
            if (p != null) p.vector2Value = value;
            return this;
        }

        public void Apply()
        {
            _so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Report every write into something that already existed, so a second run is
        /// never a silent edit of somebody's scene.
        /// </summary>
        void Note(string path)
        {
            _wired++;
            if (!_fresh) Debug.Log("[TutorialSceneBuilder_NEW] Filled empty " + _name + "." + path + ".");
        }

        SerializedProperty Find(string path)
        {
            SerializedProperty p = _so.FindProperty(path);

            if (p == null)
                Debug.LogError("[TutorialSceneBuilder_NEW] " + _name + " has no serialized field '" +
                               path + "'. The builder is out of date with the script.");

            return p;
        }
    }
}

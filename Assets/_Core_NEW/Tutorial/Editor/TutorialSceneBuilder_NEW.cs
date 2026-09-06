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
    ///   C1         closes whatever is left, in exactly 12 seconds, ending at rest at
    ///              ArrivalStandoff. See TutorialTravel_NEW.ApproachTo.
    ///
    /// So a rushing visitor arrives at C1 from about 2100 out and an unhurried one from
    /// 900, and both reach the quasar as C2 opens.
    /// </summary>
    static readonly Vector3 QuasarPosition = new Vector3(0f, 0f, 2600f);

    /// <summary>Closest the cruise may get. C1's approach is what goes inside it.</summary>
    const float TravelHoldDistance = 900f;

    /// <summary>
    /// Where the light comes to rest, from the quasar's centre. Inside the halo and
    /// just outside the core at radius 450: the player ends up at the thing that is
    /// about to emit them, with it filling the frame.
    /// </summary>
    const float ArrivalStandoff = 560f;

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
        BuildPhotonTrail(player.transform);

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
        GameObject hint = BuildHint(canvas.transform);
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
        TutorialEmission_NEW emission = BuildEmission(player, lookRig, shake,
                                                      flashObject.GetComponent<TutorialFlash_NEW>(),
                                                      quasar.transform);

        BuildPhase0(beats.transform, moteA);
        BuildPhase1(beats.transform, lookRig, moteA, moteB, moteC);
        BuildPhase2(beats.transform, lookRig, quasar, emission);

        // ── HUD and attract components ───────────────────────────────────────
        TutorialHUD_NEW hud = AddIfMissing<TutorialHUD_NEW>(canvas);
        WireHud(hud, director, legend, reticle, hint, prompt);

        TutorialAttract_NEW attract = AddIfMissing<TutorialAttract_NEW>(canvas);
        WireAttract(attract, director, card, lookRig, player.GetComponent<TutorialTravel_NEW>());

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
    static void BuildPhotonTrail(Transform player)
    {
        GameObject go = FindOrCreate("Trail", player, player.position);
        if (!IsFresh(go)) return;


        // Ahead of the eye and slightly low, which is the first person equivalent of
        // where PlaytestBuild puts it. There the trail sits at the player's origin and
        // the FreeLook orbits about three units behind, so the ribbon is always a few
        // units in front of the camera and plainly visible. Emitting from the camera
        // position instead — which is what this did before — puts the newest segment
        // inside the near plane, where it is either invisible or a full-screen smear.
        //
        // From here it streams back past and below the view: readable while flying
        // forward, and unmistakable on C4's look back.
        go.transform.localPosition = new Vector3(0f, -0.45f, 2.6f);

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
    }

    // ── Phases ───────────────────────────────────────────────────────────────

    static void BuildPhase0(Transform parent, GameObject moteA)
    {
        Beat_Cinematic_NEW a1 = Beat<Beat_Cinematic_NEW>(parent, "A1");
        Wire(a1)
            .Str("beatId", "A1")
            .Str("description", "Near black. Dark red matter drifts in slow rotation deep in frame.")
            .Enum("hintMode", (int)TutorialBeat_NEW.HintMode.Show)
            .Str("hintText", "RIGHT STICK  ·  LOOK AROUND")
            .Enum("advanceMode", (int)TutorialBeat_NEW.AdvanceMode.Duration)
            .Num("duration", 8f)
            .Apply();

        Beat_Cinematic_NEW a2 = Beat<Beat_Cinematic_NEW>(parent, "A2");
        Wire(a2)
            .Str("beatId", "A2")
            .Str("description", "The flow brightens enough to read as orbiting something. " +
                                "At the centre, a patch darker than black.")
            .Enum("advanceMode", (int)TutorialBeat_NEW.AdvanceMode.Duration)
            .Num("duration", 7f)
            .Apply();

        Beat_Cinematic_NEW a3 = Beat<Beat_Cinematic_NEW>(parent, "A3");
        Wire(a3)
            .Str("beatId", "A3")
            .Str("description", "A mote drifts out of frame at the right edge. One short controller rumble.")
            .Enum("advanceMode", (int)TutorialBeat_NEW.AdvanceMode.Duration)
            .Num("duration", 5f)
            .Flag("rumbleOnEnter", true)
            .Apply();

        // A3 is where the mote first appears. Nothing is on screen or in the world
        // before its own frame — a mote visible during A1 is a stray particle.
        AddActivateOnEnter(a3, moteA, true);
    }

    static void BuildPhase1(Transform parent, FirstPersonLookRig_NEW lookRig,
                            GameObject moteA, GameObject moteB, GameObject moteC)
    {
        Beat_LookAt_NEW b1 = Beat<Beat_LookAt_NEW>(parent, "B1");
        Wire(b1)
            .Str("beatId", "B1")
            .Str("description", "Player turns right, catches the mote, it blooms into a ripple.")
            .Enum("hintMode", (int)TutorialBeat_NEW.HintMode.Show)
            .Str("hintText", "RIGHT STICK  ·  LOOK RIGHT")
            .Enum("advanceMode", (int)TutorialBeat_NEW.AdvanceMode.PlayerAction)
            .Ref("target", moteA.transform)
            .Ref("mote", moteA.GetComponent<GuideMote_NEW>())
            .Ref("lookRig", lookRig)
            .Num("reticleHalfAngle", 12f)
            .Num("holdSeconds", 0f)
            .Apply();

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
            .Str("description", "The mote passes overhead. Looking up reveals the jet channel " +
                                "running into the dark, which is the direction of travel.")
            .Enum("hintMode", (int)TutorialBeat_NEW.HintMode.Show)
            .Str("hintText", "RIGHT STICK  ·  LOOK UP")
            .Enum("advanceMode", (int)TutorialBeat_NEW.AdvanceMode.PlayerAction)
            .Ref("target", moteB.transform)
            .Ref("mote", moteB.GetComponent<GuideMote_NEW>())
            .Ref("lookRig", lookRig)
            .Num("reticleHalfAngle", 14f)
            .Apply();

        // The storyboard's B3 is a third mote, behind. The turn is what the beat is
        // about; the mote is what makes a 150 degree turn something the player chooses
        // to do rather than something a prompt tells them to do.
        Beat_TurnAround_NEW b3 = Beat<Beat_TurnAround_NEW>(parent, "B3");
        Wire(b3)
            .Str("beatId", "B3")
            .Str("description", "Third mote, behind. Turning around, the player sees what they " +
                                "are travelling away from. Spatial orientation lands here. Protect it.")
            .Enum("hintMode", (int)TutorialBeat_NEW.HintMode.Show)
            .Str("hintText", "RIGHT STICK  ·  TURN AROUND")
            .Enum("advanceMode", (int)TutorialBeat_NEW.AdvanceMode.PlayerAction)
            .Ref("disc", moteC.transform)
            .Ref("lookRig", lookRig)
            .Num("minYawDegrees", 150f)
            .Num("discInFrameHalfAngle", 40f)
            .Apply();

        Beat_Confirm_NEW b4 = Beat<Beat_Confirm_NEW>(parent, "B4");
        Wire(b4)
            .Str("beatId", "B4")
            .Str("description", "The view is left off-axis. The A prompt appears at the lower " +
                                "edge. One press smoothly recentres on the travel axis.")
            // The hint line switches from the look instruction to the recentre one, so
            // the two never sit on screen together. The plate says what to do; the
            // button affordance below it says which control does it.
            .Enum("hintMode", (int)TutorialBeat_NEW.HintMode.Show)
            .Str("hintText", "A  TO  RECENTRE")
            .Enum("advanceMode", (int)TutorialBeat_NEW.AdvanceMode.PlayerAction)
            .Ref("lookRig", lookRig)
            .Str("promptText", "RECENTRE")
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
            .Str("description", "Spin-up. The disc accelerates, matter stretches into streaks, " +
                                "brightness and noise rise, audio swells.")
            .Enum("hintMode", (int)TutorialBeat_NEW.HintMode.Clear)
            .Enum("advanceMode", (int)TutorialBeat_NEW.AdvanceMode.Duration)
            .Num("duration", 12f)
            .Apply();

        Beat_Confirm_NEW c2 = Beat<Beat_Confirm_NEW>(parent, "C2");
        Wire(c2)
            .Str("beatId", "C2")
            .Str("description", "Threshold. Near blow-out white with high frequency frame jitter. " +
                                "A single A prompt pulses at centre.")
            .Enum("hintMode", (int)TutorialBeat_NEW.HintMode.Show)
            .Str("hintText", "A  TO  EMIT")
            .Enum("advanceMode", (int)TutorialBeat_NEW.AdvanceMode.PlayerAction)
            .Ref("lookRig", lookRig)
            .Str("promptText", "EMIT")
            // A means emit here, not recentre. Beat_Confirm_NEW switches the rig's
            // binding off for the duration and hands it back on exit.
            .Flag("recentreOnPress", false)
            .Apply();

        Beat_Cinematic_NEW c3 = Beat<Beat_Cinematic_NEW>(parent, "C3");
        Wire(c3)
            .Str("beatId", "C3")
            .Str("description", "Emission. One white frame, then a hard speed tunnel with matter " +
                                "streaking backwards. CRITICAL: the camera does not lock here — " +
                                "the old build did and playtesters read it as a bug.")
            .Enum("hintMode", (int)TutorialBeat_NEW.HintMode.Clear)
            .Enum("advanceMode", (int)TutorialBeat_NEW.AdvanceMode.Duration)
            .Num("duration", 8f)
            .Apply();

        Beat_LookAt_NEW c4 = Beat<Beat_LookAt_NEW>(parent, "C4");
        Wire(c4)
            .Str("beatId", "C4")
            .Str("description", "Look back. Speed settles. Turning around, the quasar is already " +
                                "a single bright point. Reuses the B1 lesson with no new control.")
            .Enum("hintMode", (int)TutorialBeat_NEW.HintMode.Show)
            .Str("hintText", "RIGHT STICK  ·  LOOK BACK")
            .Enum("advanceMode", (int)TutorialBeat_NEW.AdvanceMode.PlayerAction)
            .Ref("target", quasar.transform)
            .Ref("lookRig", lookRig)
            .Num("reticleHalfAngle", 18f)
            .Flag("activateTargetOnEnter", false)
            .Apply();

        Beat_Confirm_NEW c5 = Beat<Beat_Confirm_NEW>(parent, "C5");
        Wire(c5)
            .Str("beatId", "C5")
            .Str("description", "Orient forward. Facing forward again: empty dark, with very " +
                                "faint filaments a long way ahead.")
            .Enum("hintMode", (int)TutorialBeat_NEW.HintMode.Show)
            .Str("hintText", "A  TO  RECENTRE")
            .Enum("advanceMode", (int)TutorialBeat_NEW.AdvanceMode.PlayerAction)
            .Ref("lookRig", lookRig)
            .Str("promptText", "RECENTRE")
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

    // ── Object helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// The beat object for this storyboard frame, created if it is not there yet.
    ///
    /// Beats are children of the director and run in Hierarchy order, so a new phase
    /// appended by a later run lands after the existing ones — which is the storyboard
    /// order, because the storyboard runs A then B then C. Reordering after the fact is
    /// a drag in the Hierarchy and the builder will not undo it.
    /// </summary>
    static T Beat<T>(Transform parent, string name) where T : TutorialBeat_NEW
    {
        GameObject go = FindOrCreate(name, parent, Vector3.zero);
        return AddIfMissing<T>(go);
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
            .Num("arrivalStandoff", ArrivalStandoff)
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

    static GameObject BuildLegend(Transform canvas)
    {
        GameObject go = UIObject("Legend", canvas, new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(1200f, 34f));

        AddIfMissing<CanvasGroup>(go);

        TMP_Text text = AddText(go, "RIGHT STICK = LOOK      A = CONFIRM / RECENTRE", 22, TextAlignmentOptions.Center);

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

    /// <summary>
    /// The continue affordance: a round button glyph with A in it, and the verb beside it.
    ///
    /// GDD §5 requires this to be the same shape in the same position every time, which
    /// is why the shape lives here and only the words come from the beat. The glyph is a
    /// disc rather than a bare letter because a bare letter is read as text, and text
    /// does not look pressable.
    /// </summary>
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
        GameObject label = UIObject("Label", go.transform, new Vector2(0.5f, 0.5f), new Vector2(36f, 0f), new Vector2(380f, 64f));
        AddText(label, "RECENTRE", 32, TextAlignmentOptions.Left);

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
                        GameObject legend, GameObject reticle, GameObject hint, GameObject prompt)
    {
        Wire(hud)
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
        UnityEvent onEnter = GetEvent(beat, "onEnter");
        if (onEnter == null || HasListenerFor(onEnter, emission)) return;

        UnityAction call = System.Delegate.CreateDelegate(typeof(UnityAction), emission, method, false, false)
                           as UnityAction;

        if (call == null)
        {
            Debug.LogWarning("[TutorialSceneBuilder_NEW] TutorialEmission_NEW has no method '" +
                             method + "'. " + beat.name + " will not drive the emission.", beat);
            return;
        }

        UnityEventTools.AddPersistentListener(onEnter, call);
        EditorUtility.SetDirty(beat);
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

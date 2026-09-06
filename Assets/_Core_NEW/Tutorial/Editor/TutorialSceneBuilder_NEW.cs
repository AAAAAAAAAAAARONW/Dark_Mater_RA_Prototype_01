using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
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
/// So this is the layout, in code, where it can be read and re-run. Running it twice is
/// refused rather than duplicated — delete the [Tutorial] root and run it again.
///
/// WHAT IT REUSES. The first version of this builder made grey primitive spheres and
/// left the scene on Unity's default skybox, which is why it rendered as a blue-grey
/// void. Everything visible now comes from assets the project already has:
///
///   Custom_Nebula.mat + NP_Quasar     the sky, the same one the journey's quasar layer
///                                     uses, so the two cannot drift apart
///   BlazingQuasar.mat                 core, halo, accretion disc and bipolar jets in
///                                     one shader — A1's flow, A2's dark centre, B2's
///                                     jet channel and B3's silhouette are all this
///                                     one object
///   Custom_PhotonTrail.mat            the player's own light, with PhotonSpectrumTrail
///
/// See TutorialWorldAssets_NEW for the paths and for the two whitebox assets that get
/// generated because the project has no equivalent.
///
/// STAGING. The positions below are a defensible default, not a reading of the
/// storyboard — see the geometry note above ComposeWorld. Everything is a serialized
/// field with a gizmo, so moving it is a drag in the Scene view.
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
    /// </summary>
    static readonly Vector3 QuasarPosition = new Vector3(0f, 0f, 6000f);

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

    [MenuItem("Tools/Journey NEW/Build Tutorial Scene (Phase 0-1)", false, 40)]
    public static void Build()
    {
        if (GameObject.Find(RootName) != null)
        {
            EditorUtility.DisplayDialog(
                "Tutorial rig already present",
                "This scene already has a " + RootName + " root.\n\n" +
                "Delete it and run the builder again for a clean rig. Building on top " +
                "of the existing one would leave two directors fighting over the same beats.",
                "OK");
            return;
        }

        Undo.SetCurrentGroupName("Build tutorial scene");
        int group = Undo.GetCurrentGroup();

        GameObject root = NewObject(RootName, null, Vector3.zero);

        // ── Environment ──────────────────────────────────────────────────────
        GameObject environment = NewObject("Environment", root.transform, Vector3.zero);
        ApplySky(environment);
        DimSceneLights();

        // ── Player ───────────────────────────────────────────────────────────
        GameObject player = NewObject("Photon", root.transform, PlayerPosition);
        Camera camera = AcquireCamera(player.transform);
        FirstPersonLookRig_NEW lookRig = Undo.AddComponent<FirstPersonLookRig_NEW>(camera.gameObject);

        // ── World ────────────────────────────────────────────────────────────
        GameObject world = NewObject("World", root.transform, Vector3.zero);

        GameObject quasar = BuildQuasar(world.transform);

        BuildTravel(player, quasar.transform, lookRig);
        BuildDust(player.transform);
        BuildPhotonTrail(player.transform);

        // Motes ride with the light. See MoteAPosition.
        GameObject motes = NewObject("Motes", player.transform, PlayerPosition);

        GameObject moteA = Mote("Mote_A3_B1", motes.transform, MoteAPosition, Vector3.right, lookRig);

        GameObject moteB = Mote("Mote_B2", motes.transform, MoteBPosition, Vector3.up, lookRig);
        moteB.SetActive(false);

        GameObject moteC = Mote("Mote_B3", motes.transform, MoteCPosition, Vector3.left, lookRig);
        moteC.SetActive(false);

        // ── HUD ──────────────────────────────────────────────────────────────
        GameObject canvas = BuildCanvas(root.transform);

        GameObject reticle = BuildReticle(canvas.transform);
        GameObject legend = BuildLegend(canvas.transform);
        GameObject hint = BuildHint(canvas.transform);
        GameObject prompt = BuildConfirmPrompt(canvas.transform);
        GameObject card = BuildAttractCard(canvas.transform);

        reticle.SetActive(false);
        legend.SetActive(false);
        hint.SetActive(false);
        prompt.SetActive(false);

        // ── Director and beats ───────────────────────────────────────────────
        GameObject directorObject = NewObject("Director", root.transform, Vector3.zero);
        TutorialDirector_NEW director = Undo.AddComponent<TutorialDirector_NEW>(directorObject);

        GameObject beats = NewObject("Beats", directorObject.transform, Vector3.zero);

        BuildPhase0(beats.transform, moteA);
        BuildPhase1(beats.transform, lookRig, moteA, moteB, moteC);

        // ── HUD and attract components ───────────────────────────────────────
        TutorialHUD_NEW hud = Undo.AddComponent<TutorialHUD_NEW>(canvas);
        WireHud(hud, director, legend, reticle, hint, prompt);

        TutorialAttract_NEW attract = Undo.AddComponent<TutorialAttract_NEW>(canvas);
        WireAttract(attract, director, card, lookRig, player.GetComponent<TutorialTravel_NEW>());

        Undo.CollapseUndoOperations(group);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;

        Debug.Log("[TutorialSceneBuilder_NEW] Phase 0-1 rig built. " +
                  "Press Play: the title card waits for A, then A1 runs. " +
                  "F2 skips a beat while the debug overlay is on.", root);
    }

    // ── Environment ──────────────────────────────────────────────────────────

    /// <summary>
    /// Put the scene on the nebula skybox and hand the quasar look to TutorialSky_NEW.
    ///
    /// The skybox is assigned here, at build time, because RenderSettings is scene data:
    /// setting it at runtime only would leave the Scene view showing the default sky and
    /// make every screenshot of the tutorial wrong.
    /// </summary>
    static void ApplySky(GameObject environment)
    {
        Material nebula = TutorialWorldAssets_NEW.NebulaSkybox;
        if (nebula != null) RenderSettings.skybox = nebula;

        // Fog over a scene with no ground plane only greys the sky out.
        RenderSettings.fog = false;

        TutorialSky_NEW sky = Undo.AddComponent<TutorialSky_NEW>(environment);
        Wire(sky).Ref("profile", TutorialWorldAssets_NEW.QuasarNebulaProfile).Apply();
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
        foreach (Light light in Object.FindObjectsOfType<Light>())
        {
            if (light == null || light.type != LightType.Directional) continue;

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
        TutorialTravel_NEW travel = Undo.AddComponent<TutorialTravel_NEW>(player);

        Wire(travel)
            .Ref("destination", quasar)
            .Ref("lookRig", lookRig)
            .Num("speed", TravelSpeed)
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

        GameObject go = NewObject("Dust", parent, parent.position);

        ParticleSystem ps = Undo.AddComponent<ParticleSystem>(go);

        ParticleSystem.MainModule main = ps.main;
        main.duration = 20f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(24f, 40f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 3.2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.55f, 0.13f, 0.07f, 1f),
            new Color(0.75f, 0.30f, 0.16f, 1f));
        main.maxParticles = 1400;
        main.playOnAwake = true;

        // World space, or the dust would rotate with the emitter instead of orbiting.
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        // Unscaled, so D2's future slow motion does not freeze the whole disc.
        main.useUnscaledTime = true;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 90f;

        // A shell around the light rather than a disc: the player is flying through the
        // medium, not orbiting inside a ring.
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 160f;

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
        GameObject go = NewObject("Trail", player, player.position);

        // Offset below the eye rather than at it. A ribbon emitted from the exact camera
        // position starts inside the near plane, so the newest segment fills the screen
        // as a smear the moment the player looks anywhere but straight ahead. Dropped
        // here it stays out of the forward view and reads properly on the look back.
        go.transform.localPosition = new Vector3(0f, -1.2f, 0f);

        TrailRenderer trail = Undo.AddComponent<TrailRenderer>(go);
        trail.time = 6f;
        trail.widthMultiplier = 0.35f;
        trail.minVertexDistance = 0.05f;
        trail.autodestruct = false;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;

        Material material = TutorialWorldAssets_NEW.PhotonTrailMaterial();
        if (material != null) trail.sharedMaterial = material;

        // PhotonSpectrumTrail has [RequireComponent(typeof(TrailRenderer))], so the
        // TrailRenderer above must already be on the object.
        Undo.AddComponent<PhotonSpectrumTrail>(go);

        // Off at the start. The player has no trail in Phase 0–1: the piece opens on a
        // camera and a distant quasar and nothing else, and the light's own spectrum is
        // not something the player has been given a reason to care about yet. Phase 2
        // switches it on at the emission — wire that to C3's onEnter when it exists.
        go.SetActive(false);
    }

    // ── Phases ───────────────────────────────────────────────────────────────

    static void BuildPhase0(Transform parent, GameObject moteA)
    {
        Beat_Cinematic_NEW a1 = Beat<Beat_Cinematic_NEW>(parent, "A1");
        Wire(a1)
            .Str("beatId", "A1")
            .Str("description", "Near black. Dark red matter drifts in slow rotation deep in frame.")
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
            .Str("hintText", "LEFT STICK  ·  LOOK")
            .Enum("advanceMode", (int)TutorialBeat_NEW.AdvanceMode.PlayerAction)
            .Ref("target", moteA.transform)
            .Ref("mote", moteA.GetComponent<GuideMote_NEW>())
            .Ref("lookRig", lookRig)
            .Num("reticleHalfAngle", 12f)
            .Num("holdSeconds", 0f)
            .Apply();

        // B2 and B3 carry no hintText: the storyboard says "No new prompt" for both, and
        // an empty hint means the B1 line stays up rather than the line going away.
        Beat_LookAt_NEW b2 = Beat<Beat_LookAt_NEW>(parent, "B2");
        Wire(b2)
            .Str("beatId", "B2")
            .Str("description", "The mote passes overhead. Looking up reveals the jet channel " +
                                "running into the dark, which is the direction of travel.")
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
            .Enum("advanceMode", (int)TutorialBeat_NEW.AdvanceMode.PlayerAction)
            .Ref("lookRig", lookRig)
            .Str("promptText", "A  ·  RECENTRE")
            .Flag("recentreOnPress", true)
            .Flag("waitForRecentre", false)
            .Apply();

        // B2 and B3 reveal their motes when their own beat opens.
        AddActivateOnEnter(b2, moteB, true);
        AddActivateOnEnter(b3, moteC, true);
    }

    // ── Object helpers ───────────────────────────────────────────────────────

    static T Beat<T>(Transform parent, string name) where T : TutorialBeat_NEW
    {
        GameObject go = NewObject(name, parent, Vector3.zero);
        return Undo.AddComponent<T>(go);
    }

    static GameObject NewObject(string name, Transform parent, Vector3 position)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);

        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = position;

        return go;
    }

    /// <summary>
    /// Use the scene's existing Main Camera if there is one, rather than adding a second
    /// enabled camera and leaving the user to work out why the view is wrong.
    /// </summary>
    static Camera AcquireCamera(Transform parent)
    {
        Camera existing = Camera.main;
        Camera camera;

        if (existing != null)
        {
            Undo.SetTransformParent(existing.transform, parent, "Reparent camera");

            existing.transform.localPosition = Vector3.zero;
            existing.transform.localRotation = Quaternion.identity;

            camera = existing;
        }
        else
        {
            GameObject go = NewObject("Camera", parent, parent.position);
            go.tag = "MainCamera";

            camera = Undo.AddComponent<Camera>(go);
            Undo.AddComponent<AudioListener>(go);
        }

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
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);

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

        Material material = TutorialWorldAssets_NEW.MoteMaterial();
        MeshRenderer renderer = go.GetComponent<MeshRenderer>();

        if (renderer != null)
        {
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        GameObject lightObject = NewObject("Light", go.transform, position);
        Light light = Undo.AddComponent<Light>(lightObject);
        light.type = LightType.Point;
        light.range = 18f;
        light.intensity = 0.6f;
        light.color = new Color(1f, 0.92f, 0.72f);

        GuideMote_NEW mote = Undo.AddComponent<GuideMote_NEW>(go);
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
        GameObject go = NewObject("HUD", parent, Vector3.zero);

        Canvas canvas = Undo.AddComponent<Canvas>(go);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = Undo.AddComponent<CanvasScaler>(go);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // The Observatories display is curved and very wide. Matching height keeps the
        // legend a constant physical size as the aspect changes.
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        Undo.AddComponent<GraphicRaycaster>(go);

        return go;
    }

    static GameObject BuildReticle(Transform canvas)
    {
        GameObject go = UIObject("Reticle", canvas, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8f, 8f));

        Image image = Undo.AddComponent<Image>(go);
        image.color = new Color(1f, 1f, 1f, 0.55f);
        image.raycastTarget = false;

        return go;
    }

    static GameObject BuildLegend(Transform canvas)
    {
        GameObject go = UIObject("Legend", canvas, new Vector2(0.5f, 0f), new Vector2(0f, 48f), new Vector2(1200f, 40f));

        Undo.AddComponent<CanvasGroup>(go);

        Text text = AddText(go, "STICK = LOOK      A = CONFIRM / RECENTRE", 26, TextAnchor.MiddleCenter);
        text.color = new Color(1f, 1f, 1f, 0.75f);

        return go;
    }

    /// <summary>
    /// The control hint line. Sits above the legend: the legend is a permanent reference
    /// card, the hint is what to do right now, and they should not be read as one block.
    /// </summary>
    static GameObject BuildHint(Transform canvas)
    {
        GameObject go = UIObject("Hint", canvas, new Vector2(0.5f, 0f), new Vector2(0f, 240f), new Vector2(900f, 56f));

        Undo.AddComponent<CanvasGroup>(go);
        AddText(go, "LEFT STICK  ·  LOOK", 38, TextAnchor.MiddleCenter);

        return go;
    }

    static GameObject BuildConfirmPrompt(Transform canvas)
    {
        // GDD B4: the A prompt appears at the lower edge. GDD §5: same shape, same
        // position, every time. This rect is that position.
        GameObject go = UIObject("ConfirmPrompt", canvas, new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(400f, 90f));

        Undo.AddComponent<CanvasGroup>(go);
        AddText(go, "A", 64, TextAnchor.MiddleCenter);

        return go;
    }

    static GameObject BuildAttractCard(Transform canvas)
    {
        GameObject go = UIObject("AttractCard", canvas, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 400f));
        Undo.AddComponent<CanvasGroup>(go);

        GameObject titleObject = UIObject("Title", go.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1400f, 120f));
        AddText(titleObject, "THE JOURNEY OF LIGHT", 72, TextAnchor.MiddleCenter);

        GameObject ctaObject = UIObject("CallToAction", go.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(1400f, 60f));
        AddText(ctaObject, "PRESS A TO BEGIN", 34, TextAnchor.MiddleCenter);

        return go;
    }

    static GameObject UIObject(string name, Transform parent, Vector2 anchor, Vector2 offset, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);

        go.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = offset;
        rect.sizeDelta = size;

        return go;
    }

    static Text AddText(GameObject go, string content, int size, TextAnchor anchor)
    {
        Text text = Undo.AddComponent<Text>(go);

        text.text = content;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = Color.white;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        // Unity 2019 still ships Arial as a builtin resource. Later versions renamed it,
        // which is why this is a lookup with a fallback rather than a bare call.
        Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (font != null) text.font = font;
        else Debug.LogWarning("[TutorialSceneBuilder_NEW] No builtin font found. Assign one on " +
                              go.name + " by hand.", go);

        return text;
    }

    // ── Wiring ───────────────────────────────────────────────────────────────

    static void WireHud(TutorialHUD_NEW hud, TutorialDirector_NEW director,
                        GameObject legend, GameObject reticle, GameObject hint, GameObject prompt)
    {
        Wire(hud)
            .Ref("director", director)
            .Ref("legendRoot", legend)
            .Ref("legendText", legend.GetComponent<Text>())
            .Ref("legendGroup", legend.GetComponent<CanvasGroup>())
            .Ref("reticleRoot", reticle)
            .Ref("hintRoot", hint)
            .Ref("hintLabel", hint.GetComponent<Text>())
            .Ref("hintGroup", hint.GetComponent<CanvasGroup>())
            .Ref("confirmPromptRoot", prompt)
            .Ref("confirmPromptText", prompt.GetComponent<Text>())
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
            .Ref("titleText", title != null ? title.GetComponent<Text>() : null)
            .Ref("callToActionText", cta != null ? cta.GetComponent<Text>() : null)
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
        if (onEnter == null) return;

        UnityEventTools.AddBoolPersistentListener(onEnter, new UnityAction<bool>(target.SetActive), active);
        EditorUtility.SetDirty(beat);
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

        public Wiring(Object target)
        {
            _so = new SerializedObject(target);
            _name = target != null ? target.GetType().Name : "(null)";
        }

        public Wiring Ref(string path, Object value)
        {
            SerializedProperty p = Find(path);
            if (p != null) p.objectReferenceValue = value;
            return this;
        }

        public Wiring Str(string path, string value)
        {
            SerializedProperty p = Find(path);
            if (p != null) p.stringValue = value;
            return this;
        }

        public Wiring Num(string path, float value)
        {
            SerializedProperty p = Find(path);
            if (p != null) p.floatValue = value;
            return this;
        }

        public Wiring Flag(string path, bool value)
        {
            SerializedProperty p = Find(path);
            if (p != null) p.boolValue = value;
            return this;
        }

        public Wiring Enum(string path, int value)
        {
            SerializedProperty p = Find(path);
            if (p != null) p.enumValueIndex = value;
            return this;
        }

        public Wiring Vec(string path, Vector3 value)
        {
            SerializedProperty p = Find(path);
            if (p != null) p.vector3Value = value;
            return this;
        }

        public void Apply()
        {
            _so.ApplyModifiedPropertiesWithoutUndo();
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

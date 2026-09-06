using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Builds the Phase 0–1 tutorial rig into the open scene, wired and playable.
///
/// Tutorial.unity is an empty scene. Phase 0–1 is seven beats, a first person rig, three
/// HUD elements, an attract card and two motes, and every one of those has references
/// into the others. Hand-wiring that is around forty Inspector drags, which is forty
/// chances to point B2's gate at B1's mote and spend an afternoon on it. The README for
/// _Core_NEW already records what happens when scene layout is assembled by hand over
/// time: it drifts, and nobody can see that it has.
///
/// So this is the layout, in code, where it can be read and re-run. Running it twice is
/// refused rather than duplicated — delete the [Tutorial] root and run it again to get a
/// clean rig back.
///
/// The world objects it creates are whitebox stand-ins, named Placeholder_*. GDD §9
/// lists the real assets and their status; nothing here is trying to be them. The
/// positions are, though: the mote sits off the right edge of the opening view because
/// A3 says it drifts out of frame at the right edge, and the disc sits behind the player
/// because B3 is a turn of more than 150°. Move the art, keep the geometry.
/// </summary>
public static class TutorialSceneBuilder_NEW
{
    const string RootName = "[Tutorial]";

    // The jet axis is +Z. Everything below is positioned relative to that.
    static readonly Vector3 PlayerPosition = Vector3.zero;

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

        // ── Player and camera ────────────────────────────────────────────────
        GameObject player = NewObject("Player", root.transform, PlayerPosition);
        Camera camera = AcquireCamera(player.transform);
        FirstPersonLookRig_NEW lookRig = Undo.AddComponent<FirstPersonLookRig_NEW>(camera.gameObject);

        // ── World placeholders ───────────────────────────────────────────────
        GameObject world = NewObject("World", root.transform, Vector3.zero);

        GameObject disc = Placeholder("Placeholder_Disc_And_BlackHole", world.transform,
                                      new Vector3(0f, 0f, -30f), 8f);

        // A3's mote and B1's mote are the same mote. A3 shows it leaving frame at the
        // right edge; B1 is the player going after it.
        GameObject moteA = Mote("Mote_A3_B1", world.transform, new Vector3(5f, 0.4f, 10f), Vector3.right);

        // B2 looks up: the jet channel running into the dark, which is the direction
        // of travel. The second mote sits in it.
        GameObject moteB = Mote("Mote_B2", world.transform, new Vector3(1.5f, 9f, 11f), Vector3.up);
        moteB.SetActive(false);

        // ── HUD ──────────────────────────────────────────────────────────────
        GameObject canvas = BuildCanvas(root.transform);

        GameObject reticle = BuildReticle(canvas.transform);
        GameObject legend = BuildLegend(canvas.transform);
        GameObject prompt = BuildConfirmPrompt(canvas.transform);
        GameObject card = BuildAttractCard(canvas.transform);

        reticle.SetActive(false);
        legend.SetActive(false);
        prompt.SetActive(false);

        // ── Director and beats ───────────────────────────────────────────────
        GameObject directorObject = NewObject("Director", root.transform, Vector3.zero);
        TutorialDirector_NEW director = Undo.AddComponent<TutorialDirector_NEW>(directorObject);

        GameObject beats = NewObject("Beats", directorObject.transform, Vector3.zero);

        BuildPhase0(beats.transform, moteA);
        BuildPhase1(beats.transform, lookRig, moteA, moteB, disc);

        // ── HUD and attract components ───────────────────────────────────────
        TutorialHUD_NEW hud = Undo.AddComponent<TutorialHUD_NEW>(canvas);
        WireHud(hud, director, legend, reticle, prompt);

        TutorialAttract_NEW attract = Undo.AddComponent<TutorialAttract_NEW>(canvas);
        WireAttract(attract, director, card, lookRig);

        Undo.CollapseUndoOperations(group);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;

        Debug.Log("[TutorialSceneBuilder_NEW] Phase 0-1 rig built. " +
                  "Press Play: the title card waits for A, then A1 runs. " +
                  "F2 skips a beat while the debug overlay is on.", root);
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
                            GameObject moteA, GameObject moteB, GameObject disc)
    {
        Beat_LookAt_NEW b1 = Beat<Beat_LookAt_NEW>(parent, "B1");
        Wire(b1)
            .Str("beatId", "B1")
            .Str("description", "Player turns right, catches the mote, it blooms into a ripple.")
            .Enum("advanceMode", (int)TutorialBeat_NEW.AdvanceMode.PlayerAction)
            .Ref("target", moteA.transform)
            .Ref("mote", moteA.GetComponent<GuideMote_NEW>())
            .Ref("lookRig", lookRig)
            .Num("reticleHalfAngle", 12f)
            .Num("holdSeconds", 0f)
            .Apply();

        Beat_LookAt_NEW b2 = Beat<Beat_LookAt_NEW>(parent, "B2");
        Wire(b2)
            .Str("beatId", "B2")
            .Str("description", "Looking up reveals the jet channel running into the dark, " +
                                "which is the direction of travel.")
            .Enum("advanceMode", (int)TutorialBeat_NEW.AdvanceMode.PlayerAction)
            .Ref("target", moteB.transform)
            .Ref("mote", moteB.GetComponent<GuideMote_NEW>())
            .Ref("lookRig", lookRig)
            .Num("reticleHalfAngle", 14f)
            .Apply();

        Beat_TurnAround_NEW b3 = Beat<Beat_TurnAround_NEW>(parent, "B3");
        Wire(b3)
            .Str("beatId", "B3")
            .Str("description", "Turning around, the black hole silhouette and the full disc, " +
                                "seen for the first time. Spatial orientation lands here. Protect it.")
            .Enum("advanceMode", (int)TutorialBeat_NEW.AdvanceMode.PlayerAction)
            .Ref("disc", disc.transform)
            .Ref("lookRig", lookRig)
            .Num("minYawDegrees", 150f)
            .Num("discInFrameHalfAngle", 35f)
            .Apply();

        Beat_Confirm_NEW b4 = Beat<Beat_Confirm_NEW>(parent, "B4");
        Wire(b4)
            .Str("beatId", "B4")
            .Str("description", "View is left off-axis. The A prompt appears at the lower edge.")
            .Enum("advanceMode", (int)TutorialBeat_NEW.AdvanceMode.PlayerAction)
            .Ref("lookRig", lookRig)
            .Str("promptText", "A")
            .Flag("recentreOnPress", true)
            .Flag("waitForRecentre", false)
            .Apply();
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

        if (existing != null)
        {
            Undo.SetTransformParent(existing.transform, parent, "Reparent camera");

            existing.transform.localPosition = Vector3.zero;
            existing.transform.localRotation = Quaternion.identity;

            return existing;
        }

        GameObject go = NewObject("Camera", parent, Vector3.zero);
        go.tag = "MainCamera";

        Camera camera = Undo.AddComponent<Camera>(go);
        Undo.AddComponent<AudioListener>(go);

        return camera;
    }

    static GameObject Placeholder(string name, Transform parent, Vector3 position, float scale)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);

        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = Vector3.one * scale;

        // Whitebox geometry, not a physics object. A collider here would only ever
        // catch something by accident.
        Collider collider = go.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider);

        return go;
    }

    static GameObject Mote(string name, Transform parent, Vector3 position, Vector3 drift)
    {
        GameObject go = Placeholder(name, parent, position, 0.4f);

        GameObject lightObject = NewObject("Light", go.transform, position);
        Light light = Undo.AddComponent<Light>(lightObject);
        light.type = LightType.Point;
        light.range = 12f;
        light.intensity = 0.6f;
        light.color = new Color(1f, 0.92f, 0.72f);

        GuideMote_NEW mote = Undo.AddComponent<GuideMote_NEW>(go);
        Wire(mote)
            .Vec("driftDirection", drift)
            .Ref("moteLight", light)
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
                        GameObject legend, GameObject reticle, GameObject prompt)
    {
        Wire(hud)
            .Ref("director", director)
            .Ref("legendRoot", legend)
            .Ref("legendText", legend.GetComponent<Text>())
            .Ref("legendGroup", legend.GetComponent<CanvasGroup>())
            .Ref("reticleRoot", reticle)
            .Ref("confirmPromptRoot", prompt)
            .Ref("confirmPromptText", prompt.GetComponent<Text>())
            .Ref("promptGroup", prompt.GetComponent<CanvasGroup>())
            .Str("legendFirstBeatId", "B1")
            .Apply();
    }

    static void WireAttract(TutorialAttract_NEW attract, TutorialDirector_NEW director,
                            GameObject card, FirstPersonLookRig_NEW lookRig)
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

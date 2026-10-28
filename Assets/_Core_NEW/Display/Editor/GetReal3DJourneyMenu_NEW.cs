using Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

/// <summary>
/// Fits the open scene's journey camera to getReal3D: GameObject > Vizlab > Adapt Journey
/// Camera to getReal3D. Written for Vizlab_Journey; the journey's twin of Adapt Tutorial
/// Camera to getReal3D (GetReal3DTutorialMenu_NEW), and it builds the same thing, for the
/// reasons given on GetReal3DView_NEW:
///
///     Main Camera           CinemachineBrain, the lens (stops rendering in play mode)
///       [getReal3D]         GetReal3DView_NEW, getRealUser, GetReal3DHud_NEW
///         Eye               Camera, BuiltInFinalUIOverlay, getRealCameraUpdater,
///                           GetReal3DLens_NEW, post, GetReal3DHudDraw_NEW
///
/// WHAT IS DIFFERENT FROM THE TUTORIAL. The journey has no FirstPersonLookRig_NEW: its
/// camera belongs to Cinemachine. The brain writes the live virtual camera's pose and lens
/// into its Camera every frame, so the eye hangs under the brain's object and rides
/// wherever the brain puts it - every FreeLook, every blend, the intro shot and the dives -
/// with no Cinemachine setting touched. The brain's Camera stays as the lens:
/// JourneyZoom_NEW and LayerDive_NEW go on scaling its field of view after the brain, and
/// GetReal3DLens_NEW magnifies the wall by the same ratio, measured against
/// JourneyZoom_NEW's base field of view (40). That ratio covers what the shots do with the
/// lens too: the Macro rig's 30 reads a third closer on the wall, as it does on a monitor,
/// and the intro's push-in from 72 still pushes in.
///
/// Two things only the journey has:
///
///   * The world labels (quasarUI, earthUI and the rest, layer UIOverlay) are not in the
///     camera's culling mask; BuiltInFinalUIOverlay draws them after post-processing. The
///     eye gets its own, set to follow getReal3D's projection, ahead of the updater so the
///     labels are composited before getReal3D has the picture.
///
///   * AbsorberHighlight_NEW takes Camera.main when its viewCamera is empty, and on the
///     wall Camera.main is the eye - a different projection on every node, so the trail
///     could be mirrored on one node and not the next. It is pointed at the lens instead.
///
/// It works on the scene as loaded, every change is one undo step, and running it again
/// brings what it built up to date rather than adding a second eye. Untick [getReal3D] to go
/// back to the desktop camera.
/// </summary>
static class GetReal3DJourneyMenu_NEW
{
    const string OriginName = "[getReal3D]";
    const string EyeName = "Eye";
    const string MainCameraTag = "MainCamera";

    /// <summary>JourneyZoom_NEW's default base field of view, when the scene has no zoom.</summary>
    const float DefaultReferenceFieldOfView = 40f;

    [MenuItem("GameObject/Vizlab/Adapt Journey Camera to getReal3D", false, 19)]
    static void Adapt()
    {
        CinemachineBrain brain = FindWiredBrain();
        if (brain == null)
        {
            Debug.LogError("Vizlab: no CinemachineBrain with a Camera in this scene, so there is no journey camera to adapt.");
            return;
        }

        Camera lens = brain.GetComponent<Camera>();

        Undo.SetCurrentGroupName("Adapt Journey Camera to getReal3D");
        int group = Undo.GetCurrentGroup();

        // The brain, its camera and everything above them have to be on, or nothing moves the eye.
        for (Transform t = brain.transform; t != null; t = t.parent)
        {
            if (t.gameObject.activeSelf) continue;
            Undo.RecordObject(t.gameObject, "Switch the journey camera back on");
            t.gameObject.SetActive(true);
            Debug.LogWarning("Vizlab: switched '" + t.name + "' back on. The journey's CinemachineBrain is on it.", t.gameObject);
        }
        foreach (Behaviour b in new Behaviour[] { brain, lens })
        {
            if (b.enabled) continue;
            Undo.RecordObject(b, "Enable journey camera component");
            b.enabled = true;
        }

        // A getReal3D player's own movers and cameras on the brain or above it: off, the objects stay.
        for (Transform t = brain.transform; t != null; t = t.parent)
        {
            foreach (System.Type type in GetReal3DTutorialMenu_NEW.PlayerComponents)
            {
                foreach (Component c in t.GetComponents(type))
                {
                    var b = c as Behaviour;
                    if (b == null || !b.enabled) continue;
                    Undo.RecordObject(b, "Switch off getReal3D mover");
                    b.enabled = false;
                    Debug.LogWarning("Vizlab: switched off " + type.Name + " on '" + t.name + "'. It would move or " +
                                     "render the journey's camera itself.", t.gameObject);
                }
            }
        }

        // ── [getReal3D]: the tracker origin, under the brain ──
        GetReal3DView_NEW view = brain.GetComponentInChildren<GetReal3DView_NEW>(true);
        GameObject origin;
        if (view != null)
        {
            origin = view.gameObject;
        }
        else
        {
            origin = new GameObject(OriginName);
            Undo.RegisterCreatedObjectUndo(origin, "Create getReal3D origin");
            Undo.SetTransformParent(origin.transform, brain.transform, "Parent getReal3D origin");
            origin.transform.localPosition = Vector3.zero;
            origin.transform.localRotation = Quaternion.identity;
            origin.transform.localScale = Vector3.one;
            view = Undo.AddComponent<GetReal3DView_NEW>(origin);
        }
        origin.layer = brain.gameObject.layer;

        // getRealCameraUpdater looks for its user in its parents; without one it adds one
        // to the scene root at runtime, or borrows any other in the scene with an error.
        if (origin.GetComponent<getRealUser>() == null) Undo.AddComponent<getRealUser>(origin);

        var viewSO = new SerializedObject(view);
        viewSO.FindProperty("rigCamera").objectReferenceValue = lens;
        viewSO.ApplyModifiedProperties();

        // ── Eye: the camera getReal3D drives ──
        Transform eyeT = origin.transform.Find(EyeName);
        GameObject eye;
        if (eyeT != null)
        {
            eye = eyeT.gameObject;
        }
        else
        {
            eye = new GameObject(EyeName);
            Undo.RegisterCreatedObjectUndo(eye, "Create getReal3D eye");
            Undo.SetTransformParent(eye.transform, origin.transform, "Parent getReal3D eye");
            eye.transform.localPosition = Vector3.zero;
            eye.transform.localRotation = Quaternion.identity;
            eye.transform.localScale = Vector3.one;
        }
        Undo.RecordObject(eye, "Set eye layer and tag");
        eye.layer = brain.gameObject.layer;
        eye.tag = MainCameraTag;

        Camera eyeCamera = eye.GetComponent<Camera>();
        if (eyeCamera == null) eyeCamera = Undo.AddComponent<Camera>(eye);
        GetReal3DTutorialMenu_NEW.CopyValues(lens, eyeCamera);
        Undo.RecordObject(eyeCamera, "Enable eye camera");
        eyeCamera.enabled = true;

        // The world labels' compositor goes on before the updater, so that in the
        // OnRenderImage chain the labels are on the picture before getReal3D takes it.
        BuiltInFinalUIOverlay lensLabels = lens.GetComponent<BuiltInFinalUIOverlay>();
        BuiltInFinalUIOverlay eyeLabels = eye.GetComponent<BuiltInFinalUIOverlay>();
        if (lensLabels != null && lensLabels.enabled)
        {
            if (eyeLabels == null) eyeLabels = Undo.AddComponent<BuiltInFinalUIOverlay>(eye);
            GetReal3DTutorialMenu_NEW.CopyValues(lensLabels, eyeLabels);
            var labelsSO = new SerializedObject(eyeLabels);
            labelsSO.FindProperty("copyFromMainCameraEachFrame").boolValue = true;
            labelsSO.FindProperty("copyProjectionMatrix").boolValue = true;
            labelsSO.ApplyModifiedProperties();
        }

        getRealCameraUpdater updater = eye.GetComponent<getRealCameraUpdater>();
        if (updater == null) updater = Undo.AddComponent<getRealCameraUpdater>(eye);

        if (eyeLabels != null) MoveAbove(eyeLabels, updater);

        // Order matters: the lens has to run after the updater in OnPreCull/OnPreRender.
        GetReal3DLens_NEW eyeLens = eye.GetComponent<GetReal3DLens_NEW>();
        if (eyeLens == null) eyeLens = Undo.AddComponent<GetReal3DLens_NEW>(eye);
        var lensSO = new SerializedObject(eyeLens);
        lensSO.FindProperty("lensSource").objectReferenceValue = lens;
        lensSO.FindProperty("referenceFieldOfView").floatValue = ReferenceFieldOfView();
        lensSO.ApplyModifiedProperties();

        // Same grading, AA and fog as the desktop camera. The volume stays where it is.
        PostProcessLayer lensPost = lens.GetComponent<PostProcessLayer>();
        if (lensPost != null)
        {
            PostProcessLayer eyePost = eye.GetComponent<PostProcessLayer>();
            if (eyePost == null) eyePost = Undo.AddComponent<PostProcessLayer>(eye);
            GetReal3DTutorialMenu_NEW.CopyValues(lensPost, eyePost);
            Undo.RecordObject(eyePost, "Point post-processing at the eye");
            eyePost.volumeTrigger = eye.transform;
        }

        // ── The HUD, onto the wall's rows (GetReal3DHud_NEW) ──
        // Drawn last on the eye, after the post-processing above has had the picture.
        if (eye.GetComponent<GetReal3DHudDraw_NEW>() == null) Undo.AddComponent<GetReal3DHudDraw_NEW>(eye);

        GetReal3DHud_NEW hud = origin.GetComponent<GetReal3DHud_NEW>();
        if (hud == null) hud = Undo.AddComponent<GetReal3DHud_NEW>(origin);

        var overlays = new System.Collections.Generic.List<Canvas>();
        foreach (Canvas c in GetReal3DTutorialMenu_NEW.InScene<Canvas>())
        {
            // Root overlays only: nested canvases follow their root, and a canvas already in
            // world space is already where it belongs.
            if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay) overlays.Add(c);
        }

        var hudSO = new SerializedObject(hud);
        SerializedProperty list = hudSO.FindProperty("canvases");
        list.arraySize = overlays.Count;
        for (int i = 0; i < overlays.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = overlays[i];
        hudSO.FindProperty("shader").objectReferenceValue = Shader.Find("Hidden/Journey/GetReal3DWallHud_NEW");
        hudSO.ApplyModifiedProperties();

        if (hudSO.FindProperty("shader").objectReferenceValue == null)
            Debug.LogError("Vizlab: the wall HUD shader (Hidden/Journey/GetReal3DWallHud_NEW) was not found. " +
                           "Let Unity finish importing, then run this again.", hud);

        string canvasNames = "";
        foreach (Canvas c in overlays) canvasNames += (canvasNames.Length > 0 ? ", " : "") + c.name;
        Debug.Log("Vizlab: on the wall, " + (overlays.Count > 0 ? canvasNames : "no overlay canvas") +
                  " will be drawn onto the seven rows. In the editor they stay overlays; set Mode to " +
                  "Always on " + origin.name + "'s GetReal3DHud_NEW to see the rows here.", hud);

        // ── Scripts that would otherwise project through the eye ──
        int pointed = 0;
        foreach (AbsorberHighlight_NEW highlight in GetReal3DTutorialMenu_NEW.InScene<AbsorberHighlight_NEW>())
        {
            var so = new SerializedObject(highlight);
            SerializedProperty viewCamera = so.FindProperty("viewCamera");
            if (viewCamera.objectReferenceValue == lens) continue;
            if (viewCamera.objectReferenceValue != null && viewCamera.objectReferenceValue != eyeCamera) continue;
            viewCamera.objectReferenceValue = lens;
            so.ApplyModifiedProperties();
            pointed++;
        }

        // ── Anything else getReal3D that brings its own camera ──
        int switchedOff = 0;
        var others = new System.Collections.Generic.List<Component>();
        // Inactive ones too: an earlier run may have switched off the camera but left the
        // player above it - its walk and look scripts - running.
        others.AddRange(GetReal3DTutorialMenu_NEW.InScene<getRealCameraUpdater>());
        others.AddRange(GetReal3DTutorialMenu_NEW.InScene<GetReal3DView_NEW>());   // left by an earlier run elsewhere
        foreach (Component other in others)
        {
            if (other.transform.IsChildOf(origin.transform) || other.gameObject == origin) continue;

            // On the brain or above it: its component was switched off above, and the
            // object must stay - switching it off would black the screen out.
            if (brain.transform.IsChildOf(other.transform)) continue;

            // Otherwise the highest object that holds this player but not the brain.
            Transform top = other.transform;
            while (top.parent != null && !brain.transform.IsChildOf(top.parent)) top = top.parent;

            GameObject player = top.gameObject;
            if (!player.activeSelf) continue;
            Undo.RecordObject(player, "Switch off other getReal3D player");
            player.SetActive(false);
            switchedOff++;
            Debug.LogWarning("Vizlab: switched off '" + player.name + "'. It has its own getReal3D camera " +
                             "and would render over the journey's. Delete it if it is not needed.", player);
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(brain.gameObject.scene);
        Selection.activeObject = eye;

        Debug.Log("Vizlab: '" + brain.name + "' now renders through getReal3D (" + origin.name + "/" + eye.name +
                  "). Cinemachine still moves it; its own camera stays as the lens and stops rendering in " +
                  "play mode; untick " + origin.name + " to go back to it. Input reads getReal3D.Input while it is on." +
                  (eyeLabels != null ? " The world labels (BuiltInFinalUIOverlay) are drawn on the eye too." : "") +
                  (pointed > 0 ? " Pointed " + pointed + " AbsorberHighlight_NEW at the lens." : "") +
                  (switchedOff > 0 ? " Switched off " + switchedOff + " other getReal3D player(s)." : "") +
                  " Save the scene to keep it.", eye);
    }

    /// <summary>
    /// The brain the scene's scripts are wired to: the one the most serialized references
    /// point at, inactive objects included. Most journey scripts find the brain at runtime
    /// and hold no reference, so ties go to the one on the MainCamera-tagged object, then to
    /// an active one. Brains with no Camera are not candidates.
    /// </summary>
    static CinemachineBrain FindWiredBrain()
    {
        var brains = new System.Collections.Generic.List<CinemachineBrain>();
        foreach (CinemachineBrain b in GetReal3DTutorialMenu_NEW.InScene<CinemachineBrain>())
            if (b.GetComponent<Camera>() != null) brains.Add(b);

        if (brains.Count <= 1) return brains.Count == 1 ? brains[0] : null;

        var counts = new System.Collections.Generic.Dictionary<CinemachineBrain, int>();
        foreach (CinemachineBrain b in brains) counts[b] = 0;

        foreach (MonoBehaviour mb in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
        {
            if (mb == null || EditorUtility.IsPersistent(mb) || !mb.gameObject.scene.IsValid()) continue;
            if (mb is CinemachineBrain) continue;

            SerializedProperty p = new SerializedObject(mb).GetIterator();
            while (p.Next(true))
            {
                if (p.propertyType != SerializedPropertyType.ObjectReference) continue;
                var target = p.objectReferenceValue as CinemachineBrain;
                if (target != null && counts.ContainsKey(target)) counts[target]++;
            }
        }

        CinemachineBrain best = null;
        foreach (CinemachineBrain b in brains)
        {
            if (best == null || counts[b] > counts[best]) { best = b; continue; }
            if (counts[b] < counts[best]) continue;

            bool bTagged = b.CompareTag(MainCameraTag), bestTagged = best.CompareTag(MainCameraTag);
            if (bTagged != bestTagged) { if (bTagged) best = b; continue; }
            if (b.isActiveAndEnabled && !best.isActiveAndEnabled) best = b;
        }

        Debug.Log("Vizlab: " + brains.Count + " Cinemachine brains in the scene; using the one on '" + best.name +
                  "', which " + counts[best] + " journey references point at.", best);
        return best;
    }

    /// <summary>
    /// The field of view the wall shows unmagnified: JourneyZoom_NEW's base field, which the
    /// zoom is measured against, so the wall and the zoom gauge agree on what "unzoomed" is.
    /// </summary>
    static float ReferenceFieldOfView()
    {
        foreach (JourneyZoom_NEW zoom in GetReal3DTutorialMenu_NEW.InScene<JourneyZoom_NEW>())
        {
            float fov = new SerializedObject(zoom).FindProperty("baseFieldOfView").floatValue;
            if (fov > 0f) return fov;
        }
        return DefaultReferenceFieldOfView;
    }

    /// <summary>Moves a component up its GameObject's list until it sits above another.</summary>
    static void MoveAbove(Component c, Component other)
    {
        Component[] all = c.GetComponents<Component>();
        int from = System.Array.IndexOf(all, c), to = System.Array.IndexOf(all, other);
        for (int i = from; i > to; i--)
            if (!UnityEditorInternal.ComponentUtility.MoveComponentUp(c)) break;
    }

    [MenuItem("GameObject/Vizlab/Adapt Journey Camera to getReal3D", true, 19)]
    static bool AdaptValidate()
    {
        return !EditorApplication.isPlaying;
    }
}

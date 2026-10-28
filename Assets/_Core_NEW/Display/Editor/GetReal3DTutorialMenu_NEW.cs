using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

/// <summary>
/// Fits the open scene's tutorial camera to getReal3D: GameObject > Vizlab > Adapt Tutorial
/// Camera to getReal3D. Written for Vizlab_tut; works on any scene with a
/// FirstPersonLookRig_NEW. What it builds, and why it is not the GenericPlayer prefab the
/// VizLab guide names, is on GetReal3DView_NEW.
///
/// It works on the scene as loaded in the editor, not on the file, so it is safe to run
/// with unsaved changes open, and every change is one undo step. Running it again finds
/// what it built before and brings it up to date instead of adding a second eye - which is
/// what to do after Tools > Journey NEW > Tutorial > Build or Update touches the camera.
///
/// Any other getReal3D player already in the scene (getRealPlayerController, which the
/// plugin has deprecated, or a GenericPlayer) brings its own camera and navigation and
/// would fight this one, so it is switched off - not deleted - and named in the Console.
///
/// WHICH RIG. Not simply the first active one. The tutorial's scripts hold references to
/// ONE FirstPersonLookRig_NEW (the director, the beats, the zoom, the motes - about
/// seventeen of them in Vizlab_tut), and a second rig added elsewhere, say onto getReal3D's
/// own MainCamera, is invisible to all of them: the light travels, but no beat ever sees
/// the player look. So the rig chosen is the one the scene's scripts point at, found even
/// if it has been switched off, and switched back on. The first version of this command
/// took the first active rig, found it sitting on a getReal3D camera, and switched that
/// whole object off as "another player" - rig, eye and all - which is a black screen.
/// </summary>
static class GetReal3DTutorialMenu_NEW
{
    const string OriginName = "[getReal3D]";
    const string EyeName = "Eye";
    const string MainCameraTag = "MainCamera";

    /// <summary>
    /// getReal3D components that move or render a player of their own. On the rig, or on
    /// anything above it, each one fights the rig for its transform or its camera, so they
    /// are switched off there rather than the object they sit on.
    /// </summary>
    static readonly System.Type[] PlayerComponents =
    {
        typeof(getRealCameraUpdater), typeof(getRealHeadUpdater), typeof(getRealWandUpdater),
        typeof(getRealWalkthruController), typeof(getRealAimAndGoController),
        typeof(getRealJoyLook), typeof(getRealWandLook), typeof(CharacterMotorC),
        typeof(Navigation), typeof(getReal3D.GenericHeadUpdater), typeof(getReal3D.GenericWandUpdater),
    };

    [MenuItem("GameObject/Vizlab/Adapt Tutorial Camera to getReal3D", false, 18)]
    static void Adapt()
    {
        FirstPersonLookRig_NEW rig = FindWiredRig();
        if (rig == null)
        {
            Debug.LogError("Vizlab: no FirstPersonLookRig_NEW in this scene, so there is no tutorial camera to adapt.");
            return;
        }

        Camera lens = rig.GetComponent<Camera>();
        if (lens == null)
        {
            Debug.LogError("Vizlab: the look rig on '" + rig.name + "' has no Camera to take settings from.", rig);
            return;
        }

        Undo.SetCurrentGroupName("Adapt Tutorial Camera to getReal3D");
        int group = Undo.GetCurrentGroup();

        // The rig, its components and everything above it have to be on, or nothing renders.
        for (Transform t = rig.transform; t != null; t = t.parent)
        {
            if (t.gameObject.activeSelf) continue;
            Undo.RecordObject(t.gameObject, "Switch the look rig back on");
            t.gameObject.SetActive(true);
            Debug.LogWarning("Vizlab: switched '" + t.name + "' back on. The tutorial's look rig is on it, " +
                             "and every tutorial script is wired to that rig.", t.gameObject);
        }
        foreach (Behaviour b in new Behaviour[] { rig, lens, rig.GetComponent<TutorialZoom_NEW>() })
        {
            if (b == null || b.enabled) continue;
            Undo.RecordObject(b, "Enable look rig component");
            b.enabled = true;
        }

        // A getReal3D player's own movers and cameras on the rig or above it: off, the objects stay.
        for (Transform t = rig.transform; t != null; t = t.parent)
        {
            foreach (System.Type type in PlayerComponents)
            {
                foreach (Component c in t.GetComponents(type))
                {
                    var b = c as Behaviour;
                    if (b == null || !b.enabled) continue;
                    Undo.RecordObject(b, "Switch off getReal3D mover");
                    b.enabled = false;
                    Debug.LogWarning("Vizlab: switched off " + type.Name + " on '" + t.name + "'. It would move or " +
                                     "render the tutorial's camera itself.", t.gameObject);
                }
            }
        }

        // Rigs nobody is wired to read the pad too, and would recentre on A beside the real one.
        foreach (FirstPersonLookRig_NEW other in SceneRigs())
        {
            if (other == rig || !other.enabled) continue;
            Undo.RecordObject(other, "Switch off unwired look rig");
            other.enabled = false;
            Debug.LogWarning("Vizlab: switched off the extra FirstPersonLookRig_NEW on '" + other.name +
                             "'. No tutorial script points at it; the rig on '" + rig.name + "' is the one they use.", other);
        }

        // ── [getReal3D]: the tracker origin, under the rig ──
        GetReal3DView_NEW view = rig.GetComponentInChildren<GetReal3DView_NEW>(true);
        GameObject origin;
        if (view != null)
        {
            origin = view.gameObject;
        }
        else
        {
            origin = new GameObject(OriginName);
            Undo.RegisterCreatedObjectUndo(origin, "Create getReal3D origin");
            Undo.SetTransformParent(origin.transform, rig.transform, "Parent getReal3D origin");
            origin.transform.localPosition = Vector3.zero;
            origin.transform.localRotation = Quaternion.identity;
            origin.transform.localScale = Vector3.one;
            view = Undo.AddComponent<GetReal3DView_NEW>(origin);
        }
        origin.layer = rig.gameObject.layer;

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
        eye.layer = rig.gameObject.layer;
        eye.tag = MainCameraTag;

        // Order matters: the lens has to run after the updater in OnPreCull/OnPreRender.
        Camera eyeCamera = eye.GetComponent<Camera>();
        if (eyeCamera == null) eyeCamera = Undo.AddComponent<Camera>(eye);
        CopyValues(lens, eyeCamera);
        Undo.RecordObject(eyeCamera, "Enable eye camera");
        eyeCamera.enabled = true;

        if (eye.GetComponent<getRealCameraUpdater>() == null) Undo.AddComponent<getRealCameraUpdater>(eye);

        GetReal3DLens_NEW eyeLens = eye.GetComponent<GetReal3DLens_NEW>();
        if (eyeLens == null) eyeLens = Undo.AddComponent<GetReal3DLens_NEW>(eye);
        var lensSO = new SerializedObject(eyeLens);
        lensSO.FindProperty("lensSource").objectReferenceValue = lens;
        lensSO.FindProperty("referenceFieldOfView").floatValue = lens.fieldOfView;
        lensSO.ApplyModifiedProperties();

        // Same grading, AA and fog as the desktop camera. The volume stays on the rig: it is
        // global, so where it sits does not matter.
        PostProcessLayer rigPost = rig.GetComponent<PostProcessLayer>();
        if (rigPost != null)
        {
            PostProcessLayer eyePost = eye.GetComponent<PostProcessLayer>();
            if (eyePost == null) eyePost = Undo.AddComponent<PostProcessLayer>(eye);
            CopyValues(rigPost, eyePost);
            Undo.RecordObject(eyePost, "Point post-processing at the eye");
            eyePost.volumeTrigger = eye.transform;
        }

        // ── The HUD, onto the wall's rows (GetReal3DHud_NEW) ──
        // Drawn last on the eye, after the post-processing above has had the picture.
        if (eye.GetComponent<GetReal3DHudDraw_NEW>() == null) Undo.AddComponent<GetReal3DHudDraw_NEW>(eye);

        GetReal3DHud_NEW hud = origin.GetComponent<GetReal3DHud_NEW>();
        if (hud == null) hud = Undo.AddComponent<GetReal3DHud_NEW>(origin);

        var overlays = new System.Collections.Generic.List<Canvas>();
        foreach (Canvas c in InScene<Canvas>())
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
                  " will be drawn onto the seven rows. In the editor it stays an overlay; set Mode to " +
                  "Always on " + origin.name + "'s GetReal3DHud_NEW to see the rows here.", hud);

        // ── Anything else getReal3D that brings its own camera ──
        int switchedOff = 0;
        var others = new System.Collections.Generic.List<Component>();
        // Inactive ones too: an earlier run may have switched off the camera but left the
        // player above it - its walk and look scripts - running.
        others.AddRange(InScene<getRealCameraUpdater>());
        others.AddRange(InScene<GetReal3DView_NEW>());   // left by an earlier run on another rig
        foreach (Component other in others)
        {
            if (other.transform.IsChildOf(origin.transform) || other.gameObject == origin) continue;

            // On the rig or above it: its component was switched off above, and the object
            // must stay - switching it off is what blacked the screen out.
            if (rig.transform.IsChildOf(other.transform)) continue;

            // Otherwise the highest object that holds this player but not the rig: the
            // whole getRealPlayerController, without taking Photon or [Tutorial] with it.
            Transform top = other.transform;
            while (top.parent != null && !rig.transform.IsChildOf(top.parent)) top = top.parent;

            GameObject player = top.gameObject;
            if (!player.activeSelf) continue;
            Undo.RecordObject(player, "Switch off other getReal3D player");
            player.SetActive(false);
            switchedOff++;
            Debug.LogWarning("Vizlab: switched off '" + player.name + "'. It has its own getReal3D camera " +
                             "and would render over the tutorial's. Delete it if it is not needed.", player);
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);
        Selection.activeObject = eye;

        Debug.Log("Vizlab: '" + rig.name + "' now renders through getReal3D (" + origin.name + "/" + eye.name +
                  "). The rig's own camera stays as the lens and stops rendering in play mode; untick " +
                  origin.name + " to go back to it. Input reads getReal3D.Input while it is on." +
                  (switchedOff > 0 ? " Switched off " + switchedOff + " other getReal3D player(s)." : "") +
                  " Save the scene to keep it.", eye);
    }

    /// <summary>Every FirstPersonLookRig_NEW in the open scenes, switched on or not.</summary>
    static System.Collections.Generic.List<FirstPersonLookRig_NEW> SceneRigs()
    {
        return InScene<FirstPersonLookRig_NEW>();
    }

    /// <summary>Every component of a type in the open scenes, inactive objects included; not prefab assets.</summary>
    static System.Collections.Generic.List<T> InScene<T>() where T : Component
    {
        var found = new System.Collections.Generic.List<T>();
        foreach (T c in Resources.FindObjectsOfTypeAll<T>())
        {
            if (EditorUtility.IsPersistent(c) || !c.gameObject.scene.IsValid()) continue;
            found.Add(c);
        }
        return found;
    }

    /// <summary>
    /// The rig the scene's scripts are wired to: the one the most serialized references in
    /// the scene point at, inactive objects included. Ties go to an active rig.
    /// </summary>
    static FirstPersonLookRig_NEW FindWiredRig()
    {
        var rigs = SceneRigs();
        if (rigs.Count <= 1) return rigs.Count == 1 ? rigs[0] : null;

        var counts = new System.Collections.Generic.Dictionary<FirstPersonLookRig_NEW, int>();
        foreach (FirstPersonLookRig_NEW r in rigs) counts[r] = 0;

        foreach (MonoBehaviour mb in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
        {
            if (mb == null || EditorUtility.IsPersistent(mb) || !mb.gameObject.scene.IsValid()) continue;
            if (mb is FirstPersonLookRig_NEW) continue;

            SerializedProperty p = new SerializedObject(mb).GetIterator();
            while (p.Next(true))
            {
                if (p.propertyType != SerializedPropertyType.ObjectReference) continue;
                var target = p.objectReferenceValue as FirstPersonLookRig_NEW;
                if (target != null && counts.ContainsKey(target)) counts[target]++;
            }
        }

        FirstPersonLookRig_NEW best = null;
        foreach (FirstPersonLookRig_NEW r in rigs)
        {
            if (best == null || counts[r] > counts[best] ||
                (counts[r] == counts[best] && r.isActiveAndEnabled && !best.isActiveAndEnabled))
                best = r;
        }

        Debug.Log("Vizlab: " + rigs.Count + " look rigs in the scene; using the one on '" + best.name +
                  "', which " + counts[best] + " tutorial references point at.", best);
        return best;
    }

    /// <summary>
    /// The Inspector's Copy Component / Paste Component Values, which records its own undo
    /// and leaves the target's GameObject link alone.
    /// </summary>
    static void CopyValues(Component from, Component to)
    {
        if (UnityEditorInternal.ComponentUtility.CopyComponent(from))
            UnityEditorInternal.ComponentUtility.PasteComponentValues(to);
    }

    [MenuItem("GameObject/Vizlab/Adapt Tutorial Camera to getReal3D", true, 18)]
    static bool AdaptValidate()
    {
        return !EditorApplication.isPlaying;
    }
}

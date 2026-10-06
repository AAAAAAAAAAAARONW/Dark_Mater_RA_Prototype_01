using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Puts the journey's station cards (_Core_NEW/UI/Milestones) on row 3 of the display,
/// right beneath the LAF on row 2: JourneyCardHUD_NEW on AllCanvas/LAF HUD/Row_3/JourneyCards,
/// stretched over the row, the card against its right edge. If an earlier run put JourneyCards
/// somewhere else (the JourneyCanvas corner) it is moved here, not duplicated. It also
/// hides the two card systems it replaces: the world-space cards (AllCanvas/World Space UI) and MilestoneHUD's
/// history cards (AllCanvas/MilestoneUI). Both are switched off, not deleted.
///
/// When each card shows, as agreed:
///   Quasar          at the start of the journey
///   Cosmic Web      before the Cosmic Web door
///   Galaxy Cluster  before the Micro door, as the dive's approach light comes up (40)
///   Galaxy          after the Micro door, once the dive has opened onto the galaxy
///   Cosmic Web      again, before the door back out to the web
///   Milky Way, Solar System, Earth   before their doors
///   Light Absorption  from step F2 of the redshift sequence (Managers/Redshift Manager)
///   Dark Matter     nothing yet: call Show("DarkMatter") once that section exists
///
/// Gates are the tracker's layer triggers, so the cards follow the doors. Through Undo,
/// one Ctrl+Z; safe to run again (it rebuilds the card list and does not add F2's call
/// twice). The import fix on the card art (mipmaps, see FixImports) is not undone with it.
/// </summary>
public static class JourneyCardArtSetup_NEW
{
    const string ArtFolder = "Assets/_Core_NEW/UI/Milestones";

    /// <summary>Row 3 of the Vizlab canvas, the row beneath the LAF.</summary>
    const string RowPath = "AllCanvas/LAF HUD/Row_3";

    const string HudName = "JourneyCards";
    const string CardName = "Card";

    struct CardSpec
    {
        public string id, art;
        public JourneyCardHUD_NEW.Trigger trigger;
        public int gate;        // index into the tracker's layerTriggers, -1 for none
        public float lead, delay;

        public CardSpec(string id, string art, JourneyCardHUD_NEW.Trigger trigger, int gate, float lead, float delay)
        {
            this.id = id; this.art = art; this.trigger = trigger; this.gate = gate; this.lead = lead; this.delay = delay;
        }
    }

    // Tracker layerTriggers: [0] Cosmic Web door, [1] Micro (galaxy) door, [2] back to the
    // Cosmic Web, [3] Milky Way door, [4] Solar System door, [5] Earth door.
    // Galaxy's 4 s is into the Micro dive's emerge (dive 3 + hold 0.2 + emerge 2.8).
    static readonly CardSpec[] Cards =
    {
        new CardSpec("Quasar",          "Quasar",          JourneyCardHUD_NEW.Trigger.AtStart,    -1,  0f, 2f),
        new CardSpec("CosmicWeb",       "CosmicWeb",       JourneyCardHUD_NEW.Trigger.BeforeGate,  0, 25f, 0f),
        new CardSpec("LightAbsorption", "LightAbsorption", JourneyCardHUD_NEW.Trigger.Manual,     -1,  0f, 0f),
        new CardSpec("DarkMatter",      "DarkMatter",      JourneyCardHUD_NEW.Trigger.Manual,     -1,  0f, 0f),
        new CardSpec("GalaxyCluster",   "GalaxyCluster",   JourneyCardHUD_NEW.Trigger.BeforeGate,  1, 40f, 0f),
        new CardSpec("Galaxy",          "Galaxy",          JourneyCardHUD_NEW.Trigger.AfterGate,   1,  0f, 4f),
        new CardSpec("CosmicWeb2",      "CosmicWeb",       JourneyCardHUD_NEW.Trigger.BeforeGate,  2, 25f, 0f),
        new CardSpec("MilkyWay",        "MilkyWay",        JourneyCardHUD_NEW.Trigger.BeforeGate,  3, 30f, 0f),
        new CardSpec("SolarSystem",     "SolarSystem",     JourneyCardHUD_NEW.Trigger.BeforeGate,  4, 25f, 0f),
        new CardSpec("Earth",           "Earth",           JourneyCardHUD_NEW.Trigger.BeforeGate,  5, 25f, 0f),
    };

    /// <summary>The redshift sequence step that shows Light Absorption.</summary>
    const string LightAbsorptionStep = "F2";

    [MenuItem("Tools/Journey NEW/Journey Cards/Apply Art", false, 62)]
    static void Apply()
    {
        UniverseJourneyTracker tracker = FindInOpenScene<UniverseJourneyTracker>();
        GameObject rowGo = FindByPath(RowPath);
        RectTransform row = rowGo != null ? rowGo.transform as RectTransform : null;
        if (tracker == null || row == null)
        {
            EditorUtility.DisplayDialog("Journey cards",
                "Needs a UniverseJourneyTracker and " + RowPath + " in the open scene. " +
                "Open Gameplay_Scene_Ana.", "OK");
            return;
        }

        FixImports();

        var tracked = new SerializedObject(tracker);
        Transform player = tracked.FindProperty("playerTransform").objectReferenceValue as Transform;
        SerializedProperty gates = tracked.FindProperty("layerTriggers");

        Undo.SetCurrentGroupName("Apply journey card art");
        int group = Undo.GetCurrentGroup();

        // The HUD: stretched over the row, so the card sizes and centres on it. An earlier
        // run's JourneyCards, wherever it is, is moved here.
        JourneyCardHUD_NEW existing = FindInOpenScene<JourneyCardHUD_NEW>();
        if (existing != null && existing.transform.parent != row)
            Undo.SetTransformParent(existing.transform, row, "Move journey cards to row 3");

        RectTransform hud = FindOrCreateRect(row, HudName);
        Undo.RecordObject(hud, "Journey cards layout");
        hud.pivot = new Vector2(0.5f, 0.5f);
        hud.anchorMin = Vector2.zero;
        hud.anchorMax = Vector2.one;
        hud.offsetMin = hud.offsetMax = Vector2.zero;
        hud.localScale = Vector3.one;
        hud.SetAsLastSibling();

        RectTransform card = FindOrCreateRect(hud, CardName);
        Image image = GetOrAdd<Image>(card.gameObject);
        Undo.RecordObject(image, "Journey card image");
        image.raycastTarget = false;
        image.preserveAspect = true;
        image.color = Color.white;
        CanvasGroup fade = GetOrAdd<CanvasGroup>(card.gameObject);
        Undo.RecordObject(fade, "Journey card fade");
        fade.interactable = false;
        fade.blocksRaycasts = false;

        Undo.RecordObject(card, "Journey card layout");
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(1f, 0.5f);
        card.anchoredPosition = Vector2.zero;
        card.sizeDelta = new Vector2(380f, 80f);   // the HUD sizes it per card at runtime

        JourneyCardHUD_NEW cards = GetOrAdd<JourneyCardHUD_NEW>(hud.gameObject);
        var so = new SerializedObject(cards);
        so.FindProperty("player").objectReferenceValue = player;
        so.FindProperty("cardRect").objectReferenceValue = card;
        so.FindProperty("cardImage").objectReferenceValue = image;
        so.FindProperty("canvasGroup").objectReferenceValue = fade;

        SerializedProperty list = so.FindProperty("cards");
        list.arraySize = Cards.Length;
        for (int i = 0; i < Cards.Length; i++)
        {
            CardSpec spec = Cards[i];
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/JourneyMilestone_{spec.art}.png");
            if (sprite == null)
                Debug.LogWarning($"[JourneyCardArtSetup_NEW] No sprite at {ArtFolder}/JourneyMilestone_{spec.art}.png.");

            Transform gate = null;
            if (spec.gate >= 0)
            {
                if (gates != null && spec.gate < gates.arraySize)
                    gate = gates.GetArrayElementAtIndex(spec.gate).objectReferenceValue as Transform;
                if (gate == null)
                    Debug.LogWarning($"[JourneyCardArtSetup_NEW] Tracker has no layer trigger [{spec.gate}] for '{spec.id}'.", tracker);
            }

            SerializedProperty e = list.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("id").stringValue = spec.id;
            e.FindPropertyRelative("sprite").objectReferenceValue = sprite;
            e.FindPropertyRelative("trigger").enumValueIndex = (int)spec.trigger;
            e.FindPropertyRelative("gate").objectReferenceValue = gate;
            e.FindPropertyRelative("leadDistance").floatValue = spec.lead;
            e.FindPropertyRelative("delaySeconds").floatValue = spec.delay;
        }
        so.ApplyModifiedProperties();

        bool wired = WireLightAbsorption(cards);

        // The two card systems this replaces: off, not deleted.
        Hide(FindByPath("AllCanvas/World Space UI"));
        MilestoneHUD oldHud = FindInOpenScene<MilestoneHUD>();
        if (oldHud != null)
        {
            // Its parent MilestoneUI holds nothing else; hiding it keeps the PopupCard hidden
            // too, which MilestoneHUD would otherwise have hidden in its own Awake.
            Transform parent = oldHud.transform.parent;
            Hide(parent != null && parent.name == "MilestoneUI" ? parent.gameObject : oldHud.gameObject);
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
        Selection.activeGameObject = hud.gameObject;

        Debug.Log("[JourneyCardArtSetup_NEW] Journey cards applied on " + RowPath +
                  (wired ? "; Light Absorption shows on redshift step " + LightAbsorptionStep : "") +
                  ". Save the scene to keep it; Ctrl+Z undoes it.", hud);
    }

    /// <summary>Adds Show("LightAbsorption") to the redshift sequence's F2, once.</summary>
    static bool WireLightAbsorption(JourneyCardHUD_NEW cards)
    {
        JourneySequence_NEW sequence = FindInOpenScene<JourneySequence_NEW>();
        if (sequence == null)
        {
            Debug.LogWarning("[JourneyCardArtSetup_NEW] No JourneySequence_NEW in the scene: " +
                             "wire Show(\"LightAbsorption\") by hand.");
            return false;
        }

        FieldInfo stepsField = typeof(JourneySequence_NEW).GetField("steps", BindingFlags.NonPublic | BindingFlags.Instance);
        var steps = stepsField != null ? stepsField.GetValue(sequence) as JourneySequence_NEW.Step[] : null;
        JourneySequence_NEW.Step step = null;
        if (steps != null)
            foreach (var s in steps)
                if (s != null && s.id == LightAbsorptionStep) { step = s; break; }

        if (step == null)
        {
            Debug.LogWarning($"[JourneyCardArtSetup_NEW] The redshift sequence has no step '{LightAbsorptionStep}': " +
                             "wire Show(\"LightAbsorption\") by hand.", sequence);
            return false;
        }

        UnityEvent onEnter = step.onEnter;
        for (int i = 0; i < onEnter.GetPersistentEventCount(); i++)
            if (onEnter.GetPersistentTarget(i) == cards && onEnter.GetPersistentMethodName(i) == nameof(JourneyCardHUD_NEW.Show))
                return true;

        Undo.RecordObject(sequence, "Show Light Absorption on " + LightAbsorptionStep);
        UnityEventTools.AddStringPersistentListener(onEnter, cards.Show, "LightAbsorption");
        EditorUtility.SetDirty(sequence);
        return true;
    }

    /// <summary>
    /// The cards are about 5000 px wide and drawn about 700: without mipmaps the text breaks
    /// up, as the tracker's nodes did. Mipmaps, trilinear, a small negative bias for crispness.
    /// </summary>
    static void FixImports()
    {
        foreach (string guid in AssetDatabase.FindAssets("JourneyMilestone_ t:Texture2D", new[] { ArtFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;

            bool dirty = false;
            if (ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                dirty = true;
            }
            if (!ti.mipmapEnabled || ti.filterMode != FilterMode.Trilinear || ti.mipMapBias > -0.5f)
            {
                ti.mipmapEnabled = true;
                ti.filterMode = FilterMode.Trilinear;
                ti.mipMapBias = -0.5f;
                dirty = true;
            }
            if (dirty) ti.SaveAndReimport();
        }
    }

    /// <summary>GetComponent or Undo.AddComponent. Not ??: in the editor a missing
    /// component comes back as a fake null that ?? does not see.</summary>
    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : Undo.AddComponent<T>(go);
    }

    static void Hide(GameObject go)
    {
        if (go == null || !go.activeSelf) return;
        Undo.RecordObject(go, "Hide " + go.name);
        go.SetActive(false);
    }

    /// <summary>A scene object by hierarchy path, inactive ones included.</summary>
    static GameObject FindByPath(string path)
    {
        string[] parts = path.Split('/');
        foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name != parts[0]) continue;
            Transform t = root.transform;
            for (int i = 1; i < parts.Length && t != null; i++) t = t.Find(parts[i]);
            if (t != null) return t.gameObject;
        }
        return null;
    }

    /// <summary>FindObjectOfType, inactive objects included (2019.4 has no flag for it).</summary>
    static T FindInOpenScene<T>() where T : Component
    {
        foreach (T c in Resources.FindObjectsOfTypeAll<T>())
            if (c.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(c))
                return c;
        return null;
    }

    static RectTransform FindOrCreateRect(RectTransform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing is RectTransform rt) return rt;

        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }
}

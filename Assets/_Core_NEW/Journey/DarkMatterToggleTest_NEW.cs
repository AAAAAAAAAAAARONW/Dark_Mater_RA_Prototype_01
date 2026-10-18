using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Test_Journey only: Y switches the cosmic webs between what the eye would see and the
/// dark matter under it — in both webs (Macro, the early one, and CosmicWeb), from the moment
/// the light arrives.
///
///   dark matter hidden   only the galaxies (GalaxyFieldBuilder_NEW's "Galaxies", baked on
///                        the web's yellow Core meshes): the web's structure is read from
///                        where the galaxies are, as in the reference simulation.
///   dark matter shown    the web itself (every COsmicWebGameObject), Core included, and the
///                        galaxy points fade out, since Core already draws them.
///
/// The two cross-fade over fadeSeconds. The web fades through _Density on runtime copies of
/// its materials (WorldSwitcher_NEW already drives _Opacity and _Emission, so those are left
/// to it); once faded out it is deactivated, so its raymarch costs nothing while hidden. The
/// galaxies fade through their own _Fade. Nothing on disk changes.
///
/// The old scaffolds (DarkMatterScaffold_NEW) are no longer drawn here: they are deactivated,
/// but kept as the switch's state, so DarkMatterMoment_NEW still works unchanged — while it
/// runs it answers Y itself by toggling its scaffold, and this follows. Its own
/// toggleAfterMoment is off in this scene (Set Up Dark Matter Y), so a press is never counted
/// twice.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("DM Y TEST", "#5C8CFF")]
public class DarkMatterToggleTest_NEW : MonoBehaviour
{
    [Tooltip("The layers in which Y works.")]
    [SerializeField] string[] layerIds = { "Macro", "CosmicWeb" };

    [Tooltip("Objects whose name starts with this are the web: shown with dark matter.")]
    [SerializeField] string webName = "COsmicWebGameObject";

    [Tooltip("Objects with this name drawing Custom/GalaxyField_NEW are the galaxies.")]
    [SerializeField] string galaxiesName = "Galaxies";

    [Tooltip("Seconds for the cross-fade.")]
    [Min(0.05f)]
    [SerializeField] float fadeSeconds = 1.5f;

    [Tooltip("Leave empty to find every DarkMatterScaffold_NEW in the scene.")]
    [SerializeField] DarkMatterScaffold_NEW[] scaffolds;

    [Tooltip("Leave empty to find it.")]
    [SerializeField] DarkMatterMoment_NEW moment;

    [Tooltip("Leave empty to find it.")]
    [SerializeField] LayerState_NEW layerState;

    [SerializeField] bool debugLog = false;

    static readonly int DensityId = Shader.PropertyToID("_Density");
    static readonly int FadeId = Shader.PropertyToID("_Fade");

    struct WebPart
    {
        public Renderer renderer;
        public Material[] original;
        public Material[] copies;
        public float[] baseDensity;
    }

    readonly List<GameObject> _webs = new List<GameObject>();
    readonly List<WebPart> _parts = new List<WebPart>();
    readonly List<GameObject> _galaxies = new List<GameObject>();
    readonly List<Material> _galaxyMats = new List<Material>();

    bool _shown;          // dark matter (the web) asked for
    float _mix = -1f;     // 0 galaxies only … 1 web only, as drawn
    bool _following;

    void Awake()
    {
        if (scaffolds == null || scaffolds.Length == 0)
            scaffolds = Resources.FindObjectsOfTypeAll<DarkMatterScaffold_NEW>();
        scaffolds = Array.FindAll(scaffolds, s => s != null && s.gameObject.scene == gameObject.scene);
        if (moment == null) moment = FindObjectOfType<DarkMatterMoment_NEW>();
        if (layerState == null) layerState = FindObjectOfType<LayerState_NEW>();
    }

    void OnEnable()
    {
        foreach (DarkMatterScaffold_NEW s in scaffolds) s.OnChanged += Follow;
    }

    void OnDisable()
    {
        foreach (DarkMatterScaffold_NEW s in scaffolds) if (s != null) s.OnChanged -= Follow;
    }

    // Start, not Awake: after PhotonLayering_NEW has swapped in its copies and
    // WorldSwitcher_NEW has cached the renderers, so the copies made here are of what is drawn.
    void Start()
    {
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith(webName, StringComparison.OrdinalIgnoreCase)) AddWeb(t.gameObject);
                else if (t.name == galaxiesName) AddGalaxies(t.gameObject);
            }

        // The scaffolds stay as the state; their glow is not drawn any more.
        foreach (DarkMatterScaffold_NEW s in scaffolds) { _shown = s.IsShown; break; }
        foreach (DarkMatterScaffold_NEW s in scaffolds) s.gameObject.SetActive(false);

        Apply(_shown ? 1f : 0f);

        Debug.Log("[DarkMatterToggleTest_NEW] Ready: " + _webs.Count + " web object(s) (" + _parts.Count + " renderers), " +
                  _galaxies.Count + " galaxy field(s), " + scaffolds.Length + " scaffold(s) as state, moment " +
                  (moment != null ? "found" : "none") + ". Dark matter " + (_shown ? "shown" : "hidden") + ".", this);
    }

    void AddWeb(GameObject web)
    {
        _webs.Add(web);
        foreach (Renderer r in web.GetComponentsInChildren<Renderer>(true))
        {
            Material[] original = r.sharedMaterials;
            var part = new WebPart
            {
                renderer = r,
                original = original,
                copies = new Material[original.Length],
                baseDensity = new float[original.Length]
            };
            for (int i = 0; i < original.Length; i++)
            {
                Material m = original[i];
                if (m == null || !m.HasProperty(DensityId)) { part.copies[i] = m; continue; }
                part.copies[i] = new Material(m) { name = m.name + " (DM toggle)" };
                part.baseDensity[i] = m.GetFloat(DensityId);
            }
            r.sharedMaterials = part.copies;
            _parts.Add(part);
        }
    }

    void AddGalaxies(GameObject go)
    {
        Renderer r = go.GetComponent<Renderer>();
        if (r == null || r.sharedMaterial == null || r.sharedMaterial.shader.name != "Custom/GalaxyField_NEW") return;
        _galaxies.Add(go);
        _galaxyMats.Add(r.material);   // a runtime instance, so the asset is untouched
    }

    void OnDestroy()
    {
        foreach (WebPart p in _parts)
        {
            if (p.renderer != null) p.renderer.sharedMaterials = p.original;
            for (int i = 0; i < p.copies.Length; i++)
                if (p.copies[i] != null && p.copies[i] != p.original[i]) Destroy(p.copies[i]);
        }
        foreach (Material m in _galaxyMats) if (m != null) Destroy(m);
    }

    void Update()
    {
        if (InputScheme_NEW.ToggleDown()) HandleY();

        float target = _shown ? 1f : 0f;
        if (!Mathf.Approximately(_mix, target))
            Apply(Mathf.MoveTowards(_mix, target, Time.deltaTime / fadeSeconds));
    }

    void HandleY()
    {
        string layer = layerState != null ? layerState.CurrentLayerId : "(no LayerState)";
        if (!InLayer())
        {
            if (debugLog) Debug.Log("[DarkMatterToggleTest_NEW] Y in '" + layer + "': not a cosmic web layer, ignored.", this);
            return;
        }
        if (moment != null && moment.IsRunning)
        {
            if (debugLog) Debug.Log("[DarkMatterToggleTest_NEW] Y in '" + layer + "': the dark matter moment answers it.", this);
            return;   // it toggles its scaffold, and Follow picks that up
        }

        SetShown(!_shown);
        if (debugLog) Debug.Log("[DarkMatterToggleTest_NEW] Y in '" + layer + "': dark matter " + (_shown ? "shown" : "hidden") + ".", this);
    }

    /// <summary>A scaffold changed (the moment moved it, or we did): that is the state.</summary>
    void Follow(bool shown)
    {
        if (_following) return;
        _shown = shown;
    }

    void SetShown(bool shown)
    {
        _shown = shown;
        _following = true;
        foreach (DarkMatterScaffold_NEW s in scaffolds) if (s != null) s.Show(shown);
        _following = false;
    }

    void Apply(float mix)
    {
        _mix = mix;
        float k = mix * mix * (3f - 2f * mix);

        bool webOn = mix > 0.001f;
        foreach (GameObject w in _webs) if (w != null && w.activeSelf != webOn) w.SetActive(webOn);
        foreach (WebPart p in _parts)
            for (int i = 0; i < p.copies.Length; i++)
                if (p.copies[i] != null && p.copies[i] != p.original[i]) p.copies[i].SetFloat(DensityId, p.baseDensity[i] * k);

        bool galaxiesOn = mix < 0.999f;
        foreach (GameObject g in _galaxies) if (g != null && g.activeSelf != galaxiesOn) g.SetActive(galaxiesOn);
        foreach (Material m in _galaxyMats) if (m != null) m.SetFloat(FadeId, 1f - k);
    }

    bool InLayer()
    {
        if (layerState == null) return true;
        string id = layerState.CurrentLayerId;
        foreach (string l in layerIds)
            if (string.Equals(l, id, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}

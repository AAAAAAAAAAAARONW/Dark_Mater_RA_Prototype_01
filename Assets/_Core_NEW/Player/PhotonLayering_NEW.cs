using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lets cosmic web filaments pass both in front of and behind the photon.
///
/// WHY. Each baked volume integrates a whole ray in one draw, so in the draw order it can
/// only be entirely before or entirely after the photon trail: either the web buries the
/// light (queue 3300, as it was) or the light sits on top of all of it (2950, since). A
/// filament between the camera and the photon and one far behind it were drawn the same
/// way, which is why the light read as painted over the web instead of flying inside it.
///
/// HOW. Every volume whose material has Layer Around Photon ticked is drawn in two parts
/// (PreBakedCloudVolume, _SplitPart): the stretch of each ray beyond the photon's depth,
/// in the volume's own place before the trail, and the stretch between the camera and the
/// photon, after it. Where the trail is not on screen the two parts add up to the old
/// single draw; where it is, what lies in front of the photon covers it and what lies
/// behind does not. The parts share one step budget, so each ray costs what it did. What
/// is added is one more draw of each ticked mesh — about half a million vertices for the
/// three yellow volumes — which is why the purple haze and the percent volume, both on
/// the 1.7 M-vertex BIG mesh, are left whole, underneath the light.
///
/// The split is a plane at the photon's depth, pushed back by depthBias. 0 is exact, but
/// with the camera three to five units behind the light, exact means almost no web is
/// ever in front of it; a few units of bias lets the filaments right around the photon
/// wrap it. Read every frame, so it can be tuned in play mode.
///
/// Runs before WorldSwitcher_NEW, which caches each renderer's material slots in its
/// Awake and has to see the second one to fade it with the rest. Runtime copies only:
/// nothing on disk changes, and the original materials go back on destroy.
/// </summary>
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
[HierarchyBadge_NEW("LAYERS", "#E0B04A")]
public class PhotonLayering_NEW : MonoBehaviour
{
    [Tooltip("The photon. Empty = PlayerRig_NEW's object, which the trail follows.")]
    [SerializeField] Transform photon;

    [Tooltip("How far behind the photon the split sits, in world units. Filaments nearer " +
             "than this pass in front of the light, farther ones behind it. 0 is exact.")]
    [Min(0f)]
    [SerializeField] float depthBias = 6f;

    [Tooltip("Render queue of the in-front parts. Must be above the photon trail's, 3000.")]
    [SerializeField] int frontQueue = 3010;

    const string VolumeShader = "Custom/PreBakedCloudVolume";

    static readonly int OptInId = Shader.PropertyToID("_LayerAroundPhoton");
    static readonly int SplitPartId = Shader.PropertyToID("_SplitPart");
    static readonly int PhotonPosId = Shader.PropertyToID("_PhotonWorldPos");
    static readonly int BiasId = Shader.PropertyToID("_PhotonSplitBias");

    struct Swapped
    {
        public Renderer renderer;
        public Material[] original;
    }

    readonly List<Swapped> _swapped = new List<Swapped>();
    readonly List<Material> _copies = new List<Material>();

    void Awake()
    {
        if (photon == null)
        {
            PlayerRig_NEW player = FindObjectOfType<PlayerRig_NEW>();
            if (player != null) photon = player.transform;
        }

        Split();
    }

    void Split()
    {
        // One pair of copies per source material, shared by every renderer using it.
        var pairs = new Dictionary<Material, Material[]>();

        foreach (Renderer r in FindObjectsOfType<Renderer>())
        {
            Material[] mats = r.sharedMaterials;
            if (mats.Length != 1 || !OptedIn(mats[0])) continue;

            Material source = mats[0];
            if (!pairs.TryGetValue(source, out Material[] pair))
            {
                pair = new[]
                {
                    Copy(source, 1f, source.renderQueue, " (beyond photon)"),
                    Copy(source, 2f, frontQueue, " (in front of photon)")
                };
                pairs.Add(source, pair);
            }

            // The second slot has no submesh of its own, so Unity draws the mesh again with
            // it — at that material's queue, after the trail.
            _swapped.Add(new Swapped { renderer = r, original = mats });
            r.sharedMaterials = pair;
        }

        if (_swapped.Count == 0)
            Debug.Log("[PhotonLayering_NEW] No volume has Layer Around Photon ticked on its " +
                      "material, so everything stays under the light.", this);
    }

    static bool OptedIn(Material m)
    {
        return m != null && m.shader != null && m.shader.name == VolumeShader
            && m.HasProperty(OptInId) && m.GetFloat(OptInId) > 0.5f;
    }

    Material Copy(Material source, float part, int queue, string suffix)
    {
        var copy = new Material(source) { name = source.name + suffix };
        copy.SetFloat(SplitPartId, part);
        copy.renderQueue = queue;
        _copies.Add(copy);
        return copy;
    }

    void LateUpdate()
    {
        if (photon != null) Shader.SetGlobalVector(PhotonPosId, photon.position);
        Shader.SetGlobalFloat(BiasId, depthBias);
    }

    void OnDestroy()
    {
        for (int i = 0; i < _swapped.Count; i++)
            if (_swapped[i].renderer != null)
                _swapped[i].renderer.sharedMaterials = _swapped[i].original;
        _swapped.Clear();

        for (int i = 0; i < _copies.Count; i++)
            if (_copies[i] != null) Destroy(_copies[i]);
        _copies.Clear();
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// The Sun's corona: the light round a star, which the Sun's own shader — a lit ball with a
/// rim — does not draw. A bright inner corona hugging the limb in slow streamers, and a wide,
/// faint halo beyond (Resources/SunCorona_NEW). It is what makes the Sun the light of the
/// system rather than an orange ball in it.
///
/// LATER: ONLY THE CORONA. This also lit the planets (a point light at the Sun, an ambient
/// fill) and gave them atmosphere shells; CelestialBody_NEW now draws every planet with its
/// own shader, lit from the Sun and with its own air, which Unity's lights and ambient do not
/// reach, and shells over it fought it in the depth buffer. Those went; the corona stays.
///
/// It is a child of the Sun, made in Awake before WorldSwitcher_NEW gathers its groups
/// (DefaultExecutionOrder), so it fades and hides with the Solar System and grows with it in
/// the dive from the Milky Way. Nothing is saved: it is made again each run.
///
/// Put it on the Solar System's group root.
/// </summary>
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
[HierarchyBadge_NEW("SOLAR LOOK", "#F2C14E")]
public class SolarSystemLook_NEW : MonoBehaviour
{
    [Tooltip("Optional. The Sun; if empty, the child named 'Sun'.")]
    [SerializeField] Transform sun;

    [Header("Corona")]
    [ColorUsage(false, true)] [SerializeField] Color coronaColour = new Color(1.4f, 0.85f, 0.4f, 1f);

    [Tooltip("How far it reaches: scales the corona's and the halo's falloff.")]
    [Range(0.25f, 2f)] [SerializeField] float coronaSize = 1f;

    [Range(0f, 1f)] [SerializeField] float streamers = 0.5f;

    static readonly int TintId = Shader.PropertyToID("_Tint");

    readonly List<UnityEngine.Object> _made = new List<UnityEngine.Object>();

    void Awake()
    {
        if (sun == null) sun = transform.Find("Sun");
        if (sun == null)
        {
            Debug.LogWarning("[SolarSystemLook_NEW] No Sun (a child named 'Sun'); no corona.", this);
            return;
        }

        BuildCorona();
    }

    void OnDestroy()
    {
        foreach (UnityEngine.Object o in _made)
            if (o != null) Destroy(o);
        _made.Clear();
    }

    void BuildCorona()
    {
        Shader shader = Resources.Load<Shader>("SunCorona_NEW");
        if (shader == null)
        {
            Debug.LogWarning("[SolarSystemLook_NEW] Resources/SunCorona_NEW.shader is missing; no corona.", this);
            return;
        }

        // The Sun's radius in its mesh's units: a sphere's bounds are as wide as it is.
        MeshFilter mf = sun.GetComponent<MeshFilter>();
        float radius = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.extents.x : 0.5f;

        var material = new Material(shader) { name = "Sun corona (runtime)" };
        material.SetColor(TintId, coronaColour);
        material.SetFloat("_Radius", radius);
        material.SetFloat("_Inner", 0.22f * coronaSize);
        material.SetFloat("_Outer", 1.1f * coronaSize);
        material.SetFloat("_Streamers", streamers);
        _made.Add(material);

        // A unit quad; the shader turns it to the camera and sizes it, four radii out, so its
        // bounds have to hold that.
        var quad = new Mesh { name = "Sun corona quad" };
        quad.vertices = new[] { new Vector3(-1f, -1f, 0f), new Vector3(1f, -1f, 0f), new Vector3(1f, 1f, 0f), new Vector3(-1f, 1f, 0f) };
        quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        quad.bounds = new Bounds(Vector3.zero, Vector3.one * (2f * 4f * radius));
        _made.Add(quad);

        var go = new GameObject("Sun corona (runtime)");
        go.layer = sun.gameObject.layer;
        go.transform.SetParent(sun, false);
        go.AddComponent<MeshFilter>().sharedMesh = quad;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = LightProbeUsage.Off;
        mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        _made.Add(go);
    }
}

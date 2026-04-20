using UnityEngine;

/// <summary>
/// Drives a PurpleCloudVolumeMulti material by pushing each sub-volume's
/// world-to-local matrix into the renderer via MaterialPropertyBlock every frame.
///
/// Attach to a cube GameObject whose scale covers all sub-volumes' bounds.
/// Set subVolumes to the original PurpleCloudVolume transforms (up to 4).
/// Use Tools > Laxi > Create Multi-Volume from Selection to set this up automatically.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshRenderer))]
public class PurpleCloudMultiVolume : MonoBehaviour
{
    [Tooltip("Source sub-volumes (max 4). Transforms must use Custom/PurpleCloudVolume.")]
    public Transform[] subVolumes = new Transform[0];

    // Cached property IDs — computed once, reused every frame.
    static readonly int[] s_matIDs =
    {
        Shader.PropertyToID("_VolMatrix0"),
        Shader.PropertyToID("_VolMatrix1"),
        Shader.PropertyToID("_VolMatrix2"),
        Shader.PropertyToID("_VolMatrix3"),
    };
    static readonly int s_volCountID = Shader.PropertyToID("_VolCount");

    MeshRenderer        _renderer;
    MaterialPropertyBlock _mpb;

    void OnEnable()
    {
        _renderer = GetComponent<MeshRenderer>();
        _mpb      = new MaterialPropertyBlock();
    }

    void LateUpdate() => PushMatrices();

    // Called every frame and also from the editor tool after initial setup.
    public void PushMatrices()
    {
        if (_renderer == null || _mpb == null) return;

        _renderer.GetPropertyBlock(_mpb);

        int count = Mathf.Min(subVolumes != null ? subVolumes.Length : 0, 4);
        _mpb.SetFloat(s_volCountID, (float)count);

        for (int i = 0; i < count; i++)
        {
            if (subVolumes[i] != null)
                _mpb.SetMatrix(s_matIDs[i], subVolumes[i].worldToLocalMatrix);
        }

        _renderer.SetPropertyBlock(_mpb);
    }

    // Resizes and repositions this cube to the world-space AABB of all sub-volumes.
    // Call this once after placing sub-volumes, or via the editor tool.
    public void FitBounds()
    {
        if (subVolumes == null || subVolumes.Length == 0) return;

        Vector3 min = Vector3.one *  float.MaxValue;
        Vector3 max = Vector3.one * -float.MaxValue;

        foreach (var t in subVolumes)
        {
            if (t == null) continue;
            var r = t.GetComponent<Renderer>();
            if (r != null)
            {
                min = Vector3.Min(min, r.bounds.min);
                max = Vector3.Max(max, r.bounds.max);
            }
            else
            {
                Vector3 hs = t.lossyScale * 0.5f;
                min = Vector3.Min(min, t.position - hs);
                max = Vector3.Max(max, t.position + hs);
            }
        }

        transform.position   = (min + max) * 0.5f;
        transform.rotation   = Quaternion.identity;
        transform.localScale = (max - min) * 1.02f;  // 2% padding so no edge clipping
    }
}

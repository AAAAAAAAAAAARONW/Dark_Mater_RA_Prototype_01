using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Where the photon trail's two long edges actually are, read from the mesh Unity draws
/// (TrailRenderer.BakeMesh) instead of guessed from the travel direction and the camera.
///
/// The trail shader puts wavelength across the ribbon by its uv.y, and which screen side uv.y = 0
/// lands on depends on how Unity builds the ribbon for this camera — it cannot be worked out from
/// the source, which is why the old arrow could run the wrong way. The baked mesh says it exactly.
/// </summary>
public static class TrailRibbon_NEW
{
    static readonly List<Vector3> _verts = new List<Vector3>(512);
    static readonly List<Vector2> _uvs = new List<Vector2>(512);

    /// <summary>
    /// The two edges of the ribbon near target (world space): edge0 is the uv.y = 0 side,
    /// edge1 the uv.y = 1 side. False if the trail has nothing to read yet.
    /// </summary>
    public static bool Edges(TrailRenderer trail, Camera cam, Mesh scratch, Vector3 target,
                             out Vector3 edge0, out Vector3 edge1)
    {
        edge0 = edge1 = Vector3.zero;
        if (trail == null || cam == null || scratch == null || trail.positionCount < 2) return false;

        trail.BakeMesh(scratch, cam, false);
        scratch.GetVertices(_verts);
        scratch.GetUVs(0, _uvs);
        if (_verts.Count < 4 || _uvs.Count != _verts.Count) return false;

        // The baked vertices are world space; if they turn out to be the trail's local space
        // (Unity versions differ), the transformed set is the one that reaches the target.
        bool local = Nearest(target, false, trail.transform) > Nearest(target, true, trail.transform) + 0.01f;

        float best0 = float.MaxValue, best1 = float.MaxValue;
        for (int i = 0; i < _verts.Count; i++)
        {
            Vector3 p = local ? trail.transform.TransformPoint(_verts[i]) : _verts[i];
            float d = (p - target).sqrMagnitude;
            if (_uvs[i].y < 0.5f) { if (d < best0) { best0 = d; edge0 = p; } }
            else if (d < best1) { best1 = d; edge1 = p; }
        }
        return best0 < float.MaxValue && best1 < float.MaxValue;
    }

    static float Nearest(Vector3 target, bool local, Transform t)
    {
        float best = float.MaxValue;
        for (int i = 0; i < _verts.Count; i++)
        {
            Vector3 p = local ? t.TransformPoint(_verts[i]) : _verts[i];
            float d = (p - target).sqrMagnitude;
            if (d < best) best = d;
        }
        return best;
    }
}

using UnityEngine;

/// <summary>
/// A sphere of dark matter that bends the player's flight path.
///
/// Ported from DarkMatter so the new stack owns its own copy — the deflection maths is
/// unchanged, line for line. The scene currently holds zero instances, so this is a type
/// the bend system compiles against rather than something that runs today.
///
/// Model: the sphere pushes radially outward from its centre. The push is projected onto
/// the plane perpendicular to travel, so the path curves in full 3D including height, and
/// straightens again once the player leaves the radius.
///
/// Changes from the original:
///   * gizmos ask DebugView_NEW.Gizmos, like everything else that draws
///   * the Game-view gizmo path is gone — it ran Debug.DrawLine every frame from Update
///     purely to draw an editor overlay, which is per-frame cost for an editor feature
///   * gizmo drawing is behind UNITY_EDITOR; the class itself still compiles into builds,
///     so a scene holding one does not report a missing script
/// </summary>
[HierarchyBadge_NEW("DARKMTR", "#7C5CD6")]
public class DarkMatter_NEW : MonoBehaviour
{
    [Header("Path bending")]
    [Tooltip("Radius of influence. Inside it the path curves in 3D, height included.")]
    [SerializeField] float influenceRadius = 5f;

    [Tooltip("Bend strength. Higher deflects the path further.")]
    [Range(0.1f, 5f)]
    [SerializeField] float bendStrength = 1f;

    [Tooltip("Falloff. 0 = no influence at the edge, 1 = edge pushes as hard as the centre.")]
    [Range(0f, 1f)]
    [SerializeField] float falloffExponent = 0.7f;

    [Header("Gizmo")]
    [SerializeField] Color influenceColor = new Color(0.4f, 0.2f, 0.8f, 1f);

    public float InfluenceRadius => influenceRadius;
    public float BendStrength => bendStrength;
    public float FalloffExponent => falloffExponent;

    /// <summary>
    /// Deflection contributed by this sphere. Not normalised — the caller sums every
    /// sphere's contribution and normalises once.
    /// </summary>
    public Vector3 GetDeflectionAt(Vector3 playerPosition, Vector3 movementDirection)
    {
        Vector3 toPlayer = playerPosition - transform.position;
        float dist = toPlayer.magnitude;

        if (dist < 0.001f) return Vector3.zero;
        if (dist > influenceRadius) return Vector3.zero;

        float influence = 1f - Mathf.Pow(dist / influenceRadius, falloffExponent);
        Vector3 toPlayerNorm = toPlayer / dist;

        // The outward radial direction, projected onto the plane perpendicular to travel.
        Vector3 awayAxis = toPlayerNorm - Vector3.Dot(toPlayerNorm, movementDirection) * movementDirection;
        float axisMag = awayAxis.sqrMagnitude;

        if (axisMag < 0.0001f)
        {
            // Heading almost straight at the centre — any perpendicular will do, since
            // every face of the sphere pushes.
            Vector3 side = Vector3.Cross(movementDirection, Vector3.up);
            if (side.sqrMagnitude < 0.0001f) side = Vector3.Cross(movementDirection, Vector3.right);
            side.Normalize();
            return side * (bendStrength * influence);
        }

        awayAxis /= Mathf.Sqrt(axisMag);
        return awayAxis * (bendStrength * influence);
    }

    /// <summary>True when the player is inside the radius.</summary>
    public bool IsInInfluence(Vector3 playerPosition)
    {
        return (playerPosition - transform.position).sqrMagnitude <= influenceRadius * influenceRadius;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!DebugView_NEW.Gizmos) return;

        Gizmos.color = influenceColor;
        Gizmos.DrawWireSphere(transform.position, influenceRadius);
    }
#endif
}

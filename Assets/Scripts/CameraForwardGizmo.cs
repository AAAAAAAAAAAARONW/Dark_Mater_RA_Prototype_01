using UnityEngine;

/// <summary>
/// Draws camera forward direction gizmos in the editor.
/// Attach to Camera (or any transform used as camera heading).
/// </summary>
[ExecuteAlways]
public class CameraForwardGizmo : MonoBehaviour
{
    [Header("Ray")]
    [SerializeField] bool drawRay = true;
    [SerializeField] float rayLength = 80f;
    [SerializeField] Color rayColor = new Color(0.2f, 1f, 1f, 1f);
    [SerializeField] bool drawArrowHead = true;
    [SerializeField] float arrowHeadSize = 1.5f;
    [SerializeField] float arrowHeadAngle = 22f;

    [Header("Trajectory Preview")]
    [SerializeField] bool drawTrajectoryPoints = true;
    [SerializeField] int pointCount = 20;
    [SerializeField] float pointSpacing = 3f;
    [SerializeField] float pointRadius = 0.18f;
    [SerializeField] Color pointColor = new Color(1f, 0.8f, 0.2f, 0.9f);

    void OnDrawGizmos()
    {
        DrawGizmosInternal();
    }

    void OnDrawGizmosSelected()
    {
        DrawGizmosInternal();
    }

    void DrawGizmosInternal()
    {
        Vector3 origin = transform.position;
        Vector3 forward = transform.forward;
        if (forward.sqrMagnitude < 0.0001f) return;
        forward.Normalize();

        if (drawRay)
        {
            Gizmos.color = rayColor;
            Vector3 end = origin + forward * Mathf.Max(0.01f, rayLength);
            Gizmos.DrawLine(origin, end);

            if (drawArrowHead)
            {
                Quaternion rotL = Quaternion.AngleAxis(180f - arrowHeadAngle, transform.up);
                Quaternion rotR = Quaternion.AngleAxis(180f + arrowHeadAngle, transform.up);
                Vector3 left = end + (rotL * forward) * arrowHeadSize;
                Vector3 right = end + (rotR * forward) * arrowHeadSize;
                Gizmos.DrawLine(end, left);
                Gizmos.DrawLine(end, right);
            }
        }

        if (drawTrajectoryPoints)
        {
            Gizmos.color = pointColor;
            int count = Mathf.Max(1, pointCount);
            float spacing = Mathf.Max(0.05f, pointSpacing);
            float radius = Mathf.Max(0.01f, pointRadius);
            for (int i = 1; i <= count; i++)
            {
                Vector3 p = origin + forward * (i * spacing);
                Gizmos.DrawSphere(p, radius);
            }
        }
    }
}

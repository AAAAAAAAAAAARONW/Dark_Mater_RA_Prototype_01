using UnityEngine;

/// <summary>
/// Draws where a camera is pointing, in the Scene view, when debug gizmos are on.
///
/// Replaces CameraForwardGizmo. What it is for: Cinemachine draws a frustum for the live
/// camera and for whichever vcam is selected, but nothing for the other inactive rigs.
/// While laying out a scene you want to see all six at once, and that is the one job left
/// for this script.
///
/// Three changes from the original:
///
///   GATED. It asks DebugView_NEW.Gizmos first, so it turns off with everything else
///   rather than being a separate switch on six components.
///
///   TICKS OFF BY DEFAULT. The original drew twenty spheres per camera at three-unit
///   intervals — but those spheres sit on the ray it already draws, so six cameras cost
///   138 gizmo calls per repaint to add distance marks and nothing structural. The ray is
///   the useful part; ticks are opt-in.
///
///   COMPILED OUT. The whole file is inside UNITY_EDITOR. The original had no guard, so
///   its gizmo code compiled into builds where it could never run.
///
/// [ExecuteAlways] is gone too. It makes Awake and Update run in edit mode, and this class
/// has neither — OnDrawGizmos already runs in edit mode on its own.
/// </summary>
[HierarchyBadge_NEW("AIM", "#8C8C99")]
public class CameraAimGizmo_NEW : MonoBehaviour
{
    [Header("Ray")]
    [SerializeField] bool drawRay = true;
    [SerializeField] float rayLength = 80f;
    [SerializeField] Color rayColor = new Color(0.2f, 1f, 1f, 1f);
    [SerializeField] bool drawArrowHead = true;
    [SerializeField] float arrowHeadSize = 1.5f;
    [SerializeField] float arrowHeadAngle = 22f;

    [Header("Distance ticks")]
    [Tooltip("Marks along the ray. They add distance reference and nothing else — the ray " +
             "is already drawn — so six cameras' worth is mostly clutter. Off by default.")]
    [SerializeField] bool drawTicks = false;
    [SerializeField] int tickCount = 20;
    [SerializeField] float tickSpacing = 3f;
    [SerializeField] float tickRadius = 0.18f;
    [SerializeField] Color tickColor = new Color(1f, 0.8f, 0.2f, 0.9f);

    [Header("Visibility")]
    [Tooltip("Draw only while this object is selected. Leave off to see every camera's " +
             "aim at once, which is the reason this component exists.")]
    [SerializeField] bool onlyWhenSelected = false;

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (onlyWhenSelected) return;
        Draw();
    }

    void OnDrawGizmosSelected()
    {
        if (!onlyWhenSelected) return;
        Draw();
    }

    void Draw()
    {
        if (!DebugView_NEW.Gizmos) return;

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
                Quaternion left = Quaternion.AngleAxis(180f - arrowHeadAngle, transform.up);
                Quaternion right = Quaternion.AngleAxis(180f + arrowHeadAngle, transform.up);
                Gizmos.DrawLine(end, end + (left * forward) * arrowHeadSize);
                Gizmos.DrawLine(end, end + (right * forward) * arrowHeadSize);
            }
        }

        if (!drawTicks) return;

        Gizmos.color = tickColor;
        int count = Mathf.Max(1, tickCount);
        float spacing = Mathf.Max(0.05f, tickSpacing);
        float radius = Mathf.Max(0.01f, tickRadius);

        for (int i = 1; i <= count; i++)
            Gizmos.DrawSphere(origin + forward * (i * spacing), radius);
    }
#endif
}

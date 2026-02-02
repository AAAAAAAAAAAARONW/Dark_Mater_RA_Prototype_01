using UnityEngine;

/// <summary>
/// 暗物质：视为球体，球面每个方向都提供排斥力；玩家在影响范围内路线会 3D 弯曲（含高度变化），离开后恢复。
/// Inspector 可调参数；Gizmo 在 Scene 与 Game 视图中均显示球体影响范围与弯曲程度。
/// </summary>
public class DarkMatter : MonoBehaviour
{
    [Header("路径弯曲")]
    [Tooltip("球体影响半径：球面每个方向都提供排斥力，玩家在此范围内路线会被 3D 弯曲（含高度）")]
    [SerializeField] float influenceRadius = 5f;
    [Tooltip("弯曲强度：数值越大，路径偏转越明显（弧度越大）")]
    [Range(0.1f, 5f)]
    [SerializeField] float bendStrength = 1f;
    [Tooltip("弯曲衰减：0=边缘无影响，1=边缘与中心影响相同；建议 0.5~1")]
    [Range(0f, 1f)]
    [SerializeField] float falloffExponent = 0.7f;

    [Header("Gizmo 显示（Scene + Game 视图）")]
    [Tooltip("Gizmo 中弯曲程度预览的参考长度（米）")]
    [SerializeField] float gizmoBendPreviewLength = 2f;
    [Tooltip("影响范围圆的颜色")]
    [SerializeField] Color gizmoInfluenceColor = new Color(0.4f, 0.2f, 0.8f, 1f);
    [Tooltip("弯曲弧线的颜色")]
    [SerializeField] Color gizmoBendArcColor = new Color(0.8f, 0.4f, 1f, 1f);
    [Tooltip("勾选后，运行时在 Game 视图中也绘制（需开启 Game 视图的 Gizmos）")]
    [SerializeField] bool drawInGameView = true;

    /// <summary>影响半径（米）。</summary>
    public float InfluenceRadius => influenceRadius;
    /// <summary>弯曲强度。</summary>
    public float BendStrength => bendStrength;
    /// <summary>弯曲衰减指数（距离越远影响越小）。</summary>
    public float FalloffExponent => falloffExponent;

    /// <summary>
    /// 球体模型：从球心指向玩家的径向排斥，在运动平面内产生偏转（全 3D，含高度）。
    /// 根据玩家位置与基准前进方向，计算该暗物质对路径的偏转向量（未归一化，由调用方叠加后归一化）。
    /// </summary>
    /// <param name="playerPosition">玩家世界坐标</param>
    /// <param name="movementDirection">基准前进方向（归一化）</param>
    /// <returns>偏转向量；若在范围外或偏转为零则返回 Vector3.zero</returns>
    public Vector3 GetDeflectionAt(Vector3 playerPosition, Vector3 movementDirection)
    {
        Vector3 toPlayer = playerPosition - transform.position;
        float dist = toPlayer.magnitude;
        if (dist < 0.001f) return Vector3.zero;
        if (dist > influenceRadius) return Vector3.zero;

        float influence = 1f - Mathf.Pow(dist / influenceRadius, falloffExponent);
        Vector3 toPlayerNorm = toPlayer / dist;
        // 球体：径向“远离球心”的方向在运动方向垂直面上的分量 = 偏转轴（每个面都提供力，全 3D）
        Vector3 awayAxis = toPlayerNorm - Vector3.Dot(toPlayerNorm, movementDirection) * movementDirection;
        float axisMag = awayAxis.sqrMagnitude;
        if (axisMag < 0.0001f)
        {
            // 几乎正对球心：任取一垂直于运动的方向（球体任意面都会推开）
            Vector3 side = Vector3.Cross(movementDirection, Vector3.up);
            if (side.sqrMagnitude < 0.0001f) side = Vector3.Cross(movementDirection, Vector3.right);
            side.Normalize();
            return side * (bendStrength * influence);
        }
        awayAxis /= Mathf.Sqrt(axisMag);
        return awayAxis * (bendStrength * influence);
    }

    /// <summary>
    /// 玩家是否在该暗物质影响范围内。
    /// </summary>
    public bool IsInInfluence(Vector3 playerPosition)
    {
        return (playerPosition - transform.position).sqrMagnitude <= influenceRadius * influenceRadius;
    }

    void OnDrawGizmosSelected()
    {
        DrawGizmosWorld(transform.position, true);
    }

    void OnDrawGizmos()
    {
        DrawGizmosWorld(transform.position, false);
    }

    void DrawGizmosWorld(Vector3 center, bool selected)
    {
        Gizmos.matrix = Matrix4x4.identity;
        float alpha = selected ? 1f : 0.6f;
        // 球体影响范围：线框球（每个面都提供力）
        Gizmos.color = new Color(gizmoInfluenceColor.r, gizmoInfluenceColor.g, gizmoInfluenceColor.b, alpha);
        Gizmos.DrawWireSphere(center, influenceRadius);
        // 弯曲程度预览：弧线
        Gizmos.color = new Color(gizmoBendArcColor.r, gizmoBendArcColor.g, gizmoBendArcColor.b, alpha);
        Vector3 forward = Vector3.forward;
        int arcSegments = 16;
        float arcAngle = Mathf.Clamp(bendStrength * 45f, 5f, 90f) * Mathf.Deg2Rad;
        float step = arcAngle / (arcSegments - 1);
        Vector3 prev = center + forward * gizmoBendPreviewLength;
        Gizmos.DrawLine(center, prev);
        for (int i = 1; i < arcSegments; i++)
        {
            float t = i * step;
            Vector3 dir = new Vector3(Mathf.Sin(t), 0f, Mathf.Cos(t));
            Vector3 p = center + dir * gizmoBendPreviewLength;
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }

    void Update()
    {
        if (!drawInGameView) return;
        DrawDebugInGameView(transform.position, 0.05f);
    }

    /// <summary>用 Debug.DrawLine 在 Game 视图中绘制球体范围（需开启 Game 视图 Gizmos）。</summary>
    void DrawDebugInGameView(Vector3 center, float duration)
    {
        // 球体：画三条正交圆环示意
        const int circleSegments = 24;
        float da = 2f * Mathf.PI / circleSegments;
        for (int i = 0; i < circleSegments; i++)
        {
            float a0 = i * da, a1 = (i + 1) * da;
            float c0 = Mathf.Cos(a0) * influenceRadius, s0 = Mathf.Sin(a0) * influenceRadius;
            float c1 = Mathf.Cos(a1) * influenceRadius, s1 = Mathf.Sin(a1) * influenceRadius;
            Debug.DrawLine(center + new Vector3(c0, s0, 0), center + new Vector3(c1, s1, 0), gizmoInfluenceColor, duration);
            Debug.DrawLine(center + new Vector3(c0, 0, s0), center + new Vector3(c1, 0, s1), gizmoInfluenceColor, duration);
            Debug.DrawLine(center + new Vector3(0, c0, s0), center + new Vector3(0, c1, s1), gizmoInfluenceColor, duration);
        }
        // 弯曲弧线
        Vector3 forward = Vector3.forward;
        int arcSegments = 16;
        float arcAngle = Mathf.Clamp(bendStrength * 45f, 5f, 90f) * Mathf.Deg2Rad;
        float step = arcAngle / (arcSegments - 1);
        Vector3 prev = center + forward * gizmoBendPreviewLength;
        Debug.DrawLine(center, prev, gizmoBendArcColor, duration);
        for (int i = 1; i < arcSegments; i++)
        {
            float t = i * step;
            Vector3 dir = new Vector3(Mathf.Sin(t), 0f, Mathf.Cos(t));
            Vector3 p = center + dir * gizmoBendPreviewLength;
            Debug.DrawLine(prev, p, gizmoBendArcColor, duration);
            prev = p;
        }
    }
}

using UnityEngine;

/// <summary>
/// 临时诊断脚本：检查 PhotonTrail 设置是否正确
/// 添加到 Player GameObject 上，运行时按 T 键打印诊断信息
/// </summary>
public class PhotonTrailDiagnostics : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            PrintDiagnostics();
        }
    }

    void PrintDiagnostics()
    {
        Debug.Log("=== PhotonTrail 诊断 ===");
        
        // Check PhotonTrailController
        var controller = GetComponent<PhotonTrailController>();
        if (controller == null)
        {
            Debug.LogError("❌ PhotonTrailController 未找到！请添加到 Player GameObject");
        }
        else
        {
            Debug.Log("✅ PhotonTrailController 存在");
            
            // Use reflection to check private fields
            var trailField = typeof(PhotonTrailController).GetField("trail", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var trail = trailField?.GetValue(controller) as TrailRenderer;
            
            if (trail == null)
            {
                Debug.LogWarning("⚠️ TrailRenderer 未找到！");
            }
            else
            {
                Debug.Log($"✅ TrailRenderer 找到: {trail.name}");
                
                if (trail.material == null)
                {
                    Debug.LogError("❌ TrailRenderer 没有材质！");
                }
                else
                {
                    string shaderName = trail.material.shader.name;
                    Debug.Log($"材质 Shader: {shaderName}");
                    
                    if (shaderName.Contains("PhotonTrail"))
                    {
                        Debug.Log("✅ 材质使用正确的 PhotonTrail shader");
                        
                        // Check shader properties
                        if (trail.material.HasProperty("_Absorb"))
                        {
                            float absorb = trail.material.GetFloat("_Absorb");
                            Debug.Log($"  _Absorb = {absorb:F2}");
                        }
                        else
                        {
                            Debug.LogWarning("⚠️ 材质没有 _Absorb 属性");
                        }
                    }
                    else
                    {
                        Debug.LogError($"❌ 材质使用错误的 shader: {shaderName}");
                        Debug.LogError("   应该使用 'Custom/PhotonTrail' shader！");
                    }
                }
            }
        }
        
        // Check AbsorptionVolumes
        AbsorptionVolume[] volumes = FindObjectsOfType<AbsorptionVolume>();
        Debug.Log($"场景中的 AbsorptionVolume 数量: {volumes.Length}");
        
        foreach (var vol in volumes)
        {
            Collider col = vol.GetComponent<Collider>();
            Debug.Log($"  - {vol.name}: Strength={vol.AbsorptionStrength:F2}, HasCollider={col != null}, IsTrigger={col != null && col.isTrigger}");
        }
        
        Debug.Log("=== 诊断结束 ===");
        Debug.Log("提示：启用 PhotonTrailController 的 debugLog 查看实时信息");
    }
}

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// Editor-only Nebula preview applier for LayerDefinitionTest.
/// Lives in runtime assembly (not Editor folder) so LayerDefinitionTest can call it safely.
/// </summary>
public static class LayerDefinitionTestEditorPreview
{
    static readonly int ColorDarkId = Shader.PropertyToID("_ColorDark");
    static readonly int ColorMidId = Shader.PropertyToID("_ColorMid");
    static readonly int ColorBrightId = Shader.PropertyToID("_ColorBright");
    static readonly int ColorStarId = Shader.PropertyToID("_ColorStar");
    static readonly int ScaleId = Shader.PropertyToID("_Scale");
    static readonly int OctavesId = Shader.PropertyToID("_Octaves");
    static readonly int PersistenceId = Shader.PropertyToID("_Persistence");
    static readonly int DensityId = Shader.PropertyToID("_Density");
    static readonly int SharpnessId = Shader.PropertyToID("_Sharpness");
    static readonly int StarScaleId = Shader.PropertyToID("_StarScale");
    static readonly int StarThresholdId = Shader.PropertyToID("_StarThreshold");
    static readonly int StarBrightnessId = Shader.PropertyToID("_StarBrightness");
    static readonly int AnimateId = Shader.PropertyToID("_Animate");
    static readonly int SpeedId = Shader.PropertyToID("_Speed");

    public static void ApplyIfEnabled(LayerDefinitionTest def)
    {
        if (def == null) return;
        if (Application.isPlaying) return;
        if (!def.enableEditorNebulaPreview) return;
        if (!def.driveNebulaShader) return;

        int touched = 0;

        Material skyboxMat = RenderSettings.skybox;
        if (IsNebulaMaterial(skyboxMat))
        {
            ApplyToMaterial(skyboxMat, def);
            EditorUtility.SetDirty(skyboxMat);
            touched++;
        }

        Renderer[] renderers = Object.FindObjectsOfType<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;

            Material[] mats = r.sharedMaterials;
            bool changed = false;
            for (int j = 0; j < mats.Length; j++)
            {
                Material m = mats[j];
                if (!IsNebulaMaterial(m)) continue;
                ApplyToMaterial(m, def);
                EditorUtility.SetDirty(m);
                changed = true;
                touched++;
            }

            if (changed)
                EditorUtility.SetDirty(r);
        }

        if (touched > 0)
            SceneView.RepaintAll();
    }

    static bool IsNebulaMaterial(Material m)
    {
        return m != null && m.shader != null && m.shader.name == "Custom/Nebula";
    }

    static void ApplyToMaterial(Material m, LayerDefinitionTest def)
    {
        if (m == null || def == null) return;

        if (m.HasProperty(ColorDarkId)) m.SetColor(ColorDarkId, def.nebulaColorDark);
        if (m.HasProperty(ColorMidId)) m.SetColor(ColorMidId, def.nebulaColorMid);
        if (m.HasProperty(ColorBrightId)) m.SetColor(ColorBrightId, def.nebulaColorBright);
        if (m.HasProperty(ColorStarId)) m.SetColor(ColorStarId, def.nebulaColorStar);

        if (m.HasProperty(ScaleId)) m.SetFloat(ScaleId, def.nebulaScale);
        if (m.HasProperty(OctavesId)) m.SetFloat(OctavesId, def.nebulaOctaves);
        if (m.HasProperty(PersistenceId)) m.SetFloat(PersistenceId, def.nebulaPersistence);
        if (m.HasProperty(DensityId)) m.SetFloat(DensityId, def.nebulaDensity);
        if (m.HasProperty(SharpnessId)) m.SetFloat(SharpnessId, def.nebulaSharpness);

        if (m.HasProperty(StarScaleId)) m.SetFloat(StarScaleId, def.nebulaStarScale);
        if (m.HasProperty(StarThresholdId)) m.SetFloat(StarThresholdId, def.nebulaStarThreshold);
        if (m.HasProperty(StarBrightnessId)) m.SetFloat(StarBrightnessId, def.nebulaStarBrightness);

        if (m.HasProperty(AnimateId)) m.SetFloat(AnimateId, def.nebulaAnimate >= 0.5f ? 1f : 0f);
        if (m.HasProperty(SpeedId)) m.SetFloat(SpeedId, def.nebulaSpeed);
    }
}
#endif

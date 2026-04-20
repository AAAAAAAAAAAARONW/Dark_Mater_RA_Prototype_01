using UnityEngine;
using UnityEditor;

/// <summary>
/// For each Custom/PurpleCloudVolume renderer in the scene, evaluates the full
/// density + colour field in C# (mirroring the HLSL shader exactly) and bakes
/// the result into a 64^3 RGBA16 Texture3D asset.
///
/// The companion shader PreBakedCloudVolume reads this texture with a single
/// tex3D fetch per raymarch step instead of the original 4-6 fetches.
/// Each volume gets its own unique texture, so colours/densities can differ freely.
///
/// Usage: Tools > Laxi > Bake Volume Density Textures
/// Output: Assets/BakedVolumeTex/<ObjectName>_density.asset
/// </summary>
public static class BakeVolumeDensityTex
{
    const string VOLUME_SHADER = "Custom/PurpleCloudVolume";
    const string OUT_FOLDER    = "Assets/BakedVolumeTex";
    const string NOISE_PATH    = "Assets/Shaders/CloudNoiseTex3D.asset";
    const int    BAKE_SIZE     = 64;   // 64^3 RGBA16 = 2 MB/volume; change to 32 for 256 KB
    const int    NOISE_SIZE    = 32;
    const float  NTILE_INV     = 1f / NOISE_SIZE;

    // -------------------------------------------------------------------------
    [MenuItem("Tools/Laxi/Bake Volume Density Textures")]
    static void BakeAll()
    {
        var noiseTex = AssetDatabase.LoadAssetAtPath<Texture3D>(NOISE_PATH);
        if (noiseTex == null)
        {
            Debug.LogError("[BakeVolumeDensityTex] CloudNoiseTex3D not found. " +
                           "Run 'Bake Cloud Noise Texture (32^3)' first.");
            return;
        }

        // Pull noise pixel data once for all volumes.
        Color32[] noisePx = noiseTex.GetPixels32();

        if (!AssetDatabase.IsValidFolder(OUT_FOLDER))
            AssetDatabase.CreateFolder("Assets", "BakedVolumeTex");

        int count = 0;
        foreach (var mr in Object.FindObjectsOfType<MeshRenderer>())
        {
            if (mr.sharedMaterial?.shader?.name != VOLUME_SHADER) continue;
            BakeOne(mr, noisePx);
            count++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (count == 0)
            Debug.LogWarning("[BakeVolumeDensityTex] No Custom/PurpleCloudVolume renderers found.");
        else
            Debug.Log($"[BakeVolumeDensityTex] Baked {count} volume(s) → {OUT_FOLDER}");
    }

    [MenuItem("Tools/Laxi/Bake Volume Density Textures", validate = true)]
    static bool Validate() => !Application.isPlaying;

    // -------------------------------------------------------------------------
    static void BakeOne(MeshRenderer mr, Color32[] noisePx)
    {
        var mat = mr.sharedMaterial;
        var tf  = mr.transform;

        // --- Read material parameters ---
        float featureSize     = mat.GetFloat("_FeatureSize");
        float detailScale     = mat.GetFloat("_DetailScale");
        float cloudSoftness   = mat.GetFloat("_CloudSoftness");
        float coverage        = mat.GetFloat("_Coverage");
        float erode           = mat.GetFloat("_Erode");
        float edgeFade        = mat.GetFloat("_EdgeFade");
        float edgeWidth       = mat.GetFloat("_EdgeWidth");
        float boundaryBreakup = mat.GetFloat("_BoundaryBreakup");
        float anisotropy      = mat.GetFloat("_Anisotropy");
        Vector4 seedOffset    = mat.GetVector("_SeedOffset");
        Color baseCol         = mat.GetColor("_BaseColor");
        Color midCol          = mat.GetColor("_MidColor");
        Color highCol         = mat.GetColor("_HighlightColor");
        Color rimCol          = mat.GetColor("_RimColor");
        float innerGlow       = mat.GetFloat("_InnerGlow");

        float invFeatureSize  = 1f / Mathf.Max(featureSize, 0.001f);
        // Precomputed early-exit threshold (mirrors shader)
        float earlyExitThr    = ((0.34f + coverage * 0.38f)
                                 - 0.4f / (1.8f + cloudSoftness)) / 0.95f;

        Vector3 scale = tf.lossyScale;   // world-space size of the cube

        int S = BAKE_SIZE;
        var pixels = new Color[S * S * S];

        // --- Bake loop ---
        for (int zi = 0; zi < S; zi++)
        for (int yi = 0; yi < S; yi++)
        for (int xi = 0; xi < S; xi++)
        {
            // Object-space position in [-0.5, 0.5]
            var pObj = new Vector3(
                (xi + 0.5f) / S - 0.5f,
                (yi + 0.5f) / S - 0.5f,
                (zi + 0.5f) / S - 0.5f);

            // Mirror shader's pMeters: world-offset in the volume's local axes.
            // For an axis-aligned bake this is simply pObj * scale.
            var pMeters = new Vector3(
                pObj.x * scale.x,
                pObj.y * scale.y,
                pObj.z * scale.z);

            // Noise coordinate q (mirrors shader)
            var q = pMeters * invFeatureSize;
            q.y  *= anisotropy;
            q    += (Vector3)seedOffset;

            // FETCH 0: pre-baked FBM from R channel
            float macro = SampleR(noisePx, q * 0.85f);
            if (macro < earlyExitThr)
            {
                pixels[xi + yi * S + zi * S * S] = Color.clear;
                continue;
            }

            // FETCH 1: detail (A channel, 3x scale)
            float detail = SampleA(noisePx, q * (detailScale * 0.333f)
                                   + new Vector3(4.1f, -2.5f, 7.3f));

            float baseShape = Mathf.Clamp01(
                (macro * 0.95f + detail * 0.55f - (0.34f + coverage * 0.38f))
                * (1.8f + cloudSoftness));

            // FETCH 2: erosion (G channel)
            float erosion = SampleG(noisePx, q * (detailScale * 2.1f)
                                    + new Vector3(-6.2f, 5.4f, -3.8f));
            baseShape *= Mathf.Lerp(1f,
                Mathf.Clamp01((erosion - (0.40f + erode * 0.35f)) * 3f), erode);

            // Edge fade
            float maxAx     = Mathf.Max(Mathf.Abs(pObj.x),
                              Mathf.Max(Mathf.Abs(pObj.y), Mathf.Abs(pObj.z)));
            float toBoundary = Mathf.Clamp01((0.5f - maxAx) / Mathf.Max(0.001f, edgeWidth));

            // FETCH 3: breakup (B channel)
            float breakup = Mathf.Clamp01(Mathf.Lerp(
                1f, SampleB(noisePx, q * 0.63f + new Vector3(12.3f, -4.2f, 8.7f)),
                boundaryBreakup));

            float edge    = Mathf.Pow(Mathf.Clamp01(toBoundary * breakup), edgeFade);
            float density = Mathf.Pow(Mathf.Clamp01(baseShape * edge), cloudSoftness);

            // Luminance (inner glow)
            float radial = Mathf.Clamp01(pObj.magnitude / 0.95f);
            float inner  = Mathf.Clamp01(1f - radial * radial);
            float lum    = Mathf.Clamp01(inner * 0.65f + detail * 0.35f);

            // Colour (mirrors sampleCloudColor)
            float midMask  = Mathf.Clamp01(density * 1.2f  + lum * 0.35f);
            float highMask = Mathf.Clamp01((density - 0.28f) * 1.85f + lum * 0.62f);
            float rimMask  = Mathf.Clamp01((1f - density) * 0.7f + (1f - lum) * 0.4f);

            Color col  = Color.Lerp(baseCol, midCol, midMask);
            col        = Color.Lerp(col, highCol, highMask);
            col.r     += rimCol.r * rimMask * 0.2f;
            col.g     += rimCol.g * rimMask * 0.2f;
            col.b     += rimCol.b * rimMask * 0.2f;

            // Bake pre-multiplied lum into emission scale so the simple shader
            // doesn't need to recompute it.  innerGlow is baked into alpha scale.
            float lumEmit = 0.35f + lum * innerGlow;

            // Store: RGB = colour * emission-lum (premul for 1-instruction colour)
            //        A   = raw density (the shader still needs Beer-Lambert on this)
            pixels[xi + yi * S + zi * S * S] = new Color(
                col.r * lumEmit,
                col.g * lumEmit,
                col.b * lumEmit,
                density);
        }

        // --- Write Texture3D asset ---
        string safeName = mr.gameObject.name.Replace(" ", "_").Replace("/", "_");
        string assetPath = $"{OUT_FOLDER}/{safeName}_density.asset";

        var tex3D = new Texture3D(S, S, S, TextureFormat.RGBAHalf, false);
        tex3D.filterMode = FilterMode.Trilinear;
        tex3D.wrapMode   = TextureWrapMode.Clamp;   // clamp: object-space UVs stay in [0,1]
        tex3D.SetPixels(pixels);
        tex3D.Apply();

        AssetDatabase.DeleteAsset(assetPath);
        AssetDatabase.CreateAsset(tex3D, assetPath);

        // --- Swap the material to PreBakedCloudVolume ---
        string bakMatPath = $"{OUT_FOLDER}/{safeName}_mat.mat";
        var bakShader = Shader.Find("Custom/PreBakedCloudVolume");
        if (bakShader == null)
        {
            Debug.LogWarning("[BakeVolumeDensityTex] Shader 'Custom/PreBakedCloudVolume' not found — " +
                             "assign PreBakedCloudVolume.shader and retry.");
        }
        else
        {
            var bakMat = new Material(bakShader) { name = safeName + "_mat" };
            bakMat.SetTexture("_BakedTex", tex3D);
            // Copy runtime-tweakable params that are NOT baked into the texture.
            bakMat.SetFloat("_Steps",      mat.GetFloat("_Steps"));
            bakMat.SetFloat("_Density",    mat.GetFloat("_Density"));
            bakMat.SetFloat("_Absorption", mat.GetFloat("_Absorption"));
            bakMat.SetFloat("_Emission",   mat.GetFloat("_Emission"));
            bakMat.SetFloat("_Opacity",    mat.GetFloat("_Opacity"));
            bakMat.SetFloat("_AlphaFog",   mat.GetFloat("_AlphaFog"));
            bakMat.SetFloat("_StepWorldLength", mat.GetFloat("_StepWorldLength"));

            AssetDatabase.DeleteAsset(bakMatPath);
            AssetDatabase.CreateAsset(bakMat, bakMatPath);

            Undo.RecordObject(mr, "Swap to baked material");
            mr.sharedMaterial = bakMat;
        }

        Debug.Log($"[BakeVolumeDensityTex] '{mr.gameObject.name}' → {assetPath}");
    }

    // =========================================================================
    // C# trilinear sampler — mirrors the shader's tex3D(frac(p * NTILE_INV))
    // =========================================================================
    static Color32 FetchPx(Color32[] px, int x, int y, int z)
    {
        x = ((x % NOISE_SIZE) + NOISE_SIZE) % NOISE_SIZE;
        y = ((y % NOISE_SIZE) + NOISE_SIZE) % NOISE_SIZE;
        z = ((z % NOISE_SIZE) + NOISE_SIZE) % NOISE_SIZE;
        return px[x + y * NOISE_SIZE + z * NOISE_SIZE * NOISE_SIZE];
    }

    static float Frac(float v) => v - Mathf.Floor(v);

    static Color32 Trilinear(Color32[] px, Vector3 p)
    {
        // Map to [0,1] UV — mirrors frac(p * NTILE_INV)
        float u = Frac(p.x * NTILE_INV);
        float v = Frac(p.y * NTILE_INV);
        float w = Frac(p.z * NTILE_INV);

        float fx = u * NOISE_SIZE - 0.5f;
        float fy = v * NOISE_SIZE - 0.5f;
        float fz = w * NOISE_SIZE - 0.5f;

        int x0 = Mathf.FloorToInt(fx), x1 = x0 + 1;
        int y0 = Mathf.FloorToInt(fy), y1 = y0 + 1;
        int z0 = Mathf.FloorToInt(fz), z1 = z0 + 1;

        float tx = fx - Mathf.Floor(fx);
        float ty = fy - Mathf.Floor(fy);
        float tz = fz - Mathf.Floor(fz);

        Color32 c000 = FetchPx(px, x0, y0, z0), c100 = FetchPx(px, x1, y0, z0);
        Color32 c010 = FetchPx(px, x0, y1, z0), c110 = FetchPx(px, x1, y1, z0);
        Color32 c001 = FetchPx(px, x0, y0, z1), c101 = FetchPx(px, x1, y0, z1);
        Color32 c011 = FetchPx(px, x0, y1, z1), c111 = FetchPx(px, x1, y1, z1);

        byte Lerp8(byte a, byte b, float t) => (byte)Mathf.RoundToInt(Mathf.Lerp(a, b, t));
        Color32 Lerp32(Color32 a, Color32 b, float t) => new Color32(
            Lerp8(a.r, b.r, t), Lerp8(a.g, b.g, t), Lerp8(a.b, b.b, t), Lerp8(a.a, b.a, t));

        Color32 x00 = Lerp32(c000, c100, tx), x10 = Lerp32(c010, c110, tx);
        Color32 x01 = Lerp32(c001, c101, tx), x11 = Lerp32(c011, c111, tx);
        Color32 y0v = Lerp32(x00, x10, ty),   y1v = Lerp32(x01, x11, ty);
        return Lerp32(y0v, y1v, tz);
    }

    static float SampleR(Color32[] px, Vector3 p) => Trilinear(px, p).r / 255f;
    static float SampleG(Color32[] px, Vector3 p) => Trilinear(px, p).g / 255f;
    static float SampleB(Color32[] px, Vector3 p) => Trilinear(px, p).b / 255f;
    static float SampleA(Color32[] px, Vector3 p) => Trilinear(px, p).a / 255f;
}

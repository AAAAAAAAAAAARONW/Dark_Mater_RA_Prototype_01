using UnityEngine;
using UnityEditor;

/// <summary>
/// Bakes a 32^3 seamlessly-tiling RGBA32 Texture3D for PurpleCloudVolume.shader.
///
/// Channel layout (optimized for minimum per-step texture fetches in the shader):
///   R  —  3-octave FBM (pre-baked; shader does 1 fetch instead of 3 for macro shape)
///   G  —  erosion single-octave      (seed +17,+13,+7)
///   B  —  breakup single-octave      (seed -11,+31,-19)
///   A  —  2-octave detail FBM        (seed +41,-23,+37 at 3x scale, decorrelated from R)
///
/// R channel stores pre-computed weighted sum: oct1*0.5 + oct2*0.26 + oct3*0.1352
/// (normalized to [0,1]).  The 2x and 4x octaves tile correctly because 32 mod 2 = 0
/// and 32 mod 4 = 0 — all octave periods divide the texture size evenly.
///
/// This cuts the shader's hot-path from 6 tex3D fetches/step down to 4,
/// and the early-exit path from 3 fetches down to 1.
///
/// 32^3 RGBA32 = 128 KB — fits in GPU L2 texture cache.
/// Run once via: Tools > Laxi > Bake Cloud Noise Texture (32^3)
/// Auto-assigns the result to every Custom/PurpleCloudVolume material.
/// </summary>
public static class BakeCloudNoise
{
    const int    SIZE     = 32;
    const string OUT_PATH = "Assets/Shaders/CloudNoiseTex3D.asset";

    // Must match FBM3_NORM in the shader.
    const float FBM3_NORM = 1.0f / (0.500f + 0.260f + 0.1352f);  // ≈ 1.1173

    static float Frac(float x) => x - Mathf.Floor(x);

    // Seamlessly-tiling value noise.  Coordinates are wrapped modulo SIZE so the
    // texture tiles with period SIZE in every axis (and period SIZE/k for k-times
    // scaled octaves, which also divide SIZE evenly when k ∈ {2,4}).
    static float Hash31Periodic(int xi, int yi, int zi)
    {
        int x = ((xi % SIZE) + SIZE) % SIZE;
        int y = ((yi % SIZE) + SIZE) % SIZE;
        int z = ((zi % SIZE) + SIZE) % SIZE;

        var p = new Vector3(x * 0.3183099f + 0.1f,
                            y * 0.3183099f + 0.1f,
                            z * 0.3183099f + 0.1f);
        p = new Vector3(Frac(p.x), Frac(p.y), Frac(p.z)) * 17.0f;
        return Frac(p.x * p.y * p.z * (p.x + p.y + p.z));
    }

    [MenuItem("Tools/Laxi/Bake Cloud Noise Texture (32^3)")]
    static void Bake()
    {
        var tex = new Texture3D(SIZE, SIZE, SIZE, TextureFormat.RGBA32, false);
        tex.wrapMode   = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Trilinear;
        tex.anisoLevel = 0;

        var pixels = new Color32[SIZE * SIZE * SIZE];

        for (int z = 0; z < SIZE; z++)
        for (int y = 0; y < SIZE; y++)
        for (int x = 0; x < SIZE; x++)
        {
            int idx = x + y * SIZE + z * SIZE * SIZE;

            // R: pre-baked 3-octave FBM — shader samples once to get what used to
            //    require 3 separate tex3D calls (fbm3 at octaves 1x/2x/4x).
            float oct1   = Hash31Periodic(x,     y,     z    );
            float oct2   = Hash31Periodic(x * 2, y * 2, z * 2);
            float oct3   = Hash31Periodic(x * 4, y * 4, z * 4);
            float macroR = Mathf.Clamp01((oct1 * 0.500f + oct2 * 0.260f + oct3 * 0.1352f) * FBM3_NORM);

            // G: erosion (unchanged — single-octave, independent seed)
            float eroG   = Hash31Periodic(x + 17, y + 13, z +  7);

            // B: breakup (unchanged — single-octave, independent seed)
            float brkB   = Hash31Periodic(x - 11, y + 31, z - 19);

            // A: 2-octave detail FBM at ~3x internal scale, decorrelated from R.
            //    Shader samples A at base UV (q * 1.0) to get detail equivalent to
            //    sampling a 1x noise field at q * 3 — saves the _DetailScale multiply.
            float det1   = Hash31Periodic(x * 3 + 41, y * 3 - 23, z * 3 + 37);
            float det2   = Hash31Periodic(x * 6 + 41, y * 6 - 23, z * 6 + 37);
            float detA   = Mathf.Clamp01(det1 * 0.65f + det2 * 0.35f);

            pixels[idx] = new Color32(
                (byte)(macroR * 255f),
                (byte)(eroG   * 255f),
                (byte)(brkB   * 255f),
                (byte)(detA   * 255f)
            );
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        AssetDatabase.DeleteAsset(OUT_PATH);
        AssetDatabase.CreateAsset(tex, OUT_PATH);

        var texAsset = AssetDatabase.LoadAssetAtPath<Texture3D>(OUT_PATH);
        int count    = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
        {
            string   path = AssetDatabase.GUIDToAssetPath(guid);
            var      mat  = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null && mat.shader != null &&
                mat.shader.name == "Custom/PurpleCloudVolume")
            {
                mat.SetTexture("_NoiseTex", texAsset);
                EditorUtility.SetDirty(mat);
                count++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[BakeCloudNoise] {SIZE}^3 noise texture baked → {OUT_PATH}  " +
                  $"(auto-assigned to {count} PurpleCloudVolume material(s))\n" +
                  $"Channel layout: R=FBM3(1x+2x+4x), G=erosion, B=breakup, A=detailFBM2(3x+6x)");
    }
}

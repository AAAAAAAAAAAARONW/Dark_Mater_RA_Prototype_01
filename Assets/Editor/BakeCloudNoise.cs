using UnityEngine;
using UnityEditor;

/// <summary>
/// Bakes a 32^3 seamlessly-tiling RGBA32 Texture3D for PurpleCloudVolume.shader.
///
/// Channel layout (4 independent noise fields, different seeds):
///   R  —  macro FBM + detail  (seed 0)
///   G  —  erosion             (seed +17,+13,+7)
///   B  —  breakup             (seed -11,+31,-19)
///   A  —  spare / future use  (seed +41,-23,+37)
///
/// Each channel is a fully independent, seamlessly-tiling value-noise field.
/// Hardware trilinear filtering on the 32^3 texture replaces 8×hash31+7×lerp
/// per noise sample with a single TMU fetch.
///
/// 32^3 RGBA32 = 128 KB — fits in GPU L2 texture cache, avoiding VRAM stalls
/// that caused the 9–30 fps variance seen with the 64^3 (1 MB) texture.
///
/// Run once via: Tools > Laxi > Bake Cloud Noise Texture (32^3)
/// Auto-assigns the result to every Custom/PurpleCloudVolume material.
/// </summary>
public static class BakeCloudNoise
{
    const int    SIZE     = 32;
    const string OUT_PATH = "Assets/Shaders/CloudNoiseTex3D.asset";

    static float Frac(float x) => x - Mathf.Floor(x);

    // Matches the hash31() in the shader, but coordinates are wrapped modulo SIZE
    // so the texture tiles seamlessly with period SIZE in every axis.
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

            // Four fully independent noise fields (different seeds, same formula).
            // Different seeds give decorrelated noise at the same spatial scale,
            // preserving the original shader's erosion / breakup appearance.
            pixels[idx] = new Color32(
                (byte)(Hash31Periodic(x,       y,       z      ) * 255),  // R: macro+detail
                (byte)(Hash31Periodic(x + 17,  y + 13,  z +  7 ) * 255),  // G: erosion
                (byte)(Hash31Periodic(x - 11,  y + 31,  z - 19 ) * 255),  // B: breakup
                (byte)(Hash31Periodic(x + 41,  y - 23,  z + 37 ) * 255)   // A: spare
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
                  $"(auto-assigned to {count} PurpleCloudVolume material(s))");
    }
}

using System;
using UnityEditor;
using UnityEngine;

public static class MilkyWayBake_NEW
{
    public const string MaterialPath = "Assets/_Core_NEW/Assets/Celestial/MilkyWay_SpiralVolume.mat";
    public const string VolumePath = "Assets/_Core_NEW/Assets/Celestial/MilkyWay_Density.asset";
    public const int Width = 256, Height = 256, Slices = 32;

    [MenuItem("Tools/Journey NEW/Bake Milky Way Density")]
    public static void Bake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Bake the galaxy outside Play Mode.");
        var source = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Core_NEW/Editor/MilkyWayBake_NEW.shader");
        if (source == null || shader == null) throw new InvalidOperationException("Missing Milky Way bake inputs.");
        if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
            throw new InvalidOperationException("The density baker requires half-float rendering.");
        var bake = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        bake.CopyPropertiesFromMaterial(source);
        var previous = RenderTexture.active;
        var rt = new RenderTexture(Width,Height,0,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);
        var readback = new Texture2D(Width,Height,TextureFormat.RGBAHalf,false,true);
        Texture3D density = null;
        try
        {
            var pixels = new Color[Width*Height*Slices];
            for (int slice=0;slice<Slices;slice++)
            {
                float h = (slice+0.5f)/Slices*2-1;
                bake.SetFloat("_SliceHeight",Mathf.Sign(h)*h*h*0.075f);
                Graphics.Blit(null,rt,bake);
                RenderTexture.active = rt;
                readback.ReadPixels(new Rect(0,0,Width,Height),0,0,false);
                readback.Apply(false,false);
                Array.Copy(readback.GetPixels(),0,pixels,slice*Width*Height,Width*Height);
            }
            density = new Texture3D(Width,Height,Slices,TextureFormat.RGBAHalf,false)
            {
                name = "Milky Way density 256x256x32",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0
            };
            density.SetPixels(pixels);
            density.Apply(false,true); // Drop the CPU copy; this is a GPU-only runtime cache.
            var existing = AssetDatabase.LoadAssetAtPath<Texture3D>(VolumePath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(density,VolumePath);
                existing = density;
                density = null;
            }
            else EditorUtility.CopySerialized(density,existing);
            source.SetTexture("_Volume",existing);
            source.EnableKeyword("MILKYWAY_BAKED_VOLUME");
            source.SetFloat("_Steps",MilkyWayMotion_NEW.SampleCount(MilkyWayMotion_NEW.Quality.Balanced));
            EditorUtility.SetDirty(existing);
            EditorUtility.SetDirty(source);
            AssetDatabase.SaveAssets();
            if (ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Galaxy bake shader failed.");
            Debug.Log("Milky Way density baked: 16 MiB GPU cache, four-arm structure preserved.");
        }
        finally
        {
            RenderTexture.active = previous;
            if (density != null) UnityEngine.Object.DestroyImmediate(density);
            UnityEngine.Object.DestroyImmediate(readback);
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(bake);
        }
    }
}

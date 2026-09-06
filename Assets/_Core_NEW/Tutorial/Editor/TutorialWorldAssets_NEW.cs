using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Finds, and where necessary generates, the assets the tutorial rig needs.
///
/// Split out of TutorialSceneBuilder_NEW so the builder reads as layout and this reads
/// as sourcing. Two rules govern what goes where:
///
///   * Anything the project already has is looked up, never recreated. The quasar, the
///     sky and the photon trail all exist and are already tuned; a tutorial that ships
///     its own copy of the quasar look is a second set of numbers nobody remembers to
///     keep in step. The paths are constants so a moved asset fails with a named
///     warning instead of a silent grey sphere.
///   * Anything that does not exist is generated once as a real asset file, not as a
///     material created in memory and serialised into the scene. Scene-embedded
///     materials cannot be inspected from the Project window, cannot be reused by a
///     second scene, and are the reason PlaytestBuild has a trail material that exists
///     nowhere on disk.
///
/// The generated assets are deliberately plain. They are whitebox stand-ins with the
/// right blend mode and the right colour range, sized so the scene reads at the
/// Observatories' distance. Art replaces the .mat and the builder never has to change.
/// </summary>
public static class TutorialWorldAssets_NEW
{
    // ── Existing project assets ──────────────────────────────────────────────

    /// <summary>The nebula skybox PlaytestBuild uses. Driven by NebulaProfile_NEW.</summary>
    public const string NebulaSkyboxPath = "Assets/Materials/Custom_Nebula.mat";

    /// <summary>Quasar core, halo, accretion disc and bipolar jets, in one shader.</summary>
    public const string BlazingQuasarPath = "Assets/Materials/BlazingQuasar.mat";

    /// <summary>
    /// The photon trail material. Custom/PhotonTrail, additive, spectrum- and
    /// absorption-aware. Copied rather than used directly — see PhotonTrailMaterial.
    /// </summary>
    public const string PhotonTrailPath = "Assets/Shaders/Custom_PhotonTrail.mat";

    /// <summary>The quasar layer's sky look, shared with the journey so the two cannot drift.</summary>
    public const string QuasarNebulaProfilePath = "Assets/_Core_NEW/Assets/NP_Quasar.asset";

    // ── Generated assets ─────────────────────────────────────────────────────

    const string GeneratedFolder = "Assets/_Core_NEW/Tutorial/Assets";
    const string SoftDotPath = GeneratedFolder + "/TutorialSoftDot.png";
    const string DustMaterialPath = GeneratedFolder + "/TutorialDiscDust.mat";
    const string MoteMaterialPath = GeneratedFolder + "/TutorialMote.mat";
    const string TrailMaterialPath = GeneratedFolder + "/TutorialPhotonTrail.mat";

    // ── Lookup ───────────────────────────────────────────────────────────────

    public static Material NebulaSkybox { get { return Load<Material>(NebulaSkyboxPath, "nebula skybox"); } }
    public static Material BlazingQuasar { get { return Load<Material>(BlazingQuasarPath, "quasar"); } }

    /// <summary>
    /// The tutorial's own copy of the photon trail material.
    ///
    /// Not the shared asset, deliberately. PhotonSpectrumTrail is [ExecuteAlways], and
    /// in edit mode it writes the generated spectrum texture into `sharedMaterial` —
    /// which is the project asset on disk. Point the tutorial at Custom_PhotonTrail.mat
    /// directly and simply having the scene open would rewrite the material the journey
    /// uses. This is the same hazard NebulaResponder_NEW's summary records for the
    /// skybox, and PlaytestBuild dodges it the same way: its Trail carries an instance,
    /// not the asset.
    ///
    /// A copy rather than a runtime clone because the write happens in edit mode, where
    /// there is no runtime to clone in.
    /// </summary>
    public static Material PhotonTrailMaterial()
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(TrailMaterialPath);
        if (existing != null) return existing;

        Material source = Load<Material>(PhotonTrailPath, "photon trail");
        if (source == null) return null;

        EnsureFolder();

        if (!AssetDatabase.CopyAsset(PhotonTrailPath, TrailMaterialPath))
        {
            Debug.LogWarning("[TutorialWorldAssets_NEW] Could not copy " + PhotonTrailPath +
                             ". The trail will use the shared asset, which PhotonSpectrumTrail " +
                             "will then write into.");
            return source;
        }

        AssetDatabase.ImportAsset(TrailMaterialPath, ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("[TutorialWorldAssets_NEW] Generated " + TrailMaterialPath +
                  " as a copy of " + PhotonTrailPath + ".");

        return AssetDatabase.LoadAssetAtPath<Material>(TrailMaterialPath);
    }

    public static NebulaProfile_NEW QuasarNebulaProfile
    {
        get { return Load<NebulaProfile_NEW>(QuasarNebulaProfilePath, "quasar nebula profile"); }
    }

    static T Load<T>(string path, string label) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);

        if (asset == null)
            Debug.LogWarning("[TutorialWorldAssets_NEW] Could not find the " + label + " at " + path +
                             ". The tutorial will build without it; the scene will look wrong " +
                             "rather than fail. Update the path constant if the asset moved.");

        return asset;
    }

    // ── Generation ───────────────────────────────────────────────────────────

    /// <summary>
    /// A soft round dot, generated once. Used by the disc dust so particles read as
    /// motes rather than as squares.
    ///
    /// Written as a PNG asset rather than a runtime Texture2D because a runtime texture
    /// cannot be serialised into a material asset — it would come back null on the next
    /// domain reload and the dust would render as white quads.
    /// </summary>
    public static Texture2D SoftDot()
    {
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(SoftDotPath);
        if (existing != null) return existing;

        EnsureFolder();

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

        float centre = (size - 1) * 0.5f;
        float radius = centre;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre)) / radius;

                // Squared falloff: a hard-ish core with a long soft edge, which is what
                // reads as a glow when many of them overlap additively.
                float a = Mathf.Clamp01(1f - d);
                a = a * a;

                texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        texture.Apply();

        File.WriteAllBytes(SoftDotPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(SoftDotPath, ImportAssetOptions.ForceSynchronousImport);

        TextureImporter importer = AssetImporter.GetAtPath(SoftDotPath) as TextureImporter;
        if (importer != null)
        {
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        Debug.Log("[TutorialWorldAssets_NEW] Generated " + SoftDotPath + ".");

        return AssetDatabase.LoadAssetAtPath<Texture2D>(SoftDotPath);
    }

    /// <summary>Additive dust material for the accretion disc particles.</summary>
    public static Material DiscDustMaterial()
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(DustMaterialPath);
        if (existing != null) return existing;

        Shader shader = FindAdditiveParticleShader();
        if (shader == null) return null;

        EnsureFolder();

        Material material = new Material(shader);
        material.name = "TutorialDiscDust";
        material.mainTexture = SoftDot();

        // A1: dark red matter. Additive, so keep it well under white or the dust reads
        // as snow instead of as glowing gas.
        if (material.HasProperty("_TintColor"))
            material.SetColor("_TintColor", new Color(0.42f, 0.10f, 0.06f, 0.35f));

        AssetDatabase.CreateAsset(material, DustMaterialPath);
        Debug.Log("[TutorialWorldAssets_NEW] Generated " + DustMaterialPath + ".");

        return material;
    }

    /// <summary>Unlit material for the guide motes. Flat and bright, so it survives a dark scene.</summary>
    public static Material MoteMaterial()
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(MoteMaterialPath);
        if (existing != null) return existing;

        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null)
        {
            Debug.LogWarning("[TutorialWorldAssets_NEW] Unlit/Color not found. Motes will use " +
                             "the default material and look like grey spheres.");
            return null;
        }

        EnsureFolder();

        Material material = new Material(shader);
        material.name = "TutorialMote";
        material.SetColor("_Color", new Color(1f, 0.93f, 0.74f, 1f));

        AssetDatabase.CreateAsset(material, MoteMaterialPath);
        Debug.Log("[TutorialWorldAssets_NEW] Generated " + MoteMaterialPath + ".");

        return material;
    }

    static Shader FindAdditiveParticleShader()
    {
        // Mobile/Particles/Additive is the one built-in additive particle shader present
        // in every built-in-pipeline install. The legacy path is checked second only
        // because it is not guaranteed to be stripped into a player build.
        Shader shader = Shader.Find("Mobile/Particles/Additive");
        if (shader != null) return shader;

        shader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (shader != null) return shader;

        Debug.LogWarning("[TutorialWorldAssets_NEW] No built-in additive particle shader found. " +
                         "The disc dust will be skipped.");
        return null;
    }

    static void EnsureFolder()
    {
        if (AssetDatabase.IsValidFolder(GeneratedFolder)) return;

        AssetDatabase.CreateFolder("Assets/_Core_NEW/Tutorial", "Assets");
    }
}

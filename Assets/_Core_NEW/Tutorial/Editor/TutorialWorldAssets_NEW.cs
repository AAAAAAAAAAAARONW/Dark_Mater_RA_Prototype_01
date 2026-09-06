using System.IO;
using TMPro;
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

    /// <summary>
    /// The post-process profile PlaytestBuild_NEW's Main Camera uses. Shared rather than
    /// copied: grade and bloom are what make the quasar read as bright rather than pale,
    /// and a tutorial with its own grade would drift away from the journey's look with
    /// nobody noticing until they are seen side by side.
    /// </summary>
    public const string PostProcessProfilePath = "Assets/Scenes/Test Level_Profiles/Main Camera Profile.asset";

    /// <summary>Layer 11 in this project. PlaytestBuild puts its camera and volume here.</summary>
    public const string PostProcessLayerName = "PostProcessing";

    /// <summary>
    /// The HUD typeface. Gontserrat, because it is what PlaytestBuild's UI is already
    /// set in — the tutorial and the journey should not look like two products.
    ///
    /// The project also has PCap Terminal and Utendo; the scene uses Gontserrat for most
    /// of its labels, so that is the one to match.
    /// </summary>
    public const string HudFontPath = "Assets/Fonts/Gontserrat-Regular SDF.asset";

    public static TMP_FontAsset HudFont()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(HudFontPath);

        if (font == null)
            Debug.LogWarning("[TutorialWorldAssets_NEW] Could not find the HUD font at " +
                             HudFontPath + ". Labels will fall back to TextMeshPro's " +
                             "default, which is not the typeface the rest of the piece uses.");

        return font;
    }

    // ── Generated assets ─────────────────────────────────────────────────────

    const string GeneratedFolder = "Assets/_Core_NEW/Tutorial/Assets";
    const string SoftDotPath = GeneratedFolder + "/TutorialSoftDot.png";
    const string DustMaterialPath = GeneratedFolder + "/TutorialDiscDust.mat";
    const string MoteMaterialPath = GeneratedFolder + "/TutorialMote.mat";
    const string TrailMaterialPath = GeneratedFolder + "/TutorialPhotonTrail.mat";
    const string ChipPath = GeneratedFolder + "/TutorialChip.png";
    const string RingPath = GeneratedFolder + "/TutorialRing.png";
    const string DiscPath = GeneratedFolder + "/TutorialDisc.png";
    const string GlowPath = GeneratedFolder + "/TutorialGlow.png";
    const string ArrowPath = GeneratedFolder + "/TutorialArrow.png";

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

    // ── HUD sprites ──────────────────────────────────────────────────────────
    //
    // The HUD was white Arial on nothing, which is a debug readout rather than an
    // interface. These three sprites are what it takes for a prompt to read as a
    // designed element at the back of a room: something behind the words, and a glyph
    // that looks like a button rather than a letter.
    //
    // Generated rather than drawn because they are pure geometry at one colour — art
    // replaces the PNGs in place and nothing in the builder changes.

    /// <summary>Rounded rectangle, 9-sliced. The plate behind a prompt.</summary>
    public static Sprite ChipSprite()
    {
        return GenerateSprite(ChipPath, 48, PaintRoundedRect, 16f);
    }

    /// <summary>Ring outline. The reticle.</summary>
    public static Sprite RingSprite()
    {
        return GenerateSprite(RingPath, 64, PaintRing, 0f);
    }

    /// <summary>Filled circle with a soft edge. The button glyph behind the letter A.</summary>
    public static Sprite DiscSprite()
    {
        return GenerateSprite(DiscPath, 64, PaintDisc, 0f);
    }

    /// <summary>Radial falloff to nothing. The halo that makes the quasar read as bright
    /// rather than merely yellow.</summary>
    public static Sprite GlowSprite()
    {
        return GenerateSprite(GlowPath, 64, PaintGlow, 0f);
    }

    /// <summary>Triangle, base at the bottom and point at the top. The player's facing on
    /// the map — see TutorialRangeMap_NEW, which rests it pointing up and turns it from
    /// there.</summary>
    public static Sprite ArrowSprite()
    {
        return GenerateSprite(ArrowPath, 64, PaintArrow, 0f);
    }

    delegate float Painter(float x, float y, int size);

    static Sprite GenerateSprite(string path, int size, Painter painter, float border)
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;

        EnsureFolder();

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, painter(x, y, size)));

        texture.Apply();

        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;

            if (border > 0f)
            {
                // 9-slice, so one 48px plate stretches to any prompt width without the
                // corners smearing.
                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteBorder = new Vector4(border, border, border, border);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
            }

            importer.SaveAndReimport();
        }

        Debug.Log("[TutorialWorldAssets_NEW] Generated " + path + ".");

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static float PaintRoundedRect(float x, float y, int size)
    {
        float radius = size * 0.33f;
        float max = size - 1f;

        // Distance outside the rounded rectangle, in pixels.
        float dx = Mathf.Max(radius - x, 0f, x - (max - radius));
        float dy = Mathf.Max(radius - y, 0f, y - (max - radius));
        float d = Mathf.Sqrt(dx * dx + dy * dy);

        return Mathf.Clamp01(1f - (d - radius + 1f));
    }

    static float PaintRing(float x, float y, int size)
    {
        float centre = (size - 1) * 0.5f;
        float r = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre));

        float outer = centre - 1f;
        float thickness = size * 0.09f;

        // One-pixel soft edge on both sides, so the ring is not aliased at HUD scale.
        float a = Mathf.Clamp01(outer - r) * Mathf.Clamp01(r - (outer - thickness));
        return Mathf.Clamp01(a);
    }

    static float PaintDisc(float x, float y, int size)
    {
        float centre = (size - 1) * 0.5f;
        float r = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre));

        return Mathf.Clamp01(centre - 1f - r);
    }

    static float PaintGlow(float x, float y, int size)
    {
        float centre = (size - 1) * 0.5f;
        float r = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre)) / centre;

        // Squared falloff. Linear reads as a flat disc with a fuzzy rim; this has a bright
        // middle that fades out, which is what a light source looks like.
        float a = Mathf.Clamp01(1f - r);
        return a * a;
    }

    static float PaintArrow(float x, float y, int size)
    {
        float max = size - 1f;
        float centre = max * 0.5f;

        // Base along the bottom edge, apex at the top middle: half the width at this row
        // shrinks to nothing by the time it reaches the top.
        float halfWidth = (1f - y / max) * centre;

        // One-pixel soft edge, or the diagonals stair-step badly at twelve pixels across.
        return Mathf.Clamp01(halfWidth - Mathf.Abs(x - centre) + 1f);
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

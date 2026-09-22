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
    /// The tutorial's own spectrum settings. NOT shared with the journey, which is the
    /// opposite of how the sky is handled and is deliberate.
    ///
    /// The journey's SP_* profiles spawn a background Lyman-alpha forest — four lines a
    /// second — and AbsorptionField_NEW.ClearLines() says in its own summary that
    /// "Background spawning continues". D2 has to be one atom and one line, so a
    /// tutorial running on a journey profile buries its single hydrogen line under forty
    /// others inside ten seconds, and the frame the whole of Phase 3 rests on reads as
    /// noise.
    ///
    /// So SP_Tutorial holds spawnRatePerSecond, initialLineCount and driftPerSecond at
    /// zero. TutorialSpectrum_NEW checks that at runtime rather than trusting it.
    /// </summary>
    public const string TutorialSpectrumProfilePath = "Assets/_Core_NEW/Assets/SP_Tutorial.asset";

    /// <summary>SP_Tutorial with the redshift drift on. Generated, not authored.</summary>
    public const string TutorialSpectrumDriftProfilePath =
        "Assets/_Core_NEW/Assets/SP_Tutorial_Drift.asset";

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
    const string AtomCoreMaterialPath = GeneratedFolder + "/TutorialAtomCore.mat";
    const string AtomHaloMaterialPath = GeneratedFolder + "/TutorialAtomHalo.mat";
    const string TrailMaterialPath = GeneratedFolder + "/TutorialPhotonTrail.mat";
    const string ChipPath = GeneratedFolder + "/TutorialChip.png";
    const string RingPath = GeneratedFolder + "/TutorialRing.png";
    const string DiscPath = GeneratedFolder + "/TutorialDisc.png";
    const string GlowPath = GeneratedFolder + "/TutorialGlow.png";
    const string ArrowPath = GeneratedFolder + "/TutorialArrow.png";

    /// <summary>Drawn prompt art, delivered as PNGs rather than generated here.</summary>
    public const string HintFolder = GeneratedFolder + "/Hints";

    /// <summary>The title on the attract card.</summary>
    public const string TitleArtName = "TutorialTitle_JourneyOfLight";

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

    /// <summary>
    /// The tutorial's spectrum profile. Create it with Assets > Create > Journey NEW >
    /// Spectrum Profile if it is missing, and zero the three spawn fields — see
    /// TutorialSpectrumProfilePath.
    /// </summary>
    public static SpectrumProfile_NEW TutorialSpectrumProfile
    {
        get { return Load<SpectrumProfile_NEW>(TutorialSpectrumProfilePath, "tutorial spectrum profile"); }
    }

    /// <summary>
    /// The same profile with the redshift drift switched on. D3 swaps to this.
    ///
    /// Generated from SP_Tutorial rather than authored beside it, so the two cannot
    /// disagree about anything except the one number they are meant to differ on. The
    /// seed is copied deliberately: Configure only re-seeds when the seed changes, and a
    /// re-seed at D3 would change what the forest does under a line the player has just
    /// watched being cut.
    /// </summary>
    /// <summary>
    /// How fast the tutorial's spectrum drifts, in fractions of the spectrum per second.
    ///
    /// THIS NUMBER DECIDES HOW LONG AN ABSORPTION LOOKS LIKE IT HAPPENED AT THE PEAK.
    /// Every atom cuts at the same wavelength, 0.412, which sits on the Ly-alpha peak at
    /// 0.42 — the tallest thing on the bar and therefore the one place a line can take a
    /// bite worth two thirds of the bar's height. The drift then carries it off, and the
    /// peak is narrow: a sigma of 0.0085 blueward and 0.013 red. At 0.02 a fresh line was
    /// clear of the peak in well under a second and spent the rest of its life as a
    /// shallow mark on the flat shelf, so the deep notch the frame is about was over
    /// before anybody had looked up.
    ///
    /// At 0.009 it is about six pixels a second on a 720 pixel bar — still plainly moving,
    /// and a new line stays in the peak's bright neighbourhood for two to three seconds,
    /// which is long enough to be read as having been cut there.
    ///
    /// IT IS ALSO HALF OF THE SPACING BETWEEN LINES. The gap between two marks is this
    /// rate times the seconds between the atoms that made them, so slowing it down means
    /// spacing the atoms further apart or the forest merges into one thick line — see
    /// TutorialAtomCluster_NEW.secondsApart, which moved with it.
    /// </summary>
    const float TutorialDriftPerSecond = 0.009f;

    /// <summary>
    /// The same rate, for the builder to put on the still profile — which is not still
    /// any more. Both profiles now drift; the pair is kept because Configure is the seam
    /// D7 uses and removing it would mean rewiring a beat to prove a point.
    /// </summary>
    public static float DriftPerSecond { get { return TutorialDriftPerSecond; } }

    public static SpectrumProfile_NEW TutorialSpectrumDriftProfile()
    {
        SpectrumProfile_NEW existing =
            AssetDatabase.LoadAssetAtPath<SpectrumProfile_NEW>(TutorialSpectrumDriftProfilePath);

        if (existing != null)
        {
            // Generated content, not a tuned value — the same distinction beats draw with
            // builderOwnsCopy. A rate this file used to write is corrected on the next
            // run, because a changed constant otherwise never reaches an asset that has
            // already been created.
            if (!Mathf.Approximately(existing.driftPerSecond, TutorialDriftPerSecond))
            {
                Debug.Log("[TutorialWorldAssets_NEW] " + existing.name + " drift " +
                          existing.driftPerSecond + " -> " + TutorialDriftPerSecond + ".");

                existing.driftPerSecond = TutorialDriftPerSecond;
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
            }

            return existing;
        }

        SpectrumProfile_NEW still = TutorialSpectrumProfile;
        if (still == null) return null;

        SpectrumProfile_NEW drifting = Object.Instantiate(still);
        drifting.name = "SP_Tutorial_Drift";
        drifting.driftPerSecond = TutorialDriftPerSecond;

        AssetDatabase.CreateAsset(drifting, TutorialSpectrumDriftProfilePath);
        Debug.Log("[TutorialWorldAssets_NEW] Generated " + TutorialSpectrumDriftProfilePath +
                  " from " + still.name + ", with drift switched on.");

        return drifting;
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
    /// <summary>
    /// The hydrogen atom's core. A hard, small, cold-white point.
    ///
    /// Separate from the mote material and deliberately a different colour. The guide
    /// motes are warm yellow and mean "come and look at this"; the atom is the first
    /// thing in the piece that is not a guide, and a player who reads it as a fourth
    /// mote will be waiting for it to bloom rather than watching it arrive.
    ///
    /// Unlit rather than additive, so the core stays a solid object with an edge and
    /// the halo around it does the glowing. An additive core has no silhouette and reads
    /// as a smudge at distance, which is most of D1.
    /// </summary>
    /// <summary>
    /// The atom's core: a translucent cold-blue sphere, not a solid bright one.
    ///
    /// IT USED TO BE Unlit/Color AT NEARLY WHITE, on the reasoning that it needed a hard
    /// edge to read as an object at 200 metres and that sitting above the bloom
    /// threshold would give it a halo of its own. Both are true and together they made
    /// it a headlight: a flat white ball, brighter than the quasar it is flying past,
    /// and — because it is opaque — a hole punched in whatever is behind it.
    ///
    /// Hydrogen is a single atom. It should read as something the light is passing
    /// THROUGH, which means you have to be able to see through it. Alpha blended at a
    /// low tint gives a soft body you can see the starfield in, and the separate
    /// additive halo still does the work of making it visible at distance.
    ///
    /// The legacy particle shaders take colour AND alpha through _TintColor and multiply
    /// the result by two, which is why the numbers below look half of what they are.
    /// </summary>
    public static Material AtomCoreMaterial()
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(AtomCoreMaterialPath);
        if (existing != null) return existing;

        Shader shader = FindAlphaBlendedParticleShader();
        if (shader == null) return null;

        EnsureFolder();

        Material material = new Material(shader);
        material.name = "TutorialAtomCore";

        ApplyAtomCoreLook(material);

        AssetDatabase.CreateAsset(material, AtomCoreMaterialPath);
        Debug.Log("[TutorialWorldAssets_NEW] Generated " + AtomCoreMaterialPath + ".");

        return material;
    }

    /// <summary>
    /// The core's colour and translucency, in one place so the builder can apply it to a
    /// material generated before the atom stopped being opaque.
    ///
    /// Doubled by the shader, so this is half the colour you want and half the alpha.
    /// 0.22 of alpha lands at about 45% opaque: a body, with the sky visible through it.
    /// </summary>
    public static void ApplyAtomCoreLook(Material material)
    {
        if (material == null) return;

        material.mainTexture = null;

        if (material.HasProperty("_TintColor"))
            material.SetColor("_TintColor", new Color(0.30f, 0.40f, 0.50f, 0.22f));

        // Left over from the opaque version, and ignored by the particle shaders — but a
        // stale white here is exactly the sort of thing that gets read as the live value
        // when somebody comes back to this in three months.
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", new Color(0.60f, 0.80f, 1f, 0.45f));
    }

    /// <summary>
    /// An alpha-blended particle shader, by the same reasoning and the same order as
    /// FindAdditiveParticleShader.
    ///
    /// Falls back to the additive one rather than to nothing: additive is see-through
    /// too, which is the half of this that matters, and a dim atom beats no atom.
    /// </summary>
    /// <summary>The shader AtomCoreMaterial would pick, for the builder's repair.</summary>
    public static Shader AlphaBlendedParticleShader() { return FindAlphaBlendedParticleShader(); }

    static Shader FindAlphaBlendedParticleShader()
    {
        Shader shader = Shader.Find("Mobile/Particles/Alpha Blended");
        if (shader != null) return shader;

        shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        if (shader != null) return shader;

        Debug.LogWarning("[TutorialWorldAssets_NEW] No built-in alpha blended particle shader " +
                         "found; the atom's core will be additive instead. It will still be " +
                         "see-through, just brighter where it overlaps its own halo.");

        return FindAdditiveParticleShader();
    }

    /// <summary>
    /// The soft glow around the atom. Additive, on the same soft dot the dust uses.
    ///
    /// This is what actually makes a point of light read as a point of light in a scene
    /// with nothing near it. A lit sphere at 200 metres is two pixels of flat colour;
    /// the same sphere inside an additive halo that grows as it closes is something
    /// arriving.
    /// </summary>
    public static Material AtomHaloMaterial()
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(AtomHaloMaterialPath);
        if (existing != null) return existing;

        Shader shader = FindAdditiveParticleShader();
        if (shader == null) return null;

        EnsureFolder();

        Material material = new Material(shader);
        material.name = "TutorialAtomHalo";
        material.mainTexture = SoftDot();

        // Cool, and well under white: additive stacks, and the halo overlaps itself at
        // the centre where the core already is.
        if (material.HasProperty("_TintColor"))
            material.SetColor("_TintColor", new Color(0.38f, 0.62f, 0.92f, 0.5f));

        AssetDatabase.CreateAsset(material, AtomHaloMaterialPath);
        Debug.Log("[TutorialWorldAssets_NEW] Generated " + AtomHaloMaterialPath + ".");

        return material;
    }

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

    /// <summary>
    /// One of the drawn prompt plates, by file name and without the extension.
    ///
    /// These are the only images here that are DRAWN rather than generated, so this is a
    /// load and nothing else: there is no geometry to fall back to, and a missing file
    /// has to say so rather than quietly produce a blank plate. The HUD keeps the words
    /// for anything it cannot find a picture for, so the piece stays playable either way.
    /// </summary>
    public static Sprite HintSprite(string fileName)
    {
        string path = HintFolder + "/" + fileName + ".png";

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

        if (sprite == null)
            Debug.LogWarning("[TutorialWorldAssets_NEW] No prompt art at " + path + ". That " +
                             "prompt stays as words on a plate. Import the PNG as a Sprite " +
                             "(2D and UI) and run Build or Update again.");

        return sprite;
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

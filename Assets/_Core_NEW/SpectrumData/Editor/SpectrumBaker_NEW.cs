using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Unity menu wrapper around SpectrumModel_NEW. EDITOR ONLY — nothing here is in a build.
///
///   Tools > Journey NEW > Spectrum Data > Bake Journey Spectrum
///       Reads SpectrumData/Tables/*.csv, runs the model, writes SpectrumData/Baked/:
///         SpectrumJourney.bytes          the data BakedSpectrumSource_NEW loads
///         SpectrumJourney_preview.png    the whole journey as one image (see README)
///         SpectrumJourney_report.txt     every derived number, calibration check included
///
///   Tools > Journey NEW > Spectrum Data > Check Baked File
///       Loads the .bytes through the runtime reader and prints what it contains.
///
/// All the physics is in SpectrumModel_NEW, which has no UnityEngine dependency; this
/// file only does paths, files, the preview image and the menu.
/// </summary>
public static class SpectrumBaker_NEW
{
    public const string Root = "Assets/_Core_NEW/SpectrumData";
    public const string TablesFolder = Root + "/Tables";
    public const string BakedFolder = Root + "/Baked";
    public const string BytesPath = BakedFolder + "/SpectrumJourney.bytes";
    public const string PreviewPath = BakedFolder + "/SpectrumJourney_preview.png";
    public const string ReportPath = BakedFolder + "/SpectrumJourney_report.txt";
    public const string ArrivalPath = BakedFolder + "/SpectrumJourney_arrival.png";

    [MenuItem("Tools/Journey NEW/Spectrum Data/Bake Journey Spectrum")]
    public static void Bake()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Journey spectrum", "Reading tables…", 0.1f);

            SpectrumModel_NEW.Inputs inputs = SpectrumModel_NEW.LoadInputs(
                name => File.ReadAllText(ToDisk(TablesFolder + "/" + name), Encoding.UTF8));

            EditorUtility.DisplayProgressBar("Journey spectrum", "Following the light from the quasar to Earth…", 0.3f);

            Stopwatch sw = Stopwatch.StartNew();
            SpectrumModel_NEW.Result result = SpectrumModel_NEW.Bake(inputs);
            sw.Stop();

            EditorUtility.DisplayProgressBar("Journey spectrum", "Writing files…", 0.8f);

            Directory.CreateDirectory(ToDisk(BakedFolder));

            byte[] bytes = SpectrumModel_NEW.Write(result);
            File.WriteAllBytes(ToDisk(BytesPath), bytes);
            File.WriteAllBytes(ToDisk(PreviewPath), Preview(result));
            File.WriteAllBytes(ToDisk(ArrivalPath), ArrivalPreview(result));
            File.WriteAllText(ToDisk(ReportPath), Report(result, sw.ElapsedMilliseconds, bytes.Length), Encoding.UTF8);

            AssetDatabase.ImportAsset(BytesPath);
            AssetDatabase.ImportAsset(PreviewPath);
            AssetDatabase.ImportAsset(ArrivalPath);
            AssetDatabase.ImportAsset(ReportPath);

            Debug.Log("[SpectrumBaker_NEW] Baked in " + sw.ElapsedMilliseconds + " ms → " + BytesPath +
                      " (" + (bytes.Length / 1024) + " KB). z_quasar " + result.ZQuasar.ToString("0.000") +
                      ", forest frozen below z " + result.ZFreeze.ToString("0.000") +
                      ". Full numbers in " + ReportPath + ".",
                      AssetDatabase.LoadAssetAtPath<TextAsset>(BytesPath));
        }
        catch (Exception e)
        {
            // A table error names the file and line; show it where it will be read.
            Debug.LogError("[SpectrumBaker_NEW] Bake failed, nothing was written: " + e.Message);
            EditorUtility.DisplayDialog("Bake failed", e.Message, "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    [MenuItem("Tools/Journey NEW/Spectrum Data/Check Baked File")]
    public static void Check()
    {
        TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(BytesPath);
        if (asset == null)
        {
            Debug.LogWarning("[SpectrumBaker_NEW] No baked file at " + BytesPath + ". Bake first.");
            return;
        }

        string error;
        BakedSpectrumData_NEW d = BakedSpectrumData_NEW.Load(asset.bytes, out error);
        if (d == null)
        {
            Debug.LogError("[SpectrumBaker_NEW] " + BytesPath + " does not load: " + error);
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("[SpectrumBaker_NEW] " + BytesPath + " loads cleanly.");
        sb.AppendLine("  grid " + d.Columns + " × " + d.Rows + ", axis " + d.LambdaMin.ToString("0") + "–" + d.LambdaMax.ToString("0") + " Å (log)");
        sb.AppendLine("  z_quasar " + d.ZQuasar.ToString("0.000") + " at " + d.LookbackStartGyr.ToString("0.00") + " Gyr lookback; freeze below z " + d.ZFreeze.ToString("0.000"));
        sb.AppendLine("  birth tick (1216 Å) at " + d.BirthT.ToString("0.000") + " of the bar");
        sb.AppendLine("  first line: z " + (d.AbsorberZ.Length > 0 ? d.AbsorberZ[0].ToString("0.0000") : "—") +
                      ", at Earth sits at " + (d.AbsorberZ.Length > 0 ? d.LineT(d.AbsorberZ[0], 0f).ToString("0.000") : "—") + " of the bar");
        sb.AppendLine("  notable absorbers: " + d.AbsorberZ.Length);
        Debug.Log(sb.ToString(), asset);
    }

    static string ToDisk(string assetPath)
    {
        // Application.dataPath ends in /Assets; asset paths start with Assets/.
        return Path.Combine(Path.GetDirectoryName(Application.dataPath), assetPath);
    }

    // -- Preview ---------------------------------------------------------------------------

    /// <summary>
    /// The whole journey as one picture. Top row = emission, bottom row = Earth; left to
    /// right = wavelength on the same log axis as the HUD. Brightness = TRANSMISSION — how
    /// much light survived, white = all of it — which is what the HUD draws by default.
    ///
    ///   blue vertical     1216 Å, where every Ly-alpha line is born
    ///   red diagonal      the quasar's own Ly-alpha peak, walking into the infrared
    ///   green verticals   4000 Å and 7000 Å, the edges of human vision
    ///   yellow row        the freeze: no new line can appear below this row
    /// </summary>
    static byte[] Preview(SpectrumModel_NEW.Result r)
    {
        int W = r.Settings.Columns, H = r.Settings.Rows;
        Texture2D tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        Color32[] px = new Color32[W * H];

        double lnMin = Math.Log(r.Settings.LambdaMin), lnW = Math.Log(r.Settings.LambdaMax / r.Settings.LambdaMin);
        int birth = Col(BakedSpectrumData_NEW.LymanAlphaA, lnMin, lnW, W);
        int v4000 = Col(4000.0, lnMin, lnW, W);
        int v7000 = Col(7000.0, lnMin, lnW, W);

        for (int row = 0; row < H; row++)
        {
            int y = H - 1 - row;   // texture origin is bottom-left; row 0 goes at the top
            int peak = (int)Math.Round(r.EmissionPeakT[row] * (W - 1));

            for (int x = 0; x < W; x++)
            {
                byte g = (byte)Math.Round(255.0 * (1.0 - r.Absorption[row * W + x]));   // transmission, as the HUD shows it
                Color32 c = new Color32(g, g, g, 255);

                if (x == v4000 || x == v7000) c.g = (byte)Math.Max((int)g, 90);
                if (x == birth) c = new Color32(40, 120, 255, 255);
                if (Math.Abs(x - peak) <= 1) c = new Color32(255, 40, 40, 255);
                if (row == r.FreezeRow) c = new Color32(255, 220, 0, 255);

                px[y * W + x] = c;
            }
        }

        tex.SetPixels32(px);
        tex.Apply(false);
        byte[] png = tex.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(tex);
        return png;
    }

    /// <summary>
    /// The frame the HUD ends on, drawn like the classic Lyman-alpha forest figure:
    /// intensity against EMITTED wavelength, 1000–1350 Å by default, ticks every 50 Å, a
    /// dotted line at 1216 Å. Compare this image with the reference figure after a bake.
    /// </summary>
    static byte[] ArrivalPreview(SpectrumModel_NEW.Result r)
    {
        const int W = 900, H = 300;
        Texture2D tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        Color32[] px = new Color32[W * H];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);

        System.Action<int, int, byte> set = (x, row, g) =>
        {
            if (x < 0 || x >= W || row < 0 || row >= H) return;
            px[(H - 1 - row) * W + x] = new Color32(g, g, g, 255);   // row 0 at the top
        };

        double zq = r.ZQuasar;
        double e0 = r.Settings.FinalEmittedMinA, e1 = r.Settings.FinalEmittedMaxA;
        int fc = r.FinalFlux.Length;
        double lnMin = Math.Log(r.FinalLambdaMin), lnW = Math.Log(r.FinalLambdaMax / r.FinalLambdaMin);

        for (int x = 0; x < W; x++) { set(x, H - 1, 0); set(x, 0, 170); }
        for (int y = 0; y < H; y++) { set(0, y, 0); set(W - 1, y, 170); }
        for (double t = Math.Ceiling(e0 / 50.0) * 50.0; t <= e1; t += 50.0)
        {
            int x = (int)Math.Round((t - e0) / (e1 - e0) * (W - 1));
            for (int y = H - 9; y < H; y++) set(x, y, 0);
        }
        int x1216 = (int)Math.Round((BakedSpectrumData_NEW.LymanAlphaA - e0) / (e1 - e0) * (W - 1));
        for (int y = 0; y < H; y += 4) set(x1216, y, 150);

        int prev = -1;
        for (int x = 0; x < W; x++)
        {
            double em = e0 + (e1 - e0) * x / (W - 1);
            double f = (Math.Log(em * (1.0 + zq)) - lnMin) / lnW * (fc - 1);
            int lo = Math.Max(0, Math.Min(fc - 1, (int)f));
            int hi = Math.Min(fc - 1, lo + 1);
            double v = r.FinalFlux[lo] + (r.FinalFlux[hi] - r.FinalFlux[lo]) * (f - lo);

            int y = H - 3 - (int)Math.Round(Math.Min(1.0, v / 1.05) * (H - 8));
            int a = prev < 0 ? y : Math.Min(prev, y), b = prev < 0 ? y : Math.Max(prev, y);
            for (int yy = a; yy <= b; yy++) set(x, yy, 0);
            prev = y;
        }

        tex.SetPixels32(px);
        tex.Apply(false);
        byte[] png = tex.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(tex);
        return png;
    }

    static int Col(double lambda, double lnMin, double lnW, int W)
    {
        return (int)Math.Round((Math.Log(lambda) - lnMin) / lnW * (W - 1));
    }

    static string Report(SpectrumModel_NEW.Result r, long ms, int bytes)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("JOURNEY SPECTRUM — BAKE REPORT");
        sb.AppendLine("Baked " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " in " + ms + " ms, " + bytes + " bytes.");
        sb.AppendLine("Inputs: " + TablesFolder + "/*.csv. Sources and confidence: " + Root + "/SOURCES.md.");
        sb.AppendLine();
        foreach (string line in r.Report) sb.AppendLine(line);
        return sb.ToString();
    }
}

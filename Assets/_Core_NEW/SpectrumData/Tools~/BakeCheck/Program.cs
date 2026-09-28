using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;

// Command-line check of the journey spectrum bake. Runs the SAME model code the Unity
// baker runs (the .csproj links the files from ../../Editor and ../../Runtime), then
// round-trips the result through the runtime reader and checks it.
//
//   cd Assets/_Core_NEW/SpectrumData/Tools~/BakeCheck
//   dotnet run -c Release
//
// Writes out/SpectrumJourney.bytes, out/preview.png and out/report.txt. Exit code 0 =
// every check passed. Compiled as C# 7.3 on purpose — Unity 2019.4's language version —
// so anything that builds here also builds in Unity.
//
// This folder ends in '~', so Unity never imports it and it can never reach a build.
static class Program
{
    static int failures;

    static int Main(string[] args)
    {
        string tables = args.Length > 0 ? args[0] : Path.Combine("..", "..", "Tables");
        string outDir = args.Length > 1 ? args[1] : "out";
        Directory.CreateDirectory(outDir);

        var inputs = SpectrumModel_NEW.LoadInputs(name => File.ReadAllText(Path.Combine(tables, name)));

        var sw = Stopwatch.StartNew();
        var res = SpectrumModel_NEW.Bake(inputs);
        sw.Stop();

        foreach (var line in res.Report) Console.WriteLine(line);
        Console.WriteLine("BAKE TIME " + sw.ElapsedMilliseconds + " ms");
        File.WriteAllLines(Path.Combine(outDir, "report.txt"), res.Report);

        byte[] bytes = SpectrumModel_NEW.Write(res);
        File.WriteAllBytes(Path.Combine(outDir, "SpectrumJourney.bytes"), bytes);
        Console.WriteLine("FILE SIZE " + bytes.Length + " bytes");

        // ---- Round trip through the runtime reader --------------------------------------
        sw.Restart();
        string err;
        var d = BakedSpectrumData_NEW.Load(bytes, out err);
        sw.Stop();
        Check(d != null, "load: " + err);
        if (d == null) return 1;
        Console.WriteLine("LOAD TIME " + sw.Elapsed.TotalMilliseconds.ToString("0.00") + " ms");

        var s = inputs.Settings;
        Check(d.Columns == s.Columns && d.Rows == s.Rows, "header dims");
        Check(Math.Abs(d.ZQuasar - res.ZQuasar) < 1e-5, "zQ round trip");

        var flux = new float[d.Columns];
        var abs = new float[d.Columns];
        foreach (int r in new[] { 0, d.Rows / 2, d.Rows - 1 })
        {
            d.SampleRow(r, flux, abs);
            double maxErr = 0;
            for (int c = 0; c < d.Columns; c++)
            {
                maxErr = Math.Max(maxErr, Math.Abs(flux[c] - res.Flux[r * d.Columns + c]));
                maxErr = Math.Max(maxErr, Math.Abs(abs[c] - res.Absorption[r * d.Columns + c]));
            }
            Check(maxErr < 2.0 / 65535, "row " + r + " round trip, max err " + maxErr);
        }

        // ---- Journey position mapping ----------------------------------------------------
        Check(Math.Abs(d.ZFromLookback((float)s.LookbackStartGyr) - d.ZQuasar) < 1e-3, "z(lookback start) = zQ, got " + d.ZFromLookback((float)s.LookbackStartGyr));
        Check(Math.Abs(d.ZFromLookback(0f)) < 1e-6, "z(0) = 0");
        Check(Math.Abs(d.RowFromZ(d.ZQuasar)) < 1e-3, "row(zQ) = 0");
        Check(Math.Abs(d.RowFromZ(0f) - (d.Rows - 1)) < 1e-3, "row(0) = last");
        float prevZ = -1;
        for (float lb = 0; lb <= (float)s.LookbackStartGyr; lb += 0.01f)
        {
            float z = d.ZFromLookback(lb);
            Check(z >= prevZ, "z monotonic at " + lb);
            prevZ = z;
        }

        // ---- Axis --------------------------------------------------------------------------
        Check(Math.Abs(d.LineT(d.ZQuasar, d.ZQuasar) - d.BirthT) < 1e-5, "line cut at zQ sits on the birth tick at emission");
        Check(Math.Abs(d.LineT(d.ZQuasar, 0f) - d.ToT(1215.67 * (1 + d.ZQuasar))) < 1e-5, "first line at Earth = 1216(1+zQ)");
        Console.WriteLine("BIRTH T " + d.BirthT.ToString("0.000") + "   first line at Earth T " + d.LineT(d.AbsorberZ[0], 0f).ToString("0.000"));

        for (int i = 1; i < d.AbsorberZ.Length; i++) Check(d.AbsorberZ[i] < d.AbsorberZ[i - 1], "absorbers descending");
        Check(d.AbsorberZ.Length > 0 && d.AbsorberZ[0] <= d.ZQuasar && d.AbsorberZ[0] > d.ZFreeze, "first absorber between freeze and quasar");

        // ---- Arrival frame ---------------------------------------------------------------------
        Check(Math.Abs(d.FinalWindowMin - 1000 * (1 + d.ZQuasar)) < 1f && Math.Abs(d.FinalWindowMax - 1350 * (1 + d.ZQuasar)) < 1f, "reference window = emitted 1000-1350 A");
        Check(d.FinalLambdaMin <= d.LambdaMin + 0.01f && d.FinalLambdaMax >= d.FinalWindowMax - 0.01f, "arrival frame covers the journey bar and the reference window");
        double peakLam = 1215.67 * (1 + d.ZQuasar), maxFlux = 0;
        for (double lam = peakLam * 0.99; lam <= peakLam * 1.01; lam += 0.5) maxFlux = Math.Max(maxFlux, d.SampleFinal(lam, BakedSpectrumData_NEW.FinalChannel.RealSpectrum));
        Check(maxFlux > 0.7, "arrival flux peaks near 1 at the Ly-alpha emission line, got " + maxFlux.ToString("0.00"));
        Check(Math.Abs(d.SampleFinal(d.FinalWindowMax * 0.999, BakedSpectrumData_NEW.FinalChannel.Transmission) - 1f) < 0.01f, "no Ly-alpha absorption redward of the peak at arrival");
        int fc = res.FinalFlux.Length; double fmax = 0;
        for (int c = 0; c < fc; c++)
        {
            double lam = Math.Exp(Math.Log(res.FinalLambdaMin) + c * Math.Log(res.FinalLambdaMax / res.FinalLambdaMin) / (fc - 1));
            fmax = Math.Max(fmax, Math.Abs(d.SampleFinal(lam, BakedSpectrumData_NEW.FinalChannel.RealSpectrum) - res.FinalFlux[c]));
        }
        Check(fmax < 2.0 / 65535 + 1e-4, "arrival frame round trip, max err " + fmax);
        // The last journey row and the arrival frame are the same light — they must agree.
        d.SampleRow(d.Rows - 1, null, abs);
        double agree = 0; int na = 0;
        for (int c = 0; c < d.Columns; c += 16) { agree += Math.Abs((1 - abs[c]) - d.SampleFinal(d.ToLambda(c / (float)(d.Columns - 1)), BakedSpectrumData_NEW.FinalChannel.Transmission)); na++; }
        Check(agree / na < 0.05, "last journey row agrees with the arrival frame (mean diff " + (agree / na).ToString("0.000") + ")");
        // Same for the displayed spectrum: the zoom starts on exactly what the journey showed.
        d.SampleRow(d.Rows - 1, flux, null);
        double agreeF = 0; int nf = 0;
        for (int c = 0; c < d.Columns; c += 16) { agreeF += Math.Abs(flux[c] - d.SampleFinal(d.ToLambda(c / (float)(d.Columns - 1)), BakedSpectrumData_NEW.FinalChannel.DisplayedSpectrum)); nf++; }
        Check(agreeF / nf < 0.03, "last journey spectrum agrees with the arrival frame (mean diff " + (agreeF / nf).ToString("0.000") + ")");
        // At emission the displayed spectrum must be the quasar's own: Ly-alpha peak at the left edge, shape to its right.
        d.SampleRow(0, flux, null);
        float first = flux[0], later = flux[d.Columns / 2];
        Check(first > 0.7f && later > 0.02f && later < first, "at emission: peak at left edge (" + first.ToString("0.00") + "), continuum to its right (" + later.ToString("0.00") + ")");

        // ---- Per-frame cost ------------------------------------------------------------------
        sw.Restart();
        const int N = 20000;
        for (int i = 0; i < N; i++)
        {
            float z = d.ZFromLookback((float)(s.LookbackStartGyr * (i % 997) / 997.0));
            d.SampleRow(d.RowFromZ(z), flux, abs);
        }
        sw.Stop();
        Console.WriteLine("PER-FRAME COST (lookup + full row blend) " + (sw.Elapsed.TotalMilliseconds * 1000 / N).ToString("0.0") + " us");

        // The zoom samples the arrival frame once per HUD column per frame, for a few seconds.
        sw.Restart();
        const int NZ = 2000;
        for (int i = 0; i < NZ; i++)
            for (int c = 0; c < d.Columns; c++) flux[c] = d.SampleFinal(d.FinalLambdaMin * Math.Exp(c * 0.001 + i * 1e-5), (BakedSpectrumData_NEW.FinalChannel)(c % 3));
        sw.Stop();
        Console.WriteLine("PER-FRAME COST (arrival zoom, " + d.Columns + " samples) " + (sw.Elapsed.TotalMilliseconds * 1000 / NZ).ToString("0.0") + " us");

        // ---- Corrupt-file handling -----------------------------------------------------------
        Check(BakedSpectrumData_NEW.Load(new byte[10], out err) == null, "tiny file rejected");
        var trunc = new byte[bytes.Length - 100]; Array.Copy(bytes, trunc, trunc.Length);
        Check(BakedSpectrumData_NEW.Load(trunc, out err) == null && err.Contains("truncated"), "truncated rejected: " + err);
        var badVer = (byte[])bytes.Clone(); badVer[4] = 99;
        Check(BakedSpectrumData_NEW.Load(badVer, out err) == null && err.Contains("version"), "bad version rejected: " + err);

        // ---- Preview image ---------------------------------------------------------------------
        WritePreview(Path.Combine(outDir, "preview.png"), res, d);
        WriteArrival(Path.Combine(outDir, "arrival.png"), d);

        Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : failures + " CHECK(S) FAILED");
        return failures == 0 ? 0 : 1;
    }

    static void Check(bool ok, string what)
    {
        if (ok) return;
        failures++;
        Console.WriteLine("FAIL: " + what);
    }

    // Rows top-to-bottom = emission to Earth. Grey = transmission (white = nothing absorbed). Blue = birth tick (1216 A).
    // Red = the quasar's own Ly-alpha peak. Yellow row = freeze. Green = visible band edges.
    static void WritePreview(string path, SpectrumModel_NEW.Result res, BakedSpectrumData_NEW d)
    {
        int W = d.Columns, H = d.Rows;
        var rgb = new byte[H * (1 + W * 3)];
        int birth = (int)Math.Round(d.BirthT * (W - 1));
        int v4000 = (int)Math.Round(d.ToT(4000) * (W - 1));
        int v7000 = (int)Math.Round(d.ToT(7000) * (W - 1));

        for (int y = 0; y < H; y++)
        {
            int o = y * (1 + W * 3);
            rgb[o] = 0;
            int peak = (int)Math.Round(res.EmissionPeakT[y] * (W - 1));
            for (int x = 0; x < W; x++)
            {
                byte g = (byte)Math.Round(255 * (1.0 - res.Absorption[y * W + x]));
                byte r = g, gg = g, b = g;
                if (x == v4000 || x == v7000) { gg = (byte)Math.Max((int)g, 90); }
                if (x == birth) { r = 40; gg = 120; b = 255; }
                if (Math.Abs(x - peak) <= 1) { r = 255; gg = 40; b = 40; }
                if (y == res.FreezeRow) { r = 255; gg = 220; b = 0; }
                rgb[o + 1 + x * 3] = r; rgb[o + 2 + x * 3] = gg; rgb[o + 3 + x * 3] = b;
            }
        }

        using (var fs = File.Create(path))
        {
            fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
            var ihdr = new byte[13];
            BE(ihdr, 0, W); BE(ihdr, 4, H); ihdr[8] = 8; ihdr[9] = 2;
            Chunk(fs, "IHDR", ihdr);
            using (var ms = new MemoryStream())
            {
                using (var z = new ZLibStream(ms, CompressionLevel.Optimal, true)) z.Write(rgb, 0, rgb.Length);
                Chunk(fs, "IDAT", ms.ToArray());
            }
            Chunk(fs, "IEND", new byte[0]);
        }
    }

    // The arrival frame as the HUD ends on it, drawn like the classic reference figure:
    // intensity vs emitted wavelength 1000-1350 A, ticks every 50 A, dotted line at 1216 A.
    static void WriteArrival(string path, BakedSpectrumData_NEW d)
    {
        const int W = 900, H = 300;
        var rgb = new byte[H * (1 + W * 3)];
        for (int i = 0; i < rgb.Length; i++) rgb[i] = 255;
        for (int y = 0; y < H; y++) rgb[y * (1 + W * 3)] = 0;
        Action<int, int, byte> px = (x, y, g) => { if (x < 0 || x >= W || y < 0 || y >= H) return; int o = y * (1 + W * 3) + 1 + x * 3; rgb[o] = rgb[o + 1] = rgb[o + 2] = g; };

        double zq = d.ZQuasar;
        for (int x = 0; x < W; x++) { px(x, H - 1, 0); px(x, 0, 170); }
        for (int y = 0; y < H; y++) { px(0, y, 0); px(W - 1, y, 170); }
        for (int t = 1000; t <= 1350; t += 50) { int x = (int)Math.Round((t - 1000) / 350.0 * (W - 1)); for (int y = H - 9; y < H; y++) px(x, y, 0); }
        int x1216 = (int)Math.Round((1215.67 - 1000) / 350.0 * (W - 1));
        for (int y = 0; y < H; y += 4) px(x1216, y, 150);

        int prev = -1;
        for (int x = 0; x < W; x++)
        {
            double em = 1000 + 350.0 * x / (W - 1);
            float v = d.SampleFinal(em * (1 + zq), BakedSpectrumData_NEW.FinalChannel.RealSpectrum);
            int y = H - 3 - (int)Math.Round(Math.Min(1f, v / 1.05f) * (H - 8));
            int a = prev < 0 ? y : Math.Min(prev, y), b = prev < 0 ? y : Math.Max(prev, y);
            for (int yy = a; yy <= b; yy++) px(x, yy, 0);
            prev = y;
        }

        using (var fs = File.Create(path))
        {
            fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
            var ihdr = new byte[13]; BE(ihdr, 0, W); BE(ihdr, 4, H); ihdr[8] = 8; ihdr[9] = 2; Chunk(fs, "IHDR", ihdr);
            using (var ms = new MemoryStream()) { using (var z = new ZLibStream(ms, CompressionLevel.Optimal, true)) z.Write(rgb, 0, rgb.Length); Chunk(fs, "IDAT", ms.ToArray()); }
            Chunk(fs, "IEND", new byte[0]);
        }
    }

    static void BE(byte[] b, int o, int v) { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }

    static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4]; BE(len, 0, data.Length); s.Write(len, 0, 4);
        var t = System.Text.Encoding.ASCII.GetBytes(type); s.Write(t, 0, 4); s.Write(data, 0, data.Length);
        uint crc = Crc(Crc(0xFFFFFFFF, t), data) ^ 0xFFFFFFFF;
        var c = new byte[4]; BE(c, 0, (int)crc); s.Write(c, 0, 4);
    }

    static uint[] table;
    static uint Crc(uint crc, byte[] data)
    {
        if (table == null)
        {
            table = new uint[256];
            for (uint n = 0; n < 256; n++) { uint c = n; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1; table[n] = c; }
        }
        foreach (var b in data) crc = table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }
}

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The ending's one science beat: the forest is a map of the dark matter along the light's path.
///
/// Once EarthEnding_NEW has grown the spectrum to the middle of the screen, this lifts it up and
/// draws the light's line of sight beneath it, on the same horizontal scale: the quasar under its
/// own Lyman-alpha peak, the way toward Earth off to the left. Then, one by one, starting with the
/// one nearest the quasar (the first the light met), each line drops a thread down to the path,
/// and a small dim cloud of gas appears where it lands. Last, a faint haze fades in around the
/// gas, densest where the lines crowd: the dark matter the gas gathers along.
///
/// THE LINES ARE THE BAKE'S. They are the deepest dips in the arrival frame's Lyman-alpha
/// transmission (BakedSpectrumData_NEW.SampleFinal), found on the bar as it is drawn now (the
/// arrival zoom included), so each thread starts on a dip the visitor can see. A line at
/// observed wavelength λ was cut by gas at z = λ / 1216 Å - 1, and on this log axis the path is
/// drawn so that that gas sits straight below its line. The haze is the same transmission,
/// smoothed: where more light was taken, more gas, more dark matter. Its strands are drawn, not
/// measured; only where it is dense comes from the data.
///
/// THE VOICE. Says its own line (J6_4) as it starts; EarthEnding_NEW says Closing once this is
/// Finished.
///
/// Drawn on the HUD's own canvas, between the ending's backdrop and the spectrum, so on the wall
/// it is the wall's own like the bar. Put it beside EarthEnding_NEW; the ending starts it.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("DM MAP", "#7A8CC8")]
public class DarkMatterMap_NEW : MonoBehaviour
{
    [Tooltip("Off: the ending goes straight from the spectrum to Closing, as before.")]
    [SerializeField] bool play = true;

    [Header("Voice")]
    [Tooltip("Said as the map starts (J6_4).")]
    [SerializeField] AudioClip line;

    [Header("Layout (fractions of the screen)")]
    [Tooltip("Where the spectrum moves to, to make room for the path: x min, y min, x max, y max.")]
    [SerializeField] Vector4 spectrumRect = new Vector4(0.06f, 0.5f, 0.94f, 0.86f);

    [Tooltip("Height of the path across the screen.")]
    [Range(0f, 1f)] [SerializeField] float pathY = 0.28f;

    [Tooltip("How far the dark matter reaches above and below the path.")]
    [Range(0.02f, 0.4f)] [SerializeField] float hazeHalfHeight = 0.15f;

    [Header("Lines")]
    [Tooltip("Most lines that drop to the path. The deepest are taken.")]
    [Range(4, 120)] [SerializeField] int maxLines = 36;

    [Tooltip("Least depth (1 - transmission) for a dip to count as a line.")]
    [Range(0f, 1f)] [SerializeField] float minDepth = 0.4f;

    [Tooltip("Closest two lines may be, as a fraction of the bar.")]
    [Range(0f, 0.05f)] [SerializeField] float minSeparation = 0.008f;

    [Header("Look")]
    [SerializeField] Color threadColour = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] Color pathColour = new Color(1f, 1f, 1f, 0.55f);
    [Tooltip("Dim, fuzzy gas: alpha blended, never glowing.")]
    [SerializeField] Color gasColour = new Color(0.76f, 0.38f, 0.64f, 0.8f);
    [SerializeField] Color darkMatterColour = new Color(0.56f, 0.64f, 0.82f, 0.6f);
    [SerializeField] Color quasarColour = new Color(1f, 0.96f, 0.82f, 1f);
    [SerializeField] Color labelColour = new Color(1f, 1f, 1f, 0.7f);
    [Tooltip("Thread width, as a fraction of the screen's height.")]
    [SerializeField] float threadWidth = 0.0022f;
    [Tooltip("Gas cloud size for the shallowest and deepest line, fractions of the screen's height.")]
    [SerializeField] Vector2 gasSize = new Vector2(0.014f, 0.034f);
    [Tooltip("Label size, as a fraction of the screen's height.")]
    [SerializeField] float labelSize = 0.024f;
    [SerializeField] string quasarLabel = "Quasar";
    [SerializeField] string earthLabel = "Earth";
    [SerializeField] string darkMatterLabel = "Dark matter";

    [Header("Timing (seconds from the start)")]
    [SerializeField] float moveSeconds = 1.5f;
    [SerializeField] float pathAt = 0.6f;
    [SerializeField] float lineAt = 1f;
    [SerializeField] float dropAt = 1.8f;
    [Tooltip("Seconds from the first thread starting to the last.")]
    [SerializeField] float dropSpread = 3.5f;
    [SerializeField] float dropSeconds = 0.5f;
    [Tooltip("Time it to \"Gas gathers where dark matter is\".")]
    [SerializeField] float hazeAt = 6.5f;
    [SerializeField] float hazeSeconds = 3f;
    [Tooltip("Longest wait for the line to finish before the map counts as done anyway.")]
    [SerializeField] float maxVoiceWaitSeconds = 30f;

    [Header("Wiring (found if empty)")]
    [SerializeField] BakedSpectrumSource_NEW source;
    [SerializeField] JourneyVoiceOver_NEW voiceOver;

    [SerializeField] bool debugLog = false;

    // ── State ───────────────────────────────────────────────────────────────

    struct Line { public float t, depth, startAt; public Image thread, gas; }

    readonly List<Line> _lines = new List<Line>();
    RectTransform _panel, _root, _graph, _overlay;
    Vector2 _panelMinFrom, _panelMaxFrom;
    Image _path, _quasar;
    RawImage _haze;
    Texture2D _hazeTex;
    TextMeshProUGUI _quasarText, _earthText, _dmText;
    float _clock = -1f, _quasarT;
    bool _said, _finished;

    static Sprite _soft;
    readonly Vector3[] _corners = new Vector3[4];

    /// <summary>True when the ending should hand over to this: switched on and there is baked data to draw.</summary>
    public bool WillPlay
    {
        get
        {
            if (!play || !isActiveAndEnabled) return false;
            if (source == null) source = FindObjectOfType<BakedSpectrumSource_NEW>();
            return source != null && source.IsActive && source.Data != null;
        }
    }

    /// <summary>True once everything is drawn and the line has been said. Stays true.</summary>
    public bool Finished { get { return _finished; } }

    // ── Start ───────────────────────────────────────────────────────────────

    /// <summary>Called by EarthEnding_NEW once the spectrum has grown. panel is the spectrum's panel on root.</summary>
    public void Play(RectTransform panel, RectTransform root, RectTransform graph)
    {
        if (_clock >= 0f) return;
        if (voiceOver == null) voiceOver = FindObjectOfType<JourneyVoiceOver_NEW>();
        _panel = panel; _root = root; _graph = graph;
        _panelMinFrom = panel.anchorMin; _panelMaxFrom = panel.anchorMax;

        if (!WillPlay || root == null || graph == null) { _finished = true; return; }

        Build();
        _clock = 0f;
        if (debugLog) Debug.Log("[DarkMatterMap_NEW] " + _lines.Count + " lines drop to the path.", this);
    }

    void Build()
    {
        BakedSpectrumData_NEW data = source.Data;
        double lyA = BakedSpectrumData_NEW.LymanAlphaA;
        float zNow = source.DisplayZ;

        // Where the quasar is: under its own Ly-alpha peak, as observed now.
        _quasarT = Mathf.Clamp01(source.BarT(lyA * (1.0 + data.ZQuasar) / (1.0 + zNow)));

        // The bar's transmission, one sample per column, from its left edge to the quasar.
        const int N = 1024;
        var trans = new float[N];
        for (int i = 0; i < N; i++)
        {
            float t = i / (float)(N - 1);
            double lambda = source.BarLambda(t) * (1.0 + zNow);   // as observed at Earth
            double z = lambda / lyA - 1.0;
            trans[i] = (t > _quasarT || z <= 0.0) ? 1f : Mathf.Clamp01(data.SampleFinal(lambda, BakedSpectrumData_NEW.FinalChannel.Transmission));
        }

        // The lines: local minima deep enough, deepest first, kept apart.
        var found = new List<KeyValuePair<float, int>>();
        for (int i = 2; i < N - 2; i++)
        {
            float v = trans[i];
            if (1f - v < minDepth) continue;
            if (v <= trans[i - 1] && v <= trans[i + 1] && v <= trans[i - 2] && v < trans[i + 2])
                found.Add(new KeyValuePair<float, int>(1f - v, i));
        }
        found.Sort((a, b) => b.Key.CompareTo(a.Key));
        var picked = new List<KeyValuePair<float, int>>();
        foreach (var f in found)
        {
            if (picked.Count >= maxLines) break;
            float t = f.Value / (float)(N - 1);
            bool near = false;
            foreach (var p in picked) if (Mathf.Abs(p.Value / (float)(N - 1) - t) < minSeparation) { near = true; break; }
            if (!near) picked.Add(f);
        }
        // Nearest the quasar first: the order the light met them.
        picked.Sort((a, b) => b.Value.CompareTo(a.Value));

        _overlay = new GameObject("Dark matter map", typeof(RectTransform)).GetComponent<RectTransform>();
        _overlay.gameObject.layer = _panel.gameObject.layer;
        _overlay.SetParent(_root, false);
        _overlay.anchorMin = Vector2.zero; _overlay.anchorMax = Vector2.one;
        _overlay.offsetMin = _overlay.offsetMax = Vector2.zero;
        _overlay.SetSiblingIndex(_panel.GetSiblingIndex());   // behind the bar, above the backdrop

        float h = _root.rect.height;

        // Dark matter haze, first so it is behind everything.
        _hazeTex = BuildHaze(trans, N);
        _haze = new GameObject("Dark matter", typeof(RectTransform)).AddComponent<RawImage>();
        Place(_haze.rectTransform);
        _haze.texture = _hazeTex;
        _haze.color = new Color(darkMatterColour.r, darkMatterColour.g, darkMatterColour.b, 0f);
        _haze.raycastTarget = false;

        _path = MakeImage("Path", null, pathColour);
        _quasar = MakeImage("Quasar", Soft, quasarColour);
        _quasar.rectTransform.sizeDelta = Vector2.one * gasSize.y * 1.3f * h;

        for (int k = 0; k < picked.Count; k++)
        {
            float depth = Mathf.InverseLerp(minDepth, 1f, picked[k].Key);
            var l = new Line
            {
                t = picked[k].Value / (float)(N - 1),
                depth = depth,
                startAt = dropAt + (picked.Count > 1 ? dropSpread * k / (picked.Count - 1) : 0f),
                thread = MakeImage("Thread", null, threadColour),
                gas = MakeImage("Gas", Soft, gasColour),
            };
            l.gas.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(gasSize.x, gasSize.y, depth) * h;
            _lines.Add(l);
        }

        _quasarText = MakeText("Quasar label", quasarLabel, TextAlignmentOptions.Top, h);
        _earthText = MakeText("Earth label", earthLabel, TextAlignmentOptions.TopLeft, h);
        _dmText = MakeText("Dark matter label", darkMatterLabel, TextAlignmentOptions.Bottom, h);
        LayOut();
    }

    // ── Each frame ──────────────────────────────────────────────────────────

    void Update()
    {
        if (_clock < 0f || _finished) return;
        _clock += Time.deltaTime;

        float m = Mathf.SmoothStep(0f, 1f, _clock / moveSeconds);
        _panel.anchorMin = Vector2.Lerp(_panelMinFrom, new Vector2(spectrumRect.x, spectrumRect.y), m);
        _panel.anchorMax = Vector2.Lerp(_panelMaxFrom, new Vector2(spectrumRect.z, spectrumRect.w), m);
        LayOut();

        if (!_said && _clock >= lineAt)
        {
            _said = true;
            if (voiceOver != null && line != null) voiceOver.Say(line);
            else if (line == null) Debug.LogWarning("[DarkMatterMap_NEW] No line (J6_4) assigned; nothing is said.", this);
        }

        float done = hazeAt + hazeSeconds;
        if (_clock < done) return;
        if (voiceOver != null && line != null && voiceOver.IsSaying(line) && _clock < done + maxVoiceWaitSeconds) return;

        _finished = true;
        if (debugLog) Debug.Log("[DarkMatterMap_NEW] Drawn and said.", this);
    }

    void LayOut()
    {
        // The bar's rect on the screen, as fractions.
        _graph.GetWorldCorners(_corners);
        Rect r = _root.rect;
        Vector2 a = _root.InverseTransformPoint(_corners[0]), b = _root.InverseTransformPoint(_corners[2]);
        float x0 = (a.x - r.xMin) / r.width, x1 = (b.x - r.xMin) / r.width;
        float barBottom = (a.y - r.yMin) / r.height;
        float h = r.height;

        float pathK = Fade(pathAt, 1f);
        float xq = Mathf.Lerp(x0, x1, _quasarT);

        SetSpan(_path.rectTransform, new Vector2(x0, pathY), new Vector2(xq, pathY), 0f, threadWidth * h);
        _path.color = WithAlpha(pathColour, pathK);
        SetPoint(_quasar.rectTransform, new Vector2(xq, pathY));
        _quasar.color = WithAlpha(quasarColour, pathK);

        SetPoint(_quasarText.rectTransform, new Vector2(xq, pathY - 0.03f));
        _quasarText.color = WithAlpha(labelColour, pathK);
        SetPoint(_earthText.rectTransform, new Vector2(x0, pathY - 0.03f));
        _earthText.color = WithAlpha(labelColour, pathK);

        float hazeK = Fade(hazeAt, hazeSeconds);
        _haze.rectTransform.anchorMin = new Vector2(x0, pathY - hazeHalfHeight);
        _haze.rectTransform.anchorMax = new Vector2(Mathf.Lerp(x0, x1, _hazeWidthT), pathY + hazeHalfHeight);
        _haze.rectTransform.offsetMin = _haze.rectTransform.offsetMax = Vector2.zero;
        _haze.color = WithAlpha(darkMatterColour, hazeK);
        SetPoint(_dmText.rectTransform, new Vector2(Mathf.Lerp(x0, x1, _densestT), pathY + hazeHalfHeight + 0.005f));
        _dmText.color = WithAlpha(labelColour, hazeK);

        foreach (Line l in _lines)
        {
            float x = Mathf.Lerp(x0, x1, l.t);
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_clock - l.startAt) / dropSeconds));
            float yEnd = Mathf.Lerp(barBottom, pathY, k);
            SetSpan(l.thread.rectTransform, new Vector2(x, yEnd), new Vector2(x, barBottom), threadWidth * h, 0f);
            l.thread.color = WithAlpha(threadColour, k > 0f ? 1f : 0f);
            SetPoint(l.gas.rectTransform, new Vector2(x, pathY));
            float g = Mathf.Clamp01((_clock - l.startAt - dropSeconds * 0.8f) / 0.4f);
            l.gas.color = WithAlpha(gasColour, g);
            l.gas.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, g);
        }
    }

    float _densestT = 0.5f, _hazeWidthT = 1f;   // bar fractions: the haze's densest point, and its right edge

    // ── The haze ────────────────────────────────────────────────────────────

    /// <summary>
    /// The dark matter: the transmission's absorption, smoothed, as a band along the path, with a
    /// tilted strand through each dense knot and faint arcs between neighbours. Fixed seed, so
    /// every run and every wall copy draws the same web.
    /// </summary>
    Texture2D BuildHaze(float[] trans, int n)
    {
        float wFrac = Mathf.Min(1f, Mathf.Max(0.05f, _quasarT + 0.03f));
        _hazeWidthT = wFrac;
        Rect r = _root.rect;
        _graph.GetWorldCorners(_corners);
        float barW = (_root.InverseTransformPoint(_corners[2]).x - _root.InverseTransformPoint(_corners[0]).x);
        float wc = barW * wFrac, hc = 2f * hazeHalfHeight * r.height;
        int W = 512, H = Mathf.Clamp(Mathf.RoundToInt(W * hc / Mathf.Max(1f, wc)), 32, 256);

        // Smoothed absorption, over the haze's width.
        var dens = new float[W];
        float sigma = W * 0.02f, max = 1e-4f;
        var raw = new float[W];
        for (int x = 0; x < W; x++)
        {
            float t = x / (float)(W - 1) * wFrac;
            int i = Mathf.Clamp(Mathf.RoundToInt(t * (n - 1)), 0, n - 1);
            raw[x] = 1f - trans[i];
        }
        int rad = Mathf.CeilToInt(sigma * 3f);
        for (int x = 0; x < W; x++)
        {
            float s = 0f, ws = 0f;
            for (int d = -rad; d <= rad; d++)
            {
                int j = x + d; if (j < 0 || j >= W) continue;
                float wgt = Mathf.Exp(-0.5f * d * d / (sigma * sigma));
                s += raw[j] * wgt; ws += wgt;
            }
            dens[x] = s / ws;
            if (dens[x] > max) { max = dens[x]; _densestT = x / (float)(W - 1) * wFrac; }
        }
        for (int x = 0; x < W; x++) dens[x] /= max;

        // Knots: local peaks of the density, kept apart.
        var knots = new List<Vector2>();   // x, strength
        for (int x = 2; x < W - 2; x++)
            if (dens[x] > 0.35f && dens[x] >= dens[x - 1] && dens[x] >= dens[x + 1] && dens[x] >= dens[x - 2] && dens[x] > dens[x + 2])
            {
                if (knots.Count > 0 && x - knots[knots.Count - 1].x < W * 0.05f)
                {
                    if (dens[x] > knots[knots.Count - 1].y) knots[knots.Count - 1] = new Vector2(x, dens[x]);
                    continue;
                }
                knots.Add(new Vector2(x, dens[x]));
            }

        var rng = new System.Random(1216);
        var img = new float[W * H];
        float cy = (H - 1) * 0.5f;

        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float dy = (y - cy) / (H * 0.2f);
                img[y * W + x] = 0.55f * dens[x] * Mathf.Exp(-0.5f * dy * dy);
            }

        float strandW = W * 0.018f;
        Vector2 prevEnd = Vector2.zero; bool hasPrev = false;
        foreach (Vector2 kn in knots)
        {
            float ang = ((float)rng.NextDouble() * 2f - 1f) * 40f * Mathf.Deg2Rad;
            float len = (0.4f + 0.6f * kn.y) * H * 0.5f;
            Vector2 dir = new Vector2(Mathf.Sin(ang), Mathf.Cos(ang));
            Vector2 c = new Vector2(kn.x, cy);
            Stroke(img, W, H, c - dir * len, c + dir * len, strandW * (0.6f + 0.6f * kn.y), 0.75f * kn.y);

            // A faint arc to the last knot, above or below the path.
            Vector2 end = c + dir * len * (rng.NextDouble() < 0.5 ? -0.7f : 0.7f);
            if (hasPrev) Stroke(img, W, H, prevEnd, end, strandW * 0.6f, 0.3f * kn.y);
            prevEnd = end; hasPrev = true;
        }

        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Dark matter haze" };
        var px = new Color32[W * H];
        for (int y = 0; y < H; y++)
        {
            float edge = Mathf.Clamp01(Mathf.Min(y, H - 1 - y) / (H * 0.12f));   // no hard top or bottom
            for (int x = 0; x < W; x++)
            {
                float xe = Mathf.Clamp01(Mathf.Min(x, W - 1 - x) / (W * 0.03f));
                float v = Mathf.Pow(Mathf.Clamp01(img[y * W + x]), 0.8f) * edge * xe;
                px[y * W + x] = new Color32(255, 255, 255, (byte)(v * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    /// <summary>A soft line from a to b, added into img: gaussian across it, fading at its ends.</summary>
    static void Stroke(float[] img, int W, int H, Vector2 a, Vector2 b, float width, float amount)
    {
        Vector2 ab = b - a; float len2 = Mathf.Max(ab.sqrMagnitude, 1e-3f);
        int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - width * 3f)), x1 = Mathf.Min(W - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + width * 3f));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - width * 3f)), y1 = Mathf.Min(H - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + width * 3f));
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                Vector2 p = new Vector2(x, y) - a;
                float u = Mathf.Clamp01(Vector2.Dot(p, ab) / len2);
                float d = (p - ab * u).magnitude / width;
                float ends = Mathf.Sin(u * Mathf.PI);
                img[y * W + x] += amount * Mathf.Exp(-0.5f * d * d) * ends;
            }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    float Fade(float at, float seconds) { return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_clock - at) / Mathf.Max(0.01f, seconds))); }

    static Color WithAlpha(Color c, float k) { c.a *= k; return c; }

    void Place(RectTransform rt)
    {
        rt.gameObject.layer = _overlay.gameObject.layer;
        rt.SetParent(_overlay, false);
    }

    Image MakeImage(string name, Sprite sprite, Color colour)
    {
        var img = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
        Place(img.rectTransform);
        img.sprite = sprite;
        img.color = WithAlpha(colour, 0f);
        img.raycastTarget = false;
        return img;
    }

    TextMeshProUGUI MakeText(string name, string text, TextAlignmentOptions align, float h)
    {
        var tmp = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        Place(tmp.rectTransform);
        tmp.text = text;
        tmp.fontSize = labelSize * h;
        tmp.alignment = align;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;
        tmp.color = WithAlpha(labelColour, 0f);
        tmp.rectTransform.sizeDelta = new Vector2(labelSize * h * 12f, labelSize * h * 1.5f);
        tmp.rectTransform.pivot = align == TextAlignmentOptions.TopLeft ? new Vector2(0f, 1f)
                                : align == TextAlignmentOptions.Bottom ? new Vector2(0.5f, 0f)
                                : new Vector2(0.5f, 1f);
        return tmp;
    }

    static void SetPoint(RectTransform rt, Vector2 at)
    {
        rt.anchorMin = rt.anchorMax = at;
        rt.anchoredPosition = Vector2.zero;
    }

    /// <summary>Stretch between two screen fractions, padded by wide and tall canvas units.</summary>
    static void SetSpan(RectTransform rt, Vector2 from, Vector2 to, float wide, float tall)
    {
        rt.anchorMin = Vector2.Min(from, to);
        rt.anchorMax = Vector2.Max(from, to);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(wide, tall);
    }

    /// <summary>A soft round blob for the gas and the quasar.</summary>
    static Sprite Soft
    {
        get
        {
            if (_soft != null) return _soft;
            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Soft dot" };
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = (x - (S - 1) * 0.5f) / (S * 0.5f), dy = (y - (S - 1) * 0.5f) / (S * 0.5f);
                    float v = Mathf.Exp(-0.5f * (dx * dx + dy * dy) / 0.12f) * Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * S + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(v) * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _soft = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
            return _soft;
        }
    }

    void OnDisable()
    {
        if (_overlay != null) Destroy(_overlay.gameObject);
        if (_hazeTex != null) Destroy(_hazeTex);
        _lines.Clear();
    }
}

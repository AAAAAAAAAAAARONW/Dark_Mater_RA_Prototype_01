using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > Journey NEW > Flight Path
///
/// Draws the light's path through the journey in the Scene view, so the cosmic web — one huge
/// object out of Houdini — can be laid out against where the light actually goes and when:
///
///   RULER      A tick every second along the path, a labelled one every five, counted from
///              the moment the light enters each layer ("Macro +15 s").
///   VOICE      Every line said, as a bar beside the path from where it started to where it
///              ended — J3, the dark matter moment, J3b — with the moment's Y, nudge and
///              automatic toggle as points.
///   FILAMENTS  Where the path passes through the web's yellow gas, found by sampling the
///              web's own baked volumes along the path, listed with their times, with a
///              button to move the J3b trigger onto one.
///
/// WHERE THE TIMES COME FROM. A layer flown in Play mode with "Record in Play mode" ticked is
/// timed from that recording (FlightRecorder_NEW): exact, every slowdown and dive included,
/// and the only source for the voice. A layer not yet recorded is timed from its speed (base
/// speed × the layer's zoneSpeedMultiplier), marked "≈": right for a layer flown at one speed
/// like the first cosmic web, rough for the galaxies, which slow for their lines.
///
/// Moving the web does not make a recording stale — the light's path and pace do not depend
/// on it. Moving a door or changing a speed does: fly that layer again (2 jumps to the first
/// cosmic web).
/// </summary>
public class FlightPathWindow_NEW : EditorWindow
{
    // -- Settings ---------------------------------------------------------------------------

    const string Pref = "JourneyNEW.FlightPath.";

    static bool Show { get { return EditorPrefs.GetBool(Pref + "Show", true); } set { EditorPrefs.SetBool(Pref + "Show", value); } }
    static bool ShowRuler { get { return EditorPrefs.GetBool(Pref + "Ruler", true); } set { EditorPrefs.SetBool(Pref + "Ruler", value); } }
    static bool ShowVoice { get { return EditorPrefs.GetBool(Pref + "Voice", true); } set { EditorPrefs.SetBool(Pref + "Voice", value); } }
    static bool ShowFilaments { get { return EditorPrefs.GetBool(Pref + "Filaments", true); } set { EditorPrefs.SetBool(Pref + "Filaments", value); } }
    static bool Record { get { return EditorPrefs.GetBool(Pref + "Record", true); } set { EditorPrefs.SetBool(Pref + "Record", value); } }
    static float Threshold { get { return EditorPrefs.GetFloat(Pref + "Threshold", 0.35f); } set { EditorPrefs.SetFloat(Pref + "Threshold", value); } }
    static float ProbeRadius { get { return EditorPrefs.GetFloat(Pref + "Probe", 0.4f); } set { EditorPrefs.SetFloat(Pref + "Probe", value); } }
    static float LaneSpacing { get { return EditorPrefs.GetFloat(Pref + "Lane", 1.6f); } set { EditorPrefs.SetFloat(Pref + "Lane", value); } }

    const float StepUnits = 0.2f;       // path sampling for filaments and the predicted ruler
    const string CloudShader = "Custom/PreBakedCloudVolume";

    // -- Model --------------------------------------------------------------------------

    class Layer
    {
        public string id;
        public float s0, s1;            // distance along the path where it starts and ends
        public float speed;             // predicted, units/s
        public float blend;             // seconds to reach it
        public float entrySpeed;
        public FlightRecorder_NEW.Segment recorded;
    }

    class Filament
    {
        public string layerId;
        public float s0, s1, peak;
        public float t0, t1;            // seconds into the layer
        public bool predicted;
    }

    class Volume
    {
        public Matrix4x4 worldToLocal;
        public Color[] pixels;
        public int w, h, d;
        public float density;
        public Bounds bounds;
    }

    static Vector3 _origin, _dir;
    static float _length;
    static readonly List<Layer> _layers = new List<Layer>();
    static readonly List<Filament> _filaments = new List<Filament>();
    static float[] _yellow;             // yellow density per step along the path, 0..1 of the peak
    static FlightRecorder_NEW.Recording _recording;
    static string _problem;
    static int _volumeHash;
    static bool _dirty = true;
    static double _lastCheck;

    // -- Menu, play-mode hook, Scene view ------------------------------------------------

    [MenuItem("Tools/Journey NEW/Flight Path")]
    static void Open()
    {
        FlightPathWindow_NEW w = GetWindow<FlightPathWindow_NEW>("Flight Path");
        w.minSize = new Vector2(420f, 360f);
        _dirty = true;
    }

    [InitializeOnLoadMethod]
    static void Hook()
    {
        SceneView.duringSceneGui -= OnScene;
        SceneView.duringSceneGui += OnScene;

        EditorApplication.playModeStateChanged -= OnPlayMode;
        EditorApplication.playModeStateChanged += OnPlayMode;

        EditorApplication.hierarchyChanged -= MarkDirty;
        EditorApplication.hierarchyChanged += MarkDirty;
        UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += (s, m) => MarkDirty();
    }

    static void MarkDirty() { _dirty = true; }

    static void OnPlayMode(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode && Record && UnityEngine.Object.FindObjectOfType<LayerState_NEW>() != null)
        {
            GameObject go = new GameObject("[Flight Recorder]") { hideFlags = HideFlags.DontSave | HideFlags.NotEditable };
            go.AddComponent<FlightRecorder_NEW>();
        }

        if (change == PlayModeStateChange.EnteredEditMode) _dirty = true;
    }

    static void OnScene(SceneView view)
    {
        if (!Show || Event.current.type != EventType.Repaint) return;

        Rebuild(false);
        if (_problem != null || _layers.Count == 0) return;

        Camera cam = view.camera;
        DrawPath();
        if (ShowFilaments) DrawFilaments();
        if (ShowRuler) DrawRuler(cam);
        if (ShowVoice) DrawVoice();
    }

    // -- Building the model -------------------------------------------------------------

    static void Rebuild(bool force)
    {
        // Looking for moved volumes walks the scene; twice a second is plenty for a drag.
        if (!force && !_dirty && EditorApplication.timeSinceStartup - _lastCheck < 0.5) return;
        _lastCheck = EditorApplication.timeSinceStartup;

        List<Renderer> clouds = FindClouds();
        int hash = Hash(clouds);
        if (!force && !_dirty && hash == _volumeHash) return;

        _dirty = false;
        _volumeHash = hash;
        _problem = null;
        _layers.Clear();
        _filaments.Clear();
        _recording = FlightRecorder_NEW.Load();

        PlayerRig_NEW player = UnityEngine.Object.FindObjectOfType<PlayerRig_NEW>();
        LayerState_NEW state = UnityEngine.Object.FindObjectOfType<LayerState_NEW>();
        if (player == null || state == null) { _problem = "Not a journey scene (no PlayerRig_NEW / LayerState_NEW)."; return; }

        SerializedObject rig = new SerializedObject(player);
        Vector3 dir = rig.FindProperty("movementDirection").vector3Value;
        float baseSpeed = rig.FindProperty("speed").floatValue;
        _dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
        _origin = player.transform.position;

        LayerCatalog_NEW catalog = new SerializedObject(state).FindProperty("catalog").objectReferenceValue as LayerCatalog_NEW;

        // Gates in the order the light meets them.
        List<KeyValuePair<float, string>> gates = new List<KeyValuePair<float, string>>();
        foreach (LayerGate_NEW g in UnityEngine.Object.FindObjectsOfType<LayerGate_NEW>())
        {
            Collider c = g.GetComponent<Collider>();
            Vector3 p = c != null ? c.bounds.center : g.transform.position;
            float s = Vector3.Dot(p - _origin, _dir);
            if (s > 0f) gates.Add(new KeyValuePair<float, string>(s, g.LayerId));
        }
        gates.Sort((a, b) => a.Key.CompareTo(b.Key));

        LayerProfile_NEW start = catalog != null ? catalog.GetStartLayer() : null;
        string current = start != null ? start.layerId : "Quasar";
        float s0 = 0f;
        float prevSpeed = baseSpeed;

        for (int i = 0; i <= gates.Count; i++)
        {
            float s1 = i < gates.Count ? gates[i].Key : (gates.Count > 0 ? gates[gates.Count - 1].Key + 60f : 100f);
            LayerProfile_NEW profile = catalog != null ? catalog.Get(current) : null;

            Layer l = new Layer
            {
                id = current,
                s0 = s0,
                s1 = s1,
                speed = baseSpeed * (profile != null ? profile.zoneSpeedMultiplier : 1f),
                blend = profile != null ? profile.speedBlendDuration : 0f,
                entrySpeed = i == 0 ? baseSpeed * (profile != null ? profile.zoneSpeedMultiplier : 1f) : prevSpeed,
                recorded = _recording != null ? _recording.Find(current) : null
            };
            _layers.Add(l);
            prevSpeed = l.speed;

            if (i < gates.Count) { s0 = s1; current = gates[i].Value; }
        }

        _length = _layers[_layers.Count - 1].s1;
        FindFilaments(clouds);
    }

    static List<Renderer> FindClouds()
    {
        List<Renderer> list = new List<Renderer>();
        foreach (Renderer r in UnityEngine.Object.FindObjectsOfType<Renderer>())
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            Material m = r.sharedMaterial;
            if (m == null || m.shader == null || m.shader.name != CloudShader) continue;
            if (!(m.GetTexture("_BakedTex") is Texture3D)) continue;
            list.Add(r);
        }
        return list;
    }

    static int Hash(List<Renderer> clouds)
    {
        int h = clouds.Count;
        foreach (Renderer r in clouds)
            unchecked { h = h * 31 + r.transform.localToWorldMatrix.GetHashCode(); }
        foreach (LayerGate_NEW g in UnityEngine.Object.FindObjectsOfType<LayerGate_NEW>())
            unchecked { h = h * 31 + g.transform.position.GetHashCode(); }
        return h;
    }

    // -- Filaments ------------------------------------------------------------------------

    static readonly Dictionary<int, Color[]> _pixelCache = new Dictionary<int, Color[]>();

    static void FindFilaments(List<Renderer> clouds)
    {
        List<Volume> volumes = new List<Volume>();
        foreach (Renderer r in clouds)
        {
            Texture3D tex = (Texture3D)r.sharedMaterial.GetTexture("_BakedTex");
            if (!tex.isReadable) continue;

            Color[] px;
            if (!_pixelCache.TryGetValue(tex.GetInstanceID(), out px))
            {
                px = tex.GetPixels(0);
                _pixelCache[tex.GetInstanceID()] = px;
            }

            Matrix4x4 l2w = r.transform.localToWorldMatrix;
            Bounds b = new Bounds(l2w.MultiplyPoint3x4(Vector3.zero), Vector3.zero);
            for (int c = 0; c < 8; c++)
                b.Encapsulate(l2w.MultiplyPoint3x4(new Vector3((c & 1) == 0 ? -0.5f : 0.5f, (c & 2) == 0 ? -0.5f : 0.5f, (c & 4) == 0 ? -0.5f : 0.5f)));

            volumes.Add(new Volume
            {
                worldToLocal = r.transform.worldToLocalMatrix,
                pixels = px,
                w = tex.width, h = tex.height, d = tex.depth,
                density = r.sharedMaterial.HasProperty("_Density") ? r.sharedMaterial.GetFloat("_Density") : 1f,
                bounds = b
            });
        }

        int n = Mathf.CeilToInt(_length / StepUnits) + 1;
        float[] raw = new float[n];
        Vector3 side = Vector3.Cross(_dir, Vector3.up);
        if (side.sqrMagnitude < 0.01f) side = Vector3.Cross(_dir, Vector3.right);
        side.Normalize();
        Vector3 up = Vector3.Cross(side, _dir).normalized;
        float r0 = ProbeRadius;
        Vector3[] probes = { Vector3.zero, side * r0, -side * r0, up * r0, -up * r0 };

        for (int i = 0; i < n; i++)
        {
            Vector3 c = _origin + _dir * (i * StepUnits);
            float best = 0f;
            foreach (Volume v in volumes)
            {
                if (!v.bounds.Contains(c)) continue;
                foreach (Vector3 o in probes)
                {
                    float y = Yellow(v, c + o);
                    if (y > best) best = y;
                }
            }
            raw[i] = best;
        }

        // Normalised per layer: each web is judged against its own densest yellow.
        _yellow = new float[n];
        foreach (Layer l in _layers)
        {
            int a = Mathf.Clamp((int)(l.s0 / StepUnits), 0, n - 1), z = Mathf.Clamp((int)(l.s1 / StepUnits), 0, n - 1);
            float peak = 0f;
            for (int i = a; i <= z; i++) peak = Mathf.Max(peak, raw[i]);
            if (peak <= 0.0001f) continue;

            for (int i = a; i <= z; i++) _yellow[i] = raw[i] / peak;

            float th = Threshold;
            int startI = -1;
            float peakHere = 0f;
            int gapRun = 0;
            int maxGap = Mathf.CeilToInt(0.5f / StepUnits);

            for (int i = a; i <= z + 1; i++)
            {
                bool inside = i <= z && _yellow[i] >= th;
                if (inside)
                {
                    if (startI < 0) { startI = i; peakHere = 0f; }
                    peakHere = Mathf.Max(peakHere, _yellow[i]);
                    gapRun = 0;
                    continue;
                }
                if (startI < 0) continue;
                if (i <= z && ++gapRun <= maxGap) continue;

                int endI = i - gapRun;
                if ((endI - startI) * StepUnits >= 0.1f)
                    _filaments.Add(MakeFilament(l, startI * StepUnits, endI * StepUnits, peakHere));
                startI = -1;
                gapRun = 0;
            }
        }
    }

    static Filament MakeFilament(Layer l, float s0, float s1, float peak)
    {
        Filament f = new Filament { layerId = l.id, s0 = s0, s1 = s1, peak = peak };
        bool p0, p1;
        f.t0 = TimeAt(l, s0, out p0);
        f.t1 = TimeAt(l, s1, out p1);
        f.predicted = p0 || p1;
        return f;
    }

    /// <summary>The baked colour's yellowness times its density, at a world point.</summary>
    static float Yellow(Volume v, Vector3 world)
    {
        Vector3 uv = v.worldToLocal.MultiplyPoint3x4(world) + new Vector3(0.5f, 0.5f, 0.5f);
        if (uv.x < 0f || uv.y < 0f || uv.z < 0f || uv.x > 1f || uv.y > 1f || uv.z > 1f) return 0f;

        int x = Mathf.Clamp((int)(uv.x * v.w), 0, v.w - 1);
        int y = Mathf.Clamp((int)(uv.y * v.h), 0, v.h - 1);
        int z = Mathf.Clamp((int)(uv.z * v.d), 0, v.d - 1);
        Color c = v.pixels[(z * v.h + y) * v.w + x];

        float m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        if (m <= 0.0001f) return 0f;
        float yellowness = Mathf.Clamp01(((c.r + c.g) * 0.5f - c.b) / m * 1.5f);

        return c.a * v.density * yellowness;
    }

    // -- Time along the path ------------------------------------------------------------

    static Layer LayerAt(float s)
    {
        foreach (Layer l in _layers) if (s >= l.s0 && s <= l.s1) return l;
        return _layers.Count > 0 ? _layers[_layers.Count - 1] : null;
    }

    /// <summary>Seconds after entering layer l at path distance s. predicted = no recording.</summary>
    static float TimeAt(Layer l, float s, out bool predicted)
    {
        FlightRecorder_NEW.Segment rec = l.recorded;
        if (rec != null && rec.positions.Count > 1)
        {
            predicted = false;
            // Recorded positions advance along the path; find where s is passed.
            for (int i = 1; i < rec.positions.Count; i++)
            {
                float a = Vector3.Dot(rec.positions[i - 1] - _origin, _dir);
                float b = Vector3.Dot(rec.positions[i] - _origin, _dir);
                if (s <= b && b > a) return (i - 1 + Mathf.Clamp01((s - a) / (b - a))) * rec.sampleEvery;
            }
            return rec.Duration;
        }

        predicted = true;
        // Speed blends from the entry speed to the layer's over its blend time, then holds.
        float d = s - l.s0, t = 0f;
        if (d <= 0f) return 0f;
        float v0 = Mathf.Max(0.01f, l.entrySpeed), v1 = Mathf.Max(0.01f, l.speed), T = l.blend;
        float blendDist = T > 0f ? (v0 + v1) * 0.5f * T : 0f;
        if (d <= blendDist && T > 0f)
        {
            // Solve d = v0 t + (v1-v0) t² / (2T).
            float A = (v1 - v0) / (2f * T);
            if (Mathf.Abs(A) < 1e-5f) return d / v0;
            return (-v0 + Mathf.Sqrt(Mathf.Max(0f, v0 * v0 + 4f * A * d))) / (2f * A);
        }
        t = T + (d - blendDist) / v1;
        return t;
    }

    static float DistanceAt(Layer l, float t)
    {
        // Inverse of TimeAt by bisection; the ruler needs a handful per layer.
        float lo = l.s0, hi = l.s1;
        bool p;
        if (TimeAt(l, hi, out p) < t) return float.NaN;
        for (int i = 0; i < 30; i++)
        {
            float mid = (lo + hi) * 0.5f;
            if (TimeAt(l, mid, out p) < t) lo = mid; else hi = mid;
        }
        return (lo + hi) * 0.5f;
    }

    static Vector3 PointAt(float s) { return _origin + _dir * s; }

    /// <summary>Seconds the light spends in the layer.</summary>
    static float Total(Layer l, out bool predicted)
    {
        predicted = l.recorded == null || l.recorded.positions.Count < 2;
        if (!predicted) return l.recorded.Duration;
        return TimeAt(l, l.s1, out predicted);
    }

    /// <summary>Where the light is t seconds into the layer: recorded, or along the path at the predicted pace.</summary>
    static bool PositionAt(Layer l, float t, out Vector3 at)
    {
        if (l.recorded != null && l.recorded.positions.Count > 1)
        {
            at = l.recorded.At(t);
            return t <= l.recorded.Duration;
        }

        float s = DistanceAt(l, t);
        at = float.IsNaN(s) ? Vector3.zero : PointAt(s);
        return !float.IsNaN(s);
    }

    // -- Drawing --------------------------------------------------------------------------

    static readonly Color[] LayerColours =
    {
        new Color(1f, 0.55f, 0.3f), new Color(0.55f, 0.85f, 1f), new Color(1f, 0.85f, 0.45f),
        new Color(0.6f, 1f, 0.6f), new Color(0.85f, 0.65f, 1f), new Color(1f, 1f, 0.7f), new Color(0.6f, 0.8f, 1f)
    };

    static GUIStyle _label;
    static GUIStyle Label
    {
        get
        {
            if (_label == null)
            {
                _label = new GUIStyle(EditorStyles.miniBoldLabel);
                _label.normal.textColor = Color.white;
            }
            return _label;
        }
    }

    static GUIStyle Tinted(Color c)
    {
        GUIStyle s = new GUIStyle(Label);
        s.normal.textColor = c;
        return s;
    }

    static void DrawPath()
    {
        for (int i = 0; i < _layers.Count; i++)
        {
            Layer l = _layers[i];
            Color c = LayerColours[i % LayerColours.Length];
            Handles.color = new Color(c.r, c.g, c.b, 0.9f);
            Handles.DrawAAPolyLine(3f, PointAt(l.s0), PointAt(l.s1));

            bool p;
            float total = Total(l, out p);
            Handles.Label(PointAt(l.s0) + Vector3.up * 0.6f,
                          l.id + "  " + (p ? "≈" : "") + total.ToString("0") + " s" + (p ? "  (not recorded)" : ""),
                          Tinted(c));
        }
    }

    static void DrawRuler(Camera cam)
    {
        for (int i = 0; i < _layers.Count; i++)
        {
            Layer l = _layers[i];
            Color c = LayerColours[i % LayerColours.Length];
            bool p;
            float total = Total(l, out p);

            for (int sec = 1; sec <= Mathf.FloorToInt(total); sec++)
            {
                Vector3 at;
                if (!PositionAt(l, sec, out at)) break;

                Vector3 across = Vector3.Cross(_dir, cam.transform.forward);
                if (across.sqrMagnitude < 0.0001f) across = Vector3.up;
                float size = HandleUtility.GetHandleSize(at) * (sec % 5 == 0 ? 0.12f : 0.05f);
                across = across.normalized * size;

                Handles.color = new Color(c.r, c.g, c.b, sec % 5 == 0 ? 1f : 0.6f);
                Handles.DrawLine(at - across, at + across);

                if (sec % 5 == 0)
                    Handles.Label(at + across * 1.4f, (p ? "≈" : "") + "+" + sec + " s", Tinted(c));
            }
        }
    }

    static void DrawFilaments()
    {
        if (_yellow != null)
        {
            // The yellow gas along the path, as a glow on the line.
            Handles.color = new Color(1f, 0.85f, 0.2f, 1f);
            for (int i = 1; i < _yellow.Length; i++)
            {
                float y = _yellow[i];
                if (y < 0.08f) continue;
                Handles.color = new Color(1f, 0.85f, 0.2f, Mathf.Clamp01(y));
                Handles.DrawAAPolyLine(2f + 8f * y, PointAt((i - 1) * StepUnits), PointAt(i * StepUnits));
            }
        }

        for (int i = 0; i < _filaments.Count; i++)
        {
            Filament f = _filaments[i];
            Vector3 mid = PointAt((f.s0 + f.s1) * 0.5f);
            Handles.color = new Color(1f, 0.85f, 0.2f, 1f);
            Handles.DrawWireDisc(mid, _dir, HandleUtility.GetHandleSize(mid) * 0.15f);
            Handles.Label(mid + Vector3.down * HandleUtility.GetHandleSize(mid) * 0.25f,
                          "F" + (i + 1) + "  " + f.layerId + " " + (f.predicted ? "≈" : "") + "+" + f.t0.ToString("0.0") + " s",
                          Tinted(new Color(1f, 0.85f, 0.2f)));
        }
    }

    static readonly Dictionary<string, Color> LaneColours = new Dictionary<string, Color>
    {
        { "VO", new Color(0.55f, 0.75f, 1f) },
        { "Moment", new Color(0.45f, 0.6f, 1f) },
        { "Filament", new Color(1f, 0.85f, 0.2f) }
    };

    static void DrawVoice()
    {
        Vector3 side = Vector3.Cross(Vector3.up, _dir).normalized;
        if (side.sqrMagnitude < 0.01f) side = Vector3.right;

        foreach (Layer l in _layers)
        {
            if (l.recorded == null) continue;

            // Lanes alternate so two lines that touch do not draw over each other.
            int lane = 0;
            foreach (FlightRecorder_NEW.Mark m in l.recorded.marks)
            {
                Color c;
                if (!LaneColours.TryGetValue(m.lane, out c)) c = Color.white;
                bool point = m.t1 - m.t0 < 0.05f;

                if (point)
                {
                    Vector3 at = m.p0 + side * LaneSpacing * 0.5f;
                    Handles.color = c;
                    Handles.DrawLine(m.p0, at);
                    Handles.DrawSolidDisc(at, _dir, HandleUtility.GetHandleSize(at) * 0.04f);
                    Handles.Label(at, "  " + m.label + "  +" + m.t0.ToString("0.0") + " s", Tinted(c));
                    continue;
                }

                Vector3 off = side * LaneSpacing * (1 + (lane++ % 2));
                Handles.color = c;
                Handles.DrawAAPolyLine(6f, m.p0 + off, m.p1 + off);
                Handles.DrawDottedLine(m.p0, m.p0 + off, 3f);
                Handles.DrawDottedLine(m.p1, m.p1 + off, 3f);
                Handles.Label(m.p0 + off + Vector3.up * HandleUtility.GetHandleSize(m.p0) * 0.12f,
                              m.label + "   +" + m.t0.ToString("0.0") + " → +" + m.t1.ToString("0.0") + " s", Tinted(c));
            }
        }
    }

    // -- Window -------------------------------------------------------------------------

    Vector2 _scroll;

    void OnEnable() { _dirty = true; }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "The light's path in the Scene view: a tick per second, a label every 5, counted from entering each layer. " +
            "Layers flown in Play with 'Record' on are exact and show the voice-over; the rest are estimated from speed (≈). " +
            "Moving the web keeps a recording valid; moving a door or a speed needs that layer flown again (2 = first cosmic web).",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        Show = EditorGUILayout.ToggleLeft("Show in Scene view", Show);
        using (new EditorGUI.IndentLevelScope())
        {
            ShowRuler = EditorGUILayout.ToggleLeft("Ruler (seconds)", ShowRuler);
            ShowVoice = EditorGUILayout.ToggleLeft("Voice-over and moments (from the recording)", ShowVoice);
            ShowFilaments = EditorGUILayout.ToggleLeft("Yellow filaments", ShowFilaments);
            LaneSpacing = EditorGUILayout.Slider("Voice lane offset", LaneSpacing, 0.2f, 10f);
        }
        Record = EditorGUILayout.ToggleLeft("Record in Play mode (saved when Play stops)", Record);
        bool viewChanged = EditorGUI.EndChangeCheck();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Filament detection", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        Threshold = EditorGUILayout.Slider(new GUIContent("Threshold", "Fraction of the layer's densest yellow on the path that counts as being inside a filament."), Threshold, 0.05f, 0.95f);
        ProbeRadius = EditorGUILayout.Slider(new GUIContent("Probe radius", "How close the gas must pass the light's centre, in units."), ProbeRadius, 0f, 3f);
        if (EditorGUI.EndChangeCheck()) _dirty = true;

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Re-scan")) { _pixelCache.Clear(); Rebuild(true); viewChanged = true; }
            if (GUILayout.Button("Forget the recording"))
            {
                if (System.IO.File.Exists(FlightRecorder_NEW.FilePath)) System.IO.File.Delete(FlightRecorder_NEW.FilePath);
                Rebuild(true);
                viewChanged = true;
            }
        }

        Rebuild(false);
        if (viewChanged) SceneView.RepaintAll();

        if (_problem != null) { EditorGUILayout.HelpBox(_problem, MessageType.Warning); return; }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(_recording != null ? "Recording: " + _recording.savedAt : "No recording yet: press Play with 'Record' on.",
                                   EditorStyles.miniLabel);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        foreach (Layer l in _layers)
        {
            bool p;
            float total = Total(l, out p);
            EditorGUILayout.LabelField(string.Format("{0}   {1:0} units   {2}{3:0} s{4}",
                                                     l.id, l.s1 - l.s0, p ? "≈" : "", total,
                                                     l.recorded != null && l.recorded.enteredByJump ? "   (recorded after a jump)" : ""),
                                       EditorStyles.boldLabel);

            if (l.recorded != null)
                foreach (FlightRecorder_NEW.Mark m in l.recorded.marks)
                    EditorGUILayout.LabelField(m.t1 - m.t0 < 0.05f
                        ? string.Format("      {0,6:0.0} s        {1}: {2}", m.t0, m.lane, m.label)
                        : string.Format("      {0,6:0.0} → {1:0.0} s   {2}: {3}", m.t0, m.t1, m.lane, m.label),
                        EditorStyles.miniLabel);

            for (int i = 0; i < _filaments.Count; i++)
            {
                Filament f = _filaments[i];
                if (f.layerId != l.id) continue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(string.Format("      F{0}   {1}{2:0.0} → {3:0.0} s   z {4:0.0}   peak {5:0.00}",
                                                             i + 1, f.predicted ? "≈" : "", f.t0, f.t1, PointAt(f.s0).z, f.peak),
                                               EditorStyles.miniLabel);
                    if (GUILayout.Button("Frame", EditorStyles.miniButton, GUILayout.Width(48f))) Frame(f);
                    if (GUILayout.Button("J3b here", EditorStyles.miniButton, GUILayout.Width(64f))) PutJ3bOn(f);
                }
            }

            EditorGUILayout.Space();
        }

        EditorGUILayout.EndScrollView();
    }

    static void Frame(Filament f)
    {
        SceneView sv = SceneView.lastActiveSceneView;
        if (sv == null) return;
        Vector3 mid = PointAt((f.s0 + f.s1) * 0.5f);
        sv.Frame(new Bounds(mid, Vector3.one * 12f), false);
    }

    /// <summary>Moves the filament trigger that says J3b onto this crossing.</summary>
    static void PutJ3bOn(Filament f)
    {
        FilamentAbsorber_NEW target = null;
        foreach (FilamentAbsorber_NEW a in UnityEngine.Object.FindObjectsOfType<FilamentAbsorber_NEW>())
        {
            if (a.AbsorberKind != FilamentAbsorber_NEW.Kind.Filament) continue;
            SerializedProperty line = new SerializedObject(a).FindProperty("firstTimeLine");
            if (target == null || (line != null && line.objectReferenceValue != null)) target = a;
        }

        if (target == null)
        {
            EditorUtility.DisplayDialog("Flight Path", "No filament trigger in the scene. Run Tools > Journey NEW > " +
                                        "Voice Over > Wire Journey Voice Over, which makes one, then try again.", "OK");
            return;
        }

        Undo.RecordObject(target.transform, "Put J3b on a filament");
        target.transform.position = PointAt((f.s0 + f.s1) * 0.5f);

        BoxCollider box = target.GetComponent<BoxCollider>();
        if (box != null)
        {
            Undo.RecordObject(box, "Put J3b on a filament");
            Vector3 size = box.size;
            Vector3 scale = target.transform.lossyScale;
            size.z = Mathf.Max(0.5f, (f.s1 - f.s0) / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
            box.size = size;
            box.center = Vector3.zero;
        }

        Selection.activeGameObject = target.gameObject;
        Frame(f);
        Debug.Log("[FlightPathWindow_NEW] Moved " + target.name + " onto filament at z " + PointAt(f.s0).z.ToString("0.0") +
                  "–" + PointAt(f.s1).z.ToString("0.0") + ". Save the scene.", target);
    }
}

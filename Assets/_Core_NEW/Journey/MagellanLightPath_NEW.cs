using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Starlight going through the Magellan 6.5 m telescope the way it really does: down through
/// the open enclosure onto the primary, up to the secondary, down onto the tertiary at the
/// middle of the tube, and out along the elevation axis to the focus, where the instrument is.
///
/// THE OPTICS. The light comes in as a plane wave, a ring of rays spread over the primary
/// (between innerRadius and outerRadius of it). Each hits the primary's paraboloid
/// (focalLength, f/1.25) and heads for its focus; the secondary, short of that focus, sends it
/// on towards the f/11 focus; the tertiary at 45 degrees folds that out along the axis. So the
/// rays converge as they really do: a wide cone up to the secondary, a narrow one down to the
/// tertiary, and all to one point at the focus. Where the folded leg passes the tertiary the
/// rays are taken across its plane at the axis's height, which is near enough at this size.
///
/// The numbers are in the tube's own space (its pivot on the elevation axis, +Y along the
/// optical axis), measured from LCO's model and set by Tools > Journey NEW > Magellan > Build
/// Telescope from STL. The rays are children of the tube, so they tilt with it.
///
/// THE COLOUR. Mirrors bend every colour alike, so through them the light is white. It is the
/// instrument - a spectrograph - that spreads it into its colours: once the light is at the
/// focus a fan of colours opens from it inside the instrument, violet to red, fanRays of them
/// over fanSpread degrees. That fan is where the spectrum on the screen comes from.
///
/// THE MIRRORS. Each lights up as the light reaches it (its mirror submesh's emission, by a
/// property block on that material slot alone), and the instrument when the light gets there;
/// then onReachedInstrument is called — the moment for the spectrum.
///
/// Play() starts it; Stop() clears it. playOnEnable and loop are for looking at it on its own.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("LIGHT PATH", "#F2C14E")]
public class MagellanLightPath_NEW : MonoBehaviour
{
    [Header("Telescope (set by the builder)")]
    [Tooltip("The tube: its pivot on the elevation axis, +Y the optical axis. The rays are made under it.")]
    [SerializeField] Transform tube;
    [SerializeField] Renderer primaryMirror;
    [SerializeField] Renderer secondaryMirror;
    [SerializeField] Renderer tertiaryMirror;
    [Tooltip("The material slot that is the mirror's face on each mirror's renderer.")]
    [SerializeField] int mirrorSlot = 1;
    [Tooltip("The colour property the mirrors and the instrument light up by: _EmissionColor for Standard, " +
             "_Emission for SummitSet_NEW (EarthSummit_NEW sets it).")]
    [SerializeField] string emissionProperty = "_EmissionColor";
    [Tooltip("Lit up when the light reaches the focus. Empty = none.")]
    [SerializeField] Renderer instrument;

    [Header("Optics, metres in the tube's space (set by the builder)")]
    [SerializeField] float primaryVertexY = -0.775f;
    [SerializeField] float focalLength = 8.125f;
    [SerializeField] float primaryRadius = 3.25f;
    [SerializeField] float secondaryVertexY = 6.1f;
    [Tooltip("From the secondary to the final focus, along the light: f/11 over f/1.25 times the secondary's distance short of the prime focus.")]
    [SerializeField] float secondaryToFocus = 11f;
    [SerializeField] Vector3 tertiaryCentre = Vector3.zero;
    [SerializeField] Vector3 focus = new Vector3(0f, 0f, 4.9f);

    [Header("Light")]
    [Tooltip("How many rays.")]
    [Range(3, 64)] [SerializeField] int rays = 18;
    [Tooltip("Where on the primary they land, as fractions of its radius: alternate rays on the inner and outer ring.")]
    [Range(0.15f, 1f)] [SerializeField] float innerRadius = 0.45f;
    [Range(0.15f, 1f)] [SerializeField] float outerRadius = 0.9f;
    [Tooltip("How far above the primary's rim the rays start, metres.")]
    [Min(1f)] [SerializeField] float startAbove = 20f;
    [Tooltip("Metres a second the light goes. The whole path is about 40 m.")]
    [Min(0.5f)] [SerializeField] float speed = 7f;
    [Tooltip("How long a ray's tail is, metres.")]
    [Min(0.1f)] [SerializeField] float tail = 6f;
    [Tooltip("Once at the focus, the rays stay drawn whole for this many seconds, then fade.")]
    [Min(0f)] [SerializeField] float hold = 2.5f;
    [Tooltip("Metres, at the telescope's own size: it grows and shrinks with the tube.")]
    [Min(0.01f)] [SerializeField] float width = 0.05f;
    [Tooltip("White: mirrors bend every colour alike. A touch blue, starlight's.")]
    [ColorUsage(false, true)] [SerializeField] Color colour = new Color(1.45f, 1.5f, 1.65f, 1f);
    [Tooltip("Drawn behind the telescope's parts at this brightness, so the path through the tower reads.")]
    [Range(0f, 1f)] [SerializeField] float occluded = 0.3f;

    [Header("The spectrum, in the instrument")]
    [Tooltip("Spread the light into its colours at the focus.")]
    [SerializeField] bool disperse = true;
    [Range(3, 64)] [SerializeField] int fanRays = 28;
    [Tooltip("Degrees from violet to red.")]
    [Range(2f, 90f)] [SerializeField] float fanSpread = 26f;
    [Tooltip("How far the colours run on from the focus, metres.")]
    [Min(0.1f)] [SerializeField] float fanLength = 1.8f;
    [Tooltip("Seconds the fan takes to open.")]
    [Min(0.05f)] [SerializeField] float fanOpen = 0.7f;
    [Tooltip("How bright the colours are.")]
    [Min(0f)] [SerializeField] float fanBrightness = 2.2f;
    [Tooltip("Seen through the instrument's walls at this brightness: the fan is inside it.")]
    [Range(0f, 1f)] [SerializeField] float fanThroughWalls = 0.85f;

    [Header("Mirrors lighting up")]
    [ColorUsage(false, true)] [SerializeField] Color mirrorFlash = new Color(0.9f, 0.8f, 0.6f, 1f);
    [ColorUsage(false, true)] [SerializeField] Color instrumentGlow = new Color(0.35f, 0.36f, 0.42f, 1f);
    [Tooltip("Seconds a flash takes to die down to a third.")]
    [Min(0.05f)] [SerializeField] float flashDecay = 0.8f;

    [Header("Testing")]
    [SerializeField] bool playOnEnable = false;
    [SerializeField] bool loop = false;

    [Space]
    public UnityEvent onReachedInstrument = new UnityEvent();

    /// <summary>True from Play() until the light has faded out.</summary>
    public bool Playing => _playing;

    /// <summary>The light has reached the instrument (since the last Play).</summary>
    public bool Reached => _reached[3];

    /// <summary>The tube: the rays are drawn in its space.</summary>
    public Transform Tube => tube;

    /// <summary>The primary's rim, metres up the tube's axis from its pivot.</summary>
    public float RimY => primaryVertexY + primaryRadius * primaryRadius / (4f * focalLength);

    /// <summary>How far above the primary's rim the rays start, metres. Set before Play.</summary>
    public float StartAbove { get => startAbove; set => startAbove = Mathf.Max(1f, value); }

    /// <summary>Lights the mirrors and the instrument by this colour property instead (see emissionProperty).</summary>
    public void UseEmissionProperty(string property)
    {
        emissionProperty = property;
        _emissionId = Shader.PropertyToID(property);
    }

    /// <summary>Seconds from Play to the light reaching the instrument.</summary>
    public float SecondsToInstrument
    {
        get
        {
            float rimY = primaryVertexY + primaryRadius * primaryRadius / (4f * focalLength);
            float path = startAbove + (rimY - primaryVertexY) + (secondaryVertexY - primaryVertexY) +
                         (secondaryVertexY - tertiaryCentre.y) + focus.magnitude;
            return path / speed;
        }
    }

    class Ray
    {
        public Vector3[] points;   // tube space: start, primary, secondary, tertiary, focus
        public float[] at;         // distance along the path at each point
        public LineRenderer line;
    }

    readonly List<Ray> _rays = new List<Ray>();
    readonly List<LineRenderer> _fan = new List<LineRenderer>();
    readonly List<Vector3> _scratch = new List<Vector3>();
    Material _material, _fanMaterial;
    float _fanClock;
    MaterialPropertyBlock _block;
    bool _playing;
    float _clock;
    float[] _flash = new float[4];   // primary, secondary, tertiary, instrument
    bool[] _reached = new bool[4];
    int _emissionId = -1;

    void OnEnable()
    {
        if (playOnEnable) Play();
    }

    void OnDisable()
    {
        Stop();
    }

    void OnDestroy()
    {
        if (_material != null) Destroy(_material);
        if (_fanMaterial != null) Destroy(_fanMaterial);
    }

    [ContextMenu("Play")]
    public void Play()
    {
        if (tube == null) tube = transform;
        Build();
        _clock = _fanClock = 0f;
        for (int i = 0; i < 4; i++) { _flash[i] = 0f; _reached[i] = false; }
        _playing = true;
    }

    [ContextMenu("Stop")]
    public void Stop()
    {
        _playing = false;
        foreach (Ray r in _rays) if (r.line != null) r.line.positionCount = 0;
        foreach (LineRenderer l in _fan) if (l != null) l.positionCount = 0;
        for (int i = 0; i < 4; i++) _flash[i] = 0f;
        ApplyFlashes();
    }

    void Update()
    {
        if (!_playing) return;
        _clock += Time.deltaTime;

        float lightEnd = 0f, head = _clock * speed;
        foreach (Ray r in _rays) lightEnd = Mathf.Max(lightEnd, r.at[r.at.Length - 1]);
        float arrive = lightEnd / speed;
        float fade = 1f - Mathf.Clamp01((_clock - arrive - hold) / 1f);

        foreach (Ray r in _rays)
        {
            float end = r.at[r.at.Length - 1];
            // Until the focus, a tail of light running down the path; once there, the whole
            // path lit, so the converging cones stand as a picture of the optics, then fading.
            float to = Mathf.Min(head, end);
            float from = head >= end ? 0f : Mathf.Max(0f, head - tail);
            Draw(r, from, to, head >= end ? fade : 1f);

            for (int k = 0; k < 4; k++)
                if (!_reached[k] && head >= r.at[k + 1])
                {
                    _reached[k] = true;
                    _flash[k] = 1f;
                    if (k == 3) onReachedInstrument.Invoke();
                }
        }

        float decay = Mathf.Exp(-Time.deltaTime / flashDecay * 1.1f);
        for (int k = 0; k < 3; k++) _flash[k] *= decay;
        // The instrument stays lit while the path is held, then fades with it; the colours open in it.
        if (_reached[3])
        {
            _flash[3] = fade;
            _fanClock += Time.deltaTime;
            DrawFan(Mathf.SmoothStep(0f, 1f, _fanClock / fanOpen), fade);
        }
        ApplyFlashes();

        if (fade <= 0f)
        {
            Stop();
            if (loop) Play();
        }
    }

    void Draw(Ray r, float from, float to, float alpha)
    {
        _scratch.Clear();
        if (to > from)
        {
            _scratch.Add(PointAt(r, from));
            for (int i = 0; i < r.at.Length; i++)
                if (r.at[i] > from && r.at[i] < to) _scratch.Add(r.points[i]);
            _scratch.Add(PointAt(r, to));
        }
        r.line.widthMultiplier = width * (tube != null ? tube.lossyScale.x : 1f);
        r.line.positionCount = _scratch.Count;
        for (int i = 0; i < _scratch.Count; i++) r.line.SetPosition(i, _scratch[i]);

        // Faint at the tail, full at the head while it runs; even once it is whole.
        bool whole = from <= 0f && to >= r.at[r.at.Length - 1];
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(whole ? alpha * 0.55f : 0f, 0f), new GradientAlphaKey(alpha * (whole ? 0.55f : 1f), 1f) });
        r.line.colorGradient = g;
    }

    /// <summary>
    /// The colours spreading from the focus, on along the light's way and fanned across the
    /// tube's axis, violet at one edge and red at the other; open 0 to 1.
    /// </summary>
    void DrawFan(float open, float alpha)
    {
        if (!disperse || _fan.Count == 0) return;
        Vector3 on = (focus - tertiaryCentre).normalized;
        Vector3 across = Vector3.Cross(on, Vector3.up);
        if (across.sqrMagnitude < 1e-6f) across = Vector3.right;
        across.Normalize();
        float w = width * 0.8f * (tube != null ? tube.lossyScale.x : 1f);
        for (int i = 0; i < _fan.Count; i++)
        {
            float f = _fan.Count > 1 ? i / (_fan.Count - 1f) : 0.5f;
            Vector3 d = Quaternion.AngleAxis((f - 0.5f) * fanSpread, across) * on;
            LineRenderer l = _fan[i];
            l.positionCount = 2;
            l.SetPosition(0, focus);
            l.SetPosition(1, focus + d * fanLength * open);
            l.widthMultiplier = w;
            Color c = Wavelength(Mathf.Lerp(400f, 690f, f)) * fanBrightness;
            c.a = 1f;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                      new[] { new GradientAlphaKey(alpha * 0.35f, 0f), new GradientAlphaKey(alpha, 1f) });
            l.colorGradient = g;
        }
    }

    /// <summary>A visible wavelength's colour, nanometres, roughly as the eye sees it.</summary>
    static Color Wavelength(float nm)
    {
        float r, g, b;
        if (nm < 440f) { r = (440f - nm) / 60f; g = 0f; b = 1f; }
        else if (nm < 490f) { r = 0f; g = (nm - 440f) / 50f; b = 1f; }
        else if (nm < 510f) { r = 0f; g = 1f; b = (510f - nm) / 20f; }
        else if (nm < 580f) { r = (nm - 510f) / 70f; g = 1f; b = 0f; }
        else if (nm < 645f) { r = 1f; g = (645f - nm) / 65f; b = 0f; }
        else { r = 1f; g = 0f; b = 0f; }
        // Dimmer at the ends of the eye's range.
        float edge = nm < 420f ? 0.3f + 0.7f * (nm - 380f) / 40f : nm > 660f ? 0.3f + 0.7f * (700f - nm) / 40f : 1f;
        return new Color(r * edge, g * edge, b * edge, 1f);
    }

    static Vector3 PointAt(Ray r, float s)
    {
        for (int i = 1; i < r.at.Length; i++)
            if (s <= r.at[i])
            {
                float span = r.at[i] - r.at[i - 1];
                return Vector3.Lerp(r.points[i - 1], r.points[i], span > 0f ? (s - r.at[i - 1]) / span : 1f);
            }
        return r.points[r.points.Length - 1];
    }

    void ApplyFlashes()
    {
        if (_block == null) _block = new MaterialPropertyBlock();
        Flash(primaryMirror, mirrorSlot, mirrorFlash * _flash[0]);
        Flash(secondaryMirror, mirrorSlot, mirrorFlash * _flash[1]);
        Flash(tertiaryMirror, mirrorSlot, mirrorFlash * _flash[2]);
        Flash(instrument, 0, instrumentGlow * _flash[3]);
    }

    void Flash(Renderer target, int slot, Color emission)
    {
        if (target == null) return;
        slot = Mathf.Clamp(slot, 0, target.sharedMaterials.Length - 1);
        if (_emissionId < 0) _emissionId = Shader.PropertyToID(emissionProperty);
        _block.Clear();
        _block.SetColor(_emissionId, emission);
        target.SetPropertyBlock(_block, slot);
    }

    /// <summary>Lays out every ray's path through the optics, in the tube's space.</summary>
    void Build()
    {
        if (_material == null)
        {
            Shader shader = Shader.Find("Custom/MagellanLight_NEW");
            _material = new Material(shader != null ? shader : Shader.Find("Sprites/Default")) { name = "Magellan light (runtime)" };
        }
        _material.SetColor("_Color", colour);
        _material.SetFloat("_Occluded", occluded);

        while (_rays.Count > rays)
        {
            Ray last = _rays[_rays.Count - 1];
            if (last.line != null) Destroy(last.line.gameObject);
            _rays.RemoveAt(_rays.Count - 1);
        }
        while (_rays.Count < rays) _rays.Add(new Ray());

        Vector3 primeFocus = new Vector3(0f, primaryVertexY + focalLength, 0f);
        // The f/11 focus as if the tertiary were not there: straight on down the axis.
        Vector3 unfolded = new Vector3(0f, secondaryVertexY - secondaryToFocus, 0f);
        float rimY = primaryVertexY + primaryRadius * primaryRadius / (4f * focalLength);

        for (int i = 0; i < rays; i++)
        {
            Ray r = _rays[i];
            float angle = (i + 0.5f) / rays * Mathf.PI * 2f;
            float radius = primaryRadius * (i % 2 == 0 ? outerRadius : innerRadius);
            var across = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

            Vector3 onPrimary = across + Vector3.up * (primaryVertexY + radius * radius / (4f * focalLength));
            Vector3 start = across + Vector3.up * (rimY + startAbove);
            Vector3 onSecondary = Vector3.Lerp(onPrimary, primeFocus,
                                               (secondaryVertexY - onPrimary.y) / (primeFocus.y - onPrimary.y));
            Vector3 onTertiary = Vector3.Lerp(onSecondary, unfolded,
                                              (onSecondary.y - tertiaryCentre.y) / (onSecondary.y - unfolded.y));
            onTertiary.y = tertiaryCentre.y;

            r.points = new[] { start, onPrimary, onSecondary, onTertiary, focus };
            r.at = new float[r.points.Length];
            for (int k = 1; k < r.points.Length; k++)
                r.at[k] = r.at[k - 1] + Vector3.Distance(r.points[k - 1], r.points[k]);

            if (r.line == null)
            {
                var go = new GameObject("Ray " + i);
                go.layer = tube.gameObject.layer;
                go.transform.SetParent(tube, false);
                r.line = go.AddComponent<LineRenderer>();
                r.line.useWorldSpace = false;
                r.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.line.receiveShadows = false;
                r.line.numCapVertices = 2;
                r.line.numCornerVertices = 2;
            }
            r.line.sharedMaterial = _material;
            r.line.widthMultiplier = width * tube.lossyScale.x;
            r.line.positionCount = 0;
        }

        // The fan of colours at the focus: its own material, seen through the instrument's walls.
        if (_fanMaterial == null)
        {
            _fanMaterial = new Material(_material.shader) { name = "Magellan spectrum (runtime)" };
        }
        _fanMaterial.SetColor("_Color", Color.white);
        _fanMaterial.SetFloat("_Occluded", fanThroughWalls);
        int wanted = disperse ? fanRays : 0;
        while (_fan.Count > wanted)
        {
            if (_fan[_fan.Count - 1] != null) Destroy(_fan[_fan.Count - 1].gameObject);
            _fan.RemoveAt(_fan.Count - 1);
        }
        while (_fan.Count < wanted)
        {
            var go = new GameObject("Spectrum " + _fan.Count);
            go.layer = tube.gameObject.layer;
            go.transform.SetParent(tube, false);
            var l = go.AddComponent<LineRenderer>();
            l.useWorldSpace = false;
            l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            l.receiveShadows = false;
            l.numCapVertices = 2;
            _fan.Add(l);
        }
        foreach (LineRenderer l in _fan)
        {
            l.sharedMaterial = _fanMaterial;
            l.positionCount = 0;
        }
    }
}

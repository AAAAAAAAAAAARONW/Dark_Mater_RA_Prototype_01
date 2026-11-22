using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Draws the light's trail ribbon itself, with a steady facing, so it stops twitching left and
/// right when nothing is touched.
///
/// WHY IT TWITCHED. The TrailRenderer is View-aligned: Unity turns each point of the ribbon to
/// the camera on its own, its width along (trail direction × direction to the camera). The
/// orbit camera sits behind the light, so the trail runs back almost straight at it — those two
/// directions are nearly the same, their cross product is tiny, and its direction swings a long
/// way for a camera that moves a few millimetres. The camera always moves a little (its follow
/// damping against an uneven frame time), so the ribbon turned with it. The spectrum runs
/// across the ribbon, so every turn showed as the colours sliding sideways.
///
/// FIRST TRY, AND WHY NOT. Switching the TrailRenderer to TransformZ and turning its transform:
/// Unity then built the trail with NaN vertices (the tether's laid-in points, most likely), and
/// every BakeMesh of it — AbsorberHighlight_NEW's included — logged abnormal bounds each frame.
///
/// NOW. The TrailRenderer stays exactly as it was, View-aligned, recording points, and every
/// script that reads it (QuasarTether_NEW, AbsorberHighlight_NEW, TrailRibbon_NEW) sees what it
/// always saw. Only its drawing is switched off (forceRenderingOff); this draws the same ribbon
/// from the same points, with the same material and property block, uv (x along, 0 at the head;
/// y across) and vertex colours.
///
/// SECOND TRY, AND WHY NOT. One steady facing for the whole ribbon (the direction from the light
/// to the camera, square to the flight). Seen from the orbit camera, about 4 degrees above the
/// line of flight, that laid the ribbon flat like a sheet of paper running under the camera.
///
/// NOW. Every point faces the camera again, as View alignment does, but toward the camera's
/// position relative to the light EASED over facingSeconds: the small shakes that the
/// ill-conditioned cross product amplified are gone, real moves of the view are followed.
/// Where a step points almost straight at the camera the point keeps its neighbour's
/// direction instead of swinging. Zero-length steps are skipped, so nothing divides by zero.
///
/// SAME SIDE AS BEFORE. Which edge carries uv.y = 0 is checked once against the TrailRenderer's
/// own baked ribbon, and turned round if it disagrees — the colours stay on the side people
/// are used to, and the arrows that read the baked edges still point the right way.
///
/// BEAM, NOT A SHEET. With beam on (the default) each point gets a ring of vertices instead of
/// two: a real round tube, capped with a half sphere at the head, seen with the same thickness
/// from every side. PhotonTrailRainbow's _Beam mode works out the spectrum coordinate (uv.y)
/// per vertex from the view (the tube's side as seen on screen, left to right), so the colours
/// read across the beam in the same order as across the ribbon, and the shader's existing edge,
/// glow and tube shading (which all go by |2 uv.y - 1|) become "how thin the light is along this
/// sight line". Back faces are culled so nothing is added twice. Off = the flat ribbon, for A/B.
///
/// TrailLook_NEW draws its halo with the same builder (Build), at a wider width.
///
/// NOTHING TO WIRE. It adds itself at run time to the PhotonSpectrumTrail under a PlayerRig_NEW
/// (the journey scenes; the tutorial has no PlayerRig_NEW and is left alone).
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public class TrailFacing_NEW : MonoBehaviour
{
    [Tooltip("Seconds over which the facing follows the camera. Higher = steadier, slower to turn when the view is swung.")]
    [Range(0.02f, 1f)] [SerializeField] float facingSeconds = 0.25f;

    [Tooltip("The camera's sideways offset from the line of flight, as a share of its distance, below which the facing is held rather than followed.")]
    [Range(0f, 0.5f)] [SerializeField] float holdBelow = 0.05f;

    [Tooltip("Turn the ribbon round (the spectrum's ends swap sides). Set by the check at the start; change only if that check got it wrong.")]
    [SerializeField] bool flip;

    [Tooltip("A round beam (a tube) instead of a flat ribbon facing the camera.")]
    [SerializeField] bool beam = false;

    [Tooltip("Ribbon width, in trail widths. The soft beam fades to nothing over its outer fifth each side.")]
    [Range(0.5f, 2f)] [SerializeField] float ribbonWidth = 1.0f;

    [Tooltip("How far either way along the trail, in trail widths, the direction of travel is averaged over. Small jogs in the recorded points shorter than this no longer twist the ribbon.")]
    [Range(0.05f, 3f)] [SerializeField] float tangentWindow = 0.6f;

    [Tooltip("The ribbon widens towards the tail by this share of its width (the light dissipating). Pairs with the soft beam's _SoftTail.")]
    [Range(0f, 1.5f)] [SerializeField] float tailSpread = 0.3f;

    [Tooltip("Vertices around the beam. 16 reads as round at the sizes the trail is seen at.")]
    [Range(6, 24)] [SerializeField] int radialSegments = 16;

    static readonly int BeamId = Shader.PropertyToID("_Beam");
    static readonly int BeamSideId = Shader.PropertyToID("_BeamSide");
    static readonly int CullId = Shader.PropertyToID("_Cull");
    static readonly int BeamHeadFadeId = Shader.PropertyToID("_BeamHeadFade");

    TrailRenderer _trail;
    PlayerRig_NEW _player;
    AnimationCurve _widthCurve;
    Gradient _gradient;

    MeshRenderer _renderer;
    Mesh _mesh;
    MaterialPropertyBlock _block;
    Mesh _scratch;

    Vector3 _facing;
    Vector3 _eye, _eyeOffset, _eyeUp;
    bool _upFlip;      // camera below the light: its up gives the other side; kept from the last clear view

    [Tooltip("Keep the spectrum's two sides on the same sides of the screen whichever way the camera looks: " +
             "seen from in front, the light's left is the screen's right, and the colours swapped over. On in " +
             "PC_Tutorial (whose camera turns right round); the journey mirrors its spectrum for this itself (AbsorberHighlight_NEW).")]
    [SerializeField] bool keepScreenSide = false;
    int _screenRef;    // which way the head's side pointed on screen when first seen clearly: +1 right, -1 left
    bool _screenFlip;
    bool _hasEye;
    Vector3 _lastHead;
    bool _checked, _ready;

    // This frame's ribbon, head first.
    Vector3[] _raw = new Vector3[0];
    readonly List<Vector3> _p = new List<Vector3>(512);
    readonly List<Vector3> _side = new List<Vector3>(512);
    readonly List<Vector3> _tan = new List<Vector3>(512);
    readonly List<float> _u = new List<float>(512);
    readonly List<float> _w = new List<float>(512);
    readonly List<Color> _c = new List<Color>(512);

    readonly List<Vector3> _verts = new List<Vector3>(1024);
    readonly List<Vector2> _uvs = new List<Vector2>(1024);
    readonly List<Color> _cols = new List<Color>(1024);
    readonly List<Vector3> _norms = new List<Vector3>(4096);
    readonly List<Vector4> _tans = new List<Vector4>(4096);
    readonly List<int> _tris = new List<int>(3072);

    /// <summary>Total length of this frame's ribbon, world units.</summary>
    public float Length { get; private set; }

    /// <summary>Points in this frame's ribbon (0 when there is nothing to draw).</summary>
    public int Count => _p.Count;

    /// <summary>True when the trail is drawn as a round beam (and Build makes tubes).</summary>
    public bool Beam => beam;

    /// <summary>The trail's width curve as this draws it. TrailLook_NEW sets its taper here and on the trail.</summary>
    public void SetWidthCurve(AnimationCurve curve) { if (curve != null) _widthCurve = curve; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Install();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { Install(); }

    static void Install()
    {
        PhotonSpectrumTrail spectrum = FindTrail();
        if (spectrum == null || spectrum.GetComponent<TrailFacing_NEW>() != null) return;
        spectrum.gameObject.AddComponent<TrailFacing_NEW>();
    }

    /// <summary>True in the tutorial scene that gets the new trail look (PC_Tutorial).</summary>
    public static bool IsTutorialScene => SceneManager.GetActiveScene().name == "PC_Tutorial";

    /// <summary>
    /// The light's trail in a scene that gets the new look: Vizlab_Journey and PC_Journey (under
    /// the PlayerRig_NEW) and PC_Tutorial (wherever it is; it starts switched off). Every other
    /// scene (Test_Journey, Vizlab_tut...) keeps the trail as it was: null.
    /// </summary>
    public static PhotonSpectrumTrail FindTrail()
    {
        string scene = SceneManager.GetActiveScene().name;
        if (scene == "Vizlab_Journey" || scene == "PC_Journey")
        {
            PlayerRig_NEW player = FindObjectOfType<PlayerRig_NEW>();
            return player != null ? player.GetComponentInChildren<PhotonSpectrumTrail>(true) : null;
        }
        if (scene == "PC_Tutorial")
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                PhotonSpectrumTrail t = root.GetComponentInChildren<PhotonSpectrumTrail>(true);
                if (t != null) return t;
            }
        }
        return null;
    }

    void Start()
    {
        // Start, not Awake: PhotonSpectrumTrail has set its width curve and gradient by now.
        _trail = GetComponent<TrailRenderer>();
        _player = GetComponentInParent<PlayerRig_NEW>();
        if (_trail == null) { enabled = false; return; }

        if (_widthCurve == null) _widthCurve = _trail.widthCurve;
        _gradient = _trail.colorGradient;
        _block = new MaterialPropertyBlock();
        keepScreenSide |= IsTutorialScene;
        _mesh = NewMesh("Trail ribbon (stable)");
        _renderer = MakeRenderer("Trail Ribbon (stable)", _mesh);
        _lastHead = transform.position;
        _ready = true;
    }

    void OnEnable()
    {
        if (_ready && _trail != null) _trail.forceRenderingOff = _checked;
    }

    void OnDisable()
    {
        SetBeamOnMaterial(false);
        if (_trail != null) _trail.forceRenderingOff = false;
        if (_renderer != null) _renderer.enabled = false;
    }

    void OnDestroy()
    {
        if (_mesh != null) Destroy(_mesh);
        if (_scratch != null) Destroy(_scratch);
    }

    /// <summary>A mesh renderer child with no shadows or probes, drawing in the trail's local space.</summary>
    public MeshRenderer MakeRenderer(string name, Mesh mesh)
    {
        var go = new GameObject(name) { layer = gameObject.layer };
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        r.enabled = false;
        return r;
    }

    public static Mesh NewMesh(string name)
    {
        var m = new Mesh { name = name };
        m.MarkDynamic();
        return m;
    }

    void LateUpdate()
    {
        if (!_ready) return;
        Camera cam = Camera.main;
        Vector3 head = transform.position;
        Vector3 travel = Travel(head);

        Gather(head, travel);

        bool visible = _trail.enabled && _p.Count >= 2 && cam != null;
        if (!visible)
        {
            _side.Clear();
            _renderer.enabled = false;
            return;
        }

        // The camera as seen from the light, eased: its small shakes are gone, its real moves
        // (the orbit turned, a recentre) are followed within a few tenths of a second. Held
        // relative to the light, so the flight itself adds no lag.
        Vector3 offset = cam.transform.position - head;
        if (!_hasEye) { _eyeOffset = offset; _hasEye = true; }
        else _eyeOffset = Vector3.Lerp(_eyeOffset, offset, 1f - Mathf.Exp(-Time.deltaTime / facingSeconds));
        _eye = head + _eyeOffset;
        // The camera's own up, eased the same way: what the ribbon turns by when it is seen end on.
        _eyeUp = _eyeUp == Vector3.zero ? cam.transform.up
               : Vector3.Slerp(_eyeUp, cam.transform.up, 1f - Mathf.Exp(-Time.deltaTime / facingSeconds));

        Vector3 target = Sideways(_eyeOffset, travel);
        if (_facing == Vector3.zero) _facing = target != Vector3.zero ? target : Perpendicular(travel);
        else if (target != Vector3.zero) _facing = target;

        Sides();

        if (!_checked)
        {
            // Until the side is known the TrailRenderer keeps drawing; then this takes over.
            if (_p.Count < 4 || !CheckSide(cam)) { _renderer.enabled = false; return; }
            _checked = true;
            _trail.forceRenderingOff = true;
        }

        if (keepScreenSide) KeepScreenSide(cam);

        SetBeamOnMaterial(beam);
        Build(_mesh, beam ? 1f : ribbonWidth);
        _renderer.sharedMaterials = _trail.sharedMaterials;
        _trail.GetPropertyBlock(_block);
        _renderer.SetPropertyBlock(_block);
        _renderer.enabled = true;
    }

    /// <summary>The trail's points, head first, with zero-length steps dropped, and their lengths, widths, colours.</summary>
    void Gather(Vector3 head, Vector3 travel)
    {
        _p.Clear(); _u.Clear(); _w.Clear(); _c.Clear();
        int count = _trail.positionCount;
        if (_raw.Length < count) _raw = new Vector3[Mathf.NextPowerOfTwo(count)];
        count = _trail.GetPositions(_raw);

        _p.Add(head);
        for (int i = count - 1; i >= 0; i--)
            if ((_raw[i] - _p[_p.Count - 1]).sqrMagnitude > 1e-8f) _p.Add(_raw[i]);

        float length = 0f;
        _u.Add(0f);
        for (int i = 1; i < _p.Count; i++) { length += (_p[i] - _p[i - 1]).magnitude; _u.Add(length); }
        Length = Mathf.Max(length, 1e-4f);

        float multiplier = _trail.widthMultiplier;
        for (int i = 0; i < _p.Count; i++)
        {
            float u = _u[i] / Length;
            _u[i] = u;
            _w.Add(multiplier * _widthCurve.Evaluate(u));
            _c.Add(_gradient.Evaluate(u));
        }
    }

    /// <summary>
    /// Each point's width direction: across its own step, turned to the eased camera — every
    /// point faces the viewer, as the TrailRenderer's View alignment did, but toward a camera
    /// that does not shake. Where a step points straight at the camera (nothing to turn by)
    /// the point keeps its neighbour's direction.
    /// </summary>
    void Sides()
    {
        _side.Clear(); _tan.Clear();
        float sign = flip ? -1f : 1f;
        Vector3 last = Vector3.zero;
        int n = _p.Count;

        // The direction of travel at each point is taken over a stretch of the trail, not from
        // its two neighbours. The points are 0.05 apart, so a jog of a few millimetres (the
        // light placed at the start, a brush with a collider) turned a whole step sideways, the
        // ribbon twisted round with it, and the light showed a fold just behind its head.
        float window = tangentWindow * (_w.Count > 0 && _w[0] > 1e-4f ? _w[0] : 1f);
        int ahead = 0, behind = 0;
        for (int i = 0; i < n; i++)
        {
            float here = _u[i] * Length;
            while (ahead < i && here - _u[ahead] * Length > window) ahead++;
            if (behind < i) behind = i;
            while (behind < n - 1 && _u[behind + 1] * Length - here <= window) behind++;
            Vector3 along = _p[ahead] - _p[behind];
            if (along.sqrMagnitude < 1e-12f) along = _p[Mathf.Max(0, i - 1)] - _p[Mathf.Min(n - 1, i + 1)];
            float al = along.magnitude;
            _tan.Add(al > 1e-9f ? along / al : (_tan.Count > 0 ? _tan[_tan.Count - 1] : Vector3.forward));
            Vector3 toEye = _eye - _p[i];
            Vector3 s = Vector3.Cross(along, toEye);
            float m = s.magnitude;
            // Seen nearly end on (the tutorial's camera rides right behind the light), the side
            // from (along x to-eye) swings wildly for the smallest move of the camera. There it
            // turns instead by the camera's own up — the side it would have with the camera a
            // little above, which is the same side, so nothing flips — blending over 9° to 30°.
            float sinView = m / Mathf.Max(1e-9f, along.magnitude * toEye.magnitude);
            Vector3 byUp = Vector3.Cross(along, _eyeUp);
            float upM = byUp.magnitude;
            if (upM > 1e-6f * along.magnitude && sinView < 0.5f)
            {
                Vector3 sView = m > 1e-9f ? s / m : Vector3.zero;
                if (sView != Vector3.zero && sinView > 0.05f) _upFlip = Vector3.Dot(sView, byUp) < 0f;
                if (_upFlip) byUp = -byUp;
                float w = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.5f, sinView));
                s = Vector3.Lerp(byUp / upM, sView, w);
                m = s.magnitude;
            }
            if (m > 0.02f * along.magnitude * toEye.magnitude && m > 1e-9f || (sinView < 0.5f && m > 1e-6f)) s = s * (sign / m);
            else if (last != Vector3.zero) s = last;
            else
            {
                s = Vector3.Cross(along, _facing);
                s = s.sqrMagnitude > 1e-12f ? s.normalized * sign : Perpendicular(_facing);
            }
            _side.Add(s);
            last = s;
        }
    }

    /// <summary>
    /// Turns the whole ribbon round when the head's side points the other way on screen from how it
    /// was first seen, so the colours never swap sides as the camera looks back. With a margin
    /// either way, so a view from straight above does not flicker between the two.
    /// </summary>
    void KeepScreenSide(Camera cam)
    {
        int k = Mathf.Min(_side.Count, 8);
        if (k == 0) return;
        Vector3 head = Vector3.zero;
        for (int i = 0; i < k; i++) head += _side[i];
        float d = Vector3.Dot(head.normalized, cam.transform.right);
        if (_screenRef == 0) { if (Mathf.Abs(d) > 0.3f) _screenRef = d > 0f ? 1 : -1; return; }
        float along = d * _screenRef;
        if (!_screenFlip && along < -0.15f) _screenFlip = true;
        else if (_screenFlip && along > 0.15f) _screenFlip = false;
        if (_screenFlip)
            for (int i = 0; i < _side.Count; i++) _side[i] = -_side[i];
    }

    /// <summary>
    /// Builds this frame's ribbon into mesh, widthScale times the trail's width, in the trail's
    /// local space (for a renderer that is a child of it with no offset).
    /// </summary>
    public void Build(Mesh mesh, float widthScale)
    {
        if (beam) { BuildTube(mesh, widthScale); return; }
        _verts.Clear(); _uvs.Clear(); _cols.Clear(); _tris.Clear();
        int n = _p.Count;
        if (_side.Count != n || _w.Count != n) { mesh.Clear(); return; }   // nothing gathered this frame
        for (int i = 0; i < n; i++)
        {
            Vector3 half = _side[i] * (_w[i] * widthScale * 0.5f * (1f + tailSpread * _u[i]));
            _verts.Add(transform.InverseTransformPoint(_p[i] - half));
            _verts.Add(transform.InverseTransformPoint(_p[i] + half));
            _uvs.Add(new Vector2(_u[i], 0f));
            _uvs.Add(new Vector2(_u[i], 1f));
            _cols.Add(_c[i]);
            _cols.Add(_c[i]);
            if (i > 0)
            {
                int a = (i - 1) * 2;
                _tris.Add(a); _tris.Add(a + 2); _tris.Add(a + 1);
                _tris.Add(a + 1); _tris.Add(a + 2); _tris.Add(a + 3);
            }
        }
        mesh.Clear();
        mesh.SetVertices(_verts);
        mesh.SetUVs(0, _uvs);
        mesh.SetColors(_cols);
        mesh.SetTriangles(_tris, 0);
        mesh.RecalculateBounds();
    }

    /// <summary>
    /// The beam: a ring of radialSegments vertices round each point, its frame carried along
    /// the trail so it never twists, the radius rounded to a half sphere over the last half
    /// width at the head. Normals point out of the tube; the tangent is the step towards the
    /// head (the same one the ribbon's side was made from, so the shader's left/right agrees
    /// with CheckSide). uv.y is left at 0.5: the shader works it out from the view.
    /// </summary>
    void BuildTube(Mesh mesh, float widthScale)
    {
        _verts.Clear(); _uvs.Clear(); _cols.Clear(); _tris.Clear(); _norms.Clear(); _tans.Clear();
        int n = _p.Count, R = radialSegments;
        if (_tan.Count != n || _w.Count != n) { mesh.Clear(); return; }

        Vector3 n1 = Perpendicular(_tan[0]);
        float headWidth = _w[0] > 1e-4f ? _w[0] : 1f;
        for (int i = 0; i < n; i++)
        {
            Vector3 t = _tan[i];
            Vector3 carried = n1 - Vector3.Dot(n1, t) * t;     // parallel transport: no twist
            n1 = carried.sqrMagnitude > 1e-8f ? carried.normalized : Perpendicular(t);
            Vector3 n2 = Vector3.Cross(t, n1);

            float r = _w[i] * widthScale * 0.5f;
            float fromHead = _u[i] * Length, cap = headWidth * 0.5f;
            if (fromHead < cap) { float k = 1f - fromHead / cap; r *= Mathf.Sqrt(Mathf.Max(0f, 1f - k * k)); }
            r = Mathf.Max(r, 1e-4f);

            Vector3 lt = transform.InverseTransformDirection(t);
            for (int q = 0; q < R; q++)
            {
                float a = q * (Mathf.PI * 2f / R);
                Vector3 dir = n1 * Mathf.Cos(a) + n2 * Mathf.Sin(a);
                _verts.Add(transform.InverseTransformPoint(_p[i] + dir * r));
                _norms.Add(transform.InverseTransformDirection(dir));
                _tans.Add(new Vector4(lt.x, lt.y, lt.z, 1f));
                _uvs.Add(new Vector2(_u[i], 0.5f));
                _cols.Add(_c[i]);
            }
            if (i > 0)
            {
                int ring = (i - 1) * R;
                for (int q = 0; q < R; q++)
                {
                    int a = ring + q, b = ring + (q + 1) % R, a2 = a + R, b2 = b + R;
                    // Clockwise seen from outside: Unity's front faces.
                    _tris.Add(a); _tris.Add(a2); _tris.Add(b);
                    _tris.Add(b); _tris.Add(a2); _tris.Add(b2);
                }
            }
        }
        mesh.Clear();
        mesh.SetVertices(_verts);
        mesh.SetNormals(_norms);
        mesh.SetTangents(_tans);
        mesh.SetUVs(0, _uvs);
        mesh.SetColors(_cols);
        mesh.SetTriangles(_tris, 0);
        mesh.RecalculateBounds();
    }

    /// <summary>Beam mode and back-face culling on the trail's material, with the checked side.</summary>
    void SetBeamOnMaterial(bool on)
    {
        if (_trail == null) return;
        Material mat = _trail.sharedMaterial;
        if (mat == null || !mat.HasProperty(BeamId)) return;
        mat.SetFloat(BeamId, on ? 1f : 0f);
        mat.SetFloat(BeamSideId, flip ? -1f : 1f);
        mat.SetFloat(CullId, on ? (float)UnityEngine.Rendering.CullMode.Back : (float)UnityEngine.Rendering.CullMode.Off);
        // The tip fades in over three quarters of the beam's width, so it is soft, not a polygon.
        float headWidth = _w.Count > 0 ? _w[0] : _trail.widthMultiplier;
        mat.SetFloat(BeamHeadFadeId, on ? Mathf.Clamp01(0.75f * headWidth / Mathf.Max(Length, 1e-4f)) : 0f);
    }

    /// <summary>
    /// Bakes the TrailRenderer's own ribbon and compares which screen side its uv.y = 0 edge is
    /// on with this one's; turns this one round if they disagree. False if it could not tell.
    /// </summary>
    bool CheckSide(Camera cam)
    {
        if (_scratch == null) _scratch = new Mesh { name = "TrailFacing scratch" };
        int mid = _p.Count / 2;
        if (!TrailRibbon_NEW.Edges(_trail, cam, _scratch, _p[mid], out Vector3 b0, out Vector3 b1)) return false;
        Vector3 right = cam.transform.right;
        float theirs = Vector3.Dot(b1 - b0, right);
        float ours = Vector3.Dot(_side[mid], right);     // edge1 - edge0 runs along +side
        if (Mathf.Abs(theirs) < 1e-5f || Mathf.Abs(ours) < 1e-5f) return false;
        if (Mathf.Sign(theirs) != Mathf.Sign(ours))
        {
            flip = !flip;
            Sides();
        }
        return true;
    }

    Vector3 Travel(Vector3 head)
    {
        Vector3 d = _player != null ? _player.MovementDirection : head - _lastHead;
        _lastHead = head;
        return d.sqrMagnitude > 1e-8f ? d.normalized : Vector3.forward;
    }

    /// <summary>v with the part along travel taken out, normalised; zero when too little is left.</summary>
    Vector3 Sideways(Vector3 v, Vector3 travel)
    {
        float length = v.magnitude;
        if (length < 1e-5f) return Vector3.zero;
        Vector3 side = v - Vector3.Dot(v, travel) * travel;
        float s = side.magnitude;
        return s > holdBelow * length && s > 1e-5f ? side / s : Vector3.zero;
    }

    static Vector3 Perpendicular(Vector3 v)
    {
        Vector3 p = Vector3.Cross(v, Vector3.up);
        if (p.sqrMagnitude < 1e-6f) p = Vector3.Cross(v, Vector3.right);
        return p.normalized;
    }
}

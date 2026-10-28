using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Puts the HUD onto the Vizlab's seven rows when the piece runs on the wall. Lives on the
/// "[getReal3D]" object beside GetReal3DView_NEW; GetReal3DHudDraw_NEW on the eye camera
/// does the drawing. GameObject > Vizlab > Adapt Tutorial Camera to getReal3D sets both up.
///
/// WHY. The HUD is a Screen Space Overlay canvas, and an overlay canvas is drawn into
/// whatever window the game has. On the wall the game is several copies, each with a window
/// covering its own part of the surface, so each would draw the whole HUD squashed into its
/// part - the HUD repeated across the wall instead of laid across it. The VizLab guide's
/// answer is world space UI.
///
/// HOW, WITHOUT TOUCHING THE LAYOUT. The HUD canvas is already the wall, unrolled: 1371 x
/// 1080 is the surface's 1.27 : 1, each seventh of its height is a row, and VizlabRowAnchor
/// and the rest of the Display folder lay UI out against exactly that. So on the wall:
///
///   1. The canvas switches to Screen Space Camera and renders into a texture of the same
///      shape, through a camera nobody sees. Same shape means the Canvas Scaler produces the
///      same layout, so no HUD script notices anything.
///   2. That texture is laid onto seven quads that sit exactly where the seven physical
///      panels are, in getReal3D's room space (VizlabDisplay_NEW's geometry), row n of the
///      texture on panel n. They are children of this object, so they ride with the eye.
///
/// Because the quads ARE the screens, the HUD lands on the glass with no perspective, no
/// stretch and no stereo offset - the picture the flat layout promised, in the place it
/// promised. GetReal3DHudDraw_NEW draws them after post-processing, so motion blur, bloom
/// and grading leave the HUD alone, and without the zoom, so it stays put while the world
/// magnifies.
///
/// WHEN. Only on the wall (InputScheme_NEW.RunningAsSeveralCopies), by default. In the
/// editor there is one window and the overlay is already right, and the editor's getReal3D
/// config is a 53 cm monitor that would show only a corner of the rows anyway. Set mode to
/// Always to look at the arrangement from the editor.
///
/// WHAT IS ASSUMED, unverified until the wall: getReal3D's room space is the display file's
/// with forward flipped to Unity's +Z (the same assumption the eye makes), and the rows
/// sit where VizlabDisplay_NEW reconstructs them. If the HUD is upside down or behind you on
/// site, flipForward is the switch; if it is offset, VizlabDisplay_NEW.Rows is the data.
/// </summary>
[DisallowMultipleComponent]
[HierarchyBadge_NEW("WALL HUD", "#4A7BD0")]
public class GetReal3DHud_NEW : MonoBehaviour
{
    public enum Mode
    {
        /// <summary>On the rows when running as several copies (the wall); overlay otherwise.</summary>
        Auto,
        /// <summary>Always on the rows. For looking at the arrangement from the editor.</summary>
        Always,
        /// <summary>Never; the canvases stay as they are.</summary>
        Never
    }

    [Tooltip("Auto puts the HUD on the rows only on the wall. In the editor the overlay is " +
             "already right; Always shows the rows there for checking.")]
    [SerializeField] Mode mode = Mode.Auto;

    [Tooltip("The overlay canvases to move onto the wall. The tutorial has one, HUD.")]
    [SerializeField] Canvas[] canvases;

    [Tooltip("Height of the texture the HUD renders into, in pixels; the width follows the " +
             "wall's shape. 2160 is two pixels per row-pixel of the 1080 layout - soft beside " +
             "the wall's real 7560, and 47 MB rather than 190. Raise it if text reads soft and " +
             "the frame rate can afford it.")]
    [SerializeField] int textureHeight = 2160;

    [Tooltip("The display file has -Z forward, Unity +Z. On: rows are placed with Z negated, " +
             "which is what puts them in front of the eye. Off only if getReal3D turns out to " +
             "use the file's axes unchanged.")]
    [SerializeField] bool flipForward = true;

    [SerializeField] Shader shader;

    /// <summary>The one in force, or null. There is one eye and one wall.</summary>
    public static GetReal3DHud_NEW Active { get; private set; }

    /// <summary>True while the HUD is on the rows rather than in an overlay.</summary>
    public static bool OnWall { get { return Active != null; } }

    /// <summary>The seven quads, in this object's space. Drawn by GetReal3DHudDraw_NEW.</summary>
    public Mesh WallMesh { get; private set; }

    public Material WallMaterial { get; private set; }

    struct Saved
    {
        public Canvas canvas;
        public RenderMode renderMode;
        public Camera worldCamera;
        public float planeDistance;
    }

    readonly List<Saved> _saved = new List<Saved>();
    RenderTexture _texture;
    Camera _hudCamera;

    // Room-space panels, Unity axes, metres, for TryWorldToCanvas.
    readonly Vector3[] _centre = new Vector3[VizlabDisplay_NEW.RowCount];
    readonly Vector3[] _up = new Vector3[VizlabDisplay_NEW.RowCount];

    // Far from anything the tutorial puts in the world, with clip planes that see only the
    // canvas a metre in front: the HUD camera renders the HUD and nothing else.
    static readonly Vector3 HudCameraPosition = new Vector3(0f, -4000f, 0f);

    void OnEnable()
    {
        bool wall = mode == Mode.Always || (mode == Mode.Auto && InputScheme_NEW.RunningAsSeveralCopies);
        if (!wall) return;

        if (shader == null) shader = Shader.Find("Hidden/Journey/GetReal3DWallHud_NEW");
        if (shader == null)
        {
            Debug.LogError("[GetReal3DHud_NEW] The wall HUD shader is missing, so the HUD stays an overlay. " +
                           "Re-run GameObject > Vizlab > Adapt Tutorial Camera to getReal3D.", this);
            return;
        }

        BuildTexture();
        BuildCamera();
        BuildMesh();
        WallMaterial = new Material(shader) { name = "GetReal3DWallHud", mainTexture = _texture, hideFlags = HideFlags.DontSave };

        MoveCanvasesOntoTexture();
        Active = this;
    }

    void OnDisable()
    {
        if (Active == this) Active = null;

        foreach (Saved s in _saved)
        {
            if (s.canvas == null) continue;
            s.canvas.renderMode = s.renderMode;
            s.canvas.worldCamera = s.worldCamera;
            s.canvas.planeDistance = s.planeDistance;
        }
        _saved.Clear();

        if (_hudCamera != null) Destroy(_hudCamera.gameObject);
        if (_texture != null) { _texture.Release(); Destroy(_texture); }
        if (WallMesh != null) Destroy(WallMesh);
        if (WallMaterial != null) Destroy(WallMaterial);
        _hudCamera = null;
        _texture = null;
        WallMesh = null;
        WallMaterial = null;
    }

    void BuildTexture()
    {
        int h = Mathf.Max(256, textureHeight);
        int w = Mathf.RoundToInt(h * (float)VizlabDisplay_NEW.CanvasWidthPx / VizlabDisplay_NEW.CanvasHeightPx);

        // 24-bit depth carries the 8-bit stencil that uGUI masks need.
        _texture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32)
        {
            name = "GetReal3DWallHud",
            useMipMap = false,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        _texture.Create();
    }

    void BuildCamera()
    {
        var go = new GameObject("GetReal3DHud_Camera") { hideFlags = HideFlags.HideAndDontSave };
        go.transform.position = HudCameraPosition;
        go.transform.rotation = Quaternion.identity;

        _hudCamera = go.AddComponent<Camera>();
        _hudCamera.clearFlags = CameraClearFlags.SolidColor;
        _hudCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        _hudCamera.nearClipPlane = 0.5f;
        _hudCamera.farClipPlane = 1.5f;
        _hudCamera.allowHDR = false;
        _hudCamera.allowMSAA = false;
        _hudCamera.useOcclusionCulling = false;
        _hudCamera.targetTexture = _texture;

        // Before every eye, so the eyes sample this frame's HUD, not last frame's.
        _hudCamera.depth = -100f;

        int mask = 1 << 5;   // UI
        if (canvases != null)
            foreach (Canvas c in canvases)
                if (c != null) mask |= 1 << c.gameObject.layer;
        _hudCamera.cullingMask = mask;
    }

    void MoveCanvasesOntoTexture()
    {
        if (canvases == null) return;

        foreach (Canvas c in canvases)
        {
            if (c == null || !c.isRootCanvas) continue;

            _saved.Add(new Saved
            {
                canvas = c,
                renderMode = c.renderMode,
                worldCamera = c.worldCamera,
                planeDistance = c.planeDistance
            });

            c.renderMode = RenderMode.ScreenSpaceCamera;
            c.worldCamera = _hudCamera;
            c.planeDistance = 1f;
        }
    }

    /// <summary>
    /// Seven quads, 28 vertices, one draw. Row n of the texture - the nth seventh down - on
    /// panel n, left edge of the texture on the left end of the panel.
    /// </summary>
    void BuildMesh()
    {
        int rows = VizlabDisplay_NEW.RowCount;
        var vertices = new Vector3[rows * 4];
        var uvs = new Vector2[rows * 4];
        var triangles = new int[rows * 6];
        var cornersMm = new Vector3[4];

        for (int i = 0; i < rows; i++)
        {
            int rowNumber = i + 1;
            VizlabDisplay_NEW.GetCornersMm(rowNumber, cornersMm);   // TL, TR, BR, BL

            float vMin, vMax;
            VizlabDisplay_NEW.GetRowAnchorFractions(rowNumber, out vMin, out vMax);

            int v = i * 4;
            for (int k = 0; k < 4; k++) vertices[v + k] = ToRoom(cornersMm[k]);

            uvs[v + 0] = new Vector2(0f, vMax);
            uvs[v + 1] = new Vector2(1f, vMax);
            uvs[v + 2] = new Vector2(1f, vMin);
            uvs[v + 3] = new Vector2(0f, vMin);

            int t = i * 6;
            triangles[t + 0] = v + 0; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
            triangles[t + 3] = v + 0; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;

            VizlabDisplay_NEW.Row row = VizlabDisplay_NEW.GetRow(rowNumber);
            _centre[i] = ToRoom(row.CentreMm);
            _up[i] = ToRoomDirection(row.Up);
        }

        WallMesh = new Mesh { name = "GetReal3DWallHud_Rows", hideFlags = HideFlags.DontSave };
        WallMesh.vertices = vertices;
        WallMesh.uv = uvs;
        WallMesh.triangles = triangles;

        // Never culled by bounds: the draw is a command buffer that always runs, but keep the
        // bounds honest for anything that asks.
        WallMesh.RecalculateBounds();
    }

    Vector3 ToRoom(Vector3 mm)
    {
        Vector3 m = mm * 0.001f;
        if (flipForward) m.z = -m.z;
        return m;
    }

    Vector3 ToRoomDirection(Vector3 d)
    {
        if (flipForward) d.z = -d.z;
        return d.normalized;
    }

    /// <summary>
    /// Where on the HUD canvas a world point appears, seen from the eye, as a fraction of
    /// the canvas (0,0 bottom left, 1,1 top right). False if the line of sight to it misses
    /// every panel. This is the wall's WorldToScreenPoint: the trail arrow uses it to stand
    /// on the line where the visitor sees the line, not where a monitor would.
    /// </summary>
    public bool TryWorldToCanvas(Vector3 world, out Vector2 normalized)
    {
        normalized = Vector2.zero;

        Transform eye = EyeTransform();
        if (eye == null) return false;

        Vector3 from = transform.InverseTransformPoint(eye.position);
        Vector3 dir = transform.InverseTransformPoint(world) - from;
        if (dir.sqrMagnitude < 1e-8f) return false;

        float width = VizlabDisplay_NEW.RowWidthMm * 0.001f;
        float height = VizlabDisplay_NEW.RowHeightMm * 0.001f;
        float best = float.MaxValue;

        for (int i = 0; i < VizlabDisplay_NEW.RowCount; i++)
        {
            Vector3 normal = Vector3.Cross(Vector3.right, _up[i]);
            float denom = Vector3.Dot(dir, normal);
            if (Mathf.Abs(denom) < 1e-6f) continue;

            float t = Vector3.Dot(_centre[i] - from, normal) / denom;
            if (t <= 0f || t >= best) continue;

            Vector3 offset = from + dir * t - _centre[i];
            float across = offset.x / width + 0.5f;
            float up = Vector3.Dot(offset, _up[i]) / height + 0.5f;
            if (across < 0f || across > 1f || up < 0f || up > 1f) continue;

            float vMin, vMax;
            VizlabDisplay_NEW.GetRowAnchorFractions(i + 1, out vMin, out vMax);

            best = t;
            normalized = new Vector2(across, Mathf.Lerp(vMin, vMax, up));
        }

        return best < float.MaxValue;
    }

    Transform _eye;

    Transform EyeTransform()
    {
        if (_eye == null)
        {
            getRealCameraUpdater updater = GetComponentInChildren<getRealCameraUpdater>();
            if (updater != null) _eye = updater.transform;
        }
        return _eye;
    }
}

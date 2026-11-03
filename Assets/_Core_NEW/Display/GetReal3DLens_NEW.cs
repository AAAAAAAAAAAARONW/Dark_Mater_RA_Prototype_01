using UnityEngine;

/// <summary>
/// The zoom, carried onto the wall. Sits on the getReal3D eye camera, after its
/// getRealCameraUpdater.
///
/// WHY IT IS NEEDED. On a monitor, zoom is a field of view: TutorialZoom_NEW narrows the
/// camera from 40 degrees to 12. On the wall there is no field of view to narrow - getReal3D
/// builds every camera's projection from where the screens physically are around the
/// viewer, and writes it over whatever the camera had. Left alone, the zoom gauge would
/// move and the picture would not.
///
/// WHAT IT DOES. Reads the field of view the rig's lens camera has been given and magnifies
/// the wall's projection by the same ratio, about the view axis:
///
///     k = tan(reference / 2) / tan(current / 2)        40 -> 12 degrees is about 3.5x
///
/// Scaling x and y in view space keeps every screen's frustum consistent with every
/// other, so the seams between the nodes still meet: the whole wall zooms as one picture,
/// centred on the rig's forward, which is where the reticle is.
///
/// getReal3D clones this camera per eye and per screen, and the clone carries this
/// component with it, so every one of them is magnified the same way.
///
/// The updater may rewrite the projection every frame or set it once and leave it. Both
/// are handled: if the matrix is still the one written here last time, nobody has touched
/// it, and the magnification is reapplied to the stored original instead of compounding.
///
/// LATER: THE DESK'S WINDOW. At a desk getReal3D runs on editor_config, one 530 x 300
/// screen - 16:9 - and builds the projection for that, whatever shape the window is. Since
/// Play started putting the Game view in the wall's 1.27 : 1, a 16:9 picture was squeezed
/// into it and everything stood 1.39 times too tall: the Sun an upright egg. So on a desk
/// (not RunningAsSeveralCopies) the projection is refitted to the window's own shape, the
/// height getReal3D gave it kept and the width following the window. On the wall each
/// copy's window is its screens' shape, and nothing is changed.
/// </summary>
[RequireComponent(typeof(Camera))]
public class GetReal3DLens_NEW : MonoBehaviour
{
    [Tooltip("The rig's own camera, whose field of view the zoom and the outro write.")]
    [SerializeField] Camera lensSource;

    [Tooltip("The field of view that means no magnification: the rig's unzoomed field of " +
             "view, 40 in the tutorial. 0 to take it from the lens camera at start.")]
    [SerializeField] float referenceFieldOfView = 40f;

    [Tooltip("Upper limit on the magnification, against a field of view driven to near zero.")]
    [SerializeField] float maxMagnification = 8f;

    Camera _camera;
    Matrix4x4 _original;
    Matrix4x4 _written;
    bool _hasWritten;

    /// <summary>The magnification in force, 1 = none.</summary>
    public float Magnification { get; private set; }

    /// <summary>
    /// The projection getReal3D gave this camera, before the zoom (fitted to the window on a
    /// desk). GetReal3DHudDraw_NEW draws the HUD with it: the HUD is fixed to the screens,
    /// so it must not zoom.
    /// </summary>
    public Matrix4x4 UnmagnifiedProjection
    {
        get { return _hasWritten ? _original * Matrix4x4.Scale(new Vector3(_aspectFix, 1f, 1f)) : _camera.projectionMatrix; }
    }

    float _aspectFix = 1f;

    void Awake()
    {
        _camera = GetComponent<Camera>();
        Magnification = 1f;

        if (referenceFieldOfView <= 0f && lensSource != null)
            referenceFieldOfView = lensSource.fieldOfView;
    }

    // Both: the updater sets the projection before culling, and may set it again per eye
    // before rendering. Running after it in each keeps the culling and the picture agreed.
    void OnPreCull() { Apply(); }
    void OnPreRender() { Apply(); }

    void Apply()
    {
        float k = TargetMagnification();
        Magnification = k;

        Matrix4x4 current = _camera.projectionMatrix;
        Matrix4x4 source = !_hasWritten || current != _written ? current : _original;
        float fix = DeskAspectFix(source);

        // Never touched the projection, and nothing to do: leave Unity's own in charge.
        if (!_hasWritten && Mathf.Approximately(k, 1f) && Mathf.Approximately(fix, 1f)) return;

        _original = source;
        _aspectFix = fix;

        _written = Mathf.Approximately(k, 1f) && Mathf.Approximately(fix, 1f)
            ? _original
            : _original * Matrix4x4.Scale(new Vector3(k * fix, k, 1f));

        _camera.projectionMatrix = _written;
        _hasWritten = true;
    }

    /// <summary>
    /// On a desk, how much to widen x so getReal3D's frustum takes the window's shape (see
    /// LATER, above): its own aspect, m11 / m00, over the window's. 1 on the wall.
    /// </summary>
    float DeskAspectFix(Matrix4x4 projection)
    {
        if (InputScheme_NEW.RunningAsSeveralCopies) return 1f;
        if (_camera.pixelHeight <= 0 || Mathf.Abs(projection.m00) < 1e-6f) return 1f;

        float projected = projection.m11 / projection.m00;
        float window = _camera.pixelWidth / (float)_camera.pixelHeight;
        return projected / window;
    }

    float TargetMagnification()
    {
        if (lensSource == null || referenceFieldOfView <= 0f) return 1f;

        float current = Mathf.Clamp(lensSource.fieldOfView, 0.1f, 179f);
        float k = Mathf.Tan(referenceFieldOfView * 0.5f * Mathf.Deg2Rad) /
                  Mathf.Tan(current * 0.5f * Mathf.Deg2Rad);

        return Mathf.Clamp(k, 1f / maxMagnification, maxMagnification);
    }
}

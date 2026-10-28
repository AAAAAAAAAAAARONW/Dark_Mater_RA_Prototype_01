using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Draws GetReal3DHud_NEW's seven rows into this getReal3D eye, after post-processing. Sits
/// on the eye camera, last; getReal3D clones the camera per eye and per screen and the
/// clone carries this with it, so every screen draws its own part of the rows.
///
/// WHY A COMMAND BUFFER AND NOT A RENDERER. As ordinary geometry the rows would go through
/// the eye's post-processing, and the tutorial's profile has motion blur at a 270-degree
/// shutter: the HUD would smear every time the visitor looked round, then bloom and take the
/// colour grade. Drawn at AfterImageEffects it lands on the finished picture instead, the
/// way an overlay does.
///
/// It draws with the projection getReal3D built for this screen, NOT the zoomed one
/// GetReal3DLens_NEW makes: the HUD is on the glass, and the glass does not zoom.
/// </summary>
[RequireComponent(typeof(Camera))]
public class GetReal3DHudDraw_NEW : MonoBehaviour
{
    const CameraEvent When = CameraEvent.AfterImageEffects;

    Camera _camera;
    GetReal3DLens_NEW _lens;
    CommandBuffer _buffer;

    void OnEnable()
    {
        _camera = GetComponent<Camera>();
        _lens = GetComponent<GetReal3DLens_NEW>();
        _buffer = new CommandBuffer { name = "GetReal3D wall HUD" };
        _camera.AddCommandBuffer(When, _buffer);
    }

    void OnDisable()
    {
        if (_camera != null && _buffer != null) _camera.RemoveCommandBuffer(When, _buffer);
        if (_buffer != null) _buffer.Release();
        _buffer = null;
    }

    // After the updater and the lens have set this frame's projection.
    void OnPreRender()
    {
        if (_buffer == null) return;
        _buffer.Clear();

        GetReal3DHud_NEW hud = GetReal3DHud_NEW.Active;
        if (hud == null || hud.WallMesh == null || hud.WallMaterial == null) return;

        // The camera's own view, whether getReal3D moved the transform or set the matrix,
        // with the rows placed by the room. Vizlab_tut stays within a few thousand units of
        // the origin, where a float holds well under a millimetre.
        Matrix4x4 projection = _lens != null ? _lens.UnmagnifiedProjection : _camera.projectionMatrix;

        _buffer.SetViewProjectionMatrices(_camera.worldToCameraMatrix, projection);
        _buffer.DrawMesh(hud.WallMesh, hud.transform.localToWorldMatrix, hud.WallMaterial);
    }
}

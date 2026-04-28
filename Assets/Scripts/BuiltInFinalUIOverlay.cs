using UnityEngine;

/// <summary>
/// Built-in RP final-pass overlay compositor.
/// Renders a dedicated UI layer to a transparent RT, then composites it in OnRenderImage.
/// Attach to the final output camera (usually the one with CinemachineBrain).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class BuiltInFinalUIOverlay : MonoBehaviour
{
    [SerializeField] string overlayLayerName = "UIOverlay";
    [SerializeField] string overlayCameraName = "FinalUIOverlayCamera";
    [SerializeField] bool copyFromMainCameraEachFrame = true;

    Camera _mainCamera;
    Camera _overlayCamera;
    RenderTexture _overlayRT;
    Material _compositeMat;
    int _overlayLayer = -1;

    void OnEnable()
    {
        _mainCamera = GetComponent<Camera>();
        _overlayLayer = LayerMask.NameToLayer(overlayLayerName);
        EnsureResources();
    }

    void OnDisable()
    {
        ReleaseResources();
    }

    void EnsureResources()
    {
        if (_overlayLayer < 0)
        {
            Debug.LogWarning($"[BuiltInFinalUIOverlay] Layer '{overlayLayerName}' not found. Please create it in Tags and Layers.", this);
            return;
        }

        if (_overlayCamera == null)
        {
            GameObject go = new GameObject(overlayCameraName);
            go.transform.SetParent(transform, false);
            _overlayCamera = go.AddComponent<Camera>();
        }

        _overlayCamera.enabled = false;
        _overlayCamera.clearFlags = CameraClearFlags.SolidColor;
        _overlayCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        _overlayCamera.cullingMask = 1 << _overlayLayer;
        _overlayCamera.allowHDR = false;
        _overlayCamera.allowMSAA = false;
        _overlayCamera.forceIntoRenderTexture = true;

        if (_compositeMat == null)
        {
            Shader s = Shader.Find("Hidden/BuiltInUIOverlayComposite");
            if (s == null)
            {
                Debug.LogWarning("[BuiltInFinalUIOverlay] Shader 'Hidden/BuiltInUIOverlayComposite' not found.", this);
                return;
            }

            _compositeMat = new Material(s) { name = "Runtime_BuiltInUIOverlayComposite" };
        }
    }

    void EnsureOverlayRT(int width, int height)
    {
        if (_overlayRT != null && _overlayRT.width == width && _overlayRT.height == height)
            return;

        if (_overlayRT != null)
        {
            _overlayRT.Release();
            Destroy(_overlayRT);
        }

        _overlayRT = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
        {
            name = "RT_UIOverlayFinal",
            useMipMap = false,
            autoGenerateMips = false,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        _overlayRT.Create();
    }

    void SyncOverlayCamera()
    {
        if (_overlayCamera == null || _mainCamera == null)
            return;

        if (!copyFromMainCameraEachFrame)
            return;

        _overlayCamera.transform.position = _mainCamera.transform.position;
        _overlayCamera.transform.rotation = _mainCamera.transform.rotation;
        _overlayCamera.orthographic = _mainCamera.orthographic;
        _overlayCamera.fieldOfView = _mainCamera.fieldOfView;
        _overlayCamera.orthographicSize = _mainCamera.orthographicSize;
        _overlayCamera.nearClipPlane = _mainCamera.nearClipPlane;
        _overlayCamera.farClipPlane = _mainCamera.farClipPlane;
        _overlayCamera.rect = _mainCamera.rect;
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (_overlayLayer < 0)
        {
            Graphics.Blit(source, destination);
            return;
        }

        EnsureResources();
        if (_overlayCamera == null || _compositeMat == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        EnsureOverlayRT(source.width, source.height);
        SyncOverlayCamera();

        _overlayCamera.targetTexture = _overlayRT;
        _overlayCamera.Render();

        _compositeMat.SetTexture("_OverlayTex", _overlayRT);
        Graphics.Blit(source, destination, _compositeMat);
    }

    void ReleaseResources()
    {
        if (_overlayRT != null)
        {
            _overlayRT.Release();
            Destroy(_overlayRT);
            _overlayRT = null;
        }

        if (_compositeMat != null)
        {
            Destroy(_compositeMat);
            _compositeMat = null;
        }

        if (_overlayCamera != null)
        {
            if (_overlayCamera.gameObject != null)
                Destroy(_overlayCamera.gameObject);
            _overlayCamera = null;
        }
    }
}

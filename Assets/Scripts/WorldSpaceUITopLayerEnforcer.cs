using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Enforces all WorldSpaceUI images to render on the top UI sorting layer/order.
/// Attach once in scene (for example on a manager object).
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class WorldSpaceUITopLayerEnforcer : MonoBehaviour
{
    [Header("Target Filter")]
    [Tooltip("Only canvases in World Space mode are processed.")]
    [SerializeField] bool processOnlyWorldSpaceCanvas = true;
    [Tooltip("Canvas name or parent path must contain this keyword to be treated as WorldSpaceUI.")]
    [SerializeField] string worldSpaceUIKeyword = "WorldSpaceUI";
    [SerializeField] bool includeInactive = true;

    [Header("Top Layer Settings")]
    [Tooltip("Set empty to keep existing sorting layer.")]
    [SerializeField] string sortingLayerName = "UI";
    [Tooltip("Higher value renders later (on top).")]
    [SerializeField] int topSortingOrder = 32767;

    [Header("Force In Front Of 3D")]
    [Tooltip("Also force Image shader depth test to Always, so it renders in front of scene geometry/volumes.")]
    [SerializeField] bool forceAlwaysOnTopForImages = true;
    [Tooltip("Optional override material. Leave empty to auto-create from shader 'Custom/UIAlwaysOnTop'.")]
    [SerializeField] Material alwaysOnTopMaterial;

    [Header("Auto Apply")]
    [SerializeField] bool applyOnEnable = true;
    [SerializeField] bool continuousCheck = false;
    [SerializeField] float checkInterval = 1f;

    [Header("Dedicated Overlay Camera (Legacy)")]
    [Tooltip("Legacy path. For Built-in RP, prefer using BuiltInFinalUIOverlay on the main output camera.")]
    [SerializeField] bool useDedicatedOverlayCamera = false;
    [Tooltip("Layer used by WorldSpaceUI objects. Create this layer in Project Settings > Tags and Layers.")]
    [SerializeField] string overlayLayerName = "UIOverlay";
    [Tooltip("Render depth for overlay camera. Keep very high.")]
    [SerializeField] float overlayCameraDepth = 10000f;
    [SerializeField] string overlayCameraName = "WorldSpaceUIOverlayCamera";
    [Tooltip("When enabled, overlay camera copies pose/FOV/clip/projection from current render camera each frame.")]
    [SerializeField] bool followCurrentRenderCamera = true;
    [Tooltip("Optional explicit source render camera. Leave empty to auto-find.")]
    [SerializeField] Camera sourceRenderCamera;
    [Tooltip("Automatically remove overlay layer from all other cameras' culling mask.")]
    [SerializeField] bool removeOverlayLayerFromOtherCameras = true;

    float _nextCheckTime;
    Material _runtimeAlwaysOnTopMaterial;
    Camera _overlayCamera;

    void OnEnable()
    {
        _nextCheckTime = Time.realtimeSinceStartup;
        if (applyOnEnable)
            ApplyTopLayerNow();
    }

    void Update()
    {
        if (useDedicatedOverlayCamera && followCurrentRenderCamera)
            SyncOverlayCameraToSource();

        if (!continuousCheck)
            return;

        if (Time.realtimeSinceStartup < _nextCheckTime)
            return;

        _nextCheckTime = Time.realtimeSinceStartup + Mathf.Max(0.1f, checkInterval);
        ApplyTopLayerNow();
    }

    [ContextMenu("Apply Top Layer Now")]
    public void ApplyTopLayerNow()
    {
        Canvas[] canvases = GetAllCanvases();
        int matchedCanvasCount = 0;
        int updatedCanvasCount = 0;
        int totalImageCount = 0;
        int forcedLayerCount = 0;
        int overlayLayer = GetOverlayLayerIndex();

        if (useDedicatedOverlayCamera && overlayLayer >= 0)
        {
            EnsureOverlayCamera(overlayLayer);
            if (removeOverlayLayerFromOtherCameras)
                RemoveOverlayLayerFromOtherCameras(overlayLayer);
        }

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null)
                continue;

            if (processOnlyWorldSpaceCanvas && canvas.renderMode != RenderMode.WorldSpace)
                continue;

            if (!IsWorldSpaceUITarget(canvas.transform))
                continue;

            matchedCanvasCount++;
            Image[] images = canvas.GetComponentsInChildren<Image>(includeInactive);
            totalImageCount += images.Length;

            bool changed = false;

            if (!canvas.overrideSorting)
            {
                canvas.overrideSorting = true;
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(sortingLayerName) &&
                canvas.sortingLayerName != sortingLayerName)
            {
                canvas.sortingLayerName = sortingLayerName;
                changed = true;
            }

            if (canvas.sortingOrder != topSortingOrder)
            {
                canvas.sortingOrder = topSortingOrder;
                changed = true;
            }

            if (useDedicatedOverlayCamera && overlayLayer >= 0)
            {
                int changedLayerNodes = SetLayerRecursively(canvas.transform, overlayLayer);
                if (changedLayerNodes > 0)
                    forcedLayerCount += changedLayerNodes;
            }

            if (forceAlwaysOnTopForImages)
            {
                Material mat = GetAlwaysOnTopMaterial();
                if (mat != null)
                    ApplyMaterialToImages(images, mat);
            }

            if (changed)
                updatedCanvasCount++;
        }

        Debug.Log(
            $"[WorldSpaceUITopLayerEnforcer] Checked {matchedCanvasCount} WorldSpaceUI canvas(es), " +
            $"updated {updatedCanvasCount}, total images {totalImageCount}, moved-to-overlay-layer nodes {forcedLayerCount}. " +
            $"Target sortingOrder={topSortingOrder}, sortingLayer='{sortingLayerName}'.",
            this
        );
    }

    int GetOverlayLayerIndex()
    {
        if (!useDedicatedOverlayCamera)
            return -1;

        if (string.IsNullOrWhiteSpace(overlayLayerName))
        {
            Debug.LogWarning("[WorldSpaceUITopLayerEnforcer] Overlay layer name is empty. Dedicated overlay camera disabled.", this);
            return -1;
        }

        int idx = LayerMask.NameToLayer(overlayLayerName);
        if (idx < 0)
            Debug.LogWarning($"[WorldSpaceUITopLayerEnforcer] Layer '{overlayLayerName}' not found. Create it in Tags and Layers.", this);
        return idx;
    }

    void EnsureOverlayCamera(int overlayLayer)
    {
        if (_overlayCamera == null)
        {
            Camera[] cams = GetAllCameras();
            for (int i = 0; i < cams.Length; i++)
            {
                if (cams[i] != null && cams[i].name == overlayCameraName)
                {
                    _overlayCamera = cams[i];
                    break;
                }
            }
        }

        if (_overlayCamera == null)
        {
            GameObject go = new GameObject(overlayCameraName);
            go.transform.SetParent(transform, false);
            _overlayCamera = go.AddComponent<Camera>();
        }

        _overlayCamera.clearFlags = CameraClearFlags.Depth;
        _overlayCamera.cullingMask = 1 << overlayLayer;
        _overlayCamera.depth = Mathf.Max(overlayCameraDepth, GetHighestCameraDepthExcludingOverlay() + 100f);
        _overlayCamera.orthographic = false;
        _overlayCamera.nearClipPlane = 0.01f;
        _overlayCamera.farClipPlane = 5000f;
        _overlayCamera.allowHDR = false;
        _overlayCamera.allowMSAA = true;
    }

    float GetHighestCameraDepthExcludingOverlay()
    {
        Camera[] cams = GetAllCameras();
        float maxDepth = -1000f;
        for (int i = 0; i < cams.Length; i++)
        {
            Camera c = cams[i];
            if (c == null || c == _overlayCamera)
                continue;
            if (c.depth > maxDepth)
                maxDepth = c.depth;
        }
        return maxDepth;
    }

    void RemoveOverlayLayerFromOtherCameras(int overlayLayer)
    {
        int overlayMask = 1 << overlayLayer;
        Camera[] cams = GetAllCameras();
        for (int i = 0; i < cams.Length; i++)
        {
            Camera c = cams[i];
            if (c == null || c == _overlayCamera)
                continue;
            if ((c.cullingMask & overlayMask) != 0)
                c.cullingMask &= ~overlayMask;
        }
    }

    Camera[] GetAllCameras()
    {
#if UNITY_2022_2_OR_NEWER
        if (includeInactive)
            return FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return FindObjectsByType<Camera>(FindObjectsSortMode.None);
#else
        if (!includeInactive)
            return FindObjectsOfType<Camera>();

        Camera[] all = Resources.FindObjectsOfTypeAll<Camera>();
        System.Collections.Generic.List<Camera> sceneCameras = new System.Collections.Generic.List<Camera>(all.Length);
        for (int i = 0; i < all.Length; i++)
        {
            Camera c = all[i];
            if (c == null)
                continue;
            if (!c.gameObject.scene.IsValid())
                continue;
            if ((c.hideFlags & HideFlags.HideAndDontSave) != 0)
                continue;
            sceneCameras.Add(c);
        }
        return sceneCameras.ToArray();
#endif
    }

    void SyncOverlayCameraToSource()
    {
        if (_overlayCamera == null)
            return;

        Camera src = ResolveSourceRenderCamera();
        if (src == null || src == _overlayCamera)
            return;

        Transform srcTr = src.transform;
        Transform dstTr = _overlayCamera.transform;
        dstTr.position = srcTr.position;
        dstTr.rotation = srcTr.rotation;

        _overlayCamera.orthographic = src.orthographic;
        _overlayCamera.fieldOfView = src.fieldOfView;
        _overlayCamera.orthographicSize = src.orthographicSize;
        _overlayCamera.nearClipPlane = src.nearClipPlane;
        _overlayCamera.farClipPlane = src.farClipPlane;
        _overlayCamera.aspect = src.aspect;
    }

    Camera ResolveSourceRenderCamera()
    {
        if (sourceRenderCamera != null && sourceRenderCamera.isActiveAndEnabled)
            return sourceRenderCamera;

        Camera main = Camera.main;
        if (main != null && main.isActiveAndEnabled)
            return main;

        Camera[] cams = Camera.allCameras;
        Camera best = null;
        float bestDepth = float.MinValue;
        for (int i = 0; i < cams.Length; i++)
        {
            Camera c = cams[i];
            if (c == null || !c.isActiveAndEnabled || c == _overlayCamera)
                continue;
            if (c.depth > bestDepth)
            {
                bestDepth = c.depth;
                best = c;
            }
        }
        return best;
    }

    int SetLayerRecursively(Transform root, int layer)
    {
        int changed = 0;
        if (root.gameObject.layer != layer)
        {
            root.gameObject.layer = layer;
            changed++;
        }

        for (int i = 0; i < root.childCount; i++)
            changed += SetLayerRecursively(root.GetChild(i), layer);

        return changed;
    }

    void ApplyMaterialToImages(Image[] images, Material mat)
    {
        for (int i = 0; i < images.Length; i++)
        {
            Image img = images[i];
            if (img == null)
                continue;
            if (img.material != mat)
                img.material = mat;
        }
    }

    Material GetAlwaysOnTopMaterial()
    {
        if (alwaysOnTopMaterial != null)
            return alwaysOnTopMaterial;

        if (_runtimeAlwaysOnTopMaterial != null)
            return _runtimeAlwaysOnTopMaterial;

        Shader shader = Shader.Find("Custom/UIAlwaysOnTop");
        if (shader == null)
        {
            Debug.LogWarning("[WorldSpaceUITopLayerEnforcer] Shader 'Custom/UIAlwaysOnTop' not found. Top-most depth override skipped.", this);
            return null;
        }

        _runtimeAlwaysOnTopMaterial = new Material(shader)
        {
            name = "Runtime_UIAlwaysOnTop_Mat"
        };
        return _runtimeAlwaysOnTopMaterial;
    }

    Canvas[] GetAllCanvases()
    {
#if UNITY_2022_2_OR_NEWER
        if (includeInactive)
            return FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return FindObjectsByType<Canvas>(FindObjectsSortMode.None);
#else
        if (!includeInactive)
            return FindObjectsOfType<Canvas>();

        Canvas[] all = Resources.FindObjectsOfTypeAll<Canvas>();
        System.Collections.Generic.List<Canvas> sceneCanvases = new System.Collections.Generic.List<Canvas>(all.Length);
        for (int i = 0; i < all.Length; i++)
        {
            Canvas c = all[i];
            if (c == null)
                continue;
            if (!c.gameObject.scene.IsValid())
                continue; // skip prefabs/assets
            if ((c.hideFlags & HideFlags.HideAndDontSave) != 0)
                continue;
            sceneCanvases.Add(c);
        }
        return sceneCanvases.ToArray();
#endif
    }

    bool IsWorldSpaceUITarget(Transform tr)
    {
        if (string.IsNullOrWhiteSpace(worldSpaceUIKeyword))
            return true;

        string key = worldSpaceUIKeyword.Trim();
        Transform current = tr;
        while (current != null)
        {
            if (current.name.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            current = current.parent;
        }

        return false;
    }

    void OnDisable()
    {
        if (_runtimeAlwaysOnTopMaterial != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(_runtimeAlwaysOnTopMaterial);
            else
                Destroy(_runtimeAlwaysOnTopMaterial);
#else
            Destroy(_runtimeAlwaysOnTopMaterial);
#endif
            _runtimeAlwaysOnTopMaterial = null;
        }
    }
}

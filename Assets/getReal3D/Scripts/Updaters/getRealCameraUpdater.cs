using UnityEngine;
using UnityEngine.Rendering;
using getReal3D;
using System;

/// <summary>
/// Script used to update a camera position, rotation and projection.
/// </summary>
[RequireComponent(typeof(Camera))]
[AddComponentMenu("getReal3D/Updater/Camera Updater")]
[ExecuteInEditMode]
public class getRealCameraUpdater : getRealUserScript, CameraUpdaterInterface
{

    public CameraUpdaterHelper updater
    {
        get { return m_cameraUpdaterHelper; }
        set { m_cameraUpdaterHelper = value; }
    }

    public MonoBehaviour behaviour
    {
        get { return this; }
    }

    [SerializeField]
    public CameraPreviewData m_cameraPreviewData;

    public CameraPreviewData cameraPreviewData
    {
        get { return m_cameraPreviewData; }
        set { m_cameraPreviewData = value; }
    }

    private CameraUpdaterHelper m_cameraUpdaterHelper;
    private Camera m_camera = null;

    void OnEnable()
    {
        m_camera = GetComponent<Camera>();
        CameraUpdaterHelper.CreateCamerasForUserIfNeeded(GetComponent<Camera>(), userId());
        registerRenderPipeline();
    }

    public bool m_updateTrackingPosition = true;
    void OnPreCull()
    {
        if (m_cameraUpdaterHelper != null && m_updateTrackingPosition)
        {
            m_cameraUpdaterHelper.PreCull();
        }
    }

    void OnPreRender()
    {
        if (m_cameraUpdaterHelper != null)
        {
            m_cameraUpdaterHelper.PreRender();
        }
    }

    void OnPostRender()
    {
        if (m_cameraUpdaterHelper != null)
        {
            m_cameraUpdaterHelper.PostRender();
        }
    }

    void OnDestroy()
    {
        deregisterRenderPipeline();
        if (m_cameraUpdaterHelper != null)
        {
            m_cameraUpdaterHelper.Destroyed();
        }        
    }

    void OnDisable()
    {
        if (m_cameraUpdaterHelper != null)
        {
            m_cameraUpdaterHelper.Disabled();
        }
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (m_cameraUpdaterHelper != null)
        {
            m_cameraUpdaterHelper.OnRenderImage(src, dst);
        }
        else
        {
            Graphics.Blit(src, dst);
        }
    }

    #region URP/HDRP
#if UNITY_2019_4_OR_NEWER
    private void registerRenderPipeline()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        RenderPipelineManager.beginFrameRendering += OnBeginFrameRendering;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        RenderPipelineManager.endFrameRendering += OnEndFrameRendering;
    }

    private void deregisterRenderPipeline()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.beginFrameRendering -= OnBeginFrameRendering;
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        RenderPipelineManager.endFrameRendering -= OnEndFrameRendering;
    }

    void OnBeginCameraRendering(ScriptableRenderContext src, Camera camera)
    {
        if (m_cameraUpdaterHelper != null && camera == m_camera)
        {
            if (GraphicsSettings.currentRenderPipeline.GetType().ToString().Contains("HighDefinition"))
                m_cameraUpdaterHelper.hdrpBeginCameraRendering(src);
            else
                m_cameraUpdaterHelper.urpBeginCameraRendering(src);
        }
    }

    void OnBeginFrameRendering(ScriptableRenderContext src, Camera[] cameras)
    {
        if (m_cameraUpdaterHelper != null)
        {
            if (GraphicsSettings.currentRenderPipeline.GetType().ToString().Contains("HighDefinition"))
                m_cameraUpdaterHelper.hdrpBeginFrameRendering(src, cameras);
            else
                m_cameraUpdaterHelper.urpBeginFrameRendering(src, cameras);
        }
    }

    void OnEndCameraRendering(ScriptableRenderContext src, Camera camera)
    {
        if (m_cameraUpdaterHelper != null && camera == m_camera)
        {
            if (GraphicsSettings.currentRenderPipeline.GetType().ToString().Contains("HighDefinition"))
                m_cameraUpdaterHelper.hdrpEndCameraRendering(src);
            else
                m_cameraUpdaterHelper.urpEndCameraRendering(src);
        }
    }

    void OnEndFrameRendering(ScriptableRenderContext src, Camera[] cameras)
    {
        if (m_cameraUpdaterHelper != null)
        {
            if (GraphicsSettings.currentRenderPipeline.GetType().ToString().Contains("HighDefinition"))
                m_cameraUpdaterHelper.hdrpEndFrameRendering(src, cameras);
            else
                m_cameraUpdaterHelper.urpEndFrameRendering(src, cameras);
        }
    }
#else
    private void registerRenderPipeline(){}
    private void deregisterRenderPipeline(){}
#endif
    #endregion
}

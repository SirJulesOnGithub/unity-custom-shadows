using UnityEngine;
using UnityEngine.Rendering.Universal;


[RequireComponent(typeof(Camera))]
[ExecuteInEditMode]
public class ShadowCamera : MonoBehaviour
{
    public enum TextureSize // TODO: Probably Unity have build-in solution, find it and use it
    {
        _ScreenSize = 1,
        _2x2        = 2,
        _4x4        = 4,
        _8x8        = 8,
        _16x16      = 16,
        _32x32      = 32,
        _64x64      = 64,
        _128x128    = 128,
        _256x256    = 256,
        _512x512    = 512,
        _1024x1024  = 1024,
        _2048x2048  = 2048,
        _4096x4096  = 4096,
    }
    
    public enum TextureDownsample
    {
        None = 1,
        x2 = 2,
        x4 = 4,
        x8 = 8,
    }
    
    private static ShadowCamera m_instance;
#if UNITY_EDITOR
    private ShadowCamera() // some ugly editor hax to prevent NullReference
    {
        m_instance = this;
    }
#endif
    
    
    [Header("Capture Settings"), Space(10)]
    [SerializeField]
    private LayerMask m_shadowCastersLayer;
    
    [Header("Quality Settings"), Space(10)]
    [SerializeField]
    private TextureSize m_outputTextureSize = TextureSize._ScreenSize;
    
    [SerializeField] [Tooltip("Used to downsample captured texture IF its size is set to ScreenSize")]
    private TextureDownsample m_downsample = TextureDownsample.x4;

    [Header("Other Settings"), Space(10)]
    [SerializeField] [Tooltip("Used in Editor so camera wont render to frame buffer")]
    private RenderTexture m_dummyTexture;
    
    
    private Camera m_camera;
    
    public bool IsInitialized { get; private set; } = false;

#if UNITY_EDITOR
    [SerializeField, HideInInspector]
    private bool m_isCameraSet = false;
#endif
    private void Awake()
    {
        IsInitialized = CheckNecessaryComponents();
        SetupCamera();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        m_isCameraSet = false;
        IsInitialized = CheckNecessaryComponents();
        SetupCamera();
    }
#endif
    
    public bool CheckNecessaryComponents()
    {
        if (m_camera == null)
        {
            m_camera = this.GetComponent<Camera>();
            if (m_camera == null)
            {
                LogMessage("Missing Camera Component! : Initialization aborted!");
                return false;
            }
        }

        return true;
    }
    
    private void SetupCamera()
    {
        if (!IsInitialized)
            return;
        
#if UNITY_EDITOR
        if (m_isCameraSet)
            return;
#endif
        if (m_outputTextureSize == TextureSize._ScreenSize)
        {
            Debug.LogWarning($"[{nameof(ShadowCamera)}] : Screen Size Shadow Maps not supported! Switching to 1024x1024");
            m_outputTextureSize = TextureSize._1024x1024;
        }
        
        m_camera.orthographic = true;
        m_camera.aspect = 1.0f;
        m_camera.cullingMask = m_shadowCastersLayer;
        m_camera.backgroundColor = Color.black;
        m_camera.allowHDR = false;
        m_camera.allowMSAA = false;
        m_camera.forceIntoRenderTexture = true;
        
        UniversalAdditionalCameraData cameraData = m_camera.GetUniversalAdditionalCameraData();
        cameraData.antialiasing = AntialiasingMode.None;
        cameraData.antialiasingQuality = AntialiasingQuality.Low;
        cameraData.requiresColorOption = CameraOverrideOption.Off;
        cameraData.requiresColorTexture = false;
        cameraData.requiresDepthOption = CameraOverrideOption.On;
        cameraData.requiresDepthTexture = true;
        cameraData.allowXRRendering = false;
        
        #if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (m_camera.targetTexture != m_dummyTexture)
                m_camera.targetTexture = m_dummyTexture;

            m_isCameraSet = true;
            return;
        }
        #endif
            
        // setup RT
        var resolution = (int)m_outputTextureSize / (int) m_downsample;
        var depthTarget = new RenderTexture(resolution, resolution, 32, RenderTextureFormat.Depth,
            RenderTextureReadWrite.Linear);
        
        depthTarget.wrapMode = TextureWrapMode.Clamp;
        depthTarget.filterMode = FilterMode.Bilinear;
        depthTarget.autoGenerateMips = false;
        depthTarget.useMipMap = false;
        depthTarget.antiAliasing = 1; 
        depthTarget.Create();
        
        m_camera.targetTexture = depthTarget;
        
#if UNITY_EDITOR
        m_isCameraSet = true;
#endif
    }
    
    private void LogMessage(string msg)
    {
        Debug.Log($"[{nameof(ShadowCamera)}] : {msg}", this);
    }
}

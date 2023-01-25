using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CustomShadowRenderPass : ScriptableRenderPass
{
    private const int k_DepthBufferBits = 32;
    private const int k_minimalBufferResolution = 16;
    
    private ProfilingSampler m_profilingSampler;
    private FilteringSettings m_filteringSettings;

    private RenderTargetHandle m_shadowmap;
    private RenderTexture m_shadowmapTexture;
    private RenderTargetIdentifier m_shadowmapIdentifier;
    private int m_resolution = 1024;
    private GraphicsFormat m_format;
    private ShadowSamplingMode m_samplingMode;
    private FilterMode m_filterMode;
    
    /// <summary>
    /// Shadow Properties:
    /// .x - shadow bias
    /// .y - normal bias
    /// .z - shadow strength
    /// </summary>
    private Vector4 m_shadowProperties = new Vector4(1, 0, 1, 0);
    
    private readonly Color m_shadowColor;
    private Shader m_customShadowShader;
    private CustomShadowRendererSettings m_settings;
    
    private static readonly ShaderTagId m_shaderTagId = new ShaderTagId("ShadowCaster");
    private static readonly string m_profilingTag = nameof(CustomShadowRenderPass);
    
    private bool m_forcePointSampling;
    
    private Matrix4x4 m_shadowMatrix = Matrix4x4.identity;

    private Material m_blitMaterial;

    private bool m_setupEmptyShadowMap = false;
    private readonly Vector4 m_emptyProperties = new Vector4(1, 0, 1, 0);

    private Transform m_cachedCameraTransform;
    
    private static class ShaderIDs
    {
        internal const string ShadowTextureProperty = "_CustomShadowTexture";
        internal static readonly int ShadowMatrixId = Shader.PropertyToID("_CustomShadowMatrix");
        internal static readonly int ShadowColorId = Shader.PropertyToID("_CustomShadowColor");
        internal static readonly int ShadowProperties = Shader.PropertyToID("_CustomShadowProperties");
        internal static readonly int ShadowLightDirection = Shader.PropertyToID("_CustomShadowLightDirection");
    }
    
    public CustomShadowRenderPass(RenderQueueRange renderQueueRange,
        ref CustomShadowRendererSettings settings)
    {
        m_settings = settings;
        base.profilingSampler = new ProfilingSampler(m_profilingTag);
        m_profilingSampler = new ProfilingSampler(m_profilingTag);

        base.renderPassEvent = m_settings.RenderPassEvent;
        m_filteringSettings = new FilteringSettings(renderQueueRange, m_settings.ShadowCastersLayerMask);

        m_forcePointSampling = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal &&
                               GraphicsSettings.HasShaderDefine(Graphics.activeTier,
                                   BuiltinShaderDefine.UNITY_METAL_SHADOWS_USE_POINT_FILTERING);
        
        m_format = GraphicsFormatUtility.GetDepthStencilFormat(k_DepthBufferBits, 0);
        m_samplingMode = (RenderingUtils.SupportsRenderTextureFormat(RenderTextureFormat.Shadowmap) &&
                          SystemInfo.graphicsDeviceType != GraphicsDeviceType.OpenGLES2)
            ? ShadowSamplingMode.CompareDepths
            : ShadowSamplingMode.None;
        m_filterMode = m_forcePointSampling ? FilterMode.Point : FilterMode.Bilinear;
        
        //TODO: this needs to be used in ShadowCaster Pass
        m_shadowProperties = new Vector4(m_settings.ShadowBias, m_settings.NormalBias, m_settings.ShadowStrength);
        
        m_shadowColor = m_settings.ShadowColor;

        m_shadowmap.Init(ShaderIDs.ShadowTextureProperty);
    }

    public bool Setup(ref RenderingData data)
    {
        #if UNITY_EDITOR
        if(Application.isPlaying == false)
            SetupForEmptyRendering();
        #endif
        
        if (m_setupEmptyShadowMap)
            return false;

        Clear();
        // to be sure that output texture is square
        m_resolution = (data.cameraData.targetTexture.width >= data.cameraData.targetTexture.height)
            ? data.cameraData.targetTexture.width
            : data.cameraData.targetTexture.height;
        m_shadowmapTexture =
            GetTempShadowTexture(m_resolution);

        m_shadowmapIdentifier = new RenderTargetIdentifier(m_shadowmapTexture);
        
        return true;
    }
    
    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        ConfigureTarget(m_shadowmapIdentifier, m_shadowmapIdentifier);
        ConfigureClear(ClearFlag.Depth, Color.black);
    }

    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        m_cachedCameraTransform = renderingData.cameraData.camera.transform;
        cmd.SetGlobalColor(ShaderIDs.ShadowColorId, m_shadowColor); // TODO: move this into constructor since shadow color wont change (i hope)
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (m_setupEmptyShadowMap)
        {
            RenderEmptyShadowmap(ref context);
            return;
        }
        
        CommandBuffer cmd = CommandBufferPool.Get();
        using (new ProfilingScope(cmd, m_profilingSampler))
        {
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            var cameraData = renderingData.cameraData;
            var sortingFlags = cameraData.defaultOpaqueSortFlags;
            var drawSettings = CreateDrawingSettings(m_shaderTagId, ref renderingData, sortingFlags);
            drawSettings.perObjectData = PerObjectData.None;

            Vector3 lightDirection = -m_cachedCameraTransform.localToWorldMatrix.GetColumn(2);
            cmd.SetGlobalVector(ShaderIDs.ShadowLightDirection,
                new Vector4(lightDirection.x, lightDirection.y, lightDirection.z, 0.0f));//-m_cachedCameraTransform.forward);

            Matrix4x4 projectionMatrix = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(), true);
            m_shadowProperties = CalculateShadowBiasAndStrength(projectionMatrix.m00);
            cmd.SetGlobalVector(ShaderIDs.ShadowProperties, m_shadowProperties);
            
            cmd.SetGlobalDepthBias(1.0f, 2.5f);
            context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref m_filteringSettings);
            cmd.SetGlobalDepthBias(0.0f, 0.0f);
            
            cmd.SetGlobalTexture(m_shadowmap.id, m_shadowmapTexture);
            
            m_shadowMatrix = GetShadowMatrix(projectionMatrix, cameraData.GetViewMatrix());
        
            cmd.SetGlobalMatrix(ShaderIDs.ShadowMatrixId, m_shadowMatrix);
        }

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public override void OnCameraCleanup(CommandBuffer cmd)
    {
        if (cmd == null)
            throw new ArgumentNullException("cmd");

        if (m_shadowmapTexture)
        {
            RenderTexture.ReleaseTemporary(m_shadowmapTexture);
            m_shadowmapTexture = null;
        }
    }

    private void Clear()
    {
        m_shadowmapTexture = null;
        m_shadowMatrix = Matrix4x4.identity;
    }

    private RenderTexture GetTempShadowTexture(int resolution)
    {
        RenderTextureDescriptor rtd = new RenderTextureDescriptor(resolution, resolution, GraphicsFormat.None, m_format);
        rtd.shadowSamplingMode = m_samplingMode;
        var texture = RenderTexture.GetTemporary(rtd);
        texture.filterMode = m_filterMode;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.antiAliasing = 1;
        return texture;
    }

    private Matrix4x4 GetShadowMatrix(Matrix4x4 projection, Matrix4x4 view)
    {
        if (SystemInfo.usesReversedZBuffer)
        {
            projection.m20 = -projection.m20;
            projection.m21 = -projection.m21;
            projection.m22 = -projection.m22;
            projection.m23 = -projection.m23;
        }

        Matrix4x4 worldToShadow = projection * view;
        
        var texScaleBias = Matrix4x4.identity;
        texScaleBias.m00 = 0.5f;
        texScaleBias.m11 = 0.5f;
        texScaleBias.m22 = 0.5f;
        texScaleBias.m03 = 0.5f;
        texScaleBias.m23 = 0.5f;
        texScaleBias.m13 = 0.5f;

        return texScaleBias * worldToShadow;
    }

    private Vector4 CalculateShadowBiasAndStrength(float projMat_m00)
    {
        float frustumSize = 2.0f / projMat_m00; // we assume that camera is ortographic
        float texelSize = frustumSize / m_resolution;
        
        // texel size shadow/normal bias
        float depth = -m_settings.ShadowBias * texelSize;
        float normal = -m_settings.NormalBias * texelSize;

        const float pcfKernelRadius = 2.5f;
        depth *= pcfKernelRadius;
        normal *= pcfKernelRadius;
        return new Vector4(depth, normal, m_settings.ShadowStrength, 0.0f);
    }

    private void SetupForEmptyRendering()
    {
        m_shadowmapTexture = GetTempShadowTexture(2);
        m_shadowmapIdentifier = new RenderTargetIdentifier(m_shadowmapTexture);
        m_setupEmptyShadowMap = true;
    }

    private void RenderEmptyShadowmap(ref ScriptableRenderContext context)
    {
        
        CommandBuffer cmd = CommandBufferPool.Get();
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        
        cmd.SetGlobalTexture(m_shadowmap.id, m_shadowmapTexture);
        cmd.SetGlobalMatrix(ShaderIDs.ShadowMatrixId, Matrix4x4.identity);
        cmd.SetGlobalVector(ShaderIDs.ShadowProperties, m_emptyProperties);
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }
}

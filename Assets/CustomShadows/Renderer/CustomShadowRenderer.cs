using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


[Serializable]
public class CustomShadowRenderer : ScriptableRenderer
{
    private DrawSkyboxPass m_drawSkyboxPass;
    private CustomShadowRenderPass m_shadowRenderPass;
    private CustomShadowRendererSettings m_settings;
    
    public CustomShadowRenderer(CustomShadowRendererData data) : base(data)
    {
        m_settings = data.Settings;
        //Material copyDepthMat = CoreUtils.CreateEngineMaterial(data.shaders.copyDepthPS);
        
        m_shadowRenderPass =
            new CustomShadowRenderPass(RenderQueueRange.opaque, ref m_settings);
        m_drawSkyboxPass = new DrawSkyboxPass(RenderPassEvent.BeforeRenderingSkybox);
    }

    public override void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        ConfigureCameraTarget(BuiltinRenderTextureType.CameraTarget,
            BuiltinRenderTextureType.CameraTarget);
        
        var passEnabled = m_shadowRenderPass.Setup(ref renderingData);
        
        if (passEnabled)
            EnqueuePass(m_shadowRenderPass);
        
        EnqueuePass(m_drawSkyboxPass); // to avoid some ugly errors
    }
}


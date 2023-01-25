using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable]
[CreateAssetMenu(fileName = "CustomShadowRenderer", menuName = "Rendering/SRP Custom Shadow Renderer")]
public class CustomShadowRendererData : ScriptableRendererData
{
    [Serializable, ReloadGroup]
    public sealed class ShaderResources
    {
        [Reload("Shaders/Utils/CopyDepth.shader")]
        public Shader copyDepthPS;
    }

    [SerializeField]
    private CustomShadowRendererSettings m_settings;
    public CustomShadowRendererSettings Settings => m_settings;
    
    public ShaderResources shaders = null;
    
    protected override ScriptableRenderer Create()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            ResourceReloader.ReloadAllNullIn(this, UniversalRenderPipelineAsset.packagePath);
        }
#endif

        return new CustomShadowRenderer(this);
    }
}

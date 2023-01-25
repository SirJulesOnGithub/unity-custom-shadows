using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;


[Serializable]
public struct CustomShadowRendererSettings
{
    [Header("Base Settings")][Space(5)]
    [SerializeField]
    private LayerMask m_shadowCastersLayerMask;
    public LayerMask ShadowCastersLayerMask => m_shadowCastersLayerMask;

    [SerializeField]
    private RenderPassEvent m_renderPassEvent;
    public RenderPassEvent RenderPassEvent => m_renderPassEvent;

    [SerializeField][Range(0.0f, 10.0f)]
    private float m_shadowBias;
    public float ShadowBias => m_shadowBias;

    [SerializeField][Range(0.0f, 10.0f)] 
    private float m_normalBias;
    public float NormalBias => m_normalBias;

    [SerializeField]
    private float m_shadowStrength;
    public float ShadowStrength => m_shadowStrength;
    
    [SerializeField]
    private Color m_shadowColor;
    public Color ShadowColor => m_shadowColor;

    public CustomShadowRendererSettings(RenderPassEvent rpe = RenderPassEvent.AfterRenderingOpaques)
    {
        m_renderPassEvent = rpe;
        m_shadowCastersLayerMask = (LayerMask)0;
        m_shadowBias = 0.0001f;
        m_normalBias = 0.0001f;
        m_shadowStrength = 0.75f;
        m_shadowColor = Color.gray;
    }
}
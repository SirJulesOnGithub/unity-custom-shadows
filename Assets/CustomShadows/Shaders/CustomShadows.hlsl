#ifndef AM_CUSTOM_SHADOWS_INCLUDED
#define AM_CUSTOM_SHADOWS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#define USE_DEPTH_COMPARISON

CBUFFER_START(CustomShadows)
float4x4 _CustomShadowMatrix;
float4 _CustomShadowProperties;
float4 _CustomShadowLightDirection;
CBUFFER_END

#ifdef USE_DEPTH_COMPARISON
    TEXTURE2D_SHADOW(_CustomShadowTexture);
    SAMPLER_CMP(shadowSampler_linear_clamp);
#else
    TEXTURE2D(_CustomShadowTexture);
    SAMPLER(shadowSampler_linear_clamp);
#endif

real LerpShadow(real b, real t)
{
    real oneMinusT = 1.0 - t;
    return oneMinusT + b * t;
}

half SampleShadowTexture(float4 shadowCoords)
{
    // #if defined(UNITY_REVERSED_Z)
    //     shadowCoords.z = 1.0 - shadowCoords.z;
    // #endif

    half shadowTex = 1.0;
    #ifdef USE_DEPTH_COMPARISON
        shadowTex = SAMPLE_TEXTURE2D_SHADOW(_CustomShadowTexture, shadowSampler_linear_clamp, shadowCoords.xyz).r;
    #else
        shadowTex = SAMPLE_TEXTURE2D(_CustomShadowTexture, shadowSampler_linear_clamp, shadowCoords.xyz).r;
    #endif
    
    // if (shadowTex < shadowCoords.z - _CustomShadowProperties.x)
    //     return 0.5;
    // return 1.0;

    return shadowTex;
}

half CustomShadow(float4 shadowCoord)
{
    half atten = SampleShadowTexture(shadowCoord);
    #if defined(UNITY_REVERSED_Z) // why this :(
         atten = 1.0f - atten;
    #endif
        
    atten = LerpShadow(atten, _CustomShadowProperties.z);
    half shadow = (shadowCoord.z <= 0.0 || shadowCoord.z >= 1.0) ? 1.0 : atten;
    return shadow;
}

float3 CustomShadowBias(float3 positionWS, float3 normalWS)
{
    float invNdotL = 1.0 - saturate(dot(_CustomShadowLightDirection.xyz, normalWS));
    float scale = invNdotL * _CustomShadowProperties.y;

    // normal bias is negative since we want to apply an inset normal offset
    positionWS = _CustomShadowLightDirection.xyz * _CustomShadowProperties.xxx + positionWS;
    positionWS = normalWS * scale.xxx + positionWS;
    return positionWS;
}

float4 GetCustomShadowCoord(float3 positionWS)
{
    float4 shadowCoord = mul(_CustomShadowMatrix, float4(positionWS, 1.0));

    #ifdef UNITY_UV_STARTS_AT_TOP
        shadowCoord.y = 1.0 - shadowCoord.y;
    #endif
    
    return float4(shadowCoord.xyz, 0.0);
}
#endif
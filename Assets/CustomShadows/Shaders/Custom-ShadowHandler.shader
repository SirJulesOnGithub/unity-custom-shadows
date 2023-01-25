Shader "Custom/Shadow Handler"
{
    Properties
    {
        [Header(Textures)] [Space(10)]
        _BaseMap        ("   Albedo(RGB)", 2D) = "white" {}
        [NoScaleOffset]
        _BumpMap      ("   Normal Map(RGB)", 2D) = "bump" {}
        
        [Header(General Settings)] [Space(10)]
        _BaseColor      ("   Base Color", Color) = (1,1,1,1)
        
        [Toggle(CUSTOM_SHADOWS_ENABLED)]
        _CustomShadows ("Enable Custom Shadows", Float) = 0
        
        [Header(Unity Stuff)] [Space(10)]
        [ToggleUI] _ReceiveShadows          ("   Receive Shadows", Float) = 1.0
        [ToggleOff] _SpecularHighlights     ("   Specular Highlights", Float) = 1.0
        [ToggleOff] _EnvironmentReflections ("   Environment Reflections", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #define _NORMALMAP
            #define _SPECULAR_SETUP
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            
            #pragma shader_feature_local CUSTOM_SHADOWS_ENABLED
            
            #include "CustomShadows.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _Occlusion;
                float _Metallic;
                float _Smoothness;
            CBUFFER_END
            
            struct Attributes
            {
                float4 positionOS           : POSITION;
                float3 normalOS             : NORMAL;
                float4 tangentOS            : TANGENT;
                float4 color                : COLOR;
                float2 texcoord             : TEXCOORD0;
                float2 staticLightmapUV     : TEXCOORD1;
                float2 dynamicLightmapUV    : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS               : SV_POSITION;
                float2 uv                       : TEXCOORD0;
                
                float4 vertexColor              : TEXCOORD2;
                half4 normalWS                  : TEXCOORD3;    // xyz: normalWS, w: viewDirWS.x
                half4 tangentWS                 : TEXCOORD4;    // xyz: tangentWS, w: viewDirWS.y
                half4 bitangentWS               : TEXCOORD5;    // xyz: bitangentWS, w: viewDirWS.z
        
                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                    half4 fogFactorAndVertexLight   : TEXCOORD6; // x: fogFactor, yzw: vertex light
                #else
                    half  fogFactor                 : TEXCOORD6;
                #endif
        
                float4 shadowCoord              : TEXCOORD7;
                
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 8);
                #ifdef DYNAMICLIGHTMAP_ON
                    float2  dynamicLightmapUV : TEXCOORD9; // Dynamic lightmap UVs
                #endif
                
                float3 positionWS               : TEXCOORD10;
                
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
                
            void InitializeInputData(Varyings input, half3 normalTS, out InputData inputData)
            {
                inputData = (InputData)0;
        
                inputData.positionWS = input.positionWS;
                
                half3 viewDirWS = half3(input.normalWS.w, input.tangentWS.w, input.bitangentWS.w);
                half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, input.bitangentWS.xyz, input.normalWS.xyz);
                inputData.tangentToWorld = tangentToWorld;
                inputData.normalWS = TransformTangentToWorld(normalTS, tangentToWorld);
                inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
                inputData.viewDirectionWS = SafeNormalize(viewDirWS);
        
                #if defined(CUSTOM_SHADOWS_ENABLED)
                    inputData.shadowCoord = GetCustomShadowCoord(inputData.positionWS);
                #else 
                    #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                        inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
                    #else
                        inputData.shadowCoord = float4(0, 0, 0, 0);
                    #endif
                #endif
        
                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactorAndVertexLight.x);
                    inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
                #else
                    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                #endif
        
                #if defined(DYNAMICLIGHTMAP_ON)
                    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
                #else
                    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
                #endif
        
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
        
                #if defined(DEBUG_DISPLAY)
                    #if defined(DYNAMICLIGHTMAP_ON)
                        inputData.dynamicLightmapUV = input.dynamicLightmapUV;
                    #endif
                
                    #if defined(LIGHTMAP_ON)
                        inputData.staticLightmapUV = input.staticLightmapUV;
                    #else
                        inputData.vertexSH = input.vertexSH;
                    #endif
                #endif
            }
            
            void InitializeSurfaceData(Varyings input, out SurfaceData surfaceData)
            {
                surfaceData = (SurfaceData)0; // avoids "not completely initalized" errors
        
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                
                surfaceData.albedo = albedo.rgb * _BaseColor.rgb;
                surfaceData.normalTS = SampleNormal(input.uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap));
                surfaceData.occlusion = 1.0;
                surfaceData.smoothness = 0.5;
                surfaceData.metallic = 1.0;
                surfaceData.specular = half3(1.0, 1.0, 1.0);
                surfaceData.alpha = 1.0;
            }
            
            Varyings LitPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
        
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
        
                VertexPositionInputs vertexInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
        
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
        
                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInputs.positionWS);
                output.normalWS = half4(normalInputs.normalWS, viewDirWS.x);
                output.tangentWS = half4(normalInputs.tangentWS, viewDirWS.y);
                output.bitangentWS = half4(normalInputs.bitangentWS,viewDirWS.z);
                
                OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
                #ifdef DYNAMICLIGHTMAP_ON
                    output.dynamicLightmapUV = input.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
                #endif
        
                OUTPUT_SH(output.normalWS.xyz, output.vertexSH);
        
                half fogFactor = 0;
                #if !defined(_FOG_FRAGMENT)
                    fogFactor = ComputeFogFactor(vertexInputs.positionCS.z);
                #endif
        
                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                    half3 vertexLight = VertexLighting(vertexInputs.positionWS, normalInputs.normalWS);
                    output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);
                #else
                    output.fogFactor = fogFactor;
                #endif
        
                output.positionWS = vertexInputs.positionWS;
                
                #if defined(CUSTOM_SHADOWS_ENABLED)
                    output.shadowCoord = GetCustomShadowCoord(vertexInputs.positionWS);
                #else
                    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                        output.shadowCoord = GetShadowCoord(vertexInputs);
                    #endif
                #endif
        
                output.positionCS = vertexInputs.positionCS;
                output.vertexColor = input.color;
                return output;
            }
            
            half4 LitPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SurfaceData surfaceData;
                InitializeSurfaceData(input, surfaceData);
        
                InputData inputData;
                InitializeInputData(input, surfaceData.normalTS, inputData);
                SETUP_DEBUG_TEXTURE_DATA(inputData, input.uv12.xy, _MainTex);
                
//              half4 color = UniversalFragmentPBR(inputData, surfaceData);
                half4 color = UniversalFragmentBakedLit(inputData, surfaceData);
        
                #if defined(CUSTOM_SHADOWS_ENABLED)
                    half shadow = CustomShadow(input.shadowCoord);
                    color.rgb *= shadow;
                #endif
                
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1.0; // OutputAlpha(color.a, 0.0);
        
                return color;
            }
            ENDHLSL
        }
        
        Pass 
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            
            #include "CustomShadows.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                //float2 texcoord     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
        
            struct Varyings
            {
                //float2 uv           : TEXCOORD0;
                float4 positionCS   : SV_POSITION;
            };
        
            float4 GetShadowPositionHClip(Attributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    
                float4 positionCS = TransformWorldToHClip(CustomShadowBias(positionWS, normalWS));
                
                #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
        
                return positionCS;
            }
        
            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
        
                output.positionCS = GetShadowPositionHClip(input);
                return output;
            }
        
            half4 ShadowPassFragment(Varyings input) : SV_TARGET
            {
                return 0;
            }

            ENDHLSL
        }
    }
}

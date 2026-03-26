Shader "GPU Animation/Lit"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map (Albedo)", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5

        [NoScaleOffset] _MetallicGlossMap("Metallic Map", 2D) = "white" {}
        [NoScaleOffset] _OcclusionMap("Occlusion Map", 2D) = "white" {}
        _OcclusionStrength("Occlusion Strength", Range(0.0, 1.0)) = 1.0

        [NoScaleOffset] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1.0

        [NoScaleOffset] _EmissionMap("Emission Map", 2D) = "black" {}
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 0)

        [NoScaleOffset] _AnimationTex("Animation Texture", 2D) = "black" {}

        [HideInInspector] _AnimFrame("Anim Frame", Float) = 0
        [HideInInspector] _AnimFrameNext("Anim Frame Next", Float) = 0
        [HideInInspector] _AnimLerp("Anim Lerp", Float) = 0
        [HideInInspector] _PrevAnimFrame("Prev Anim Frame", Float) = 0
        [HideInInspector] _PrevAnimFrameNext("Prev Anim Frame Next", Float) = 0
        [HideInInspector] _PrevAnimLerp("Prev Anim Lerp", Float) = 0
        [HideInInspector] _BlendWeight("Blend Weight", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5

            #pragma vertex GPUAnimVert
            #pragma fragment GPUAnimFrag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"

            #include "GPUAnimationCore.hlsl"

            TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);        SAMPLER(sampler_BumpMap);
            TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
            TEXTURE2D(_OcclusionMap);   SAMPLER(sampler_OcclusionMap);
            TEXTURE2D(_EmissionMap);    SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Metallic;
                half _Smoothness;
                half _OcclusionStrength;
                half _BumpScale;
                half4 _EmissionColor;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(GPUAnimPerInstance)
                UNITY_DEFINE_INSTANCED_PROP(float, _AnimFrame)
                UNITY_DEFINE_INSTANCED_PROP(float, _AnimFrameNext)
                UNITY_DEFINE_INSTANCED_PROP(float, _AnimLerp)
                UNITY_DEFINE_INSTANCED_PROP(float, _PrevAnimFrame)
                UNITY_DEFINE_INSTANCED_PROP(float, _PrevAnimFrameNext)
                UNITY_DEFINE_INSTANCED_PROP(float, _PrevAnimLerp)
                UNITY_DEFINE_INSTANCED_PROP(float, _BlendWeight)
            UNITY_INSTANCING_BUFFER_END(GPUAnimPerInstance)

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 texcoord     : TEXCOORD0;
                float4 boneData     : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                float3 normalWS     : TEXCOORD2;
                float4 tangentWS    : TEXCOORD3;
                float fogFactor     : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings GPUAnimVert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                uint frame        = (uint)UNITY_ACCESS_INSTANCED_PROP(GPUAnimPerInstance, _AnimFrame);
                uint frameNext    = (uint)UNITY_ACCESS_INSTANCED_PROP(GPUAnimPerInstance, _AnimFrameNext);
                float frameLerp   = UNITY_ACCESS_INSTANCED_PROP(GPUAnimPerInstance, _AnimLerp);
                uint prevFrame    = (uint)UNITY_ACCESS_INSTANCED_PROP(GPUAnimPerInstance, _PrevAnimFrame);
                uint prevNext     = (uint)UNITY_ACCESS_INSTANCED_PROP(GPUAnimPerInstance, _PrevAnimFrameNext);
                float prevLerp    = UNITY_ACCESS_INSTANCED_PROP(GPUAnimPerInstance, _PrevAnimLerp);
                float blendWeight = UNITY_ACCESS_INSTANCED_PROP(GPUAnimPerInstance, _BlendWeight);

                float3 posOS = input.positionOS.xyz;
                float3 nrmOS = input.normalOS;
                float4 tanOS = input.tangentOS;

                ApplyGPUAnimationFullBlend(posOS, nrmOS, tanOS, input.boneData, frame, frameNext, frameLerp,
                    prevFrame, prevNext, prevLerp, blendWeight);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(posOS);
                VertexNormalInputs normalInput = GetVertexNormalInputs(nrmOS, tanOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;

                real sign = input.tangentOS.w * GetOddNegativeScale();
                output.tangentWS = float4(normalInput.tangentWS.xyz, sign);

                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

                return output;
            }

            half4 GPUAnimFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half4 metallicGloss = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, input.uv);
                half occlusion = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, input.uv).g;
                occlusion = lerp(1.0h, occlusion, _OcclusionStrength);

                half metallic = metallicGloss.r * _Metallic;
                half smoothness = metallicGloss.a * _Smoothness;

                float3 normalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);

                float sgn = input.tangentWS.w;
                float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
                half3x3 TBN = half3x3(input.tangentWS.xyz, bitangent, input.normalWS.xyz);
                float3 normalWS = normalize(mul(normalTS, TBN));

                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                    inputData.shadowCoord = float4(0, 0, 0, 0);
                #endif

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo.rgb;
                surfaceData.metallic = metallic;
                surfaceData.smoothness = smoothness;
                surfaceData.normalTS = normalTS;
                surfaceData.emission = emission;
                surfaceData.occlusion = occlusion;
                surfaceData.alpha = albedo.a;
                surfaceData.specular = half3(0, 0, 0);
                surfaceData.clearCoatMask = 0;
                surfaceData.clearCoatSmoothness = 0;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, input.fogFactor);

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
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            #include "GPUAnimationCore.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Metallic;
                half _Smoothness;
                half _OcclusionStrength;
                half _BumpScale;
                half4 _EmissionColor;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(GPUAnimPerInstance)
                UNITY_DEFINE_INSTANCED_PROP(float, _AnimFrame)
                UNITY_DEFINE_INSTANCED_PROP(float, _AnimFrameNext)
                UNITY_DEFINE_INSTANCED_PROP(float, _AnimLerp)
                UNITY_DEFINE_INSTANCED_PROP(float, _PrevAnimFrame)
                UNITY_DEFINE_INSTANCED_PROP(float, _PrevAnimFrameNext)
                UNITY_DEFINE_INSTANCED_PROP(float, _PrevAnimLerp)
                UNITY_DEFINE_INSTANCED_PROP(float, _BlendWeight)
            UNITY_INSTANCING_BUFFER_END(GPUAnimPerInstance)

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 boneData     : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);

                uint frame        = (uint)UNITY_ACCESS_INSTANCED_PROP(GPUAnimPerInstance, _AnimFrame);
                uint frameNext    = (uint)UNITY_ACCESS_INSTANCED_PROP(GPUAnimPerInstance, _AnimFrameNext);
                float frameLerp   = UNITY_ACCESS_INSTANCED_PROP(GPUAnimPerInstance, _AnimLerp);
                uint prevFrame    = (uint)UNITY_ACCESS_INSTANCED_PROP(GPUAnimPerInstance, _PrevAnimFrame);
                uint prevNext     = (uint)UNITY_ACCESS_INSTANCED_PROP(GPUAnimPerInstance, _PrevAnimFrameNext);
                float prevLerp    = UNITY_ACCESS_INSTANCED_PROP(GPUAnimPerInstance, _PrevAnimLerp);
                float blendWeight = UNITY_ACCESS_INSTANCED_PROP(GPUAnimPerInstance, _BlendWeight);

                float3 posOS = input.positionOS.xyz;
                float3 nrmOS = input.normalOS;
                ApplyGPUAnimationBlend(posOS, nrmOS, input.boneData, frame, frameNext, frameLerp,
                    prevFrame, prevNext, prevLerp, blendWeight);

                float3 positionWS = TransformObjectToWorld(posOS);
                float3 normalWS = TransformObjectToWorldNormal(nrmOS);

                output.positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, _LightDirection));

                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #include "GPUAnimationCore.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Metallic;
                half _Smoothness;
                half _OcclusionStrength;
                half _BumpScale;
                half4 _EmissionColor;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(GPUAnimPerInstance)
                UNITY_DEFINE_INSTANCED_PROP(float, _AnimFrame)
                UNITY_DEFINE_INSTANCED_PROP(float, _AnimFrameNext)
                UNITY_DEFINE_INSTANCED_PROP(float, _AnimLerp)
                UNITY_DEFINE_INSTANCED_PROP(float, _PrevAnimFrame)
                UNITY_DEFINE_INSTANCED_PROP(float, _PrevAnimFrameNext)
                UNITY_DEFINE_INSTANCED_PROP(float, _PrevAnimLerp)
                UNITY_DEFINE_INSTANCED_PROP(float, _BlendWeight)
            UNITY_INSTANCING_BUFFER_END(GPUAnimPerInstance)

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float4 boneData     : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);

                uint frame        = (uint)UNITY_ACCESS_INSTANCED_PROP(GPUAnimPerInstance, _AnimFrame);
                uint frameNext    = (uint)UNITY_ACCESS_INSTANCED_PROP(GPUAnimPerInstance, _AnimFrameNext);
                float frameLerp   = UNITY_ACCESS_INSTANCED_PROP(GPUAnimPerInstance, _AnimLerp);
                uint prevFrame    = (uint)UNITY_ACCESS_INSTANCED_PROP(GPUAnimPerInstance, _PrevAnimFrame);
                uint prevNext     = (uint)UNITY_ACCESS_INSTANCED_PROP(GPUAnimPerInstance, _PrevAnimFrameNext);
                float prevLerp    = UNITY_ACCESS_INSTANCED_PROP(GPUAnimPerInstance, _PrevAnimLerp);
                float blendWeight = UNITY_ACCESS_INSTANCED_PROP(GPUAnimPerInstance, _BlendWeight);

                float3 posOS = input.positionOS.xyz;
                ApplyGPUAnimationBlendPosOnly(posOS, input.boneData, frame, frameNext, frameLerp,
                    prevFrame, prevNext, prevLerp, blendWeight);

                output.positionCS = TransformObjectToHClip(posOS);

                return output;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

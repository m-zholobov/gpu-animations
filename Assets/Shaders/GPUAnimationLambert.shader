Shader "GPU Animation/Lambert"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)

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

        LOD 150

        Pass
        {
            Name "ForwardLambert"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5

            #pragma vertex LambertVert
            #pragma fragment LambertFrag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #include "GPUAnimationCore.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
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
                float2 texcoord     : TEXCOORD0;
                float4 boneData     : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                half3 diffuse       : TEXCOORD1;
                float fogFactor     : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings LambertVert(Attributes input)
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
                ApplyGPUAnimationBlend(posOS, nrmOS, input.boneData, frame, frameNext, frameLerp,
                    prevFrame, prevNext, prevLerp, blendWeight);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(posOS);
                float3 normalWS = TransformObjectToWorldNormal(nrmOS);

                Light mainLight = GetMainLight();
                half NdotL = saturate(dot(normalWS, mainLight.direction));
                half3 lighting = mainLight.color * NdotL;

                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                    uint lightCount = GetAdditionalLightsCount();
                    for (uint i = 0u; i < lightCount; i++)
                    {
                        Light addLight = GetAdditionalLight(i, vertexInput.positionWS);
                        half addNdotL = saturate(dot(normalWS, addLight.direction));
                        lighting += addLight.color * addNdotL * addLight.distanceAttenuation;
                    }
                #endif

                half3 ambient = SampleSH(normalWS);
                output.diffuse = lighting + ambient;

                output.positionCS = vertexInput.positionCS;
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

                return output;
            }

            half4 LambertFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half3 color = albedo.rgb * input.diffuse;
                color = MixFog(color, input.fogFactor);

                return half4(color, albedo.a);
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

                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));

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

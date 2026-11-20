// 천장·기둥 조명의 빛나는 면. 빛을 받지 않고 Base Color + Emission Color를 그대로 낸다
// (전에 쓰던 URP Particles/Unlit과 같은 결과).
//
// Particles/Unlit과 다른 점은 하나다: 조명 종류(Lamp Bloom Volume 항목)마다 Bloom을 따로 조절할 수 있다.
//   - 씬의 "Corridor Ceiling / Living Room Ceiling / Pillar Lamp Bloom Volume" 중 하나가 이 램프의 번짐을 맡는다.
//   - 그 Volume의 Intensity가 0이거나 Volume이 없으면 예전처럼 씬 전체 Bloom(Lamp Bloom Volume)을 따른다.
// 실제 번짐은 렌더러(PC_Renderer)의 Lamp Bloom 기능이 "LampBloom" 패스를 따로 그려서 만든다.
Shader "Semester/Environment/Lamp Emissive"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [HDR] _EmissionColor("Emission Color (added on top)", Color) = (0, 0, 0, 1)
        [Enum(CorridorCeiling, 0, LivingRoomCeiling, 1, Pillar, 2)] _LampBloomGroup("Lamp Bloom Volume that controls this lamp", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "IgnoreProjector"="True" }

        Cull [_Cull]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "LampBloomSource.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _EmissionColor;
            half _LampBloomGroup;
            half _Cull;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            half4 color : COLOR;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            half3 normalWS : TEXCOORD2;
            half4 color : COLOR;
            half fogFactor : TEXCOORD3;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.color = input.color;
            output.fogFactor = ComputeFogFactor(output.positionCS.z);
            return output;
        }

        // 램프 면이 내는 빛(HDR). 누르기 전의 값이다.
        half3 LampColor(Varyings input)
        {
            half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb * input.color.rgb;
            return albedo + _EmissionColor.rgb;
        }
        ENDHLSL

        // 화면에 보이는 램프 면.
        Pass
        {
            Name "Lamp"
            Tags { "LightMode"="UniversalForwardOnly" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/AmbientOcclusion.hlsl"

            half4 Frag(Varyings input) : SV_Target
            {
                half3 color = LampColor(input);

                // 이 종류의 전용 Bloom이 켜진 카메라에서는 씬 Bloom에 잡히지 않도록 면을 상한 아래로 누른다.
                // 눌린 만큼의 빛은 아래 "LampBloom" 패스를 거쳐 전용 Bloom으로 돌아온다.
                half limit = dot(LampBloomSelect(_LampBloomGroup), _LampBloomBodyLimits.xyz);
                if (limit > 0.0h)
                {
                    color = LampBloomLimitBody(color, limit);
                }

                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor occlusion = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(input.positionCS));
                    color *= occlusion.directAmbientOcclusion;
                #endif

                color = MixFog(color, InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor));
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDepth

            half FragDepth(Varyings input) : SV_Target
            {
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode"="DepthNormalsOnly" }

            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDepthNormals

            half4 FragDepthNormals(Varyings input) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0h);
            }
            ENDHLSL
        }

        // 전용 Bloom의 재료. 일반 그리기에서는 쓰이지 않고 LampBloomFeature가 이 패스만 골라 따로 그린다.
        // 누르지 않은 빛 중 Volume의 Threshold보다 밝은 부분에 Intensity와 Tint를 곱해서 내보낸다.
        // 벽이나 캐릭터에 가린 부분은 깊이로 걸러진다(같은 면을 한 번 더 그리는 것이라 살짝 앞으로 당겨서 비교한다).
        Pass
        {
            Name "LampBloom"
            Tags { "LightMode"="LampBloom" }

            ZWrite Off
            ZTest LEqual
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBloomSource

            half4 FragBloomSource(Varyings input) : SV_Target
            {
                half3 select = LampBloomSelect(_LampBloomGroup);
                // 번지는 넓이(Scatter)가 다른 종류는 따로 흐려야 해서, 지금 그리는 묶음에 속한 종류만 남긴다.
                clip(dot(select, _LampBloomBucket.xyz) - 0.5h);

                half3 color = LampBloomThreshold(LampColor(input), dot(select, _LampBloomThresholds.xyz));
                half3 tint = select.x * _LampBloomTint0.rgb + select.y * _LampBloomTint1.rgb + select.z * _LampBloomTint2.rgb;
                return half4(color * tint * dot(select, _LampBloomIntensities.xyz), 1.0h);
            }
            ENDHLSL
        }
    }
}

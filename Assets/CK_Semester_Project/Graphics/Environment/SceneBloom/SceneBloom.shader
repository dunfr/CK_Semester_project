// 배경과 동적 객체(캐릭터·몬스터)를 따로 번지게 하는 Bloom의 계산 단계. SceneBloomFeature가 차례로 쓴다.
// 임계값·흐림·섞기·더하기는 URP Bloom(가우시안)과 같은 식이라, 두 Volume 항목을 같은 값으로 두면 URP Bloom 하나와 같은 그림이 나온다.
// 다른 점은 하나뿐이다: 밝은 부분을 고를 때 "동적 객체 마스크"로 화면을 둘로 나눠서, 한쪽 몫만 가져간다.
Shader "Hidden/Semester/Scene Bloom"
{
    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        TEXTURE2D_X(_SceneBloomMask);
        TEXTURE2D_X(_SceneBloomLowMip);

        float4 _SceneBloomFilter;     // x: 섞는 비율(Scatter), y: 밝기 상한, z: 임계값(리니어), w: 임계값 무릎
        float4 _SceneBloomGroup;      // x: 0 = 배경 몫, 1 = 동적 객체 몫
        float4 _SceneBloomComposite;  // rgb: 세기 × 색
        float4 _SceneBloomLowMipSize; // 작은 단계의 (너비, 높이, 1/너비, 1/높이)

        half3 SampleSource(float2 uv)
        {
            return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
        }

        // 화면 색 중 이 그룹의 몫만 남긴다. 마스크는 동적 객체가 보이는 픽셀에서 1이다.
        half3 SampleGroup(float2 uv)
        {
            half mask = SAMPLE_TEXTURE2D_X(_SceneBloomMask, sampler_LinearClamp, uv).r;
            half weight = lerp(1.0h - mask, mask, (half)_SceneBloomGroup.x);
            return SampleSource(uv) * weight;
        }

        half4 FragPrefilter(Varyings input) : SV_Target
        {
            float2 uv = input.texcoord;

        #if defined(_SCENE_BLOOM_HQ)
            // URP Bloom의 High Quality Filtering과 같은 13탭(반짝임이 덜하다).
            float2 texel = _BlitTexture_TexelSize.xy;
            half3 a = SampleGroup(uv + texel * float2(-1.0, -1.0));
            half3 b = SampleGroup(uv + texel * float2(0.0, -1.0));
            half3 c = SampleGroup(uv + texel * float2(1.0, -1.0));
            half3 d = SampleGroup(uv + texel * float2(-0.5, -0.5));
            half3 e = SampleGroup(uv + texel * float2(0.5, -0.5));
            half3 f = SampleGroup(uv + texel * float2(-1.0, 0.0));
            half3 g = SampleGroup(uv);
            half3 h = SampleGroup(uv + texel * float2(1.0, 0.0));
            half3 i = SampleGroup(uv + texel * float2(-0.5, 0.5));
            half3 j = SampleGroup(uv + texel * float2(0.5, 0.5));
            half3 k = SampleGroup(uv + texel * float2(-1.0, 1.0));
            half3 l = SampleGroup(uv + texel * float2(0.0, 1.0));
            half3 m = SampleGroup(uv + texel * float2(1.0, 1.0));

            half2 div = (1.0 / 4.0) * half2(0.5, 0.125);
            half3 color = (d + e + i + j) * div.x;
            color += (a + b + g + f) * div.y;
            color += (b + c + h + g) * div.y;
            color += (f + g + l + k) * div.y;
            color += (g + h + m + l) * div.y;
        #else
            half3 color = SampleGroup(uv);
        #endif

            // 임계값(부드러운 무릎). URP Bloom과 같은 식이다.
            color = min(_SceneBloomFilter.y, color);
            half brightness = Max3(color.r, color.g, color.b);
            half softness = clamp(brightness - _SceneBloomFilter.z + _SceneBloomFilter.w, 0.0, 2.0 * _SceneBloomFilter.w);
            softness = (softness * softness) / (4.0 * _SceneBloomFilter.w + 1e-4);
            half multiplier = max(brightness - _SceneBloomFilter.z, softness) / max(brightness, 1e-4);
            color *= multiplier;

            return half4(max(color, 0.0), 1.0);
        }

        // 절반으로 줄이면서 가로로 흐린다(9탭 가우시안).
        half4 FragBlurHorizontal(Varyings input) : SV_Target
        {
            float texel = _BlitTexture_TexelSize.x * 2.0;
            float2 uv = input.texcoord;

            half3 color = SampleSource(uv - float2(texel * 4.0, 0.0)) * 0.01621622
                        + SampleSource(uv - float2(texel * 3.0, 0.0)) * 0.05405405
                        + SampleSource(uv - float2(texel * 2.0, 0.0)) * 0.12162162
                        + SampleSource(uv - float2(texel * 1.0, 0.0)) * 0.19459459
                        + SampleSource(uv) * 0.22702703
                        + SampleSource(uv + float2(texel * 1.0, 0.0)) * 0.19459459
                        + SampleSource(uv + float2(texel * 2.0, 0.0)) * 0.12162162
                        + SampleSource(uv + float2(texel * 3.0, 0.0)) * 0.05405405
                        + SampleSource(uv + float2(texel * 4.0, 0.0)) * 0.01621622;

            return half4(color, 1.0);
        }

        // 같은 크기에서 세로로 흐린다(쌍선형 보간을 이용한 5탭, 9탭과 같은 결과).
        half4 FragBlurVertical(Varyings input) : SV_Target
        {
            float texel = _BlitTexture_TexelSize.y;
            float2 uv = input.texcoord;

            half3 color = SampleSource(uv - float2(0.0, texel * 3.23076923)) * 0.07027027
                        + SampleSource(uv - float2(0.0, texel * 1.38461538)) * 0.31621622
                        + SampleSource(uv) * 0.22702703
                        + SampleSource(uv + float2(0.0, texel * 1.38461538)) * 0.31621622
                        + SampleSource(uv + float2(0.0, texel * 3.23076923)) * 0.07027027;

            return half4(color, 1.0);
        }

        // 큰 단계(선명)와 작은 단계(넓게 번짐)를 섞는다.
        half4 FragUpsample(Varyings input) : SV_Target
        {
            half3 high = SampleSource(input.texcoord);
        #if defined(_SCENE_BLOOM_HQ)
            half3 low = SampleTexture2DBicubic(TEXTURE2D_X_ARGS(_SceneBloomLowMip, sampler_LinearClamp), input.texcoord,
                _SceneBloomLowMipSize, (1.0).xx, unity_StereoEyeIndex).rgb;
        #else
            half3 low = SAMPLE_TEXTURE2D_X(_SceneBloomLowMip, sampler_LinearClamp, input.texcoord).rgb;
        #endif
            return half4(lerp(high, low, _SceneBloomFilter.x), 1.0);
        }

        // 톤매핑 전의 화면에 그대로 더한다(URP Bloom과 같다).
        half4 FragComposite(Varyings input) : SV_Target
        {
            return half4(SampleSource(input.texcoord) * _SceneBloomComposite.rgb, 0.0);
        }

        struct MaskAttributes
        {
            float4 positionOS : POSITION;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct MaskVaryings
        {
            float4 positionCS : SV_POSITION;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        MaskVaryings VertMask(MaskAttributes input)
        {
            MaskVaryings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            return output;
        }

        half4 FragMask(MaskVaryings input) : SV_Target
        {
            return half4(1.0, 1.0, 1.0, 1.0);
        }
    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }

        Pass // 0: 동적 객체가 보이는 픽셀을 1로 칠한다(카메라 깊이로 가려진 부분은 빠진다).
        {
            Name "Scene Bloom Mask"

            ZTest LEqual
            ZWrite Off
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex VertMask
            #pragma fragment FragMask
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass // 1
        {
            Name "Scene Bloom Prefilter"

            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragPrefilter
            #pragma multi_compile_local_fragment _ _SCENE_BLOOM_HQ
            ENDHLSL
        }

        Pass // 2
        {
            Name "Scene Bloom Blur Horizontal"

            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBlurHorizontal
            ENDHLSL
        }

        Pass // 3
        {
            Name "Scene Bloom Blur Vertical"

            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBlurVertical
            ENDHLSL
        }

        Pass // 4
        {
            Name "Scene Bloom Upsample"

            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragUpsample
            #pragma multi_compile_local_fragment _ _SCENE_BLOOM_HQ
            ENDHLSL
        }

        Pass // 5
        {
            Name "Scene Bloom Composite"

            ZTest Always
            ZWrite Off
            Cull Off
            // 더하기. 알파는 건드리지 않는다.
            Blend One One, Zero One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite
            ENDHLSL
        }
    }
}

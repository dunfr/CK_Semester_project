// 홀로그램 전용 Bloom의 계산 단계. HologramBloomFeature가 차례로 쓴다.
// 흐림·섞기는 URP Bloom(가우시안)과 같은 식이라, Volume의 Scatter가 기본 Bloom과 같은 감각으로 움직인다.
// 임계값은 여기서 하지 않는다. 빛을 내보내는 쪽("HologramBloom" 패스, HologramBloomSource.hlsl)이 이미 적용해서 준다.
// 마지막 단계만 다르다: 톤매핑이 끝난 화면 위에 얹으므로, 1을 넘지 않게 눌러서 스크린 방식으로 합친다.
Shader "Hidden/Semester/Hologram Bloom"
{
    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        TEXTURE2D_X(_HologramBloomLowMip);

        float4 _HologramBloomFilter;    // x: 섞는 비율, y: 밝기 상한
        float4 _HologramBloomComposite; // x: 세기, yzw: 색

        half3 SampleSource(float2 uv)
        {
            return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
        }

        half4 FragPrefilter(Varyings input) : SV_Target
        {
            // 절반 해상도로 줄이기만 한다(너무 밝은 값만 상한으로 자른다).
            return half4(clamp(SampleSource(input.texcoord), 0.0, _HologramBloomFilter.y), 1.0);
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
            half3 low = SAMPLE_TEXTURE2D_X(_HologramBloomLowMip, sampler_LinearClamp, input.texcoord).rgb;
            return half4(lerp(high, low, _HologramBloomFilter.x), 1.0);
        }

        half4 FragComposite(Varyings input) : SV_Target
        {
            half3 bloom = SampleSource(input.texcoord) * _HologramBloomComposite.x * _HologramBloomComposite.yzw;
            // 화면은 이미 0~1로 정리된 뒤라서, 번짐도 1에 부드럽게 붙도록 누른다(그대로 더하면 밝은 곳이 뚝 잘린다).
            return half4(1.0 - exp(-bloom), 1.0);
        }
    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off

        Pass // 0
        {
            Name "Hologram Bloom Prefilter"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragPrefilter
            ENDHLSL
        }

        Pass // 1
        {
            Name "Hologram Bloom Blur Horizontal"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBlurHorizontal
            ENDHLSL
        }

        Pass // 2
        {
            Name "Hologram Bloom Blur Vertical"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBlurVertical
            ENDHLSL
        }

        Pass // 3
        {
            Name "Hologram Bloom Upsample"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragUpsample
            ENDHLSL
        }

        Pass // 4
        {
            Name "Hologram Bloom Composite"

            // 스크린 합성: 결과 = 번짐 + 화면 × (1 - 번짐). 알파는 건드리지 않는다.
            Blend One OneMinusSrcColor, Zero One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite
            ENDHLSL
        }
    }
}

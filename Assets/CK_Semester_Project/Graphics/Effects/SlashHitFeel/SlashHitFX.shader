// 베기 타격감 이펙트(번쩍임·가로 퍼짐·흰 슬래시·파편)용 유니티 URP 파티클 셰이더.
// 텍스처 알파가 모양이고, 파티클 Color over Lifetime(버텍스 컬러)과 HDR 색을 곱한다.
// Additive(_SrcBlend=SrcAlpha, _DstBlend=One) / Alpha(SrcAlpha, OneMinusSrcAlpha)는 머티리얼에서 고른다.
Shader "CK/Effects/SlashHitFX"
{
    Properties
    {
        [MainTexture] _BaseMap("Shape (alpha)", 2D) = "white" {}
        [HDR] _BaseColor("Color", Color) = (1, 1, 1, 1)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("Z Test", Float) = 8
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "SlashHitFXUnlit"
            Tags { "LightMode"="UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            Cull Off
            ZWrite Off
            ZTest [_ZTest]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 shape = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 tint = _BaseColor * input.color;
                return half4(tint.rgb * shape.rgb, tint.a * shape.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}

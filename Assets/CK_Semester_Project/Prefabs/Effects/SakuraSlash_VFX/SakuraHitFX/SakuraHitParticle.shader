Shader "Sakura/Hit Particle"
{
    Properties
    {
        [HDR] _CoreColor("Core Color", Color) = (1, 0.96, 1, 1)
        [HDR] _GlowColor("Glow Color", Color) = (0.65, 0.20, 1, 1)
        _Intensity("Intensity", Range(0, 20)) = 3
        _CoreStrength("Core Strength", Range(0, 4)) = 1
        _CoreRadius("Core Radius", Range(0, 0.9)) = 0.15
        _CoreFeather("Core Feather", Range(0.01, 1)) = 0.2
        _GlowFalloff("Glow Falloff", Range(0.5, 8)) = 2
        _Diamond("Diamond Shape", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "SakuraHitUnlit"
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            Cull Off
            ZWrite Off
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _CoreColor;
                float4 _GlowColor;
                float _Intensity;
                float _CoreStrength;
                float _CoreRadius;
                float _CoreFeather;
                float _GlowFalloff;
                float _Diamond;
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
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = abs(input.uv * 2.0 - 1.0);
                float distance = lerp(length(p), p.x + p.y, _Diamond);
                float glow = pow(saturate(1.0 - distance), _GlowFalloff);
                float core = 1.0 - smoothstep(_CoreRadius, min(1.0, _CoreRadius + max(0.001, _CoreFeather)), distance);
                float3 emission = (_CoreColor.rgb * _CoreColor.a * core * _CoreStrength
                    + _GlowColor.rgb * _GlowColor.a * glow) * _Intensity;
                // Additive RGB is faded explicitly so Particle Color over Lifetime works.
                emission *= input.color.rgb * saturate(input.color.a);
                return half4(emission, 0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}

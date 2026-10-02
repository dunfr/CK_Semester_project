Shader "Semester/Fuchsia/Lens Toon"
{
    Properties
    {
        _BaseColor("Lens tint", Color) = (.86,.95,1,1)
        _Opacity("Lens opacity", Range(0,1)) = .035
        _EdgeOpacity("Edge opacity", Range(0,1)) = .16
        _EdgePower("Edge power", Range(1,12)) = 5
        [HDR] _HighlightColor("Highlight color", Color) = (.9,.95,1,1)
        _HighlightStrength("Highlight strength", Range(0,1)) = .2
        _HighlightSize("Highlight band size", Range(.001,.3)) = .015
        _HighlightFeather("Highlight softness", Range(.001,.1)) = .005
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull mode", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Cull [_Cull]
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _HighlightColor;
                half _Opacity, _EdgeOpacity, _EdgePower, _HighlightStrength;
                half _HighlightSize, _HighlightFeather, _Cull;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                half3 n = normalize(i.normalWS);
                half3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);
                Light light = GetMainLight();
                half edge = pow(saturate(1.0h - abs(dot(n, view))), _EdgePower);
                half specular = saturate(dot(n, SafeNormalize(light.direction + view)));
                half band = smoothstep(1.0h - _HighlightSize - _HighlightFeather, 1.0h - _HighlightSize + _HighlightFeather, specular);
                band *= saturate(dot(n, light.direction));
                half alpha = saturate(_Opacity + edge * _EdgeOpacity + band * _HighlightStrength);
                half3 color = _BaseColor.rgb + _HighlightColor.rgb * light.color * band * _HighlightStrength;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}

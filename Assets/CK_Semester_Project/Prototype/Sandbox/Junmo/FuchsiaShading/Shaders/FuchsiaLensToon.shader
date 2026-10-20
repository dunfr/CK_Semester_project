Shader "Semester/Fuchsia/Lens Toon"
{
    Properties
    {
        [HideInInspector] _CharacterKeyDirection("Character key direction and blend", Vector) = (0,1,0,0)
        [HideInInspector] _CharacterKeyColor("Character key linear color", Vector) = (1,1,1,0)
        _EnvironmentShadowStrength("Environment shadow dimming", Range(0,1)) = 1
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
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "FuchsiaLighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _HighlightColor;
                half4 _CharacterKeyDirection, _CharacterKeyColor;
                half _EnvironmentShadowStrength;
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
                Light light = FuchsiaKeyLight(i.positionWS);
                half environmentVisibility = FuchsiaEnvironmentVisibility(light.shadowAttenuation,
                    _CharacterKeyDirection.w, 1.0h, _EnvironmentShadowStrength);
                FuchsiaApplyCharacterKey(light, _CharacterKeyDirection, _CharacterKeyColor.rgb);
                half edge = pow(saturate(1.0h - abs(dot(n, view))), _EdgePower);
                half specular = saturate(dot(n, SafeNormalize(light.direction + view)));
                half band = smoothstep(1.0h - _HighlightSize - _HighlightFeather, 1.0h - _HighlightSize + _HighlightFeather, specular);
                band *= saturate(dot(n, light.direction)) * light.shadowAttenuation * min(light.distanceAttenuation, 1.0);
                half alpha = saturate(_Opacity + edge * _EdgeOpacity + band * _HighlightStrength);
                half3 color = _BaseColor.rgb + _HighlightColor.rgb * light.color * band * _HighlightStrength * environmentVisibility;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}

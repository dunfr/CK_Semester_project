Shader "Semester/Character Toon"
{
    Properties
    {
        _BaseMap("Color", 2D) = "white" {}
        _BaseColor("Tint", Color) = (1,1,1,1)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull mode", Float) = 2
        _ShadeColor("First shade tint", Color) = (.65,.69,.78,1)
        _DeepShadeColor("Deep shade tint", Color) = (.38,.43,.55,1)
        _ShadeStep("Light / shade boundary", Range(0,1)) = .57
        _DeepShadeStep("Deep shade boundary", Range(0,1)) = .29
        _ShadeFeather("Boundary softness", Range(.001,.2)) = .025
        _AmbientStrength("Ambient fill", Range(0,1)) = .12
        [Toggle(_EMISSION)] _UseEmission("Enable emission", Float) = 0
        _EmissionMap("Emission mask", 2D) = "black" {}
        [HDR] _EmissionColor("Emission", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull [_Cull]
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor, _ShadeColor, _DeepShadeColor, _EmissionColor;
            half _ShadeStep, _DeepShadeStep, _ShadeFeather, _AmbientStrength, _UseEmission, _Cull;
        CBUFFER_END
        struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
        struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; };
        Varyings Vert(Attributes v)
        {
            Varyings o;
            o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
            o.positionCS = TransformWorldToHClip(o.positionWS);
            o.normalWS = TransformObjectToWorldNormal(v.normalOS);
            o.uv = TRANSFORM_TEX(v.uv,_BaseMap);
            return o;
        }
        ENDHLSL
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local_fragment _EMISSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
            half4 Frag(Varyings i):SV_Target
            {
                half3 n = normalize(i.normalWS);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(i.positionWS));
                #else
                    float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                #endif
                Light light = GetMainLight(shadowCoord);
                half illumination = saturate(dot(n, light.direction) * .5h + .5h) * light.shadowAttenuation;
                half deepStep = min(_DeepShadeStep, _ShadeStep);
                half lit = smoothstep(_ShadeStep - _ShadeFeather, _ShadeStep + _ShadeFeather, illumination);
                half shade = smoothstep(deepStep - _ShadeFeather, deepStep + _ShadeFeather, illumination);
                half3 tint = lerp(_DeepShadeColor.rgb, _ShadeColor.rgb, shade);
                tint = lerp(tint, half3(1,1,1), lit);
                half3 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                // Constant ambient SH term: no reflection probe texture or per-pixel SH evaluation.
                half3 ambient = max(half3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w), 0) * _AmbientStrength;
                half3 color = baseColor * (tint * light.color * light.distanceAttenuation + ambient);
                #if defined(_EMISSION)
                    color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv).rgb * _EmissionColor.rgb;
                #endif
                return half4(color, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode"="MotionVectors" }
            ColorMask RG
            HLSLPROGRAM
            #pragma vertex MotionVert
            #pragma fragment MotionFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MotionVectorsCommon.hlsl"
            struct MotionInput { float4 positionOS:POSITION; float3 previousOS:TEXCOORD4; };
            struct MotionOutput { float4 positionCS:SV_POSITION; float4 currentCS:TEXCOORD0; float4 previousCS:TEXCOORD1; };
            MotionOutput MotionVert(MotionInput i)
            {
                MotionOutput o;
                o.positionCS=TransformObjectToHClip(i.positionOS.xyz);
                o.currentCS=mul(_NonJitteredViewProjMatrix,mul(UNITY_MATRIX_M,i.positionOS));
                float4 previous=unity_MotionVectorsParams.x==1 ? float4(i.previousOS,1) : i.positionOS;
                o.previousCS=mul(_PrevViewProjMatrix,mul(UNITY_PREV_MATRIX_M,previous));
                return o;
            }
            float4 MotionFrag(MotionOutput i):SV_Target
            {
                if (unity_MotionVectorsParams.y == 0.0) return 0;
                return float4(CalcNdcMotionVectorFromCsPositions(i.currentCS,i.previousCS),0,0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment DepthFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection, _LightPosition;
            float4 ShadowVert(Attributes v):SV_POSITION
            {
                float3 p=TransformObjectToWorld(v.positionOS.xyz);
                float3 n=TransformObjectToWorldNormal(v.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirection=normalize(_LightPosition-p);
                #else
                float3 lightDirection=_LightDirection;
                #endif
                float4 clip=TransformWorldToHClip(ApplyShadowBias(p,n,lightDirection));
                #if UNITY_REVERSED_Z
                clip.z=min(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
                #else
                clip.z=max(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
                #endif
                return clip;
            }
            half4 DepthFrag():SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DepthFrag
            half DepthFrag(Varyings i):SV_Target { return i.positionCS.z; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment NormalFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            half4 NormalFrag(Varyings i):SV_Target
            {
                float3 n=normalize(i.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(n)*.5+.5)),0);
                #else
                return half4(n,0);
                #endif
            }
            ENDHLSL
        }
    }
}

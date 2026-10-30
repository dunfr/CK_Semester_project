Shader "Semester/Environment/Corridor Floor Reflection"
{
    Properties
    {
        _Strength("Reflection strength", Range(0,1)) = 0.65
        _Tint("Reflection tint", Color) = (.88,.94,1,1)
        _BlurPixels("Reflection blur in pixels", Range(0,5)) = 1.5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-10" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        Pass
        {
            Name "FloorReflection"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_CorridorPlanarReflection); SAMPLER(sampler_CorridorPlanarReflection);
            float4x4 _CorridorReflectionMatrix;
            float4 _CorridorReflectionTexelSize;
            float4 _CorridorReflectionSourcePosition;
            half _CorridorReflectionAvailable;
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Strength, _BlurPixels;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float3 positionWS:TEXCOORD0;
                float4 reflection:TEXCOORD1;
            };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.reflection = mul(_CorridorReflectionMatrix, float4(o.positionWS,1));
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float2 uv = i.reflection.xy / i.reflection.w * .5 + .5;
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0 - uv.y;
                #endif
                float2 offset = _CorridorReflectionTexelSize.xy * _BlurPixels;
                half3 color = SAMPLE_TEXTURE2D(_CorridorPlanarReflection, sampler_CorridorPlanarReflection, uv).rgb * .4;
                color += SAMPLE_TEXTURE2D(_CorridorPlanarReflection, sampler_CorridorPlanarReflection, uv + offset).rgb * .15;
                color += SAMPLE_TEXTURE2D(_CorridorPlanarReflection, sampler_CorridorPlanarReflection, uv - offset).rgb * .15;
                color += SAMPLE_TEXTURE2D(_CorridorPlanarReflection, sampler_CorridorPlanarReflection, uv + offset * float2(1,-1)).rgb * .15;
                color += SAMPLE_TEXTURE2D(_CorridorPlanarReflection, sampler_CorridorPlanarReflection, uv + offset * float2(-1,1)).rgb * .15;
                half3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half fresnel = .25h + .75h * pow(saturate(1.0h - abs(view.y)), 3);
                // 타일 사이의 틈에는 반사막을 겹치지 않는다.
                float2 tile = frac(float2((i.positionWS.x + 3.0)/1.2, (i.positionWS.z + 7.0)/2.0));
                float2 edge = min(tile, 1.0 - tile);
                half seams = smoothstep(.012,.022,min(edge.x,edge.y));
                half screenFade = smoothstep(0,.025,min(min(uv.x,uv.y),min(1-uv.x,1-uv.y)));
                // 비교 카메라에서는 원본 카메라의 반사를 재사용하지 않고 PBR 바닥만 표시한다.
                half sourceMatches = 1.0h - step(.02, distance(_WorldSpaceCameraPos, _CorridorReflectionSourcePosition.xyz));
                return half4(color * _Tint.rgb, _Strength * fresnel * seams * screenFade * step(0,i.reflection.w) * sourceMatches * _CorridorReflectionAvailable);
            }
            ENDHLSL
        }
    }
}

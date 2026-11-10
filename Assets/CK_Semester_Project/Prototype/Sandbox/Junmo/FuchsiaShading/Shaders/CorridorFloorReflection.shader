Shader "Semester/Environment/Corridor Floor Reflection"
{
    Properties
    {
        _Strength("Reflection strength", Range(0,1)) = 0.65
        _Tint("Reflection tint", Color) = (.88,.94,1,1)
        _BlurPixels("Reflection blur in pixels", Range(0,5)) = 1.5
        _SeamMask("Tile seam mask (corridor test floor tiles)", Range(0,1)) = 1
        _PlaneHeightFade("Fade on floors away from the reflection plane", Range(0,1)) = 0
        _FloorColorLoss("Floor color hidden by reflection (1 = blend over, 0 = add on top)", Range(0,1)) = 1
        _BlurMip("Glossy blur as mip level (0 = off, uses pixel blur)", Range(0,6)) = 0
        _GrazingBoost("Stronger at grazing angles (1 = old behavior)", Range(0,1)) = 1
        _NearDistance("Full strength within distance (m)", Float) = 0
        _FarDistance("Fade end distance (m, 0 = no distance fade)", Float) = 0
        _FarStrength("Strength left at fade end", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-10" }
        // 미리 곱한 알파: 반사색은 더하고, 바닥색은 알파 × _FloorColorLoss만큼만 가린다(1이면 예전 알파 블렌드와 같다).
        Blend One OneMinusSrcAlpha
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
            float _CorridorReflectionPlaneY;
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Strength, _BlurPixels, _SeamMask, _PlaneHeightFade, _FloorColorLoss;
                half _BlurMip, _GrazingBoost, _NearDistance, _FarDistance, _FarStrength;
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
                // 거울처럼 또렷하지 않게: _BlurMip가 있으면 밉맵을 다섯 번 겹쳐 넓고 부드럽게 흐린다.
                float lod = max(_BlurMip, 0.0);
                float2 offset = _CorridorReflectionTexelSize.xy * (lod > 0.01 ? exp2(lod) * 0.75 : _BlurPixels);
                half3 color = SAMPLE_TEXTURE2D_LOD(_CorridorPlanarReflection, sampler_CorridorPlanarReflection, uv, lod).rgb * .4;
                color += SAMPLE_TEXTURE2D_LOD(_CorridorPlanarReflection, sampler_CorridorPlanarReflection, uv + offset, lod).rgb * .15;
                color += SAMPLE_TEXTURE2D_LOD(_CorridorPlanarReflection, sampler_CorridorPlanarReflection, uv - offset, lod).rgb * .15;
                color += SAMPLE_TEXTURE2D_LOD(_CorridorPlanarReflection, sampler_CorridorPlanarReflection, uv + offset * float2(1,-1), lod).rgb * .15;
                color += SAMPLE_TEXTURE2D_LOD(_CorridorPlanarReflection, sampler_CorridorPlanarReflection, uv + offset * float2(-1,1), lod).rgb * .15;
                half3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half fresnel = lerp(1.0h, .25h + .75h * pow(saturate(1.0h - abs(view.y)), 3), _GrazingBoost);
                // 가까운 바닥일수록 반사가 강하고 멀어질수록 약해진다(_FarDistance가 0이면 끈다).
                float viewDistance = distance(_WorldSpaceCameraPos, i.positionWS);
                half distanceFade = _FarDistance > 0.001
                    ? lerp(1.0h, _FarStrength, saturate((viewDistance - _NearDistance) / max(_FarDistance - _NearDistance, 0.001)))
                    : 1.0h;
                fresnel *= distanceFade;
                // 타일 사이의 틈에는 반사막을 겹치지 않는다.
                float2 tile = frac(float2((i.positionWS.x + 3.0)/1.2, (i.positionWS.z + 7.0)/2.0));
                float2 edge = min(tile, 1.0 - tile);
                half seams = lerp(1.0h, smoothstep(.012,.022,min(edge.x,edge.y)), _SeamMask);
                // 반사 평면은 하나라서, 평면 높이에서 벗어난 다른 층 바닥에는 반사막을 흐린다.
                seams *= lerp(1.0h, 1.0h - smoothstep(.15, .4, abs(i.positionWS.y - _CorridorReflectionPlaneY)), _PlaneHeightFade);
                half screenFade = smoothstep(0,.025,min(min(uv.x,uv.y),min(1-uv.x,1-uv.y)));
                // 비교 카메라에서는 원본 카메라의 반사를 재사용하지 않고 PBR 바닥만 표시한다.
                half sourceMatches = 1.0h - step(.02, distance(_WorldSpaceCameraPos, _CorridorReflectionSourcePosition.xyz));
                half alpha = _Strength * fresnel * seams * screenFade * step(0,i.reflection.w) * sourceMatches * _CorridorReflectionAvailable;
                return half4(color * _Tint.rgb * alpha, alpha * _FloorColorLoss);
            }
            ENDHLSL
        }
    }
}

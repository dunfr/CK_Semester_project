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
        _FresnelMin("Fresnel: strength looking straight down", Range(0,1)) = 0.25
        _FresnelPower("Fresnel: curve power (higher = only near grazing)", Range(1,8)) = 3
        _GrazingSharpness("Sharper reflection at grazing angles", Range(0,1)) = 0
        _PlaneFadeStart("Plane height fade: full reflection within (m)", Range(0,1)) = 0.15
        _PlaneFadeEnd("Plane height fade: no reflection beyond (m)", Range(0,1)) = 0.4
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-10" }
        // 미리 곱한 알파: 반사색은 더하고, 바닥색은 알파 × _FloorColorLoss만큼만 가린다(1이면 예전 알파 블렌드와 같다).
        Blend One OneMinusSrcAlpha
        ZWrite Off
        // 바닥과 같은 메시를 겹쳐 그리는 반사막이다. 플레이 중 바닥은 정적 배칭으로 정점이 미리 월드 좌표가 되어
        // 깊이가 미세하게 어긋나므로, 카메라 쪽으로 살짝 당겨 깊이 겹침(z-fighting) 깜빡임을 막는다.
        Offset -1, -1
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
                half _FresnelMin, _FresnelPower, _GrazingSharpness, _PlaneFadeStart, _PlaneFadeEnd;
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
                half3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);
                // 바닥을 비스듬히 볼수록(시선이 바닥과 평행에 가까울수록) 1에 가깝다.
                half grazing = saturate(1.0h - abs(view.y));
                // 거울처럼 또렷하지 않게: _BlurMip가 있으면 밉맵을 다섯 번 겹쳐 넓고 부드럽게 흐린다.
                // 실제 광택면처럼 비스듬히 볼수록 반사가 또렷해진다(_GrazingSharpness, 0이면 예전과 같다).
                half sharpen = 1.0h - _GrazingSharpness * grazing * grazing;
                float lod = max(_BlurMip, 0.0) * sharpen;
                float2 offset = _CorridorReflectionTexelSize.xy * (lod > 0.01 ? exp2(lod) * 0.75 : _BlurPixels * sharpen);
                half3 color = SAMPLE_TEXTURE2D_LOD(_CorridorPlanarReflection, sampler_CorridorPlanarReflection, uv, lod).rgb * .4;
                color += SAMPLE_TEXTURE2D_LOD(_CorridorPlanarReflection, sampler_CorridorPlanarReflection, uv + offset, lod).rgb * .15;
                color += SAMPLE_TEXTURE2D_LOD(_CorridorPlanarReflection, sampler_CorridorPlanarReflection, uv - offset, lod).rgb * .15;
                color += SAMPLE_TEXTURE2D_LOD(_CorridorPlanarReflection, sampler_CorridorPlanarReflection, uv + offset * float2(1,-1), lod).rgb * .15;
                color += SAMPLE_TEXTURE2D_LOD(_CorridorPlanarReflection, sampler_CorridorPlanarReflection, uv + offset * float2(-1,1), lod).rgb * .15;
                // 프레넬: 내려다볼 때는 _FresnelMin, 비스듬할수록 1로 커진다. _GrazingBoost가 0이면 각도와 상관없이 1이다.
                half fresnel = lerp(1.0h, _FresnelMin + (1.0h - _FresnelMin) * pow(grazing, _FresnelPower), _GrazingBoost);
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
                // 반사 평면은 하나라서, 평면 높이에서 벗어난 바닥(다른 방·다른 층)에는 반사막을 흐린다.
                // 높이가 다른 바닥에 그대로 두면 반사가 바닥에서 떠 보인다.
                seams *= lerp(1.0h, 1.0h - smoothstep(_PlaneFadeStart, max(_PlaneFadeEnd, _PlaneFadeStart + 0.001h),
                    abs(i.positionWS.y - _CorridorReflectionPlaneY)), _PlaneHeightFade);
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

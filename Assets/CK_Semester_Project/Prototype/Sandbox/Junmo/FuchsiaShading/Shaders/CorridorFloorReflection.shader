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
        // 바닥 재질의 표면 마감(부위별 매끈함)을 반사에도 반영한다. 0이면 예전처럼 바닥 전체가 같은 세기·같은 흐림으로 반사한다.
        // 매끈함은 CorridorPlanarReflection이 바닥 재질(Hide In Reflection의 첫 Stylized Lit 재질)에서 읽어 전역 값으로 넘긴다.
        [Header(Floor Surface Finish)]
        _FinishResponse("Follow the floor's surface finish (0 = uniform)", Range(0,1)) = 0
        _FinishReference("Finish: smoothness that reflects as tuned above", Range(0,1)) = 0.65
        _FinishRange("Finish: smoothness difference that counts as full change", Range(0.02,0.6)) = 0.2
        _FinishRoughStrength("Finish: reflection left on the roughest parts", Range(0,1)) = 0.35
        _FinishRoughBlur("Finish: extra blur on the roughest parts (mip levels)", Range(0,4)) = 2
        _FinishGlossBoost("Finish: extra reflection on the smoothest parts", Range(0,1)) = 0.25
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
            // 두 번째 반사: 플레이어가 선 바닥과 높이가 다른 바닥이 보일 때 CorridorPlanarReflection이 채운다.
            TEXTURE2D(_CorridorPlanarReflection2); SAMPLER(sampler_CorridorPlanarReflection2);
            float4x4 _CorridorReflectionMatrix2;
            half _CorridorReflectionAvailable2;
            float _CorridorReflectionPlaneY2;
            // 바닥 재질의 표면 마감 값(CorridorPlanarReflection이 채운다). 바닥 셰이더와 같은 식으로 매끈함을 다시 계산한다.
            #include "BackgroundSurfaceFinish.hlsl"
            TEXTURE2D(_CorridorFloorSurfaceMap);
            TEXTURE2D(_CorridorFloorPartMap);
            half4 _CorridorFloorRemap;      // 러프니스 맵 범위 다시 펴기(맵 낮은 값, 높은 값, 그때의 매끈함 둘)
            half4 _CorridorFloorVariation;  // 부위별 변화량, 얼룩 크기(m), 가장자리 변화, 모서리 면 광택 양
            half4 _CorridorFloorFinish;     // x Smoothness 배율, y 모서리 면 매끈함, z 값이 채워졌는지(1/0)
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Strength, _BlurPixels, _SeamMask, _PlaneHeightFade, _FloorColorLoss;
                half _BlurMip, _GrazingBoost, _NearDistance, _FarDistance, _FarStrength;
                half _FresnelMin, _FresnelPower, _GrazingSharpness, _PlaneFadeStart, _PlaneFadeEnd;
                half _FinishResponse, _FinishReference, _FinishRange, _FinishRoughStrength, _FinishRoughBlur, _FinishGlossBoost;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float3 positionWS:TEXCOORD0;
                float4 reflection:TEXCOORD1;
                float4 reflection2:TEXCOORD2;
                float2 uv:TEXCOORD3;
            };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.reflection = mul(_CorridorReflectionMatrix, float4(o.positionWS,1));
                o.reflection2 = mul(_CorridorReflectionMatrix2, float4(o.positionWS,1));
                // 반사막은 바닥 타일과 같은 메시라 UV도 같다(바닥 재질의 타일링은 1로 본다).
                o.uv = i.uv;
                return o;
            }
            // 바닥 재질과 같은 매끈함을 이 픽셀에서 다시 계산해, 기준보다 거친 정도(-1)~매끈한 정도(+1)로 돌려준다.
            half FloorFinish(float2 uv, float3 positionWS)
            {
                if (_FinishResponse <= 0.001h || _CorridorFloorFinish.z < 0.5h)
                {
                    return 0;
                }
                half mapSmoothness = SAMPLE_TEXTURE2D(_CorridorFloorSurfaceMap, sampler_LinearRepeat, uv).a;
                half smoothness = BgFinishRemap(mapSmoothness, _CorridorFloorRemap) * _CorridorFloorFinish.x;
                half partId = SAMPLE_TEXTURE2D_LOD(_CorridorFloorPartMap, sampler_PointRepeat, uv, 0).r;
                half2 bevelInside = SAMPLE_TEXTURE2D(_CorridorFloorPartMap, sampler_LinearRepeat, uv).gb;
                smoothness = BgFinishSmoothness(smoothness, partId, bevelInside, positionWS, _CorridorFloorVariation, _CorridorFloorFinish.y);
                return clamp((smoothness - _FinishReference) / max(_FinishRange, 0.02h), -1.0h, 1.0h) * _FinishResponse;
            }
            // 흐린 광택 반사: 가운데 한 번과 대각선 네 번을 겹쳐 읽는다.
            half3 SampleReflection(TEXTURE2D_PARAM(reflectionTexture, reflectionSampler), float2 uv, float2 offset, float lod)
            {
                half3 color = SAMPLE_TEXTURE2D_LOD(reflectionTexture, reflectionSampler, uv, lod).rgb * .4;
                color += SAMPLE_TEXTURE2D_LOD(reflectionTexture, reflectionSampler, uv + offset, lod).rgb * .15;
                color += SAMPLE_TEXTURE2D_LOD(reflectionTexture, reflectionSampler, uv - offset, lod).rgb * .15;
                color += SAMPLE_TEXTURE2D_LOD(reflectionTexture, reflectionSampler, uv + offset * float2(1,-1), lod).rgb * .15;
                color += SAMPLE_TEXTURE2D_LOD(reflectionTexture, reflectionSampler, uv + offset * float2(-1,1), lod).rgb * .15;
                return color;
            }
            half4 Frag(Varyings i):SV_Target
            {
                // 이 픽셀 높이에 더 가까운 반사 평면을 고른다(방마다 바닥 높이가 10 cm쯤 다르다).
                float planeDistance1 = abs(i.positionWS.y - _CorridorReflectionPlaneY);
                float planeDistance2 = _CorridorReflectionAvailable2 > 0.5h ? abs(i.positionWS.y - _CorridorReflectionPlaneY2) : 1e6;
                bool useSecond = planeDistance2 < planeDistance1;
                float4 reflection = useSecond ? i.reflection2 : i.reflection;
                float planeDistance = min(planeDistance1, planeDistance2);
                float2 uv = reflection.xy / reflection.w * .5 + .5;
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0 - uv.y;
                #endif
                half3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);
                // 바닥을 비스듬히 볼수록(시선이 바닥과 평행에 가까울수록) 1에 가깝다.
                half grazing = saturate(1.0h - abs(view.y));
                // 거울처럼 또렷하지 않게: _BlurMip가 있으면 밉맵을 다섯 번 겹쳐 넓고 부드럽게 흐린다.
                // 실제 광택면처럼 비스듬히 볼수록 반사가 또렷해진다(_GrazingSharpness, 0이면 예전과 같다).
                half sharpen = 1.0h - _GrazingSharpness * grazing * grazing;
                // 표면 마감: 기준보다 거친 부위는 반사가 더 흐리고 약하게, 매끈한 부위는 조금 더 또렷하고 강하게 비친다.
                half finish = FloorFinish(i.uv, i.positionWS);
                half rough = saturate(-finish), glossy = saturate(finish);
                float lod = max(max(_BlurMip, 0.0) * sharpen * (1.0 - 0.5 * glossy) + rough * _FinishRoughBlur, 0.0);
                float2 offset = _CorridorReflectionTexelSize.xy * (lod > 0.01 ? exp2(lod) * 0.75 : _BlurPixels * sharpen);
                half3 color;
                UNITY_BRANCH if (useSecond)
                {
                    color = SampleReflection(TEXTURE2D_ARGS(_CorridorPlanarReflection2, sampler_CorridorPlanarReflection2), uv, offset, lod);
                }
                else
                {
                    color = SampleReflection(TEXTURE2D_ARGS(_CorridorPlanarReflection, sampler_CorridorPlanarReflection), uv, offset, lod);
                }
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
                // 두 반사 평면 어느 쪽 높이에도 맞지 않는 바닥(그 밖의 방·다른 층)에는 반사막을 흐린다.
                // 높이가 다른 바닥에 그대로 두면 반사가 바닥에서 떠 보인다.
                seams *= lerp(1.0h, 1.0h - smoothstep(_PlaneFadeStart, max(_PlaneFadeEnd, _PlaneFadeStart + 0.001h),
                    planeDistance), _PlaneHeightFade);
                half screenFade = smoothstep(0,.025,min(min(uv.x,uv.y),min(1-uv.x,1-uv.y)));
                // 비교 카메라에서는 원본 카메라의 반사를 재사용하지 않고 PBR 바닥만 표시한다.
                half sourceMatches = 1.0h - step(.02, distance(_WorldSpaceCameraPos, _CorridorReflectionSourcePosition.xyz));
                half finishStrength = lerp(1.0h, _FinishRoughStrength, rough) * (1.0h + _FinishGlossBoost * glossy);
                half alpha = saturate(_Strength * finishStrength) * fresnel * seams * screenFade * step(0,reflection.w) * sourceMatches * _CorridorReflectionAvailable;
                return half4(color * _Tint.rgb * alpha, alpha * _FloorColorLoss);
            }
            ENDHLSL
        }
    }
}

#ifndef FUCHSIA_TOON_COMMON_INCLUDED
#define FUCHSIA_TOON_COMMON_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "FuchsiaLighting.hlsl"

TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
TEXTURE2D(_FaceShadowMap); SAMPLER(sampler_FaceShadowMap);
TEXTURE2D(_HairHighlightMap); SAMPLER(sampler_HairHighlightMap);
TEXTURE2D_X_FLOAT(_FuchsiaHairDepthTexture); SAMPLER(sampler_FuchsiaHairDepthTexture);
float4 _FuchsiaHairDepthParams;
// 모든 패스의 상수 배치를 맞춰 SRP Batcher 호환성을 유지한다.
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor, _ShadeColor, _DeepShadeColor, _EmissionColor;
    half4 _HighlightColor, _RimColor;
    half4 _CharacterKeyDirection, _CharacterKeyColor;
    half _EnvironmentShadowStrength;
    half _ShadeStep, _DeepShadeStep, _ShadeFeather, _AmbientStrength, _UseEmission, _Cull;
    half _ReceiveShadowStrength, _ShadingStrength, _DirectLightStrength, _DirectLightCeiling;
    half _AdditionalLightStrength, _AdditionalLightCeiling, _AdditionalShadowFill;
    half _HighlightStrength, _HighlightSize, _HighlightFeather;
    half _RimStrength, _RimPower;
    half4 _StyleRightOS, _StyleUpOS, _StyleForwardOS;
    half _StyleNormalStrength, _FaceStyleStrength, _FaceShadowFeather, _FaceAmbientFill;
    half _FaceShadowOffset, _HairShadowStrength, _HairShadowDistance, _HairShadowBias;
    half _HairStyleStrength, _HairBandHeight, _HairBandWidth, _HairBandFeather, _HairAmbientFill;
    half _HairHighlightMaskStrength;
CBUFFER_END

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    float3 styleNormalOS : TEXCOORD1;
    float4 styleData : TEXCOORD2;
};
struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    half3 normalWS : TEXCOORD1;
    float2 uv : TEXCOORD2;
    half3 tangentWS : TEXCOORD3;
    half3 styleNormalWS : TEXCOORD4;
    half4 styleData : TEXCOORD5;
};
Varyings Vert(Attributes v)
{
    Varyings o;
    o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
    o.positionCS = TransformWorldToHClip(o.positionWS);
    o.normalWS = TransformObjectToWorldNormal(v.normalOS);
    o.tangentWS = TransformObjectToWorldDir(v.tangentOS.xyz);
    o.styleNormalWS = TransformObjectToWorldNormal(v.styleNormalOS, false);
    o.styleData = v.styleData;
    o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
    return o;
}
half4 FuchsiaFrag(Varyings i)
{
    half3 n = normalize(i.normalWS);
    // 원본 FBX를 사용하는 프리팹에는 보정 채널이 없으므로 기존 셰이딩으로 돌아간다.
    half styleAvailable = step(.0001h, dot(i.styleNormalWS, i.styleNormalWS));
    half faceStyleStrength = _FaceStyleStrength * styleAvailable;
    half hairStyleStrength = _HairStyleStrength * styleAvailable;
    if (_StyleNormalStrength * styleAvailable > .001h && dot(i.styleNormalWS, i.styleNormalWS) > .001h)
    {
        n = normalize(lerp(n, normalize(i.styleNormalWS), _StyleNormalStrength));
    }
    half3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);
    Light light = FuchsiaKeyLight(i.positionWS);
    half environmentVisibility = FuchsiaEnvironmentVisibility(light.shadowAttenuation,
        _CharacterKeyDirection.w, _ReceiveShadowStrength, _EnvironmentShadowStrength);
    FuchsiaApplyCharacterKey(light, _CharacterKeyDirection, _CharacterKeyColor.rgb);
    half3 fill = FuchsiaAdditionalFill(i.positionWS, GetNormalizedScreenSpaceUV(i.positionCS),
        n, _ReceiveShadowStrength, _AdditionalLightStrength, _AdditionalLightCeiling);
    half shadow = lerp(1.0h, light.shadowAttenuation, _ReceiveShadowStrength);
    // 그림자 한 단계의 방향은 주광으로만 결정한다. 보조광의 수와 위치는 경계를 바꾸지 않는다.
    half illumination = saturate(dot(n, light.direction)) * shadow;
    half feather = max(_ShadeFeather, fwidth(illumination) * 0.75h);
    half lit = smoothstep(_ShadeStep - feather, _ShadeStep + feather, illumination);
    half3 styleUp = half3(0, 1, 0);
    #if defined(FUCHSIA_FACE) || defined(FUCHSIA_HAIR)
        if (faceStyleStrength + hairStyleStrength > .001h)
        {
            styleUp = normalize(TransformObjectToWorldDir(_StyleUpOS.xyz));
        }
    #endif
    #if defined(FUCHSIA_FACE)
        if (faceStyleStrength > .001h)
        {
            half3 faceForward = normalize(TransformObjectToWorldDir(_StyleForwardOS.xyz));
            half3 faceRight = normalize(TransformObjectToWorldDir(_StyleRightOS.xyz));
            half3 horizontalLight = light.direction - styleUp * dot(light.direction, styleUp);
            horizontalLight = dot(horizontalLight, horizontalLight) > .015h
                ? normalize(horizontalLight) : faceForward;
            float2 faceUV = saturate(i.styleData.xy);
            faceUV.x = dot(horizontalLight, faceRight) < 0 ? 1.0 - faceUV.x : faceUV.x;
            half faceThreshold = saturate(acos(clamp(dot(horizontalLight, faceForward), -.9999h, .9999h))
                / PI + _FaceShadowOffset);
            half faceField = SAMPLE_TEXTURE2D(_FaceShadowMap, sampler_FaceShadowMap, faceUV).r;
            half faceLit = smoothstep(faceThreshold - _FaceShadowFeather,
                faceThreshold + _FaceShadowFeather, faceField);
            // 별도 머리 깊이 패스에서, 주광 쪽으로 조금 이동한 지점의 가림을 검사한다.
            // 양의 시선 깊이를 저장해 플랫폼별 reversed-Z 규칙에 의존하지 않는다.
            if (_HairShadowStrength > .001h && _FuchsiaHairDepthParams.w > .5)
            {
                float3 rayPositionWS = i.positionWS + light.direction * _HairShadowDistance;
                float4 rayCS = TransformWorldToHClip(rayPositionWS);
                float4 rayScreen = ComputeScreenPos(rayCS);
                float2 rayUV = rayScreen.xy / rayScreen.w;
                float hairDepth = SAMPLE_TEXTURE2D_X(_FuchsiaHairDepthTexture,
                    sampler_FuchsiaHairDepthTexture, rayUV).r;
                float rayDepth = -TransformWorldToView(rayPositionWS).z;
                float separation = rayDepth - hairDepth;
                half inScreen = step(0.0, min(min(rayUV.x, rayUV.y), min(1.0-rayUV.x, 1.0-rayUV.y)))
                    * step(0.0, rayCS.w);
                half hairOcclusion = smoothstep(_HairShadowBias, _HairShadowBias + .003, separation)
                    * (1.0h - smoothstep(.09, .13, separation)) * inScreen;
                hairOcclusion *= smoothstep(.32h, .58h, i.styleData.y);
                faceLit = min(faceLit, 1.0h - hairOcclusion * _HairShadowStrength);
            }
            faceLit = min(faceLit, shadow);
            half frontMask = smoothstep(-.15h, .45h, dot(n, faceForward));
            lit = lerp(lit, faceLit, faceStyleStrength * frontMask);
        }
    #endif
    half3 shadeTint = lerp(half3(1, 1, 1), _ShadeColor.rgb, _ShadingStrength);
    half3 tint = lerp(shadeTint, half3(1, 1, 1), lit);
    half3 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
    // 주변광은 직접광 그림자에 곱하지 않아, 그늘에서도 환경의 색과 방향을 유지한다.
    half3 ambient = max(SampleSH(n), 0) * _AmbientStrength;
    #if defined(FUCHSIA_FACE)
        ambient += max(SampleSH(styleUp), 0) * _FaceAmbientFill * faceStyleStrength;
    #elif defined(FUCHSIA_HAIR)
        ambient += max(SampleSH(styleUp), 0) * _HairAmbientFill * hairStyleStrength;
    #endif
    // 밝은 실내의 광원 수가 늘어도 캐릭터의 텍스처와 색을 보존한다.
    // RGB를 같은 배율로 제한하여 주변 조명의 색조는 유지한다.
    half3 directResponse = light.color * light.distanceAttenuation * _DirectLightStrength * environmentVisibility;
    half responseLuminance = dot(directResponse, half3(.2126h,.7152h,.0722h));
    directResponse *= min(1.0h, _DirectLightCeiling / max(responseLuminance, .0001h));
    // 실제 실내광도 한 단계 명암 안에서 작동한다. 주광과 보조광을 합친 뒤
    // 같은 RGB 배율로 제한하여 광원 반응을 키워도 흰색으로 날아가지 않게 한다.
    half3 surfaceLighting = directResponse + fill * lerp(_AdditionalShadowFill, 1.0h, lit);
    half surfaceLuminance = dot(surfaceLighting, half3(.2126h, .7152h, .0722h));
    surfaceLighting *= min(1.0h, _DirectLightCeiling / max(surfaceLuminance, .0001h));
    half3 color = baseColor * (ambient + surfaceLighting) * tint;

    #if defined(FUCHSIA_HAIR) || defined(FUCHSIA_ACCESSORY)
        half3 halfDirection = SafeNormalize(light.direction + view);
        half highlight;
        #if defined(FUCHSIA_HAIR)
            // UV 접선에서 수직인 결 방향을 이용해 머리카락의 띠 광택을 만든다.
            half3 tangent = i.tangentWS - n * dot(n, i.tangentWS);
            half3 axis = abs(n.y) < 0.95h ? half3(0, 1, 0) : half3(1, 0, 0);
            if (dot(tangent, tangent) < 0.0001h) { tangent = cross(axis, n); }
            half3 strand = normalize(cross(n, normalize(tangent)));
            half alongStrand = dot(strand, halfDirection);
            highlight = sqrt(saturate(1.0h - alongStrand * alongStrand));
        #else
            highlight = saturate(dot(n, halfDirection));
        #endif
        half band = smoothstep(1.0h - _HighlightSize - _HighlightFeather, 1.0h - _HighlightSize + _HighlightFeather, highlight);
        #if defined(FUCHSIA_HAIR)
            if (hairStyleStrength > .001h)
            {
                half3 styleRight = normalize(TransformObjectToWorldDir(_StyleRightOS.xyz));
                // 원통 투영 제어맵으로 모발 덩어리별 띠의 끊김·높이·폭을 지정한다.
                float2 controlUV = float2(atan2(i.styleData.x, i.styleData.z) / TWO_PI + .5, i.styleData.y);
                half3 control = SAMPLE_TEXTURE2D(_HairHighlightMap, sampler_HairHighlightMap, controlUV).rgb;
                control = lerp(half3(1, .5, .5), control, _HairHighlightMaskStrength);
                half bandCenter = _HairBandHeight + .045h * (1.0h - saturate(i.styleData.x * i.styleData.x))
                    + .035h * dot(view, styleUp) + .025h * dot(light.direction, styleRight)
                    + (control.g - .5h) * .12h;
                half distanceToBand = abs(i.styleData.y - bandCenter);
                half bandFeather = max(_HairBandFeather, fwidth(i.styleData.y));
                half bandWidth = _HairBandWidth * lerp(.5h, 1.4h, control.b);
                band = 1.0h - smoothstep(bandWidth, bandWidth + bandFeather, distanceToBand);
                band *= control.r * smoothstep(.0h, .45h, dot(n, view)) * lerp(.65h, 1.0h, lit)
                    * lerp(.65h, 1.0h, saturate(dot(n, light.direction))) * shadow;
                half3 highlightLight = directResponse + fill * .2h;
                color += _HighlightColor.rgb * highlightLight
                    * band * _HighlightStrength * hairStyleStrength;
            }
            else
        #endif
        {
            // 반대편 광택과 그림자 안의 광택을 억제한다.
            band *= saturate(dot(n, light.direction)) * shadow;
            color += _HighlightColor.rgb * light.color * min(light.distanceAttenuation, 1.0)
                * band * _HighlightStrength * _DirectLightStrength;
        }
    #endif
    #if defined(FUCHSIA_HAIR) || defined(FUCHSIA_ACCESSORY)
        half rim = pow(1.0h - saturate(dot(n, view)), _RimPower);
        color += _RimColor.rgb * light.color * min(light.distanceAttenuation, 1.0) * rim * _RimStrength * _DirectLightStrength * saturate(dot(n, light.direction)) * shadow;
    #endif
    #if defined(_EMISSION)
        color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv).rgb * _EmissionColor.rgb;
    #endif
    return half4(color, 1);
}
#endif

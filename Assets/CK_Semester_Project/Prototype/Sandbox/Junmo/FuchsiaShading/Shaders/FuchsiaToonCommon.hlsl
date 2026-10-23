#ifndef FUCHSIA_TOON_COMMON_INCLUDED
#define FUCHSIA_TOON_COMMON_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
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
    // 0: _Style*OS가 오브젝트 공간(정적 메시). 1: 월드 공간(스킨 메시, FuchsiaStyleAxes가 머리 뼈 회전으로 매 프레임 채움).
    half _StyleAxesSpace;
    // 애니메 룩: 텍스처 색을 그대로 보여주는 밝은 면, 채도를 올린 그늘, 경계 띠, 깊이 림, 외곽선
    half4 _ShadeEdgeColor, _OutlineColor, _DepthRimColor;
    half _LitBrightness, _LightNormalize, _ShadeSaturation, _ShadeEdgeWidth, _ShadeAmbientTint;
    half _OutlineWidth, _OutlineZOffset, _OutlineSaturation, _OutlineLightTint;
    half _DepthRimWidth, _DepthRimThreshold, _DepthRimStrength, _DepthRimShadowFade;
    half _ShadeEdgeSaturation, _ShadeEdgeHardness, _BacklightShadeDim, _BacklightHighlightFade;
    half _OutlineDarkWiden, _OutlineShadeWiden, _OutlineUseThickness, _OutlineThinScale;
    half _OutlineMinScale, _OutlineMaxScale;
    // CharacterLighting 컴포넌트가 렌더러별로 채운다. x 기준 광량, y 최소, z 최대, w 반응 정도.
    half4 _CharacterBrightness;
    // 구역/보스 연출용 림 색조. rgb 색, a 세기 배율.
    half4 _CharacterRimTint;
CBUFFER_END

// 휘도를 유지한 채 채도만 조절한다.
half3 FuchsiaSaturation(half3 color, half amount)
{
    half luminance = dot(color, half3(.2126h, .7152h, .0722h));
    return max(lerp(luminance.xxx, color, amount), 0);
}

// 광원의 색조만 분리한다. 밝기는 재질이 정해, 실내 광량이 바뀌어도 원화 색을 유지한다.
half3 FuchsiaLightHue(half3 radiance)
{
    half luminance = dot(radiance, half3(.2126h, .7152h, .0722h));
    return luminance > .0001h ? min(radiance / luminance, 2.0h) : half3(1, 1, 1);
}

// 화면 공간 깊이 차이로 실루엣 안쪽에 일정한 픽셀 폭의 림을 만든다.
// 뒤쪽 배경과의 깊이 차가 클 때만 켜지므로 몸 안쪽 굴곡에는 생기지 않는다.
half FuchsiaDepthRim(float4 positionCS, half3 normalWS, half3 viewWS)
{
    if (_DepthRimStrength <= .001h || !IsPerspectiveProjection())
    {
        return 0;
    }
    float depth = LinearEyeDepth(positionCS.z, _ZBufferParams);
    half3 normalVS = TransformWorldToViewNormal(normalWS, true);
    // cot(30°) = 1.732: 시야각 60도, 1080p, 1m 거리 기준의 픽셀 폭
    float widthPx = _DepthRimWidth * (_ScaledScreenParams.y / 1080.0)
        * (unity_CameraProjection._m11 / 1.732) / max(depth, .1);
    widthPx = min(widthPx, 48.0);
    float2 offset = float2(normalVS.x >= 0 ? widthPx : -widthPx, 0);
    uint2 index = (uint2)clamp(positionCS.xy + offset, 0, _ScaledScreenParams.xy - 1);
    float offsetDepth = LinearEyeDepth(LoadSceneDepth(index), _ZBufferParams);
    half rim = smoothstep(_DepthRimThreshold, _DepthRimThreshold * 1.6, offsetDepth - depth);
    return rim * smoothstep(.05h, .55h, 1.0h - saturate(dot(normalWS, viewWS)));
}

// 얼굴/머리 기준 축. 스킨 메시는 머리 뼈를 따라 도는 월드 축을 받는다.
half3 FuchsiaStyleAxis(half4 axis)
{
    return normalize(_StyleAxesSpace > .5h ? axis.xyz : TransformObjectToWorldDir(axis.xyz));
}

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
    // 스킨 메시: UV1은 머리 기준축(오른쪽, 위, 앞) 성분으로 구워져 있어, 머리 뼈를 따라 도는 축으로 다시 조립한다.
    o.styleNormalWS = _StyleAxesSpace > .5
        ? v.styleNormalOS.x * _StyleRightOS.xyz + v.styleNormalOS.y * _StyleUpOS.xyz + v.styleNormalOS.z * _StyleForwardOS.xyz
        : TransformObjectToWorldNormal(v.styleNormalOS, false);
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
    // 그늘 쪽으로 넓게 퍼지는 보조 계단. 램프 텍스처의 경계 색 띠를 대신한다.
    half litSoft = smoothstep(_ShadeStep - feather - _ShadeEdgeWidth, _ShadeStep + feather, illumination);
    half3 styleUp = half3(0, 1, 0);
    #if defined(FUCHSIA_FACE) || defined(FUCHSIA_HAIR)
        if (faceStyleStrength + hairStyleStrength > .001h)
        {
            styleUp = FuchsiaStyleAxis(_StyleUpOS);
        }
    #endif
    #if defined(FUCHSIA_FACE)
        if (faceStyleStrength > .001h)
        {
            half3 faceForward = FuchsiaStyleAxis(_StyleForwardOS);
            half3 faceRight = FuchsiaStyleAxis(_StyleRightOS);
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
            half faceLitSoft = smoothstep(faceThreshold - _FaceShadowFeather - _ShadeEdgeWidth * .5h,
                faceThreshold + _FaceShadowFeather, faceField);
            litSoft = lerp(litSoft, max(faceLitSoft, faceLit), faceStyleStrength * frontMask);
        }
    #endif
    half3 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
    // 스타레일/젠레스식 구성: 밝은 면은 원화 색 x 광원 색조, 그늘은 채도를 올린 그늘 색.
    // 실제 광량은 색조만 빌리고, 밝기는 _LitBrightness로 아트 디렉팅한다.
    half3 keyRadiance = light.color * light.distanceAttenuation * _DirectLightStrength;
    // 부분 밝기 반응: 반응 0이면 원화 밝기를 고정하고, 1이면 (광량 / 기준 광량)을 [최소, 최대] 안에서 따른다.
    // 재질별 _DirectLightStrength 차이가 부위별 밝기 차이로 번지지 않도록 광원 자체의 광량만 쓴다.
    half sceneLuminance = dot(light.color * light.distanceAttenuation, half3(.2126h, .7152h, .0722h));
    half relativeBrightness = clamp(sceneLuminance / max(_CharacterBrightness.x, .001h),
        _CharacterBrightness.y, _CharacterBrightness.z);
    half keyBrightness = lerp(1.0h, relativeBrightness, saturate(_CharacterBrightness.w))
        * _LitBrightness * environmentVisibility;
    half3 directResponse = FuchsiaLightHue(keyRadiance) * keyBrightness;

    // 주변광은 그늘 색조에만 섞어, 환경색을 받으면서도 그늘 밝기는 재질이 정한다.
    half3 ambient = max(SampleSH(n), 0);
    half3 ambientHue = lerp(half3(1, 1, 1), FuchsiaLightHue(ambient), _ShadeAmbientTint);
    half shadeLift = 0;
    #if defined(FUCHSIA_FACE)
        shadeLift = _FaceAmbientFill * faceStyleStrength * .25h;
    #elif defined(FUCHSIA_HAIR)
        shadeLift = _HairAmbientFill * hairStyleStrength * .25h;
    #endif
    half3 shadeAlbedo = FuchsiaSaturation(baseColor, _ShadeSaturation) * _ShadeColor.rgb * ambientHue;
    shadeAlbedo = lerp(shadeAlbedo, baseColor, shadeLift);
    // 명암 경계 바로 안쪽에 채도 높은 띠를 둬서 램프 텍스처의 따뜻한 경계를 흉내 낸다.
    half edgeBand = saturate(litSoft - lit);
    // 경도를 올리면 퍼지는 그라데이션 대신 일정 폭의 선명한 색 선이 된다.
    edgeBand = lerp(edgeBand, smoothstep(.3h, .5h, edgeBand), _ShadeEdgeHardness) * _ShadeEdgeColor.a;
    shadeAlbedo = lerp(shadeAlbedo, FuchsiaSaturation(shadeAlbedo, _ShadeEdgeSaturation) * _ShadeEdgeColor.rgb, edgeBand);
    shadeAlbedo = lerp(baseColor, shadeAlbedo, _ShadingStrength);
    // 역광: 광원이 시선 뒤편에 있을수록 보이는 면 대부분이 그늘이 되므로 그늘을 더 낮춰 실루엣 림이 살아나게 한다.
    half backlit = smoothstep(.15h, .85h, saturate(-dot(light.direction, view)));
    shadeAlbedo *= 1.0h - _BacklightShadeDim * backlit;
    // 그늘 밝기 자체도 주변광 세기에 약하게 반응한다(어두운 구역에서 캐릭터만 떠 보이지 않게).
    half ambientLevel = lerp(1.0h, saturate(dot(ambient, half3(.2126h, .7152h, .0722h)) * 2.0h),
        saturate(1.0h - _AmbientStrength));
    half3 color = lerp(shadeAlbedo * ambientLevel, baseColor, lit) * directResponse;
    // 보조광은 색 보충만 한다. 그늘에서는 _AdditionalShadowFill 만큼만 들어온다.
    color += baseColor * fill * lerp(_AdditionalShadowFill, 1.0h, lit);

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
                half3 styleRight = FuchsiaStyleAxis(_StyleRightOS);
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
                // 그늘과 역광에서는 띠 광택을 거의 지운다. 예전에는 그늘에서도 40% 남아 역광 때 앞머리가 빛났다.
                band *= control.r * smoothstep(.0h, .45h, dot(n, view))
                    * lerp(.08h, 1.0h, lit * smoothstep(0.0h, .35h, dot(n, light.direction))) * shadow
                    * (1.0h - _BacklightHighlightFade * backlit);
                half3 highlightLight = directResponse + fill * .2h;
                color += _HighlightColor.rgb * highlightLight
                    * band * _HighlightStrength * hairStyleStrength;
            }
            else
        #endif
        {
            // 반대편 광택과 그림자 안의 광택을 억제한다.
            band *= saturate(dot(n, light.direction)) * shadow;
            color += _HighlightColor.rgb * directResponse * band * _HighlightStrength
                * (1.0h - _BacklightHighlightFade * backlit);
        }
    #endif
    #if defined(FUCHSIA_HAIR) || defined(FUCHSIA_ACCESSORY)
        half rim = pow(1.0h - saturate(dot(n, view)), _RimPower);
        color += _RimColor.rgb * directResponse * rim * _RimStrength * saturate(dot(n, light.direction)) * shadow;
    #endif
    // 실루엣 림: 그늘 쪽에서도 약하게 남겨 배경과 캐릭터를 분리한다.
    half depthRim = FuchsiaDepthRim(i.positionCS, n, view) * _DepthRimStrength;
    half rimLighting = lerp(_DepthRimShadowFade, 1.0h, saturate(dot(n, light.direction)) * shadow);
    color += lerp(half3(1, 1, 1), baseColor, .35h) * _DepthRimColor.rgb * _CharacterRimTint.rgb * directResponse
        * depthRim * rimLighting * _CharacterRimTint.a;
    #if defined(_EMISSION)
        color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv).rgb * _EmissionColor.rgb;
    #endif
    return half4(color, 1);
}

// 뒷면 확장 외곽선. 시선 거리에 비례해 화면에서 거의 일정한 굵기를 유지하고,
// 법선의 z를 눌러 정면 쪽 면에 선이 비쳐 나오지 않게 한다.
struct OutlineAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    float4 color : COLOR;
};
struct OutlineVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float2 uv : TEXCOORD1;
};
OutlineVaryings OutlineVert(OutlineAttributes v)
{
    OutlineVaryings o;
    float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
    float3 positionVS = TransformWorldToView(positionWS);
    float3 normalVS = TransformWorldToViewNormal(normalWS, true);
    normalVS.z = -.1;
    float distanceScale = IsPerspectiveProjection()
        ? -positionVS.z * 1.732 / unity_CameraProjection._m11
        : 1.0 / unity_CameraProjection._m11;

    // 선 굵기 강약: 짙은 텍스처와 그늘 쪽은 굵게, 얇은 부위(구운 두께)는 가늘게.
    float2 uv = TRANSFORM_TEX(v.uv, _BaseMap);
    half3 texel = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uv, 0).rgb * _BaseColor.rgb;
    half darkness = 1.0h - saturate(dot(texel, half3(.2126h, .7152h, .0722h)));
    Light light = GetMainLight();
    FuchsiaApplyCharacterKey(light, _CharacterKeyDirection, _CharacterKeyColor.rgb);
    half shade = 1.0h - smoothstep(_ShadeStep - .2h, _ShadeStep + .1h, saturate(dot(normalWS, light.direction)));
    // 두께는 정점 색 A에 0~1로 구워 둔다(FuchsiaOutlineThicknessBaker). 사용하지 않는 재질은 1로 본다.
    half thickness = lerp(1.0h, lerp(_OutlineThinScale, 1.0h, v.color.a), _OutlineUseThickness);
    half widthScale = (1.0h + _OutlineDarkWiden * darkness) * (1.0h + _OutlineShadeWiden * shade) * thickness;
    widthScale = clamp(widthScale, _OutlineMinScale, _OutlineMaxScale);

    // _OutlineWidth 1 ≈ 시야각 60도, 1080p에서 약 1픽셀
    float width = _OutlineWidth * widthScale * .00107 * clamp(distanceScale, .35, 8.0);
    positionVS += normalize(normalVS) * width;
    positionVS.z -= _OutlineZOffset * .01;
    o.positionCS = _OutlineWidth > .001 ? TransformWViewToHClip(positionVS) : float4(0, 0, 0, 0);
    o.positionWS = TransformViewToWorld(positionVS);
    o.uv = uv;
    return o;
}
half4 OutlineFrag(OutlineVaryings i) : SV_Target
{
    half3 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
    // 검은 선 대신 원화 색을 어둡고 진하게 만든 색선을 쓴다.
    half3 color = FuchsiaSaturation(baseColor, _OutlineSaturation) * _OutlineColor.rgb;
    Light light = GetMainLight();
    FuchsiaApplyCharacterKey(light, _CharacterKeyDirection, _CharacterKeyColor.rgb);
    color *= lerp(half3(1, 1, 1), FuchsiaLightHue(light.color), _OutlineLightTint);
    return half4(color, 1);
}
#endif

#ifndef BACKGROUND_STYLIZED_LIGHTING_INCLUDED
#define BACKGROUND_STYLIZED_LIGHTING_INCLUDED
// 배경 모듈용 스타일 PBR. URP Lit의 계산(UniversalFragmentPBR)을 그대로 따르고, 그 위에 애니메이션 배경 느낌을 얹는다.
// - 해(주광) 명암: 감싸기(wrap)로 어두운 쪽을 부드럽게 열고, 램프처럼 경계를 또렷하게 할 수 있다.
// - 그늘·간접광: 밝기는 그대로 두고 색조만 차갑게 바꾼다. 앰비언트 리프트로 어두운 곳을 하이키로 들어 올린다.
// - 금속감 배율: 모델러 텍스처는 그대로 두고, 패널이 칠한 면처럼 밝게 읽히도록 금속도만 줄인다.
// 모든 스타일 값이 기본값(0, 금속 배율 1, 채도 1)이면 URP Lit과 같은 결과가 나온다.

// 캐릭터 전용 그림자 맵(CharacterSelfShadowFeature가 채움): 칸 4개 가로 배치, 캐릭터 툰 주광 쪽에서 잰 0~1 깊이.
TEXTURE2D_FLOAT(_FuchsiaCharShadowMap);
float4x4 _FuchsiaCharShadowMatrices[4];
float4 _FuchsiaCharShadowDepthScale[4];
float4 _FuchsiaCharShadowParams;

// 이 점이 캐릭터에 가려 캐릭터 주광을 못 받는 정도(0 = 안 가려짐, 1 = 완전히 가려짐).
// 바닥은 그림자 맵의 깊이 범위 밖(캐릭터 뒤쪽)에 있으므로, 맵에 캐릭터가 그려진 자리면 깊이 범위를 넘어도 가려진 것으로 본다.
// 캐릭터 뒤로 2.5 m를 넘는 곳(아래층 바닥 등)은 그림자를 받지 않는다.
half BgStyleCharacterShadow(float3 positionWS)
{
    if (_FuchsiaCharShadowParams.x < 0.5 || _StyleCharacterShadow <= half(0.001))
    {
        return 0;
    }
    float resolution = _FuchsiaCharShadowParams.z;
    int maxCoord = (int)resolution - 1;
    float spacing = max(_StyleCharacterShadowSoftness, 1.0);
    half occluded = 0;
    UNITY_UNROLL for (int slot = 0; slot < 4; slot++)
    {
        float3 clip = mul(_FuchsiaCharShadowMatrices[slot], float4(positionWS, 1)).xyz;
        float4 scale = _FuchsiaCharShadowDepthScale[slot];   // x: 1/깊이 범위, y: 깊이 범위(m), z: 텍셀 크기(m)
        if (any(abs(clip.xy) > 1) || clip.z <= 0 || clip.z > 1.0 + 2.5 * scale.x)
        {
            continue;
        }
        float depth = min(clip.z, 1.0) - 0.01 * scale.x;
        float2 texelPosition = (clip.xy * 0.5 + 0.5) * resolution - 0.5;
        int slotX = slot * (int)resolution;
        float hidden = 0;
        // 4x4 표본을 spacing 텍셀 간격으로 펼쳐 부드러운 가장자리를 만든다.
        UNITY_UNROLL for (int y = 0; y < 4; y++)
        {
            UNITY_UNROLL for (int x = 0; x < 4; x++)
            {
                float2 offset = (float2(x, y) - 1.5) * spacing;
                int2 coord = clamp(int2(round(texelPosition + offset)), 0, maxCoord) + int2(slotX, 0);
                float stored = LOAD_TEXTURE2D(_FuchsiaCharShadowMap, coord).r;
                hidden += depth > stored ? 1.0 : 0.0;
            }
        }
        occluded = max(occluded, half(hidden / 16.0));
    }
    return occluded;
}

// 칸마다 캐릭터 발밑 위치(xyz)와 사용 여부(w). CharacterSelfShadowFeature가 채운다.
float4 _FuchsiaCharFeet[4];

// 발밑 접지 그림자(0~1): 천장 조명 아래 선 캐릭터의 발 주변이 어두워지는 것을 흉내 낸다.
// 캐릭터 주광 방향과 상관없이 늘 보이므로, 그림자가 캐릭터 뒤로 숨는 시점에서도 캐릭터가 바닥에 붙어 보인다.
// 점프하면 발 높이 차이만큼 옅어진다.
half BgStyleCharacterContact(float3 positionWS)
{
    if (_FuchsiaCharShadowParams.x < 0.5 || _StyleCharacterContact <= half(0.001))
    {
        return 0;
    }
    half contact = 0;
    UNITY_UNROLL for (int slot = 0; slot < 4; slot++)
    {
        float4 feet = _FuchsiaCharFeet[slot];
        if (feet.w < 0.5)
        {
            continue;
        }
        float r = length(positionWS.xz - feet.xz) / max(_StyleCharacterContactRadius, 0.05);
        float h = abs(positionWS.y - feet.y);
        contact = max(contact, half((1.0 - smoothstep(0.15, 1.0, r)) * (1.0 - smoothstep(0.15, 0.6, h))));
    }
    return contact;
}

// 색을 밝기 1로 맞춘다. 그늘·빛의 색조만 바꾸고 밝기는 유지하려고 쓴다.
half3 BgStyleHue(half3 tint)
{
    return tint / max(Luminance(tint), half(0.001));
}

// 주광 하나의 PBR 기여(URP LightingPhysicallyBased와 같은 식)에서 NdotL만 감싸기·램프로 바꾼다.
half3 BgStyleMainLight(BRDFData brdfData, BRDFData brdfDataClearCoat, Light light, half3 normalWS, half3 viewWS,
    half clearCoatMask, bool specularHighlightsOff, out half litAmount)
{
    half NdotL = dot(normalWS, light.direction);
    half wrapped = saturate((NdotL + _StyleWrap) / (half(1.0) + _StyleWrap));
    half ramp = smoothstep(_StyleRampThreshold - _StyleRampSoftness, _StyleRampThreshold + _StyleRampSoftness, wrapped);
    half styledNdotL = lerp(wrapped, ramp, _StyleRamp);
    litAmount = styledNdotL * light.shadowAttenuation;
    half3 radiance = light.color * (light.distanceAttenuation * light.shadowAttenuation * styledNdotL);

    half3 brdf = brdfData.diffuse;
    #ifndef _SPECULARHIGHLIGHTS_OFF
    [branch] if (!specularHighlightsOff)
    {
        brdf += brdfData.specular * DirectBRDFSpecular(brdfData, normalWS, light.direction, viewWS);
        #if defined(_CLEARCOAT) || defined(_CLEARCOATMAP)
        half brdfCoat = kDielectricSpec.r * DirectBRDFSpecular(brdfDataClearCoat, normalWS, light.direction, viewWS);
        half NoV = saturate(dot(normalWS, viewWS));
        half coatFresnel = kDielectricSpec.x + kDielectricSpec.a * Pow4(half(1.0) - NoV);
        brdf = brdf * (half(1.0) - clearCoatMask * coatFresnel) + brdfCoat * clearCoatMask;
        #endif
    }
    #endif
    return brdf * radiance;
}

// 구운 빛의 주 방향(xyz)과 방향성 정도(w, 0~1). 방향성 라이트맵이면 그 방향 텍스처에서, 아니면 라이트 프로브의 L1 항에서 얻는다.
half4 BgStyleBakedLightDirection(Varyings input)
{
#if defined(LIGHTMAP_ON) && defined(DIRLIGHTMAP_COMBINED)
    float2 staticLightmapUV = input.staticLightmapUV;
    half4 encoded = SAMPLE_TEXTURE2D_LIGHTMAP(LIGHTMAP_INDIRECTION_NAME, LIGHTMAP_SAMPLER_NAME, LIGHTMAP_SAMPLE_EXTRA_ARGS);
    half3 d = encoded.xyz * half(2.0) - half(1.0);
    half len = length(d);
    return half4(d / max(len, half(0.0001)), saturate(len));
#elif !defined(LIGHTMAP_ON)
    half3 d = unity_SHAr.xyz * half(0.2126) + unity_SHAg.xyz * half(0.7152) + unity_SHAb.xyz * half(0.0722);
    half len = length(d);
    return half4(d / max(len, half(0.0001)), saturate(len * half(4.0)));
#else
    return half4(0, 1, 0, 0);
#endif
}

// 구운 빛 투톤에서 빛을 받는 정도(0 = 못 받음, 1 = 받음). 기준값 앞뒤 Softness 폭에서만 바뀌어 경계가 또렷하다.
// 판단 값은 구운 빛 밝기에 방향성(_StyleCelDirectional만큼)을 곱한 것: 한 방향에서 오는 빛(램프 직사광)일수록 크고,
// 사방에서 오는 반사광뿐인 곳일수록 작다.
half BgStyleCelLit(half bakedLuma, half4 bakedLightDir)
{
#if defined(LIGHTMAP_ON) && !defined(DIRLIGHTMAP_COMBINED)
    half directionality = half(1.0);   // 방향 정보가 없는 라이트맵이면 밝기만 본다.
#else
    half directionality = bakedLightDir.w;
#endif
    half signal = bakedLuma * lerp(half(1.0), directionality, _StyleCelDirectional);
    return smoothstep(_StyleCelThreshold - _StyleCelSoftness, _StyleCelThreshold + _StyleCelSoftness, signal);
}

// 구운 빛을 두 톤으로 다시 칠한다. 빛 받는 쪽은 적어도 Lit level 밝기로 올리고(더 밝은 곳은 Lit flatten만큼 그 값으로 모은다),
// 못 받는 쪽은 Unlit level(반사광 밝기)로 모은다. 빛 색과 반사광 색은 그 자리 구운 빛의 색을 그대로 쓴다.
half3 BgStyleCelBakedGI(half3 bakedGI, half bakedLuma, half lit)
{
    half litLuma = lerp(max(bakedLuma, _StyleCelLitLevel), _StyleCelLitLevel, _StyleCelLitFlatten);
    half unlitLuma = lerp(bakedLuma, _StyleCelShadowLevel, _StyleCelShadowFlatten);
    half target = lerp(unlitLuma, litLuma, lit);
    // 아주 어두운 틈(밝기 0.02 미만)은 덜 키워 더 어둡게 남긴다. 거의 검은 값을 크게 키우면 색 잡음이 생긴다.
    half3 styled = bakedGI * (target / max(bakedLuma, half(0.02)));
    return lerp(bakedGI, styled, _StyleCel);
}

half4 BgStyleFragmentPBR(InputData inputData, SurfaceData surfaceData, half4 bakedLightDir)
{
    #if defined(_SPECULARHIGHLIGHTS_OFF)
    bool specularHighlightsOff = true;
    #else
    bool specularHighlightsOff = false;
    #endif
    surfaceData.metallic *= _StyleMetallicScale;
    BRDFData brdfData;
    InitializeBRDFData(surfaceData, brdfData);

    #if defined(DEBUG_DISPLAY)
    half4 debugColor;
    if (CanDebugOverrideOutputColor(inputData, surfaceData, brdfData, debugColor))
    {
        return debugColor;
    }
    #endif

    BRDFData brdfDataClearCoat = CreateClearCoatBRDFData(surfaceData, brdfData);
    half4 shadowMask = CalculateShadowMask(inputData);
    AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData, surfaceData);
    // SSAO를 _StyleSSAO만큼만 받는다(0이면 재질 AO만 남는다). 조명 계산 전에 줄여야 주광·추가광의 직접 AO에도 적용된다.
    aoFactor.indirectAmbientOcclusion = lerp(surfaceData.occlusion, aoFactor.indirectAmbientOcclusion, _StyleSSAO);
    aoFactor.directAmbientOcclusion = lerp(half(1.0), aoFactor.directAmbientOcclusion, _StyleSSAO);
    uint meshRenderingLayers = GetMeshRenderingLayer();
    Light mainLight = GetMainLight(inputData, shadowMask, aoFactor);
    MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI);

    // 구운 빛 밝기: 그늘 판단과 투톤의 기준(투톤으로 다시 칠하기 전 값).
    half bakedLuma = Luminance(inputData.bakedGI);
    half celLit = BgStyleCelLit(bakedLuma, bakedLightDir);
    if (_StyleCel > half(0.001))
    {
        inputData.bakedGI = BgStyleCelBakedGI(inputData.bakedGI, bakedLuma, celLit);
    }

    LightingData lightingData = CreateLightingData(inputData, surfaceData);
    lightingData.giColor = GlobalIllumination(brdfData, brdfDataClearCoat, surfaceData.clearCoatMask,
        inputData.bakedGI, aoFactor.indirectAmbientOcclusion, inputData.positionWS,
        inputData.normalWS, inputData.viewDirectionWS, inputData.normalizedScreenSpaceUV);

    half mainLit = half(0.0);
    #ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
    #endif
    {
        lightingData.mainLightColor = BgStyleMainLight(brdfData, brdfDataClearCoat, mainLight,
            inputData.normalWS, inputData.viewDirectionWS, surfaceData.clearCoatMask, specularHighlightsOff, mainLit);
    }

    #if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();
    #if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
        #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
        #endif
        {
            lightingData.additionalLightsColor += LightingPhysicallyBased(brdfData, brdfDataClearCoat, light,
                inputData.normalWS, inputData.viewDirectionWS, surfaceData.clearCoatMask, specularHighlightsOff);
        }
    }
    #endif
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
        #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
        #endif
        {
            lightingData.additionalLightsColor += LightingPhysicallyBased(brdfData, brdfDataClearCoat, light,
                inputData.normalWS, inputData.viewDirectionWS, surfaceData.clearCoatMask, specularHighlightsOff);
        }
    LIGHT_LOOP_END
    #endif

    #if defined(_ADDITIONAL_LIGHTS_VERTEX)
    lightingData.vertexLightingColor += inputData.vertexLighting * brdfData.diffuse;
    #endif

    // 하이키: 어두운 곳을 일정한 색의 채움광으로 들어 올린다(오목한 곳은 차폐만큼 덜).
    half occlusion = min(surfaceData.occlusion, aoFactor.indirectAmbientOcclusion);
    lightingData.giColor += brdfData.diffuse * _StyleAmbientColor.rgb * (_StyleAmbientLift * occlusion);
    // 그늘 판단: 해(주광)가 닿지 않는 곳이 그늘이다. 실내처럼 빛이 전부 구운 빛(라이트맵·프로브)이면
    // 해 기준으로는 화면 전체가 그늘이 되므로, _StyleBakedShadow만큼 "구운 빛이 어두운 곳"으로 그늘을 좁힌다.
    half bakedShadow = half(1.0) - smoothstep(_StyleBakedShadowStart,
        max(_StyleBakedShadowEnd, _StyleBakedShadowStart + half(0.001)), bakedLuma);
    // 투톤을 켜면 그늘 판단도 투톤 경계를 따른다(그늘 색조도 경계에서 또렷하게 바뀐다).
    bakedShadow = lerp(bakedShadow, half(1.0) - celLit, _StyleCel);
    half shadeMask = (half(1.0) - saturate(mainLit)) * lerp(half(1.0), bakedShadow, _StyleBakedShadow);

    // 그늘은 밝기를 유지한 채 그늘 색조로 물들인다(곱하기라 베이스 컬러가 씻기지 않는다). 해가 비치는 면은 따뜻한 색조로.
    half3 shadowHue = BgStyleHue(_StyleShadowTint.rgb);
    lightingData.giColor *= lerp(half3(1.0, 1.0, 1.0), shadowHue, _StyleShadowTintStrength * shadeMask);
    // 그늘 대비: 그늘을 이만큼 더 어둡게 해서 밝은 면과 투톤으로 또렷하게 나뉘게 한다(경계는 Start~End 폭).
    lightingData.giColor *= half(1.0) - _StyleShadowDarken * shadeMask;
    // 그늘 채움: 너무 어두운 그늘을 같은 색조의 약한 빛으로 들어 올린다(오목한 곳은 차폐만큼 덜).
    lightingData.giColor += brdfData.diffuse * shadowHue * (_StyleShadowLift * shadeMask * occlusion);
    lightingData.mainLightColor *= lerp(half3(1.0, 1.0, 1.0), BgStyleHue(_StyleLitTint.rgb), _StyleLitTintStrength);

    half4 color = CalculateFinalColor(lightingData, surfaceData.alpha);
    half luma = Luminance(color.rgb);
    half saturation = _StyleSaturation * lerp(half(1.0), _StyleShadowSaturation, shadeMask);
    color.rgb = max(lerp(half3(luma, luma, luma), color.rgb, saturation), half3(0.0, 0.0, 0.0));

    // 구운 빛 하이라이트(투톤): 구운 램프 빛은 스페큘러를 만들지 않으므로, 방향성 라이트맵의 주 빛 방향으로
    // 블린-퐁 하이라이트를 계산하고 경계를 계단처럼 잘라 애니풍의 또렷한 하이라이트로 만든다. 색은 그 자리 빛 색을 따른다.
    if (_StyleSpecular > half(0.001))
    {
        half3 lightDir = bakedLightDir.xyz;
        half3 halfDir = SafeNormalize(lightDir + inputData.viewDirectionWS);
        half nh = saturate(dot(inputData.normalWS, halfDir));
        half nl = saturate(dot(inputData.normalWS, lightDir));
        half specular = pow(nh, _StyleSpecularGloss) * nl;
        half tone = smoothstep(_StyleSpecularStep - _StyleSpecularSoftness, _StyleSpecularStep + _StyleSpecularSoftness, specular);
        color.rgb += inputData.bakedGI * (tone * _StyleSpecular * bakedLightDir.w * min(surfaceData.occlusion, aoFactor.indirectAmbientOcclusion));
    }

    // 캐릭터가 드리우는 그림자: 캐릭터 실루엣 그림자와 발밑 접지 그림자 중 진한 쪽만큼 그늘 색조로 어둡게 한다
    // (구운 조명은 움직이는 캐릭터의 그림자를 만들지 못한다).
    half characterShadow = max(BgStyleCharacterShadow(inputData.positionWS) * _StyleCharacterShadow,
        BgStyleCharacterContact(inputData.positionWS) * _StyleCharacterContact);
    color.rgb *= (half(1.0) - characterShadow) * lerp(half3(1.0, 1.0, 1.0), shadowHue, characterShadow);

    // 조정용 화면: 1 = 그늘로 판단한 곳(흰색일수록 그늘), 2 = 구운 빛 밝기(회색조),
    // 3 = 캐릭터 그림자(R: 가려진 정도, G: 캐릭터 그림자 맵 사용 중, B: 캐릭터 그림자 세기 설정),
    // 4 = 구운 빛의 방향성(흰색일수록 한 방향에서 오는 빛 = 램프 직사광, 검을수록 사방에서 오는 반사광).
    if (_StyleDebugView > half(0.5))
    {
        half debugValue = _StyleDebugView < half(1.5) ? shadeMask : (_StyleDebugView > half(3.5) ? bakedLightDir.w : bakedLuma);
        color.rgb = (_StyleDebugView < half(2.5) || _StyleDebugView > half(3.5)) ? half3(debugValue, debugValue, debugValue)
            : half3(characterShadow, half(_FuchsiaCharShadowParams.x), _StyleCharacterShadow);
    }

#if REAL_IS_HALF
    return min(color, HALF_MAX);
#else
    return color;
#endif
}

void BackgroundStylizedFragment(
    Varyings input
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

#if defined(_PARALLAXMAP)
#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirTS = input.viewDirTS;
#else
    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    half3 viewDirTS = GetViewDirectionTangentSpace(input.tangentWS, input.normalWS, viewDirWS);
#endif
    ApplyPerPixelDisplacement(viewDirTS, input.uv);
#endif

    SurfaceData surfaceData;
    InitializeStandardLitSurfaceData(input.uv, surfaceData);

#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif

    InputData inputData;
    InitializeInputData(input, surfaceData.normalTS, inputData);
    SETUP_DEBUG_TEXTURE_DATA(inputData, UNDO_TRANSFORM_TEX(input.uv, _BaseMap));

#if defined(_DBUFFER)
    ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
#endif

    InitializeBakedGIData(input, inputData);

    half4 color = BgStyleFragmentPBR(inputData, surfaceData, BgStyleBakedLightDirection(input));
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = OutputAlpha(color.a, IsSurfaceTypeTransparent(_Surface));

    outColor = color;

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}
#endif

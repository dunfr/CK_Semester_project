#ifndef BACKGROUND_STYLIZED_LIGHTING_INCLUDED
#define BACKGROUND_STYLIZED_LIGHTING_INCLUDED
// 배경 모듈용 스타일 PBR. URP Lit의 계산(UniversalFragmentPBR)을 그대로 따르고, 그 위에 애니메이션 배경 느낌을 얹는다.
// - 해(주광) 명암: 감싸기(wrap)로 어두운 쪽을 부드럽게 열고, 램프처럼 경계를 또렷하게 할 수 있다.
// - 그늘·간접광: 밝기는 그대로 두고 색조만 차갑게 바꾼다. 앰비언트 리프트로 어두운 곳을 하이키로 들어 올린다.
// - 금속감 배율: 모델러 텍스처는 그대로 두고, 패널이 칠한 면처럼 밝게 읽히도록 금속도만 줄인다.
// 모든 스타일 값이 기본값(0, 금속 배율 1, 채도 1)이면 URP Lit과 같은 결과가 나온다.

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

half4 BgStyleFragmentPBR(InputData inputData, SurfaceData surfaceData)
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
    uint meshRenderingLayers = GetMeshRenderingLayer();
    Light mainLight = GetMainLight(inputData, shadowMask, aoFactor);
    MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI);

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
    // 그늘(해가 닿지 않는 간접광)은 밝기를 유지한 채 차가운 색조로, 해가 비치는 면은 따뜻한 색조로.
    half shadowAmount = _StyleShadowTintStrength * (half(1.0) - saturate(mainLit));
    lightingData.giColor *= lerp(half3(1.0, 1.0, 1.0), BgStyleHue(_StyleShadowTint.rgb), shadowAmount);
    lightingData.mainLightColor *= lerp(half3(1.0, 1.0, 1.0), BgStyleHue(_StyleLitTint.rgb), _StyleLitTintStrength);

    half4 color = CalculateFinalColor(lightingData, surfaceData.alpha);
    half luma = Luminance(color.rgb);
    color.rgb = max(lerp(half3(luma, luma, luma), color.rgb, _StyleSaturation), half3(0.0, 0.0, 0.0));

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

    half4 color = BgStyleFragmentPBR(inputData, surfaceData);
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = OutputAlpha(color.a, IsSurfaceTypeTransparent(_Surface));

    outColor = color;

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}
#endif

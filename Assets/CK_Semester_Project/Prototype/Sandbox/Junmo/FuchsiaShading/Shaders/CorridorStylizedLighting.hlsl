#ifndef CORRIDOR_STYLIZED_LIGHTING_INCLUDED
#define CORRIDOR_STYLIZED_LIGHTING_INCLUDED
// URP Complex Lit의 UniversalFragmentPBR 흐름을 따르되, 보조광(점광/스폿광)만 배경용으로 다시 계산한다.
// 금속·고광택 재질에서 보조광이 날카로운 거울 하이라이트(타원 웅덩이)로만 보여 스폿광처럼 읽히는 문제를 줄인다.

// 보조광 전용 BRDF: 거칠기 하한을 둬서 하이라이트를 넓고 낮게 퍼뜨린다. 정규화 GGX라 에너지는 유지된다.
BRDFData CorridorRoughenedBRDF(BRDFData brdfData, half minPerceptualRoughness)
{
    BRDFData rough = brdfData;
    rough.perceptualRoughness = max(brdfData.perceptualRoughness, minPerceptualRoughness);
    rough.roughness = max(PerceptualRoughnessToRoughness(rough.perceptualRoughness), HALF_MIN_SQRT);
    rough.roughness2 = max(rough.roughness * rough.roughness, HALF_MIN);
    rough.normalizationTerm = rough.roughness * half(4.0) + half(2.0);
    rough.roughness2MinusOne = rough.roughness2 - half(1.0);
    return rough;
}

half3 CorridorPunctualLighting(BRDFData brdfData, BRDFData brdfDataClearCoat, Light light,
    half3 normalWS, half3 viewWS, half clearCoatMask, bool specularHighlightsOff)
{
    // 광원 바로 앞의 역제곱 급증을 완만하게 눌러, 광원 위치에 밝은 원이 맺히지 않게 한다.
    half attenuation = light.distanceAttenuation;
    attenuation = attenuation / (half(1.0) + attenuation * _PunctualAttenuationKnee);
    half3 radiance = light.color * (attenuation * light.shadowAttenuation);

    half NdotL = dot(normalWS, light.direction);
    // 감싸는 확산광: 명암 경계를 부드럽게 넓혀 빛이 벽면 전체에 번지도록 한다.
    half wrapped = saturate((NdotL + _PunctualWrap) / (half(1.0) + _PunctualWrap));
    // 금속은 확산 성분이 0이라 점광이 반사 하이라이트로만 남는다. 금속 색으로 약한 확산 번짐을 더한다.
    half3 diffuse = brdfData.diffuse + brdfData.specular * _PunctualMetalWash;
    half3 color = diffuse * wrapped;

    #ifndef _SPECULARHIGHLIGHTS_OFF
    [branch] if (!specularHighlightsOff)
    {
        half NdotLClamped = saturate(NdotL);
        BRDFData rough = CorridorRoughenedBRDF(brdfData, _PunctualMinRoughness);
        color += brdfData.specular * DirectBRDFSpecular(rough, normalWS, light.direction, viewWS)
            * (NdotLClamped * _PunctualSpecular);
        #if defined(_CLEARCOAT) || defined(_CLEARCOATMAP)
            BRDFData roughCoat = CorridorRoughenedBRDF(brdfDataClearCoat, _PunctualMinRoughness);
            color += kDielectricSpec.r * DirectBRDFSpecular(roughCoat, normalWS, light.direction, viewWS)
                * (NdotLClamped * clearCoatMask * _PunctualSpecular);
        #endif
    }
    #endif
    return color * radiance;
}

half4 CorridorFragmentPBR(InputData inputData, SurfaceData surfaceData)
{
    #if defined(_SPECULARHIGHLIGHTS_OFF)
    bool specularHighlightsOff = true;
    #else
    bool specularHighlightsOff = false;
    #endif
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
    #ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
    #endif
    {
        // 주광(방향광)은 원래 PBR 그대로 둔다.
        lightingData.mainLightColor = LightingPhysicallyBased(brdfData, brdfDataClearCoat, mainLight,
            inputData.normalWS, inputData.viewDirectionWS, surfaceData.clearCoatMask, specularHighlightsOff);
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
            // 보조 방향광은 웅덩이 문제가 없으므로 PBR 그대로 둔다.
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
            lightingData.additionalLightsColor += CorridorPunctualLighting(brdfData, brdfDataClearCoat, light,
                inputData.normalWS, inputData.viewDirectionWS, surfaceData.clearCoatMask, specularHighlightsOff);
        }
    LIGHT_LOOP_END
    #endif

    #if defined(_ADDITIONAL_LIGHTS_VERTEX)
    lightingData.vertexLightingColor += inputData.vertexLighting * brdfData.diffuse;
    #endif

#if REAL_IS_HALF
    return min(CalculateFinalColor(lightingData, surfaceData.alpha), HALF_MAX);
#else
    return CalculateFinalColor(lightingData, surfaceData.alpha);
#endif
}

void CorridorStylizedFragment(
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

    half4 color = CorridorFragmentPBR(inputData, surfaceData);
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = OutputAlpha(color.a, IsSurfaceTypeTransparent(_Surface));

    outColor = color;

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}
#endif

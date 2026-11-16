#ifndef BACKGROUND_STYLIZED_LIGHTING_INCLUDED
#define BACKGROUND_STYLIZED_LIGHTING_INCLUDED
// 배경 모듈용 스타일 PBR. URP Lit의 계산(UniversalFragmentPBR)을 그대로 따르고, 그 위에 애니메이션 배경 느낌을 얹는다.
// - 해(주광) 명암: 감싸기(wrap)로 어두운 쪽을 부드럽게 열고, 램프처럼 경계를 또렷하게 할 수 있다.
// - 그늘·간접광: 밝기는 그대로 두고 색조만 차갑게 바꾼다. 앰비언트 리프트로 어두운 곳을 하이키로 들어 올린다.
// - 금속감 배율: 모델러 텍스처는 그대로 두고, 패널이 칠한 면처럼 밝게 읽히도록 금속도만 줄인다.
// 모든 스타일 값이 기본값(0, 금속 배율 1, 채도 1)이면 URP Lit과 같은 결과가 나온다.

// 까끌까끌한 결용 3D 값 노이즈(해시 기반, 텍스처 없음). 0~1.
float BgStyleHash31(float3 p)
{
    p = frac(p * float3(0.1031, 0.1030, 0.0973));
    p += dot(p, p.yxz + 33.33);
    return frac((p.x + p.y) * p.z);
}

float BgStyleValueNoise(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float3 u = f * f * (3.0 - 2.0 * f);
    float n000 = BgStyleHash31(i);
    float n100 = BgStyleHash31(i + float3(1, 0, 0));
    float n010 = BgStyleHash31(i + float3(0, 1, 0));
    float n110 = BgStyleHash31(i + float3(1, 1, 0));
    float n001 = BgStyleHash31(i + float3(0, 0, 1));
    float n101 = BgStyleHash31(i + float3(1, 0, 1));
    float n011 = BgStyleHash31(i + float3(0, 1, 1));
    float n111 = BgStyleHash31(i + float3(1, 1, 1));
    return lerp(lerp(lerp(n000, n100, u.x), lerp(n010, n110, u.x), u.y),
                lerp(lerp(n001, n101, u.x), lerp(n011, n111, u.x), u.y), u.z);
}

// 결이 빛을 가리는 배율(평균 1 - Tone, Grit만큼 얼룩덜룩). 두 크기의 노이즈를 겹친다.
// 결 한 칸이 화면에서 1픽셀 안팎으로 작아지는 먼 곳은 결을 지운다(지우지 않으면 자글자글 반짝인다).
half BgStyleGrit(float3 positionWS)
{
    float scale = max((float)_StyleGritScale, 1.0);
    float3 p = positionWS * scale;
    float n = BgStyleValueNoise(p) * 0.65 + BgStyleValueNoise(p * 2.03 + 17.1) * 0.35;
    float cellsPerPixel = length(fwidth(positionWS)) * scale;
    float fade = 1.0 - smoothstep(0.35, 0.8, cellsPerPixel);
    return half(max((1.0 - _StyleGritTone) * (1.0 + _StyleGrit * fade * (n - 0.5) * 2.0), 0.0));
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

// 가는 하이라이트 깜빡임 방지(스페큘러 안티에일리어싱: Kaplanyan 2016, Tokuyoshi & Kaplanyan 2019).
// 배관처럼 휜 면은 멀어질수록 화면의 몇 픽셀 안에서 법선이 크게 돈다. 그러면 매끈한 면의 하이라이트가 한 픽셀보다 가는 선이 되어,
// 카메라가 조금만 움직여도 선이 픽셀 사이로 들어갔다 나왔다 하며 깜빡인다(블룸이 그 깜빡임을 더 키운다).
// 한 픽셀 안에서 법선이 도는 만큼 거칠기를 올려, 하이라이트가 픽셀보다 가늘어지지 않게 한다. 넓어진 만큼 정점 밝기는 낮아지므로
// 화면에 닿는 빛의 양은 같다(가까이서 보는 면은 법선이 거의 돌지 않아 그대로다).
half BgStyleStableSmoothness(half smoothness, float3 normalWS, half amount)
{
    float3 du = ddx(normalWS);
    float3 dv = ddy(normalWS);
    // 0.25 = 픽셀 반 칸 크기의 필터(표준값). amount로 필터를 넓힌다.
    float variance = 0.25 * amount * (dot(du, du) + dot(dv, dv));
    float roughness = PerceptualSmoothnessToRoughness(smoothness);
    // 거칠기를 올리는 양에는 한계를 둔다(모서리처럼 법선이 갑자기 꺾이는 곳이 뿌옇게 번지지 않게).
    float squared = saturate(roughness * roughness + min(2.0 * variance, 0.18));
    return half(RoughnessToPerceptualSmoothness(sqrt(squared)));
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
    // 까끌까끌한 결: SSAO처럼 빛을 가리는 값으로 넣는다(구운 빛·반사광 모두 어두워진다). 월드 좌표라 표면에 붙어 있다.
    if (_StyleGrit + _StyleGritTone > half(0.001))
    {
        half gritOcclusion = BgStyleGrit(inputData.positionWS);
        aoFactor.indirectAmbientOcclusion *= gritOcclusion;
        aoFactor.directAmbientOcclusion *= gritOcclusion;
    }
    uint meshRenderingLayers = GetMeshRenderingLayer();
    Light mainLight = GetMainLight(inputData, shadowMask, aoFactor);
    MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI);

    // 구운 빛 밝기: 그늘 판단과 투톤의 기준(투톤으로 다시 칠하기 전 값).
    half3 bakedGIRaw = inputData.bakedGI;
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

    // 구운 빛 광택(표면 마감 3): 구운 램프 빛은 스페큘러를 만들지 않으므로, 방향성 라이트맵에 담긴 주 빛 방향으로
    // URP Lit과 같은 GGX 하이라이트를 계산한다. 거친 면은 넓고 은은하게, 매끈한 면은 좁고 밝게 빛나므로
    // 부위별 러프니스 차이가 하이라이트 모양으로 드러난다. 빛의 양은 그 자리 구운 빛 가운데 한 방향에서 오는 몫(방향성)만 쓴다.
    if (_StyleBakedSpecular > half(0.001))
    {
        // 라이트맵의 빛 방향은 여러 램프의 평균이라, 아주 매끈한 면에 그대로 쓰면 작은 점 하나로 찍힌다.
        // 최소 거칠기로 하이라이트를 넓혀 긴 램프의 넓은 반사처럼 보이게 한다(GGX 정규화로 넓어진 만큼 정점은 낮아진다).
        BRDFData wide = brdfData;
        wide.perceptualRoughness = max(brdfData.perceptualRoughness, _StyleBakedSpecularMinRoughness);
        wide.roughness = max(PerceptualRoughnessToRoughness(wide.perceptualRoughness), HALF_MIN_SQRT);
        wide.roughness2 = max(wide.roughness * wide.roughness, HALF_MIN);
        wide.normalizationTerm = wide.roughness * half(4.0) + half(2.0);
        wide.roughness2MinusOne = wide.roughness2 - half(1.0);
        half3 bakedDir = bakedLightDir.xyz;
        // 빛을 등진 면에는 하이라이트가 생기지 않게 한다(경계는 부드럽게).
        half facing = saturate(dot(inputData.normalWS, bakedDir) * half(2.0) + half(0.3));
        // 정점 값은 64에서 자른다(매끈한 배관·모서리 면의 하이라이트가 화면을 하얗게 태우지 않게).
        half specularTerm = min(DirectBRDFSpecular(wide, inputData.normalWS, bakedDir, inputData.viewDirectionWS), half(64.0));
        color.rgb += brdfData.specular * bakedGIRaw * (specularTerm * facing * bakedLightDir.w * _StyleBakedSpecular
            * min(surfaceData.occlusion, aoFactor.indirectAmbientOcclusion));
    }

    // 조정용 화면: 1 = 그늘로 판단한 곳(흰색일수록 그늘), 2 = 구운 빛 밝기(회색조),
    // 4 = 구운 빛의 방향성(흰색일수록 한 방향에서 오는 빛 = 램프 직사광, 검을수록 사방에서 오는 반사광),
    // 5 = 표면 마감을 거친 매끈함(흰색일수록 매끈함 = 러프니스가 낮음).
    if (_StyleDebugView > half(4.5))
    {
        color.rgb = surfaceData.smoothness.xxx;
    }
    else if (_StyleDebugView > half(0.5))
    {
        half debugValue = _StyleDebugView < half(1.5) ? shadeMask : (_StyleDebugView > half(3.5) ? bakedLightDir.w : bakedLuma);
        color.rgb = half3(debugValue, debugValue, debugValue);
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

    // 표면 마감 2): 부위 맵으로 부위마다 매끈함을 조금씩 달리한다(세 값이 모두 0이면 건너뛴다).
    if (_StylePartVariation + abs(_StyleEdgeFinish) + _StyleBevel > half(0.001))
    {
        // 부위 번호는 경계에서 섞이면 안 되므로 가장 큰 밉을 점 샘플로 읽고, 모서리 면·안쪽 정도는 부드럽게 읽는다.
        half partId = SAMPLE_TEXTURE2D_LOD(_StylePartMap, sampler_PointRepeat, input.uv, 0).r;
        half2 bevelInside = SAMPLE_TEXTURE2D(_StylePartMap, sampler_LinearRepeat, input.uv).gb;
        surfaceData.smoothness = BgFinishSmoothness(surfaceData.smoothness, partId, bevelInside, inputData.positionWS,
            half4(_StylePartVariation, _StylePartWearScale, _StyleEdgeFinish, _StyleBevel), _StyleBevelSmoothness);
    }

    // 가는 하이라이트 깜빡임 방지: 화면에서 법선이 빨리 도는 곳(멀리 있는 배관·둥근 모서리)의 매끈함을 낮춘다.
    // 노멀맵을 거치기 전의 면 법선으로 잰다(노멀맵 무늬 때문에 가까운 면까지 뿌옇게 되지 않도록).
    if (_StyleHighlightStability > half(0.001))
    {
        surfaceData.smoothness = BgStyleStableSmoothness(surfaceData.smoothness, normalize(input.normalWS), _StyleHighlightStability);
    }

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

#ifndef FUCHSIA_LIGHTING_INCLUDED
#define FUCHSIA_LIGHTING_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

Light FuchsiaKeyLight(float3 positionWS)
{
    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
        float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(positionWS));
    #else
        float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
    #endif
    return GetMainLight(shadowCoord);
}

void FuchsiaApplyCharacterKey(inout Light light, half4 directionAndWeight, half3 color)
{
    half weight = saturate(directionAndWeight.w);
    half3 direction = SafeNormalize(directionAndWeight.xyz);
    light.direction = SafeNormalize(lerp(light.direction, direction, weight));
    light.color = lerp(light.color, color, weight);
    light.distanceAttenuation = lerp(light.distanceAttenuation, 1.0, weight);
    // 실내의 가상 주광은 천장의 그림자 맵으로 명암 형태를 잃지 않는다.
    light.shadowAttenuation = lerp(light.shadowAttenuation, 1.0h, weight);
}

half FuchsiaEnvironmentVisibility(half environmentShadow, half weight,
    half receiveShadows, half environmentShadowStrength)
{
    return lerp(1.0h, environmentShadow, saturate(weight * receiveShadows * environmentShadowStrength));
}

half3 FuchsiaFillContribution(Light source, half3 normalWS, half receiveShadows)
{
    // 반대쪽 면까지 같은 색으로 밝히지 않도록 최소 랩 조명만 남긴다.
    half facing = .10h + .90h * saturate(dot(normalWS, source.direction));
    half visibility = lerp(1.0h, source.shadowAttenuation, receiveShadows);
    return source.color * source.distanceAttenuation * visibility * facing;
}

// 보조광은 색 보충만 맡는다. 주광의 명암 경계와 얼굴 방향을 바꾸지 않는다.
half3 FuchsiaAdditionalFill(float3 positionWS, float2 screenUV, half3 normalWS,
    half receiveShadows, half strength, half ceiling)
{
    half3 fill = 0;
    #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX) || USE_CLUSTER_LIGHT_LOOP
        InputData inputData = (InputData)0;
        inputData.positionWS = positionWS;
        inputData.normalizedScreenSpaceUV = screenUV;
        #if USE_CLUSTER_LIGHT_LOOP
            UNITY_LOOP for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
            {
                CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                Light additional = GetAdditionalLight(lightIndex, positionWS, half4(1, 1, 1, 1));
                fill += FuchsiaFillContribution(additional, normalWS, receiveShadows);
            }
        #endif
        uint count = GetAdditionalLightsCount();
        LIGHT_LOOP_BEGIN(count)
            Light additional = GetAdditionalLight(lightIndex, positionWS, half4(1, 1, 1, 1));
            fill += FuchsiaFillContribution(additional, normalWS, receiveShadows);
        LIGHT_LOOP_END
    #endif
    fill *= strength;
    half luminance = dot(fill, half3(.2126h, .7152h, .0722h));
    // RGB를 같은 비율로 연속 압축해 광원 수가 늘어도 색조와 밝기 상한을 유지한다.
    return fill * (ceiling / max(ceiling + luminance, .0001h));
}

#endif

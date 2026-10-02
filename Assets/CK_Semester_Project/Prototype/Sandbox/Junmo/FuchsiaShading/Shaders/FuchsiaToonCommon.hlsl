#ifndef FUCHSIA_TOON_COMMON_INCLUDED
#define FUCHSIA_TOON_COMMON_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"

TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
// 모든 패스의 상수 배치를 맞춰 SRP Batcher 호환성을 유지한다.
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor, _ShadeColor, _DeepShadeColor, _EmissionColor;
    half4 _HighlightColor, _RimColor;
    half _ShadeStep, _DeepShadeStep, _ShadeFeather, _AmbientStrength, _UseEmission, _Cull;
    half _ReceiveShadowStrength, _ShadingStrength;
    half _HighlightStrength, _HighlightSize, _HighlightFeather;
    half _RimStrength, _RimPower;
CBUFFER_END

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
};
struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    half3 normalWS : TEXCOORD1;
    float2 uv : TEXCOORD2;
    half3 tangentWS : TEXCOORD3;
};
Varyings Vert(Attributes v)
{
    Varyings o;
    o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
    o.positionCS = TransformWorldToHClip(o.positionWS);
    o.normalWS = TransformObjectToWorldNormal(v.normalOS);
    o.tangentWS = TransformObjectToWorldDir(v.tangentOS.xyz);
    o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
    return o;
}
Light FuchsiaMainLight(float3 positionWS)
{
    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
        float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(positionWS));
    #else
        float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
    #endif
    return GetMainLight(shadowCoord);
}
half4 FuchsiaFrag(Varyings i)
{
    half3 n = normalize(i.normalWS);
    half3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);
    Light light = FuchsiaMainLight(i.positionWS);
    half shadow = lerp(1.0h, light.shadowAttenuation, _ReceiveShadowStrength);
    half illumination = saturate(dot(n, light.direction) * 0.5h + 0.5h) * shadow;
    half deepStep = min(_DeepShadeStep, _ShadeStep);
    // 화면 픽셀보다 얇은 명암 경계의 계단 현상을 완화한다.
    half feather = max(_ShadeFeather, fwidth(illumination) * 0.75h);
    half lit = smoothstep(_ShadeStep - feather, _ShadeStep + feather, illumination);
    half shade = smoothstep(deepStep - feather, deepStep + feather, illumination);
    half3 tint = lerp(_DeepShadeColor.rgb, _ShadeColor.rgb, shade);
    tint = lerp(tint, half3(1, 1, 1), lit);
    tint = lerp(half3(1, 1, 1), tint, _ShadingStrength);
    half3 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
    half3 ambient = max(half3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w), 0) * _AmbientStrength;
    half3 color = baseColor * (tint * light.color * light.distanceAttenuation + ambient);

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
        // 반대편 광택과 그림자 안의 광택을 억제한다.
        band *= saturate(dot(n, light.direction)) * shadow;
        color += _HighlightColor.rgb * light.color * band * _HighlightStrength;
    #endif
    #if defined(FUCHSIA_HAIR) || defined(FUCHSIA_ACCESSORY)
        half rim = pow(1.0h - saturate(dot(n, view)), _RimPower);
        color += _RimColor.rgb * rim * _RimStrength * saturate(dot(n, light.direction));
    #endif
    #if defined(_EMISSION)
        color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv).rgb * _EmissionColor.rgb;
    #endif
    return half4(color, 1);
}
#endif

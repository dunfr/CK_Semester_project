#ifndef FUCHSIA_OUTLINE_INCLUDED
#define FUCHSIA_OUTLINE_INCLUDED

// FuchsiaToonCommon supplies Attributes, textures and the shared UnityPerMaterial layout.
struct FuchsiaOutlineVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
};

FuchsiaOutlineVaryings FuchsiaOutlineVert(Attributes input)
{
    FuchsiaOutlineVaryings output;
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    if (dot(input.styleNormalOS, input.styleNormalOS) > .0001)
    {
        float3 styleNormalWS = TransformObjectToWorldNormal(input.styleNormalOS);
        normalWS = normalize(lerp(normalWS, styleNormalWS, _StyleNormalStrength));
    }

    float3 positionVS = TransformWorldToView(positionWS);
    float3 normalVS = TransformWorldToViewDir(normalWS, true);
    float4 positionCS = TransformWorldToHClip(positionWS);
    float4 normalCS = mul(UNITY_MATRIX_P, float4(normalVS, 0));

    // The perspective divide derivative stays correct away from the screen centre.
    // Normalize in pixel space so portrait aspect ratios do not widen horizontal edges.
    float2 directionNDC = normalCS.xy * positionCS.w - positionCS.xy * normalCS.w;
    float2 directionPixels = directionNDC * _ScaledScreenParams.xy;
    float lengthSquared = dot(directionPixels, directionPixels);
    float2 direction = directionPixels * rsqrt(max(lengthSquared, .000001));
    float eyeDepth = -positionVS.z;
    float nearFade = saturate((eyeDepth - _ProjectionParams.y) / max(_ProjectionParams.y, .001));
    float widthPixels = max(_OutlineWidth, 0) * nearFade * step(.000001, lengthSquared);
    positionCS.xy += direction * (2.0 * widthPixels / _ScaledScreenParams.xy) * positionCS.w;

    // Keep the original depth. Only the expanded silhouette can pass the existing opaque Z.
    output.positionCS = positionCS;
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    return output;
}

half4 FuchsiaOutlineFrag(FuchsiaOutlineVaryings input) : SV_Target
{
    clip(_OutlineWidth - .001h);
    half3 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
    half3 edgeColor = _OutlineColor.rgb * lerp(half3(1, 1, 1), saturate(baseColor), .55h);
    return half4(edgeColor, 1);
}
#endif

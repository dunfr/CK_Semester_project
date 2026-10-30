#ifndef SAKURA_MAGENTA_BURN_INCLUDED
#define SAKURA_MAGENTA_BURN_INCLUDED

float SakuraBurnHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float SakuraBurnNoise(float2 p)
{
    float2 cell = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(SakuraBurnHash(cell), SakuraBurnHash(cell + float2(1, 0)), f.x),
                lerp(SakuraBurnHash(cell + float2(0, 1)), SakuraBurnHash(cell + 1), f.x), f.y);
}

// Custom2.y advances the dark front; Custom2.z advances the trailing erase front.
// Use particle-local coordinates, not _Time, so scrubbing/replaying is deterministic.
void SakuraMagentaBurn_float(float Radius, float Along, float HeadDistance,
    float CoreRadius, float BodyWidth, float Growth, float Dissolve,
    float BurnLeadWidth, float FrontRoughness, float NoiseScale, float EdgeWidth,
    float EdgeGlow, float3 DarkColor,
    float3 MainColor, float BodyIntensity, float BurnDarkIntensity, float CoreMask,
    out float MainWeight, out float Survival, out float3 BurnColor,
    out float3 BodyColor, out float CoreWeight)
{
    float inward = saturate((CoreRadius - Radius) / max(BodyWidth, 0.001));
    float2 p = float2(Along * max(NoiseScale, 1.0), inward * 6.0);
    float noise = SakuraBurnNoise(p) * 0.72 + SakuraBurnNoise(p * 2.13 + 7.1) * 0.28;

    // Along runs from the tail (0) toward the head (1). Both fronts use the same
    // distorted coordinate: coherent travel, with narrow tears rather than holes
    // appearing everywhere at once. Never animate noise with global time.
    float field = Along + (noise - 0.5) * FrontRoughness + (1.0 - inward) * 0.035;
    float darkFront = lerp(-0.20, 1.30, saturate(Growth));
    float eraseFront = min(lerp(-0.20, 1.30, saturate(Dissolve)),
                           darkFront - max(BurnLeadWidth, 0.01));
    float colorFeather = max(fwidth(field), 0.012);
    MainWeight = smoothstep(darkFront - colorFeather, darkFront + colorFeather, field);
    float signedEdge = field - eraseFront;
    float aa = max(fwidth(signedEdge) * 0.75, 0.012);
    Survival = smoothstep(-aa, aa, signedEdge);
    Survival *= 1.0 - step(0.9999, Dissolve);
    float rim = (1.0 - smoothstep(aa, aa + max(EdgeWidth, 0.001), signedEdge)) * Survival;
    rim *= smoothstep(0.0, 0.04, Dissolve);
    float3 charColor = max(DarkColor, 0.0) * max(BurnDarkIntensity, 0.0);
    // Separate intensity controls: increasing the main pink cannot brighten ash.
    BodyColor = lerp(charColor, MainColor * max(BodyIntensity, 0.0), MainWeight);
    CoreWeight = MainWeight;
    // Replace the white core with the same dark tint before erasing it. The body
    // excludes the core in the existing graph, so fill that strip explicitly.
    BurnColor = charColor * saturate(CoreMask) * (1.0 - MainWeight)
              + charColor * rim * max(EdgeGlow, 0.0);
}

#endif

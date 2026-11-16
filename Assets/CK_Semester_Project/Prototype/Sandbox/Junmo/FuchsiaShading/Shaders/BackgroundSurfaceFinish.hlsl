#ifndef BACKGROUND_SURFACE_FINISH_INCLUDED
#define BACKGROUND_SURFACE_FINISH_INCLUDED
// 표면 마감: 모델러의 러프니스 맵과 부위 맵(BackgroundPartMapBaker가 모듈 메시에서 구운 보조 텍스처)으로
// 부위마다 다른 매끈함(smoothness = 1 - 러프니스)을 만든다.
// 배경 셰이더(Stylized Lit)와 바닥 반사막(Corridor Floor Reflection)이 같은 식을 쓰도록 따로 뺐다.

// 3D 값 노이즈(해시 기반, 텍스처 없음). 0~1.
float BgFinishHash31(float3 p)
{
    p = frac(p * float3(0.1031, 0.1030, 0.0973));
    p += dot(p, p.yxz + 33.33);
    return frac((p.x + p.y) * p.z);
}

float BgFinishValueNoise(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float3 u = f * f * (3.0 - 2.0 * f);
    float n000 = BgFinishHash31(i);
    float n100 = BgFinishHash31(i + float3(1, 0, 0));
    float n010 = BgFinishHash31(i + float3(0, 1, 0));
    float n110 = BgFinishHash31(i + float3(1, 1, 0));
    float n001 = BgFinishHash31(i + float3(0, 0, 1));
    float n101 = BgFinishHash31(i + float3(1, 0, 1));
    float n011 = BgFinishHash31(i + float3(0, 1, 1));
    float n111 = BgFinishHash31(i + float3(1, 1, 1));
    return lerp(lerp(lerp(n000, n100, u.x), lerp(n010, n110, u.x), u.y),
                lerp(lerp(n001, n101, u.x), lerp(n011, n111, u.x), u.y), u.z);
}

// 러프니스 맵 값의 범위를 다시 편다. remap = (맵에서 가장 거친 값, 가장 매끈한 값, 그때의 매끈함, 그때의 매끈함).
// 범위 밖의 맵 값도 같은 기울기로 이어서 계산한 뒤 0~1로 자른다. (0, 1, 0, 1)이면 맵 값 그대로다.
half BgFinishRemap(half mapValue, half4 remap)
{
    half t = (mapValue - remap.x) / max(remap.y - remap.x, half(0.001));
    return saturate(lerp(remap.z, remap.w, t));
}

// 부위마다 다른 매끈함을 돌려준다.
// partId: 부위 맵 R을 점 샘플로 읽은 값(부위 번호 / 255). 경계에서 번호가 섞이면 안 되므로 반드시 점 샘플이어야 한다.
// bevelInside: 부위 맵 G(모서리 면 정도)와 B(가장자리 0 → 안쪽 1). 부드럽게 읽는다.
// variation: x 부위별 변화량, y 얼룩 크기(m), z 가장자리 변화(음수 = 거칠게, 양수 = 매끈하게), w 모서리 면 광택 양.
half BgFinishSmoothness(half smoothness, half partId, half2 bevelInside, float3 positionWS, half4 variation, half bevelSmoothness)
{
    if (variation.x > half(0.001))
    {
        // 부위 번호마다 노이즈를 읽는 자리를 멀리 옮긴다. 그래서 이웃한 부위끼리 값이 따로 놀고,
        // 같은 부위 안에서는 월드 좌표를 따라 완만하게만 변한다(닳은 정도가 조금씩 다른 느낌).
        // 같은 모듈이 반복 배치돼도 월드 좌표가 달라 무늬가 되풀이되지 않는다.
        float k = round(partId * 255.0);
        float3 offset = frac(k * float3(0.7548777, 0.5698403, 0.4301597)) * 97.0;
        float n = BgFinishValueNoise(positionWS / max((float)variation.y, 0.05) + offset);
        // 값 노이즈는 0.5 근처에 몰리므로 양 끝으로 펴서, 눈에 띄게 매끈한 부위와 거친 부위가 고르게 섞이게 한다.
        n = smoothstep(0.2, 0.8, n);
        smoothness += half((n - 0.5) * 2.0) * variation.x;
    }
    smoothness += (half(1.0) - bevelInside.y) * variation.z;
    smoothness = lerp(smoothness, max(smoothness, bevelSmoothness), bevelInside.x * variation.w);
    return saturate(smoothness);
}

#endif

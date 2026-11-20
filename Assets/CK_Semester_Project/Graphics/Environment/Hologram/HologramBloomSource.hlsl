// 홀로그램 전용 Bloom에 빛을 내보내는 셰이더("HologramBloom" 패스)가 함께 쓰는 부분.
// 임계값을 여기서 미리 적용하는 까닭: 선·글자처럼 밝은 부분만 고르는 빛과,
// 화면 전체가 광원처럼 내는 빛(임계값과 무관하게 그대로 번져야 하는 빛)을 한 패스에서 같이 내보내기 위해서다.
#ifndef SEMESTER_HOLOGRAM_BLOOM_SOURCE_INCLUDED
#define SEMESTER_HOLOGRAM_BLOOM_SOURCE_INCLUDED

// HologramBloomFeature가 카메라마다 넣는다. x: 임계값(리니어), y: 임계값 무릎.
half4 _HologramBloomSource;

// Volume의 Threshold보다 밝은 부분만 남긴다(URP Bloom과 같은 식).
half3 HologramBloomThreshold(half3 color)
{
    half threshold = _HologramBloomSource.x;
    half knee = _HologramBloomSource.y;
    half brightness = max(color.r, max(color.g, color.b));
    half softness = clamp(brightness - threshold + knee, 0.0h, 2.0h * knee);
    softness = (softness * softness) / (4.0h * knee + 1e-4h);
    half multiplier = max(brightness - threshold, softness) / max(brightness, 1e-4h);
    return max(color * multiplier, 0.0h);
}

#endif

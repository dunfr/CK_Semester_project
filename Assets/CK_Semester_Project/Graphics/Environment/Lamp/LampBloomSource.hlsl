// 조명 전용 Bloom(LampBloomFeature)이 카메라마다 넣는 값과, 램프 셰이더가 그 값을 읽는 방법.
// 조명 종류는 세 가지다. 0: 복도 천장, 1: 거실 천장, 2: 기둥. 머티리얼의 Lamp Bloom Volume 항목으로 고른다.
#ifndef SEMESTER_LAMP_BLOOM_SOURCE_INCLUDED
#define SEMESTER_LAMP_BLOOM_SOURCE_INCLUDED

// xyz가 조명 종류 0·1·2의 값이다.
half4 _LampBloomBodyLimits;  // 화면에 보이는 램프 면 밝기의 상한. 0이면 그 종류의 전용 Bloom이 꺼져 있다.
half4 _LampBloomThresholds;  // 임계값(리니어).
half4 _LampBloomIntensities; // 번짐 세기.
half4 _LampBloomTint0;       // 번짐 색(밝기를 1로 맞춘 값).
half4 _LampBloomTint1;
half4 _LampBloomTint2;
half4 _LampBloomBucket;      // 지금 그리는 번짐 묶음에 들어가는 종류만 1.

// 조명 종류 번호를 (1,0,0) / (0,1,0) / (0,0,1)로 바꾼다.
half3 LampBloomSelect(half group)
{
    return half3(step(group, 0.5h), step(0.5h, group) * step(group, 1.5h), step(1.5h, group));
}

// 상한의 절반까지는 그대로 두고, 그 위는 상한에 부드럽게 붙인다(홀로그램과 같은 식).
// 채널마다 따로 눌러서, 아주 밝은 램프 면은 지금처럼 흰빛으로 보인다.
half3 LampBloomLimitBody(half3 color, half limit)
{
    half knee = limit * 0.5h;
    half shoulder = limit - knee;
    return min(color, knee) + shoulder * (1.0h - exp(-max(color - knee, 0.0h) / shoulder));
}

// 임계값보다 밝은 부분만 남긴다(URP Bloom과 같은 식, 무릎은 임계값의 절반).
half3 LampBloomThreshold(half3 color, half threshold)
{
    half knee = threshold * 0.5h;
    half brightness = max(color.r, max(color.g, color.b));
    half softness = clamp(brightness - threshold + knee, 0.0h, 2.0h * knee);
    softness = (softness * softness) / (4.0h * knee + 1e-4h);
    half multiplier = max(brightness - threshold, softness) / max(brightness, 1e-4h);
    return max(color * multiplier, 0.0h);
}

#endif

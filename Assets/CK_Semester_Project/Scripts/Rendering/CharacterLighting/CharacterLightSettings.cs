using System;
using UnityEngine;

namespace CK.SemesterProject.Rendering
{
    public enum CharacterKeyDirectionMode
    {
        // 씬 주광(방향광)의 회전에 오프셋을 더한다. 시간대/주광 연출을 그대로 따른다.
        FollowMainLight,
        // 월드 기준 고정 회전. 실내처럼 주광과 무관한 조명 구역에 쓴다.
        FixedWorld,
        // 카메라 수평 회전 기준. 보스전처럼 화면 구도에 맞춘 조명에 쓴다.
        CameraRelative,
        // 캐릭터 위치에서 주변 점광/스폿광이 들어오는 방향·색·광량을 합산한다. 배경 조명 배치가 곧 캐릭터 주광이 된다.
        // 주변에 들어오는 빛이 없으면 KeyEulerAngles(월드 고정)로 돌아간다.
        SceneLights,
    }

    // 구역/프로필이 작성하는 캐릭터 조명 값. 블렌드는 Resolve로 계산한 CharacterLightState에서 한다.
    [Serializable]
    public struct CharacterLightSettings
    {
        [Range(0f, 1f), Tooltip("0이면 씬 주광을 그대로 쓰고, 1이면 아래 가상 주광의 방향·색으로 완전히 바꿉니다.")]
        public float KeyOverride;

        [Tooltip("가상 주광 방향의 기준입니다.")]
        public CharacterKeyDirectionMode DirectionMode;

        [Tooltip("가상 주광 회전(도). FollowMainLight에서는 주광 회전에 더하는 오프셋입니다.")]
        public Vector3 KeyEulerAngles;

        [ColorUsage(false, true), Tooltip("가상 주광 색입니다.")]
        public Color KeyColor;

        [Min(0f), Tooltip("가상 주광 광량입니다. 밝기 반응이 0이면 색조에만 쓰입니다.")]
        public float KeyIntensity;

        [Range(0f, 1f), Tooltip("0이면 광량과 무관하게 원화 밝기를 유지하고, 1이면 광량/기준 광량 비율을 그대로 따릅니다.")]
        public float BrightnessResponse;

        [Min(.01f), Tooltip("밝기 1로 보는 광량입니다. 이보다 밝으면 밝아지고 어두우면 어두워집니다.")]
        public float BrightnessReference;

        [Range(0f, 1f), Tooltip("어두운 곳에서 내려갈 수 있는 최저 밝기입니다.")]
        public float MinBrightness;

        [Range(1f, 2f), Tooltip("밝은 곳에서 올라갈 수 있는 최고 밝기입니다.")]
        public float MaxBrightness;

        [Range(0f, 1f), Tooltip("환경 그림자가 캐릭터를 어둡게 하는 최대 비율입니다(가상 주광 사용 중일 때).")]
        public float EnvironmentShadowStrength;

        [ColorUsage(false, true), Tooltip("실루엣 림의 색조입니다.")]
        public Color RimTint;

        [Range(0f, 3f), Tooltip("실루엣 림 세기 배율입니다.")]
        public float RimStrength;

        [Header("SceneLights 모드")]
        [Tooltip("주광 계산에 넣을 배경 조명의 레이어입니다. 길 안내용 보조광 등을 빼려면 다른 레이어로 옮기세요.")]
        public LayerMask SceneLightLayers;

        [Range(0f, 1f), Tooltip("가상 주광 색을 배경 조명 색에서 가져오는 비율입니다. 0이면 KeyColor만 씁니다.")]
        public float SceneLightColorInfluence;

        [Range(-30f, 89f), Tooltip("주광 높이각 하한(도). 발밑에서 올라오는 빛을 막습니다.")]
        public float MinElevation;

        [Range(0f, 89f), Tooltip("주광 높이각 상한(도). 바로 위 천장등에서 얼굴이 전부 그늘지는 것을 막습니다.")]
        public float MaxElevation;

        public static CharacterLightSettings Default => new CharacterLightSettings
        {
            KeyOverride = 0f,
            DirectionMode = CharacterKeyDirectionMode.FollowMainLight,
            KeyEulerAngles = Vector3.zero,
            KeyColor = Color.white,
            KeyIntensity = 1f,
            BrightnessResponse = 0f,
            BrightnessReference = 1f,
            MinBrightness = .35f,
            MaxBrightness = 1.15f,
            EnvironmentShadowStrength = .18f,
            RimTint = Color.white,
            RimStrength = 1f,
            SceneLightLayers = ~0,
            SceneLightColorInfluence = .6f,
            MinElevation = 10f,
            MaxElevation = 50f,
        };

        // position: 조명을 판정할 캐릭터 지점. ignoreRoot: 이 아래에 달린 조명은 배경 조명에서 뺀다.
        public CharacterLightState Resolve(Light mainLight, Camera camera, Vector3 position, Transform ignoreRoot)
        {
            if (DirectionMode == CharacterKeyDirectionMode.SceneLights)
            {
                return ResolveSceneLights(position, ignoreRoot);
            }
            Quaternion rotation;
            switch (DirectionMode)
            {
                case CharacterKeyDirectionMode.FollowMainLight when mainLight != null:
                    // 주광은 forward 방향으로 비춘다. rotation * back = -forward = 광원을 향하는 방향.
                    rotation = mainLight.transform.rotation * Quaternion.Euler(KeyEulerAngles);
                    break;
                case CharacterKeyDirectionMode.CameraRelative when camera != null:
                    rotation = Quaternion.Euler(0f, camera.transform.eulerAngles.y, 0f) * Quaternion.Euler(KeyEulerAngles);
                    break;
                default:
                    rotation = Quaternion.Euler(KeyEulerAngles);
                    break;
            }

            Color color = QualitySettings.activeColorSpace == ColorSpace.Linear ? KeyColor.linear : KeyColor;
            // 셰이더 규칙: 표면에서 광원을 향하는 방향.
            return Build((rotation * Vector3.back).normalized, new Vector3(color.r, color.g, color.b) * KeyIntensity);
        }

        private CharacterLightState ResolveSceneLights(Vector3 position, Transform ignoreRoot)
        {
            Vector3 fallback = (Quaternion.Euler(KeyEulerAngles) * Vector3.back).normalized;
            Color tint = QualitySettings.activeColorSpace == ColorSpace.Linear ? KeyColor.linear : KeyColor;
            var tintColor = new Vector3(tint.r, tint.g, tint.b);
            if (!CharacterLightEnvironment.SampleSceneLights(position, SceneLightLayers, ignoreRoot,
                    out Vector3 direction, out Vector3 radiance))
            {
                return Build(fallback, tintColor * KeyIntensity);
            }

            direction = ClampElevation(direction, fallback);
            float luminance = radiance.x * .2126f + radiance.y * .7152f + radiance.z * .0722f;
            Vector3 hue = luminance > 1e-5f ? radiance / luminance : Vector3.one;
            // 배경 조명 색조를 KeyColor 쪽으로 섞고, 광량은 실제로 들어오는 양을 쓴다(밝기 반응용).
            Vector3 color = Vector3.Scale(Vector3.Lerp(Vector3.one, hue, SceneLightColorInfluence), tintColor);
            return Build(direction, color * (luminance * KeyIntensity));
        }

        private Vector3 ClampElevation(Vector3 direction, Vector3 fallback)
        {
            float minElevation = MinElevation, maxElevation = MaxElevation;
            if (maxElevation <= minElevation)
            {
                minElevation = 10f;
                maxElevation = 50f;
            }
            var horizontal = new Vector3(direction.x, 0f, direction.z);
            if (horizontal.sqrMagnitude < 1e-4f)
            {
                // 바로 위나 아래에서 오는 빛은 수평 방향이 정해지지 않으므로 기본 방향의 수평을 빌린다.
                horizontal = new Vector3(fallback.x, 0f, fallback.z);
                if (horizontal.sqrMagnitude < 1e-4f) horizontal = Vector3.forward;
            }
            horizontal.Normalize();
            float elevation = Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
            elevation = Mathf.Clamp(elevation, minElevation, maxElevation) * Mathf.Deg2Rad;
            return (horizontal * Mathf.Cos(elevation) + Vector3.up * Mathf.Sin(elevation)).normalized;
        }

        private CharacterLightState Build(Vector3 direction, Vector3 keyColor)
        {
            Color rim = QualitySettings.activeColorSpace == ColorSpace.Linear ? RimTint.linear : RimTint;
            return new CharacterLightState
            {
                KeyDirection = direction,
                KeyWeight = KeyOverride,
                KeyColor = keyColor,
                Brightness = new Vector4(BrightnessReference, MinBrightness, MaxBrightness, BrightnessResponse),
                EnvironmentShadowStrength = EnvironmentShadowStrength,
                RimTint = new Vector4(rim.r, rim.g, rim.b, RimStrength),
            };
        }
    }

    // 셰이더에 그대로 넣을 수 있게 풀어 둔 조명 상태. 구역 겹침, 보스 덮어쓰기, 시간 보간은 모두 이 값으로 한다.
    public struct CharacterLightState
    {
        public Vector3 KeyDirection;
        public float KeyWeight;
        public Vector3 KeyColor;
        public Vector4 Brightness;
        public float EnvironmentShadowStrength;
        public Vector4 RimTint;

        public static CharacterLightState Lerp(CharacterLightState a, CharacterLightState b, float t)
        {
            t = Mathf.Clamp01(t);
            Vector3 direction = Vector3.Slerp(a.KeyDirection, b.KeyDirection, t);
            return new CharacterLightState
            {
                KeyDirection = direction.sqrMagnitude > 1e-6f ? direction.normalized : b.KeyDirection,
                KeyWeight = Mathf.Lerp(a.KeyWeight, b.KeyWeight, t),
                KeyColor = Vector3.Lerp(a.KeyColor, b.KeyColor, t),
                Brightness = Vector4.Lerp(a.Brightness, b.Brightness, t),
                EnvironmentShadowStrength = Mathf.Lerp(a.EnvironmentShadowStrength, b.EnvironmentShadowStrength, t),
                RimTint = Vector4.Lerp(a.RimTint, b.RimTint, t),
            };
        }

        public bool Approximately(CharacterLightState other)
        {
            return (KeyDirection - other.KeyDirection).sqrMagnitude < 1e-8f
                && Mathf.Abs(KeyWeight - other.KeyWeight) < 1e-4f
                && (KeyColor - other.KeyColor).sqrMagnitude < 1e-8f
                && (Brightness - other.Brightness).sqrMagnitude < 1e-8f
                && Mathf.Abs(EnvironmentShadowStrength - other.EnvironmentShadowStrength) < 1e-4f
                && (RimTint - other.RimTint).sqrMagnitude < 1e-8f;
        }
    }
}

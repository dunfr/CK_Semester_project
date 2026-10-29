using System.Collections.Generic;
using UnityEngine;

namespace CK.SemesterProject.Rendering
{
    // FollowMainLight 기준이 되는 씬 주광을 찾는다. URP와 같은 순서로 RenderSettings.sun, 없으면 가장 밝은 방향광.
    public static class CharacterLightEnvironment
    {
        private const float SearchInterval = 1f;
        private static Light _cached;
        private static float _nextSearchTime;
        private static readonly List<Light> PunctualCache = new List<Light>();
        private static float _nextPunctualSearchTime;

        // 씬의 점광/스폿광 목록. 매 프레임 찾지 않도록 1초마다 갱신한다.
        public static IReadOnlyList<Light> PunctualLights
        {
            get
            {
                float now = Time.realtimeSinceStartup;
                if (now >= _nextPunctualSearchTime || PunctualCache.Exists(light => light == null))
                {
                    _nextPunctualSearchTime = now + SearchInterval;
                    PunctualCache.Clear();
                    foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    {
                        if (light.type == LightType.Point || light.type == LightType.Spot)
                        {
                            PunctualCache.Add(light);
                        }
                    }
                }
                return PunctualCache;
            }
        }

        // 배경 조명이 한 지점에 들어오는 양을 URP와 같은 감쇠식으로 합산한다.
        // direction: 그 지점에서 광원들을 향하는 가중 평균 방향, radiance: 선형 색 x 광량 합.
        public static bool SampleSceneLights(Vector3 position, LayerMask layers, Transform ignoreRoot,
            out Vector3 direction, out Vector3 radiance)
        {
            direction = Vector3.zero;
            radiance = Vector3.zero;
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            foreach (Light light in PunctualLights)
            {
                if (light == null || !light.isActiveAndEnabled || light.intensity <= 0f
                    || (layers.value & (1 << light.gameObject.layer)) == 0
                    // 캐릭터에 붙어 함께 움직이는 연출광은 배경 조명이 아니므로 뺀다.
                    || (ignoreRoot != null && light.transform.IsChildOf(ignoreRoot)))
                {
                    continue;
                }
                Vector3 toLight = light.transform.position - position;
                float distanceSquared = toLight.sqrMagnitude;
                float rangeSquared = light.range * light.range;
                if (distanceSquared >= rangeSquared)
                {
                    continue;
                }
                // URP: 역제곱 x 범위 창 (1 - (d²/r²)²)²
                float window = 1f - (distanceSquared / rangeSquared) * (distanceSquared / rangeSquared);
                float attenuation = window * window / Mathf.Max(distanceSquared, .0001f);
                Vector3 toLightDirection = toLight / Mathf.Sqrt(Mathf.Max(distanceSquared, 1e-8f));
                if (light.type == LightType.Spot)
                {
                    float cosOuter = Mathf.Cos(light.spotAngle * .5f * Mathf.Deg2Rad);
                    float cosInner = Mathf.Cos(Mathf.Max(light.innerSpotAngle, 0f) * .5f * Mathf.Deg2Rad);
                    float cosAngle = Vector3.Dot(light.transform.forward, -toLightDirection);
                    float spot = Mathf.Clamp01((cosAngle - cosOuter) / Mathf.Max(cosInner - cosOuter, 1e-4f));
                    attenuation *= spot * spot;
                }
                if (attenuation <= 0f)
                {
                    continue;
                }
                Color color = linear ? light.color.linear : light.color;
                Vector3 contribution = new Vector3(color.r, color.g, color.b) * (light.intensity * attenuation);
                radiance += contribution;
                direction += toLightDirection * (contribution.x * .2126f + contribution.y * .7152f + contribution.z * .0722f);
            }
            if (direction.sqrMagnitude < 1e-10f)
            {
                return false;
            }
            direction.Normalize();
            return true;
        }

        public static Light MainLight
        {
            get
            {
                if (RenderSettings.sun != null && RenderSettings.sun.isActiveAndEnabled)
                {
                    return RenderSettings.sun;
                }
                float now = Time.realtimeSinceStartup;
                if (_cached != null && _cached.isActiveAndEnabled && now < _nextSearchTime)
                {
                    return _cached;
                }
                _nextSearchTime = now + SearchInterval;
                _cached = null;
                float best = -1f;
                foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                {
                    if (light.type == LightType.Directional && light.isActiveAndEnabled && light.intensity > best)
                    {
                        best = light.intensity;
                        _cached = light;
                    }
                }
                return _cached;
            }
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace CK.SemesterProject.Tutorial
{
    /// <summary>
    /// 베기 타임라인의 타격감 클립. 클립 자체는 재생되지 않고, 컷신 재생기(TutorialSlashHitFeel)가 트랙 데이터를 직접 읽는다.
    /// HitStop: 클립 시작 시점(타임라인 시간)부터 Duration초 동안 '실시간'으로 월드를 SlowScale 배속으로 거의 멈춘다.
    ///          그동안 타임라인은 Duration x SlowScale 만큼만 흐른다(클립 뒤의 이펙트는 그만큼 뒤에 배치).
    /// CameraShake: 클립 시작(= 그 시점이 실시간으로 도달했을 때)부터 Duration초(실시간) 동안 카메라를 흔든다. 히트스톱 중에도 계속 흔들린다.
    /// </summary>
    public sealed class TutorialSlashHitFeelClip : PlayableAsset, ITimelineClipAsset
    {
        public enum FeelKind
        {
            HitStop,
            CameraShake,
        }

        public FeelKind Kind = FeelKind.HitStop;

        [Header("Hit Stop")]
        [Range(0f, 1f), Tooltip("히트스톱 중 월드 진행 속도 배율. 0이면 완전 정지, 0.05면 거의 멈춘 채 아주 천천히 움직인다.")]
        public float SlowScale = 0.06f;

        [Header("Camera Shake")]
        [Min(0f), Tooltip("위치 흔들림 세기(m)")]
        public float Amplitude = 0.08f;
        [Min(0f), Tooltip("회전 흔들림 세기(도)")]
        public float RotationAmplitude = 0.9f;
        [Min(1f), Tooltip("흔들리는 빠르기(Hz)")]
        public float Frequency = 26f;
        [Range(0.5f, 4f), Tooltip("시간에 따라 줄어드는 정도. 1이면 직선 감쇠, 2면 제곱 감쇠")]
        public float DecayPower = 1.6f;
        [Tooltip("히트스톱 중에는 흔들림을 유지하고, 끝난 뒤부터 감쇠를 시작한다.")]
        public bool HoldDuringHitStop = true;

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            return Playable.Create(graph);
        }

        /// <summary>localTime(초, 실시간) 시점의 위치(카메라 로컬 m)·회전(도) 흔들림. 감쇠는 0~1 진행률로 계산한다.</summary>
        public bool EvaluateShake(float localTime, float duration, float holdTime, int seed, out Vector3 position, out Vector3 rotation)
        {
            position = Vector3.zero;
            rotation = Vector3.zero;
            if (localTime < 0f || localTime > duration || duration <= 0f)
            {
                return false;
            }
            float fade = Mathf.Max(0f, localTime - holdTime) / Mathf.Max(0.0001f, duration - holdTime);
            float decay = Mathf.Pow(1f - Mathf.Clamp01(fade), DecayPower);
            float t = localTime * Frequency;
            position = new Vector3(Noise(t, seed), Noise(t, seed + 17), Noise(t, seed + 31) * 0.3f) * (Amplitude * decay);
            rotation = new Vector3(Noise(t, seed + 47), Noise(t, seed + 59), Noise(t, seed + 71)) * (RotationAmplitude * decay);
            return true;
        }

        private static float Noise(float t, int seed)
        {
            return Mathf.PerlinNoise(t, seed * 0.731f) * 2f - 1f;
        }
    }
}

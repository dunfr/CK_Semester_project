using System;
using UnityEngine;

namespace CK.SemesterProject.SkillSequence
{
    /// <summary>스킬 연출 중 특정 시점의 카메라 흔들림. 시작할 때 가장 세고 길이 동안 줄어든다.</summary>
    [Serializable]
    public sealed class SkillCameraShakeEvent
    {
        [SerializeField, Min(0f), Tooltip("흔들림 시작 시점(초)")]
        private float _time;
        [SerializeField, Min(0.01f), Tooltip("흔들림 길이(초)")]
        private float _duration = 0.35f;
        [SerializeField, Min(0f), Tooltip("위치 흔들림 세기(m). 기본 공격 0.05~0.1, 강한 타격 0.15~0.3")]
        private float _amplitude = 0.1f;
        [SerializeField, Min(0f), Tooltip("회전 흔들림 세기(도). 0.5~2 권장")]
        private float _rotationAmplitude = 0.8f;
        [SerializeField, Min(1f), Tooltip("흔들리는 빠르기(Hz). 빠른 타격감 20~30, 묵직한 진동 8~12")]
        private float _frequency = 22f;

        public float Time => _time;
        public float Duration => _duration;
        public float Amplitude => _amplitude;
        public float RotationAmplitude => _rotationAmplitude;
        public float Frequency => _frequency;

        /// <summary>시작 후 localTime 초의 위치(카메라 로컬, m)·회전(도) 흔들림.</summary>
        public bool Evaluate(float localTime, int seed, out Vector3 position, out Vector3 rotation)
        {
            position = Vector3.zero;
            rotation = Vector3.zero;
            if (localTime < 0f || localTime > _duration)
            {
                return false;
            }
            float decay = 1f - localTime / _duration;
            decay *= decay;
            float t = localTime * _frequency;
            position = new Vector3(Noise(t, seed), Noise(t, seed + 17), Noise(t, seed + 31) * 0.3f) * (_amplitude * decay);
            rotation = new Vector3(Noise(t, seed + 47), Noise(t, seed + 59), Noise(t, seed + 71)) * (_rotationAmplitude * decay);
            return true;
        }

        private static float Noise(float t, int seed)
        {
            return Mathf.PerlinNoise(t, seed * 0.731f) * 2f - 1f;
        }
    }
}

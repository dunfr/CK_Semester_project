using System;
using UnityEngine;

namespace CK.SemesterProject.Tutorial
{
    [Serializable]
    public struct BattleVfxEffectPlacement
    {
        [Tooltip("기준 더미의 로컬 축 기준 위치(m)")]
        public Vector3 Position;
        [Tooltip("기준 더미의 로컬 축 기준 회전(도)")]
        public Vector3 Rotation;
        [Tooltip("저장 프리팹 크기에 곱할 배율")]
        public float Scale;
        [Tooltip("파티클 재생 속도 배율. 0은 기본값 1배를 사용합니다")]
        public float PlaybackSpeed;
        [Tooltip("생성 후 재생 시작까지의 지연(초)")]
        public float StartDelay;
        [Tooltip("재생 시작 후 표시 시간(초). 0은 이펙트의 재생 길이를 사용합니다")]
        public float Lifetime;

        public float EffectivePlaybackSpeed => PlaybackSpeed > 0f ? Mathf.Clamp(PlaybackSpeed, .05f, 4f) : 1f;

        public void Apply(GameObject instance, Transform dummy)
        {
            if (instance == null || dummy == null)
            {
                throw new ArgumentException("재생할 이펙트와 기준 더미가 필요합니다.");
            }
            instance.transform.SetPositionAndRotation(dummy.position + dummy.rotation * Position,
                dummy.rotation * Quaternion.Euler(Rotation));
            instance.transform.localScale *= Scale > 0f ? Scale : 1f;
        }
    }
}

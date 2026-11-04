using System;
using UnityEngine;

namespace CK.SemesterProject.Tutorial
{
    public enum SkillVfxAnchor
    {
        Actor = 0,
        Target = 1,
        BladeSocket = 2
    }

    [Serializable]
    public struct SkillVfxPlacement
    {
        public bool Enabled;
        public SkillVfxAnchor Anchor;
        [Tooltip("기준점의 로컬 축 기준 위치(m)")]
        public Vector3 Position;
        [Tooltip("기준점의 로컬 축 기준 회전(도)")]
        public Vector3 Rotation;
        [Tooltip("프리팹 원래 크기에 곱할 배율")]
        public float Scale;
        public bool Follow;
        [Tooltip("파티클 재생 속도 배율. 0은 기본값 1배를 사용합니다")]
        public float PlaybackSpeed;
        [Tooltip("생성 후 파티클 재생 시작까지의 지연(초)")]
        public float StartDelay;
        [Tooltip("재생 시작 후 표시 시간(초). 0은 기존 종료 처리를 사용합니다")]
        public float Lifetime;

        public float EffectivePlaybackSpeed => PlaybackSpeed > 0f ? Mathf.Clamp(PlaybackSpeed, .05f, 4f) : 1f;

        public Transform GetAnchor(Transform actor, Transform target)
        {
            if (Anchor == SkillVfxAnchor.BladeSocket)
            {
                Transform socket = WeaponEffectSocket.Find(actor);
                if (socket == null)
                {
                    throw new InvalidOperationException("칼날 소켓(BladeSocket)을 찾을 수 없습니다.");
                }
                return socket;
            }
            return Anchor == SkillVfxAnchor.Actor ? actor : target;
        }

        public void Apply(GameObject instance, Transform actor, Transform target)
        {
            Transform anchor = GetAnchor(actor, target);
            if (Anchor == SkillVfxAnchor.BladeSocket)
            {
                instance.transform.SetParent(anchor, false);
                instance.transform.localPosition = Position;
                instance.transform.localRotation = Quaternion.Euler(Rotation);
                instance.transform.localScale *= Scale > 0f ? Scale : 1f;
            }
            else
            {
                instance.transform.SetPositionAndRotation(anchor.position + anchor.rotation * Position,
                    anchor.rotation * Quaternion.Euler(Rotation));
                instance.transform.localScale *= Scale > 0f ? Scale : 1f;
                if (Follow)
                {
                    instance.transform.SetParent(anchor, true);
                }
            }
            // 수동 배치는 프리팹에 저장된 자식 배치를 사용한다. 자동 배치가 값을 덮어쓰지 않게 한다.
            foreach (TutorialSlashVfxPlacement placement in instance.GetComponentsInChildren<TutorialSlashVfxPlacement>(true))
            {
                placement.enabled = false;
            }
            foreach (TutorialChargeVfxPlacement placement in instance.GetComponentsInChildren<TutorialChargeVfxPlacement>(true))
            {
                placement.enabled = false;
            }
        }
    }
}

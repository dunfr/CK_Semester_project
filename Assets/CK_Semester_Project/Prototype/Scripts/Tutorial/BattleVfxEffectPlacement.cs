using System;
using UnityEngine;

namespace CK.SemesterProject.Tutorial
{
    public enum BattleVfxEffectAnchor
    {
        Target = 0,
        BladeSocket = 1
    }

    [Serializable]
    public struct BattleVfxEffectPlacement
    {
        [Tooltip("씬에서 저장할 때 더미 또는 칼날 소켓 부착 기준을 자동으로 기록합니다.")]
        public BattleVfxEffectAnchor AttachmentAnchor;
        [Tooltip("더미 또는 칼날 소켓 기준 상대 위치")]
        public Vector3 Position;
        [Tooltip("더미 또는 칼날 소켓 기준 상대 회전(도)")]
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

        public Transform GetAnchor(Transform dummy)
        {
            if (dummy == null)
            {
                throw new ArgumentException("재생 기준이 있는 씬을 확인하세요.");
            }
            if (AttachmentAnchor != BattleVfxEffectAnchor.BladeSocket)
            {
                return dummy;
            }
            foreach (GameObject root in dummy.gameObject.scene.GetRootGameObjects())
            {
                Transform socket = WeaponEffectSocket.Find(root.transform);
                if (socket != null)
                {
                    return socket;
                }
            }
            throw new InvalidOperationException("현재 씬에 칼날 소켓 BladeSocket이 없습니다. 검의 소켓이 있는 캐릭터를 배치하세요.");
        }

        public void Apply(GameObject instance, Transform dummy)
        {
            if (instance == null || dummy == null)
            {
                throw new ArgumentException("재생할 이펙트와 기준 더미가 필요합니다.");
            }
            Transform anchor = GetAnchor(dummy);
            Vector3 worldScale = instance.transform.lossyScale * (Scale > 0f ? Scale : 1f);
            if (AttachmentAnchor == BattleVfxEffectAnchor.BladeSocket)
            {
                instance.transform.SetParent(anchor, false);
                SamplePose(instance.transform, anchor);
                Vector3 parentScale = anchor.lossyScale;
                instance.transform.localScale = new Vector3(
                    DivideScale(worldScale.x, parentScale.x),
                    DivideScale(worldScale.y, parentScale.y),
                    DivideScale(worldScale.z, parentScale.z));
            }
            else
            {
                SamplePose(instance.transform, anchor);
                instance.transform.localScale *= Scale > 0f ? Scale : 1f;
            }
        }

        public void SamplePose(Transform instance, Transform anchor)
        {
            if (AttachmentAnchor == BattleVfxEffectAnchor.BladeSocket)
            {
                instance.localPosition = Position;
                instance.localRotation = Quaternion.Euler(Rotation);
            }
            else
            {
                instance.SetPositionAndRotation(anchor.position + anchor.rotation * Position,
                    anchor.rotation * Quaternion.Euler(Rotation));
            }
        }

        private static float DivideScale(float scale, float parentScale)
        {
            return Mathf.Abs(parentScale) > .00001f ? scale / parentScale : scale;
        }
    }
}

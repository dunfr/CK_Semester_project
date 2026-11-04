using System;
using UnityEngine;

namespace CK.SemesterProject.SkillSequence
{
    /// <summary>스킬 연출 중 특정 시점에 이펙트 프리팹을 생성하는 이벤트.</summary>
    [Serializable]
    public sealed class SkillVfxEvent
    {
        [SerializeField, Tooltip("에디터 타임라인에 표시할 이름")]
        private string _name = "이펙트";
        [SerializeField, Min(0f), Tooltip("생성 시점(초)")]
        private float _time;
        [SerializeField, Tooltip("생성할 이펙트 프리팹")]
        private GameObject _prefab;
        [SerializeField, Tooltip("생성 위치의 기준점")]
        private SkillVfxAnchorPoint _anchor = SkillVfxAnchorPoint.TargetCenter;
        [SerializeField, Tooltip("기준점이 시전자 본(CasterBone)일 때 사용할 휴머노이드 본")]
        private HumanBodyBones _bone = HumanBodyBones.RightHand;
        [SerializeField, Tooltip("켜면 생성 후에도 기준점을 따라 움직입니다(칼날 궤적, 손에 붙는 빛 등).")]
        private bool _followAnchor;
        [SerializeField, Tooltip("대상 기준 이펙트를 선택 대상에만 낼지, 모든 대상마다 낼지")]
        private SkillVfxTargetMode _targetMode = SkillVfxTargetMode.SelectedTarget;
        [SerializeField, Tooltip("기준점에서의 위치 차이(m). 시전자·대상·무대 기준은 '시전자가 대상을 바라보는 방향' 기준이고, 본·칼날 소켓 기준은 그 본의 로컬 방향 기준입니다.")]
        private Vector3 _positionOffset;
        [SerializeField, Tooltip("기준 방향에서의 회전 차이(도)")]
        private Vector3 _rotationOffset;
        [SerializeField, Min(0.01f), Tooltip("이펙트 크기 배율")]
        private float _scale = 1f;
        [SerializeField, Min(0f), Tooltip("유지 시간(초). 0이면 파티클 길이로 자동 계산합니다.")]
        private float _lifetime;
        [SerializeField, Min(0.01f), Tooltip("파티클 재생 속도 배율")]
        private float _playbackSpeed = 1f;

        public string Name => _name;
        public float Time => _time;
        public GameObject Prefab => _prefab;
        public SkillVfxAnchorPoint Anchor => _anchor;
        public HumanBodyBones Bone => _bone;
        public bool FollowAnchor => _followAnchor;
        public SkillVfxTargetMode TargetMode => _targetMode;
        public Vector3 PositionOffset => _positionOffset;
        public Vector3 RotationOffset => _rotationOffset;
        public float Scale => _scale;
        public float Lifetime => _lifetime;
        public float PlaybackSpeed => _playbackSpeed;
        public bool UsesTarget => _anchor == SkillVfxAnchorPoint.TargetCenter || _anchor == SkillVfxAnchorPoint.TargetFeet;

        /// <summary>실제 유지 시간. 직접 지정하지 않았으면 프리팹의 파티클 길이로 추정한다.</summary>
        public float GetEffectiveLifetime()
        {
            return _lifetime > 0f ? _lifetime : SkillSequenceUtility.EstimateLifetime(_prefab) / Mathf.Max(0.01f, _playbackSpeed);
        }
    }
}

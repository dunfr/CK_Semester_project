using System.Collections.Generic;
using UnityEngine;

namespace CK.SemesterProject.SkillSequence
{
    /// <summary>
    /// 스킬 하나의 연출 데이터. 시전자 애니메이션, 선택적인 대상 접근·복귀 이동, 이펙트 생성 이벤트, 타격 시점을 담는다.
    /// 스킬 연출 스튜디오(CK > 스킬 연출 스튜디오)에서 편집한다.
    /// </summary>
    [CreateAssetMenu(menuName = "CK/Skill Sequence", fileName = "SkillSequence")]
    public sealed class SkillSequenceAsset : ScriptableObject
    {
        [Header("Animation")]
        [SerializeField, Tooltip("시전자 애니메이션. 이동은 이동 설정이 담당하므로 제자리(In-place) 클립을 권장합니다.")]
        private AnimationClip _casterClip;
        [SerializeField, Min(0f), Tooltip("애니메이션이 시작되는 시점(초). 타임라인에서 애니메이션 블럭을 끌어 조절합니다.")]
        private float _clipStart;
        [SerializeField, Min(0.1f), Tooltip("연출 전체 길이(초)")]
        private float _duration = 1.5f;
        [SerializeField, Min(1f), Tooltip("에디터 프레임 표시·스냅 기준(fps)")]
        private float _frameRate = 30f;

        [Header("Movement (선택)")]
        [SerializeField, Tooltip("켜면 대상 앞까지 다가갔다가 제자리로 돌아옵니다.")]
        private bool _useMovement;
        [SerializeField, Min(0f), Tooltip("접근 시작(초)")]
        private float _approachStart;
        [SerializeField, Min(0f), Tooltip("접근 완료(초)")]
        private float _approachEnd = 0.25f;
        [SerializeField, Min(0f), Tooltip("대상 표면에서 멈출 거리(m). 대상 크기가 달라도 표면 기준으로 맞춥니다.")]
        private float _attackDistance = 1.2f;
        [SerializeField, Min(0f), Tooltip("복귀 시작(초)")]
        private float _returnStart = 1f;
        [SerializeField, Min(0f), Tooltip("복귀 완료(초)")]
        private float _returnEnd = 1.3f;
        [SerializeField, Tooltip("접근·복귀 이동 곡선(0~1)")]
        private AnimationCurve _moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Events")]
        [SerializeField] private List<SkillVfxEvent> _vfxEvents = new List<SkillVfxEvent>();
        [SerializeField, Tooltip("타격(피해 표시) 시점(초)")]
        private List<float> _hitTimes = new List<float>();

        public AnimationClip CasterClip => _casterClip;
        public float ClipStart => _clipStart;
        public float Duration => _duration;
        public float FrameRate => _frameRate;
        public bool UseMovement => _useMovement;
        public float ApproachStart => _approachStart;
        public float ApproachEnd => Mathf.Max(_approachStart, _approachEnd);
        public float AttackDistance => _attackDistance;
        public float ReturnStart => Mathf.Max(ApproachEnd, _returnStart);
        public float ReturnEnd => Mathf.Max(ReturnStart, _returnEnd);
        public AnimationCurve MoveCurve => _moveCurve;
        public IReadOnlyList<SkillVfxEvent> VfxEvents => _vfxEvents;
        public IReadOnlyList<float> HitTimes => _hitTimes;
    }
}

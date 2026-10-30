using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CK.SemesterProject.SkillSequence
{
    /// <summary>
    /// 지정 시간의 시전자 위치(접근·복귀 이동)와 애니메이션 포즈를 적용한다.
    /// 에디터 미리보기(슬라이더)와 실제 재생이 같은 계산을 쓰도록 분리했다.
    /// </summary>
    public sealed class SkillSequenceSampler : IDisposable
    {
        private readonly SkillSequenceAsset _sequence;
        private readonly SkillSequenceContext _context;
        private PlayableGraph _graph;
        private AnimationClipPlayable _clipPlayable;
        private Animator _animator;
        private bool _hadRootMotion;

        public SkillSequenceSampler(SkillSequenceAsset sequence, SkillSequenceContext context)
        {
            _sequence = sequence;
            _context = context;
            _animator = context.CasterAnimator;
            if (sequence.CasterClip == null || _animator == null)
            {
                return;
            }
            // 위치는 이동 설정이 담당하므로 루트 모션은 샘플링 동안 끈다.
            _hadRootMotion = _animator.applyRootMotion;
            _animator.applyRootMotion = false;
            _graph = PlayableGraph.Create("SkillSequenceSampler");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(_graph, "Caster", _animator);
            _clipPlayable = AnimationClipPlayable.Create(_graph, sequence.CasterClip);
            _clipPlayable.SetApplyFootIK(false);
            output.SetSourcePlayable(_clipPlayable);
        }

        public SkillSequenceContext Context => _context;

        /// <summary>대상 표면 앞의 공격 위치. 대상이 없으면 제자리.</summary>
        public Vector3 ApproachPoint
        {
            get
            {
                Transform target = _context.SelectedTarget;
                if (target == null)
                {
                    return _context.CasterHome.position;
                }
                Vector3 feet = _context.GetTargetFeet(target);
                Vector3 toCaster = Vector3.ProjectOnPlane(_context.CasterHome.position - feet, Vector3.up);
                float distance = _context.GetTargetRadius(target) + _sequence.AttackDistance;
                Vector3 point = toCaster.sqrMagnitude > 0.0001f ? feet + toCaster.normalized * distance : feet;
                point.y = _context.CasterHome.position.y;
                return point;
            }
        }

        public Vector3 EvaluateCasterPosition(float time)
        {
            Vector3 home = _context.CasterHome.position;
            if (!_sequence.UseMovement || _context.SelectedTarget == null)
            {
                return home;
            }
            Vector3 approach = ApproachPoint;
            if (time <= _sequence.ApproachStart)
            {
                return home;
            }
            if (time < _sequence.ApproachEnd)
            {
                return Vector3.LerpUnclamped(home, approach, EvaluateCurve(time, _sequence.ApproachStart, _sequence.ApproachEnd));
            }
            if (time <= _sequence.ReturnStart)
            {
                return approach;
            }
            if (time < _sequence.ReturnEnd)
            {
                return Vector3.LerpUnclamped(approach, home, EvaluateCurve(time, _sequence.ReturnStart, _sequence.ReturnEnd));
            }
            return home;
        }

        public void Sample(float time)
        {
            _context.Caster.SetPositionAndRotation(EvaluateCasterPosition(time), _context.Facing);
            if (_graph.IsValid())
            {
                // 애니메이션 시작 전에는 첫 프레임, 끝난 뒤에는 마지막 프레임 포즈를 유지한다.
                _clipPlayable.SetTime(Mathf.Clamp(time - _sequence.ClipStart, 0f, _sequence.CasterClip.length));
                _graph.Evaluate(0f);
            }
        }

        public void Dispose()
        {
            if (_graph.IsValid())
            {
                _graph.Destroy();
                if (_animator != null)
                {
                    _animator.applyRootMotion = _hadRootMotion;
                }
            }
            if (_context.Caster != null)
            {
                _context.Caster.SetPositionAndRotation(_context.CasterHome.position, _context.CasterHome.rotation);
            }
        }

        private float EvaluateCurve(float time, float start, float end)
        {
            float progress = end > start ? Mathf.Clamp01((time - start) / (end - start)) : 1f;
            return _sequence.MoveCurve != null && _sequence.MoveCurve.length > 0 ? _sequence.MoveCurve.Evaluate(progress) : progress;
        }
    }
}

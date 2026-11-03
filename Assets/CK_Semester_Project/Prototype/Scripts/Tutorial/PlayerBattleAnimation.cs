using System;
using System.Collections.Generic;
using UnityEngine;

namespace CK.SemesterProject.Tutorial
{
    // 전투 중 플레이어 모션의 FSM. 필드 이동 모션은 PlayerMovement가 맡고, 전투 중에는 PlayerMovement가 꺼진다.
    // - 전투에 들어가면 전투 대기(BattleIdle) 상태.
    // - 스킬을 쓰면 아래 표에 있는 그 스킬의 행동 모션을 재생하고, 끝나면 전투 대기로 돌아간다.
    // - 전투가 끝나면 상태를 비우고, 다시 켜진 PlayerMovement가 필드 모션으로 되돌린다.
    // 스킬 모션이 생기면 Animator Controller에 상태를 추가하고 표에 스킬 ID와 상태 이름을 넣는다.
    // 표에 없거나 Controller에 그 상태가 없는 스킬은 전투 대기 모션을 유지한다.
    // 모션에 연출 카메라가 있으면(궁극기의 Ult_cam) 그 노드를 표에 넣는다. 재생하는 동안 TutorialBattleController가 그 시점으로 바꾼다.
    [DisallowMultipleComponent]
    public sealed class PlayerBattleAnimation : MonoBehaviour
    {
        [Serializable]
        private struct SkillMotion
        {
            [Tooltip("Skill_DT의 스킬 ID")]
            public string SkillId;

            [Tooltip("Animator Controller의 상태 이름")]
            public string AnimatorState;

            [Tooltip("대상 앞까지 다가갔다 돌아오는 기존 전투 연출을 함께 쓸지 정합니다. 모션 안에 동작이 다 들어 있으면 끕니다.")]
            public bool ApproachTarget;

            [Tooltip("모션이 움직이는 연출 카메라 노드(예: 궁극기의 Ult_cam). 넣으면 모션이 재생되는 동안 전투 카메라가 이 시점으로 바뀌고, 끝나면 원래 시점으로 돌아갑니다.")]
            public Transform CinematicCamera;

            [Tooltip("연출 카메라가 바라보는 타깃 노드(예: Ult_cam.Target). 3ds Max 타깃 카메라처럼 카메라 위치에서 이 노드 쪽을 봅니다.")]
            public Transform CinematicCameraTarget;

            [Tooltip("연출 카메라의 가로 시야각(도, 3ds Max 카메라 FOV와 같은 기준). FBX에 시야각이 없어 Max 기본값 45를 씁니다. 0이면 전투 카메라 시야각을 유지합니다.")]
            public float CinematicFieldOfView;
        }

        [SerializeField] private Animator _animator;
        [SerializeField, Tooltip("전투 대기 모션의 Animator 상태 이름")]
        private string _battleIdleState = "BattleIdle";
        [SerializeField, Min(0f), Tooltip("모션 사이를 넘어가는 시간(초)")]
        private float _fadeSeconds = .2f;
        [SerializeField, Tooltip("스킬별 행동 모션")]
        private SkillMotion[] _skillMotions =
        {
            new SkillMotion { SkillId = "SK10", AnimatorState = "Ult", ApproachTarget = false, CinematicFieldOfView = 45f },
        };

        private readonly HashSet<string> _reportedMissingStates = new HashSet<string>();
        private PlayerBattleState _currentState;
        private PlayerBattleIdleState _idleState;

        public bool IsInBattle => _currentState != null;
        public string CurrentStateName => _currentState?.Name ?? "Field";

        internal PlayerBattleState IdleState => _idleState ??= new PlayerBattleIdleState(_battleIdleState);

        public void EnterBattle()
        {
            ChangeState(IdleState);
        }

        // 스킬 행동 모션을 재생하고 그 길이(초)를 돌려준다. 재생할 모션이 없으면 전투 대기를 유지하고 0을 돌려준다.
        public float PlaySkill(string skillId, out bool approachTarget)
        {
            approachTarget = true;
            if (!IsInBattle)
            {
                return 0f;
            }
            foreach (SkillMotion motion in _skillMotions)
            {
                if (motion.SkillId != skillId)
                {
                    continue;
                }
                if (!HasAnimatorState(motion.AnimatorState))
                {
                    if (_reportedMissingStates.Add(motion.AnimatorState))
                    {
                        Debug.LogWarning($"Animator Controller에 '{motion.AnimatorState}' 상태가 없어 전투 대기 모션을 유지합니다.", this);
                    }
                    return 0f;
                }

                var action = new PlayerBattleActionState(motion.AnimatorState,
                    motion.CinematicCamera, motion.CinematicCameraTarget, motion.CinematicFieldOfView);
                ChangeState(action);
                approachTarget = motion.ApproachTarget;
                return action.Length;
            }
            return 0f;
        }

        // 지금 재생 중인 행동 모션에 연출 카메라가 있으면 그 시점과 가로 시야각을 돌려준다. 전투 카메라는 이 동안 이 시점을 쓴다.
        // 시선은 3ds Max 타깃 카메라처럼 카메라 노드 위치에서 타깃 노드 위치 쪽이다. 노드 회전(쿼터니언)은 숏이 바뀌는 컷에서
        // 한 프레임 동안 돌며 두 숏 사이를 훑으므로, 컷이 깨끗한 두 위치로 방향을 정하고 기울기(roll)만 노드 회전의 위쪽을 쓴다.
        // 타깃이 없으면 노드 회전을 쓴다. 노드는 Max 카메라처럼 자기 -Z 쪽을 보므로 Y축으로 180° 돌려 Unity 카메라의 +Z로 맞춘다.
        public bool TryGetCinematicCamera(out Pose pose, out float horizontalFieldOfView)
        {
            if (_currentState is PlayerBattleActionState action && action.CinematicCamera != null)
            {
                Transform node = action.CinematicCamera;
                Quaternion view = node.rotation * Quaternion.AngleAxis(180f, Vector3.up);
                Vector3 toTarget = action.CinematicCameraTarget != null
                    ? action.CinematicCameraTarget.position - node.position
                    : Vector3.zero;
                if (toTarget.sqrMagnitude > 1e-6f)
                {
                    view = Quaternion.LookRotation(toTarget, node.up);
                }
                pose = new Pose(node.position, view);
                horizontalFieldOfView = action.CinematicFieldOfView;
                return true;
            }

            pose = default;
            horizontalFieldOfView = 0f;
            return false;
        }

        public void ExitBattle()
        {
            ChangeState(null);
        }

        private void Awake()
        {
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }
        }

        private void Update()
        {
            _currentState?.Tick(this, Time.deltaTime);
        }

        internal void ChangeState(PlayerBattleState nextState)
        {
            _currentState?.Exit(this);
            _currentState = nextState;
            nextState?.Enter(this);
        }

        // 그 상태 모션으로 넘어가고 모션 길이(초)를 돌려준다.
        internal float CrossFade(string animatorState)
        {
            if (!HasAnimatorState(animatorState))
            {
                return 0f;
            }

            int hash = Animator.StringToHash(animatorState);
            _animator.CrossFadeInFixedTime(hash, _fadeSeconds, 0);
            _animator.Update(0f);
            AnimatorStateInfo next = _animator.GetNextAnimatorStateInfo(0);
            return next.shortNameHash == hash ? next.length : _animator.GetCurrentAnimatorStateInfo(0).length;
        }

        private bool HasAnimatorState(string animatorState) => _animator != null && _animator.isActiveAndEnabled
            && _animator.runtimeAnimatorController != null && _animator.HasState(0, Animator.StringToHash(animatorState));
    }
}

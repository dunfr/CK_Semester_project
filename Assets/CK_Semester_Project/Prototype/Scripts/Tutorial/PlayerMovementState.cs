using UnityEngine;

namespace CK.SemesterProject.Tutorial
{
    // 플레이어 이동 FSM의 상태 하나(상태 패턴). 상태마다 클래스를 하나씩 두고 아래를 정한다.
    // - Enter / Exit: 들어오고 나갈 때 할 일. 기본 Enter는 Animator Controller의 AnimatorStateName 상태로 넘어간다.
    // - GetNextState: 이번 입력으로 넘어갈 상태. 그대로 있으려면 자기 자신을 돌려준다.
    // - Tick: 이 상태에 있는 동안 매 프레임 하는 이동.
    // 새 상태(예: 점프)는 이 클래스를 상속해 만들고, PlayerMovement에 인스턴스를 두고,
    // 그 상태로 들어갈 수 있는 상태(땅 위 상태라면 PlayerGroundedState)의 GetNextState에 전이를 넣는다.
    internal abstract class PlayerMovementState
    {
        private int _animatorStateHash;

        public abstract string Name { get; }
        public virtual bool IsSprinting => false;

        // Animator Controller의 상태 이름. 기본은 Name과 같다.
        // Controller에 이 이름의 상태가 없으면(모션이 아직 없는 상태) 이전 모션을 그대로 둔다.
        public virtual string AnimatorStateName => Name;

        public int AnimatorStateHash => _animatorStateHash != 0
            ? _animatorStateHash
            : _animatorStateHash = Animator.StringToHash(AnimatorStateName);

        // 이전 모션에서 이 상태의 모션으로 넘어가는 데 걸리는 시간(초).
        public virtual float AnimationFadeDuration => .15f;

        public virtual void Enter(PlayerMovement player)
        {
            player.PlayStateAnimation(this);
        }

        public virtual void Exit(PlayerMovement player)
        {
        }

        public abstract PlayerMovementState GetNextState(PlayerMovement player, in PlayerMovementInput input);

        public abstract void Tick(PlayerMovement player, in PlayerMovementInput input);
    }
}

using UnityEngine;

namespace CK.SemesterProject.Tutorial
{
    // 점프. 땅 위 상태(Idle / Walk / Run)에서 땅에 닿아 있을 때 Space를 누르면 들어온다. 공중에서는 다시 뛰지 않는다.
    // - 도약: 점프 모션 앞부분의 웅크림 동안(JumpTakeoffDelay) 땅에 있다가 발을 떼며 위로 뛴다.
    // - 공중: 이동 입력으로 방향을 바꾼다. 속도는 뛰기 직전 그대로다(달리다 뛰면 달리기 속도).
    // - 착지: 이동 입력이 있으면 곧바로 Walk / Run, 없으면 점프 모션의 착지 동작이 끝난 뒤 Idle.
    //   착지 동작 중 Space를 다시 누르면 처음부터 다시 뛴다.
    // 점프 모션(FuchsiaJump.anim)은 몸이 떠오르는 높이를 뺀 제자리 모션이고, 실제 높이는 CharacterController가 낸다.
    internal sealed class PlayerJumpState : PlayerMovementState
    {
        private float _elapsed;
        private bool _hasTakenOff;
        private bool _keepsRunSpeed;

        public override string Name => "Jump";
        public override bool IsSprinting => _keepsRunSpeed;

        // 웅크림이 0.1초뿐이라 이전 모션에서 빨리 넘어간다.
        public override float AnimationFadeDuration => .1f;

        public override void Enter(PlayerMovement player)
        {
            // PlayerMovement는 Enter가 끝난 뒤 IsSprinting을 바꾸므로, 여기서는 아직 직전 상태의 값이다.
            _keepsRunSpeed = player.IsSprinting;
            Begin();
            base.Enter(player);
        }

        public override PlayerMovementState GetNextState(PlayerMovement player, in PlayerMovementInput input)
        {
            // 도약 전이거나 아직 공중이면 점프를 이어 간다. 착지 동작 중 다시 누른 Space는 Tick에서 처음부터 다시 뛴다.
            if (!_hasTakenOff || !player.IsGrounded || input.JumpRequested)
            {
                return this;
            }

            return input.HasMove || player.HasFinishedStateAnimation(this)
                ? PlayerGroundedState.SelectByMove(player, input)
                : this;
        }

        public override void Tick(PlayerMovement player, in PlayerMovementInput input)
        {
            if (_hasTakenOff && player.IsGrounded && input.JumpRequested)
            {
                Begin();
                player.RestartStateAnimation(this);
            }

            _elapsed += Time.deltaTime;
            if (!_hasTakenOff && _elapsed >= player.JumpTakeoffDelay)
            {
                _hasTakenOff = true;
                player.TakeOff();
            }
            player.MoveCharacter(input.Move, _keepsRunSpeed ? player.RunSpeed : player.WalkSpeed);
        }

        private void Begin()
        {
            _elapsed = 0f;
            _hasTakenOff = false;
        }
    }
}

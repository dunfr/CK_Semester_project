namespace CK.SemesterProject.Tutorial
{
    // 땅 위 상태(Idle / Walk / Run)의 공통 전이. 입력만 보고 고른다.
    // - 땅에 닿아 있을 때 Space를 누르면 Jump. 땅을 떠나는 전이라 이동 전이보다 먼저 검사한다.
    // - 이동 입력이 없으면 Idle. Shift만 눌러서는 달리지 않는다.
    // - 이동 중 달리기 요청(Shift 또는 HUD 달리기 토글)이 있으면 Run, 없으면 Walk.
    internal abstract class PlayerGroundedState : PlayerMovementState
    {
        public override PlayerMovementState GetNextState(PlayerMovement player, in PlayerMovementInput input)
        {
            if (input.JumpRequested && player.IsGrounded)
            {
                return player.JumpState;
            }

            return SelectByMove(player, input);
        }

        // 이동 입력으로 고르는 땅 위 상태. 점프가 착지한 뒤에도 이 규칙으로 돌아온다.
        internal static PlayerMovementState SelectByMove(PlayerMovement player, in PlayerMovementInput input)
        {
            if (!input.HasMove)
            {
                return player.IdleState;
            }

            return input.SprintRequested ? player.RunState : player.WalkState;
        }
    }
}

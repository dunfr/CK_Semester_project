namespace CK.SemesterProject.Tutorial
{
    // 땅 위 상태(Idle / Walk / Run)의 공통 전이. 입력만 보고 고른다.
    // - 이동 입력이 없으면 Idle. Shift만 눌러서는 달리지 않는다.
    // - 이동 중 달리기 요청(Shift 또는 HUD 달리기 토글)이 있으면 Run, 없으면 Walk.
    // 점프처럼 땅을 떠나는 상태는 이 전이보다 먼저 검사하도록 여기에 추가한다.
    internal abstract class PlayerGroundedState : PlayerMovementState
    {
        public override PlayerMovementState GetNextState(PlayerMovement player, in PlayerMovementInput input)
        {
            if (!input.HasMove)
            {
                return player.IdleState;
            }

            return input.SprintRequested ? player.RunState : player.WalkState;
        }
    }
}

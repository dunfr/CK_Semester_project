namespace CK.SemesterProject.Tutorial
{
    // 걷기·달리기처럼 정해진 속도로 땅 위를 움직이는 상태.
    internal abstract class PlayerMoveState : PlayerGroundedState
    {
        protected abstract float GetMoveSpeed(PlayerMovement player);

        public override void Tick(PlayerMovement player, in PlayerMovementInput input)
        {
            player.MoveCharacter(input.Move, GetMoveSpeed(player));
            player.UpdateBlockedPose(this);
        }
    }
}

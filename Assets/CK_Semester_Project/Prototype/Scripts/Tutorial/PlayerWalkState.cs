namespace CK.SemesterProject.Tutorial
{
    internal sealed class PlayerWalkState : PlayerMoveState
    {
        public override string Name => "Walk";

        protected override float GetMoveSpeed(PlayerMovement player) => player.WalkSpeed;
    }
}

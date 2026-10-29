namespace CK.SemesterProject.Tutorial
{
    internal sealed class PlayerRunState : PlayerMoveState
    {
        public override string Name => "Run";
        public override bool IsSprinting => true;

        protected override float GetMoveSpeed(PlayerMovement player) => player.RunSpeed;
    }
}

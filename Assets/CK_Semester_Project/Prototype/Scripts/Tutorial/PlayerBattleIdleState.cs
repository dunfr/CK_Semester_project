namespace CK.SemesterProject.Tutorial
{
    // 전투 대기. 행동 모션이 끝나면 여기로 돌아온다.
    internal sealed class PlayerBattleIdleState : PlayerBattleState
    {
        public PlayerBattleIdleState(string animatorState) : base(animatorState)
        {
        }

        public override string Name => "BattleIdle";

        public override void Enter(PlayerBattleAnimation owner)
        {
            owner.CrossFade(AnimatorState);
        }
    }
}

namespace CK.SemesterProject.Tutorial
{
    // 전투 중 플레이어 모션 FSM의 상태 하나(상태 패턴). 상태마다 클래스를 하나씩 두고,
    // Enter에서 자기 Animator 상태로 넘어가고, Tick에서 다음 상태로 넘어갈지 정한다.
    // 새 전투 상태(피격·방어·승리 등)는 이 클래스를 상속해 만들고 PlayerBattleAnimation에서 그 상태로 바꾼다.
    internal abstract class PlayerBattleState
    {
        protected PlayerBattleState(string animatorState)
        {
            AnimatorState = animatorState;
        }

        public abstract string Name { get; }

        // Animator Controller의 상태 이름.
        public string AnimatorState { get; }

        public virtual void Enter(PlayerBattleAnimation owner)
        {
        }

        public virtual void Exit(PlayerBattleAnimation owner)
        {
        }

        public virtual void Tick(PlayerBattleAnimation owner, float deltaTime)
        {
        }
    }
}

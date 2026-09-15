namespace CK.SemesterProject.Battle
{
    public sealed class BattleStateChange
    {
        public CombatantState Before { get; }
        public CombatantState After { get; }
        public int HpDelta => After.Hp - Before.Hp;
        public int MemoryDelta => After.Memory - Before.Memory;
        public bool BecameDead => !Before.IsDead && After.IsDead;

        internal BattleStateChange(CombatantState before, CombatantState after)
        {
            Before = before;
            After = after;
        }
    }
}

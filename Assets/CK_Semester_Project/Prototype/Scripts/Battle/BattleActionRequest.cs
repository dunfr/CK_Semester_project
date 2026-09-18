namespace CK.SemesterProject.Battle
{
    public sealed class BattleActionRequest
    {
        public long TurnId { get; }
        public string ActorId { get; }
        public BattleActionKind Kind { get; }
        public string SkillId { get; }
        public string TargetId { get; }
        public int MemoryInvestment { get; }
        public int? InvestmentStage { get; }

        public BattleActionRequest(long turnId, string actorId, BattleActionKind kind,
            string skillId = null, string targetId = null, int memoryInvestment = 0, int? investmentStage = null)
        {
            TurnId = turnId;
            ActorId = actorId;
            Kind = kind;
            SkillId = skillId;
            TargetId = targetId;
            MemoryInvestment = memoryInvestment;
            InvestmentStage = investmentStage;
        }
    }
}

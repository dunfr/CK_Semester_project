namespace CK.SemesterProject.Battle
{
    // 다음 기능 브랜치의 계산기가 반환할 상태 변화. 세션이 전부 검증한 후 일괄 적용한다.
    public sealed class BattleEffect
    {
        public string TargetId { get; }
        public int HpDelta { get; }
        public int MemoryDelta { get; }
        public int? SkippedTurns { get; }
        public bool? IsDefending { get; }
        public int RageDelta { get; }
        public int? ChainStep { get; }
        public int? ImprintDamage { get; }

        public BattleEffect(string targetId, int hpDelta = 0, int memoryDelta = 0, int? skippedTurns = null,
            bool? isDefending = null, int rageDelta = 0, int? chainStep = null, int? imprintDamage = null)
        {
            TargetId = targetId;
            HpDelta = hpDelta;
            MemoryDelta = memoryDelta;
            SkippedTurns = skippedTurns;
            IsDefending = isDefending;
            RageDelta = rageDelta;
            ChainStep = chainStep;
            ImprintDamage = imprintDamage;
        }
    }
}

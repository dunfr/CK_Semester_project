namespace CK.SemesterProject.Battle
{
    // 다음 기능 브랜치의 계산기가 반환할 상태 변화. 세션이 전부 검증한 후 일괄 적용한다.
    public sealed class BattleEffect
    {
        public string TargetId { get; }
        public int HpDelta { get; }
        public int MemoryDelta { get; }
        public int? SkippedTurns { get; }

        public BattleEffect(string targetId, int hpDelta = 0, int memoryDelta = 0, int? skippedTurns = null)
        {
            TargetId = targetId;
            HpDelta = hpDelta;
            MemoryDelta = memoryDelta;
            SkippedTurns = skippedTurns;
        }
    }
}

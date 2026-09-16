using System;

namespace CK.SemesterProject.Battle
{
    public sealed class MonsterAiSettings
    {
        public int MaxMemoryInvestment { get; }
        public double MemoryWeight { get; }
        public double KillBonus { get; }

        // 초기 AI 정책값이며 몬스터별 확정 밸런스 데이터가 아니다.
        public MonsterAiSettings(int maxMemoryInvestment = 3, double memoryWeight = 1,
            double killBonus = 100)
        {
            if (maxMemoryInvestment < 0 || double.IsNaN(memoryWeight) || double.IsInfinity(memoryWeight)
                || memoryWeight < 0 || double.IsNaN(killBonus) || double.IsInfinity(killBonus) || killBonus < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxMemoryInvestment));
            }
            MaxMemoryInvestment = maxMemoryInvestment;
            MemoryWeight = memoryWeight;
            KillBonus = killBonus;
        }
    }
}

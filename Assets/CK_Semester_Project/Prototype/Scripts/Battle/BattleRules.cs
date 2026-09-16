using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.SemesterProject.Battle
{
    public sealed class BattleRules
    {
        public IReadOnlyList<double> InvestmentMultipliers { get; }
        public int DefenseRageReductionPerMemory { get; }
        public double DefenseDamageMultiplier { get; }

        // 인덱스는 투자 메모리 수량이다. 상세 기획 확정 전에는 0 투자만 기본 제공한다.
        public BattleRules(IEnumerable<double> investmentMultipliers = null,
            int defenseRageReductionPerMemory = 0, double defenseDamageMultiplier = 0.5)
        {
            double[] values = (investmentMultipliers ?? new[] { 1.0 }).ToArray();
            if (values.Length == 0 || values[0] != 1.0
                || values.Any(value => double.IsNaN(value) || double.IsInfinity(value) || value < 1)
                || defenseRageReductionPerMemory < 0 || double.IsNaN(defenseDamageMultiplier)
                || defenseDamageMultiplier < 0 || defenseDamageMultiplier > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(investmentMultipliers));
            }
            InvestmentMultipliers = Array.AsReadOnly(values);
            DefenseRageReductionPerMemory = defenseRageReductionPerMemory;
            DefenseDamageMultiplier = defenseDamageMultiplier;
        }
    }
}

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
        public BattleCombatRules Mechanics { get; }
        public bool UsesInvestmentStages { get; }
        public bool EnableMemoryLoss { get; }
        public int MonsterCollisionMemoryBonus { get; }

        // 기본값은 초기 메모리 기준 5단계다. 명시적인 배열은 이전 수량별 표 호환용이다.
        public BattleRules(IEnumerable<double> investmentMultipliers = null,
            int defenseRageReductionPerMemory = 0, double defenseDamageMultiplier = 0.5,
            BattleCombatRules mechanics = null, bool? enableMemoryLoss = null, int monsterCollisionMemoryBonus = 0)
        {
            if (monsterCollisionMemoryBonus < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(monsterCollisionMemoryBonus));
            }
            MonsterCollisionMemoryBonus = monsterCollisionMemoryBonus;
            UsesInvestmentStages = investmentMultipliers == null;
            EnableMemoryLoss = enableMemoryLoss ?? UsesInvestmentStages;
            double[] values = (investmentMultipliers ?? new[] { 1.0, 1.1, 1.2, 1.35, 1.5, 1.7 }).ToArray();
            if (values.Length == 0 || values[0] != 1.0
                || values.Any(value => double.IsNaN(value) || double.IsInfinity(value) || value < 1)
                || defenseRageReductionPerMemory < 0 || double.IsNaN(defenseDamageMultiplier)
                || defenseDamageMultiplier < 0 || defenseDamageMultiplier > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(investmentMultipliers));
            }
            InvestmentMultipliers = Array.AsReadOnly(values);
            Mechanics = mechanics ?? new BattleCombatRules();
            DefenseRageReductionPerMemory = defenseRageReductionPerMemory;
            DefenseDamageMultiplier = defenseDamageMultiplier;
        }

        public int GetStageCost(CombatantData actor, int stage)
        {
            if (actor == null || stage < 0 || stage > 5)
            {
                throw new ArgumentOutOfRangeException(nameof(stage));
            }
            return (int)((long)actor.InitialMemory * stage / 10);
        }

        public bool TryGetInvestment(CombatantData actor, BattleActionRequest request,
            out int cost, out double multiplier, out int rageReduction)
        {
            cost = 0;
            multiplier = 1;
            rageReduction = 0;
            if (UsesInvestmentStages)
            {
                int stage = request.InvestmentStage ?? 0;
                if (request.MemoryInvestment != 0 || stage < 0 || stage > 5)
                {
                    return false;
                }
                cost = GetStageCost(actor, stage);
                if (stage > 0 && cost == 0)
                {
                    return false;
                }
                multiplier = InvestmentMultipliers[stage];
                rageReduction = stage == 0 ? 0 : 5 * stage + 5;
                return true;
            }
            // 명시적인 수량별 표는 기존 데모와 회귀 테스트 호환용이다.
            if (request.InvestmentStage.HasValue || request.MemoryInvestment < 0
                || (request.Kind == BattleActionKind.Skill && request.MemoryInvestment >= InvestmentMultipliers.Count))
            {
                return false;
            }
            cost = request.MemoryInvestment;
            multiplier = request.Kind == BattleActionKind.Skill ? InvestmentMultipliers[cost] : 1;
            rageReduction = (int)Math.Min(int.MaxValue, (long)cost * DefenseRageReductionPerMemory);
            return true;
        }
    }
}

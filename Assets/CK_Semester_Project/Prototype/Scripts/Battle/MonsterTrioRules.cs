using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.SemesterProject.Battle
{
    public sealed class MonsterTrioRules
    {
        public int FinisherHpPercent { get; }
        public int SurvivorHpPercent { get; }
        public double SurvivorDefenseChance { get; }
        public IReadOnlyList<int> AttackWeights { get; }
        public IReadOnlyList<MonsterInvestmentRange> WaitingAttack { get; }
        public IReadOnlyList<MonsterInvestmentRange> DefenseForward { get; }
        public IReadOnlyList<MonsterInvestmentRange> DefenseReverse { get; }
        public IReadOnlyList<MonsterInvestmentRange> SurvivorDefense { get; }

        public MonsterTrioRules(int finisherHpPercent = 40, int survivorHpPercent = 75,
            double survivorDefenseChance = 0.5, int[] attackWeights = null,
            MonsterInvestmentRange[] waitingAttack = null, MonsterInvestmentRange[] defenseForward = null,
            MonsterInvestmentRange[] defenseReverse = null, MonsterInvestmentRange[] survivorDefense = null)
        {
            int[] weights = attackWeights ?? new[] { 50, 30, 20 };
            if (finisherHpPercent < 0 || finisherHpPercent > 100 || survivorHpPercent < 0 || survivorHpPercent > 100
                || double.IsNaN(survivorDefenseChance) || survivorDefenseChance < 0 || survivorDefenseChance > 1
                || weights.Length != 3 || weights.Any(value => value < 0) || weights.Sum(value => (long)value) <= 0)
            {
                throw new ArgumentException("3기 AI 설정이 잘못되었습니다.");
            }
            FinisherHpPercent = finisherHpPercent;
            SurvivorHpPercent = survivorHpPercent;
            SurvivorDefenseChance = survivorDefenseChance;
            AttackWeights = Array.AsReadOnly(weights.ToArray());
            WaitingAttack = Copy(waitingAttack ?? new[] { R(2, 3), R(2, 4), R(1, 3), R(1, 3), R(4, 5) }, 5, 7);
            DefenseForward = Copy(defenseForward ?? new[] { R(0, 0, 0), R(0, 0, 0), R(2, 2, .5), R(2, 2, .5), R(1, 1, .6), R(1, 1, .7) }, 6, 5);
            DefenseReverse = Copy(defenseReverse ?? new[] { R(0, 0, 0), R(0, 0, 0), R(1, 1, .25), R(2, 2, .5), R(2, 2, .6), R(3, 3, .7) }, 6, 5);
            SurvivorDefense = Copy(survivorDefense ?? new[] { R(1, 2), R(2, 3), R(2, 4), R(2, 3), R(1, 3), R(1, 3) }, 6, 4);
        }

        private static MonsterInvestmentRange R(int min, int max, double chance = 1)
        {
            return new MonsterInvestmentRange(min, max, chance);
        }

        private static IReadOnlyList<MonsterInvestmentRange> Copy(MonsterInvestmentRange[] rows, int count, int max)
        {
            if (rows.Length != count || rows.Any(row => row == null || row.Max > max))
            {
                throw new ArgumentException("3기 AI 행동표가 잘못되었습니다.");
            }
            return Array.AsReadOnly(rows.ToArray());
        }
    }
}

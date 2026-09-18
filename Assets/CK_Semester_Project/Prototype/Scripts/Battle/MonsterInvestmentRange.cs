using System;

namespace CK.SemesterProject.Battle
{
    public sealed class MonsterInvestmentRange
    {
        public int Min { get; }
        public int Max { get; }
        public double DefenseChance { get; }

        public MonsterInvestmentRange(int min, int max, double defenseChance = 1)
        {
            if (min < 0 || max < min || max > 7 || double.IsNaN(defenseChance)
                || defenseChance < 0 || defenseChance > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(min));
            }
            Min = min;
            Max = max;
            DefenseChance = defenseChance;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.SemesterProject.Battle
{
    public sealed class BattleCombatRules
    {
        public double CriticalChance { get; }
        public double CriticalMultiplier { get; }
        public double WeaknessMultiplier { get; }
        public double ResistanceMultiplier { get; }
        public IReadOnlyList<double> ChainMultipliers { get; }
        public bool EnableElementEffects { get; }
        public bool EnableRage { get; }
        public int AfterimageRecovery { get; }
        public int OblivionSteal { get; }
        public double ImprintRatio { get; }
        public const int OverheatThreshold = 91;

        // 기존 코어 회귀 테스트와 최소 데모에서 명시적으로 사용하는 설정이다.
        public static BattleCombatRules Basic => new BattleCombatRules(criticalChance: 0,
            enableElementEffects: false, enableRage: false);

        public BattleCombatRules(double criticalChance = 0.1, double criticalMultiplier = 1.2,
            double weaknessMultiplier = 1.25, double resistanceMultiplier = 0.75,
            IEnumerable<double> chainMultipliers = null, bool enableElementEffects = true,
            bool enableRage = true, int afterimageRecovery = 3, int oblivionSteal = 2, double imprintRatio = 0.1)
        {
            double[] chain = (chainMultipliers ?? new[] { 1.0, 1.0, 1.2, 1.3, 1.0 }).ToArray();
            if (!IsFinite(criticalChance) || criticalChance < 0 || criticalChance > 1
                || !IsFinite(criticalMultiplier) || criticalMultiplier < 1
                || !IsFinite(weaknessMultiplier) || weaknessMultiplier < 0
                || !IsFinite(resistanceMultiplier) || resistanceMultiplier < 0
                || chain.Length != 5 || chain.Any(value => !IsFinite(value) || value < 0)
                || afterimageRecovery < 0 || oblivionSteal < 0
                || !IsFinite(imprintRatio) || imprintRatio < 0 || imprintRatio > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(criticalChance));
            }
            CriticalChance = criticalChance;
            CriticalMultiplier = criticalMultiplier;
            WeaknessMultiplier = weaknessMultiplier;
            ResistanceMultiplier = resistanceMultiplier;
            ChainMultipliers = Array.AsReadOnly(chain);
            EnableElementEffects = enableElementEffects;
            EnableRage = enableRage;
            AfterimageRecovery = afterimageRecovery;
            OblivionSteal = oblivionSteal;
            ImprintRatio = imprintRatio;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}

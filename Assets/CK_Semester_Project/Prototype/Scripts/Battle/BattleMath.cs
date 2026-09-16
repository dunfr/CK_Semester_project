using System;

namespace CK.SemesterProject.Battle
{
    public static class BattleMath
    {
        public static bool IsWeakness(BattleElement attack, BattleElement target)
        {
            return (attack == BattleElement.Afterimage && target == BattleElement.Imprint)
                || (attack == BattleElement.Imprint && target == BattleElement.Oblivion)
                || (attack == BattleElement.Oblivion && target == BattleElement.Afterimage);
        }

        public static double ElementMultiplier(BattleElement attack, BattleElement target, BattleCombatRules rules)
        {
            if (attack == BattleElement.None || target == BattleElement.None || attack == target)
            {
                return 1;
            }
            return IsWeakness(attack, target) ? rules.WeaknessMultiplier : rules.ResistanceMultiplier;
        }

        public static double RageMultiplier(int energy)
        {
            return energy >= 75 ? 1.35 : energy >= 45 ? 1.2 : energy >= 20 ? 1.08 : 1;
        }

        public static int RoundDamage(double value)
        {
            return (int)Math.Max(0, Math.Min(int.MaxValue, Math.Round(value, MidpointRounding.AwayFromZero)));
        }
    }
}

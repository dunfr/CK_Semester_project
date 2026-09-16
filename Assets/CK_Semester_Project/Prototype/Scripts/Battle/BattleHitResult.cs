namespace CK.SemesterProject.Battle
{
    public sealed class BattleHitResult
    {
        public bool IsHit { get; }
        public bool IsCritical { get; }
        public double HitChance { get; }
        public int BaseDamage { get; }
        public int Damage { get; }
        public double ElementMultiplier { get; }
        public double ChainMultiplier { get; }
        public double RageMultiplier { get; }
        public double CriticalMultiplier { get; }
        public double InvestmentMultiplier { get; }
        public double DefenseMultiplier { get; }
        public int ChainStepReached { get; }
        public bool GrantsExtraAction => ChainStepReached == 4;

        internal BattleHitResult(bool isHit, bool isCritical, double hitChance, int baseDamage,
            int damage, double elementMultiplier, double chainMultiplier, double rageMultiplier,
            double criticalMultiplier, double investmentMultiplier, double defenseMultiplier, int chainStepReached)
        {
            IsHit = isHit;
            IsCritical = isCritical;
            HitChance = hitChance;
            BaseDamage = baseDamage;
            Damage = damage;
            ElementMultiplier = elementMultiplier;
            ChainMultiplier = chainMultiplier;
            RageMultiplier = rageMultiplier;
            CriticalMultiplier = criticalMultiplier;
            InvestmentMultiplier = investmentMultiplier;
            DefenseMultiplier = defenseMultiplier;
            ChainStepReached = chainStepReached;
        }
    }
}

using System;

namespace CK.SemesterProject.Battle
{
    public sealed class SkillData
    {
        public string Id { get; }
        public string DisplayName { get; }
        public BattleElement Element { get; }
        public SkillTarget Target { get; }
        public int Power { get; }
        public int MemoryCost { get; }
        public int MemoryRecovery { get; }
        public int MemorySteal { get; }
        public double Accuracy { get; }
        public double? CriticalChance { get; }
        public double BonusCriticalChance { get; }
        public string EnglishName { get; }
        public int RageGain { get; }
        public int RageCost { get; }
        public bool IsAreaOfEffect { get; }
        public int InflictedSkippedTurns { get; }

        public SkillData(string id, string displayName, int power,
            BattleElement element = BattleElement.None, SkillTarget target = SkillTarget.Enemy,
            int memoryCost = 0, int memoryRecovery = 0, int memorySteal = 0,
            double accuracy = 1, double? criticalChance = null, int rageGain = 0, int inflictedSkippedTurns = 0,
            double bonusCriticalChance = 0, string englishName = null, int rageCost = 0, bool isAreaOfEffect = false)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("스킬 ID와 표시 이름이 필요합니다.");
            }
            if (memoryCost < 0 || memoryRecovery < 0 || memorySteal < 0 || power < 0 || !Enum.IsDefined(typeof(BattleElement), element)
                || !Enum.IsDefined(typeof(SkillTarget), target))
            {
                throw new ArgumentOutOfRangeException(nameof(power), "스킬 수치 또는 분류가 잘못되었습니다.");
            }

            if (double.IsNaN(accuracy) || accuracy < 0 || accuracy > 1
                || (criticalChance.HasValue && (double.IsNaN(criticalChance.Value) || criticalChance < 0 || criticalChance > 1))
                || rageGain < 0 || rageGain > 100 || rageCost < 0 || rageCost > 100
                || (isAreaOfEffect && target != SkillTarget.Enemy) || inflictedSkippedTurns < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(accuracy));
            }
            if (double.IsNaN(bonusCriticalChance) || bonusCriticalChance < 0 || bonusCriticalChance > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(bonusCriticalChance));
            }
            BonusCriticalChance = bonusCriticalChance;
            EnglishName = englishName ?? displayName;
            Accuracy = accuracy;
            CriticalChance = criticalChance;
            RageGain = rageGain;
            RageCost = rageCost;
            IsAreaOfEffect = isAreaOfEffect;
            InflictedSkippedTurns = inflictedSkippedTurns;
            Id = id;
            DisplayName = displayName;
            Power = power;
            MemoryCost = memoryCost;
            MemoryRecovery = memoryRecovery;
            MemorySteal = memorySteal;
            Element = element;
            Target = target;
        }

        public SkillData WithPower(int power)
        {
            return new SkillData(Id, DisplayName, power, Element, Target,
                MemoryCost, MemoryRecovery, MemorySteal, Accuracy, CriticalChance,
                RageGain, InflictedSkippedTurns, BonusCriticalChance, EnglishName, RageCost, IsAreaOfEffect);
        }
    }
}

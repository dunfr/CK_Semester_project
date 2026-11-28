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
        // 플레이어 기본 스킬은 각성 전까지 속성 효과가 없다. 몬스터·특수·궁극기는 항상 true.
        public bool HasElementEffect { get; }
        // 각성 스킬은 BattleCombatRules의 각성 효과 값을 쓴다.
        public bool IsAwakened { get; }

        public SkillData(string id, string displayName, int power,
            BattleElement element = BattleElement.None, SkillTarget target = SkillTarget.Enemy,
            int memoryCost = 0, int memoryRecovery = 0, int memorySteal = 0,
            double accuracy = 1, double? criticalChance = null, int rageGain = 0, int inflictedSkippedTurns = 0,
            double bonusCriticalChance = 0, string englishName = null, int rageCost = 0, bool isAreaOfEffect = false,
            bool hasElementEffect = true, bool isAwakened = false)
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
            HasElementEffect = hasElementEffect || isAwakened;
            IsAwakened = isAwakened;
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
            return Copy(power, HasElementEffect, IsAwakened);
        }

        public SkillData WithoutElementEffect()
        {
            return Copy(Power, false, false);
        }

        public SkillData AsAwakened()
        {
            return Copy(Power, true, true);
        }

        private SkillData Copy(int power, bool hasElementEffect, bool isAwakened)
        {
            return new SkillData(Id, DisplayName, power, Element, Target,
                MemoryCost, MemoryRecovery, MemorySteal, Accuracy, CriticalChance,
                RageGain, InflictedSkippedTurns, BonusCriticalChance, EnglishName, RageCost, IsAreaOfEffect,
                hasElementEffect, isAwakened);
        }
    }
}

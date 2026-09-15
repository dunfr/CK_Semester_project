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

        public SkillData(string id, string displayName, int power,
            BattleElement element = BattleElement.None, SkillTarget target = SkillTarget.Enemy)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("스킬 ID와 표시 이름이 필요합니다.");
            }
            if (power < 0 || !Enum.IsDefined(typeof(BattleElement), element)
                || !Enum.IsDefined(typeof(SkillTarget), target))
            {
                throw new ArgumentOutOfRangeException(nameof(power), "스킬 수치 또는 분류가 잘못되었습니다.");
            }

            Id = id;
            DisplayName = displayName;
            Power = power;
            Element = element;
            Target = target;
        }
    }
}

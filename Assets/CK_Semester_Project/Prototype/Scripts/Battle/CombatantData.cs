using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.SemesterProject.Battle
{
    // 캐릭터와 몬스터가 공유하는 정의. 같은 몬스터 정의로 여러 전투 개체를 만들 수 있다.
    public sealed class CombatantData
    {
        public string Id { get; }
        public string DisplayName { get; }
        public BattleTeam Team { get; }
        public BattleElement Element { get; }
        public int MaxHp { get; }
        public int MaxMemory { get; }
        public int InitialMemory { get; }
        public IReadOnlyList<SkillData> Skills { get; }
        public double Evasion { get; }
        public IReadOnlyList<BattleElement> WeaknessChain { get; }

        public CombatantData(string id, string displayName, BattleTeam team, int maxHp,
            int maxMemory, int initialMemory, IEnumerable<SkillData> skills,
            BattleElement element = BattleElement.None, double evasion = 0,
            IEnumerable<BattleElement> weaknessChain = null)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("개체 정의 ID와 표시 이름이 필요합니다.");
            }
            if (maxHp <= 0 || maxMemory < 0 || initialMemory < 0 || initialMemory > maxMemory)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHp), "HP 또는 메모리 범위가 잘못되었습니다.");
            }
            if (!Enum.IsDefined(typeof(BattleTeam), team) || !Enum.IsDefined(typeof(BattleElement), element))
            {
                throw new ArgumentException("알 수 없는 팀 또는 속성입니다.");
            }
            SkillData[] skillArray = (skills ?? throw new ArgumentNullException(nameof(skills))).ToArray();
            if (skillArray.Any(skill => skill == null)
                || skillArray.Select(skill => skill.Id).Distinct(StringComparer.Ordinal).Count() != skillArray.Length)
            {
                throw new ArgumentException("스킬이 비어 있거나 ID가 중복됩니다.", nameof(skills));
            }

            BattleElement[] chain = (weaknessChain ?? Array.Empty<BattleElement>()).ToArray();
            if (double.IsNaN(evasion) || evasion < 0 || evasion > 1
                || (chain.Length != 0 && chain.Length != 4)
                || chain.Any(value => value == BattleElement.None || !Enum.IsDefined(typeof(BattleElement), value)))
            {
                throw new ArgumentOutOfRangeException(nameof(evasion));
            }
            Evasion = evasion;
            WeaknessChain = Array.AsReadOnly(chain);
            Id = id;
            DisplayName = displayName;
            Team = team;
            Element = element;
            MaxHp = maxHp;
            MaxMemory = maxMemory;
            InitialMemory = initialMemory;
            Skills = Array.AsReadOnly(skillArray);
        }
    }
}

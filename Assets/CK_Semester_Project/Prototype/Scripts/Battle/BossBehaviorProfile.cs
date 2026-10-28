using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.SemesterProject.Battle
{
    public sealed class BossBehaviorProfile
    {
        public sealed class SkillRule
        {
            public int MemoryBand { get; }
            public int BossHpBand { get; }
            public int PlayerHpBand { get; }
            public int SkillNumber { get; }

            public SkillRule(int memoryBand, int bossHpBand, int playerHpBand, int skillNumber)
            {
                if (memoryBand < 0 || memoryBand > 3 || bossHpBand < 0 || bossHpBand > 3
                    || playerHpBand < 0 || playerHpBand > 3 || skillNumber < 1 || skillNumber > 2)
                {
                    throw new ArgumentOutOfRangeException(nameof(memoryBand));
                }
                MemoryBand = memoryBand;
                BossHpBand = bossHpBand;
                PlayerHpBand = playerHpBand;
                SkillNumber = skillNumber;
            }
        }

        public string SkillOneId { get; }
        public string SkillTwoId { get; }
        public IReadOnlyList<SkillRule> Rules { get; }

        public BossBehaviorProfile(string skillOneId, string skillTwoId, IEnumerable<SkillRule> rules = null)
        {
            if (string.IsNullOrWhiteSpace(skillOneId) || string.IsNullOrWhiteSpace(skillTwoId) || skillOneId == skillTwoId)
            {
                throw new ArgumentException("보스의 서로 다른 스킬 ID 2개가 필요합니다.");
            }
            SkillRule[] entries = (rules ?? CreateDefaultRules()).ToArray();
            if (entries.Length == 0 || entries.Any(rule => rule == null))
            {
                throw new ArgumentException("보스 스킬 선택표가 비어 있습니다.");
            }
            SkillOneId = skillOneId;
            SkillTwoId = skillTwoId;
            Rules = Array.AsReadOnly(entries);
        }

        public string ChooseSkill(int memory, int initialMemory, int bossHp, int maxBossHp, int playerHp, int maxPlayerHp)
        {
            int memoryBand = GetBand(memory, initialMemory);
            int hpBand = GetBand(bossHp, maxBossHp);
            int playerBand = GetBand(playerHp, maxPlayerHp);
            SkillRule selected = Rules.OrderBy(rule => Math.Abs(rule.MemoryBand - memoryBand)
                + Math.Abs(rule.BossHpBand - hpBand) + Math.Abs(rule.PlayerHpBand - playerBand)).First();
            return selected.SkillNumber == 1 ? SkillOneId : SkillTwoId;
        }

        public static int GetBand(int current, int maximum)
        {
            if (maximum <= 0 || current < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximum));
            }
            return (long)current * 100 >= (long)maximum * 75 ? 0
                : (long)current * 100 >= (long)maximum * 50 ? 1
                : (long)current * 100 >= (long)maximum * 25 ? 2 : 3;
        }

        public static SkillRule[] CreateDefaultRules()
        {
            int[][] skills =
            {
                new[] { 1, 1, 2, 2 }, new[] { 2, 1, 1, 2 }, new[] { 2, 2, 1, 2 },
                new[] { 2, 2, 1, 1 }, new[] { 2, 2, 1, 1 }, new[] { 2, 1, 1, 2 }, new[] { 2, 2, 1, 2 }
            };
            var result = new List<SkillRule>();
            for (int table = 0; table < skills.Length; table++)
            {
                for (int band = 0; band < 4; band++)
                {
                    bool reverseMemory = table == 1 || table == 4 || table == 5;
                    bool reverseBoss = table == 2 || table == 4 || table == 6;
                    bool reversePlayer = table == 3 || table == 5 || table == 6;
                    result.Add(new SkillRule(reverseMemory ? 3 - band : band, reverseBoss ? 3 - band : band,
                        reversePlayer ? 3 - band : band, skills[table][band]));
                }
            }
            return result.ToArray();
        }
    }
}

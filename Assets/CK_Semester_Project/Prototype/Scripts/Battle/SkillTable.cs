using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace CK.SemesterProject.Battle
{
    public static class SkillTable
    {
        private const string Header = "skill_id,skill_name,english_name,skill_damage,skill_element,bonus_critical_chance,skill_overheat_gain,skill_memory_cost";

        // 현재 제공된 표의 쉼표·줄바꿈 없는 셀만 지원한다. 복잡한 CSV는 조용히 오독하지 않고 거절한다.
        public static IReadOnlyList<SkillData> LoadCsv(string csv)
        {
            if (csv == null)
            {
                throw new ArgumentNullException(nameof(csv));
            }
            var skills = new List<SkillData>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            using (var reader = new StringReader(csv.TrimStart('\uFEFF')))
            {
                if (reader.ReadLine() != Header)
                {
                    throw new FormatException("Skill_DT.csv: 헤더가 스키마와 다릅니다.");
                }
                string line;
                int row = 1;
                while ((line = reader.ReadLine()) != null)
                {
                    row++;
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }
                    string[] fields = line.Split(',');
                    if (fields.Length != 8 || line.Contains("\"") || !ids.Add(fields[0]))
                    {
                        throw new FormatException("Skill_DT.csv: 행 " + row + " 열 개수·따옴표·중복 ID 오류");
                    }
                    try
                    {
                        if (!Enum.TryParse(fields[4], false, out BattleElement element)
                            || !Enum.IsDefined(typeof(BattleElement), element))
                        {
                            throw new FormatException("알 수 없는 속성");
                        }
                        skills.Add(new SkillData(fields[0], fields[1], int.Parse(fields[3], CultureInfo.InvariantCulture),
                            element, memoryCost: int.Parse(fields[7], CultureInfo.InvariantCulture),
                            rageGain: int.Parse(fields[6], CultureInfo.InvariantCulture),
                            bonusCriticalChance: double.Parse(fields[5], CultureInfo.InvariantCulture) / 100,
                            englishName: fields[2]));
                    }
                    catch (Exception error) when (error is ArgumentException || error is FormatException || error is OverflowException)
                    {
                        throw new FormatException("Skill_DT.csv: 행 " + row + " / ID " + fields[0] + " 데이터 오류", error);
                    }
                }
            }
            if (skills.Count == 0)
            {
                throw new FormatException("Skill_DT.csv: 스킬 데이터가 없습니다.");
            }
            return skills.AsReadOnly();
        }
    }
}

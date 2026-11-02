using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CK.SemesterProject.Tutorial
{
    // Tutorial_DT 한 행. 기획 xlsx(Sheet2)와 같은 열 순서를 쓴다.
    // tutorial_id, tutorial_condition, unlocked_feature, tutorial_text_1 ~ tutorial_text_5
    public sealed class BattleTutorialEntry
    {
        public BattleTutorialEntry(string id, int condition, string[] features, string[] texts)
        {
            Id = id;
            Condition = condition;
            Features = features;
            Texts = texts;
        }

        public string Id { get; }
        // 0 = 최초 전투, 1 = 두 번째 전투, 2 = 세 번째 전투
        public int Condition { get; }
        // MI 몬스터 상세보기 · WC 약점 연쇄 · ML 메모리 로스 · OH 폭주 게이지 · SA 특수공격
        public string[] Features { get; }
        // 비어 있는 텍스트 칸은 제외한 페이지 목록
        public string[] Texts { get; }

        public bool HasFeature(string code)
        {
            return Array.IndexOf(Features, code) >= 0;
        }
    }

    public static class BattleTutorialTable
    {
        // xlsx 그대로 내보낸 CSV를 읽는다. 머리글 3줄(한글명, 필드명, 타입)은 ID가 TT로 시작하지 않으므로 건너뛴다.
        public static List<BattleTutorialEntry> Parse(string csv)
        {
            var entries = new List<BattleTutorialEntry>();
            if (string.IsNullOrEmpty(csv))
            {
                return entries;
            }
            string[] lines = csv.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (string line in lines)
            {
                List<string> cells = SplitLine(line.TrimStart('﻿'));
                if (cells.Count < 4 || !cells[0].Trim().StartsWith("TT", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                float condition;
                float.TryParse(cells[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out condition);
                var features = new List<string>();
                foreach (string feature in cells[2].Split(','))
                {
                    if (feature.Trim().Length > 0)
                    {
                        features.Add(feature.Trim().ToUpperInvariant());
                    }
                }
                var texts = new List<string>();
                for (int index = 3; index < cells.Count && index < 8; index++)
                {
                    if (cells[index].Trim().Length > 0)
                    {
                        texts.Add(cells[index].Trim());
                    }
                }
                entries.Add(new BattleTutorialEntry(cells[0].Trim().ToUpperInvariant(),
                    (int)Math.Round(condition), features.ToArray(), texts.ToArray()));
            }
            return entries;
        }

        private static List<string> SplitLine(string line)
        {
            var cells = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;
            for (int index = 0; index < line.Length; index++)
            {
                char c = line[index];
                if (quoted)
                {
                    if (c == '"' && index + 1 < line.Length && line[index + 1] == '"')
                    {
                        cell.Append('"');
                        index++;
                    }
                    else if (c == '"')
                    {
                        quoted = false;
                    }
                    else
                    {
                        cell.Append(c);
                    }
                }
                else if (c == '"')
                {
                    quoted = true;
                }
                else if (c == ',')
                {
                    cells.Add(cell.ToString());
                    cell.Length = 0;
                }
                else
                {
                    cell.Append(c);
                }
            }
            cells.Add(cell.ToString());
            return cells;
        }
    }
}

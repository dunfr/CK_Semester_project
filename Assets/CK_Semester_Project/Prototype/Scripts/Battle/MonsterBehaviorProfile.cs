using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.SemesterProject.Battle
{
    // 기획서의 1기/2기 행동표. 정의는 불변이며 Inspector 에셋에서 복사해 생성한다.
    public sealed class MonsterBehaviorProfile
    {
        public BattleElement Element { get; }
        public IReadOnlyList<int> CostPercent { get; }
        public IReadOnlyList<double> Multipliers { get; }
        public IReadOnlyList<int> PlayerThresholds { get; }
        public IReadOnlyList<int> SelfThresholds { get; }
        public MonsterTacticsTable Solo { get; }
        public MonsterTacticsTable WithImprint { get; }
        public MonsterTacticsTable WithAfterimage { get; }
        public MonsterTacticsTable WithOblivion { get; }
        public int MaxStage => CostPercent.Count - 1;

        public MonsterBehaviorProfile(BattleElement element, IEnumerable<int> costs, IEnumerable<double> multipliers,
            IEnumerable<int> playerThresholds, IEnumerable<int> selfThresholds, MonsterTacticsTable solo,
            MonsterTacticsTable withImprint, MonsterTacticsTable withAfterimage, MonsterTacticsTable withOblivion)
        {
            int[] percentages = costs.ToArray();
            double[] damage = multipliers.ToArray();
            int[] player = playerThresholds.ToArray();
            int[] self = selfThresholds.ToArray();
            int bands = element == BattleElement.Afterimage ? 6 : 5;
            if (element == BattleElement.None || !Enum.IsDefined(typeof(BattleElement), element)
                || percentages.Length < 2 || percentages.Length > 8 || percentages.Length != damage.Length
                || percentages[0] != 0 || damage[0] != 1 || percentages.Any(value => value < 0 || value > 100)
                || damage.Any(value => double.IsNaN(value) || double.IsInfinity(value) || value < 1)
                || !ValidThresholds(player, bands) || !ValidThresholds(self, bands))
            {
                throw new ArgumentException("몬스터 투자표/조건표가 잘못되었습니다.");
            }
            foreach (MonsterTacticsTable table in new[] { solo, withImprint, withAfterimage, withOblivion })
            {
                if (table == null || table.Attack.Count != bands
                    || new[] { table.Attack, table.RageThree, table.RageFour, table.ChainOne, table.ChainTwo }
                        .Any(rows => rows.Any(range => range.Max >= percentages.Length)))
                {
                    throw new ArgumentException("몬스터 행동표가 투자 단계와 일치하지 않습니다.");
                }
            }
            Element = element;
            CostPercent = Array.AsReadOnly(percentages);
            Multipliers = Array.AsReadOnly(damage);
            PlayerThresholds = Array.AsReadOnly(player);
            SelfThresholds = Array.AsReadOnly(self);
            Solo = solo;
            WithImprint = withImprint;
            WithAfterimage = withAfterimage;
            WithOblivion = withOblivion;
        }

        private static bool ValidThresholds(int[] values, int bands)
        {
            if (values.Length != bands - 1 || values.Any(value => value < 0 || value > 100))
            {
                return false;
            }
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] >= values[i - 1])
                {
                    return false;
                }
            }
            return true;
        }

        public bool AllowsStage(int stage)
        {
            return stage >= 0 && stage <= MaxStage && !(Element == BattleElement.Imprint && stage == 1);
        }

        public MonsterTacticsTable ForPartner(BattleElement partner)
        {
            switch (partner)
            {
                case BattleElement.Imprint: return WithImprint;
                case BattleElement.Afterimage: return WithAfterimage;
                case BattleElement.Oblivion: return WithOblivion;
                default: return Solo;
            }
        }

        public static MonsterBehaviorProfile CreateDefault(BattleElement element)
        {
            MonsterTacticsTable solo;
            MonsterTacticsTable imprint;
            MonsterTacticsTable afterimage;
            MonsterTacticsTable oblivion;
            int[] costs;
            double[] multipliers;
            int[] player;
            int[] self;
            if (element == BattleElement.Imprint)
            {
                costs = new[] { 0, 10, 20, 30, 40, 50, 60, 70 };
                multipliers = new[] { 1.0, 1.1, 1.2, 1.35, 1.5, 1.7, 1.75, 1.8 };
                player = new[] { 90, 75, 50, 30 };
                self = new[] { 90, 70, 50, 35 };
                solo = new MonsterTacticsTable(Rows("5-7,4-7,3-6,2-5,6-7"));
                imprint = afterimage = solo;
                oblivion = new MonsterTacticsTable(Rows("6-7,5-7,4-6,2-6,6-7"));
            }
            else if (element == BattleElement.Afterimage)
            {
                costs = new[] { 0, 5, 10, 15, 20, 30 };
                multipliers = new[] { 1.0, 1.05, 1.1, 1.2, 1.25, 1.4 };
                player = new[] { 85, 70, 50, 35, 15 };
                self = new[] { 90, 70, 50, 35, 20 };
                solo = new MonsterTacticsTable(Rows("1-5,1-2,2-4,2-3,1-4,1"));
                afterimage = solo;
                imprint = new MonsterTacticsTable(Rows("1-2,1-2,1-3,1-2,1-4,1"));
                oblivion = new MonsterTacticsTable(Rows("1-5,1-4,1-3,1-2,1-2,1"));
            }
            else if (element == BattleElement.Oblivion)
            {
                costs = new[] { 0, 10, 20, 30, 40 };
                multipliers = new[] { 1.0, 1.1, 1.2, 1.35, 1.5 };
                player = new[] { 85, 70, 50, 35 };
                self = new[] { 90, 70, 50, 35 };
                MonsterInvestmentRange[] attack = Rows("1-3,1-2,2-4,2-3,1-2");
                solo = new MonsterTacticsTable(attack, Rows("2-3,2-3,1-3,1-2,1"), Rows("3-4,2-4,2-3,2-3,2"),
                    Rows("2-4,2-3,n,n,n"), Rows("2-4,2-3,2-3,2,n"));
                afterimage = new MonsterTacticsTable(attack, Rows("2-3,1-3,1-2,n,n"), Rows("2-4,1-4,1-2,1,n"),
                    Rows("2-4,2-3,1-2,n,n"), Rows("2-4,2-3,1-3,n,n"));
                imprint = new MonsterTacticsTable(attack, Rows("2-3,1-3/n,n,n,n"), Rows("3-4,2-4/n,n,n,n"),
                    Rows("2-4,2-3/n,n,n,n"), Rows("2-4,2-3,1-3/n,n,n"));
                oblivion = new MonsterTacticsTable(attack, Rows("2-3,1-3,1/n,n,n"), Rows("3-4,2-4,2/n,n,n"),
                    Rows("2-4,2-3/n,1-2/n,n,n"), Rows("2-4,2-3,1-3,1-2/n,n"));
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(element));
            }
            return new MonsterBehaviorProfile(element, costs, multipliers, player, self, solo, imprint, afterimage, oblivion);
        }

        private static MonsterInvestmentRange[] Rows(string specification)
        {
            return specification.Split(',').Select(value =>
            {
                if (value == "n")
                {
                    return new MonsterInvestmentRange(0, 0, 0);
                }
                bool chance = value.EndsWith("/n", StringComparison.Ordinal);
                string[] range = value.Replace("/n", "").Split('-');
                int min = int.Parse(range[0]);
                return new MonsterInvestmentRange(min, range.Length == 1 ? min : int.Parse(range[1]), chance ? 0.5 : 1);
            }).ToArray();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.SemesterProject.Battle
{
    public sealed class MonsterTacticsTable
    {
        public IReadOnlyList<MonsterInvestmentRange> Attack { get; }
        public IReadOnlyList<MonsterInvestmentRange> RageThree { get; }
        public IReadOnlyList<MonsterInvestmentRange> RageFour { get; }
        public IReadOnlyList<MonsterInvestmentRange> ChainOne { get; }
        public IReadOnlyList<MonsterInvestmentRange> ChainTwo { get; }

        public MonsterTacticsTable(IEnumerable<MonsterInvestmentRange> attack,
            IEnumerable<MonsterInvestmentRange> rageThree = null, IEnumerable<MonsterInvestmentRange> rageFour = null,
            IEnumerable<MonsterInvestmentRange> chainOne = null, IEnumerable<MonsterInvestmentRange> chainTwo = null)
        {
            Attack = Copy(attack, false);
            RageThree = Copy(rageThree, true);
            RageFour = Copy(rageFour, true);
            ChainOne = Copy(chainOne, true);
            ChainTwo = Copy(chainTwo, true);
        }

        private static IReadOnlyList<MonsterInvestmentRange> Copy(IEnumerable<MonsterInvestmentRange> values, bool optional)
        {
            MonsterInvestmentRange[] copy = values?.ToArray() ?? Array.Empty<MonsterInvestmentRange>();
            if ((!optional && copy.Length == 0) || (optional && copy.Length != 0 && copy.Length != 5)
                || copy.Any(value => value == null))
            {
                throw new ArgumentException("몬스터 행동표의 행 수 또는 범위가 잘못되었습니다.");
            }
            return Array.AsReadOnly(copy);
        }
    }
}

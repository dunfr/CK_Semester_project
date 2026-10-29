using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.SemesterProject.Battle
{
    public sealed class BattleRules
    {
        public IReadOnlyList<double> InvestmentMultipliers { get; }
        public int DefenseRageReductionPerMemory { get; }
        public double DefenseDamageMultiplier { get; }
        public BattleCombatRules Mechanics { get; }
        public bool UsesInvestmentStages { get; }
        public bool EnableMemoryLoss { get; }
        public int MonsterCollisionMemoryBonus { get; }

        // 기본값은 초기 메모리 기준 5단계다. 명시적인 배열은 이전 수량별 표 호환용이다.
        public BattleRules(IEnumerable<double> investmentMultipliers = null,
            int defenseRageReductionPerMemory = 0, double defenseDamageMultiplier = 0.5,
            BattleCombatRules mechanics = null, bool? enableMemoryLoss = null, int monsterCollisionMemoryBonus = 0)
        {
            if (monsterCollisionMemoryBonus < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(monsterCollisionMemoryBonus));
            }
            MonsterCollisionMemoryBonus = monsterCollisionMemoryBonus;
            UsesInvestmentStages = investmentMultipliers == null;
            EnableMemoryLoss = enableMemoryLoss ?? UsesInvestmentStages;
            double[] values = (investmentMultipliers ?? new[] { 1.0, 1.1, 1.2, 1.35, 1.5, 1.7 }).ToArray();
            if (values.Length == 0 || values[0] != 1.0
                || values.Any(value => double.IsNaN(value) || double.IsInfinity(value) || value < 1)
                || defenseRageReductionPerMemory < 0 || double.IsNaN(defenseDamageMultiplier)
                || defenseDamageMultiplier < 0 || defenseDamageMultiplier > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(investmentMultipliers));
            }
            InvestmentMultipliers = Array.AsReadOnly(values);
            Mechanics = mechanics ?? new BattleCombatRules();
            DefenseRageReductionPerMemory = defenseRageReductionPerMemory;
            DefenseDamageMultiplier = defenseDamageMultiplier;
        }

        // 방어 투자 전의 현재 메모리를 비교한다. 죽은 플레이어는 조건에서 제외한다.
        public bool CanMonsterDefend(CombatantState actor, IEnumerable<CombatantState> combatants)
        {
            if (actor == null || !actor.CanAct || actor.Data.Team != BattleTeam.Monster
                || actor.Data.MonsterProfile?.Element == BattleElement.Imprint)
            {
                return false;
            }
            bool hasPlayer = false;
            foreach (CombatantState player in combatants)
            {
                if (player.IsDead || player.Data.Team != BattleTeam.Player)
                {
                    continue;
                }
                hasPlayer = true;
                if (actor.Memory <= player.Memory || player.IsOverheated)
                {
                    return false;
                }
            }
            return hasPlayer;
        }

        public int GetStageCost(CombatantData actor, int stage)
        {
            if (actor == null || stage < 0 || stage > (actor.MonsterProfile?.MaxStage ?? 5))
            {
                throw new ArgumentOutOfRangeException(nameof(stage));
            }
            int percent = actor.MonsterProfile == null ? stage * 10 : actor.MonsterProfile.CostPercent[stage];
            return (int)((long)actor.InitialMemory * percent / 100);
        }

        public bool TryGetInvestment(CombatantData actor, BattleActionRequest request,
            out int cost, out double multiplier, out int rageReduction, BattleSnapshot snapshot = null)
        {
            cost = 0;
            multiplier = 1;
            rageReduction = 0;
            if (UsesInvestmentStages)
            {
                int stage = request.InvestmentStage ?? 0;
                if (request.MemoryInvestment != 0 || stage < 0 || stage > (actor.MonsterProfile?.MaxStage ?? 5)
                    || (actor.MonsterProfile != null && !actor.MonsterProfile.AllowsStage(stage)
                        && !(stage == 1 && snapshot != null && MonsterTrioAi.IsWaitingImprint(snapshot, request.ActorId))))
                {
                    return false;
                }
                cost = GetStageCost(actor, stage);
                if (stage > 0 && cost == 0)
                {
                    return false;
                }
                multiplier = actor.MonsterProfile == null ? InvestmentMultipliers[stage] : actor.MonsterProfile.Multipliers[stage];
                if (snapshot != null && MonsterTrioAi.UsesSurvivorDefense(snapshot, request))
                {
                    if (stage > 4)
                    {
                        return false;
                    }
                    cost = (int)((long)actor.InitialMemory * stage * 10 / 100);
                    multiplier = new[] { 1.0, 1.1, 1.2, 1.35, 1.5 }[stage];
                }
                rageReduction = stage == 0 ? 0 : 5 * stage + 5;
                return true;
            }
            // 명시적인 수량별 표는 기존 데모와 회귀 테스트 호환용이다.
            if (request.InvestmentStage.HasValue || request.MemoryInvestment < 0
                || (request.Kind == BattleActionKind.Skill && request.MemoryInvestment >= InvestmentMultipliers.Count))
            {
                return false;
            }
            cost = request.MemoryInvestment;
            multiplier = request.Kind == BattleActionKind.Skill ? InvestmentMultipliers[cost] : 1;
            rageReduction = (int)Math.Min(int.MaxValue, (long)cost * DefenseRageReductionPerMemory);
            return true;
        }
    }
}

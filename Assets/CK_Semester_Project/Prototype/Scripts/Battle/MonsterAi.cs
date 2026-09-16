using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.SemesterProject.Battle
{
    public sealed class MonsterAi
    {
        private readonly MonsterAiSettings _settings;

        public MonsterAi(MonsterAiSettings settings = null)
        {
            _settings = settings ?? new MonsterAiSettings();
        }

        public bool TryChooseAction(BattleSession session, out BattleActionRequest request)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }
            request = null;
            BattleSnapshot snapshot = session.GetSnapshot();
            if (snapshot.Phase != BattlePhase.AwaitingAction)
            {
                return false;
            }
            CombatantState actor = snapshot.Combatants.First(state => state.InstanceId == snapshot.CurrentActorId);
            if (actor.Data.Team != BattleTeam.Monster || !actor.CanAct)
            {
                return false;
            }

            // 실행기를 호출하지 않고 현재 공통 계산식으로만 예측한다. 난수·상태를 소비하지 않는다.
            var estimator = new BattleActionResolver(session.Rules);
            double bestScore = 0;
            long bestCost = long.MaxValue;
            int maxInvestment = Math.Min(_settings.MaxMemoryInvestment, session.Rules.InvestmentMultipliers.Count - 1);
            foreach (SkillData skill in actor.Data.Skills)
            {
                int affordableInvestment = Math.Min(maxInvestment, actor.Memory - skill.MemoryCost);
                foreach (string targetId in session.GetSelectableTargets(skill.Id))
                {
                    for (int investment = 0; investment <= affordableInvestment; investment++)
                    {
                        var candidate = new BattleActionRequest(snapshot.TurnId, actor.InstanceId,
                            BattleActionKind.Skill, skill.Id, targetId, investment);
                        if (session.ValidateRequest(candidate) != BattleActionError.None
                            || estimator.Resolve(snapshot, candidate, out IReadOnlyList<BattleEffect> effects) != BattleActionError.None)
                        {
                            continue;
                        }
                        double score = Evaluate(snapshot, actor.Data.Team, effects);
                        long cost = (long)skill.MemoryCost + investment;
                        // 동점이면 적은 비용, 이후 데이터에 정의된 스킬·대상 순서를 유지한다.
                        if (score > bestScore || (score == bestScore && score > 0 && cost < bestCost))
                        {
                            request = candidate;
                            bestScore = score;
                            bestCost = cost;
                        }
                    }
                }
            }
            if (request != null)
            {
                return true;
            }

            // 유효한 이득이 없거나 비용이 부족해도 턴 진행이 멈추지 않게 한다.
            request = new BattleActionRequest(snapshot.TurnId, actor.InstanceId, BattleActionKind.Defend);
            return true;
        }

        private double Evaluate(BattleSnapshot snapshot, BattleTeam team, IReadOnlyList<BattleEffect> effects)
        {
            double score = 0;
            foreach (BattleEffect effect in effects)
            {
                CombatantState target = snapshot.Combatants.First(state => state.InstanceId == effect.TargetId);
                int hpAfter = (int)Math.Max(0L, Math.Min(target.Data.MaxHp, (long)target.Hp + effect.HpDelta));
                int memoryAfter = (int)Math.Max(0L, Math.Min(target.Data.MaxMemory, (long)target.Memory + effect.MemoryDelta));
                int sign = target.Data.Team == team ? 1 : -1;
                score += sign * ((double)hpAfter - target.Hp + ((double)memoryAfter - target.Memory) * _settings.MemoryWeight);
                if (hpAfter == 0 && !target.IsDead)
                {
                    score -= sign * _settings.KillBonus;
                }
            }
            return score;
        }
    }
}

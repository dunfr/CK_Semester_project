using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.SemesterProject.Battle
{
    public sealed class MonsterAi
    {
        private readonly MonsterAiSettings _settings;
        private readonly int _randomSeed;

        public MonsterAi(MonsterAiSettings settings = null, int randomSeed = 0)
        {
            _settings = settings ?? new MonsterAiSettings();
            _randomSeed = randomSeed;
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
            int? selectedInvestment = null;
            if (actor.Data.MinMemoryInvestment.HasValue)
            {
                var choices = new List<int>();
                for (int level = 0; level <= maxInvestment; level++)
                {
                    int amount = session.Rules.UsesInvestmentStages ? session.Rules.GetStageCost(actor.Data, level) : level;
                    if ((level > 0 && amount == 0) || amount < actor.Data.MinMemoryInvestment.Value
                        || amount > actor.Data.MaxMemoryInvestment.Value)
                    {
                        continue;
                    }
                    if (actor.Data.Skills.Any(skill => (long)skill.MemoryCost + amount <= actor.Memory
                        && session.GetSelectableTargets(skill.Id).Count > 0))
                    {
                        choices.Add(level);
                    }
                }
                if (choices.Count == 0)
                {
                    request = new BattleActionRequest(snapshot.TurnId, actor.InstanceId, BattleActionKind.Defend);
                    return true;
                }
                // 같은 턴의 재조회는 같은 선택을 반환하고 전투 판정 난수와 분리한다.
                int hash = _randomSeed;
                unchecked
                {
                    foreach (char character in actor.InstanceId)
                    {
                        hash = hash * 31 + character;
                    }
                    hash = hash * 31 + snapshot.TurnId.GetHashCode();
                }
                selectedInvestment = choices[new Random(hash).Next(choices.Count)];
            }
            foreach (SkillData skill in actor.Data.Skills)
            {
                int affordableInvestment = session.Rules.UsesInvestmentStages ? maxInvestment
                    : Math.Min(maxInvestment, actor.Memory - skill.MemoryCost);
                foreach (string targetId in session.GetSelectableTargets(skill.Id))
                {
                    for (int investment = 0; investment <= affordableInvestment; investment++)
                    {
                        if (selectedInvestment.HasValue && selectedInvestment.Value != investment)
                        {
                            continue;
                        }
                        var candidate = new BattleActionRequest(snapshot.TurnId, actor.InstanceId,
                            BattleActionKind.Skill, skill.Id, targetId,
                            session.Rules.UsesInvestmentStages ? 0 : investment,
                            session.Rules.UsesInvestmentStages ? investment : (int?)null);
                        if (session.ValidateRequest(candidate) != BattleActionError.None)
                        {
                            continue;
                        }
                        double score = EvaluateCandidate(snapshot, actor.Data.Team, skill, candidate, estimator);
                        session.Rules.TryGetInvestment(actor.Data, candidate, out int investmentCost, out _, out _);
                        long cost = (long)skill.MemoryCost + investmentCost;
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

        private double EvaluateCandidate(BattleSnapshot snapshot, BattleTeam team, SkillData skill,
            BattleActionRequest request, BattleActionResolver estimator)
        {
            CombatantState target = snapshot.Combatants.First(state => state.InstanceId == request.TargetId);
            double hitChance = BattleActionResolver.GetHitChance(target, skill);
            double criticalChance = skill.Target == SkillTarget.Enemy && skill.Power > 0
                ? estimator.GetCriticalChance(snapshot.Combatants.First(state => state.InstanceId == request.ActorId), skill) : 0;
            double score = 0;
            for (int branch = 0; branch < 3; branch++)
            {
                bool isHit = branch != 0;
                bool isCritical = branch == 2;
                double probability = !isHit ? 1 - hitChance
                    : hitChance * (isCritical ? criticalChance : 1 - criticalChance);
                if (probability <= 0)
                {
                    continue;
                }
                if (estimator.ResolveOutcome(snapshot, request, isHit, isCritical,
                    out IReadOnlyList<BattleEffect> effects, out BattleHitResult hit) != BattleActionError.None)
                {
                    continue;
                }
                double value = Evaluate(snapshot, team, effects);
                // 추가 행동은 확정적인 기회만 평가하고 이후 행동까지 재귀 탐색하지 않는다.
                if (hit != null && hit.GrantsExtraAction)
                {
                    value += skill.Power;
                }
                score += probability * value;
            }
            return score;
        }

        private double Evaluate(BattleSnapshot snapshot, BattleTeam team, IReadOnlyList<BattleEffect> effects)
        {
            double score = 0;
            foreach (BattleEffect effect in effects)
            {
                CombatantState target = snapshot.Combatants.First(state => state.InstanceId == effect.TargetId);
                int hpAfter = (int)Math.Max(0L, Math.Min(target.Data.MaxHp, (long)target.Hp + effect.HpDelta));
                int memoryAfter = (int)Math.Max(0L, Math.Min(Math.Max(target.Memory, target.Data.MaxMemory), (long)target.Memory + effect.MemoryDelta));
                int sign = target.Data.Team == team ? 1 : -1;
                score += sign * ((double)hpAfter - target.Hp + ((double)memoryAfter - target.Memory) * _settings.MemoryWeight);
                if (hpAfter > 0 && effect.ImprintDamage.HasValue)
                {
                    score -= sign * (Math.Min(hpAfter, effect.ImprintDamage.Value)
                        - Math.Min(hpAfter, target.ImprintDamage));
                }
                if (hpAfter == 0 && !target.IsDead)
                {
                    score -= sign * _settings.KillBonus;
                }
            }
            return score;
        }
    }
}

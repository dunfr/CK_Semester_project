using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.SemesterProject.Battle
{
    // BattleSession이 검증한 요청만 계산하며 실제 상태 변경은 세션에 맡긴다.
    public sealed class BattleActionResolver : IBattleActionResolver
    {
        private readonly BattleRules _rules;
        public BattleRules Rules => _rules;

        public BattleActionResolver(BattleRules rules = null)
        {
            _rules = rules ?? new BattleRules();
        }

        public BattleActionError Resolve(BattleSnapshot snapshot, BattleActionRequest request,
            out IReadOnlyList<BattleEffect> effects)
        {
            effects = Array.Empty<BattleEffect>();
            CombatantState actor = snapshot.Combatants.First(state => state.InstanceId == request.ActorId);
            if (request.Kind == BattleActionKind.Wait)
            {
                return BattleActionError.None;
            }
            if (request.Kind == BattleActionKind.Defend)
            {
                int reduction = (int)Math.Min(actor.RageEnergy,
                    (long)request.MemoryInvestment * _rules.DefenseRageReductionPerMemory);
                effects = new[] { new BattleEffect(actor.InstanceId, memoryDelta: -request.MemoryInvestment,
                    isDefending: true, rageDelta: -reduction) };
                return BattleActionError.None;
            }
            SkillData skill = actor.Data.Skills.First(data => data.Id == request.SkillId);
            if (request.MemoryInvestment >= _rules.InvestmentMultipliers.Count)
            {
                return BattleActionError.InvalidMemoryInvestment;
            }
            long cost = (long)skill.MemoryCost + request.MemoryInvestment;
            if (cost > actor.Memory)
            {
                return BattleActionError.InsufficientMemory;
            }
            CombatantState target = snapshot.Combatants.First(state => state.InstanceId == request.TargetId);
            int remaining = actor.Memory - (int)cost;
            int recovered = (int)Math.Min(skill.MemoryRecovery, (long)actor.Data.MaxMemory - remaining);
            remaining += recovered;
            int stolen = actor.InstanceId == target.InstanceId ? 0
                : Math.Min(skill.MemorySteal, Math.Min(target.Memory, actor.Data.MaxMemory - remaining));
            int actorDelta = -(int)cost + recovered + stolen;
            double damage = skill.Power * _rules.InvestmentMultipliers[request.MemoryInvestment];
            if (target.IsDefending)
            {
                damage *= _rules.DefenseDamageMultiplier;
            }
            int roundedDamage = (int)Math.Min(int.MaxValue, Math.Round(damage, MidpointRounding.AwayFromZero));
            // 자기 자신을 대상으로 하는 스킬도 효과를 한 개로 합쳐 원자적으로 적용한다.
            if (actor.InstanceId == target.InstanceId)
            {
                effects = new[] { new BattleEffect(actor.InstanceId, -roundedDamage, actorDelta) };
            }
            else
            {
                var changes = new List<BattleEffect>();
                if (actorDelta != 0)
                {
                    changes.Add(new BattleEffect(actor.InstanceId, memoryDelta: actorDelta));
                }
                changes.Add(new BattleEffect(target.InstanceId, -roundedDamage, -stolen));
                effects = changes.AsReadOnly();
            }
            return BattleActionError.None;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.SemesterProject.Battle
{
    // 실행과 예측은 같은 계산식을 사용하되 예측은 난수를 소비하지 않는다.
    public sealed class BattleActionResolver : IBattleActionResolver
    {
        private readonly BattleRules _rules;
        private readonly Random _random;
        public BattleRules Rules => _rules;

        public BattleActionResolver(BattleRules rules = null, int? randomSeed = null)
        {
            _rules = rules ?? new BattleRules();
            _random = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();
        }

        public static double GetHitChance(CombatantState target, SkillData skill)
        {
            return skill.Target == SkillTarget.Enemy ? Math.Max(0, Math.Min(1, skill.Accuracy - target.Data.Evasion)) : 1;
        }

        public BattleActionError Resolve(BattleSnapshot snapshot, BattleActionRequest request,
            out IReadOnlyList<BattleEffect> effects)
        {
            return ResolveDetailed(snapshot, request, out effects, out _);
        }

        public BattleActionError ResolveDetailed(BattleSnapshot snapshot, BattleActionRequest request,
            out IReadOnlyList<BattleEffect> effects, out BattleHitResult hit)
        {
            bool isHit = true;
            bool isCritical = false;
            if (request.Kind == BattleActionKind.Skill)
            {
                CombatantState actor = snapshot.Combatants.First(state => state.InstanceId == request.ActorId);
                SkillData skill = actor.Data.Skills.First(data => data.Id == request.SkillId);
                CombatantState target = snapshot.Combatants.First(state => state.InstanceId == request.TargetId);
                isHit = Roll(GetHitChance(target, skill));
                isCritical = isHit && skill.Target == SkillTarget.Enemy && skill.Power > 0
                    && Roll(GetCriticalChance(actor, skill));
            }
            return ResolveOutcome(snapshot, request, isHit, isCritical, out effects, out hit);
        }

        public double GetCriticalChance(CombatantState actor, SkillData skill)
        {
            return skill.CriticalChance ?? Math.Min(1, (actor.Data.BaseCriticalChance
                ?? _rules.Mechanics.CriticalChance) + skill.BonusCriticalChance);
        }

        private bool Roll(double probability)
        {
            return probability >= 1 || (probability > 0 && _random.NextDouble() < probability);
        }

        // AI는 빗나감·일반 명중·치명타의 결과를 확률 가중해 평가한다.
        public BattleActionError ResolveOutcome(BattleSnapshot snapshot, BattleActionRequest request,
            bool isHit, bool isCritical, out IReadOnlyList<BattleEffect> effects, out BattleHitResult hit)
        {
            effects = Array.Empty<BattleEffect>();
            hit = null;
            CombatantState actor = snapshot.Combatants.First(state => state.InstanceId == request.ActorId);
            if (request.Kind == BattleActionKind.Wait)
            {
                return BattleActionError.None;
            }
            if (!_rules.TryGetInvestment(actor.Data, request, out int investmentCost,
                out double investmentMultiplier, out int defenseReduction))
            {
                return BattleActionError.InvalidMemoryInvestment;
            }
            if (investmentCost > actor.Memory)
            {
                return BattleActionError.InsufficientMemory;
            }
            if (request.Kind == BattleActionKind.Defend)
            {
                int reduction = Math.Min(actor.RageEnergy, defenseReduction);
                effects = new[] { new BattleEffect(actor.InstanceId, memoryDelta: -investmentCost,
                    isDefending: true, rageDelta: -reduction,
                    defenseDamageMultiplier: actor.Data.MonsterProfile?.Element == BattleElement.Afterimage
                        ? Math.Max(0, _rules.DefenseDamageMultiplier - 2 * (investmentMultiplier - 1))
                        : _rules.DefenseDamageMultiplier) };
                return BattleActionError.None;
            }
            SkillData skill = actor.Data.Skills.First(data => data.Id == request.SkillId);
            long cost = (long)skill.MemoryCost + investmentCost;
            if (cost > actor.Memory)
            {
                return BattleActionError.InsufficientMemory;
            }
            CombatantState target = snapshot.Combatants.First(state => state.InstanceId == request.TargetId);
            BattleCombatRules mechanics = _rules.Mechanics;
            bool hostile = actor.Data.Team != target.Data.Team;
            bool elementEffects = isHit && hostile && mechanics.EnableElementEffects;
            int chain = 0;
            bool hasChain = hostile && target.Data.Team == BattleTeam.Monster && target.Data.WeaknessChain.Count == 4;
            if (isHit && hasChain)
            {
                chain = skill.Element == target.Data.WeaknessChain[target.ChainStep]
                    ? target.ChainStep + 1 : skill.Element == target.Data.WeaknessChain[0] ? 1 : 0;
            }
            double element = chain > 0 ? mechanics.WeaknessMultiplier
                : BattleMath.ElementMultiplier(skill.Element, target.Data.Element, mechanics);
            double chainMultiplier = mechanics.ChainMultipliers[chain];
            double rage = mechanics.EnableRage ? BattleMath.RageMultiplier(actor.RageEnergy) : 1;
            double critical = isHit && isCritical ? actor.Data.CriticalDamageMultiplier ?? mechanics.CriticalMultiplier : 1;
            double investment = investmentMultiplier;
            double defense = target.IsDefending ? target.DefenseDamageMultiplier ?? _rules.DefenseDamageMultiplier : 1;
            double damage = skill.Power;
            // 0 배율과 큰 수의 곱도 NaN이 되지 않게 0 피해를 먼저 확정한다.
            int roundedDamage = !isHit || damage == 0 || element == 0 || chainMultiplier == 0 || defense == 0 ? 0
                : BattleMath.RoundDamage(damage * rage * element * chainMultiplier * critical * investment * defense);
            hit = new BattleHitResult(isHit, isHit && isCritical, GetHitChance(target, skill), skill.Power,
                roundedDamage, element, chainMultiplier, rage, critical, investment, defense, chain);

            int remaining = actor.Memory - (int)cost;
            long recovery = isHit ? skill.MemoryRecovery : 0;
            if (elementEffects && skill.Element == BattleElement.Afterimage)
            {
                recovery += mechanics.AfterimageRecovery;
            }
            int recovered = (int)Math.Min(recovery, Math.Max(0L, (long)actor.Data.MaxMemory - remaining));
            remaining += recovered;
            long steal = isHit ? skill.MemorySteal : 0;
            if (elementEffects && skill.Element == BattleElement.Oblivion)
            {
                steal += mechanics.OblivionSteal;
            }
            int stolen = actor.InstanceId == target.InstanceId ? 0
                : (int)Math.Min(steal, Math.Min(target.Memory, Math.Max(0, actor.Data.MaxMemory - remaining)));
            int actorDelta = -(int)cost + recovered + stolen;
            int rageGain = mechanics.EnableRage ? skill.RageGain : 0;
            int? imprint = elementEffects && skill.Element == BattleElement.Imprint
                ? BattleMath.RoundDamage(roundedDamage * mechanics.ImprintRatio) : (int?)null;
            int? skipped = isHit && skill.InflictedSkippedTurns > 0
                ? Math.Max(target.SkippedTurns, skill.InflictedSkippedTurns) : (int?)null;
            int? nextChain = hasChain ? (chain == 4 ? 0 : chain) : (int?)null;
            if (actor.InstanceId == target.InstanceId)
            {
                effects = new[] { new BattleEffect(actor.InstanceId, -roundedDamage, actorDelta,
                    skipped, rageDelta: rageGain, chainStep: nextChain, imprintDamage: imprint) };
            }
            else
            {
                var changes = new List<BattleEffect>();
                if (actorDelta != 0 || rageGain != 0)
                {
                    changes.Add(new BattleEffect(actor.InstanceId, memoryDelta: actorDelta, rageDelta: rageGain));
                }
                changes.Add(new BattleEffect(target.InstanceId, -roundedDamage, -stolen,
                    skipped, chainStep: nextChain, imprintDamage: imprint));
                effects = changes.AsReadOnly();
            }
            return BattleActionError.None;
        }
    }
}

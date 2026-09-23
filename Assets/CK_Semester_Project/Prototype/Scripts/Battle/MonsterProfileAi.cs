using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.SemesterProject.Battle
{
    public static class MonsterProfileAi
    {
        public static bool TryChooseAction(BattleSession session, BattleSnapshot snapshot, CombatantState actor,
            int seed, out BattleActionRequest request)
        {
            request = null;
            MonsterBehaviorProfile profile = actor.Data.MonsterProfile;
            CombatantState player = snapshot.Combatants.Where(unit => !unit.IsDead && unit.Data.Team == BattleTeam.Player)
                .OrderBy(unit => unit.Hp).FirstOrDefault();
            if (profile == null || player == null)
            {
                return false;
            }
            CombatantState[] alive = snapshot.Combatants.Where(unit => !unit.IsDead && unit.Data.Team == BattleTeam.Monster).ToArray();
            BattleElement partner = alive.Length == 2 ? alive.First(unit => unit.InstanceId != actor.InstanceId).Data.Element : BattleElement.None;
            MonsterTacticsTable table = profile.ForPartner(partner);
            var random = new Random(DecisionSeed(seed, actor.InstanceId, snapshot.TurnId));
            bool duel = snapshot.Combatants.Count(unit => unit.Data.Team == BattleTeam.Monster) == 1
                && snapshot.Combatants.Count(unit => unit.Data.Team == BattleTeam.Player) == 1;
            bool canDefend = !MonsterAi.IsPlayerOverheated(snapshot);
            int openingDefenseAction = session.EntryInitiatorId == actor.InstanceId ? 1 : 0;
            if (canDefend && profile.Element == BattleElement.Afterimage && duel
                && session.GetActionCount(actor.InstanceId) == openingDefenseAction)
            {
                if (TryDefend(session, snapshot, actor, new MonsterInvestmentRange(2, 2), random, out request))
                {
                    return true;
                }
            }
            if (canDefend && profile.Element == BattleElement.Oblivion)
            {
                int band = Band(actor.Memory, actor.Data.InitialMemory, profile.SelfThresholds);
                IReadOnlyList<MonsterInvestmentRange> defense = actor.ChainStep >= 2 ? table.ChainTwo
                    : actor.ChainStep == 1 ? table.ChainOne
                    : player.RageEnergy >= 75 && player.RageEnergy <= 90 ? table.RageFour
                    : player.RageEnergy >= 45 && player.RageEnergy < 75 ? table.RageThree : null;
                // 연쇄 표가 n이면 폭주 표로 돌아가지 않고 공격한다.
                if (defense != null && defense.Count > band
                    && TryDefend(session, snapshot, actor, defense[band], random, out request))
                {
                    return true;
                }
            }
            bool usesHp = profile.Element == BattleElement.Imprint;
            int playerBand = Band(usesHp ? player.Hp : player.Memory,
                usesHp ? player.Data.MaxHp : player.Data.InitialMemory, profile.PlayerThresholds);
            int selfBand = Band(usesHp ? actor.Hp : actor.Memory,
                usesHp ? actor.Data.MaxHp : actor.Data.InitialMemory, profile.SelfThresholds);
            int action = ResolveActionNumber(playerBand + 1, selfBand + 1, table.Attack.Count) - 1;
            for (int row = action; row >= 0; row--)
            {
                if (TryAttack(session, snapshot, actor, player, table.Attack[row], random, out request))
                {
                    return true;
                }
            }
            // 행동표 전체가 비싸면 남은 메모리로 가능한 낮은 단계부터 0단계까지 찾는다.
            for (int stage = Math.Min(table.Attack[action].Max, profile.MaxStage); stage >= 0; stage--)
            {
                if (TryAttack(session, snapshot, actor, player, new MonsterInvestmentRange(stage, stage), random, out request))
                {
                    return true;
                }
            }
            // 스킬 기본 비용조차 없으면 방어 금지 각인은 대기한다. 턴을 멈추지 않는다.
            request = new BattleActionRequest(snapshot.TurnId, actor.InstanceId,
                !canDefend || profile.Element == BattleElement.Imprint ? BattleActionKind.Wait : BattleActionKind.Defend);
            return session.ValidateRequest(request) == BattleActionError.None;
        }

        public static int ResolveActionNumber(int player, int self, int count)
        {
            if (count == 6 && (player == 6 || self == 6))
            {
                return 6;
            }
            if (player == 5 || self == 5)
            {
                return 5;
            }
            int difference = Math.Abs(player - self);
            return difference >= 2 ? Math.Min(player, self) + 1 : Math.Min(player, self);
        }

        private static int Band(int value, int initial, IReadOnlyList<int> thresholds)
        {
            double percentage = initial <= 0 ? 0 : 100.0 * value / initial;
            for (int i = 0; i < thresholds.Count; i++)
            {
                if (percentage >= thresholds[i])
                {
                    return i;
                }
            }
            return thresholds.Count;
        }

        private static int DecisionSeed(int seed, string actorId, long turn)
        {
            unchecked
            {
                foreach (char character in actorId)
                {
                    seed = seed * 31 + character;
                }
                return seed * 31 + turn.GetHashCode();
            }
        }

        private static bool TryDefend(BattleSession session, BattleSnapshot snapshot, CombatantState actor,
            MonsterInvestmentRange range, Random random, out BattleActionRequest request)
        {
            request = null;
            if (range.DefenseChance <= 0 || (range.DefenseChance < 1 && random.NextDouble() >= range.DefenseChance))
            {
                return false;
            }
            var choices = new List<BattleActionRequest>();
            for (int stage = range.Min; stage <= range.Max; stage++)
            {
                var candidate = new BattleActionRequest(snapshot.TurnId, actor.InstanceId, BattleActionKind.Defend,
                    investmentStage: stage);
                if (session.ValidateRequest(candidate) == BattleActionError.None)
                {
                    choices.Add(candidate);
                }
            }
            if (choices.Count == 0)
            {
                return false;
            }
            request = choices[random.Next(choices.Count)];
            return true;
        }

        private static bool TryAttack(BattleSession session, BattleSnapshot snapshot, CombatantState actor,
            CombatantState target, MonsterInvestmentRange range, Random random, out BattleActionRequest request)
        {
            request = null;
            var choices = new List<BattleActionRequest>();
            var estimator = new BattleActionResolver(session.Rules);
            for (int stage = range.Min; stage <= range.Max; stage++)
            {
                double bestScore = double.NegativeInfinity;
                BattleActionRequest best = null;
                foreach (SkillData skill in actor.Data.Skills)
                {
                    if (skill.Target != SkillTarget.Enemy)
                    {
                        continue;
                    }
                    var candidate = new BattleActionRequest(snapshot.TurnId, actor.InstanceId, BattleActionKind.Skill,
                        skill.Id, target.InstanceId, investmentStage: stage);
                    if (session.ValidateRequest(candidate) != BattleActionError.None)
                    {
                        continue;
                    }
                    estimator.ResolveOutcome(snapshot, candidate, true, false, out _, out BattleHitResult normal);
                    estimator.ResolveOutcome(snapshot, candidate, true, true, out _, out BattleHitResult critical);
                    double chance = estimator.GetCriticalChance(actor, skill);
                    double score = BattleActionResolver.GetHitChance(target, skill)
                        * (normal.Damage * (1 - chance) + critical.Damage * chance);
                    if (score > bestScore)
                    {
                        best = candidate;
                        bestScore = score;
                    }
                }
                if (best != null)
                {
                    choices.Add(best);
                }
            }
            if (choices.Count == 0)
            {
                return false;
            }
            request = choices[random.Next(choices.Count)];
            return true;
        }
    }
}

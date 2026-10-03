using System;
using System.Linq;

namespace CK.SemesterProject.Battle
{
    // 전투 진입 당시 조합을 유지한다. 사망 뒤에도 해당 조합의 생존/역할 전환 규칙을 적용한다.
    public static class MonsterTrioAi
    {
        private static CombatantState[] Monsters(BattleSnapshot snapshot)
        {
            return snapshot.Combatants.Where(unit => unit.Data.Team == BattleTeam.Monster).ToArray();
        }

        public static bool Applies(BattleSnapshot snapshot)
        {
            CombatantState[] monsters = Monsters(snapshot);
            return monsters.Length == 3 && monsters.All(unit => unit.Data.MonsterProfile != null);
        }

        public static bool IsWaitingImprint(BattleSnapshot snapshot, string actorId)
        {
            if (!Applies(snapshot))
            {
                return false;
            }
            CombatantState[] imprints = Monsters(snapshot).Where(unit => unit.Data.Element == BattleElement.Imprint).ToArray();
            if (imprints.Length < 2 || !imprints.Any(unit => unit.InstanceId == actorId))
            {
                return false;
            }
            CombatantState attacker = imprints.FirstOrDefault(unit => unit.InstanceId == snapshot.AttackImprintId);
            return actorId != snapshot.AttackImprintId && (imprints.Length == 3 || (attacker != null && !attacker.IsDead));
        }

        public static bool UsesSurvivorDefense(BattleSnapshot snapshot, BattleActionRequest request)
        {
            if (!Applies(snapshot) || request.Kind != BattleActionKind.Defend)
            {
                return false;
            }
            CombatantState[] monsters = Monsters(snapshot);
            return monsters.Count(unit => unit.Data.Element == BattleElement.Afterimage) == 2
                && monsters.Any(unit => unit.InstanceId == request.ActorId && unit.Data.Element == BattleElement.Afterimage)
                && monsters.All(unit => unit.IsDead || unit.Data.Element == BattleElement.Afterimage);
        }

        public static bool TryChooseAction(BattleSession session, BattleSnapshot snapshot, CombatantState actor,
            CombatantState player, int seed, out BattleActionRequest request)
        {
            request = null;
            CombatantState[] monsters = Monsters(snapshot);
            int imprints = monsters.Count(unit => unit.Data.Element == BattleElement.Imprint);
            int afterimages = monsters.Count(unit => unit.Data.Element == BattleElement.Afterimage);
            int oblivions = monsters.Count(unit => unit.Data.Element == BattleElement.Oblivion);
            MonsterBehaviorProfile profile = actor.Data.MonsterProfile;
            MonsterTrioRules rules = profile.Trio;
            var random = new Random(MonsterProfileAi.DecisionSeed(seed, actor.InstanceId, snapshot.TurnId));
            bool canDefend = !MonsterAi.IsPlayerOverheated(snapshot);
            bool wasHit = snapshot.DamagedByPlayer.Contains(actor.InstanceId);
            bool aloneWithAfterimages = monsters.All(unit => unit.IsDead || unit.Data.Element == BattleElement.Afterimage);

            if (actor.Data.Element == BattleElement.Imprint)
            {
                if (IsWaitingImprint(snapshot, actor.InstanceId))
                {
                    int playerBand = MonsterProfileAi.Band(player.Hp, player.Data.MaxHp, profile.PlayerThresholds);
                    int selfBand = MonsterProfileAi.Band(actor.Hp, actor.Data.MaxHp, profile.SelfThresholds);
                    int row = MonsterProfileAi.ResolveActionNumber(playerBand + 1, selfBand + 1, 5) - 1;
                    MonsterInvestmentRange range = rules.WaitingAttack[row];
                    return AttackDescending(session, snapshot, actor, player, random.Next(range.Min, range.Max + 1), random, out request);
                }
                if (imprints == 1 && (long)player.Hp * 100 <= (long)player.Data.MaxHp * rules.FinisherHpPercent)
                {
                    return AttackDescending(session, snapshot, actor, player, profile.MaxStage, random, out request);
                }
                return Standard(session, snapshot, actor, seed, profile.WithOblivion, false, out request);
            }
            if (actor.Data.Element == BattleElement.Oblivion)
            {
                if (oblivions == 3 && canDefend && player.RageEnergy >= 75 && player.RageEnergy <= 90
                    && MonsterProfileAi.TryDefend(session, snapshot, actor, new MonsterInvestmentRange(2, 3), random, out request))
                {
                    return true;
                }
                bool requiresHit = oblivions == 2 || afterimages == 2;
                MonsterTacticsTable table = oblivions == 3 ? profile.Solo
                    : oblivions == 2 || imprints == 2 ? profile.WithImprint : profile.WithAfterimage;
                return Standard(session, snapshot, actor, seed, table, canDefend && (!requiresHit || wasHit), out request);
            }
            if (afterimages == 3)
            {
                if (session.GetActionCount(actor.InstanceId) == 0)
                {
                    return AttackDescending(session, snapshot, actor, player, 0, random, out request);
                }
                if (canDefend && snapshot.LastAfterimageHitId == actor.InstanceId
                    && MonsterProfileAi.TryDefend(session, snapshot, actor, new MonsterInvestmentRange(2, 2), random, out request))
                {
                    return true;
                }
                return Standard(session, snapshot, actor, seed, profile.Solo, false, out request);
            }
            if (afterimages == 2)
            {
                if (canDefend && aloneWithAfterimages
                    && (long)actor.Hp * 100 <= (long)actor.Data.MaxHp * rules.SurvivorHpPercent)
                {
                    int playerBand = MonsterProfileAi.Band(player.Memory, player.Data.InitialMemory, profile.PlayerThresholds);
                    int hpBand = MonsterProfileAi.Band(actor.Hp, actor.Data.MaxHp, new[] { 65, 50, 40, 35, 25 });
                    int row = MonsterProfileAi.ResolveActionNumber(playerBand + 1, hpBand + 1, 6) - 1;
                    MonsterInvestmentRange range = rules.SurvivorDefense[row];
                    if (MonsterProfileAi.TryDefend(session, snapshot, actor,
                        new MonsterInvestmentRange(range.Min, range.Max, rules.SurvivorDefenseChance), random, out request))
                    {
                        return true;
                    }
                }
                return Standard(session, snapshot, actor, seed, profile.WithImprint, false, out request);
            }
            if (imprints == 2)
            {
                return Standard(session, snapshot, actor, seed, profile.WithImprint, false, out request);
            }
            if (canDefend && (aloneWithAfterimages || (oblivions == 2 && wasHit)))
            {
                int playerBand = MonsterProfileAi.Band(player.Memory, player.Data.InitialMemory, profile.PlayerThresholds);
                int hpBand = MonsterProfileAi.Band(actor.Hp, actor.Data.MaxHp, new[] { 90, 70, 50, 35, 20 });
                int reverseBand = 5 - playerBand;
                bool reverse = Math.Abs(reverseBand - hpBand) < Math.Abs(playerBand - hpBand);
                int row = MonsterProfileAi.ResolveActionNumber((reverse ? reverseBand : playerBand) + 1, hpBand + 1, 6) - 1;
                MonsterInvestmentRange range = (reverse ? rules.DefenseReverse : rules.DefenseForward)[row];
                if (MonsterProfileAi.TryDefend(session, snapshot, actor, range, random, out request))
                {
                    return true;
                }
            }
            double roll = random.NextDouble() * rules.AttackWeights.Sum(value => (double)value);
            int stage = roll < rules.AttackWeights[0] ? 1 : roll < rules.AttackWeights[0] + (double)rules.AttackWeights[1] ? 2 : 3;
            return AttackDescending(session, snapshot, actor, player, stage, random, out request);
        }

        private static bool Standard(BattleSession session, BattleSnapshot snapshot, CombatantState actor,
            int seed, MonsterTacticsTable table, bool canDefend, out BattleActionRequest request)
        {
            return MonsterProfileAi.TryChooseAction(session, snapshot, actor, seed, out request, table, canDefend, false);
        }

        private static bool AttackDescending(BattleSession session, BattleSnapshot snapshot, CombatantState actor,
            CombatantState player, int stage, Random random, out BattleActionRequest request)
        {
            for (int current = stage; current >= 0; current--)
            {
                if (MonsterProfileAi.TryAttack(session, snapshot, actor, player,
                    new MonsterInvestmentRange(current, current), random, out request))
                {
                    return true;
                }
            }
            request = new BattleActionRequest(snapshot.TurnId, actor.InstanceId, BattleActionKind.Wait);
            return session.ValidateRequest(request) == BattleActionError.None;
        }
    }
}

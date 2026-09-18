using System;
using System.Linq;
using NUnit.Framework;

namespace CK.SemesterProject.Battle.Tests
{
    public sealed class MonsterDesignTests
    {
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void EncounterTargetsRemainIndependentAndVictoryWaitsForLastMonster(int count)
        {
            var strike = new SkillData("strike", "공격", 1000, criticalChance: 0);
            var player = new CombatantData("p", "플레이어", BattleTeam.Player, 10000, 1000, 1000, new[] { strike });
            var monster = new CombatantData("shared", "같은 몬스터", BattleTeam.Monster, 100, 100, 100,
                new[] { new SkillData("attack", "공격", 1, criticalChance: 0) });
            var roster = new System.Collections.Generic.List<BattleParticipant> { new BattleParticipant("player", player) };
            for (int i = 0; i < count; i++)
            {
                roster.Add(new BattleParticipant("monster_" + i, monster));
            }
            var session = new BattleSession(randomSeed: 7);
            session.Start(roster);
            for (int defeated = 0; defeated < count; defeated++)
            {
                for (int guard = 0; guard < 12 && session.GetSnapshot().CurrentActorId != "player"; guard++)
                {
                    BattleSnapshot state = session.GetSnapshot();
                    Assert.That(session.TrySubmit(new BattleActionRequest(state.TurnId, state.CurrentActorId, BattleActionKind.Wait),
                        out BattleActionResult wait, out BattleActionError error), Is.True, error.ToString());
                    session.CompletePresentation(wait.ActionId);
                }
                Assert.That(session.GetSelectableTargets("strike"), Is.EquivalentTo(
                    Enumerable.Range(defeated, count - defeated).Select(i => "monster_" + i)));
                BattleSnapshot before = session.GetSnapshot();
                Assert.That(session.TrySubmit(new BattleActionRequest(before.TurnId, "player", BattleActionKind.Skill,
                    "strike", "monster_" + defeated), out BattleActionResult hit, out BattleActionError failure), Is.True, failure.ToString());
                session.CompletePresentation(hit.ActionId);
                BattleSnapshot after = session.GetSnapshot();
                Assert.That(after.Combatants.Count(unit => unit.Data.Team == BattleTeam.Monster && unit.IsDead), Is.EqualTo(defeated + 1));
                Assert.That(after.Phase == BattlePhase.Finished, Is.EqualTo(defeated == count - 1));
            }
            Assert.That(session.GetSnapshot().Outcome, Is.EqualTo(BattleOutcome.Victory));
        }

        private static BattleSession Create(BattleElement element, BattleElement partner = BattleElement.None,
            int ownHp = 100, int playerHp = 100, int ownMemory = 100, int playerMemory = 100,
            int playerRage = 0, int skillCost = 0)
        {
            var skill = new SkillData("attack", "공격", 10, element, memoryCost: skillCost, criticalChance: 0);
            var monster = new CombatantData("m", "몬스터", BattleTeam.Monster, 100, 100, 100, new[] { skill },
                element, monsterProfile: MonsterBehaviorProfile.CreateDefault(element));
            var player = new CombatantData("p", "플레이어", BattleTeam.Player, 100, 100, 100,
                new[] { new SkillData("player_attack", "공격", 100, criticalChance: 0) });
            var participants = new System.Collections.Generic.List<BattleParticipant>
            {
                new BattleParticipant("player", player, initialHp: playerHp, initialMemory: playerMemory, initialRageEnergy: playerRage),
                new BattleParticipant("monster", monster, initialHp: ownHp, initialMemory: ownMemory)
            };
            if (partner != BattleElement.None)
            {
                participants.Add(new BattleParticipant("partner", new CombatantData("ally", "동료", BattleTeam.Monster,
                    100, 100, 100, new[] { skill }, partner, monsterProfile: MonsterBehaviorProfile.CreateDefault(partner)), initialMemory: 1));
            }
            var session = new BattleSession(randomSeed: 7);
            session.Start(participants);
            for (int i = 0; i < 10 && session.GetSnapshot().CurrentActorId != "monster"; i++)
            {
                BattleSnapshot state = session.GetSnapshot();
                Assert.That(session.TrySubmit(new BattleActionRequest(state.TurnId, state.CurrentActorId, BattleActionKind.Wait),
                    out BattleActionResult result, out BattleActionError error), Is.True, error.ToString());
                session.CompletePresentation(result.ActionId);
            }
            Assert.That(session.GetSnapshot().CurrentActorId, Is.EqualTo("monster"));
            return session;
        }

        [TestCase(1, 1, 1)]
        [TestCase(1, 3, 2)]
        [TestCase(1, 4, 2)]
        [TestCase(2, 5, 5)]
        [TestCase(5, 1, 5)]
        public void ImprintCombinesConditionsAsDocumented(int player, int self, int expected)
        {
            Assert.That(MonsterProfileAi.ResolveActionNumber(player, self, 5), Is.EqualTo(expected));
        }

        [Test]
        public void AfterimageLowestMemoryTakesPriority()
        {
            Assert.That(MonsterProfileAi.ResolveActionNumber(6, 5, 6), Is.EqualTo(6));
            Assert.That(MonsterProfileAi.ResolveActionNumber(1, 6, 6), Is.EqualTo(6));
        }

        [TestCase(BattleElement.Imprint, 7, 70, 1.8)]
        [TestCase(BattleElement.Imprint, 6, 60, 1.75)]
        [TestCase(BattleElement.Afterimage, 2, 10, 1.1)]
        [TestCase(BattleElement.Afterimage, 5, 30, 1.4)]
        [TestCase(BattleElement.Oblivion, 4, 40, 1.5)]
        public void ActorSpecificInvestmentReachesCommonResolver(BattleElement element, int stage, int cost, double multiplier)
        {
            BattleSession session = Create(element);
            BattleSnapshot state = session.GetSnapshot();
            var request = new BattleActionRequest(state.TurnId, "monster", BattleActionKind.Skill, "attack", "player", investmentStage: stage);
            Assert.That(session.TrySubmit(request, out BattleActionResult result, out _), Is.True);
            Assert.That(result.Hit.InvestmentMultiplier, Is.EqualTo(multiplier).Within(0.00001));
            Assert.That(result.Changes.First(change => change.After.InstanceId == "monster").Before.Memory
                - result.Changes.First(change => change.After.InstanceId == "monster").After.Memory,
                Is.EqualTo(cost - (element == BattleElement.Afterimage ? 3 : element == BattleElement.Oblivion ? 2 : 0)));
        }

        [Test]
        public void PlayerAndOblivionCannotUseImprintStageSeven()
        {
            BattleSession session = Create(BattleElement.Oblivion);
            Assert.That(session.ValidateRequest(new BattleActionRequest(session.GetSnapshot().TurnId, "monster",
                BattleActionKind.Skill, "attack", "player", investmentStage: 7)), Is.EqualTo(BattleActionError.InvalidMemoryInvestment));
            CombatantData player = session.GetSnapshot().Combatants.First(unit => unit.InstanceId == "player").Data;
            Assert.That(session.Rules.TryGetInvestment(player, new BattleActionRequest(1, "player", BattleActionKind.Skill,
                investmentStage: 6), out _, out _, out _), Is.False);
        }

        [Test]
        public void ImprintRejectsStageOneAndDefense()
        {
            BattleSession session = Create(BattleElement.Imprint);
            long turn = session.GetSnapshot().TurnId;
            Assert.That(session.ValidateRequest(new BattleActionRequest(turn, "monster", BattleActionKind.Defend)), Is.EqualTo(BattleActionError.InvalidAction));
            Assert.That(session.ValidateRequest(new BattleActionRequest(turn, "monster", BattleActionKind.Skill, "attack", "player", investmentStage: 1)), Is.EqualTo(BattleActionError.InvalidMemoryInvestment));
        }

        [TestCase(100, 100, 5, 7)]
        [TestCase(55, 95, 4, 7)]
        [TestCase(40, 92, 4, 7)]
        [TestCase(30, 80, 6, 7)]
        [TestCase(60, 60, 3, 6)]
        public void ImprintHpExamples(int ownHp, int playerHp, int min, int max)
        {
            BattleSession session = Create(BattleElement.Imprint, ownHp: ownHp, playerHp: playerHp);
            for (int seed = 0; seed < 15; seed++)
            {
                Assert.That(new MonsterAi(randomSeed: seed).TryChooseAction(session, out BattleActionRequest request), Is.True);
                Assert.That(request.Kind, Is.EqualTo(BattleActionKind.Skill));
                Assert.That(request.InvestmentStage.Value, Is.InRange(min, max));
            }
        }

        [TestCase(0, 0, BattleActionKind.Skill)]
        [TestCase(10, 0, BattleActionKind.Skill)]
        [TestCase(0, 5, BattleActionKind.Wait)]
        public void ImprintLowMemoryNeverDefends(int memory, int skillCost, BattleActionKind kind)
        {
            BattleSession session = Create(BattleElement.Imprint, ownMemory: memory, skillCost: skillCost);
            Assert.That(new MonsterAi().TryChooseAction(session, out BattleActionRequest request), Is.True);
            Assert.That(request.Kind, Is.EqualTo(kind));
            Assert.That(request.InvestmentStage ?? 0, Is.Zero);
            Assert.That(session.ValidateRequest(request), Is.EqualTo(BattleActionError.None));
        }

        [Test]
        public void SoloAfterimageOpensWithSeventyPercentDefenseAndDoesNotMutateOnQuery()
        {
            BattleSession session = Create(BattleElement.Afterimage);
            var ai = new MonsterAi();
            ai.TryChooseAction(session, out BattleActionRequest request);
            ai.TryChooseAction(session, out BattleActionRequest repeated);
            Assert.That(request.Kind, Is.EqualTo(BattleActionKind.Defend));
            Assert.That(request.InvestmentStage, Is.EqualTo(2));
            Assert.That(repeated.InvestmentStage, Is.EqualTo(request.InvestmentStage));
            Assert.That(session.GetActionCount("monster"), Is.Zero);
            session.TrySubmit(request, out BattleActionResult result, out _);
            CombatantState monster = session.GetSnapshot().Combatants.First(unit => unit.InstanceId == "monster");
            Assert.That(monster.Memory, Is.EqualTo(90));
            Assert.That(monster.DefenseDamageMultiplier, Is.EqualTo(0.3).Within(0.00001));
            Assert.That(session.GetActionCount("monster"), Is.EqualTo(1));
            session.CompletePresentation(result.ActionId);
            BattleSnapshot next = session.GetSnapshot();
            Assert.That(next.CurrentActorId, Is.EqualTo("player"));
            session.TrySubmit(new BattleActionRequest(next.TurnId, "player", BattleActionKind.Skill, "player_attack", "monster"), out result, out _);
            Assert.That(result.Hit.Damage, Is.EqualTo(30));
            session.CompletePresentation(result.ActionId);
            Assert.That(session.GetSnapshot().Combatants.First(unit => unit.InstanceId == "monster").IsDefending, Is.False);
            ai.TryChooseAction(session, out request);
            Assert.That(request.Kind, Is.EqualTo(BattleActionKind.Skill));
        }

        [TestCase(BattleElement.Imprint, 2)]
        [TestCase(BattleElement.Oblivion, 5)]
        [TestCase(BattleElement.Afterimage, 5)]
        public void PairedAfterimageDoesNotOpenWithDefense(BattleElement partner, int max)
        {
            BattleSession session = Create(BattleElement.Afterimage, partner);
            for (int seed = 0; seed < 20; seed++)
            {
                new MonsterAi(randomSeed: seed).TryChooseAction(session, out BattleActionRequest request);
                Assert.That(request.Kind, Is.EqualTo(BattleActionKind.Skill));
                Assert.That(request.InvestmentStage.Value, Is.InRange(1, max));
            }
        }

        [Test]
        public void PairedAfterimageCannotSubmitRemovedStageFive()
        {
            BattleSession session = Create(BattleElement.Afterimage, BattleElement.Imprint);
            Assert.That(session.ValidateRequest(new BattleActionRequest(session.GetSnapshot().TurnId, "monster",
                BattleActionKind.Skill, "attack", "player", investmentStage: 5)), Is.EqualTo(BattleActionError.InvalidMemoryInvestment));
        }

        [Test]
        public void ChainTakesPriorityOverRageIncludingNullAttackBranch()
        {
            var attack = new SkillData("chain", "연쇄", 0, BattleElement.Afterimage, criticalChance: 0);
            var player = new CombatantData("p", "플레이어", BattleTeam.Player, 1000, 200, 200, new[] { attack });
            var monster = new CombatantData("m", "망각", BattleTeam.Monster, 1000, 100, 100,
                new[] { new SkillData("attack", "공격", 10) }, BattleElement.Oblivion,
                weaknessChain: new[] { BattleElement.Afterimage, BattleElement.Afterimage, BattleElement.Afterimage, BattleElement.Afterimage },
                monsterProfile: MonsterBehaviorProfile.CreateDefault(BattleElement.Oblivion));
            var session = new BattleSession();
            session.Start(new[] { new BattleParticipant("player", player, initialRageEnergy: 80),
                new BattleParticipant("monster", monster, initialMemory: 60) });
            for (int chain = 1; chain <= 2; chain++)
            {
                BattleSnapshot state = session.GetSnapshot();
                Assert.That(state.CurrentActorId, Is.EqualTo("player"));
                Assert.That(session.TrySubmit(new BattleActionRequest(state.TurnId, "player", BattleActionKind.Skill, "chain", "monster"), out BattleActionResult result, out _), Is.True);
                session.CompletePresentation(result.ActionId);
                new MonsterAi().TryChooseAction(session, out BattleActionRequest request);
                Assert.That(request.Kind, Is.EqualTo(chain == 1 ? BattleActionKind.Skill : BattleActionKind.Defend));
                if (chain == 2)
                {
                    Assert.That(request.InvestmentStage.Value, Is.InRange(2, 3));
                }
                state = session.GetSnapshot();
                session.TrySubmit(new BattleActionRequest(state.TurnId, "monster", BattleActionKind.Wait), out result, out _);
                session.CompletePresentation(result.ActionId);
            }
        }

        [Test]
        public void OblivionPartnerRaisesImprintOpeningInvestment()
        {
            BattleSession session = Create(BattleElement.Imprint, BattleElement.Oblivion);
            for (int seed = 0; seed < 20; seed++)
            {
                new MonsterAi(randomSeed: seed).TryChooseAction(session, out BattleActionRequest request);
                Assert.That(request.InvestmentStage.Value, Is.InRange(6, 7));
            }
        }

        [TestCase(44, BattleActionKind.Skill, 1, 3)]
        [TestCase(45, BattleActionKind.Defend, 2, 3)]
        [TestCase(74, BattleActionKind.Defend, 2, 3)]
        [TestCase(75, BattleActionKind.Defend, 3, 4)]
        [TestCase(90, BattleActionKind.Defend, 3, 4)]
        public void OblivionRageBoundaries(int rage, BattleActionKind kind, int min, int max)
        {
            BattleSession session = Create(BattleElement.Oblivion, playerRage: rage);
            new MonsterAi().TryChooseAction(session, out BattleActionRequest request);
            Assert.That(request.Kind, Is.EqualTo(kind));
            Assert.That(request.InvestmentStage.Value, Is.InRange(min, max));
        }

        [Test]
        public void PartnerDefenseCoinIsStablePerTurnAndVariesAcrossSeeds()
        {
            BattleSession session = Create(BattleElement.Oblivion, BattleElement.Imprint, ownMemory: 80, playerRage: 50);
            var kinds = new System.Collections.Generic.HashSet<BattleActionKind>();
            for (int seed = 0; seed < 60; seed++)
            {
                var ai = new MonsterAi(randomSeed: seed);
                ai.TryChooseAction(session, out BattleActionRequest first);
                ai.TryChooseAction(session, out BattleActionRequest second);
                Assert.That(first.Kind, Is.EqualTo(second.Kind));
                Assert.That(first.InvestmentStage, Is.EqualTo(second.InvestmentStage));
                Assert.That(session.ValidateRequest(first), Is.EqualTo(BattleActionError.None));
                kinds.Add(first.Kind);
            }
            Assert.That(kinds.Count, Is.EqualTo(2));
        }

        [TestCase(BattleElement.Imprint)]
        [TestCase(BattleElement.Afterimage)]
        [TestCase(BattleElement.Oblivion)]
        public void AllMemoryBandsAndPairsReturnAffordableActions(BattleElement element)
        {
            foreach (BattleElement partner in new[] { BattleElement.None, BattleElement.Imprint, BattleElement.Afterimage, BattleElement.Oblivion })
            foreach (int own in new[] { 0, 1, 10, 19, 20, 34, 35, 49, 50, 69, 70, 89, 90, 100 })
            foreach (int player in new[] { 0, 14, 15, 34, 35, 50, 70, 85, 100 })
            {
                BattleSession session = Create(element, partner, ownMemory: own, playerMemory: player);
                Assert.That(new MonsterAi().TryChooseAction(session, out BattleActionRequest request), Is.True);
                Assert.That(session.ValidateRequest(request), Is.EqualTo(BattleActionError.None), element + " " + partner + " " + own + " " + player);
            }
        }
    }
}

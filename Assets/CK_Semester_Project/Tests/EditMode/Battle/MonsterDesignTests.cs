using System;
using System.Linq;
using NUnit.Framework;

namespace CK.SemesterProject.Battle.Tests
{
    public sealed class MonsterDesignTests
    {
        private static BattleSession CreateFixScenario(BattleElement element, int playerRage = 0,
            int monsterRage = 0, bool profile = true, int skillCost = 0,
            BattleEntryCondition entry = BattleEntryCondition.Normal)
        {
            var player = new CombatantData("p", "플레이어", BattleTeam.Player, 10000, 100, 100,
                new[] { new SkillData("hit", "공격", 1) });
            var monster = new CombatantData("m", "몬스터", BattleTeam.Monster, 10000, 1000, 1000,
                new[] { new SkillData("attack", "공격", 10, element, memoryCost: skillCost, rageGain: 100) },
                element, monsterProfile: profile ? MonsterBehaviorProfile.CreateDefault(element) : null);
            var session = new BattleSession(randomSeed: 7);
            session.Start(new[] { new BattleParticipant("p", player, initialRageEnergy: playerRage),
                new BattleParticipant("m", monster, initialRageEnergy: monsterRage) }, entry);
            return session;
        }

        [Test]
        public void AfterimageOpeningBonusAttacksThenDefendsOnSecondAction()
        {
            BattleSession session = CreateFixScenario(BattleElement.Afterimage, entry: BattleEntryCondition.MonsterCollision);
            var ai = new MonsterAi();
            Assert.That(session.EntryInitiatorId, Is.EqualTo("m"));
            Assert.That(ai.TryChooseAction(session, out BattleActionRequest first), Is.True);
            Assert.That(first.Kind, Is.EqualTo(BattleActionKind.Skill));
            Assert.That(session.TrySubmit(first, out BattleActionResult hit, out _), Is.True);
            session.CompletePresentation(hit.ActionId);
            Assert.That(session.GetSnapshot().CurrentActorId, Is.EqualTo("m"));
            Assert.That(ai.TryChooseAction(session, out BattleActionRequest second), Is.True);
            Assert.That(second.Kind, Is.EqualTo(BattleActionKind.Defend));
            Assert.That(second.InvestmentStage, Is.EqualTo(2));
        }

        [TestCase(BattleElement.Afterimage)]
        [TestCase(BattleElement.Imprint)]
        [TestCase(BattleElement.Oblivion)]
        public void OverheatedPlayerPreventsProfileDefenseIncludingNoAffordableAttack(BattleElement element)
        {
            foreach (int cost in new[] { 0, 2000 })
            {
                BattleSession session = CreateFixScenario(element, playerRage: 100, skillCost: cost);
                Assert.That(session.GetSnapshot().Combatants.First(unit => unit.InstanceId == "p").IsOverheated, Is.True);
                Assert.That(new MonsterAi().TryChooseAction(session, out BattleActionRequest request), Is.True);
                Assert.That(request.Kind, Is.EqualTo(cost == 0 ? BattleActionKind.Skill : BattleActionKind.Wait));
                Assert.That(session.ValidateRequest(request), Is.EqualTo(BattleActionError.None));
            }
        }

        [Test]
        public void OverheatedPlayerPreventsGenericAiFallbackDefense()
        {
            BattleSession session = CreateFixScenario(BattleElement.None, playerRage: 100, profile: false, skillCost: 2000);
            Assert.That(new MonsterAi().TryChooseAction(session, out BattleActionRequest request), Is.True);
            Assert.That(request.Kind, Is.EqualTo(BattleActionKind.Wait));
        }

        [TestCase(0)]
        [TestCase(100)]
        public void MonsterCannotGainRageBonusOrOverheat(int initialRage)
        {
            BattleSession session = CreateFixScenario(BattleElement.None, monsterRage: initialRage, profile: false);
            BattleSnapshot before = session.GetSnapshot();
            CombatantState monster = before.Combatants.First(unit => unit.InstanceId == "m");
            Assert.That(monster.RageEnergy, Is.Zero);
            Assert.That(monster.IsOverheated, Is.False);
            Assert.That(before.Phase, Is.EqualTo(BattlePhase.AwaitingAction));
            Assert.That(session.TrySubmit(new BattleActionRequest(before.TurnId, "m", BattleActionKind.Skill,
                "attack", "p"), out BattleActionResult result, out _), Is.True);
            Assert.That(result.Hit.RageMultiplier, Is.EqualTo(1));
            Assert.That(session.GetSnapshot().Combatants.First(unit => unit.InstanceId == "m").RageEnergy, Is.Zero);
            session.CompletePresentation(result.ActionId);
            BattleSnapshot next = session.GetSnapshot();
            Assert.That(session.TrySubmit(new BattleActionRequest(next.TurnId, "p", BattleActionKind.Wait), out result, out _), Is.True);
            session.CompletePresentation(result.ActionId);
            Assert.That(session.GetSnapshot().CurrentActorId, Is.EqualTo("m"));
            Assert.That(session.GetSnapshot().Phase, Is.EqualTo(BattlePhase.AwaitingAction));
        }

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
            // 행동표의 메모리 구간을 그대로 비교한다. 실제 고갈·행동 소모·회복은 별도 세션 테스트에서 검증한다.
            var session = new BattleSession(randomSeed: 7, rules: new BattleRules(enableMemoryLoss: false));
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
                Is.EqualTo(cost - (element == BattleElement.Oblivion ? 10 : 0)));
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
            BattleSession session = Create(BattleElement.Afterimage, playerMemory: 99);
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
            // 방어 투자 후 메모리가 줄어 새 라운드에서도 플레이어가 먼저 행동한다.
            next = session.GetSnapshot();
            Assert.That(next.CurrentActorId, Is.EqualTo("player"));
            Assert.That(next.Combatants.First(unit => unit.InstanceId == "monster").IsDefending, Is.True);
            Assert.That(session.TrySubmit(new BattleActionRequest(next.TurnId, "player", BattleActionKind.Wait),
                out result, out _), Is.True);
            session.CompletePresentation(result.ActionId);
            Assert.That(session.GetSnapshot().CurrentActorId, Is.EqualTo("monster"));
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
            session.Start(new[] { new BattleParticipant("player", player, initialMemory: 55, initialRageEnergy: 80),
                new BattleParticipant("monster", monster, initialMemory: 60) }, BattleEntryCondition.PlayerInitiated);
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
            BattleSession session = Create(BattleElement.Oblivion, playerMemory: 99, playerRage: rage);
            new MonsterAi().TryChooseAction(session, out BattleActionRequest request);
            Assert.That(request.Kind, Is.EqualTo(kind));
            Assert.That(request.InvestmentStage.Value, Is.InRange(min, max));
        }

        [Test]
        public void PartnerDefenseCoinIsStablePerTurnAndVariesAcrossSeeds()
        {
            BattleSession session = Create(BattleElement.Oblivion, BattleElement.Imprint,
                ownMemory: 80, playerMemory: 79, playerRage: 50);
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

        private static BattleSession CreateDefenseMemoryScenario(int ownMemory, int playerMemory,
            BattleElement element = BattleElement.None, int skillCost = 0, int monsterCount = 1,
            int playerRage = 0, bool includeDeadPlayer = false)
        {
            var player = new CombatantData("player", "플레이어", BattleTeam.Player, 10000, 1000, 1000,
                new[] { new SkillData("hit", "공격", 1, criticalChance: 0) });
            var monster = new CombatantData("monster", "몬스터", BattleTeam.Monster, 10000, 1000, 1000,
                new[] { new SkillData("attack", "공격", 10, element, memoryCost: skillCost, criticalChance: 0) },
                element, monsterProfile: element == BattleElement.None ? null : MonsterBehaviorProfile.CreateDefault(element));
            var participants = new System.Collections.Generic.List<BattleParticipant>
            {
                new BattleParticipant("player", player, initialMemory: playerMemory, initialRageEnergy: playerRage)
            };
            if (includeDeadPlayer)
            {
                participants.Add(new BattleParticipant("dead_player", player, initialHp: 0,
                    initialMemory: 1000, initialRageEnergy: 100));
            }
            for (int index = 0; index < monsterCount; index++)
            {
                participants.Add(new BattleParticipant("monster_" + index, monster, initialMemory: ownMemory));
            }
            var session = new BattleSession(randomSeed: 7, rules: new BattleRules(enableMemoryLoss: false));
            session.Start(participants);
            AdvanceToDefenseActor(session, "monster_0");
            return session;
        }

        private static void AdvanceToDefenseActor(BattleSession session, string actorId)
        {
            for (int count = 0; count < 12; count++)
            {
                BattleSnapshot snapshot = session.GetSnapshot();
                if (snapshot.CurrentActorId == actorId)
                {
                    return;
                }
                Assert.That(session.TrySubmit(new BattleActionRequest(snapshot.TurnId,
                    snapshot.CurrentActorId, BattleActionKind.Wait), out BattleActionResult result,
                    out BattleActionError error), Is.True, error.ToString());
                Assert.That(session.CompletePresentation(result.ActionId), Is.True);
            }
            Assert.Fail("몬스터 행동 순서가 오지 않았습니다: " + actorId);
        }

        [TestCase(BattleElement.None, 99)]
        [TestCase(BattleElement.None, 100)]
        [TestCase(BattleElement.Afterimage, 99)]
        [TestCase(BattleElement.Afterimage, 100)]
        public void MonsterDefenseRejectsLowerAndEqualMemoryWithoutChangingState(BattleElement element, int memory)
        {
            BattleSession session = CreateDefenseMemoryScenario(memory, 100, element);
            BattleSnapshot before = session.GetSnapshot();
            int historyCount = session.History.Count;
            var defend = new BattleActionRequest(before.TurnId, "monster_0", BattleActionKind.Defend);
            Assert.That(session.ValidateRequest(defend), Is.EqualTo(BattleActionError.InvalidAction));
            Assert.That(session.TrySubmit(defend, out BattleActionResult result, out BattleActionError error), Is.False);
            Assert.That(error, Is.EqualTo(BattleActionError.InvalidAction));
            Assert.That(result, Is.Null);
            BattleSnapshot after = session.GetSnapshot();
            Assert.That(after.TurnId, Is.EqualTo(before.TurnId));
            Assert.That(after.CurrentActorId, Is.EqualTo(before.CurrentActorId));
            Assert.That(after.Phase, Is.EqualTo(before.Phase));
            CollectionAssert.AreEqual(before.TurnOrder, after.TurnOrder);
            for (int index = 0; index < before.Combatants.Count; index++)
            {
                Assert.That(after.Combatants[index], Is.SameAs(before.Combatants[index]));
            }
            Assert.That(session.PendingResult, Is.Null);
            Assert.That(session.GetActionCount("monster_0"), Is.Zero);
            Assert.That(session.History.Count, Is.EqualTo(historyCount));
        }

        [TestCase(BattleElement.None, 50)]
        [TestCase(BattleElement.Afterimage, 150)]
        public void MonsterDefenseComparesCurrentMemoryBeforeInvestment(BattleElement element, int remainingMemory)
        {
            BattleSession session = CreateDefenseMemoryScenario(250, 200, element);
            BattleSnapshot before = session.GetSnapshot();
            var defend = new BattleActionRequest(before.TurnId, "monster_0", BattleActionKind.Defend, investmentStage: 2);
            Assert.That(session.ValidateRequest(defend), Is.EqualTo(BattleActionError.None));
            Assert.That(session.TrySubmit(defend, out _, out BattleActionError error), Is.True, error.ToString());
            CombatantState monster = session.GetSnapshot().Combatants.First(unit => unit.InstanceId == "monster_0");
            Assert.That(monster.IsDefending, Is.True);
            Assert.That(monster.Memory, Is.EqualTo(remainingMemory));
            Assert.That(monster.Memory, Is.LessThan(200));
        }

        [TestCase(BattleElement.Afterimage, 99, 0, BattleActionKind.Skill)]
        [TestCase(BattleElement.Afterimage, 100, 0, BattleActionKind.Skill)]
        [TestCase(BattleElement.Afterimage, 100, 1001, BattleActionKind.Wait)]
        [TestCase(BattleElement.Oblivion, 99, 0, BattleActionKind.Skill)]
        [TestCase(BattleElement.Oblivion, 100, 0, BattleActionKind.Skill)]
        [TestCase(BattleElement.Oblivion, 100, 1001, BattleActionKind.Wait)]
        public void ProfileAiUsesAttackOrWaitWhenMemoryPreventsDefense(BattleElement element,
            int memory, int skillCost, BattleActionKind expected)
        {
            BattleSession session = CreateDefenseMemoryScenario(memory, 100, element, skillCost, playerRage: 80);
            Assert.That(new MonsterAi().TryChooseAction(session, out BattleActionRequest request), Is.True);
            Assert.That(request.Kind, Is.EqualTo(expected));
            Assert.That(session.ValidateRequest(request), Is.EqualTo(BattleActionError.None));
            Assert.That(session.TrySubmit(request, out _, out BattleActionError error), Is.True, error.ToString());
        }

        [TestCase(99, 0, BattleActionKind.Skill)]
        [TestCase(100, 0, BattleActionKind.Skill)]
        [TestCase(99, 1001, BattleActionKind.Wait)]
        [TestCase(100, 1001, BattleActionKind.Wait)]
        [TestCase(101, 1001, BattleActionKind.Defend)]
        public void GenericAiFallbackChecksStrictMemoryComparison(int memory, int skillCost, BattleActionKind expected)
        {
            BattleSession session = CreateDefenseMemoryScenario(memory, 100, skillCost: skillCost);
            Assert.That(new MonsterAi().TryChooseAction(session, out BattleActionRequest request), Is.True);
            Assert.That(request.Kind, Is.EqualTo(expected));
            Assert.That(session.ValidateRequest(request), Is.EqualTo(BattleActionError.None));
        }

        [TestCase(499, 0, BattleActionKind.Skill)]
        [TestCase(500, 0, BattleActionKind.Skill)]
        [TestCase(501, 0, BattleActionKind.Defend)]
        [TestCase(499, 1001, BattleActionKind.Wait)]
        [TestCase(500, 1001, BattleActionKind.Wait)]
        public void TrioAiChecksEachMonstersCurrentMemory(int memory, int skillCost, BattleActionKind expected)
        {
            BattleSession session = CreateDefenseMemoryScenario(memory, 500, BattleElement.Oblivion,
                skillCost, monsterCount: 3, playerRage: 80);
            for (int index = 0; index < 3; index++)
            {
                AdvanceToDefenseActor(session, "monster_" + index);
                Assert.That(new MonsterAi().TryChooseAction(session, out BattleActionRequest request), Is.True);
                Assert.That(request.Kind, Is.EqualTo(expected));
                Assert.That(session.ValidateRequest(request), Is.EqualTo(BattleActionError.None));
            }
        }

        [Test]
        public void DeadPlayerDoesNotPreventOtherwiseValidMonsterDefense()
        {
            BattleSession session = CreateDefenseMemoryScenario(250, 200, includeDeadPlayer: true);
            var defend = new BattleActionRequest(session.GetSnapshot().TurnId, "monster_0", BattleActionKind.Defend);
            Assert.That(session.TrySubmit(defend, out _, out BattleActionError error), Is.True, error.ToString());
        }

        [TestCase(BattleElement.None, 100)]
        [TestCase(BattleElement.Afterimage, 100)]
        [TestCase(BattleElement.Imprint, 0)]
        public void GreaterMemoryDoesNotOverrideExistingDefenseProhibitions(BattleElement element, int playerRage)
        {
            BattleSession session = CreateDefenseMemoryScenario(250, 200, element, playerRage: playerRage);
            // 몬스터가 먼저 행동하므로 플레이어의 행동 불능은 아직 소모되지 않았다.
            CombatantState player = session.GetSnapshot().Combatants.First(unit => unit.InstanceId == "player");
            Assert.That(player.IsOverheated, Is.EqualTo(playerRage == 100));
            var defend = new BattleActionRequest(session.GetSnapshot().TurnId, "monster_0", BattleActionKind.Defend);
            Assert.That(session.ValidateRequest(defend), Is.EqualTo(BattleActionError.InvalidAction));
            Assert.That(new MonsterAi().TryChooseAction(session, out BattleActionRequest request), Is.True);
            Assert.That(request.Kind, Is.EqualTo(BattleActionKind.Skill));
            Assert.That(session.ValidateRequest(request), Is.EqualTo(BattleActionError.None));
        }
    }
}

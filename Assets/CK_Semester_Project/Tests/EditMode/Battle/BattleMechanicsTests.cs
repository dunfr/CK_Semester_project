using System;
using System.Linq;
using NUnit.Framework;

namespace CK.SemesterProject.Battle.Tests
{
    public sealed class BattleMechanicsTests
    {
        private static readonly BattleElement[] Chain = { BattleElement.Afterimage, BattleElement.Imprint,
            BattleElement.Oblivion, BattleElement.Oblivion };

        private static BattleSession Start(SkillData[] skills, BattleElement element = BattleElement.None,
            BattleElement[] chain = null, int rage = 0, double evasion = 0, int monsterHp = 1000,
            BattleCombatRules mechanics = null, int seed = 17, int monsterMemory = 10, SkillData enemySkill = null)
        {
            var player = new CombatantData("p", "Player", BattleTeam.Player, 1000, 100, 50, skills);
            var monster = new CombatantData("m", "Monster", BattleTeam.Monster, 1000, 100, monsterMemory,
                new[] { enemySkill ?? new SkillData("enemy", "Enemy", 10) }, element, evasion, chain);
            var rules = new BattleRules(new[] { 1.0, 1.1, 1.2, 1.35 }, 5,
                mechanics: mechanics ?? new BattleCombatRules(criticalChance: 0));
            var session = new BattleSession(randomSeed: seed, rules: rules);
            session.Start(new[] { new BattleParticipant("p", player, initialRageEnergy: rage),
                new BattleParticipant("m", monster, initialHp: monsterHp) });
            return session;
        }

        private static BattleActionResult Attack(BattleSession session, string skill = "hit", int investment = 0)
        {
            BattleSnapshot state = session.GetSnapshot();
            var request = new BattleActionRequest(state.TurnId, state.CurrentActorId, BattleActionKind.Skill,
                skill, state.CurrentActorId == "p" ? "m" : "p", investment);
            Assert.That(session.TrySubmit(request, out BattleActionResult result, out BattleActionError error), Is.True, error.ToString());
            return result;
        }

        private static void Finish(BattleSession session)
        {
            Assert.That(session.CompletePresentation(session.PendingResult.ActionId), Is.True);
        }

        private static void ReturnToPlayer(BattleSession session)
        {
            for (int i = 0; i < 12; i++)
            {
                BattleSnapshot state = session.GetSnapshot();
                if (state.Phase == BattlePhase.AwaitingAction && state.CurrentActorId == "p")
                {
                    return;
                }
                if (state.Phase == BattlePhase.AwaitingPresentation)
                {
                    Finish(session);
                }
                else
                {
                    Assert.That(session.TrySubmit(new BattleActionRequest(state.TurnId, state.CurrentActorId,
                        BattleActionKind.Wait), out _, out _), Is.True);
                }
            }
            Assert.Fail("플레이어 행동 기회로 복귀하지 못했습니다.");
        }

        [TestCase(BattleElement.Afterimage, BattleElement.Imprint, 1.25)]
        [TestCase(BattleElement.Imprint, BattleElement.Oblivion, 1.25)]
        [TestCase(BattleElement.Oblivion, BattleElement.Afterimage, 1.25)]
        [TestCase(BattleElement.Imprint, BattleElement.Afterimage, 0.75)]
        [TestCase(BattleElement.Oblivion, BattleElement.Imprint, 0.75)]
        [TestCase(BattleElement.Afterimage, BattleElement.Oblivion, 0.75)]
        [TestCase(BattleElement.None, BattleElement.Imprint, 1)]
        [TestCase(BattleElement.Imprint, BattleElement.Imprint, 1)]
        public void ElementCycleMatchesDesign(BattleElement attack, BattleElement target, double multiplier)
        {
            BattleSession session = Start(new[] { new SkillData("hit", "Hit", 100, attack) }, element: target);
            BattleHitResult hit = Attack(session).Hit;
            Assert.That(hit.ElementMultiplier, Is.EqualTo(multiplier));
            Assert.That(hit.Damage, Is.EqualTo((int)(100 * multiplier)));
        }

        [Test]
        public void MissSpendsCostAndGainsRageWithoutDamageEffectsOrCritical()
        {
            BattleSession session = Start(new[] { new SkillData("hit", "Hit", 100, BattleElement.Imprint,
                memoryCost: 2, accuracy: 1, criticalChance: 1, rageGain: 10, inflictedSkippedTurns: 2) },
                evasion: 1, chain: Chain);
            BattleActionResult result = Attack(session, investment: 3);
            Assert.That(result.Hit.IsHit, Is.False);
            Assert.That(result.Hit.IsCritical, Is.False);
            Assert.That(result.Hit.Damage, Is.Zero);
            CombatantState player = session.GetSnapshot().Combatants[0];
            CombatantState monster = session.GetSnapshot().Combatants[1];
            Assert.That(player.Memory, Is.EqualTo(45));
            Assert.That(player.RageEnergy, Is.EqualTo(10));
            Assert.That(monster.Hp, Is.EqualTo(1000));
            Assert.That(monster.ImprintDamage, Is.Zero);
            Assert.That(monster.SkippedTurns, Is.Zero);
        }

        [Test]
        public void HitChanceSubtractsEvasionAndClampsAtZero()
        {
            BattleSession session = Start(new[] { new SkillData("hit", "Hit", 10, accuracy: 0.7) }, evasion: 0.2);
            Assert.That(Attack(session).Hit.HitChance, Is.EqualTo(0.5).Within(0.00001));
            session = Start(new[] { new SkillData("hit", "Hit", 10, accuracy: 0.2) }, evasion: 0.9);
            Assert.That(Attack(session).Hit.IsHit, Is.False);
        }

        [Test]
        public void FormulaMatchesDocumentExample292AndRoundsOnlyAtEnd()
        {
            BattleSession session = Start(new[] { new SkillData("hit", "Hit", 100,
                BattleElement.Afterimage, criticalChance: 1) }, element: BattleElement.Imprint,
                chain: new[] { BattleElement.Afterimage, BattleElement.Afterimage, BattleElement.Oblivion, BattleElement.Oblivion }, rage: 45);
            Attack(session);
            ReturnToPlayer(session);
            BattleHitResult hit = Attack(session, investment: 3).Hit;
            Assert.That(hit.ChainStepReached, Is.EqualTo(2));
            Assert.That(hit.IsCritical, Is.True);
            Assert.That(hit.Damage, Is.EqualTo(292));
            Assert.That(hit.RageMultiplier, Is.EqualTo(1.2));
            Assert.That(hit.ElementMultiplier, Is.EqualTo(1.25));
            Assert.That(hit.ChainMultiplier, Is.EqualTo(1.2));
            Assert.That(hit.InvestmentMultiplier, Is.EqualTo(1.35));
        }

        [TestCase(0, 100)]
        [TestCase(19, 100)]
        [TestCase(20, 108)]
        [TestCase(44, 108)]
        [TestCase(45, 120)]
        [TestCase(74, 120)]
        [TestCase(75, 135)]
        [TestCase(90, 135)]
        public void RageBonusUsesEnergyBeforeSkillGain(int rage, int damage)
        {
            BattleSession session = Start(new[] { new SkillData("hit", "Hit", 100, rageGain: 10) }, rage: rage);
            Assert.That(Attack(session).Hit.Damage, Is.EqualTo(damage));
            Assert.That(session.GetSnapshot().Combatants[0].RageEnergy, Is.EqualTo(Math.Min(100, rage + 10)));
        }

        [Test]
        public void AfterimageRecoveryAndOblivionStealAreImmediateAndCapped()
        {
            BattleSession session = Start(new[] { new SkillData("hit", "Hit", 0, BattleElement.Afterimage) });
            Attack(session);
            Assert.That(session.GetSnapshot().Combatants[0].Memory, Is.EqualTo(53));
            session = Start(new[] { new SkillData("hit", "Hit", 0, BattleElement.Oblivion) }, monsterMemory: 1);
            Attack(session);
            Assert.That(session.GetSnapshot().Combatants[0].Memory, Is.EqualTo(51));
            Assert.That(session.GetSnapshot().Combatants[1].Memory, Is.Zero);
        }

        [Test]
        public void ImprintTriggersOnceBeforeSelectionAndResumesSameActionOpportunity()
        {
            BattleSession session = Start(new[] { new SkillData("hit", "Hit", 100, BattleElement.Imprint) });
            Attack(session);
            Assert.That(session.GetSnapshot().Combatants[1].ImprintDamage, Is.EqualTo(10));
            Finish(session);
            BattleActionResult tick = session.PendingResult;
            Assert.That(tick.IsTurnStartEffect, Is.True);
            Assert.That(tick.WasSkipped, Is.False);
            Assert.That(tick.Changes.Single().HpDelta, Is.EqualTo(-10));
            Assert.That(session.GetSnapshot().Combatants[1].ImprintDamage, Is.Zero);
            Finish(session);
            Assert.That(session.GetSnapshot().CurrentActorId, Is.EqualTo("m"));
            Assert.That(session.GetSnapshot().Phase, Is.EqualTo(BattlePhase.AwaitingAction));
            Assert.That(session.GetSnapshot().TurnId, Is.GreaterThan(tick.ActionId));
            Assert.That(session.CompletePresentation(tick.ActionId), Is.False);
            Attack(session, "enemy");
            Finish(session);
            Assert.That(session.GetSnapshot().Combatants[1].Hp, Is.EqualTo(890));
        }

        [Test]
        public void LethalImprintEndsBattleWithoutAllowingVictimAction()
        {
            BattleSession session = Start(new[] { new SkillData("hit", "Hit", 100, BattleElement.Imprint) }, monsterHp: 105);
            Attack(session);
            Finish(session);
            Assert.That(session.PendingResult.IsTurnStartEffect, Is.True);
            Assert.That(session.PendingResult.WasSkipped, Is.True);
            Assert.That(session.PendingResult.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(new MonsterAi().TryChooseAction(session, out _), Is.False);
            Finish(session);
            Assert.That(session.GetSnapshot().Phase, Is.EqualTo(BattlePhase.Finished));
        }

        [Test]
        public void ChainFailureRestartsOnFirstElementAndMissResetsProgress()
        {
            BattleSession session = Start(new[] { new SkillData("first", "First", 1, BattleElement.Afterimage),
                new SkillData("wrong", "Wrong", 1, BattleElement.Oblivion),
                new SkillData("miss", "Miss", 1, BattleElement.Afterimage, accuracy: 0) }, chain: Chain);
            Attack(session, "first");
            ReturnToPlayer(session);
            Assert.That(Attack(session, "first").Hit.ChainStepReached, Is.EqualTo(1));
            ReturnToPlayer(session);
            Assert.That(Attack(session, "wrong").Hit.ChainStepReached, Is.Zero);
            ReturnToPlayer(session);
            Attack(session, "first");
            ReturnToPlayer(session);
            Attack(session, "miss");
            Assert.That(session.GetSnapshot().Combatants[1].ChainStep, Is.Zero);
        }

        [Test]
        public void FourthChainGrantsImmediateExtraActionWithoutStartingNewRound()
        {
            BattleSession session = Start(new[] { new SkillData("a", "A", 1, BattleElement.Afterimage),
                new SkillData("i", "I", 1, BattleElement.Imprint), new SkillData("o", "O", 1, BattleElement.Oblivion) }, chain: Chain);
            foreach (string skill in new[] { "a", "i", "o" })
            {
                Attack(session, skill);
                ReturnToPlayer(session);
            }
            int round = session.GetSnapshot().Round;
            BattleActionResult fourth = Attack(session, "o");
            Assert.That(fourth.GrantsExtraAction, Is.True);
            Assert.That(session.GetSnapshot().Combatants[1].ChainStep, Is.Zero);
            Assert.That(session.GetSnapshot().TurnOrder[0], Is.EqualTo("p"));
            Finish(session);
            Assert.That(session.GetSnapshot().CurrentActorId, Is.EqualTo("p"));
            Assert.That(session.GetSnapshot().Round, Is.EqualTo(round));
            Assert.That(Attack(session, "a").Hit.ChainStepReached, Is.EqualTo(1));
            Finish(session);
            Assert.That(session.GetSnapshot().CurrentActorId, Is.EqualTo("m"));
        }

        [TestCase(91)]
        [TestCase(100)]
        public void OverheatSkipsExactlyOneOpportunityAndResetsGauge(int rage)
        {
            BattleSession session = Start(new[] { new SkillData("hit", "Hit", 1) }, rage: rage);
            Assert.That(session.PendingResult.WasSkipped, Is.True);
            Assert.That(session.PendingResult.Changes.Single().Before.RageEnergy, Is.EqualTo(rage));
            Assert.That(session.GetSnapshot().Combatants[0].RageEnergy, Is.Zero);
            ReturnToPlayer(session);
            Assert.That(session.GetSnapshot().Round, Is.EqualTo(2));
            Assert.That(session.GetSnapshot().Combatants[0].CanAct, Is.True);
        }

        [Test]
        public void SkillIncapacityAndImprintConsumeOneOpportunityTogether()
        {
            BattleSession session = Start(new[] { new SkillData("hit", "Hit", 100,
                BattleElement.Imprint, inflictedSkippedTurns: 2) });
            Attack(session);
            Finish(session);
            Assert.That(session.PendingResult.WasSkipped, Is.True);
            Assert.That(session.GetSnapshot().Combatants[1].Hp, Is.EqualTo(890));
            Assert.That(session.GetSnapshot().Combatants[1].SkippedTurns, Is.EqualTo(1));
            Finish(session);
            Assert.That(session.GetSnapshot().CurrentActorId, Is.EqualTo("p"));
        }

        [Test]
        public void AiPredictionAndInvalidRequestsDoNotConsumeCombatRandomStream()
        {
            SkillData[] skills = { new SkillData("hit", "Hit", 10, accuracy: 0.65, criticalChance: 0.5) };
            BattleSession first = Start(skills, monsterMemory: 60, enemySkill: new SkillData("enemy", "Enemy", 10, accuracy: 0.65, criticalChance: 0.5));
            BattleSession second = Start(skills, monsterMemory: 60, enemySkill: new SkillData("enemy", "Enemy", 10, accuracy: 0.65, criticalChance: 0.5));
            var ai = new MonsterAi();
            for (int i = 0; i < 20; i++)
            {
                Assert.That(ai.TryChooseAction(first, out _), Is.True);
            }
            Assert.That(first.TrySubmit(new BattleActionRequest(-1, "m", BattleActionKind.Wait), out _, out _), Is.False);
            BattleActionResult a = Attack(first, "enemy");
            BattleActionResult b = Attack(second, "enemy");
            Assert.That(a.Hit.IsHit, Is.EqualTo(b.Hit.IsHit));
            Assert.That(a.Hit.IsCritical, Is.EqualTo(b.Hit.IsCritical));
            Assert.That(a.Hit.Damage, Is.EqualTo(b.Hit.Damage));
        }

        [Test]
        public void ImprintReapplicationReplacesRatherThanStacks()
        {
            var skill = new SkillData("hit", "Hit", 100, BattleElement.Imprint, criticalChance: 0);
            var player = new CombatantData("p", "P", BattleTeam.Player, 1000, 100, 50, new[] { skill });
            var monster = new CombatantData("m", "M", BattleTeam.Monster, 1000, 100, 10, Array.Empty<SkillData>());
            var session = new BattleSession(randomSeed: 4);
            session.Start(new[] { new BattleParticipant("p", player), new BattleParticipant("p2", player, initialMemory: 40),
                new BattleParticipant("m", monster) });
            Attack(session);
            Finish(session);
            BattleSnapshot state = session.GetSnapshot();
            Assert.That(state.CurrentActorId, Is.EqualTo("p2"));
            Assert.That(session.TrySubmit(new BattleActionRequest(state.TurnId, "p2", BattleActionKind.Skill, "hit", "m"), out _, out _), Is.True);
            Assert.That(session.GetSnapshot().Combatants[2].ImprintDamage, Is.EqualTo(10));
            Finish(session);
            Assert.That(session.PendingResult.Changes.Single().HpDelta, Is.EqualTo(-10));
        }

        [Test]
        public void DefenseAppliesAfterAllAttackMultipliers()
        {
            BattleSession session = Start(new[] { new SkillData("hit", "Hit", 100, criticalChance: 1) },
                rage: 45, monsterMemory: 60);
            BattleSnapshot state = session.GetSnapshot();
            Assert.That(session.TrySubmit(new BattleActionRequest(state.TurnId, "m", BattleActionKind.Defend), out _, out _), Is.True);
            Finish(session);
            BattleHitResult hit = Attack(session, investment: 3).Hit;
            Assert.That(hit.DefenseMultiplier, Is.EqualTo(0.5));
            Assert.That(hit.Damage, Is.EqualTo(97));
        }

        [Test]
        public void RageGainedAtChainCompletionConsumesExtraOpportunityAsSkip()
        {
            BattleSession session = Start(new[] { new SkillData("a", "A", 1, BattleElement.Afterimage),
                new SkillData("i", "I", 1, BattleElement.Imprint),
                new SkillData("o", "O", 1, BattleElement.Oblivion),
                new SkillData("burst", "Burst", 1, BattleElement.Oblivion, rageGain: 100) }, chain: Chain);
            foreach (string skill in new[] { "a", "i", "o" })
            {
                Attack(session, skill);
                ReturnToPlayer(session);
            }
            Assert.That(Attack(session, "burst").GrantsExtraAction, Is.True);
            Assert.That(session.GetSnapshot().Combatants[0].IsOverheated, Is.True);
            Assert.That(session.GetSnapshot().Combatants[0].CanAct, Is.False);
            Finish(session);
            Assert.That(session.PendingResult.WasSkipped, Is.True);
            Assert.That(session.GetSnapshot().Combatants[0].RageEnergy, Is.Zero);
            Finish(session);
            Assert.That(session.GetSnapshot().CurrentActorId, Is.EqualTo("m"));
        }

        [Test]
        public void LastKillCancelsChainExtraAction()
        {
            BattleSession session = Start(new[] { new SkillData("hit", "Hit", 1, BattleElement.Afterimage),
                new SkillData("kill", "Kill", 1000, BattleElement.Afterimage) },
                chain: new[] { BattleElement.Afterimage, BattleElement.Afterimage, BattleElement.Afterimage, BattleElement.Afterimage });
            for (int i = 0; i < 3; i++)
            {
                Attack(session);
                ReturnToPlayer(session);
            }
            BattleActionResult result = Attack(session, "kill");
            Assert.That(result.Hit.ChainStepReached, Is.EqualTo(4));
            Assert.That(result.GrantsExtraAction, Is.False);
            Assert.That(session.GetSnapshot().TurnOrder, Is.Empty);
            Finish(session);
            Assert.That(session.GetSnapshot().Phase, Is.EqualTo(BattlePhase.Finished));
        }

        [Test]
        public void LargeDamageSaturatesWithoutOverflow()
        {
            BattleSession session = Start(new[] { new SkillData("hit", "Hit", int.MaxValue, criticalChance: 1) },
                mechanics: new BattleCombatRules(criticalMultiplier: double.MaxValue));
            BattleActionResult result = Attack(session);
            Assert.That(result.Hit.Damage, Is.EqualTo(int.MaxValue));
            Assert.That(result.Changes.Last().After.Hp, Is.Zero);
            Assert.That(BattleMath.RoundDamage(double.PositiveInfinity), Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void AiPrefersReliableDamageToGuaranteedMiss()
        {
            var player = new CombatantData("p", "P", BattleTeam.Player, 1000, 100, 10, Array.Empty<SkillData>());
            var monster = new CombatantData("m", "M", BattleTeam.Monster, 1000, 100, 50,
                new[] { new SkillData("miss", "Miss", 1000, accuracy: 0),
                    new SkillData("reliable", "Reliable", 10, criticalChance: 0) });
            var session = new BattleSession();
            session.Start(new[] { new BattleParticipant("p", player), new BattleParticipant("m", monster) });
            Assert.That(new MonsterAi().TryChooseAction(session, out BattleActionRequest request), Is.True);
            Assert.That(request.SkillId, Is.EqualTo("reliable"));
        }

        [Test]
        public void NonlethalTurnStartEffectDoesNotRerollTiedActors()
        {
            var player = new CombatantData("p", "P", BattleTeam.Player, 1000, 100, 50,
                new[] { new SkillData("hit", "Hit", 100, BattleElement.Imprint, criticalChance: 0) });
            var monster = new CombatantData("m", "M", BattleTeam.Monster, 1000, 100, 10, Array.Empty<SkillData>());
            for (int seed = 0; seed < 20; seed++)
            {
                var session = new BattleSession(randomSeed: seed);
                session.Start(new[] { new BattleParticipant("p", player), new BattleParticipant("m", monster),
                    new BattleParticipant("other", monster) });
                Attack(session);
                Finish(session);
                if (session.GetSnapshot().Phase == BattlePhase.AwaitingAction)
                {
                    BattleSnapshot state = session.GetSnapshot();
                    Assert.That(state.CurrentActorId, Is.EqualTo("other"));
                    session.TrySubmit(new BattleActionRequest(state.TurnId, "other", BattleActionKind.Wait), out _, out _);
                    Finish(session);
                }
                Assert.That(session.PendingResult.IsTurnStartEffect, Is.True);
                Assert.That(session.GetSnapshot().TurnOrder[0], Is.EqualTo("m"));
                Finish(session);
                Assert.That(session.GetSnapshot().TurnOrder[0], Is.EqualTo(session.GetSnapshot().CurrentActorId));
            }
        }

        [TestCase(1)]
        [TestCase(17)]
        [TestCase(999)]
        public void FullBattleWithAllMechanicsTerminatesWithoutInvalidState(int seed)
        {
            SkillData[] skills = {
                new SkillData("a", "A", 80, BattleElement.Afterimage, accuracy: 0.9, rageGain: 25),
                new SkillData("i", "I", 80, BattleElement.Imprint, accuracy: 0.9, rageGain: 25),
                new SkillData("o", "O", 80, BattleElement.Oblivion, accuracy: 0.9, rageGain: 25) };
            BattleSession session = Start(skills, chain: Chain, seed: seed,
                mechanics: new BattleCombatRules(), enemySkill: new SkillData("enemy", "Enemy", 90,
                    BattleElement.Imprint, accuracy: 0.9, rageGain: 30, inflictedSkippedTurns: 1));
            var ai = new MonsterAi();
            for (int step = 0; step < 160 && session.GetSnapshot().Phase != BattlePhase.Finished; step++)
            {
                BattleSnapshot state = session.GetSnapshot();
                foreach (CombatantState unit in state.Combatants)
                {
                    Assert.That(unit.Hp, Is.InRange(0, unit.Data.MaxHp));
                    Assert.That(unit.Memory, Is.InRange(0, unit.Data.MaxMemory));
                    Assert.That(unit.ChainStep, Is.InRange(0, 3));
                    Assert.That(unit.RageEnergy, Is.InRange(0, 100));
                }
                if (state.Phase == BattlePhase.AwaitingPresentation)
                {
                    Finish(session);
                }
                else if (state.CurrentActorId == "p")
                {
                    string skill = state.Combatants[1].ChainStep == 0 ? "a"
                        : state.Combatants[1].ChainStep == 1 ? "i" : "o";
                    Attack(session, skill);
                }
                else
                {
                    Assert.That(ai.TryChooseAction(session, out BattleActionRequest request), Is.True);
                    Assert.That(session.TrySubmit(request, out _, out _), Is.True);
                }
            }
            Assert.That(session.GetSnapshot().Phase, Is.EqualTo(BattlePhase.Finished));
            Assert.That(session.GetSnapshot().Outcome, Is.Not.EqualTo(BattleOutcome.None));
            Assert.That(session.GetSnapshot().TurnOrder, Is.Empty);
        }

        [Test]
        public void SettingsAndDataRejectInvalidProbabilitiesAndChainLengths()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SkillData("x", "X", 1, accuracy: double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SkillData("x", "X", 1, criticalChance: 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SkillData("x", "X", 1, rageGain: 101));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleCombatRules(chainMultipliers: new[] { 1.0 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleCombatRules(imprintRatio: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatantData("x", "X", BattleTeam.Monster,
                100, 10, 5, Array.Empty<SkillData>(), weaknessChain: new[] { BattleElement.Afterimage }));
        }
    }
}

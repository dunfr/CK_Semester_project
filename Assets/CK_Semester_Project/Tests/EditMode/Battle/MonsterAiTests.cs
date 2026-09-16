using System;
using System.Linq;
using NUnit.Framework;

namespace CK.SemesterProject.Battle.Tests
{
    public sealed class MonsterAiTests
    {
        private static BattleSession Start(SkillData[] skills, int memory = 20, int playerHp = 100,
            BattleRules rules = null, int skippedTurns = 0)
        {
            var session = new BattleSession(randomSeed: 11,
                rules: rules ?? new BattleRules(new[] { 1.0, 1.1, 1.2, 1.35 }, mechanics: BattleCombatRules.Basic));
            var player = new CombatantData("hero", "Hero", BattleTeam.Player, 100, 40, 0,
                new[] { new SkillData("attack", "Attack", 10) });
            var monster = new CombatantData("monster", "Monster", BattleTeam.Monster,
                100, 40, memory, skills);
            session.Start(new[]
            {
                new BattleParticipant("p", player, initialHp: playerHp),
                new BattleParticipant("m", monster, skippedTurns: skippedTurns)
            });
            return session;
        }

        private static BattleActionRequest Choose(BattleSession session, MonsterAi ai = null)
        {
            Assert.That((ai ?? new MonsterAi()).TryChooseAction(session, out BattleActionRequest request), Is.True);
            Assert.That(session.ValidateRequest(request), Is.EqualTo(BattleActionError.None));
            return request;
        }

        [Test]
        public void SelectsStrongestAffordableSkillAndInvestmentThroughCommonExecution()
        {
            BattleSession session = Start(new[] { new SkillData("weak", "Weak", 10),
                new SkillData("strong", "Strong", 20, memoryCost: 2),
                new SkillData("expensive", "Expensive", 100, memoryCost: 30) });
            BattleActionRequest request = Choose(session);
            Assert.That(request.SkillId, Is.EqualTo("strong"));
            Assert.That(request.TargetId, Is.EqualTo("p"));
            Assert.That(request.MemoryInvestment, Is.EqualTo(3));
            Assert.That(session.TrySubmit(request, out _, out _), Is.True);
            Assert.That(session.GetSnapshot().Combatants[0].Hp, Is.EqualTo(73));
            Assert.That(session.GetSnapshot().Combatants[1].Memory, Is.EqualTo(15));
        }

        [Test]
        public void LethalAttackUsesSmallestSufficientInvestment()
        {
            BattleSession session = Start(new[] { new SkillData("hit", "Hit", 10) }, playerHp: 12);
            Assert.That(Choose(session).MemoryInvestment, Is.EqualTo(2));
        }

        [Test]
        public void ZeroBenefitInvestmentIsNotSpent()
        {
            BattleSession session = Start(new[] { new SkillData("hit", "Hit", 1) });
            Assert.That(Choose(session).MemoryInvestment, Is.Zero);
        }

        [Test]
        public void ChoiceIsRepeatableAndDoesNotChangeStateOrConsumeTurn()
        {
            BattleSession session = Start(new[] { new SkillData("hit", "Hit", 20) });
            BattleSnapshot before = session.GetSnapshot();
            BattleActionRequest first = Choose(session);
            BattleActionRequest second = Choose(session);
            Assert.That(second.SkillId, Is.EqualTo(first.SkillId));
            Assert.That(second.TargetId, Is.EqualTo(first.TargetId));
            Assert.That(second.MemoryInvestment, Is.EqualTo(first.MemoryInvestment));
            Assert.That(session.GetSnapshot().TurnId, Is.EqualTo(before.TurnId));
            Assert.That(session.GetSnapshot().Combatants[0], Is.SameAs(before.Combatants[0]));
            Assert.That(session.GetSnapshot().Combatants[1], Is.SameAs(before.Combatants[1]));
            CollectionAssert.AreEqual(before.TurnOrder, session.GetSnapshot().TurnOrder);
            Assert.That(session.PendingResult, Is.Null);
        }

        [Test]
        public void EmptyOrUnaffordableSkillSetFallsBackToFreeDefense()
        {
            foreach (SkillData[] skills in new[] { Array.Empty<SkillData>(),
                new[] { new SkillData("expensive", "Expensive", 100, memoryCost: 21) } })
            {
                BattleSession session = Start(skills);
                BattleActionRequest request = Choose(session);
                Assert.That(request.Kind, Is.EqualTo(BattleActionKind.Defend));
                Assert.That(request.MemoryInvestment, Is.Zero);
                Assert.That(session.TrySubmit(request, out _, out _), Is.True);
                Assert.That(session.GetSnapshot().Combatants[1].IsDefending, Is.True);
                Assert.That(session.CompletePresentation(session.PendingResult.ActionId), Is.True);
                Assert.That(session.GetSnapshot().CurrentActorId, Is.EqualTo("p"));
            }
        }

        [Test]
        public void CanChooseSelfRecoveryButDoesNotAttackOwnTeam()
        {
            BattleSession session = Start(new[] {
                new SkillData("friendly", "Friendly", 100, target: SkillTarget.Ally),
                new SkillData("recover", "Recover", 0, target: SkillTarget.Self, memoryRecovery: 5) });
            BattleActionRequest request = Choose(session);
            Assert.That(request.SkillId, Is.EqualTo("recover"));
            Assert.That(request.TargetId, Is.EqualTo("m"));
            Assert.That(session.TrySubmit(request, out _, out _), Is.True);
            Assert.That(session.GetSnapshot().Combatants[1].Memory, Is.EqualTo(25));
        }

        [Test]
        public void KillBonusSelectsKillableTargetAndDeadTargetsAreExcluded()
        {
            var hit = new SkillData("hit", "Hit", 10);
            var player = new CombatantData("hero", "Hero", BattleTeam.Player, 100, 40, 0, new[] { hit });
            var monster = new CombatantData("monster", "Monster", BattleTeam.Monster, 100, 40, 20, new[] { hit });
            var session = new BattleSession(rules: new BattleRules(mechanics: BattleCombatRules.Basic));
            session.Start(new[] { new BattleParticipant("full", player),
                new BattleParticipant("dead", player, initialHp: 0),
                new BattleParticipant("low", player, initialHp: 5), new BattleParticipant("m", monster) });
            Assert.That(Choose(session).TargetId, Is.EqualTo("low"));
        }

        [Test]
        public void EqualScoresPreferLowerCostThenDefinitionOrder()
        {
            BattleSession session = Start(new[] { new SkillData("paid", "Paid", 10, memoryCost: 2),
                new SkillData("free", "Free", 10), new SkillData("also_free", "Also Free", 10) });
            var ai = new MonsterAi(new MonsterAiSettings(maxMemoryInvestment: 0, memoryWeight: 0));
            Assert.That(Choose(session, ai).SkillId, Is.EqualTo("free"));
        }

        [Test]
        public void CannotChooseOnPlayerTurnSkippedTurnPresentationOrFinishedBattle()
        {
            var ai = new MonsterAi();
            SkillData[] skills = { new SkillData("hit", "Hit", 10) };
            Assert.That(ai.TryChooseAction(new BattleSession(rules: new BattleRules(mechanics: BattleCombatRules.Basic)), out _), Is.False);
            Assert.That(ai.TryChooseAction(Start(skills, memory: 0), out _), Is.False);
            Assert.That(ai.TryChooseAction(Start(skills, skippedTurns: 1), out _), Is.False);
            Assert.That(ai.TryChooseAction(Start(skills, playerHp: 0), out _), Is.False);
            BattleSession session = Start(skills);
            session.TrySubmit(Choose(session), out _, out _);
            Assert.That(ai.TryChooseAction(session, out _), Is.False);
        }

        [Test]
        public void InvestmentRespectsAvailableMemoryAndConfiguredLimit()
        {
            BattleSession session = Start(new[] { new SkillData("hit", "Hit", 100, memoryCost: 1) }, memory: 2);
            Assert.That(Choose(session).MemoryInvestment, Is.Zero, "이미 처치 가능한 공격에 투자하지 않는다.");
            session = Start(new[] { new SkillData("hit", "Hit", 20) });
            Assert.That(Choose(session, new MonsterAi(new MonsterAiSettings(maxMemoryInvestment: 1))).MemoryInvestment,
                Is.EqualTo(1));
            session = Start(new[] { new SkillData("hit", "Hit", 20) }, rules: new BattleRules(mechanics: BattleCombatRules.Basic));
            Assert.That(Choose(session).MemoryInvestment, Is.Zero);
        }

        [Test]
        public void InvalidSettingsAndNullSessionAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MonsterAiSettings(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MonsterAiSettings(memoryWeight: double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MonsterAiSettings(killBonus: double.PositiveInfinity));
            Assert.Throws<ArgumentNullException>(() => new MonsterAi().TryChooseAction(null, out _));
        }
    }
}

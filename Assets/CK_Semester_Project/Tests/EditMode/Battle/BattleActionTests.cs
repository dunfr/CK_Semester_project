using System;
using System.Linq;
using NUnit.Framework;

namespace CK.SemesterProject.Battle.Tests
{
    public sealed class BattleActionTests
    {
        private static BattleSession Start(SkillData skill, int memory = 10, int enemyMemory = 5,
            int rage = 50, BattleRules rules = null)
        {
            var session = new BattleSession(randomSeed: 1,
                rules: rules ?? new BattleRules(new[] { 1.0, 1.1, 1.2, 1.35 }, 5));
            session.Start(new[]
            {
                new BattleParticipant("p", new CombatantData("p", "Player", BattleTeam.Player,
                    100, 20, memory, new[] { skill }), initialRageEnergy: rage),
                new BattleParticipant("m", new CombatantData("m", "Monster", BattleTeam.Monster,
                    100, 20, enemyMemory, new[] { new SkillData("hit", "Hit", 13) }))
            });
            return session;
        }

        private static BattleActionRequest Request(BattleSession session, BattleActionKind kind,
            int investment = 0, string skill = "hit", string target = "m")
        {
            BattleSnapshot state = session.GetSnapshot();
            return new BattleActionRequest(state.TurnId, state.CurrentActorId, kind,
                kind == BattleActionKind.Skill ? skill : null,
                kind == BattleActionKind.Skill ? target : null, investment);
        }

        private static void Submit(BattleSession session, BattleActionRequest request)
        {
            Assert.That(session.TrySubmit(request, out _, out BattleActionError error), Is.True, error.ToString());
        }

        private static void Finish(BattleSession session)
        {
            Assert.That(session.CompletePresentation(session.PendingResult.ActionId), Is.True);
        }

        [Test]
        public void CostAndInvestmentApplyOnceAndDamageRoundsAwayFromZero()
        {
            BattleSession session = Start(new SkillData("hit", "Hit", 10, memoryCost: 2));
            BattleActionRequest request = Request(session, BattleActionKind.Skill, 3);
            Submit(session, request);
            Assert.That(session.GetSnapshot().Combatants[0].Memory, Is.EqualTo(5));
            Assert.That(session.GetSnapshot().Combatants[1].Hp, Is.EqualTo(86));
            Assert.That(session.TrySubmit(request, out _, out _), Is.False);
            Assert.That(session.GetSnapshot().Combatants[0].Memory, Is.EqualTo(5));
        }

        [Test]
        public void RecoveryCannotFundAnUnaffordableRequest()
        {
            BattleSession session = Start(new SkillData("hit", "Hit", 10, memoryCost: 9, memoryRecovery: 20));
            BattleActionRequest request = Request(session, BattleActionKind.Skill, 2);
            Assert.That(session.ValidateRequest(request), Is.EqualTo(BattleActionError.InsufficientMemory));
            Assert.That(session.TrySubmit(request, out _, out BattleActionError error), Is.False);
            Assert.That(error, Is.EqualTo(BattleActionError.InsufficientMemory));
            Assert.That(session.GetSnapshot().Combatants[0].Memory, Is.EqualTo(10));
            Assert.That(session.GetSnapshot().Combatants[1].Hp, Is.EqualTo(100));
            Assert.That(session.PendingResult, Is.Null);
        }

        [TestCase(19, 5, 1)]
        [TestCase(10, 2, 2)]
        [TestCase(20, 5, 0)]
        [TestCase(10, 0, 0)]
        public void StealConservesMemoryAndRespectsBothLimits(int memory, int enemyMemory, int stolen)
        {
            BattleSession session = Start(new SkillData("hit", "Hit", 0, memorySteal: 8), memory, enemyMemory);
            Submit(session, Request(session, BattleActionKind.Skill));
            Assert.That(session.GetSnapshot().Combatants[0].Memory, Is.EqualTo(memory + stolen));
            Assert.That(session.GetSnapshot().Combatants[1].Memory, Is.EqualTo(enemyMemory - stolen));
        }

        [Test]
        public void SpendThenRecoverThenStealUsesAvailableCapacity()
        {
            BattleSession session = Start(new SkillData("hit", "Hit", 0,
                memoryCost: 4, memoryRecovery: 2, memorySteal: 8), 20);
            Submit(session, Request(session, BattleActionKind.Skill));
            Assert.That(session.GetSnapshot().Combatants[0].Memory, Is.EqualTo(20));
            Assert.That(session.GetSnapshot().Combatants[1].Memory, Is.EqualTo(3));
        }

        [Test]
        public void SelfRecoveryAggregatesOneEffectAndDoesNotStealFromSelf()
        {
            BattleSession session = Start(new SkillData("hit", "Recover", 0,
                target: SkillTarget.Self, memoryRecovery: int.MaxValue, memorySteal: 5));
            Submit(session, Request(session, BattleActionKind.Skill, target: "p"));
            Assert.That(session.PendingResult.Changes.Count, Is.EqualTo(1));
            Assert.That(session.GetSnapshot().Combatants[0].Memory, Is.EqualTo(20));
        }

        [Test]
        public void DefenseSpendsMemoryReducesRageAndExpiresAtNextOwnTurn()
        {
            BattleSession session = Start(new SkillData("hit", "Hit", 10));
            Submit(session, Request(session, BattleActionKind.Defend, 3));
            CombatantState guarded = session.GetSnapshot().Combatants[0];
            Assert.That(guarded.IsDefending, Is.True);
            Assert.That(guarded.Memory, Is.EqualTo(7));
            Assert.That(guarded.RageEnergy, Is.EqualTo(35));
            Finish(session);
            Submit(session, Request(session, BattleActionKind.Skill, target: "p"));
            Assert.That(session.GetSnapshot().Combatants[0].Hp, Is.EqualTo(93));
            Finish(session);
            Assert.That(session.GetSnapshot().CurrentActorId, Is.EqualTo("p"));
            Assert.That(session.GetSnapshot().Combatants[0].IsDefending, Is.False);
            Assert.That(guarded.IsDefending, Is.True, "기존 스냅샷은 변하지 않는다.");
        }

        [Test]
        public void ZeroInvestmentDefenseIsFreeAndRageNeverBecomesNegative()
        {
            BattleSession session = Start(new SkillData("hit", "Hit", 10), rage: 1);
            Submit(session, Request(session, BattleActionKind.Defend, 3));
            Assert.That(session.GetSnapshot().Combatants[0].RageEnergy, Is.Zero);
            session = Start(new SkillData("hit", "Hit", 10));
            Submit(session, Request(session, BattleActionKind.Defend));
            Assert.That(session.GetSnapshot().Combatants[0].Memory, Is.EqualTo(10));
            Assert.That(session.GetSnapshot().Combatants[0].RageEnergy, Is.EqualTo(50));
        }

        [Test]
        public void MonsterUsesTheSameCostRecoveryAndDefensePath()
        {
            BattleSession session = Start(new SkillData("hit", "Hit", 10));
            Submit(session, Request(session, BattleActionKind.Wait));
            Finish(session);
            Submit(session, Request(session, BattleActionKind.Defend, 2));
            Assert.That(session.GetSnapshot().Combatants[1].IsDefending, Is.True);
            Assert.That(session.GetSnapshot().Combatants[1].Memory, Is.EqualTo(3));
        }

        [Test]
        public void MissingInvestmentTableRejectsPositiveSkillInvestment()
        {
            BattleSession session = Start(new SkillData("hit", "Hit", 10), rules: new BattleRules());
            Assert.That(session.ValidateRequest(Request(session, BattleActionKind.Skill, 1)),
                Is.EqualTo(BattleActionError.InvalidMemoryInvestment));
            Assert.That(session.TrySubmit(Request(session, BattleActionKind.Skill, 1), out _, out _), Is.False);
        }

        [Test]
        public void RulesAndSkillRejectInvalidValuesAndCloneInput()
        {
            double[] values = { 1, 1.5 };
            var rules = new BattleRules(values);
            values[1] = 100;
            Assert.That(rules.InvestmentMultipliers[1], Is.EqualTo(1.5));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleRules(new[] { 1.0, double.NaN }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleRules(defenseDamageMultiplier: double.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SkillData("x", "X", 0, memoryCost: -1));
        }
    }
}

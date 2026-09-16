using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CK.SemesterProject.Battle.Tests
{
    public sealed class BattleSessionTests
    {
        private static BattleParticipant Unit(string id, BattleTeam team, int memory = 10,
            int hp = 100, int skippedTurns = 0, int power = 25)
        {
            var skill = new SkillData("attack", "기본 공격", power);
            var data = new CombatantData("definition-" + id, id, team, 100, 100, memory, new[] { skill });
            return new BattleParticipant(id, data, hp, skippedTurns: skippedTurns);
        }

        private static BattleSession Start(IBattleActionResolver resolver = null, params BattleParticipant[] roster)
        {
            var session = new BattleSession(resolver, 42, new BattleRules(mechanics: BattleCombatRules.Basic));
            session.Start(roster.Length == 0
                ? new[] { Unit("player", BattleTeam.Player), Unit("monster", BattleTeam.Monster) }
                : roster);
            return session;
        }

        private static BattleActionRequest Request(BattleSession session, BattleActionKind kind = BattleActionKind.Wait,
            string targetId = null, int investment = 0)
        {
            BattleSnapshot snapshot = session.GetSnapshot();
            return new BattleActionRequest(snapshot.TurnId, snapshot.CurrentActorId, kind,
                kind == BattleActionKind.Skill ? "attack" : null, targetId, investment);
        }

        private static BattleActionResult Submit(BattleSession session, BattleActionRequest request = null)
        {
            Assert.That(session.TrySubmit(request ?? Request(session), out BattleActionResult result,
                out BattleActionError error), Is.True, error.ToString());
            return result;
        }

        [Test]
        public void StartSortsByMemoryAndPrioritizesPlayerOnTie()
        {
            BattleSession session = Start(null, Unit("slow", BattleTeam.Monster, 1),
                Unit("tie", BattleTeam.Monster, 10), Unit("player", BattleTeam.Player, 10),
                Unit("fast", BattleTeam.Monster, 20));
            CollectionAssert.AreEqual(new[] { "fast", "player", "tie", "slow" }, session.GetSnapshot().TurnOrder);
            Assert.That(session.GetSnapshot().Round, Is.EqualTo(1));
        }

        [Test]
        public void EqualMonsterMemoryUsesSeededRandomOrderAndSnapshotReadsDoNotReroll()
        {
            BattleParticipant[] roster = { Unit("player", BattleTeam.Player, 0),
                Unit("a", BattleTeam.Monster), Unit("b", BattleTeam.Monster), Unit("c", BattleTeam.Monster) };
            var observed = new HashSet<string>();
            for (int seed = 0; seed < 20; seed++)
            {
                var first = new BattleSession(randomSeed: seed, rules: new BattleRules(mechanics: BattleCombatRules.Basic));
                var second = new BattleSession(randomSeed: seed, rules: new BattleRules(mechanics: BattleCombatRules.Basic));
                first.Start(roster);
                second.Start(roster);
                CollectionAssert.AreEqual(first.GetSnapshot().TurnOrder, second.GetSnapshot().TurnOrder);
                string order = string.Join(",", first.GetSnapshot().TurnOrder);
                Assert.That(string.Join(",", first.GetSnapshot().TurnOrder), Is.EqualTo(order));
                observed.Add(order);
            }
            Assert.That(observed.Count, Is.GreaterThan(1));
        }

        [Test]
        public void PresentationMustCompleteBeforeNextActorAndDuplicateCallbacksAreIgnored()
        {
            BattleSession session = Start();
            BattleActionResult result = Submit(session);
            Assert.That(session.GetSnapshot().Phase, Is.EqualTo(BattlePhase.AwaitingPresentation));
            Assert.That(session.GetSnapshot().CurrentActorId, Is.EqualTo("player"));
            Assert.That(session.CompletePresentation(result.ActionId + 1), Is.False);
            Assert.That(session.TrySubmit(result.Request, out _, out BattleActionError error), Is.False);
            Assert.That(error, Is.EqualTo(BattleActionError.InvalidPhase));
            Assert.That(session.CompletePresentation(result.ActionId), Is.True);
            Assert.That(session.GetSnapshot().CurrentActorId, Is.EqualTo("monster"));
            Assert.That(session.CompletePresentation(result.ActionId), Is.False);
            Assert.That(session.TrySubmit(result.Request, out _, out error), Is.False);
            Assert.That(error, Is.EqualTo(BattleActionError.StaleTurn));
        }

        [Test]
        public void FasterUnitDoesNotStarveOthersAndNewRoundStartsAfterEveryoneActs()
        {
            BattleSession session = Start(null, Unit("player", BattleTeam.Player, 100), Unit("monster", BattleTeam.Monster, 1));
            for (int round = 1; round <= 3; round++)
            {
                Assert.That(session.GetSnapshot().Round, Is.EqualTo(round));
                Assert.That(session.GetSnapshot().CurrentActorId, Is.EqualTo("player"));
                session.CompletePresentation(Submit(session).ActionId);
                Assert.That(session.GetSnapshot().CurrentActorId, Is.EqualTo("monster"));
                session.CompletePresentation(Submit(session).ActionId);
            }
        }

        [Test]
        public void MemoryChangesResortOnlyRemainingActors()
        {
            var resolver = new EffectResolver(new BattleEffect("slow", memoryDelta: 80));
            BattleSession session = Start(resolver, Unit("player", BattleTeam.Player, 100),
                Unit("fast", BattleTeam.Monster, 50), Unit("slow", BattleTeam.Monster, 1));
            BattleActionResult result = Submit(session);
            CollectionAssert.AreEqual(new[] { "slow", "fast" }, session.GetSnapshot().TurnOrder);
            session.CompletePresentation(result.ActionId);
            Assert.That(session.GetSnapshot().CurrentActorId, Is.EqualTo("slow"));
        }

        [Test]
        public void SkillDamageProducesImmutableBeforeAfterResult()
        {
            BattleSession session = Start();
            BattleSnapshot before = session.GetSnapshot();
            BattleActionResult result = Submit(session, Request(session, BattleActionKind.Skill, "monster"));
            Assert.That(result.Changes.Single().HpDelta, Is.EqualTo(-25));
            Assert.That(result.Changes.Single().After.Hp, Is.EqualTo(75));
            Assert.That(before.Combatants.Single(state => state.InstanceId == "monster").Hp, Is.EqualTo(100));
            Assert.That(session.PendingResult, Is.SameAs(result));
        }

        [Test]
        public void DeadTargetsAreRemovedFromTurnOrderAndTargetSelection()
        {
            BattleSession session = Start(null, Unit("player", BattleTeam.Player, 100, power: 100),
                Unit("a", BattleTeam.Monster), Unit("b", BattleTeam.Monster));
            BattleActionResult result = Submit(session, Request(session, BattleActionKind.Skill, "a"));
            Assert.That(result.Changes.Single().BecameDead, Is.True);
            CollectionAssert.DoesNotContain(session.GetSnapshot().TurnOrder, "a");
            session.CompletePresentation(result.ActionId);
            session.CompletePresentation(Submit(session).ActionId);
            CollectionAssert.AreEqual(new[] { "b" }, session.GetSelectableTargets("attack"));
            Assert.That(session.TrySubmit(Request(session, BattleActionKind.Skill, "a"), out _, out BattleActionError error), Is.False);
            Assert.That(error, Is.EqualTo(BattleActionError.InvalidTarget));
        }

        [TestCase(BattleTeam.Player, BattleOutcome.Victory)]
        [TestCase(BattleTeam.Monster, BattleOutcome.Defeat)]
        public void LastKillEndsBattleAfterFinalPresentation(BattleTeam attackerTeam, BattleOutcome expected)
        {
            BattleTeam targetTeam = attackerTeam == BattleTeam.Player ? BattleTeam.Monster : BattleTeam.Player;
            BattleSession session = Start(null, Unit("attacker", attackerTeam, 100, power: 500), Unit("target", targetTeam));
            BattleActionResult result = Submit(session, Request(session, BattleActionKind.Skill, "target"));
            Assert.That(result.Outcome, Is.EqualTo(expected));
            Assert.That(result.Changes.Single().After.Hp, Is.Zero);
            Assert.That(session.GetSnapshot().Phase, Is.EqualTo(BattlePhase.AwaitingPresentation));
            session.CompletePresentation(result.ActionId);
            Assert.That(session.GetSnapshot().Phase, Is.EqualTo(BattlePhase.Finished));
            Assert.That(session.GetSnapshot().CurrentActorId, Is.Null);
            Assert.That(session.TrySubmit(result.Request, out _, out _), Is.False);
        }

        [Test]
        public void SimultaneousDeathsProduceDraw()
        {
            BattleSession session = Start(new EffectResolver(new BattleEffect("player", -100), new BattleEffect("monster", -100)));
            Assert.That(Submit(session).Outcome, Is.EqualTo(BattleOutcome.Draw));
        }

        [TestCase(0, 100, BattleOutcome.Defeat)]
        [TestCase(100, 0, BattleOutcome.Victory)]
        [TestCase(0, 0, BattleOutcome.Draw)]
        public void InitialDeadPartyFinishesWithoutSelectingActor(int playerHp, int monsterHp, BattleOutcome outcome)
        {
            BattleSession session = Start(null, Unit("player", BattleTeam.Player, hp: playerHp),
                Unit("monster", BattleTeam.Monster, hp: monsterHp));
            Assert.That(session.GetSnapshot().Outcome, Is.EqualTo(outcome));
            Assert.That(session.GetSnapshot().Phase, Is.EqualTo(BattlePhase.Finished));
            Assert.That(session.GetSnapshot().TurnOrder, Is.Empty);
        }

        [Test]
        public void IncapacitatedActorsEmitSkipResultsAndRecoverWithoutAnInfiniteLoop()
        {
            BattleSession session = Start(null, Unit("player", BattleTeam.Player, skippedTurns: 1),
                Unit("monster", BattleTeam.Monster, skippedTurns: 1));
            Assert.That(session.PendingResult.WasSkipped, Is.True);
            Assert.That(session.PendingResult.Changes.Single().After.SkippedTurns, Is.Zero);
            session.CompletePresentation(session.PendingResult.ActionId);
            Assert.That(session.PendingResult.WasSkipped, Is.True);
            session.CompletePresentation(session.PendingResult.ActionId);
            Assert.That(session.GetSnapshot().Phase, Is.EqualTo(BattlePhase.AwaitingAction));
            Assert.That(session.GetSnapshot().Round, Is.EqualTo(2));
        }

        [TestCase(-1)]
        [TestCase(11)]
        public void OutOfRangeInvestmentIsRejectedWithoutChangingState(int amount)
        {
            BattleSession session = Start();
            Assert.That(session.TrySubmit(Request(session, BattleActionKind.Skill, "monster", amount), out _, out BattleActionError error), Is.False);
            Assert.That(error, Is.EqualTo(BattleActionError.InvalidMemoryInvestment));
            Assert.That(session.GetSnapshot().Combatants.All(state => state.Hp == 100), Is.True);
            Assert.That(session.GetSnapshot().Phase, Is.EqualTo(BattlePhase.AwaitingAction));
        }

        [Test]
        public void UnsupportedPrototypeFeaturesAreExplicitlyRejected()
        {
            BattleSession session = Start(new PrototypeActionResolver());
            Assert.That(session.TrySubmit(Request(session, BattleActionKind.Defend), out _, out BattleActionError error), Is.False);
            Assert.That(error, Is.EqualTo(BattleActionError.UnsupportedAction));
            Assert.That(session.TrySubmit(Request(session, BattleActionKind.Skill, "monster", 1), out _, out error), Is.False);
            Assert.That(error, Is.EqualTo(BattleActionError.UnsupportedAction));
        }

        [Test]
        public void WrongActorUnknownSkillAndFriendlyTargetAreRejected()
        {
            BattleSession session = Start();
            long turn = session.GetSnapshot().TurnId;
            Assert.That(session.TrySubmit(new BattleActionRequest(turn, "monster", BattleActionKind.Wait), out _, out BattleActionError error), Is.False);
            Assert.That(error, Is.EqualTo(BattleActionError.InvalidActor));
            Assert.That(session.TrySubmit(new BattleActionRequest(turn, "player", BattleActionKind.Skill, "missing", "monster"), out _, out error), Is.False);
            Assert.That(error, Is.EqualTo(BattleActionError.UnknownSkill));
            Assert.That(session.TrySubmit(Request(session, BattleActionKind.Skill, "player"), out _, out error), Is.False);
            Assert.That(error, Is.EqualTo(BattleActionError.InvalidTarget));
        }

        [Test]
        public void NullAndMalformedRequestsAreRejected()
        {
            BattleSession session = Start();
            Assert.That(session.TrySubmit(null, out _, out BattleActionError error), Is.False);
            Assert.That(error, Is.EqualTo(BattleActionError.InvalidAction));
            long turn = session.GetSnapshot().TurnId;
            Assert.That(session.TrySubmit(new BattleActionRequest(turn, "player", BattleActionKind.Wait, targetId: "monster"), out _, out error), Is.False);
            Assert.That(error, Is.EqualTo(BattleActionError.InvalidAction));
            Assert.That(session.TrySubmit(new BattleActionRequest(turn, "player", (BattleActionKind)999), out _, out error), Is.False);
        }

        [Test]
        public void ResolverFailureLeavesAllStateUntouched()
        {
            BattleSession session = Start(new EffectResolver(new BattleEffect("monster", -20), new BattleEffect("missing", -20)));
            Assert.Throws<InvalidOperationException>(() => session.TrySubmit(Request(session), out _, out _));
            Assert.That(session.GetSnapshot().Combatants.All(state => state.Hp == 100), Is.True);
            Assert.That(session.GetSnapshot().Phase, Is.EqualTo(BattlePhase.AwaitingAction));
            Assert.That(session.PendingResult, Is.Null);
        }

        [Test]
        public void DuplicateEffectsAreRejectedBeforeAnyMutation()
        {
            BattleSession session = Start(new EffectResolver(new BattleEffect("monster", -20), new BattleEffect("monster", -20)));
            Assert.Throws<InvalidOperationException>(() => session.TrySubmit(Request(session), out _, out _));
            Assert.That(session.GetSnapshot().Combatants.Single(state => state.InstanceId == "monster").Hp, Is.EqualTo(100));
        }

        [Test]
        public void LargeChangesClampWithoutIntegerOverflow()
        {
            BattleSession session = Start(new EffectResolver(new BattleEffect("player", int.MaxValue, int.MaxValue),
                new BattleEffect("monster", int.MinValue, int.MinValue)));
            BattleActionResult result = Submit(session);
            Assert.That(result.Changes[0].After.Hp, Is.EqualTo(100));
            Assert.That(result.Changes[0].After.Memory, Is.EqualTo(100));
            Assert.That(result.Changes[1].After.Hp, Is.Zero);
            Assert.That(result.Changes[1].After.Memory, Is.Zero);
        }

        [Test]
        public void DefinitionsAreSharedButRuntimeStateIsIndependent()
        {
            var skills = new List<SkillData> { new SkillData("attack", "공격", 10) };
            var data = new CombatantData("slime", "슬라임", BattleTeam.Monster, 100, 100, 10, skills);
            skills.Clear();
            BattleSession session = Start(new EffectResolver(new BattleEffect("a", -10)),
                Unit("player", BattleTeam.Player), new BattleParticipant("a", data), new BattleParticipant("b", data));
            Submit(session);
            Assert.That(data.Skills.Count, Is.EqualTo(1));
            Assert.That(session.GetSnapshot().Combatants.Single(state => state.InstanceId == "b").Hp, Is.EqualTo(100));
        }

        [Test]
        public void InvalidRosterDoesNotPartiallyStartSession()
        {
            var session = new BattleSession(rules: new BattleRules(mechanics: BattleCombatRules.Basic));
            Assert.Throws<ArgumentException>(() => session.Start(new[] { Unit("same", BattleTeam.Player), Unit("same", BattleTeam.Monster) }));
            Assert.That(session.GetSnapshot().Combatants, Is.Empty);
            Assert.Throws<ArgumentException>(() => session.Start(new[] { Unit("player", BattleTeam.Player) }));
            session.Start(new[] { Unit("player", BattleTeam.Player), Unit("monster", BattleTeam.Monster) });
            Assert.Throws<InvalidOperationException>(() => session.Start(Array.Empty<BattleParticipant>()));
        }

        [Test]
        public void EntryConditionIsPreservedWithoutInventingBonusMemory()
        {
            var session = new BattleSession(rules: new BattleRules(mechanics: BattleCombatRules.Basic));
            session.Start(new[] { Unit("player", BattleTeam.Player), Unit("monster", BattleTeam.Monster) }, BattleEntryCondition.MonsterCollision);
            Assert.That(session.GetSnapshot().EntryCondition, Is.EqualTo(BattleEntryCondition.MonsterCollision));
            Assert.That(session.GetSnapshot().Combatants.All(state => state.Memory == 10), Is.True);
        }

        [Test]
        public void InvalidDefinitionValuesFailEarly()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SkillData("bad", "잘못된 스킬", -1));
            Assert.Throws<ArgumentException>(() => new CombatantData("bad", "잘못된 정의", BattleTeam.Player,
                100, 10, 10, new[] { new SkillData("same", "스킬", 1), new SkillData("same", "스킬", 1) }));
            Assert.Throws<ArgumentOutOfRangeException>(() => Unit("bad", BattleTeam.Player, hp: -1));
        }

        private sealed class EffectResolver : IBattleActionResolver
        {
            private readonly BattleEffect[] _effects;

            public EffectResolver(params BattleEffect[] effects)
            {
                _effects = effects;
            }

            public BattleActionError Resolve(BattleSnapshot snapshot, BattleActionRequest request,
                out IReadOnlyList<BattleEffect> effects)
            {
                effects = _effects;
                return BattleActionError.None;
            }
        }
    }
}

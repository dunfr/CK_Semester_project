using System.Linq;
using NUnit.Framework;

namespace CK.SemesterProject.Battle.Tests
{
    public sealed class MemoryDepletionTests
    {
        private static BattleSession Start(BattleEntryCondition entry = BattleEntryCondition.Normal,
            int playerMemory = 5, int playerBaseline = 100, bool enableMemoryLoss = true,
            SkillData playerSkill = null, BattleElement[] weaknessChain = null)
        {
            var player = new CombatantData("p", "플레이어", BattleTeam.Player, 1000, 100, playerBaseline,
                new[] { playerSkill ?? new SkillData("hit", "공격", 1) });
            var monster = new CombatantData("m", "망각", BattleTeam.Monster, 1000, 100, 10,
                new[] { new SkillData("steal", "망각 공격", 20, BattleElement.Oblivion) },
                weaknessChain: weaknessChain);
            var rules = new BattleRules(mechanics: new BattleCombatRules(criticalChance: 0),
                enableMemoryLoss: enableMemoryLoss);
            var session = new BattleSession(randomSeed: 10, rules: rules);
            session.Start(new[]
            {
                new BattleParticipant("p", player, initialMemory: playerMemory),
                new BattleParticipant("m", monster)
            }, entry);
            return session;
        }

        private static BattleActionResult Submit(BattleSession session, BattleActionKind kind,
            string skill = null, string target = null)
        {
            BattleSnapshot snapshot = session.GetSnapshot();
            Assert.That(session.TrySubmit(new BattleActionRequest(snapshot.TurnId,
                snapshot.CurrentActorId, kind, skill, target), out BattleActionResult result,
                out BattleActionError error), Is.True, error.ToString());
            return result;
        }

        private static void Finish(BattleSession session)
        {
            Assert.That(session.CompletePresentation(session.PendingResult.ActionId), Is.True);
        }

        private static CombatantState Player(BattleSession session)
        {
            return session.GetSnapshot().Combatants.Single(unit => unit.InstanceId == "p");
        }

        [Test]
        public void DefendedPlayerDepletedByOblivionSkipsRemainingActionInSameRound()
        {
            BattleSession session = Start(BattleEntryCondition.PlayerInitiated);
            Submit(session, BattleActionKind.Defend);
            Finish(session);
            Assert.That(session.GetSnapshot().CurrentActorId, Is.EqualTo("m"));
            BattleActionResult hit = Submit(session, BattleActionKind.Skill, "steal", "p");
            Assert.That(hit.Hit.DefenseMultiplier, Is.EqualTo(0.5));
            Assert.That(Player(session).Memory, Is.Zero);
            Finish(session);

            Assert.That(session.GetSnapshot().Round, Is.EqualTo(1));
            Assert.That(session.PendingResult.Request.ActorId, Is.EqualTo("p"));
            Assert.That(session.PendingResult.IsTurnStartEffect, Is.True);
            Assert.That(session.PendingResult.WasSkipped, Is.True);
            Assert.That(session.PendingResult.Changes.Single().MemoryDelta, Is.EqualTo(100));
            Assert.That(Player(session).IsDefending, Is.False);
            Assert.That(Player(session).Memory, Is.EqualTo(100));
            Assert.That(session.ValidateRequest(new BattleActionRequest(session.GetSnapshot().TurnId,
                "p", BattleActionKind.Defend)), Is.EqualTo(BattleActionError.InvalidPhase));

            Finish(session);
            Assert.That(session.GetSnapshot().Round, Is.EqualTo(2));
            Assert.That(session.GetSnapshot().CurrentActorId, Is.EqualTo("p"));
            Assert.That(Player(session).CanAct, Is.True);
            Assert.That(session.GetActionCount("p"), Is.EqualTo(1));
        }

        [Test]
        public void DepletionBeforeFirstPlayerActionDoesNotAllowFreeWaitOrDefense()
        {
            BattleSession session = Start();
            Submit(session, BattleActionKind.Skill, "steal", "p");
            Finish(session);

            Assert.That(session.GetSnapshot().Round, Is.EqualTo(1));
            Assert.That(session.GetSnapshot().Phase, Is.EqualTo(BattlePhase.AwaitingPresentation));
            Assert.That(session.PendingResult.WasSkipped, Is.True);
            Assert.That(session.PendingResult.Request.ActorId, Is.EqualTo("p"));
            Assert.That(Player(session).Memory, Is.EqualTo(100));
            Assert.That(session.GetActionCount("p"), Is.Zero);
        }

        [Test]
        public void ZeroMemoryOpeningBonusIsConsumedOnceAndRecoversForNormalAction()
        {
            BattleSession session = Start(BattleEntryCondition.PlayerInitiated, playerMemory: 0);
            Assert.That(session.PendingResult.WasSkipped, Is.True);
            Assert.That(session.PendingResult.Request.ActorId, Is.EqualTo("p"));
            Assert.That(Player(session).Memory, Is.EqualTo(100));
            Finish(session);

            Assert.That(session.GetSnapshot().Round, Is.EqualTo(1));
            Assert.That(session.GetSnapshot().CurrentActorId, Is.EqualTo("p"));
            Assert.That(session.GetSnapshot().Phase, Is.EqualTo(BattlePhase.AwaitingAction));
            Assert.That(session.PendingResult, Is.Null);
            Assert.That(Player(session).CanAct, Is.True);
        }

        [Test]
        public void DepletedWeaknessChainBonusSkipsBeforeOtherRemainingActors()
        {
            BattleElement[] chain = Enumerable.Repeat(BattleElement.Afterimage, 4).ToArray();
            var free = new SkillData("hit", "잔상", 1, BattleElement.Afterimage);
            BattleSession session = Start(playerMemory: 20, playerSkill: free, weaknessChain: chain);
            for (int step = 0; step < 3; step++)
            {
                Submit(session, BattleActionKind.Skill, "hit", "m");
                Finish(session);
                Submit(session, BattleActionKind.Wait);
                Finish(session);
            }
            // 마지막 단계에서 투자 비용으로 남은 메모리를 모두 소모한다.
            BattleSnapshot snapshot = session.GetSnapshot();
            Assert.That(session.TrySubmit(new BattleActionRequest(snapshot.TurnId, "p",
                BattleActionKind.Skill, "hit", "m", investmentStage: 2), out BattleActionResult result,
                out _), Is.True);
            Assert.That(result.Hit.GrantsExtraAction, Is.True);
            Assert.That(Player(session).Memory, Is.Zero);
            Finish(session);

            Assert.That(session.PendingResult.WasSkipped, Is.True);
            Assert.That(session.PendingResult.Request.ActorId, Is.EqualTo("p"));
            Assert.That(Player(session).Memory, Is.EqualTo(100));
            Assert.That(Player(session).HasAfterimageRecovery, Is.False);
            Finish(session);
            Assert.That(session.GetSnapshot().CurrentActorId, Is.EqualTo("m"));
        }

        [TestCase(false, 100)]
        [TestCase(true, 0)]
        public void DisabledDepletionOrZeroBaselineKeepsFreeActionsAvailable(bool enableMemoryLoss, int baseline)
        {
            BattleSession session = Start(playerMemory: 0, playerBaseline: baseline,
                enableMemoryLoss: enableMemoryLoss);
            Submit(session, BattleActionKind.Wait);
            Finish(session);

            Assert.That(session.GetSnapshot().CurrentActorId, Is.EqualTo("p"));
            Assert.That(session.PendingResult, Is.Null);
            Assert.That(Player(session).Memory, Is.Zero);
            Assert.That(Player(session).CanAct, Is.True);
            Submit(session, BattleActionKind.Defend);
        }
    }
}

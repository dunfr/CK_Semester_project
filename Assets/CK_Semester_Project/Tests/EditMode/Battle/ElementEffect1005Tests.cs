using System;
using System.Linq;
using NUnit.Framework;

namespace CK.SemesterProject.Battle.Tests
{
    public sealed class ElementEffect1005Tests
    {
        [TestCase(0, 0)]
        [TestCase(1, 0)]
        [TestCase(8, 0)]
        [TestCase(9, 0)]
        [TestCase(10, 0)]
        [TestCase(11, 1)]
        public void PlayerOblivionRecoversTenFromLowOrEmptyMonsterMemoryAfterPayingCost(int targetMemory, int remaining)
        {
            var skill = new SkillData("steal", "망각", 0, BattleElement.Oblivion, memoryCost: 20);
            BattleSession session = CreateTransferScenario(BattleTeam.Player, 100, targetMemory, skill);
            Assert.That(session.TrySubmit(new BattleActionRequest(session.GetSnapshot().TurnId,
                "actor", BattleActionKind.Skill, "steal", "target"), out BattleActionResult result, out _), Is.True);
            Assert.That(session.GetSnapshot().Combatants.Single(unit => unit.InstanceId == "actor").Memory, Is.EqualTo(90));
            Assert.That(session.GetSnapshot().Combatants.Single(unit => unit.InstanceId == "target").Memory, Is.EqualTo(remaining));
            Assert.That(result.Changes.Single(change => change.After.InstanceId == "actor").MemoryDelta, Is.EqualTo(-10));
        }

        [TestCase(0, true)]
        [TestCase(0, false)]
        [TestCase(8, true)]
        [TestCase(8, false)]
        public void LowMemoryMinimumRecoveryDoesNotApplyOnMissOrWhenEffectsDisabled(int targetMemory, bool enableEffects)
        {
            var skill = new SkillData("steal", "망각", 0, BattleElement.Oblivion, memoryCost: 20,
                accuracy: enableEffects ? 0 : 1);
            BattleSession session = CreateTransferScenario(BattleTeam.Player, 100, targetMemory, skill, enableEffects);
            Assert.That(session.TrySubmit(new BattleActionRequest(session.GetSnapshot().TurnId,
                "actor", BattleActionKind.Skill, "steal", "target"), out _, out _), Is.True);
            Assert.That(session.GetSnapshot().Combatants.Single(unit => unit.InstanceId == "actor").Memory, Is.EqualTo(80));
            Assert.That(session.GetSnapshot().Combatants.Single(unit => unit.InstanceId == "target").Memory, Is.EqualTo(targetMemory));
        }

        [TestCase(false, 200, 0)]
        [TestCase(true, 190, 1)]
        public void AreaOblivionAppliesMinimumToEachHitAndPaysCostsOnce(bool dodgeSecond, int memory, int secondMemory)
        {
            var skill = new SkillData("area", "광역 망각", 0, BattleElement.Oblivion, memoryCost: 30,
                rageCost: 50, isAreaOfEffect: true, criticalChance: 0);
            var player = new CombatantData("p", "플레이어", BattleTeam.Player, 1000, 200, 200, new[] { skill });
            var monster = new CombatantData("m", "몬스터", BattleTeam.Monster, 1000, 100, 100, Array.Empty<SkillData>());
            var second = new CombatantData("second", "두번째", BattleTeam.Monster, 1000, 100, 100,
                Array.Empty<SkillData>(), evasion: dodgeSecond ? 1 : 0);
            var session = new BattleSession(rules: new BattleRules(enableMemoryLoss: false));
            session.Start(new[]
            {
                new BattleParticipant("p", player, initialRageEnergy: 80),
                new BattleParticipant("a", monster, initialMemory: 8),
                new BattleParticipant("b", second, initialMemory: 1),
                new BattleParticipant("c", monster, initialMemory: 0)
            });
            Assert.That(session.TrySubmit(new BattleActionRequest(session.GetSnapshot().TurnId,
                "p", BattleActionKind.Skill, "area", "a"), out BattleActionResult result, out _), Is.True);
            var snapshot = session.GetSnapshot();
            Assert.That(snapshot.Combatants.Single(unit => unit.InstanceId == "p").Memory, Is.EqualTo(memory));
            Assert.That(snapshot.Combatants.Single(unit => unit.InstanceId == "p").RageEnergy, Is.EqualTo(30));
            Assert.That(snapshot.Combatants.Single(unit => unit.InstanceId == "a").Memory, Is.Zero);
            Assert.That(snapshot.Combatants.Single(unit => unit.InstanceId == "b").Memory, Is.EqualTo(secondMemory));
            Assert.That(snapshot.Combatants.Single(unit => unit.InstanceId == "c").Memory, Is.Zero);
            Assert.That(result.TargetHits["b"].IsHit, Is.EqualTo(!dodgeSecond));
        }

        private static BattleSession CreateTransferScenario(BattleTeam actorTeam, int actorMemory,
            int targetMemory, SkillData skill, bool enableEffects = true)
        {
            var actor = new CombatantData("actor", "행동자", actorTeam, 1000, 100, actorMemory, new[] { skill });
            var target = new CombatantData("target", "대상",
                actorTeam == BattleTeam.Player ? BattleTeam.Monster : BattleTeam.Player,
                1000, 100, targetMemory, Array.Empty<SkillData>());
            var session = new BattleSession(randomSeed: 7, rules: new BattleRules(enableMemoryLoss: false,
                mechanics: new BattleCombatRules(criticalChance: 0, enableElementEffects: enableEffects)));
            session.Start(new[] { new BattleParticipant("actor", actor), new BattleParticipant("target", target) },
                actorTeam == BattleTeam.Player ? BattleEntryCondition.PlayerInitiated : BattleEntryCondition.MonsterCollision,
                "actor");
            return session;
        }

        [TestCase(BattleTeam.Player, 50, 20, 0, 10, 10)]
        [TestCase(BattleTeam.Monster, 50, 20, 0, 10, 10)]
        [TestCase(BattleTeam.Player, 50, 7, 0, 7, 10)]
        [TestCase(BattleTeam.Monster, 50, 7, 0, 7, 7)]
        [TestCase(BattleTeam.Player, 99, 20, 0, 1, 1)]
        [TestCase(BattleTeam.Monster, 99, 20, 0, 1, 1)]
        [TestCase(BattleTeam.Player, 100, 20, 0, 0, 0)]
        [TestCase(BattleTeam.Monster, 100, 20, 0, 0, 0)]
        [TestCase(BattleTeam.Player, 50, 0, 0, 0, 10)]
        [TestCase(BattleTeam.Monster, 50, 0, 0, 0, 0)]
        [TestCase(BattleTeam.Player, 100, 20, 5, 5, 5)]
        [TestCase(BattleTeam.Monster, 100, 20, 5, 5, 5)]
        [TestCase(BattleTeam.Player, 100, 20, 10, 10, 10)]
        [TestCase(BattleTeam.Monster, 100, 20, 10, 10, 10)]
        public void OblivionUsesPlayerMinimumRecoveryAndPreservesMonsterTransferRules(BattleTeam actorTeam,
            int actorMemory, int targetMemory, int cost, int stolen, int received)
        {
            var skill = new SkillData("steal", "망각", 0, BattleElement.Oblivion, memoryCost: cost);
            BattleSession session = CreateTransferScenario(actorTeam, actorMemory, targetMemory, skill);
            Assert.That(session.TrySubmit(new BattleActionRequest(session.GetSnapshot().TurnId,
                "actor", BattleActionKind.Skill, "steal", "target"), out BattleActionResult result,
                out BattleActionError error), Is.True, error.ToString());
            CombatantState actor = session.GetSnapshot().Combatants.Single(unit => unit.InstanceId == "actor");
            CombatantState target = session.GetSnapshot().Combatants.Single(unit => unit.InstanceId == "target");
            Assert.That(actor.Memory, Is.EqualTo(actorMemory - cost + received));
            Assert.That(target.Memory, Is.EqualTo(targetMemory - stolen));
            Assert.That(actor.Memory + target.Memory, Is.EqualTo(actorMemory + targetMemory - cost + received - stolen));
            Assert.That(result.Changes.Single(change => change.After.InstanceId == "target").MemoryDelta,
                Is.EqualTo(-stolen));
        }

        [TestCase(BattleElement.Oblivion, true, 0)]
        [TestCase(BattleElement.Oblivion, false, 1)]
        [TestCase(BattleElement.Imprint, true, 0)]
        [TestCase(BattleElement.Imprint, false, 1)]
        public void MissOrDisabledElementEffectsDoesNotApplyUpdatedEffect(BattleElement element,
            bool enableEffects, double accuracy)
        {
            var skill = new SkillData("hit", "공격", 100, element, memoryCost: 5,
                accuracy: accuracy, criticalChance: 0);
            BattleSession session = CreateTransferScenario(BattleTeam.Player, 50, 20, skill, enableEffects);
            Assert.That(session.TrySubmit(new BattleActionRequest(session.GetSnapshot().TurnId,
                "actor", BattleActionKind.Skill, "hit", "target"), out _, out BattleActionError error),
                Is.True, error.ToString());
            BattleSnapshot snapshot = session.GetSnapshot();
            Assert.That(snapshot.Combatants.Single(unit => unit.InstanceId == "actor").Memory, Is.EqualTo(45));
            CombatantState target = snapshot.Combatants.Single(unit => unit.InstanceId == "target");
            Assert.That(target.Memory, Is.EqualTo(20));
            Assert.That(target.ImprintDamage, Is.Zero);
        }

        [TestCase(BattleTeam.Player, 108, 22)]
        [TestCase(BattleTeam.Monster, 90, 18)]
        public void ImprintUsesTwentyPercentOfFinalSkillDamageOnceAtVictimsNextAction(BattleTeam actorTeam,
            int skillDamage, int delayedDamage)
        {
            var skill = new SkillData("imprint", "각인", 100, BattleElement.Imprint, criticalChance: 1);
            int actorMemory = actorTeam == BattleTeam.Player ? 50 : 90;
            int targetMemory = actorTeam == BattleTeam.Player ? 90 : 100;
            var actor = new CombatantData("actor", "행동자", actorTeam, 1000, 100, actorMemory, new[] { skill });
            var target = new CombatantData("target", "대상",
                actorTeam == BattleTeam.Player ? BattleTeam.Monster : BattleTeam.Player,
                1000, 100, targetMemory, Array.Empty<SkillData>(), BattleElement.Oblivion);
            var session = new BattleSession(randomSeed: 7, rules: new BattleRules(enableMemoryLoss: false));
            session.Start(new[] { new BattleParticipant("actor", actor,
                initialRageEnergy: actorTeam == BattleTeam.Player ? 45 : 0), new BattleParticipant("target", target) });
            Assert.That(session.GetSnapshot().CurrentActorId, Is.EqualTo("target"));
            SubmitAndFinish(session, new BattleActionRequest(session.GetSnapshot().TurnId,
                "target", BattleActionKind.Defend));
            Assert.That(session.GetSnapshot().CurrentActorId, Is.EqualTo("actor"));
            Assert.That(session.TrySubmit(new BattleActionRequest(session.GetSnapshot().TurnId,
                "actor", BattleActionKind.Skill, "imprint", "target", investmentStage: 2),
                out BattleActionResult hit, out BattleActionError error), Is.True, error.ToString());
            Assert.That(hit.Hit.Damage, Is.EqualTo(skillDamage));
            Assert.That(hit.Hit.IsCritical, Is.True);
            Assert.That(hit.Hit.ElementMultiplier, Is.EqualTo(1.25));
            Assert.That(hit.Hit.InvestmentMultiplier, Is.EqualTo(1.2));
            Assert.That(hit.Hit.DefenseMultiplier, Is.EqualTo(0.5));
            CombatantState marked = session.GetSnapshot().Combatants.Single(unit => unit.InstanceId == "target");
            Assert.That(marked.Hp, Is.EqualTo(1000 - skillDamage));
            Assert.That(marked.ImprintDamage, Is.EqualTo(delayedDamage));
            Assert.That(session.CompletePresentation(hit.ActionId), Is.True);
            BattleActionResult tick = session.PendingResult;
            Assert.That(tick.IsTurnStartEffect, Is.True);
            Assert.That(tick.WasSkipped, Is.False);
            Assert.That(tick.Changes.Single().After.InstanceId, Is.EqualTo("target"));
            Assert.That(tick.Changes.Single().HpDelta, Is.EqualTo(-delayedDamage));
            Assert.That(tick.Changes.Single().After.ImprintDamage, Is.Zero);
            Assert.That(session.CompletePresentation(tick.ActionId), Is.True);
            Assert.That(session.GetSnapshot().CurrentActorId, Is.EqualTo("target"));
            SubmitAndFinish(session, new BattleActionRequest(session.GetSnapshot().TurnId,
                "target", BattleActionKind.Wait));
            AdvanceToActor(session, "target");
            CombatantState next = session.GetSnapshot().Combatants.Single(unit => unit.InstanceId == "target");
            Assert.That(next.Hp, Is.EqualTo(1000 - skillDamage - delayedDamage));
            Assert.That(next.ImprintDamage, Is.Zero);
            Assert.That(session.History.Count(entry => entry.Result.IsTurnStartEffect
                && entry.Result.Changes.Any(change => change.Before.ImprintDamage > 0)), Is.EqualTo(1));
        }

        private static void SubmitAndFinish(BattleSession session, BattleActionRequest request)
        {
            Assert.That(session.TrySubmit(request, out BattleActionResult result, out BattleActionError error),
                Is.True, error.ToString());
            Assert.That(session.CompletePresentation(result.ActionId), Is.True);
        }

        private static void AdvanceToActor(BattleSession session, string actorId)
        {
            for (int count = 0; count < 8; count++)
            {
                BattleSnapshot snapshot = session.GetSnapshot();
                if (snapshot.Phase == BattlePhase.AwaitingAction && snapshot.CurrentActorId == actorId)
                {
                    return;
                }
                Assert.That(snapshot.Phase, Is.EqualTo(BattlePhase.AwaitingAction),
                    "각인 효과가 반복 적용되어 행동 시작 연출을 기다리고 있습니다.");
                SubmitAndFinish(session, new BattleActionRequest(snapshot.TurnId,
                    snapshot.CurrentActorId, BattleActionKind.Wait));
            }
            Assert.Fail("대상의 다음 행동에 도달하지 못했습니다.");
        }
    }
}

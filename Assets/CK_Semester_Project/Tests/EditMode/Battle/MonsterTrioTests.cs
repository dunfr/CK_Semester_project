using System;
using System.Linq;
using NUnit.Framework;

namespace CK.SemesterProject.Battle.Tests
{
    public sealed class MonsterTrioTests
    {
        private static readonly BattleElement I = BattleElement.Imprint;
        private static readonly BattleElement A = BattleElement.Afterimage;
        private static readonly BattleElement O = BattleElement.Oblivion;

        private static BattleSession Create(BattleElement[] elements, int playerHp = 10000,
            int playerRage = 0, int memory = 1000, int seed = 7, string initiator = null, int playerMemory = 1000)
        {
            var player = new CombatantData("p", "Player", BattleTeam.Player, 10000, 1000, 1000,
                new[] { new SkillData("hit", "Hit", 400, criticalChance: 0),
                    new SkillData("kill", "Kill", 20000, criticalChance: 0),
                    new SkillData("miss", "Miss", 100, accuracy: 0) });
            var roster = new System.Collections.Generic.List<BattleParticipant>
            {
                new BattleParticipant("p", player, initialHp: playerHp, initialRageEnergy: playerRage, initialMemory: playerMemory)
            };
            for (int n = 0; n < elements.Length; n++)
            {
                BattleElement element = elements[n];
                roster.Add(new BattleParticipant("m" + n,
                    new CombatantData("monster", "Monster", BattleTeam.Monster, 1000, 1000, 1000,
                        new[] { new SkillData("attack", "Attack", 1, element, criticalChance: 0) }, element,
                        monsterProfile: MonsterBehaviorProfile.CreateDefault(element)), initialMemory: memory));
            }
            var session = new BattleSession(randomSeed: seed);
            session.Start(roster, initiator == null ? BattleEntryCondition.Normal : BattleEntryCondition.MonsterCollision, initiator);
            return session;
        }

        private static void Advance(BattleSession session, string id)
        {
            for (int n = 0; n < 20; n++)
            {
                BattleSnapshot state = session.GetSnapshot();
                if (state.Phase == BattlePhase.AwaitingAction && state.CurrentActorId == id)
                {
                    return;
                }
                if (session.PendingResult != null)
                {
                    session.CompletePresentation(session.PendingResult.ActionId);
                    continue;
                }
                Assert.That(session.TrySubmit(new BattleActionRequest(state.TurnId, state.CurrentActorId, BattleActionKind.Wait),
                    out BattleActionResult result, out _), Is.True);
                session.CompletePresentation(result.ActionId);
            }
            Assert.Fail("Actor did not receive a turn: " + id);
        }

        private static void Hit(BattleSession session, string target, string skill = "hit")
        {
            Advance(session, "p");
            Assert.That(session.TrySubmit(new BattleActionRequest(session.GetSnapshot().TurnId, "p", BattleActionKind.Skill,
                skill, target), out BattleActionResult result, out _), Is.True);
            session.CompletePresentation(result.ActionId);
        }

        private static BattleActionRequest Choose(BattleSession session, string actor, int seed = 0)
        {
            Advance(session, actor);
            Assert.That(new MonsterAi(randomSeed: seed).TryChooseAction(session, out BattleActionRequest request), Is.True);
            Assert.That(session.ValidateRequest(request), Is.EqualTo(BattleActionError.None));
            return request;
        }

        [TestCase(4000, 7)]
        [TestCase(4001, 5)]
        public void MixedImprintFinisherBoundary(int hp, int minimum)
        {
            BattleSession session = Create(new[] { I, A, O }, playerHp: hp);
            for (int seed = 0; seed < 30; seed++)
            {
                Assert.That(Choose(session, "m0", seed).InvestmentStage.Value, Is.InRange(minimum, 7));
            }
        }

        [Test]
        public void TwoImprintsChooseOneStableAttackerAndPromoteSurvivor()
        {
            BattleSession session = Create(new[] { I, I, O });
            string attacker = session.GetSnapshot().AttackImprintId;
            Assert.That(new[] { "m0", "m1" }, Does.Contain(attacker));
            string waiting = attacker == "m0" ? "m1" : "m0";
            Assert.That(Choose(session, waiting).InvestmentStage.Value, Is.InRange(2, 3));
            Assert.That(session.GetSnapshot().AttackImprintId, Is.EqualTo(attacker));
            Hit(session, attacker, "kill");
            Assert.That(Choose(session, waiting).InvestmentStage.Value, Is.InRange(6, 7));
        }

        [Test]
        public void ThreeImprintsWaitForActualHitAndReassignAfterDeath()
        {
            BattleSession session = Create(new[] { I, I, I });
            Assert.That(session.GetSnapshot().AttackImprintId, Is.Null);
            Hit(session, "m0", "miss");
            Assert.That(session.GetSnapshot().AttackImprintId, Is.Null);
            Hit(session, "m1");
            Assert.That(session.GetSnapshot().AttackImprintId, Is.EqualTo("m1"));
            Hit(session, "m0");
            Assert.That(session.GetSnapshot().AttackImprintId, Is.EqualTo("m1"));
            Hit(session, "m1", "kill");
            Assert.That(MonsterTrioAi.IsWaitingImprint(session.GetSnapshot(), "m2"), Is.True);
            Hit(session, "m2");
            Assert.That(session.GetSnapshot().AttackImprintId, Is.EqualTo("m2"));
        }

        [Test]
        public void WaitingImprintMayUseStageOneButAttackerCannot()
        {
            BattleSession session = Create(new[] { I, I, I }, memory: 150);
            BattleActionRequest waiting = Choose(session, "m0");
            Assert.That(waiting.InvestmentStage, Is.EqualTo(1));
            Assert.That(session.TrySubmit(waiting, out BattleActionResult result, out _), Is.True);
            session.CompletePresentation(result.ActionId);
            Hit(session, "m1");
            Assert.That(Choose(session, "m1").InvestmentStage, Is.EqualTo(0));
        }

        [Test]
        public void WeightedAfterimageChoiceIsStableAndDowngradesWhenUnaffordable()
        {
            BattleSession session = Create(new[] { I, A, O });
            var counts = new int[4];
            for (int seed = 0; seed < 1000; seed++)
            {
                BattleActionRequest request = Choose(session, "m1", seed);
                counts[request.InvestmentStage.Value]++;
                Assert.That(Choose(session, "m1", seed).InvestmentStage, Is.EqualTo(request.InvestmentStage));
            }
            Assert.That(counts[1], Is.InRange(450, 550));
            Assert.That(counts[2], Is.InRange(250, 350));
            Assert.That(counts[3], Is.InRange(150, 250));
            BattleSession low = Create(new[] { I, A, O }, memory: 50);
            for (int seed = 0; seed < 30; seed++)
            {
                Assert.That(Choose(low, "m1", seed).InvestmentStage, Is.EqualTo(1));
            }
        }

        [Test]
        public void ThreeAfterimagesOpenUninvestedAndDefenseFollowsLastHit()
        {
            BattleSession session = Create(new[] { A, A, A }, playerMemory: 999);
            BattleSnapshot opening = session.GetSnapshot();
            Assert.That(opening.CurrentActorId, Is.Not.EqualTo("p"));
            for (int n = 0; n < 3; n++)
            {
                BattleActionRequest first = Choose(session, session.GetSnapshot().CurrentActorId);
                Assert.That(first.Kind, Is.EqualTo(BattleActionKind.Skill));
                Assert.That(first.InvestmentStage, Is.EqualTo(0));
                session.TrySubmit(first, out BattleActionResult result, out _);
                session.CompletePresentation(result.ActionId);
            }
            Hit(session, "m0");
            Assert.That(Choose(session, "m0").Kind, Is.EqualTo(BattleActionKind.Defend));
            Hit(session, "m1");
            Assert.That(Choose(session, "m0").Kind, Is.EqualTo(BattleActionKind.Skill));
            Assert.That(Choose(session, "m1").Kind, Is.EqualTo(BattleActionKind.Defend));
        }

        [Test]
        public void TwoOblivionsTrackDamageIndependently()
        {
            BattleSession session = Create(new[] { O, O, A }, playerRage: 80, playerMemory: 999);
            Hit(session, "m0");
            Assert.That(Choose(session, "m0").Kind, Is.EqualTo(BattleActionKind.Defend));
            Assert.That(Choose(session, "m1").Kind, Is.EqualTo(BattleActionKind.Skill));
            Assert.That(session.GetSnapshot().DamagedByPlayer, Is.EquivalentTo(new[] { "m0" }));
        }

        [Test]
        public void TwoAfterimagesUnlockCostTableAfterOtherElementDies()
        {
            BattleSession session = Create(new[] { A, A, I }, playerMemory: 999);
            Hit(session, "m0");
            for (int seed = 0; seed < 10; seed++)
            {
                Assert.That(Choose(session, "m0", seed).Kind, Is.EqualTo(BattleActionKind.Skill));
            }
            Hit(session, "m2", "kill");
            BattleActionRequest defense = null;
            for (int seed = 0; seed < 40; seed++)
            {
                BattleActionRequest candidate = Choose(session, "m0", seed);
                if (candidate.Kind == BattleActionKind.Defend) { defense = candidate; break; }
            }
            Assert.That(defense, Is.Not.Null);
            int before = session.GetSnapshot().Combatants.First(unit => unit.InstanceId == "m0").Memory;
            Assert.That(session.TrySubmit(defense, out _, out _), Is.True);
            int after = session.GetSnapshot().Combatants.First(unit => unit.InstanceId == "m0").Memory;
            Assert.That(before - after, Is.EqualTo(defense.InvestmentStage.Value * 100));
        }

        [Test]
        public void ThreeOblivionsDefendAtRageFour()
        {
            BattleSession session = Create(new[] { O, O, O }, playerRage: 80, playerMemory: 999);
            for (int n = 0; n < 3; n++)
            {
                BattleActionRequest request = Choose(session, "m" + n);
                Assert.That(request.Kind, Is.EqualTo(BattleActionKind.Defend));
                Assert.That(request.InvestmentStage.Value, Is.InRange(2, 3));
            }
        }

        [Test]
        public void EveryTrioReturnsLegalActionsAcrossLowMemoryAndPreservesOverheatRule()
        {
            BattleElement[][] rosters = { new[] { I, A, O }, new[] { I, I, A }, new[] { I, I, O },
                new[] { A, A, I }, new[] { A, A, O }, new[] { O, O, A }, new[] { O, O, I },
                new[] { I, I, I }, new[] { A, A, A }, new[] { O, O, O } };
            foreach (BattleElement[] roster in rosters)
            foreach (int memory in new[] { 0, 49, 100, 200, 1000 })
            {
                BattleSession session = Create(roster, memory: memory);
                for (int n = 0; n < 3; n++) { Choose(session, "m" + n); }
            }
            // 플레이어가 행동 불능을 소모하기 전 모든 속성의 방어 금지를 확인한다.
            foreach (BattleElement[] roster in rosters)
            for (int n = 0; n < 3; n++)
            {
                BattleSession session = Create(roster, playerRage: 100, initiator: "m" + n);
                Assert.That(session.GetSnapshot().Combatants.First(unit => unit.InstanceId == "p").IsOverheated, Is.True);
                Assert.That(Choose(session, "m" + n).Kind, Is.Not.EqualTo(BattleActionKind.Defend));
            }
        }
    }
}

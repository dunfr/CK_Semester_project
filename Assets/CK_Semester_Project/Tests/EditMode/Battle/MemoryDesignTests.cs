using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace CK.SemesterProject.Battle.Tests
{
    public sealed class MemoryDesignTests
    {
        private static BattleSession Start(SkillData skill = null, int baseline = 100, int? memory = null,
            int rage = 80, IBattleActionResolver resolver = null)
        {
            var player = new CombatantData("p", "P", BattleTeam.Player, 1000, 200, baseline,
                new[] { skill ?? new SkillData("hit", "Hit", 100, memoryCost: 10) });
            var monster = new CombatantData("m", "M", BattleTeam.Monster, 1000, 200, 5,
                new[] { new SkillData("hit", "Hit", 1) });
            var session = new BattleSession(resolver, 10, new BattleRules(mechanics: BattleCombatRules.Basic));
            session.Start(new[] { new BattleParticipant("p", player, initialMemory: memory, initialRageEnergy: rage),
                new BattleParticipant("m", monster) });
            return session;
        }

        private static BattleActionRequest Request(BattleSession session, BattleActionKind kind, int stage = 0)
        {
            BattleSnapshot state = session.GetSnapshot();
            return new BattleActionRequest(state.TurnId, state.CurrentActorId, kind,
                kind == BattleActionKind.Skill ? "hit" : null, kind == BattleActionKind.Skill ? "m" : null,
                investmentStage: stage);
        }

        private static void Finish(BattleSession session)
        {
            Assert.That(session.CompletePresentation(session.PendingResult.ActionId), Is.True);
        }

        private static void Wait(BattleSession session)
        {
            Assert.That(session.TrySubmit(Request(session, BattleActionKind.Wait), out _, out _), Is.True);
            Finish(session);
        }

        [TestCase(0, 0, 100)]
        [TestCase(1, 10, 110)]
        [TestCase(2, 20, 120)]
        [TestCase(3, 30, 135)]
        [TestCase(4, 40, 150)]
        [TestCase(5, 50, 170)]
        public void StagesUseBaselinePercentageAndDesignMultiplier(int stage, int cost, int damage)
        {
            BattleSession session = Start();
            Assert.That(session.TrySubmit(Request(session, BattleActionKind.Skill, stage), out BattleActionResult result, out _), Is.True);
            Assert.That(session.GetSnapshot().Combatants[0].Memory, Is.EqualTo(100 - 10 - cost));
            Assert.That(result.Hit.Damage, Is.EqualTo(damage));
        }

        [TestCase(1, 10)]
        [TestCase(2, 15)]
        [TestCase(3, 20)]
        [TestCase(4, 25)]
        [TestCase(5, 30)]
        public void DefenseUsesStageReductionRatherThanLinearCost(int stage, int reduction)
        {
            BattleSession session = Start();
            Assert.That(session.TrySubmit(Request(session, BattleActionKind.Defend, stage), out _, out _), Is.True);
            Assert.That(session.GetSnapshot().Combatants[0].RageEnergy, Is.EqualTo(80 - reduction));
            Assert.That(session.GetSnapshot().Combatants[0].Memory, Is.EqualTo(100 - stage * 10));
        }

        [Test]
        public void CostRoundingDoesNotExceedHalfBaselineOrGrantFreeBonus()
        {
            BattleSession session = Start(baseline: 19);
            Assert.That(session.Rules.GetStageCost(session.GetSnapshot().Combatants[0].Data, 5), Is.EqualTo(9));
            session = Start(baseline: 9);
            Assert.That(session.TrySubmit(Request(session, BattleActionKind.Defend, 1), out _, out _), Is.False);
            Assert.That(session.TrySubmit(Request(session, BattleActionKind.Defend, 5), out _, out _), Is.True);
            Assert.That(session.GetSnapshot().Combatants[0].Memory, Is.EqualTo(5));
        }

        [Test]
        public void InvalidStageRawAmountAndUnaffordableCombinedCostDoNotMutate()
        {
            BattleSession session = Start(memory: 55);
            foreach (int stage in new[] { -1, 6, 5 })
            {
                Assert.That(session.TrySubmit(Request(session, BattleActionKind.Skill, stage), out _, out _), Is.False);
            }
            BattleSnapshot state = session.GetSnapshot();
            Assert.That(session.TrySubmit(new BattleActionRequest(state.TurnId, "p", BattleActionKind.Defend,
                memoryInvestment: 10, investmentStage: 1), out _, out _), Is.False);
            Assert.That(session.TrySubmit(Request(session, BattleActionKind.Wait, 1), out _, out _), Is.False);
            Assert.That(session.GetSnapshot().Combatants[0].Memory, Is.EqualTo(55));
            Assert.That(session.PendingResult, Is.Null);
        }

        [Test]
        public void DepletionWaitsForRoundBoundaryThenRecoversBaselineAndSkipsOnce()
        {
            BattleSession session = Start(new SkillData("hit", "Hit", 0, memoryCost: 10), memory: 10);
            Assert.That(session.TrySubmit(Request(session, BattleActionKind.Skill), out _, out _), Is.True);
            Assert.That(session.GetSnapshot().Combatants[0].HasMemoryLoss, Is.False);
            Finish(session);
            Wait(session);
            Assert.That(session.GetSnapshot().Round, Is.EqualTo(2));
            Assert.That(session.GetSnapshot().Combatants[0].HasMemoryLoss, Is.True);
            Assert.That(session.GetSnapshot().Combatants[0].CanAct, Is.False);
            Wait(session);
            Assert.That(session.PendingResult.WasSkipped, Is.True);
            Assert.That(session.PendingResult.IsTurnStartEffect, Is.True);
            Assert.That(session.PendingResult.Changes.Single().MemoryDelta, Is.EqualTo(100));
            Assert.That(session.GetSnapshot().Combatants[0].HasMemoryLoss, Is.False);
            Finish(session);
            Assert.That(session.GetSnapshot().CurrentActorId, Is.EqualTo("p"));
            Assert.That(session.GetSnapshot().Phase, Is.EqualTo(BattlePhase.AwaitingAction));
        }

        private sealed class RecoveryResolver : IBattleActionResolver
        {
            public BattleActionError Resolve(BattleSnapshot snapshot, BattleActionRequest request,
                out IReadOnlyList<BattleEffect> effects)
            {
                effects = new[] { new BattleEffect("p", memoryDelta: request.ActorId == "p" ? -100 : 20) };
                return BattleActionError.None;
            }
        }

        [Test]
        public void RecoveryBeforeBoundaryPreventsDepletionPenalty()
        {
            BattleSession session = Start(resolver: new RecoveryResolver());
            session.TrySubmit(Request(session, BattleActionKind.Wait), out _, out _);
            Finish(session);
            Wait(session);
            Assert.That(session.GetSnapshot().Round, Is.EqualTo(2));
            Assert.That(session.GetSnapshot().Combatants[0].Memory, Is.EqualTo(20));
            Assert.That(session.GetSnapshot().Combatants[0].HasMemoryLoss, Is.False);
            Assert.That(session.GetSnapshot().CurrentActorId, Is.EqualTo("p"));
        }

        [Test]
        public void CriticalUsesActorBasePlusBonusAndActorDamageMultiplier()
        {
            var skill = new SkillData("hit", "Hit", 100, bonusCriticalChance: 0.15);
            var actor = new CombatantData("p", "P", BattleTeam.Player, 1000, 100, 50, new[] { skill },
                baseCriticalChance: 0.1, criticalDamageMultiplier: 2);
            var target = new CombatantData("m", "M", BattleTeam.Monster, 1000, 100, 10, Array.Empty<SkillData>());
            var session = new BattleSession();
            session.Start(new[] { new BattleParticipant("p", actor), new BattleParticipant("m", target) });
            var resolver = new BattleActionResolver(session.Rules);
            Assert.That(resolver.GetCriticalChance(session.GetSnapshot().Combatants[0], skill), Is.EqualTo(0.25).Within(0.00001));
            resolver.ResolveOutcome(session.GetSnapshot(), Request(session, BattleActionKind.Skill), true, true, out _, out BattleHitResult hit);
            Assert.That(hit.Damage, Is.EqualTo(200));
            Assert.That(resolver.GetCriticalChance(session.GetSnapshot().Combatants[0],
                new SkillData("cap", "Cap", 1, bonusCriticalChance: 1)), Is.EqualTo(1));
        }

        [Test]
        public void SuppliedSkillCsvLoadsExactIdsCostsRageAndPercentPoints()
        {
            string csv = File.ReadAllText("Assets/CK_Semester_Project/Prototype/Data/Battle/Skill_DT.csv");
            IReadOnlyList<SkillData> skills = SkillTable.LoadCsv(csv);
            CollectionAssert.AreEqual(new[] { "SK00", "SK01", "Sk02" }, skills.Select(skill => skill.Id));
            CollectionAssert.AreEqual(new[] { 13, 15, 20 }, skills.Select(skill => skill.MemoryCost));
            CollectionAssert.AreEqual(new[] { 20, 45, 15 }, skills.Select(skill => skill.RageGain));
            CollectionAssert.AreEqual(new[] { 0.15, 0.1, 0.15 }, skills.Select(skill => skill.BonusCriticalChance));
            Assert.That(skills.All(skill => skill.Power == 100 && skill.CriticalChance == null), Is.True);
            Assert.Throws<FormatException>(() => SkillTable.LoadCsv(csv + csv.Split('\n')[1]));
            Assert.Throws<FormatException>(() => SkillTable.LoadCsv(csv.Replace(",15,20,13", ",150,20,13")));
            Assert.Throws<FormatException>(() => SkillTable.LoadCsv("wrong"));
        }

        [TestCase(true, -10)]
        [TestCase(false, -13)]
        public void AfterimageCsvSkillNetCostIncludesHitRecovery(bool hit, int expectedDelta)
        {
            SkillData skill = SkillTable.LoadCsv(File.ReadAllText(
                "Assets/CK_Semester_Project/Prototype/Data/Battle/Skill_DT.csv"))[0];
            var player = new CombatantData("p", "플레이어", BattleTeam.Player, 1000, 100, 100, new[] { skill });
            var monster = new CombatantData("m", "몬스터", BattleTeam.Monster, 1000, 10, 10,
                new[] { new SkillData("attack", "공격", 1) });
            var session = new BattleSession();
            BattleSnapshot snapshot = session.Start(new[] { new BattleParticipant("p", player), new BattleParticipant("m", monster) });
            var request = new BattleActionRequest(snapshot.TurnId, "p", BattleActionKind.Skill, skill.Id, "m");
            Assert.That(new BattleActionResolver().ResolveOutcome(snapshot, request, hit, false,
                out IReadOnlyList<BattleEffect> effects, out _), Is.EqualTo(BattleActionError.None));
            Assert.That(effects.Single(effect => effect.TargetId == "p").MemoryDelta, Is.EqualTo(expectedDelta));
        }

        [Test]
        public void CollisionBonusExceedsCapWithoutChangingInvestmentBaselineOrTruncatingOnAction()
        {
            var skill = new SkillData("hit", "Hit", 0, memoryCost: 10, memoryRecovery: 3);
            var player = new CombatantData("p", "P", BattleTeam.Player, 100, 100, 10, Array.Empty<SkillData>());
            var monster = new CombatantData("m", "M", BattleTeam.Monster, 100, 50, 50, new[] { skill });
            var rules = new BattleRules(monsterCollisionMemoryBonus: 30);
            var session = new BattleSession(rules: rules);
            session.Start(new[] { new BattleParticipant("p", player), new BattleParticipant("m", monster) }, BattleEntryCondition.MonsterCollision);
            Assert.That(session.GetSnapshot().Combatants[1].Memory, Is.EqualTo(80));
            Assert.That(rules.GetStageCost(monster, 5), Is.EqualTo(25));
            BattleSnapshot state = session.GetSnapshot();
            Assert.That(session.TrySubmit(new BattleActionRequest(state.TurnId, "m", BattleActionKind.Skill, "hit", "p"), out _, out _), Is.True);
            Assert.That(session.GetSnapshot().Combatants[1].Memory, Is.EqualTo(70));
        }

        [Test]
        public void MonsterRangeChoosesOnlyAffordableStagesAndIsRepeatable()
        {
            var skill = new SkillData("hit", "Hit", 100, memoryCost: 10);
            var player = new CombatantData("p", "P", BattleTeam.Player, 1000, 100, 10, Array.Empty<SkillData>());
            var monster = new CombatantData("m", "M", BattleTeam.Monster, 1000, 100, 100, new[] { skill },
                minMemoryInvestment: 20, maxMemoryInvestment: 40);
            var session = new BattleSession();
            session.Start(new[] { new BattleParticipant("p", player), new BattleParticipant("m", monster) });
            var observed = new HashSet<int>();
            for (int seed = 0; seed < 30; seed++)
            {
                var ai = new MonsterAi(randomSeed: seed);
                Assert.That(ai.TryChooseAction(session, out BattleActionRequest first), Is.True);
                Assert.That(ai.TryChooseAction(session, out BattleActionRequest second), Is.True);
                Assert.That(first.InvestmentStage.Value, Is.InRange(2, 4));
                Assert.That(second.InvestmentStage, Is.EqualTo(first.InvestmentStage));
                Assert.That(session.ValidateRequest(first), Is.EqualTo(BattleActionError.None));
                observed.Add(first.InvestmentStage.Value);
            }
            Assert.That(observed.Count, Is.GreaterThan(1));
            Assert.That(session.GetSnapshot().Combatants[1].Memory, Is.EqualTo(100));
        }
    }
}

using System;
using System.Linq;
using System.IO;
using NUnit.Framework;

namespace CK.SemesterProject.Battle.Tests
{
    public sealed class SkillEnhancementTests
    {
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void AreaSkillPaysOnceAndHitsEachLivingEnemy(int count)
        {
            var skill = new SkillData("area", "광역", 50, memoryCost: 30, rageCost: 50,
                isAreaOfEffect: true, criticalChance: 0);
            var player = new CombatantData("player", "플레이어", BattleTeam.Player, 1000, 200, 200, new[] { skill });
            var monster = new CombatantData("monster", "몬스터", BattleTeam.Monster, 1000, 20, 20, Array.Empty<SkillData>());
            var members = new[] { new BattleParticipant("p", player, initialRageEnergy: 80) }
                .Concat(Enumerable.Range(0, count).Select(i => new BattleParticipant("m" + i, monster)))
                .Concat(new[] { new BattleParticipant("dead", monster, initialHp: 0) }).ToArray();
            var session = new BattleSession();
            BattleSnapshot before = session.Start(members);
            Assert.That(session.TrySubmit(new BattleActionRequest(before.TurnId, "p", BattleActionKind.Skill,
                "area", "m0", investmentStage: 1), out BattleActionResult result, out _), Is.True);
            Assert.That(result.Changes.Single(change => change.After.InstanceId == "p").MemoryDelta, Is.EqualTo(-50));
            Assert.That(result.Changes.Single(change => change.After.InstanceId == "p").After.RageEnergy, Is.EqualTo(30));
            Assert.That(result.TargetHits.Count, Is.EqualTo(count));
            Assert.That(result.Changes.Count(change => change.HpDelta < 0), Is.EqualTo(count));
            Assert.That(result.Changes.Any(change => change.After.InstanceId == "dead"), Is.False);
        }

        [TestCase(0, 50, false)]
        [TestCase(49, 50, false)]
        [TestCase(50, 50, true)]
        [TestCase(79, 80, false)]
        [TestCase(80, 80, true)]
        public void RageCostBoundaryIsValidatedBeforeStateChanges(int energy, int cost, bool valid)
        {
            var skill = new SkillData("special", "특수", 50, memoryCost: 30, rageCost: cost, criticalChance: 0);
            var player = new CombatantData("player", "플레이어", BattleTeam.Player, 1000, 200, 200, new[] { skill });
            var monster = new CombatantData("monster", "몬스터", BattleTeam.Monster, 1000, 20, 20, Array.Empty<SkillData>());
            var session = new BattleSession();
            BattleSnapshot before = session.Start(new[]
            {
                new BattleParticipant("p", player, initialRageEnergy: energy), new BattleParticipant("m", monster)
            });
            Assert.That(session.TrySubmit(new BattleActionRequest(before.TurnId, "p", BattleActionKind.Skill,
                "special", "m"), out BattleActionResult result, out BattleActionError error), Is.EqualTo(valid));
            if (!valid)
            {
                Assert.That(error, Is.EqualTo(BattleActionError.InsufficientRage));
                Assert.That(session.GetSnapshot().Combatants.Single(unit => unit.InstanceId == "p").Memory, Is.EqualTo(200));
                Assert.That(session.GetSnapshot().TurnId, Is.EqualTo(before.TurnId));
            }
            else
            {
                Assert.That(result.Changes.Single(change => change.After.InstanceId == "p").After.RageEnergy, Is.EqualTo(energy - cost));
            }
        }

        [Test]
        public void AreaOblivionConservesMemoryAndHasIndependentMisses()
        {
            var skill = new SkillData("area", "광역 망각", 50, BattleElement.Oblivion,
                memoryCost: 15, rageCost: 50, isAreaOfEffect: true, criticalChance: 0);
            var player = new CombatantData("player", "플레이어", BattleTeam.Player, 1000, 200, 200, new[] { skill });
            var monster = new CombatantData("monster", "몬스터", BattleTeam.Monster, 1000, 20, 20, Array.Empty<SkillData>());
            var dodger = new CombatantData("dodge", "회피", BattleTeam.Monster, 1000, 20, 20,
                Array.Empty<SkillData>(), evasion: 1);
            var session = new BattleSession();
            BattleSnapshot before = session.Start(new[]
            {
                new BattleParticipant("p", player, initialRageEnergy: 80),
                new BattleParticipant("m0", monster), new BattleParticipant("m1", monster), new BattleParticipant("m2", dodger)
            });
            Assert.That(session.TrySubmit(new BattleActionRequest(before.TurnId, "p", BattleActionKind.Skill,
                "area", "m0"), out BattleActionResult result, out _), Is.True);
            Assert.That(result.Changes.Sum(change => change.MemoryDelta), Is.EqualTo(-15));
            Assert.That(result.Changes.Single(change => change.After.InstanceId == "p").After.Memory, Is.EqualTo(200));
            Assert.That(result.TargetHits["m0"].IsHit, Is.True);
            Assert.That(result.TargetHits["m1"].IsHit, Is.True);
            Assert.That(result.TargetHits["m2"].IsHit, Is.False);
            Assert.That(result.Changes.Single(change => change.After.InstanceId == "m2").HpDelta, Is.Zero);
        }

        [Test]
        public void LatestTableIncludesSpecialUltimateAndPreservesMetadataWhenCloning()
        {
            var skills = SkillTable.LoadCsv(File.ReadAllText("Assets/CK_Semester_Project/Prototype/Data/Battle/Skill_DT.csv"));
            Assert.That(skills.Count, Is.EqualTo(11));
            SkillData special = skills.Single(skill => skill.Id == "SK09");
            Assert.That(special.IsAreaOfEffect, Is.True);
            Assert.That(special.RageCost, Is.EqualTo(50));
            Assert.That(special.MemoryCost, Is.EqualTo(30));
            Assert.That(special.WithPower(180).IsAreaOfEffect, Is.True);
            Assert.That(special.WithPower(180).RageCost, Is.EqualTo(50));
            SkillData ultimate = skills.Single(skill => skill.Id == "SK10");
            Assert.That(ultimate.IsAreaOfEffect, Is.False);
            Assert.That(ultimate.RageCost, Is.EqualTo(80));
            Assert.That(ultimate.MemoryCost, Is.EqualTo(40));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SkillData("bad", "잘못된 값", 1, rageCost: 101));
        }

        private static string[][] StageIds()
        {
            return new[]
            {
                new[] { "SK00", "SK03", "SK06" },
                new[] { "SK01", "SK04", "SK07" },
                new[] { "SK02", "SK05", "SK08" }
            };
        }

        [TestCase(true)]
        [TestCase(false)]
        public void AreaPredictionChargesOnceEvenWhenEveryTargetMisses(bool isHit)
        {
            var skill = new SkillData("area", "광역", 50, BattleElement.Oblivion,
                memoryCost: 30, rageCost: 50, isAreaOfEffect: true);
            var player = new CombatantData("player", "플레이어", BattleTeam.Player, 1000, 200, 200, new[] { skill });
            var monster = new CombatantData("monster", "몬스터", BattleTeam.Monster, 1000, 20, 20, Array.Empty<SkillData>());
            var session = new BattleSession();
            BattleSnapshot before = session.Start(new[]
            {
                new BattleParticipant("p", player, initialRageEnergy: 80),
                new BattleParticipant("m0", monster), new BattleParticipant("m1", monster), new BattleParticipant("m2", monster)
            });
            var resolver = new BattleActionResolver();
            Assert.That(resolver.ResolveOutcome(before, new BattleActionRequest(before.TurnId, "p", BattleActionKind.Skill,
                "area", "m0"), isHit, false, out var effects, out _), Is.EqualTo(BattleActionError.None));
            Assert.That(effects.Single(effect => effect.TargetId == "p").RageDelta, Is.EqualTo(-50));
            Assert.That(effects.Single(effect => effect.TargetId == "p").MemoryDelta, Is.EqualTo(isHit ? 0 : -30));
            Assert.That(effects.Sum(effect => effect.MemoryDelta), Is.EqualTo(-30));
            Assert.That(resolver.TargetHits.Count, Is.EqualTo(3));
            Assert.That(resolver.TargetHits.Values.All(hit => hit.IsHit == isHit), Is.True);
            Assert.That(before.Combatants.Single(unit => unit.InstanceId == "p").RageEnergy, Is.EqualTo(80));
        }

        [TestCase(0, 100, "SK00")]
        [TestCase(4, 100, "SK00")]
        [TestCase(5, 120, "SK03")]
        [TestCase(9, 120, "SK03")]
        [TestCase(10, 150, "SK06")]
        public void TableStagesSelectThreeCommandsByIdAfterVictoryBoundaries(int victories, int power, string firstId)
        {
            SkillData[] source = SkillTable.LoadCsv(File.ReadAllText(
                "Assets/CK_Semester_Project/Prototype/Data/Battle/Skill_DT.csv")).Take(9).Reverse().ToArray();
            SkillData[] selected = SkillTable.SelectStage(source, StageIds(), new SkillEnhancementRules().GetStage(victories));
            Assert.That(selected.Length, Is.EqualTo(3));
            Assert.That(selected[0].Id, Is.EqualTo(firstId));
            Assert.That(selected.All(skill => skill.Power == power), Is.True);
            CollectionAssert.AreEqual(new[] { BattleElement.Afterimage, BattleElement.Imprint, BattleElement.Oblivion },
                selected.Select(skill => skill.Element));
            CollectionAssert.AreEqual(new[] { 10, 10, 20 }, selected.Select(skill => skill.MemoryCost));
            CollectionAssert.AreEqual(new[] { 20, 25, 15 }, selected.Select(skill => skill.RageGain));
            Assert.That(selected.All(skill => source.Contains(skill)), Is.True);
        }

        [Test]
        public void StageTableRejectsMissingDuplicateUnmappedAndMixedElementRows()
        {
            SkillData[] source = SkillTable.LoadCsv(File.ReadAllText(
                "Assets/CK_Semester_Project/Prototype/Data/Battle/Skill_DT.csv")).Take(9).ToArray();
            Assert.Throws<ArgumentException>(() => SkillTable.SelectStage(source.Take(8).ToArray(), StageIds(), 1));
            string[][] duplicate = StageIds();
            duplicate[2][2] = "SK00";
            Assert.Throws<ArgumentException>(() => SkillTable.SelectStage(source, duplicate, 1));
            string[][] missing = StageIds();
            missing[2][2] = "SK99";
            Assert.Throws<ArgumentException>(() => SkillTable.SelectStage(source, missing, 1));
            source[8] = new SkillData("SK08", "잘못된 속성", 150, BattleElement.Afterimage);
            Assert.Throws<ArgumentException>(() => SkillTable.SelectStage(source, StageIds(), 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => SkillTable.SelectStage(source, StageIds(), 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => SkillTable.SelectStage(source, StageIds(), 4));
        }

        private static CombatantData Player(string id = "player", SkillData skill = null)
        {
            return new CombatantData(id, "플레이어", BattleTeam.Player, 1000, 100, 100,
                new[] { skill ?? new SkillData("hit", "공격", 100) });
        }

        [TestCase(0, 1, 5)]
        [TestCase(4, 1, 1)]
        [TestCase(5, 2, 5)]
        [TestCase(9, 2, 1)]
        [TestCase(10, 3, 0)]
        [TestCase(11, 3, 0)]
        [TestCase(int.MaxValue, 3, 0)]
        public void VictoryBoundariesAdvanceAtFiveAndTenAndStopAtThree(int victories, int stage, int remaining)
        {
            var rules = new SkillEnhancementRules();
            Assert.That(rules.VictoriesPerStage, Is.EqualTo(5));
            Assert.That(rules.GetStage(victories), Is.EqualTo(stage));
            Assert.That(rules.GetRemainingVictories(victories), Is.EqualTo(remaining));
        }

        [Test]
        public void CustomPeriodAndLargeValuesDoNotOverflow()
        {
            var everyVictory = new SkillEnhancementRules(1);
            Assert.That(everyVictory.GetStage(int.MaxValue), Is.EqualTo(3));
            Assert.That(everyVictory.GetRemainingVictories(int.MaxValue), Is.Zero);
            var longPeriod = new SkillEnhancementRules(int.MaxValue);
            Assert.That(longPeriod.GetStage(int.MaxValue - 1), Is.EqualTo(1));
            Assert.That(longPeriod.GetRemainingVictories(int.MaxValue - 1), Is.EqualTo(1));
            Assert.That(longPeriod.GetStage(int.MaxValue), Is.EqualTo(2));
            Assert.That(longPeriod.GetRemainingVictories(int.MaxValue), Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void InvalidPeriodAndVictoryCountAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SkillEnhancementRules(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SkillEnhancementRules(-1));
            var rules = new SkillEnhancementRules();
            Assert.Throws<ArgumentOutOfRangeException>(() => rules.GetStage(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => rules.GetRemainingVictories(-1));
        }

        [Test]
        public void WithPowerPreservesAllOtherSkillValuesAndOriginalData()
        {
            var original = new SkillData("skill", "각인", 100, BattleElement.Imprint, SkillTarget.Self,
                memoryCost: 17, memoryRecovery: 9, memorySteal: 6, accuracy: 0.73,
                criticalChance: 0.21, rageGain: 42, inflictedSkippedTurns: 2,
                bonusCriticalChance: 0.15, englishName: "Imprint");
            SkillData enhanced = original.WithPower(150);

            Assert.That(enhanced, Is.Not.SameAs(original));
            Assert.That(original.Power, Is.EqualTo(100));
            Assert.That(enhanced.Power, Is.EqualTo(150));
            Assert.That(enhanced.Id, Is.EqualTo(original.Id));
            Assert.That(enhanced.DisplayName, Is.EqualTo(original.DisplayName));
            Assert.That(enhanced.EnglishName, Is.EqualTo(original.EnglishName));
            Assert.That(enhanced.Element, Is.EqualTo(original.Element));
            Assert.That(enhanced.Target, Is.EqualTo(original.Target));
            Assert.That(enhanced.MemoryCost, Is.EqualTo(original.MemoryCost));
            Assert.That(enhanced.MemoryRecovery, Is.EqualTo(original.MemoryRecovery));
            Assert.That(enhanced.MemorySteal, Is.EqualTo(original.MemorySteal));
            Assert.That(enhanced.Accuracy, Is.EqualTo(original.Accuracy));
            Assert.That(enhanced.CriticalChance, Is.EqualTo(original.CriticalChance));
            Assert.That(enhanced.BonusCriticalChance, Is.EqualTo(original.BonusCriticalChance));
            Assert.That(enhanced.RageGain, Is.EqualTo(original.RageGain));
            Assert.That(enhanced.InflictedSkippedTurns, Is.EqualTo(original.InflictedSkippedTurns));
            Assert.Throws<ArgumentOutOfRangeException>(() => original.WithPower(-1));
            Assert.That(new SkillData("default", "기본", 1).WithPower(0).CriticalChance, Is.Null);
        }

        [TestCase(120, 24)]
        [TestCase(150, 30)]
        public void EnhancedPowerDrivesDamageAndTwentyPercentImprintWithoutIncreasingCost(int power, int imprint)
        {
            var original = new SkillData("hit", "각인", 100, BattleElement.Imprint, memoryCost: 15);
            CombatantData player = Player(skill: original.WithPower(power));
            var monster = new CombatantData("monster", "적", BattleTeam.Monster, 1000, 100, 10,
                Array.Empty<SkillData>());
            var session = new BattleSession(rules: new BattleRules(mechanics:
                new BattleCombatRules(criticalChance: 0, imprintRatio: 0.2)));
            BattleSnapshot snapshot = session.Start(new[]
            {
                new BattleParticipant("p", player),
                new BattleParticipant("m", monster)
            });
            Assert.That(session.TrySubmit(new BattleActionRequest(snapshot.TurnId, "p",
                BattleActionKind.Skill, "hit", "m"), out BattleActionResult result, out _), Is.True);
            Assert.That(result.Hit.Damage, Is.EqualTo(power));
            Assert.That(session.GetSnapshot().Combatants.Single(unit => unit.InstanceId == "p").Memory,
                Is.EqualTo(85));
            Assert.That(session.GetSnapshot().Combatants.Single(unit => unit.InstanceId == "m").ImprintDamage,
                Is.EqualTo(imprint));
            Assert.That(session.CompletePresentation(result.ActionId), Is.True);
            Assert.That(session.PendingResult.IsTurnStartEffect, Is.True);
            Assert.That(session.PendingResult.Changes.Single().HpDelta, Is.EqualTo(-imprint));
            Assert.That(original.Power, Is.EqualTo(100));
        }

        [TestCase(BattleOutcome.None)]
        [TestCase(BattleOutcome.Defeat)]
        [TestCase(BattleOutcome.Draw)]
        public void NonVictoryResultsDoNotAdvanceOrNotify(BattleOutcome outcome)
        {
            var state = new PlayerRuntimeState();
            state.Initialize(Player());
            int changes = 0;
            state.Changed += () => changes++;
            Assert.That(state.RecordBattleOutcome(outcome), Is.False);
            Assert.That(state.VictoryCount, Is.Zero);
            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void VictoryNotifiesSubscribersAndSurvivesRecoveryAndSameCharacterInitialization()
        {
            var state = new PlayerRuntimeState();
            state.Initialize(Player());
            int observedVictories = 0;
            state.Changed += () => observedVictories = state.VictoryCount;
            for (int victory = 0; victory < 5; victory++)
            {
                Assert.That(state.RecordBattleOutcome(BattleOutcome.Victory), Is.True);
            }
            Assert.That(observedVictories, Is.EqualTo(5));
            Assert.That(new SkillEnhancementRules().GetStage(state.VictoryCount), Is.EqualTo(2));
            state.SetVitals(430, 20);
            state.Initialize(Player());
            Assert.That(state.Hp, Is.EqualTo(430));
            Assert.That(state.Memory, Is.EqualTo(20));
            state.RestoreFieldMemory();
            state.RestoreFull();
            Assert.That(state.VictoryCount, Is.EqualTo(5));
            Assert.That(observedVictories, Is.EqualTo(5));
            state.Initialize(Player("other_player"));
            Assert.That(state.VictoryCount, Is.Zero);
            Assert.That(observedVictories, Is.Zero);
        }

        [Test]
        public void OutcomeRecordingRequiresInitializationAndAValidOutcome()
        {
            var state = new PlayerRuntimeState();
            Assert.Throws<InvalidOperationException>(() => state.RecordBattleOutcome(BattleOutcome.Victory));
            Assert.Throws<InvalidOperationException>(() => state.RecordBattleOutcome(BattleOutcome.None));
            state.Initialize(Player());
            Assert.Throws<ArgumentOutOfRangeException>(() => state.RecordBattleOutcome((BattleOutcome)100));
            Assert.That(state.VictoryCount, Is.Zero);
        }

        [Test]
        public void VictoryCountSaturatesInsteadOfWrappingNegative()
        {
            var state = new PlayerRuntimeState();
            state.Initialize(Player());
            // 20억 회 반복 없이 장기 저장 상태의 최대 경계를 검증한다.
            typeof(PlayerRuntimeState).GetProperty(nameof(PlayerRuntimeState.VictoryCount))
                .GetSetMethod(true).Invoke(state, new object[] { int.MaxValue });
            Assert.That(state.RecordBattleOutcome(BattleOutcome.Victory), Is.True);
            Assert.That(state.VictoryCount, Is.EqualTo(int.MaxValue));
            Assert.That(new SkillEnhancementRules().GetStage(state.VictoryCount), Is.EqualTo(3));
        }
    }
}

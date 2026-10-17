using System;
using System.Linq;
using NUnit.Framework;

namespace CK.SemesterProject.Battle.Tests
{
    public sealed class BossAndDefenseFixTests
    {
        private static CombatantData Boss(int memory = 200)
        {
            return new CombatantData("MO09", "보스", BattleTeam.Monster, 2000, 200, 200,
                new[]
                {
                    new SkillData("boss_1", "스킬_1", 50, BattleElement.Oblivion, memoryCost: 10),
                    new SkillData("boss_2", "스킬_2", 70, BattleElement.Oblivion, memoryCost: 20)
                }, BattleElement.Oblivion, weaknessChain: new[]
                {
                    BattleElement.Afterimage, BattleElement.Imprint, BattleElement.Oblivion, BattleElement.Imprint
                }, monsterProfile: MonsterBehaviorProfile.CreateDefault(BattleElement.Oblivion),
                bossProfile: new BossBehaviorProfile("boss_1", "boss_2"));
        }

        [TestCase(100, 0)]
        [TestCase(75, 0)]
        [TestCase(74, 1)]
        [TestCase(50, 1)]
        [TestCase(49, 2)]
        [TestCase(25, 2)]
        [TestCase(24, 3)]
        [TestCase(0, 3)]
        public void BossResourceBandIncludesExactBoundaries(int current, int band)
        {
            Assert.That(BossBehaviorProfile.GetBand(current, 100), Is.EqualTo(band));
        }

        [Test]
        public void BossUsesEveryDocumentedRowAndHasDeterministicFallback()
        {
            var profile = new BossBehaviorProfile("one", "two");
            Assert.That(profile.Rules.Count, Is.EqualTo(28));
            // 중복 조건 6개는 사용자가 승인한 문서의 앞 행 우선순위를 따른다.
            foreach (var rule in profile.Rules.GroupBy(rule => Tuple.Create(rule.MemoryBand, rule.BossHpBand, rule.PlayerHpBand))
                .Select(group => group.First()))
            {
                string expected = rule.SkillNumber == 1 ? "one" : "two";
                Assert.That(profile.ChooseSkill(new[] { 100, 60, 30, 10 }[rule.MemoryBand], 100, new[] { 100, 60, 30, 10 }[rule.BossHpBand],
                    100, new[] { 100, 60, 30, 10 }[rule.PlayerHpBand], 100), Is.EqualTo(expected));
            }
            for (int memory = 0; memory < 4; memory++)
            {
                for (int boss = 0; boss < 4; boss++)
                {
                    for (int player = 0; player < 4; player++)
                    {
                        string selected = profile.ChooseSkill(new[] { 100, 60, 30, 10 }[memory], 100, new[] { 100, 60, 30, 10 }[boss], 100, new[] { 100, 60, 30, 10 }[player], 100);
                        Assert.That(selected == "one" || selected == "two", Is.True);
                    }
                }
            }
        }

        [TestCase(200, 2000, 1000, "boss_1")]
        [TestCase(120, 1200, 600, "boss_1")]
        [TestCase(60, 600, 300, "boss_2")]
        [TestCase(15, 2000, 1000, "boss_1")]
        public void BossUsesResourceTableInsteadOfAlwaysPickingHigherDamage(int memory, int hp, int playerHp, string expected)
        {
            var player = new CombatantData("p", "플레이어", BattleTeam.Player, 1000, 200, 200, Array.Empty<SkillData>());
            var session = new BattleSession(randomSeed: 5);
            session.Start(new[] { new BattleParticipant("b", Boss(), initialHp: hp, initialMemory: memory),
                new BattleParticipant("p", player, initialHp: playerHp, initialMemory: 1) });
            Assert.That(new MonsterAi(randomSeed: 8).TryChooseAction(session, out BattleActionRequest request), Is.True);
            Assert.That(request.Kind, Is.EqualTo(BattleActionKind.Skill));
            Assert.That(request.SkillId, Is.EqualTo(expected));
            Assert.That(session.ValidateRequest(request), Is.EqualTo(BattleActionError.None));
        }

        [TestCase(20, true)]
        [TestCase(200, false)]
        public void BossSharesMonsterDefenseMemoryCondition(int playerMemory, bool allowed)
        {
            var player = new CombatantData("p", "플레이어", BattleTeam.Player, 1000, 200, 200, Array.Empty<SkillData>());
            var session = new BattleSession();
            session.Start(new[] { new BattleParticipant("b", Boss()), new BattleParticipant("p", player, initialMemory: playerMemory) },
                BattleEntryCondition.MonsterCollision, "b");
            Assert.That(session.Rules.CanMonsterDefend(session.GetSnapshot().Combatants.First(unit => unit.InstanceId == "b"),
                session.GetSnapshot().Combatants), Is.EqualTo(allowed));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void TwoConsecutiveDefensesTurnFirstIntoAttackWithoutMutatingSession(int seed)
        {
            var skill = new SkillData("hit", "공격", 1, BattleElement.Oblivion, criticalChance: 0);
            var monster = new CombatantData("m", "망각", BattleTeam.Monster, 2000, 200, 200, new[] { skill },
                BattleElement.Oblivion, monsterProfile: MonsterBehaviorProfile.CreateDefault(BattleElement.Oblivion));
            var player = new CombatantData("p", "플레이어", BattleTeam.Player, 1000, 100, 100, Array.Empty<SkillData>());
            var session = new BattleSession(randomSeed: seed);
            session.Start(new[] { new BattleParticipant("m", monster), new BattleParticipant("p", player, initialRageEnergy: 80) },
                BattleEntryCondition.MonsterCollision, "m");
            var ai = new MonsterAi(randomSeed: seed);
            int beforeMemory = session.GetSnapshot().Combatants.First(unit => unit.InstanceId == "m").Memory;
            Assert.That(ai.TryChooseAction(session, out BattleActionRequest request), Is.True);
            Assert.That(request.Kind, Is.EqualTo(BattleActionKind.Skill));
            Assert.That(ai.TryChooseAction(session, out BattleActionRequest repeat), Is.True);
            Assert.That(repeat.Kind, Is.EqualTo(request.Kind));
            Assert.That(repeat.InvestmentStage, Is.EqualTo(request.InvestmentStage));
            Assert.That(session.History.Count, Is.Zero);
            Assert.That(session.GetSnapshot().Combatants.First(unit => unit.InstanceId == "m").Memory, Is.EqualTo(beforeMemory));
        }

        [Test]
        public void ConsecutiveDefensesDoNotStackAndOnlyNextIncomingAttackIsReduced()
        {
            var hit = new SkillData("hit", "공격", 100, criticalChance: 0);
            var player = new CombatantData("p", "플레이어", BattleTeam.Player, 2000, 100, 100, new[] { hit });
            var monster = new CombatantData("m", "몬스터", BattleTeam.Monster, 2000, 100, 100, new[] { hit });
            var session = new BattleSession(rules: new BattleRules(enableMemoryLoss: false));
            session.Start(new[] { new BattleParticipant("p", player), new BattleParticipant("m", monster, initialMemory: 90) },
                BattleEntryCondition.PlayerInitiated, "p");
            for (int i = 0; i < 2; i++)
            {
                Assert.That(session.TrySubmit(new BattleActionRequest(session.GetSnapshot().TurnId, "p", BattleActionKind.Defend),
                    out BattleActionResult result, out _), Is.True);
                session.CompletePresentation(result.ActionId);
            }
            Assert.That(session.TrySubmit(new BattleActionRequest(session.GetSnapshot().TurnId, "m", BattleActionKind.Skill, "hit", "p"),
                out BattleActionResult defended, out _), Is.True);
            Assert.That(defended.Hit.Damage, Is.EqualTo(50));
            Assert.That(session.GetSnapshot().Combatants.First(unit => unit.InstanceId == "p").IsDefending, Is.False);
        }

        [Test]
        public void OriginalMonsterDoubleDefenseScenarioReducesOnlyFirstPlayerAttack()
        {
            var hit = new SkillData("hit", "공격", 100, criticalChance: 0);
            var player = new CombatantData("p", "플레이어", BattleTeam.Player, 2000, 100, 100, new[] { hit });
            var monster = new CombatantData("m", "망각", BattleTeam.Monster, 2000, 200, 200, new[] { hit }, BattleElement.Oblivion);
            var session = new BattleSession(randomSeed: 7);
            session.Start(new[] { new BattleParticipant("p", player), new BattleParticipant("m", monster) },
                BattleEntryCondition.MonsterCollision, "m");
            for (int i = 0; i < 2; i++)
            {
                Assert.That(session.TrySubmit(new BattleActionRequest(session.GetSnapshot().TurnId, "m", BattleActionKind.Defend,
                    investmentStage: 3), out BattleActionResult result, out _), Is.True);
                session.CompletePresentation(result.ActionId);
            }
            for (int i = 0; i < 2; i++)
            {
                Assert.That(session.GetSnapshot().CurrentActorId, Is.EqualTo("p"));
                Assert.That(session.TrySubmit(new BattleActionRequest(session.GetSnapshot().TurnId, "p", BattleActionKind.Skill,
                    "hit", "m"), out BattleActionResult result, out _), Is.True);
                Assert.That(result.Hit.Damage, Is.EqualTo(i == 0 ? 50 : 100));
                session.CompletePresentation(result.ActionId);
            }
        }
    }
}

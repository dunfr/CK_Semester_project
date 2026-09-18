using NUnit.Framework;

namespace CK.SemesterProject.Battle.Tests
{
    public sealed class PlayerRuntimeStateTests
    {
        [Test]
        public void FieldReturnRestoresInitialMemoryWithoutHealingHp()
        {
            var state = new PlayerRuntimeState();
            state.Initialize(Player());
            state.SetVitals(430, 0);
            state.RestoreFieldMemory();
            Assert.That(state.Hp, Is.EqualTo(430));
            Assert.That(state.Memory, Is.EqualTo(200));
            state.RestoreFieldMemory();
            Assert.That(state.Hp, Is.EqualTo(430));
            Assert.That(state.Memory, Is.EqualTo(200));
        }

        private static CombatantData Player()
        {
            return new CombatantData("player_data", "플레이어", BattleTeam.Player, 1000, 200, 200,
                new[] { new SkillData("attack", "공격", 10) });
        }

        [Test]
        public void NewBattleDoesNotCarryRageOrSkippedTurnsFromPreviousBattle()
        {
            var state = new PlayerRuntimeState();
            CombatantData player = Player();
            var enemy = new CombatantData("enemy", "적", BattleTeam.Monster, 100, 100, 100,
                new[] { new SkillData("attack", "공격", 1) });
            state.Initialize(player);
            var previous = new BattleSession();
            BattleSnapshot oldState = previous.Start(new[]
            {
                new BattleParticipant("player", player, initialMemory: 30, skippedTurns: 2, initialRageEnergy: 80),
                new BattleParticipant("enemy", enemy)
            });
            state.ApplyBattleState(oldState.Combatants[0]);
            state.RestoreFieldMemory();
            var next = new BattleSession();
            CombatantState fresh = next.Start(new[]
            {
                new BattleParticipant("player", player, initialHp: state.Hp, initialMemory: state.Memory),
                new BattleParticipant("enemy", enemy)
            }).Combatants[0];
            Assert.That(fresh.Memory, Is.EqualTo(200));
            Assert.That(fresh.RageEnergy, Is.Zero);
            Assert.That(fresh.SkippedTurns, Is.Zero);
            Assert.That(fresh.ImprintDamage, Is.Zero);
            Assert.That(fresh.HasMemoryLoss, Is.False);
            Assert.That(fresh.IsOverheated, Is.False);
            Assert.That(fresh.IsDefending, Is.False);
        }

        [Test]
        public void SceneInitializationPreservesVitalsAndNextBattleUsesThem()
        {
            var state = new PlayerRuntimeState();
            CombatantData data = Player();
            state.Initialize(data);
            state.SetVitals(430, 37);
            state.Initialize(Player());
            var session = new BattleSession();
            var enemy = new CombatantData("enemy", "적", BattleTeam.Monster, 100, 100, 100,
                new[] { new SkillData("attack", "공격", 1) });
            BattleSnapshot snapshot = session.Start(new[]
            {
                new BattleParticipant("player", data, initialHp: state.Hp, initialMemory: state.Memory),
                new BattleParticipant("enemy", enemy)
            });
            state.ApplyBattleState(snapshot.Combatants[0]);
            Assert.That(state.Hp, Is.EqualTo(430));
            Assert.That(state.Memory, Is.EqualTo(37));
        }

        [Test]
        public void RecoveryAndBoundsNotifySubscribersWithFinalValues()
        {
            var state = new PlayerRuntimeState();
            state.Initialize(Player());
            int hp = -1;
            int memory = -1;
            state.Changed += () => { hp = state.Hp; memory = state.Memory; };
            state.SetVitals(-20, 999);
            Assert.That(hp, Is.Zero);
            Assert.That(memory, Is.EqualTo(200));
            state.RestoreFull();
            Assert.That(hp, Is.EqualTo(1000));
            Assert.That(memory, Is.EqualTo(200));
        }
    }
}

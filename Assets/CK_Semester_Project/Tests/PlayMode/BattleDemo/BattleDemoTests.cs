using System.Collections;
using System.Linq;
using CK.SemesterProject.Battle.Demo;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CK.SemesterProject.Battle.Tests
{
    public sealed class BattleDemoTests
    {
        private GameObject _root;
        private BattleDemoController _demo;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Demo under test");
            _demo = _root.AddComponent<BattleDemoController>();
            _demo.SetAutoAdvance(false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [UnityTest]
        public IEnumerator ManualInputDoesNotDoubleSubmitAndPresentationReleasesNextActor()
        {
            Assert.That(_demo.Attack(), Is.True);
            Assert.That(_demo.Attack(), Is.False);
            Assert.That(_demo.SelectTarget("sentinel_b"), Is.False);
            Assert.That(_demo.Snapshot.Phase, Is.EqualTo(BattlePhase.AwaitingPresentation));
            yield return null;
            Assert.That(_demo.Snapshot.Phase, Is.EqualTo(BattlePhase.AwaitingPresentation));
            Assert.That(_demo.Advance(), Is.True);
            Assert.That(_demo.Snapshot.CurrentActorId, Is.EqualTo("sentinel_a"));
            Assert.That(_demo.Advance(), Is.True);
            Assert.That(_demo.Snapshot.Combatants[0].Hp, Is.EqualTo(127));
        }

        [Test]
        public void DisruptionReordersRemainingEnemies()
        {
            Assert.That(_demo.SelectSkill("disrupt"), Is.True);
            Assert.That(_demo.Attack(), Is.True);
            Assert.That(_demo.Snapshot.Combatants[1].Memory, Is.EqualTo(10));
            CollectionAssert.AreEqual(new[] { "sentinel_b", "sentinel_a" }, _demo.Snapshot.TurnOrder);
        }

        [Test]
        public void TargetSelectionAndScenarioResetUseNewSession()
        {
            Assert.That(_demo.SelectTarget("missing"), Is.False);
            Assert.That(_demo.SelectTarget("sentinel_b"), Is.True);
            _demo.Attack();
            Assert.That(_demo.Snapshot.Combatants[2].Hp, Is.EqualTo(48));
            _demo.RestartScenario(1);
            Assert.That(_demo.Snapshot.Combatants.All(unit => unit.Hp == unit.Data.MaxHp), Is.True);
            Assert.That(_demo.Snapshot.CurrentActorId, Is.EqualTo("player"));
            Assert.That(_demo.Snapshot.Combatants.All(unit => unit.Memory == 24), Is.True);
            Assert.That(_demo.PendingResult, Is.Null);
        }

        [Test]
        public void SkipScenarioAndRestartDoNotLeaveOldPresentationPending()
        {
            _demo.RestartScenario(2);
            Assert.That(_demo.PendingResult.WasSkipped, Is.True);
            Assert.That(_demo.Wait(), Is.False);
            _demo.RestartScenario(0);
            Assert.That(_demo.PendingResult, Is.Null);
            Assert.That(_demo.IsPlayerInput, Is.True);
        }

        [Test]
        public void FullBattleCanWinAndAutoRetargetsAfterDeath()
        {
            for (int steps = 0; steps < 80 && _demo.Snapshot.Phase != BattlePhase.Finished; steps++)
            {
                if (_demo.IsPlayerInput)
                {
                    _demo.SelectSkill("heavy");
                    Assert.That(_demo.Attack(), Is.True);
                }
                else
                {
                    Assert.That(_demo.Advance(), Is.True);
                }
            }
            Assert.That(_demo.Snapshot.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(_demo.Attack(), Is.False);
            Assert.That(_demo.Advance(), Is.False);
        }

        [Test]
        public void WaitingCanLoseAndRestartRestoresInput()
        {
            for (int steps = 0; steps < 120 && _demo.Snapshot.Phase != BattlePhase.Finished; steps++)
            {
                if (_demo.IsPlayerInput)
                {
                    _demo.Wait();
                }
                else
                {
                    _demo.Advance();
                }
            }
            Assert.That(_demo.Snapshot.Outcome, Is.EqualTo(BattleOutcome.Defeat));
            _demo.RestartScenario(0);
            Assert.That(_demo.IsPlayerInput, Is.True);
        }

        [UnityTest]
        public IEnumerator AutomaticPresentationAndMonsterResponsesReturnToPlayer()
        {
            _demo.SetAutoAdvance(true);
            _demo.Attack();
            float deadline = Time.realtimeSinceStartup + 8f;
            while (!_demo.IsPlayerInput && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.That(_demo.IsPlayerInput, Is.True);
            Assert.That(_demo.Snapshot.Round, Is.EqualTo(2));
            Assert.That(_demo.Snapshot.Combatants[0].Hp, Is.EqualTo(114));
        }
    }
}

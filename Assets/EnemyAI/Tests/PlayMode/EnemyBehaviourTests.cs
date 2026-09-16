using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Semester.Enemies.Tests
{
    public class EnemyBehaviourTests
    {
        private GameObject enemyObject, playerObject, wall;
        private EnemyStateMachine enemy;
        private NavMeshData navData;
        private NavMeshDataInstance navInstance;
        private readonly Vector3 origin = new Vector3(1000f, 0f, 1000f);

        [UnitySetUp]
        public IEnumerator Setup()
        {
            var source = new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                transform = Matrix4x4.TRS(origin + Vector3.down * 0.25f, Quaternion.identity, Vector3.one),
                size = new Vector3(40f, 0.5f, 40f), area = 0
            };
            navData = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0),
                new List<NavMeshBuildSource> { source }, new Bounds(origin, new Vector3(45f, 10f, 45f)), Vector3.zero, Quaternion.identity);
            navInstance = NavMesh.AddNavMeshData(navData);
            enemyObject = new GameObject("Test Enemy");
            enemyObject.SetActive(false);
            enemyObject.transform.position = origin;
            enemyObject.AddComponent<NavMeshAgent>();
            enemy = enemyObject.AddComponent<EnemyStateMachine>();
            enemy.patrolHalfExtents = Vector2.zero;
            enemy.patrolWaitSeconds = new Vector2(100f, 100f);
            enemy.eyeHeight = 1f;
            enemy.targetHeight = 1f;
            enemy.turnSpeed = 720f;
            enemy.soundLookSeconds = 0.2f;
            enemy.alertSeconds = 0.1f;
            enemy.searchPauseSeconds = 0.01f;
            enemy.pursuitSpeed = 15f;
            enemyObject.SetActive(true);
            yield return null;
            Assert.IsTrue(enemyObject.GetComponent<NavMeshAgent>().isOnNavMesh);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(enemyObject);
            if (playerObject != null) Object.Destroy(playerObject);
            if (wall != null) Object.Destroy(wall);
            yield return null;
            navInstance.Remove();
            Object.Destroy(navData);
        }

        private void CreatePlayer(Vector3 offset)
        {
            playerObject = new GameObject("Test Player");
            playerObject.transform.position = origin + offset;
            enemy.player = playerObject.transform;
        }

        [UnityTest]
        public IEnumerator SoundBehindEnemyWorksWithoutVisiblePlayerAndOnlyTurns()
        {
            Vector3 start = enemyObject.transform.position;
            EnemyNoise.Emit(origin + Vector3.back * 5f, 10f);
            yield return null;
            Assert.AreEqual(EnemyStateMachine.State.SoundLook, enemy.CurrentState);
            Assert.AreEqual("?", enemy.indicator.text);
            Assert.IsFalse(enemy.HasLastSeenPosition);
            yield return new WaitForSeconds(0.3f);
            Assert.Greater(Vector3.Dot(enemyObject.transform.forward, Vector3.back), 0.98f);
            Assert.Less(Vector3.Distance(start, enemyObject.transform.position), 0.1f);
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(EnemyStateMachine.State.PatrolWait, enemy.CurrentState);
        }

        [UnityTest]
        public IEnumerator HearingUsesBothNoiseRadiusAndEnemyRange()
        {
            enemy.hearingDistance = 2f;
            EnemyNoise.Emit(origin + Vector3.right * 3f, 20f);
            yield return null;
            Assert.AreNotEqual(EnemyStateMachine.State.SoundLook, enemy.CurrentState);
            enemy.hearingDistance = 20f;
            EnemyNoise.Emit(origin + Vector3.right * 3f, 2f);
            yield return null;
            Assert.AreNotEqual(EnemyStateMachine.State.SoundLook, enemy.CurrentState);
        }

        [UnityTest]
        public IEnumerator OccludedPlayerIsNotSeenButSoundStillTriggers()
        {
            CreatePlayer(Vector3.forward * 6f);
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = origin + new Vector3(0f, 1f, 3f);
            wall.transform.localScale = new Vector3(4f, 4f, 1f);
            Physics.SyncTransforms();
            Assert.IsFalse(enemy.IsPlayerVisible());
            EnemyNoise.Emit(playerObject.transform.position, 10f, playerObject);
            yield return null;
            Assert.AreEqual(EnemyStateMachine.State.SoundLook, enemy.CurrentState);
            Assert.IsFalse(enemy.HasLastSeenPosition);
        }

        [UnityTest]
        public IEnumerator SightOverridesSimultaneousNoiseAndRecordsLastSeenOnly()
        {
            CreatePlayer(Vector3.forward * 5f);
            EnemyNoise.Emit(origin + Vector3.back * 5f, 10f);
            yield return null;
            Assert.AreEqual(EnemyStateMachine.State.Alert, enemy.CurrentState);
            Assert.AreEqual("!", enemy.indicator.text);
            Vector3 lastSeen = enemy.LastSeenPosition;
            playerObject.transform.position = origin + Vector3.back * 100f;
            yield return null;
            Assert.IsFalse(enemy.CanSeePlayer);
            Assert.AreEqual(lastSeen, enemy.LastSeenPosition);
            yield return new WaitForSeconds(0.15f);
            Assert.AreEqual(EnemyStateMachine.State.PursueLastSeen, enemy.CurrentState);
        }

        [UnityTest]
        public IEnumerator LostPlayerLeadsToSearchThenPatrol()
        {
            CreatePlayer(Vector3.forward * 2f);
            yield return null;
            playerObject.SetActive(false);
            float deadline = Time.time + 5f;
            while (enemy.CurrentState != EnemyStateMachine.State.Search && Time.time < deadline) yield return null;
            Assert.AreEqual(EnemyStateMachine.State.Search, enemy.CurrentState);
            deadline = Time.time + 5f;
            while (enemy.CurrentState != EnemyStateMachine.State.PatrolWait && Time.time < deadline) yield return null;
            Assert.AreEqual(EnemyStateMachine.State.PatrolWait, enemy.CurrentState);
        }

        [UnityTest]
        public IEnumerator PatrolChoosesReachableDestinationInsideBoundsAndMoves()
        {
            enemy.enabled = false;
            enemy.patrolHalfExtents = new Vector2(6f, 6f);
            enemy.minimumPatrolDistance = 3f;
            enemy.enabled = true;
            yield return null;
            var agent = enemyObject.GetComponent<NavMeshAgent>();
            Assert.IsTrue(agent.hasPath);
            Vector3 destination = agent.destination - origin;
            Assert.LessOrEqual(Mathf.Abs(destination.x), 6f);
            Assert.LessOrEqual(Mathf.Abs(destination.z), 6f);
            Assert.GreaterOrEqual(new Vector2(destination.x, destination.z).magnitude, 3f);
            yield return new WaitForSeconds(0.5f);
            Assert.Greater(Vector3.Distance(origin, enemyObject.transform.position), 0.2f);
        }

        [UnityTest]
        public IEnumerator PatrolArrivalWaitsBeforeChoosingAnotherDestination()
        {
            enemy.enabled = false;
            enemy.patrolHalfExtents = new Vector2(3f, 3f);
            enemy.minimumPatrolDistance = 1f;
            enemy.patrolSpeed = 10f;
            enemy.patrolWaitSeconds = new Vector2(0.6f, 0.6f);
            enemy.enabled = true;
            yield return null;
            Assert.AreEqual(EnemyStateMachine.State.Patrol, enemy.CurrentState);
            float deadline = Time.time + 5f;
            while (enemy.CurrentState != EnemyStateMachine.State.PatrolWait && Time.time < deadline) yield return null;
            Assert.AreEqual(EnemyStateMachine.State.PatrolWait, enemy.CurrentState);
            Vector3 arrival = enemy.transform.position;
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(EnemyStateMachine.State.PatrolWait, enemy.CurrentState);
            Assert.Less(Vector3.Distance(arrival, enemy.transform.position), 0.1f);
            yield return new WaitForSeconds(0.35f);
            Assert.AreEqual(EnemyStateMachine.State.Patrol, enemy.CurrentState);
        }

        [UnityTest]
        public IEnumerator DisabledEnemyDoesNotHearAndReenableHasNoStaleSound()
        {
            enemy.enabled = false;
            EnemyNoise.Emit(origin + Vector3.back * 3f, 10f);
            enemy.enabled = true;
            yield return null;
            Assert.AreNotEqual(EnemyStateMachine.State.SoundLook, enemy.CurrentState);
        }
    }
}

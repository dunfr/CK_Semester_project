using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

namespace CK.SemesterProject.Battle.Tests
{
    // 비워 둔 제작용 맵에 적을 저장하지 않고 통합 테스트 중에만 전투 공간을 만든다.
    internal sealed class TutorialBattleTestArena : IDisposable
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Vector3 Center = new Vector3(0f, 1000f, 0f);
        private readonly Component _battle;
        private readonly GameObject _root;
        private readonly NavMeshData _navigation;
        private NavMeshDataInstance _navigationInstance;

        public TutorialBattleTestArena(Component battle)
        {
            _battle = battle;
            Type enemyType = Type.GetType("Semester.Enemies.EnemyStateMachine, Semester.EnemyAI", true);
            Array sceneEnemies = (Array)Field("_enemies").GetValue(_battle);
            Assert.That(sceneEnemies.Length, Is.Zero, "제작용 씬에 기존 적 배치가 남아 있으면 안 됩니다.");

            _root = new GameObject("Temporary Battle Test Arena");
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Test Floor";
            floor.transform.SetParent(_root.transform);
            floor.transform.position = Center + Vector3.down * .5f;
            floor.transform.localScale = new Vector3(60f, 1f, 60f);
            var source = new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                transform = Matrix4x4.TRS(floor.transform.position, Quaternion.identity, Vector3.one),
                size = floor.transform.localScale,
                area = 0,
            };
            NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(0);
            _navigation = NavMeshBuilder.BuildNavMeshData(settings, new List<NavMeshBuildSource> { source },
                new Bounds(Center, new Vector3(60f, 8f, 60f)), Vector3.zero, Quaternion.identity);
            Assert.That(_navigation, Is.Not.Null);
            _navigationInstance = NavMesh.AddNavMeshData(_navigation);

            GameObject enemy = new GameObject("emey AI");
            enemy.SetActive(false);
            enemy.transform.SetParent(_root.transform);
            enemy.transform.position = Center;
            NavMeshAgent agent = enemy.AddComponent<NavMeshAgent>();
            agent.agentTypeID = settings.agentTypeID;
            agent.radius = .4f;
            agent.height = 2f;
            Component state = enemy.AddComponent(enemyType);
            enemyType.GetField("patrolSpeed").SetValue(state, 0f);
            enemyType.GetField("patrolHalfExtents").SetValue(state, Vector2.zero);
            enemyType.GetField("hearingDistance").SetValue(state, 0f);
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Test Enemy Body";
            body.transform.SetParent(enemy.transform, false);
            body.transform.localPosition = Vector3.up;
            body.transform.localScale = new Vector3(.8f, 2f, .8f);
            enemy.SetActive(true);

            Array enemies = Array.CreateInstance(enemyType, 1);
            enemies.SetValue(state, 0);
            Field("_enemies").SetValue(_battle, enemies);
            Field("_enemyEnabled").SetValue(_battle, new bool[1]);
            Component movement = (Component)Field("_movement").GetValue(_battle);
            movement.GetType().GetMethod("Teleport").Invoke(movement,
                new object[] { Center + Vector3.back * 2f + Vector3.up * .05f });
            Physics.SyncTransforms();
        }

        public void Dispose()
        {
            if (_battle != null)
            {
                Type enemyType = Field("_enemies").FieldType.GetElementType();
                Field("_enemies").SetValue(_battle, Array.CreateInstance(enemyType, 0));
                Field("_enemyEnabled").SetValue(_battle, Array.Empty<bool>());
            }
            if (_navigationInstance.valid)
            {
                _navigationInstance.Remove();
            }
            UnityEngine.Object.Destroy(_navigation);
            UnityEngine.Object.Destroy(_root);
        }

        private FieldInfo Field(string name)
        {
            return _battle.GetType().GetField(name, PrivateInstance);
        }
    }
}

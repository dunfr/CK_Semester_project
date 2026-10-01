using System;
using System.Linq;
using CK.SemesterProject.Tutorial;
using Semester.Enemies;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace CK.SemesterProject.Editor
{
    public static class TutorialGrayboxingIntegration
    {
        public const string OutputScene = "Assets/CK_Semester_Project/Prototype/Scenes/TutorialGrayboxingDemo.unity";
        private const string SourceScene = "Assets/CK_Semester_Project/Prototype/Scenes/MapGrayboxingFuchsia.unity";
        private const string PrototypeScene = "Assets/CK_Semester_Project/Prototype/Scenes/TutorialDemo.unity";
        private const string NavigationPath = "Assets/CK_Semester_Project/Prototype/Data/TutorialGrayboxingNavMesh.asset";

        // 명시적으로 재생성할 때만 실행한다. 가져온 원본 맵은 수정하지 않는다.
        [MenuItem("CK/Tutorial/Build Grayboxing Demo")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.OpenScene(SourceScene);
            GameObject player = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<PlayerMovement>(true)).Single().gameObject;
            Camera camera = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Camera>(true)).Single(item => item.CompareTag("MainCamera"));
            CameraController orbit = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CameraController>(true)).Single();

            Scene prototype = EditorSceneManager.OpenScene(PrototypeScene, OpenSceneMode.Additive);
            var sourceGroup = new GameObject("Tutorial Systems");
            SceneManager.MoveGameObjectToScene(sourceGroup, prototype);
            foreach (GameObject root in prototype.GetRootGameObjects())
            {
                if (root.name == "Canvas" || root.name == "Encounter Canvas"
                    || root.name == "Tutorial Battle" || root.name == "EventSystem")
                {
                    root.transform.SetParent(sourceGroup.transform, true);
                }
            }
            // 하나의 계층으로 복제해야 버튼/텍스트와 전투 제어기의 상호 참조가 함께 복제된다.
            GameObject systems = UnityEngine.Object.Instantiate(sourceGroup);
            systems.name = "Tutorial Systems";
            systems.transform.SetParent(null);
            SceneManager.MoveGameObjectToScene(systems, scene);
            EditorSceneManager.CloseScene(prototype, true);
            SceneManager.SetActiveScene(scene);

            TutorialBattleController battle = systems.GetComponentInChildren<TutorialBattleController>(true);
            var battleSettings = new SerializedObject(battle);
            battleSettings.FindProperty("_movement").objectReferenceValue = player.GetComponent<PlayerMovement>();
            battleSettings.FindProperty("_orbit").objectReferenceValue = orbit;
            battleSettings.FindProperty("_camera").objectReferenceValue = camera;
            battleSettings.FindProperty("_brain").objectReferenceValue = camera.GetComponent<CinemachineBrain>();
            battleSettings.FindProperty("_encounterSize").intValue = 1;
            battleSettings.FindProperty("_automaticEncounters").boolValue = true;
            // 그레이박스의 작은 실내 구역에서도 기존 전투 배치 검사를 통과하도록 간격을 줄인다.
            battleSettings.FindProperty("_battleSpacing").floatValue = 4f;

            Transform[] markers = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(item => item.name == "emey" || item.name.StartsWith("emey (", StringComparison.Ordinal))
                .OrderBy(item => item.name, StringComparer.Ordinal).ToArray();
            if (markers.Length == 0)
            {
                throw new InvalidOperationException("그레이박스 emey 몬스터를 찾을 수 없습니다.");
            }
            string[] definitions = { "Imprint", "Afterimage", "Oblivion" };
            var enemies = new EnemyStateMachine[markers.Length];
            for (int index = 0; index < markers.Length; index++)
            {
                Transform marker = markers[index];
                Renderer visual = marker.GetComponent<Renderer>();
                Bounds bounds = visual.bounds;
                var actor = new GameObject(marker.name + " AI");
                actor.transform.position = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                actor.transform.rotation = marker.rotation;
                SceneManager.MoveGameObjectToScene(actor, scene);
                marker.SetParent(actor.transform, true);
                NavMeshAgent agent = actor.AddComponent<NavMeshAgent>();
                agent.radius = 0.3f;
                agent.height = Mathf.Max(0.85f, bounds.size.y);
                agent.stoppingDistance = 0.15f;
                EnemyStateMachine enemy = actor.AddComponent<EnemyStateMachine>();
                enemy.player = player.transform;
                enemy.eyeHeight = agent.height * 0.8f;
                enemy.patrolHalfExtents = new Vector2(3f, 3f);
                enemy.minimumPatrolDistance = 1f;
                enemy.navMeshSampleRadius = 0.6f;
                enemy.patrolSpeed = 1.5f;
                enemy.pursuitSpeed = 2.5f;
                enemy.viewDistance = 8f;
                enemy.hearingDistance = 6f;
                enemy.indicatorOffset = Vector3.up * (agent.height + 0.3f);
                TutorialMonster monster = actor.AddComponent<TutorialMonster>();
                var monsterSettings = new SerializedObject(monster);
                monsterSettings.FindProperty("_definition").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MonsterBattleDefinition>(
                    "Assets/CK_Semester_Project/Prototype/Data/Monsters/" + definitions[index % definitions.Length] + ".asset");
                monsterSettings.ApplyModifiedPropertiesWithoutUndo();
                enemies[index] = enemy;
            }
            SerializedProperty enemyList = battleSettings.FindProperty("_enemies");
            enemyList.arraySize = enemies.Length;
            for (int index = 0; index < enemies.Length; index++)
            {
                enemyList.GetArrayElementAtIndex(index).objectReferenceValue = enemies[index];
            }
            battleSettings.ApplyModifiedPropertiesWithoutUndo();

            var navigation = new GameObject("Tutorial Grayboxing Navigation");
            NavMeshSurface surface = navigation.AddComponent<NavMeshSurface>();
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.1f;
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            NavMeshData existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavigationPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(surface.navMeshData, NavigationPath);
            }
            else
            {
                EditorUtility.CopySerialized(surface.navMeshData, existing);
                surface.RemoveData();
                surface.navMeshData = existing;
                surface.AddData();
            }
            foreach (EnemyStateMachine enemy in enemies)
            {
                NavMeshHit hit;
                if (!NavMesh.SamplePosition(enemy.transform.position, out hit, 1.5f, NavMesh.AllAreas))
                {
                    throw new InvalidOperationException(enemy.name + ": 시작 위치에 NavMesh가 없습니다.");
                }
                enemy.transform.position = hit.position;
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, OutputScene);
            PrototypeUiIntegration.ApplyCurrentScene();
            TutorialLevelIntegration.ApplyCurrentScene();
            Validate();
            Debug.Log("TUTORIAL_GRAYBOX_BUILD_PASS enemies=" + enemies.Length);
        }

        public static void Validate()
        {
            Scene scene = SceneManager.GetActiveScene();
            EnemyStateMachine[] enemies = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<EnemyStateMachine>(true)).ToArray();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                {
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) != 0)
                    {
                        throw new InvalidOperationException(item.name + ": 스크립트가 누락되었습니다.");
                    }
                }
            }
            if (enemies.Length != 10)
            {
                throw new InvalidOperationException("원본의 몬스터 10기와 연결 수가 다릅니다.");
            }
            foreach (EnemyStateMachine enemy in enemies)
            {
                if (enemy.player == null || enemy.GetComponent<TutorialMonster>().GetData() == null)
                {
                    throw new InvalidOperationException(enemy.name + ": 플레이어 또는 몬스터 정의가 누락되었습니다.");
                }
                NavMeshHit hit;
                if (!NavMesh.SamplePosition(enemy.transform.position, out hit, 0.3f, NavMesh.AllAreas))
                {
                    throw new InvalidOperationException(enemy.name + ": NavMesh 연결이 잘못되었습니다.");
                }
            }
            Debug.Log("TUTORIAL_GRAYBOX_VALIDATE_PASS enemies=" + enemies.Length);
        }
    }
}

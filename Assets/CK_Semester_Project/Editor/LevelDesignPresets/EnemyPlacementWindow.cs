using System.Collections.Generic;
using System.Linq;
using CK.SemesterProject.LevelDesign;
using CK.SemesterProject.Tutorial;
using Semester.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace CK.SemesterProject.LevelDesign.EditorTools
{
    public enum EnemyPlacementBehaviour
    {
        [InspectorName("제자리 감시")] Guard,
        [InspectorName("웨이포인트 순찰 (적 이동 프리셋)")] Route,
        [InspectorName("주변 무작위 순찰")] Wander
    }

    /// <summary>
    /// 적 배치 도구. 몬스터 종류와 행동을 고르고 씬 뷰를 클릭하면 적이 생성되고,
    /// 플레이어 감지 대상·전투 목록·(선택) 튜토리얼 경비병까지 자동으로 연결된다.
    /// </summary>
    public sealed class EnemyPlacementWindow : EditorWindow
    {
        private const string PrefabPath = "Assets/CK_Semester_Project/Prefabs/Characters/FieldEnemy.prefab";
        private const string DefinitionFilter = "t:MonsterBattleDefinition";

        [SerializeField] private GameObject _prefab;
        [SerializeField] private MonsterBattleDefinition _definition;
        [SerializeField] private Transform _parent;
        [SerializeField] private EnemyPlacementBehaviour _behaviour = EnemyPlacementBehaviour.Guard;
        [SerializeField] private bool _withTrigger = true;
        [SerializeField] private bool _addToBattle = true;
        [SerializeField] private bool _assignTutorialGuard;
        [SerializeField] private bool _isPlacing;

        private Vector2 _scroll;
        private MonsterBattleDefinition[] _definitions = new MonsterBattleDefinition[0];
        private bool _hasHoverPoint;
        private Vector3 _hoverPoint;

        [MenuItem("GameObject/CK Level Presets/적 생성 (Enemy)", false, 9)]
        [MenuItem("CK/적 배치 도구")]
        private static void Open()
        {
            GetWindow<EnemyPlacementWindow>("적 배치 도구").Show();
        }

        private void OnEnable()
        {
            if (_prefab == null)
            {
                _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            }
            RefreshDefinitions();
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            _isPlacing = false;
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.HelpBox("몬스터 종류와 행동을 고른 뒤 '클릭으로 배치'를 켜고 씬 뷰 바닥을 클릭하면 적이 생깁니다. (Esc로 배치 종료)", MessageType.None);

            _prefab = (GameObject)EditorGUILayout.ObjectField(new GUIContent("적 프리팹", "AI·전투 컴포넌트가 들어 있는 기본 적"), _prefab, typeof(GameObject), false);
            DrawDefinitionPopup();
            EditorGUILayout.BeginHorizontal();
            _parent = (Transform)EditorGUILayout.ObjectField(new GUIContent("넣을 위치", "생성된 적이 들어갈 부모(예: 구역의 Enemies). 비우면 씬 최상위"), _parent, typeof(Transform), true);
            if (GUILayout.Button("선택한 것", GUILayout.Width(70f)) && Selection.activeTransform != null)
            {
                _parent = Selection.activeTransform;
            }
            EditorGUILayout.EndHorizontal();

            _behaviour = (EnemyPlacementBehaviour)EditorGUILayout.EnumPopup(new GUIContent("행동"), _behaviour);
            if (_behaviour == EnemyPlacementBehaviour.Route)
            {
                EditorGUI.indentLevel++;
                _withTrigger = EditorGUILayout.Toggle(new GUIContent("트리거 반응 포함", "플레이어가 영역에 들어오면 지정 지점으로 이동했다가 복귀"), _withTrigger);
                EditorGUI.indentLevel--;
            }
            _addToBattle = EditorGUILayout.Toggle(new GUIContent("전투 대상에 등록", "부딪히거나 선제공격하면 전투가 시작됩니다."), _addToBattle);
            _assignTutorialGuard = EditorGUILayout.Toggle(new GUIContent("튜토리얼 경비병으로 지정", "01 구역 시작 연출(버튼 누르기, 20초 뒤 접근, 처치 시 카드 키)을 맡깁니다. 하나만 지정됩니다."), _assignTutorialGuard);

            EditorGUILayout.Space(6f);
            using (new EditorGUI.DisabledScope(_prefab == null || _definition == null))
            {
                EditorGUILayout.BeginHorizontal();
                Color previous = GUI.backgroundColor;
                if (_isPlacing)
                {
                    GUI.backgroundColor = new Color(1f, 0.6f, 0.3f);
                }
                if (GUILayout.Button(_isPlacing ? "클릭으로 배치 중… (끄기)" : "씬 뷰 클릭으로 배치", GUILayout.Height(28f)))
                {
                    _isPlacing = !_isPlacing;
                    SceneView.RepaintAll();
                }
                GUI.backgroundColor = previous;
                if (GUILayout.Button("씬 뷰 중앙에 생성", GUILayout.Height(28f)))
                {
                    CreateEnemy(LevelPresetUtility.GetSceneViewGroundPoint(Vector3.zero));
                }
                EditorGUILayout.EndHorizontal();
            }
            if (_prefab == null || _definition == null)
            {
                EditorGUILayout.HelpBox("적 프리팹과 몬스터 종류를 지정하세요.", MessageType.Warning);
            }

            EditorGUILayout.Space(8f);
            DrawSceneEnemies();
            EditorGUILayout.EndScrollView();
        }

        private void DrawDefinitionPopup()
        {
            if (_definitions.Length == 0)
            {
                _definition = (MonsterBattleDefinition)EditorGUILayout.ObjectField("몬스터 종류", _definition, typeof(MonsterBattleDefinition), false);
                return;
            }
            string[] names = _definitions.Select(definition => definition.name + "  (" + System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(definition))) + ")").ToArray();
            int index = System.Array.IndexOf(_definitions, _definition);
            EditorGUILayout.BeginHorizontal();
            int next = EditorGUILayout.Popup(new GUIContent("몬스터 종류", "능력치·AI 행동표 에셋"), Mathf.Max(0, index), names);
            if (GUILayout.Button("↻", GUILayout.Width(24f)))
            {
                RefreshDefinitions();
            }
            EditorGUILayout.EndHorizontal();
            _definition = _definitions[Mathf.Clamp(next, 0, _definitions.Length - 1)];
        }

        private void DrawSceneEnemies()
        {
            List<EnemyStateMachine> enemies = FindSceneEnemies();
            EditorGUILayout.LabelField("씬의 적 (" + enemies.Count + ")", EditorStyles.boldLabel);
            TutorialBattleController battle = FindFirstObjectByType<TutorialBattleController>();
            HashSet<EnemyStateMachine> registered = GetBattleEnemies(battle);
            foreach (EnemyStateMachine enemy in enemies)
            {
                EditorGUILayout.BeginHorizontal();
                var monster = enemy.GetComponent<TutorialMonster>();
                string type = monster != null && monster.Definition != null ? monster.Definition.name : "-";
                string mark = registered.Contains(enemy) ? "" : "  (전투 미등록)";
                EditorGUILayout.LabelField(enemy.name, type + mark);
                if (GUILayout.Button("선택", GUILayout.Width(44f)))
                {
                    Selection.activeGameObject = enemy.gameObject;
                    SceneView.lastActiveSceneView?.FrameSelected();
                }
                if (GUILayout.Button("삭제", GUILayout.Width(44f)))
                {
                    DeleteEnemy(enemy);
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
            }
            if (battle != null && GUILayout.Button("전투 목록을 씬의 적 전체로 맞추기"))
            {
                SetBattleEnemies(battle, enemies);
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!_isPlacing)
            {
                return;
            }
            Event current = Event.current;
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                _isPlacing = false;
                current.Use();
                Repaint();
                return;
            }
            Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            _hasHoverPoint = Physics.Raycast(ray, out RaycastHit hit, 500f, ~0, QueryTriggerInteraction.Ignore);
            if (_hasHoverPoint)
            {
                _hoverPoint = hit.point;
                Handles.color = new Color(1f, 0.5f, 0.2f);
                Handles.DrawWireDisc(_hoverPoint, Vector3.up, 0.45f, 2f);
                Handles.Label(_hoverPoint + Vector3.up * 1.2f, (_definition != null ? _definition.name : "") + " 배치", EditorStyles.whiteBoldLabel);
            }
            if (current.type == EventType.MouseMove)
            {
                sceneView.Repaint();
            }
            if (current.type == EventType.MouseDown && current.button == 0 && !current.alt && _hasHoverPoint)
            {
                CreateEnemy(_hoverPoint);
                current.Use();
            }
        }

        private void CreateEnemy(Vector3 position)
        {
            if (_prefab == null || _definition == null)
            {
                return;
            }
            if (NavMesh.SamplePosition(position, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
            {
                position = navHit.position;
            }
            var enemyObject = (GameObject)PrefabUtility.InstantiatePrefab(_prefab, EditorSceneManager.GetActiveScene());
            Undo.RegisterCreatedObjectUndo(enemyObject, "적 생성");
            if (_parent != null)
            {
                Undo.SetTransformParent(enemyObject.transform, _parent, "적 생성");
            }
            enemyObject.transform.position = position;
            Transform view = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.camera.transform : null;
            if (view != null)
            {
                Vector3 facing = Vector3.ProjectOnPlane(view.position - position, Vector3.up);
                if (facing.sqrMagnitude > 0.01f)
                {
                    enemyObject.transform.rotation = Quaternion.LookRotation(facing.normalized);
                }
            }
            enemyObject.name = MakeUniqueName("Enemy_" + _definition.name);

            var stateMachine = enemyObject.GetComponent<EnemyStateMachine>();
            var machineData = new SerializedObject(stateMachine);
            PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
            machineData.FindProperty("player").objectReferenceValue = player != null ? player.transform : null;
            machineData.FindProperty("_stationaryGuard").boolValue = _behaviour == EnemyPlacementBehaviour.Guard;
            machineData.FindProperty("patrolPoints").arraySize = 0;
            machineData.FindProperty("patrolCenter").objectReferenceValue = null;
            machineData.ApplyModifiedProperties();

            var monster = enemyObject.GetComponent<TutorialMonster>();
            var monsterData = new SerializedObject(monster);
            monsterData.FindProperty("_definition").objectReferenceValue = _definition;
            monsterData.ApplyModifiedProperties();

            if (_behaviour == EnemyPlacementBehaviour.Route)
            {
                LevelPresetUtility.CreateEnemyRoute(enemyObject.GetComponent<NavMeshAgent>(), _withTrigger);
            }
            if (_addToBattle)
            {
                TutorialBattleController battle = FindFirstObjectByType<TutorialBattleController>();
                if (battle != null)
                {
                    List<EnemyStateMachine> list = GetBattleEnemies(battle).ToList();
                    list.Add(stateMachine);
                    SetBattleEnemies(battle, list);
                }
            }
            if (_assignTutorialGuard)
            {
                TutorialStartSequence sequence = FindFirstObjectByType<TutorialStartSequence>();
                if (sequence != null)
                {
                    var sequenceData = new SerializedObject(sequence);
                    sequenceData.FindProperty("_guard").objectReferenceValue = stateMachine;
                    sequenceData.ApplyModifiedProperties();
                }
                _assignTutorialGuard = false;
            }
            EditorSceneManager.MarkSceneDirty(enemyObject.scene);
            Selection.activeGameObject = enemyObject;
            Repaint();
        }

        private static void DeleteEnemy(EnemyStateMachine enemy)
        {
            TutorialBattleController battle = FindFirstObjectByType<TutorialBattleController>();
            if (battle != null)
            {
                SetBattleEnemies(battle, GetBattleEnemies(battle).Where(item => item != enemy).ToList());
            }
            // 이 적을 움직이던 적 이동 프리셋도 함께 지운다.
            foreach (EnemyRoutePreset route in FindObjectsByType<EnemyRoutePreset>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (route.Enemy != null && route.Enemy.gameObject == enemy.gameObject)
                {
                    Undo.DestroyObjectImmediate(route.gameObject);
                }
            }
            UnityEngine.SceneManagement.Scene scene = enemy.gameObject.scene;
            Undo.DestroyObjectImmediate(enemy.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static List<EnemyStateMachine> FindSceneEnemies()
        {
            return FindObjectsByType<EnemyStateMachine>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(enemy => enemy.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(enemy))
                .OrderBy(enemy => enemy.name)
                .ToList();
        }

        private static HashSet<EnemyStateMachine> GetBattleEnemies(TutorialBattleController battle)
        {
            var result = new HashSet<EnemyStateMachine>();
            if (battle == null)
            {
                return result;
            }
            SerializedProperty list = new SerializedObject(battle).FindProperty("_enemies");
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue is EnemyStateMachine enemy)
                {
                    result.Add(enemy);
                }
            }
            return result;
        }

        private static void SetBattleEnemies(TutorialBattleController battle, IEnumerable<EnemyStateMachine> enemies)
        {
            var data = new SerializedObject(battle);
            SerializedProperty list = data.FindProperty("_enemies");
            EnemyStateMachine[] items = enemies.Where(enemy => enemy != null).Distinct().ToArray();
            list.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }
            data.ApplyModifiedProperties();
        }

        private void RefreshDefinitions()
        {
            _definitions = AssetDatabase.FindAssets(DefinitionFilter)
                .Select(guid => AssetDatabase.LoadAssetAtPath<MonsterBattleDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(definition => definition != null)
                .OrderBy(definition => AssetDatabase.GetAssetPath(definition))
                .ToArray();
            if (_definition == null && _definitions.Length > 0)
            {
                _definition = _definitions[0];
            }
        }

        private static string MakeUniqueName(string baseName)
        {
            var names = new HashSet<string>(FindSceneEnemies().Select(enemy => enemy.name));
            for (int i = 1; ; i++)
            {
                string candidate = baseName + "_" + i.ToString("00");
                if (!names.Contains(candidate))
                {
                    return candidate;
                }
            }
        }
    }
}

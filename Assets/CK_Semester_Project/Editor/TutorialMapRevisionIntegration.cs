using System;
using System.Linq;
using CK.SemesterProject.Tutorial;
using Semester.Enemies;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace CK.SemesterProject.Editor
{
    public static class TutorialMapRevisionIntegration
    {
        private const string SourceScene = "Assets/CK_Semester_Project/Prototype/Scenes/MapGrayboxingFuchsia.unity";
        private const string DataFolder = "Assets/CK_Semester_Project/Prototype/Data/";
        private static Transform _content;
        private static Transform _map;
        private static TutorialLevelFlow _flow;
        private static Material _neutral;

        public static void ReplaceCurrentMap()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != TutorialGrayboxingIntegration.OutputScene || scene.GetRootGameObjects().Any(item => item.name == "Dummy Modeling Map"))
            {
                throw new InvalidOperationException("기존 TutorialGrayboxingDemo 씬에서 한 번만 실행하세요.");
            }
            System.IO.File.Copy(scene.path, "Temp/TutorialBeforeMapRevision.unity", true);
            _flow = UnityEngine.Object.FindFirstObjectByType<TutorialLevelFlow>();
            _content = _flow.transform;
            var settings = new SerializedObject(_flow);
            PlayerMovement movement = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();
            var battle = UnityEngine.Object.FindFirstObjectByType<TutorialBattleController>();
            var enemies = UnityEngine.Object.FindObjectsByType<EnemyStateMachine>(FindObjectsSortMode.None)
                .OrderBy(item => item.name == "emey AI" ? 0 : 1).ThenBy(item => item.name, StringComparer.Ordinal).ToArray();
            Scene source = SceneManager.GetSceneByPath(SourceScene);
            if (!source.IsValid() || !source.isLoaded)
            {
                source = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Additive);
            }
            GameObject map = UnityEngine.Object.Instantiate(source.GetRootGameObjects().Single(item => item.name == "Dummy Modeling Map"));
            map.name = "Dummy Modeling Map";
            SceneManager.MoveGameObjectToScene(map, scene);
            EditorSceneManager.CloseScene(source, true);
            SceneManager.SetActiveScene(scene);
            _map = map.transform;
            _neutral = AssetDatabase.LoadAssetAtPath<Material>("Assets/CK_Semester_Project/Prototype/Materials/GrayboxingNeutral.mat");
            foreach (GameObject root in scene.GetRootGameObjects().Where(item => new[]
                { "stage01", "stage01-1", "stage02", "stage03", "ceiling", "FuchsiaUpdatedAnimated" }.Contains(item.name)).ToArray())
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
            foreach (Transform child in _content.Cast<Transform>().ToArray())
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
            Transform start = Point("Capsule Start", new Vector3(-4.472f, 1.05f, 0.884f));
            movement.transform.SetPositionAndRotation(start.position, Quaternion.Euler(0, 180, 0));
            var floor = new SerializedObject(movement.GetComponent<FloorTravelButtons>());
            floor.FindProperty("_secondFloorPosition").vector3Value = new Vector3(0, -119.52f, 42);
            floor.FindProperty("_showButtons").boolValue = false;
            floor.ApplyModifiedPropertiesWithoutUndo();
            Set(settings, "_startPoint", start);
            Set(settings, "_capsuleLid", CreateCapsulePress(start.position));
            Set(settings, "_guardConsolePoint", Point("Guard Console Point", new Vector3(0, .32f, -5.3f)));
            Set(settings, "_consoleFocus", Point("Console Focus", new Vector3(0, 1.2f, -7.2f)));
            GameObject card = Cube("Console Card Key", new Vector3(0, 1.13f, -7.2f), new Vector3(.32f, .03f, .2f),
                MaterialAsset("TutorialCardKey", new Color(.15f, .85f, 1f)));
            card.GetComponent<Collider>().isTrigger = true;
            card.SetActive(false);
            Set(settings, "_cardKey", card.transform);
            var effects = _content.gameObject.AddComponent<TutorialSequenceEffects>();
            Set(settings, "_effects", effects);
            var effectSettings = new SerializedObject(effects);
            Material alarm = MaterialAsset("TutorialCapsuleAlarm", new Color(.8f, .03f, .04f));
            Set(effectSettings, "_alarmMaterial", alarm);
            Set(effectSettings, "_effectMaterial", MaterialAsset("TutorialSequenceEffect", new Color(.6f, .8f, .85f)));
            Renderer[] capsules = _map.GetComponentsInChildren<Renderer>().Where(item =>
                item.bounds.center.y > .4f && item.bounds.center.y < 1 && Mathf.Abs(item.bounds.size.x - 1.2f) < .02f).ToArray();
            SetArray(effectSettings, "_capsuleIndicators", capsules.Select((item, index) => Cube("Capsule Indicator " + index,
                item.bounds.center + Vector3.up * .43f, new Vector3(.6f, .04f, .08f), _neutral).GetComponent<Renderer>()).ToArray());
            effectSettings.ApplyModifiedPropertiesWithoutUndo();

            CutOpening(new Bounds(new Vector3(0, 2.3f, 13.781f), new Vector3(3, 4, 1.2f)));
            CutOpening(new Bounds(new Vector3(0, 2.3f, 45.031f), new Vector3(6, 4, 1.2f)));
            CutOpening(new Bounds(new Vector3(0, -117.6f, 45.031f), new Vector3(6, 4, 1.2f)));
            CutOpening(new Bounds(new Vector3(-32.5f, -117.6f, 30.281f), new Vector3(1.2f, 4, 3)));
            CutOpening(new Bounds(new Vector3(-3, -117.6f, 26.78f), new Vector3(4, 4, 1.2f)));
            CutOpening(new Bounds(new Vector3(-32.5f, -117.6f, 21.28f), new Vector3(1.2f, 4, 6)));
            CutOpening(new Bounds(new Vector3(20, -117.6f, -24.584f), new Vector3(5, 4, 1.2f)));
            CutOpening(new Bounds(new Vector3(20, -117.6f, -18.084f), new Vector3(5, 4, 1.2f)));
            CutOpening(new Bounds(new Vector3(37.863f, -113.6f, -8.084f), new Vector3(1.2f, 4, 6)));
            Cube("Upper Exit Landing", new Vector3(40.4f, -115.7f, -8.084f), new Vector3(6, .2f, 6), _neutral);

            Transform car = Point("Elevator Car", new Vector3(0, .3f, 50.5f));
            foreach (Renderer renderer in _map.GetComponentsInChildren<Renderer>().ToArray())
            {
                Bounds bounds = renderer.bounds;
                bool inCar = Mathf.Abs(bounds.center.x) < 5 && bounds.center.z > 45.3f;
                if (inCar && bounds.min.y >= .28f && bounds.max.y < 7)
                {
                    renderer.transform.SetParent(car, true);
                }
                else if (inCar && bounds.min.y >= -119.72f && bounds.max.y < -112.8f)
                {
                    renderer.gameObject.SetActive(false);
                }
            }
            var boarding = new GameObject("Elevator Boarding Zone").AddComponent<BoxCollider>();
            boarding.transform.SetParent(car, false);
            boarding.center = new Vector3(0, 1.5f, 0);
            boarding.size = new Vector3(8, 3, 8);
            boarding.isTrigger = true;
            Set(settings, "_elevatorBoardingZone", boarding);
            Set(settings, "_elevatorCar", car);
            Set(settings, "_elevatorDestination", Point("Elevator Destination", new Vector3(0, -119.6f, 50.5f)));
            Set(settings, "_arrivalPoint", Point("Elevator Checkpoint", new Vector3(0, -119.5f, 42)));
            Set(settings, "_ductCheckpoint", Point("Duct Checkpoint", new Vector3(-34.78f, -109.5f, -18)));
            Set(settings, "_hallCheckpoint", Point("Hall Checkpoint", new Vector3(20, -119.5f, -20)));
            Set(settings, "_observationNoisePoint", Point("Guard Noise Point", new Vector3(-34.78f, -119.5f, 12)));
            Set(settings, "_ductSection", _map.Find("Box5259"));
            TMP_FontAsset font = ((TMP_Text)settings.FindProperty("_objective").objectReferenceValue).font;
            var labelObject = new GameObject("Elevator Floor Display", typeof(TextMeshPro));
            labelObject.transform.SetParent(car, false);
            labelObject.transform.localPosition = new Vector3(0, 2.2f, 2.6f);
            labelObject.transform.localRotation = Quaternion.Euler(0, 180, 0);
            var label = labelObject.GetComponent<TextMeshPro>();
            label.font = font;
            label.fontSize = 2.5f;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(2, 1);
            label.text = "하층 이동\n좌클릭";
            Set(settings, "_elevatorStatus", label);
            var doors = new[]
            {
                Door("Tutorial Card Door", TutorialFieldAction.CardDoor, new Vector3(0, 2.3f, 13.781f), new Vector3(3, 4, .25f), "카드키로 출입문 열기"),
                Door("Elevator Entry Door", TutorialFieldAction.ElevatorDoor, new Vector3(0, 2.3f, 45.031f), new Vector3(6, 4, .25f), "엘리베이터 문 열기"),
                Door("Elevator Arrival Door", TutorialFieldAction.ArrivalDoor, new Vector3(0, -117.6f, 45.031f), new Vector3(6, 4, .25f), "도착 문"),
                Door("Corridor Card Door", TutorialFieldAction.CorridorCardDoor, new Vector3(-32.5f, -117.6f, 30.281f), new Vector3(.25f, 4, 3), "카드키 문 열기")
            };
            Set(settings, "_corridorCardDoor", doors[3]);
            TutorialFieldInteractable panel = car.gameObject.AddComponent<TutorialFieldInteractable>();
            Configure(panel, TutorialFieldAction.ElevatorPanel, car, Point("Elevator Panel Point", new Vector3(0, 1.4f, 52.1f), car), "하층으로 이동");
            TutorialFieldInteractable pickup = _content.gameObject.AddComponent<TutorialFieldInteractable>();
            Configure(pickup, TutorialFieldAction.CardKey, card.transform, Point("Card Key Pickup Point", new Vector3(0, 1.5f, -6.8f)), "카드키 획득");
            SetArray(settings, "_doors", doors);
            var interaction = new SerializedObject(_content.GetComponent<TutorialFieldInteraction>());
            SetArray(interaction, "_targets", doors.Concat(new[] { panel, pickup }).ToArray());
            interaction.ApplyModifiedPropertiesWithoutUndo();

            Vector3[] positions =
            {
                new Vector3(0, .32f, -2.8f), new Vector3(-3, -119.58f, 30), new Vector3(-12, -119.58f, 31),
                new Vector3(-22, -119.58f, 29), new Vector3(-43, -119.58f, 12), new Vector3(-46, -119.58f, -8),
                new Vector3(-41, -119.58f, -30), new Vector3(-38, -119.58f, -32),
                new Vector3(-22, -119.58f, -35), new Vector3(-8, -119.58f, -36)
            };
            for (int index = 0; index < enemies.Length; index++)
            {
                EnemyStateMachine enemy = enemies[index];
                enemy.transform.position = positions[index];
                enemy.transform.rotation = Quaternion.Euler(0, index < 2 ? 180 : 90, 0);
                enemy.viewDistance = index == 0 ? 30 : 10;
                enemy.hearingDistance = index == 0 ? 0 : 12;
                enemy.patrolHalfExtents = Vector2.zero;
                enemy.patrolWaitSeconds = new Vector2(1.5f, 2.5f);
                enemy.minimumPatrolDistance = .2f;
                enemy.patrolPoints = index == 0 || index == 1 || index == 6 || index == 7 ? Array.Empty<Transform>()
                    : new[] { Point(enemy.name + " Route A", positions[index] + (index == 4 || index == 5 ? Vector3.forward : Vector3.right) * 3),
                        Point(enemy.name + " Route B", positions[index] - (index == 4 || index == 5 ? Vector3.forward : Vector3.right) * 3) };
                var enemySettings = new SerializedObject(enemy);
                enemySettings.FindProperty("_maximumSightHeightDifference").floatValue = 2.5f;
                enemySettings.FindProperty("_stationaryGuard").boolValue = enemy.patrolPoints.Length == 0;
                enemySettings.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(enemy);
            }
            Set(settings, "_ductLookout", enemies[1]);
            SetArray(settings, "_ductInvestigators", new[] { enemies[2], enemies[3] });
            SetArray(settings, "_collapseInvestigators", new[] { enemies[6], enemies[7], enemies[8], enemies[9] });
            // 유리 너머 시선은 통과시키고 실제 벽 구간의 차폐는 유지한다.
            ConfigureGlass();
            Cube("Corridor Guard Desk", new Vector3(-3, -119.1f, 28.5f), new Vector3(1.05f, 1, .75f), _neutral);
            Trigger("Observe Guard", new Vector3(-3, -118.5f, 22), new Vector3(4, 2.2f, 3), TutorialLevelStage.DuctRamp);
            Trigger("Duct Observation", new Vector3(-33, -108.5f, -22), new Vector3(4, 2, 4), TutorialLevelStage.DuctObservation);
            Trigger("Duct Collapse", new Vector3(-29.04f, -108.5f, -26.77f), new Vector3(4, 2, 4), TutorialLevelStage.DuctEscape, true);
            Trigger("Enter Hall", new Vector3(20, -118.5f, -22.5f), new Vector3(5, 2.2f, 2), TutorialLevelStage.Hall);
            Trigger("Reach Upper Floor", new Vector3(36, -114.5f, -8.084f), new Vector3(2.5f, 2, 5), TutorialLevelStage.UpperExit);
            Trigger("Tutorial Exit", new Vector3(41, -114.5f, -8.084f), new Vector3(2, 2, 5), TutorialLevelStage.Completed);
            Vector3[] goals = { start.position, positions[0], doors[0].InteractionPosition, new Vector3(0, .4f, 51),
                new Vector3(-3, -119.5f, 22), new Vector3(-34.78f, -119.5f, 13), new Vector3(-33, -109.5f, -22),
                new Vector3(-29, -109.5f, -26.77f), new Vector3(29, -119.5f, -8), new Vector3(41, -115.5f, -8), new Vector3(41, -115.5f, -8) };
            SetArray(settings, "_goalPoints", goals.Select((position, index) => Point("Goal " + index, position)).ToArray());
            settings.ApplyModifiedPropertiesWithoutUndo();
            var allEnemies = enemies.ToList();
            for (int index = 0; index < 2; index++)
            {
                EnemyStateMachine guard = UnityEngine.Object.Instantiate(enemies[8 + index]);
                guard.name = "Hall Guard " + (index + 1) + " AI";
                Vector3 position = new Vector3(14 + index * 7, -119.58f, -7 - index * 7);
                guard.transform.position = position;
                guard.patrolPoints = new[] { Point(guard.name + " Route A", position + Vector3.right * 3),
                    Point(guard.name + " Route B", position - Vector3.right * 3) };
                allEnemies.Add(guard);
            }
            var battleSettings = new SerializedObject(battle);
            SetArray(battleSettings, "_enemies", allEnemies.ToArray());
            battleSettings.ApplyModifiedPropertiesWithoutUndo();
            BakeNavigation(allEnemies.ToArray());
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        public static void UpdateCapsulePress()
        {
            _flow = UnityEngine.Object.FindFirstObjectByType<TutorialLevelFlow>();
            _content = _flow.transform;
            _neutral = AssetDatabase.LoadAssetAtPath<Material>("Assets/CK_Semester_Project/Prototype/Materials/GrayboxingNeutral.mat");
            Transform fluid = _content.Find("Capsule Red Fluid");
            if (fluid != null)
            {
                UnityEngine.Object.DestroyImmediate(fluid.gameObject);
            }
            var settings = new SerializedObject(_flow);
            Transform start = (Transform)settings.FindProperty("_startPoint").objectReferenceValue;
            Set(settings, "_capsuleLid", CreateCapsulePress(start.position));
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        private static Transform CreateCapsulePress(Vector3 start)
        {
            Transform existing = _content.Find("Capsule Press");
            GameObject press = existing == null
                ? Cube("Capsule Press", start + Vector3.up * 3.2f, new Vector3(1.35f, .25f, 2.65f), _neutral)
                : existing.gameObject;
            Collider collider = press.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
            return press.transform;
        }

        public static void RebuildNavigation()
        {
            BakeNavigation(UnityEngine.Object.FindObjectsByType<EnemyStateMachine>(FindObjectsSortMode.None));
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        public static void RefreshObservationGlass()
        {
            _map = GameObject.Find("Dummy Modeling Map").transform;
            ConfigureGlass();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        private static void ConfigureGlass()
        {
            Material glass = AssetDatabase.LoadAssetAtPath<Material>(DataFolder + "TutorialObservationGlass.mat");
            foreach (Renderer renderer in _map.GetComponentsInChildren<Renderer>())
            {
                Bounds bounds = renderer.bounds;
                if (Mathf.Abs(bounds.center.z - 26.78f) < .7f && bounds.center.x < -6 && bounds.center.x > -29
                    && bounds.center.y < -113 && bounds.size.z < 1.2f && bounds.size.y >= 2)
                {
                    // 두 유리 구간만 투명하게 바꾸고 중간 벽은 차폐물로 유지한다.
                    if (bounds.center.x > -13 || bounds.center.x < -23)
                    {
                        renderer.sharedMaterial = glass;
                        renderer.gameObject.layer = 2;
                    }
                }
            }
            foreach (string name in new[] { "Box4632", "Box4677", "Box4682", "Box4687", "Box4697", "Box4702" })
            {
                Transform window = _map.Find(name);
                window.GetComponent<Renderer>().sharedMaterial = glass;
                window.gameObject.layer = 2;
            }
            foreach (EnemyStateMachine enemy in UnityEngine.Object.FindObjectsByType<EnemyStateMachine>(FindObjectsSortMode.None))
            {
                enemy.sightBlockingLayers &= ~(1 << 2);
            }
        }

        private static void BakeNavigation(EnemyStateMachine[] enemies)
        {
            var surface = GameObject.Find("Tutorial Grayboxing Navigation").GetComponent<NavMeshSurface>();
            surface.RemoveData();
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideVoxelSize = true;
            surface.voxelSize = .12f;
            Physics.SyncTransforms();
            Collider[] actorColliders = enemies.SelectMany(enemy => enemy.GetComponentsInChildren<Collider>())
                .Concat(UnityEngine.Object.FindFirstObjectByType<PlayerMovement>().GetComponentsInChildren<Collider>()).Where(item => item.enabled).ToArray();
            foreach (Collider collider in actorColliders)
            {
                collider.enabled = false;
            }
            try
            {
                surface.BuildNavMesh();
            }
            finally
            {
                foreach (Collider collider in actorColliders)
                {
                    collider.enabled = true;
                }
            }
            var asset = AssetDatabase.LoadAssetAtPath<NavMeshData>(DataFolder + "TutorialGrayboxingNavMesh.asset");
            EditorUtility.CopySerialized(surface.navMeshData, asset);
            asset.name = "TutorialGrayboxingNavMesh";
            surface.RemoveData();
            surface.navMeshData = asset;
            surface.AddData();
            EditorUtility.SetDirty(asset);
            foreach (EnemyStateMachine enemy in enemies)
            {
                if (!NavMesh.SamplePosition(enemy.transform.position, out NavMeshHit hit, 1, NavMesh.AllAreas))
                {
                    throw new InvalidOperationException(enemy.name + ": 신규 맵에 이동 가능한 바닥이 없습니다.");
                }
                enemy.transform.position = hit.position;
            }
        }

        private static void CutOpening(Bounds opening)
        {
            foreach (Renderer renderer in _map.GetComponentsInChildren<Renderer>().ToArray())
            {
                Bounds bounds = renderer.bounds;
                if (bounds.size.y >= 1 && (bounds.size.x < 1.6f || bounds.size.z < 1.6f)
                    && bounds.min.y < opening.max.y - .05f && bounds.max.y > opening.min.y + .05f && bounds.Intersects(opening))
                {
                    renderer.gameObject.SetActive(false);
                }
            }
        }

        private static TutorialFieldInteractable Door(string name, TutorialFieldAction kind, Vector3 center, Vector3 size, string prompt)
        {
            GameObject door = Cube(name, center, size, _neutral);
            door.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            var obstacle = door.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = Vector3.one;
            obstacle.carving = true;
            var result = door.AddComponent<TutorialFieldInteractable>();
            Configure(result, kind, door.transform, Point(name + " Interaction Point", center), prompt);
            return result;
        }

        private static void Configure(TutorialFieldInteractable target, TutorialFieldAction action, Transform moving, Transform point, string prompt)
        {
            var settings = new SerializedObject(target);
            Set(settings, "_flow", _flow);
            Set(settings, "_movingPart", moving);
            Set(settings, "_interactionPoint", point);
            settings.FindProperty("_kind").enumValueIndex = (int)action;
            settings.FindProperty("_prompt").stringValue = prompt;
            settings.FindProperty("_openOffset").vector3Value = Vector3.up * 4.2f;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Trigger(string name, Vector3 center, Vector3 size, TutorialLevelStage destination, bool collapse = false)
        {
            GameObject item = Point(name, center).gameObject;
            var collider = item.AddComponent<BoxCollider>();
            collider.size = size;
            collider.isTrigger = true;
            var body = item.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var settings = new SerializedObject(item.AddComponent<TutorialLevelTrigger>());
            Set(settings, "_flow", _flow);
            settings.FindProperty("_destination").enumValueIndex = (int)destination;
            settings.FindProperty("_collapse").boolValue = collapse;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform Point(string name, Vector3 position, Transform parent = null)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent == null ? _content : parent, true);
            item.transform.position = position;
            return item.transform;
        }

        private static GameObject Cube(string name, Vector3 position, Vector3 size, Material material)
        {
            GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name;
            item.transform.SetParent(_content);
            item.transform.position = position;
            item.transform.localScale = size;
            item.GetComponent<Renderer>().sharedMaterial = material;
            return item;
        }

        private static Material MaterialAsset(string name, Color color)
        {
            string path = DataFolder + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                material.SetColor("_BaseColor", color);
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }

        private static void Set(SerializedObject settings, string name, UnityEngine.Object value)
        {
            settings.FindProperty(name).objectReferenceValue = value;
        }

        private static void SetArray<T>(SerializedObject settings, string name, T[] values) where T : UnityEngine.Object
        {
            SerializedProperty array = settings.FindProperty(name);
            array.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                array.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }
        }
    }
}

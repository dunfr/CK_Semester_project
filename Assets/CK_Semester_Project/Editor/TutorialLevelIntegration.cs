using System;
using System.Linq;
using CK.SemesterProject.Tutorial;
using Semester.Enemies;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace CK.SemesterProject.Editor
{
    public static class TutorialLevelIntegration
    {
        private const string UiFolder = "Assets/CK_Semester_Project/Prototype/Temp/Graphics/UI/10-01/Battle_State/";

        // MCP 또는 명시적인 데모 재생성 단계에서 실행한다. 원본 맵은 수정하지 않는다.
        public static void ApplyCurrentScene()
        {
            if (UnityEngine.Object.FindFirstObjectByType<TutorialLevelFlow>() != null)
            {
                throw new InvalidOperationException("콘텐츠가 이미 연결된 씬입니다. 중복 생성하지 않습니다.");
            }
            var movement = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();
            var battle = UnityEngine.Object.FindFirstObjectByType<TutorialBattleController>();
            var hud = UnityEngine.Object.FindFirstObjectByType<PrototypeHudPresenter>();
            Transform field = GameObject.Find("Default_State").transform;
            TMP_FontAsset font = field.GetComponentsInChildren<TMP_Text>(true).First().font;
            var root = new GameObject("Tutorial Level Content");
            TutorialLevelFlow flow = root.AddComponent<TutorialLevelFlow>();
            TutorialFieldInteraction interaction = root.AddComponent<TutorialFieldInteraction>();
            var settings = new SerializedObject(flow);
            Set(settings, "_movement", movement);
            Set(settings, "_battle", battle);
            EnemyStateMachine firstEnemy = GameObject.Find("emey AI").GetComponent<EnemyStateMachine>();
            Set(settings, "_firstEnemy", firstEnemy);
            var firstMonsterSettings = new SerializedObject(firstEnemy.GetComponent<TutorialMonster>());
            const string definitionPath = "Assets/CK_Semester_Project/Prototype/Data/TutorialFirstEncounter.asset";
            MonsterBattleDefinition definition = AssetDatabase.LoadAssetAtPath<MonsterBattleDefinition>(definitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<MonsterBattleDefinition>();
                EditorUtility.CopySerialized(firstMonsterSettings.FindProperty("_definition").objectReferenceValue, definition);
                var definitionSettings = new SerializedObject(definition);
                definitionSettings.FindProperty("_id").stringValue = "tutorial_console_guard";
                definitionSettings.FindProperty("_displayName").stringValue = "콘솔 경비병";
                definitionSettings.FindProperty("_maxHp").intValue = 240;
                definitionSettings.FindProperty("_power").intValue = 25;
                definitionSettings.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(definition, definitionPath);
            }
            Set(firstMonsterSettings, "_definition", definition);
            firstMonsterSettings.ApplyModifiedPropertiesWithoutUndo();

            Transform start = Point(root.transform, "Capsule Start", new Vector3(-57.2f, 47.02f, 4.4f));
            movement.transform.position = start.position;
            movement.transform.rotation = Quaternion.Euler(0, -110, 0);
            var movementSettings = new SerializedObject(movement);
            movementSettings.FindProperty("_moveSpeed").floatValue = 3f;
            movementSettings.ApplyModifiedPropertiesWithoutUndo();
            Set(settings, "_startPoint", start);
            Transform lid = GameObject.Find("stage01/cap").GetComponentsInChildren<Renderer>()
                .OrderBy(item => Vector3.Distance(item.bounds.center, start.position)).First().transform;
            Set(settings, "_capsuleLid", lid);

            Transform car = GameObject.Find("stage01-1/elevator").transform;
            Transform lowerCar = GameObject.Find("stage01-1/elevator (1)").transform;
            Transform destination = Point(root.transform, "Elevator Destination", lowerCar.position);
            lowerCar.gameObject.SetActive(false);
            Set(settings, "_elevatorCar", car);
            Set(settings, "_elevatorDestination", destination);
            Set(settings, "_arrivalPoint", Point(root.transform, "Elevator Checkpoint", new Vector3(-32.5f, 8.7f, -0.08f)));
            Set(settings, "_ductCheckpoint", Point(root.transform, "Duct Checkpoint", new Vector3(-52.9f, 13.2f, 18.91f)));
            Set(settings, "_hallCheckpoint", Point(root.transform, "Hall Checkpoint", new Vector3(-52.67f, 8.7f, 10.5f)));
            Set(settings, "_observationNoisePoint", Point(root.transform, "Guard Noise Point", new Vector3(-53f, 8.8f, 24f)));
            Set(settings, "_ductSection", GameObject.Find("stage02/duct/Cube (24)").transform);

            TMP_Text objective = field.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == "Runtime Tutorial");
            objective.fontSize = 18;
            objective.rectTransform.sizeDelta = new Vector2(342, 92);
            Set(settings, "_objective", objective);
            Set(settings, "_floorTravel", movement.GetComponent<FloorTravelButtons>());
            Set(settings, "_camera", Camera.main);
            TMP_Text routeHint = UiPanel(field, "Runtime Route Hint", new Vector2(0, -74), new Vector2(390, 38), font);
            RectTransform routeRect = (RectTransform)routeHint.transform.parent;
            routeRect.anchorMin = routeRect.anchorMax = new Vector2(.5f, 1);
            routeHint.fontSize = 17;
            Set(settings, "_routeHint", routeHint);
            TMP_Text location = field.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == "Runtime Location");
            location.enableAutoSizing = true;
            location.fontSizeMin = 14;
            location.fontSizeMax = 22;
            location.enableWordWrapping = false;
            Set(settings, "_location", location);
            TMP_Text feedback = UiPanel(field, "Runtime Level Feedback", new Vector2(0, 132), new Vector2(630, 58), font);
            feedback.fontSize = 18;
            feedback.transform.parent.gameObject.SetActive(false);
            Set(settings, "_feedback", feedback);
            TMP_Text hint = UiPanel(field, "Runtime Interaction Hint", new Vector2(0, 72), new Vector2(470, 42), font);
            TMP_Text crosshair = Label(field, "Runtime Aim", font);
            crosshair.text = "+";
            crosshair.fontSize = 22;
            crosshair.color = new Color(0.75f, 0.95f, 1, 0.6f);
            crosshair.rectTransform.anchorMin = crosshair.rectTransform.anchorMax = new Vector2(.5f, .5f);
            crosshair.rectTransform.sizeDelta = new Vector2(24, 24);

            Transform panel = car.Find("Cube (19)");
            TMP_Text elevatorStatus = WorldLabel(panel, "Elevator Floor Display", font);
            elevatorStatus.transform.localPosition = new Vector3(-.3f, 1.1f, 0);
            elevatorStatus.transform.localRotation = Quaternion.Euler(0, -90, 0);
            elevatorStatus.text = "상층 / 하층 이동\n좌클릭";
            Set(settings, "_elevatorStatus", elevatorStatus);

            TutorialFieldInteractable firstDoor = Door("stage01/door/Door", TutorialFieldAction.CardDoor, "카드키로 출입문 열기", flow, root.transform);
            TutorialFieldInteractable elevatorDoor = Door("stage01/door/Door (1)", TutorialFieldAction.ElevatorDoor, "카드키로 엘리베이터 문 열기", flow, root.transform);
            TutorialFieldInteractable arrivalDoor = Door("stage02/door/Door (3)", TutorialFieldAction.ArrivalDoor, "도착 문 열기", flow, root.transform);
            TutorialFieldInteractable hallDoor = Door("stage03/Door (8)", TutorialFieldAction.HallDoor, "중앙 홀 출입문 열기", flow, root.transform);
            TutorialFieldInteractable exitDoor = Door("stage03/Door (9)", TutorialFieldAction.ExitDoor, "상층 출구 열기", flow, root.transform);
            TutorialFieldInteractable elevatorPanel = panel.gameObject.AddComponent<TutorialFieldInteractable>();
            Configure(elevatorPanel, flow, TutorialFieldAction.ElevatorPanel, panel, panel, "하층으로 이동");
            TutorialFieldInteractable[] targets = { firstDoor, elevatorDoor, arrivalDoor, hallDoor, exitDoor, elevatorPanel };
            SetArray(settings, "_doors", targets);
            Transform[] goals =
            {
                start, GameObject.Find("emey AI").transform,
                firstDoor.transform, elevatorPanel.transform,
                Point(root.transform, "Observe Guard Goal", new Vector3(-36.7f, 8.7f, 7.5f)),
                Point(root.transform, "Duct Ramp Goal", new Vector3(-43f, 8.7f, 18.91f)),
                Point(root.transform, "Duct Observation Goal", new Vector3(-52.9f, 13.2f, 18.91f)),
                Point(root.transform, "Duct Collapse Goal", new Vector3(-55.7f, 13.2f, 18.91f)),
                Point(root.transform, "Hall Stairs Goal", new Vector3(-52.67f, 8.7f, -11f)),
                exitDoor.transform, Point(root.transform, "Finish Goal", new Vector3(-52.67f, 12.7f, -21f))
            };
            SetArray(settings, "_goalPoints", goals);
            settings.ApplyModifiedPropertiesWithoutUndo();

            // 열린 옆길은 실제로 통과 가능하게 만든다. 카드키/진행 문은 위 컴포넌트가 제어한다.
            foreach (Transform door in GameObject.Find("stage02/door").GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.StartsWith("Door") && t.GetComponent<TutorialFieldInteractable>() == null).ToArray())
            {
                door.position += Vector3.up * 4.2f;
            }
            Trigger(root.transform, flow, "Observe Guard", new Vector3(-36.7f, 9.7f, 7.5f), new Vector3(3, 2.4f, 5), TutorialLevelStage.DuctRamp);
            Trigger(root.transform, flow, "Duct Observation", new Vector3(-52.9f, 14.1f, 18.91f), new Vector3(1.3f, 2, 2.3f), TutorialLevelStage.DuctObservation);
            Trigger(root.transform, flow, "Duct Collapse", new Vector3(-55.7f, 14.1f, 18.91f), new Vector3(2, 2, 2.3f), TutorialLevelStage.DuctEscape, true);
            Trigger(root.transform, flow, "Enter Hall", new Vector3(-52.67f, 9.7f, 11.1f), new Vector3(3.5f, 2.2f, 2.5f), TutorialLevelStage.Hall);
            Trigger(root.transform, flow, "Reach Upper Floor", new Vector3(-52.67f, 13.6f, -18.1f), new Vector3(5, 2, 2), TutorialLevelStage.UpperExit);
            Trigger(root.transform, flow, "Tutorial Exit", new Vector3(-52.67f, 13.8f, -21f), new Vector3(3.5f, 2.2f, 2), TutorialLevelStage.Completed);
            GameObject landing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            landing.name = "Upper Exit Landing";
            landing.transform.SetParent(root.transform);
            landing.transform.position = new Vector3(-52.67f, 12.53f, -21.2f);
            landing.transform.localScale = new Vector3(4, .2f, 3.8f);
            landing.GetComponent<Renderer>().sharedMaterial = GameObject.Find("stage03/Cube (60)").GetComponent<Renderer>().sharedMaterial;

            var interactionSettings = new SerializedObject(interaction);
            Set(interactionSettings, "_flow", flow);
            Set(interactionSettings, "_battle", battle);
            Set(interactionSettings, "_movement", movement);
            Set(interactionSettings, "_camera", Camera.main);
            Set(interactionSettings, "_hint", hint);
            SetArray(interactionSettings, "_targets", targets);
            interactionSettings.ApplyModifiedPropertiesWithoutUndo();
            foreach (MonoBehaviour owner in new MonoBehaviour[] { battle, hud })
            {
                var ownerSettings = new SerializedObject(owner);
                Set(ownerSettings, "_fieldInteraction", interaction);
                ownerSettings.ApplyModifiedPropertiesWithoutUndo();
            }
            var hudSettings = new SerializedObject(hud);
            Set(hudSettings, "_levelFlow", flow);
            hudSettings.ApplyModifiedPropertiesWithoutUndo();
            ConfigureConnectedRoute();
            ConfigureGlass();
            ConfigurePatrols(root.transform);
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        public static void ConfigureConnectedRoute()
        {
            var floorSettings = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<FloorTravelButtons>());
            floorSettings.FindProperty("_secondFloorPosition").vector3Value = new Vector3(-32.5f, 8.7f, -.08f);
            floorSettings.ApplyModifiedPropertiesWithoutUndo();
            // 복도 끝 임시 칸막이를 열고 두 바닥 사이의 틈을 연결한다.
            Transform divider = GameObject.Find("stage02/door/Cube (29)").transform;
            Vector3 dividerPosition = divider.position;
            dividerPosition.y = 14.58f;
            divider.position = dividerPosition;
            Transform hallDivider = GameObject.Find("stage02/wall/Cube (18)").transform;
            Vector3 hallDividerPosition = hallDivider.position;
            hallDividerPosition.y = 14.83f;
            hallDivider.position = hallDividerPosition;
            GameObject bridge = GameObject.Find("Side Corridor Landing");
            if (bridge == null)
            {
                bridge = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bridge.name = "Side Corridor Landing";
                bridge.transform.SetParent(GameObject.Find("Tutorial Level Content").transform);
            }
            bridge.transform.position = new Vector3(-40.71f, 8.53f, 18.91f);
            bridge.transform.localScale = new Vector3(8f, .2f, 2.2f);
            bridge.GetComponent<Renderer>().sharedMaterial = GameObject.Find("stage02/floor/Cube (17)").GetComponent<Renderer>().sharedMaterial;
        }

        public static void ConfigureGlass()
        {
            const string materialPath = "Assets/CK_Semester_Project/Prototype/Data/TutorialObservationGlass.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetFloat("_Surface", 1);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0);
                material.SetColor("_BaseColor", new Color(.25f, .7f, .9f, .16f));
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = 3000;
                AssetDatabase.CreateAsset(material, materialPath);
            }
            // 옆 복도와 경비 구역 사이의 기존 창 패널. 충돌은 남기고 보이게 만든다.
            foreach (Renderer renderer in GameObject.Find("stage02").GetComponentsInChildren<Renderer>())
            {
                Bounds bounds = renderer.bounds;
                if (Mathf.Abs(bounds.center.x + 38.81f) < .2f && bounds.size.x < .25f && bounds.size.y >= 3)
                {
                    renderer.sharedMaterial = material;
                    foreach (Collider collider in renderer.GetComponents<Collider>())
                    {
                        collider.gameObject.layer = 2; // Ignore Raycast: 물리 충돌을 유지하며 투명한 창 너머로 시야 검사.
                    }
                }
            }
            foreach (EnemyStateMachine enemy in UnityEngine.Object.FindObjectsByType<EnemyStateMachine>(FindObjectsSortMode.None))
            {
                enemy.sightBlockingLayers &= ~(1 << 2);
            }
        }

        private static void ConfigurePatrols(Transform root)
        {
            foreach (EnemyStateMachine enemy in UnityEngine.Object.FindObjectsByType<EnemyStateMachine>(FindObjectsSortMode.None))
            {
                if (enemy.name == "emey AI")
                {
                    enemy.patrolHalfExtents = new Vector2(.4f, .4f);
                    enemy.minimumPatrolDistance = .1f;
                    continue;
                }
                Vector3 position = enemy.transform.position;
                if (enemy.name == "emey (8) AI")
                {
                    position = new Vector3(-56.5f, 8.7f, 2);
                }
                if (enemy.name == "emey (9) AI")
                {
                    position = new Vector3(-48.7f, 8.7f, -6);
                }
                NavMeshHit hit;
                if (NavMesh.SamplePosition(position, out hit, 2, NavMesh.AllAreas))
                {
                    position = hit.position;
                }
                enemy.transform.position = position;
                enemy.hearingDistance = 12;
                enemy.viewDistance = 6;
                enemy.patrolWaitSeconds = new Vector2(1.5f, 2.5f);
                enemy.patrolPoints = new[]
                {
                    Point(root, enemy.name + " Route A", position + Vector3.forward * 2),
                    Point(root, enemy.name + " Route B", position - Vector3.forward * 2)
                };
                EditorUtility.SetDirty(enemy);
            }
        }

        private static TutorialFieldInteractable Door(string path, TutorialFieldAction kind, string prompt,
            TutorialLevelFlow flow, Transform root)
        {
            GameObject item = GameObject.Find(path);
            if (item == null)
            {
                throw new InvalidOperationException("콘텐츠 문 연결 실패: " + path);
            }
            var door = item.AddComponent<TutorialFieldInteractable>();
            Vector3 center = item.GetComponent<Renderer>().bounds.center;
            Transform point = Point(root, kind + " Interaction Point", new Vector3(center.x, center.y - 1, center.z));
            Configure(door, flow, kind, item.transform, point, prompt);
            return door;
        }

        private static void Configure(TutorialFieldInteractable item, TutorialLevelFlow flow, TutorialFieldAction kind,
            Transform moving, Transform point, string prompt)
        {
            var settings = new SerializedObject(item);
            Set(settings, "_flow", flow);
            settings.FindProperty("_kind").enumValueIndex = (int)kind;
            Set(settings, "_movingPart", moving);
            Set(settings, "_interactionPoint", point);
            settings.FindProperty("_prompt").stringValue = prompt;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Trigger(Transform root, TutorialLevelFlow flow, string name, Vector3 position, Vector3 size,
            TutorialLevelStage destination, bool collapse = false)
        {
            Transform item = Point(root, name, position);
            BoxCollider collider = item.gameObject.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = size;
            var body = item.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var settings = new SerializedObject(item.gameObject.AddComponent<TutorialLevelTrigger>());
            Set(settings, "_flow", flow);
            settings.FindProperty("_destination").enumValueIndex = (int)destination;
            settings.FindProperty("_collapse").boolValue = collapse;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform Point(Transform parent, string name, Vector3 position)
        {
            var point = new GameObject(name);
            point.transform.SetParent(parent);
            point.transform.position = position;
            return point.transform;
        }

        private static TMP_Text UiPanel(Transform parent, string name, Vector2 position, Vector2 size, TMP_FontAsset font)
        {
            var panel = new GameObject(name + " Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(parent, false);
            Image image = panel.GetComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiFolder + "Turn_SkillButton_Default.png");
            image.raycastTarget = false;
            RectTransform rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            TMP_Text label = Label(panel.transform, name, font);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(20, 4);
            label.rectTransform.offsetMax = new Vector2(-20, -4);
            return label;
        }

        private static TMP_Text Label(Transform parent, string name, TMP_FontAsset font)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            item.transform.SetParent(parent, false);
            var label = item.GetComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = 20;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(.75f, .95f, 1);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        private static TMP_Text WorldLabel(Transform parent, string name, TMP_FontAsset font)
        {
            var item = new GameObject(name, typeof(TextMeshPro));
            item.transform.SetParent(parent, false);
            item.transform.localScale = Vector3.one * .1f;
            TextMeshPro label = item.GetComponent<TextMeshPro>();
            label.font = font;
            label.fontSize = 6;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(14, 7);
            return label;
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

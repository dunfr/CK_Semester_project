using System;
using CK.SemesterProject.Battle.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CK.SemesterProject.Battle.Editor
{
    public static class BattleDemoTools
    {
        private const string ScenePath = "Assets/CK_Semester_Project/Prototype/Scenes/BattleCoreDemo.unity";
        private const string MaterialPath = "Assets/CK_Semester_Project/Prototype/Materials/Battle";
        private static readonly Color Ink = new Color(0.025f, 0.04f, 0.065f, 0.88f);
        private static readonly Color Cyan = new Color(0.4f, 0.85f, 0.93f);
        private static Font _font;
        private static double _nextMenuCheck;

        [InitializeOnLoadMethod]
        private static void ScheduleMenuCleanup()
        {
            EditorApplication.update += HideJobsMenu;
        }

        private static void HideJobsMenu()
        {
            // Burst가 지연 등록하거나 메뉴를 다시 만드는 경우도 처리한다.
            if (EditorApplication.timeSinceStartup < _nextMenuCheck)
            {
                return;
            }
            _nextMenuCheck = EditorApplication.timeSinceStartup + 2;
            // 사용자 요청으로 메뉴 표시만 숨긴다. Burst 패키지와 컴파일 설정은 유지한다.
            Type menu = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.Menu");
            System.Reflection.MethodInfo remove = menu?.GetMethod("RemoveMenuItem",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (remove == null)
            {
                EditorApplication.update -= HideJobsMenu;
                Debug.LogWarning("현재 Unity 버전에서는 Jobs 메뉴 숨기기를 지원하지 않습니다.");
                return;
            }
            System.Reflection.MethodInfo exists = menu.GetMethod("MenuItemExists",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (exists != null && (bool)exists.Invoke(null, new object[] { "Jobs/Burst/Enable Compilation" }))
            {
                remove.Invoke(null, new object[] { "Jobs" });
            }
        }

        [MenuItem("Tools/Battle/Open Scene")]
        public static void OpenScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            EditorSceneManager.OpenScene(ScenePath);
        }

        // MCP에서 호출하거나 메뉴로 재생성한다. 게임을 실행하지 않는다.
        [MenuItem("Tools/Battle/Rebuild Screen")]
        public static void RebuildScreen()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != ScenePath)
            {
                Debug.LogError("전투 씬을 편집 모드로 열어 주세요.");
                return;
            }
            BattleDemoController controller = UnityEngine.Object.FindFirstObjectByType<BattleDemoController>();
            if (controller == null)
            {
                Debug.LogError("전투 컨트롤러가 없습니다.");
                return;
            }
            Undo.RegisterFullObjectHierarchyUndo(controller.gameObject, "Rebuild battle screen");
            foreach (string name in new[] { "Battle Stage", "Battle HUD" })
            {
                Transform previous = controller.transform.Find(name);
                if (previous != null)
                {
                    Undo.DestroyObjectImmediate(previous.gameObject);
                }
            }
            controller.name = "Battle";
            EnsureFolder(MaterialPath);
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Camera camera = Camera.main;
            Undo.RecordObject(camera, "Battle camera");
            Undo.RecordObject(camera.transform, "Battle camera composition");
            camera.orthographic = false;
            camera.fieldOfView = 48;
            camera.rect = new Rect(0, 0, 1, 1);
            camera.transform.position = new Vector3(-2, 3.4f, -8.7f);
            camera.transform.LookAt(new Vector3(0.4f, 1.25f, 2.2f));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.022f, 0.035f, 0.06f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.36f, 0.43f, 0.56f);
            Light light = UnityEngine.Object.FindFirstObjectByType<Light>();
            Undo.RecordObject(light, "Battle lighting");
            light.intensity = 1.65f;
            light.color = new Color(0.8f, 0.88f, 1);
            light.shadows = LightShadows.Soft;
            Transform[] figures = CreateStage(controller.transform);
            RectTransform hud = CreateHud(controller.transform);
            Canvas canvas = hud.GetComponent<Canvas>();
            canvas.worldCamera = camera;
            canvas.planeDistance = 0.5f;
            for (int i = 1; i < figures.Length; i++)
            {
                Vector3 viewport = camera.WorldToViewportPoint(figures[i].position + Vector3.up * 2.8f);
                var target = (RectTransform)hud.Find("Target" + i);
                target.anchorMin = target.anchorMax = new Vector2(viewport.x, viewport.y);
                target.anchoredPosition = Vector2.zero;
            }
            var view = new SerializedObject(controller.GetComponent<BattleDemoView>());
            view.FindProperty("_hud").objectReferenceValue = hud;
            view.FindProperty("_camera").objectReferenceValue = camera;
            AssignFigures(view, figures);
            var stage = new SerializedObject(controller.GetComponent<BattleDemoStage>());
            AssignFigures(stage, figures);
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("Battle Input", typeof(EventSystem), typeof(InputSystemUIInputModule));
                Undo.RegisterCreatedObjectUndo(events, "Battle input");
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            Canvas.ForceUpdateCanvases();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = hud.gameObject;
        }

        private static void AssignFigures(SerializedObject serialized, Transform[] figures)
        {
            SerializedProperty array = serialized.FindProperty("_figures");
            array.arraySize = figures.Length;
            for (int i = 0; i < figures.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = figures[i];
            }
            serialized.ApplyModifiedProperties();
        }

        private static Transform[] CreateStage(Transform parent)
        {
            var root = new GameObject("Battle Stage");
            root.transform.SetParent(parent, false);
            Material ground = Material("Ground", new Color(0.12f, 0.16f, 0.2f));
            Material stone = Material("Stone", new Color(0.08f, 0.11f, 0.16f));
            Material metal = Material("Armor", new Color(0.16f, 0.24f, 0.31f));
            Material cyan = Material("Memory", new Color(0.2f, 0.73f, 0.86f), true);
            Material gold = Material("Enemy", new Color(0.92f, 0.54f, 0.24f));
            Material white = Material("Light", new Color(0.78f, 0.89f, 1), true);
            Shape("Floor", PrimitiveType.Cube, root.transform, new Vector3(0, -0.2f, 5), new Vector3(40, 0.3f, 40), ground);
            for (int i = -4; i <= 4; i++)
            {
                Shape("Floor seam", PrimitiveType.Cube, root.transform, new Vector3(i * 3, -0.04f, 5), new Vector3(0.025f, 0.01f, 35), metal);
                Shape("Floor seam", PrimitiveType.Cube, root.transform, new Vector3(0, -0.04f, i * 3 + 5), new Vector3(35, 0.01f, 0.025f), metal);
            }
            for (int i = -3; i <= 3; i++)
            {
                float height = 5 + Mathf.Abs(i) % 3;
                Shape("Pillar", PrimitiveType.Cube, root.transform, new Vector3(i * 4, height / 2, 14), new Vector3(1.4f, height, 1.6f), stone);
                Shape("Pillar light", PrimitiveType.Cube, root.transform, new Vector3(i * 4, 2.5f, 13.17f), new Vector3(0.07f, 2.2f, 0.04f), cyan);
            }
            Shape("Back wall", PrimitiveType.Cube, root.transform, new Vector3(0, 1.2f, 15), new Vector3(30, 2.5f, 1), stone);
            var figures = new Transform[3];
            Vector3[] positions = { new Vector3(-2.5f, 0, -0.7f), new Vector3(0.8f, 0, 4), new Vector3(4.1f, 0, 5.5f) };
            for (int i = 0; i < 3; i++)
            {
                var figure = new GameObject(i == 0 ? "Player" : "Sentinel " + i);
                figure.transform.SetParent(root.transform, false);
                figure.transform.localPosition = positions[i];
                figures[i] = figure.transform;
                if (i == 0)
                {
                    Shape("Torso", PrimitiveType.Capsule, figure.transform, new Vector3(0, 1.25f, 0), new Vector3(0.6f, 0.58f, 0.42f), metal);
                    Shape("Head", PrimitiveType.Sphere, figure.transform, new Vector3(0, 2.05f, 0), Vector3.one * 0.44f, white);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Shape("Leg", PrimitiveType.Capsule, figure.transform, new Vector3(side * 0.18f, 0.45f, 0), new Vector3(0.23f, 0.45f, 0.27f), stone);
                        Transform arm = Shape("Arm", PrimitiveType.Capsule, figure.transform, new Vector3(side * 0.4f, 1.25f, 0), new Vector3(0.2f, 0.42f, 0.22f), metal);
                        arm.localRotation = Quaternion.Euler(0, 0, side * 15);
                    }
                    Shape("Coat", PrimitiveType.Cube, figure.transform, new Vector3(0, 0.85f, -0.22f), new Vector3(0.68f, 0.85f, 0.12f), stone);
                    Shape("Coat trim", PrimitiveType.Cube, figure.transform, new Vector3(0, 1.23f, -0.29f), new Vector3(0.07f, 0.95f, 0.03f), cyan);
                    Transform blade = Shape("Blade", PrimitiveType.Cube, figure.transform, new Vector3(0.67f, 1.2f, 0.35f), new Vector3(0.06f, 1.6f, 0.13f), white);
                    blade.localRotation = Quaternion.Euler(25, 0, -15);
                    figure.transform.localRotation = Quaternion.Euler(0, 25, 0);
                }
                else
                {
                    Transform shell = Shape("Shell", PrimitiveType.Cube, figure.transform, new Vector3(0, 1.6f, 0), new Vector3(0.85f, 1.1f, 0.85f), gold);
                    shell.localRotation = Quaternion.Euler(0, 25, 45);
                    Shape("Core", PrimitiveType.Sphere, figure.transform, new Vector3(0, 1.65f, -0.58f), Vector3.one * 0.28f, white);
                    Shape("Base", PrimitiveType.Cylinder, figure.transform, new Vector3(0, 0.48f, 0), new Vector3(0.85f, 0.2f, 0.85f), metal);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Transform wing = Shape("Wing", PrimitiveType.Cube, figure.transform, new Vector3(side * 0.8f, 1.35f, 0), new Vector3(0.18f, 1.2f, 0.4f), metal);
                        wing.localRotation = Quaternion.Euler(0, 0, side * -35);
                    }
                }
            }
            return figures;
        }

        private static RectTransform CreateHud(Transform parent)
        {
            var go = new GameObject("Battle HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceCamera;
            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;
            RectTransform root = (RectTransform)go.transform;
            RectTransform order = Rect("Turn Order", root, new Vector2(0, 1), new Vector2(115, -65), new Vector2(170, 300));
            Label("Round", order, "01  /  ROUND", 15, new Vector2(0, 0), new Vector2(160, 24), Cyan);
            for (int i = 0; i < 3; i++)
            {
                RectTransform card = Panel("OrderCard" + i, order, new Vector2(0, -64 - i * 78), new Vector2(165, 65), i == 0 ? new Color(0.08f, 0.38f, 0.48f, 0.94f) : Ink);
                Label("Order" + i, card, i == 0 ? "플레이어" : "센티널 " + (i == 1 ? "A" : "B"), 19, new Vector2(5, 10), new Vector2(145, 26), Color.white);
                Label("OrderMemory" + i, card, "MEM " + (i == 0 ? 24 : i == 1 ? 18 : 14), 12, new Vector2(5, -17), new Vector2(145, 20), Cyan);
            }
            for (int i = 1; i < 3; i++)
            {
                RectTransform target = Button("Target" + i, root, "", new Vector2(i == 1 ? 820 : 1130, 570), new Vector2(185, 55));
                target.anchorMin = target.anchorMax = Vector2.zero;
                Label("EnemyName" + i, target, (i == 1 ? "◇  " : "") + "센티널 " + (i == 1 ? "A" : "B"), 18, new Vector2(0, 10), new Vector2(180, 28), Color.white);
                Bar("EnemyHealth" + i, target, new Vector2(0, -15), new Vector2(160, 4), new Color(0.94f, 0.43f, 0.32f), 1);
                Label("Damage" + i, target, "", 40, new Vector2(0, -90), new Vector2(190, 60), Color.white);
            }
            RectTransform player = Rect("Player Status", root, new Vector2(0, 0), new Vector2(210, 103), new Vector2(320, 150));
            Panel("Status Background", player, Vector2.zero, new Vector2(320, 155), Ink);
            Label("PlayerName", player, "플레이어", 26, new Vector2(-5, 47), new Vector2(270, 38), Color.white);
            Label("Health", player, "140 / 140", 18, new Vector2(0, 16), new Vector2(280, 25), Color.white);
            Bar("HealthFill", player, new Vector2(0, -4), new Vector2(278, 6), Cyan, 1);
            Label("Memory", player, "메모리  24", 15, new Vector2(-70, -25), new Vector2(140, 24), Cyan);
            Label("Rage", player, "폭주  50", 15, new Vector2(76, -25), new Vector2(130, 24), new Color(1, 0.68f, 0.39f));
            Bar("MemoryFill", player, new Vector2(-73, -46), new Vector2(130, 3), Cyan, 0.6f);
            Bar("RageFill", player, new Vector2(74, -46), new Vector2(130, 3), new Color(1, 0.68f, 0.39f), 0.5f);
            Label("Status", player, "", 16, new Vector2(0, 103), new Vector2(280, 25), Color.white);
            Label("PlayerDamage", player, "", 38, new Vector2(0, 190), new Vector2(240, 60), Color.white);
            RectTransform commands = Rect("Commands", root, new Vector2(1, 0), new Vector2(-248, 100), new Vector2(430, 210));
            string[] labels = { "잔상 · 기본 공격", "각인 · 강타", "망각 · 메모리 강탈" };
            for (int i = 0; i < 3; i++)
            {
                Button("Skill" + i, commands, labels[i], new Vector2(-86, 109 - i * 49), new Vector2(226, 42));
            }
            RectTransform investment = Button("Investment", commands, "", new Vector2(-86, -43), new Vector2(226, 39));
            Label("InvestmentLabel", investment, "메모리 투자  0  +", 16, Vector2.zero, new Vector2(225, 35), Cyan);
            RectTransform attack = Button("Attack", commands, "공격", new Vector2(116, -11), new Vector2(124, 124));
            attack.GetComponent<Image>().sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            attack.GetComponent<Image>().color = new Color(0.17f, 0.43f, 0.5f, 0.97f);
            RectTransform defend = Button("Defend", commands, "방어", new Vector2(147, 111), new Vector2(78, 78));
            defend.GetComponent<Image>().sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            RectTransform notice = Rect("Battle Notice", root, new Vector2(0.5f, 0), new Vector2(0, 42), new Vector2(430, 30));
            Label("Notice", notice, "대상을 선택하세요", 16, Vector2.zero, new Vector2(430, 30), Color.white);
            RectTransform result = Panel("Result", root, Vector2.zero, new Vector2(460, 230), Ink);
            Label("Outcome", result, "승리", 48, new Vector2(0, 35), new Vector2(400, 70), Color.white);
            Button("Restart", result, "다시하기", new Vector2(0, -65), new Vector2(200, 45));
            result.gameObject.SetActive(false);
            go.AddComponent<BattleHudFont>();
            return root;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static RectTransform Panel(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            RectTransform rect = Rect(name, parent, new Vector2(0.5f, 0.5f), position, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static void Label(string name, Transform parent, string value, int size, Vector2 position, Vector2 dimensions, Color color)
        {
            RectTransform rect = Rect(name, parent, new Vector2(0.5f, 0.5f), position, dimensions);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = _font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
        }

        private static RectTransform Button(string name, Transform parent, string value, Vector2 position, Vector2 size)
        {
            RectTransform rect = Panel(name, parent, position, size, Ink);
            Image image = rect.GetComponent<Image>();
            image.raycastTarget = true;
            UnityEngine.UI.Button button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.7f, 0.9f, 1);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.65f);
            button.colors = colors;
            if (!string.IsNullOrEmpty(value))
            {
                Label(name + "Label", rect, value, 18, Vector2.zero, size - Vector2.one * 8, Color.white);
            }
            return rect;
        }

        private static void Bar(string name, Transform parent, Vector2 position, Vector2 size, Color color, float fill)
        {
            RectTransform track = Panel(name + "Track", parent, position, size, new Color(0.25f, 0.3f, 0.36f));
            RectTransform bar = Panel(name, track, Vector2.zero, size, color);
            Image image = bar.GetComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillAmount = fill;
        }

        private static Material Material(string name, Color color, bool emission = false)
        {
            string path = MaterialPath + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.25f);
            if (emission)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.3f);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Transform Shape(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go.transform;
        }

        private static void EnsureFolder(string path)
        {
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
            }
        }
    }
}

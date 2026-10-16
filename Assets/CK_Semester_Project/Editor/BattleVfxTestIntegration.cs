using System.Linq;
using CK.SemesterProject.Battle;
using CK.SemesterProject.Tutorial;
using Semester.Enemies;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace CK.SemesterProject.EditorTools
{
    public static class BattleVfxTestIntegration
    {
        public const string ScenePath = "Assets/CK_Semester_Project/Prototype/Scenes/BattleVfxTest.unity";
        private const string DataFolder = "Assets/CK_Semester_Project/Prototype/Data/BattleVfxTest";
        private const string PrefabFolder = "Assets/CK_Semester_Project/Prototype/Prefabs/BattleVfxTest";

        public static void CreateScene()
        {
            if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            if (!AssetDatabase.IsValidFolder(DataFolder))
            {
                AssetDatabase.CreateFolder("Assets/CK_Semester_Project/Prototype/Data", "BattleVfxTest");
            }
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
            {
                AssetDatabase.CreateFolder("Assets/CK_Semester_Project/Prototype/Prefabs", "BattleVfxTest");
            }
            var scene = EditorSceneManager.OpenScene("Assets/CK_Semester_Project/Prototype/Scenes/TutorialGrayboxingDemo.unity");
            EditorSceneManager.SaveScene(scene, ScenePath);
            string[] keep = { "Directional Light", "Global Volume", "Main Camera", "Fuchsia Player", "Tutorial Systems", "emey AI" };
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (!keep.Contains(root.name))
                {
                    Object.DestroyImmediate(root);
                }
            }
            var battle = Object.FindFirstObjectByType<TutorialBattleController>();
            var player = Object.FindFirstObjectByType<PlayerMovement>();
            var dummy = Object.FindFirstObjectByType<EnemyStateMachine>();
            player.transform.SetPositionAndRotation(new Vector3(0, .05f, -4.5f), Quaternion.identity);
            dummy.transform.SetPositionAndRotation(new Vector3(0, .05f, 4.5f), Quaternion.Euler(0, 180, 0));
            dummy.name = "VFX Training Dummy";
            Object.DestroyImmediate(player.GetComponent<FloorTravelButtons>());
            var battleSettings = new SerializedObject(battle);
            battleSettings.FindProperty("_automaticEncounters").boolValue = false;
            var enemies = battleSettings.FindProperty("_enemies");
            enemies.arraySize = 1;
            enemies.GetArrayElementAtIndex(0).objectReferenceValue = dummy;
            var enhancement = battleSettings.FindProperty("_basicSkillEnhancement");
            string enhancementPath = DataFolder + "/BattleVfxTestEnhancement.asset";
            CopyAsset(AssetDatabase.GetAssetPath(enhancement.objectReferenceValue), enhancementPath);
            enhancement.objectReferenceValue = AssetDatabase.LoadAssetAtPath<BasicSkillEnhancementAsset>(enhancementPath);
            battleSettings.ApplyModifiedPropertiesWithoutUndo();
            CopyEffects((BasicSkillEnhancementAsset)enhancement.objectReferenceValue);

            string dummyPath = DataFolder + "/VfxTrainingDummy.asset";
            MonsterBattleDefinition definition = AssetDatabase.LoadAssetAtPath<MonsterBattleDefinition>(dummyPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<MonsterBattleDefinition>();
                definition.InitializeDefaults(BattleElement.Imprint);
                AssetDatabase.CreateAsset(definition, dummyPath);
            }
            var dummySettings = new SerializedObject(definition);
            dummySettings.FindProperty("_id").stringValue = "vfx_training_dummy";
            dummySettings.FindProperty("_displayName").stringValue = "이펙트 테스트 더미";
            dummySettings.FindProperty("_maxHp").intValue = 1000000;
            dummySettings.FindProperty("_power").intValue = 0;
            dummySettings.ApplyModifiedPropertiesWithoutUndo();
            var monster = new SerializedObject(dummy.GetComponent<TutorialMonster>());
            monster.FindProperty("_definition").objectReferenceValue = definition;
            monster.ApplyModifiedPropertiesWithoutUndo();

            GameObject arena = new GameObject("VFX Test Arena");
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Neutral Floor";
            floor.transform.SetParent(arena.transform);
            floor.transform.position = new Vector3(0, -.15f, 0);
            floor.transform.localScale = new Vector3(50, .3f, 50);
            string materialPath = DataFolder + "/VfxTestFloor.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.color = new Color(.16f, .19f, .23f);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            floor.GetComponent<Renderer>().sharedMaterial = material;
            NavMeshSurface surface = arena.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.agentTypeID = dummy.GetComponent<NavMeshAgent>().agentTypeID;
            surface.BuildNavMesh();
            string navPath = DataFolder + "/VfxTestNavigation.asset";
            NavMeshData previous = AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
            if (previous == null)
            {
                AssetDatabase.CreateAsset(surface.navMeshData, navPath);
            }
            else
            {
                EditorUtility.CopySerialized(surface.navMeshData, previous);
                surface.RemoveData();
                surface.navMeshData = previous;
                surface.AddData();
            }
            CreateControls(battle, player, dummy);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = battle.gameObject;
        }

        private static void CopyAsset(string source, string destination)
        {
            if (AssetDatabase.LoadMainAssetAtPath(destination) == null)
            {
                AssetDatabase.CopyAsset(source, destination);
            }
        }

        private static void CopyEffects(BasicSkillEnhancementAsset enhancement)
        {
            var settings = new SerializedObject(enhancement);
            SerializedProperty property = settings.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference || !(property.objectReferenceValue is GameObject))
                {
                    continue;
                }
                string source = AssetDatabase.GetAssetPath(property.objectReferenceValue);
                string destination = PrefabFolder + "/" + System.IO.Path.GetFileName(source);
                CopyAsset(source, destination);
                property.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(destination);
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateControls(TutorialBattleController battle, PlayerMovement player, EnemyStateMachine dummy)
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/CK_Semester_Project/Prototype/Temp/Fonts/Paperlogy-1Thin SDF.asset");
            var root = new GameObject("VFX Test Controls", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(BattleVfxTestController));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            root.GetComponent<Canvas>().sortingOrder = 100;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            TMP_Text status = Label(root.transform, "Test Instructions", font, new Vector2(0, -112), new Vector2(950, 60));
            status.color = new Color(.04f, .09f, .15f);
            Button restart = Button(root.transform, "Restart", "다시 시작 [F5]", font, -135);
            Button stage = Button(root.transform, "Skill Stage", "강화 단계 변경 [F6]", font, 135);
            var settings = new SerializedObject(root.GetComponent<BattleVfxTestController>());
            settings.FindProperty("_battle").objectReferenceValue = battle;
            settings.FindProperty("_player").objectReferenceValue = player;
            settings.FindProperty("_dummy").objectReferenceValue = dummy;
            settings.FindProperty("_restart").objectReferenceValue = restart;
            settings.FindProperty("_stage").objectReferenceValue = stage;
            settings.FindProperty("_status").objectReferenceValue = status;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Button Button(Transform parent, string name, string caption, TMP_FontAsset font, float x)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)item.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(x, -166);
            rect.sizeDelta = new Vector2(250, 40);
            item.GetComponent<Image>().color = new Color(.035f, .12f, .2f, .95f);
            TMP_Text label = Label(item.transform, "Label", font, Vector2.zero, rect.sizeDelta);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, .5f);
            label.text = caption;
            return item.GetComponent<Button>();
        }

        private static TMP_Text Label(Transform parent, string name, TMP_FontAsset font, Vector2 position, Vector2 size)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var text = item.GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent, false);
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(.5f, 1);
            text.rectTransform.anchoredPosition = position;
            text.rectTransform.sizeDelta = size;
            text.font = font;
            text.fontSize = 20;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }
    }
}

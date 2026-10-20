using System.Linq;
using CK.SemesterProject.Tutorial;
using Semester.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CK.SemesterProject.EditorTools
{
    public static class BattleVfxTestIntegration
    {
        public const string ScenePath = "Assets/CK_Semester_Project/Prototype/Scenes/BattleVfxTest.unity";
        private const string DataFolder = "Assets/CK_Semester_Project/Prototype/Data/BattleVfxTest";

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
            var player = Object.FindFirstObjectByType<PlayerMovement>();
            var dummy = Object.FindFirstObjectByType<EnemyStateMachine>();
            player.transform.SetPositionAndRotation(new Vector3(0, .05f, -4.5f), Quaternion.identity);
            dummy.transform.SetPositionAndRotation(new Vector3(0, .05f, 4.5f), Quaternion.Euler(0, 180, 0));
            dummy.name = "VFX Training Dummy";
            Object.DestroyImmediate(player.GetComponent<FloorTravelButtons>());

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
            CreateControls(player, dummy);
            var savedEffects = new GameObject("저장된 이펙트 관리", typeof(BattleVfxSavedEffects));
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = savedEffects;
        }

        private static void CreateControls(PlayerMovement player, EnemyStateMachine dummy)
        {
            var root = new GameObject("VFX Test Controls", typeof(BattleVfxTestController));
            var settings = new SerializedObject(root.GetComponent<BattleVfxTestController>());
            settings.FindProperty("_player").objectReferenceValue = player;
            settings.FindProperty("_dummy").objectReferenceValue = dummy;
            settings.ApplyModifiedPropertiesWithoutUndo();
            ConfigureControls(root.GetComponent<BattleVfxTestController>());
        }

        public static void ConfigureControls(BattleVfxTestController controls)
        {
            RemoveBattleComponents(controls.gameObject.scene);
            foreach (UnityEngine.UI.Graphic graphic in controls.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
            {
                if (graphic != null)
                {
                    Object.DestroyImmediate(graphic.gameObject);
                }
            }
            RemoveComponents<UnityEngine.UI.GraphicRaycaster>(controls.gameObject);
            RemoveComponents<UnityEngine.UI.CanvasScaler>(controls.gameObject);
            RemoveComponents<Canvas>(controls.gameObject);
            foreach (Canvas canvas in controls.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Canvas>(true)))
            {
                canvas.enabled = false;
                UnityEngine.UI.GraphicRaycaster raycaster = canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
                if (raycaster != null)
                {
                    raycaster.enabled = false;
                }
            }
        }

        private static void RemoveBattleComponents(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                RemoveComponents<PrototypeHudPresenter>(root);
                RemoveComponents<TutorialFieldInteraction>(root);
                RemoveComponents<TutorialLevelFlow>(root);
                RemoveComponents<TutorialBattleController>(root);
                foreach (EnemyStateMachine dummy in root.GetComponentsInChildren<EnemyStateMachine>(true))
                {
                    dummy.enabled = false;
                    UnityEngine.AI.NavMeshAgent agent = dummy.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    if (agent != null)
                    {
                        agent.enabled = false;
                    }
                }
            }
        }

        private static void RemoveComponents<T>(GameObject root) where T : Component
        {
            foreach (T component in root.GetComponentsInChildren<T>(true))
            {
                Object.DestroyImmediate(component);
            }
        }
    }
}

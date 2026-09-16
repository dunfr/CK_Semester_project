using System;
using System.IO;
using CK.SemesterProject.Battle.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CK.SemesterProject.Battle.Editor
{
    [InitializeOnLoad]
    public static class BattleDemoTools
    {
        private const string ScenePath = "Assets/CK_Semester_Project/Prototype/Scenes/BattleCoreDemo.unity";
        private const string CaptureKey = "CK.BattleDemo.Capture";
        private static double _captureAt;

        static BattleDemoTools()
        {
            _captureAt = EditorApplication.timeSinceStartup + 5;
            EditorApplication.update += CaptureWhenReady;
        }

        [MenuItem("CK/Battle Demo/Create Scene")]
        public static void CreateScene()
        {
            if (File.Exists(ScenePath))
            {
                return;
            }
            Scene previous = SceneManager.GetActiveScene();
            bool hasSavedScene = !string.IsNullOrEmpty(previous.path);
            if (!hasSavedScene && !Application.isBatchMode
                && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                hasSavedScene ? NewSceneMode.Additive : NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var lightObject = new GameObject("Directional Light", typeof(Light));
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.8f;
            lightObject.transform.rotation = Quaternion.Euler(45, -30, 0);
            var root = new GameObject("Battle Core Demo");
            root.AddComponent<BattleDemoController>();
            root.AddComponent<BattleDemoView>();
            root.AddComponent<BattleDemoStage>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (hasSavedScene)
            {
                EditorSceneManager.CloseScene(scene, true);
                SceneManager.SetActiveScene(previous);
            }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("CK/Battle Demo/Open And Play")]
        public static void OpenAndPlay()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            CreateScene();
            EditorSceneManager.OpenScene(ScenePath);
            Type gameViewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            EditorWindow window = EditorWindow.GetWindow(gameViewType);
            window.Focus();
            window.maximized = true;
            SessionState.SetBool(CaptureKey, true);
            _captureAt = EditorApplication.timeSinceStartup + 5;
            EditorApplication.EnterPlaymode();
        }

        private static void CaptureWhenReady()
        {
            if (!SessionState.GetBool(CaptureKey, false) || !EditorApplication.isPlaying
                || EditorApplication.timeSinceStartup < _captureAt)
            {
                return;
            }
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs"));
            Directory.CreateDirectory(directory);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, "battle-demo.png"));
            SessionState.SetBool(CaptureKey, false);
        }
    }
}

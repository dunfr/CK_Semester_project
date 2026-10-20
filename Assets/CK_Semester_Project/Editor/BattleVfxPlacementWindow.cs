using System.Linq;
using CK.SemesterProject.Tutorial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace CK.SemesterProject.EditorTools
{
    [CustomEditor(typeof(BattleVfxTestController))]
    public sealed class BattleVfxTestControllerEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new HelpBox("씬에 이펙트를 놓고 Inspector에서 편집한 뒤 저장하세요. 저장본은 관리 오브젝트에서 재생합니다.", HelpBoxMessageType.Info));
            root.Add(new Button(BattleVfxPlacementWindow.Open) { text = "저장된 이펙트 관리 선택" });
            return root;
        }
    }

    public static class BattleVfxPlacementWindow
    {
        public static void Open()
        {
            Scene scene = SceneManager.GetActiveScene();
            BattleVfxSavedEffects manager = scene.GetRootGameObjects()
                .SelectMany(item => item.GetComponentsInChildren<BattleVfxSavedEffects>(true)).FirstOrDefault();
            if (manager == null)
            {
                var root = new GameObject("저장된 이펙트 관리");
                SceneManager.MoveGameObjectToScene(root, scene);
                Undo.RegisterCreatedObjectUndo(root, "이펙트 관리 오브젝트 추가");
                manager = Undo.AddComponent<BattleVfxSavedEffects>(root);
                EditorSceneManager.MarkSceneDirty(scene);
            }
            Selection.activeGameObject = manager.gameObject;
            EditorGUIUtility.PingObject(manager.gameObject);
        }
    }
}

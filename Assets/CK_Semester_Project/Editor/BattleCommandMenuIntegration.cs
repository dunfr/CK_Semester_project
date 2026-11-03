using System;
using System.Linq;
using CK.SemesterProject.Tutorial;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CK.SemesterProject.Editor
{
    public static class BattleCommandMenuIntegration
    {
        public static void ApplyCurrentScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("재생을 종료한 뒤 전투 UI를 수정하세요.");
            }
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var hud = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<PrototypeHudPresenter>(true)).SingleOrDefault();
            if (hud == null)
            {
                throw new InvalidOperationException("현재 씬에 PrototypeHudPresenter가 없습니다.");
            }
            var settings = new SerializedObject(hud);
            var playerTurn = (GameObject)settings.FindProperty("_playerTurn").objectReferenceValue;
            if (playerTurn == null)
            {
                throw new InvalidOperationException("PlayerTurnUI 연결이 없습니다.");
            }
            Transform turn = playerTurn.transform;
            Transform panel = Find(turn, "CommandSelect");
            Transform skillPanel = Find(turn, "SkillPanel");
            Transform backHint = Find(turn, "Back Hint");
            TMP_Text hint = Find(turn, "Skill Navigation Hint").GetComponent<TMP_Text>();
            Sprite normal = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/CK_Semester_Project/Graphics/UI/Image/Battle_State/DefaultUI/ActionButton/ui_skill_normal_btn.png");
            Sprite selected = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/CK_Semester_Project/Graphics/UI/Image/Battle_State/CommandSelect/ui_skill_pressed_btn.png");
            if (hint == null || normal == null || selected == null)
            {
                throw new InvalidOperationException("커맨드 안내 문구나 스프라이트가 없습니다.");
            }
            string[] names = { "Command_SkillSelect", "Command_Defend", "Command_Target", "Command_SkillView" };
            var images = new UnityEngine.UI.Image[names.Length];
            var labels = new TMP_Text[names.Length];
            // 참조를 확인한 뒤 Button만 추가한다. 기존 이미지 설정은 유지한다.
            for (int index = 0; index < names.Length; index++)
            {
                Transform command = panel.Find(names[index]);
                Transform label = command == null ? null : command.Find("Label");
                images[index] = command == null ? null : command.GetComponent<UnityEngine.UI.Image>();
                labels[index] = label == null ? null : label.GetComponent<TMP_Text>();
                if (images[index] == null || labels[index] == null)
                {
                    throw new InvalidOperationException(names[index] + "의 Image 또는 Label이 없습니다.");
                }
            }
            SerializedProperty buttons = settings.FindProperty("_commands");
            SerializedProperty graphics = settings.FindProperty("_commandImages");
            SerializedProperty texts = settings.FindProperty("_commandLabels");
            buttons.arraySize = graphics.arraySize = texts.arraySize = names.Length;
            for (int index = 0; index < names.Length; index++)
            {
                var button = images[index].GetComponent<UnityEngine.UI.Button>();
                if (button == null)
                {
                    button = images[index].gameObject.AddComponent<UnityEngine.UI.Button>();
                }
                button.targetGraphic = images[index];
                button.transition = UnityEngine.UI.Selectable.Transition.None;
                button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
                buttons.GetArrayElementAtIndex(index).objectReferenceValue = button;
                graphics.GetArrayElementAtIndex(index).objectReferenceValue = images[index];
                texts.GetArrayElementAtIndex(index).objectReferenceValue = labels[index];
            }
            settings.FindProperty("_normalCommand").objectReferenceValue = normal;
            settings.FindProperty("_selectedCommandSprite").objectReferenceValue = selected;
            settings.FindProperty("_commandPanel").objectReferenceValue = panel.gameObject;
            settings.FindProperty("_skillPanel").objectReferenceValue = skillPanel.gameObject;
            settings.FindProperty("_backHint").objectReferenceValue = backHint.gameObject;
            settings.FindProperty("_skillNavigationHint").objectReferenceValue = hint;
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
            if (!EditorSceneManager.SaveScene(hud.gameObject.scene))
            {
                throw new InvalidOperationException("전투 커맨드 UI 씬을 저장하지 못했습니다.");
            }
        }

        private static Transform Find(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).Single(item => item.name == name);
        }
    }
}

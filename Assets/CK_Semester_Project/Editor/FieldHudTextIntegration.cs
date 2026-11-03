using System;
using System.Linq;
using CK.SemesterProject.LevelDesign;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CK.SemesterProject.Editor
{
    public static class FieldHudTextIntegration
    {
        public static void ApplyCurrentScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("재생을 종료한 뒤 탐험 HUD를 수정하세요.");
            }
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path))
            {
                throw new InvalidOperationException("저장된 씬을 연 뒤 탐험 HUD를 수정하세요.");
            }
            GameObject[] roots = scene.GetRootGameObjects();
            // HUD Canvas는 Tutorial Systems 아래에 있으므로 루트로 한정하지 않는다.
            Transform field = roots.SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
                .Where(canvas => canvas.name == "Canvas")
                .Select(canvas => canvas.transform.Find("Default_State")).SingleOrDefault(item => item != null);
            if (field == null)
            {
                throw new InvalidOperationException("현재 씬에 Canvas/Default_State가 없습니다.");
            }
            Transform location = Find(field, "01_Location");
            Transform story = Find(field, "09_Story_Panel");
            TMP_Text areaName = FindText(location, "Runtime Location", true);
            TMP_Text storyTitle = FindText(story, "Runtime Tutorial", true);
            TMP_Text areaEnglish = FindText(location, "Runtime Location EN", false);
            TMP_Text storyNumber = FindText(story, "Story Number", false);
            TMP_Text storyEnglish = FindText(story, "Story Title EN", false);
            TMP_Text objective = FindText(story, "Story Objective", false);
            var progress = roots.SelectMany(root => root.GetComponentsInChildren<LevelProgress>(true)).SingleOrDefault();
            if (progress == null || areaName.font == null)
            {
                throw new InvalidOperationException("LevelProgress 또는 Runtime Location의 폰트가 없습니다.");
            }
            LevelTriggerZone[] zones = roots.SelectMany(root => root.GetComponentsInChildren<LevelTriggerZone>(true)).ToArray();
            string[] zoneNames = { "Zone02_Entry_Checkpoint", "Zone03_Entry_Checkpoint", "Zone04_Entry_Checkpoint" };
            LevelTriggerZone[] checkpoints = zoneNames.Select(name => zones.SingleOrDefault(zone => zone.name == name)).ToArray();

            // 기존 텍스트는 재사용하고, 없는 줄만 만든다.
            TMP_FontAsset font = areaName.font;
            Configure(areaName, font, new Vector2(30, 12), new Vector2(220, 32), 26, FontStyles.Bold, Color.white);
            areaEnglish = GetOrCreateText(location, "Runtime Location EN", areaEnglish);
            Configure(areaEnglish, font, new Vector2(30, -14), new Vector2(220, 24), 17, FontStyles.Normal,
                new Color(.85f, .92f, 1f));
            storyNumber = GetOrCreateText(story, "Story Number", storyNumber);
            Configure(storyNumber, font, new Vector2(-62, 72), new Vector2(80, 26), 19, FontStyles.Normal, Color.white);
            Configure(storyTitle, font, new Vector2(-25, 25), new Vector2(350, 34), 26, FontStyles.Bold, Color.white);
            storyEnglish = GetOrCreateText(story, "Story Title EN", storyEnglish);
            Configure(storyEnglish, font, new Vector2(-25, -4), new Vector2(350, 24), 18, FontStyles.Normal,
                new Color(.45f, .78f, 1f));
            objective = GetOrCreateText(story, "Story Objective", objective);
            Configure(objective, font, new Vector2(5, -46), new Vector2(300, 28), 18, FontStyles.Normal, Color.white,
                TextOverflowModes.Ellipsis);

            const string startArea = "튜토리얼 구역";
            const string startAreaEnglish = "TUTORIAL AREA";
            const string startNumber = "#01";
            const string startTitle = "조용한 인공도시";
            const string startTitleEnglish = "Quiet Artificial city";
            var settings = new SerializedObject(progress);
            settings.FindProperty("_areaNameText").objectReferenceValue = areaName;
            settings.FindProperty("_areaNameEnglishText").objectReferenceValue = areaEnglish;
            settings.FindProperty("_storyNumberText").objectReferenceValue = storyNumber;
            settings.FindProperty("_storyTitleText").objectReferenceValue = storyTitle;
            settings.FindProperty("_storyTitleEnglishText").objectReferenceValue = storyEnglish;
            settings.FindProperty("_storyObjectiveText").objectReferenceValue = objective;
            settings.FindProperty("_startAreaName").stringValue = startArea;
            settings.FindProperty("_startAreaNameEnglish").stringValue = startAreaEnglish;
            settings.FindProperty("_startStoryNumber").stringValue = startNumber;
            settings.FindProperty("_startStoryTitle").stringValue = startTitle;
            settings.FindProperty("_startStoryTitleEnglish").stringValue = startTitleEnglish;
            settings.FindProperty("_objectivePanel").objectReferenceValue = null;
            settings.FindProperty("_objectiveText").objectReferenceValue = null;
            settings.ApplyModifiedPropertiesWithoutUndo();

            // 편집 화면에도 시작값을 표시한다. 목표는 기존 진행 스크립트가 정한다.
            areaName.text = startArea;
            areaEnglish.text = startAreaEnglish;
            storyNumber.text = startNumber;
            storyTitle.text = startTitle;
            storyEnglish.text = startTitleEnglish;
            objective.text = progress.Objective;
            foreach (TMP_Text text in new[] { areaName, areaEnglish, storyNumber, storyTitle, storyEnglish, objective })
            {
                EditorUtility.SetDirty(text);
                EditorUtility.SetDirty(text.rectTransform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(text);
                PrefabUtility.RecordPrefabInstancePropertyModifications(text.rectTransform);
            }

            string[] areaNames = { "엘리베이터 복도", "중앙 복도", "홀" };
            string[] englishNames = { "ELEVATOR CORRIDOR", "CENTRAL CORRIDOR", "HALL" };
            for (int index = 0; index < checkpoints.Length; index++)
            {
                LevelTriggerZone zone = checkpoints[index];
                if (zone == null)
                {
                    Debug.LogWarning("FieldHudTextIntegration: " + zoneNames[index] + "가 없어 위치 연결을 건너뜁니다.");
                    continue;
                }
                var zoneSettings = new SerializedObject(zone);
                zoneSettings.FindProperty("_areaName").stringValue = areaNames[index];
                zoneSettings.FindProperty("_areaNameEnglish").stringValue = englishNames[index];
                zoneSettings.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException("탐험 HUD 씬을 저장하지 못했습니다.");
            }
        }

        private static Transform Find(Transform root, string name)
        {
            Transform result = root.GetComponentsInChildren<Transform>(true).SingleOrDefault(item => item.name == name);
            if (result == null)
            {
                throw new InvalidOperationException(root.name + " 아래에 " + name + "가 없습니다.");
            }
            return result;
        }

        private static TMP_Text FindText(Transform parent, string name, bool required)
        {
            Transform child = parent.Find(name);
            if (child == null && !required)
            {
                return null;
            }
            TMP_Text text = child == null ? null : child.GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                throw new InvalidOperationException(parent.name + "/" + name + "의 TextMeshProUGUI가 없습니다.");
            }
            return text;
        }

        private static TMP_Text GetOrCreateText(Transform parent, string name, TMP_Text existing)
        {
            if (existing != null)
            {
                return existing;
            }
            var child = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            child.layer = parent.gameObject.layer;
            child.transform.SetParent(parent, false);
            return child.GetComponent<TextMeshProUGUI>();
        }

        private static void Configure(TMP_Text text, TMP_FontAsset font, Vector2 position, Vector2 size,
            float fontSize, FontStyles style, Color color, TextOverflowModes overflow = TextOverflowModes.Overflow)
        {
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition3D = new Vector3(position.x, position.y, 0f);
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            text.font = font;
            text.enableAutoSizing = false;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = overflow;
            text.margin = Vector4.zero;
            text.color = color;
            text.raycastTarget = false;
        }
    }
}

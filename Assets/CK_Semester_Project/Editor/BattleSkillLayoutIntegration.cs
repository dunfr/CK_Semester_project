using System;
using System.Linq;
using CK.SemesterProject.Tutorial;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CK.SemesterProject.Editor
{
    public static class BattleSkillLayoutIntegration
    {
        public static void ApplyCurrentScene()
        {
            if (EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("재생을 종료한 뒤 전투 UI를 수정하세요.");
            }
            var hud = UnityEngine.Object.FindFirstObjectByType<PrototypeHudPresenter>();
            var settings = new SerializedObject(hud);
            Transform turn = ((GameObject)settings.FindProperty("_playerTurn").objectReferenceValue).transform;
            Transform combat = turn.parent;
            RectTransform panel = Find(turn, "SkillPanel");
            Place(panel, new Vector2(.46f, .52f), Vector2.zero, new Vector2(420, 460));
            panel.GetComponent<UnityEngine.UI.Image>().enabled = false;
            foreach (Transform child in panel.Cast<Transform>().Where(item => !item.name.StartsWith("SkillButton", StringComparison.Ordinal) && item.name != "MemoryThrowPanel" && item.name != "MemoryCounter").ToArray())
            {
                child.gameObject.SetActive(false);
            }
            Transform arc = panel.Find("Skill Arc");
            if (arc == null)
            {
                arc = new GameObject("Skill Arc", typeof(RectTransform), typeof(CanvasRenderer), typeof(BattleSkillArcGraphic)).transform;
                arc.SetParent(panel, false);
            }
            arc.gameObject.SetActive(true);
            arc.SetAsFirstSibling();
            Place((RectTransform)arc, new Vector2(.5f, .5f), Vector2.zero, new Vector2(420, 460));
            arc.GetComponent<BattleSkillArcGraphic>().raycastTarget = false;
            string[] names = { "SkillButton_NormalAttack", "SkillButton_QuickSlash", "SkillButton_PinkTrail" };
            for (int index = 0; index < names.Length; index++)
            {
                RectTransform button = Find(panel, names[index]);
                float angle = (1 - index) * 25f * Mathf.Deg2Rad;
                Place(button, new Vector2(.5f, .5f), new Vector2(155 - Mathf.Abs(1 - index) * 22, Mathf.Sin(angle) * 238), new Vector2(282, 64));
                Place(Find(button, "Icon"), new Vector2(.5f, .5f), new Vector2(-110, 0), new Vector2(49, 49));
                Place(Find(button, "Title"), new Vector2(.5f, .5f), new Vector2(-20, 7), new Vector2(126, 30));
                Place(Find(button, "Level"), new Vector2(.5f, .5f), new Vector2(-20, -16), new Vector2(126, 20));
                Place(Find(button, "SPCost"), new Vector2(.5f, .5f), new Vector2(100, 0), new Vector2(78, 30));
                TMP_Text title = button.Find("Title").GetComponent<TMP_Text>();
                title.fontSize = 19;
                title.enableAutoSizing = true;
                title.fontSizeMin = 14;
                title.fontSizeMax = 19;
                title.alignment = TextAlignmentOptions.MidlineLeft;
                button.Find("SPCost").GetComponent<TMP_Text>().fontSize = 17;
                var navigation = button.GetComponent<UnityEngine.UI.Button>();
                navigation.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            }
            RectTransform investment = Find(turn, "MemoryThrowPanel");
            investment.SetParent(panel, false);
            investment.gameObject.SetActive(true);
            investment.SetSiblingIndex(1);
            Place(investment, new Vector2(.5f, .5f), new Vector2(125, -220), new Vector2(300, 116));
            investment.GetComponent<UnityEngine.UI.Image>().color = Color.white;
            investment.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            for (int index = 0; index < 5; index++)
            {
                RectTransform slot = Find(investment, "MemorySlot_0" + (index + 1));
                Place(slot, new Vector2(.5f, .5f), new Vector2(-104 + index * 52, -18), new Vector2(44, 44));
                slot.localScale = Vector3.one * .85f;
                var emptySlot = slot.Find("Star").GetComponent<UnityEngine.UI.Image>();
                emptySlot.color = new Color(.4f, .7f, .9f, .65f);
            }
            TMP_Text investmentLabel = Find(investment, "Runtime Investment").GetComponent<TMP_Text>();
            Place(investmentLabel.rectTransform, new Vector2(.5f, .5f), new Vector2(-8, 25), new Vector2(230, 26));
            investmentLabel.fontSize = 17;
            investmentLabel.alignment = TextAlignmentOptions.Center;
            Find(investment, "Runtime Slot Track").gameObject.SetActive(false);
            RectTransform counter = Find(combat, "MemoryCounter");
            counter.SetParent(panel, false);
            Place(counter, new Vector2(.5f, .5f), new Vector2(125, -310), new Vector2(250, 44));
            counter.localScale = Vector3.one;
            counter.gameObject.SetActive(true);

            RectTransform description = Find(turn, "SkillDescriptionPanel");
            Place(description, new Vector2(.5f, .52f), new Vector2(414, 0), new Vector2(370, 310));
            settings.FindProperty("_skillDetailsPanel").objectReferenceValue = description.gameObject;
            description.gameObject.SetActive(false);
            RectTransform execute = Find(turn, "Use Selected Skill");
            execute.SetParent(description, false);
            Place(execute, new Vector2(.5f, 0), new Vector2(-73, -28), new Vector2(190, 34));
            Place(Find(turn, "HUD Defend"), new Vector2(.5f, .52f), new Vector2(604, -183), new Vector2(130, 34));
            RectTransform prompt = turn.Find("Skill Navigation Hint") as RectTransform;
            if (prompt == null)
            {
                prompt = new GameObject("Skill Navigation Hint", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<RectTransform>();
                prompt.SetParent(turn, false);
            }
            Place(prompt, new Vector2(1, 0), new Vector2(-230, 65), new Vector2(410, 34));
            var promptText = prompt.GetComponent<TMP_Text>();
            promptText.font = investmentLabel.font;
            promptText.text = "↑ ↓ 스킬 선택   ·   Enter 사용   ·   F 투자";
            promptText.fontSize = 17;
            promptText.color = new Color(.7f, .92f, 1);
            promptText.alignment = TextAlignmentOptions.Right;
            promptText.raycastTarget = false;

            RectTransform status = Find(combat, "PlayerStatusPanel");
            Place(status, Vector2.zero, new Vector2(475, 96), status.sizeDelta);
            status.localScale = Vector3.one * .78f;
            Place(Find(combat, "EnemyCard"), new Vector2(1, 1), new Vector2(-255, -260), new Vector2(489.48f, 265.12f));
            Find(combat, "EnemyCard").localScale = Vector3.one * .9f;
            var enemyCardName = (TMP_Text)settings.FindProperty("_enemyCardName").objectReferenceValue;
            Place(enemyCardName.rectTransform, new Vector2(.5f, .5f), new Vector2(-72, 84), new Vector2(270, 36));
            var enemyDetails = (TMP_Text)settings.FindProperty("_enemyDetails").objectReferenceValue;
            Place(enemyDetails.rectTransform, new Vector2(.5f, .5f), new Vector2(0, -13), new Vector2(400, 146));
            RectTransform banner = Find(combat, "EnemyNameBanner");
            Place(banner, new Vector2(.5f, 1), new Vector2(0, -70), banner.sizeDelta);
            banner.localScale = Vector3.one * .8f;
            RectTransform header = Find(combat, "CombatHeader");
            Place(header, new Vector2(0, 1), new Vector2(238, -48), header.sizeDelta);
            header.localScale = Vector3.one * .85f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        private static RectTransform Find(Transform root, string name)
        {
            return root.GetComponentsInChildren<RectTransform>(true).Single(item => item.name == name);
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}


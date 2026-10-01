using System.Linq;
using CK.SemesterProject.Tutorial;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CK.SemesterProject.Editor
{
    public static class PrototypeUiIntegration
    {
        private static readonly Color PanelColor = new Color(0.025f, 0.06f, 0.105f, 0.94f);
        private static readonly Color AccentColor = new Color(0.2f, 0.85f, 1f);
        private static TMP_FontAsset _font;

        [MenuItem("CK/Tutorial/Connect Prototype UI")]
        public static void ApplyCurrentScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            TutorialBattleController battle = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TutorialBattleController>(true)).Single();
            var settings = new SerializedObject(battle);
            PlayerMovement player = (PlayerMovement)settings.FindProperty("_movement").objectReferenceValue;
            GameObject oldCanvas = (GameObject)settings.FindProperty("_fieldUI").objectReferenceValue;
            while (oldCanvas.GetComponent<Canvas>() == null)
            {
                oldCanvas = oldCanvas.transform.parent.gameObject;
            }
            Transform canvasParent = oldCanvas.transform.parent;
            _font = ((TMP_Text)settings.FindProperty("_notice").objectReferenceValue).font;
            foreach (string output in new[] { "_notice", "_continueButton" })
            {
                ((Component)settings.FindProperty(output).objectReferenceValue).transform.SetParent(battle.transform, false);
            }
            // 코어가 참조하는 호환용 컨트롤은 별도 Canvas에 남긴다.
            Transform legacy = ((Button)settings.FindProperty("_skillButtons").GetArrayElementAtIndex(0).objectReferenceValue).transform.parent;
            if (legacy.IsChildOf(oldCanvas.transform))
            {
                legacy.SetParent(battle.transform, false);
            }
            legacy.gameObject.SetActive(false);
            UnityEngine.Object.DestroyImmediate(oldCanvas);
            PrototypeHudPresenter previous = battle.GetComponent<PrototypeHudPresenter>();
            if (previous != null)
            {
                UnityEngine.Object.DestroyImmediate(previous);
            }

            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(canvasParent, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            Transform field = Stretch(canvas.transform, "Default_State");
            Transform combat = Stretch(canvas.transform, "Battle_State");
            Transform turn = Stretch(combat, "PlayerTurnUI");
            var hud = battle.gameObject.AddComponent<PrototypeHudPresenter>();
            var bindings = new SerializedObject(hud);
            Set(bindings, "_battle", battle);
            Set(bindings, "_player", player.transform);
            Set(bindings, "_fieldRoot", field.gameObject);
            Set(bindings, "_playerTurn", turn.gameObject);
            FloorTravelButtons travel = player.GetComponent<FloorTravelButtons>();
            Set(bindings, "_floorTravel", travel);
            if (travel != null)
            {
                var travelSettings = new SerializedObject(travel);
                travelSettings.FindProperty("_showButtons").boolValue = false;
                travelSettings.ApplyModifiedPropertiesWithoutUndo();
            }

            Transform location = Panel(field, "Field Location", new Vector2(0, 1), new Vector2(28, -28), new Vector2(390, 146));
            Text(location, "Location Header", "EXPLORATION / TUTORIAL", 20, -22, 350, 22, 18, AccentColor);
            BindText(bindings, "_fieldLocation", location, "Runtime Location", 20, -54, 350, 36, 30);
            BindText(bindings, "_floorBanner", location, "Runtime Floor", 20, -101, 350, 26, 18);
            Transform card = Panel(field, "Field Player", new Vector2(1, 1), new Vector2(-28, -28), new Vector2(380, 146));
            BindText(bindings, "_fieldName", card, "Runtime Character", 20, -20, 340, 38, 28);
            Slider fieldHp = Bar(card, "Runtime Field HP", 20, -72, 340, 10, AccentColor);
            Set(bindings, "_fieldHp", fieldHp);
            TMP_Text fieldVitals = BindText(bindings, "_fieldMemory", card, "Runtime Field Vitals", 20, -96, 340, 28, 19);
            Set(settings, "_fieldHpBar", fieldHp);
            Set(settings, "_fieldVitals", fieldVitals);
            Transform guide = Panel(field, "Field Guide", new Vector2(0, 0), new Vector2(28, 296), new Vector2(390, 118));
            Text(guide, "Guide Header", "튜토리얼 데모", 20, -18, 350, 30, 23, AccentColor);
            Text(guide, "Guide Text", "몬스터에 접근해 전투를 시작하세요", 20, -61, 350, 36, 20);
            Transform mapPanel = Panel(field, "Field Map", new Vector2(0, 0), new Vector2(28, 28), new Vector2(248, 248));
            RectTransform mapRect = Rect(mapPanel, "Runtime Minimap", 10, -10, 228, 228);
            RawImage map = mapRect.gameObject.AddComponent<RawImage>();
            map.raycastTarget = false;
            Set(bindings, "_minimap", map);
            TMP_Text marker = Text(mapRect, "Player Marker", "▲", 99, -99, 30, 30, 24, AccentColor);
            marker.alignment = TextAlignmentOptions.Center;
            Transform floorButtons = Panel(field, "Floor Travel", new Vector2(1, 0), new Vector2(-28, 28), new Vector2(290, 94));
            Text(floorButtons, "Travel Header", "층 이동", 16, -12, 258, 22, 18, AccentColor);
            Set(bindings, "_floorOne", ActionButton(floorButtons, "Travel First Floor", "1층", 16, -43, 122, 36));
            Set(bindings, "_floorTwo", ActionButton(floorButtons, "Travel Second Floor", "2층", 152, -43, 122, 36));

            Transform enemyHeader = Panel(combat, "Enemy Header", new Vector2(.5f, 1), new Vector2(0, -28), new Vector2(480, 116));
            BindText(bindings, "_enemyName", enemyHeader, "EnemyName_TMP", 20, -18, 440, 42, 30);
            Set(bindings, "_enemyHp", Bar(enemyHeader, "EnemyHP_Slider", 20, -80, 440, 12, new Color(1, .25f, .35f)));
            Transform rounds = Panel(combat, "Battle Round", new Vector2(1, 1), new Vector2(-28, -28), new Vector2(360, 68));
            BindText(bindings, "_round", rounds, "Runtime Round", 18, -18, 324, 32, 23);
            Transform enemyCard = Panel(combat, "EnemyCard", new Vector2(1, 1), new Vector2(-28, -120), new Vector2(400, 285));
            BindText(bindings, "_enemyCardName", enemyCard, "EnemyCardName_TMP", 20, -18, 360, 34, 25);
            BindText(bindings, "_enemyDetails", enemyCard, "Runtime Enemy Details", 20, -66, 360, 128, 20);
            Set(bindings, "_enemyCardHp", Bar(enemyCard, "EnemyCardHP_Slider", 20, -208, 360, 8, new Color(1, .25f, .35f)));
            var targets = new Button[3];
            var targetNames = new TMP_Text[3];
            for (int index = 0; index < 3; index++)
            {
                targets[index] = ActionButton(enemyCard, "HUD Target " + (index + 1), "", 20 + index * 122, -232, 116, 36);
                targetNames[index] = targets[index].GetComponentInChildren<TMP_Text>();
                targetNames[index].fontSize = 16;
            }
            SetArray(bindings, "_targets", targets);
            SetArray(bindings, "_targetNames", targetNames);
            Transform order = Panel(combat, "Turn Order", new Vector2(0, 1), new Vector2(28, -28), new Vector2(260, 326));
            Text(order, "Order Header", "행동 순서", 18, -16, 224, 30, 21, AccentColor);
            SerializedProperty slots = bindings.FindProperty("_turnSlots");
            slots.arraySize = 4;
            for (int index = 0; index < 4; index++)
            {
                RectTransform slot = Rect(order, "TurnOrderSlot0" + (index + 1), 14, -58 - index * 64, 232, 56);
                Image highlight = slot.gameObject.AddComponent<Image>();
                highlight.color = new Color(.08f, .3f, .42f, .9f);
                highlight.raycastTarget = false;
                TMP_Text actor = Text(slot, "Runtime Actor", "", 10, -7, 212, 30, 19);
                Slider hp = Bar(slot, "Actor HP", 10, -44, 212, 5, AccentColor);
                SerializedProperty row = slots.GetArrayElementAtIndex(index);
                row.FindPropertyRelative("_root").objectReferenceValue = slot.gameObject;
                RectTransform glow = Rect(slot, "Highlight", 0, 0, 3, 56);
                Image glowImage = glow.gameObject.AddComponent<Image>();
                glowImage.color = AccentColor;
                glowImage.raycastTarget = false;
                row.FindPropertyRelative("_highlight").objectReferenceValue = glow.gameObject;
                row.FindPropertyRelative("_hp").objectReferenceValue = hp;
                row.FindPropertyRelative("_name").objectReferenceValue = actor;
            }
            Transform vitals = Panel(combat, "Player Status", new Vector2(0, 0), new Vector2(28, 72), new Vector2(770, 108));
            Text(vitals, "HP Label", "HP", 20, -18, 42, 22, 18, AccentColor);
            Set(bindings, "_playerHp", Bar(vitals, "PlayerHP_Slider", 70, -20, 400, 12, AccentColor));
            Text(vitals, "Memory Label", "MEM", 20, -44, 44, 22, 16, new Color(.75f, .5f, 1));
            Set(bindings, "_playerMemory", Bar(vitals, "PlayerSP_Slider", 70, -46, 400, 10, new Color(.75f, .5f, 1)));
            BindText(bindings, "_playerVitals", vitals, "Runtime Vitals", 20, -76, 730, 24, 18);
            Text(vitals, "Memory Counter Header", "메모리", 500, -18, 100, 24, 18);
            BindText(bindings, "_memoryCurrent", vitals, "MemoryCurrentValue", 604, -18, 60, 24, 20);
            Text(vitals, "Memory Separator", "/", 666, -18, 20, 24, 20);
            BindText(bindings, "_memoryCapacity", vitals, "MemoryCapacityValue", 690, -18, 60, 24, 20);

            Transform skillsPanel = Panel(turn, "SkillPanel", new Vector2(0, 0), new Vector2(28, 204), new Vector2(340, 294));
            Text(skillsPanel, "Skill Header", "스킬", 18, -16, 150, 30, 23, AccentColor);
            Text(skillsPanel, "Skill Memory Header", "MEM", 210, -20, 55, 24, 17);
            BindText(bindings, "_skillMemory", skillsPanel, "SkillMemory", 270, -20, 52, 24, 19);
            var skills = new Button[3];
            var skillNames = new TMP_Text[3];
            var costs = new TMP_Text[3];
            var images = new Image[3];
            string[] names = { "SkillButton_NormalAttack", "SkillButton_QuickSlash", "SkillButton_PinkTrail" };
            for (int index = 0; index < 3; index++)
            {
                skills[index] = ActionButton(skillsPanel, names[index], "", 16, -66 - index * 70, 308, 58);
                UnityEngine.Object.DestroyImmediate(skills[index].GetComponentInChildren<TMP_Text>().gameObject);
                skillNames[index] = Text(skills[index].transform, "Title", "", 14, -15, 184, 30, 22);
                costs[index] = Text(skills[index].transform, "SPCost", "", 202, -17, 96, 26, 18, AccentColor);
                images[index] = skills[index].GetComponent<Image>();
            }
            SetArray(bindings, "_skills", skills);
            SetArray(bindings, "_skillNames", skillNames);
            SetArray(bindings, "_skillCosts", costs);
            SetArray(bindings, "_skillImages", images);
            Transform detail = Panel(turn, "Skill Details", new Vector2(0, 0), new Vector2(388, 204), new Vector2(410, 398));
            BindText(bindings, "_skillName", detail, "SkillNameText", 20, -18, 370, 36, 27);
            BindText(bindings, "_skillType", detail, "SkillTypeText", 20, -60, 370, 26, 18);
            BindText(bindings, "_skillDescription", detail, "DescriptionText", 20, -104, 370, 86, 21);
            string[] properties = { "_skillPower", "_skillAccuracy", "_skillElement", "_skillTarget" };
            string[] labels = { "기본 위력", "명중", "속성", "대상" };
            string[] valueNames = { "PowerValueText", "AccuracyValueText", "AttributeValueText", "RangeValueText" };
            for (int index = 0; index < properties.Length; index++)
            {
                Text(detail, "Stat Label " + index, labels[index], 20, -210 - index * 30, 120, 26, 18, AccentColor);
                BindText(bindings, properties[index], detail, valueNames[index], 150, -210 - index * 30, 240, 26, 19);
            }
            Set(bindings, "_execute", ActionButton(detail, "Use Selected Skill", "사용 [Enter]", 20, -346, 222, 36));
            Set(bindings, "_defend", ActionButton(detail, "HUD Defend", "방어", 256, -346, 134, 36));
            Transform investmentPanel = Panel(turn, "Investment Panel", new Vector2(0, 0), new Vector2(28, 518), new Vector2(340, 84));
            Button investment = ActionButton(investmentPanel, "MemoryThrowPanel", "", 8, -8, 324, 68);
            UnityEngine.Object.DestroyImmediate(investment.GetComponentInChildren<TMP_Text>().gameObject);
            Set(bindings, "_investment", investment);
            BindText(bindings, "_investmentCost", investment.transform, "Runtime Investment", 10, -7, 304, 26, 18);
            var glowSlots = new GameObject[5];
            var locks = new GameObject[5];
            for (int index = 0; index < 5; index++)
            {
                RectTransform slot = Rect(investment.transform, "MemorySlot_0" + (index + 1), 12 + index * 60, -43, 52, 14);
                Image track = slot.gameObject.AddComponent<Image>();
                track.color = new Color(.12f, .22f, .32f);
                track.raycastTarget = false;
                RectTransform glow = Rect(slot, "Glow", 0, 0, 52, 14);
                glow.gameObject.AddComponent<Image>().color = AccentColor;
                glow.GetComponent<Image>().raycastTarget = false;
                glowSlots[index] = glow.gameObject;
                locks[index] = Text(slot, "Lock", "×", 0, 0, 52, 14, 14).gameObject;
                locks[index].GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;
            }
            SetArray(bindings, "_investmentGlow", glowSlots);
            SetArray(bindings, "_investmentLock", locks);
            foreach (string output in new[] { "_notice", "_continueButton" })
            {
                Component item = (Component)settings.FindProperty(output).objectReferenceValue;
                RectTransform rect = (RectTransform)item.transform;
                rect.SetParent(combat, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 0);
                rect.anchoredPosition = new Vector2(0, output == "_notice" ? 20 : 76);
                rect.sizeDelta = output == "_notice" ? new Vector2(1100, 32) : new Vector2(300, 48);
            }
            Transform noticePanel = Panel(combat, "Battle Notice", new Vector2(.5f, 0), new Vector2(0, 12), new Vector2(1100, 44));
            TMP_Text notice = (TMP_Text)settings.FindProperty("_notice").objectReferenceValue;
            notice.transform.SetParent(noticePanel, false);
            notice.rectTransform.anchorMin = notice.rectTransform.anchorMax = notice.rectTransform.pivot = new Vector2(.5f, .5f);
            notice.rectTransform.anchoredPosition = Vector2.zero;
            notice.rectTransform.sizeDelta = new Vector2(1060, 32);
            notice.raycastTarget = false;
            Set(settings, "_fieldUI", field.gameObject);
            Set(settings, "_battleUI", combat.gameObject);
            bindings.ApplyModifiedPropertiesWithoutUndo();
            settings.ApplyModifiedPropertiesWithoutUndo();
            combat.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static RectTransform Stretch(Transform parent, string name)
        {
            RectTransform rect = Rect(parent, name, 0, 0, 0, 0);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Transform Panel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            RectTransform rect = Rect(parent, name, 0, 0, size.x, size.y);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = PanelColor;
            image.raycastTarget = false;
            RectTransform edge = Rect(rect, "Accent", 0, 0, size.x, 2);
            edge.gameObject.AddComponent<Image>().color = AccentColor;
            edge.GetComponent<Image>().raycastTarget = false;
            return rect;
        }

        private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var item = new GameObject(name, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static TMP_Text Text(Transform parent, string name, string value, float x, float y, float width, float height, float size, Color? color = null)
        {
            RectTransform rect = Rect(parent, name, x, y, width, height);
            TMP_Text text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = _font;
            text.text = value;
            text.fontSize = size;
            text.color = color ?? new Color(.9f, .95f, 1);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private static TMP_Text BindText(SerializedObject settings, string field, Transform parent, string name, float x, float y, float width, float height, float size)
        {
            TMP_Text text = Text(parent, name, "", x, y, width, height, size);
            Set(settings, field, text);
            return text;
        }

        private static Button ActionButton(Transform parent, string name, string value, float x, float y, float width, float height)
        {
            RectTransform rect = Rect(parent, name, x, y, width, height);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.07f, .17f, .26f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(.55f, .9f, 1);
            colors.pressedColor = new Color(.3f, .7f, .9f);
            colors.disabledColor = new Color(.4f, .4f, .4f, .7f);
            button.colors = colors;
            TMP_Text text = Text(rect, "Label", value, 8, -4, width - 16, height - 8, 21);
            text.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private static Slider Bar(Transform parent, string name, float x, float y, float width, float height, Color color)
        {
            RectTransform rect = Rect(parent, name, x, y, width, height);
            Image background = rect.gameObject.AddComponent<Image>();
            background.color = new Color(.13f, .2f, .28f);
            background.raycastTarget = false;
            RectTransform fill = Stretch(rect, "Fill");
            Image image = fill.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            Slider slider = rect.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.interactable = false;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            return slider;
        }

        private static void Set(SerializedObject settings, string field, UnityEngine.Object value)
        {
            settings.FindProperty(field).objectReferenceValue = value;
        }

        private static void SetArray<T>(SerializedObject settings, string field, T[] values) where T : UnityEngine.Object
        {
            SerializedProperty property = settings.FindProperty(field);
            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }
        }
    }
}

using System;
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
        private const string UiScene = "Assets/CK_Semester_Project/Prototype/Scenes/TutorialDemo.unity";
        private const string SpriteFolder = "Assets/CK_Semester_Project/Prototype/Temp/Graphics/UI/10-01/Battle_State/";

        public static void ApplyCurrentScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            TutorialBattleController battle = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TutorialBattleController>(true)).Single();
            var settings = new SerializedObject(battle);
            PlayerMovement player = (PlayerMovement)settings.FindProperty("_movement").objectReferenceValue;
            GameObject canvas;
            if (scene.path == UiScene)
            {
                canvas = scene.GetRootGameObjects().Single(root => root.name == "Canvas");
            }
            else
            {
                GameObject oldCanvas = ((GameObject)settings.FindProperty("_fieldUI").objectReferenceValue);
                while (oldCanvas.GetComponent<Canvas>() == null)
                {
                    oldCanvas = oldCanvas.transform.parent.gameObject;
                }
                Transform parent = oldCanvas.transform.parent;
                foreach (string output in new[] { "_notice", "_continueButton" })
                {
                    Component item = (Component)settings.FindProperty(output).objectReferenceValue;
                    item.transform.SetParent(battle.transform, false);
                }
                Scene source = EditorSceneManager.OpenScene(UiScene, OpenSceneMode.Additive);
                canvas = UnityEngine.Object.Instantiate(source.GetRootGameObjects().Single(root => root.name == "Canvas"));
                canvas.name = "Canvas";
                canvas.transform.SetParent(null);
                SceneManager.MoveGameObjectToScene(canvas, scene);
                EditorSceneManager.CloseScene(source, true);
                SceneManager.SetActiveScene(scene);
                canvas.transform.SetParent(parent, false);
                UnityEngine.Object.DestroyImmediate(oldCanvas);
            }
            Transform field = canvas.transform.Find("Default_State");
            Transform combat = canvas.transform.Find("Battle_State");
            field.gameObject.SetActive(true);
            combat.gameObject.SetActive(false);
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.matchWidthOrHeight = 0.5f;
            TMP_FontAsset font = Find<TMP_Text>(combat, "EnemyName_TMP").font;
            // 장식 그림이 투명한 영역에서도 클릭을 가로채지 않도록 입력은 버튼으로 제한한다.
            foreach (Graphic graphic in canvas.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }
            foreach (Button button in canvas.GetComponentsInChildren<Button>(true))
            {
                if (button.targetGraphic != null)
                {
                    button.targetGraphic.raycastTarget = true;
                }
            }
            foreach (Button button in field.GetComponentsInChildren<Button>(true))
            {
                // 원본에서 아직 동작이 연결되지 않은 메뉴/달리기 등은 조작 가능한 것처럼 표시하지 않는다.
                button.interactable = false;
                button.targetGraphic.raycastTarget = false;
            }

            PrototypeHudPresenter previous = battle.GetComponent<PrototypeHudPresenter>();
            if (previous != null)
            {
                UnityEngine.Object.DestroyImmediate(previous);
            }
            PrototypeHudPresenter hud = battle.gameObject.AddComponent<PrototypeHudPresenter>();
            var hudSettings = new SerializedObject(hud);
            Set(hudSettings, "_battle", battle);
            Set(hudSettings, "_floorTravel", player.GetComponent<FloorTravelButtons>());
            Set(hudSettings, "_player", player.transform);
            Set(hudSettings, "_fieldRoot", field.gameObject);
            Set(hudSettings, "_playerTurn", Find<Transform>(combat, "PlayerTurnUI").gameObject);
            FloorTravelButtons floorTravel = player.GetComponent<FloorTravelButtons>();
            if (floorTravel != null)
            {
                var floorSettings = new SerializedObject(floorTravel);
                floorSettings.FindProperty("_showButtons").boolValue = false;
                floorSettings.ApplyModifiedPropertiesWithoutUndo();
            }

            Transform card = Find<Transform>(field, "06_Student_Card");
            Set(hudSettings, "_fieldName", Label(card, "Runtime Character", new Vector2(-65f, 0f), new Vector2(215f, 62f), 26f, font));
            TMP_Text fieldVitals = Label(card, "Runtime Field Vitals", new Vector2(0f, -66f), new Vector2(350f, 34f), 18f, font);
            Set(hudSettings, "_fieldMemory", fieldVitals);
            Slider fieldHp = CreateHp(card, Find<Slider>(combat, "PlayerHP_Slider"), new Vector2(-25f, -44f), new Vector2(290f, 9f));
            Set(hudSettings, "_fieldHp", fieldHp);
            Set(settings, "_fieldHpBar", fieldHp);
            Set(settings, "_fieldVitals", fieldVitals);
            Set(hudSettings, "_fieldLocation", Label(Find<Transform>(field, "01_Location"), "Runtime Location", Vector2.zero, new Vector2(300f, 70f), 27f, font));
            Set(hudSettings, "_floorBanner", Label(Find<Transform>(field, "02_Floor_Banner"), "Runtime Floor", Vector2.zero, new Vector2(690f, 90f), 32f, font));
            Label(Find<Transform>(field, "09_Story_Panel"), "Runtime Tutorial", new Vector2(0f, -10f), new Vector2(400f, 150f), 24f, font).text
                = "튜토리얼 데모\n몬스터에 접근해 전투를 시작하세요";
            Transform minimap = Find<Transform>(field, "10_Minimap");
            var map = new GameObject("Runtime Minimap", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            map.transform.SetParent(minimap, false);
            RectTransform mapRect = map.GetComponent<RectTransform>();
            mapRect.anchoredPosition = new Vector2(0f, -10f);
            mapRect.sizeDelta = new Vector2(230f, 230f);
            map.GetComponent<RawImage>().raycastTarget = false;
            Set(hudSettings, "_minimap", map.GetComponent<RawImage>());
            Label(map.transform, "Player Marker", Vector2.zero, new Vector2(30f, 30f), 22f, font).text = "▲";

            Set(hudSettings, "_enemyName", Find<TMP_Text>(combat, "EnemyName_TMP"));
            Set(hudSettings, "_enemyCardName", Find<TMP_Text>(combat, "EnemyCardName_TMP"));
            Find<TMP_Text>(combat, "EnemyName_TMP").rectTransform.sizeDelta = new Vector2(450f, 45f);
            Find<TMP_Text>(combat, "EnemyName_TMP").fontSize = 28f;
            TMP_Text enemyCardName = Find<TMP_Text>(combat, "EnemyCardName_TMP");
            enemyCardName.rectTransform.sizeDelta = new Vector2(285f, 40f);
            enemyCardName.rectTransform.anchoredPosition = new Vector2(-50f, 80f);
            enemyCardName.fontSize = 27f;
            Transform enemyCard = Find<Transform>(combat, "EnemyCard");
            Set(hudSettings, "_enemyDetails", Label(enemyCard, "Runtime Enemy Details", Vector2.zero, new Vector2(430f, 235f), 19f, font));
            enemyCardName.transform.SetAsLastSibling();
            Find<Slider>(combat, "EnemyCardHP_Slider").transform.SetAsLastSibling();
            Find<Slider>(combat, "EnemyCardHP_Slider").GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -80f);
            Set(hudSettings, "_enemyHp", Find<Slider>(combat, "EnemyHP_Slider"));
            Set(hudSettings, "_enemyCardHp", Find<Slider>(combat, "EnemyCardHP_Slider"));
            Find<Transform>(combat, "EnemyHPbar").gameObject.SetActive(true);
            Set(hudSettings, "_playerHp", Find<Slider>(combat, "PlayerHP_Slider"));
            Set(hudSettings, "_playerMemory", Find<Slider>(combat, "PlayerSP_Slider"));
            Transform playerStatus = Find<Transform>(combat, "PlayerStatusPanel");
            Set(hudSettings, "_playerVitals", Label(playerStatus, "Runtime Vitals", new Vector2(0f, -60f), new Vector2(510f, 33f), 20f, font));
            Label(Find<Transform>(combat, "PlayerSP_Label"), "Memory Label", Vector2.zero, new Vector2(48f, 25f), 15f, font).text = "MEM";
            Transform counter = Find<Transform>(combat, "MemoryCounter");
            Set(hudSettings, "_memoryCurrent", counter.Find("MemoryCurrentValue").GetComponent<TMP_Text>());
            Set(hudSettings, "_memoryCapacity", counter.Find("MemoryCapacityValue").GetComponent<TMP_Text>());
            counter.Find("MemoryCurrentValue").GetComponent<RectTransform>().sizeDelta = new Vector2(56f, 25f);
            counter.Find("MemoryCurrentValue").GetComponent<RectTransform>().anchoredPosition = new Vector2(14f, 0f);
            counter.Find("MemorySeparator").GetComponent<RectTransform>().anchoredPosition = new Vector2(47f, 0f);
            counter.Find("MemoryCapacityValue").GetComponent<RectTransform>().sizeDelta = new Vector2(56f, 25f);
            counter.Find("MemoryCapacityValue").GetComponent<RectTransform>().anchoredPosition = new Vector2(83f, 0f);
            Set(hudSettings, "_round", Label(Find<Transform>(combat, "WaveTurnControls"), "Runtime Round", new Vector2(-55f, 2f), new Vector2(285f, 48f), 22f, font));
            Transform skillPanel = Find<Transform>(combat, "SkillPanel");
            Set(hudSettings, "_skillMemory", skillPanel.Find("MemoryCurrentValue").GetComponent<TMP_Text>());
            skillPanel.Find("MemoryCurrentValue").GetComponent<RectTransform>().sizeDelta = new Vector2(65f, 26f);
            skillPanel.Find("MemoryCurrentValue").GetComponent<RectTransform>().anchoredPosition = new Vector2(132f, 143f);
            skillPanel.Find("MemoryCurrentValue").GetComponent<TMP_Text>().fontSize = 19f;

            string[] skillNames = { "SkillButton_NormalAttack", "SkillButton_QuickSlash", "SkillButton_PinkTrail" };
            var skills = new Button[3];
            var names = new TMP_Text[3];
            var costs = new TMP_Text[3];
            var images = new Image[3];
            for (int index = 0; index < skills.Length; index++)
            {
                Transform item = Find<Transform>(combat, skillNames[index]);
                skills[index] = item.GetComponent<Button>();
                names[index] = item.Find("Title").GetComponent<TMP_Text>();
                names[index].fontSize = 21f;
                costs[index] = item.Find("SPCost").GetComponent<TMP_Text>();
                costs[index].fontSize = 18f;
                costs[index].rectTransform.sizeDelta = new Vector2(84f, 34f);
                images[index] = item.GetComponent<Image>();
                item.Find("Level").GetComponent<TMP_Text>().text = "";
            }
            SetArray(hudSettings, "_skills", skills);
            SetArray(hudSettings, "_skillNames", names);
            SetArray(hudSettings, "_skillCosts", costs);
            SetArray(hudSettings, "_skillImages", images);
            Set(hudSettings, "_normalSkill", AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + "Turn_SkillButton_Default.png"));
            Set(hudSettings, "_selectedSkillSprite", AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + "Turn_SkillButton_Selected.png"));
            foreach (string detail in new[] { "SkillName", "SkillType", "Description", "PowerValue", "AccuracyValue", "AttributeValue", "RangeValue" })
            {
                string fieldName = detail == "SkillName" ? "_skillName" : detail == "SkillType" ? "_skillType"
                    : detail == "Description" ? "_skillDescription" : detail == "PowerValue" ? "_skillPower"
                    : detail == "AccuracyValue" ? "_skillAccuracy" : detail == "AttributeValue" ? "_skillElement" : "_skillTarget";
                Set(hudSettings, fieldName, Find<TMP_Text>(combat, detail + "Text"));
            }
            Find<TMP_Text>(combat, "PowerValueText").text = "";
            Find<TMP_Text>(combat, "DescriptionText").fontSize = 18f;
            Find<TMP_Text>(combat, "PowerLabelText").text = "기본 위력";
            Find<TMP_Text>(combat, "DetailPromptText").text = "Enter로 사용";
            Find<TMP_Text>(combat, "DetailKeyText").text = "";
            Transform turn = Find<Transform>(combat, "PlayerTurnUI");
            Set(hudSettings, "_execute", ActionButton(turn, "Use Selected Skill", "사용 [Enter]", new Vector2(-315f, -361f), new Vector2(190f, 28f), font));
            Set(hudSettings, "_defend", ActionButton(turn, "HUD Defend", "방어", new Vector2(-105f, -361f), new Vector2(150f, 28f), font));
            Transform investment = Find<Transform>(combat, "MemoryThrowPanel");
            Button investmentButton = investment.gameObject.AddComponent<Button>();
            investmentButton.targetGraphic = investment.GetComponent<Image>();
            investmentButton.targetGraphic.raycastTarget = true;
            Set(hudSettings, "_investment", investmentButton);
            Set(hudSettings, "_investmentCost", Label(investment, "Runtime Investment", new Vector2(0f, 60f), new Vector2(280f, 30f), 18f, font));
            TMP_Text slotCover = Label(investment, "Runtime Slot Track", new Vector2(0f, -15f), new Vector2(218f, 51f), 18f, font);

            slotCover.text = "";
            GameObject fifth = UnityEngine.Object.Instantiate(investment.Find("MemorySlot_04").gameObject, investment);
            fifth.name = "MemorySlot_05";
            var glow = new GameObject[5];
            var locks = new GameObject[5];
            for (int index = 0; index < 5; index++)
            {
                Transform slot = investment.Find("MemorySlot_0" + (index + 1));
                slot.GetComponent<RectTransform>().anchoredPosition = new Vector2(-85f + index * 42f, -15f);
                slot.localScale = Vector3.one * 0.75f;
                glow[index] = slot.Find("Glow").gameObject;
                locks[index] = slot.Find("Lock").gameObject;
                slot.Find("Star").gameObject.SetActive(true);
                slot.Find("Star").GetComponent<Image>().color = new Color(0.4f, 0.7f, 0.9f, 0.2f);
            }
            SetArray(hudSettings, "_investmentGlow", glow);
            SetArray(hudSettings, "_investmentLock", locks);

            var targets = new Button[3];
            var targetTexts = new TMP_Text[3];
            for (int index = 0; index < 3; index++)
            {
                targets[index] = ActionButton(enemyCard, "HUD Target " + (index + 1), "대상 " + (index + 1),
                    new Vector2(-140f + index * 140f, -150f), new Vector2(135f, 35f), font);
                targetTexts[index] = targets[index].GetComponentInChildren<TMP_Text>();
                targetTexts[index].fontSize = 17f;
            }
            SetArray(hudSettings, "_targets", targets);
            SetArray(hudSettings, "_targetNames", targetTexts);
            SerializedProperty slots = hudSettings.FindProperty("_turnSlots");
            slots.arraySize = 4;
            Transform turnRoot = Find<Transform>(combat, "DefaultUI");
            for (int index = 0; index < 4; index++)
            {
                GameObject slot = index < 2 ? Find<Transform>(combat, "TurnOrderSlot0" + (index + 1)).gameObject
                    : UnityEngine.Object.Instantiate(Find<Transform>(combat, "TurnOrderSlot02").gameObject, turnRoot);
                slot.name = "TurnOrderSlot0" + (index + 1);
                slot.GetComponent<RectTransform>().anchoredPosition = new Vector2(-854f, 364f - index * 80f);
                slot.transform.localScale = Vector3.one * 0.72f;
                slot.transform.Find("Portrait").gameObject.SetActive(false);
                TMP_Text actor = Label(slot.transform, "Runtime Actor", new Vector2(-7f, 7f), new Vector2(100f, 63f), 19f, font);
                slot.transform.Find("IndexText").gameObject.SetActive(false);
                SerializedProperty row = slots.GetArrayElementAtIndex(index);
                row.FindPropertyRelative("_root").objectReferenceValue = slot;
                row.FindPropertyRelative("_highlight").objectReferenceValue = slot.transform.Find("Highlight").gameObject;
                row.FindPropertyRelative("_hp").objectReferenceValue = slot.GetComponentInChildren<Slider>(true);
                row.FindPropertyRelative("_name").objectReferenceValue = actor;
            }
            foreach (Slider slider in canvas.GetComponentsInChildren<Slider>(true))
            {
                slider.interactable = false;
            }
            // 기존 조작 패널의 연결은 코어 호환용으로 유지하되 화면에는 새 HUD만 표시한다.
            Transform legacy = ((Button)settings.FindProperty("_skillButtons").GetArrayElementAtIndex(0).objectReferenceValue).transform.parent;
            legacy.gameObject.SetActive(false);
            Set(settings, "_fieldUI", field.gameObject);
            Set(settings, "_battleUI", combat.gameObject);
            MoveLegacyOutput(settings, "_notice", combat, new Vector2(0f, -515f), new Vector2(850f, 35f));
            MoveLegacyOutput(settings, "_continueButton", combat, new Vector2(0f, -400f), new Vector2(270f, 48f));
            hudSettings.ApplyModifiedPropertiesWithoutUndo();
            settings.ApplyModifiedPropertiesWithoutUndo();
            RefineCurrentScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("PROTOTYPE_UI_CONNECT_PASS " + scene.path);
        }

        // 기존 UI 프레임과 아이콘을 재사용하고 앵커·텍스트 자리만 정리한다.
        public static void RefineCurrentScene()
        {
            var battle = UnityEngine.Object.FindFirstObjectByType<TutorialBattleController>();
            var settings = new SerializedObject(battle);
            Transform field = ((GameObject)settings.FindProperty("_fieldUI").objectReferenceValue).transform;
            Transform combat = ((GameObject)settings.FindProperty("_battleUI").objectReferenceValue).transform;
            Canvas canvas = field.GetComponentInParent<Canvas>();
            foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true).Where(item => item.name.EndsWith("Backdrop")).ToArray())
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
            RemoveUnavailableControls(field, combat);
            EnsureFieldControls(field, battle);
            Find<Transform>(field, "background").gameObject.SetActive(false);
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            foreach (string group in new[] { "Default_State", "Battle_State", "DefaultUI", "PlayerTurnUI", "EnemyHPbar" })
            {
                RectTransform rect = Find<RectTransform>(canvas.transform, group);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            RectTransform overlay = Find<RectTransform>(field, "02_Decorative_HUD_Overlay");
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            ReplaceSprite(field, "06_Student_Card", "card");
            ReplaceSprite(field, "02_Floor_Banner", "floor");
            ReplaceSprite(field, "01_Location", "location");
            ReplaceSprite(field, "09_Story_Panel", "story");
            ReplaceSprite(field, "10_Minimap", "map");
            ReplaceSprite(combat, "WaveTurnControls", "round");
            ReplaceSprite(combat, "MemoryThrowPanel", "investment");
            ReplaceSprite(combat, "EnemyCard", "enemycard");
            Place(field, "01_Location", new Vector2(0, 1), 190, -88);
            Place(field, "02_Floor_Banner", new Vector2(.5f, 1), 0, -158);
            Place(field, "06_Student_Card", new Vector2(1, 1), -214, -248);
            Place(field, "09_Story_Panel", Vector2.zero, 230, 535, 400, 180);
            Place(field, "10_Minimap", Vector2.zero, 175, 218, 300, 356);
            Place(field, "Runtime Character", new Vector2(.5f, .5f), -65, -2, 190, 36);
            Find<TMP_Text>(field, "Runtime Character").color = new Color(.04f, .09f, .2f);
            Place(field, "Runtime Field HP", new Vector2(.5f, .5f), -65, -52, 166, 8);
            Slider hp = Find<Slider>(field, "Runtime Field HP");
            hp.fillRect.SetParent(hp.transform, false);
            hp.fillRect.gameObject.SetActive(true);
            foreach (Transform child in hp.GetComponentsInChildren<Transform>(true).Where(t => t != hp.transform && t != hp.fillRect).ToArray())
            {
                if (!hp.fillRect.IsChildOf(child) && !child.IsChildOf(hp.fillRect))
                {
                    child.gameObject.SetActive(false);
                }
            }
            hp.fillRect.anchorMin = Vector2.zero;
            hp.fillRect.anchorMax = Vector2.one;
            hp.fillRect.offsetMin = hp.fillRect.offsetMax = Vector2.zero;
            foreach (Image fill in hp.fillRect.GetComponentsInChildren<Image>(true))
            {
                fill.gameObject.SetActive(true);
                fill.rectTransform.anchorMin = Vector2.zero;
                fill.rectTransform.anchorMax = Vector2.one;
                fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            }
            Place(field, "Runtime Field Vitals", new Vector2(.5f, .5f), -8, -104, 354, 28);
            Place(field, "Runtime Location", new Vector2(.5f, .5f), 25, 0, 230, 58);
            Place(field, "Runtime Floor", new Vector2(.5f, .5f), -260, 0, 120, 64);
            Find<TMP_Text>(field, "Runtime Floor").fontSize = 40;
            if (Find<Transform>(field, "02_Floor_Banner").Find("Banner Area") == null)
            {
                Label(Find<Transform>(field, "02_Floor_Banner"), "Banner Area", new Vector2(72, 0), new Vector2(254, 58), 27,
                    Find<TMP_Text>(field, "Runtime Floor").font).text = "튜토리얼 구역";
            }
            Place(field, "Runtime Tutorial", new Vector2(.5f, .5f), 12, -4, 342, 64);
            Find<TMP_Text>(field, "Runtime Tutorial").fontSize = 21;
            Place(field, "Runtime Minimap", new Vector2(.5f, .5f), 0, 17, 265, 265);
            RawImage map = Find<RawImage>(field, "Runtime Minimap");
            const string materialPath = "Assets/CK_Semester_Project/Prototype/Data/TutorialMinimap.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("UI/TutorialCircularMinimap"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            map.material = material;
            Place(combat, "CombatHeader", new Vector2(0, 1), 255, -52);
            Place(combat, "EnemyNameBanner", new Vector2(.5f, 1), 0, -76);
            Place(combat, "WaveTurnControls", new Vector2(1, 1), -226, -58);
            Place(combat, "Runtime Round", new Vector2(.5f, .5f), -63, 10, 266, 30);
            Find<TMP_Text>(combat, "Runtime Round").fontSize = 19;
            Place(combat, "EnemyHP_Track", new Vector2(.5f, 1), 0, -153);
            Place(combat, "EnemyHP_Slider", new Vector2(.5f, 1), 0, -153);
            Place(combat, "EnemyStatusIcon", new Vector2(.5f, 1), -150, -153);
            Place(combat, "PlayerStatusPanel", Vector2.zero, 568, 135);
            Place(combat, "MemoryCounter", new Vector2(.5f, .5f), 135, 52, 250, 44);
            Transform counter = Find<Transform>(combat, "MemoryCounter");
            Place(counter, "MemoryCurrentValue", new Vector2(.5f, .5f), 40, 0, 40, 25);
            Place(counter, "MemorySeparator", new Vector2(.5f, .5f), 69, 0, 12, 25);
            Place(counter, "MemoryCapacityValue", new Vector2(.5f, .5f), 99, 0, 44, 25);
            foreach (TMP_Text value in counter.GetComponentsInChildren<TMP_Text>(true))
            {
                value.fontSize = 17;
            }
            Place(combat, "Runtime Vitals", new Vector2(.5f, .5f), 0, -64, 530, 30);
            for (int index = 0; index < 4; index++)
            {
                string name = "TurnOrderSlot0" + (index + 1);
                TMP_Text[] actors = Find<Transform>(combat, name).GetComponentsInChildren<TMP_Text>(true)
                    .Where(text => text.name == "Runtime Actor").ToArray();
                foreach (TMP_Text duplicate in actors.Take(actors.Length - 1))
                {
                    UnityEngine.Object.DestroyImmediate(duplicate.gameObject);
                }
                Place(combat, name, new Vector2(0, 1), 94, -140 - index * 78);
                Find<Transform>(combat, name).localScale = Vector3.one * .65f;
                Transform portrait = Find<Transform>(combat, name).Find("Portrait");
                portrait.gameObject.SetActive(true);
                if (index == 0)
                {
                    portrait.GetComponent<Image>().sprite = Find<Image>(combat, "PlayerPortrait").sprite;
                }
                Place(Find<Transform>(combat, name), "Runtime Actor", new Vector2(.5f, .5f), 104, 2, 125, 60);
                Find<TMP_Text>(Find<Transform>(combat, name), "Runtime Actor").fontSize = 20;
            }
            Place(combat, "EnemyCard", new Vector2(1, 1), -268, -285);
            Place(combat, "Runtime Enemy Details", new Vector2(.5f, .5f), -101, -13, 252, 146);
            Find<TMP_Text>(combat, "Runtime Enemy Details").alignment = TextAlignmentOptions.MidlineLeft;
            Find<TMP_Text>(combat, "Runtime Enemy Details").fontSize = 17;
            Place(combat, "EnemyCardName_TMP", new Vector2(.5f, .5f), -96, 84, 270, 36);
            Place(combat, "EnemyCardHP_Slider", new Vector2(.5f, .5f), 0, -109, 388, 12);
            Place(combat, "EnemyCardHP_Track", new Vector2(.5f, .5f), 0, -109, 400, 18);
            Place(combat, "SkillPanel", Vector2.zero, 254, 475);
            Place(Find<Transform>(combat, "SkillPanel"), "MemoryCurrentValue", new Vector2(.5f, .5f), 155, 143, 38, 26);
            Find<TMP_Text>(Find<Transform>(combat, "SkillPanel"), "MemoryCurrentValue").fontSize = 17;
            Place(combat, "SkillDescriptionPanel", Vector2.zero, 676, 464);
            Find<TMP_Text>(combat, "PowerLabelText").text = "위력";
            Find<TMP_Text>(combat, "DetailKeyText").text = "ENT";
            Find<TMP_Text>(combat, "DetailKeyText").fontSize = 8;
            Find<TMP_Text>(combat, "DetailKeyText").color = new Color(.03f, .1f, .25f);
            Find<TMP_Text>(combat, "DetailPromptText").text = "Enter로 사용";
            Place(combat, "MemoryThrowPanel", Vector2.zero, 656, 686, 300, 116);
            Place(combat, "Runtime Investment", new Vector2(.5f, .5f), -8, 25, 230, 26);
            Find<TMP_Text>(combat, "Runtime Investment").fontSize = 17;
            for (int index = 0; index < 5; index++)
            {
                Place(Find<Transform>(combat, "MemoryThrowPanel"), "MemorySlot_0" + (index + 1), new Vector2(.5f, .5f), -104 + index * 52, -18);
            }
            Place(combat, "Use Selected Skill", Vector2.zero, 603, 286, 198, 36);
            Place(combat, "HUD Defend", Vector2.zero, 780, 286, 140, 36);
            foreach (string name in new[] { "Use Selected Skill", "HUD Defend", "HUD Target 1", "HUD Target 2", "HUD Target 3", "Return To Exploration" })
            {
                Transform root = combat;
                Image image = Find<Image>(root, name);
                image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + "Turn_SkillButton_Default.png");
                image.color = Color.white;
                foreach (TMP_Text text in image.GetComponentsInChildren<TMP_Text>(true))
                {
                    text.fontSize = 19;
                    text.enableAutoSizing = true;
                    text.fontSizeMin = 15;
                    text.fontSizeMax = 19;
                }
            }
            foreach (Transform child in Find<Transform>(combat, "SkillPanel").GetComponentsInChildren<Transform>(true).Where(t => t.name == "SPCost"))
            {
                RectTransform rect = (RectTransform)child;
                rect.anchoredPosition = new Vector2(130, -1);
                rect.sizeDelta = new Vector2(94, 32);
                child.GetComponent<TMP_Text>().fontSize = 18;
            }
            Find<Image>(combat, "PlayerSP_Label").enabled = false;
            Find<Transform>(combat, "Runtime Slot Track").gameObject.SetActive(false);
            Find<RectTransform>(combat, "Encounter Notice").anchorMin = Find<RectTransform>(combat, "Encounter Notice").anchorMax = new Vector2(.5f, 0);
            Find<RectTransform>(combat, "Encounter Notice").anchoredPosition = new Vector2(0, 28);
            foreach (Graphic graphic in canvas.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }
            foreach (TMP_Text text in canvas.GetComponentsInChildren<TMP_Text>(true))
            {
                text.fontStyle = FontStyles.Bold;
            }
            foreach (Button button in canvas.GetComponentsInChildren<Button>(true).Where(b => b.interactable && b.targetGraphic != null))
            {
                button.targetGraphic.raycastTarget = true;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        private static void EnsureFieldControls(Transform field, TutorialBattleController battle)
        {
            string[] names = { "11_Interact_LMB", "12_Sprint_Shift" };
            if (names.Any(name => !field.GetComponentsInChildren<Transform>(true).Any(item => item.name == name)))
            {
                Scene source = EditorSceneManager.OpenScene(UiScene, OpenSceneMode.Additive);
                Transform[] original = source.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
                for (int index = 0; index < names.Length; index++)
                {
                    if (!field.GetComponentsInChildren<Transform>(true).Any(item => item.name == names[index]))
                    {
                        string sourceName = index == 0 ? "11_Interact_E" : names[index];
                        GameObject item = UnityEngine.Object.Instantiate(original.Single(child => child.name == sourceName).gameObject, field);
                        item.name = names[index];
                    }
                }
                EditorSceneManager.CloseScene(source, true);
            }
            Place(field, names[0], new Vector2(1, 0), -92, 252, 108, 116);
            Place(field, names[1], new Vector2(1, 0), -140, 112, 160, 160);
            Image attackImage = Find<Image>(field, names[0]);
            attackImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/CK_Semester_Project/Prototype/Temp/Graphics/UI/10-01/Refined/ui_field_attack_lmb_btn.png");
            if (attackImage.sprite == null)
            {
                throw new InvalidOperationException("좌클릭 UI 에셋 누락");
            }
            var settings = new SerializedObject(battle);
            var hudSettings = new SerializedObject(battle.GetComponent<PrototypeHudPresenter>());
            PlayerMovement movement = (PlayerMovement)settings.FindProperty("_movement").objectReferenceValue;
            Set(hudSettings, "_movement", movement);
            Set(hudSettings, "_sprint", Find<Button>(field, names[1]));
            Set(hudSettings, "_fieldAttack", Find<Button>(field, names[0]));
            foreach (string name in names)
            {
                Button button = Find<Button>(field, name);
                button.interactable = true;
                button.targetGraphic = button.GetComponent<Image>();
                button.onClick.RemoveAllListeners();
            }
            Transform banner = Find<Transform>(field, "02_Floor_Banner");
            CanvasGroup group = banner.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = banner.gameObject.AddComponent<CanvasGroup>();
            }
            banner.gameObject.SetActive(true);
            group.alpha = 1f;
            group.interactable = false;
            group.blocksRaycasts = false;
            Set(hudSettings, "_areaBanner", group);
            hudSettings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RemoveUnavailableControls(Transform field, Transform combat)
        {
            // 원본 에셋은 보존하고 데모에 기능이 없는 조작 오브젝트만 제거한다.
            string[] unavailable = { "Commands", "Travel First Floor", "Travel Second Floor",
                "SkillButton_LockedBoundary", "SkillDetailsPrompt" };
            foreach (Transform root in new[] { field, combat })
            {
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true)
                    .Where(item => unavailable.Contains(item.name)).ToArray())
                {
                    UnityEngine.Object.DestroyImmediate(item.gameObject);
                }
            }
            Button minimapButton = Find<Transform>(field, "10_Minimap").GetComponent<Button>();
            if (minimapButton != null)
            {
                UnityEngine.Object.DestroyImmediate(minimapButton);
            }
        }

        private static void ReplaceSprite(Transform parent, string name, string asset)
        {
            Image image = Find<Image>(parent, name);
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/CK_Semester_Project/Prototype/Temp/Graphics/UI/10-01/Refined/ui_" + asset + "_blank.png");
            if (image.sprite == null)
            {
                throw new InvalidOperationException("UI 편집 에셋 누락: " + asset);
            }
        }

        private static void Place(Transform parent, string name, Vector2 anchor, float x, float y, float width = 0, float height = 0)
        {
            RectTransform rect = Find<RectTransform>(parent, name);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, y);
            if (width > 0)
            {
                rect.sizeDelta = new Vector2(width, height);
            }
        }

        private static void MoveLegacyOutput(SerializedObject settings, string field, Transform parent, Vector2 position, Vector2 size)
        {
            Component item = (Component)settings.FindProperty(field).objectReferenceValue;
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static Slider CreateHp(Transform parent, Slider template, Vector2 position, Vector2 size)
        {
            Slider slider = UnityEngine.Object.Instantiate(template, parent);
            slider.name = "Runtime Field HP";
            RectTransform rect = slider.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            slider.fillRect.anchorMin = Vector2.zero;
            slider.fillRect.anchorMax = Vector2.one;
            slider.fillRect.sizeDelta = Vector2.zero;
            return slider;
        }

        private static Button ActionButton(Transform parent, string name, string text, Vector2 position, Vector2 size, TMP_FontAsset font)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = item.GetComponent<Image>();
            image.color = new Color(0.04f, 0.15f, 0.23f, 0.95f);
            Button button = item.GetComponent<Button>();
            button.targetGraphic = image;
            Label(item.transform, "Label", Vector2.zero, size, 22f, font).text = text;
            return button;
        }

        private static TMP_Text Label(Transform parent, string name, Vector2 position, Vector2 size, float fontSize, TMP_FontAsset font)
        {

            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            item.transform.SetParent(parent, false);
            TMP_Text label = item.GetComponent<TMP_Text>();
            label.rectTransform.anchoredPosition = position;
            label.rectTransform.sizeDelta = size;
            label.font = font;
            label.fontSize = fontSize;
            label.color = new Color(0.85f, 0.96f, 1f);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        private static T Find<T>(Transform parent, string name) where T : Component
        {
            Transform item = parent.GetComponentsInChildren<Transform>(true).Single(child => child.name == name);
            T component = item.GetComponent<T>();
            if (component == null)
            {
                throw new InvalidOperationException(name + ": 필요한 " + typeof(T).Name + "가 없습니다.");
            }
            return component;
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

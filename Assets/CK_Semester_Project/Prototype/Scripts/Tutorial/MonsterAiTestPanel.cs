using System.Linq;
using CK.SemesterProject.Battle;
using Semester.Enemies;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CK.SemesterProject.Tutorial
{
    // 테스트 씬 전용 조합 선택. 실제 계산·AI·연출은 TutorialBattleController를 사용한다.
    public sealed class MonsterAiTestPanel : MonoBehaviour
    {
        [SerializeField, Tooltip("전투 진행 컴포넌트")]
        private TutorialBattleController _controller;
        [SerializeField, Tooltip("각인 A/B, 잔상 A/B, 망각 A/B, 각인 C, 잔상 C, 망각 C 순서")]
        private EnemyStateMachine[] _monsters;
        [SerializeField, Tooltip("각인·잔상·망각 순서의 Lv.4 정의. 일반 1기 전투에 고정 적용")]
        private MonsterBattleDefinition[] _levelFourDefinitions;
        [SerializeField, Tooltip("MO09 보스 정의. 테스트 박스는 첫 망각 몬스터 자리를 재사용합니다")]
        private MonsterBattleDefinition _bossDefinition;
        [SerializeField, Tooltip("기존 1~2기 조합 9개, 3기 조합 10개")]
        private Button[] _presets;
        [SerializeField, Tooltip("일반, 플레이어 선제, 몬스터 선제 선택 버튼")]
        private Button[] _entryButtons;
        [SerializeField, Tooltip("HP·메모리·적을 초기화하는 버튼. 누적 승리는 유지합니다")]
        private Button _reset;
        [SerializeField, Tooltip("기본 스킬 테스트 단계: 1→2→3→1. 실제 승리 횟수는 변경하지 않습니다")]
        private Button _enhanceSkill;
        [SerializeField, Tooltip("필드에서 표시하는 테스트 선택 패널")]
        private GameObject _selection;
        [SerializeField, Tooltip("선택한 진입 방식 표시")]
        private TMP_Text _entryLabel;
        [SerializeField, Tooltip("테스트 씬에서는 필드 마우스를 버튼 선택에 사용")]
        private CameraController _fieldCamera;
        private readonly int[][] _rosters =
        {
            new[] { 0 }, new[] { 2 }, new[] { 4 },
            new[] { 0, 1 }, new[] { 2, 3 }, new[] { 4, 5 },
            new[] { 0, 2 }, new[] { 2, 4 }, new[] { 4, 0 },
            new[] { 0, 2, 4 }, new[] { 0, 1, 2 }, new[] { 0, 1, 4 },
            new[] { 2, 3, 0 }, new[] { 2, 3, 4 }, new[] { 4, 5, 2 }, new[] { 4, 5, 0 },
            new[] { 0, 1, 6 }, new[] { 2, 3, 7 }, new[] { 4, 5, 8 }
        };
        private Pose[] _homes;
        private UnityAction[] _presetActions;
        private UnityAction[] _entryActions;
        private BattleEntryCondition _entry;
        private TMP_Text _enhanceSkillLabel;
        private int _displayedSkillStage;
        private MonsterBattleDefinition[] _definitions;
        private TutorialMonster[] _monsterViews;
        private readonly int[] _levels = { 1, 1, 1 };
        private readonly UnityEngine.UI.Button[] _levelButtons = new UnityEngine.UI.Button[3];
        private readonly TMP_Text[] _levelLabels = new TMP_Text[3];
        private UnityEngine.UI.Button _startBattle;
        private TMP_Text _groupLabel;
        private GameObject _levelSelection;
        private int _selectedPreset;
        private bool _isBossSelected;
        private static readonly int[][] LevelIndices =
        {
            new[] { 0, 1, 6 }, new[] { 2, 3, 7 }, new[] { 4, 5, 8 }
        };

        private void Start()
        {
            if (_controller == null || _monsters == null || _monsters.Length != 9
                || _presets == null || _presets.Length != _rosters.Length || _entryButtons == null || _entryButtons.Length != 3
                || _reset == null || _enhanceSkill == null || _selection == null || _entryLabel == null || _fieldCamera == null
                || _levelFourDefinitions == null || _levelFourDefinitions.Length != 3 || _levelFourDefinitions.Any(definition => definition == null)
                || _bossDefinition == null)
            {
                Debug.LogError("MonsterAiTestPanel: 테스트 씬 참조가 누락되었습니다.", this);
                enabled = false;
                return;
            }
            _homes = new Pose[_monsters.Length];
            _definitions = new MonsterBattleDefinition[_monsters.Length];
            _monsterViews = new TutorialMonster[_monsters.Length];
            for (int i = 0; i < _monsters.Length; i++)
            {
                _homes[i] = new Pose(_monsters[i].transform.position, _monsters[i].transform.rotation);
                _monsterViews[i] = _monsters[i].GetComponent<TutorialMonster>();
                if (_monsterViews[i] == null || _monsterViews[i].Definition == null)
                {
                    Debug.LogError("MonsterAiTestPanel: 몬스터 정의가 누락되었습니다.", this);
                    enabled = false;
                    return;
                }
                _definitions[i] = _monsterViews[i].Definition;
            }
            _fieldCamera.enabled = false;
            _presetActions = new UnityAction[_rosters.Length];
            for (int i = 0; i < _presets.Length; i++)
            {
                int index = i;
                _presetActions[i] = () => SelectPreset(index);
                _presets[i].onClick.AddListener(_presetActions[i]);
                TMP_Text presetLabel = _presets[i].GetComponentInChildren<TMP_Text>(true);
                if (presetLabel != null)
                {
                    presetLabel.text = _rosters[i].Length + "기 " + string.Join(" · ", _rosters[i]
                        .GroupBy(member => _definitions[member].Element)
                        .Select(group => TutorialMonster.GetElementName(group.Key) + (group.Count() > 1 ? "×" + group.Count() : "")));
                }
            }
            _entryActions = new UnityAction[3];
            for (int i = 0; i < _entryButtons.Length; i++)
            {
                int index = i;
                _entryActions[i] = () => SelectEntry(index);
                _entryButtons[i].onClick.AddListener(_entryActions[i]);
            }
            _reset.onClick.AddListener(ResetTest);
            _enhanceSkillLabel = _enhanceSkill.GetComponentInChildren<TMP_Text>(true);
            _enhanceSkill.onClick.AddListener(EnhanceTestSkill);
            RefreshEnhanceButton();
            BattleHistoryPanel.Create(_controller, _entryLabel, _reset);
            foreach (TMP_Text label in _selection.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.name == "AI Test Help")
                {
                    label.text = "진입 방식 선택 → 몬스터 그룹 선택 → 개별 레벨 지정 → 전투 시작";
                }
            }
            CreateLevelSelection();
            CreateBossButton();
            SelectEntry(0);
            ResetTest();
        }

        private void CreateLevelSelection()
        {
            var panelObject = new GameObject("Monster Level Selection", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            _levelSelection = panelObject;
            panelObject.layer = 5;
            panelObject.transform.SetParent(_selection.transform, false);
            var backdrop = panelObject.GetComponent<RectTransform>();
            backdrop.anchorMin = Vector2.zero;
            backdrop.anchorMax = Vector2.one;
            backdrop.offsetMin = backdrop.offsetMax = Vector2.zero;
            panelObject.GetComponent<UnityEngine.UI.Image>().color = new Color(0.04f, 0.05f, 0.07f, 0.97f);
            var content = new GameObject("Level Selection Content", typeof(RectTransform));
            content.layer = 5;
            content.transform.SetParent(backdrop, false);
            var panel = content.GetComponent<RectTransform>();
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(700, 350);
            _groupLabel = Instantiate(_entryLabel, panel);
            _groupLabel.name = "Selected Group";
            _groupLabel.rectTransform.anchorMin = _groupLabel.rectTransform.anchorMax = new Vector2(0.5f, 1);
            _groupLabel.rectTransform.anchoredPosition = new Vector2(0, -30);
            _groupLabel.rectTransform.sizeDelta = new Vector2(610, 45);
            _groupLabel.fontSize = 20;
            _groupLabel.raycastTarget = false;
            for (int i = 0; i < 3; i++)
            {
                int slot = i;
                _levelButtons[i] = CreateSelectionButton(panel, "Monster Level " + (i + 1),
                    new Vector2(-210 + i * 210, -105), new Vector2(200, 75));
                _levelLabels[i] = _levelButtons[i].GetComponentInChildren<TMP_Text>(true);
                _levelLabels[i].fontSize = 18;
                _levelButtons[i].onClick.AddListener(() =>
                {
                    if (_controller.IsInBattle)
                    {
                        return;
                    }
                    _levels[slot] = _levels[slot] % 4 + 1;
                    RefreshLevelSelection();
                });
            }
            _startBattle = CreateSelectionButton(panel, "Start Selected Battle", new Vector2(0, -205), new Vector2(420, 60));
            _startBattle.GetComponentInChildren<TMP_Text>(true).text = "선택한 그룹으로 전투 시작";
            _startBattle.onClick.AddListener(() =>
            {
                if (_isBossSelected)
                {
                    StartBoss();
                }
                else
                {
                    StartPreset(_selectedPreset);
                }
            });
            UnityEngine.UI.Button back = CreateSelectionButton(panel, "Back To Groups", new Vector2(0, -280), new Vector2(420, 50));
            back.GetComponentInChildren<TMP_Text>(true).text = "그룹 다시 선택";
            back.onClick.AddListener(() => _levelSelection.SetActive(false));
            SelectPreset(0);
            _levelSelection.SetActive(false);
        }

        private UnityEngine.UI.Button CreateSelectionButton(Transform parent, string objectName, Vector2 position, Vector2 size)
        {
            UnityEngine.UI.Button button = Instantiate(_reset, parent);
            button.name = objectName;
            button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(5, 5);
            label.rectTransform.offsetMax = new Vector2(-5, -5);
            return button;
        }

        private void SelectPreset(int index)
        {
            if (_controller.IsInBattle || index < 0 || index >= _rosters.Length)
            {
                return;
            }
            _selectedPreset = index;
            _isBossSelected = false;
            RefreshLevelSelection();
            _levelSelection.SetActive(true);
        }

        private int GetLevelDefinitionIndex(int monsterIndex, int level)
        {
            BattleElement element = _definitions[monsterIndex].Element;
            int group = element == BattleElement.Imprint ? 0 : element == BattleElement.Afterimage ? 1 : 2;
            return LevelIndices[group][level - 1];
        }

        private MonsterBattleDefinition GetLevelDefinition(int monsterIndex, int level)
        {
            BattleElement element = _definitions[monsterIndex].Element;
            int group = element == BattleElement.Imprint ? 0 : element == BattleElement.Afterimage ? 1 : 2;
            return level == 4 ? _levelFourDefinitions[group] : _definitions[GetLevelDefinitionIndex(monsterIndex, level)];
        }

        private void CreateBossButton()
        {
            var button = Instantiate(_presets[_presets.Length - 1], _presets[_presets.Length - 1].transform.parent);
            button.name = "Boss Test";
            button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            button.GetComponent<RectTransform>().anchoredPosition += new Vector2(280, 0);
            button.GetComponentInChildren<TMP_Text>(true).text = "보스 MO09";
            button.onClick.AddListener(() =>
            {
                if (_controller.IsInBattle)
                {
                    return;
                }
                _isBossSelected = true;
                CombatantData boss = _bossDefinition.CreateData();
                _groupLabel.text = "보스 " + boss.Id + " · HP " + boss.MaxHp + " / 메모리 " + boss.InitialMemory
                    + " / 스킬 " + boss.Skills.Count + "종";
                foreach (UnityEngine.UI.Button levelButton in _levelButtons)
                {
                    levelButton.gameObject.SetActive(false);
                }
                _levelSelection.SetActive(true);
            });
        }

        private void StartBoss()
        {
            if (_controller.IsInBattle)
            {
                return;
            }
            ResetTest();
            const int BossSlot = 4;
            _monsterViews[BossSlot].SetDefinition(_bossDefinition);
            var members = new[] { _monsters[BossSlot] };
            if (!_controller.BeginBattle(members[0], _entry, members))
            {
                _entryLabel.text = "보스 전투 진입 실패: Console과 전투 자리를 확인하세요.";
            }
        }

        private void RefreshLevelSelection()
        {
            int[] roster = _rosters[_selectedPreset];
            _groupLabel.text = "그룹 " + (_selectedPreset + 1) + " · " + roster.Length + "기 / "
                + (roster.Length == 1 ? "1기 전투는 Lv.4 고정" : "각 몬스터 레벨 선택");
            for (int i = 0; i < 3; i++)
            {
                _levelButtons[i].gameObject.SetActive(i < roster.Length);
                if (i < roster.Length)
                {
                    int level = roster.Length == 1 ? 4 : _levels[i];
                    _levelButtons[i].interactable = roster.Length != 1;
                    _levelLabels[i].text = (i + 1) + ". " + TutorialMonster.GetElementName(_definitions[roster[i]].Element)
                        + " Lv." + level + (roster.Length == 1 ? "\n1기 전투 고정" : "\n클릭하여 변경");
                }
            }
        }

        private void Update()
        {
            bool inBattle = _controller.IsInBattle;
            _selection.SetActive(!inBattle);
            _enhanceSkill.interactable = !inBattle && _controller.enabled;
            if (_displayedSkillStage != _controller.TestSkillStage)
            {
                RefreshEnhanceButton();
            }
            if (!inBattle)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        public void SelectEntry(int index)
        {
            _entry = index == 1 ? BattleEntryCondition.PlayerInitiated
                : index == 2 ? BattleEntryCondition.MonsterCollision : BattleEntryCondition.Normal;
            _entryLabel.text = "진입: " + (index == 1 ? "플레이어 선제 +1행동" : index == 2 ? "몬스터 선제 +1행동 / 메모리 +1" : "일반 (메모리 순서)");
        }

        private void EnhanceTestSkill()
        {
            _controller.CycleTestSkillStage();
            RefreshEnhanceButton();
        }

        private void RefreshEnhanceButton()
        {
            _displayedSkillStage = _controller.TestSkillStage;
            if (_enhanceSkillLabel != null)
            {
                _enhanceSkillLabel.text = _displayedSkillStage == 3 ? "강화 초기화 (3 → 1단계)"
                    : "스킬 강화 (" + _displayedSkillStage + " → " + (_displayedSkillStage + 1) + "단계)";
            }
        }

        public void StartPreset(int index)
        {
            if (_controller.IsInBattle || index < 0 || index >= _rosters.Length)
            {
                return;
            }
            ResetTest();
            int[] roster = _rosters[index];
            var members = new EnemyStateMachine[roster.Length];
            for (int i = 0; i < roster.Length; i++)
            {
                members[i] = _monsters[roster[i]];
                _monsterViews[roster[i]].SetDefinition(GetLevelDefinition(roster[i], roster.Length == 1 ? 4 : _levels[i]));
            }
            if (!_controller.BeginBattle(members[0], _entry, members))
            {
                _entryLabel.text = "전투 진입 실패: Console과 전투 자리 설정을 확인하세요.";
            }
        }

        public void ResetTest()
        {
            _controller.CancelBattle();
            if (_levelSelection != null)
            {
                _levelSelection.SetActive(false);
            }
            PlayerSessionState.Current.RestoreFull();
            for (int i = 0; i < _monsters.Length; i++)
            {
                EnemyStateMachine monster = _monsters[i];
                _monsterViews[i].SetDefinition(_definitions[i]);
                NavMeshAgent agent = monster.GetComponent<NavMeshAgent>();
                agent.enabled = false;
                monster.transform.SetPositionAndRotation(_homes[i].position, _homes[i].rotation);
                monster.gameObject.SetActive(true);
                monster.enabled = true;
                agent.enabled = true;
            }
            Physics.SyncTransforms();
            _selection.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnDestroy()
        {
            if (_presetActions == null)
            {
                return;
            }
            for (int i = 0; i < _presetActions.Length; i++)
            {
                if (_presets[i] != null)
                {
                    _presets[i].onClick.RemoveListener(_presetActions[i]);
                }
            }
            for (int i = 0; i < _entryActions.Length; i++)
            {
                if (_entryButtons[i] != null)
                {
                    _entryButtons[i].onClick.RemoveListener(_entryActions[i]);
                }
            }
            if (_reset != null)
            {
                _reset.onClick.RemoveListener(ResetTest);
            }
            if (_enhanceSkill != null)
            {
                _enhanceSkill.onClick.RemoveListener(EnhanceTestSkill);
            }
        }
    }
}

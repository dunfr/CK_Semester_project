using System;
using System.Linq;
using CK.SemesterProject.Battle;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CK.SemesterProject.Tutorial
{
    // 표현·선택만 담당한다. 피해/비용/행동 가능 여부는 기존 전투 세션을 사용한다.
    public sealed class PrototypeHudPresenter : MonoBehaviour
    {
        [Serializable]
        private sealed class TurnSlot
        {
            [SerializeField] private GameObject _root;
            [SerializeField] private GameObject _highlight;
            [SerializeField] private Slider _hp;
            [SerializeField] private TMP_Text _name;

            public GameObject Root => _root;
            public GameObject Highlight => _highlight;
            public Slider Hp => _hp;
            public TMP_Text Name => _name;
        }

        [SerializeField] private TutorialBattleController _battle;
        [SerializeField] private FloorTravelButtons _floorTravel;
        [SerializeField] private GameObject _playerTurn;
        [SerializeField] private TMP_Text _fieldName;
        [SerializeField] private TMP_Text _fieldLocation;
        [SerializeField] private TMP_Text _floorBanner;
        [SerializeField] private TMP_Text _fieldMemory;
        [SerializeField] private Slider _fieldHp;
        [SerializeField] private TMP_Text _enemyName;
        [SerializeField] private TMP_Text _enemyCardName;
        [SerializeField] private TMP_Text _enemyDetails;
        [SerializeField] private Slider _enemyHp;
        [SerializeField] private Slider _enemyCardHp;
        [SerializeField] private Slider _playerHp;
        [SerializeField] private Slider _playerMemory;
        [SerializeField] private TMP_Text _playerVitals;
        [SerializeField] private TMP_Text _memoryCurrent;
        [SerializeField] private TMP_Text _memoryCapacity;
        [SerializeField] private TMP_Text _skillMemory;
        [SerializeField] private TMP_Text _round;
        [SerializeField] private TMP_Text _investmentCost;
        [SerializeField] private TMP_Text _skillName;
        [SerializeField] private TMP_Text _skillType;
        [SerializeField] private TMP_Text _skillDescription;
        [SerializeField] private TMP_Text _skillPower;
        [SerializeField] private TMP_Text _skillAccuracy;
        [SerializeField] private TMP_Text _skillElement;
        [SerializeField] private TMP_Text _skillTarget;
        [SerializeField] private Button[] _skills;
        [SerializeField] private TMP_Text[] _skillNames;
        [SerializeField] private TMP_Text[] _skillCosts;
        [SerializeField] private Image[] _skillImages;
        [SerializeField] private Sprite _normalSkill;
        [SerializeField] private Sprite _selectedSkillSprite;
        [SerializeField] private Transform _player;
        [SerializeField] private GameObject _fieldRoot;
        [SerializeField] private RawImage _minimap;
        [SerializeField] private Button _floorOne;
        [SerializeField] private Button _floorTwo;
        [SerializeField] private Button _execute;
        [SerializeField] private Button _investment;
        [SerializeField] private Button _defend;
        [SerializeField] private Button[] _targets;
        [SerializeField] private TMP_Text[] _targetNames;
        [SerializeField] private GameObject[] _investmentGlow;
        [SerializeField] private GameObject[] _investmentLock;
        [SerializeField] private TurnSlot[] _turnSlots;

        private UnityAction[] _selectActions;
        private UnityAction[] _targetActions;
        private BattleSnapshot _previousSnapshot;
        private int _selectedSkill;
        private int _previousStage = -1;
        private int _previousFloor = -1;
        private bool _couldChoose;
        private string _previousTarget;
        private Camera _mapCamera;
        private RenderTexture _mapTexture;
        private float _nextMapFrame;

        public int SelectedSkill => _selectedSkill;

        private void Awake()
        {
            _selectActions = new UnityAction[_skills.Length];
            for (int index = 0; index < _skills.Length; index++)
            {
                int skillIndex = index;
                _selectActions[index] = () => SelectSkill(skillIndex);
                _skills[index].onClick.AddListener(_selectActions[index]);
            }
            _targetActions = new UnityAction[_targets.Length];
            for (int index = 0; index < _targets.Length; index++)
            {
                int targetIndex = index;
                _targetActions[index] = () => _battle.SelectTarget(targetIndex);
                _targets[index].onClick.AddListener(_targetActions[index]);
            }
            _execute.onClick.AddListener(ExecuteSelectedSkill);
            _investment.onClick.AddListener(_battle.CycleInvestment);
            _defend.onClick.AddListener(_battle.Defend);
            if (_floorTravel != null)
            {
                _floorOne.onClick.AddListener(_floorTravel.TravelToFirstFloor);
                _floorTwo.onClick.AddListener(_floorTravel.TravelToSecondFloor);
            }
            _floorOne.gameObject.SetActive(_floorTravel != null);
            _floorTwo.gameObject.SetActive(_floorTravel != null);
            _mapTexture = new RenderTexture(256, 256, 16);
            var mapCamera = new GameObject("Field Minimap Camera", typeof(Camera));
            _mapCamera = mapCamera.GetComponent<Camera>();
            _mapCamera.enabled = false;
            _mapCamera.allowHDR = false;
            _mapCamera.allowMSAA = false;
            _mapCamera.orthographic = true;
            _mapCamera.orthographicSize = 9f;
            _mapCamera.nearClipPlane = 0.1f;
            _mapCamera.farClipPlane = 25f;
            _mapCamera.clearFlags = CameraClearFlags.SolidColor;
            _mapCamera.backgroundColor = new Color(0.03f, 0.08f, 0.14f);
            _mapCamera.targetTexture = _mapTexture;
            _mapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _minimap.texture = _mapTexture;
        }

        private void OnEnable()
        {
            PlayerSessionState.Current.Changed += RefreshField;
            RefreshField();
        }

        private void Update()
        {
            if (_battle.CanChooseAction && Application.isFocused && Keyboard.current != null)
            {
                if (Keyboard.current.fKey.wasPressedThisFrame)
                {
                    _battle.CycleInvestment();
                }
                if (Keyboard.current.enterKey.wasPressedThisFrame)
                {
                    ExecuteSelectedSkill();
                }
            }
        }

        private void LateUpdate()
        {
            bool renderMap = _fieldRoot.activeInHierarchy && Time.unscaledTime >= _nextMapFrame;
            _mapCamera.enabled = renderMap;
            if (renderMap)
            {
                _mapCamera.transform.position = _player.position + Vector3.up * 15f;
                _nextMapFrame = Time.unscaledTime + 0.1f;
            }
            int floor = _floorTravel == null ? 1 : _floorTravel.CurrentFloor;
            if (floor != _previousFloor || _fieldName.text.Length == 0)
            {
                _previousFloor = floor;
                RefreshField();
            }
            BattleSnapshot state = _battle.Snapshot;
            if (state == null)
            {
                _previousSnapshot = null;
                return;
            }
            if (state != _previousSnapshot || _couldChoose != _battle.CanChooseAction
                || _previousStage != _battle.InvestmentStage || _previousTarget != _battle.TargetId)
            {
                RefreshBattle();
            }
        }

        private void OnDisable()
        {
            PlayerSessionState.Current.Changed -= RefreshField;
        }

        private void OnDestroy()
        {
            for (int index = 0; index < _skills.Length; index++)
            {
                _skills[index].onClick.RemoveListener(_selectActions[index]);
            }
            for (int index = 0; index < _targets.Length; index++)
            {
                _targets[index].onClick.RemoveListener(_targetActions[index]);
            }
            _execute.onClick.RemoveListener(ExecuteSelectedSkill);
            _investment.onClick.RemoveListener(_battle.CycleInvestment);
            _defend.onClick.RemoveListener(_battle.Defend);
            if (_floorTravel != null)
            {
                _floorOne.onClick.RemoveListener(_floorTravel.TravelToFirstFloor);
                _floorTwo.onClick.RemoveListener(_floorTravel.TravelToSecondFloor);
            }
            if (_mapTexture != null)
            {
                _mapTexture.Release();
                Destroy(_mapTexture);
            }
            if (_mapCamera != null)
            {
                Destroy(_mapCamera.gameObject);
            }
        }

        public void SelectSkill(int index)
        {
            if (index < 0 || index >= _battle.Skills.Count)
            {
                return;
            }
            _selectedSkill = index;
            RefreshBattle();
        }

        public void ExecuteSelectedSkill()
        {
            if (_battle.CanChooseAction)
            {
                _battle.UseSkill(_selectedSkill);
            }
        }

        private void RefreshField()
        {
            PlayerRuntimeState state = PlayerSessionState.Current;
            if (state.MaxHp == 0)
            {
                return;
            }
            SetBar(_fieldHp, state.Hp, state.MaxHp);
            _fieldName.text = _battle.PlayerData == null ? state.CharacterId : _battle.PlayerData.DisplayName;
            _fieldMemory.text = "HP " + state.Hp + "/" + state.MaxHp + "   메모리 " + state.Memory + "/" + state.MaxMemory;
            int floor = _floorTravel == null ? 1 : _floorTravel.CurrentFloor;
            _fieldLocation.text = floor + "층 · 그레이박스";
            _floorBanner.text = "0" + floor + "F";
        }

        private void RefreshBattle()
        {
            BattleSnapshot state = _battle.Snapshot;
            if (state == null)
            {
                return;
            }
            _previousSnapshot = state;
            _previousStage = _battle.InvestmentStage;
            _previousTarget = _battle.TargetId;
            _couldChoose = _battle.CanChooseAction;
            CombatantState player = state.Combatants.First(unit => unit.InstanceId == "player");
            CombatantState[] enemies = state.Combatants.Where(unit => unit.Data.Team == BattleTeam.Monster).ToArray();
            CombatantState target = enemies.FirstOrDefault(unit => unit.InstanceId == _battle.TargetId) ?? enemies[0];
            _playerTurn.SetActive(_couldChoose);
            SetBar(_playerHp, player.Hp, player.Data.MaxHp);
            SetBar(_playerMemory, player.Memory, player.Data.MaxMemory);
            SetBar(_enemyHp, target.Hp, target.Data.MaxHp);
            SetBar(_enemyCardHp, target.Hp, target.Data.MaxHp);
            _playerVitals.text = "HP " + player.Hp + "/" + player.Data.MaxHp + "  메모리 " + player.Memory + "/" + player.Data.MaxMemory
                + "  폭주 " + player.RageEnergy;
            _memoryCurrent.text = player.Memory.ToString();
            _memoryCapacity.text = player.Data.MaxMemory.ToString();
            _skillMemory.text = player.Memory.ToString();
            string element = TutorialMonster.GetElementName(target.Data.Element);
            _enemyName.text = target.Data.DisplayName + " [" + element + "]";
            _enemyCardName.text = target.Data.DisplayName;
            _enemyDetails.text = element + " · HP " + target.Hp + "/" + target.Data.MaxHp + "\n메모리 " + target.Memory
                + "  연쇄 " + target.ChainStep + "/4\n약점 순서: "
                + string.Join(" → ", target.Data.WeaknessChain.Select(TutorialMonster.GetElementName));
            _round.text = "ROUND " + state.Round.ToString("00") + "   TURN " + state.TurnId.ToString("00");
            _investmentCost.text = "투자 " + _battle.InvestmentStage + "/5 · 비용 " + _battle.GetInvestmentCost(_battle.InvestmentStage);
            for (int index = 0; index < _investmentGlow.Length; index++)
            {
                _investmentGlow[index].SetActive(index < _battle.InvestmentStage);
                _investmentLock[index].SetActive(_battle.GetInvestmentCost(index + 1) > player.Memory);
            }
            for (int index = 0; index < _skills.Length; index++)
            {
                SkillData skill = _battle.Skills[index];
                _skillNames[index].text = skill.DisplayName;
                int cost = skill.MemoryCost + _battle.GetInvestmentCost(_battle.InvestmentStage);
                _skillCosts[index].text = "MEM " + cost;
                _skills[index].interactable = _couldChoose;
                _skillImages[index].sprite = index == _selectedSkill ? _selectedSkillSprite : _normalSkill;
                _skillCosts[index].color = cost > player.Memory ? new Color(1f, 0.4f, 0.4f) : Color.cyan;
            }
            SkillData selected = _battle.Skills[_selectedSkill];
            _execute.interactable = _couldChoose && selected.MemoryCost + _battle.GetInvestmentCost(_battle.InvestmentStage) <= player.Memory;
            _defend.interactable = _couldChoose && _battle.GetInvestmentCost(_battle.InvestmentStage) <= player.Memory;
            _investment.interactable = _couldChoose;
            RefreshSkillDetails(selected);
            for (int index = 0; index < _targets.Length; index++)
            {
                _targets[index].gameObject.SetActive(index < enemies.Length);
                if (index < enemies.Length)
                {
                    CombatantState enemy = enemies[index];
                    _targets[index].interactable = _couldChoose && !enemy.IsDead;
                    _targetNames[index].text = (enemy.InstanceId == target.InstanceId ? "▶ " : "") + (index + 1) + " "
                        + TutorialMonster.GetElementName(enemy.Data.Element) + " " + enemy.Hp;
                }
            }
            string[] order = state.TurnOrder.Distinct().ToArray();
            for (int index = 0; index < _turnSlots.Length; index++)
            {
                _turnSlots[index].Root.SetActive(index < order.Length);
                if (index < order.Length)
                {
                    CombatantState unit = state.Combatants.First(item => item.InstanceId == order[index]);
                    int actions = state.TurnOrder.Count(id => id == unit.InstanceId);
                    _turnSlots[index].Name.text = (index + 1).ToString("00") + " "
                        + (unit.Data.Team == BattleTeam.Player ? "플레이어" : TutorialMonster.GetElementName(unit.Data.Element))
                        + (actions > 1 ? " ×" + actions : "");
                    SetBar(_turnSlots[index].Hp, unit.Hp, unit.Data.MaxHp);
                    _turnSlots[index].Highlight.SetActive(unit.InstanceId == state.CurrentActorId);
                }
            }
        }

        private void RefreshSkillDetails(SkillData skill)
        {
            _skillName.text = skill.DisplayName;
            _skillType.text = TutorialMonster.GetElementName(skill.Element);
            _skillDescription.text = "기본 메모리 " + skill.MemoryCost + " · 투자 비용 " + _battle.GetInvestmentCost(_battle.InvestmentStage)
                + "\nEnter / 사용 버튼으로 실행";
            _skillPower.text = skill.Power.ToString();
            _skillAccuracy.text = (skill.Accuracy * 100).ToString("0.#") + "%";
            _skillElement.text = TutorialMonster.GetElementName(skill.Element);
            _skillTarget.text = skill.Target == SkillTarget.Enemy ? "적 1명" : skill.Target.ToString();
        }

        private static void SetBar(Slider bar, int value, int maximum)
        {
            bar.minValue = 0;
            bar.maxValue = maximum;
            bar.SetValueWithoutNotify(value);
        }
    }
}

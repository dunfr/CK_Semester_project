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
    [DefaultExecutionOrder(-40)]
    public sealed class PrototypeHudPresenter : MonoBehaviour
    {
        [Serializable]
        private sealed class EnemyHeadBar
        {
            [SerializeField] private RectTransform _root;
            [SerializeField] private Image _background;
            [SerializeField] private Slider _hp;

            public RectTransform Root => _root;
            public Image Background => _background;
            public Slider Hp => _hp;

            private Transform _monster;
            private Renderer[] _renderers;
            private Collider[] _colliders;

            public Vector3 GetWorldPosition(Transform monster, float height)
            {
                if (_monster != monster || _renderers == null || _colliders == null)
                {
                    _monster = monster;
                    // 목록만 캐시하고 활성 상태와 Bounds는 매번 확인한다.
                    _renderers = monster.GetComponentsInChildren<Renderer>(true);
                    _colliders = monster.GetComponentsInChildren<Collider>(true);
                }
                Bounds bounds = default;
                bool hasBounds = false;
                foreach (Collider collider in _colliders)
                {
                    if (collider == null || !collider.enabled || collider.isTrigger || !collider.gameObject.activeInHierarchy)
                    {
                        continue;
                    }
                    if (hasBounds)
                    {
                        bounds.Encapsulate(collider.bounds);
                    }
                    else
                    {
                        bounds = collider.bounds;
                        hasBounds = true;
                    }
                }
                if (!hasBounds)
                {
                    foreach (Renderer renderer in _renderers)
                    {
                        if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                        {
                            continue;
                        }
                        if (hasBounds)
                        {
                            bounds.Encapsulate(renderer.bounds);
                        }
                        else
                        {
                            bounds = renderer.bounds;
                            hasBounds = true;
                        }
                    }
                }
                return hasBounds ? new Vector3(bounds.center.x, bounds.max.y + height, bounds.center.z)
                    : monster.position + Vector3.up * 2f;
            }
        }

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

            private RectTransform _rect;
            private Vector2 _originalPosition;
            private Vector2 _moveStart;
            private Vector2 _moveTarget;
            private float _moveElapsed;

            public void SetPositionOffset(Vector2 offset, float seconds)
            {
                if (_root == null)
                {
                    return;
                }
                if (_rect == null)
                {
                    _rect = _root.transform as RectTransform;
                    if (_rect == null)
                    {
                        return;
                    }
                    // 최초 위치는 한 번만 기억한다.
                    _originalPosition = _rect.anchoredPosition;
                    _moveStart = _originalPosition;
                    _moveTarget = _originalPosition;
                }
                Vector2 target = _originalPosition + offset;
                if (_moveTarget != target)
                {
                    _moveStart = _rect.anchoredPosition;
                    _moveTarget = target;
                    _moveElapsed = 0f;
                }
                if (seconds <= 0f)
                {
                    _rect.anchoredPosition = _moveTarget;
                }
            }

            public void UpdatePosition(float seconds, bool reset)
            {
                if (_rect == null)
                {
                    return;
                }
                if (reset || _root == null || !_root.activeInHierarchy)
                {
                    _rect.anchoredPosition = _originalPosition;
                    _moveStart = _originalPosition;
                    _moveTarget = _originalPosition;
                    _moveElapsed = 0f;
                    return;
                }
                _moveElapsed += Time.unscaledDeltaTime;
                float progress = seconds <= 0f ? 1f : Mathf.Clamp01(_moveElapsed / seconds);
                _rect.anchoredPosition = Vector2.Lerp(_moveStart, _moveTarget, Mathf.SmoothStep(0f, 1f, progress));
            }
        }

        [SerializeField] private TutorialBattleController _battle;
        [SerializeField] private FloorTravelButtons _floorTravel;
        [SerializeField] private GameObject _playerTurn;
        [SerializeField] private TMP_Text _fieldName;
        // 위치 문구는 LevelProgress가 맡는다. 기존 에디터 도구 호환용으로만 남긴다.
        [SerializeField] private TMP_Text _fieldLocation;
        [SerializeField] private TMP_Text _floorBanner;
        [SerializeField] private TMP_Text _fieldMemory;
        [SerializeField] private Slider _fieldHp;
        [SerializeField] private TMP_Text _enemyName;
        [SerializeField] private TMP_Text _enemyCardName;
        [SerializeField] private TMP_Text _enemyDetails;
        [SerializeField] private Slider _enemyHp;
        [SerializeField] private EnemyHeadBar[] _enemyHeadBars = Array.Empty<EnemyHeadBar>();
        [SerializeField, Tooltip("잔상 몬스터 머리 위 체력바의 배경. 비어 있으면 기존 스프라이트를 유지한다.")]
        private Sprite _afterimageHeadBar;
        [SerializeField, Tooltip("각인 몬스터 머리 위 체력바의 배경. 비어 있으면 기존 스프라이트를 유지한다.")]
        private Sprite _imprintHeadBar;
        [SerializeField, Tooltip("망각 몬스터 머리 위 체력바의 배경. 비어 있으면 기존 스프라이트를 유지한다.")]
        private Sprite _oblivionHeadBar;
        [SerializeField, Tooltip("몬스터 렌더러 Bounds 윗면에서 체력바를 위로 띄우는 월드 거리(m).")]
        private float _enemyHeadBarHeight = 0.35f;
        [SerializeField, Tooltip("머리 위 체력바의 화면 위치 보정(px).")]
        private Vector2 _enemyHeadBarScreenOffset = Vector2.zero;
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
        [SerializeField] private GameObject _skillDetailsPanel;
        [SerializeField] private GameObject _commandPanel;
        [SerializeField] private GameObject _skillPanel;
        [SerializeField] private GameObject _backHint;
        [SerializeField] private TMP_Text _skillNavigationHint;
        [SerializeField] private Button[] _commands = Array.Empty<Button>();
        [SerializeField] private Image[] _commandImages = Array.Empty<Image>();
        [SerializeField] private TMP_Text[] _commandLabels = Array.Empty<TMP_Text>();
        [SerializeField] private Sprite _normalCommand;
        [SerializeField] private Sprite _selectedCommandSprite;
        [SerializeField, Tooltip("선택된 커맨드 버튼의 크기 배율.")]
        private float _selectedCommandScale = 1f;
        [SerializeField, Tooltip("나머지 커맨드 버튼의 기본 크기 배율.")]
        private float _normalCommandScale = 1f;
        [SerializeField] private TMP_Text _skillPower;
        [SerializeField] private TMP_Text _skillAccuracy;
        [SerializeField] private TMP_Text _skillElement;
        [SerializeField] private TMP_Text _skillTarget;
        [SerializeField] private Button[] _skills;
        [SerializeField] private TMP_Text[] _skillNames;
        [SerializeField] private TMP_Text[] _skillCosts;
        [SerializeField, Tooltip("선택된 스킬의 비용 문구 색. 비용이 부족하면 빨강을 우선 적용한다.")]
        private Color _selectedSkillCostColor = new Color(.45f, .9f, 1f);
        [SerializeField] private Image[] _skillImages;
        [SerializeField] private Sprite _normalSkill;
        [SerializeField] private Sprite _selectedSkillSprite;
        [SerializeField] private Transform _player;
        [SerializeField] private GameObject _fieldRoot;
        [SerializeField] private RawImage _minimap;
        [SerializeField, Tooltip("미니맵 플레이어 방향 표시. 위쪽(+Y)이 앞인 아이콘을 연결한다.")]
        private RectTransform _minimapPlayerMarker;
        [SerializeField] private Button _execute;
        [SerializeField] private Button _investment;
        [SerializeField, Tooltip("메모리 투자 패널이 선택된 스킬 버튼을 따라간다.")]
        private bool _investmentFollowsSelectedSkill = true;
        [SerializeField] private Button _defend;
        [SerializeField] private Button[] _targets;
        [SerializeField] private TMP_Text[] _targetNames;
        [SerializeField] private GameObject[] _investmentGlow;
        [SerializeField] private GameObject[] _investmentLock;
        [SerializeField] private TurnSlot[] _turnSlots;
        [SerializeField, Tooltip("현재 행동할 유닛의 슬롯 번호 색.")]
        private Color _turnHighlightColor = new Color(102f / 255f, 251f / 255f, 1f);
        [SerializeField, Tooltip("나머지 슬롯 번호의 기본 색.")]
        private Color _turnNormalColor = new Color(231f / 255f, 1f, 1f);
        [SerializeField, Tooltip("현재 행동할 유닛의 슬롯 번호 크기.")]
        private float _turnHighlightFontSize = 21f;
        [SerializeField, Tooltip("나머지 슬롯 번호의 기본 크기.")]
        private float _turnNormalFontSize = 18f;
        [SerializeField, Tooltip("현재 행동할 유닛의 슬롯을 원래 위치에서 내미는 거리.")]
        private Vector2 _turnSlotActiveOffset = new Vector2(14f, 0f);
        [SerializeField, Min(0f), Tooltip("턴 슬롯 이동에 걸리는 초. 0이면 즉시 이동한다.")]
        private float _turnSlotMoveSeconds = 0.12f;
        [SerializeField, Range(0f, 1f), Tooltip("죽은 유닛 슬롯의 투명도. 루트에 CanvasGroup이 있을 때 적용한다.")]
        private float _deadTurnSlotAlpha = 0.4f;

        [SerializeField] private PlayerMovement _movement;
        [SerializeField] private Button _sprint;
        [SerializeField] private Button _fieldAttack;
        [SerializeField] private CanvasGroup _areaBanner;
        [SerializeField, Min(0f), Tooltip("시작 시 구역 배너를 유지하는 초.")]
        private float _bannerHoldSeconds = 2.5f;
        [SerializeField, Min(0.01f), Tooltip("구역 배너가 사라지는 데 걸리는 초.")]
        private float _bannerFadeSeconds = 1.2f;
        private float _bannerStartedAt;

        private UnityAction[] _selectActions;
        private UnityAction[] _targetActions;
        private UnityAction[] _commandActions;
        private int _selectedCommand;
        private bool _isCommandPhase = true;
        private int _skillPhaseEnteredFrame = -1;
        private BattleSnapshot _previousSnapshot;
        private CombatantData _previousPlayerData;
        private int _selectedSkill;
        private bool _hasSkillSelection;
        private int _previousStage = -1;
        private int _previousFloor = -1;
        private bool _couldChoose;
        private string _previousTarget;
        private bool _previousTargetSelected;
        private Camera _mapCamera;
        private RenderTexture _mapTexture;
        private float _nextMapFrame;

        public int SelectedSkill => _selectedSkill;
        public bool HasSkillSelection => _hasSkillSelection;
        public bool IsCommandPhase => HasCommandMenu && _isCommandPhase;

        // 연결하지 않은 기존 씬은 스킬 메뉴를 그대로 사용한다.
        private bool HasCommandMenu => _commandPanel != null && _skillPanel != null && _backHint != null
            && _skillNavigationHint != null && _normalCommand != null && _selectedCommandSprite != null
            && _commands != null && _commands.Length == 4 && _commands.All(button => button != null)
            && _commandImages != null && _commandImages.Length == 4 && _commandImages.All(image => image != null)
            && _commandLabels != null && _commandLabels.Length == 4 && _commandLabels.All(label => label != null);

        private void Awake()
        {
            if (_skillDetailsPanel == null)
            {
                _skillDetailsPanel = _skillName.transform.parent.gameObject;
            }
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
            _commandActions = new UnityAction[_commands == null ? 0 : _commands.Length];
            for (int index = 0; index < _commandActions.Length; index++)
            {
                int commandIndex = index;
                _commandActions[index] = () => ExecuteCommand(commandIndex);
                if (_commands[index] != null)
                {
                    _commands[index].onClick.AddListener(_commandActions[index]);
                }
            }
            _bannerStartedAt = Time.unscaledTime;
            _areaBanner.alpha = 1f;
            _areaBanner.interactable = false;
            _areaBanner.blocksRaycasts = false;
            _sprint.onClick.AddListener(_movement.ToggleSprint);
            _fieldAttack.onClick.AddListener(InitiateFieldBattle);
            _execute.onClick.AddListener(ExecuteSelectedSkill);
            _investment.onClick.AddListener(_battle.CycleInvestment);
            _defend.onClick.AddListener(_battle.Defend);
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
            Canvas.willRenderCanvases += UpdateEnemyHeadBars;
            PlayerSessionState.Current.Changed += RefreshField;
            RefreshField();
        }

        private void Update()
        {
            RefreshBattleIfChanged();
            Keyboard keyboard = Keyboard.current;
            if (!_battle.HasSelectedBattleTarget || !_battle.CanChooseAction || !Application.isFocused || keyboard == null)
            {
                return;
            }
            bool up = keyboard.upArrowKey.wasPressedThisFrame;
            bool down = keyboard.downArrowKey.wasPressedThisFrame;
            bool confirm = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
            if (up || down || confirm)
            {
                // UI Submit과 별도 전투 입력이 같은 Enter를 이중 소비하지 않게 한다.
                var events = UnityEngine.EventSystems.EventSystem.current;
                if (events != null && events.currentSelectedGameObject != null
                    && events.currentSelectedGameObject.transform.IsChildOf(_playerTurn.transform))
                {
                    events.SetSelectedGameObject(null);
                }
            }
            if (IsCommandPhase)
            {
                if (up != down)
                {
                    _selectedCommand = (_selectedCommand + (up ? -1 : 1) + _commands.Length) % _commands.Length;
                    RefreshBattle();
                }
                if (confirm)
                {
                    ExecuteCommand(_selectedCommand);
                }
                return;
            }
            // 마우스로 커맨드를 연 프레임도 스킬 입력을 넘겨받지 않는다.
            if (_skillPhaseEnteredFrame == Time.frameCount)
            {
                return;
            }
            if (HasCommandMenu && (keyboard.backspaceKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
            {
                ReturnToCommands();
                return;
            }
            if (up != down)
            {
                MoveSkillSelection(up ? -1 : 1);
            }
            if (keyboard.fKey.wasPressedThisFrame)
            {
                _battle.CycleInvestment();
            }
            if (confirm)
            {
                ExecuteSelectedSkill();
            }
        }

        private void LateUpdate()
        {
            float fadeElapsed = Time.unscaledTime - _bannerStartedAt - _bannerHoldSeconds;
            _areaBanner.alpha = 1f - Mathf.Clamp01(fadeElapsed / _bannerFadeSeconds);
            if (_areaBanner.alpha <= 0f)
            {
                _areaBanner.gameObject.SetActive(false);
            }
            _sprint.interactable = _movement.isActiveAndEnabled;
            _sprint.targetGraphic.color = _movement.IsSprinting || _movement.IsSprintToggled
                ? new Color(.35f, 1f, 1f) : Color.white;
            _fieldAttack.interactable = _battle.CanInitiateNearbyBattle;
            if (_fieldRoot.activeInHierarchy && _minimapPlayerMarker != null && _player != null)
            {
                _minimapPlayerMarker.localRotation = Quaternion.Euler(0f, 0f, -_player.eulerAngles.y);
            }
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
            RefreshBattleIfChanged();
            UpdateTurnSlotPositions(_battle.Snapshot == null);
        }

        private void UpdateTurnSlotPositions(bool reset)
        {
            if (_turnSlots == null)
            {
                return;
            }
            foreach (TurnSlot slot in _turnSlots)
            {
                if (slot != null)
                {
                    slot.UpdatePosition(_turnSlotMoveSeconds, reset);
                }
            }
        }

        private void UpdateEnemyHeadBars()
        {
            if (!isActiveAndEnabled || _enemyHeadBars == null || _enemyHeadBars.Length == 0)
            {
                return;
            }
            BattleSnapshot state = _battle == null ? null : _battle.Snapshot;
            if (state == null)
            {
                return;
            }
            Camera camera = Camera.main;
            int index = 0;
            foreach (CombatantState unit in state.Combatants)
            {
                if (unit.Data.Team != BattleTeam.Monster)
                {
                    continue;
                }
                if (index >= _enemyHeadBars.Length)
                {
                    break;
                }
                EnemyHeadBar bar = _enemyHeadBars[index++];
                if (bar == null || bar.Root == null)
                {
                    continue;
                }
                if (unit.IsDead || camera == null || !_battle.TryGetMonsterTransform(unit.InstanceId, out Transform monster))
                {
                    bar.Root.gameObject.SetActive(false);
                    continue;
                }
                Sprite background = unit.Data.Element == BattleElement.Afterimage ? _afterimageHeadBar
                    : unit.Data.Element == BattleElement.Imprint ? _imprintHeadBar
                    : unit.Data.Element == BattleElement.Oblivion ? _oblivionHeadBar : null;
                if (bar.Background != null && background != null)
                {
                    bar.Background.sprite = background;
                }
                if (bar.Hp != null)
                {
                    SetBar(bar.Hp, unit.Hp, unit.Data.MaxHp);
                }
                Vector3 screen = camera.WorldToScreenPoint(bar.GetWorldPosition(monster, _enemyHeadBarHeight));
                RectTransform parent = bar.Root.parent as RectTransform;
                Canvas canvas = bar.Root.GetComponentInParent<Canvas>(true);
                canvas = canvas == null ? null : canvas.rootCanvas;
                if (screen.z <= 0f || parent == null || canvas == null)
                {
                    bar.Root.gameObject.SetActive(false);
                    continue;
                }
                Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,
                    (Vector2)screen + _enemyHeadBarScreenOffset, uiCamera, out Vector2 local))
                {
                    bar.Root.gameObject.SetActive(false);
                    continue;
                }
                // 부모 로컬 좌표를 써서 앵커 위치의 영향을 받지 않는다.
                bar.Root.localPosition = new Vector3(local.x, local.y, 0f);
                bar.Root.gameObject.SetActive(true);
            }
            for (; index < _enemyHeadBars.Length; index++)
            {
                EnemyHeadBar bar = _enemyHeadBars[index];
                if (bar != null && bar.Root != null)
                {
                    bar.Root.gameObject.SetActive(false);
                }
            }
        }

        private void HideEnemyHeadBars()
        {
            if (_enemyHeadBars == null)
            {
                return;
            }
            foreach (EnemyHeadBar bar in _enemyHeadBars)
            {
                if (bar != null && bar.Root != null)
                {
                    bar.Root.gameObject.SetActive(false);
                }
            }
        }

        private void RefreshBattleIfChanged()
        {
            BattleSnapshot state = _battle.Snapshot;
            if (state == null)
            {
                HideEnemyHeadBars();
                _previousSnapshot = null;
                _previousPlayerData = null;
                _hasSkillSelection = false;
                _selectedSkill = 0;
                _isCommandPhase = true;
                _selectedCommand = 0;
                _couldChoose = false;
                _previousTargetSelected = false;
                _skillDetailsPanel.SetActive(false);
                return;
            }
            if (state != _previousSnapshot || _couldChoose != _battle.CanChooseAction
                || _previousStage != _battle.InvestmentStage || _previousTarget != _battle.TargetId
                || _previousTargetSelected != _battle.HasSelectedBattleTarget)
            {
                RefreshBattle();
            }
        }

        private void OnDisable()
        {
            Canvas.willRenderCanvases -= UpdateEnemyHeadBars;
            HideEnemyHeadBars();
            PlayerSessionState.Current.Changed -= RefreshField;
            UpdateTurnSlotPositions(true);
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
            for (int index = 0; index < _commandActions.Length; index++)
            {
                if (_commands[index] != null)
                {
                    _commands[index].onClick.RemoveListener(_commandActions[index]);
                }
            }
            _sprint.onClick.RemoveListener(_movement.ToggleSprint);
            _fieldAttack.onClick.RemoveListener(InitiateFieldBattle);
            _execute.onClick.RemoveListener(ExecuteSelectedSkill);
            _investment.onClick.RemoveListener(_battle.CycleInvestment);
            _defend.onClick.RemoveListener(_battle.Defend);
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

        private void InitiateFieldBattle()
        {
            _battle.TryInitiateNearbyBattle();
        }

        public void SelectSkill(int index)
        {
            if (!_battle.HasSelectedBattleTarget || !_battle.CanChooseAction
                || index < 0 || index >= Mathf.Min(_battle.Skills.Count, _skills.Length))
            {
                return;
            }
            RefreshBattle();
            _selectedSkill = index;
            EnterSkillPhase(true);
        }

        public void MoveSkillSelection(int direction)
        {
            int count = Mathf.Min(_battle.Skills.Count, _skills.Length);
            if (!_battle.HasSelectedBattleTarget || !_battle.CanChooseAction || count == 0 || direction == 0)
            {
                return;
            }
            SelectSkill((_selectedSkill + (direction > 0 ? 1 : -1) + count) % count);
        }

        public void ExecuteSelectedSkill()
        {
            if (_battle.HasSelectedBattleTarget && _battle.CanChooseAction)
            {
                RefreshBattle();
                if (IsCommandPhase)
                {
                    EnterSkillPhase(_hasSkillSelection);
                }
                _battle.UseSkill(_selectedSkill);
            }
        }

        public void ExecuteCommand(int index)
        {
            RefreshBattle();
            if (!IsCommandPhase || !_battle.HasSelectedBattleTarget || !_battle.CanChooseAction
                || index < 0 || index >= _commands.Length || !_commands[index].interactable)
            {
                return;
            }
            _selectedCommand = index;
            switch (index)
            {
                case 0:
                    EnterSkillPhase(false);
                    break;
                case 1:
                    _battle.Defend();
                    break;
                case 2:
                    CombatantState[] enemies = _battle.Snapshot.Combatants
                        .Where(unit => unit.Data.Team == BattleTeam.Monster).ToArray();
                    int current = Array.FindIndex(enemies, unit => unit.InstanceId == _battle.TargetId);
                    for (int offset = 1; offset <= enemies.Length; offset++)
                    {
                        int next = (current + offset) % enemies.Length;
                        if (!enemies[next].IsDead)
                        {
                            _battle.SelectTarget(next);
                            break;
                        }
                    }
                    break;
                case 3:
                    EnterSkillPhase(true);
                    break;
            }
            RefreshBattle();
        }

        public void ReturnToCommands()
        {
            RefreshBattle();
            if (!HasCommandMenu)
            {
                return;
            }
            _isCommandPhase = true;
            _hasSkillSelection = false;
            RefreshBattle();
        }

        private void EnterSkillPhase(bool showDetails)
        {
            if (IsCommandPhase)
            {
                _skillPhaseEnteredFrame = Time.frameCount;
            }
            _isCommandPhase = false;
            _hasSkillSelection = showDetails;
            RefreshBattle();
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
            _floorBanner.text = floor + "F";
        }

        private void RefreshBattle()
        {
            BattleSnapshot state = _battle.Snapshot;
            if (state == null)
            {
                return;
            }
            // 전투 시작 때 재생성되는 데이터로 같은 프레임의 재시작도 구분한다.
            if (HasCommandMenu && (!_battle.CanChooseAction || !_battle.HasSelectedBattleTarget || !_couldChoose
                || !_previousTargetSelected || _previousSnapshot == null || state.TurnId != _previousSnapshot.TurnId
                || !ReferenceEquals(_previousPlayerData, _battle.PlayerData)))
            {
                _isCommandPhase = true;
                _selectedCommand = 0;
                _hasSkillSelection = false;
            }
            _previousSnapshot = state;
            _previousPlayerData = _battle.PlayerData;
            _previousStage = _battle.InvestmentStage;
            _previousTarget = _battle.TargetId;
            _previousTargetSelected = _battle.HasSelectedBattleTarget;
            _couldChoose = _battle.CanChooseAction;
            if (!_couldChoose || !_battle.HasSelectedBattleTarget)
            {
                _hasSkillSelection = false;
            }
            _skillDetailsPanel.SetActive(!IsCommandPhase && _hasSkillSelection && _couldChoose && _battle.HasSelectedBattleTarget);
            CombatantState player = state.Combatants.First(unit => unit.Data.Team == BattleTeam.Player);
            CombatantState[] enemies = state.Combatants.Where(unit => unit.Data.Team == BattleTeam.Monster).ToArray();
            CombatantState target = enemies.FirstOrDefault(unit => unit.InstanceId == _battle.TargetId) ?? enemies[0];
            _playerTurn.SetActive(_couldChoose && _battle.HasSelectedBattleTarget);
            SetBar(_playerHp, player.Hp, player.Data.MaxHp);
            SetBar(_playerMemory, player.Memory, player.Data.MaxMemory);
            if (_enemyHp != null)
            {
                SetBar(_enemyHp, target.Hp, target.Data.MaxHp);
            }
            SetBar(_enemyCardHp, target.Hp, target.Data.MaxHp);
            if (_playerVitals != null)
            {
                _playerVitals.text = "HP " + player.Hp + "/" + player.Data.MaxHp + "  메모리 " + player.Memory + "/" + player.Data.MaxMemory
                    + "  폭주 " + player.RageEnergy;
            }
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
                _skillNames[index].color = index == _selectedSkill ? new Color(.04f, .12f, .2f) : Color.white;
                _skillCosts[index].color = cost > player.Memory
                    ? new Color(1f, 0.4f, 0.4f)
                    : index == _selectedSkill ? _selectedSkillCostColor : Color.cyan;
            }
            FollowInvestmentToSelectedSkill();
            SkillData selected = _battle.Skills[_selectedSkill];
            _execute.interactable = _couldChoose && _hasSkillSelection && selected.MemoryCost + _battle.GetInvestmentCost(_battle.InvestmentStage) <= player.Memory;
            _defend.interactable = _couldChoose && _battle.GetInvestmentCost(_battle.InvestmentStage) <= player.Memory;
            _investment.interactable = _couldChoose;
            RefreshCommandMenu();
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
            // 슬롯은 플레이어, 몬스터 순서로 고정하고 행동 강조만 옮긴다.
            for (int index = 0; index < _turnSlots.Length; index++)
            {
                TurnSlot slot = _turnSlots[index];
                if (slot == null)
                {
                    continue;
                }
                CombatantState unit = index == 0 ? player : index - 1 < enemies.Length ? enemies[index - 1] : null;
                bool highlighted = unit != null && unit.InstanceId == state.CurrentActorId;
                if (slot.Root != null)
                {
                    slot.Root.SetActive(unit != null);
                    CanvasGroup group = slot.Root.GetComponent<CanvasGroup>();
                    if (group != null)
                    {
                        group.alpha = unit != null && unit.IsDead ? _deadTurnSlotAlpha : 1f;
                    }
                }
                slot.SetPositionOffset(highlighted ? _turnSlotActiveOffset : Vector2.zero, _turnSlotMoveSeconds);
                if (unit == null || !isActiveAndEnabled)
                {
                    slot.UpdatePosition(_turnSlotMoveSeconds, true);
                }
                if (slot.Highlight != null)
                {
                    slot.Highlight.SetActive(highlighted);
                }
                if (unit != null)
                {
                    if (slot.Name != null)
                    {
                        slot.Name.text = (index + 1).ToString("00");
                        slot.Name.color = highlighted ? _turnHighlightColor : _turnNormalColor;
                        slot.Name.fontSize = highlighted ? _turnHighlightFontSize : _turnNormalFontSize;
                    }
                    if (slot.Hp != null)
                    {
                        SetBar(slot.Hp, unit.Hp, unit.Data.MaxHp);
                    }
                }
            }
        }

        private void FollowInvestmentToSelectedSkill()
        {
            if (!_investmentFollowsSelectedSkill || _investment == null || _skills == null
                || _selectedSkill < 0 || _selectedSkill >= _skills.Length || _skills[_selectedSkill] == null)
            {
                return;
            }
            RectTransform panel = _investment.transform as RectTransform;
            RectTransform target = _skills[_selectedSkill].transform as RectTransform;
            if (panel == null || target == null)
            {
                return;
            }
            // 같은 부모에서는 UI 기준점과 크기도 맞춘다.
            if (panel.parent == target.parent)
            {
                panel.anchorMin = target.anchorMin;
                panel.anchorMax = target.anchorMax;
                panel.pivot = target.pivot;
                panel.anchoredPosition = target.anchoredPosition;
                panel.sizeDelta = target.sizeDelta;
                panel.localRotation = target.localRotation;
            }
            else
            {
                panel.position = target.position;
                panel.rotation = target.rotation;
            }
            panel.localScale = target.localScale;
            if (panel.parent != null && panel.GetSiblingIndex() != panel.parent.childCount - 1)
            {
                panel.SetAsLastSibling();
            }
        }

        private void RefreshCommandMenu()
        {
            bool hasMenu = HasCommandMenu;
            if (_commandPanel != null)
            {
                _commandPanel.SetActive(IsCommandPhase);
            }
            if (_skillPanel != null)
            {
                _skillPanel.SetActive(!IsCommandPhase);
            }
            _defend.gameObject.SetActive(!hasMenu);
            if (!hasMenu)
            {
                return;
            }
            _backHint.SetActive(!IsCommandPhase);
            _skillNavigationHint.text = IsCommandPhase
                ? "↑ ↓ 커맨드 선택   ·   Enter 결정"
                : "↑ ↓ 스킬 선택   ·   Enter 사용   ·   F 투자   ·   Backspace 뒤로";
            for (int index = 0; index < _commands.Length; index++)
            {
                _commands[index].interactable = _couldChoose && _battle.HasSelectedBattleTarget
                    && (index != 1 || _defend.interactable);
                _commandImages[index].sprite = index == _selectedCommand ? _selectedCommandSprite : _normalCommand;
                _commandLabels[index].color = index == _selectedCommand ? new Color(.04f, .12f, .2f) : Color.white;
                float scale = index == _selectedCommand ? _selectedCommandScale : _normalCommandScale;
                _commands[index].transform.localScale = Vector3.one * scale;
            }
        }

        private void RefreshSkillDetails(SkillData skill)
        {
            _skillName.text = skill.DisplayName;
            _skillType.text = TutorialMonster.GetElementName(skill.Element);
            _skillDescription.text = (skill.Target == SkillTarget.Enemy
                ? "적 1명에게 " + TutorialMonster.GetElementName(skill.Element) + " 속성으로 공격합니다."
                : "대상: " + skill.Target)
                + "\n기본 메모리 " + skill.MemoryCost + " · 투자 비용 " + _battle.GetInvestmentCost(_battle.InvestmentStage);
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

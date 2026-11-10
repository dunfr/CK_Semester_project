using System;
using System.Collections.Generic;
using System.Linq;
using CK.SemesterProject.Battle;
using CK.SemesterProject.Data;
using CK.SemesterProject.SkillSequence;
using Semester.Enemies;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CK.SemesterProject.Tutorial
{
    public sealed class TutorialBattleController : MonoBehaviour
    {
        [SerializeField, Min(0.1f), Tooltip("행동 결과를 표시한 뒤 다음 행동으로 넘어가는 시간(초)")]
        private float _actionSeconds = 1.2f;
        private const float MinimumBattleSpacing = 4f;
        private const float ArenaMemberSpacing = 2.5f;
        private static readonly float[] FallbackSpacingScales = { 1f, 0.75f, 0.55f };

        [SerializeField, Min(2f), Tooltip("전투 진입 시 플레이어와 몬스터 사이의 목표 간격(m)")]
        private float _battleSpacing = 7.5f;
        // 기존 씬의 Vector3 설정을 새 XY / Distance 필드로 한 번 이전한다.
        [SerializeField, HideInInspector]
        private Vector3 _battleCameraOffset = new Vector3(2.5f, 1.25f, -1.75f);
        [SerializeField, HideInInspector] private bool _cameraSettingsMigrated;
        [Header("Battle Camera")]
        [SerializeField, Tooltip("플레이어 기준 X: 오른쪽, Y: 높이(m). 전투 간격에 따라 확대되며 실제 전투에서는 장애물/화면 보정을 적용합니다.")]
        private Vector2 _battleCameraOffsetXY = new Vector2(2.5f, 1.25f);
        [SerializeField, Min(0.1f), Tooltip("플레이어 뒤쪽으로 떨어지는 기본 거리(m). 양수일수록 멀어집니다.")]
        private float _battleCameraDistance = 1.75f;
        [SerializeField, Tooltip("Play 모드 디버그: true = 전투 구도, false = 탐색 구도. 비전투에서는 카메라만 미리 봅니다. 실제 전투 진입/종료 시 자동 전환됩니다.")]
        private bool _debugBattleCamera;
        [SerializeField, Tooltip("비전투 카메라 미리보기의 대상(선택). 비어 있으면 플레이어 전방 Battle Spacing 지점을 바라봅니다.")]
        private Transform _debugCameraTarget;
        [SerializeField, Range(35f, 65f), Tooltip("전투 카메라의 수직 시야각(도)")]
        private float _battleFieldOfView = 56f;
        [SerializeField, Min(0.1f), Tooltip("전투 진입 카메라가 자리를 잡는 시간(초)")]
        private float _cameraEntrySeconds = 0.85f;
        [SerializeField, Min(0f), Tooltip("대기 중 카메라의 좌우 이동 폭(m)")]
        private float _cameraDrift = 0.18f;
        [SerializeField, Min(0f), Tooltip("행동 시 카메라의 이동 폭(m)")]
        private float _cameraActionTravel = 0.45f;
        [Header("Battle Setup")]
        [SerializeField, Min(1), Tooltip("DT 미제공 몬스터의 임시 HP. 밸런스 확정값이 아닙니다.")]
        private int _monsterHp = 600;
        [SerializeField, Min(1), Tooltip("DT 미제공 몬스터의 임시 초기 메모리")]
        private int _monsterMemory = 100;
        [SerializeField, Min(0), Tooltip("몬스터 접촉으로 진입할 때 몬스터가 얻는 추가 메모리")]
        private int _collisionMemoryBonus = 1;
        [SerializeField, Range(1, 3), Tooltip("전투 참여 몬스터 수. 접촉한 몬스터와 가까운 살아 있는 필드 몬스터를 선택합니다. 부족하면 현재 수로 진입합니다.")]
        private int _encounterSize = 1;
        [SerializeField, Tooltip("필드 좌클릭/접촉 자동 진입 사용. 조합 선택 테스트 씬에서는 끕니다.")]
        private bool _automaticEncounters = true;
        [SerializeField, Tooltip("전투 전용 장소의 중심(선택). 지정하면 전투 시작 시 플레이어와 참여 몬스터를 이곳으로 옮겨 싸우고, 종료 후 원래 자리로 돌려보냅니다. 플레이어는 중심의 -Z(뒤), 몬스터는 +Z(앞) 쪽에 섭니다.")]
        private Transform _battleArena;
        [SerializeField, Tooltip("튜토리얼 플레이어 이동 컴포넌트")]
        private PlayerMovement _movement;
        [SerializeField, Tooltip("필드 카메라 입력")]
        private CameraController _orbit;
        [SerializeField, Tooltip("메인 카메라")]
        private Camera _camera;
        [SerializeField, Tooltip("필드 카메라 제어기")]
        private CinemachineBrain _brain;
        [SerializeField, Tooltip("탐색 중 동작할 박스 몬스터")]
        private EnemyStateMachine[] _enemies = Array.Empty<EnemyStateMachine>();
        [SerializeField, Tooltip("가져온 캐릭터 DT")]
        private CharacterDatabase _characters;
        [SerializeField, Tooltip("사용할 캐릭터 ID")]
        private string _characterId = "CH00";
        [SerializeField, Tooltip("기존 Skill_DT CSV")]
        private TextAsset _skillTable;
        [SerializeField, Tooltip("기본 스킬의 승리 횟수별 강화 공격력과 단계별 연출")]
        private BasicSkillEnhancementAsset _basicSkillEnhancement;
        [SerializeField, Tooltip("원본 필드 UI. 전투 중 표시만 잠시 숨깁니다.")]
        private GameObject _fieldUI;
        [SerializeField, Tooltip("필드 UI와 별개인 전투 조작 패널")]
        private GameObject _battleUI;
        [SerializeField, Tooltip("전투 전용 HP 슬라이더")]
        private Slider _hpBar;
        [SerializeField, Tooltip("필드 HP 표시. 전투와 같은 플레이어 상태를 사용합니다.")]
        private Slider _fieldHpBar;
        [SerializeField, Tooltip("필드 HP·메모리 수치 표시")]
        private TMP_Text _fieldVitals;
        [SerializeField, Tooltip("전투 전용 스킬 버튼 3개")]
        private Button[] _skillButtons;
        [SerializeField, Tooltip("투자 단계 버튼")]
        private Button _investmentButton;
        [SerializeField, Tooltip("방어 버튼")]
        private Button _defendButton;
        [SerializeField, Tooltip("결과 확인 버튼")]
        private Button _continueButton;
        [SerializeField, Tooltip("HP·메모리·폭주 표시")]
        private TMP_Text _status;
        [SerializeField, Tooltip("행동 순서와 몬스터 상태 표시")]
        private TMP_Text _enemyStatus;
        [SerializeField, Tooltip("행동 결과와 탐색 조작 안내")]
        private TMP_Text _notice;
        [SerializeField, Tooltip("메모리 투자 비용 표시")]
        private TMP_Text _investmentLabel;
        [SerializeField, Tooltip("전투 대상 선택 버튼 3개. 숫자 1~3 키로도 선택할 수 있습니다.")]
        private Button[] _targetButtons;

        [Serializable]
        private struct SkillCinematicBinding
        {
            public string SkillId;
            public SkillCinematic Cinematic;
        }

        [Header("Skill Cinematics")]
        [SerializeField, Tooltip("스킬별 타임라인 연출(이펙터 시네머신 연출). 지정하면 사용 시 연출 카메라로 넘겨 재생합니다")]
        private SkillCinematicBinding[] _skillCinematics = Array.Empty<SkillCinematicBinding>();

        [Header("Skill Cutscene")]
        [SerializeField, Tooltip("스킬별 완성 컷신(Timeline 프리팹) 재생을 맡는 컴포넌트. 비워 두면 같은 오브젝트에서 찾습니다")]
        private TutorialSkillCutscene _skillCutscene;
        // 스킬 연출 스튜디오 시퀀스 재생기(선택). 연결된 스킬은 기존 임시 연출 대신 시퀀스를 재생한다.
        private TutorialSkillSequenceRunner _sequenceRunner;
        private long _sequenceActionId = -1;

        [Header("Cinematic Camera")]
        [SerializeField, Min(0f), Tooltip("스킬 연출 카메라(Ult_cam 등)를 시선 반대 방향으로 물리는 거리(m). 이펙트가 화면에 들어오도록 원본 카메라보다 멀리 잡습니다")]
        private float _cinematicPullback = 1.5f;
        [SerializeField, Range(1f, 2f), Tooltip("스킬 연출 카메라 시야각 배율. 1이면 원본 그대로")]
        private float _cinematicFieldOfViewScale = 1.25f;

        [Header("Cheat (Editor/Development)")]
        [SerializeField, Tooltip("치트 키 사용. 켜 두면 에디터와 빌드(exe) 모두에서 동작합니다. 배포용 빌드에서는 끄세요")]
        private bool _enableCheats = true;
        [SerializeField, Tooltip("누르면 플레이어 폭주 게이지를 채웁니다(특수·궁극기 테스트용)")]
        private Key _rageCheatKey = Key.F9;
        [SerializeField, Range(0, 90), Tooltip("치트로 채울 폭주 값. 91 이상은 폭주(과열)로 행동 불능이 되므로 90까지")]
        private int _rageCheatValue = 90;
        [SerializeField, Tooltip("켜면 전투에서 플레이어 메모리가 늘 최대이고, 폭주는 과열 직전(90)으로 유지되며, 과열·메모리 고갈로 턴을 잃지 않습니다. 빌드에서도 적용됩니다.")]
        private bool _unlimitedPlayerResources = true;
        [SerializeField, Tooltip("전투 중: 몬스터를 모두 쓰러뜨려 바로 승리합니다(행동 입력 차례일 때)")]
        private Key _winCheatKey = Key.F10;
        [SerializeField, Tooltip("필드·전투: 플레이어 HP·메모리를 최대로 채웁니다")]
        private Key _healCheatKey = Key.F11;
        [SerializeField, Tooltip("필드·전투: 게임을 처음(시작 연출)부터 다시 시작합니다. 플레이 상태도 초기화됩니다")]
        private Key _restartCheatKey = Key.F12;
        [SerializeField, Tooltip("필드: 보스방 입구(재시작 지점)로 순간이동합니다")]
        private Key _bossWarpCheatKey = Key.F8;
        [SerializeField, Tooltip("보스방 순간이동 위치. 비우면 이름이 Boss_RetryPoint인 오브젝트를 찾습니다")]
        private Transform _bossWarpPoint;
        [SerializeField, Tooltip("필드: 다음 중간 체크포인트로 순간이동합니다. Shift와 함께 누르면 이전 체크포인트로 갑니다")]
        private Key _checkpointCheatKey = Key.F7;

        [SerializeField, Min(0.01f), Tooltip("전투 중 우클릭 드래그 회전 감도(도/픽셀)")]
        private float _battleMouseSensitivity = 0.15f;

        [SerializeField, Min(0f), Tooltip("걷기 발소리 반경(m)")]
        private float _walkNoiseRadius = 3f;
        [SerializeField, Min(0f), Tooltip("달리기 발소리 반경(m)")]
        private float _sprintNoiseRadius = 8f;

        private float _battleYaw;
        private float _battlePitch;
        private bool _isCameraDragging;
        private BattleSnapshot _cachedSnapshot;
        private readonly Dictionary<GameObject, bool> _backgroundEnemyStates = new Dictionary<GameObject, bool>();
        private readonly MonsterAi _ai = new MonsterAi();
        private BattleSession _session;
        private CombatantData _playerData;
        private SkillData[] _baseSkills;
        private SkillData[] _skills;
        private TMP_Text[] _skillLabels;
        private SkillEnhancementRules _enhancementRules;
        private int _battleSkillStage;
        private BattleActionResolver _damagePredictor;
        private int _testSkillStage;
        private bool _hasRecordedBattleResult;
        private EnemyStateMachine _encounter;
        private TutorialBattleFormation _formation;
        private EnemyStateMachine[] _encounterMembers = Array.Empty<EnemyStateMachine>();
        private TutorialBattleActionPresentation[] _presentations = Array.Empty<TutorialBattleActionPresentation>();
        private TutorialBattleActionPresentation[] _monsterPresentations = Array.Empty<TutorialBattleActionPresentation>();
        private string _targetId = "monster";
        private Vector3 _playerHome;
        private Vector3 _enemyCenter;
        private UnityEngine.Events.UnityAction[] _targetActions;
        private CharacterController _characterController;
        private Animator _animator;
        private PlayerBattleAnimation _battleAnimation;
        // 플레이어 행동 모션(궁극기 등)을 재생 중인 행동과 그 길이. 연출은 모션이 끝날 때까지 기다린다.
        private long _playerMotionActionId = -1;
        private float _playerMotionSeconds;
        private bool _playerMotionApproaches = true;
        // 연출 카메라(궁극기의 Ult_cam)로 보고 있는지와, 끝나면 되돌아갈 그 직전의 전투 시점.
        private bool _isCinematicCameraActive;
        private Pose _cinematicReturnPose;
        private float _cinematicReturnFieldOfView;
        private bool[] _enemyEnabled;
        private bool _enemiesPaused;
        private LevelDesign.LevelProgress _levelProgress;

        /// <summary>
        /// 전투·컷신으로 필드 몬스터가 멈춘 동안 true. 몬스터 AI(EnemyStateMachine)를 거치지 않고
        /// 에이전트를 직접 움직이는 연출(EnemyRoutePreset 등)도 이 값을 보고 멈춘다.
        /// </summary>
        public static bool AreFieldEnemiesPaused { get; private set; }
        private bool _fieldUIWasActive;
        private bool _movementEnabled;
        private bool _orbitEnabled;
        private bool _brainEnabled;
        private bool _isInBattleArena;
        private Pose _arenaReturnPlayerPose;
        private Pose[] _arenaReturnMemberPoses = Array.Empty<Pose>();
        private float _animatorSpeed;
        private Vector3 _spawn;
        private Vector3 _cameraPosition;
        private Quaternion _cameraRotation;
        private float _cameraFieldOfView;
        private bool _cameraWasOrthographic;
        private Pose _battleCameraPose;
        private float _cameraTime;
        private PlayerRuntimeState _playerState;
        private int _investment;
        private float _elapsed;
        private BattleActionResult _timedPresentation;
        private float _presentationSeconds;
        private float _encounterCooldown;
        private float _nextFootstep;
        private Vector3 _lastPlayerPosition;
        private UnityEngine.Events.UnityAction[] _skillActions;
        private TutorialBattleCameraPreview _cameraPreview;
        private readonly TutorialBattleTargetHighlight _targetHighlight = new TutorialBattleTargetHighlight();
        private bool _isBattleCameraActive;

        private Vector3 ConfiguredCameraOffset => new Vector3(
            _battleCameraOffsetXY.x, _battleCameraOffsetXY.y, -_battleCameraDistance);

        private float PresentationSeconds
        {
            get
            {
                BattleActionResult pending = _session?.PendingResult;
                if (pending == null)
                {
                    _timedPresentation = null;
                    return _actionSeconds;
                }
                if (ReferenceEquals(_timedPresentation, pending))
                {
                    return GetMotionAwareSeconds(pending, _presentationSeconds);
                }
                bool isPlayerSkill = pending != null && pending.Request.ActorId == "player"
                    && pending.Request.Kind == BattleActionKind.Skill && !pending.WasSkipped
                    && !pending.IsTurnStartEffect && _basicSkillEnhancement != null;
                _timedPresentation = pending;
                _presentationSeconds = isPlayerSkill ? _basicSkillEnhancement.GetPresentationSeconds(pending.Request.SkillId,
                    TestSkillStage, _actionSeconds) : _actionSeconds;
                return GetMotionAwareSeconds(pending, _presentationSeconds);
            }
        }

        // 플레이어 행동 모션(궁극기 등)이 연출 시간보다 길면 모션이 끝날 때까지 기다린다.
        private float GetMotionAwareSeconds(BattleActionResult pending, float seconds)
        {
            return pending != null && pending.ActionId == _playerMotionActionId ? Mathf.Max(seconds, _playerMotionSeconds) : seconds;
        }

        public event Action<BattleOutcome> FieldBattleEnded;

        public void SetRespawnPoint(Vector3 position)
        {
            _spawn = position;
        }

        public bool IsInBattle => _session != null;

        // TutorialSkillCutscene이 컷신 동안 카메라·플레이어·적을 다루기 위한 최소한의 접근.
        public Camera BattleCamera => _camera;
        public CinemachineBrain BattleBrain => _brain;
        public Transform PlayerRoot => _movement.transform;
        public IReadOnlyList<EnemyStateMachine> EncounterMembers => _encounterMembers;
        public void PlacePlayer(Vector3 position, Quaternion rotation) => SetPlayerPose(position, rotation);

        public void SetBattleUiVisible(bool visible)
        {
            _battleUI.SetActive(visible);
            if (visible && IsInBattle)
            {
                RefreshUI();
            }
        }

        private bool IsSkillCutscenePlaying => _skillCutscene != null && _skillCutscene.IsPlaying;
        public int SkillStage => _battleSkillStage > 0 ? _battleSkillStage : TestSkillStage;
        public int TestSkillStage => _testSkillStage > 0 ? _testSkillStage
            : _enhancementRules?.GetStage(_playerState?.VictoryCount ?? 0) ?? 1;

        public void CycleTestSkillStage()
        {
            if (IsInBattle || _playerData == null || !enabled)
            {
                return;
            }
            // 테스트 편의를 위한 단계 선택이다. 실제 승리 횟수는 변경하지 않는다.
            _testSkillStage = TestSkillStage % 3 + 1;
            RefreshPlayerData();
        }
        private IReadOnlyList<BattleHistoryEntry> _lastHistory = Array.Empty<BattleHistoryEntry>();
        public IReadOnlyList<BattleHistoryEntry> History => _session?.History ?? _lastHistory;
        public BattleSnapshot Snapshot => _session == null ? null : _cachedSnapshot ?? (_cachedSnapshot = _session.GetSnapshot());
        public IReadOnlyList<SkillData> Skills => _skills ?? Array.Empty<SkillData>();
        public CombatantData PlayerData => _playerData;
        public int InvestmentStage => _investment;
        public string TargetId => _targetId;
        public bool HasSelectedBattleTarget { get; private set; }
        public bool CanChooseAction => CanInput(Snapshot);

        public int GetInvestmentCost(int stage)
        {
            return _session == null ? 0 : _session.Rules.GetStageCost(_playerData, stage);
        }

        public void SetInvestmentStage(int stage)
        {
            if (!CanChooseAction || stage < 0 || stage > 5)
            {
                return;
            }
            _investment = stage;
            RefreshUI();
        }

        private void Awake()
        {
            // 씬을 다시 불러와도 지난 판의 멈춤 상태가 남지 않게 한다.
            AreFieldEnemiesPaused = false;
            MigrateCameraSettings();
            if (_skillCutscene == null)
            {
                _skillCutscene = GetComponent<TutorialSkillCutscene>();
            }
            _sequenceRunner = GetComponent<TutorialSkillSequenceRunner>();
            // 에디터에서 지운 적이 목록에 빈 칸으로 남아 있을 수 있으므로 빼고 쓴다.
            _enemies = _enemies == null ? Array.Empty<EnemyStateMachine>() : _enemies.Where(enemy => enemy != null).ToArray();
            if (_movement == null || _orbit == null || _camera == null || _brain == null || _characters == null
                || _fieldUI == null || _skillTable == null || _basicSkillEnhancement == null
                || _battleUI == null
                || _hpBar == null || _skillButtons == null || _skillButtons.Length != 3
                || _investmentButton == null || _defendButton == null || _continueButton == null
                || _status == null || _enemyStatus == null || _notice == null || _investmentLabel == null)
            {
                Debug.LogError("TutorialBattleController: 씬의 전투 참조가 누락되었습니다.", this);
                enabled = false;
                return;
            }
            try
            {
                _baseSkills = SkillTable.LoadCsv(_skillTable.text).ToArray();
                CreateAdditionalSkillButtons();
                _playerState = PlayerSessionState.Current;
                RefreshPlayerData();
            }
            catch (Exception exception)
            {
                Debug.LogError("TutorialBattleController: " + exception.Message, this);
                enabled = false;
                return;
            }
            _characterController = _movement.GetComponent<CharacterController>();
            _animator = _movement.GetComponentsInChildren<Animator>()
                .FirstOrDefault(animator => animator.enabled && animator.runtimeAnimatorController != null);
            _battleAnimation = _movement.GetComponentInChildren<PlayerBattleAnimation>();
            _spawn = _movement.transform.position;
            _lastPlayerPosition = _spawn;
            _playerState.Initialize(_playerData);
            _playerState.RestoreFieldMemory();
            InitializeFieldVitals();
            _playerState.Changed += RefreshFieldVitals;
            RefreshFieldVitals();
            _enemyEnabled = new bool[_enemies.Length];
            ConfigureSkillLayout();
            _skillActions = new UnityEngine.Events.UnityAction[_skills.Length];
            for (int i = 0; i < _skills.Length; i++)
            {
                int index = i;
                _skillActions[i] = () => UseSkill(index);
                _skillButtons[i].onClick.AddListener(_skillActions[i]);
            }
            _investmentButton.onClick.AddListener(CycleInvestment);
            _defendButton.onClick.AddListener(Defend);
            _continueButton.onClick.AddListener(ReturnToExploration);
            if (_targetButtons != null)
            {
                _targetActions = new UnityEngine.Events.UnityAction[_targetButtons.Length];
                for (int i = 0; i < _targetButtons.Length; i++)
                {
                    int index = i;
                    _targetActions[i] = () => SelectTarget(index);
                    if (_targetButtons[i] != null)
                    {
                        _targetButtons[i].onClick.AddListener(_targetActions[i]);
                    }
                }
            }
            _battleUI.SetActive(false);
            _notice.text = _enemies.Any(enemy => enemy != null)
                ? "WASD 이동 · 가까운 몬스터에 좌클릭 선제 진입 · Q 소리"
                : "WASD 이동 · SHIFT 달리기";
        }

        private void Update()
        {
            if (HandleCheatKeys())
            {
                return;
            }
            if (!IsInBattle)
            {
                UpdateCameraPreview();
                if (_cameraPreview != null)
                {
                    return;
                }
                if (_automaticEncounters)
                {
                    UpdateExploration();
                }
                return;
            }
            if (_enableCheats && Application.isFocused && Keyboard.current != null
                && Keyboard.current[_rageCheatKey].wasPressedThisFrame)
            {
                DebugFillRage();
            }
            if (_cameraTime < _cameraEntrySeconds)
            {
                return;
            }
            Mouse targetingMouse = Mouse.current;
            if (CanInput(Snapshot) && Application.isFocused && targetingMouse != null
                && targetingMouse.leftButton.wasPressedThisFrame
                && (UnityEngine.EventSystems.EventSystem.current == null
                    || !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()))
            {
                TrySelectBattleTargetAtScreenPoint(targetingMouse.position.ReadValue());
            }
            _elapsed += Time.deltaTime;
            BattleSnapshot snapshot = Snapshot;
            if (snapshot != null && snapshot.Phase == BattlePhase.Finished)
            {
                ReturnToExploration();
                return;
            }
            if (snapshot.Phase == BattlePhase.AwaitingPresentation)
            {
                BattleActionResult pending = _session.PendingResult;
                // 시퀀스가 재생 중인 행동은 시퀀스가 시전자 이동·포즈를 맡고, 피격 대상 리액션은 계속 재생한다.
                bool isSequence = pending.ActionId == _sequenceActionId;
                for (int i = 0; i < _presentations.Length; i++)
                {
                    if (pending.Request.ActorId == "player" && pending.Request.TargetId == GetMonsterId(i))
                    {
                        bool approach = !isSequence && (pending.ActionId != _playerMotionActionId || _playerMotionApproaches);
                        _presentations[i].Sample(pending, _elapsed / PresentationSeconds,
                            approach, PresentationSeconds, playAnimation: !isSequence);
                    }
                    else if (pending.Request.ActorId == GetMonsterId(i) && pending.Request.TargetId == "player")
                    {
                        _monsterPresentations[i].Sample(pending, _elapsed / PresentationSeconds,
                            approachTarget: !isSequence, presentationSeconds: PresentationSeconds, playAnimation: !isSequence);
                    }
                }
            }
            // 컷신이 끝나기 전에는 결과 연출을 끝내지 않는다(컷신 길이와 연출 시간이 달라도 컷신이 잘리지 않게).
            if (_elapsed >= PresentationSeconds && snapshot.Phase == BattlePhase.AwaitingPresentation && !IsSkillCutscenePlaying)
            {
                _session.CompletePresentation(_session.PendingResult.ActionId);
                _cachedSnapshot = null;
                _elapsed = 0;
                RefreshUI();
            }
            else if (_elapsed >= _actionSeconds && snapshot.Phase == BattlePhase.AwaitingAction
                && snapshot.CurrentActorId != "player" && _ai.TryChooseAction(_session, out BattleActionRequest request))
            {
                Submit(request);
            }
        }

        private void LateUpdate()
        {
            if (!IsInBattle)
            {
                _targetHighlight.Clear();
                UpdateCameraPreview();
                return;
            }
            UpdateTargetHighlight();
            bool wasEntering = _cameraTime < _cameraEntrySeconds;
            _cameraTime += Time.deltaTime;
            if (wasEntering && _cameraTime >= _cameraEntrySeconds)
            {
                RefreshUI();
            }
            SetBattleCameraActive(_debugBattleCamera);
            // 컷신 재생 중에는 Cinemachine Brain이 카메라를 쥐고 있으므로 전투 카메라를 움직이지 않는다.
            if (IsSkillCutscenePlaying || !_isBattleCameraActive || UpdateCinematicCamera())
            {
                return;
            }
            float previousYaw = _battleYaw;
            float previousPitch = _battlePitch;
            ReadBattleCameraInput();
            BattleActionResult result = _session.PendingResult;
            // 플레이어 피격 중에는 공격에 따른 카메라 이동·줌을 적용하지 않는다.
            bool isAttack = result != null && !result.WasSkipped && !result.IsTurnStartEffect
                && result.Request.Kind == BattleActionKind.Skill && result.Request.ActorId == "player";
            float progress = isAttack ? Mathf.Clamp01(_elapsed / PresentationSeconds) : 0f;
            float direction = isAttack && result.Request.ActorId != "player" ? -1f : 1f;
            float opening = 1f - Mathf.SmoothStep(0f, 1f, _cameraTime / _cameraEntrySeconds);
            Vector3 offset = TutorialBattleCamera.GetMotionOffset(ConfiguredCameraOffset,
                _cameraTime, opening, progress, direction, _cameraDrift, _cameraActionTravel);
            float fieldOfView = _battleFieldOfView - 2f * Mathf.Sin(progress * Mathf.PI);
            Pose target;
            if (!TryGetGroupPose(_playerHome, _enemyCenter, _formation.EnemySlots, offset, fieldOfView, out target))
            {
                target = _battleCameraPose;
                fieldOfView = _battleFieldOfView;
            }
            Pose orbitPose;
            if (!TutorialBattleCamera.TryGetOrbitPose(target, _playerHome, _enemyCenter,
                _movement.transform, _encounter.transform,
                _battleYaw, _battlePitch, fieldOfView, _camera.aspect, out orbitPose)
                || !IsGroupFramed(orbitPose, _formation.EnemySlots, fieldOfView)
                || !TutorialBattleCamera.CanTravel(_camera.transform.position, orbitPose.position,
                    _movement.transform, _encounter.transform))
            {
                _battleYaw = previousYaw;
                _battlePitch = previousPitch;
                return;
            }
            target = orbitPose;
            float blend = 1f - Mathf.Exp(-6f * Time.deltaTime);
            Vector3 position = Vector3.Lerp(_camera.transform.position, target.position, blend);
            if (TutorialBattleCamera.CanTravel(_camera.transform.position, position,
                _movement.transform, _encounter.transform))
            {
                _camera.transform.SetPositionAndRotation(position,
                    Quaternion.Slerp(_camera.transform.rotation, target.rotation, blend));
                _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, fieldOfView, blend);
            }
        }

        // 플레이어 행동 모션에 연출 카메라가 있으면(궁극기의 Ult_cam) 재생하는 동안 그 시점으로 바꾸고, 참을 돌려준다.
        // 모션이 끝나면 연출 직전의 전투 시점으로 되돌리고 거짓을 돌려줘, 기존 전투 카메라가 그 시점에서 이어서 움직인다.
        private bool UpdateCinematicCamera()
        {
            Pose pose = default;
            float horizontalFieldOfView = 0f;
            if (_battleAnimation != null && _battleAnimation.TryGetCinematicCamera(out pose, out horizontalFieldOfView))
            {
                if (!_isCinematicCameraActive)
                {
                    _isCinematicCameraActive = true;
                    _cinematicReturnPose = new Pose(_camera.transform.position, _camera.transform.rotation);
                    _cinematicReturnFieldOfView = _camera.fieldOfView;
                }
                // 모션의 컷 전환을 그대로 보여 주도록 따라가지 않고 바로 맞춘다.
                _camera.transform.SetPositionAndRotation(pose.position - pose.rotation * Vector3.forward * _cinematicPullback,
                    pose.rotation);
                if (horizontalFieldOfView > 0f)
                {
                    _camera.fieldOfView = Camera.HorizontalToVerticalFieldOfView(
                        Mathf.Min(120f, horizontalFieldOfView * _cinematicFieldOfViewScale), _camera.aspect);
                }
                return true;
            }

            if (_isCinematicCameraActive)
            {
                _isCinematicCameraActive = false;
                _camera.transform.SetPositionAndRotation(_cinematicReturnPose.position, _cinematicReturnPose.rotation);
                _camera.fieldOfView = _cinematicReturnFieldOfView;
            }
            return false;
        }

        private void OnValidate()
        {
            MigrateCameraSettings();
            _battleCameraDistance = Mathf.Max(0.1f, _battleCameraDistance);
        }

        private void MigrateCameraSettings()
        {
            if (_cameraSettingsMigrated)
            {
                return;
            }
            _battleCameraOffsetXY = new Vector2(_battleCameraOffset.x, _battleCameraOffset.y);
            _battleCameraDistance = Mathf.Max(0.1f, -_battleCameraOffset.z);
            _cameraSettingsMigrated = true;
        }

        private void UpdateCameraPreview()
        {
            if (!_debugBattleCamera)
            {
                EndCameraPreview();
                return;
            }
            if (_cameraPreview == null)
            {
                _cameraPreview = new TutorialBattleCameraPreview(_camera, _brain, _orbit, _movement);
            }
            _cameraPreview.Show(_debugCameraTarget, _battleSpacing, ConfiguredCameraOffset, _battleFieldOfView);
        }

        private void EndCameraPreview()
        {
            _cameraPreview?.Restore();
            _cameraPreview = null;
        }

        private void SetBattleCameraActive(bool active)
        {
            if (_isBattleCameraActive == active)
            {
                return;
            }
            _isBattleCameraActive = active;
            _isCameraDragging = false;
            _isCinematicCameraActive = false;
            if (active)
            {
                _orbit.enabled = false;
                _brain.enabled = false;
                _camera.orthographic = false;
                _camera.fieldOfView = _battleFieldOfView;
                _camera.transform.SetPositionAndRotation(_battleCameraPose.position, _battleCameraPose.rotation);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                _camera.transform.SetPositionAndRotation(_cameraPosition, _cameraRotation);
                _camera.fieldOfView = _cameraFieldOfView;
                _camera.orthographic = _cameraWasOrthographic;
                _brain.enabled = _brainEnabled;
                _orbit.enabled = _orbitEnabled;
            }
        }

        private void RefreshPlayerData()
        {
            if (!_characters.TryGet(_characterId, out CharacterData character))
            {
                throw new InvalidOperationException("Character_DT: 캐릭터 ID " + _characterId + "를 찾을 수 없습니다.");
            }
            int victories = _playerState.CharacterId == character.Id ? _playerState.VictoryCount : 0;
            _enhancementRules = _basicSkillEnhancement.CreateRules();
            _battleSkillStage = _testSkillStage > 0 ? _testSkillStage : _enhancementRules.GetStage(victories);
            _skills = _basicSkillEnhancement.CreateSkillsForStage(_baseSkills, _battleSkillStage);
            if (_skills.Length != _skillButtons.Length)
            {
                throw new InvalidOperationException("Skill_DT: 현재 단계 스킬 수와 버튼 수가 다릅니다.");
            }
            _playerData = new CombatantData(character.Id, character.Name, BattleTeam.Player,
                checked((int)character.Hp), checked((int)character.BaseMemory), checked((int)character.BaseMemory),
                _skills, evasion: character.EvasionRate, baseCriticalChance: character.BaseCriticalChance,
                criticalDamageMultiplier: character.CriticalDamage);
        }

        private void ReadBattleCameraInput()
        {
            Mouse mouse = Mouse.current;
            bool canDrag = Application.isFocused && mouse != null && mouse.rightButton.isPressed
                && _cameraTime >= _cameraEntrySeconds;
            if (!canDrag)
            {
                if (_isCameraDragging)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                _isCameraDragging = false;
                return;
            }
            if (!_isCameraDragging)
            {
                _isCameraDragging = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                return;
            }
            Vector2 delta = mouse.delta.ReadValue();
            _battleYaw = Mathf.Repeat(_battleYaw + delta.x * _battleMouseSensitivity + 180f, 360f) - 180f;
            _battlePitch = Mathf.Clamp(_battlePitch - delta.y * _battleMouseSensitivity, -10f, 30f);
        }

        private void UpdateExploration()
        {
            Vector3 position = _movement.transform.position;
            if (Time.time >= _nextFootstep && Vector3.Distance(position, _lastPlayerPosition) >= 0.7f)
            {
                EnemyNoise.Emit(position, _movement.IsSprinting ? _sprintNoiseRadius : _walkNoiseRadius, _movement.gameObject);
                _lastPlayerPosition = position;
                _nextFootstep = Time.time + 0.4f;
            }
            Keyboard keyboard = Keyboard.current;
            if (!Application.isFocused || Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }
            // Space는 점프(PlayerMovement)라 유인 소리는 Q로 낸다.
            if (keyboard != null && keyboard.qKey.wasPressedThisFrame)
            {
                EnemyNoise.Emit(position, 12f, _movement.gameObject);
            }
            EnemyStateMachine nearest = FindNearbyFieldEnemy(2.5f);
            if (nearest == null)
            {
                return;
            }
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame
                && (UnityEngine.EventSystems.EventSystem.current == null
                    || !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()))
            {
                TryInitiateNearbyBattle();
            }
            else if (Vector3.Distance(nearest.transform.position, position) <= 1.15f)
            {
                BeginBattle(nearest, BattleEntryCondition.MonsterCollision);
            }
        }

        public bool CanInitiateNearbyBattle => FindNearbyFieldEnemy(2.5f) != null;

        public bool TryInitiateNearbyBattle()
        {
            EnemyStateMachine enemy = FindNearbyFieldEnemy(2.5f);
            return enemy != null && BeginBattle(enemy, BattleEntryCondition.PlayerInitiated);
        }

        private EnemyStateMachine FindNearbyFieldEnemy(float range)
        {
            if (!enabled || IsInBattle || !_movement.isActiveAndEnabled || Time.time < _encounterCooldown || IsFieldEventPlaying())
            {
                return null;
            }
            Vector3 position = _movement.transform.position;
            EnemyStateMachine nearest = null;
            float nearestDistanceSquared = range * range;
            foreach (EnemyStateMachine enemy in _enemies)
            {
                if (enemy == null || !enemy.isActiveAndEnabled)
                {
                    continue;
                }
                float distanceSquared = (enemy.transform.position - position).sqrMagnitude;
                if (distanceSquared > nearestDistanceSquared)
                {
                    continue;
                }
                Vector3 start = position + Vector3.up * 0.8f;
                Vector3 end = enemy.transform.position + Vector3.up * 0.8f;
                if (Physics.Linecast(start, end, out RaycastHit obstacle, ~0, QueryTriggerInteraction.Ignore)
                    && !obstacle.transform.IsChildOf(enemy.transform) && !obstacle.transform.IsChildOf(_movement.transform))
                {
                    continue;
                }
                nearest = enemy;
                nearestDistanceSquared = distanceSquared;
            }
            return nearest;
        }

        public bool BeginBattle(EnemyStateMachine enemy, BattleEntryCondition entry,
            IReadOnlyList<EnemyStateMachine> members = null)
        {
            if (!enabled || _playerData == null || IsInBattle || Time.time < _encounterCooldown
                || enemy == null || !enemy.isActiveAndEnabled || !_enemies.Contains(enemy)
                || IsFieldEventPlaying())
            {
                return false;
            }
            // 조우 그룹(EncounterFollower)이 있으면 리더와 동료를 한 전투에 넣는다.
            if (members == null)
            {
                members = CK.SemesterProject.LevelDesign.EncounterFollower.GetEncounterMembers(enemy);
            }
            if (members != null && (members.Count < 1 || members.Count > 3 || members[0] != enemy
                || members.Distinct().Count() != members.Count
                || members.Any(member => member == null || !member.isActiveAndEnabled || !_enemies.Contains(member))))
            {
                return false;
            }
            // 미리보기에서 진입해도 실제 탐색 카메라와 이동 상태를 복원 대상으로 저장한다.
            EndCameraPreview();
            _encounterMembers = members != null ? members.ToArray() : new[] { enemy }.Concat(_enemies
                .Where(other => other != null && other != enemy && other.isActiveAndEnabled)
                .Distinct().OrderBy(other => (other.transform.position - enemy.transform.position).sqrMagnitude))
                .Take(Mathf.Clamp(_encounterSize, 1, 3)).ToArray();
            var participants = new List<BattleParticipant>();
            try
            {
                // 강화는 전투 시작에만 반영해 진행 중인 전투의 스킬 계산을 고정한다.
                RefreshPlayerData();
                participants.Add(new BattleParticipant("player", _playerData,
                    initialHp: _playerState.Hp, initialMemory: _playerState.Memory));
                for (int i = 0; i < _encounterMembers.Length; i++)
                {
                    participants.Add(new BattleParticipant(GetMonsterId(i), GetMonsterData(_encounterMembers[i])));
                }
            }
            catch (Exception exception)
            {
                Debug.LogError("전투 데이터 오류: " + exception.Message, enemy);
                _encounterMembers = Array.Empty<EnemyStateMachine>();
                return false;
            }
            var session = new BattleSession(rules: new BattleRules(monsterCollisionMemoryBonus: _collisionMemoryBonus))
            {
                PlayerHasUnlimitedResources = _unlimitedPlayerResources
            };
            session.Start(participants, entry, entry == BattleEntryCondition.PlayerInitiated ? "player"
                : entry == BattleEntryCondition.MonsterCollision ? GetMonsterId(0) : null);
            MoveToBattleArena();
            var formation = new TutorialBattleFormation(_characterController,
                _encounterMembers.Select(member => member.GetComponent<NavMeshAgent>()).ToArray());
            _encounter = enemy;
            SetBackgroundEnemiesHidden(true);
            if (!TryPlaceFormation(formation, out Pose battlePose))
            {
                ReturnFromBattleArena(false);
                SetBackgroundEnemiesHidden(false);
                _encounterMembers = Array.Empty<EnemyStateMachine>();
                _encounterCooldown = Time.time + 1;
                Debug.LogWarning("전투 자리를 확보할 수 없습니다. 넓은 공간에서 다시 진입하세요.", enemy);
                return false;
            }
            _session = session;
            _debugBattleCamera = true;
            _isBattleCameraActive = true;
            _damagePredictor = new BattleActionResolver(session.Rules);
            _hasRecordedBattleResult = false;
            _cachedSnapshot = null;
            _battleYaw = 0f;
            _battlePitch = 0f;
            _isCameraDragging = false;
            _formation = formation;
            _presentations = _encounterMembers.Select((member, index) =>
                new TutorialBattleActionPresentation(_movement.transform, member.transform, 1.6f, GetMonsterId(index),
                    enhancement: _basicSkillEnhancement, skillStage: _battleSkillStage)).ToArray();
            _monsterPresentations = _encounterMembers.Select((member, index) =>
                new TutorialBattleActionPresentation(member.transform, _movement.transform, 1.6f, "player", GetMonsterId(index),
                    enhancement: _basicSkillEnhancement, skillStage: _battleSkillStage)).ToArray();
            _playerHome = _movement.transform.position;
            _enemyCenter = Vector3.zero;
            foreach (Vector3 slot in formation.EnemySlots)
            {
                _enemyCenter += slot / formation.EnemySlots.Length;
            }
            _targetId = GetMonsterId(0);
            HasSelectedBattleTarget = false;
            _encounter = enemy;
            _investment = 0;
            _elapsed = 0;
            _fieldUIWasActive = _fieldUI.activeSelf;
            _fieldUI.SetActive(false);
            _movementEnabled = _movement.enabled;
            _orbitEnabled = _orbit.enabled;
            _brainEnabled = _brain.enabled;
            _movement.enabled = false;
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                _animatorSpeed = _animator.speed;
                // 이동을 끄면 PlayerMovement가 멈추고, PlayerBattleAnimation이 전투 대기 모션을 반복한다.
                // 전투 모션이 없는 캐릭터는 PlayerMovement의 Idle 대기 모션을 그대로 반복한다.
                _animator.speed = 1f;
                if (_battleAnimation != null)
                {
                    _battleAnimation.EnterBattle();
                }
                _animator.Update(0f);
            }
            _orbit.enabled = false;
            SetEnemiesPaused(true);
            SetBackgroundEnemiesHidden(true);
            _cameraPosition = _camera.transform.position;
            _cameraRotation = _camera.transform.rotation;
            _cameraFieldOfView = _camera.fieldOfView;
            _cameraWasOrthographic = _camera.orthographic;
            _brain.enabled = false;
            _camera.orthographic = false;
            _camera.fieldOfView = _battleFieldOfView;
            _battleCameraPose = battlePose;
            _cameraTime = 0f;
            Pose openingPose;
            Vector3 openingOffset = TutorialBattleCamera.GetMotionOffset(ConfiguredCameraOffset,
                0f, 1f, 0f, 1f, _cameraDrift, _cameraActionTravel);
            if (TryGetGroupPose(_playerHome, _enemyCenter, _formation.EnemySlots, openingOffset, _battleFieldOfView, out openingPose)
                && TutorialBattleCamera.CanTravel(openingPose.position, battlePose.position,
                    _movement.transform, enemy.transform))
            {
                battlePose = openingPose;
            }
            _camera.transform.SetPositionAndRotation(battlePose.position, battlePose.rotation);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _battleUI.SetActive(true);
            _notice.text = entry == BattleEntryCondition.PlayerInitiated ? "플레이어 선제 행동 +1"
                : entry == BattleEntryCondition.MonsterCollision ? enemy.name + " 선제 행동 +1" : "전투 진입";
            RefreshUI();
            return true;
        }

        public bool TryGetMonsterTransform(string instanceId, out Transform monster)
        {
            monster = null;
            if (!IsInBattle || string.IsNullOrEmpty(instanceId))
            {
                return false;
            }
            for (int index = 0; index < _encounterMembers.Length; index++)
            {
                if (instanceId == GetMonsterId(index) && _encounterMembers[index] != null)
                {
                    monster = _encounterMembers[index].transform;
                    return true;
                }
            }
            return false;
        }

        private static string GetMonsterId(int index)
        {
            return index == 0 ? "monster" : "monster_" + index;
        }

        private CombatantData GetMonsterData(EnemyStateMachine enemy)
        {
            TutorialMonster definition = enemy.GetComponent<TutorialMonster>();
            if (definition != null)
            {
                return definition.GetData();
            }
            var skill = new SkillData("monster_attack", "공격", 90);
            return new CombatantData("tutorial_box", enemy.name, BattleTeam.Monster,
                _monsterHp, _monsterMemory, _monsterMemory, new[] { skill }, BattleElement.Imprint,
                weaknessChain: new[] { BattleElement.Afterimage, BattleElement.Imprint, BattleElement.Oblivion, BattleElement.Afterimage });
        }

        public bool TrySelectBattleTargetAtScreenPoint(Vector2 screenPoint)
        {
            if (!CanInput(Snapshot))
            {
                return false;
            }
            RaycastHit hit;
            if (!Physics.Raycast(_camera.ScreenPointToRay(screenPoint), out hit, _camera.farClipPlane,
                ~0, QueryTriggerInteraction.Ignore))
            {
                return false;
            }
            for (int index = 0; index < _encounterMembers.Length; index++)
            {
                if (hit.transform.IsChildOf(_encounterMembers[index].transform)
                    && Snapshot.Combatants.Any(unit => unit.InstanceId == GetMonsterId(index) && unit.Hp > 0))
                {
                    SelectTarget(index);
                    return true;
                }
            }
            return false;
        }

        // 테스트용 치트: 전투 중 플레이어 폭주 게이지를 채운다. 에디터·Development 빌드에서만 동작한다.
        public bool DebugFillRage()
        {
            if (!_enableCheats || _session == null || !_session.DebugSetRageEnergy("player", _rageCheatValue))
            {
                return false;
            }
            _cachedSnapshot = null;
            RefreshUI();
            return true;
        }

        // 테스트용 치트 키. 에디터와 Development 빌드에서만 동작한다. 씬을 다시 여는 경우 true를 돌려 이번 프레임을 끝낸다.
        private CK.SemesterProject.LevelDesign.LevelTriggerZone[] _cheatCheckpoints;
        private int _cheatCheckpointIndex = -1;

        private bool HandleCheatKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (!_enableCheats || keyboard == null || !Application.isFocused)
            {
                return false;
            }
            if (keyboard[_restartCheatKey].wasPressedThisFrame)
            {
                DebugRestartGame();
                return true;
            }
            if (keyboard[_healCheatKey].wasPressedThisFrame)
            {
                DebugRestorePlayer();
            }
            if (IsInBattle)
            {
                if (keyboard[_winCheatKey].wasPressedThisFrame)
                {
                    DebugWinBattle();
                }
            }
            else if (keyboard[_bossWarpCheatKey].wasPressedThisFrame)
            {
                DebugWarpToBoss();
            }
            else if (keyboard[_checkpointCheatKey].wasPressedThisFrame)
            {
                DebugWarpToCheckpoint(keyboard.shiftKey.isPressed ? -1 : 1);
            }
            return false;
        }

        // 체크포인트 구역(LevelTriggerZone, 체크포인트 켜짐)을 하이어라키 순서(구역 진행 순서)로 돌며 순간이동한다.
        // 구역 안으로 들어가므로 구역 진입 처리(위치 이름·목표·재시작 지점)도 그대로 실행된다.
        public void DebugWarpToCheckpoint(int step)
        {
            if (!_enableCheats || IsInBattle)
            {
                return;
            }
            if (_cheatCheckpoints == null)
            {
                _cheatCheckpoints = FindObjectsByType<CK.SemesterProject.LevelDesign.LevelTriggerZone>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .Where(zone => zone.IsCheckpoint)
                    .OrderBy(zone => GetHierarchyOrder(zone.transform), StringComparer.Ordinal)
                    .ToArray();
            }
            if (_cheatCheckpoints.Length == 0)
            {
                return;
            }
            int count = _cheatCheckpoints.Length;
            _cheatCheckpointIndex = ((_cheatCheckpointIndex < 0 && step < 0 ? 0 : _cheatCheckpointIndex + step) % count + count) % count;
            CK.SemesterProject.LevelDesign.LevelTriggerZone checkpoint = _cheatCheckpoints[_cheatCheckpointIndex];
            Bounds bounds = checkpoint.GetComponent<Collider>().bounds;
            Vector3 target = new Vector3(bounds.center.x, bounds.min.y + 0.1f, bounds.center.z);
            if (NavMesh.SamplePosition(target, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            {
                target = hit.position;
            }
            SetPlayerPose(target, _movement.transform.rotation);
            Physics.SyncTransforms();
        }

        /// <summary>
        /// 마지막 체크포인트(전투 패배 시 다시 시작하는 위치)로 플레이어를 옮긴다. 구멍에 떨어졌을 때 FallRecovery가 부른다.
        /// 전투 중이면 아무것도 하지 않고 false를 돌려준다.
        /// </summary>
        public bool RecoverToRespawnPoint()
        {
            if (IsInBattle || _movement == null || _characterController == null)
            {
                return false;
            }
            Vector3 target = _spawn;
            if (NavMesh.SamplePosition(target, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                target = hit.position;
            }
            SetPlayerPose(target, _movement.transform.rotation);
            _movement.ResetFall();
            _lastPlayerPosition = target;
            // 돌아온 자리 근처 몬스터와 바로 부딪혀 전투가 시작되지 않게 잠시 쉰다.
            _encounterCooldown = Mathf.Max(_encounterCooldown, Time.time + 2f);
            Physics.SyncTransforms();
            return true;
        }

        private static string GetHierarchyOrder(Transform transform)
        {
            var order = new System.Text.StringBuilder();
            for (Transform current = transform; current != null; current = current.parent)
            {
                order.Insert(0, current.GetSiblingIndex().ToString("D4") + "/");
            }
            return order.ToString();
        }

        public bool DebugWinBattle()
        {
            if (!_enableCheats || _session == null || _skillCutscene != null && _skillCutscene.IsPlaying || !_session.DebugWinBattle())
            {
                return false;
            }
            if (_sequenceRunner != null)
            {
                _sequenceRunner.Abort();
            }
            _cachedSnapshot = null;
            _elapsed = 0f;
            RefreshUI();
            return true;
        }

        public void DebugRestorePlayer()
        {
            if (!_enableCheats || _playerState == null)
            {
                return;
            }
            if (IsInBattle && _session != null && _session.DebugRestore("player"))
            {
                _cachedSnapshot = null;
                RefreshUI();
            }
            _playerState.RestoreFull();
        }

        public void DebugWarpToBoss()
        {
            if (!_enableCheats || IsInBattle)
            {
                return;
            }
            if (_bossWarpPoint == null)
            {
                GameObject point = GameObject.Find("Boss_RetryPoint");
                _bossWarpPoint = point != null ? point.transform : null;
            }
            if (_bossWarpPoint == null)
            {
                return;
            }
            SetPlayerPose(_bossWarpPoint.position, _bossWarpPoint.rotation);
            Physics.SyncTransforms();
        }

        public void DebugRestartGame()
        {
            if (!_enableCheats)
            {
                return;
            }
            Time.timeScale = 1f;
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (!CK.SemesterProject.UI.SceneFadeTransition.Run(scene, 0.4f, 0.6f, PlayerSessionState.ResetSession))
            {
                PlayerSessionState.ResetSession();
                UnityEngine.SceneManagement.SceneManager.LoadScene(scene);
            }
        }

        public void SelectTarget(int index)
        {
            if (!CanInput(Snapshot) || index < 0 || index >= _encounterMembers.Length)
            {
                return;
            }
            string id = GetMonsterId(index);
            if (Snapshot.Combatants.Any(unit => unit.InstanceId == id && unit.Hp > 0))
            {
                _targetId = id;
                HasSelectedBattleTarget = true;
                RefreshUI();
            }
        }

        // 필드 지형과 관계없이 넓은 전용 장소에서 전투하도록 참여자를 옮긴다. 장소가 없으면 기존처럼 현재 자리에서 배치한다.
        private void MoveToBattleArena()
        {
            if (_battleArena == null || _isInBattleArena)
            {
                return;
            }
            _arenaReturnPlayerPose = new Pose(_movement.transform.position, _movement.transform.rotation);
            _arenaReturnMemberPoses = _encounterMembers.Select(member => new Pose(member.transform.position, member.transform.rotation)).ToArray();
            Vector3 forward = Vector3.ProjectOnPlane(_battleArena.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 center = _battleArena.position;
            SetPlayerPose(center - forward * (_battleSpacing * 0.5f), Quaternion.LookRotation(forward));
            for (int i = 0; i < _encounterMembers.Length; i++)
            {
                float sideOffset = (i - (_encounterMembers.Length - 1) * 0.5f) * ArenaMemberSpacing;
                WarpEnemy(_encounterMembers[i], center + forward * (_battleSpacing * 0.5f) + right * sideOffset, Quaternion.LookRotation(-forward));
            }
            Physics.SyncTransforms();
            _isInBattleArena = true;
        }

        private void ReturnFromBattleArena(bool keepPlayerPosition)
        {
            if (!_isInBattleArena)
            {
                return;
            }
            _isInBattleArena = false;
            // 패배 시에는 Formation이 플레이어를 체크포인트로 옮겼으므로 플레이어 위치는 유지한다.
            if (!keepPlayerPosition)
            {
                SetPlayerPose(_arenaReturnPlayerPose.position, _arenaReturnPlayerPose.rotation);
            }
            for (int i = 0; i < _encounterMembers.Length && i < _arenaReturnMemberPoses.Length; i++)
            {
                if (_encounterMembers[i] != null)
                {
                    WarpEnemy(_encounterMembers[i], _arenaReturnMemberPoses[i].position, _arenaReturnMemberPoses[i].rotation);
                }
            }
            _arenaReturnMemberPoses = Array.Empty<Pose>();
            Physics.SyncTransforms();
        }

        private void SetPlayerPose(Vector3 position, Quaternion rotation)
        {
            bool wasEnabled = _characterController.enabled;
            _characterController.enabled = false;
            _movement.transform.SetPositionAndRotation(position, rotation);
            _characterController.enabled = wasEnabled;
            CinemachineCamera follow = _movement.GetComponentInChildren<CinemachineCamera>();
            if (follow != null)
            {
                follow.PreviousStateIsValid = false;
            }
        }

        internal static void WarpEnemy(EnemyStateMachine enemy, Vector3 position, Quaternion rotation)
        {
            NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.Warp(position);
            }
            else
            {
                enemy.transform.position = position;
            }
            enemy.transform.rotation = rotation;
        }

        // 좁은 방(튜토리얼 캡슐 구역 등)에서도 진입할 수 있도록 기본 간격으로 자리가 없으면 간격을 줄여 다시 찾는다.
        private bool TryPlaceFormation(TutorialBattleFormation formation, out Pose battlePose)
        {
            foreach (float scale in FallbackSpacingScales)
            {
                float spacing = Mathf.Max(MinimumBattleSpacing, _battleSpacing * scale);
                Pose candidatePose = default;
                if (formation.TryPlace(spacing, (playerSlot, enemySlot) => TryGetGroupPose(
                    playerSlot, enemySlot, formation.EnemySlots, ConfiguredCameraOffset,
                    _battleFieldOfView, out candidatePose)))
                {
                    battlePose = candidatePose;
                    return true;
                }
            }
            battlePose = default;
            return false;
        }

        private bool TryGetGroupPose(Vector3 playerSlot, Vector3 enemyCenter, Vector3[] slots,
            Vector3 offset, float fieldOfView, out Pose pose)
        {
            for (int step = 0; step < 5; step++)
            {
                Vector3 adjusted = offset + new Vector3(step * 0.3f, step * 0.15f, -step * 0.65f);
                if (TutorialBattleCamera.TryGetPose(playerSlot, enemyCenter, _movement.transform,
                    _encounter.transform, adjusted, fieldOfView, _camera.aspect, out pose)
                    && IsGroupFramed(pose, slots, fieldOfView))
                {
                    return true;
                }
            }
            pose = default;
            return false;
        }

        private bool IsGroupFramed(Pose pose, Vector3[] slots, float fieldOfView)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (!TutorialBattleCamera.IsFramed(slots[i] + Vector3.up * 0.2f, pose, fieldOfView,
                    _camera.aspect, _movement.transform, _encounterMembers[i].transform)
                    || !TutorialBattleCamera.IsFramed(slots[i] + Vector3.up * 1.5f, pose, fieldOfView,
                    _camera.aspect, _movement.transform, _encounterMembers[i].transform))
                {
                    return false;
                }
            }
            return true;
        }

        public void UseSkill(int index)
        {
            BattleSnapshot state = Snapshot;
            if (!CanInput(state) || index < 0 || index >= _skills.Length)
            {
                return;
            }
            Submit(new BattleActionRequest(state.TurnId, "player", BattleActionKind.Skill,
                _skills[index].Id, _targetId, investmentStage: _investment));
        }

        public void Defend()
        {
            BattleSnapshot state = Snapshot;
            if (CanInput(state))
            {
                Submit(new BattleActionRequest(state.TurnId, "player", BattleActionKind.Defend, investmentStage: _investment));
            }
        }

        public void CycleInvestment()
        {
            if (CanInput(Snapshot))
            {
                _investment = (_investment + 1) % 6;
                RefreshUI();
            }
        }

        private void Submit(BattleActionRequest request)
        {
            if (!_session.TrySubmit(request, out BattleActionResult result, out BattleActionError error))
            {
                _notice.text = error == BattleActionError.InsufficientMemory ? "메모리가 부족합니다. 투자 단계를 줄이거나 방어하세요."
                    : error == BattleActionError.InsufficientRage ? "폭주 에너지가 부족합니다." : "현재 행동할 수 없습니다.";
                return;
            }
            _cachedSnapshot = null;
            _elapsed = 0;
            RefreshUI();
            bool startedCutscene = request.ActorId == "player" && request.Kind == BattleActionKind.Skill
                && !result.WasSkipped && TryPlaySkillCutscene(request, result);
            bool startedSequence = !startedCutscene && request.Kind == BattleActionKind.Skill
                && !result.WasSkipped && !result.IsTurnStartEffect && TryPlaySkillSequence(request, result);
            startedCutscene |= startedSequence;
            if (!startedCutscene && request.ActorId == "player" && request.Kind == BattleActionKind.Skill && !result.WasSkipped
                && _battleAnimation != null)
            {
                _playerMotionSeconds = _battleAnimation.PlaySkill(request.SkillId, out _playerMotionApproaches);
                _playerMotionActionId = _playerMotionSeconds > 0f ? result.ActionId : -1;
            }
            if (!startedCutscene && request.ActorId == "player" && request.Kind == BattleActionKind.Skill && !result.WasSkipped)
            {
                foreach (SkillCinematicBinding binding in _skillCinematics)
                {
                    if (binding.Cinematic != null && binding.SkillId == request.SkillId)
                    {
                        // 연출이 끝날 때까지 결과 연출(피해 반영·다음 행동)을 기다린다.
                        _playerMotionSeconds = Mathf.Max(_playerMotionSeconds, binding.Cinematic.Play(_camera));
                        _playerMotionActionId = result.ActionId;
                        break;
                    }
                }
            }
            string actor = Snapshot.Combatants.First(unit => unit.InstanceId == request.ActorId).Data.DisplayName;
            _notice.text = result.WasSkipped ? actor + " 행동 불능" : request.Kind == BattleActionKind.Defend ? actor + " 방어"
                : result.Hit != null && !result.Hit.IsHit ? actor + " 공격 빗나감" : actor + " 행동 완료";
            foreach (BattleStateChange change in result.Changes)
            {
                if (change.HpDelta < 0)
                {
                    _notice.text += " · 피해 " + -change.HpDelta;
                }
            }
            RefreshUI();
        }

        // 스킬에 완성 컷신이 지정돼 있으면 컷신 컴포넌트가 재생한다. 컷신이 카메라·플레이어를 쥐므로 기존 접근·모션은 쓰지 않는다.
        private bool TryPlaySkillCutscene(BattleActionRequest request, BattleActionResult result)
        {
            if (_skillCutscene == null)
            {
                return false;
            }
            TryGetMonsterTransform(string.IsNullOrEmpty(request.TargetId) ? _targetId : request.TargetId, out Transform target);
            if (!_skillCutscene.TryPlay(request.SkillId, target, out float seconds))
            {
                return false;
            }
            // 컷신이 끝날 때까지 결과 연출(피해 반영·다음 행동)을 기다린다.
            _playerMotionSeconds = seconds;
            _playerMotionActionId = result.ActionId;
            _playerMotionApproaches = false;
            return true;
        }

        // 스튜디오에서 만든 스킬 시퀀스가 연결돼 있으면 재생한다. 플레이어 스킬은 스킬 ID, 몬스터 공격은 몬스터 정의·속성으로 찾는다.
        private bool TryPlaySkillSequence(BattleActionRequest request, BattleActionResult result)
        {
            if (_sequenceRunner == null)
            {
                return false;
            }
            SkillSequenceAsset sequence;
            Transform caster;
            var targets = new List<Transform>();
            int selected = 0;
            if (request.ActorId == "player")
            {
                sequence = _sequenceRunner.FindPlayerSequence(request.SkillId);
                if (sequence == null)
                {
                    return false;
                }
                caster = _movement.transform;
                string targetId = string.IsNullOrEmpty(request.TargetId) ? _targetId : request.TargetId;
                for (int i = 0; i < _encounterMembers.Length; i++)
                {
                    EnemyStateMachine member = _encounterMembers[i];
                    if (member == null || !member.gameObject.activeInHierarchy)
                    {
                        continue;
                    }
                    if (GetMonsterId(i) == targetId)
                    {
                        selected = targets.Count;
                    }
                    targets.Add(member.transform);
                }
            }
            else
            {
                if (!TryGetMonsterTransform(request.ActorId, out caster))
                {
                    return false;
                }
                TutorialMonster monster = caster.GetComponent<TutorialMonster>();
                sequence = _sequenceRunner.FindMonsterSequence(monster != null ? monster.Definition : null);
                if (sequence == null)
                {
                    return false;
                }
                targets.Add(_movement.transform);
            }
            // 시퀀스가 끝날 때까지 결과 연출(피해 반영·다음 행동)을 기다린다.
            _playerMotionSeconds = _sequenceRunner.Play(sequence, caster, targets, selected, casterIsMonster: request.ActorId != "player");
            _playerMotionActionId = result.ActionId;
            _playerMotionApproaches = false;
            _sequenceActionId = result.ActionId;
            return true;
        }

        private bool CanInput(BattleSnapshot state)
        {
            return _cameraTime >= _cameraEntrySeconds && state != null && state.Phase == BattlePhase.AwaitingAction && state.CurrentActorId == "player";
        }

        private void RefreshUI()
        {
            BattleSnapshot state = Snapshot;
            CombatantState player = state.Combatants.First(unit => unit.InstanceId == "player");
            _playerState.ApplyBattleState(player);
            CombatantState[] monsters = state.Combatants.Where(unit => unit.Data.Team == BattleTeam.Monster).ToArray();
            if (!monsters.Any(unit => unit.InstanceId == _targetId && unit.Hp > 0))
            {
                _targetId = monsters.FirstOrDefault(unit => unit.Hp > 0)?.InstanceId;
            }
            for (int i = 0; i < _encounterMembers.Length; i++)
            {
                if (state.Phase != BattlePhase.AwaitingPresentation && monsters[i].Hp <= 0)
                {
                    _encounterMembers[i].gameObject.SetActive(false);
                }
                if (_targetButtons == null || i >= _targetButtons.Length || _targetButtons[i] == null)
                {
                    continue;
                }
                _targetButtons[i].gameObject.SetActive(true);
                _targetButtons[i].interactable = CanInput(state) && monsters[i].Hp > 0;
                TMP_Text label = _targetButtons[i].GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.text = (_targetId == monsters[i].InstanceId ? "▶ " : "") + (i + 1) + ". "
                        + monsters[i].Data.DisplayName + " [" + TutorialMonster.GetElementName(monsters[i].Data.Element) + "]"
                        + (monsters[i].Hp <= 0 ? " (사망)" : "");
                }
            }
            if (_targetButtons != null)
            {
                for (int i = _encounterMembers.Length; i < _targetButtons.Length; i++)
                {
                    if (_targetButtons[i] != null)
                    {
                        _targetButtons[i].gameObject.SetActive(false);
                    }
                }
            }
            _hpBar.maxValue = player.Data.MaxHp;
            _hpBar.value = player.Hp;
            _status.text = "HP " + player.Hp + "/" + player.Data.MaxHp + "  메모리 " + player.Memory + "  폭주 " + player.RageEnergy
                + (player.ImprintDamage > 0 ? "  각인 " + player.ImprintDamage : "")
                + (player.HasAfterimageRecovery ? "  다음 방어 회복" : "") + (player.HasMemoryLoss ? "  메모리 고갈" : "");
            _enemyStatus.text = string.Join("\n", monsters.Select((monster, index) =>
                (monster.InstanceId == _targetId ? "▶ " : "") + (index + 1) + ". " + monster.Data.DisplayName
                + " [" + TutorialMonster.GetElementName(monster.Data.Element) + "]  HP " + monster.Hp + "/" + monster.Data.MaxHp
                + "  메모리 " + monster.Memory + "  연쇄 " + monster.ChainStep + "/4"
                + (monster.ImprintDamage > 0 ? "  각인 " + monster.ImprintDamage : "")
                + (monster.HasAfterimageRecovery ? "  다음 방어 회복" : "")))
                + "\n순서: " + string.Join(" → ", state.TurnOrder.Select(id =>
                    state.Combatants.First(unit => unit.InstanceId == id).Data.DisplayName));
            _investmentLabel.text = "투자 " + _investment + "단계 (" + _session.Rules.GetStageCost(_playerData, _investment) + ")";
            for (int i = 0; i < _skillButtons.Length; i++)
            {
                int cost = _session.Rules.GetStageCost(_playerData, _investment) + _skills[i].MemoryCost;
                _skillButtons[i].interactable = CanInput(state) && player.Memory >= cost && player.RageEnergy >= _skills[i].RageCost;
                if (_skillLabels[i] != null)
                {
                    _skillLabels[i].text = (i < 3 ? TutorialMonster.GetElementName(_skills[i].Element)
                        + " " + _battleSkillStage + "단계" : _skills[i].DisplayName + (_skills[i].IsAreaOfEffect ? " (광역)" : ""))
                        + "\n위력 " + _skills[i].Power + " · M" + _skills[i].MemoryCost
                        + (_skills[i].RageCost > 0 ? " · 폭주-" + _skills[i].RageCost : " · 폭주+" + _skills[i].RageGain)
                        + "\n" + GetDamagePreview(state, _skills[i]);
                }
            }
            _investmentButton.interactable = CanInput(state);
            _defendButton.interactable = CanInput(state);
            _continueButton.gameObject.SetActive(false);
            if (state.Phase == BattlePhase.Finished)
            {
                if (!_hasRecordedBattleResult)
                {
                    _hasRecordedBattleResult = true;
                    _playerState.RecordBattleOutcome(state.Outcome);
                }
                _notice.text = state.Outcome == BattleOutcome.Victory ? "승리" : state.Outcome == BattleOutcome.Defeat ? "패배 · 시작 위치로 돌아갑니다" : "무승부";
                if (_testSkillStage > 0)
                {
                    _notice.text += " · 테스트 스킬 " + _testSkillStage + "단계 / 누적 " + _playerState.VictoryCount + "승";
                    return;
                }
                int nextStage = _enhancementRules.GetStage(_playerState.VictoryCount);
                int remaining = _enhancementRules.GetRemainingVictories(_playerState.VictoryCount);
                _notice.text += nextStage > _battleSkillStage ? " · 기본 스킬 " + nextStage + "단계 강화 (다음 전투부터)"
                    : remaining > 0 ? " · 누적 " + _playerState.VictoryCount + "승 / 다음 강화까지 " + remaining + "승"
                    : " · 기본 스킬 최대 단계";
            }
        }

        private void CreateAdditionalSkillButtons()
        {
            if (_baseSkills.Length == 11)
            {
                var buttons = _skillButtons.ToList();
                for (int i = 0; i < 2; i++)
                {
                    Button button = Instantiate(_skillButtons[0], _skillButtons[0].transform.parent);
                    button.name = i == 0 ? "Special Skill" : "Ultimate Skill";
                    button.onClick = new Button.ButtonClickedEvent();
                    RectTransform rect = button.GetComponent<RectTransform>();
                    RectTransform sourceRect = _skillButtons[i].GetComponent<RectTransform>();
                    rect.anchoredPosition = sourceRect.anchoredPosition + new Vector2(0, -55);
                    buttons.Add(button);
                }
                _skillButtons = buttons.ToArray();
            }
        }

        private void ConfigureSkillLayout()
        {
            _skillLabels = _skillButtons.Select(button => button.GetComponentInChildren<TMP_Text>(true)).ToArray();
            foreach (TMP_Text label in _skillLabels.Where(label => label != null))
            {
                label.fontSize = 16;
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(6, 4);
                label.rectTransform.offsetMax = new Vector2(-6, -4);
            }
            for (int i = 0; i < _skillButtons.Length; i++)
            {
                RectTransform rect = _skillButtons[i].GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(1, 0);
                rect.sizeDelta = new Vector2(280, 95);
                int column = i < 3 ? i : i - 3;
                rect.anchoredPosition = new Vector2(-735 + column * 290, i < 3 ? 150 : 50);
            }
            _investmentButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-435, 225);
            _defendButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-145, 225);
            _status.rectTransform.anchoredPosition = new Vector2(0, 275);
            _hpBar.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 305);
            _continueButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-240, 340);
            _notice.rectTransform.anchorMin = _notice.rectTransform.anchorMax = new Vector2(0, 1);
            _notice.rectTransform.anchoredPosition = new Vector2(350, -30);
            _notice.rectTransform.sizeDelta = new Vector2(660, 48);
            _notice.fontSize = 18;
        }

        private string GetDamagePreview(BattleSnapshot state, SkillData skill)
        {
            if (_damagePredictor == null || _targetId == null || state.Outcome != BattleOutcome.None)
            {
                return "대상 없음";
            }
            var request = new BattleActionRequest(state.TurnId, "player", BattleActionKind.Skill,
                skill.Id, _targetId, investmentStage: _investment);
            BattleActionError error = _damagePredictor.ResolveOutcome(state, request, true, false, out _, out _);
            if (error != BattleActionError.None)
            {
                return error == BattleActionError.InsufficientRage ? "폭주 부족" : "메모리 부족";
            }
            BattleHitResult[] normal = _damagePredictor.TargetHits.Values.ToArray();
            _damagePredictor.ResolveOutcome(state, request, true, true, out _, out _);
            BattleHitResult[] critical = _damagePredictor.TargetHits.Values.ToArray();
            CombatantState player = state.Combatants.First(unit => unit.InstanceId == "player");
            CombatantState target = state.Combatants.First(unit => unit.InstanceId == _targetId);
            string normalText = DamageRange(normal);
            string criticalText = DamageRange(critical);
            return (skill.IsAreaOfEffect ? "적별 " : "") + "명중 " + normalText + " / 치명 " + criticalText
                + "\n명중 " + (BattleActionResolver.GetHitChance(target, skill) * 100).ToString("0")
                + "% · 치명 " + (_damagePredictor.GetCriticalChance(player, skill) * 100).ToString("0") + "%";
        }

        private static string DamageRange(BattleHitResult[] hits)
        {
            if (hits.Length == 0)
            {
                return "0";
            }
            int min = hits.Min(hit => hit.Damage);
            int max = hits.Max(hit => hit.Damage);
            return min == max ? min.ToString() : min + "~" + max;
        }

        /// <summary>컷신·대사·연출 잠금 중이면 true. 이 동안 필드 몬스터는 멈추고 전투도 시작하지 않는다.</summary>
        private const float LevelProgressSearchInterval = 1f;
        private float _nextLevelProgressSearchTime;

        public bool IsFieldEventPlaying()
        {
            if (LevelDesign.CutsceneDialogueView.IsShowing || LevelDesign.CutscenePreset.IsAnyCutscenePlaying)
            {
                return true;
            }
            // LevelProgress가 없는 씬에서 매 프레임 씬 전체를 뒤지지 않도록 1초에 한 번만 다시 찾는다.
            if (_levelProgress == null && Time.unscaledTime >= _nextLevelProgressSearchTime)
            {
                _nextLevelProgressSearchTime = Time.unscaledTime + LevelProgressSearchInterval;
                _levelProgress = FindFirstObjectByType<LevelDesign.LevelProgress>();
            }
            return _levelProgress != null && _levelProgress.IsControlLocked;
        }

        public void SetEnemiesCutscenePaused(bool isPaused)
        {
            if (IsInBattle)
            {
                return;
            }
            // 컷신과 연출 잠금은 따로 끝나므로, 한쪽이 끝나도 다른 쪽이 남아 있으면 계속 멈춰 둔다.
            isPaused |= IsFieldEventPlaying();
            SetEnemiesPaused(isPaused);
            if (isPaused)
            {
                _encounterCooldown = Time.time + 1.5f;
            }
        }

        public void ReturnToExploration()
        {
            if (!IsInBattle || Snapshot.Phase != BattlePhase.Finished)
            {
                return;
            }
            BattleSnapshot state = Snapshot;
            CombatantState player = state.Combatants.First(unit => unit.InstanceId == "player");
            if (state.Outcome == BattleOutcome.Victory)
            {
                _playerState.ApplyBattleState(player);
                foreach (EnemyStateMachine member in _encounterMembers)
                {
                    member.gameObject.SetActive(false);
                }
            }
            else
            {
                _playerState.RestoreFull();
            }
            _lastHistory = _session.History;
            _session = null;
            HasSelectedBattleTarget = false;
            _encounterCooldown = Time.time + 3;
            RestoreExploration(state.Outcome != BattleOutcome.Victory, state.Outcome == BattleOutcome.Victory);
            _battleUI.SetActive(false);
            FieldBattleEnded?.Invoke(state.Outcome);
            _notice.text = _enemies.All(enemy => enemy == null || !enemy.gameObject.activeSelf) ? "모든 몬스터를 처치했습니다." : "WASD 이동 · 좌클릭 선제 진입 · Q 소리";
        }

        public void CancelBattle()
        {
            _debugBattleCamera = false;
            EndCameraPreview();
            if (IsInBattle)
            {
                RestoreExploration();
                _lastHistory = _session.History;
                _session = null;
                HasSelectedBattleTarget = false;
                _cachedSnapshot = null;
                _battleUI.SetActive(false);
            }
            _encounterCooldown = 0f;
        }

        // 선택한 대상 몬스터의 발밑 링·하이라이트를 갱신한다. 전투 카메라가 자리 잡은 뒤, 컷신이 아닐 때만 보인다.
        private void UpdateTargetHighlight()
        {
            Transform target = null;
            BattleElement element = default;
            if (!string.IsNullOrEmpty(_targetId) && TryGetMonsterTransform(_targetId, out target))
            {
                TutorialMonster monster = target.GetComponent<TutorialMonster>();
                if (monster != null && monster.Definition != null)
                {
                    element = monster.Definition.Element;
                }
            }
            bool visible = target != null && !IsSkillCutscenePlaying && _cameraTime >= _cameraEntrySeconds;
            _targetHighlight.UpdateHighlight(target, element, visible, CanInput(Snapshot));
        }

        private void RestoreExploration(bool respawn = false, bool defeated = false)
        {
            _targetHighlight.Clear();
            if (_skillCutscene != null)
            {
                _skillCutscene.Abort();
            }
            if (_sequenceRunner != null)
            {
                _sequenceRunner.Abort();
            }
            _sequenceActionId = -1;
            _debugBattleCamera = false;
            _isBattleCameraActive = false;
            // 필드에서는 HP를 보존하고 메모리만 최초값으로 회복한다.
            _playerState.RestoreFieldMemory();
            if (_isCameraDragging)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                _isCameraDragging = false;
            }
            _cachedSnapshot = null;
            foreach (TutorialBattleActionPresentation presentation in _presentations)
            {
                presentation.Restore();
            }
            _presentations = Array.Empty<TutorialBattleActionPresentation>();
            foreach (TutorialBattleActionPresentation presentation in _monsterPresentations)
            {
                presentation.Restore();
            }
            _monsterPresentations = Array.Empty<TutorialBattleActionPresentation>();
            _formation?.Restore(respawn ? (Vector3?)_spawn : null);
            ReturnFromBattleArena(respawn);
            _formation = null;
            if (_fieldUI != null)
            {
                _fieldUI.SetActive(_fieldUIWasActive);
            }
            _camera.transform.SetPositionAndRotation(_cameraPosition, _cameraRotation);
            _camera.fieldOfView = _cameraFieldOfView;
            _camera.orthographic = _cameraWasOrthographic;
            _brain.enabled = _brainEnabled;
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                _animator.speed = _animatorSpeed;
            }
            // 전투 모션을 비우면 다시 켜지는 PlayerMovement가 필드 모션으로 되돌린다.
            if (_battleAnimation != null)
            {
                _battleAnimation.ExitBattle();
            }
            _playerMotionActionId = -1;
            _playerMotionSeconds = 0f;
            _isCinematicCameraActive = false;
            _movement.enabled = _movementEnabled;
            _orbit.enabled = _orbitEnabled;
            SetBackgroundEnemiesHidden(false);
            foreach (EnemyStateMachine member in _encounterMembers)
            {
                if (member != null)
                {
                    member.gameObject.SetActive(!defeated);
                }
            }
            _encounterMembers = Array.Empty<EnemyStateMachine>();
            SetEnemiesPaused(false);
            _lastPlayerPosition = _movement.transform.position;
        }

        private void SetBackgroundEnemiesHidden(bool isHidden)
        {
            if (isHidden)
            {
                foreach (EnemyStateMachine enemy in _enemies)
                {
                    if (enemy == null || _encounterMembers.Contains(enemy) || _backgroundEnemyStates.ContainsKey(enemy.gameObject))
                    {
                        continue;
                    }
                    _backgroundEnemyStates.Add(enemy.gameObject, enemy.gameObject.activeSelf);
                    enemy.gameObject.SetActive(false);
                }
                return;
            }
            foreach (KeyValuePair<GameObject, bool> entry in _backgroundEnemyStates)
            {
                if (entry.Key != null)
                {
                    entry.Key.SetActive(entry.Value);
                }
            }
            _backgroundEnemyStates.Clear();
        }

        private void SetEnemiesPaused(bool isPaused)
        {
            // 연출 잠금이 겹치거나(멈춤 중 다시 멈춤) 멈춘 적 없이 풀면 저장해 둔 켜짐 상태가 꺼짐으로 덮여
            // 필드 AI가 전부 꺼진 채 남는다. 상태가 바뀔 때만 저장·복원한다.
            if (_enemiesPaused == isPaused)
            {
                return;
            }
            _enemiesPaused = isPaused;
            AreFieldEnemiesPaused = isPaused;
            for (int i = 0; i < _enemies.Length; i++)
            {
                EnemyStateMachine enemy = _enemies[i];
                // 삭제되었거나 Inspector 연결이 비어 있는 몬스터는 제외한다.
                if (enemy == null)
                {
                    continue;
                }
                if (isPaused)
                {
                    _enemyEnabled[i] = enemy.enabled;
                    enemy.enabled = false;
                }
                else
                {
                    // 아직 꺼져 있는 구역의 몬스터도 되돌려야 나중에 구역이 켜졌을 때 필드 AI가 움직인다.
                    enemy.enabled = _enemyEnabled[i];
                }
            }
        }

        private void OnDisable()
        {
            _debugBattleCamera = false;
            EndCameraPreview();
            if (IsInBattle && _camera != null && _movement != null && _orbit != null && _brain != null)
            {
                RestoreExploration();
                _session = null;
                HasSelectedBattleTarget = false;
                _battleUI.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            _targetHighlight.Dispose();
            if (_playerState != null)
            {
                _playerState.Changed -= RefreshFieldVitals;
            }
            if (_targetActions != null)
            {
                for (int i = 0; i < _targetActions.Length; i++)
                {
                    if (_targetButtons[i] != null)
                    {
                        _targetButtons[i].onClick.RemoveListener(_targetActions[i]);
                    }
                }
            }
            if (_skillActions == null)
            {
                return;
            }
            for (int i = 0; i < _skillButtons.Length; i++)
            {
                if (_skillButtons[i] != null)
                {
                    _skillButtons[i].onClick.RemoveListener(_skillActions[i]);
                }
            }
            if (_investmentButton != null)
            {
                _investmentButton.onClick.RemoveListener(CycleInvestment);
            }
            if (_defendButton != null)
            {
                _defendButton.onClick.RemoveListener(Defend);
            }
            if (_continueButton != null)
            {
                _continueButton.onClick.RemoveListener(ReturnToExploration);
            }
        }

        private void InitializeFieldVitals()
        {
            // 기존 TutorialDemo 연결도 사용할 수 있도록 필드 UI 안에서만 참조를 보완한다.
            if (_fieldHpBar == null)
            {
                _fieldHpBar = _fieldUI.GetComponentsInChildren<Slider>(true)
                    .FirstOrDefault(slider => slider.name == "hp_bar");
            }
            if (_fieldHpBar == null)
            {
                Debug.LogError("TutorialBattleController: 필드 HP 바 연결이 없습니다.", this);
                return;
            }
            _fieldHpBar.interactable = false;
            if (_fieldVitals != null)
            {
                return;
            }
            Transform parent = _fieldHpBar.transform.parent;
            Transform existing = parent.Find("Player Field Vitals");
            if (existing != null)
            {
                _fieldVitals = existing.GetComponent<TMP_Text>();
            }
            if (_fieldVitals == null)
            {
                var label = new GameObject("Player Field Vitals", typeof(RectTransform), typeof(TextMeshProUGUI));
                label.layer = _fieldHpBar.gameObject.layer;
                label.transform.SetParent(parent, false);
                _fieldVitals = label.GetComponent<TMP_Text>();
                RectTransform bar = _fieldHpBar.GetComponent<RectTransform>();
                RectTransform rect = _fieldVitals.rectTransform;
                rect.anchorMin = bar.anchorMin;
                rect.anchorMax = bar.anchorMax;
                rect.pivot = bar.pivot;
                rect.anchoredPosition = bar.anchoredPosition + Vector2.up * 48f;
                rect.sizeDelta = new Vector2(bar.sizeDelta.x, 36f);
                _fieldVitals.font = _status.font;
                _fieldVitals.fontSize = 24f;
                _fieldVitals.alignment = TextAlignmentOptions.MidlineLeft;
                _fieldVitals.raycastTarget = false;
            }
        }

        private void RefreshFieldVitals()
        {
            if (_fieldHpBar != null)
            {
                _fieldHpBar.minValue = 0;
                _fieldHpBar.maxValue = _playerState.MaxHp;
                _fieldHpBar.SetValueWithoutNotify(_playerState.Hp);
            }
            if (_fieldVitals != null)
            {
                _fieldVitals.text = "HP " + _playerState.Hp + "/" + _playerState.MaxHp
                    + "   메모리 " + _playerState.Memory + "/" + _playerState.MaxMemory;
            }
        }
    }
}

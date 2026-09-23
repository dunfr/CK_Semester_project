using System;
using System.Collections.Generic;
using System.Linq;
using CK.SemesterProject.Battle;
using CK.SemesterProject.Data;
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
        [SerializeField, Min(2f), Tooltip("전투 진입 시 플레이어와 몬스터 사이의 목표 간격(m)")]
        private float _battleSpacing = 9f;
        [SerializeField, Tooltip("전투 카메라 위치: 플레이어 기준 오른쪽/높이/전방(m)")]
        private Vector3 _battleCameraOffset = new Vector3(2.2f, 1.3f, -2.3f);
        [SerializeField, Range(35f, 65f), Tooltip("전투 카메라의 수직 시야각(도)")]
        private float _battleFieldOfView = 56f;
        [SerializeField, Min(0.1f), Tooltip("전투 진입 카메라가 자리를 잡는 시간(초)")]
        private float _cameraEntrySeconds = 0.85f;
        [SerializeField, Min(0f), Tooltip("대기 중 카메라의 좌우 이동 폭(m)")]
        private float _cameraDrift = 0.18f;
        [SerializeField, Min(0f), Tooltip("행동 시 카메라의 이동 폭(m)")]
        private float _cameraActionTravel = 0.45f;
        [SerializeField, Min(1), Tooltip("DT 미제공 몬스터의 임시 HP. 밸런스 확정값이 아닙니다.")]
        private int _monsterHp = 600;
        [SerializeField, Min(1), Tooltip("DT 미제공 몬스터의 임시 초기 메모리")]
        private int _monsterMemory = 100;
        [SerializeField, Min(0), Tooltip("몬스터 접촉으로 진입할 때 몬스터가 얻는 추가 메모리")]
        private int _collisionMemoryBonus = 1;
        [SerializeField, Range(1, 3), Tooltip("전투 참여 몬스터 수. 접촉한 몬스터와 가까운 살아 있는 필드 몬스터를 선택합니다. 부족하면 현재 수로 진입합니다.")]
        private int _encounterSize = 1;
        [SerializeField, Tooltip("필드 E/접촉 자동 진입 사용. 조합 선택 테스트 씬에서는 끕니다.")]
        private bool _automaticEncounters = true;
        [SerializeField, Tooltip("튜토리얼 플레이어 이동 컴포넌트")]
        private PlayerMovement _movement;
        [SerializeField, Tooltip("필드 카메라 입력")]
        private CameraController _orbit;
        [SerializeField, Tooltip("메인 카메라")]
        private Camera _camera;
        [SerializeField, Tooltip("필드 카메라 제어기")]
        private CinemachineBrain _brain;
        [SerializeField, Tooltip("탐색 중 동작할 박스 몬스터")]
        private EnemyStateMachine[] _enemies;
        [SerializeField, Tooltip("가져온 캐릭터 DT")]
        private CharacterDatabase _characters;
        [SerializeField, Tooltip("사용할 캐릭터 ID")]
        private string _characterId = "CH00";
        [SerializeField, Tooltip("기존 Skill_DT CSV")]
        private TextAsset _skillTable;
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

        [SerializeField, Min(0.01f), Tooltip("전투 중 우클릭 드래그 회전 감도(도/픽셀)")]
        private float _battleMouseSensitivity = 0.15f;

        private float _battleYaw;
        private float _battlePitch;
        private bool _isCameraDragging;
        private BattleSnapshot _cachedSnapshot;
        private readonly Dictionary<GameObject, bool> _backgroundEnemyStates = new Dictionary<GameObject, bool>();
        private readonly MonsterAi _ai = new MonsterAi();
        private BattleSession _session;
        private CombatantData _playerData;
        private SkillData[] _skills;
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
        private bool[] _enemyEnabled;
        private bool _fieldUIWasActive;
        private bool _movementEnabled;
        private bool _orbitEnabled;
        private bool _brainEnabled;
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
        private float _encounterCooldown;
        private float _nextFootstep;
        private Vector3 _lastPlayerPosition;
        private UnityEngine.Events.UnityAction[] _skillActions;

        public bool IsInBattle => _session != null;
        public BattleSnapshot Snapshot => _session == null ? null : _cachedSnapshot ?? (_cachedSnapshot = _session.GetSnapshot());

        private void Awake()
        {
            if (_movement == null || _orbit == null || _camera == null || _brain == null || _characters == null
                || _fieldUI == null || _skillTable == null || _enemies == null || _enemies.Length == 0 || _battleUI == null
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
                if (!_characters.TryGet(_characterId, out CharacterData character))
                {
                    throw new InvalidOperationException("Character_DT: 캐릭터 ID " + _characterId + "를 찾을 수 없습니다.");
                }
                _skills = SkillTable.LoadCsv(_skillTable.text).ToArray();
                if (_skills.Length != _skillButtons.Length)
                {
                    throw new InvalidOperationException("Skill_DT: 튜토리얼 스킬 버튼 수와 데이터가 다릅니다.");
                }
                _playerData = new CombatantData(character.Id, character.Name, BattleTeam.Player,
                    checked((int)character.Hp), checked((int)character.BaseMemory), checked((int)character.BaseMemory),
                    _skills, evasion: character.EvasionRate, baseCriticalChance: character.BaseCriticalChance,
                    criticalDamageMultiplier: character.CriticalDamage);
            }
            catch (Exception exception)
            {
                Debug.LogError("TutorialBattleController: " + exception.Message, this);
                enabled = false;
                return;
            }
            _characterController = _movement.GetComponent<CharacterController>();
            _animator = _movement.GetComponent<Animator>();
            _spawn = _movement.transform.position;
            _lastPlayerPosition = _spawn;
            _playerState = PlayerSessionState.Current;
            _playerState.Initialize(_playerData);
            _playerState.RestoreFieldMemory();
            InitializeFieldVitals();
            _playerState.Changed += RefreshFieldVitals;
            RefreshFieldVitals();
            _enemyEnabled = new bool[_enemies.Length];
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
            _notice.text = "WASD 이동 · 가까운 몬스터에 좌클릭 선제 진입 · Space 소리";
        }

        private void Update()
        {
            if (!IsInBattle)
            {
                if (_automaticEncounters)
                {
                    UpdateExploration();
                }
                return;
            }
            if (_cameraTime < _cameraEntrySeconds)
            {
                return;
            }
            _elapsed += Time.deltaTime;
            BattleSnapshot snapshot = Snapshot;
            if (CanInput(snapshot) && Keyboard.current != null && Application.isFocused)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame)
                {
                    SelectTarget(0);
                }
                if (Keyboard.current.digit2Key.wasPressedThisFrame)
                {
                    SelectTarget(1);
                }
                if (Keyboard.current.digit3Key.wasPressedThisFrame)
                {
                    SelectTarget(2);
                }
            }
            if (snapshot.Phase == BattlePhase.AwaitingPresentation)
            {
                BattleActionResult pending = _session.PendingResult;
                for (int i = 0; i < _presentations.Length; i++)
                {
                    if (pending.Request.ActorId == "player" && pending.Request.TargetId == GetMonsterId(i))
                    {
                        _presentations[i].Sample(pending, _elapsed / _actionSeconds);
                    }
                    else if (pending.Request.ActorId == GetMonsterId(i) && pending.Request.TargetId == "player")
                    {
                        _monsterPresentations[i].Sample(pending, _elapsed / _actionSeconds);
                    }
                }
            }
            if (_elapsed >= _actionSeconds && snapshot.Phase == BattlePhase.AwaitingPresentation)
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
                return;
            }
            bool wasEntering = _cameraTime < _cameraEntrySeconds;
            _cameraTime += Time.deltaTime;
            if (wasEntering && _cameraTime >= _cameraEntrySeconds)
            {
                RefreshUI();
            }
            float previousYaw = _battleYaw;
            float previousPitch = _battlePitch;
            ReadBattleCameraInput();
            BattleActionResult result = _session.PendingResult;
            // 플레이어 피격 중에는 공격에 따른 카메라 이동·줌을 적용하지 않는다.
            bool isAttack = result != null && !result.WasSkipped && !result.IsTurnStartEffect
                && result.Request.Kind == BattleActionKind.Skill && result.Request.ActorId == "player";
            float progress = isAttack ? Mathf.Clamp01(_elapsed / _actionSeconds) : 0f;
            float direction = isAttack && result.Request.ActorId != "player" ? -1f : 1f;
            float opening = 1f - Mathf.SmoothStep(0f, 1f, _cameraTime / _cameraEntrySeconds);
            Vector3 offset = TutorialBattleCamera.GetMotionOffset(_battleCameraOffset,
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
                EnemyNoise.Emit(position, 5f, _movement.gameObject);
                _lastPlayerPosition = position;
                _nextFootstep = Time.time + 0.4f;
            }
            Keyboard keyboard = Keyboard.current;
            if (!Application.isFocused || Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                EnemyNoise.Emit(position, 12f, _movement.gameObject);
            }
            if (Time.time < _encounterCooldown)
            {
                return;
            }
            EnemyStateMachine nearest = null;
            float nearestDistanceSquared = float.PositiveInfinity;
            foreach (EnemyStateMachine enemy in _enemies)
            {
                if (enemy == null || !enemy.isActiveAndEnabled)
                {
                    continue;
                }
                float distanceSquared = (enemy.transform.position - position).sqrMagnitude;
                if (distanceSquared < nearestDistanceSquared)
                {
                    nearest = enemy;
                    nearestDistanceSquared = distanceSquared;
                }
            }
            if (nearest == null)
            {
                return;
            }
            float distance = Vector3.Distance(nearest.transform.position, position);
            Vector3 start = position + Vector3.up * 0.8f;
            Vector3 end = nearest.transform.position + Vector3.up * 0.8f;
            if (Physics.Linecast(start, end, out RaycastHit obstacle, ~0, QueryTriggerInteraction.Ignore)
                && !obstacle.transform.IsChildOf(nearest.transform) && !obstacle.transform.IsChildOf(_movement.transform))
            {
                return;
            }
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && distance <= 2.5f)
            {
                BeginBattle(nearest, BattleEntryCondition.PlayerInitiated);
            }
            else if (distance <= 1.15f)
            {
                BeginBattle(nearest, BattleEntryCondition.MonsterCollision);
            }
        }

        public bool BeginBattle(EnemyStateMachine enemy, BattleEntryCondition entry,
            IReadOnlyList<EnemyStateMachine> members = null)
        {
            if (!enabled || _playerData == null || IsInBattle || Time.time < _encounterCooldown
                || enemy == null || !enemy.isActiveAndEnabled || !_enemies.Contains(enemy))
            {
                return false;
            }
            if (members != null && (members.Count < 1 || members.Count > 3 || members[0] != enemy
                || members.Distinct().Count() != members.Count
                || members.Any(member => member == null || !member.isActiveAndEnabled || !_enemies.Contains(member))))
            {
                return false;
            }
            _encounterMembers = members != null ? members.ToArray() : new[] { enemy }.Concat(_enemies
                .Where(other => other != null && other != enemy && other.isActiveAndEnabled)
                .Distinct().OrderBy(other => (other.transform.position - enemy.transform.position).sqrMagnitude))
                .Take(Mathf.Clamp(_encounterSize, 1, 3)).ToArray();
            var participants = new List<BattleParticipant>
            {
                new BattleParticipant("player", _playerData, initialHp: _playerState.Hp, initialMemory: _playerState.Memory)
            };
            try
            {
                for (int i = 0; i < _encounterMembers.Length; i++)
                {
                    participants.Add(new BattleParticipant(GetMonsterId(i), GetMonsterData(_encounterMembers[i])));
                }
            }
            catch (Exception exception)
            {
                Debug.LogError("몬스터 데이터 오류: " + exception.Message, enemy);
                _encounterMembers = Array.Empty<EnemyStateMachine>();
                return false;
            }
            var session = new BattleSession(rules: new BattleRules(monsterCollisionMemoryBonus: _collisionMemoryBonus));
            session.Start(participants, entry, entry == BattleEntryCondition.PlayerInitiated ? "player"
                : entry == BattleEntryCondition.MonsterCollision ? GetMonsterId(0) : null);
            var formation = new TutorialBattleFormation(_characterController,
                _encounterMembers.Select(member => member.GetComponent<NavMeshAgent>()).ToArray());
            _encounter = enemy;
            SetBackgroundEnemiesHidden(true);
            Pose battlePose = default;
            if (!formation.TryPlace(_battleSpacing, (playerSlot, enemySlot) => TryGetGroupPose(
                playerSlot, enemySlot, formation.EnemySlots, _battleCameraOffset,
                _battleFieldOfView, out battlePose)))
            {
                SetBackgroundEnemiesHidden(false);
                _encounterMembers = Array.Empty<EnemyStateMachine>();
                _encounterCooldown = Time.time + 1;
                Debug.LogWarning("전투 자리를 확보할 수 없습니다. 넓은 공간에서 다시 진입하세요.", enemy);
                return false;
            }
            _session = session;
            _cachedSnapshot = null;
            _battleYaw = 0f;
            _battlePitch = 0f;
            _isCameraDragging = false;
            _formation = formation;
            _presentations = _encounterMembers.Select((member, index) =>
                new TutorialBattleActionPresentation(_movement.transform, member.transform, 1.6f, GetMonsterId(index))).ToArray();
            _monsterPresentations = _encounterMembers.Select((member, index) =>
                new TutorialBattleActionPresentation(member.transform, _movement.transform, 1.6f, "player", GetMonsterId(index))).ToArray();
            _playerHome = _movement.transform.position;
            _enemyCenter = Vector3.zero;
            foreach (Vector3 slot in formation.EnemySlots)
            {
                _enemyCenter += slot / formation.EnemySlots.Length;
            }
            _targetId = GetMonsterId(0);
            _encounter = enemy;
            _investment = 0;
            _elapsed = 0;
            _fieldUIWasActive = _fieldUI.activeSelf;
            _fieldUI.SetActive(false);
            _movementEnabled = _movement.enabled;
            _orbitEnabled = _orbit.enabled;
            _brainEnabled = _brain.enabled;
            _animatorSpeed = _animator.speed;
            _movement.enabled = false;
            _animator.SetFloat("Speed", 0);
            _animator.Update(0f);
            _animator.speed = 0;
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
            Vector3 openingOffset = TutorialBattleCamera.GetMotionOffset(_battleCameraOffset,
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
                RefreshUI();
            }
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
                _notice.text = error == BattleActionError.InsufficientMemory ? "메모리가 부족합니다. 투자 단계를 줄이거나 방어하세요." : "현재 행동할 수 없습니다.";
                return;
            }
            _cachedSnapshot = null;
            _elapsed = 0;
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
                + (player.ImprintDamage > 0 ? "  각인" : "") + (player.HasMemoryLoss ? "  메모리 고갈" : "");
            _enemyStatus.text = string.Join("\n", monsters.Select((monster, index) =>
                (monster.InstanceId == _targetId ? "▶ " : "") + (index + 1) + ". " + monster.Data.DisplayName
                + " [" + TutorialMonster.GetElementName(monster.Data.Element) + "]  HP " + monster.Hp + "/" + monster.Data.MaxHp
                + "  메모리 " + monster.Memory + "  연쇄 " + monster.ChainStep + "/4"))
                + "\n순서: " + string.Join(" → ", state.TurnOrder.Select(id =>
                    state.Combatants.First(unit => unit.InstanceId == id).Data.DisplayName));
            _investmentLabel.text = "투자 " + _investment + "단계 (" + _session.Rules.GetStageCost(_playerData, _investment) + ")";
            for (int i = 0; i < _skillButtons.Length; i++)
            {
                _skillButtons[i].interactable = CanInput(state);
            }
            _investmentButton.interactable = CanInput(state);
            _defendButton.interactable = CanInput(state);
            _continueButton.gameObject.SetActive(state.Phase == BattlePhase.Finished);
            if (state.Phase == BattlePhase.Finished)
            {
                _notice.text = state.Outcome == BattleOutcome.Victory ? "승리" : state.Outcome == BattleOutcome.Defeat ? "패배 · 시작 위치로 돌아갑니다" : "무승부";
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
            _session = null;
            _encounterCooldown = Time.time + 3;
            RestoreExploration(state.Outcome != BattleOutcome.Victory, state.Outcome == BattleOutcome.Victory);
            _battleUI.SetActive(false);
            _notice.text = _enemies.All(enemy => enemy == null || !enemy.gameObject.activeSelf) ? "모든 몬스터를 처치했습니다." : "WASD 이동 · 좌클릭 선제 진입 · Space 소리";
        }

        public void CancelBattle()
        {
            if (IsInBattle)
            {
                RestoreExploration();
                _session = null;
                _cachedSnapshot = null;
                _battleUI.SetActive(false);
            }
            _encounterCooldown = 0f;
        }

        private void RestoreExploration(bool respawn = false, bool defeated = false)
        {
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
            _formation = null;
            if (_fieldUI != null)
            {
                _fieldUI.SetActive(_fieldUIWasActive);
            }
            _camera.transform.SetPositionAndRotation(_cameraPosition, _cameraRotation);
            _camera.fieldOfView = _cameraFieldOfView;
            _camera.orthographic = _cameraWasOrthographic;
            _brain.enabled = _brainEnabled;
            _animator.speed = _animatorSpeed;
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
                else if (enemy.gameObject.activeInHierarchy)
                {
                    enemy.enabled = _enemyEnabled[i];
                }
            }
        }

        private void OnDisable()
        {
            if (IsInBattle && _camera != null && _movement != null && _orbit != null && _brain != null && _animator != null)
            {
                RestoreExploration();
                _session = null;
                _battleUI.SetActive(false);
            }
        }

        private void OnDestroy()
        {
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

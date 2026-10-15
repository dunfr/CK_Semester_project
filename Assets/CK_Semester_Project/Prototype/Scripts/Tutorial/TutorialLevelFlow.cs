using System.Collections;
using CK.SemesterProject.Battle;
using Semester.Enemies;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using Unity.Cinemachine;

namespace CK.SemesterProject.Tutorial
{
    public enum TutorialLevelStage
    {
        Awakening, FirstBattle, CardDoor, Elevator, ObserveGuard,
        DuctRamp, DuctObservation, DuctEscape, Hall, UpperExit, Completed
    }

    public sealed class TutorialLevelFlow : MonoBehaviour
    {
        [SerializeField, Min(0.1f), Tooltip("프레스 전체 하강에 걸리는 기준 시간(초). 중간 정지 비율에 도달하면 즉시 멈춥니다.")]
        private float _awakeningSeconds = 2f;
        [SerializeField, Min(0f), Tooltip("프레스의 전체 하강 거리(m). 실제 이동은 중간 정지 비율까지만 적용합니다.")]
        private float _pressTravelDistance = 2f;
        [SerializeField, Range(0.1f, 0.9f), Tooltip("전체 하강 중 프레스가 정지하는 시점 비율(0~1)")]
        private float _pressStopProgress = 0.5f;
        [SerializeField, Min(0f), Tooltip("프레스가 멈춘 뒤 조작을 해제하기까지 대기(초)")]
        private float _pressStopHoldSeconds = 0.5f;
        [SerializeField, Min(0f), Tooltip("시작 경비병이 콘솔까지 걷는 시간(초)")]
        private float _guardWalkSeconds = 2.5f;
        [SerializeField, Min(0f), Tooltip("경비병 버튼 조작과 놀람 연출 시간(초)")]
        private float _guardGestureSeconds = 1.5f;
        [SerializeField, Min(0.1f), Tooltip("조작 해제 후 첫 적이 플레이어를 추적하기까지 대기(초)")]
        private float _firstGuardWaitSeconds = 20f;
        [SerializeField, Tooltip("맵 아래 낙하 복귀 기준 월드 높이(m)")]
        private float _fallRecoveryHeight = -130f;
        [SerializeField, Min(0.1f), Tooltip("카드키 상승 시간(초)")]
        private float _cardRiseSeconds = 0.2f;
        [SerializeField] private Transform _guardConsolePoint;
        [SerializeField] private Transform _consoleFocus;
        [SerializeField] private Transform _cardKey;
        [SerializeField] private BoxCollider _elevatorBoardingZone;
        [SerializeField] private TutorialSequenceEffects _effects;
        [SerializeField] private EnemyStateMachine _ductLookout;
        [SerializeField] private EnemyStateMachine[] _ductInvestigators;
        [SerializeField] private EnemyStateMachine[] _collapseInvestigators;
        [SerializeField] private TutorialFieldInteractable _corridorCardDoor;
        [SerializeField, Min(0.1f), Tooltip("엘리베이터 운행 시간(초)")]
        private float _elevatorSeconds = 7f;
        [SerializeField, Min(0.1f), Tooltip("덕트에서 소리 반응을 관찰할 시간(초)")]
        private float _observationSeconds = 3f;
        [SerializeField, Min(0.1f), Tooltip("덕트 붕괴 시간(초)")]
        private float _collapseSeconds = 0.8f;
        [SerializeField, Min(0f), Tooltip("덕트 낙하 소음 반경(m)")]
        private float _collapseNoiseRadius = 12f;
        [SerializeField] private PlayerMovement _movement;
        [SerializeField] private TutorialBattleController _battle;
        [SerializeField] private EnemyStateMachine _firstEnemy;
        [SerializeField] private Transform _capsuleLid;
        [SerializeField] private Transform _elevatorCar;
        [SerializeField] private Transform _elevatorDestination;
        [SerializeField] private Transform _arrivalPoint;
        [SerializeField] private Transform _ductSection;
        [SerializeField] private Transform _ductCheckpoint;
        [SerializeField] private Transform _hallCheckpoint;
        [SerializeField] private Transform _startPoint;
        [SerializeField] private Transform _observationNoisePoint;
        [SerializeField] private TMP_Text _objective;
        [SerializeField] private TMP_Text _location;
        [SerializeField] private TMP_Text _elevatorStatus;
        [SerializeField] private TMP_Text _feedback;
        [SerializeField] private TMP_Text _routeHint;
        [SerializeField] private Camera _camera;
        [SerializeField] private Transform[] _goalPoints;
        [SerializeField] private FloorTravelButtons _floorTravel;
        [SerializeField] private TutorialFieldInteractable[] _doors;

        private TutorialLevelStage _checkpointStage;
        private Vector3 _checkpoint;
        private float _feedbackUntil;
        private bool _isBusy;
        private bool _hasCollapsed;
        private bool _hasDuctNoise;
        private bool _isFirstGuardTriggered;
        private float _firstGuardElapsed;

        public TutorialLevelStage Stage { get; private set; }
        public bool HasCardKey { get; private set; }
        public bool IsCardKeyRaised { get; private set; }
        public bool HasDuctNoise => _hasDuctNoise;
        public bool IsBusy => _isBusy;
        public bool HasCollapsed => _hasCollapsed;
        public Vector3 Checkpoint => _checkpoint;
        public string AreaName => Stage <= TutorialLevelStage.CardDoor ? "튜토리얼"
            : Stage == TutorialLevelStage.Elevator ? "엘리베이터"
            : Stage < TutorialLevelStage.Hall ? "경비 복도" : "중앙 홀";

        private void OnEnable()
        {
            _battle.FieldBattleEnded += OnBattleEnded;
        }

        private IEnumerator Start()
        {
            SetCheckpoint(_startPoint.position, TutorialLevelStage.FirstBattle);
            _isBusy = true;
            _movement.enabled = false;
            _firstEnemy.enabled = false;
            _movement.GetComponentInChildren<CameraController>().FaceDirection(_firstEnemy.transform.position - _movement.transform.position);
            SetIdle();
            Stage = TutorialLevelStage.Awakening;
            RefreshObjective();
            NavMeshAgent firstAgent = _firstEnemy.GetComponent<NavMeshAgent>();
            firstAgent.enabled = false;
            Vector3 guardStart = _firstEnemy.transform.position;
            for (float elapsed = 0; elapsed < _guardWalkSeconds; elapsed += Time.deltaTime)
            {
                _firstEnemy.transform.position = Vector3.Lerp(guardStart, _guardConsolePoint.position, elapsed / _guardWalkSeconds);
                yield return null;
            }
            _firstEnemy.transform.position = _guardConsolePoint.position;
            _firstEnemy.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(_consoleFocus.position - _guardConsolePoint.position, Vector3.up));
            ShowFeedback("경비병: 폐기 절차를 시작한다.", _guardGestureSeconds);
            _effects.PlayCue(0, _consoleFocus.position);
            yield return new WaitForSeconds(_guardGestureSeconds);
            _effects.SetCapsuleAlarm(true);
            Vector3 lidStart = _capsuleLid.position;
            float stopTime = _awakeningSeconds * _pressStopProgress;
            for (float elapsed = 0; elapsed < stopTime; elapsed += Time.deltaTime)
            {
                float progress = Mathf.SmoothStep(0, 1, elapsed / _awakeningSeconds);
                _capsuleLid.position = lidStart + Vector3.down * (_pressTravelDistance * progress);
                yield return null;
            }
            float stopDistance = _pressTravelDistance * Mathf.SmoothStep(0, 1, _pressStopProgress);
            _capsuleLid.position = lidStart + Vector3.down * stopDistance;
            _effects.PlayCue(0, _capsuleLid.position);
            ShowFeedback("프레스가 멈췄습니다.", 2f);
            yield return new WaitForSeconds(_pressStopHoldSeconds);
            firstAgent.enabled = true;
            _movement.enabled = true;
            _firstEnemy.enabled = true;
            _isBusy = false;
            Advance(TutorialLevelStage.FirstBattle);
        }

        private void Update()
        {
            if (!_battle.IsInBattle)
            {
                Transform target = Stage == TutorialLevelStage.CardDoor && !HasCardKey ? _cardKey
                    : Stage == TutorialLevelStage.DuctEscape && _hasCollapsed ? _hallCheckpoint : _goalPoints[(int)Stage];
                float distance = Vector3.Distance(_movement.transform.position, target.position);
                Vector3 view = _camera.WorldToViewportPoint(target.position);
                string direction = view.z < 0 ? "뒤쪽" : view.x < .4f ? "좌측" : view.x > .6f ? "우측" : "정면";
                _routeHint.text = Stage == TutorialLevelStage.Completed ? "튜토리얼 완료"
                    : "목표 " + Mathf.CeilToInt(distance) + "m · " + direction + (HasCardKey ? " · 카드키 보유" : "");
            }
            if (Time.unscaledTime > _feedbackUntil && _feedback.transform.parent.gameObject.activeSelf)
            {
                _feedback.transform.parent.gameObject.SetActive(false);
            }
            if (Stage == TutorialLevelStage.FirstBattle && !_isBusy && !_battle.IsInBattle && !_isFirstGuardTriggered)
            {
                _firstGuardElapsed += Time.deltaTime;
                if (_firstGuardElapsed >= _firstGuardWaitSeconds)
                {
                    _isFirstGuardTriggered = true;
                    StartCoroutine(AlertFirstGuard());
                }
            }
            if (!_battle.IsInBattle && !_isBusy && _movement.transform.position.y < _fallRecoveryHeight)
            {
                RespawnAtCheckpoint();
            }
        }

        private void OnDisable()
        {
            _battle.FieldBattleEnded -= OnBattleEnded;
        }

        public void ShowFeedback(string message, float seconds = 3f)
        {
            _feedback.text = message;
            _feedback.transform.parent.gameObject.SetActive(true);
            _feedbackUntil = Time.unscaledTime + seconds;
        }

        public void OpenedDoor(TutorialFieldInteractable door)
        {
            if (door.Kind == TutorialFieldAction.CorridorCardDoor)
            {
                TriggerDuctNoise();
            }
            if (door.Kind == TutorialFieldAction.CardDoor && Stage == TutorialLevelStage.CardDoor)
            {
                Advance(TutorialLevelStage.Elevator);
                SetCheckpoint(_movement.transform.position, Stage);
            }
        }

        public bool BeginElevator()
        {
            if (_isBusy || _battle.IsInBattle || Stage != TutorialLevelStage.Elevator || !HasCardKey)
            {
                return false;
            }
            // 패널 버튼에만 접근한 상태에서는 운행하지 않는다. 플레이어도 카 안에 있어야 한다.
            if (!_elevatorBoardingZone.bounds.Contains(_movement.transform.position + Vector3.up * 0.5f))
            {
                ShowFeedback("엘리베이터 안에 탑승한 뒤 패널을 누르세요.");
                return false;
            }
            StartCoroutine(RideElevator());
            return true;
        }

        private IEnumerator RideElevator()
        {
            _isBusy = true;
            _movement.enabled = false;
            SetIdle();
            CharacterController controller = _movement.GetComponent<CharacterController>();
            controller.enabled = false;
            Vector3 carStart = _elevatorCar.position;
            Vector3 passengerOffset = _movement.transform.position - carStart;
            foreach (TutorialFieldInteractable door in _doors)
            {
                if (door.Kind == TutorialFieldAction.ElevatorDoor)
                {
                    door.Close();
                }
            }
            for (float elapsed = 0; elapsed < _elevatorSeconds; elapsed += Time.deltaTime)
            {
                float progress = Mathf.SmoothStep(0, 1, elapsed / _elevatorSeconds);
                _elevatorCar.position = Vector3.Lerp(carStart, _elevatorDestination.position, progress);
                _movement.transform.position = _elevatorCar.position + passengerOffset;
                _elevatorStatus.text = "이동 중 · " + Mathf.RoundToInt(progress * 100) + "%";
                yield return null;
            }
            _elevatorCar.position = _elevatorDestination.position;
            _movement.transform.position = _elevatorCar.position + passengerOffset;
            controller.enabled = true;
            _movement.enabled = true;
            foreach (TutorialFieldInteractable door in _doors)
            {
                if (door.Kind == TutorialFieldAction.ArrivalDoor)
                {
                    door.Open();
                }
            }
            _elevatorStatus.text = "하층 도착 · 문 열림";
            Advance(TutorialLevelStage.ObserveGuard);
            SetCheckpoint(_arrivalPoint.position, Stage);
            _isBusy = false;
            ShowFeedback("하층 도착 · 유리 너머 경비를 관찰하세요.");
        }

        public void EnterZone(TutorialLevelStage destination)
        {
            if (_isBusy || _battle.IsInBattle || destination <= Stage || Stage == TutorialLevelStage.Completed)
            {
                return;
            }
            if (destination == TutorialLevelStage.DuctRamp && Stage == TutorialLevelStage.ObserveGuard)
            {
                TriggerDuctNoise();
                Advance(destination);
            }
            else if (destination == TutorialLevelStage.DuctObservation && Stage >= TutorialLevelStage.ObserveGuard && Stage <= TutorialLevelStage.DuctRamp)
            {
                SetCheckpoint(_ductCheckpoint.position, TutorialLevelStage.DuctRamp);
                StartCoroutine(ObserveNoise());
            }
            else if (destination == TutorialLevelStage.Hall && Stage >= TutorialLevelStage.ObserveGuard && Stage <= TutorialLevelStage.DuctEscape && HasCardKey)
            {
                Advance(destination);
                SetCheckpoint(_hallCheckpoint.position, destination);
            }
            else if (destination == TutorialLevelStage.UpperExit && Stage == TutorialLevelStage.Hall)
            {
                Advance(destination);
            }
            else if (destination == TutorialLevelStage.Completed && Stage == TutorialLevelStage.UpperExit)
            {
                Advance(destination);
                ShowFeedback("튜토리얼 완료 · 상층 출구에 도착했습니다.", 30f);
            }
        }

        private IEnumerator ObserveNoise()
        {
            Advance(TutorialLevelStage.DuctObservation);
            _isBusy = true;
            _movement.enabled = false;
            SetIdle();
            ShowFeedback("경비병: 위쪽은 이상 없나?\n덕트 위에서 경비의 대화를 듣습니다.", _observationSeconds);
            yield return new WaitForSeconds(_observationSeconds);
            _movement.enabled = true;
            _isBusy = false;
            Advance(TutorialLevelStage.DuctEscape);
        }

        public bool CollapseDuct()
        {
            if (_hasCollapsed || _isBusy || _battle.IsInBattle || (Stage < TutorialLevelStage.ObserveGuard || Stage > TutorialLevelStage.DuctEscape))
            {
                return false;
            }
            Advance(TutorialLevelStage.DuctEscape);
            _hasCollapsed = true;
            StartCoroutine(CollapseSection());
            return true;
        }

        private IEnumerator CollapseSection()
        {
            _isBusy = true;
            foreach (Collider collider in _ductSection.GetComponentsInChildren<Collider>())
            {
                collider.enabled = false;
            }
            Vector3 start = _ductSection.position;
            EnemyNoise.Emit(start, _collapseNoiseRadius, _movement.gameObject);
            _effects.PlayCollapse(start);
            foreach (EnemyStateMachine enemy in _collapseInvestigators)
            {
                if (enemy != null && enemy.isActiveAndEnabled)
                {
                    enemy.InvestigatePosition(new Vector3(start.x, _hallCheckpoint.position.y, start.z));
                }
            }
            ShowFeedback("덕트 붕괴! · 소리 때문에 적이 경계합니다. 홀 입구로 이동하세요.", 5f);
            for (float elapsed = 0; elapsed < _collapseSeconds; elapsed += Time.deltaTime)
            {
                _ductSection.position = start + Vector3.down * (3.5f * elapsed / _collapseSeconds);
                yield return null;
            }
            _ductSection.gameObject.SetActive(false);
            // 낙하 위치를 새 체크포인트로 저장하기 전에 실제 착지 프레임을 기다린다.
            CharacterController controller = _movement.GetComponent<CharacterController>();
            float timeout = Time.time + 4f;
            while (!controller.isGrounded && Time.time < timeout)
            {
                yield return null;
            }
            _isBusy = false;
            if (controller.isGrounded)
            {
                SetCheckpoint(_movement.transform.position, TutorialLevelStage.DuctEscape);
            }
        }

        private void OnBattleEnded(BattleOutcome outcome)
        {
            if (outcome == BattleOutcome.Victory && Stage == TutorialLevelStage.FirstBattle)
            {
                Advance(TutorialLevelStage.CardDoor);
                SetCheckpoint(_movement.transform.position, Stage);
                StartCoroutine(RaiseCardKey());
            }
            else if (outcome != BattleOutcome.Victory)
            {
                RespawnAtCheckpoint();
            }
        }

        public void CollectCardKey()
        {
            if (!IsCardKeyRaised || HasCardKey || _isBusy || _battle.IsInBattle)
            {
                return;
            }
            IsCardKeyRaised = false;
            HasCardKey = true;
            _cardKey.gameObject.SetActive(false);
            _effects.PlayCue(2, _consoleFocus.position);
            RefreshObjective();
            ShowFeedback("카드키 획득 · 출입문에서 좌클릭하세요.");
        }

        public void FocusCardDesk()
        {
            if (!_isBusy && !_battle.IsInBattle)
            {
                StartCoroutine(FocusDesk(0.5f));
            }
        }

        public void TriggerDuctNoise()
        {
            if (_hasDuctNoise || Stage < TutorialLevelStage.ObserveGuard || Stage >= TutorialLevelStage.Hall)
            {
                return;
            }
            _hasDuctNoise = true;
            _corridorCardDoor.Open();
            _ductLookout.ObserveNoise(_observationNoisePoint.position);
            _effects.PlayCue(3, _observationNoisePoint.position);
            foreach (EnemyStateMachine enemy in _ductInvestigators)
            {
                if (enemy != null && enemy.isActiveAndEnabled)
                {
                    StartCoroutine(InvestigateAfterDoorOpens(enemy));
                }
            }
            ShowFeedback("경비병: 무슨 소리지? 덕트 쪽을 확인해.\n감시병은 소리 방향을 보고, 순찰병 둘은 확인하러 이동합니다.", 4f);
        }

        private IEnumerator InvestigateAfterDoorOpens(EnemyStateMachine enemy)
        {
            yield return new WaitForSeconds(1.1f);
            while (_battle.IsInBattle)
            {
                yield return null;
            }
            if (enemy != null && enemy.isActiveAndEnabled)
            {
                enemy.InvestigatePosition(_observationNoisePoint.position);
            }
        }

        private IEnumerator AlertFirstGuard()
        {
            ShowFeedback("경비병: 깨어났다고?!", _guardGestureSeconds);
            Quaternion start = _firstEnemy.transform.rotation;
            Vector3 direction = Vector3.ProjectOnPlane(_movement.transform.position - _firstEnemy.transform.position, Vector3.up);
            Quaternion target = direction.sqrMagnitude > 0.001f ? Quaternion.LookRotation(direction) : start;
            for (float elapsed = 0; elapsed < _guardGestureSeconds && !_battle.IsInBattle; elapsed += Time.deltaTime)
            {
                _firstEnemy.transform.rotation = Quaternion.Slerp(start, target, elapsed / _guardGestureSeconds);
                yield return null;
            }
            if (!_battle.IsInBattle && _firstEnemy.isActiveAndEnabled)
            {
                _firstEnemy.InvestigatePosition(_movement.transform.position);
            }
        }

        private IEnumerator RaiseCardKey()
        {
            _isBusy = true;
            _movement.enabled = false;
            SetIdle();
            _cardKey.gameObject.SetActive(true);
            Vector3 start = _cardKey.position;
            _effects.PlayCue(1, start);
            // 잠시 콘솔을 보여 준 뒤 원래 카메라/조작 상태로 복귀한다.
            Coroutine focus = StartCoroutine(FocusDesk(Mathf.Max(0.5f, _cardRiseSeconds)));
            for (float elapsed = 0; elapsed < _cardRiseSeconds; elapsed += Time.deltaTime)
            {
                _cardKey.position = start + Vector3.up * (0.35f * Mathf.Clamp01(elapsed / _cardRiseSeconds));
                yield return null;
            }
            _cardKey.position = start + Vector3.up * 0.35f;
            IsCardKeyRaised = true;
            yield return focus;
            _isBusy = false;
            _movement.enabled = true;
            ShowFeedback("콘솔에서 카드키가 올라왔습니다. 가까이 가서 좌클릭하세요.");
        }

        private IEnumerator FocusDesk(float seconds)
        {
            bool wasBusy = _isBusy;
            bool wasMoving = _movement.enabled;
            var orbit = _movement.GetComponentInChildren<CameraController>();
            var brain = _camera.GetComponent<CinemachineBrain>();
            bool wasOrbitEnabled = orbit.enabled;
            bool wasBrainEnabled = brain.enabled;
            Vector3 position = _camera.transform.position;
            Quaternion rotation = _camera.transform.rotation;
            _isBusy = true;
            _movement.enabled = false;
            SetIdle();
            orbit.enabled = false;
            brain.enabled = false;
            _camera.transform.position = _consoleFocus.position + new Vector3(0f, 1.6f, 3.2f);
            _camera.transform.LookAt(_consoleFocus.position);
            yield return new WaitForSeconds(seconds);
            _camera.transform.SetPositionAndRotation(position, rotation);
            brain.enabled = wasBrainEnabled;
            orbit.enabled = wasOrbitEnabled;
            _movement.enabled = wasMoving;
            _isBusy = wasBusy;
        }

        private void SetIdle()
        {
            Animator animator = _movement.GetComponentInChildren<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetFloat("Speed", 0f);
            }
        }

        private void SetCheckpoint(Vector3 position, TutorialLevelStage stage)
        {
            _checkpoint = position;
            _checkpointStage = stage;
            _battle.SetRespawnPoint(position);
        }

        public void RespawnAtCheckpoint()
        {
            if (_isBusy || _battle.IsInBattle)
            {
                return;
            }
            _movement.Teleport(_checkpoint);
            PlayerSessionState.Current.RestoreFull();
            Stage = _checkpointStage;
            if (Stage == TutorialLevelStage.FirstBattle && _firstEnemy.gameObject.activeSelf)
            {
                _firstEnemy.enabled = false;
                _firstEnemy.GetComponent<NavMeshAgent>().Warp(_guardConsolePoint.position);
                _firstEnemy.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(_consoleFocus.position - _guardConsolePoint.position, Vector3.up));
                _firstEnemy.enabled = true;
                _firstGuardElapsed = 0f;
                _isFirstGuardTriggered = false;
            }
            RefreshObjective();
            ShowFeedback("체크포인트에서 다시 시작합니다.");
        }

        private void Advance(TutorialLevelStage stage)
        {
            Stage = stage;
            RefreshObjective();
        }

        private void RefreshObjective()
        {
            string[] objectives =
            {
                "캡슐에서 깨어나는 중…",
                "WASD 이동 · SHIFT 달리기\n콘솔 앞 몬스터에게 접근해 좌클릭",
                HasCardKey ? "카드키 획득\n출입문에 접근해 좌클릭" : "콘솔 데스크에서 올라온\n카드키를 좌클릭해 획득하세요",
                "복도를 지나 엘리베이터 탑승\n패널을 좌클릭해 하층으로 이동",
                "유리 너머 경비를 관찰하고\n열린 옆 복도로 우회하세요",
                "부서진 덕트 또는 지상 복도로\n홀 입구를 향해 이동하세요",
                "소리에 반응하는 적을 관찰하세요",
                "덕트 또는 지상 경로를 선택하세요\n소리에 반응하는 경비를 주의하세요",
                "적 순찰을 관찰하며 우회하세요\n홀 끝 계단으로 상층 이동",
                "상층 출구를 통과해\n다음 구역으로 이동하세요",
                "튜토리얼 완료\n조작 · 전투 · 카드키 · 소리 학습 완료"
            };
            _objective.text = objectives[(int)Stage];
            _location.text = _floorTravel.CurrentFloor + "층 · " + AreaName;
        }
    }
}

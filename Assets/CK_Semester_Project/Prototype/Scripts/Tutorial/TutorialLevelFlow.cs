using System.Collections;
using CK.SemesterProject.Battle;
using Semester.Enemies;
using TMPro;
using UnityEngine;

namespace CK.SemesterProject.Tutorial
{
    public enum TutorialLevelStage
    {
        Awakening, FirstBattle, CardDoor, Elevator, ObserveGuard,
        DuctRamp, DuctObservation, DuctEscape, Hall, UpperExit, Completed
    }

    public sealed class TutorialLevelFlow : MonoBehaviour
    {
        [SerializeField, Min(0.1f), Tooltip("캡슐 시작 연출 시간(초)")]
        private float _awakeningSeconds = 2f;
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

        public TutorialLevelStage Stage { get; private set; }
        public bool HasCardKey { get; private set; }
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
            Stage = TutorialLevelStage.Awakening;
            RefreshObjective();
            Vector3 lidStart = _capsuleLid.position;
            for (float elapsed = 0; elapsed < _awakeningSeconds; elapsed += Time.deltaTime)
            {
                _capsuleLid.position = lidStart + Vector3.down * Mathf.SmoothStep(0, 0.6f, elapsed / _awakeningSeconds);
                yield return null;
            }
            _movement.enabled = true;
            _firstEnemy.enabled = true;
            _isBusy = false;
            Advance(TutorialLevelStage.FirstBattle);
        }

        private void Update()
        {
            if (!_battle.IsInBattle)
            {
                Transform target = Stage == TutorialLevelStage.DuctEscape && _hasCollapsed ? _hallCheckpoint : _goalPoints[(int)Stage];
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
            if (!_battle.IsInBattle && !_isBusy && _movement.transform.position.y < 2f)
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
            if (Stage == TutorialLevelStage.CardDoor)
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
            Vector3 offset = _movement.transform.position - _elevatorCar.position;
            if (offset.x < -0.3f || offset.x > 3f || Mathf.Abs(offset.z) > 1.3f || Mathf.Abs(offset.y) > 1f)
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
                Advance(destination);
            }
            else if (destination == TutorialLevelStage.DuctObservation && Stage == TutorialLevelStage.DuctRamp)
            {
                SetCheckpoint(_ductCheckpoint.position, TutorialLevelStage.DuctRamp);
                StartCoroutine(ObserveNoise());
            }
            else if (destination == TutorialLevelStage.Hall && Stage == TutorialLevelStage.DuctEscape && _hasCollapsed)
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
            EnemyNoise.Emit(_observationNoisePoint.position, 8f, gameObject);
            ShowFeedback("경비병: 방금 소리 들었나?\n적이 소리 난 방향을 바라봅니다.", _observationSeconds);
            yield return new WaitForSeconds(_observationSeconds);
            _movement.enabled = true;
            _isBusy = false;
            Advance(TutorialLevelStage.DuctEscape);
        }

        public bool CollapseDuct()
        {
            if (_hasCollapsed || _isBusy || _battle.IsInBattle || Stage != TutorialLevelStage.DuctEscape)
            {
                return false;
            }
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
                HasCardKey = true;
                Advance(TutorialLevelStage.CardDoor);
                SetCheckpoint(_movement.transform.position, Stage);
                ShowFeedback("카드키 획득 · 출입문에서 좌클릭하세요.");
            }
            else if (outcome != BattleOutcome.Victory)
            {
                RespawnAtCheckpoint();
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
                "카드키 획득\n출입문에 접근해 좌클릭",
                "복도를 지나 엘리베이터 탑승\n패널을 좌클릭해 하층으로 이동",
                "유리 너머 경비를 관찰하고\n열린 옆 복도로 우회하세요",
                "복도 끝 경사 덕트를 따라\n상부 경로로 올라가세요",
                "소리에 반응하는 적을 관찰하세요",
                "덕트를 따라 이동하세요\n붕괴 후 홀 입구로 이동",
                "적 순찰을 관찰하며 우회하세요\n홀 끝 계단으로 상층 이동",
                "상층 출입문을 열고\n다음 구역으로 이동하세요",
                "튜토리얼 완료\n조작 · 전투 · 카드키 · 소리 학습 완료"
            };
            _objective.text = objectives[(int)Stage];
            _location.text = _floorTravel.CurrentFloor + "층 · " + AreaName;
        }
    }
}

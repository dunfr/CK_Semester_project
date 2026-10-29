using System.Collections;
using CK.SemesterProject.Battle;
using Semester.Enemies;
using UnityEngine;
using UnityEngine.AI;

namespace CK.SemesterProject.LevelDesign
{
    /// <summary>
    /// 01 튜토리얼 구역 흐름. T01 시작 시퀀스, C02 20초 대기 후 경비병 접근, 경비병 처치(C03) 후 T02 카드 키 상승.
    /// </summary>
    public sealed class TutorialStartSequence : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField, Min(0f), Tooltip("경비병이 버튼까지 걷는 시간(초). 1-4-A01")]
        private float _guardWalkSeconds = 2.5f;
        [SerializeField, Min(0f), Tooltip("버튼 누르기 시간(초). 1-4-A02")]
        private float _buttonPressSeconds = 1.5f;
        [SerializeField, Min(0.01f), Tooltip("유압프레스 하강 시간(초). 1-2a-A01")]
        private float _pressDropSeconds = 2f;
        [SerializeField, Min(0f), Tooltip("유압프레스 하강 거리(m)")]
        private float _pressDropDistance = 1.2f;
        [SerializeField, Min(0.01f), Tooltip("관의 붉은 것 상승 시간(초). 1-2a-V02")]
        private float _tubeRiseSeconds = 2f;
        [SerializeField, Min(0.1f), Tooltip("조작 해제 후 플레이어가 공격하지 않으면 경비병이 다가오기까지의 시간(초). C02")]
        private float _guardWaitSeconds = 20f;
        [SerializeField, Min(0f), Tooltip("경비병 뒤돌기·놀람 시간(초). 1-4-A03")]
        private float _guardSurpriseSeconds = 1.5f;
        [SerializeField, Min(0f), Tooltip("카드 키 상승을 비추는 시간(초). T02")]
        private float _cardFocusSeconds = 1.5f;

        [Header("References")]
        [SerializeField] private LevelProgress _progress;
        [SerializeField, Tooltip("콘솔 데스크 앞 경비병(1-4)")]
        private EnemyStateMachine _guard;
        [SerializeField, Tooltip("경비병이 버튼을 누르는 위치·방향")]
        private Transform _guardButtonPoint;
        [SerializeField] private CardKeyDesk _desk;
        [SerializeField, Tooltip("카드 키 상승을 비출 카메라 위치")]
        private Transform _deskView;
        [SerializeField, Tooltip("카메라가 바라볼 데스크 지점")]
        private Transform _deskFocus;
        [SerializeField, Tooltip("열린 캡슐의 유압프레스")]
        private Transform _press;
        [SerializeField, Tooltip("관 안의 붉은 것. 바닥 피벗 기준으로 Y 스케일을 키웁니다.")]
        private Transform _tubeFill;
        [SerializeField, Tooltip("적색 점등할 캡슐 램프")]
        private Renderer[] _alarmRenderers;
        [SerializeField] private Color _alarmColor = new Color(1f, 0.08f, 0.05f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private NavMeshAgent _guardAgent;
        private float _waitElapsed;
        private bool _isWaitingForAttack;
        private bool _hasRaisedCardKey;

        private void Awake()
        {
            if (_progress == null || _guard == null || _guardButtonPoint == null || _desk == null || _deskView == null
                || _deskFocus == null || _press == null || _tubeFill == null)
            {
                Debug.LogError("TutorialStartSequence: 연출 참조가 누락되었습니다.", this);
                enabled = false;
                return;
            }
            _guardAgent = _guard.GetComponent<NavMeshAgent>();
            _tubeFill.localScale = new Vector3(_tubeFill.localScale.x, 0.01f, _tubeFill.localScale.z);
        }

        private void OnEnable()
        {
            if (_progress != null && _progress.Battle != null)
            {
                _progress.Battle.FieldBattleEnded += OnBattleEnded;
            }
        }

        private IEnumerator Start()
        {
            _progress.LockControls(this);
            _progress.SetObjective("상황 지켜보기");
            _guard.enabled = false;
            yield return null;
            yield return WalkGuardToButton();
            yield return new WaitForSeconds(_buttonPressSeconds);
            SetAlarm(true);
            yield return AnimatePress();
            yield return AnimateTube();
            _progress.UnlockControls(this);
            _progress.SetObjective("경비병을 처치하거나 출입문 확인");
            _guard.enabled = true;
            _isWaitingForAttack = true;
        }

        private void Update()
        {
            if (!_isWaitingForAttack)
            {
                return;
            }
            if (_progress.IsInBattle || !_guard.gameObject.activeInHierarchy)
            {
                // I02 전투가 발생하면 대기 시간은 더 이상 증가하지 않는다.
                _isWaitingForAttack = false;
                return;
            }
            _waitElapsed += Time.deltaTime;
            if (_waitElapsed >= _guardWaitSeconds)
            {
                _isWaitingForAttack = false;
                StartCoroutine(ApproachPlayer());
            }
        }

        private void OnDisable()
        {
            if (_progress != null && _progress.Battle != null)
            {
                _progress.Battle.FieldBattleEnded -= OnBattleEnded;
            }
        }

        private IEnumerator WalkGuardToButton()
        {
            Vector3 destination = _guardButtonPoint.position;
            bool canWalk = _guardAgent != null && _guardAgent.isOnNavMesh;
            if (canWalk)
            {
                float distance = Vector3.Distance(_guard.transform.position, destination);
                _guardAgent.isStopped = false;
                _guardAgent.speed = Mathf.Max(0.5f, distance / Mathf.Max(0.1f, _guardWalkSeconds));
                _guardAgent.SetDestination(destination);
            }
            Vector3 start = _guard.transform.position;
            for (float elapsed = 0f; elapsed < _guardWalkSeconds; elapsed += Time.deltaTime)
            {
                if (!canWalk)
                {
                    _guard.transform.position = Vector3.Lerp(start, destination, elapsed / _guardWalkSeconds);
                }
                yield return null;
            }
            if (canWalk)
            {
                _guardAgent.ResetPath();
                _guardAgent.Warp(destination);
            }
            else
            {
                _guard.transform.position = destination;
            }
            _guard.transform.rotation = Quaternion.Euler(0f, _guardButtonPoint.eulerAngles.y, 0f);
        }

        private IEnumerator AnimatePress()
        {
            Vector3 start = _press.position;
            for (float elapsed = 0f; elapsed < _pressDropSeconds; elapsed += Time.deltaTime)
            {
                _press.position = start + Vector3.down * (_pressDropDistance * Mathf.SmoothStep(0f, 1f, elapsed / _pressDropSeconds));
                yield return null;
            }
            _press.position = start + Vector3.down * _pressDropDistance;
        }

        private IEnumerator AnimateTube()
        {
            Vector3 scale = _tubeFill.localScale;
            for (float elapsed = 0f; elapsed < _tubeRiseSeconds; elapsed += Time.deltaTime)
            {
                _tubeFill.localScale = new Vector3(scale.x, Mathf.Lerp(0.01f, 1f, elapsed / _tubeRiseSeconds), scale.z);
                yield return null;
            }
            _tubeFill.localScale = new Vector3(scale.x, 1f, scale.z);
        }

        private IEnumerator ApproachPlayer()
        {
            _guard.enabled = false;
            Quaternion start = _guard.transform.rotation;
            for (float elapsed = 0f; elapsed < _guardSurpriseSeconds; elapsed += Time.deltaTime)
            {
                Vector3 direction = Vector3.ProjectOnPlane(_progress.Player.position - _guard.transform.position, Vector3.up);
                if (direction.sqrMagnitude > 0.0001f)
                {
                    _guard.transform.rotation = Quaternion.Slerp(start, Quaternion.LookRotation(direction), elapsed / _guardSurpriseSeconds);
                }
                yield return null;
            }
            if (_progress.IsInBattle || !_guard.gameObject.activeInHierarchy)
            {
                yield break;
            }
            _guard.enabled = true;
            _guard.InvestigatePosition(_progress.Player.position);
        }

        private void OnBattleEnded(BattleOutcome outcome)
        {
            if (_hasRaisedCardKey || outcome != BattleOutcome.Victory || _guard.gameObject.activeSelf)
            {
                return;
            }
            _hasRaisedCardKey = true;
            _isWaitingForAttack = false;
            StartCoroutine(RaiseCardKey());
        }

        private IEnumerator RaiseCardKey()
        {
            _progress.LockControls(this);
            _progress.FocusCamera(_deskView, _deskFocus, _cardFocusSeconds);
            yield return new WaitForSeconds(0.3f);
            yield return _desk.Raise();
            yield return new WaitForSeconds(Mathf.Max(0f, _cardFocusSeconds - 0.5f));
            _progress.UnlockControls(this);
            _progress.SetObjective("콘솔 데스크에서 카드 키 획득");
        }

        private void SetAlarm(bool isOn)
        {
            var block = new MaterialPropertyBlock();
            foreach (Renderer alarmRenderer in _alarmRenderers)
            {
                if (alarmRenderer == null)
                {
                    continue;
                }
                alarmRenderer.GetPropertyBlock(block);
                if (isOn)
                {
                    block.SetColor(BaseColorId, _alarmColor);
                    block.SetColor(EmissionColorId, _alarmColor * 2f);
                }
                else
                {
                    block.Clear();
                }
                alarmRenderer.SetPropertyBlock(block);
            }
        }
    }
}

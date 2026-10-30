using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Semester.Enemies;
using UnityEngine;
using UnityEngine.AI;

namespace CK.SemesterProject.LevelDesign
{
    public enum EnemyRouteLoopMode
    {
        [InspectorName("반복 (1→2→3→1…)")] Loop,
        [InspectorName("왕복 (1→2→3→2→1…)")] PingPong
    }

    public enum EnemyReactionTargetMode
    {
        [InspectorName("지점으로 이동")] Point,
        [InspectorName("방향으로 이동")] Direction,
        [InspectorName("플레이어 쪽으로 이동")] Player
    }

    public enum EnemyReturnMode
    {
        [InspectorName("도착 후 대기 시간이 지나면")] AfterWait,
        [InspectorName("플레이어가 영역을 나간 뒤 대기 시간이 지나면")] WhenPlayerLeaves
    }

    /// <summary>
    /// 적 이동 프리셋. 웨이포인트 순서대로 순찰하고, 플레이어가 트리거 영역에 들어오면 지정 지점·방향으로 이동했다가
    /// 복귀 조건이 되면 원래 순찰로 돌아간다. 코드 수정 없이 씬에서 웨이포인트·영역·반응 지점을 옮겨 설정한다.
    /// 적에게 EnemyStateMachine이 있으면 순찰은 그 AI가 맡고(감지·추적 유지), 반응 이동 동안만 이 프리셋이 직접 움직인다.
    /// </summary>
    public sealed class EnemyRoutePreset : MonoBehaviour
    {
        private const float ArrivalTolerance = 0.4f;
        private const float TravelTimeout = 20f;
        private const float NavMeshSampleRadius = 2f;

        [Header("대상")]
        [SerializeField, Tooltip("움직일 적(NavMeshAgent 필요)")]
        private NavMeshAgent _enemy;

        [Header("순찰")]
        [SerializeField, Tooltip("웨이포인트 묶음. 자식 오브젝트 순서(하이어라키 위→아래)가 순찰 순서입니다.")]
        private Transform _waypointRoot;
        [SerializeField, Tooltip("웨이포인트를 도는 방식")]
        private EnemyRouteLoopMode _loopMode = EnemyRouteLoopMode.Loop;
        [SerializeField, Min(0.1f), Tooltip("순찰 이동 속도(m/s)")]
        private float _patrolSpeed = 2f;
        [SerializeField, Min(0f), Tooltip("각 웨이포인트에서 멈춰 있는 시간(초)")]
        private float _waitAtWaypoint = 1f;

        [Header("트리거 반응")]
        [SerializeField, Tooltip("플레이어가 들어오면 반응하는 영역. 비우면 반응하지 않고 순찰만 합니다.")]
        private PresetTriggerZone _trigger;
        [SerializeField, Tooltip("반응 시 이동할 곳을 정하는 방식")]
        private EnemyReactionTargetMode _reactionTarget = EnemyReactionTargetMode.Point;
        [SerializeField, Tooltip("반응 시 이동할 지점(지점으로 이동)")]
        private Transform _reactionPoint;
        [SerializeField, Tooltip("반응 시 이동할 방향(월드 기준, 방향으로 이동). 씬 뷰 화살표로 확인할 수 있습니다.")]
        private Vector3 _reactionDirection = Vector3.forward;
        [SerializeField, Min(0f), Tooltip("방향으로 이동할 거리(m)")]
        private float _reactionDistance = 5f;
        [SerializeField, Min(0.1f), Tooltip("반응 이동 속도(m/s)")]
        private float _reactionSpeed = 3.5f;
        [SerializeField, Tooltip("도착 후 바라볼 곳(선택). 비우면 이동 방향을 봅니다.")]
        private Transform _lookAtOnArrive;

        [Header("복귀")]
        [SerializeField, Tooltip("순찰로 돌아가는 시점")]
        private EnemyReturnMode _returnMode = EnemyReturnMode.AfterWait;
        [SerializeField, Min(0f), Tooltip("복귀 전 대기 시간(초)")]
        private float _returnDelay = 3f;

        [Header("발동 조건")]
        [SerializeField, Tooltip("한 번만 반응합니다.")]
        private bool _triggerOnce;
        [SerializeField, Min(0f), Tooltip("복귀 후 다시 반응하기까지 쉬는 시간(초)")]
        private float _retriggerCooldown = 3f;
        [SerializeField, Tooltip("적이 이미 플레이어를 발견해 추적 중이면 반응하지 않습니다.")]
        private bool _ignoreWhileChasing = true;

        private readonly List<Transform> _waypoints = new List<Transform>();
        private EnemyStateMachine _stateMachine;
        private Coroutine _patrolRoutine;
        private Coroutine _reactionRoutine;
        private bool _hasReacted;
        private float _nextReactionTime;

        public bool IsReacting => _reactionRoutine != null;
        public NavMeshAgent Enemy => _enemy;
        public Transform WaypointRoot => _waypointRoot;
        public PresetTriggerZone Trigger => _trigger;

        private void Awake()
        {
            if (_enemy == null)
            {
                Debug.LogError(name + ": 움직일 적(NavMeshAgent)이 지정되지 않았습니다.", this);
                enabled = false;
                return;
            }
            _stateMachine = _enemy.GetComponent<EnemyStateMachine>();
            CollectWaypoints();
            ApplyPatrolToStateMachine();
        }

        private void OnEnable()
        {
            if (_trigger != null)
            {
                _trigger.PlayerEntered += OnPlayerEntered;
            }
            if (_stateMachine == null && _waypoints.Count > 0)
            {
                _patrolRoutine = StartCoroutine(PatrolRoutine());
            }
        }

        private void OnDisable()
        {
            if (_trigger != null)
            {
                _trigger.PlayerEntered -= OnPlayerEntered;
            }
            StopAllCoroutines();
            _patrolRoutine = null;
            _reactionRoutine = null;
            if (_stateMachine != null && _enemy != null && !_stateMachine.enabled)
            {
                _stateMachine.enabled = true;
            }
        }

        /// <summary>순찰 순서대로 실제 방문할 지점 목록(왕복이면 되돌아오는 지점 포함).</summary>
        public List<Transform> GetRoute()
        {
            var route = new List<Transform>();
            if (_waypointRoot == null)
            {
                return route;
            }
            foreach (Transform child in _waypointRoot)
            {
                if (child.gameObject.activeSelf)
                {
                    route.Add(child);
                }
            }
            if (_loopMode == EnemyRouteLoopMode.PingPong)
            {
                for (int i = route.Count - 2; i >= 1; i--)
                {
                    route.Add(route[i]);
                }
            }
            return route;
        }

        /// <summary>반응 시 이동할 목표 위치. 플레이어 쪽 이동은 감지된 플레이어 위치를 쓴다.</summary>
        public Vector3 GetReactionDestination(Transform player)
        {
            switch (_reactionTarget)
            {
                case EnemyReactionTargetMode.Point:
                    return _reactionPoint != null ? _reactionPoint.position : _enemy.transform.position;
                case EnemyReactionTargetMode.Player:
                    return player != null ? player.position : _enemy.transform.position;
                default:
                    Vector3 origin = _enemy != null ? _enemy.transform.position : transform.position;
                    Vector3 direction = Vector3.ProjectOnPlane(_reactionDirection, Vector3.up);
                    return origin + (direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward) * _reactionDistance;
            }
        }

        private void CollectWaypoints()
        {
            _waypoints.Clear();
            _waypoints.AddRange(GetRoute());
        }

        // 순찰은 기존 AI(EnemyStateMachine)에 웨이포인트·속도·대기 시간을 넘겨 맡긴다. 감지·추적·소리 반응이 그대로 유지된다.
        private void ApplyPatrolToStateMachine()
        {
            if (_stateMachine == null || _waypoints.Count == 0)
            {
                return;
            }
            _stateMachine.patrolPoints = _waypoints.ToArray();
            _stateMachine.patrolSpeed = _patrolSpeed;
            _stateMachine.patrolWaitSeconds = new Vector2(_waitAtWaypoint, _waitAtWaypoint);
            // 제자리 감시로 설정된 적도 웨이포인트를 받으면 순찰해야 하므로 감시 옵션을 끈다(AI 쪽 공개 설정이 없어 리플렉션 사용).
            FieldInfo guardField = typeof(EnemyStateMachine).GetField("_stationaryGuard", BindingFlags.Instance | BindingFlags.NonPublic);
            if (guardField != null)
            {
                guardField.SetValue(_stateMachine, false);
            }
        }

        private void OnPlayerEntered(Transform player)
        {
            if (!isActiveAndEnabled || _reactionRoutine != null || (_triggerOnce && _hasReacted) || Time.time < _nextReactionTime)
            {
                return;
            }
            if (_ignoreWhileChasing && _stateMachine != null && _stateMachine.enabled
                && (_stateMachine.CurrentState == EnemyStateMachine.State.Alert || _stateMachine.CurrentState == EnemyStateMachine.State.PursueLastSeen))
            {
                return;
            }
            if (!_enemy.gameObject.activeInHierarchy || !_enemy.isOnNavMesh)
            {
                return;
            }
            _hasReacted = true;
            _reactionRoutine = StartCoroutine(ReactionRoutine(player));
        }

        private IEnumerator ReactionRoutine(Transform player)
        {
            if (_patrolRoutine != null)
            {
                StopCoroutine(_patrolRoutine);
                _patrolRoutine = null;
            }
            if (_stateMachine != null)
            {
                _stateMachine.enabled = false;
            }

            Vector3 destination = GetReactionDestination(player);
            yield return MoveTo(destination, _reactionSpeed);
            yield return FaceAfterArrival(destination);

            if (_returnMode == EnemyReturnMode.WhenPlayerLeaves && _trigger != null)
            {
                while (_trigger.IsPlayerInside)
                {
                    yield return null;
                }
            }
            yield return new WaitForSeconds(_returnDelay);

            // 복귀: AI가 다시 켜지면 순찰 상태로 들어가 다음 웨이포인트부터 이어서 돈다.
            _reactionRoutine = null;
            _nextReactionTime = Time.time + _retriggerCooldown;
            if (_stateMachine != null)
            {
                _stateMachine.enabled = true;
            }
            else if (_waypoints.Count > 0)
            {
                _patrolRoutine = StartCoroutine(PatrolRoutine());
            }
        }

        private IEnumerator PatrolRoutine()
        {
            int index = FindNearestWaypoint();
            while (true)
            {
                Transform point = _waypoints[index];
                if (point != null)
                {
                    yield return MoveTo(point.position, _patrolSpeed);
                    yield return new WaitForSeconds(_waitAtWaypoint);
                }
                index = (index + 1) % _waypoints.Count;
            }
        }

        private IEnumerator MoveTo(Vector3 destination, float speed)
        {
            if (!_enemy.isOnNavMesh)
            {
                yield break;
            }
            if (NavMesh.SamplePosition(destination, out NavMeshHit hit, NavMeshSampleRadius, NavMesh.AllAreas))
            {
                destination = hit.position;
            }
            _enemy.speed = speed;
            _enemy.updateRotation = true;
            _enemy.isStopped = false;
            if (!_enemy.SetDestination(destination))
            {
                yield break;
            }
            float timeout = Time.time + TravelTimeout;
            while (Time.time < timeout)
            {
                if (!_enemy.pathPending
                    && (_enemy.remainingDistance <= _enemy.stoppingDistance + ArrivalTolerance || _enemy.pathStatus == NavMeshPathStatus.PathInvalid))
                {
                    break;
                }
                yield return null;
            }
            if (_enemy.isOnNavMesh)
            {
                _enemy.ResetPath();
            }
        }

        private IEnumerator FaceAfterArrival(Vector3 destination)
        {
            Vector3 look = _lookAtOnArrive != null
                ? _lookAtOnArrive.position - _enemy.transform.position
                : (_reactionTarget == EnemyReactionTargetMode.Direction ? _reactionDirection : destination - _enemy.transform.position);
            look = Vector3.ProjectOnPlane(look, Vector3.up);
            if (look.sqrMagnitude < 0.0001f)
            {
                yield break;
            }
            Quaternion target = Quaternion.LookRotation(look.normalized);
            while (Quaternion.Angle(_enemy.transform.rotation, target) > 1f)
            {
                _enemy.transform.rotation = Quaternion.RotateTowards(_enemy.transform.rotation, target, 360f * Time.deltaTime);
                yield return null;
            }
        }

        private int FindNearestWaypoint()
        {
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < _waypoints.Count; i++)
            {
                if (_waypoints[i] == null)
                {
                    continue;
                }
                float distance = (_waypoints[i].position - _enemy.transform.position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }
            return best;
        }

        private void OnDrawGizmos()
        {
            List<Transform> route = GetRoute();
            Gizmos.color = new Color(0.2f, 0.9f, 1f);
            for (int i = 0; i < route.Count; i++)
            {
                if (route[i] == null)
                {
                    continue;
                }
                Gizmos.DrawSphere(route[i].position + Vector3.up * 0.1f, 0.2f);
                Transform next = route[(i + 1) % route.Count];
                if (next != null && route.Count > 1)
                {
                    DrawArrow(route[i].position + Vector3.up * 0.1f, next.position + Vector3.up * 0.1f);
                }
            }
            if (_enemy != null && _trigger != null)
            {
                Gizmos.color = new Color(1f, 0.35f, 0.3f);
                Vector3 start = _enemy.transform.position + Vector3.up * 0.15f;
                Vector3 end = (_reactionTarget == EnemyReactionTargetMode.Player ? _trigger.transform.position : GetReactionDestination(null)) + Vector3.up * 0.15f;
                DrawArrow(start, end);
                Gizmos.DrawWireSphere(end, 0.35f);
            }
        }

        private static void DrawArrow(Vector3 from, Vector3 to)
        {
            Gizmos.DrawLine(from, to);
            Vector3 direction = to - from;
            if (direction.sqrMagnitude < 0.01f)
            {
                return;
            }
            Quaternion look = Quaternion.LookRotation(direction);
            Vector3 head = to - direction.normalized * Mathf.Min(0.6f, direction.magnitude * 0.3f);
            Gizmos.DrawLine(to, head + look * Vector3.right * 0.25f);
            Gizmos.DrawLine(to, head - look * Vector3.right * 0.25f);
        }
    }
}

using UnityEngine;
using UnityEngine.AI;

namespace Semester.Enemies
{
    [RequireComponent(typeof(NavMeshAgent))]
    [AddComponentMenu("Enemy AI/Enemy State Machine")]
    public class EnemyStateMachine : MonoBehaviour
    {
        public enum State { Patrol, PatrolWait, SoundLook, Alert, PursueLastSeen, Search }

        [Header("대상 / 시야 기준")]
        [Tooltip("감지할 플레이어 Transform. 비어 있으면 시각 감지만 비활성화됩니다.")]
        public Transform player;
        [Tooltip("선택: 눈 위치/시야 방향. 비어 있으면 본체 위치 + eyeHeight, 본체 전방 사용")]
        public Transform eyes;
        [Min(0f)] public float eyeHeight = 0.8f;
        [Tooltip("플레이어 피벗에서 위로 더하는 시야 검사 높이")]
        public float targetHeight = 0.5f;
        [Min(0f)] public float viewDistance = 15f;
        [Range(0f, 360f)] public float viewAngle = 100f;
        [Tooltip("시야를 막는 레이어. 자신과 플레이어의 Collider는 자동 제외")]
        public LayerMask sightBlockingLayers = ~0;

        [Header("배회 영역 (월드 X/Z)")]
        [Tooltip("선택: 영역 중심. 비어 있으면 시작 위치를 고정 중심으로 사용")]
        public Transform patrolCenter;
        public Vector2 patrolHalfExtents = new Vector2(10f, 10f);
        [Min(0f), Tooltip("현재 위치에서 새 목적지까지의 최소 직선거리")]
        public float minimumPatrolDistance = 3f;
        [Min(0f)] public float patrolSpeed = 2f;
        [Tooltip("도착 후 무작위 대기 시간 범위(초)")]
        public Vector2 patrolWaitSeconds = new Vector2(1f, 3f);
        [Min(1)] public int destinationAttempts = 20;
        [Min(0.05f)] public float navMeshSampleRadius = 2f;

        [Header("소리 감지 (시야와 독립)")]
        [Min(0f), Tooltip("실제 감지 거리 = 적 청각 범위와 소리 반경 중 작은 값. 벽 뒤도 감지")]
        public float hearingDistance = 12f;
        [Min(0f), Tooltip("소리 방향을 바라본 뒤 유지할 시간. ?도 이때까지 표시")]
        public float soundLookSeconds = 1.5f;
        [Min(0.05f), Tooltip("지속 소리가 반응 타이머를 매 프레임 초기화하는 것을 방지")]
        public float soundReactionCooldown = 0.5f;
        [Min(1f)] public float turnSpeed = 180f;

        [Header("발견 / 마지막 목격 위치 추적")]
        [Min(0f), Tooltip("! 표시 및 발견 정지 시간(초)")]
        public float alertSeconds = 1f;
        [Min(0f)] public float pursuitSpeed = 3.5f;
        [Min(0.02f)] public float repathInterval = 0.2f;
        [Min(0.01f)] public float arrivalDistance = 0.35f;
        [Min(1f), Tooltip("막힌 경로에서 영원히 대기하지 않도록 이동 제한 시간")]
        public float travelTimeout = 25f;

        [Header("추적 종료 후 좌우 탐색")]
        [Min(0), Tooltip("왼쪽→오른쪽 확인 횟수")]
        public int searchCycles = 2;
        [Range(0f, 180f)] public float searchAngle = 60f;
        [Min(0f)] public float searchPauseSeconds = 0.4f;

        [Header("머리 위 표시 / 디버그")]
        [Tooltip("선택: 기존 TextMesh. 비어 있으면 실행 시 자동 생성")]
        public TextMesh indicator;
        public Vector3 indicatorOffset = new Vector3(0f, 1.8f, 0f);
        [Min(0.01f)] public float indicatorSize = 0.18f;
        public Color soundColor = Color.yellow;
        public Color alertColor = Color.red;
        public bool showGizmos = true;

        public State CurrentState { get; private set; }
        public bool CanSeePlayer { get; private set; }
        public bool HasLastSeenPosition { get; private set; }
        public Vector3 LastSeenPosition { get; private set; }
        public Vector3 LastHeardPosition { get; private set; }

        private NavMeshAgent agent;
        private NavMeshPath path;
        private Vector3 spawnPosition;
        private float timer, stateAge, repathTimer, nextSoundTime;
        private bool soundPending, soundAligned, searchingHold, resumeSearch, patrolDestinationSet;
        private Vector3 pendingSound;
        private Quaternion searchBase;
        private int searchStep;

        private bool AgentReady => agent != null && agent.enabled && agent.isOnNavMesh;
        private Vector3 EyePosition => eyes != null ? eyes.position : transform.position + Vector3.up * eyeHeight;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            path = new NavMeshPath();
            spawnPosition = transform.position;
            EnsureIndicator();
        }

        private void OnEnable()
        {
            EnemyNoise.Emitted += HearNoise;
            soundPending = false;
            nextSoundTime = 0f;
            HasLastSeenPosition = false;
            Enter(State.Patrol);
        }

        private void OnDisable()
        {
            EnemyNoise.Emitted -= HearNoise;
            StopAgent();
            if (indicator != null) indicator.text = "";
        }

        private void Update()
        {
            stateAge += Time.deltaTime;
            // Vision is evaluated every frame in EVERY state. Hearing is an independent event.
            CanSeePlayer = IsPlayerVisible();
            if (CanSeePlayer)
            {
                LastSeenPosition = player.position;
                HasLastSeenPosition = true;
                soundPending = false; // Confirmed sight has priority over a simultaneous sound.
                if (CurrentState != State.Alert && CurrentState != State.PursueLastSeen)
                    Enter(State.Alert);
            }
            else if (soundPending)
            {
                soundPending = false;
                if (CurrentState != State.Alert && CurrentState != State.PursueLastSeen)
                {
                    if (CurrentState != State.SoundLook) resumeSearch = CurrentState == State.Search;
                    LastHeardPosition = pendingSound;
                    Enter(State.SoundLook);
                }
            }

            switch (CurrentState)
            {
                case State.Patrol:
                    if (!AgentReady) break;
                    if (!patrolDestinationSet)
                    {
                        patrolDestinationSet = ChoosePatrolDestination();
                        if (!patrolDestinationSet) Enter(State.PatrolWait);
                    }
                    else if ((!agent.hasPath && !agent.pathPending) || Arrived() || PathFailed() || stateAge >= travelTimeout)
                        Enter(State.PatrolWait);
                    break;
                case State.PatrolWait:
                    timer -= Time.deltaTime;
                    if (timer <= 0f) Enter(State.Patrol);
                    break;
                case State.SoundLook:
                    if (!soundAligned) soundAligned = Face(LastHeardPosition);
                    else timer -= Time.deltaTime;
                    if (soundAligned && timer <= 0f) Enter(resumeSearch ? State.Search : State.Patrol);
                    break;
                case State.Alert:
                    Face(LastSeenPosition);
                    timer -= Time.deltaTime;
                    if (timer <= 0f) Enter(State.PursueLastSeen);
                    break;
                case State.PursueLastSeen:
                    TickPursuit();
                    break;
                case State.Search:
                    TickSearch();
                    break;
            }
        }

        public bool IsPlayerVisible()
        {
            if (player == null || !player.gameObject.activeInHierarchy) return false;
            Vector3 delta = player.position + Vector3.up * targetHeight - EyePosition;
            if (delta.sqrMagnitude > viewDistance * viewDistance) return false;
            Vector3 forward = eyes != null ? eyes.forward : transform.forward;
            if (Vector3.Angle(forward, delta) > viewAngle * 0.5f) return false;
            foreach (RaycastHit hit in Physics.RaycastAll(EyePosition, delta.normalized, delta.magnitude,
                         sightBlockingLayers, QueryTriggerInteraction.Ignore))
            {
                Transform t = hit.transform;
                if (t.IsChildOf(transform) || t.IsChildOf(player)) continue;
                return false;
            }
            return true;
        }

        private void HearNoise(Vector3 position, float radius, GameObject source)
        {
            if (source != null && source.transform.IsChildOf(transform)) return;
            float range = Mathf.Min(Mathf.Max(0f, hearingDistance), radius);
            if ((position - transform.position).sqrMagnitude > range * range || Time.time < nextSoundTime) return;
            pendingSound = position;
            soundPending = true;
            nextSoundTime = Time.time + soundReactionCooldown;
        }

        private void Enter(State next)
        {
            CurrentState = next;
            stateAge = 0f;
            StopAgent();
            if (agent != null) agent.updateRotation = next == State.Patrol || next == State.PursueLastSeen;
            if (indicator != null) indicator.text = "";
            switch (next)
            {
                case State.Patrol:
                    patrolDestinationSet = false;
                    break;
                case State.PatrolWait:
                    timer = Random.Range(Mathf.Max(0f, patrolWaitSeconds.x), Mathf.Max(patrolWaitSeconds.x, patrolWaitSeconds.y));
                    break;
                case State.SoundLook:
                    soundAligned = false;
                    timer = soundLookSeconds;
                    ShowIndicator("?", soundColor);
                    break;
                case State.Alert:
                    timer = alertSeconds;
                    ShowIndicator("!", alertColor);
                    break;
                case State.PursueLastSeen:
                    repathTimer = 0f;
                    break;
                case State.Search:
                    searchBase = transform.rotation;
                    searchStep = 0;
                    searchingHold = false;
                    ShowIndicator("?", soundColor);
                    break;
            }
        }

        private bool ChoosePatrolDestination()
        {
            Vector3 center = patrolCenter != null ? patrolCenter.position : spawnPosition;
            Vector2 extents = new Vector2(Mathf.Abs(patrolHalfExtents.x), Mathf.Abs(patrolHalfExtents.y));
            for (int i = 0; i < destinationAttempts; i++)
            {
                Vector3 candidate = center + new Vector3(Random.Range(-extents.x, extents.x), 0f, Random.Range(-extents.y, extents.y));
                NavMeshHit hit;
                if (!Sample(candidate, out hit)) continue;
                if (Mathf.Abs(hit.position.x - center.x) > extents.x || Mathf.Abs(hit.position.z - center.z) > extents.y) continue;
                Vector3 flat = hit.position - transform.position;
                flat.y = 0f;
                if (flat.magnitude < Mathf.Max(minimumPatrolDistance, arrivalDistance + 0.1f)) continue;
                if (MoveTo(hit.position, patrolSpeed)) return true;
            }
            return false;
        }

        private void TickPursuit()
        {
            if (!AgentReady) { if (stateAge >= travelTimeout) Enter(State.Search); return; }
            repathTimer -= Time.deltaTime;
            if (repathTimer <= 0f)
            {
                repathTimer = Mathf.Max(0.02f, repathInterval);
                NavMeshHit hit;
                if (!HasLastSeenPosition || !Sample(LastSeenPosition, out hit) || !MoveTo(hit.position, pursuitSpeed))
                {
                    if (!CanSeePlayer) Enter(State.Search);
                    return;
                }
            }
            if (CanSeePlayer)
            {
                stateAge = 0f;
                if (Arrived()) Face(LastSeenPosition);
            }
            else if (Arrived() || PathFailed() || stateAge >= travelTimeout) Enter(State.Search);
        }

        private bool Sample(Vector3 target, out NavMeshHit hit)
        {
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            return NavMesh.SamplePosition(target, out hit, Mathf.Max(0.05f, navMeshSampleRadius), filter);
        }

        private bool MoveTo(Vector3 position, float speed)
        {
            if (!AgentReady || !agent.CalculatePath(position, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            agent.speed = Mathf.Max(0f, speed);
            agent.stoppingDistance = Mathf.Max(0.01f, arrivalDistance);
            agent.isStopped = false;
            return agent.SetPath(path);
        }

        private bool Arrived() => AgentReady && !agent.pathPending && agent.remainingDistance <= arrivalDistance + 0.05f;
        private bool PathFailed() => AgentReady && !agent.pathPending && agent.pathStatus != NavMeshPathStatus.PathComplete;

        private void StopAgent()
        {
            if (!AgentReady) return;
            agent.isStopped = true;
            agent.ResetPath();
            // Clear residual steering velocity as well: waiting/hearing must stop immediately.
            agent.velocity = Vector3.zero;
        }

        private bool Face(Vector3 position)
        {
            Vector3 direction = position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return true;
            return TurnTo(Quaternion.LookRotation(direction));
        }

        private bool TurnTo(Quaternion rotation)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, rotation, Mathf.Max(1f, turnSpeed) * Time.deltaTime);
            return Quaternion.Angle(transform.rotation, rotation) < 1f;
        }

        private void TickSearch()
        {
            if (searchStep >= Mathf.Max(0, searchCycles) * 2) { Enter(State.Patrol); return; }
            Quaternion target = searchBase * Quaternion.Euler(0f, searchStep % 2 == 0 ? -searchAngle : searchAngle, 0f);
            if (!searchingHold)
            {
                if (TurnTo(target)) { searchingHold = true; timer = searchPauseSeconds; }
            }
            else
            {
                timer -= Time.deltaTime;
                if (timer <= 0f) { searchStep++; searchingHold = false; }
            }
        }

        private void EnsureIndicator()
        {
            if (indicator != null) return;
            var label = new GameObject("Awareness Indicator");
            label.transform.SetParent(transform, false);
            indicator = label.AddComponent<TextMesh>();
            indicator.anchor = TextAnchor.MiddleCenter;
            indicator.alignment = TextAlignment.Center;
            indicator.fontSize = 64;
            indicator.text = "";
        }

        private void ShowIndicator(string text, Color color)
        {
            if (indicator == null) return;
            indicator.text = text;
            indicator.color = color;
        }

        private void LateUpdate()
        {
            if (indicator == null) return;
            indicator.transform.position = transform.position + indicatorOffset;
            indicator.characterSize = indicatorSize;
            Camera camera = Camera.main;
            if (camera != null) indicator.transform.rotation = camera.transform.rotation;
        }

        private void OnDrawGizmosSelected()
        {
            if (!showGizmos) return;
            Vector3 center = patrolCenter != null ? patrolCenter.position : Application.isPlaying ? spawnPosition : transform.position;
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(center, new Vector3(Mathf.Abs(patrolHalfExtents.x) * 2f, 0.1f, Mathf.Abs(patrolHalfExtents.y) * 2f));
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, hearingDistance);
            Gizmos.color = Color.cyan;
            Vector3 forward = eyes != null ? eyes.forward : transform.forward;
            Gizmos.DrawRay(EyePosition, Quaternion.Euler(0f, -viewAngle * 0.5f, 0f) * forward * viewDistance);
            Gizmos.DrawRay(EyePosition, Quaternion.Euler(0f, viewAngle * 0.5f, 0f) * forward * viewDistance);
            if (Application.isPlaying && HasLastSeenPosition) Gizmos.DrawWireSphere(LastSeenPosition, 0.3f);
        }
    }
}

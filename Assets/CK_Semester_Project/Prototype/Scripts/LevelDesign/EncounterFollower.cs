using System.Collections.Generic;
using Semester.Enemies;
using UnityEngine;
using UnityEngine.AI;

namespace CK.SemesterProject.LevelDesign
{
    /// <summary>
    /// 조우(전투 한 번)에 같이 나오는 동료 몬스터. 필드에서는 보이지 않게 대표 몬스터(리더) 뒤를 따라다니고,
    /// 리더와 부딪히면 리더·동료가 한 전투에 함께 들어가 그때만 모습을 드러낸다.
    /// 필드 AI(EnemyStateMachine)와 NavMeshAgent는 필드에서 꺼 두고, 전투 시작 직전에만 켠다.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class EncounterFollower : MonoBehaviour
    {
        [SerializeField, Tooltip("따라다닐 대표 몬스터")]
        private EnemyStateMachine _leader;
        [SerializeField, Tooltip("리더 기준 자리(리더의 로컬 좌표, m). 예: (-1.1, 0, -0.9) = 왼쪽 뒤")]
        private Vector3 _offset = new Vector3(-1.1f, 0f, -0.9f);
        [SerializeField, Min(0.1f), Tooltip("따라가는 최대 속도(m/s)")]
        private float _maxSpeed = 4.5f;
        [SerializeField, Min(0f), Tooltip("방향을 돌리는 속도(도/초)")]
        private float _turnSpeed = 360f;

        private static readonly List<EncounterFollower> Followers = new List<EncounterFollower>();

        private EnemyStateMachine _self;
        private NavMeshAgent _agent;
        private Renderer[] _renderers;
        private bool _visible = true;

        public EnemyStateMachine Leader => _leader;
        /// <summary>리더가 필드에서 움직이는 중(전투 아님)이면 true.</summary>
        public bool IsFollowing => _leader != null && _leader.isActiveAndEnabled;

        public void Setup(EnemyStateMachine leader, Vector3 offset)
        {
            _leader = leader;
            _offset = offset;
        }

        /// <summary>
        /// 리더와 같이 전투에 들어갈 몬스터 목록(리더 먼저). 동료가 없으면 null.
        /// 동료의 필드 AI를 켜 두어 전투 코드가 참여 몬스터로 받아들이게 한다.
        /// </summary>
        public static IReadOnlyList<EnemyStateMachine> GetEncounterMembers(EnemyStateMachine leader)
        {
            if (leader == null)
            {
                return null;
            }
            List<EnemyStateMachine> members = null;
            foreach (EncounterFollower follower in Followers)
            {
                if (follower == null || follower._leader != leader || !follower.isActiveAndEnabled || follower._self == null)
                {
                    continue;
                }
                if (members == null)
                {
                    members = new List<EnemyStateMachine> { leader };
                }
                if (members.Count >= 3)
                {
                    break;
                }
                follower._self.enabled = true;
                members.Add(follower._self);
            }
            return members;
        }

        private void Awake()
        {
            _self = GetComponent<EnemyStateMachine>();
            _agent = GetComponent<NavMeshAgent>();
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        private void OnEnable()
        {
            Followers.Add(this);
        }

        private void OnDisable()
        {
            Followers.Remove(this);
        }

        private void Update()
        {
            if (_leader == null || !_leader.gameObject.activeInHierarchy)
            {
                return;
            }
            if (!IsFollowing)
            {
                // 전투 중: 모습을 드러내고 위치는 전투 코드가 맡는다.
                SetVisible(true);
                return;
            }
            SetVisible(false);
            // 필드로 돌아왔으면 자기 AI·에이전트는 다시 끈다.
            if (_self != null && _self.enabled)
            {
                _self.enabled = false;
            }
            if (_agent != null && _agent.enabled)
            {
                _agent.enabled = false;
            }
            Follow();
        }

        private void SetVisible(bool visible)
        {
            if (_visible == visible)
            {
                return;
            }
            _visible = visible;
            foreach (Renderer target in _renderers)
            {
                // 원래 꺼 둔 캡슐(Visual) 같은 렌더러는 건드리지 않도록 모델 아래만 켜고 끈다.
                if (target != null && target.transform.IsChildOf(transform) && target.GetComponentInParent<Animator>() != null)
                {
                    target.enabled = visible;
                }
            }
        }

        private void Follow()
        {
            Transform leader = _leader.transform;
            Vector3 target = leader.TransformPoint(_offset);
            // 벽 너머로 자리 잡지 않도록 리더에서 목표까지 NavMesh 위로만 잇는다.
            if (NavMesh.Raycast(leader.position, target, out NavMeshHit wallHit, NavMesh.AllAreas))
            {
                target = wallHit.position;
            }
            if (NavMesh.SamplePosition(target, out NavMeshHit ground, 1.5f, NavMesh.AllAreas))
            {
                target = ground.position;
            }
            Vector3 position = transform.position;
            Vector3 next = Vector3.MoveTowards(position, target, _maxSpeed * Time.deltaTime);
            Vector3 flat = next - position;
            flat.y = 0f;
            transform.position = next;
            Quaternion facing = flat.sqrMagnitude > 0.0004f ? Quaternion.LookRotation(flat) : leader.rotation;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, _turnSpeed * Time.deltaTime);
        }

        private void OnDrawGizmosSelected()
        {
            if (_leader != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position + Vector3.up, _leader.transform.position + Vector3.up);
            }
        }
    }
}

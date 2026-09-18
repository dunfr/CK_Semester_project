using UnityEngine;
using UnityEngine.AI;

namespace CK.SemesterProject.Tutorial
{
    // 전투 계산과 분리된 자리 배치. NavMesh와 실제 장애물을 모두 확인한다.
    public sealed class TutorialBattleFormation
    {
        private const float SampleDistance = 0.6f;
        private const float GroundClearance = 0.08f;
        private readonly CharacterController _player;
        private readonly NavMeshAgent _enemy;
        private readonly NavMeshAgent[] _members;
        private readonly Pose[] _originalPoses;
        private readonly bool[] _agentEnabled;
        public Vector3[] EnemySlots { get; private set; }
        private Vector3 _playerPosition;
        private Quaternion _playerRotation;
        private bool _playerEnabled;
        private bool _isPlaced;

        public TutorialBattleFormation(CharacterController player, NavMeshAgent enemy)
            : this(player, new[] { enemy })
        {
        }

        public TutorialBattleFormation(CharacterController player, NavMeshAgent[] enemies)
        {
            _player = player;
            _members = enemies;
            _enemy = enemies != null && enemies.Length > 0 ? enemies[0] : null;
            _originalPoses = new Pose[enemies == null ? 0 : enemies.Length];
            _agentEnabled = new bool[_originalPoses.Length];
        }

        public bool TryPlace(float spacing, System.Func<Vector3, Vector3, bool> canFrame = null)
        {
            if (_isPlaced || _player == null || _enemy == null || spacing < 2f || float.IsNaN(spacing) || float.IsInfinity(spacing)
                || !TryFindPositions(spacing, canFrame, out Vector3 playerSlot, out Vector3 enemySlot))
            {
                return false;
            }
            for (int i = 0; i < _members.Length; i++)
            {
                _originalPoses[i] = new Pose(_members[i].transform.position, _members[i].transform.rotation);
                _agentEnabled[i] = _members[i].enabled;
                _members[i].enabled = false;
            }
            _playerPosition = _player.transform.position;
            _playerRotation = _player.transform.rotation;
            _playerEnabled = _player.enabled;
            // Agent의 회피·회전과 CharacterController의 충돌 보정이 전투 자리를 밀지 않게 한다.
            _player.enabled = false;
            _enemy.enabled = false;
            Vector3 forward = Vector3.ProjectOnPlane(enemySlot - playerSlot, Vector3.up).normalized;
            _player.transform.SetPositionAndRotation(playerSlot, Quaternion.LookRotation(forward));
            _enemy.transform.SetPositionAndRotation(enemySlot, Quaternion.LookRotation(-forward));
            for (int i = 0; i < _members.Length; i++)
            {
                _members[i].transform.SetPositionAndRotation(EnemySlots[i],
                    Quaternion.LookRotation(Vector3.ProjectOnPlane(playerSlot - EnemySlots[i], Vector3.up)));
            }
            Physics.SyncTransforms();
            _isPlaced = true;
            return true;
        }

        public void Restore(Vector3? respawnPosition = null)
        {
            if (!_isPlaced)
            {
                return;
            }
            if (_player != null)
            {
                _player.enabled = false;
                _player.transform.SetPositionAndRotation(respawnPosition ?? _playerPosition, _playerRotation);
                _player.enabled = _playerEnabled;
            }
            for (int i = 0; i < _members.Length; i++)
            {
                if (_members[i] == null)
                {
                    continue;
                }
                _members[i].enabled = false;
                _members[i].transform.SetPositionAndRotation(_originalPoses[i].position, _originalPoses[i].rotation);
                _members[i].enabled = _agentEnabled[i];
            }
            Physics.SyncTransforms();
            _isPlaced = false;
        }

        private bool TryFindPositions(float spacing, System.Func<Vector3, Vector3, bool> canFrame, out Vector3 playerSlot, out Vector3 enemySlot)
        {
            playerSlot = default;
            enemySlot = default;
            Physics.SyncTransforms();
            var filter = new NavMeshQueryFilter { agentTypeID = _enemy.agentTypeID, areaMask = _enemy.areaMask };
            if (!NavMesh.SamplePosition(_player.transform.position, out NavMeshHit playerStart, 1f, filter)
                || !NavMesh.SamplePosition(_enemy.transform.position, out NavMeshHit enemyStart, 1f, filter))
            {
                return false;
            }
            Vector3 center = (playerStart.position + enemyStart.position) * 0.5f;
            Vector3 direction = Vector3.ProjectOnPlane(enemyStart.position - playerStart.position, Vector3.up).normalized;
            if (direction.sqrMagnitude < 0.01f)
            {
                direction = Vector3.forward;
            }
            var path = new NavMeshPath();
            // 현재 대치 방향부터 좌우로 돌려 보며 통로의 긴 방향을 찾는다.
            float[] angles = { 0, 45, -45, 90, -90, 135, -135, 180 };
            foreach (float angle in angles)
            {
                Vector3 axis = Quaternion.Euler(0, angle, 0) * direction;
                if (!NavMesh.SamplePosition(center - axis * spacing * 0.5f, out NavMeshHit left, SampleDistance, filter)
                    || !NavMesh.SamplePosition(center + axis * spacing * 0.5f, out NavMeshHit right, SampleDistance, filter))
                {
                    continue;
                }
                float distance = Vector3.Distance(left.position, right.position);
                if (distance < spacing - SampleDistance || Mathf.Abs(left.position.y - right.position.y) > 0.5f
                    || !HasSpace(left.position, _player.radius, _player.height)
                    || !HasSpace(right.position, Mathf.Max(0.55f, _enemy.radius), _enemy.height)
                    || !HasSight(left.position, right.position)
                    || !TryFindMemberSlots(left.position, right.position)
                    || (canFrame != null && !canFrame(left.position, right.position))
                    || !HasPath(playerStart.position, left.position, filter, path)
                    || !HasPath(enemyStart.position, right.position, filter, path))
                {
                    continue;
                }
                playerSlot = left.position;
                enemySlot = right.position;
                return true;
            }
            return false;
        }

        private bool HasSpace(Vector3 feet, float radius, float height)
        {
            Vector3 bottom = feet + Vector3.up * (radius + GroundClearance);
            Vector3 top = feet + Vector3.up * Mathf.Max(radius + GroundClearance, height - radius);
            foreach (Collider collider in Physics.OverlapCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!IsParticipant(collider.transform))
                {
                    return false;
                }
            }
            return true;
        }

        private bool HasSight(Vector3 playerSlot, Vector3 enemySlot)
        {
            Vector3 direction = enemySlot - playerSlot;
            foreach (RaycastHit hit in Physics.RaycastAll(playerSlot + Vector3.up, direction.normalized,
                direction.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!IsParticipant(hit.transform))
                {
                    return false;
                }
            }
            return true;
        }

        private bool IsParticipant(Transform target)
        {
            if (target.IsChildOf(_player.transform))
            {
                return true;
            }
            foreach (NavMeshAgent member in _members)
            {
                if (member != null && target.IsChildOf(member.transform))
                {
                    return true;
                }
            }
            return false;
        }

        private bool TryFindMemberSlots(Vector3 playerSlot, Vector3 center)
        {
            if (_members == null || _members.Length < 1 || _members.Length > 3)
            {
                return false;
            }
            var slots = new Vector3[_members.Length];
            Vector3 sideways = Vector3.Cross(Vector3.up, (center - playerSlot).normalized);
            for (int i = 0; i < _members.Length; i++)
            {
                NavMeshAgent member = _members[i];
                if (member == null)
                {
                    return false;
                }
                var filter = new NavMeshQueryFilter { agentTypeID = member.agentTypeID, areaMask = member.areaMask };
                float offset = _members.Length == 2 ? (i - 0.5f) * 2.5f : i == 0 ? 0f : i == 1 ? -2.5f : 2.5f;
                if (!NavMesh.SamplePosition(center + sideways * offset, out NavMeshHit hit, SampleDistance, filter)
                    || Mathf.Abs(hit.position.y - playerSlot.y) > 0.5f
                    || !HasSpace(hit.position, Mathf.Max(0.55f, member.radius), member.height)
                    || !HasSight(playerSlot, hit.position)
                    || !NavMesh.SamplePosition(member.transform.position, out NavMeshHit start, 1f, filter)
                    || !HasPath(start.position, hit.position, filter, new NavMeshPath()))
                {
                    return false;
                }
                for (int j = 0; j < i; j++)
                {
                    if (Vector3.Distance(slots[j], hit.position) < 1.5f)
                    {
                        return false;
                    }
                }
                slots[i] = hit.position;
            }
            EnemySlots = slots;
            return true;
        }

        private static bool HasPath(Vector3 start, Vector3 end, NavMeshQueryFilter filter, NavMeshPath path)
        {
            return NavMesh.CalculatePath(start, end, filter, path) && path.status == NavMeshPathStatus.PathComplete;
        }
    }
}

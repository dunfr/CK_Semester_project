using Semester.Enemies;
using UnityEngine;
using UnityEngine.AI;

namespace CK.SemesterProject.LevelDesign
{
    /// <summary>
    /// 03 구역 T01 덕트 a 파손음. 복도 b 진입 또는 3-6 카드 키 문 개방 중 먼저 일어난 쪽에서 1회 발동한다.
    /// 감시 적은 소리 방향만 보고, 순찰 적은 3-6 문을 지나 덕트 a를 확인한 뒤 순찰로 돌아간다.
    /// </summary>
    public sealed class DuctNoiseEvent : MonoBehaviour
    {
        [SerializeField] private LevelProgress _progress;
        [SerializeField, Tooltip("덕트 a 파손 위치")]
        private Transform _noisePoint;
        [SerializeField, Min(0f), Tooltip("소리 범위 표시 반경(m)")]
        private float _pulseRadius = 10f;
        [SerializeField, Tooltip("제자리에서 소리 방향만 바라보는 적(A1)")]
        private EnemyStateMachine[] _observers;
        [SerializeField, Tooltip("덕트 a를 확인하러 가는 적(A2, A3)")]
        private EnemyStateMachine[] _investigators;
        [SerializeField, Tooltip("확인하러 가는 적이 지나갈 문(3-6)")]
        private LevelDoor _passageDoor;
        [SerializeField] private NoisePulse _pulse;

        private bool _hasTriggered;

        private void Awake()
        {
            if (_progress == null || _noisePoint == null)
            {
                Debug.LogError("DuctNoiseEvent: 진행 상태 또는 소리 위치가 없습니다.", this);
                enabled = false;
            }
        }

        public void Trigger()
        {
            if (!enabled || _hasTriggered)
            {
                return;
            }
            _hasTriggered = true;
            Vector3 point = _noisePoint.position;
            if (_pulse != null)
            {
                _pulse.Play(point, _pulseRadius);
            }
            foreach (EnemyStateMachine observer in _observers)
            {
                if (observer != null)
                {
                    observer.ObserveNoise(point);
                }
            }
            if (_passageDoor != null)
            {
                _passageDoor.Open();
            }
            Vector3 floorPoint = NavMesh.SamplePosition(point, out NavMeshHit hit, 8f, NavMesh.AllAreas) ? hit.position : point;
            foreach (EnemyStateMachine investigator in _investigators)
            {
                if (investigator != null)
                {
                    investigator.InvestigatePosition(floorPoint);
                }
            }
        }
    }
}

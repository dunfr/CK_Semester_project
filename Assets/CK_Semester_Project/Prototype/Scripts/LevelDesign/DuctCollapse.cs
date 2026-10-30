using System.Collections;
using Semester.Enemies;
using UnityEngine;
using UnityEngine.AI;

namespace CK.SemesterProject.LevelDesign
{
    /// <summary>
    /// 03 구역 T03 덕트 b 붕괴. 플레이어가 덕트 위에 올라서면 덕트가 0.8초간 내려앉아 플레이어가 떨어지고,
    /// 소리 범위를 표시하며 지정된 적이 낙하 지점으로 이동한다. C02 덕트 b 상태: 정상 → 붕괴.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class DuctCollapse : MonoBehaviour
    {
        [SerializeField] private LevelProgress _progress;
        [SerializeField, Tooltip("함께 내려앉을 덕트 b 메시(더미 덕트 포함)")]
        private Transform[] _ductParts;
        [SerializeField, Min(0f), Tooltip("내려앉는 거리(m)")]
        private float _dropDistance = 5f;
        [SerializeField, Min(0.01f), Tooltip("하강 시간(초). 3-5-A01 0.8초")]
        private float _dropSeconds = 0.8f;
        [SerializeField, Tooltip("소리 위치이자 적이 모일 낙하 지점")]
        private Transform _landingPoint;
        [SerializeField, Min(0f), Tooltip("소리 범위 표시 반경(m)")]
        private float _pulseRadius = 14f;
        [SerializeField, Tooltip("낙하 지점으로 모일 적(C3~C6)")]
        private EnemyStateMachine[] _investigators;
        [SerializeField] private NoisePulse _pulse;

        private bool _hasCollapsed;

        public bool HasCollapsed => _hasCollapsed;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            // 씬에서 덕트 메시가 지워지면 빈 칸이 남으므로 빼고 쓴다.
            _ductParts = _ductParts == null ? null : System.Array.FindAll(_ductParts, part => part != null);
            if (_progress == null || _landingPoint == null || _ductParts == null || _ductParts.Length == 0)
            {
                Debug.LogError("DuctCollapse: 덕트 b 참조가 누락되었습니다.", this);
                enabled = false;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!enabled || _hasCollapsed || _progress.Player == null || other.transform != _progress.Player)
            {
                return;
            }
            _hasCollapsed = true;
            StartCoroutine(Collapse());
        }

        private IEnumerator Collapse()
        {
            foreach (Transform part in _ductParts)
            {
                foreach (Collider partCollider in part.GetComponentsInChildren<Collider>())
                {
                    partCollider.enabled = false;
                }
            }
            Vector3[] starts = new Vector3[_ductParts.Length];
            for (int i = 0; i < _ductParts.Length; i++)
            {
                starts[i] = _ductParts[i].position;
            }
            for (float elapsed = 0f; elapsed < _dropSeconds; elapsed += Time.deltaTime)
            {
                float progress = elapsed / _dropSeconds;
                SetDrop(starts, progress * progress);
                yield return null;
            }
            SetDrop(starts, 1f);
            Vector3 point = _landingPoint.position;
            if (_pulse != null)
            {
                _pulse.Play(point, _pulseRadius);
            }
            Vector3 floorPoint = NavMesh.SamplePosition(point, out NavMeshHit hit, 4f, NavMesh.AllAreas) ? hit.position : point;
            foreach (EnemyStateMachine investigator in _investigators)
            {
                if (investigator != null && investigator.isActiveAndEnabled)
                {
                    investigator.InvestigatePosition(floorPoint);
                }
            }
            _progress.SetObjective("적을 피하거나 처치하고 출구로 이동");
        }

        private void SetDrop(Vector3[] starts, float progress)
        {
            for (int i = 0; i < _ductParts.Length; i++)
            {
                _ductParts[i].position = starts[i] + Vector3.down * (_dropDistance * progress);
            }
        }
    }
}

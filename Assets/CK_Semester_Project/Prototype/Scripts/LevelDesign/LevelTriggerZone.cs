using UnityEngine;
using UnityEngine.Events;

namespace CK.SemesterProject.LevelDesign
{
    /// <summary>플레이어 진입 시 안내 문구·목표·체크포인트·이벤트를 실행하는 범위(T 트리거, 구역 진입).</summary>
    [RequireComponent(typeof(Collider))]
    public sealed class LevelTriggerZone : MonoBehaviour
    {
        [SerializeField] private LevelProgress _progress;
        [SerializeField, Tooltip("한 번만 발동합니다(기획서 횟수 1회).")]
        private bool _isOnce = true;
        [SerializeField, TextArea, Tooltip("진입 시 표시할 문구(대사 등). 비우면 표시하지 않습니다.")]
        private string _message;
        [SerializeField, Min(0.1f), Tooltip("문구 표시 시간(초)")]
        private float _messageSeconds = 3f;
        [SerializeField, Tooltip("진입 시 바꿀 현재 목표. 비우면 유지합니다.")]
        private string _objective;
        [SerializeField, Tooltip("전투 패배 시 이 위치(진입 시점의 플레이어 위치)에서 다시 시작합니다.")]
        private bool _isCheckpoint;
        [SerializeField] private UnityEvent _entered;

        private bool _hasFired;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            if (_progress == null)
            {
                Debug.LogError(name + ": LevelProgress 참조가 없습니다.", this);
                enabled = false;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!enabled || (_isOnce && _hasFired) || _progress.Player == null || other.transform != _progress.Player)
            {
                return;
            }
            _hasFired = true;
            _progress.ShowMessage(_message, _messageSeconds);
            if (!string.IsNullOrEmpty(_objective))
            {
                _progress.SetObjective(_objective);
            }
            if (_isCheckpoint)
            {
                _progress.Battle.SetRespawnPoint(_progress.Player.position);
            }
            _entered.Invoke();
        }
    }
}

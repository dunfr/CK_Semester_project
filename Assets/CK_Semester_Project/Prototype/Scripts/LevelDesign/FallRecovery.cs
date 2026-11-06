using UnityEngine;
using UnityEngine.AI;

namespace CK.SemesterProject.LevelDesign
{
    /// <summary>
    /// 플레이어가 구멍이나 맵 밖으로 떨어지면 마지막 체크포인트(체크포인트 트리거 영역에 들어간 위치)로 되돌린다.
    /// 씬에 따로 놓지 않아도 LevelProgress가 시작할 때 자동으로 붙는다. 값을 바꾸려면 LevelProgress 오브젝트에 직접 추가한다.
    /// 전투·컷신·연출 잠금(엘리베이터 등) 동안에는 판단하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FallRecovery : MonoBehaviour
    {
        [SerializeField, Min(1f), Tooltip("마지막으로 서 있던 높이보다 이만큼(m) 아래로 떨어지면 되돌립니다. 덕트 붕괴처럼 일부러 떨어뜨리는 높이(약 5m)보다 크게 둡니다.")]
        private float _maxFallHeight = 10f;
        [SerializeField, Min(0f), Tooltip("가장 낮은 NavMesh 바닥보다 이만큼(m) 더 내려가면 높이와 상관없이 바로 되돌립니다.")]
        private float _belowLowestFloor = 4f;
        [SerializeField, TextArea, Tooltip("되돌린 뒤 보여 줄 문구. 비우면 표시하지 않습니다.")]
        private string _message = "떨어져서 마지막 체크포인트로 돌아왔습니다.";
        [SerializeField, Min(0.1f), Tooltip("문구 표시 시간(초)")]
        private float _messageSeconds = 2.5f;

        private LevelProgress _progress;
        private CharacterController _controller;
        private float _lastGroundY;
        private float _killHeight = float.NegativeInfinity;

        private void Awake()
        {
            _progress = GetComponent<LevelProgress>();
            if (_progress == null)
            {
                _progress = FindFirstObjectByType<LevelProgress>();
            }
        }

        private void Start()
        {
            // 맵 맨 아래층을 NavMesh에서 찾아, 그보다 아래는 "맵 밖"으로 본다(씬마다 따로 높이를 적지 않아도 된다).
            Vector3[] vertices = NavMesh.CalculateTriangulation().vertices;
            if (vertices.Length > 0)
            {
                float lowest = float.PositiveInfinity;
                foreach (Vector3 vertex in vertices)
                {
                    lowest = Mathf.Min(lowest, vertex.y);
                }
                _killHeight = lowest - _belowLowestFloor;
            }
            ResetTracking();
        }

        private void Update()
        {
            Transform player = _progress != null ? _progress.Player : null;
            if (player == null || _progress.Battle == null)
            {
                return;
            }
            if (_controller == null || _controller.transform != player)
            {
                _controller = player.GetComponent<CharacterController>();
            }
            // 전투 배치·컷신·엘리베이터처럼 코드가 플레이어를 옮기는 동안에는 낙하로 보지 않는다.
            if (_controller == null || !_controller.enabled || _progress.Battle.IsInBattle || _progress.Battle.IsFieldEventPlaying())
            {
                _lastGroundY = player.position.y;
                return;
            }
            float y = player.position.y;
            if (_controller.isGrounded)
            {
                _lastGroundY = y;
                return;
            }
            if (_lastGroundY - y < _maxFallHeight && y > _killHeight)
            {
                return;
            }
            if (_progress.Battle.RecoverToRespawnPoint())
            {
                _progress.ShowMessage(_message, _messageSeconds);
                ResetTracking();
            }
        }

        private void ResetTracking()
        {
            Transform player = _progress != null ? _progress.Player : null;
            _lastGroundY = player != null ? player.position.y : 0f;
        }
    }
}

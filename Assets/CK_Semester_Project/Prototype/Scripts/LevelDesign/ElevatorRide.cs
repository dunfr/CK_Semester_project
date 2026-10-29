using System.Collections;
using UnityEngine;

namespace CK.SemesterProject.LevelDesign
{
    /// <summary>02 구역 층수 조작 데스크(2-5). 탑승한 플레이어와 엘리베이터 카를 하층까지 옮기고 층수 패널(2-4)을 갱신한다.</summary>
    public sealed class ElevatorRide : LevelInteractable
    {
        [SerializeField, Tooltip("함께 이동할 카 구성 요소(더미 카 메시, 패널, 데스크, 이 트리거 등)")]
        private Transform[] _carParts;
        [SerializeField, Tooltip("플레이어가 이 범위 안에 있어야 운행합니다.")]
        private BoxCollider _boardingZone;
        [SerializeField, Tooltip("이동량(m). 음수면 아래로 내려갑니다.")]
        private float _travelHeight = -55f;
        [SerializeField, Min(0.1f), Tooltip("운행 시간(초)")]
        private float _travelSeconds = 7f;
        [SerializeField, Min(1f), Tooltip("층 표시가 바뀌는 높이 간격(m)")]
        private float _floorHeight = 5f;
        [SerializeField, Tooltip("출발 시 닫을 문(선택)")]
        private LevelDoor _departureDoor;
        [SerializeField, Tooltip("도착 후 열 문")]
        private LevelDoor _arrivalDoor;
        [SerializeField, Tooltip("층수 패널 표시")]
        private TextMesh[] _floorDisplays;

        private bool _hasArrived;
        private bool _isMoving;

        public override bool CanInteract => !_hasArrived && !_isMoving;

        protected override void Awake()
        {
            base.Awake();
            if (_carParts == null || _carParts.Length == 0 || _boardingZone == null || _arrivalDoor == null)
            {
                Debug.LogError(name + ": 엘리베이터 참조가 누락되었습니다.", this);
                enabled = false;
                return;
            }
            SetFloorDisplays(0);
        }

        public override void Interact()
        {
            if (!_boardingZone.bounds.Contains(Progress.Player.position + Vector3.up * 0.5f))
            {
                Progress.ShowMessage("엘리베이터 안에 탑승한 뒤 조작하세요.", 2f);
                return;
            }
            StartCoroutine(Ride());
        }

        private IEnumerator Ride()
        {
            _isMoving = true;
            Progress.LockControls(this);
            if (_departureDoor != null)
            {
                _departureDoor.CloseImmediately();
            }
            Transform player = Progress.Player;
            CharacterController controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            Vector3[] starts = new Vector3[_carParts.Length];
            for (int i = 0; i < _carParts.Length; i++)
            {
                starts[i] = _carParts[i].position;
            }
            Vector3 playerStart = player.position;
            for (float elapsed = 0f; elapsed < _travelSeconds; elapsed += Time.deltaTime)
            {
                MoveCar(starts, playerStart, Mathf.SmoothStep(0f, 1f, elapsed / _travelSeconds));
                yield return null;
            }
            MoveCar(starts, playerStart, 1f);
            Physics.SyncTransforms();
            controller.enabled = true;
            _hasArrived = true;
            _arrivalDoor.Open();
            Progress.Battle.SetRespawnPoint(player.position);
            Progress.UnlockControls(this);
            Progress.SetObjective("복도 구역 통과");
            _isMoving = false;
        }

        private void MoveCar(Vector3[] starts, Vector3 playerStart, float progress)
        {
            Vector3 offset = Vector3.up * (_travelHeight * progress);
            for (int i = 0; i < _carParts.Length; i++)
            {
                _carParts[i].position = starts[i] + offset;
            }
            Progress.Player.position = playerStart + offset;
            SetFloorDisplays(Mathf.Abs(_travelHeight * progress));
        }

        private void SetFloorDisplays(float travelled)
        {
            int floor = Mathf.FloorToInt(travelled / _floorHeight + 0.001f);
            string label = floor == 0 ? "1F" : "B" + floor;
            foreach (TextMesh display in _floorDisplays)
            {
                if (display != null)
                {
                    display.text = label;
                }
            }
        }
    }
}

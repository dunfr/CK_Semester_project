using Unity.Cinemachine;
using UnityEngine;

namespace CK.SemesterProject.Tutorial
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController), typeof(PlayerMovement))]
    public sealed class FloorTravelButtons : MonoBehaviour
    {
        [SerializeField, Tooltip("2층 이동 지점의 월드 좌표 (m).")]
        private Vector3 _secondFloorPosition;
        [SerializeField] private Font _font;

        private static readonly Rect PanelRect = new Rect(16f, 16f, 256f, 100f);
        private CharacterController _controller;
        private PlayerMovement _movement;
        private CinemachineCamera _followCamera;
        private Camera _outputCamera;
        private Vector3 _firstFloorPosition;
        private GUIStyle _buttonStyle;
        private GUIStyle _labelStyle;

        private void Awake()
        {
            _firstFloorPosition = transform.position;
            _controller = GetComponent<CharacterController>();
            _movement = GetComponent<PlayerMovement>();
            _followCamera = GetComponentInChildren<CinemachineCamera>();
            _outputCamera = Camera.main;
        }

        private void OnGUI()
        {
            if (_buttonStyle == null)
            {
                _buttonStyle = new GUIStyle(GUI.skin.button) { font = _font, fontSize = 20 };
                _labelStyle = new GUIStyle(GUI.skin.label) { font = _font, fontSize = 14 };
            }

            GUI.Box(PanelRect, GUIContent.none);
            GUI.Label(new Rect(28f, 24f, 236f, 24f), "층 이동 · Esc로 마우스 해제", _labelStyle);
            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && _movement.enabled && Cursor.lockState != CursorLockMode.Locked;
            if (GUI.Button(new Rect(28f, 58f, 108f, 42f), "1층", _buttonStyle))
            {
                TravelTo(_firstFloorPosition);
            }
            if (GUI.Button(new Rect(148f, 58f, 108f, 42f), "2층", _buttonStyle))
            {
                TravelTo(_secondFloorPosition);
            }
            GUI.enabled = wasEnabled;
        }

        public bool ContainsScreenPoint(Vector2 screenPosition)
        {
            return isActiveAndEnabled && PanelRect.Contains(new Vector2(screenPosition.x, Screen.height - screenPosition.y));
        }

        public void TravelTo(Vector3 destination)
        {
            if (!_movement.enabled)
            {
                return;
            }

            bool wasMoving = _movement.enabled;
            bool wasColliding = _controller.enabled;
            _movement.enabled = false;
            _controller.enabled = false;
            try
            {
                Physics.SyncTransforms();
                RaycastHit ground;
                if (!Physics.Raycast(destination + Vector3.up * 0.3f, Vector3.down, out ground,
                    5f, ~0, QueryTriggerInteraction.Ignore) || ground.normal.y < 0.7f)
                {
                    Debug.LogWarning("층 이동 지점 아래에 안전한 바닥이 없습니다: " + destination, this);
                    return;
                }
                destination = ground.point + Vector3.up * 0.05f;
                Vector3 lower = destination + _controller.center + Vector3.down * (_controller.height * 0.5f - _controller.radius);
                Vector3 upper = destination + _controller.center + Vector3.up * (_controller.height * 0.5f - _controller.radius);
                if (Physics.CheckCapsule(lower, upper, _controller.radius, ~0, QueryTriggerInteraction.Ignore))
                {
                    Debug.LogWarning("층 이동 지점이 장애물과 겹칩니다: " + destination, this);
                    return;
                }

                Vector3 delta = destination - transform.position;
                transform.position = destination;
                // 카메라가 플레이어 자식이어도 위치 보정이 중복되지 않게 이전 추적 상태만 초기화합니다.
                if (_followCamera != null)
                {
                    _followCamera.PreviousStateIsValid = false;
                }
                if (_outputCamera != null && !_outputCamera.transform.IsChildOf(transform))
                {
                    _outputCamera.transform.position += delta;
                }
                Physics.SyncTransforms();
            }
            finally
            {
                _controller.enabled = wasColliding;
                _movement.enabled = wasMoving;
            }
        }
    }
}

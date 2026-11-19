using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CK.SemesterProject.Tutorial
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CinemachineFollow))]
    public sealed class CameraController : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float _sensitivity = 0.12f;
        [SerializeField, Min(0.5f)] private float _distance = 4.5f;
        [SerializeField] private float _targetHeight = 1.3f;
        [SerializeField, Range(-80f, 80f)] private float _minPitch = 10f;
        [SerializeField, Range(-80f, 80f)] private float _maxPitch = 70f;
        [SerializeField, Range(-80f, 80f)] private float _pitch = 30f;

        private CinemachineFollow _follow;
        private FloorTravelButtons _floorTravelButtons;
        private float _yaw;
        private bool _skipMouseDelta;
        private bool _isCursorFreedByAlt;

        private void Awake()
        {
            _follow = GetComponent<CinemachineFollow>();
            _floorTravelButtons = GetComponentInParent<FloorTravelButtons>();
            _follow.TrackerSettings.BindingMode = BindingMode.WorldSpace;
            Vector3 offset = _follow.FollowOffset;
            _yaw = Mathf.Atan2(-offset.x, -offset.z) * Mathf.Rad2Deg;
            ApplyOrbit();
        }

        private void OnEnable()
        {
            SetCursorLocked(true);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                _isCursorFreedByAlt = false;
                SetCursorLocked(false);
                return;
            }

            if (UpdateAltCursor())
            {
                return;
            }

            Mouse mouse = Mouse.current;
            if (!Application.isFocused || mouse == null)
            {
                return;
            }

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (mouse.leftButton.wasPressedThisFrame
                    && (UnityEngine.EventSystems.EventSystem.current == null
                        || !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                    && (_floorTravelButtons == null || !_floorTravelButtons.ContainsScreenPoint(mouse.position.ReadValue())))
                {
                    SetCursorLocked(true);
                }
                return;
            }

            if (_skipMouseDelta)
            {
                _skipMouseDelta = false;
                return;
            }

            // 마우스 delta는 프레임 동안 누적된 이동량이므로 deltaTime을 곱하지 않습니다.
            Vector2 delta = mouse.delta.ReadValue();
            _yaw = Mathf.Repeat(_yaw + delta.x * _sensitivity, 360f);
            _pitch = Mathf.Clamp(_pitch - delta.y * _sensitivity, _minPitch, _maxPitch);
            ApplyOrbit();
        }

        // Alt를 누르는 동안 커서를 풀어 HUD(이동 속도, 설정 버튼 등)를 클릭할 수 있게 한다. 떼면 다시 잠근다.
        // 커서를 Alt로 풀어 둔 동안이면 true를 돌려 카메라 회전과 클릭 재잠금을 건너뛴다.
        private bool UpdateAltCursor()
        {
            Keyboard keyboard = Keyboard.current;
            bool isAltHeld = keyboard != null && (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed);
            if (isAltHeld)
            {
                if (Cursor.lockState == CursorLockMode.Locked && Time.timeScale > 0f)
                {
                    SetCursorLocked(false);
                    _isCursorFreedByAlt = true;
                }
                return _isCursorFreedByAlt;
            }

            if (!_isCursorFreedByAlt)
            {
                return false;
            }

            _isCursorFreedByAlt = false;
            // Alt를 누른 채 설정 메뉴 등을 열어 시간이 멈췄으면 커서를 그 화면에 남겨 둔다.
            if (Time.timeScale > 0f)
            {
                SetCursorLocked(true);
            }
            return true;
        }

        public void FaceDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude > 0.001f)
            {
                _yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                ApplyOrbit();
                _skipMouseDelta = true;
            }
        }

        private void ApplyOrbit()
        {
            _follow.FollowOffset = Vector3.up * _targetHeight
                + Quaternion.Euler(_pitch, _yaw, 0f) * Vector3.back * _distance;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                SetCursorLocked(false);
            }
        }

        private void OnDisable()
        {
            SetCursorLocked(false);
        }

        private void OnValidate()
        {
            _maxPitch = Mathf.Max(_minPitch, _maxPitch);
            _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);
        }

        private void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
            _skipMouseDelta = true;
        }
    }
}

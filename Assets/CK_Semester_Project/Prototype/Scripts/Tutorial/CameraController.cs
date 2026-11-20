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
        [SerializeField, Min(0.5f)] private float _distance = 4f;
        [SerializeField] private float _targetHeight = 1.4f;
        [SerializeField, Range(-80f, 80f), Tooltip("음수면 캐릭터보다 낮은 곳에서 올려다본다(도)")]
        private float _minPitch = -35f;
        [SerializeField, Range(-80f, 80f)] private float _maxPitch = 70f;
        [SerializeField, Range(-80f, 80f)] private float _pitch = 20f;

        [Header("Follow")]
        [SerializeField, Min(0f), Tooltip("수평 위치가 캐릭터를 따라붙는 데 걸리는 시간(초). 0이면 즉시 붙는다.")]
        private float _horizontalDamping = 0.2f;
        [SerializeField, Min(0f), Tooltip("수직 위치 따라붙는 시간(초). 점프·계단에서 화면이 덜 흔들리도록 수평보다 길게 둔다.")]
        private float _verticalDamping = 0.35f;
        [SerializeField, Min(0f), Tooltip("벽에 밀려 당겨졌던 카메라가 원래 거리로 돌아가는 시간(초). 당겨지는 쪽은 즉시.")]
        private float _obstacleReturnDamping = 0.3f;

        [Header("Auto Align")]
        [SerializeField, Min(0f), Tooltip("마우스 조작 후 이만큼(초) 지나야 이동 방향 뒤로 자동 회전을 시작한다.")]
        private float _autoAlignDelay = 1.5f;
        [SerializeField, Min(0f), Tooltip("이동 방향 뒤로 돌아가는 속도(1/초). 0이면 끈다.")]
        private float _autoAlignSpeed = 1.5f;
        [SerializeField, Min(0f), Tooltip("이 속도(m/s) 이상 움직일 때만 자동 회전한다.")]
        private float _autoAlignMinSpeed = 0.5f;
        [SerializeField, Range(0f, 180f), Tooltip("카메라 정면과 이동 방향의 각도가 이보다 크면(카메라 쪽으로 달려오면) 돌리지 않는다(도).")]
        private float _autoAlignMaxAngle = 135f;

        [Header("Look Up")]
        [SerializeField, Min(0.5f), Tooltip("가장 많이 올려다볼 때(최소 피치)의 거리(m). 피치 0°부터 이 값까지 줄어든다.")]
        private float _lookUpDistance = 2f;

        [Header("Zoom")]
        [SerializeField, Min(0.5f), Tooltip("휠로 당길 수 있는 가장 가까운 거리(m)")]
        private float _minDistance = 2.5f;
        [SerializeField, Min(0.5f), Tooltip("휠로 밀 수 있는 가장 먼 거리(m)")]
        private float _maxDistance = 8f;
        [SerializeField, Min(0.05f), Tooltip("휠 한 칸에 바뀌는 거리(m)")]
        private float _zoomStep = 0.6f;
        [SerializeField, Min(0.1f), Tooltip("확대/축소가 목표 거리로 따라가는 속도(1/초)")]
        private float _zoomSmoothing = 10f;

        [Header("Idle Return")]
        [SerializeField, Min(0f), Tooltip("마우스·휠을 이만큼(초) 건드리지 않으면 기본 카메라 위치로 돌아간다. 0이면 끈다.")]
        private float _idleReturnDelay = 4f;
        [SerializeField, Min(0.1f), Tooltip("기본 위치로 돌아가는 속도(1/초)")]
        private float _idleReturnSpeed = 1.2f;

        // 휠 한 칸의 원시 값(Windows 120). 장치마다 달라도 방향만 쓰도록 부호로 나눈다.
        private const float ScrollNotch = 120f;
        private const float MouseIdleThreshold = 0.5f;

        private CinemachineFollow _follow;
        private FloorTravelButtons _floorTravelButtons;
        private Transform _followTarget;
        private float _yaw;
        private bool _skipMouseDelta;
        private bool _isCursorFreedByAlt;
        private float _defaultDistance;
        private float _defaultPitch;
        private float _targetDistance;
        private float _lastCameraInputTime;
        private Vector3 _lastTargetPosition;

        private void Awake()
        {
            _follow = GetComponent<CinemachineFollow>();
            _floorTravelButtons = GetComponentInParent<FloorTravelButtons>();
            CinemachineCamera cinemachineCamera = GetComponent<CinemachineCamera>();
            _followTarget = cinemachineCamera != null ? cinemachineCamera.Follow : null;
            _lastTargetPosition = _followTarget != null ? _followTarget.position : Vector3.zero;
            _follow.TrackerSettings.BindingMode = BindingMode.WorldSpace;
            _follow.TrackerSettings.PositionDamping = new Vector3(_horizontalDamping, _verticalDamping, _horizontalDamping);
            ApplyObstacleDamping();
            Vector3 offset = _follow.FollowOffset;
            _yaw = Mathf.Atan2(-offset.x, -offset.z) * Mathf.Rad2Deg;
            _defaultDistance = _distance;
            _defaultPitch = _pitch;
            _targetDistance = _distance;
            _lastCameraInputTime = Time.unscaledTime;
            ApplyOrbit();
        }

        private void OnEnable()
        {
            SetCursorLocked(true);
        }

        private void Update()
        {
            // Esc 메뉴가 이번 프레임의 Esc를 썼으면(열기·닫기) 커서는 메뉴가 정한다.
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame
                && !CK.SemesterProject.UI.InGameMenu.HandledEscapeThisFrame)
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
            if (delta.sqrMagnitude > MouseIdleThreshold * MouseIdleThreshold)
            {
                _lastCameraInputTime = Time.unscaledTime;
            }
            _yaw = Mathf.Repeat(_yaw + delta.x * _sensitivity, 360f);
            _pitch = Mathf.Clamp(_pitch - delta.y * _sensitivity, _minPitch, _maxPitch);
            UpdateZoom(mouse);
            UpdateAutoAlign();
            UpdateIdleReturn();
            ApplyOrbit();
        }

        // 이동 중이고 한동안 마우스를 안 썼으면 이동 방향 뒤로 천천히 돌아간다.
        // 카메라 쪽으로 달려올 때 화면이 뒤집히지 않도록 일정 각도 이상이면 돌리지 않는다.
        private void UpdateAutoAlign()
        {
            if (_followTarget == null)
            {
                return;
            }

            Vector3 targetPosition = _followTarget.position;
            Vector3 moveDelta = Vector3.ProjectOnPlane(targetPosition - _lastTargetPosition, Vector3.up);
            _lastTargetPosition = targetPosition;

            bool isAlignEnabled = _autoAlignSpeed > 0f && Time.deltaTime > 0f;
            bool isWaitingAfterInput = Time.unscaledTime - _lastCameraInputTime < _autoAlignDelay;
            if (!isAlignEnabled || isWaitingAfterInput)
            {
                return;
            }

            float speed = moveDelta.magnitude / Time.deltaTime;
            if (speed < _autoAlignMinSpeed)
            {
                return;
            }

            float moveYaw = Mathf.Atan2(moveDelta.x, moveDelta.z) * Mathf.Rad2Deg;
            if (Mathf.Abs(Mathf.DeltaAngle(_yaw, moveYaw)) > _autoAlignMaxAngle)
            {
                return;
            }

            _yaw = Mathf.LerpAngle(_yaw, moveYaw, 1f - Mathf.Exp(-_autoAlignSpeed * Time.deltaTime));
        }

        private void UpdateZoom(Mouse mouse)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                _lastCameraInputTime = Time.unscaledTime;
                float notches = Mathf.Abs(scroll) >= ScrollNotch ? scroll / ScrollNotch : Mathf.Sign(scroll);
                // 휠을 앞으로 굴리면 가까워진다.
                _targetDistance = Mathf.Clamp(_targetDistance - notches * _zoomStep, _minDistance, _maxDistance);
            }
            _distance = Mathf.Lerp(_distance, _targetDistance, 1f - Mathf.Exp(-_zoomSmoothing * Time.deltaTime));
        }

        // 한동안 카메라를 조작하지 않으면 플레이어 등 뒤의 기본 높이·각도·거리로 천천히 돌아온다.
        private void UpdateIdleReturn()
        {
            if (_idleReturnDelay <= 0f || Time.unscaledTime - _lastCameraInputTime < _idleReturnDelay)
            {
                return;
            }
            float blend = 1f - Mathf.Exp(-_idleReturnSpeed * Time.deltaTime);
            _pitch = Mathf.Lerp(_pitch, _defaultPitch, blend);
            _targetDistance = Mathf.Lerp(_targetDistance, _defaultDistance, blend);
            if (_followTarget != null)
            {
                Vector3 forward = Vector3.ProjectOnPlane(_followTarget.forward, Vector3.up);
                if (forward.sqrMagnitude > 0.001f)
                {
                    float behindYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
                    _yaw = Mathf.LerpAngle(_yaw, behindYaw, blend);
                }
            }
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
                _lastCameraInputTime = Time.unscaledTime;
            }
        }

        private void ApplyOrbit()
        {
            _follow.FollowOffset = Vector3.up * _targetHeight
                + Quaternion.Euler(_pitch, _yaw, 0f) * Vector3.back * GetPitchAdjustedDistance();
        }

        // 올려다볼수록(피치 0° 아래) 카메라가 캐릭터 쪽으로 다가와 바닥에 파묻히지 않게 한다.
        private float GetPitchAdjustedDistance()
        {
            if (_pitch >= 0f || _minPitch >= 0f)
            {
                return _distance;
            }
            float lookUpAmount = Mathf.InverseLerp(0f, _minPitch, _pitch);
            return Mathf.Lerp(_distance, Mathf.Min(_distance, _lookUpDistance), lookUpAmount);
        }

        // 벽에 가려지면 즉시 당기고, 가림이 풀리면 천천히 원래 거리로 돌려보낸다.
        private void ApplyObstacleDamping()
        {
            CinemachineDeoccluder deoccluder = GetComponent<CinemachineDeoccluder>();
            if (deoccluder == null)
            {
                return;
            }
            CinemachineDeoccluder.ObstacleAvoidance avoidance = deoccluder.AvoidObstacles;
            avoidance.Damping = _obstacleReturnDamping;
            avoidance.DampingWhenOccluded = 0f;
            deoccluder.AvoidObstacles = avoidance;
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
            _maxDistance = Mathf.Max(_minDistance, _maxDistance);
            _lookUpDistance = Mathf.Min(_lookUpDistance, _maxDistance);
        }

        private void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
            _skipMouseDelta = true;
        }
    }
}

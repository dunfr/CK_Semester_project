using UnityEngine;
using UnityEngine.InputSystem;

namespace CK.SemesterProject.Prototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class FuchsiaTestMovement : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("기본 이동 속도(m/s)입니다.")]
        private float _moveSpeed = 1.7f;

        [SerializeField, Min(0f), Tooltip("Shift를 누른 동안의 이동 속도(m/s)입니다.")]
        private float _fastMoveSpeed = 2.8f;

        [SerializeField, Min(0f), Tooltip("이동 방향으로 회전하는 속도(도/초)입니다.")]
        private float _rotationSpeed = 540f;

        [SerializeField, Min(0f), Tooltip("바닥에 붙이는 중력 가속도(m/s²)입니다.")]
        private float _gravity = 20f;

        [SerializeField, Min(0.01f), Tooltip("카메라가 위치를 따라가는 반응 속도(1/초)입니다.")]
        private float _cameraFollowSharpness = 10f;

        [SerializeField, Tooltip("복도 장식과 카메라를 고려한 월드 X 이동 범위(m)입니다.")]
        private Vector2 _horizontalLimits = new Vector2(-1.8f, 1.8f);

        [SerializeField, Tooltip("열린 복도 입구로 떨어지지 않도록 제한하는 월드 Z 범위(m)입니다.")]
        private Vector2 _corridorLimits = new Vector2(-2f, 15.5f);

        [SerializeField, Tooltip("현재 구도를 유지하며 따라갈 카메라입니다.")]
        private Transform _followCamera;

        [SerializeField, Tooltip("C키로 전환할 넓은 화면·뒤쪽·정면·얼굴 비교 카메라입니다.")]
        private Camera[] _comparisonCameras = new Camera[0];

        [SerializeField, Min(0), Tooltip("시작할 비교 카메라의 인덱스입니다. 1은 뒤쪽 확대 화면입니다.")]
        private int _initialCameraView = 1;

        private CharacterController _controller;
        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private Vector3 _cameraOffset;
        private float _verticalSpeed;
        private Camera _cameraComponent;
        private int _cameraViewIndex;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _startPosition = transform.position;
            _startRotation = transform.rotation;
            if (_followCamera == null && Camera.main != null)
            {
                _followCamera = Camera.main.transform;
            }

            if (_followCamera != null)
            {
                _cameraOffset = _followCamera.position - transform.position;
                _cameraComponent = _followCamera.GetComponent<Camera>();
                if (_comparisonCameras.Length > 0)
                {
                    ApplyComparisonView(_initialCameraView);
                }
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            bool canReadInput = Application.isFocused && keyboard != null;
            if (canReadInput && keyboard.cKey.wasPressedThisFrame)
            {
                CycleComparisonView();
            }
            if (canReadInput && keyboard.rKey.wasPressedThisFrame)
            {
                ResetTestPosition();
                return;
            }

            Vector2 input = canReadInput ? ReadMovement(keyboard) : Vector2.zero;
            bool isFast = canReadInput && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
            MoveCharacter(input, isFast, Time.deltaTime);
        }

        private void LateUpdate()
        {
            if (_followCamera == null)
            {
                return;
            }

            Vector3 targetPosition = transform.position + _cameraOffset;
            float blend = 1f - Mathf.Exp(-_cameraFollowSharpness * Time.deltaTime);
            _followCamera.position = Vector3.Lerp(_followCamera.position, targetPosition, blend);
        }

        private void OnDisable()
        {
            _verticalSpeed = 0f;
        }

        public void ResetTestPosition()
        {
            if (_controller == null)
            {
                return;
            }

            _controller.enabled = false;
            transform.SetPositionAndRotation(_startPosition, _startRotation);
            _controller.enabled = true;
            _verticalSpeed = 0f;
            if (_followCamera != null)
            {
                _followCamera.position = _startPosition + _cameraOffset;
            }
        }

        public void CycleComparisonView()
        {
            if (_comparisonCameras.Length == 0)
            {
                return;
            }

            ApplyComparisonView((_cameraViewIndex + 1) % _comparisonCameras.Length);
        }

        private void ApplyComparisonView(int viewIndex)
        {
            if (_followCamera == null)
            {
                return;
            }

            _cameraViewIndex = Mathf.Clamp(viewIndex, 0, _comparisonCameras.Length - 1);
            Camera view = _comparisonCameras[_cameraViewIndex];
            if (view == null)
            {
                Debug.LogWarning("푸시아 비교 카메라 연결이 없습니다: " + _cameraViewIndex, this);
                return;
            }

            _cameraOffset = view.transform.position - _startPosition;
            _followCamera.SetPositionAndRotation(transform.position + _cameraOffset, view.transform.rotation);
            if (_cameraComponent != null)
            {
                _cameraComponent.fieldOfView = view.fieldOfView;
            }
        }

        private static Vector2 ReadMovement(Keyboard keyboard)
        {
            float horizontal = ((keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) ? 1f : 0f)
                - ((keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) ? 1f : 0f);
            float vertical = ((keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) ? 1f : 0f)
                - ((keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) ? 1f : 0f);
            return Vector2.ClampMagnitude(new Vector2(horizontal, vertical), 1f);
        }

        private void MoveCharacter(Vector2 input, bool isFast, float deltaTime)
        {
            Vector3 forward = _followCamera != null ? _followCamera.forward : Vector3.forward;
            forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .001f)
            {
                forward = Vector3.forward;
            }

            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector2 clampedInput = Vector2.ClampMagnitude(input, 1f);
            Vector3 direction = forward * clampedInput.y + right * clampedInput.x;
            if (direction.sqrMagnitude > .001f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(direction), _rotationSpeed * deltaTime);
            }

            if (_controller.isGrounded && _verticalSpeed < 0f)
            {
                _verticalSpeed = -2f;
            }

            _verticalSpeed -= _gravity * deltaTime;
            float speed = isFast ? _fastMoveSpeed : _moveSpeed;
            Vector3 targetPosition = transform.position + direction * speed * deltaTime;
            targetPosition.x = Mathf.Clamp(targetPosition.x, _horizontalLimits.x, _horizontalLimits.y);
            targetPosition.z = Mathf.Clamp(targetPosition.z, _corridorLimits.x, _corridorLimits.y);
            Vector3 displacement = targetPosition - transform.position;
            displacement.y = _verticalSpeed * deltaTime;
            CollisionFlags collisions = _controller.Move(displacement);
            if ((collisions & CollisionFlags.Below) != 0 && _verticalSpeed < 0f)
            {
                _verticalSpeed = -2f;
            }
        }
    }
}

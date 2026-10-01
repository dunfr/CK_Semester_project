using UnityEngine;
using UnityEngine.InputSystem;

namespace CK.SemesterProject.Tutorial
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        [SerializeField] private Transform _cameraTransform;
        [SerializeField, Min(0f)] private float _moveSpeed = 3f;
        [SerializeField, Min(0f)] private float _rotationSpeed = 540f;
        [SerializeField, Min(0f)] private float _gravity = 20f;

        [SerializeField, Min(1f), Tooltip("달리기 시 기본 이동 속도 배율.")]
        private float _sprintMultiplier = 1.8f;
        private bool _isSprintToggled;

        public bool IsSprinting { get; private set; }
        public bool IsSprintToggled => _isSprintToggled;
        public float CurrentMoveSpeed => IsSprinting ? _moveSpeed * _sprintMultiplier : _moveSpeed;

        public void ToggleSprint()
        {
            if (isActiveAndEnabled)
            {
                _isSprintToggled = !_isSprintToggled;
            }
        }

        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int DirectionId = Animator.StringToHash("Direction");
        private CharacterController _controller;
        private Animator _animator;
        private float _verticalSpeed;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _animator = GetComponent<Animator>();
            if (_animator != null)
            {
                _animator.applyRootMotion = false;
            }
            if (_cameraTransform == null && Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }
        }

        private void Update()
        {
            bool isShiftHeld = Keyboard.current != null
                && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
            Move(ReadMovement(), isShiftHeld);
        }

        private void Move(Vector2 input, bool isShiftHeld)
        {
            IsSprinting = input.sqrMagnitude > 0.001f && (_isSprintToggled || isShiftHeld);
            Vector3 forward = _cameraTransform != null ? _cameraTransform.forward : Vector3.forward;
            forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }

            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 direction = forward * input.y + right * input.x;
            if (direction.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(direction), _rotationSpeed * Time.deltaTime);
            }

            if (_controller.isGrounded && _verticalSpeed < 0f)
            {
                _verticalSpeed = -2f;
            }

            _verticalSpeed -= _gravity * Time.deltaTime;
            CollisionFlags collisions = _controller.Move(
                (direction * CurrentMoveSpeed + Vector3.up * _verticalSpeed) * Time.deltaTime);
            if ((collisions & CollisionFlags.Below) != 0 && _verticalSpeed < 0f)
            {
                _verticalSpeed = -2f;
            }

            // 뼈대 없는 모델도 같은 이동을 사용하며, 기존 UnityChan 애니메이션은 유지합니다.
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                _animator.SetFloat(SpeedId, input.magnitude * (IsSprinting ? _sprintMultiplier : 1f), 0.1f, Time.deltaTime);
                _animator.SetFloat(DirectionId, 0f);
            }
        }

        private static Vector2 ReadMovement()
        {
            Keyboard keyboard = Keyboard.current;
            if (!Application.isFocused || Cursor.lockState != CursorLockMode.Locked || keyboard == null)
            {
                return Vector2.zero;
            }

            return Vector2.ClampMagnitude(new Vector2(
                (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f)), 1f);
        }

        private void OnDisable()
        {
            _verticalSpeed = 0f;
            IsSprinting = false;
            _isSprintToggled = false;
        }
    }
}

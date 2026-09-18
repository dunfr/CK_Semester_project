using UnityEngine;
using UnityEngine.InputSystem;

namespace CK.SemesterProject.Tutorial
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController), typeof(Animator))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        [SerializeField] private Transform _cameraTransform;
        [SerializeField, Min(0f)] private float _moveSpeed = 3f;
        [SerializeField, Min(0f)] private float _rotationSpeed = 540f;
        [SerializeField, Min(0f)] private float _gravity = 20f;

        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int DirectionId = Animator.StringToHash("Direction");
        private CharacterController _controller;
        private Animator _animator;
        private float _verticalSpeed;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _animator = GetComponent<Animator>();
            _animator.applyRootMotion = false;
            if (_cameraTransform == null && Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }
        }

        private void Update()
        {
            Vector2 input = ReadMovement();
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
                (direction * _moveSpeed + Vector3.up * _verticalSpeed) * Time.deltaTime);
            if ((collisions & CollisionFlags.Below) != 0 && _verticalSpeed < 0f)
            {
                _verticalSpeed = -2f;
            }

            // UnityChanLocomotions의 전진 블렌드를 사용하고 이동 방향으로 모델을 돌립니다.
            _animator.SetFloat(SpeedId, input.magnitude, 0.1f, Time.deltaTime);
            _animator.SetFloat(DirectionId, 0f);
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
        }
    }
}

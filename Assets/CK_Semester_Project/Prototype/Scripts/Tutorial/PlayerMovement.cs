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
        [SerializeField] private Animator _animator;
        [SerializeField, Tooltip("실제 이동 속도를 Idle 0, Walk 0.5, Run 1로 전달합니다.")]
        private bool _normalizeAnimationSpeed;

        [SerializeField, Min(1f), Tooltip("달리기 시 기본 이동 속도 배율.")]
        private float _sprintMultiplier = 1.8f;
        private bool _isSprintToggled;

        public bool IsSprinting { get; private set; }
        public bool IsSprintToggled => _isSprintToggled;
        public float CurrentMoveSpeed => IsSprinting ? _moveSpeed * _sprintMultiplier : _moveSpeed;
        public string CurrentMovementStateName => _currentState?.Name ?? "Idle";

        internal PlayerMovementState IdleState { get; } = new PlayerIdleState();
        internal PlayerMovementState WalkState { get; } = new PlayerWalkState();
        internal PlayerMovementState RunState { get; } = new PlayerRunState();
        internal float WalkSpeed => _moveSpeed;
        internal float RunSpeed => _moveSpeed * _sprintMultiplier;

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
        private bool _hasSpeedParameter;
        private bool _hasDirectionParameter;
        private float _verticalSpeed;
        private PlayerMovementState _currentState;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            ChangeState(IdleState);
            if (_animator == null)
            {
                foreach (Animator candidate in GetComponentsInChildren<Animator>())
                {
                    if (candidate.enabled && candidate.runtimeAnimatorController != null)
                    {
                        _animator = candidate;
                        break;
                    }
                }
            }
            if (_animator != null)
            {
                _animator.applyRootMotion = false;
                if (_animator.runtimeAnimatorController != null)
                {
                    foreach (AnimatorControllerParameter parameter in _animator.parameters)
                    {
                        if (parameter.type != AnimatorControllerParameterType.Float)
                        {
                            continue;
                        }
                        _hasSpeedParameter |= parameter.nameHash == SpeedId;
                        _hasDirectionParameter |= parameter.nameHash == DirectionId;
                    }
                }
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
            ChangeState(_currentState.GetNextState(this, input, _isSprintToggled || isShiftHeld));
            _currentState.Move(this, input);
            UpdateAnimation(input);
        }

        private void ChangeState(PlayerMovementState nextState)
        {
            _currentState = nextState;
            IsSprinting = nextState.IsSprinting;
        }

        // States choose horizontal movement; grounding and gravity apply in every state.
        internal void MoveCharacter(Vector2 input, float moveSpeed)
        {
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
                (direction * moveSpeed + Vector3.up * _verticalSpeed) * Time.deltaTime);
            if ((collisions & CollisionFlags.Below) != 0 && _verticalSpeed < 0f)
            {
                _verticalSpeed = -2f;
            }
        }

        private void UpdateAnimation(Vector2 input)
        {
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                if (_hasSpeedParameter)
                {
                    float speed = _normalizeAnimationSpeed
                        ? GetNormalizedAnimationSpeed()
                        : input.magnitude * (IsSprinting ? _sprintMultiplier : 1f);
                    _animator.SetFloat(SpeedId, speed, 0.1f, Time.deltaTime);
                }
                if (_hasDirectionParameter)
                {
                    _animator.SetFloat(DirectionId, 0f);
                }
            }
        }

        private float GetNormalizedAnimationSpeed()
        {
            if (_moveSpeed <= 0f)
            {
                return 0f;
            }

            float horizontalSpeed = Vector3.ProjectOnPlane(_controller.velocity, Vector3.up).magnitude;
            if (_sprintMultiplier <= 1f)
            {
                return Mathf.Clamp01(horizontalSpeed / _moveSpeed) * (IsSprinting ? 1f : 0.5f);
            }
            if (horizontalSpeed <= _moveSpeed)
            {
                return Mathf.Clamp01(horizontalSpeed / _moveSpeed) * 0.5f;
            }

            return Mathf.Lerp(0.5f, 1f,
                Mathf.InverseLerp(_moveSpeed, _moveSpeed * _sprintMultiplier, horizontalSpeed));
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

        public void Teleport(Vector3 position)
        {
            bool wasEnabled = _controller.enabled;
            _controller.enabled = false;
            Vector3 delta = position - transform.position;
            transform.position = position;
            _verticalSpeed = 0;
            _controller.enabled = wasEnabled;
            var follow = GetComponentInChildren<Unity.Cinemachine.CinemachineCamera>();
            if (follow != null)
            {
                follow.PreviousStateIsValid = false;
            }
            if (Camera.main != null)
            {
                Camera.main.transform.position += delta;
            }
            Physics.SyncTransforms();
        }

        private void OnDisable()
        {
            _verticalSpeed = 0f;
            ChangeState(IdleState);
            _isSprintToggled = false;
            if (_normalizeAnimationSpeed && _hasSpeedParameter && _animator != null
                && _animator.runtimeAnimatorController != null)
            {
                _animator.SetFloat(SpeedId, 0f);
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CK.SemesterProject.Tutorial
{
    // 플레이어 이동 FSM의 주인. 입력을 모아 현재 상태에 넘기고, 상태가 고른 다음 상태로 바꾼다.
    // 상태(Idle / Walk / Run)는 PlayerMovementState를 상속한 클래스이며, 들어올 때 Animator Controller의 같은 이름 상태 모션을 재생한다.
    // Controller에 그 상태들이 없으면(다른 씬의 UnityChan 등) 기존처럼 Speed / Direction 파라미터만 넘긴다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        [SerializeField] private Transform _cameraTransform;
        [SerializeField, Min(0f)] private float _moveSpeed = 3f;
        [SerializeField, Min(0f)] private float _rotationSpeed = 540f;
        [SerializeField, Min(0f)] private float _gravity = 20f;
        [SerializeField] private Animator _animator;
        [SerializeField, Tooltip("Speed 파라미터 방식 Controller에서 실제 이동 속도를 Idle 0, Walk 0.5, Run 1로 전달합니다.")]
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
        // 걷기·달리기 중 벽에 막혔다고 보는 실제 속도(GetNormalizedAnimationSpeed 값). 경계에서 모션이 깜빡이지 않도록 들어갈 때와 나올 때를 나눈다.
        private const float BlockedEnterSpeed = .05f;
        private const float BlockedExitSpeed = .15f;
        private CharacterController _controller;
        private bool _hasSpeedParameter;
        private bool _hasDirectionParameter;
        private bool _usesStateAnimations;
        private int _playingAnimationHash;
        private bool _showsBlockedPose;
        private readonly HashSet<int> _reportedMissingAnimations = new HashSet<int>();
        private float _verticalSpeed;
        private PlayerMovementState _currentState;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
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
            ChangeState(IdleState);
        }

        // 자식 Animator가 초기화된 뒤에 Controller 구성을 확인하고 현재 상태의 모션을 바로 재생한다.
        private void Start()
        {
            _usesStateAnimations = _animator != null && _animator.runtimeAnimatorController != null
                && HasAnimation(IdleState) && HasAnimation(WalkState) && HasAnimation(RunState);
            if (_usesStateAnimations)
            {
                _animator.Play(_currentState.AnimatorStateHash, 0, 0f);
                _playingAnimationHash = _currentState.AnimatorStateHash;
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
            var movementInput = new PlayerMovementInput(input, _isSprintToggled || isShiftHeld);
            ChangeState(_currentState.GetNextState(this, movementInput));
            _currentState.Tick(this, movementInput);
            if (!_usesStateAnimations)
            {
                UpdateAnimation(input);
            }
        }

        private void ChangeState(PlayerMovementState nextState)
        {
            if (nextState == _currentState)
            {
                return;
            }

            _currentState?.Exit(this);
            _currentState = nextState;
            IsSprinting = nextState.IsSprinting;
            nextState.Enter(this);
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

        // 상태가 들어올 때 그 상태의 모션으로 넘어간다.
        internal void PlayStateAnimation(PlayerMovementState state)
        {
            _showsBlockedPose = false;
            CrossFadeTo(state, state.AnimatorStateHash);
        }

        // 걷기·달리기 중 벽에 막혀 실제로 거의 움직이지 못하면 Idle 모션을 보여 준다(제자리 걸음 방지).
        // 상태는 그대로 두므로 다시 움직이면 곧바로 그 상태의 모션으로 돌아간다.
        internal void UpdateBlockedPose(PlayerMovementState state)
        {
            float speed = GetNormalizedAnimationSpeed();
            bool blocked = _showsBlockedPose ? speed < BlockedExitSpeed : speed < BlockedEnterSpeed;
            if (blocked == _showsBlockedPose)
            {
                return;
            }

            _showsBlockedPose = blocked;
            CrossFadeTo(state, blocked ? IdleState.AnimatorStateHash : state.AnimatorStateHash);
        }

        // Controller에 없는 상태(모션이 아직 없는 점프 등)로는 넘어가지 않고 지금 모션을 유지한다.
        private void CrossFadeTo(PlayerMovementState state, int animationHash)
        {
            if (!_usesStateAnimations || animationHash == _playingAnimationHash
                || _animator == null || !_animator.isActiveAndEnabled)
            {
                return;
            }
            if (!_animator.HasState(0, animationHash))
            {
                if (_reportedMissingAnimations.Add(animationHash))
                {
                    Debug.LogWarning($"Animator Controller에 '{state.AnimatorStateName}' 상태가 없어 이전 모션을 유지합니다.", _animator);
                }
                return;
            }

            _animator.CrossFadeInFixedTime(animationHash, state.AnimationFadeDuration, 0);
            _playingAnimationHash = animationHash;
        }

        private bool HasAnimation(PlayerMovementState state) => _animator.HasState(0, state.AnimatorStateHash);

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

        // 이동을 끄면(전투 진입 등) Idle로 돌아가 대기 모션을 재생한다.
        private void OnDisable()
        {
            _verticalSpeed = 0f;
            ChangeState(IdleState);
            _isSprintToggled = false;
            if (!_usesStateAnimations && _hasSpeedParameter && _animator != null
                && _animator.runtimeAnimatorController != null)
            {
                _animator.SetFloat(SpeedId, 0f);
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace CK.SemesterProject.UIAnimation
{
    // Open → Stay → Close 흐름을 맡는다.
    // 켜지면 Open부터 재생되고, 버튼 OnClick에는 Open()과 Close()를 연결하면 된다.
    // 위치와 회전은 상대값이라 같은 클립을 여러 Image가 써도 각자 자리에서 움직인다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    [AddComponentMenu("UI/UI Animation Player")]
    public sealed class UIAnimationPlayer : MonoBehaviour
    {
        private static readonly int IsOpen = Animator.StringToHash(UIAnimationPreset.IsOpenParameter);
        private static readonly int StayState = Animator.StringToHash(nameof(UIAnimationState.Stay));
        private static readonly int CloseState = Animator.StringToHash(nameof(UIAnimationState.Close));

        // 속도가 0이면 Animator가 값을 쓰지 않아 이전 모습이 보인다. 멈춘 것과 다름없는 속도로 시작 모습을 유지한다.
        private const float HoldSpeed = 0.0001f;
        private const float MaxWaitStep = 0.05f;
        private const float MoveTolerance = 0.0001f;
        private const float AngleTolerance = 0.01f;

        [SerializeField] private UIAnimationPreset _preset;
        [SerializeField, Min(0f)] private float _openDelay;
        [SerializeField, Min(0f)] private float _closeDelay;
        [SerializeField] private List<UIAnimationPlayer> _followers = new List<UIAnimationPlayer>();
        [SerializeField] private UnityEvent _openStarted = new UnityEvent();
        [SerializeField] private UnityEvent _openFinished = new UnityEvent();
        [SerializeField] private UnityEvent _closeStarted = new UnityEvent();
        [SerializeField] private UnityEvent _closeFinished = new UnityEvent();

        // Animator가 쓰는 상대값이다. 클립에는 이 값만 들어가고 실제 자리는 LateUpdate에서 더한다.
        [SerializeField, HideInInspector] private Vector2 _animatedOffset;
        [SerializeField, HideInInspector] private float _animatedRotation;

        private Animator _animator;
        private RectTransform _rect;
        private bool _parked;
        private bool _closing;
        private bool _stateFinished;
        private float _openWait;
        private float _closeWait;
        private bool _poseApplied;
        private Vector2 _pointerOffset;
        private float _pointerRotation;
        private Vector2 _basePosition;
        private Quaternion _baseRotation = Quaternion.identity;
        private Vector2 _writtenPosition;
        private Quaternion _writtenRotation = Quaternion.identity;

        public UIAnimationPreset Preset => _preset;
        public bool IsClosing => _closing;
        public float OpenDelay => _openDelay;
        public float CloseDelay => _closeDelay;
        public IReadOnlyList<UIAnimationPlayer> Followers => _followers;
        public UnityEvent OpenStarted => _openStarted;
        public UnityEvent OpenFinished => _openFinished;
        public UnityEvent CloseStarted => _closeStarted;
        public UnityEvent CloseFinished => _closeFinished;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _rect = (RectTransform)transform;
        }

        private void OnEnable()
        {
            _closing = false;
            _closeWait = 0f;
            foreach (UIAnimationPlayer follower in _followers)
            {
                if (follower != null && !follower.gameObject.activeSelf)
                {
                    follower.gameObject.SetActive(true);
                }
            }

            if (_openDelay > 0f)
            {
                _openWait = _openDelay;
                _animator.speed = HoldSpeed;
            }
            else
            {
                BeginOpen();
            }
        }

        private void OnDisable()
        {
            if (_parked)
            {
                _parked = false;
                _animator.enabled = true;
            }
            _openWait = 0f;
            _animator.speed = 1f;
            RestorePose();
        }

        public void Open()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
                return;
            }
            if (!isActiveAndEnabled || !_closing)
            {
                return;
            }

            _closing = false;
            foreach (UIAnimationPlayer follower in _followers)
            {
                if (follower != null)
                {
                    follower.Open();
                }
            }
            if (_closeWait > 0f)
            {
                _closeWait = 0f;
                return;
            }
            _animator.SetBool(IsOpen, true);
            BeginOpen();
        }

        public void Close()
        {
            if (!isActiveAndEnabled || _closing)
            {
                return;
            }

            _closing = true;
            _openWait = 0f;
            _animator.speed = 1f;
            foreach (UIAnimationPlayer follower in _followers)
            {
                if (follower != null)
                {
                    follower.Close();
                }
            }

            if (_closeDelay > 0f)
            {
                _closeWait = _closeDelay;
            }
            else
            {
                BeginClose();
            }
        }

        // 입력 반응이 Animator 결과 위에 더하는 값이다.
        public void SetPointerPose(Vector2 offset, float rotation)
        {
            _pointerOffset = offset;
            _pointerRotation = rotation;
        }

        private void BeginOpen()
        {
            _stateFinished = false;
            PlaySound(UIAnimationState.Open);
            _openStarted.Invoke();
        }

        private void BeginClose()
        {
            bool wasParked = _parked;
            if (wasParked)
            {
                _parked = false;
                _animator.enabled = true;
            }
            _animator.SetBool(IsOpen, false);
            if (wasParked)
            {
                _animator.Play(CloseState, 0, 0f);
            }
            _stateFinished = false;
            PlaySound(UIAnimationState.Close);
            _closeStarted.Invoke();
        }

        private void FinishState(UnityEvent finished)
        {
            if (!_stateFinished)
            {
                _stateFinished = true;
                finished.Invoke();
            }
        }

        private void PlaySound(UIAnimationState state)
        {
            if (_preset != null)
            {
                UIAnimationSound.Play(_preset.Get(state).Sound);
            }
        }

        private void LateUpdate()
        {
            TickWaits();
            FollowAnimator();
            ApplyPose();
        }

        private void TickWaits()
        {
            // 씬을 불러오며 생긴 긴 프레임 하나가 순차 등장의 지연을 건너뛰지 않게 한다.
            float elapsed = Mathf.Min(Time.unscaledDeltaTime, MaxWaitStep);
            if (_openWait > 0f)
            {
                _openWait -= elapsed;
                if (_openWait <= 0f)
                {
                    _animator.speed = 1f;
                    BeginOpen();
                }
            }
            if (_closeWait > 0f)
            {
                _closeWait -= elapsed;
                if (_closeWait <= 0f)
                {
                    BeginClose();
                }
            }
        }

        private void FollowAnimator()
        {
            if (_parked || _preset == null || !_animator.isActiveAndEnabled
                || _animator.runtimeAnimatorController == null || _animator.IsInTransition(0))
            {
                return;
            }

            AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
            if (state.shortNameHash == CloseState)
            {
                if (state.normalizedTime >= 1f)
                {
                    FinishClose();
                }
            }
            else if (state.shortNameHash == StayState && !_closing)
            {
                FollowStay(state);
            }
        }

        private void FinishClose()
        {
            FinishState(_closeFinished);
            // Animator는 자기 오브젝트를 끌 수 없어서 여기서 끈다. 트리거가 다시 열었으면 끄지 않는다.
            if (_closing && !_preset.Get(UIAnimationState.Close).KeepEnabled)
            {
                gameObject.SetActive(false);
            }
        }

        private void FollowStay(AnimatorStateInfo state)
        {
            FinishState(_openFinished);
            if (_closing)
            {
                return;
            }

            if (!_preset.Get(UIAnimationState.Open).KeepEnabled)
            {
                gameObject.SetActive(false);
            }
            else if (_preset.Get(UIAnimationState.Stay).AutoClose)
            {
                if (state.normalizedTime >= 1f)
                {
                    Close();
                }
            }
            else if (_preset.IsStayStatic)
            {
                // 값이 변하지 않으면 Animator를 세워 Canvas가 매 프레임 다시 그려지지 않게 한다.
                _parked = true;
                _animator.enabled = false;
            }
        }

        private void ApplyPose()
        {
            Vector2 offset = _animatedOffset + _pointerOffset;
            float rotation = _animatedRotation + _pointerRotation;
            bool moved = offset != Vector2.zero || !Mathf.Approximately(rotation, 0f);
            if (!moved && !_poseApplied)
            {
                return;
            }

            // 레이아웃이나 다른 코드가 옮겼다면 그 자리를 새 기준으로 삼는다.
            // anchoredPosition은 읽을 때 오차가 있어 조금 다른 것은 같은 자리로 본다.
            if (!_poseApplied || (_rect.anchoredPosition - _writtenPosition).sqrMagnitude > MoveTolerance)
            {
                _basePosition = _rect.anchoredPosition;
            }
            if (!_poseApplied || Quaternion.Angle(_rect.localRotation, _writtenRotation) > AngleTolerance)
            {
                _baseRotation = _rect.localRotation;
            }
            _writtenPosition = _basePosition + offset;
            _writtenRotation = _baseRotation * Quaternion.Euler(0f, 0f, rotation);
            _rect.anchoredPosition = _writtenPosition;
            _rect.localRotation = _writtenRotation;
            _poseApplied = moved;
        }

        private void RestorePose()
        {
            if (_poseApplied)
            {
                _rect.anchoredPosition = _basePosition;
                _rect.localRotation = _baseRotation;
            }
            _poseApplied = false;
            _animatedOffset = Vector2.zero;
            _animatedRotation = 0f;
            _pointerOffset = Vector2.zero;
            _pointerRotation = 0f;
        }
    }
}

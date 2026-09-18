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
        [SerializeField] private float _targetHeight = 1f;
        [SerializeField, Range(-80f, 80f)] private float _minPitch = -70f;
        [SerializeField, Range(-80f, 80f)] private float _maxPitch = 70f;
        [SerializeField, Range(-80f, 80f)] private float _pitch = 16f;

        private CinemachineFollow _follow;
        private float _yaw;
        private bool _skipMouseDelta;

        private void Awake()
        {
            _follow = GetComponent<CinemachineFollow>();
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
                SetCursorLocked(false);
                return;
            }

            Mouse mouse = Mouse.current;
            if (!Application.isFocused || mouse == null)
            {
                return;
            }

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (mouse.leftButton.wasPressedThisFrame)
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

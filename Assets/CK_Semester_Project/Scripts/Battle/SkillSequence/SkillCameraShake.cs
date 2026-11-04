using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CK.SemesterProject.SkillSequence
{
    /// <summary>
    /// 카메라 흔들림 계산과 적용. Cinemachine이 매 프레임 카메라 위치를 다시 정하므로,
    /// 렌더링 직전에만 흔들림을 더하고 렌더링이 끝나면 원래 위치로 되돌린다(게임 로직·Cinemachine에 영향 없음).
    /// </summary>
    public static class SkillCameraShake
    {
        private static Camera _target;
        private static Vector3 _positionOffset;
        private static Vector3 _rotationOffset;
        private static bool _isHooked;
        private static bool _isApplied;
        private static Vector3 _savedPosition;
        private static Quaternion _savedRotation;

        /// <summary>time 시점에 겹치는 모든 흔들림의 합.</summary>
        public static bool Evaluate(IReadOnlyList<SkillCameraShakeEvent> shakes, float time, out Vector3 position, out Vector3 rotation)
        {
            position = Vector3.zero;
            rotation = Vector3.zero;
            if (shakes == null)
            {
                return false;
            }
            bool any = false;
            for (int i = 0; i < shakes.Count; i++)
            {
                SkillCameraShakeEvent shake = shakes[i];
                if (shake != null && shake.Evaluate(time - shake.Time, i * 13 + 5, out Vector3 shakePosition, out Vector3 shakeRotation))
                {
                    position += shakePosition;
                    rotation += shakeRotation;
                    any = true;
                }
            }
            return any;
        }

        /// <summary>camera에 다음 렌더링부터 흔들림을 적용한다. camera가 null이면 해제.</summary>
        public static void SetOffset(Camera camera, Vector3 position, Vector3 rotation)
        {
            _target = camera;
            _positionOffset = position;
            _rotationOffset = rotation;
            if (!_isHooked)
            {
                RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
                RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
                _isHooked = true;
            }
        }

        public static void Clear()
        {
            SetOffset(null, Vector3.zero, Vector3.zero);
        }

        private static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == null || camera != _target || (_positionOffset == Vector3.zero && _rotationOffset == Vector3.zero))
            {
                return;
            }
            Transform cameraTransform = camera.transform;
            _savedPosition = cameraTransform.position;
            _savedRotation = cameraTransform.rotation;
            cameraTransform.SetPositionAndRotation(_savedPosition + _savedRotation * _positionOffset, _savedRotation * Quaternion.Euler(_rotationOffset));
            _isApplied = true;
        }

        private static void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (!_isApplied || camera == null || camera != _target)
            {
                return;
            }
            camera.transform.SetPositionAndRotation(_savedPosition, _savedRotation);
            _isApplied = false;
        }
    }
}

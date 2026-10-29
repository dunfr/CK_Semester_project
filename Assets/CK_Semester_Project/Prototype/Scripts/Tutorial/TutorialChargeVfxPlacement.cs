using UnityEngine;

namespace CK.SemesterProject.Tutorial
{
    [DefaultExecutionOrder(100)]
    public sealed class TutorialChargeVfxPlacement : MonoBehaviour
    {
        [SerializeField, Tooltip("캐릭터 앞쪽에 표시할 중심 링·빛·광륜")]
        private Transform[] _centerEffects;
        [SerializeField, Min(0f), Tooltip("중심 효과를 카메라 쪽으로 띄우는 수평 거리(m)")]
        private float _cameraSideOffset = .8f;

        private Camera _camera;
        private Vector3[] _localCenters;

        public void Initialize(Camera camera)
        {
            _camera = camera;
            _localCenters = new Vector3[_centerEffects.Length];
            for (int index = 0; index < _centerEffects.Length; index++)
            {
                _localCenters[index] = transform.InverseTransformPoint(_centerEffects[index].position);
            }
            LateUpdate();
        }

        private void LateUpdate()
        {
            if (_camera == null || _localCenters == null)
            {
                return;
            }
            for (int index = 0; index < _centerEffects.Length; index++)
            {
                if (_centerEffects[index] == null)
                {
                    continue;
                }
                Vector3 center = transform.TransformPoint(_localCenters[index]);
                Vector3 towardCamera = Vector3.ProjectOnPlane(_camera.transform.position - center, Vector3.up).normalized;
                _centerEffects[index].position = center + towardCamera * _cameraSideOffset;
            }
        }
    }
}

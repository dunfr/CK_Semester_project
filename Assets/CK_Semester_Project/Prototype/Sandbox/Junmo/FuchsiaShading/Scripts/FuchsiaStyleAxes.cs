using UnityEngine;

namespace CK.SemesterProject.Prototype
{
    // 스킨 메시용 얼굴/머리 기준축. 정적 메시는 재질의 _Style*OS(오브젝트 공간)를 쓰지만,
    // 스킨 메시는 머리가 뼈로 돌기 때문에 머리 뼈 회전을 따라가는 월드 축을 매 프레임 렌더러별로 넣는다.
    // 축은 스타일 데이터를 구울 때의 모델 루트 기준(오른쪽 +X, 위 +Y, 앞 +Z)에서 머리 뼈가 돈 만큼 회전한다.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class FuchsiaStyleAxes : MonoBehaviour
    {
        private static readonly int RightId = Shader.PropertyToID("_StyleRightOS");
        private static readonly int UpId = Shader.PropertyToID("_StyleUpOS");
        private static readonly int ForwardId = Shader.PropertyToID("_StyleForwardOS");
        private static readonly int SpaceId = Shader.PropertyToID("_StyleAxesSpace");

        [SerializeField, Tooltip("얼굴 방향을 따라갈 머리 뼈입니다.")]
        private Transform _head;

        [SerializeField, Tooltip("스타일 데이터를 쓰는 얼굴·머리카락 렌더러입니다.")]
        private Renderer[] _renderers = new Renderer[0];

        [SerializeField, HideInInspector, Tooltip("스타일 데이터를 구울 때 모델 루트 기준 머리 뼈 회전입니다.")]
        private Quaternion _bindHeadRotation = Quaternion.identity;

        private MaterialPropertyBlock _block;

        // FuchsiaSkinnedStyleBuilder가 구울 때 호출한다.
        public void Configure(Transform head, Renderer[] renderers)
        {
            _head = head;
            _renderers = renderers;
            _bindHeadRotation = head != null ? Quaternion.Inverse(transform.rotation) * head.rotation : Quaternion.identity;
        }

        private void OnDisable()
        {
            if (_block == null)
            {
                return;
            }
            foreach (Renderer renderer in _renderers)
            {
                if (renderer == null)
                {
                    continue;
                }
                renderer.GetPropertyBlock(_block);
                _block.SetFloat(SpaceId, 0f);
                renderer.SetPropertyBlock(_block);
            }
        }

        private void LateUpdate()
        {
            _block ??= new MaterialPropertyBlock();
            Quaternion delta = _head != null
                ? Quaternion.Inverse(transform.rotation) * _head.rotation * Quaternion.Inverse(_bindHeadRotation)
                : Quaternion.identity;
            Quaternion frame = transform.rotation * delta;
            Vector4 right = frame * Vector3.right;
            Vector4 up = frame * Vector3.up;
            Vector4 forward = frame * Vector3.forward;
            foreach (Renderer renderer in _renderers)
            {
                if (renderer == null)
                {
                    continue;
                }
                renderer.GetPropertyBlock(_block);
                _block.SetVector(RightId, right);
                _block.SetVector(UpId, up);
                _block.SetVector(ForwardId, forward);
                _block.SetFloat(SpaceId, 1f);
                renderer.SetPropertyBlock(_block);
            }
        }
    }
}

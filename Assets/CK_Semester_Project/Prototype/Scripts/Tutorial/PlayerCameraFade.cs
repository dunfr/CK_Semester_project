using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CK.SemesterProject.Tutorial
{
    // 카메라가 플레이어에 너무 붙으면(벽에 밀려 Deoccluder가 당겨올 때 등) 플레이어를 반투명하게 만든다.
    // Fuchsia 셰이더의 _CameraFade(디더)를 MaterialPropertyBlock으로 채운다. 이 값을 모르는 셰이더는 영향을 받지 않는다.
    [DisallowMultipleComponent]
    public sealed class PlayerCameraFade : MonoBehaviour
    {
        [SerializeField, Tooltip("비워 두면 Main Camera를 사용합니다.")]
        private Camera _camera;
        [SerializeField, Min(0f), Tooltip("카메라와 플레이어 몸 표면 사이가 이 거리(m)보다 가까워지면 흐려지기 시작합니다.")]
        private float _fadeStartDistance = 1.2f;
        [SerializeField, Min(0f), Tooltip("이 거리(m) 이하에서 최소 불투명도가 됩니다.")]
        private float _fadeEndDistance = .4f;
        [SerializeField, Range(0f, 1f), Tooltip("가장 가까울 때의 불투명도. 0이면 완전히 사라집니다.")]
        private float _minOpacity = .25f;
        [SerializeField, Min(.01f), Tooltip("불투명도가 초당 바뀌는 양. 클수록 즉시 반응합니다.")]
        private float _fadeSpeed = 6f;

        private static readonly int CameraFadeId = Shader.PropertyToID("_CameraFade");

        private readonly List<Renderer> _renderers = new List<Renderer>();
        private MaterialPropertyBlock _block;
        private CharacterController _controller;
        private float _opacity = 1f;

        public float Opacity => _opacity;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _controller = GetComponent<CharacterController>();
        }

        private void OnEnable()
        {
            CollectRenderers();
            _opacity = 1f;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            _opacity = 1f;
            ApplyOpacity();
        }

        // 모델 교체 등으로 자식 렌더러가 바뀌었을 때 다시 모은다.
        public void CollectRenderers()
        {
            _renderers.Clear();
            foreach (Renderer childRenderer in GetComponentsInChildren<Renderer>(true))
            {
                // 이펙트(파티클·트레일)는 셰이더가 다르므로 캐릭터 메시만 대상으로 한다.
                if (childRenderer is SkinnedMeshRenderer || childRenderer is MeshRenderer)
                {
                    _renderers.Add(childRenderer);
                }
            }
        }

        // Cinemachine이 LateUpdate에서 카메라 위치를 확정한 뒤, 실제 렌더링 직전 위치로 거리를 잰다.
        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
        {
            Camera targetCamera = _camera != null ? _camera : Camera.main;
            if (renderingCamera != targetCamera)
            {
                return;
            }

            float targetOpacity = CalculateTargetOpacity(renderingCamera.transform.position);            _opacity = Mathf.MoveTowards(_opacity, targetOpacity, _fadeSpeed * Time.unscaledDeltaTime);
            // 전투 연출 등이 저장해 둔 PropertyBlock으로 되돌려도 값이 맞도록 매 프레임 다시 적용한다(렌더러 수십 개 이하).
            ApplyOpacity();
        }

        private float CalculateTargetOpacity(Vector3 cameraPosition)
        {
            float distance = DistanceToBody(cameraPosition);
            float fadeRange = Mathf.Max(_fadeStartDistance - _fadeEndDistance, .001f);
            float closeness = 1f - Mathf.Clamp01((distance - _fadeEndDistance) / fadeRange);
            return Mathf.Lerp(1f, _minOpacity, closeness);
        }

        // CharacterController 캡슐 표면까지의 거리. 없으면 피벗 기준 1m 높이 점까지의 거리를 쓴다.
        private float DistanceToBody(Vector3 point)
        {
            if (_controller == null)
            {
                return Vector3.Distance(point, transform.position + Vector3.up);
            }

            Vector3 center = transform.TransformPoint(_controller.center);
            float halfSegment = Mathf.Max(_controller.height * .5f - _controller.radius, 0f);
            Vector3 up = transform.up;
            Vector3 bottom = center - up * halfSegment;
            float along = Mathf.Clamp(Vector3.Dot(point - bottom, up), 0f, halfSegment * 2f);
            Vector3 closest = bottom + up * along;
            return Mathf.Max(Vector3.Distance(point, closest) - _controller.radius, 0f);
        }

        private void ApplyOpacity()
        {
            for (int i = 0; i < _renderers.Count; i++)
            {
                Renderer targetRenderer = _renderers[i];
                if (targetRenderer == null)
                {
                    continue;
                }
                targetRenderer.GetPropertyBlock(_block);
                _block.SetFloat(CameraFadeId, _opacity);
                targetRenderer.SetPropertyBlock(_block);
            }
        }

        private void OnValidate()
        {
            _fadeEndDistance = Mathf.Min(_fadeEndDistance, _fadeStartDistance);
        }
    }
}

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class CorridorPlanarReflection : MonoBehaviour
{
    [SerializeField, Tooltip("반사를 계산할 원본 카메라입니다.")]
    private Camera _sourceCamera;

    [SerializeField, Min(-1), Tooltip("반사 카메라가 사용할 URP Renderer 번호입니다. -1은 파이프라인 기본값입니다.")]
    private int _rendererIndex = -1;

    [SerializeField, Range(0.25f, 1f), Tooltip("원본 화면에 대한 반사 텍스처 해상도 비율입니다.")]
    private float _resolutionScale = 0.5f;

    [SerializeField, Min(0.001f), Tooltip("바닥 아래 물체가 반사되지 않도록 자르는 여유 거리(m)입니다.")]
    private float _clipOffset = 0.03f;

    [SerializeField, Tooltip("반사 평면 높이를 이 대상(예: 플레이어)이 서 있는 바닥에 맞춥니다. 비워 두면 이 오브젝트 높이를 씁니다. 층이 여러 개인 맵용입니다.")]
    private Transform _followFloorOf;

    [SerializeField, Min(0.1f), Tooltip("대상 아래로 바닥을 찾는 최대 거리(m)입니다.")]
    private float _floorSearchDistance = 3f;

    private static readonly int ReflectionTextureId = Shader.PropertyToID("_CorridorPlanarReflection");
    private static readonly int ReflectionMatrixId = Shader.PropertyToID("_CorridorReflectionMatrix");
    private static readonly int ReflectionTexelSizeId = Shader.PropertyToID("_CorridorReflectionTexelSize");
    private static readonly int ReflectionSourceId = Shader.PropertyToID("_CorridorReflectionSourcePosition");
    private static readonly int ReflectionAvailableId = Shader.PropertyToID("_CorridorReflectionAvailable");
    private static readonly int ReflectionPlaneYId = Shader.PropertyToID("_CorridorReflectionPlaneY");

    private static readonly RaycastHit[] FloorHits = new RaycastHit[8];

    private Camera _reflectionCamera;
    private RenderTexture _reflectionTexture;
    private bool _isRendering;

    private void OnEnable()
    {
        Shader.SetGlobalFloat(ReflectionAvailableId, 0);
        RenderPipelineManager.beginCameraRendering += RenderReflection;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= RenderReflection;
        Shader.SetGlobalFloat(ReflectionAvailableId, 0);
        Shader.SetGlobalTexture(ReflectionTextureId, Texture2D.blackTexture);
        ReleaseResources();
    }

    private void RenderReflection(ScriptableRenderContext context, Camera camera)
    {
        if (_isRendering || camera != _sourceCamera || camera.cameraType == CameraType.Reflection)
        {
            return;
        }

        if (UniversalRenderPipeline.asset == null)
        {
            return;
        }

        EnsureResources(camera);
        _reflectionCamera.CopyFrom(camera);
        _reflectionCamera.enabled = false;
        _reflectionCamera.cameraType = CameraType.Reflection;
        _reflectionCamera.useOcclusionCulling = false;
        _reflectionCamera.targetTexture = _reflectionTexture;
        _reflectionCamera.allowMSAA = false;

        var data = _reflectionCamera.GetUniversalAdditionalCameraData();
        data.SetRenderer(_rendererIndex);
        data.renderPostProcessing = false;
        data.antialiasing = AntialiasingMode.None;
        data.renderShadows = false;

        Vector3 normal = transform.up;
        Vector3 planePosition = GetPlanePosition();
        Vector4 plane = new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(normal, planePosition));
        Matrix4x4 reflection = CreateReflectionMatrix(plane);
        _reflectionCamera.transform.position = reflection.MultiplyPoint(camera.transform.position);
        _reflectionCamera.transform.rotation = Quaternion.LookRotation(
            Vector3.Reflect(camera.transform.forward, normal), Vector3.Reflect(camera.transform.up, normal));
        _reflectionCamera.worldToCameraMatrix = camera.worldToCameraMatrix * reflection;

        Vector3 clipPosition = _reflectionCamera.worldToCameraMatrix.MultiplyPoint(planePosition + normal * _clipOffset);
        Vector3 clipNormal = _reflectionCamera.worldToCameraMatrix.MultiplyVector(normal).normalized;
        Vector4 clipPlane = new Vector4(clipNormal.x, clipNormal.y, clipNormal.z, -Vector3.Dot(clipPosition, clipNormal));
        _reflectionCamera.projectionMatrix = camera.CalculateObliqueMatrix(clipPlane);

        bool previousCulling = GL.invertCulling;
        _isRendering = true;
        try
        {
            GL.invertCulling = !previousCulling;
            var request = new UniversalRenderPipeline.SingleCameraRequest
            {
                destination = _reflectionTexture
            };
            RenderPipeline.SubmitRenderRequest(_reflectionCamera, request);
            Matrix4x4 gpuProjection = GL.GetGPUProjectionMatrix(_reflectionCamera.projectionMatrix, true);
            Shader.SetGlobalMatrix(ReflectionMatrixId, gpuProjection * _reflectionCamera.worldToCameraMatrix);
            Shader.SetGlobalTexture(ReflectionTextureId, _reflectionTexture);
            Shader.SetGlobalVector(ReflectionSourceId, camera.transform.position);
            Shader.SetGlobalFloat(ReflectionPlaneYId, planePosition.y);
            Shader.SetGlobalFloat(ReflectionAvailableId, 1);
            Shader.SetGlobalVector(ReflectionTexelSizeId, new Vector4(
                1f / _reflectionTexture.width, 1f / _reflectionTexture.height,
                _reflectionTexture.width, _reflectionTexture.height));
        }
        finally
        {
            GL.invertCulling = previousCulling;
            _isRendering = false;
        }
    }

    private void EnsureResources(Camera camera)
    {
        if (_reflectionCamera == null)
        {
            var reflectionObject = new GameObject("Corridor Reflection Camera")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _reflectionCamera = reflectionObject.AddComponent<Camera>();
            _reflectionCamera.enabled = false;
            reflectionObject.AddComponent<UniversalAdditionalCameraData>();
        }

        int width = Mathf.Max(128, Mathf.RoundToInt(camera.pixelWidth * _resolutionScale));
        int height = Mathf.Max(128, Mathf.RoundToInt(camera.pixelHeight * _resolutionScale));
        if (_reflectionTexture != null && _reflectionTexture.width == width && _reflectionTexture.height == height)
        {
            return;
        }

        if (_reflectionTexture != null)
        {
            _reflectionTexture.Release();
            DestroyResource(_reflectionTexture);
        }

        _reflectionTexture = new RenderTexture(width, height, 24, RenderTextureFormat.DefaultHDR)
        {
            name = "Corridor Planar Reflection",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Trilinear,
            wrapMode = TextureWrapMode.Clamp,
            // 밉맵은 바닥 셰이더가 거울 대신 흐린 광택 반사(_BlurMip)를 낼 때 쓴다.
            useMipMap = true,
            autoGenerateMips = true
        };
        _reflectionTexture.Create();
    }

    // 따라갈 대상이 있으면 그 아래 바닥 표면 높이로 평면을 옮긴다. 바닥을 못 찾으면 대상 발 높이를 쓴다.
    private Vector3 GetPlanePosition()
    {
        Vector3 position = transform.position;
        if (_followFloorOf == null)
        {
            return position;
        }

        // 대상 자신의 충돌체(캐릭터 컨트롤러 등)에 먼저 맞으면 평면이 바닥보다 떠서 반사가 깜빡이므로 건너뛴다.
        Vector3 origin = _followFloorOf.position + Vector3.up * 0.5f;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, FloorHits, _floorSearchDistance + 0.5f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        int nearest = -1;
        for (int i = 0; i < count; i++)
        {
            if (!FloorHits[i].collider.transform.IsChildOf(_followFloorOf)
                && (nearest < 0 || FloorHits[i].distance < FloorHits[nearest].distance))
            {
                nearest = i;
            }
        }

        if (nearest < 0)
        {
            position.y = _followFloorOf.position.y;
            return position;
        }

        // 충돌체 윗면이 보이는 바닥면보다 낮은 경우가 있어, 얇은 바닥 메시면 그 메시 윗면을 쓴다.
        RaycastHit hit = FloorHits[nearest];
        position.y = hit.collider.TryGetComponent(out Renderer floorRenderer) && floorRenderer.bounds.size.y < 0.6f
            ? floorRenderer.bounds.max.y
            : hit.point.y;
        return position;
    }

    private static Matrix4x4 CreateReflectionMatrix(Vector4 plane)
    {
        Matrix4x4 matrix = Matrix4x4.identity;
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 4; column++)
            {
                matrix[row, column] -= 2f * plane[row] * plane[column];
            }
        }

        return matrix;
    }

    private void ReleaseResources()
    {
        if (_reflectionCamera != null)
        {
            DestroyResource(_reflectionCamera.gameObject);
            _reflectionCamera = null;
        }

        if (_reflectionTexture != null)
        {
            _reflectionTexture.Release();
            DestroyResource(_reflectionTexture);
            _reflectionTexture = null;
        }
    }

    private static void DestroyResource(Object resource)
    {
        if (Application.isPlaying)
        {
            Destroy(resource);
        }
        else
        {
            DestroyImmediate(resource);
        }
    }
}

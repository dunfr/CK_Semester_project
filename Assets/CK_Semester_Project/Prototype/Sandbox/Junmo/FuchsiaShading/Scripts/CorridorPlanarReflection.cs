using System.Collections.Generic;
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

    [SerializeField, Range(-0.1f, 0.1f), Tooltip("반사 평면에서 이 거리(m)만큼 위(+) 또는 아래(-)부터 반사에 그립니다. 양수면 벽·기둥 밑동이 그만큼 반사에서 잘려 그 틈으로 바깥(하늘)이 비칩니다. 바닥 재질을 반사에서 숨기면(Hide In Reflection) 음수로 두어 밑동까지 그릴 수 있습니다.")]
    private float _clipOffset = 0.03f;

    [SerializeField, Tooltip("반사를 그리는 동안만 숨길 바닥 재질입니다. 반사 평면 높이(±0.3 m)에 있는 이 재질의 렌더러만 숨깁니다. 바닥을 숨겨야 Clip Offset을 음수로 둘 수 있습니다.")]
    private Material[] _hideInReflection = new Material[0];

    [SerializeField, Tooltip("반사 평면 높이를 이 대상(예: 플레이어)이 서 있는 바닥에 맞춥니다. 비워 두면 이 오브젝트 높이를 씁니다. 층이 여러 개인 맵용입니다.")]
    private Transform _followFloorOf;

    [SerializeField, Min(0.1f), Tooltip("대상 아래로 바닥을 찾는 최대 거리(m)입니다.")]
    private float _floorSearchDistance = 3f;

    [SerializeField, Min(0f), Tooltip("새로 찾은 바닥 높이가 지금 평면과 이 값(m) 이내로 다르면 평면을 옮기지 않습니다. 같은 방 타일끼리 높이가 1~3 cm씩 달라 반사가 튀는 것을 막습니다. 방마다 바닥 높이가 10 cm쯤 다르므로 그보다 작게 둡니다(크면 다른 방 높이에 평면이 남아 반사가 바닥에서 떠 보입니다).")]
    private float _floorHeightTolerance = 0.05f;

    [SerializeField, Tooltip("반사 카메라의 배경색입니다. 반사에 아무것도 없는 곳(수평선 너머, 바닥 아래로 잘린 곳)이 이 색으로 채워지고, 흐림 때문에 수평선 근처 바닥에 번집니다. 하늘을 그대로 두면 카메라가 낮을 때 바닥이 하늘색으로 물듭니다.")]
    private Color _backgroundColor = new Color(0.52f, 0.54f, 0.6f, 1f);

    [SerializeField, Tooltip("높이가 다른 바닥(예: 10 cm 낮은 옆 방·복도)이 화면에 보이면 그 높이로 반사를 하나 더 그립니다. 끄면 플레이어가 선 바닥만 반사되어, 다른 방 바닥은 그 방에 들어가야 반사가 나타납니다. 켜 두면 그런 바닥이 보이는 동안 반사를 두 번 그립니다.")]
    private bool _secondFloorLevel = true;

    [SerializeField, Min(0.05f), Tooltip("두 번째 반사를 그릴 바닥의 최대 높이 차(m)입니다. 이보다 크게 다른 바닥(다른 층)은 무시합니다.")]
    private float _secondFloorMaxStep = 1.5f;

    [SerializeField, Min(1f), Tooltip("두 번째 반사를 그릴 바닥을 찾는 카메라로부터의 거리(m)입니다.")]
    private float _secondFloorSearchDistance = 30f;

    private float _lockedPlaneY = float.NaN;
    private float _lockedSecondPlaneY = float.NaN;

    private static readonly int ReflectionTextureId = Shader.PropertyToID("_CorridorPlanarReflection");
    private static readonly int ReflectionMatrixId = Shader.PropertyToID("_CorridorReflectionMatrix");
    private static readonly int ReflectionTexelSizeId = Shader.PropertyToID("_CorridorReflectionTexelSize");
    private static readonly int ReflectionSourceId = Shader.PropertyToID("_CorridorReflectionSourcePosition");
    private static readonly int ReflectionAvailableId = Shader.PropertyToID("_CorridorReflectionAvailable");
    private static readonly int ReflectionPlaneYId = Shader.PropertyToID("_CorridorReflectionPlaneY");
    // 두 번째 반사(높이가 다른 바닥용). 바닥 셰이더가 픽셀 높이에 더 가까운 평면의 반사를 고른다.
    private static readonly int SecondTextureId = Shader.PropertyToID("_CorridorPlanarReflection2");
    private static readonly int SecondMatrixId = Shader.PropertyToID("_CorridorReflectionMatrix2");
    private static readonly int SecondPlaneYId = Shader.PropertyToID("_CorridorReflectionPlaneY2");
    private static readonly int SecondAvailableId = Shader.PropertyToID("_CorridorReflectionAvailable2");

    private const string FloorReflectionShaderName = "Semester/Environment/Corridor Floor Reflection";

    private static readonly RaycastHit[] FloorHits = new RaycastHit[8];
    private static readonly Plane[] FrustumPlanes = new Plane[6];

    private Camera _reflectionCamera;
    private RenderTexture _reflectionTexture;
    private RenderTexture _secondTexture;
    private bool _isRendering;
    private readonly List<Renderer> _hideCandidates = new List<Renderer>();
    private readonly List<Renderer> _hiddenNow = new List<Renderer>();
    private readonly List<Renderer> _reflectiveFloors = new List<Renderer>();

    private void OnEnable()
    {
        _lockedPlaneY = float.NaN;
        _lockedSecondPlaneY = float.NaN;
        CollectHideCandidates();
        CollectReflectiveFloors();
        Shader.SetGlobalFloat(ReflectionAvailableId, 0);
        Shader.SetGlobalFloat(SecondAvailableId, 0);
        RenderPipelineManager.beginCameraRendering += RenderReflection;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= RenderReflection;
        Shader.SetGlobalFloat(ReflectionAvailableId, 0);
        Shader.SetGlobalFloat(SecondAvailableId, 0);
        Shader.SetGlobalTexture(ReflectionTextureId, Texture2D.blackTexture);
        Shader.SetGlobalTexture(SecondTextureId, Texture2D.blackTexture);
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
        // 반사에 아무것도 없는 곳은 하늘 대신 배경색으로 채운다(하늘 수평선의 밝은 하늘색이 흐림을 타고 바닥에 번지던 문제).
        _reflectionCamera.clearFlags = CameraClearFlags.SolidColor;
        _reflectionCamera.backgroundColor = _backgroundColor;
        _reflectionCamera.cameraType = CameraType.Reflection;
        _reflectionCamera.useOcclusionCulling = false;
        _reflectionCamera.targetTexture = _reflectionTexture;
        _reflectionCamera.allowMSAA = false;

        var data = _reflectionCamera.GetUniversalAdditionalCameraData();
        data.SetRenderer(_rendererIndex);
        data.renderPostProcessing = false;
        data.antialiasing = AntialiasingMode.None;
        data.renderShadows = false;

        Vector3 planePosition = GetPlanePosition();
        // 플레이어가 선 바닥과 높이가 다른 바닥(예: 10 cm 낮은 복도)이 보이면 그 높이로도 반사를 그린다.
        // 평면이 하나뿐이면 그 바닥은 높이 차 때문에 반사가 꺼져, 그 방에 들어가야 반사가 나타난다.
        float secondPlaneY = 0f;
        bool hasSecond = _secondFloorLevel && FindSecondFloor(camera, planePosition.y, out secondPlaneY);
        if (hasSecond)
        {
            EnsureSecondTexture();
        }

        bool previousCulling = GL.invertCulling;
        _isRendering = true;
        try
        {
            GL.invertCulling = !previousCulling;
            RenderPlane(camera, planePosition.y, _reflectionTexture, ReflectionMatrixId);
            Shader.SetGlobalTexture(ReflectionTextureId, _reflectionTexture);
            Shader.SetGlobalVector(ReflectionSourceId, camera.transform.position);
            Shader.SetGlobalFloat(ReflectionPlaneYId, planePosition.y);
            Shader.SetGlobalFloat(ReflectionAvailableId, 1);
            Shader.SetGlobalVector(ReflectionTexelSizeId, new Vector4(
                1f / _reflectionTexture.width, 1f / _reflectionTexture.height,
                _reflectionTexture.width, _reflectionTexture.height));

            if (hasSecond)
            {
                RenderPlane(camera, secondPlaneY, _secondTexture, SecondMatrixId);
                Shader.SetGlobalTexture(SecondTextureId, _secondTexture);
                Shader.SetGlobalFloat(SecondPlaneYId, secondPlaneY);
            }

            Shader.SetGlobalFloat(SecondAvailableId, hasSecond ? 1 : 0);
        }
        finally
        {
            GL.invertCulling = previousCulling;
            _isRendering = false;
        }
    }

    // 높이 planeY의 수평 거울로 장면을 target에 그리고, 바닥 셰이더가 쓸 투영 행렬을 matrixId에 넣는다.
    private void RenderPlane(Camera camera, float planeY, RenderTexture target, int matrixId)
    {
        Vector3 normal = transform.up;
        Vector3 planePosition = new Vector3(transform.position.x, planeY, transform.position.z);
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
        _reflectionCamera.targetTexture = target;

        var request = new UniversalRenderPipeline.SingleCameraRequest
        {
            destination = target
        };
        HideFloors(planeY);
        try
        {
            RenderPipeline.SubmitRenderRequest(_reflectionCamera, request);
        }
        finally
        {
            ShowFloors();
        }

        Matrix4x4 gpuProjection = GL.GetGPUProjectionMatrix(_reflectionCamera.projectionMatrix, true);
        Shader.SetGlobalMatrix(matrixId, gpuProjection * _reflectionCamera.worldToCameraMatrix);
    }

    // 화면에 보이는 반사 바닥 중, 지금 평면과 높이가 다르면서(타일 높이 차보다 크게) 가장 가까운 바닥의 높이를 찾는다.
    private bool FindSecondFloor(Camera camera, float primaryY, out float floorY)
    {
        floorY = 0f;
        GeometryUtility.CalculateFrustumPlanes(camera, FrustumPlanes);
        Vector3 eye = camera.transform.position;
        float maxDistanceSq = _secondFloorSearchDistance * _secondFloorSearchDistance;
        float bestDistanceSq = float.MaxValue;
        foreach (var floor in _reflectiveFloors)
        {
            if (floor == null || !floor.enabled || !floor.gameObject.activeInHierarchy)
            {
                continue;
            }

            Bounds bounds = floor.bounds;
            float step = Mathf.Abs(bounds.max.y - primaryY);
            if (step <= _floorHeightTolerance || step > _secondFloorMaxStep)
            {
                continue;
            }

            float distanceSq = bounds.SqrDistance(eye);
            if (distanceSq > maxDistanceSq || distanceSq >= bestDistanceSq || !GeometryUtility.TestPlanesAABB(FrustumPlanes, bounds))
            {
                continue;
            }

            bestDistanceSq = distanceSq;
            floorY = bounds.max.y;
        }

        if (bestDistanceSq == float.MaxValue)
        {
            _lockedSecondPlaneY = float.NaN;
            return false;
        }

        // 같은 방 타일끼리 1~3 cm 다른 높이 때문에 평면이 매번 움찔거리지 않게, 허용 범위 안이면 그대로 둔다.
        if (float.IsNaN(_lockedSecondPlaneY) || Mathf.Abs(floorY - _lockedSecondPlaneY) > _floorHeightTolerance)
        {
            _lockedSecondPlaneY = floorY;
        }

        floorY = _lockedSecondPlaneY;
        return true;
    }

    // 반사 바닥 셰이더를 쓰는 렌더러(바닥 위에 겹친 반사막)를 모은다. 두 번째 반사를 그릴 바닥 높이를 찾는 데 쓴다.
    private void CollectReflectiveFloors()
    {
        _reflectiveFloors.Clear();
        foreach (var candidate in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            foreach (var material in candidate.sharedMaterials)
            {
                if (material != null && material.shader != null && material.shader.name == FloorReflectionShaderName)
                {
                    _reflectiveFloors.Add(candidate);
                    break;
                }
            }
        }
    }

    // 바닥 자체는 반사에 나올 필요가 없다. 반사를 그리는 동안만 숨기면 클립 평면을 바닥보다 조금 아래로 내려
    // 벽·기둥 밑동을 자르지 않을 수 있다(밑동이 잘리면 그 틈으로 바깥 하늘이 반사에 비친다).
    private void CollectHideCandidates()
    {
        _hideCandidates.Clear();
        if (_hideInReflection == null || _hideInReflection.Length == 0)
        {
            return;
        }

        foreach (var candidate in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            foreach (var material in candidate.sharedMaterials)
            {
                if (material != null && System.Array.IndexOf(_hideInReflection, material) >= 0)
                {
                    _hideCandidates.Add(candidate);
                    break;
                }
            }
        }
    }

    private void HideFloors(float planeY)
    {
        _hiddenNow.Clear();
        foreach (var candidate in _hideCandidates)
        {
            if (candidate == null || candidate.forceRenderingOff || Mathf.Abs(candidate.bounds.max.y - planeY) > 0.3f)
            {
                continue;
            }

            candidate.forceRenderingOff = true;
            _hiddenNow.Add(candidate);
        }
    }

    private void ShowFloors()
    {
        foreach (var hidden in _hiddenNow)
        {
            if (hidden != null)
            {
                hidden.forceRenderingOff = false;
            }
        }

        _hiddenNow.Clear();
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

        ReleaseTexture(ref _reflectionTexture);
        ReleaseTexture(ref _secondTexture);
        _reflectionTexture = CreateReflectionTexture(width, height, "Corridor Planar Reflection");
    }

    // 두 번째 반사 텍스처는 높이가 다른 바닥이 처음 보일 때 만든다(첫 텍스처와 같은 크기).
    private void EnsureSecondTexture()
    {
        if (_secondTexture != null && _secondTexture.width == _reflectionTexture.width && _secondTexture.height == _reflectionTexture.height)
        {
            return;
        }

        ReleaseTexture(ref _secondTexture);
        _secondTexture = CreateReflectionTexture(_reflectionTexture.width, _reflectionTexture.height, "Corridor Planar Reflection 2");
    }

    private static RenderTexture CreateReflectionTexture(int width, int height, string textureName)
    {
        var texture = new RenderTexture(width, height, 24, RenderTextureFormat.DefaultHDR)
        {
            name = textureName,
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Trilinear,
            wrapMode = TextureWrapMode.Clamp,
            // 밉맵은 바닥 셰이더가 거울 대신 흐린 광택 반사(_BlurMip)를 낼 때 쓴다.
            useMipMap = true,
            autoGenerateMips = true
        };
        texture.Create();
        return texture;
    }

    private static void ReleaseTexture(ref RenderTexture texture)
    {
        if (texture == null)
        {
            return;
        }

        texture.Release();
        DestroyResource(texture);
        texture = null;
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

        float floorY;
        if (nearest < 0)
        {
            floorY = _followFloorOf.position.y;
        }
        else
        {
            // 충돌체 윗면이 보이는 바닥면보다 낮은 경우가 있어, 얇은 바닥 메시면 그 메시 윗면을 쓴다.
            RaycastHit hit = FloorHits[nearest];
            floorY = hit.collider.TryGetComponent(out Renderer floorRenderer) && floorRenderer.bounds.size.y < 0.6f
                ? floorRenderer.bounds.max.y
                : hit.point.y;
        }

        // 같은 층 안에서 타일을 건너갈 때마다 평면이 몇 cm씩 뛰면 반사 전체가 움찔거려 깜빡여 보인다.
        // 허용 범위 안의 차이는 무시하고, 층이 바뀔 만큼 달라졌을 때만 평면을 옮긴다.
        if (float.IsNaN(_lockedPlaneY) || Mathf.Abs(floorY - _lockedPlaneY) > _floorHeightTolerance)
        {
            _lockedPlaneY = floorY;
        }

        position.y = _lockedPlaneY;
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

        ReleaseTexture(ref _reflectionTexture);
        ReleaseTexture(ref _secondTexture);
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

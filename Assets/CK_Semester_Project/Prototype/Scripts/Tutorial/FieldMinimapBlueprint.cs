using System.Collections;
using System.Collections.Generic;
using CK.SemesterProject.LevelDesign;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace CK.SemesterProject.Tutorial
{
    /// <summary>
    /// 필드 미니맵을 청사진처럼 그린다. 플레이어가 있는 층의 NavMesh를 평면도로 굽고,
    /// 현재 목표(LevelProgress.ObjectiveTarget)까지 걸어갈 경로를 노란 안내선으로 보여 준다.
    /// 색과 굵기는 연결한 재질(UI/TutorialBlueprintMinimap)에서 고친다.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    [DisallowMultipleComponent]
    public sealed class FieldMinimapBlueprint : MonoBehaviour
    {
        private const string ShaderName = "UI/TutorialBlueprintMinimap";
        private const int MaxRoutePoints = 64;
        private const float FloorCheckSeconds = 0.25f;

        private static readonly int CenterId = Shader.PropertyToID("_Center");
        private static readonly int ViewRadiusId = Shader.PropertyToID("_ViewRadius");
        private static readonly int MaskRectId = Shader.PropertyToID("_MaskRect");
        private static readonly int RangeId = Shader.PropertyToID("_Range");
        private static readonly int WallOffsetId = Shader.PropertyToID("_WallOffset");
        private static readonly int HasMaskId = Shader.PropertyToID("_HasMask");
        private static readonly int RouteCountId = Shader.PropertyToID("_RouteCount");
        private static readonly int RouteId = Shader.PropertyToID("_Route");
        private static readonly int TargetId = Shader.PropertyToID("_Target");
        private static readonly int HasTargetId = Shader.PropertyToID("_HasTarget");

        [Header("References")]
        [SerializeField, Tooltip("지도 중심이 되는 플레이어")]
        private Transform _player;
        [SerializeField, Tooltip("현재 목표와 목표 위치를 알려 주는 레벨 진행")]
        private LevelProgress _progress;
        [SerializeField, Tooltip("청사진 재질(UI/TutorialBlueprintMinimap). 실행 중에는 복사본에 값을 넣는다.")]
        private Material _material;
        [SerializeField, Tooltip("목표 위치 아이콘(선택). 목표가 지도 밖이면 원 가장자리에 붙어 방향을 알려 준다.")]
        private RectTransform _destinationIcon;

        [Header("View")]
        [SerializeField, Min(4f), Tooltip("지도 중심에서 원 가장자리까지 보이는 거리(m)")]
        private float _viewRadius = 14f;
        [SerializeField, Range(0.5f, 1f), Tooltip("목표가 지도 밖일 때 아이콘을 붙일 위치(원 반지름 대비)")]
        private float _iconEdge = 0.84f;

        [Header("Floor Plan")]
        [SerializeField, Min(1f), Tooltip("평면도 해상도(1m당 픽셀)")]
        private float _pixelsPerMeter = 5f;
        [SerializeField, Min(0.5f), Tooltip("발밑보다 이만큼 낮은 바닥까지 같은 층으로 그린다(m)")]
        private float _floorBelow = 1.5f;
        [SerializeField, Min(0.5f), Tooltip("발밑보다 이만큼 높은 바닥까지 같은 층으로 그린다(m). 천장 위 NavMesh가 보이면 줄인다.")]
        private float _floorAbove = 2f;
        [SerializeField, Min(0.5f), Tooltip("발밑 높이가 이만큼 바뀌면 평면도를 다시 굽는다(m)")]
        private float _rebakeHeight = 1f;

        [Header("Route")]
        [SerializeField, Min(0.05f), Tooltip("안내 경로를 다시 계산하는 간격(초)")]
        private float _routeInterval = 0.2f;

        private readonly NavMeshFloorPlanBaker _baker = new NavMeshFloorPlanBaker(2f);
        private readonly Vector4[] _route = new Vector4[MaxRoutePoints];
        private readonly Vector3[] _corners = new Vector3[MaxRoutePoints];
        private readonly List<Vector3> _routePoints = new List<Vector3>();
        private NavMeshPath _path;
        private RawImage _image;
        private RectTransform _rect;
        private Material _instance;
        private Coroutine _bakeRoutine;
        private Transform _routeTarget;
        private bool _isPlanStale;
        private float _nextFloorCheck;
        private float _nextRoute;
        private int _routeCount;
        private float _wallOffset;

        /// <summary>평면도를 한 번이라도 구웠는지.</summary>
        public bool HasFloorPlan => _baker.HasPlan;
        /// <summary>지금 안내선에 쓰는 점 개수(0이면 안내선 없음).</summary>
        public int RoutePointCount => _routeCount;

        private Transform CurrentTarget => _progress != null ? _progress.ObjectiveTarget : null;

        private void Awake()
        {
            _image = GetComponent<RawImage>();
            _rect = (RectTransform)transform;
            _path = new NavMeshPath();
            if (_material == null || _material.shader == null || _material.shader.name != ShaderName || _player == null)
            {
                Debug.LogError(name + ": 미니맵 재질(" + ShaderName + ") 또는 플레이어 참조가 없습니다.", this);
                enabled = false;
                return;
            }
            _instance = new Material(_material) { name = _material.name + " (Runtime)", hideFlags = HideFlags.DontSave };
            _image.material = _instance;
            _image.texture = null;
            // NavMesh 바닥은 에이전트 반지름만큼 벽에서 떨어져 있으므로 그만큼 바깥에 벽선을 긋는다.
            _wallOffset = NavMesh.GetSettingsByID(0).agentRadius;
            if (_destinationIcon != null)
            {
                _destinationIcon.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (_progress != null)
            {
                _progress.ObjectiveChanged += OnObjectiveChanged;
            }
            _nextFloorCheck = 0f;
            _nextRoute = 0f;
        }

        private void OnDisable()
        {
            if (_progress != null)
            {
                _progress.ObjectiveChanged -= OnObjectiveChanged;
            }
            if (_bakeRoutine != null)
            {
                StopCoroutine(_bakeRoutine);
                _bakeRoutine = null;
            }
        }

        private void OnDestroy()
        {
            _baker.Release();
            if (_instance != null)
            {
                Destroy(_instance);
            }
        }

        private void LateUpdate()
        {
            float now = Time.unscaledTime;
            if (now >= _nextFloorCheck)
            {
                _nextFloorCheck = now + FloorCheckSeconds;
                CheckFloor();
            }
            if (now >= _nextRoute || CurrentTarget != _routeTarget)
            {
                _nextRoute = now + _routeInterval;
                RebuildRoute();
            }
            ApplyToMaterial();
            PlaceDestinationIcon();
        }

        private void OnObjectiveChanged()
        {
            // 목표가 바뀔 즈음 문이 열려 NavMesh가 달라지므로 평면도와 경로를 새로 만든다.
            _isPlanStale = true;
            _nextRoute = 0f;
        }

        private void CheckFloor()
        {
            if (_bakeRoutine != null
                || !NavMesh.SamplePosition(_player.position, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
            {
                return;
            }
            if (_baker.HasPlan && !_isPlanStale && Mathf.Abs(hit.position.y - _baker.FloorY) <= _rebakeHeight)
            {
                return;
            }
            _isPlanStale = false;
            _bakeRoutine = StartCoroutine(BakeFloorPlan(hit.position.y));
        }

        private IEnumerator BakeFloorPlan(float floorY)
        {
            IEnumerator bake = _baker.Bake(floorY, _floorBelow, _floorAbove, _pixelsPerMeter, 2048, 3f);
            while (bake.MoveNext())
            {
                yield return bake.Current;
            }
            // 다 구운 그 프레임에 바꿔야 지운 텍스처가 한 프레임도 보이지 않는다.
            _image.texture = _baker.Texture;
            _bakeRoutine = null;
        }

        private void RebuildRoute()
        {
            Transform target = CurrentTarget;
            _routeTarget = target;
            _routePoints.Clear();
            _routeCount = 0;
            if (target == null)
            {
                return;
            }
            Vector3 from = _player.position;
            Vector3 to = target.position;
            if (NavMesh.SamplePosition(from, out NavMeshHit start, 2f, NavMesh.AllAreas)
                && NavMesh.SamplePosition(to, out NavMeshHit end, 4f, NavMesh.AllAreas)
                && NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, _path)
                && _path.status != NavMeshPathStatus.PathInvalid)
            {
                int count = _path.GetCornersNonAlloc(_corners);
                for (int i = 0; i < count; i++)
                {
                    _routePoints.Add(_corners[i]);
                }
            }
            if (_routePoints.Count == 0)
            {
                // 길을 못 찾으면(엘리베이터 안 등) 목표 방향으로 곧게 잇는다.
                _routePoints.Add(from);
            }
            // 닫힌 문 앞에서 끊긴 경로도 목표 표시까지 이어 그린다.
            Vector3 last = _routePoints[_routePoints.Count - 1];
            if (new Vector2(last.x - to.x, last.z - to.z).sqrMagnitude > 0.09f)
            {
                _routePoints.Add(to);
            }
            if (_routePoints.Count > MaxRoutePoints)
            {
                _routePoints.RemoveRange(MaxRoutePoints - 1, _routePoints.Count - MaxRoutePoints);
            }
            _routeCount = _routePoints.Count;
            float remaining = 0f;
            for (int i = _routeCount - 1; i >= 0; i--)
            {
                Vector3 point = _routePoints[i];
                if (i < _routeCount - 1)
                {
                    Vector3 next = _routePoints[i + 1];
                    remaining += new Vector2(next.x - point.x, next.z - point.z).magnitude;
                }
                _route[i] = new Vector4(point.x, point.z, remaining, 0f);
            }
        }

        private void ApplyToMaterial()
        {
            Vector3 center = _player.position;
            _instance.SetVector(CenterId, new Vector4(center.x, center.z, 0f, 0f));
            _instance.SetFloat(ViewRadiusId, _viewRadius);
            _instance.SetFloat(WallOffsetId, _wallOffset);
            _instance.SetFloat(HasMaskId, _baker.HasPlan ? 1f : 0f);
            if (_baker.HasPlan)
            {
                _instance.SetVector(MaskRectId, _baker.WorldRect);
                _instance.SetFloat(RangeId, _baker.Range);
            }
            if (_routeCount >= 2)
            {
                // 첫 점은 매 프레임 플레이어 위치로 옮겨 경로를 다시 계산하는 사이에도 선이 발밑에 붙어 있게 한다.
                Vector4 next = _route[1];
                float toNext = new Vector2(next.x - center.x, next.y - center.z).magnitude;
                _route[0] = new Vector4(center.x, center.z, next.z + toNext, 0f);
            }
            _instance.SetFloat(RouteCountId, _routeCount);
            _instance.SetVectorArray(RouteId, _route);
            Transform target = CurrentTarget;
            _instance.SetFloat(HasTargetId, target != null ? 1f : 0f);
            if (target != null)
            {
                _instance.SetVector(TargetId, new Vector4(target.position.x, target.position.z, 0f, 0f));
            }
        }

        private void PlaceDestinationIcon()
        {
            if (_destinationIcon == null)
            {
                return;
            }
            Transform target = CurrentTarget;
            bool isVisible = target != null;
            if (_destinationIcon.gameObject.activeSelf != isVisible)
            {
                _destinationIcon.gameObject.SetActive(isVisible);
            }
            if (!isVisible)
            {
                return;
            }
            Vector3 delta = target.position - _player.position;
            Vector2 offset = new Vector2(delta.x, delta.z) / _viewRadius;
            if (offset.magnitude > _iconEdge)
            {
                offset = offset.normalized * _iconEdge;
            }
            Rect rect = _rect.rect;
            _destinationIcon.localPosition = rect.center + offset * (rect.width * 0.5f);
        }
    }
}

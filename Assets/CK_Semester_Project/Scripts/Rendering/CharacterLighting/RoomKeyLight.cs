using System.Collections.Generic;
using UnityEngine;

namespace CK.SemesterProject.Rendering
{
    // 방 하나의 주광 설정. 주광은 캐릭터·몬스터를 비추는 씬의 방향광이고, RoomKeyLightDirector가 대상(플레이어)이 있는 방의 값으로 섞는다.
    // 상자 안에서는 가중치 1, 바깥 블렌드 거리 안에서는 0으로 부드럽게 줄어든다(문턱에서 두 방의 주광이 섞인다).
    // 겹친 방은 우선순위가 낮은 것부터 차례로 섞는다.
    // 방향은 회전 값으로 직접 적거나, 주광 위치(천장등 등) 오브젝트를 두고 거기서 방 가운데를 향하게 정한다.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class RoomKeyLight : MonoBehaviour
    {
        private static readonly List<RoomKeyLight> ActiveRooms = new List<RoomKeyLight>();
        private static bool _sorted;

        [SerializeField, Tooltip("이 오브젝트를 중심으로 하는 방의 로컬 크기(m)입니다.")]
        private Vector3 _size = new Vector3(10f, 6f, 10f);

        [SerializeField, Min(0f), Tooltip("방 바깥에서 이전 주광으로 넘어가는 거리(m)입니다. 문턱 폭 정도로 둡니다.")]
        private float _blendDistance = 2f;

        [SerializeField, Tooltip("겹친 방 중 값이 클수록 나중에 섞여 우선합니다.")]
        private int _priority;

        [Header("주광")]
        [SerializeField, Tooltip("이 방의 주광 방향입니다(도). 방향광의 Rotation과 같은 값입니다(X = 높이각, Y = 빛이 나아가는 수평 방향). "
            + "아래 '주광 위치'를 지정하면 쓰이지 않습니다.")]
        private Vector3 _lightEulerAngles = new Vector3(50f, 0f, 0f);

        [SerializeField, ColorUsage(false), Tooltip("이 방의 주광 색입니다.")]
        private Color _color = Color.white;

        [SerializeField, Tooltip("켜면 주광 세기도 이 방의 값으로 바꿉니다. 끄면 방향광에 적어 둔 세기를 그대로 씁니다.")]
        private bool _overrideIntensity;

        [SerializeField, Min(0f), Tooltip("이 방의 주광 세기입니다(위 항목을 켰을 때만).")]
        private float _intensity = .3f;

        [Header("주광 위치로 방향 정하기 (선택)")]
        [SerializeField, Tooltip("주광이 나오는 자리입니다(예: 복도 끝 천장등). 지정하면 이 자리에서 방 가운데에 선 캐릭터를 향하는 방향으로 방 전체를 비춥니다. "
            + "이 오브젝트를 옮기면 방향이 바뀝니다. 비워 두면 위의 방향 값을 씁니다.")]
        private Transform _lightSource;

        [SerializeField, Tooltip("켜면 방 가운데가 아니라 대상(플레이어)이 서 있는 곳을 향해 비춥니다. 어디에 서든 주광 위치를 향한 면이 밝아지지만, "
            + "플레이어가 움직일 때마다 방향광이 돌아 배경 벽의 음영과 그림자도 같이 움직입니다.")]
        private bool _followTarget;

        [SerializeField, Range(0f, 89f), Tooltip("주광 위치를 쓸 때의 최소 높이각(도)입니다. 낮거나 먼 자리에서 오는 빛이 너무 누워 그림자가 방 끝까지 늘어지는 것을 막습니다.")]
        private float _minElevation = 28f;

        [SerializeField, Range(0f, 89f), Tooltip("주광 위치를 쓸 때의 최대 높이각(도)입니다. 자리 바로 밑에서 얼굴이 전부 그늘지는 것을 막습니다.")]
        private float _maxElevation = 60f;

        public static IReadOnlyList<RoomKeyLight> Active
        {
            get
            {
                if (!_sorted)
                {
                    ActiveRooms.Sort((a, b) => a._priority.CompareTo(b._priority));
                    _sorted = true;
                }
                return ActiveRooms;
            }
        }

        public Color Color => _color;
        public bool OverrideIntensity => _overrideIntensity;
        public float Intensity => _intensity;

        // 이 방에서 방향광이 가져야 할 회전(forward = 빛이 나아가는 방향). targetWS는 빛을 받는 대상(플레이어)의 위치.
        public Quaternion GetLightRotation(Vector3 targetWS)
        {
            if (_lightSource == null)
            {
                return Quaternion.Euler(_lightEulerAngles);
            }

            // 빛이 닿는 곳: 방 가운데의 가슴 높이(고정) 또는 대상이 서 있는 곳.
            Vector3 aim = _followTarget ? targetWS : transform.TransformPoint(new Vector3(0f, 1f - _size.y * .5f, 0f));
            Vector3 travel = aim - _lightSource.position;
            var horizontal = new Vector3(travel.x, 0f, travel.z);
            float distance = horizontal.magnitude;
            if (distance < .05f)
            {
                // 자리 바로 밑에서는 수평 방향이 정해지지 않으므로 고정 회전의 수평 방향을 빌린다.
                return Quaternion.Euler(Mathf.Max(_minElevation, _maxElevation), _lightEulerAngles.y, 0f);
            }

            // 높이각 = 빛이 수평에서 아래로 기운 각도. 자리가 낮아 거의 수평이면 최소 높이각까지 세운다.
            float elevation = Mathf.Atan2(-travel.y, distance) * Mathf.Rad2Deg;
            elevation = Mathf.Clamp(elevation, _minElevation, Mathf.Max(_minElevation, _maxElevation));
            float yaw = Mathf.Atan2(horizontal.x, horizontal.z) * Mathf.Rad2Deg;
            return Quaternion.Euler(elevation, yaw, 0f);
        }

        public float GetWeight(Vector3 positionWS)
        {
            Vector3 local = transform.InverseTransformPoint(positionWS);
            Vector3 half = _size * .5f;
            var outside = new Vector3(Mathf.Max(0f, Mathf.Abs(local.x) - half.x),
                Mathf.Max(0f, Mathf.Abs(local.y) - half.y),
                Mathf.Max(0f, Mathf.Abs(local.z) - half.z));
            // 로컬 거리를 월드 거리로 맞춰 스케일이 있어도 블렌드 거리가 미터 단위가 되게 한다.
            float distance = transform.TransformVector(outside).magnitude;
            return distance <= 0f ? 1f
                : _blendDistance > 0f ? 1f - Mathf.SmoothStep(0f, 1f, distance / _blendDistance) : 0f;
        }

        private void OnEnable()
        {
            if (!ActiveRooms.Contains(this))
            {
                ActiveRooms.Add(this);
            }
            _sorted = false;
        }

        private void OnDisable()
        {
            ActiveRooms.Remove(this);
        }

        private void OnValidate()
        {
            _size = new Vector3(Mathf.Max(.01f, _size.x), Mathf.Max(.01f, _size.y), Mathf.Max(.01f, _size.z));
            _sorted = false;
        }

        private void OnDrawGizmos()
        {
            DrawBox(.25f, false);
        }

        private void OnDrawGizmosSelected()
        {
            DrawBox(.9f, true);
            // 방 가운데에 선 대상이 받는 빛의 방향을 화살표로 그린다(공 쪽이 빛이 나아가는 쪽).
            Vector3 center = transform.position;
            Vector3 travel = GetLightRotation(center) * Vector3.forward;
            Gizmos.color = new Color(_color.r, _color.g, _color.b, 1f);
            Gizmos.DrawLine(center - travel * 2f, center + travel * 2f);
            Gizmos.DrawSphere(center + travel * 2f, .12f);
            if (_lightSource != null)
            {
                Gizmos.DrawWireSphere(_lightSource.position, .25f);
                Gizmos.DrawLine(_lightSource.position, center);
            }
        }

        private void DrawBox(float alpha, bool withBlend)
        {
            Gizmos.color = new Color(.35f, .8f, 1f, alpha);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, _size);
            if (withBlend)
            {
                Gizmos.color = new Color(.35f, .8f, 1f, alpha * .4f);
                Gizmos.DrawWireCube(Vector3.zero, _size + Vector3.one * (_blendDistance * 2f));
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}

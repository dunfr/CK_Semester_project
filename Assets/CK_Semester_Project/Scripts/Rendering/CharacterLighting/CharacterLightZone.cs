using System.Collections.Generic;
using UnityEngine;

namespace CK.SemesterProject.Rendering
{
    // 캐릭터 조명 구역. 상자 안에서는 가중치 1, 바깥 블렌드 거리 안에서는 0으로 부드럽게 줄어든다.
    // 겹친 구역은 우선순위가 낮은 것부터 차례로 섞는다(URP Volume과 같은 방식).
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class CharacterLightZone : MonoBehaviour
    {
        private static readonly List<CharacterLightZone> ActiveZones = new List<CharacterLightZone>();
        private static bool _sorted;

        [SerializeField, Tooltip("켜면 위치와 무관하게 모든 캐릭터에 적용합니다(기본 조명용).")]
        private bool _isGlobal;

        [SerializeField, Tooltip("이 오브젝트를 중심으로 하는 구역의 로컬 크기(m)입니다.")]
        private Vector3 _size = new Vector3(6f, 4f, 24f);

        [SerializeField, Min(0f), Tooltip("구역 바깥에서 이전 조명으로 전환되는 거리(m)입니다.")]
        private float _blendDistance = 2f;

        [SerializeField, Tooltip("겹친 구역 중 값이 클수록 나중에 섞여 우선합니다.")]
        private int _priority;

        [SerializeField, Range(0f, 1f), Tooltip("구역 전체의 적용 비율입니다.")]
        private float _weight = 1f;

        [SerializeField, Tooltip("지정하면 아래 값 대신 프로필을 씁니다.")]
        private CharacterLightProfile _profile;

        [SerializeField]
        private CharacterLightSettings _settings = CharacterLightSettings.Default;

        public static IReadOnlyList<CharacterLightZone> Active
        {
            get
            {
                if (!_sorted)
                {
                    ActiveZones.Sort((a, b) => a._priority.CompareTo(b._priority));
                    _sorted = true;
                }
                return ActiveZones;
            }
        }

        public CharacterLightSettings Settings => _profile != null ? _profile.Settings : _settings;
        public int Priority => _priority;

        public float GetWeight(Vector3 positionWS)
        {
            if (_isGlobal)
            {
                return _weight;
            }
            Vector3 local = transform.InverseTransformPoint(positionWS);
            Vector3 half = _size * .5f;
            Vector3 outside = new Vector3(Mathf.Max(0f, Mathf.Abs(local.x) - half.x),
                Mathf.Max(0f, Mathf.Abs(local.y) - half.y),
                Mathf.Max(0f, Mathf.Abs(local.z) - half.z));
            // 로컬 거리를 월드 거리로 맞춰 스케일이 있어도 블렌드 거리가 미터 단위가 되게 한다.
            float distance = transform.TransformVector(outside).magnitude;
            float weight = distance <= 0f ? 1f
                : _blendDistance > 0f ? 1f - Mathf.SmoothStep(0f, 1f, distance / _blendDistance) : 0f;
            return weight * _weight;
        }

        private void OnEnable()
        {
            if (!ActiveZones.Contains(this))
            {
                ActiveZones.Add(this);
            }
            _sorted = false;
        }

        private void OnDisable()
        {
            ActiveZones.Remove(this);
        }

        private void OnValidate()
        {
            _size = new Vector3(Mathf.Max(.01f, _size.x), Mathf.Max(.01f, _size.y), Mathf.Max(.01f, _size.z));
            _sorted = false;
        }

        private void OnDrawGizmos()
        {
            if (_isGlobal)
            {
                return;
            }
            Gizmos.color = new Color(1f, .75f, .3f, .25f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, _size);
            Gizmos.matrix = Matrix4x4.identity;
        }

        private void OnDrawGizmosSelected()
        {
            if (_isGlobal)
            {
                return;
            }
            Gizmos.color = new Color(1f, .75f, .3f, .8f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, _size);
            Gizmos.DrawWireCube(Vector3.zero, _size + Vector3.one * (_blendDistance * 2f));
            Gizmos.matrix = Matrix4x4.identity;
            CharacterLightState state = Settings.Resolve(CharacterLightEnvironment.MainLight, Camera.main, transform.position, null);
            Gizmos.DrawLine(transform.position, transform.position + state.KeyDirection);
        }
    }
}

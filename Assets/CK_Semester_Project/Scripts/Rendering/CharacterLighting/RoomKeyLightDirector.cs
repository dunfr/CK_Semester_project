using UnityEngine;

namespace CK.SemesterProject.Rendering
{
    // 씬의 주광(캐릭터·몬스터를 비추는 방향광)에 붙인다. 대상(플레이어)이 있는 방의 RoomKeyLight 값으로
    // 방향광의 방향·색·세기를 바꾸고, 방이 바뀌면 시간을 두고 부드럽게 섞는다.
    // 어느 방에도 없으면 방향광에 원래 적어 둔 값으로 돌아간다. 방향광 하나를 바꾸므로 플레이어·몬스터·그림자가 모두 같이 따라간다.
    // 플레이 중에만 동작한다(편집 중에는 방향광을 건드리지 않는다).
    // 전투 진입·종료처럼 대상이 순간이동하는 장면 전환에서는 섞지 않고 그 프레임에 바로 바꾼다.
    // 캐릭터 쪽(CharacterLighting)이 같은 프레임에 새 주광을 읽도록 먼저 실행한다.
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Light))]
    [DisallowMultipleComponent]
    public sealed class RoomKeyLightDirector : MonoBehaviour
    {
        [SerializeField, Tooltip("어느 방에 있는지 판정할 대상입니다. 보통 플레이어입니다. 비우면 Player 태그를 찾습니다.")]
        private Transform _target;

        [SerializeField, Tooltip("판정 지점의 로컬 오프셋(m)입니다. 보통 가슴 높이입니다.")]
        private Vector3 _targetOffset = new Vector3(0f, 1f, 0f);

        [SerializeField, Min(0f), Tooltip("방이 바뀔 때 주광이 새 방의 값으로 넘어가는 시간(초)입니다. 0이면 즉시 바뀝니다.")]
        private float _blendSeconds = 1f;

        [SerializeField, Min(0f), Tooltip("대상이 한 프레임에 이 거리(m)보다 멀리 옮겨지면 순간이동(전투 진입·종료, 리스폰 등)으로 보고, 섞지 않고 즉시 바꿉니다. 0이면 끕니다.")]
        private float _snapDistance = 4f;

        private Light _light;
        private Quaternion _baseRotation;
        private Color _baseColor;
        private float _baseIntensity;
        // 빛이 나아가는 방향. 회전이 아니라 방향을 섞어야 반대편 방으로 넘어갈 때 빛이 옆으로 빙 돌지 않고 머리 위로 넘어간다.
        private Vector3 _direction;
        private Color _color;
        private float _intensity;
        private bool _hasState;
        private Vector3 _lastPosition;
        private bool _snapRequested;

        // 다음 갱신에서 섞지 않고 바로 바꾼다. 컷 전환처럼 대상이 움직이지 않는 장면 전환에서 직접 부른다.
        public void Snap()
        {
            _snapRequested = true;
        }

        private void OnEnable()
        {
            _light = GetComponent<Light>();
            _baseRotation = transform.rotation;
            _baseColor = _light.color;
            _baseIntensity = _light.intensity;
            _hasState = false;
        }

        private void OnDisable()
        {
            if (_light != null && _hasState)
            {
                transform.rotation = _baseRotation;
                _light.color = _baseColor;
                _light.intensity = _baseIntensity;
            }
            _hasState = false;
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player == null)
                {
                    return;
                }
                _target = player.transform;
            }

            // 목표 값: 기본 주광에서 시작해, 대상이 걸쳐 있는 방들을 가중치만큼 차례로 섞는다.
            Vector3 position = _target.TransformPoint(_targetOffset);
            Vector3 targetDirection = _baseRotation * Vector3.forward;
            Color targetColor = _baseColor;
            float targetIntensity = _baseIntensity;
            foreach (RoomKeyLight room in RoomKeyLight.Active)
            {
                float weight = room.GetWeight(position);
                if (weight <= 0f)
                {
                    continue;
                }
                targetDirection = BlendDirection(targetDirection, room.GetLightRotation(position) * Vector3.forward, weight);
                targetColor = Color.Lerp(targetColor, room.Color, weight);
                if (room.OverrideIntensity)
                {
                    targetIntensity = Mathf.Lerp(targetIntensity, room.Intensity, weight);
                }
            }

            bool teleported = _hasState && _snapDistance > 0f
                && (position - _lastPosition).sqrMagnitude > _snapDistance * _snapDistance;
            _lastPosition = position;
            if (!_hasState || _blendSeconds <= 0f || teleported || _snapRequested)
            {
                _direction = targetDirection;
                _color = targetColor;
                _intensity = targetIntensity;
                _hasState = true;
                _snapRequested = false;
            }
            else
            {
                // 프레임 속도와 무관한 지수 보간. 블렌드 시간 안에 차이의 95%를 따라간다.
                float t = 1f - Mathf.Exp(-3f * Time.deltaTime / _blendSeconds);
                _direction = BlendDirection(_direction, targetDirection, t);
                _color = Color.Lerp(_color, targetColor, t);
                _intensity = Mathf.Lerp(_intensity, targetIntensity, t);
            }

            // 지금 회전에서 새 방향까지만 최소로 돌린다. 위쪽 기준으로 회전을 새로 만들면 빛이 수직을 지날 때 축이 튀어 그림자가 한 번 떨린다.
            transform.rotation = Quaternion.Normalize(Quaternion.FromToRotation(transform.forward, _direction) * transform.rotation);
            _light.color = _color;
            _light.intensity = _intensity;
        }

        // 두 방향 사이를 가장 짧은 호로 섞는다. 둘 다 아래를 향하므로 그 사이는 더 가파르게 내리비추는 방향을 지난다.
        private static Vector3 BlendDirection(Vector3 from, Vector3 to, float t)
        {
            Vector3 direction = Vector3.Slerp(from, to, Mathf.Clamp01(t));
            return direction.sqrMagnitude > 1e-6f ? direction.normalized : to;
        }
    }
}

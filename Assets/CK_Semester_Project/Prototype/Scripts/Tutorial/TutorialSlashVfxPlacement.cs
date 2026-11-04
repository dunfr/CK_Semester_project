using UnityEngine;

namespace CK.SemesterProject.Tutorial
{
    [DefaultExecutionOrder(100)]
    public sealed class TutorialSlashVfxPlacement : MonoBehaviour
    {
        private static readonly System.Random _directionRandom = new System.Random();

        [SerializeField, Tooltip("베기 면의 Transform")]
        private Transform _slash;
        [SerializeField, Tooltip("적을 기준으로 발생하는 타격 파티클")]
        private Transform _hitDetails;
        [SerializeField, Tooltip("검기 방향을 따르는 HitSparksFast")]
        private Transform _fastSparks;
        [SerializeField, Tooltip("기존 벚꽃 파티클")]
        private Transform _petals;
        [SerializeField, Tooltip("가로로 번쩍이는 타격선")]
        private Transform _impactStreak;
        [SerializeField, Min(0f), Tooltip("검기 생성 후 타격선이 나타나는 시간(초)")]
        private float _impactDelay = .17f;
        [SerializeField, Tooltip("이번 공격의 방향. 이펙트 생성 시 좌우를 50% 확률로 선택합니다")]
        private bool _isLeftSlash;
        [SerializeField, Tooltip("왼쪽 베기의 플레이어→적 지면 방향 기준 회전(도)")]
        private Vector3 _leftRotation = new Vector3(-63.493f, 197.69f, -9.382f);
        [SerializeField, Tooltip("오른쪽 베기의 플레이어→적 지면 방향 기준 회전(도)")]
        private Vector3 _rightRotation = new Vector3(61.358f, 1.604f, -8.73f);
        [SerializeField, Min(.01f), Tooltip("첨부 시안의 베기 면 크기(배)")]
        private float _slashScale = 6f;
        [SerializeField, Min(0f), Tooltip("적 표면과 이펙트 사이의 최소 간격(m)")]
        private float _surfacePadding = .6f;

        private Transform _actor;
        private Transform _target;
        private Camera _camera;
        private Vector3 _centerOffset;
        private Vector3 _targetExtents;
        private Quaternion _fastSparksLocalRotation;
        private float _startedAt;
        private bool _hasTriggeredImpact;
        private float _playbackSpeed = 1f;

        public void SetPlaybackTiming(float delay, float speed)
        {
            _startedAt = Time.time + Mathf.Max(0f, delay);
            _playbackSpeed = Mathf.Max(.05f, speed);
        }

        public void SamplePreview(float elapsedSeconds)
        {
            if (_impactStreak != null)
            {
                _hasTriggeredImpact = elapsedSeconds >= _impactDelay;
                _impactStreak.gameObject.SetActive(_hasTriggeredImpact);
            }
            UpdatePlacement(elapsedSeconds);
        }

        public float GetPreviewParticleTime(ParticleSystem particle, float elapsedSeconds, float speed)
        {
            return Mathf.Max(0f, GetPreviewEffectTime(particle.transform, elapsedSeconds, speed));
        }

        public float GetPreviewEffectTime(Transform effect, float elapsedSeconds, float speed)
        {
            return _impactStreak != null && effect.IsChildOf(_impactStreak)
                ? elapsedSeconds - _impactDelay / Mathf.Max(.05f, speed) : elapsedSeconds;
        }

        public void Initialize(Transform actor, Transform target, Camera camera)
        {
            _isLeftSlash = _directionRandom.Next(2) == 0;
            _actor = actor;
            _target = target;
            _camera = camera;
            _startedAt = Time.time;
            _hasTriggeredImpact = false;
            if (_impactStreak != null)
            {
                _impactStreak.gameObject.SetActive(false);
            }
            if (_fastSparks != null)
            {
                _fastSparksLocalRotation = _fastSparks.localRotation;
            }
            Bounds bounds = new Bounds(target.position + Vector3.up, new Vector3(1f, 2f, 1f));
            bool hasBounds = false;
            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>())
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
                {
                    continue;
                }
                if (renderer.GetComponent<TMPro.TMP_Text>() != null || renderer.GetComponent<TextMesh>() != null)
                {
                    continue;
                }
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            _centerOffset = bounds.center - target.position;
            _targetExtents = bounds.extents;
            LateUpdate();
        }

        private void LateUpdate()
        {
            UpdatePlacement((Time.time - _startedAt) * _playbackSpeed);
        }

        private void UpdatePlacement(float elapsedSeconds)
        {
            if (_target == null || _camera == null || _slash == null)
            {
                return;
            }
            Vector3 center = _target.position + _centerOffset;
            Vector3 towardCamera = (_camera.transform.position - center).normalized;
            Vector3 towardActor = _actor != null
                ? Vector3.ProjectOnPlane(_actor.position - center, Vector3.up).normalized
                : Vector3.ProjectOnPlane(towardCamera, Vector3.up).normalized;
            // 첨부된 회전은 지면 기준이다. 카메라 피치까지 더하면 검기가 납작하게 눕는다.
            transform.SetPositionAndRotation(center, Quaternion.LookRotation(-towardActor, Vector3.up));
            _slash.localRotation = Quaternion.Euler(_isLeftSlash ? _leftRotation : _rightRotation);
            _slash.localScale = Vector3.one * _slashScale;
            float targetDepth = Vector3.Dot(_targetExtents,
                new Vector3(Mathf.Abs(towardCamera.x), Mathf.Abs(towardCamera.y), Mathf.Abs(towardCamera.z)));
            float actorDepth = Vector3.Dot(_targetExtents,
                new Vector3(Mathf.Abs(towardActor.x), Mathf.Abs(towardActor.y), Mathf.Abs(towardActor.z)));
            Vector3 hitPosition = center + towardActor * (actorDepth + _surfacePadding);
            // 적 앞쪽 간격을 유지하면서, 옆에서 보는 카메라에도 타격 중심이 몸 안에 묻히지 않게 한다.
            float cameraClearance = targetDepth + .05f - Vector3.Dot(hitPosition - center, towardCamera);
            hitPosition += towardCamera * Mathf.Max(0f, cameraClearance);
            _slash.position = hitPosition;
            if (_hitDetails != null)
            {
                _hitDetails.SetPositionAndRotation(hitPosition, _target.rotation);
            }
            if (_fastSparks != null)
            {
                _fastSparks.rotation = _slash.rotation * _fastSparksLocalRotation;
            }
            if (_petals != null)
            {
                _petals.SetPositionAndRotation(hitPosition, _slash.rotation);
                _petals.localScale = Vector3.one * _slashScale;
            }
            if (_impactStreak != null)
            {
                _impactStreak.SetPositionAndRotation(hitPosition,
                    Quaternion.LookRotation(-towardCamera, Vector3.up) * Quaternion.Euler(0f, 0f, -5f));
                if (!_hasTriggeredImpact && elapsedSeconds >= _impactDelay)
                {
                    _hasTriggeredImpact = true;
                    _impactStreak.gameObject.SetActive(true);
                }
            }
        }
    }
}

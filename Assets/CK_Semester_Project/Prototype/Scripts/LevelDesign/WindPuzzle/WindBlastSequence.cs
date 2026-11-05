using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace CK.SemesterProject.LevelDesign.WindPuzzle
{
    /// <summary>
    /// 바람길 퍼즐 완료 연출. 바람이 환풍구 길을 따라 흘러가 선반 위 상자를 밀어 떨어뜨리고,
    /// 떨어진 상자가 막힌 길(잔해)을 부숴 통로를 연다.
    /// </summary>
    public sealed class WindBlastSequence : MonoBehaviour
    {
        [SerializeField] private LevelProgress _progress;

        [Header("Wind")]
        [SerializeField, Tooltip("바람이 흘러갈 경로(퍼즐 장치 → 상자). 마지막 지점이 상자를 미는 위치입니다.")]
        private Transform[] _windPath;
        [SerializeField, Tooltip("경로를 따라 움직이며 바람 줄기를 남기는 파티클")]
        private ParticleSystem _windGust;
        [SerializeField, Min(0.1f)] private float _windTravelSeconds = 2.2f;

        [Header("Crate")]
        [SerializeField, Tooltip("선반 위에서 떨어질 상자")]
        private Transform _crate;
        [SerializeField, Tooltip("상자가 부딪힐 지점(잔해 윗면)")]
        private Transform _impactPoint;
        [SerializeField, Tooltip("부순 뒤 상자가 멈출 위치·방향")]
        private Transform _crateRestPoint;
        [SerializeField, Min(0.05f)] private float _pushSeconds = 0.35f;
        [SerializeField, Min(0.05f)] private float _fallSeconds = 0.5f;

        [Header("Blocked path")]
        [SerializeField, Tooltip("부서져 흩어질 잔해 조각(Rigidbody, 평소 Kinematic)")]
        private Rigidbody[] _debris;
        [SerializeField, Tooltip("길을 막고 있던 충돌체(부서지면 끕니다)")]
        private Collider[] _blockers;
        [SerializeField] private NavMeshObstacle _obstacle;
        [SerializeField] private ParticleSystem _impactDust;
        [SerializeField, Min(0f)] private float _debrisForce = 4f;
        [SerializeField, Min(0f), Tooltip("부서진 잔해를 치우기까지의 시간(초)")]
        private float _debrisLifetime = 3f;

        [Header("Camera")]
        [SerializeField, Tooltip("연출을 비출 카메라 위치")]
        private Transform _focusView;
        [SerializeField, Tooltip("카메라가 바라볼 곳")]
        private Transform _focusTarget;

        [Header("After")]
        [SerializeField] private string _message = "(임시) 바람에 상자가 떨어져 막힌 길이 뚫렸어.";
        [SerializeField, Tooltip("길이 뚫린 뒤 실행(컷씬 등)")]
        private UnityEvent _opened = new UnityEvent();

        private bool _hasPlayed;

        public bool HasOpened { get; private set; }

        private void Awake()
        {
            foreach (Rigidbody piece in _debris)
            {
                if (piece != null)
                {
                    piece.isKinematic = true;
                }
            }
            if (_windGust != null)
            {
                _windGust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        public void Play()
        {
            if (_hasPlayed)
            {
                return;
            }
            _hasPlayed = true;
            StartCoroutine(Sequence());
        }

        private IEnumerator Sequence()
        {
            if (_progress != null)
            {
                _progress.LockControls(this);
                float total = _windTravelSeconds + _pushSeconds + _fallSeconds + 2.2f;
                _progress.FocusCamera(_focusView, _focusTarget, total);
            }
            // 1) 바람이 환풍구 길을 따라 흐른다.
            yield return MoveGust();
            // 2) 상자가 선반에서 밀려 떨어진다.
            yield return PushAndDropCrate();
            // 3) 잔해가 부서지며 길이 열린다.
            BreakBlockade();
            yield return CrateSettle();
            yield return new WaitForSeconds(1.2f);
            if (_progress != null)
            {
                _progress.UnlockControls(this);
                if (!string.IsNullOrEmpty(_message))
                {
                    _progress.ShowMessage(_message, 3f);
                }
            }
            HasOpened = true;
            _opened.Invoke();
            yield return new WaitForSeconds(_debrisLifetime);
            foreach (Rigidbody piece in _debris)
            {
                if (piece != null)
                {
                    piece.gameObject.SetActive(false);
                }
            }
        }

        private IEnumerator MoveGust()
        {
            if (_windPath == null || _windPath.Length < 2 || _windGust == null)
            {
                yield break;
            }
            float total = 0f;
            for (int i = 1; i < _windPath.Length; i++)
            {
                total += Vector3.Distance(_windPath[i - 1].position, _windPath[i].position);
            }
            Transform gust = _windGust.transform;
            gust.position = _windPath[0].position;
            _windGust.Play(true);
            for (float elapsed = 0f; elapsed < _windTravelSeconds; elapsed += Time.deltaTime)
            {
                float target = Mathf.SmoothStep(0f, 1f, elapsed / _windTravelSeconds) * total;
                gust.position = PointAlongPath(target, out Vector3 direction);
                if (direction.sqrMagnitude > 0.0001f)
                {
                    gust.rotation = Quaternion.LookRotation(direction);
                }
                yield return null;
            }
            gust.position = _windPath[_windPath.Length - 1].position;
            _windGust.Emit(60);
            _windGust.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private Vector3 PointAlongPath(float distance, out Vector3 direction)
        {
            for (int i = 1; i < _windPath.Length; i++)
            {
                Vector3 a = _windPath[i - 1].position, b = _windPath[i].position;
                float length = Vector3.Distance(a, b);
                direction = b - a;
                if (distance <= length || i == _windPath.Length - 1)
                {
                    return Vector3.Lerp(a, b, length > 0f ? Mathf.Clamp01(distance / length) : 1f);
                }
                distance -= length;
            }
            direction = Vector3.forward;
            return _windPath[_windPath.Length - 1].position;
        }

        private IEnumerator PushAndDropCrate()
        {
            if (_crate == null || _impactPoint == null)
            {
                yield break;
            }
            Vector3 start = _crate.position;
            Quaternion startRotation = _crate.rotation;
            Vector3 push = _windPath != null && _windPath.Length >= 2
                ? Vector3.ProjectOnPlane(_windPath[_windPath.Length - 1].position - _windPath[_windPath.Length - 2].position, Vector3.up).normalized
                : (_impactPoint.position - start).normalized;
            // 선반 끝까지 밀리며 앞쪽으로 기운다.
            Vector3 edge = start + push * 0.6f;
            Vector3 tipAxis = Vector3.Cross(Vector3.up, push);
            for (float elapsed = 0f; elapsed < _pushSeconds; elapsed += Time.deltaTime)
            {
                float t = elapsed / _pushSeconds;
                _crate.position = Vector3.Lerp(start, edge, t * t);
                _crate.rotation = Quaternion.AngleAxis(t * 20f, tipAxis) * startRotation;
                yield return null;
            }
            // 포물선을 그리며 잔해 위로 떨어진다.
            Vector3 from = _crate.position;
            Quaternion fromRotation = _crate.rotation;
            for (float elapsed = 0f; elapsed < _fallSeconds; elapsed += Time.deltaTime)
            {
                float t = elapsed / _fallSeconds;
                Vector3 position = Vector3.Lerp(from, _impactPoint.position, t);
                position.y = Mathf.Lerp(from.y, _impactPoint.position.y, t * t) + Mathf.Sin(t * Mathf.PI) * 0.25f;
                _crate.position = position;
                _crate.rotation = Quaternion.AngleAxis(20f + t * 70f, tipAxis) * startRotation;
                yield return null;
            }
            _crate.position = _impactPoint.position;
        }

        private void BreakBlockade()
        {
            foreach (Collider blocker in _blockers)
            {
                if (blocker != null)
                {
                    blocker.enabled = false;
                }
            }
            if (_obstacle != null)
            {
                _obstacle.enabled = false;
            }
            if (_impactDust != null)
            {
                _impactDust.transform.position = _impactPoint != null ? _impactPoint.position : _impactDust.transform.position;
                _impactDust.Play(true);
            }
            Vector3 origin = _impactPoint != null ? _impactPoint.position + Vector3.up * 0.4f : transform.position;
            foreach (Rigidbody piece in _debris)
            {
                if (piece == null)
                {
                    continue;
                }
                piece.isKinematic = false;
                piece.AddExplosionForce(_debrisForce, origin, 4f, 0.4f, ForceMode.VelocityChange);
                piece.AddTorque(Random.insideUnitSphere * _debrisForce, ForceMode.VelocityChange);
            }
        }

        private IEnumerator CrateSettle()
        {
            if (_crate == null || _crateRestPoint == null)
            {
                yield break;
            }
            Vector3 from = _crate.position;
            Quaternion fromRotation = _crate.rotation;
            const float seconds = 0.45f;
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
            {
                float t = elapsed / seconds;
                Vector3 position = Vector3.Lerp(from, _crateRestPoint.position, t);
                position.y += Mathf.Sin(t * Mathf.PI) * 0.35f;
                _crate.SetPositionAndRotation(position, Quaternion.Slerp(fromRotation, _crateRestPoint.rotation, t));
                yield return null;
            }
            _crate.SetPositionAndRotation(_crateRestPoint.position, _crateRestPoint.rotation);
        }
    }
}

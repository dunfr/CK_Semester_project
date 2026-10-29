using System.Collections;
using UnityEngine;

namespace CK.SemesterProject.LevelDesign
{
    /// <summary>소리 범위를 퍼지는 원으로 표시하는 시각 효과(3-5-V02). 실제 오디오는 재생하지 않는다.</summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class NoisePulse : MonoBehaviour
    {
        [SerializeField, Min(0.1f), Tooltip("원이 최대 반경까지 퍼지는 시간(초)")]
        private float _seconds = 1f;
        [SerializeField, Range(12, 128), Tooltip("원의 분할 수")]
        private int _segments = 48;
        [SerializeField] private Color _color = new Color(1f, 0.85f, 0.2f, 1f);

        private LineRenderer _line;
        private Coroutine _routine;

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.loop = true;
            _line.positionCount = _segments;
            _line.enabled = false;
        }

        public void Play(Vector3 center, float radius)
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
            }
            _routine = StartCoroutine(PlayRoutine(center, radius));
        }

        private IEnumerator PlayRoutine(Vector3 center, float radius)
        {
            _line.enabled = true;
            for (float elapsed = 0f; elapsed < _seconds; elapsed += Time.deltaTime)
            {
                float progress = elapsed / _seconds;
                SetCircle(center, radius * progress);
                Color color = _color;
                color.a = 1f - progress;
                _line.startColor = color;
                _line.endColor = color;
                yield return null;
            }
            _line.enabled = false;
            _routine = null;
        }

        private void SetCircle(Vector3 center, float radius)
        {
            for (int i = 0; i < _segments; i++)
            {
                float angle = i * Mathf.PI * 2f / _segments;
                _line.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * radius, 0.05f, Mathf.Sin(angle) * radius));
            }
        }
    }
}

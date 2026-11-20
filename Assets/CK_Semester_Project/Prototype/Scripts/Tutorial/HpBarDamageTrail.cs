using UnityEngine;
using UnityEngine.UI;

namespace CK.SemesterProject.Tutorial
{
    /// <summary>
    /// HP 바가 줄면 줄어든 만큼 밝은 잔상 바를 잠깐 남겼다가 따라 내려보낸다.
    /// "맞은 만큼 깎여 나갔다"는 느낌을 준다. Slider의 Fill 바로 뒤에 잔상 이미지를 만들어 쓴다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Slider))]
    public sealed class HpBarDamageTrail : MonoBehaviour
    {
        [SerializeField, Tooltip("깎여 나간 부분에 남는 잔상 색")]
        private Color _trailColor = new Color(1f, 0.85f, 0.35f, 0.98f);
        [SerializeField, Min(0f), Tooltip("잔상이 그대로 머무는 시간(초)")]
        private float _holdSeconds = 0.4f;
        [SerializeField, Min(0.01f), Tooltip("잔상이 실제 HP까지 내려가는 시간(초)")]
        private float _drainSeconds = 0.5f;
        [SerializeField, Min(0f), Tooltip("깎인 순간 잔상이 하얗게 번쩍이는 시간(초)")]
        private float _flashSeconds = 0.14f;

        private const float Epsilon = 0.0005f;
        private const string TrailName = "Damage Trail";

        private Slider _slider;
        private RectTransform _fill;
        private Image _fillImage;
        private RectTransform _trail;
        private Image _trailImage;
        private float _trailValue = -1f;
        private float _lastValue = -1f;
        private float _drainFrom;
        private float _hitAt = float.NegativeInfinity;

        /// <summary>데미지 잔상 바가 아직 머물거나 줄어들고 있는 중이면 true.</summary>
        public bool IsAnimating
        {
            get
            {
                if (_slider == null)
                {
                    return false;
                }
                float current = _slider.normalizedValue;
                if (_trailValue > current + Epsilon)
                {
                    return true;
                }
                // 이번 프레임에 막 깎여서 아직 LateUpdate 전인 경우도 애니메이션 시작으로 본다.
                return _lastValue >= 0f && current < _lastValue - Epsilon;
            }
        }

        /// <summary>슬라이더에 잔상 연출을 붙인다(이미 있으면 그대로 쓴다).</summary>
        public static HpBarDamageTrail Attach(Slider slider)
        {
            if (slider == null)
            {
                return null;
            }
            HpBarDamageTrail trail = slider.GetComponent<HpBarDamageTrail>();
            return trail != null ? trail : slider.gameObject.AddComponent<HpBarDamageTrail>();
        }

        private void Awake()
        {
            _slider = GetComponent<Slider>();
            _fill = _slider.fillRect;
            if (_fill == null || _fill.parent == null)
            {
                Debug.LogWarning("HpBarDamageTrail: Fill Rect가 없는 슬라이더입니다. " + name, this);
                enabled = false;
                return;
            }
            _fillImage = _fill.GetComponent<Image>();
            CreateTrail();
        }

        private void OnDisable()
        {
            // 다시 켜질 때 지난 값으로 잔상이 남지 않게 한다.
            _trailValue = -1f;
            _lastValue = -1f;
            if (_trailImage != null)
            {
                _trailImage.enabled = false;
            }
        }

        private void LateUpdate()
        {
            float value = _slider.normalizedValue;
            float now = Time.unscaledTime;
            if (_trailValue < 0f)
            {
                _trailValue = value;
                _lastValue = value;
            }
            if (value < _lastValue - Epsilon)
            {
                // 새로 깎였다. 잔상은 깎이기 전 위치(또는 아직 남은 더 높은 위치)에서 다시 머문다.
                _trailValue = Mathf.Max(_trailValue, _lastValue);
                _drainFrom = _trailValue;
                _hitAt = now;
            }
            if (value >= _trailValue)
            {
                _trailValue = value;
            }
            else
            {
                float drain = Mathf.Clamp01((now - _hitAt - _holdSeconds) / _drainSeconds);
                float eased = drain * drain * (3f - 2f * drain);
                _trailValue = Mathf.Lerp(_drainFrom, value, eased);
            }
            _lastValue = value;
            ApplyTrail(value, now);
        }

        private void CreateTrail()
        {
            Transform existing = _fill.parent.Find(TrailName);
            GameObject trailObject = existing != null ? existing.gameObject
                : new GameObject(TrailName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            trailObject.layer = _fill.gameObject.layer;
            _trail = (RectTransform)trailObject.transform;
            _trail.SetParent(_fill.parent, false);
            // Fill 바로 뒤에 그려 실제 HP 바 뒤로 잔상이 비치게 한다.
            _trail.SetSiblingIndex(_fill.GetSiblingIndex());
            _trail.pivot = _fill.pivot;
            _trailImage = trailObject.GetComponent<Image>();
            _trailImage.raycastTarget = false;
            if (_fillImage != null)
            {
                _trailImage.sprite = _fillImage.sprite;
                _trailImage.type = _fillImage.type;
                _trailImage.fillMethod = _fillImage.fillMethod;
                _trailImage.fillOrigin = _fillImage.fillOrigin;
                _trailImage.pixelsPerUnitMultiplier = _fillImage.pixelsPerUnitMultiplier;
                _trailImage.preserveAspect = _fillImage.preserveAspect;
            }
            _trailImage.enabled = false;
        }

        private void ApplyTrail(float value, float now)
        {
            bool isVisible = _trailValue > value + Epsilon;
            _trailImage.enabled = isVisible;
            if (!isVisible)
            {
                return;
            }
            float flash = _flashSeconds > 0f ? 1f - Mathf.Clamp01((now - _hitAt) / _flashSeconds) : 0f;
            _trailImage.color = Color.Lerp(_trailColor, Color.white, flash);
            _trail.offsetMin = _fill.offsetMin;
            _trail.offsetMax = _fill.offsetMax;
            if (_fillImage != null && _fillImage.type == Image.Type.Filled)
            {
                _trail.anchorMin = _fill.anchorMin;
                _trail.anchorMax = _fill.anchorMax;
                _trailImage.fillAmount = _trailValue;
                return;
            }
            // Slider는 방향에 따라 Fill의 한쪽 앵커를 값으로 옮긴다. 잔상도 같은 방식으로 늘린다.
            Vector2 min = _fill.anchorMin;
            Vector2 max = _fill.anchorMax;
            switch (_slider.direction)
            {
                case Slider.Direction.LeftToRight:
                    max.x = _trailValue;
                    break;
                case Slider.Direction.RightToLeft:
                    min.x = 1f - _trailValue;
                    break;
                case Slider.Direction.BottomToTop:
                    max.y = _trailValue;
                    break;
                default:
                    min.y = 1f - _trailValue;
                    break;
            }
            _trail.anchorMin = min;
            _trail.anchorMax = max;
        }
    }
}
